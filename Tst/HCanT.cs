using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

using AppFlow = global::R3Config.AppFlow;

internal static class HCanTst
{
    private static readonly string[] Rpcs3AuthoringOperations =
    [
        "alloc",
        "calloc",
        "jump",
        "jumpl",
        "jumpf",
        "load",
        "byte",
        "le16",
        "le32",
        "le64",
        "bef32",
        "bef64",
        "be16",
        "be32",
        "bd32",
        "be64",
        "bd64",
        "lef32",
        "lef64",
        "bpex",
        "utf8",
        "cutf8",
        "move_file",
        "hide_file"
    ];

    public static void Run()
    {
        Hash_Ext();
        LogLocator_All();
        Discovery_All();
        DiscoveryStore_Hardening();
        Launch_All();
        Capture_All().GetAwaiter().GetResult();
        Vld_All();
        Yaml_All();
        Imp_Edit();
        Txn_All();
        Vm_All();
        Vm_InstructionCommands();
        Ui_Lang();
        Console.WriteLine("H-Canary authoring, import, validation, transaction, ViewModel, and localization tests passed.");
    }

    private static void Hash_Ext()
    {
        const string ppu = "8645cfa235c9980ff74b5e06388b29e97fdbbed9";
        const string spu = "1234567890abcdef1234567890abcdef12345678";
        var log = $"noise\nPPU executable hash: PPU-{ppu}\nSPU executable hash: SPU-{spu}\n" +
                  $"PPU executable hash: PPU-{ppu}\nPPU executable hash: PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n";
        var service = new HashExt();
        var result = service.Parse(log, "RPCS3.log");
        Assert(result.IsOk, "Hash parsing should succeed.");
        Assert(result.Hashes.Count == 3, "Hash parsing should keep multiple values and remove duplicates.");
        Assert(result.Hashes[0].Kind == HashKind.Ppu && result.Hashes[0].Value == ppu, "First PPU Hash should preserve useful log order.");
        Assert(result.Hashes[1].Kind == HashKind.Spu && result.Hashes[1].Value == spu, "SPU Hash should be classified separately.");
        Assert(result.Hashes[0].SrcPath == "RPCS3.log" && result.Hashes[0].Line == 2, "Hash source and line should be exposed.");
        Assert(service.Parse("invalid log").Hashes.Count == 0, "A log without executable Hashes should return an empty result.");
        Assert(service.Parse("PPU executable hash: PPU-nothex").Hashes.Count == 0, "Malformed Hash text must be ignored.");
        Assert(service.Parse($"PPU executable hash: SPU-{spu}").Hashes.Count == 0, "A mismatched marker and executable prefix must be ignored.");

        var temp = NewTemp();
        try
        {
            var logPath = Path.Combine(temp, "RPCS3.log");
            File.WriteAllText(logPath, log);
            Assert(service.Read(logPath).Hashes.Count == 3, "RPCS3.log file reading should extract all Hashes.");
            Assert(!service.Read(Path.Combine(temp, "missing.log")).IsOk, "Missing RPCS3.log should be reported.");

            using (var oversized = new FileStream(
                       logPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                oversized.SetLength(256L * 1024 * 1024 + 1);
            }

            var oversizedResult = service.Read(logPath);
            Assert(
                !oversizedResult.IsOk &&
                oversizedResult.Code == HashReadCode.Error &&
                oversizedResult.Error?.Contains(
                    "input limit",
                    StringComparison.OrdinalIgnoreCase) == true,
                "Oversized RPCS3.log input should be rejected before whole-file allocation.");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static void LogLocator_All()
    {
        const string ppuA =
            "1111111111111111111111111111111111111111";
        const string ppuB =
            "2222222222222222222222222222222222222222";

        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "rpcs3");
            var externalData = Path.Combine(temp, "external-data");
            var modernLog = Path.Combine(
                root,
                "log",
                "RPCS3.log");
            var legacyLog = Path.Combine(
                root,
                "RPCS3.log");
            var gamePath = Path.Combine(
                temp,
                "games",
                "BLUS30109");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(externalData);
            Directory.CreateDirectory(gamePath);
            File.WriteAllBytes(
                Path.Combine(root, "rpcs3.exe"),
                []);

            var install = new Rpcs3InstallDescriptor(
                root,
                Path.Combine(root, "rpcs3.exe"),
                Path.Combine(externalData, "config"),
                Path.Combine(externalData, "config", "custom_configs"),
                Path.Combine(externalData, "patches"),
                Path.Combine(externalData, "config", "patch_config.yml"),
                externalData);
            var game = new Rpcs3GameDescriptor(
                "BLUS30109",
                "METAL GEAR SOLID 4",
                gamePath,
                "02.00",
                null,
                null);

            static string LogSession(
                string serial,
                string? version,
                string ppu)
            {
                return $"SYS: Serial: {serial}{Environment.NewLine}" +
                       $"SYS: App version: {version}{Environment.NewLine}" +
                       $"ppu_loader: PPU executable hash: PPU-{ppu}{Environment.NewLine}" +
                       $"GUI: Boot successful.{Environment.NewLine}";
            }

            var paths = Rpcs3LogLocator
                .PlainLogPaths(install)
                .ToArray();
            Assert(
                paths.SequenceEqual(
                    new[] { modernLog, legacyLog },
                    StringComparer.OrdinalIgnoreCase),
                "RPCS3 log location must be derived from the emulator root and cover modern log/RPCS3.log plus legacy root/RPCS3.log in deterministic order.");

            var discovery = new HCanHashDiscoveryService();
            Assert(
                discovery.GetLogPaths(install).SequenceEqual(
                    paths,
                    StringComparer.OrdinalIgnoreCase),
                "H-Canary discovery must use the centralized RPCS3 log locator instead of rebuilding a DataRoot-specific path.");

            var store = new HCanDiscoveryStore(
                Path.Combine(temp, "dual-log-state.json"));
            var useCase = new HashEvidenceUseCase(
                discovery,
                store,
                new FakeProcessProbe());
            var useCaseSession = useCase.Begin(
                install,
                game);
            Assert(
                useCaseSession.LogSources is { Count: 2 } &&
                useCaseSession.LogSources.Select(source =>
                    source.LogPath).SequenceEqual(
                        paths,
                        StringComparer.OrdinalIgnoreCase),
                "HashEvidenceUseCase must baseline both emulator-root log locations even when RPCS3 DataRoot points elsewhere.");
            useCase.Cancel(useCaseSession);

            var legacyUseCaseSession = useCase.Begin(
                install,
                game);
            File.WriteAllText(
                legacyLog,
                LogSession(
                    game.Serial,
                    game.Version,
                    ppuA));
            var savedLegacy = useCase.Read(
                install,
                game);
            var savedLegacyBinding = store.LoadBinding(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath);
            Assert(
                savedLegacy.Code == HashReadCode.Found &&
                savedLegacy.IsSaved &&
                savedLegacy.SrcPath.Equals(
                    legacyLog,
                    StringComparison.OrdinalIgnoreCase) &&
                savedLegacyBinding is not null &&
                savedLegacyBinding.LogPath.Equals(
                    legacyLog,
                    StringComparison.OrdinalIgnoreCase),
                "HashEvidenceUseCase must persist the actual legacy root/RPCS3.log source when that candidate produced the fresh selected-game fingerprint.");
            Assert(
                useCase.DeleteSaved(install, game),
                "Dual-log regression setup must be able to remove its saved legacy binding.");
            useCase.Cancel(legacyUseCaseSession);
            File.Delete(legacyLog);

            HCanDiscoverySession BeginSession()
            {
                var sources = discovery
                    .GetLogPaths(install)
                    .Select(path => new HCanLogSource(
                        path,
                        discovery.Capture(path)))
                    .ToArray();
                return new HCanDiscoverySession(
                    install.ExePath,
                    game.Serial,
                    game.Version,
                    game.InstPath,
                    0,
                    DateTime.UtcNow,
                    sources[0].LogPath,
                    sources[0].Baseline,
                    sources);
            }

            var legacySession = BeginSession();
            File.WriteAllText(
                legacyLog,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}");
            Assert(
                !discovery.IsSelectedGameBooted(legacySession),
                "Selected-game discovery must not start the one-minute timer from the serial alone before the selected executable has produced a fresh PPU hash.");
            File.AppendAllText(
                legacyLog,
                $"ppu_loader: PPU executable hash: PPU-{ppuA}{Environment.NewLine}");
            Assert(
                discovery.IsSelectedGameBooted(legacySession),
                "Selected-game boot detection must inspect the legacy root/RPCS3.log candidate and require a fresh selected-game PPU executable hash before the one-minute timer can start.");
            var legacyRead = discovery.Read(legacySession);
            Assert(
                legacyRead.Code == HashReadCode.Found &&
                legacyRead.SrcPath.Equals(
                    legacyLog,
                    StringComparison.OrdinalIgnoreCase) &&
                legacyRead.Hashes.Any(hash =>
                    hash.Kind == HashKind.Ppu &&
                    hash.Value == ppuA),
                "H-Canary discovery must read a fresh selected-game PPU hash from legacy root/RPCS3.log.");

            File.Delete(legacyLog);
            File.WriteAllText(
                legacyLog,
                LogSession(
                    game.Serial,
                    game.Version,
                    ppuA));
            var modernSession = BeginSession();
            Directory.CreateDirectory(
                Path.GetDirectoryName(modernLog)!);
            File.WriteAllText(
                modernLog,
                LogSession(
                    game.Serial,
                    game.Version,
                    ppuB));
            var modernRead = discovery.Read(modernSession);
            Assert(
                modernRead.Code == HashReadCode.Found &&
                modernRead.SrcPath.Equals(
                    modernLog,
                    StringComparison.OrdinalIgnoreCase) &&
                modernRead.Hashes.Any(hash =>
                    hash.Kind == HashKind.Ppu &&
                    hash.Value == ppuB),
                "When legacy evidence is unchanged and modern log/RPCS3.log changes after the baseline, H-Canary must select the modern session evidence.");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static void Discovery_All()
    {
        const string ppuA = "1111111111111111111111111111111111111111";
        const string ppuB = "2222222222222222222222222222222222222222";
        const string ppuC = "3333333333333333333333333333333333333333";
        const string spuA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string spuB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "rpcs3");
            var logDir = Path.Combine(root, "log");
            var logPath = Path.Combine(logDir, "RPCS3.log");
            var gamePath = Path.Combine(temp, "games", "BLUS30109");
            var otherPath = Path.Combine(temp, "games", "BLUS30109-alt");
            Directory.CreateDirectory(logDir);
            Directory.CreateDirectory(gamePath);
            Directory.CreateDirectory(otherPath);
            Directory.CreateDirectory(Path.Combine(root, "config"));
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            File.WriteAllBytes(Path.Combine(root, "rpcs3.exe"), []);

            var install = new Rpcs3InstallDescriptor(
                root,
                Path.Combine(root, "rpcs3.exe"),
                Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"),
                Path.Combine(root, "patches"),
                Path.Combine(root, "config", "patch_config.yml"),
                root);
            var game = new Rpcs3GameDescriptor(
                "BLUS30109",
                "METAL GEAR SOLID 4",
                gamePath,
                "02.00",
                null,
                null);
            var otherGame = game with
            {
                Serial = "BLUS99999",
                Title = "Other Game"
            };

            var storePath = Path.Combine(temp, "hdis.json");
            var store = new HCanDiscoveryStore(storePath);
            var process = new FakeProcessProbe();
            var discovery = new HCanHashDiscoveryService();
            var useCase = new HashEvidenceUseCase(
                discovery,
                store,
                process);

            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA));
            var staleRunningSession = useCase.Begin(install, game);
            Assert(
                staleRunningSession.LogSources is { Count: 2 } &&
                staleRunningSession.LogSources.Select(source =>
                    source.LogPath).SequenceEqual(
                        Rpcs3LogLocator.PlainLogPaths(install),
                        StringComparer.OrdinalIgnoreCase),
                "HashEvidenceUseCase must baseline both centralized RPCS3 log candidates before launching the game.");
            useCase.AttachProcess(staleRunningSession, 4141);
            process.Running = true;
            Assert(
                useCase.Read(install, game).Code == HashReadCode.Rpcs3Running,
                "A stale pre-launch log must not make an open RPCS3 process look like the selected game is running.");
            process.Running = false;
            useCase.Cancel(staleRunningSession);

            File.Delete(logPath);
            var runningSession = useCase.Begin(install, game);
            useCase.AttachProcess(runningSession, 4242);
            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA));
            process.Running = true;
            Assert(
                useCase.Read(install, game).Code == HashReadCode.SelectedGameRunning,
                "Discovery must refuse to read while the selected game session is still active in RPCS3.");
            Assert(
                useCase.CheckRunning(install, otherGame) == HashReadCode.Rpcs3Running,
                "Discovery must distinguish an unrelated open RPCS3 process from the selected game session.");
            File.AppendAllText(
                logPath,
                $"SYS: Stopping emulator...{Environment.NewLine}");
            Assert(
                useCase.Read(install, game).Code == HashReadCode.Rpcs3Running,
                "After the selected game stops, discovery must report that RPCS3 itself is still open.");
            process.Running = false;
            useCase.Cancel(runningSession);

            File.Delete(logPath);
            useCase.Begin(install, game);
            Assert(
                useCase.Read(install, game).Code == HashReadCode.NoLog,
                "Discovery must report a missing new RPCS3.log without consulting cache evidence.");

            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA));
            useCase.Begin(install, game);
            Assert(
                useCase.Read(install, game).Code == HashReadCode.LogNotChanged,
                "Discovery must reject an unchanged log captured before the launch session.");

            var touchSession = useCase.Begin(install, game);
            File.SetLastWriteTimeUtc(
                logPath,
                DateTime.UtcNow.AddSeconds(1));
            Assert(
                useCase.Read(install, game).Code == HashReadCode.LogNotChanged,
                "Changing only RPCS3.log metadata must not make old session content count as a new discovery log.");
            useCase.Cancel(touchSession);

            var oldTimestampSession = useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuB));
            File.SetLastWriteTimeUtc(
                logPath,
                oldTimestampSession.StartedUtc.AddMinutes(-1));
            Assert(
                useCase.Read(install, game).Code == HashReadCode.LogNotChanged,
                "A changed file whose timestamp predates the discovery session must not be accepted as that session's log.");
            useCase.Cancel(oldTimestampSession);

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(logPath, string.Empty);
            Assert(
                useCase.Read(install, game).Code == HashReadCode.EmptyLog,
                "Discovery must distinguish a new empty RPCS3.log.");

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(logPath, "RPCS3 startup without a game session.");
            Assert(
                useCase.Read(install, game).Code == HashReadCode.SelectedGameNotFound,
                "Discovery must reject a new log without the selected game's serial session.");

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                Session(otherGame.Serial, otherGame.Version, ppuA));
            Assert(
                useCase.Read(install, game).Code == HashReadCode.WrongGame,
                "Discovery must reject the latest session when it belongs to another game.");

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                Session(game.Serial, "01.00", ppuA));
            Assert(
                useCase.Read(install, game).Code == HashReadCode.WrongVersion,
                "Discovery must reject an explicit game-version mismatch without guessing compatibility.");

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                "Game booted without executable hash output." + Environment.NewLine);
            Assert(
                useCase.Read(install, game).Code == HashReadCode.NoExecutableHashes,
                "Discovery must reject a selected-game session without exact PPU/SPU executable hash lines.");

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{new string('f', 41)}{Environment.NewLine}");
            Assert(
                useCase.Read(install, game).Code == HashReadCode.NoExecutableHashes,
                "Discovery must reject a 41-character executable hash without throwing during Regex initialization.");

            File.Delete(logPath);
            var spuOnlySession = useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"spu_loader: SPU executable hash: SPU-{spuA}{Environment.NewLine}");
            var spuOnlyDiscovery = useCase.Read(install, game);
            Assert(
                spuOnlyDiscovery.Code == HashReadCode.Found &&
                !spuOnlyDiscovery.IsSaved &&
                useCase.LoadSaved(install, game).Code == HashReadCode.NoMatch,
                "An SPU-only session must never lock the one-time binding because Canary capture requires at least one PPU fingerprint.");
            useCase.Cancel(spuOnlySession);

            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{ppuA}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{ppuB}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{ppuA}{Environment.NewLine}" +
                $"spu_loader: SPU executable hash: SPU-{spuA}{Environment.NewLine}" +
                $"spu_loader: SPU executable hash: SPU-{spuB}{Environment.NewLine}" +
                $"spu_loader: SPU executable hash: SPU-{spuA}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: SPU-{spuB}{Environment.NewLine}");
            var found = useCase.Read(install, game);
            Assert(
                found.Code == HashReadCode.Found &&
                found.IsSaved &&
                found.Hashes.Select(item => item.Key).SequenceEqual(
                [
                    $"PPU-{ppuA}",
                    $"PPU-{ppuB}",
                    $"SPU-{spuA}",
                    $"SPU-{spuB}"
                ]),
                "Discovery must preserve first appearance, remove exact duplicates, and reject mismatched PPU/SPU prefixes.");

            var saved = useCase.LoadSaved(install, game);
            Assert(
                saved.Code == HashReadCode.Found &&
                saved.IsSaved &&
                saved.Hashes.Select(item => item.Key).SequenceEqual(found.Hashes.Select(item => item.Key)),
                "A successful discovery must persist only for the selected RPCS3 path, serial, version, and game path.");

            var enriched = new HCanBindingEvidenceService(store).Enrich(
                install,
                game);
            Assert(
                enriched.HCanPpu is not null &&
                enriched.HCanPpu.SequenceEqual([ppuA, ppuB]) &&
                enriched.HCanSpu is not null &&
                enriched.HCanSpu.SequenceEqual([spuA, spuB]) &&
                enriched.HashEvi == HashEvi.PlainLog &&
                enriched.HashSrc == logPath,
                "Saved discovery evidence must flow into normal patch analysis as exact per-game PPU/SPU evidence.");
            Assert(
                useCase.LoadSaved(install, game with { Serial = otherGame.Serial }).Code == HashReadCode.NoMatch &&
                useCase.LoadSaved(install, game with { Version = "01.00" }).Code == HashReadCode.NoMatch &&
                useCase.LoadSaved(install, game with { InstPath = otherPath }).Code == HashReadCode.NoMatch,
                "A saved binding must not leak into another game version or installation path.");

            File.Delete(logPath);
            var immutable = useCase.Read(install, game);
            Assert(
                immutable.Code == HashReadCode.Found &&
                immutable.IsSaved &&
                immutable.Hashes.Select(item => item.Key).SequenceEqual(
                    found.Hashes.Select(item => item.Key)),
                "Once an exact PPU binding is saved, later reads must return it directly without requiring or scanning RPCS3.log.");

            var beginBlocked = false;
            try
            {
                _ = useCase.Begin(install, game);
            }
            catch (InvalidOperationException)
            {
                beginBlocked = true;
            }

            Assert(
                beginBlocked,
                "A saved selected-game PPU binding must block creation of another discovery session before RPCS3 can launch.");

            Assert(
                useCase.DeleteSaved(install, game),
                "Only an explicit user reset may unlock one new selected-game fingerprint session.");
            File.Delete(logPath);
            useCase.Begin(install, game);
            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuC, spuB));
            var resetCapture = useCase.Read(install, game);
            Assert(
                resetCapture.Code == HashReadCode.Found &&
                resetCapture.IsSaved &&
                useCase.LoadSaved(install, game).Hashes.Select(item => item.Key).SequenceEqual(
                    [$"PPU-{ppuC}", $"SPU-{spuB}"]),
                "Only an explicit saved-binding reset may permit one new selected-game capture.");

            var canaryExpectation = new CanaryHashExpectation(
                [ppuB],
                [spuA]);

            var expectationCannotReplaceSaved = false;
            try
            {
                _ = useCase.Begin(
                    install,
                    game,
                    canaryExpectation);
            }
            catch (InvalidOperationException)
            {
                expectationCannotReplaceSaved = true;
            }

            Assert(
                expectationCannotReplaceSaved,
                "A catalog expectation must not unlock or replace a saved selected-game PPU binding.");

            Assert(
                useCase.DeleteSaved(install, game),
                "An explicit reset is required before one new selected-game fingerprint session.");

            File.Delete(logPath);
            var independentSession = useCase.Begin(
                install,
                game,
                canaryExpectation);
            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA, spuB));
            var independent = useCase.Complete(
                independentSession,
                canaryExpectation);
            Assert(
                independent.Code == HashReadCode.Found &&
                independent.IsSaved &&
                independent.Hashes.Select(item => item.Key).SequenceEqual(
                    [$"PPU-{ppuA}", $"SPU-{spuB}"]) &&
                useCase.LoadSaved(install, game).Hashes.Select(item => item.Key).SequenceEqual(
                    [$"PPU-{ppuA}", $"SPU-{spuB}"]),
                "The selected session PPU and SPU must be saved once as local authoring evidence even when they differ from a bundled Canary runtime key.");

            var immutableBeginBlocked = false;
            try
            {
                _ = useCase.Begin(
                    install,
                    game,
                    canaryExpectation);
            }
            catch (InvalidOperationException)
            {
                immutableBeginBlocked = true;
            }

            Assert(
                immutableBeginBlocked,
                "Any saved selected-game PPU must block another RPCS3 capture until the user explicitly deletes that binding.");

            Directory.CreateDirectory(Path.Combine(root, "cache", otherGame.Serial, $"ppu-{ppuA}-EBOOT.BIN"));
            File.Delete(logPath);
            var cacheSession = useCase.Begin(install, otherGame);
            var cacheOnly = useCase.Read(install, otherGame);
            Assert(
                cacheOnly.Code == HashReadCode.NoLog && cacheOnly.Hashes.Count == 0,
                "H-Canary discovery must never fall back to ppu/cache.");
            useCase.Cancel(cacheSession);

            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA, spuA));
            var oldMark = discovery.Capture(logPath);
            var cleanSession = new HCanDiscoverySession(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath,
                0,
                DateTime.UtcNow,
                logPath,
                oldMark);
            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuB, spuB));
            var newOnly = discovery.Read(cleanSession);
            Assert(
                newOnly.Hashes.Select(item => item.Key).SequenceEqual(
                [$"PPU-{ppuB}", $"SPU-{spuB}"]),
                "Discovery must read the replaced current log only and must not merge old session hashes.");

            File.WriteAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"PPU executable hash: PPU-{ppuA}{Environment.NewLine}" +
                $"SPU executable hash: SPU-{spuA}{Environment.NewLine}");
            var repeatedSerialBaseline = discovery.Capture(logPath);
            var repeatedSerialSession = new HCanDiscoverySession(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath,
                0,
                DateTime.UtcNow,
                logPath,
                repeatedSerialBaseline);
            File.AppendAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"PPU executable hash: PPU-{ppuB}{Environment.NewLine}");
            var repeatedSerial = discovery.Read(repeatedSerialSession);
            Assert(
                repeatedSerial.Hashes.Select(item => item.Key).SequenceEqual(
                    [$"PPU-{ppuB}"]),
                "A new post-baseline same-game session must not inherit PPU or SPU hashes from an older log region.");

            File.WriteAllText(
                logPath,
                Session(game.Serial, game.Version, ppuA) +
                $"SYS: Stopping emulator...{Environment.NewLine}");
            var stoppedBaseline = discovery.Capture(logPath);
            var stoppedSession = new HCanDiscoverySession(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath,
                0,
                DateTime.UtcNow,
                logPath,
                stoppedBaseline);
            File.AppendAllText(
                logPath,
                Session(game.Serial, game.Version, ppuC, spuB));
            var restartedSameGame = discovery.Read(stoppedSession);
            Assert(
                restartedSameGame.Hashes.Select(item => item.Key).SequenceEqual(
                [$"PPU-{ppuC}", $"SPU-{spuB}"]),
                "A new same-serial session after RPCS3's stop marker must replace, not merge, the earlier session hashes.");

            var compatibility = new PatchCompatibilityService();
            var target = new R3PatTgt(game.Title, game.Serial, [game.Version!]);
            var withoutDiscovery = game with
            {
                PpuHash = ppuB,
                SpuHash = spuB,
                HCanPpu = null,
                HCanSpu = null
            };
            var guardedWithoutDiscovery = compatibility.Evaluate(
                Patch("PPU without discovery", HashKind.Ppu, ppuB, target),
                [withoutDiscovery]);
            Assert(
                guardedWithoutDiscovery.Status == MatStat.Unknown &&
                guardedWithoutDiscovery.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.Hash,
                    CanOn: false,
                    UseGuard: false
                },
                "A Canary patch must remain blocked until the selected game has a saved local executable fingerprint.");

            Assert(
                compatibility.Evaluate(Patch("PPU exact", HashKind.Ppu, ppuB, target), [enriched]).Status == MatStat.Match &&
                compatibility.Evaluate(Patch("SPU exact", HashKind.Spu, spuB, target), [enriched]).Status == MatStat.Match,
                "Canary compatibility must accept an exact match against any saved PPU or SPU hash.");
            var ppuMismatch = compatibility.Evaluate(
                Patch("PPU mismatch", HashKind.Ppu, new string('f', 40), target),
                [enriched]);
            var spuMismatch = compatibility.Evaluate(
                Patch("SPU mismatch", HashKind.Spu, new string('e', 40), target),
                [enriched]);
            Assert(
                ppuMismatch.Status == MatStat.HashBad &&
                spuMismatch.Status == MatStat.HashBad &&
                ppuMismatch.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.ExeBad,
                    CanOn: false,
                    UseGuard: false
                } &&
                spuMismatch.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.ExeBad,
                    CanOn: false,
                    UseGuard: false
                },
                "A different local Canary fingerprint must block activation as contradictory executable evidence.");

            var stalePpu = new string('c', 40);
            var staleSpu = new string('d', 40);
            var staleEnriched = new HCanBindingEvidenceService(store).Enrich(
                install,
                game with
                {
                    PpuHash = stalePpu,
                    SpuHash = staleSpu
                });
            var stalePpuMatch = compatibility.Evaluate(
                Patch("Stale PPU", HashKind.Ppu, stalePpu, target),
                [staleEnriched]);
            var staleSpuMatch = compatibility.Evaluate(
                Patch("Stale SPU", HashKind.Spu, staleSpu, target),
                [staleEnriched]);
            Assert(
                stalePpuMatch.Status == MatStat.HashBad &&
                staleSpuMatch.Status == MatStat.HashBad &&
                stalePpuMatch.Cmp?.CanOn == false &&
                staleSpuMatch.Cmp?.CanOn == false,
                "Saved H-Canary evidence must remain distinct and contradictory executable evidence must block activation.");

            var spuOnlyEvidence = game with
            {
                PpuHash = null,
                SpuHash = spuA,
                PpuRaw = [],
                SpuRaw = [spuA],
                HCanPpu = [],
                HCanSpu = [spuA],
                HashEvi = HashEvi.PlainLog
            };
            Assert(
                compatibility.Evaluate(Patch("SPU-only mismatch", HashKind.Spu, new string('e', 40), target), [spuOnlyEvidence]).Status == MatStat.HashBad,
                "A complete SPU discovery set must prove an SPU mismatch even when the session contains no PPU fingerprints.");

            Assert(
                useCase.DeleteSaved(install, game) &&
                useCase.LoadSaved(install, game).Code == HashReadCode.NoMatch,
                "Deleting a saved binding must affect only the selected game binding.");

            static string Session(
                string serial,
                string? version,
                string ppu,
                string? spu = null)
            {
                return $"SYS: Serial: {serial}{Environment.NewLine}" +
                       $"SYS: App version: {version}{Environment.NewLine}" +
                       $"ppu_loader: PPU executable hash: PPU-{ppu}{Environment.NewLine}" +
                       (spu is null
                           ? string.Empty
                           : $"spu_loader: SPU executable hash: SPU-{spu}{Environment.NewLine}");
            }

            static Rpcs3PatchEntry Patch(
                string title,
                HashKind kind,
                string hash,
                R3PatTgt target)
            {
                return new Rpcs3PatchEntry(
                    title,
                    kind,
                    hash,
                    [target],
                    "Canary",
                    PatRisk.Canary,
                    null,
                    "Tests",
                    "1.0",
                    null,
                    PatSrc.Can,
                    Class: PatClass.Canary);
            }
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void DiscoveryStore_Hardening()
    {
        const string ppu = "1111111111111111111111111111111111111111";
        const string spu = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var temp = NewTemp();

        try
        {
            var storePath = Path.Combine(temp, "hdis.json");
            var exePath = Path.Combine(temp, "RPCS3", "rpcs3.exe");
            var gamePath = Path.Combine(temp, "games", "BLUS30109");
            var logPath = Path.Combine(temp, "RPCS3", "log", "RPCS3.log");
            var now = DateTime.UtcNow;
            var fingerprint = new string('a', 64);
            string[] bindingPpu =
            [
                ppu.ToUpperInvariant(),
                ppu
            ];
            string[] bindingSpu =
            [
                spu.ToUpperInvariant()
            ];
            var binding = new HCanHashBinding(
                exePath,
                "blus30109",
                "2.0",
                gamePath,
                bindingPpu,
                bindingSpu,
                now,
                now.AddSeconds(-2),
                now.AddSeconds(-1),
                fingerprint.ToUpperInvariant(),
                logPath);
            var store = new HCanDiscoveryStore(storePath);

            store.SaveBinding(binding, replaceExisting: true);
            var loaded = store.LoadBinding(
                exePath,
                "BLUS30109",
                "02.00",
                gamePath);

            Assert(
                loaded is not null &&
                loaded.Serial == "BLUS30109" &&
                loaded.GameVersion == "02.00" &&
                loaded.DetectedPpuHashes.Count == 1 &&
                loaded.DetectedPpuHashes[0] == ppu &&
                loaded.DetectedSpuHashes.Count == 1 &&
                loaded.DetectedSpuHashes[0] == spu &&
                loaded.LogFingerprint == fingerprint,
                "H-Canary discovery bindings must be normalized before persistence and lookup.");

            MustFail<ArgumentException>(
                () => store.SaveBinding(
                    binding with
                    {
                        DetectedPpuHashes = ["not-a-valid-executable-hash"]
                    },
                    replaceExisting: true),
                "Malformed H-Canary executable hashes must be rejected before persistence.");

            var legacyLogPath = Path.Combine(
                temp,
                "RPCS3",
                "RPCS3.log");
            var active = new HCanDiscoverySession(
                exePath,
                "blus30109",
                "2.0",
                gamePath,
                4242,
                now,
                logPath,
                new HCanLogMark(
                    false,
                    0,
                    null,
                    "ignored-when-log-does-not-exist"),
                [
                    new HCanLogSource(
                        logPath,
                        new HCanLogMark(
                            false,
                            0,
                            null,
                            "ignored-when-log-does-not-exist")),
                    new HCanLogSource(
                        legacyLogPath,
                        new HCanLogMark(
                            false,
                            0,
                            null,
                            "ignored-when-log-does-not-exist"))
                ]);
            store.SaveActive(active);
            var loadedActive = store.LoadActive();

            Assert(
                loadedActive is not null &&
                loadedActive.Serial == "BLUS30109" &&
                loadedActive.GameVersion == "02.00" &&
                !loadedActive.Baseline.Exists &&
                loadedActive.Baseline.Fingerprint.Length == 0 &&
                loadedActive.LogSources is { Count: 2 } &&
                loadedActive.LogSources.All(source =>
                    !source.Baseline.Exists &&
                    source.Baseline.Fingerprint.Length == 0),
                "H-Canary discovery sessions must normalize canonical identity, dual log candidates, and absent-log marks.");

            MustFail<ArgumentException>(
                () => store.SaveActive(active with
                {
                    ProcessId = -1
                }),
                "Negative H-Canary process identifiers must be rejected before persistence.");

            var invalidBinding = binding with
            {
                Serial = "INVALID",
                DetectedPpuHashes = [ppu]
            };
            var invalidActive = active with
            {
                Serial = "INVALID"
            };
            File.WriteAllText(
                storePath,
                JsonSerializer.Serialize(
                    new
                    {
                        Schema = 1,
                        Active = invalidActive,
                        Bindings = new[]
                        {
                            binding,
                            invalidBinding
                        }
                    }));

            var sanitizedStore = new HCanDiscoveryStore(storePath);
            Assert(
                sanitizedStore.LoadActive() is null,
                "Malformed persisted H-Canary active-session state must be discarded.");
            Assert(
                sanitizedStore.LoadBinding(
                    exePath,
                    "BLUS30109",
                    "02.00",
                    gamePath) is not null &&
                sanitizedStore.LoadBinding(
                    exePath,
                    "INVALID",
                    "02.00",
                    gamePath) is null,
                "Malformed persisted H-Canary bindings must be discarded without losing valid bindings.");

            byte[] invalidUtf8 = [0xC3, 0x28];
            File.WriteAllBytes(
                storePath,
                invalidUtf8);
            Assert(
                sanitizedStore.LoadActive() is null,
                "Invalid UTF-8 in the H-Canary discovery store must fail closed to empty state.");

            using (var oversized = new FileStream(
                       storePath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                oversized.SetLength(4L * 1024 * 1024 + 1);
            }

            Assert(
                sanitizedStore.LoadBinding(
                    exePath,
                    "BLUS30109",
                    "02.00",
                    gamePath) is null,
                "Oversized H-Canary discovery state must fail closed before whole-file allocation.");

            var concurrentPath = Path.Combine(
                temp,
                "hdis-concurrent.json");
            var concurrentBindings = Enumerable.Range(1, 24)
                .Select(index => binding with
                {
                    Serial = $"BLUS{index:00000}",
                    GameInstallPath = Path.Combine(
                        temp,
                        "games",
                        $"BLUS{index:00000}"),
                    DetectedPpuHashes =
                    [index.ToString("x40", CultureInfo.InvariantCulture)],
                    DetectedSpuHashes = [],
                    DetectedUtc = now.AddSeconds(index)
                })
                .ToArray();

            Parallel.ForEach(
                concurrentBindings,
                item => new HCanDiscoveryStore(concurrentPath)
                    .SaveBinding(item, replaceExisting: true));

            foreach (var item in concurrentBindings)
            {
                Assert(
                    new HCanDiscoveryStore(concurrentPath).LoadBinding(
                        item.Rpcs3ExecutablePath,
                        item.Serial,
                        item.GameVersion,
                        item.GameInstallPath) is not null,
                    "Concurrent H-Canary store updates must not lose bindings.");
            }
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void Launch_All()
    {
        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "RPCS3 Root");
            var exePath = Path.Combine(root, "rpcs3.exe");
            Directory.CreateDirectory(Path.Combine(root, "config"));
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            File.WriteAllBytes(exePath, []);

            var install = new Rpcs3InstallDescriptor(
                root,
                exePath,
                Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"),
                Path.Combine(root, "patches"),
                Path.Combine(root, "config", "patch_config.yml"));

            var game = new Rpcs3GameDescriptor(
                "BLES01465",
                "Resident Evil 6 Europe",
                Path.Combine(temp, "Unavailable Library", "Resident Evil 6 (Europe).chd"),
                "01.01",
                null,
                null);

            var launcher = new FakeGameProcessLauncher();

            var useCase = new GameLaunchUseCase(
                new Rpcs3BootPathResolver(),
                launcher);

            var result = useCase.Launch(install, game);

            Assert(
                result.Started &&
                result.Error == GameLaunchError.None &&
                result.ProcessId == 4242,
                "A selected game with a valid Game ID must produce an RPCS3 launch request.");

            Assert(
                launcher.ExecutablePath == Path.GetFullPath(exePath) &&
                launcher.WorkingDirectory == Path.GetFullPath(root) &&
                launcher.BootTarget == "%RPCS3_GAMEID%:BLES01465",
                "RPCS3 launch must pass its executable, data root, and official GAMEID token without probing the library path.");

            Assert(
                result.BootTarget == "%RPCS3_GAMEID%:BLES01465" &&
                !result.BootTarget.Contains(temp, StringComparison.OrdinalIgnoreCase),
                "The launch request must let RPCS3 resolve games.yml instead of exposing a host game path.");

            var missing = useCase.Launch(
                install,
                game with { Serial = "bad/id" });

            Assert(
                !missing.Started &&
                missing.Error == GameLaunchError.GameIdentityMissing,
                "An invalid Game ID must produce a precise non-launch result without probing a path.");

            var launchCalls = 0;
            var hashHarness = new FakeHashHarness();

            var vm = new HCanVm(
                new AppFlow(),
                isRpcs3Running: () => false,
                confirm: (_, _) => false,
                launchGame: (_, _) =>
                {
                    launchCalls++;

                    return new GameLaunchResult(
                        true,
                        GameLaunchError.None,
                        "%RPCS3_GAMEID%:BLES01465",
                        ProcessId: 4242);
                },
                hashOps: hashHarness.Ops);

            vm.SetCtx(install, game);

            Assert(
                vm.LaunchCmd.CanRun(null),
                "The selected game must enable the explicit launch command.");

            vm.LaunchCmd.Execute(null);

            Assert(
                launchCalls == 1 &&
                hashHarness.Active?.ProcessId == 4242 &&
                vm.LogStat == UiText.Fmt("HCanary.Launch.Started", game.Title),
                "Launching from H-Canary must report the selected game and preserve the next log-scan step.");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static async Task Capture_All()
    {
        Assert(
            GameFingerprintCaptureOptions.Default.StartupStabilityDelay ==
            TimeSpan.FromMinutes(1),
            "Automatic executable-hash discovery must keep the selected game running for one minute before closing RPCS3 and reading RPCS3.log.");

        const string oldPpu = "3333333333333333333333333333333333333333";
        const string ppu = "4444444444444444444444444444444444444444";
        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "RPCS3 Capture Root");
            var logDir = Path.Combine(root, "log");
            var logPath = Path.Combine(logDir, "RPCS3.log");
            var exePath = Path.Combine(root, "rpcs3.exe");
            var gamePath = Path.Combine(temp, "games", "BLES01465");
            var otherGamePath = Path.Combine(temp, "games", "BLUS99999");
            Directory.CreateDirectory(logDir);
            Directory.CreateDirectory(gamePath);
            Directory.CreateDirectory(otherGamePath);
            Directory.CreateDirectory(Path.Combine(root, "config"));
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            File.WriteAllBytes(exePath, []);
            File.WriteAllText(
                logPath,
                $"SYS: Serial: BLUS00000{Environment.NewLine}" +
                $"SYS: App version: 01.00{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{oldPpu}{Environment.NewLine}");

            var install = new Rpcs3InstallDescriptor(
                root,
                exePath,
                Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"),
                Path.Combine(root, "patches"),
                Path.Combine(root, "config", "patch_config.yml"),
                root);
            var game = new Rpcs3GameDescriptor(
                "BLES01465",
                "Resident Evil 6 Europe",
                gamePath,
                "01.01",
                null,
                null);
            var otherGame = new Rpcs3GameDescriptor(
                "BLUS99999",
                "Other Game",
                otherGamePath,
                "01.00",
                null,
                null);
            var expectation = new CanaryHashExpectation(
                [ppu],
                []);

            var discovery = new HCanHashDiscoveryService();
            var store = new HCanDiscoveryStore(
                Path.Combine(temp, "capture-state.json"));
            var processProbe = new FakeProcessProbe();
            var hashEvidence = new HashEvidenceUseCase(
                discovery,
                store,
                processProbe);
            var launcher = new FakeGameProcessLauncher();
            var processController = new FakeCaptureProcessController
            {
                StartupReadyAfterChecks = 1,
                LogReadyAfterChecks = 2,
                OnStartupReady = () => File.AppendAllText(
                    logPath,
                    $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                    $"SYS: App version: {game.Version}{Environment.NewLine}" +
                    $"ppu_loader: PPU executable hash: PPU-{ppu}{Environment.NewLine}"),
                BeforeStop = () => File.AppendAllText(
                    logPath,
                    $"SYS: Stopping emulator...{Environment.NewLine}")
            };
            var capture = new GameFingerprintCaptureUseCase(
                hashEvidence,
                new GameLaunchUseCase(
                    new Rpcs3BootPathResolver(),
                    launcher),
                processController,
                new GameFingerprintCaptureOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    PollInterval: TimeSpan.FromMilliseconds(1),
                    StartupStabilityDelay: TimeSpan.Zero,
                    GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                    PostExitLogDelay: TimeSpan.Zero));

            var result = await capture.CaptureAsync(
                install,
                game,
                expectation);

            Assert(
                result.Succeeded &&
                result.Evidence is
                {
                    Code: HashReadCode.Found,
                    IsSaved: true
                } &&
                result.Evidence.Hashes.Count(item =>
                    item.Kind == HashKind.Ppu) == 1 &&
                result.Evidence.Hashes.Any(item =>
                    item.Kind == HashKind.Ppu &&
                    item.Value == ppu) &&
                result.Evidence.Hashes.All(item =>
                    item.Value != oldPpu),
                "Automatic capture must read only the selected post-launch session after the owned RPCS3 process exits.");
            Assert(
                launcher.StartCalls == 1 &&
                processController.AttachCalls == 1 &&
                processController.StartupReadyChecks >= 2 &&
                processController.StopCalls == 1 &&
                !processController.Running,
                "Automatic capture must launch once and close only the process owned by that capture session.");

            var secondLauncher = new FakeGameProcessLauncher();
            var secondController = new FakeCaptureProcessController();
            var secondCapture = new GameFingerprintCaptureUseCase(
                hashEvidence,
                new GameLaunchUseCase(
                    new Rpcs3BootPathResolver(),
                    secondLauncher),
                secondController,
                new GameFingerprintCaptureOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    PollInterval: TimeSpan.FromMilliseconds(1),
                    StartupStabilityDelay: TimeSpan.Zero,
                    GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                    PostExitLogDelay: TimeSpan.Zero));

            var secondResult = await secondCapture.CaptureAsync(
                install,
                game,
                expectation);

            Assert(
                !secondResult.Succeeded &&
                secondResult.Error ==
                    GameFingerprintCaptureError.FingerprintAlreadyCaptured &&
                secondLauncher.StartCalls == 0 &&
                secondController.AttachCalls == 0 &&
                secondController.StopCalls == 0,
                "A saved selected-game PPU binding must make capture immutable and prevent a second RPCS3 launch.");

            processProbe.Running = true;
            var blockedLauncher = new FakeGameProcessLauncher();
            var blockedController = new FakeCaptureProcessController();
            var blocked = new GameFingerprintCaptureUseCase(
                hashEvidence,
                new GameLaunchUseCase(
                    new Rpcs3BootPathResolver(),
                    blockedLauncher),
                blockedController,
                new GameFingerprintCaptureOptions(
                    Timeout: TimeSpan.FromSeconds(1),
                    PollInterval: TimeSpan.FromMilliseconds(1),
                    StartupStabilityDelay: TimeSpan.Zero,
                    GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                    PostExitLogDelay: TimeSpan.Zero));

            var blockedResult = await blocked.CaptureAsync(
                install,
                otherGame);

            Assert(
                !blockedResult.Succeeded &&
                blockedResult.Error ==
                    GameFingerprintCaptureError.Rpcs3AlreadyRunning &&
                blockedLauncher.StartCalls == 0 &&
                blockedController.StopCalls == 0,
                "A pre-existing RPCS3 instance must block a new uncaptured game and must never be closed by the workflow.");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void Vld_All()
    {
        var service = new HCanaryPatchAuthoringService();
        var valid = GoodDrft();
        var validResult = service.Validate(valid);

        Assert(
            validResult.IsValid,
            "A complete PPU draft should validate: " +
            string.Join(
                " | ",
                validResult.Issues.Select(issue => issue.Message)));

        AssertOperationContract(HCanaryPatchAuthoringService.Op);

        HasIssue(service, valid, draft => draft.ExeHash = "PPU-abc", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.ExeHash = "PPU-gggggggggggggggggggggggggggggggggggggggg", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.PatName = string.Empty, "PatchName.Required");
        HasIssue(service, valid, draft => draft.PatVer = string.Empty, "PatchVersion.Required");
        HasIssue(service, valid, draft => draft.PatVer = "١.٠", "PatchVersion.Invalid");
        HasIssue(service, valid, draft => draft.Games[0].GameId = string.Empty, "GameId.Required");
        HasIssue(service, valid, draft => draft.Games[0].GameVer = string.Empty, "GameVersion.Required");
        HasIssue(service, valid, draft => draft.Ins.Clear(), "Instructions.Required");
        HasIssue(service, valid, draft => draft.Ins[0].Address = "not-an-address", "Address.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Value = "hello", "Value.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Op = "unknown", "Operation.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Op = "be8", "Operation.Invalid");

        HasIssue(
            service,
            valid,
            draft => draft.Ins[0] = new HCanInst
            {
                Op = "load",
                Address = "Missing"
            },
            "Anchor.Missing");

        HasIssue(service, valid, draft => draft.Ins[0].Address = "1234", "Address.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Address = "0x100000000", "Address.Invalid");

        var decimalInteger = valid.Clone();

        decimalInteger.Ins[0] = new HCanInst
        {
            Op = "be16",
            Address = "0x0001c310",
            Value = "720"
        };

        Assert(
            service.Validate(decimalInteger).IsValid,
            "RPCS3 decimal integer values should validate.");

        var floatingPoint = valid.Clone();

        floatingPoint.Ins[0] = new HCanInst
        {
            Op = "bef32",
            Address = "0x0001c310",
            Value = "0.65"
        };

        Assert(
            service.Validate(floatingPoint).IsValid,
            "RPCS3 floating-point values should validate.");

        var hideFile = valid.Clone();

        hideFile.Ins[0] = new HCanInst
        {
            Op = "hide_file",
            Address = "/dev_hdd0/game/NPUB30899/USRDIR/file.bin",
            Value = string.Empty
        };

        Assert(
            service.Validate(hideFile).IsValid,
            "hide_file should accept a source path and an empty value.");

        var moveFile = valid.Clone();

        moveFile.Ins[0] = new HCanInst
        {
            Op = "move_file",
            Address = "/dev_hdd0/game/NPUB30899/USRDIR/source.bin",
            Value = "/dev_hdd0/game/NPUB30899/USRDIR/target.bin"
        };

        Assert(
            service.Validate(moveFile).IsValid,
            "move_file should accept source and destination paths.");

        var allTarget = valid.Clone();

        allTarget.Games =
        [
            new HCanTgt
            {
                GameName = "All",
                GameId = "All",
                GameVer = "All"
            }
        ];

        Assert(
            service.Validate(allTarget).IsValid,
            "The official Games: All / All / All target should validate.");

        HasIssue(
            service,
            valid,
            draft =>
            {
                draft.Games[0].GameName = "All";
                draft.Games[0].GameId = "NPUB30899";
            },
            "GameTarget.AllPair");

        var duplicateAnchor = valid.Clone();

        duplicateAnchor.Anchors =
        [
            new HCanAnc
            {
                Name = "Shared",
                Ins = [new HCanInst()]
            },
            new HCanAnc
            {
                Name = "Shared",
                Ins = [new HCanInst()]
            }
        ];

        Assert(
            service.Validate(duplicateAnchor).Issues.Any(
                issue => issue.Code == "Anchor.Duplicate"),
            "Duplicate Anchors must be rejected.");

        var spu = valid.Clone();
        spu.PatType = HCanType.Spu;

        Assert(
            service.Validate(spu).Issues.Any(
                issue => issue.Code == "Hash.Kind"),
            "SPU Patch Type must require an SPU Hash.");
    }

    private static void Yaml_All()
    {
        var service = new HCanaryPatchAuthoringService();
        var draft = GoodDrft();

        draft.PatName = "Reduce \"input\" lag";
        draft.Author = "WAZAAAAA";
        draft.Notes = "ملاحظة عربية\nSecond line";
        draft.Group = "FPS";

        draft.Games.Add(
            new HCanTgt
            {
                GameName = "Game Two",
                GameId = "BLES00001",
                GameVer = "01.01"
            });

        draft.Games.Add(
            new HCanTgt
            {
                GameName = "Game Two",
                GameId = "BLES00001",
                GameVer = "All"
            });

        draft.Ins.Add(
            new HCanInst
            {
                Op = "be16",
                Address = "0x0001c314",
                Value = "0x6000"
            });

        draft.Anchors.Add(
            new HCanAnc
            {
                Name = "AnchorDesc",
                Ins =
                [
                    new HCanInst
                    {
                        Op = "be32",
                        Address = "0x00000000",
                        Value = "0x60000000"
                    }
                ]
            });

        draft.Ins.Add(
            new HCanInst
            {
                Op = "load",
                Address = "AnchorDesc",
                Value = string.Empty
            });

        var result = service.Validate(draft);

        Assert(
            result.IsValid,
            "PPU YAML with games, Group, Arabic Notes, and Anchors should validate: " +
            string.Join(
                " | ",
                result.Issues.Select(issue => issue.Message)));

        var yaml = result.Yaml;

        Assert(
            yaml.StartsWith("Version: 1.2\r\n", StringComparison.Ordinal),
            "Version: 1.2 must be the first line.");

        AssertCrLfOnly(
            yaml,
            "Generated YAML must use CRLF only.");

        Assert(
            yaml.Contains(
                "PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9:",
                StringComparison.Ordinal),
            "PPU key should be unchanged.");

        Assert(
            yaml.Contains(
                "Group: \"FPS\"",
                StringComparison.Ordinal),
            "Group should be generated.");

        Assert(
            yaml.Contains(
                "BLES00001: [ 01.01, All ]",
                StringComparison.Ordinal),
            "Multiple versions should share one Game ID mapping.");

        Assert(
            yaml.Contains(
                "AnchorDesc: &AnchorDesc",
                StringComparison.Ordinal) &&
            yaml.Contains(
                "- [ load, *AnchorDesc ]",
                StringComparison.Ordinal),
            "Anchor definition and load should be generated.");

        Assert(
            yaml.Contains(
                "ملاحظة عربية",
                StringComparison.Ordinal),
            "Arabic Notes must be preserved as UTF-8 text.");

        Assert(
            yaml.Contains(
                "Reduce \\\"input\\\" lag",
                StringComparison.Ordinal),
            "Quoted patch names must be escaped.");

        var fileOperations = GoodDrft();

        fileOperations.Ins =
        [
            new HCanInst
            {
                Op = "hide_file",
                Address = "/dev_hdd0/game/NPUB30899/USRDIR/file.bin",
                Value = string.Empty
            },
            new HCanInst
            {
                Op = "move_file",
                Address = "/dev_hdd0/game/NPUB30899/USRDIR/source.bin",
                Value = "/dev_hdd0/game/NPUB30899/USRDIR/target.bin"
            }
        ];

        var fileYaml = service.Validate(fileOperations);

        Assert(
            fileYaml.IsValid &&
            fileYaml.Yaml.Contains(
                "[ hide_file, \"/dev_hdd0/game/NPUB30899/USRDIR/file.bin\", \"\" ]",
                StringComparison.Ordinal) &&
            fileYaml.Yaml.Contains(
                "[ move_file, \"/dev_hdd0/game/NPUB30899/USRDIR/source.bin\", \"/dev_hdd0/game/NPUB30899/USRDIR/target.bin\" ]",
                StringComparison.Ordinal),
            "File operations must generate quoted YAML paths and preserve hide_file's empty value.");

        var allTarget = GoodDrft();

        allTarget.Games =
        [
            new HCanTgt
            {
                GameName = "all",
                GameId = "all",
                GameVer = "all"
            }
        ];

        var allYaml = service.Validate(allTarget);

        Assert(
            allYaml.IsValid &&
            allYaml.Yaml.Contains(
                "All: [ All ]",
                StringComparison.Ordinal),
            "The All target must be emitted with RPCS3's canonical spelling.");

        var spu = GoodDrft();
        spu.PatType = HCanType.Spu;
        spu.ExeHash = "SPU-1234567890abcdef1234567890abcdef12345678";

        Assert(
            service.Validate(spu).IsValid &&
            service.Generate(spu).Contains(
                "SPU-",
                StringComparison.Ordinal),
            "SPU YAML should generate.");

        var experimental = GoodDrft();
        experimental.IsExp = true;
        experimental.PatVer = "0.1";

        Assert(
            service.Validate(experimental).IsValid,
            "Experimental classification must remain independent from the PPU/SPU executable type.");
    }

    private static void Imp_Edit()
    {
        var service = new HCanaryPatchAuthoringService();
        var temp = NewTemp();

        try
        {
            var patches = Path.Combine(temp, "patches");
            var backup = Path.Combine(temp, "backups");
            Directory.CreateDirectory(patches);

            var path = Path.Combine(
                patches,
                "imported_patch.yml");

            var unmanagedPath = Path.Combine(
                temp,
                "unmanaged",
                "imported_patch.yml");

            Directory.CreateDirectory(
                Path.GetDirectoryName(unmanagedPath)!);

            File.WriteAllText(
                unmanagedPath,
                service.Generate(GoodDrft()));

            Assert(
                service.Load(unmanagedPath).Count == 0,
                "The editor must not offer unmanaged imported patches that it cannot round-trip without losing unsupported fields.");

            var first = GoodDrft();

            first.Anchors.Add(
                new HCanAnc
                {
                    Name = "SharedCode",
                    Ins =
                    [
                        new HCanInst
                        {
                            Op = "be32",
                            Address = "0x00000020",
                            Value = "0x60000000"
                        }
                    ]
                });

            first.Ins.Add(
                new HCanInst
                {
                    Op = "load",
                    Address = "SharedCode",
                    Value = string.Empty
                });

            var saved = service.Save(
                path,
                backup,
                first);

            Assert(
                saved.PatNum == 1 &&
                File.Exists(path),
                "Saving should create imported_patch.yml.");

            Assert(
                File.Exists(
                    Path.Combine(
                        patches,
                        HCanaryPatchAuthoringService.MetaName)) &&
                !File.Exists(
                    Path.Combine(
                        patches,
                        HCanaryPatchAuthoringService.OldName)),
                "A new H-Canary save must write only r3hcan.json.");

            var bytes = File.ReadAllBytes(path);

            Assert(
                !(
                    bytes.Length >= 3 &&
                    bytes[0] == 0xEF &&
                    bytes[1] == 0xBB &&
                    bytes[2] == 0xBF),
                "imported_patch.yml must be UTF-8 without BOM.");

            AssertCrLfOnly(
                File.ReadAllText(
                    path,
                    Encoding.UTF8),
                "Saved file must use CRLF only.");

            var loaded = service.Load(path);

            Assert(
                loaded.Count == 1,
                "A saved custom Patch should open.");

            Assert(
                loaded[0].Draft.Status == DraftSt.Saved,
                "Saving a Draft should persist its local state as Saved.");

            var edited = loaded[0].Draft;

            Assert(
                edited.Games.Count == first.Games.Count &&
                edited.Ins.Count == first.Ins.Count,
                "Open/edit should retain Games and Patch instructions.");

            Assert(
                edited.Anchors.Count == 1 &&
                edited.Anchors[0].Name == "SharedCode",
                "Open/edit should retain referenced Anchors.");

            edited.Ins[0].Value = "0x39200001";

            edited.Games.Add(
                new HCanTgt
                {
                    GameName = "Alt Region",
                    GameId = "BLES00001",
                    GameVer = "01.01"
                });

            edited.Notes = "edited";

            service.Save(
                path,
                backup,
                edited,
                loaded[0].Identity);

            var reloaded = service.Load(path).Single();

            Assert(
                reloaded.Draft.Ins[0].Value == "0x39200001" &&
                reloaded.Draft.Games.Count == 2,
                "Structured edits should survive regeneration.");

            var second = GoodDrft();
            second.ExeHash = "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            second.PatName = "Other Patch";
            second.IsExp = true;
            second.Status = DraftSt.Testing;

            service.Save(
                path,
                backup,
                second);

            var merged = service.Load(path);

            Assert(
                merged.Count == 2 &&
                merged.Any(
                    item => item.Draft.PatName == first.PatName),
                "Merge must not delete other imported patches.");

            Assert(
                merged.Single(
                    item => item.Draft.PatName == "Other Patch")
                .Draft.Status == DraftSt.Testing,
                "Local Draft/Testing status should persist in separate R3Config metadata.");

            Assert(
                merged.Single(
                    item => item.Draft.PatName == "Other Patch")
                .Draft.IsExp &&
                merged.Single(
                    item => item.Draft.PatName == "Other Patch")
                .Draft.PatType == HCanType.Ppu,
                "Experimental classification must persist separately from the PPU executable type.");

            var localCatalog = new R3Config.Svc.ReadSvc()
                .Read(
                    path,
                    PatSrc.Imported)
                .Catalog;

            var classified = service.Apply(
                localCatalog,
                path);

            var classifiedOther = classified.Entries.Single(
                item => item.Title == "Other Patch");

            Assert(
                classifiedOther.Risk == PatRisk.Canary &&
                classifiedOther.Class == PatClass.Canary &&
                classifiedOther.HashKind == HashKind.Ppu &&
                !classifiedOther.PatType.Equals(
                    "Canary",
                    StringComparison.OrdinalIgnoreCase),
                "H-Canary metadata must classify stability without replacing the PPU executable kind or functional category.");

            MustFail<InvalidOperationException>(
                () => service.Save(
                    path,
                    backup,
                    second),
                "Duplicate Patch name and Hash should be detected.");

            service.Delete(
                path,
                backup,
                merged.Single(
                    item => item.Draft.PatName == "Other Patch")
                .Identity);

            Assert(
                service.Load(path).Count == 1,
                "Deleting one custom Patch must preserve other patches.");

            var oldDir = Path.Combine(temp, "old");
            Directory.CreateDirectory(oldDir);

            var oldPat = Path.Combine(
                oldDir,
                "imported_patch.yml");

            var oldBkp = Path.Combine(
                temp,
                "old-bkp");

            service.Save(
                oldPat,
                oldBkp,
                GoodDrft());

            var newMeta = Path.Combine(
                oldDir,
                HCanaryPatchAuthoringService.MetaName);

            var oldMeta = Path.Combine(
                oldDir,
                HCanaryPatchAuthoringService.OldName);

            File.Move(
                newMeta,
                oldMeta);

            var oldLoad = service.Load(oldPat).Single();

            Assert(
                oldLoad.Draft.PatName == GoodDrft().PatName,
                "Legacy r3config_hcanary.json metadata must remain readable.");

            oldLoad.Draft.Notes = "migrated";

            service.Save(
                oldPat,
                oldBkp,
                oldLoad.Draft,
                oldLoad.Identity);

            Assert(
                File.Exists(newMeta) &&
                File.Exists(oldMeta),
                "Saving legacy H-Canary data must transactionally write r3hcan.json without deleting the old file.");

            Assert(
                service.Load(oldPat).Single().Draft.Notes == "migrated",
                "Once written, r3hcan.json must be authoritative over the retained legacy metadata.");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static void Txn_All()
    {
        var temp = NewTemp();

        try
        {
            var path = Path.Combine(
                temp,
                "imported_patch.yml");

            var backupRoot = Path.Combine(
                temp,
                "backups");

            var baseline = new HCanaryPatchAuthoringService();

            baseline.Save(
                path,
                backupRoot,
                GoodDrft());

            var original = File.ReadAllText(path);

            var newMeta = Path.Combine(
                temp,
                HCanaryPatchAuthoringService.MetaName);

            var oldMeta = Path.Combine(
                temp,
                HCanaryPatchAuthoringService.OldName);

            File.Move(
                newMeta,
                oldMeta);

            var oldBytes = File.ReadAllBytes(oldMeta);

            Assert(
                Directory.EnumerateFiles(
                    backupRoot,
                    "manifest.json",
                    SearchOption.AllDirectories)
                .Any(),
                "Save should create a Backup manifest.");

            var failingTransaction = new PatchFileTransactionService(
                commitFile: (_, _) =>
                    throw new IOException(
                        "simulated atomic replace failure"));

            var failing = new HCanaryPatchAuthoringService(
                transaction: failingTransaction);

            var changed = GoodDrft();
            changed.PatName = "Rollback Patch";
            changed.ExeHash = "PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            MustFail<IOException>(
                () => failing.Save(
                    path,
                    backupRoot,
                    changed),
                "A failed atomic commit should surface the original failure.");

            Assert(
                File.ReadAllText(path) == original,
                "Transaction failure must roll back imported_patch.yml exactly.");

            Assert(
                !File.Exists(newMeta) &&
                File.Exists(oldMeta) &&
                File.ReadAllBytes(oldMeta).SequenceEqual(oldBytes),
                "A failed H-Canary migration must leave the legacy metadata byte-for-byte unchanged.");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static void Vm_All()
    {
        var temp = NewTemp();
        var originalLang = UiText.CurLang;

        try
        {
            UiText.SetLang(UiText.ArabLang);

            var root = Path.Combine(
                temp,
                "rpcs3");

            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    "patches"));

            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    "config"));

            File.WriteAllText(
                Path.Combine(
                    root,
                    "rpcs3.exe"),
                string.Empty);

            var install = new Rpcs3InstallDescriptor(
                root,
                Path.Combine(
                    root,
                    "rpcs3.exe"),
                Path.Combine(
                    root,
                    "config"),
                Path.Combine(
                    root,
                    "config",
                    "custom_configs"),
                Path.Combine(
                    root,
                    "patches"),
                Path.Combine(
                    root,
                    "config",
                    "patch_config.yml"));

            var game = new Rpcs3GameDescriptor(
                "NPUB30899",
                "TEKKEN TAG TOURNAMENT 2",
                "game",
                "01.01",
                "8645cfa235c9980ff74b5e06388b29e97fdbbed9",
                null,
                ["8645cfa235c9980ff74b5e06388b29e97fdbbed9"],
                [],
                Path.Combine(
                    root,
                    "RPCS3.log"));

            var hashHarness = new FakeHashHarness
            {
                Saved = new HashRead(
                    true,
                    Path.Combine(root, "log", "RPCS3.log"),
                    [
                        new HCanHash(
                            HashKind.Ppu,
                            "8645cfa235c9980ff74b5e06388b29e97fdbbed9",
                            Path.Combine(root, "log", "RPCS3.log"),
                            10)
                    ],
                    Code: HashReadCode.Found,
                    Serial: game.Serial,
                    Version: game.Version,
                    DetectedUtc: DateTime.UtcNow,
                    IsSaved: true)
            };

            var advancedLaunchCalls = 0;
            var vm = new HCanVm(
                new AppFlow(),
                isRpcs3Running: () => false,
                confirm: (_, _) => false,
                launchGame: (_, _) =>
                {
                    advancedLaunchCalls++;
                    return new GameLaunchResult(
                        true,
                        GameLaunchError.None,
                        ProcessId: 4242);
                },
                hashOps: hashHarness.Ops);

            vm.SetCtx(
                install,
                game);

            Assert(
                vm.Games.Count == 1 &&
                vm.Games[0].GameId == "NPUB30899",
                "Selected game should initialize a new authoring draft.");

            Assert(
                vm.Hashes.Count == 1,
                "Selected game Hash evidence should populate the chooser.");

            Assert(
                vm.HasHashInput &&
                vm.HasSavedBinding &&
                vm.ExeHash == "PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9",
                "A single saved discovery hash should populate the new draft without exposing unrelated game metadata evidence.");

            Assert(
                vm.HashHead == UiText.GetText("HCanary.Hash.Saved"),
                "A binding loaded for the exact RPCS3 path, game, version, and install path must be labeled as previously identified rather than freshly extracted.");

            Assert(
                !vm.LaunchCmd.CanRun(null) &&
                !vm.ReadCmd.CanRun(null) &&
                vm.RediscoverCmd.CanRun(null),
                "A saved exact PPU binding must disable every launch or log-read command while leaving only a saved-binding reload action.");

            vm.RediscoverCmd.Execute(null);
            Assert(
                advancedLaunchCalls == 0 &&
                vm.HasSavedBinding &&
                vm.LogStat == UiText.GetText("HCanary.Hash.Reused"),
                "Reloading a saved fingerprint must reuse persistent evidence without launching RPCS3 or reading a new log.");

            Assert(
                vm.CanAdvanceTarget &&
                !vm.CanAdvanceDetails &&
                !vm.CanAdvanceInstructions,
                "Exact saved target evidence should unlock only the target step until required patch details are complete.");

            vm.PatName = "VM Patch";
            vm.Author = "Author";

            Assert(
                string.IsNullOrEmpty(vm.LogStat),
                "Editing the draft after reloading a saved fingerprint must clear the transient reload status so the compact saved-fingerprint summary remains the only routine state text.");

            var previewBefore = vm.Preview;

            vm.Ins[0].Address = "0x0001c310";
            vm.Ins[0].Value = "0x39200000";

            Assert(
                vm.Preview != previewBefore &&
                vm.Preview.Contains(
                    "0x39200000",
                    StringComparison.Ordinal),
                "Preview should update when an instruction changes.");

            Assert(
                vm.CanAdvanceTarget &&
                vm.CanAdvanceDetails &&
                vm.CanAdvanceInstructions,
                "A complete valid draft must unlock every authoring step.");

            Assert(
                vm.IsValid &&
                vm.SaveCmd.CanRun(null),
                $"CanExecute should allow a complete valid draft (IsValid={vm.IsValid}, CanMut={vm.CanMut}).");

            Assert(
                vm.SaveHint == UiText.GetText("HCanary.SaveHint.Ready") &&
                vm.IsStoredEmpty,
                "A valid new draft should explain that saving is ready and that no stored patches exist.");

            vm.SetAvail(
                isBusy: false,
                isRpcs3Running: true);

            Assert(
                !vm.SaveCmd.CanRun(null) &&
                vm.SaveHint == UiText.GetText("HCanary.SaveHint.Rpcs3Running"),
                "The save hint and CanExecute must both report the RPCS3 running guard.");

            vm.SetAvail(
                isBusy: false,
                isRpcs3Running: false);

            vm.AddICmd.Execute(null);

            Assert(
                vm.Ins.Count == 2,
                "ViewModel should add instructions.");

            vm.DelICmd.Execute(null);

            Assert(
                vm.Ins.Count == 1,
                "ViewModel should delete the selected instruction.");

            vm.AddGmCmd.Execute(null);

            Assert(
                vm.Games.Count == 2,
                "ViewModel should add game targets.");

            vm.DelGmCmd.Execute(null);

            Assert(
                vm.Games.Count == 1,
                "ViewModel should delete the selected game target.");

            vm.AddACmd.Execute(null);

            Assert(
                vm.Anchors.Count == 1,
                "ViewModel should add Anchors.");

            vm.AncLoad.Execute(null);

            Assert(
                vm.Ins.Any(
                    item => item.Op == "load"),
                "ViewModel should add a load reference.");

            vm.SelIns = vm.Ins.Last();
            vm.DelICmd.Execute(null);

            vm.PatVer = "bad";
            vm.Games[0].GameVer = "bad";
            vm.GameTgt = vm.Games[0];
            vm.SelIns = vm.Ins[0];

            var selectedAnchorItem = vm.Anchors[0];

            vm.SelAnc = selectedAnchorItem;
            vm.AncIns = selectedAnchorItem.Ins[0];

            vm.SelPat = new HCanStor(
                "stored",
                "Stored patch",
                GoodDrft());

            var draftName = vm.PatName;
            var preview = vm.Preview;
            var games = JsonSerializer.Serialize(
                vm.Games.Select(
                    item => item.ToModel()));

            var instructions = JsonSerializer.Serialize(
                vm.Ins.Select(
                    item => item.ToModel()));

            var anchors = JsonSerializer.Serialize(
                vm.Anchors.Select(
                    item => item.ToModel()));

            var selectedGame = vm.GameTgt;
            var selectedInstruction = vm.SelIns;
            var selectedAnchor = vm.SelAnc;
            var selectedAnchorInstruction = vm.AncIns;
            var selectedStoredPatch = vm.SelPat;
            var dirty = vm.IsDirty;
            var arabicMessage = vm.Msg;
            var arabicLogStatus = vm.LogStat;
            var arabicValidation = vm.VldSum;
            var arabicVersionError = vm.VerErr;
            var arabicGameError = vm.Games[0].Error;
            var filesBefore = FileState(root);

            UiText.SetLang(UiText.EnLang);
            vm.SetLang();

            Assert(
                vm.Msg == UiText.GetText("HCanary.Message.NewDraft") &&
                vm.Msg != arabicMessage,
                "H-Canary message must refresh from its localization key.");

            Assert(
                string.IsNullOrEmpty(vm.LogStat) &&
                string.IsNullOrEmpty(arabicLogStatus),
                "Saved fingerprint results must not duplicate the compact summary with routine status text.");

            Assert(
                vm.VldSum != arabicValidation &&
                vm.VerErr != arabicVersionError &&
                vm.Games[0].Error != arabicGameError,
                "Validation summary and field errors must refresh without re-validating the draft.");

            Assert(
                vm.PatName == draftName &&
                vm.Preview == preview &&
                vm.IsDirty == dirty,
                "Language refresh must preserve draft values, preview, and dirty state.");

            Assert(
                JsonSerializer.Serialize(
                    vm.Games.Select(
                        item => item.ToModel())) == games &&
                JsonSerializer.Serialize(
                    vm.Ins.Select(
                        item => item.ToModel())) == instructions &&
                JsonSerializer.Serialize(
                    vm.Anchors.Select(
                        item => item.ToModel())) == anchors,
                "Language refresh must preserve games, instructions, and anchors.");

            Assert(
                ReferenceEquals(
                    vm.GameTgt,
                    selectedGame) &&
                ReferenceEquals(
                    vm.SelIns,
                    selectedInstruction) &&
                ReferenceEquals(
                    vm.SelAnc,
                    selectedAnchor) &&
                ReferenceEquals(
                    vm.AncIns,
                    selectedAnchorInstruction) &&
                ReferenceEquals(
                    vm.SelPat,
                    selectedStoredPatch),
                "Language refresh must preserve every H-Canary selection.");

            Assert(
                FileState(root).SequenceEqual(filesBefore),
                "Language refresh must not mutate the RPCS3 tree.");

            UiText.SetLang(UiText.ArabLang);
            vm.SetLang();

            Assert(
                vm.Msg == arabicMessage &&
                vm.LogStat == arabicLogStatus &&
                vm.VldSum == arabicValidation &&
                vm.VerErr == arabicVersionError &&
                vm.Games[0].Error == arabicGameError,
                "Returning to Arabic must restore all localized H-Canary presentation text.");

            vm.SetCtx(
                install,
                game with
                {
                    Serial = "BLUS00001",
                    Title = "Other Game"
                });

            Assert(
                vm.PatName == draftName &&
                vm.IsDirty,
                "Changing the selected game must preserve an active draft.");
        }
        finally
        {
            UiText.SetLang(originalLang);
            Directory.Delete(temp, true);
        }
    }

    private static void Vm_InstructionCommands()
    {
        var vm = new HCanVm(
            new AppFlow(),
            isRpcs3Running: () => false,
            confirm: (_, _) => false);

        Assert(
            vm.Ins.Count == 1 &&
            ReferenceEquals(vm.SelIns, vm.Ins[0]),
            "A new H-Canary draft must contain and select one instruction.");

        Assert(
            vm.DelICmd.CanRun(null) &&
            vm.DupICmd.CanRun(null),
            "Delete and duplicate must be available for the selected instruction.");

        Assert(
            !vm.InsUp.CanRun(null) &&
            !vm.InsDown.CanRun(null),
            "Move commands must be disabled when the instruction is both first and last.");

        var original = vm.Ins[0];
        original.Op = "be32";
        original.Address = "0x0001c310";
        original.Value = "0x39200000";

        vm.DupICmd.Execute(null);

        Assert(
            vm.Ins.Count == 2 &&
            ReferenceEquals(vm.SelIns, vm.Ins[1]) &&
            !ReferenceEquals(vm.Ins[0], vm.Ins[1]) &&
            vm.Ins[1].Op == original.Op &&
            vm.Ins[1].Address == original.Address &&
            vm.Ins[1].Value == original.Value,
            "Duplicate must insert an independent copy immediately after the source and select it.");

        Assert(
            vm.InsUp.CanRun(null) &&
            !vm.InsDown.CanRun(null),
            "The selected last instruction must move up but not down.");

        var duplicate = vm.SelIns!;
        vm.InsUp.Execute(null);

        Assert(
            ReferenceEquals(vm.Ins[0], duplicate) &&
            ReferenceEquals(vm.SelIns, duplicate) &&
            !vm.InsUp.CanRun(null) &&
            vm.InsDown.CanRun(null),
            "Move Up must preserve selection and update boundary CanExecute state.");

        vm.InsDown.Execute(null);

        Assert(
            ReferenceEquals(vm.Ins[1], duplicate) &&
            ReferenceEquals(vm.SelIns, duplicate),
            "Move Down must restore the selected instruction to the next position.");

        vm.DelICmd.Execute(null);

        Assert(
            vm.Ins.Count == 1 &&
            ReferenceEquals(vm.SelIns, vm.Ins[0]),
            "Deleting the selected instruction must select the nearest remaining row.");

        vm.SelIns = null;

        Assert(
            !vm.DelICmd.CanRun(null) &&
            !vm.DupICmd.CanRun(null) &&
            !vm.InsUp.CanRun(null) &&
            !vm.InsDown.CanRun(null),
            "Instruction edit commands must be disabled without a selected row.");

        vm.SelIns = vm.Ins[0];
        vm.SelIns.Address = "invalid";

        Assert(
            vm.SelIns.HasError,
            "Instruction validation state must expose whether the warning icon should be visible.");

        vm.SelIns.Address = "0x0001c310";
        vm.SelIns.Op = "load";

        Assert(
            vm.SelectedOperationHelp ==
            UiText.GetText("HCanary.Instructions.OperationHelp.Load"),
            "The selected operation must expose localized, user-facing guidance without a raw key.");

        vm.AddACmd.Execute(null);
        var firstGroup = vm.SelAnc!;

        Assert(
            vm.Anchors.Count == 1 &&
            firstGroup.Ins.Count == 1 &&
            !vm.AncUp.CanRun(null) &&
            !vm.AncDown.CanRun(null),
            "A new instruction group must contain one instruction and have disabled move commands until a row is selected.");

        vm.AddACmd.Execute(null);
        var secondGroup = vm.SelAnc!;
        vm.AncIns = secondGroup.Ins[0];
        vm.AncAdd.Execute(null);

        Assert(
            firstGroup.Ins.Count == 1 &&
            secondGroup.Ins.Count == 2 &&
            ReferenceEquals(vm.AncIns, secondGroup.Ins[1]),
            "Adding a group instruction must modify only the selected group and select the new row.");

        var movedGroupInstruction = vm.AncIns!;

        Assert(
            vm.AncUp.CanRun(null) &&
            !vm.AncDown.CanRun(null),
            "The last group instruction must move up but not down.");

        vm.AncUp.Execute(null);

        Assert(
            ReferenceEquals(secondGroup.Ins[0], movedGroupInstruction) &&
            !vm.AncUp.CanRun(null) &&
            vm.AncDown.CanRun(null),
            "Group Move Up must preserve selection and update boundary state.");

        vm.AncDown.Execute(null);

        Assert(
            ReferenceEquals(secondGroup.Ins[1], movedGroupInstruction),
            "Group Move Down must preserve the instruction order exactly.");

        vm.AncDel.Execute(null);

        Assert(
            secondGroup.Ins.Count == 1 &&
            ReferenceEquals(vm.AncIns, secondGroup.Ins[0]),
            "Deleting a group instruction must select the nearest remaining row.");

        var beforeLoad = vm.Ins.Count;
        vm.AncLoad.Execute(null);
        var load = vm.Ins[^1];

        Assert(
            vm.Ins.Count == beforeLoad + 1 &&
            load.Op == "load" &&
            load.Address == secondGroup.Name &&
            load.Value == string.Empty,
            "Use Group in Patch must add a load instruction with the selected group name and an empty value.");

        vm.SelAnc = null;

        Assert(
            !vm.AncLoad.CanRun(null) &&
            !vm.AncUp.CanRun(null) &&
            !vm.AncDown.CanRun(null),
            "Group load and move commands must be disabled without a selected group.");
    }

    private static void Ui_Lang()
    {
        var root = RepoRoot();
        var appRoot = Path.Combine(root, "App");

        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(
                Path.Combine(
                    appRoot,
                    "Lang",
                    "en-US.json")))!;

        var ar = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(
                Path.Combine(
                    appRoot,
                    "Lang",
                    "ar-SA.json")))!;

        Assert(
            en.Keys.Order().SequenceEqual(
                ar.Keys.Order()),
            "Arabic and English localization keys must match exactly.");

        Assert(
            en.Keys.Any(
                key => key.StartsWith(
                    "HCanary.Validation.",
                    StringComparison.Ordinal)),
            "H-Canary validation messages must be localized.");

        var xamlPaths = Directory
            .EnumerateFiles(
                appRoot,
                "*.xaml",
                SearchOption.AllDirectories)
            .ToArray();

        var xamlDocuments = xamlPaths
            .Select(
                path => new
                {
                    Path = path,
                    Text = File.ReadAllText(path)
                })
            .ToArray();

        var xamlKeys = xamlDocuments
            .SelectMany(
                document => Regex.Matches(
                    document.Text,
                    @"\{lang:TextExt\s+Key=([^},\s]+)",
                    RegexOptions.CultureInvariant)
                .Select(
                    match => match.Groups[1].Value));

        var codeKeys = Directory
            .EnumerateFiles(
                appRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(
                path => Regex.Matches(
                    File.ReadAllText(path),
                    @"""((?:Adv|HCanary)\.[A-Za-z0-9_.]+)""",
                    RegexOptions.CultureInvariant)
                .Select(
                    match => match.Groups[1].Value))
            .Where(
                key => !key.EndsWith('.'));

        var referencedKeys = xamlKeys
            .Concat(codeKeys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var missingArabic = referencedKeys
            .Where(
                key => !ar.ContainsKey(key))
            .ToArray();

        var missingEnglish = referencedKeys
            .Where(
                key => !en.ContainsKey(key))
            .ToArray();

        Assert(
            missingArabic.Length == 0,
            "Arabic localization is missing UI keys: " +
            FormatItems(missingArabic));

        Assert(
            missingEnglish.Length == 0,
            "English localization is missing UI keys: " +
            FormatItems(missingEnglish));

        var project = File.ReadAllText(
            Path.Combine(
                appRoot,
                "R3App.csproj"));

        Assert(
            project.Contains(
                "Script\\LangPack\\R3LangPack.csproj",
                StringComparison.Ordinal) &&
            project.Contains(
                "CompileR3LanguagePacks",
                StringComparison.Ordinal) &&
            project.Contains(
                "ar-SA.r3lang",
                StringComparison.Ordinal) &&
            project.Contains(
                "en-US.r3lang",
                StringComparison.Ordinal) &&
            project.Split(
                "ExcludeFromSingleFile=\"true\"",
                StringSplitOptions.None).Length - 1 >= 2 &&
            !project.Contains(
                "Lang\\ar-SA.json\" CopyToOutputDirectory",
                StringComparison.Ordinal) &&
            !project.Contains(
                "Lang\\en-US.json\" CopyToOutputDirectory",
                StringComparison.Ordinal),
            "Language sources must stay editable in source while build output ships compiled .r3lang packs only.");

        var xaml = ReadSources(
            Path.Combine(appRoot, "Ctrl", "HCan.xaml"),
            Path.Combine(appRoot, "Ctrl", "HSty.xaml"),
            Path.Combine(appRoot, "Ctrl", "HGam.xaml"),
            Path.Combine(appRoot, "Ctrl", "HTgt.xaml"),
            Path.Combine(appRoot, "Ctrl", "HHsh.xaml"),
            Path.Combine(appRoot, "Ctrl", "HDet.xaml"),
            Path.Combine(appRoot, "Ctrl", "HIns.xaml"),
            Path.Combine(appRoot, "Ctrl", "HRev.xaml"),
            Path.Combine(appRoot, "Ctrl", "HLch.xaml"),
            Path.Combine(appRoot, "Ctrl", "HBar.xaml"),
            Path.Combine(appRoot, "Ctrl", "HSav.xaml"));

        var hCanCode = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "HCan.xaml.cs"));

        var hGamXaml = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "HGam.xaml"));

        var gamesXaml = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "Games.xaml"));

        var hInsXaml = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "HIns.xaml"));

        var hInsCode = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "HIns.xaml.cs"));

        var advancedXaml = ReadSources(
            Path.Combine(appRoot, "View", "Adv.xaml"),
            Path.Combine(appRoot, "View", "ASty.xaml"),
            Path.Combine(appRoot, "View", "ARec.xaml"),
            Path.Combine(appRoot, "View", "ADiag.xaml"));

        var advancedCode = File.ReadAllText(
            Path.Combine(
                appRoot,
                "View",
                "Adv.xaml.cs"));

        Assert(
            !xaml.Contains(
                "CheckBox",
                StringComparison.OrdinalIgnoreCase),
            "H-Canary authoring view must not be an activation checklist.");

        Assert(
            hGamXaml.Contains(
                "VirtualizingPanel.ScrollUnit=\"Item\"",
                StringComparison.Ordinal) &&
            !hGamXaml.Contains(
                "VirtualizingPanel.ScrollUnit=\"Pixel\"",
                StringComparison.Ordinal) &&
            !hGamXaml.Contains(
                "local:Scrl.IsEnabled=\"True\"",
                StringComparison.Ordinal),
            "H-Canary game selection must use whole-item scrolling so rows cannot open partially clipped.");

        Assert(
            gamesXaml.Contains(
                "VirtualizingPanel.ScrollUnit=\"Item\"",
                StringComparison.Ordinal) &&
            !gamesXaml.Contains(
                "VirtualizingPanel.ScrollUnit=\"Pixel\"",
                StringComparison.Ordinal) &&
            !gamesXaml.Contains(
                "local:Scrl.IsEnabled=\"True\"",
                StringComparison.Ordinal),
            "The main game list must use whole-item scrolling so game names stay aligned and readable.");

        Assert(
            hGamXaml.Contains(
                "Text=\"{Binding Title}\"",
                StringComparison.Ordinal) &&
            hGamXaml.Contains(
                "ToolTip=\"{Binding Title}\"",
                StringComparison.Ordinal) &&
            !hGamXaml.Contains(
                "{Binding Game.Title}",
                StringComparison.Ordinal),
            "H-Canary must display the resolved GameVm title, including the exact-serial catalog fallback, instead of the raw scanner title.");

        Assert(
            xaml.Contains(
                "HCanary.Step.Game",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Step.Targets",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Step.Target",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Step.Details",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Step.Instructions",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Step.Review",
                StringComparison.Ordinal),
            "H-Canary must expose the six-step authoring workflow.");

        Assert(
            xaml.Contains(
                "HCanary.Start.Game.Title",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Start.Hash.Title",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Start.Game.Empty",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "x:Name=\"GameSearchBox\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Launch.Action",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "Command=\"{Binding HCanary.LaunchCmd}\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "SelectedItem=\"{Binding GameList.SelGame",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "CanAdvanceGame",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "CanAdvanceTargets",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "CanAdvanceFingerprint",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "UpdateNavigationState",
                StringComparison.Ordinal),
            "H-Canary must explain where to start and progressively reveal game and Hash evidence.");

        Assert(
            advancedXaml.Contains(
                "<controls:HCanView",
                StringComparison.Ordinal) &&
            advancedXaml.Contains(
                "x:Name=\"HCanaryWorkspace\"",
                StringComparison.Ordinal) &&
            advancedXaml.Contains(
                "x:Name=\"AdvancedNavigation\"",
                StringComparison.Ordinal) &&
            advancedXaml.Contains(
                "x:Name=\"RecoveryWorkspace\"",
                StringComparison.Ordinal) &&
            advancedXaml.Contains(
                "x:Name=\"DiagnosticsWorkspace\"",
                StringComparison.Ordinal) &&
            !advancedXaml.Contains(
                "Key=Adv.OpenHCanary",
                StringComparison.Ordinal) &&
            !advancedCode.Contains(
                "OpenHCanary",
                StringComparison.Ordinal) &&
            !advancedCode.Contains(
                "BackToTools",
                StringComparison.Ordinal) &&
            !advancedCode.Contains(
                "new HCanDlg",
                StringComparison.Ordinal) &&
            !advancedCode.Contains(
                "new HCanVm",
                StringComparison.Ordinal) &&
            !hCanCode.Contains(
                "new HCanVm",
                StringComparison.Ordinal),
            "Advanced Tools must open directly on the existing HCanVm workspace and expose recovery and diagnostics through one navigation rail.");

        Assert(
            xaml.Contains(
                "x:Name=\"SavedDrawer\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "x:Name=\"TargetsStep\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "x:Name=\"AnchorsPanel\"",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "ShowSaved",
                StringComparison.Ordinal) &&
            !hCanCode.Contains(
                "HCanSavedDlg",
                StringComparison.Ordinal) &&
            !hCanCode.Contains(
                "HCanTargetsDlg",
                StringComparison.Ordinal) &&
            !hCanCode.Contains(
                "HCanAnchorsDlg",
                StringComparison.Ordinal),
            "Saved patches, target versions, and advanced references must remain available inline without reopening dedicated dialogs.");

        Assert(
            xaml.Contains(
                "x:Name=\"NextButton\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "Command=\"{Binding HCanary.OpenCmd}\"",
                StringComparison.Ordinal) &&
            hCanCode.Contains(
                "CanAdvanceThrough",
                StringComparison.Ordinal),
            "H-Canary navigation and saved-patch actions must visibly enforce their CanExecute state.");

        Assert(
            Regex.Count(
                hInsXaml,
                "ColumnHeaderHeight=\\\"36\\\"",
                RegexOptions.CultureInvariant) == 2 &&
            Regex.Count(
                hInsXaml,
                "(?<!Min)RowHeight=\\\"40\\\"",
                RegexOptions.CultureInvariant) == 2 &&
            hInsXaml.Contains(
                "Width=\"150\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Width=\"1.2*\"",
                StringComparison.Ordinal),
            "Both instruction tables must use the approved 36-pixel headers, 40-pixel rows, and responsive column contract.");

        Assert(
            Regex.Count(
                hInsXaml,
                "Style=\\\"{StaticResource HInsInfoButton}\\\"",
                RegexOptions.CultureInvariant) == 8 &&
            Regex.Count(
                hInsXaml,
                "Click=\\\"OpenHelp_Click\\\"",
                RegexOptions.CultureInvariant) == 8 &&
            Regex.Count(
                hInsXaml,
                "AutomationProperties.Name=",
                RegexOptions.CultureInvariant) >= 12 &&
            hInsCode.Contains(
                "menu.IsOpen = true",
                StringComparison.Ordinal) &&
            !hInsXaml.Contains(
                "StaysOpen=\"False\"",
                StringComparison.Ordinal),
            "Every column-help and icon-only action must expose accessible names, tooltips, and light-dismiss help without an unsupported ToolTip lifetime.");

        Assert(
            !hInsXaml.Contains(
                "<WrapPanel",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.AddICmd}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.DupICmd}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.DelICmd}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.InsUp}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.InsDown}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.AncUp}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Command=\"{Binding HCanary.AncDown}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "Key=HCanary.LoadAnchorTip",
                StringComparison.Ordinal),
            "Instruction actions must use the grouped command bars and real move/load commands rather than text-only placeholder controls.");

        Assert(
            hInsXaml.Contains(
                "FlowDirection=\"{DynamicResource App.FlowDirection}\"",
                StringComparison.Ordinal) &&
            hInsXaml.Contains(
                "FlowDirection=\"{DynamicResource TechnicalText.FlowDirection}\"",
                StringComparison.Ordinal) &&
            !hInsXaml.Contains(
                "TextAlignment=\"Right\"",
                StringComparison.Ordinal) &&
            !hInsXaml.Contains(
                "FlowDirection=\"LeftToRight\"",
                StringComparison.Ordinal) &&
            !hInsXaml.Contains(
                "FlowDirection=\"RightToLeft\"",
                StringComparison.Ordinal),
            "The instruction redesign must preserve application and technical direction resources without introducing literal text directions.");

        var hashView = File.ReadAllText(
            Path.Combine(
                appRoot,
                "Ctrl",
                "HHsh.xaml"));
        var hashCode = ReadSources(
            Path.Combine(appRoot, "Application", "HashEvidenceUseCase.cs"),
            Path.Combine(appRoot, "Svc", "HDis.cs"),
            Path.Combine(appRoot, "Vm", "HCan.cs"),
            Path.Combine(appRoot, "Vm", "HCan.Evi.cs"),
            Path.Combine(appRoot, "Vm", "HCan.Io.cs"),
            Path.Combine(root, "Core", "Svc", "Patch", "HCan.cs"));

        Assert(
            hashView.Contains(
                "Text=\"{Binding HCanary.HashHead}\"",
                StringComparison.Ordinal) &&
            hashCode.Contains(
                "HCanary.Hash.Saved",
                StringComparison.Ordinal) &&
            hashCode.Contains(
                "HCanary.Hash.Success",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "Key=HCanary.Hash.Rediscover",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "Key=HCanary.Hash.Delete",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "Key=HCanary.Hash.Technical",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "HCanary.PpuHashCount",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "HCanary.SpuHashCount",
                StringComparison.Ordinal) &&
            hashView.Contains(
                "ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"",
                StringComparison.Ordinal) &&
            !hashView.Contains(
                "HashDetectedTime",
                StringComparison.Ordinal) &&
            !hashView.Contains(
                "HashBindingState",
                StringComparison.Ordinal) &&
            !hashView.Contains(
                "Text=\"{Binding Source}\"",
                StringComparison.Ordinal) &&
            !hashView.Contains(
                "Key=HCanary.Hash.Summary.Game",
                StringComparison.Ordinal) &&
            !hashView.Contains(
                "Key=HCanary.Hash.Summary.Version",
                StringComparison.Ordinal),
            "H-Canary fingerprint UI must distinguish previously saved fingerprints from a fresh extraction while showing only PPU/SPU counts and collapsed values without source, date, binding state, or repeated game metadata.");

        Assert(
            hashCode.Contains(
                @"(?<kind>PPU|SPU) executable hash:",
                StringComparison.Ordinal) &&
            hashCode.Contains(
                @"(?<prefix>PPU|SPU)-",
                StringComparison.Ordinal) &&
            hashCode.Contains(
                "LogNotChanged",
                StringComparison.Ordinal) &&
            hashCode.Contains(
                "SelectedGameRunning",
                StringComparison.Ordinal) &&
            !hashCode.Contains(
                @"ppu\cache",
                StringComparison.OrdinalIgnoreCase) &&
            !hashCode.Contains(
                "RPCS3.log.gz",
                StringComparison.OrdinalIgnoreCase) &&
            !hashView.Contains(
                "similar",
                StringComparison.OrdinalIgnoreCase) &&
            !hashView.Contains(
                "تشابه",
                StringComparison.Ordinal),
            "H-Canary discovery must read exact PPU/SPU markers from one new plain log session without cache fallback, archived-log merging, or similarity language.");

        Assert(
            xaml.Contains(
                "HCanary.Instructions",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Anchors",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "HCanary.Preview",
                StringComparison.Ordinal),
            "H-Canary view must expose instructions, advanced references, and true YAML preview editing.");

        Assert(
            xaml.Contains(
                "x:Key=\"HCanTechnicalTextBlock\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "BasedOn=\"{StaticResource TechnicalTextBlock}\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "x:Key=\"HCanTechnicalTextBox\"",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "Text=\"{Binding HCanary.Preview",
                StringComparison.Ordinal) &&
            xaml.Contains(
                "Style=\"{StaticResource HCanTechnicalTextBox}\"",
                StringComparison.Ordinal),
            "Technical YAML content must remain LTR and left aligned.");

        Assert(
            !xaml.Contains(
                "Enable Code",
                StringComparison.OrdinalIgnoreCase) &&
            !xaml.Contains(
                "AnalysisItems",
                StringComparison.Ordinal),
            "The obsolete patch-browser and compatibility-analysis concepts must be absent.");
    }

    private static string[] FileState(string root)
    {
        return Directory
            .EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(
                path =>
                {
                    var relative = Path
                        .GetRelativePath(
                            root,
                            path)
                        .Replace(
                            '\\',
                            '/');

                    var bytes = File.ReadAllBytes(path);

                    return $"{relative}|{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))}";
                })
            .ToArray();
    }

    private sealed class FakeHashHarness
    {
        public HCanDiscoverySession? Active { get; private set; }

        public HashRead Saved { get; set; } = new(
            true,
            string.Empty,
            [],
            Code: HashReadCode.NoMatch);

        public HashReadCode RunningCode { get; set; } = HashReadCode.NoMatch;

        public HCanHashOps Ops => new(
            Begin,
            Attach,
            Cancel,
            (_, _) => RunningCode,
            (_, _) => Saved,
            (_, _) => Saved,
            (_, _) =>
            {
                var existed = Saved.Code == HashReadCode.Found;
                Saved = new HashRead(
                    true,
                    string.Empty,
                    [],
                    Code: HashReadCode.NoMatch);
                return existed;
            });

        private HCanDiscoverySession Begin(
            Rpcs3InstallDescriptor install,
            Rpcs3GameDescriptor game)
        {
            var dataRoot = install.DataRoot ?? install.RootPath;
            Active = new HCanDiscoverySession(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath,
                0,
                DateTime.UtcNow,
                Path.Combine(dataRoot, "log", "RPCS3.log"),
                new HCanLogMark(false, 0, null, string.Empty));
            return Active;
        }

        private void Attach(
            HCanDiscoverySession session,
            int processId)
        {
            Active = session with
            {
                ProcessId = processId
            };
        }

        private void Cancel(HCanDiscoverySession session)
        {
            if (Active?.StartedUtc == session.StartedUtc)
            {
                Active = null;
            }
        }
    }

    private sealed class FakeProcessProbe : IRpcs3ProcessProbe
    {
        public bool Running { get; set; }

        public Rpcs3ProcessState Find(
            string executablePath,
            int? expectedProcessId = null)
        {
            return Running
                ? new Rpcs3ProcessState(true, expectedProcessId ?? 4242)
                : new Rpcs3ProcessState(false);
        }
    }

    private sealed class FakeCaptureProcessController
        : IRpcs3OwnedProcessController
    {
        public bool Running { get; private set; } = true;

        public int AttachCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int StartupReadyChecks { get; private set; }

        public int StartupReadyAfterChecks { get; init; } = 1;

        public int LogReadyAfterChecks { get; init; } = 1;

        public Action? OnStartupReady { get; init; }

        public Action? BeforeStop { get; init; }

        private bool StartupReadyNotified { get; set; }

        public bool TryAttach(
            string executablePath,
            int processId,
            out DateTime processStartedUtc)
        {
            AttachCalls++;
            processStartedUtc = DateTime.UtcNow;
            return Running && processId > 0;
        }

        public bool IsRunning(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return Running;
        }

        public bool IsGameStartupReady(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            StartupReadyChecks++;
            var ready = Running &&
                        StartupReadyChecks >= StartupReadyAfterChecks;
            if (ready &&
                StartupReadyChecks >= LogReadyAfterChecks &&
                !StartupReadyNotified)
            {
                StartupReadyNotified = true;
                OnStartupReady?.Invoke();
            }

            return ready;
        }

        public Task<Rpcs3OwnedProcessStopResult> StopAsync(
            string executablePath,
            int processId,
            DateTime processStartedUtc,
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken)
        {
            StopCalls++;
            BeforeStop?.Invoke();
            Running = false;
            return Task.FromResult(
                new Rpcs3OwnedProcessStopResult(
                    Stopped: true));
        }
    }

    private sealed class FakeGameProcessLauncher : IRpcs3GameProcessLauncher
    {
        public string? ExecutablePath { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public string? BootTarget { get; private set; }

        public int StartCalls { get; private set; }

        public int Start(
            string executablePath,
            string workingDirectory,
            string bootTarget)
        {
            StartCalls++;
            ExecutablePath = executablePath;
            WorkingDirectory = workingDirectory;
            BootTarget = bootTarget;
            return 4242;
        }
    }

    private static HCanDrft GoodDrft() => new()
    {
        PatType = HCanType.Ppu,
        ExeHash = "PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9",
        PatName = "Reduce input lag",
        Author = "WAZAAAAA",
        Notes = "Reduce input delay.",
        PatVer = "1.0",
        Games =
        [
            new HCanTgt
            {
                GameName = "TEKKEN TAG TOURNAMENT 2",
                GameId = "NPUB30899",
                GameVer = "01.01"
            }
        ],
        Ins =
        [
            new HCanInst
            {
                Op = "be32",
                Address = "0x0001c310",
                Value = "0x39200000"
            }
        ]
    };

    private static void AssertOperationContract(
        IEnumerable<string> actualOperations)
    {
        var actual = actualOperations.ToArray();

        if (actual.SequenceEqual(
                Rpcs3AuthoringOperations,
                StringComparer.Ordinal))
        {
            return;
        }

        var missing = Rpcs3AuthoringOperations
            .Except(
                actual,
                StringComparer.Ordinal)
            .ToArray();

        var unexpected = actual
            .Except(
                Rpcs3AuthoringOperations,
                StringComparer.Ordinal)
            .ToArray();

        var duplicates = actual
            .GroupBy(
                operation => operation,
                StringComparer.Ordinal)
            .Where(
                group => group.Count() > 1)
            .Select(
                group => group.Key)
            .ToArray();

        var firstMismatch = -1;

        var comparedLength = Math.Min(
            actual.Length,
            Rpcs3AuthoringOperations.Length);

        for (var index = 0; index < comparedLength; index++)
        {
            if (!string.Equals(
                    actual[index],
                    Rpcs3AuthoringOperations[index],
                    StringComparison.Ordinal))
            {
                firstMismatch = index;
                break;
            }
        }

        var mismatch = firstMismatch >= 0
            ? $"First order mismatch at index {firstMismatch}: expected '{Rpcs3AuthoringOperations[firstMismatch]}', actual '{actual[firstMismatch]}'."
            : actual.Length == Rpcs3AuthoringOperations.Length
                ? "The operation order differs."
                : $"Operation count differs: expected {Rpcs3AuthoringOperations.Length}, actual {actual.Length}.";

        throw new InvalidOperationException(
            "The H-Canary authoring operation contract does not match RPCS3. " +
            mismatch +
            $" Missing: {FormatItems(missing)}." +
            $" Unexpected: {FormatItems(unexpected)}." +
            $" Duplicates: {FormatItems(duplicates)}." +
            $" Expected: [{string.Join(", ", Rpcs3AuthoringOperations)}]." +
            $" Actual: [{string.Join(", ", actual)}].");
    }

    private static void AssertCrLfOnly(
        string value,
        string message)
    {
        var remainder = value.Replace(
            "\r\n",
            string.Empty,
            StringComparison.Ordinal);

        Assert(
            !remainder.Contains('\r') &&
            !remainder.Contains('\n'),
            message);
    }

    private static string FormatItems(
        IEnumerable<string> items)
    {
        var values = items.ToArray();

        return values.Length == 0
            ? "<none>"
            : string.Join(
                ", ",
                values);
    }

    private static void HasIssue(
        HCanaryPatchAuthoringService service,
        HCanDrft valid,
        Action<HCanDrft> mutate,
        string code)
    {
        var draft = valid.Clone();
        mutate(draft);

        Assert(
            service.Validate(draft).Issues.Any(
                issue => issue.Code == code),
            "Expected validation issue " + code);
    }

    private static string NewTemp()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "R3Config-HCan-" +
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return path;
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(
            Environment.CurrentDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "R3Cfg.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "R3Config repository root was not found.");
    }

    private static void MustFail<T>(
        Action action,
        string message)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static string ReadSources(params string[] paths)
    {
        return string.Join(
            Environment.NewLine,
            paths.Select(File.ReadAllText));
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
