using System.IO;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private bool _isFingerprintCaptureRunning;
    private GameFingerprintCaptureStage? _fingerprintCaptureStage;
    private GameFingerprintCaptureError _fingerprintCaptureError;
    private string? _fingerprintCaptureDetail;
    private int _fingerprintPpuCount;
    private int _fingerprintSpuCount;

    public bool IsFingerprintCaptureRunning
    {
        get => _isFingerprintCaptureRunning;
        private set
        {
            if (!Set(ref _isFingerprintCaptureRunning, value))
            {
                return;
            }

            OnChange(nameof(CanMut));
            OnChange(nameof(CanSelectGame));
            OnChange(nameof(ShowFingerprintCaptureStatus));
            OnPatchPresentationChanged();
            FingerprintCaptureCmd.RaiseCan();
            RaiseCmd();
        }
    }

    public bool HasCanaryDefinition =>
        CanaryExpectation().HasTargets;

    public bool HasCanaryFingerprint =>
        GameList.SelGame is { } selected &&
        selected.Game.HCanPpuList.Count > 0;

    public bool HasUnmatchedCanaryFingerprint =>
        GameList.SelGame is { } selected &&
        HasCanaryDefinition &&
        HasCanaryFingerprint &&
        !MatchesExpectation(
            selected.Game,
            CanaryExpectation());

    public bool NeedsCanaryFingerprint =>
        GameList.SelGame is { InR3: true } &&
        HasCanaryDefinition &&
        !HasCanaryFingerprint;

    public bool ShowFingerprintCaptureAction =>
        NeedsCanaryFingerprint;

    public bool ShowFingerprintCaptureStatus =>
        HasCanaryDefinition ||
        IsFingerprintCaptureRunning ||
        _fingerprintCaptureStage is not null ||
        _fingerprintCaptureError != GameFingerprintCaptureError.None;

    public string FingerprintCaptureStatus
    {
        get
        {
            if (_fingerprintCaptureError !=
                GameFingerprintCaptureError.None)
            {
                return CaptureErrorText();
            }

            return _fingerprintCaptureStage switch
            {
                GameFingerprintCaptureStage.Preparing =>
                    UiText.GetText("PatchReadiness.Capture.Preparing"),
                GameFingerprintCaptureStage.Launching =>
                    UiText.GetText("PatchReadiness.Capture.Launching"),
                GameFingerprintCaptureStage.WaitingForGameStart =>
                    UiText.GetText(
                        "PatchReadiness.Capture.WaitingForGameStart"),
                GameFingerprintCaptureStage.StabilizingStartup =>
                    UiText.GetText("PatchReadiness.Capture.Stabilizing"),
                GameFingerprintCaptureStage.ClosingEmulator =>
                    UiText.GetText("PatchReadiness.Capture.Closing"),
                GameFingerprintCaptureStage.ReadingLog =>
                    UiText.GetText("PatchReadiness.Capture.Reading"),
                GameFingerprintCaptureStage.Completed =>
                    UiText.Fmt(
                        "PatchReadiness.Capture.Completed",
                        _fingerprintPpuCount,
                        _fingerprintSpuCount),
                _ => HasCanaryFingerprint
                    ? UiText.GetText("PatchReadiness.Capture.Saved")
                    : UiText.GetText("PatchReadiness.Capture.Hint")
            };
        }
    }

    private bool CanCaptureFingerprint()
    {
        return !_disposed &&
               _install is not null &&
               GameList.SelGame is not null &&
               File.Exists(_install.ExePath) &&
               NeedsCanaryFingerprint &&
               !IsBusy &&
               !RunUi &&
               !IsFingerprintCaptureRunning;
    }

    private async Task CaptureFingerprintAsync()
    {
        var install = _install;
        var selectedGame = GameList.SelGame;
        if (install is null || selectedGame is null)
        {
            return;
        }

        var expectation = CanaryExpectation();
        if (!expectation.HasTargets)
        {
            return;
        }

        const bool replaceExisting = false;

        var game = selectedGame.Game;
        var operation = BeginFingerprintCapture();
        var captureSucceeded = false;
        IsFingerprintCaptureRunning = true;
        SetFingerprintCaptureProgress(new GameFingerprintCaptureProgress(
            GameFingerprintCaptureStage.Preparing));

        try
        {
            var progress = new Progress<GameFingerprintCaptureProgress>(
                value =>
                {
                    if (IsCurrentFingerprintCapture(
                            operation,
                            install,
                            game))
                    {
                        SetFingerprintCaptureProgress(value);
                    }
                });
            var result = await _flow.CaptureFingerprintAsync(
                install,
                game,
                expectation,
                replaceExisting,
                progress,
                operation.Token);
            if (!IsCurrentFingerprintCapture(
                    operation,
                    install,
                    game))
            {
                return;
            }

            SetRunUi(
                result.Error ==
                GameFingerprintCaptureError.Rpcs3AlreadyRunning);

            if (result.Succeeded && result.Evidence is not null)
            {
                var ppu = result.Evidence.Hashes.Count(
                    item => item.Kind == HashKind.Ppu);
                var spu = result.Evidence.Hashes.Count(
                    item => item.Kind == HashKind.Spu);
                SetFingerprintCaptureProgress(
                    new GameFingerprintCaptureProgress(
                        GameFingerprintCaptureStage.Completed,
                        ppu,
                        spu));
                Log(UiText.Fmt(
                    "PatchReadiness.Capture.Completed",
                    ppu,
                    spu));
                captureSucceeded = true;

                await DoAsync();
                return;
            }

            _fingerprintCaptureStage = null;
            _fingerprintCaptureError = result.Error;
            _fingerprintCaptureDetail = result.Detail;
            OnFingerprintCaptureStateChanged();
            Log(FingerprintCaptureStatus);
        }
        finally
        {
            var wasCancelled =
                operation.Token.IsCancellationRequested;
            if (EndFingerprintCapture(operation))
            {
                IsFingerprintCaptureRunning = false;
                if (wasCancelled &&
                    (!captureSucceeded ||
                     !IsSameFingerprintContext(install, game)))
                {
                    ResetFingerprintCaptureState();
                }
            }
        }
    }

    private FingerprintOperation BeginFingerprintCapture()
    {
        FingerprintOperation operation;
        lock (_operationSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _fingerprintCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _fingerprintCancellation = cancellation;
            operation = new FingerprintOperation(
                ++_fingerprintVersion,
                cancellation,
                cancellation.Token);
        }

        return operation;
    }

    private void CancelFingerprintCapture()
    {
        lock (_operationSync)
        {
            _fingerprintCancellation?.Cancel();
        }
    }

    private bool IsCurrentFingerprintCapture(
        FingerprintOperation operation,
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        lock (_operationSync)
        {
            return !_disposed &&
                   !operation.Token.IsCancellationRequested &&
                   operation.Version == _fingerprintVersion &&
                   ReferenceEquals(
                       _fingerprintCancellation,
                       operation.Cancellation) &&
                   ReferenceEquals(_install, install) &&
                   ReferenceEquals(
                       GameList.SelGame?.Game,
                       game);
        }
    }

    private bool IsSameFingerprintContext(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        var currentGame = GameList.SelGame?.Game;
        return !_disposed &&
               currentGame is not null &&
               string.Equals(
                   ExePath,
                   install.ExePath,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   currentGame.Serial,
                   game.Serial,
                   StringComparison.OrdinalIgnoreCase) &&
               PatVer.Equals(currentGame.Version, game.Version) &&
               string.Equals(
                   currentGame.InstPath,
                   game.InstPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private bool EndFingerprintCapture(
        FingerprintOperation operation)
    {
        var isCurrent = false;
        lock (_operationSync)
        {
            if (operation.Version == _fingerprintVersion &&
                ReferenceEquals(
                    _fingerprintCancellation,
                    operation.Cancellation))
            {
                _fingerprintCancellation = null;
                isCurrent = true;
            }
        }

        operation.Cancellation.Dispose();
        return isCurrent;
    }

    private sealed record FingerprintOperation(
        long Version,
        CancellationTokenSource Cancellation,
        CancellationToken Token);

    private void SetFingerprintCaptureProgress(
        GameFingerprintCaptureProgress progress)
    {
        _fingerprintCaptureStage = progress.Stage;
        _fingerprintCaptureError = GameFingerprintCaptureError.None;
        _fingerprintCaptureDetail = progress.Detail;
        _fingerprintPpuCount = progress.PpuCount;
        _fingerprintSpuCount = progress.SpuCount;

        var processExpected = progress.Stage is
            GameFingerprintCaptureStage.WaitingForGameStart or
            GameFingerprintCaptureStage.StabilizingStartup or
            GameFingerprintCaptureStage.ClosingEmulator;
        var processClosed = progress.Stage is
            GameFingerprintCaptureStage.ReadingLog or
            GameFingerprintCaptureStage.Completed;

        if (processExpected && !RunUi)
        {
            SetRunUi(isRunning: true);
        }
        else if (processClosed && RunUi)
        {
            SetRunUi(isRunning: false);
        }

        OnFingerprintCaptureStateChanged();
    }

    private void ResetFingerprintCaptureState()
    {
        if (IsFingerprintCaptureRunning)
        {
            return;
        }

        _fingerprintCaptureStage = null;
        _fingerprintCaptureError = GameFingerprintCaptureError.None;
        _fingerprintCaptureDetail = null;
        _fingerprintPpuCount = 0;
        _fingerprintSpuCount = 0;
        OnFingerprintCaptureStateChanged();
    }

    private void OnFingerprintCaptureStateChanged()
    {
        OnChange(nameof(HasCanaryDefinition));
        OnChange(nameof(HasCanaryFingerprint));
        OnChange(nameof(HasUnmatchedCanaryFingerprint));
        OnChange(nameof(NeedsCanaryFingerprint));
        OnChange(nameof(ShowFingerprintCaptureAction));
        OnChange(nameof(ShowFingerprintCaptureStatus));
        OnChange(nameof(FingerprintCaptureStatus));
        OnPatchPresentationChanged();
        FingerprintCaptureCmd.RaiseCan();
    }

    private string CaptureErrorText()
    {
        return _fingerprintCaptureError switch
        {
            GameFingerprintCaptureError.FingerprintAlreadyCaptured =>
                UiText.GetText(
                    "PatchReadiness.Capture.AlreadyCaptured"),
            GameFingerprintCaptureError.Rpcs3AlreadyRunning =>
                UiText.GetText(
                    "PatchReadiness.Capture.AlreadyRunning"),
            GameFingerprintCaptureError.Rpcs3Missing =>
                UiText.GetText(
                    "PatchReadiness.Capture.Rpcs3Missing"),
            GameFingerprintCaptureError.GameIdentityMissing =>
                UiText.Fmt(
                    "PatchReadiness.Capture.GameIdentityMissing",
                    _fingerprintCaptureDetail ?? string.Empty),
            GameFingerprintCaptureError.LaunchFailed =>
                UiText.Fmt(
                    "PatchReadiness.Capture.LaunchFailed",
                    _fingerprintCaptureDetail ?? string.Empty),
            GameFingerprintCaptureError.ProcessExitedEarly =>
                UiText.GetText(
                    "PatchReadiness.Capture.ExitedEarly"),
            GameFingerprintCaptureError.TimedOut =>
                UiText.GetText(
                    "PatchReadiness.Capture.TimedOut"),
            GameFingerprintCaptureError.StopFailed =>
                UiText.Fmt(
                    "PatchReadiness.Capture.StopFailed",
                    _fingerprintCaptureDetail ?? string.Empty),
            GameFingerprintCaptureError.FingerprintNotFound =>
                UiText.GetText(
                    "PatchReadiness.Capture.NotFound"),
            GameFingerprintCaptureError.CatalogHashMismatch =>
                UiText.GetText(
                    "PatchReadiness.Capture.CatalogMismatch"),
            GameFingerprintCaptureError.Cancelled =>
                UiText.GetText(
                    "PatchReadiness.Capture.Cancelled"),
            _ => UiText.Fmt(
                "PatchReadiness.Capture.Unexpected",
                _fingerprintCaptureDetail ?? string.Empty)
        };
    }
}
