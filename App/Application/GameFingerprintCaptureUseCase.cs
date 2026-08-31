using System.Diagnostics;
using R3Config.Mdl;

namespace R3Config.Application;

public enum GameFingerprintCaptureStage
{
    Preparing,
    Launching,
    WaitingForGameStart,
    StabilizingStartup,
    ClosingEmulator,
    ReadingLog,
    Completed
}

public enum GameFingerprintCaptureError
{
    None,
    FingerprintAlreadyCaptured,
    Rpcs3AlreadyRunning,
    Rpcs3Missing,
    GameIdentityMissing,
    LaunchFailed,
    ProcessExitedEarly,
    TimedOut,
    StopFailed,
    FingerprintNotFound,
    CatalogHashMismatch,
    Cancelled,
    Unexpected
}

public sealed record GameFingerprintCaptureProgress(
    GameFingerprintCaptureStage Stage,
    int PpuCount = 0,
    int SpuCount = 0,
    string? Detail = null);

public sealed record GameFingerprintCaptureResult(
    bool Succeeded,
    GameFingerprintCaptureError Error,
    HashRead? Evidence = null,
    string? Detail = null)
{
    public static GameFingerprintCaptureResult Success(HashRead evidence)
    {
        return new GameFingerprintCaptureResult(
            true,
            GameFingerprintCaptureError.None,
            evidence);
    }

    public static GameFingerprintCaptureResult Fail(
        GameFingerprintCaptureError error,
        string? detail = null,
        HashRead? evidence = null)
    {
        return new GameFingerprintCaptureResult(
            false,
            error,
            evidence,
            detail);
    }
}

public sealed record GameFingerprintCaptureOptions(
    TimeSpan Timeout,
    TimeSpan PollInterval,
    TimeSpan StartupStabilityDelay,
    TimeSpan GracefulCloseTimeout,
    TimeSpan PostExitLogDelay)
{
    public static GameFingerprintCaptureOptions Default { get; } = new(
        Timeout: TimeSpan.FromMinutes(5),
        PollInterval: TimeSpan.FromSeconds(1),
        StartupStabilityDelay: TimeSpan.FromSeconds(20),
        GracefulCloseTimeout: TimeSpan.FromSeconds(8),
        PostExitLogDelay: TimeSpan.FromMilliseconds(750));
}

public sealed record Rpcs3OwnedProcessStopResult(
    bool Stopped,
    bool Forced = false,
    string? Error = null);

public interface IRpcs3OwnedProcessController
{
    bool TryAttach(
        string executablePath,
        int processId,
        out DateTime processStartedUtc);

    bool IsRunning(
        string executablePath,
        int processId,
        DateTime processStartedUtc);

    bool IsGameStartupReady(
        string executablePath,
        int processId,
        DateTime processStartedUtc);

    Task<Rpcs3OwnedProcessStopResult> StopAsync(
        string executablePath,
        int processId,
        DateTime launchedUtc,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// Owns one immutable Canary fingerprint acquisition for one RPCS3 install
/// and one selected game. The RPCS3 log is parsed exactly once and only after
/// the owned emulator process has exited.
/// </summary>
public sealed class GameFingerprintCaptureUseCase
{
    private readonly HashEvidenceUseCase _hashEvidence;
    private readonly GameLaunchUseCase _gameLaunch;
    private readonly IRpcs3OwnedProcessController _processController;
    private readonly GameFingerprintCaptureOptions _options;

    public GameFingerprintCaptureUseCase(
        HashEvidenceUseCase hashEvidence,
        GameLaunchUseCase gameLaunch,
        IRpcs3OwnedProcessController processController,
        GameFingerprintCaptureOptions? options = null)
    {
        _hashEvidence = hashEvidence ??
            throw new ArgumentNullException(nameof(hashEvidence));
        _gameLaunch = gameLaunch ??
            throw new ArgumentNullException(nameof(gameLaunch));
        _processController = processController ??
            throw new ArgumentNullException(nameof(processController));
        _options = options ?? GameFingerprintCaptureOptions.Default;

        if (_options.Timeout <= TimeSpan.Zero ||
            _options.PollInterval <= TimeSpan.Zero ||
            _options.StartupStabilityDelay < TimeSpan.Zero ||
            _options.GracefulCloseTimeout <= TimeSpan.Zero ||
            _options.PostExitLogDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Fingerprint capture timing values are invalid.");
        }
    }

    public Task<GameFingerprintCaptureResult> CaptureAsync(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        IProgress<GameFingerprintCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return CaptureAsync(
            install,
            game,
            CanaryHashExpectation.Empty,
            progress,
            cancellationToken);
    }

    public async Task<GameFingerprintCaptureResult> CaptureAsync(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation,
        IProgress<GameFingerprintCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(expectation);

        HCanDiscoverySession? session = null;
        var ownsProcess = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, GameFingerprintCaptureStage.Preparing);

            var saved = await Task.Run(
                    () => _hashEvidence.LoadSaved(install, game),
                    cancellationToken)
                .ConfigureAwait(false);
            if (HasPpu(saved))
            {
                return GameFingerprintCaptureResult.Fail(
                    GameFingerprintCaptureError.FingerprintAlreadyCaptured,
                    evidence: saved);
            }

            var running = await Task.Run(
                    () => _hashEvidence.CheckRunning(
                        install,
                        game,
                        cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (running !=
                HashReadCode.NoMatch)
            {
                return GameFingerprintCaptureResult.Fail(
                    GameFingerprintCaptureError.Rpcs3AlreadyRunning);
            }

            session = await Task.Run(
                    () => _hashEvidence.Begin(
                        install,
                        game,
                        expectation,
                        cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);

            Report(progress, GameFingerprintCaptureStage.Launching);
            var launch = _gameLaunch.Launch(install, game);
            if (!launch.Started || launch.ProcessId <= 0)
            {
                _hashEvidence.Cancel(session);
                return FromLaunchFailure(launch);
            }

            if (!_processController.TryAttach(
                    session.Rpcs3ExecutablePath,
                    launch.ProcessId,
                    out var processStartedUtc))
            {
                _hashEvidence.Cancel(session);
                return GameFingerprintCaptureResult.Fail(
                    GameFingerprintCaptureError.LaunchFailed,
                    "The RPCS3 process identity could not be attached.");
            }

            session = session with
            {
                ProcessId = launch.ProcessId,
                StartedUtc = processStartedUtc
            };
            _hashEvidence.AttachProcess(
                session,
                launch.ProcessId,
                processStartedUtc);
            ownsProcess = true;

            var totalTimer = Stopwatch.StartNew();
            Stopwatch? startupStableTimer = null;
            Report(progress, GameFingerprintCaptureStage.WaitingForGameStart);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_processController.IsRunning(
                        session.Rpcs3ExecutablePath,
                        session.ProcessId,
                        session.StartedUtc))
                {
                    ownsProcess = false;
                    _hashEvidence.Cancel(session);
                    return GameFingerprintCaptureResult.Fail(
                        GameFingerprintCaptureError.ProcessExitedEarly);
                }

                if (totalTimer.Elapsed >= _options.Timeout)
                {
                    var timedOutStop = await StopOwnedProcessAsync(
                        session,
                        CancellationToken.None);
                    ownsProcess = !timedOutStop.Stopped;
                    _hashEvidence.Cancel(session);

                    return GameFingerprintCaptureResult.Fail(
                        timedOutStop.Stopped
                            ? GameFingerprintCaptureError.TimedOut
                            : GameFingerprintCaptureError.StopFailed,
                        timedOutStop.Error);
                }

                var startupReady =
                    _processController.IsGameStartupReady(
                        session.Rpcs3ExecutablePath,
                        session.ProcessId,
                        session.StartedUtc);

                if (!startupReady)
                {
                    startupStableTimer = null;
                    Report(
                        progress,
                        GameFingerprintCaptureStage.WaitingForGameStart);
                }
                else
                {
                    startupStableTimer ??= Stopwatch.StartNew();
                    Report(
                        progress,
                        GameFingerprintCaptureStage.StabilizingStartup);

                    if (startupStableTimer.Elapsed >=
                        _options.StartupStabilityDelay)
                    {
                        break;
                    }
                }

                await Task.Delay(
                    _options.PollInterval,
                    cancellationToken);
            }

            Report(progress, GameFingerprintCaptureStage.ClosingEmulator);
            var stop = await StopOwnedProcessAsync(
                session,
                CancellationToken.None);
            ownsProcess = !stop.Stopped;
            if (!stop.Stopped)
            {
                _hashEvidence.Cancel(session);
                return GameFingerprintCaptureResult.Fail(
                    GameFingerprintCaptureError.StopFailed,
                    stop.Error);
            }

            if (_options.PostExitLogDelay > TimeSpan.Zero)
            {
                await Task.Delay(
                    _options.PostExitLogDelay,
                    cancellationToken);
            }

            var final = await CompleteAsync(
                session,
                expectation,
                progress,
                cancellationToken);
            if (!HasPpu(final))
            {
                return FailAndCancel(
                    session,
                    GameFingerprintCaptureError.FingerprintNotFound,
                    final.Error,
                    final);
            }

            var (finalPpu, finalSpu) = Counts(final);
            Report(
                progress,
                GameFingerprintCaptureStage.Completed,
                finalPpu,
                finalSpu);
            return GameFingerprintCaptureResult.Success(final);
        }
        catch (OperationCanceledException)
        {
            if (session is not null)
            {
                if (ownsProcess)
                {
                    await StopOwnedProcessAsync(
                        session,
                        CancellationToken.None);
                }

                _hashEvidence.Cancel(session);
            }

            return GameFingerprintCaptureResult.Fail(
                GameFingerprintCaptureError.Cancelled);
        }
        catch (Exception ex)
        {
            if (session is not null)
            {
                if (ownsProcess)
                {
                    await StopOwnedProcessAsync(
                        session,
                        CancellationToken.None);
                }

                _hashEvidence.Cancel(session);
            }

            return GameFingerprintCaptureResult.Fail(
                GameFingerprintCaptureError.Unexpected,
                ex.Message);
        }
    }

    private Task<HashRead> CompleteAsync(
        HCanDiscoverySession session,
        CanaryHashExpectation expectation,
        IProgress<GameFingerprintCaptureProgress>? progress,
        CancellationToken cancellationToken)
    {
        Report(progress, GameFingerprintCaptureStage.ReadingLog);
        return Task.Run(
            () => _hashEvidence.Complete(
                session,
                expectation,
                cancellationToken),
            cancellationToken);
    }

    private Task<Rpcs3OwnedProcessStopResult> StopOwnedProcessAsync(
        HCanDiscoverySession session,
        CancellationToken cancellationToken)
    {
        return _processController.StopAsync(
            session.Rpcs3ExecutablePath,
            session.ProcessId,
            session.StartedUtc,
            _options.GracefulCloseTimeout,
            cancellationToken);
    }

    private GameFingerprintCaptureResult FailAndCancel(
        HCanDiscoverySession session,
        GameFingerprintCaptureError error,
        string? detail,
        HashRead evidence)
    {
        _hashEvidence.Cancel(session);
        return GameFingerprintCaptureResult.Fail(
            error,
            detail,
            evidence);
    }

    private static GameFingerprintCaptureResult FromLaunchFailure(
        GameLaunchResult launch)
    {
        var error = launch.Error switch
        {
            GameLaunchError.Rpcs3Missing =>
                GameFingerprintCaptureError.Rpcs3Missing,
            GameLaunchError.GameIdentityMissing =>
                GameFingerprintCaptureError.GameIdentityMissing,
            _ => GameFingerprintCaptureError.LaunchFailed
        };

        return GameFingerprintCaptureResult.Fail(
            error,
            launch.Detail);
    }

    private static bool HasPpu(HashRead read)
    {
        return read.Code == HashReadCode.Found &&
               read.Hashes.Any(item => item.Kind == HashKind.Ppu);
    }

    private static (int Ppu, int Spu) Counts(HashRead read)
    {
        return (
            read.Hashes.Count(item => item.Kind == HashKind.Ppu),
            read.Hashes.Count(item => item.Kind == HashKind.Spu));
    }

    private static void Report(
        IProgress<GameFingerprintCaptureProgress>? progress,
        GameFingerprintCaptureStage stage,
        int ppuCount = 0,
        int spuCount = 0,
        string? detail = null)
    {
        progress?.Report(new GameFingerprintCaptureProgress(
            stage,
            ppuCount,
            spuCount,
            detail));
    }
}
