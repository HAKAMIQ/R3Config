using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed record Rpcs3ProcessState(
    bool IsRunning,
    int? ProcessId = null);

public interface IRpcs3ProcessProbe
{
    Rpcs3ProcessState Find(
        string executablePath,
        int? expectedProcessId = null);
}

public interface IHCanDiscoveryStore
{
    HCanDiscoverySession? LoadActive();

    void SaveActive(HCanDiscoverySession session);

    void ClearActive();

    HCanHashBinding? LoadBinding(
        string rpcs3ExecutablePath,
        string serial,
        string? gameVersion,
        string gameInstallPath);

    void SaveBinding(
        HCanHashBinding binding,
        bool replaceExisting = false);

    bool DeleteBinding(
        string rpcs3ExecutablePath,
        string serial,
        string? gameVersion,
        string gameInstallPath);
}

public sealed class HCanBindingEvidenceService(
    IHCanDiscoveryStore store)
{
    public Rpcs3GameDescriptor Enrich(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(game.InstPath))
        {
            return game;
        }

        var binding = store.LoadBinding(
            install.ExePath,
            game.Serial,
            game.Version,
            game.InstPath);
        if (binding is null)
        {
            return game;
        }

        var ppu = Normalize(binding.DetectedPpuHashes);
        var spu = Normalize(binding.DetectedSpuHashes);
        if (ppu.Length == 0 && spu.Length == 0)
        {
            return game;
        }

        return game with
        {
            PpuHash = ppu.FirstOrDefault(),
            SpuHash = spu.FirstOrDefault(),
            PpuRaw = Normalize(game.Hashes.Concat(ppu)),
            SpuRaw = Normalize(game.SpuList.Concat(spu)),
            HashSrc = binding.LogPath,
            HashMsg = null,
            HashEvi = HashEvi.PlainLog,
            HashTime = binding.DetectedUtc,
            HCanPpu = ppu,
            HCanSpu = spu
        };
    }

    private static string[] Normalize(
        IEnumerable<string> values)
    {
        return values
            .Select(value =>
                ExeHash.TryNorm(value, out var normalized)
                    ? normalized
                    : null)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class HashEvidenceUseCase(
    HCanHashDiscoveryService discovery,
    IHCanDiscoveryStore store,
    IRpcs3ProcessProbe processProbe)
{
    public HCanDiscoverySession Begin(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        return Begin(
            install,
            game,
            CanaryHashExpectation.Empty,
            cancellationToken);
    }

    public HCanDiscoverySession Begin(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        return Begin(
            install,
            game,
            expectation,
            replaceExisting: false,
            cancellationToken: cancellationToken);
    }

    public HCanDiscoverySession Begin(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(expectation);
        cancellationToken.ThrowIfCancellationRequested();

        _ = expectation;

        var saved = LoadBindingRead(install, game);
        if (HasPpu(saved) && !replaceExisting)
        {
            throw new InvalidOperationException(
                "A Canary PPU fingerprint is already saved for this RPCS3 installation and game.");
        }

        var logSources = discovery.GetLogPaths(install)
            .Select(logPath => new HCanLogSource(
                logPath,
                discovery.Capture(
                    logPath,
                    cancellationToken)))
            .ToArray();
        var primaryLog = logSources[0];
        var session = new HCanDiscoverySession(
            Path.GetFullPath(install.ExePath),
            game.Serial.Trim().ToUpperInvariant(),
            PatVer.Norm(game.Version),
            Path.GetFullPath(game.InstPath),
            ProcessId: 0,
            StartedUtc: DateTime.UtcNow,
            LogPath: primaryLog.LogPath,
            Baseline: primaryLog.Baseline,
            LogSources: logSources);

        cancellationToken.ThrowIfCancellationRequested();
        store.SaveActive(session);
        return session;
    }

    public void AttachProcess(
        HCanDiscoverySession session,
        int processId,
        DateTime? processStartedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        store.SaveActive(session with
        {
            ProcessId = Math.Max(0, processId),
            StartedUtc = processStartedUtc ?? session.StartedUtc
        });
    }

    public void Cancel(HCanDiscoverySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var active = store.LoadActive();
        if (active is not null && SameSession(active, session))
        {
            store.ClearActive();
        }
    }

    public bool IsSelectedGameBooted(
        HCanDiscoverySession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        return discovery.IsSelectedGameBooted(
            session,
            cancellationToken);
    }

    public HashReadCode CheckRunning(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        var active = store.LoadActive();
        var state = processProbe.Find(
            Path.GetFullPath(install.ExePath),
            active?.ProcessId > 0
                ? active.ProcessId
                : null);

        if (!state.IsRunning)
        {
            return HashReadCode.NoMatch;
        }

        return RunningCode(
            active,
            install,
            game,
            cancellationToken);
    }

    public HashRead Read(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        var saved = LoadBindingRead(install, game);
        if (HasPpu(saved))
        {
            return saved;
        }

        var active = store.LoadActive();
        var state = processProbe.Find(
            Path.GetFullPath(install.ExePath),
            active?.ProcessId > 0
                ? active.ProcessId
                : null);

        if (state.IsRunning)
        {
            return new HashRead(
                IsOk: true,
                SrcPath: active?.LogPath ?? discovery.GetLogPath(install),
                Hashes: [],
                Code: RunningCode(
                    active,
                    install,
                    game,
                    cancellationToken));
        }

        if (active is null || !IsCurrent(active, install, game))
        {
            return new HashRead(
                IsOk: true,
                SrcPath: discovery.GetLogPath(install),
                Hashes: [],
                Code: HashReadCode.LogNotChanged);
        }

        var result = discovery.Read(
            active,
            cancellationToken);
        if (result.Code != HashReadCode.Found)
        {
            return result;
        }

        return SaveOnce(
            active,
            result,
            replaceExisting: false,
            cancellationToken: cancellationToken);
    }

    public HashRead Complete(HCanDiscoverySession session)
    {
        return Complete(
            session,
            CanaryHashExpectation.Empty,
            CancellationToken.None);
    }

    public HashRead Complete(
        HCanDiscoverySession session,
        CanaryHashExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        return Complete(
            session,
            expectation,
            replaceExisting: false,
            cancellationToken: cancellationToken);
    }

    public HashRead Complete(
        HCanDiscoverySession session,
        CanaryHashExpectation expectation,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(expectation);
        cancellationToken.ThrowIfCancellationRequested();
        _ = expectation;

        var existing = store.LoadBinding(
            session.Rpcs3ExecutablePath,
            session.Serial,
            session.GameVersion,
            session.GameInstallPath);
        if (existing is not null && !replaceExisting)
        {
            var saved = discovery.FromBinding(existing);
            if (HasPpu(saved))
            {
                store.ClearActive();
                return saved;
            }
        }

        var active = store.LoadActive();
        if (active is null || !SameSession(active, session))
        {
            return new HashRead(
                IsOk: true,
                SrcPath: session.LogPath,
                Hashes: [],
                Code: HashReadCode.LogNotChanged);
        }

        var result = discovery.Read(
            active,
            cancellationToken);
        if (result.Code != HashReadCode.Found)
        {
            return result;
        }

        return SaveOnce(
            active,
            result,
            replaceExisting,
            cancellationToken);
    }

    private HashRead SaveOnce(
        HCanDiscoverySession session,
        HashRead result,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        if (!HasPpu(result))
        {
            return result;
        }

        var existing = store.LoadBinding(
            session.Rpcs3ExecutablePath,
            session.Serial,
            session.GameVersion,
            session.GameInstallPath);
        if (existing is not null && !replaceExisting)
        {
            var saved = discovery.FromBinding(existing);
            if (saved.Hashes.Any(item => item.Kind == HashKind.Ppu))
            {
                store.ClearActive();
                return saved;
            }
        }

        var binding = discovery.ToBinding(
            session,
            result,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        store.SaveBinding(
            binding,
            replaceExisting);
        store.ClearActive();

        return result with
        {
            IsSaved = true
        };
    }

    public HashRead LoadSaved(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);

        return LoadBindingRead(install, game);
    }

    private HashRead LoadBindingRead(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        var binding = store.LoadBinding(
            install.ExePath,
            game.Serial,
            game.Version,
            game.InstPath);

        return binding is null
            ? new HashRead(
                IsOk: true,
                SrcPath: discovery.GetLogPath(install),
                Hashes: [],
                Code: HashReadCode.NoMatch)
            : discovery.FromBinding(binding);
    }

    private static bool HasPpu(HashRead read)
    {
        return read.Code == HashReadCode.Found &&
               read.Hashes.Any(item => item.Kind == HashKind.Ppu);
    }

    public bool DeleteSaved(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);

        return store.DeleteBinding(
            install.ExePath,
            game.Serial,
            game.Version,
            game.InstPath);
    }

    private HashReadCode RunningCode(
        HCanDiscoverySession? active,
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken)
    {
        if (active is null ||
            !IsCurrent(active, install, game))
        {
            return HashReadCode.Rpcs3Running;
        }

        return discovery.IsSelectedGameBooted(
                active,
                cancellationToken)
            ? HashReadCode.SelectedGameRunning
            : HashReadCode.Rpcs3Running;
    }

    private static bool IsCurrent(
        HCanDiscoverySession session,
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return PathEquals(
                   session.Rpcs3ExecutablePath,
                   install.ExePath) &&
               session.Serial.Equals(
                   game.Serial.Trim(),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   PatVer.Norm(session.GameVersion),
                   PatVer.Norm(game.Version),
                   StringComparison.OrdinalIgnoreCase) &&
               PathEquals(
                   session.GameInstallPath,
                   game.InstPath);
    }

    private static bool SameSession(
        HCanDiscoverySession first,
        HCanDiscoverySession second)
    {
        return first.StartedUtc == second.StartedUtc &&
               PathEquals(
                   first.Rpcs3ExecutablePath,
                   second.Rpcs3ExecutablePath) &&
               first.Serial.Equals(
                   second.Serial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathEquals(
        string first,
        string second)
    {
        try
        {
            return Path.GetFullPath(first)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(
                    Path.GetFullPath(second)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }
}
