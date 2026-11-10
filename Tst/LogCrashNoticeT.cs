using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using R3Config;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// The main-window crash notice of Log Diagnostics (Domain 5). It appears only
// for a rule-backed Crash or FatalError event in the diagnosed session of the
// selected game, never for another game's log or a missing GameExit, and only
// until the user opens or dismisses it for that exact session. The
// acknowledgement lives in R3Config's own settings (temp paths only here).
internal static class LogCrashNoticeT
{
    private const string Serial = "BLUS90061";
    private const string OtherSerial = "BLUS90062";

    public static void Run(string root)
    {
        var dir = Path.Combine(root, "CrashNotice");
        Directory.CreateDirectory(dir);
        AssertIdentity(dir);
        AssertSettings(dir);
        AssertViewModel(dir).GetAwaiter().GetResult();
        Console.WriteLine("Log crash notice (rule-backed crash, selected serial only, acknowledged session, settings round-trip) PASS.");
    }

    // ------------------------------------------------------------- fixtures

    internal static string Line(char level, string time, string channel, string message) =>
        $"\u00B7{level} {time} {channel}: {message}";

    internal static List<string> Log(string serial, params string[] tail)
    {
        var lines = new List<string>
        {
            "\uFEFFRPCS3 v0.0.42-19900-abcdef12 Alpha | master",
            "Architecture: x64",
            "Test CPU | 8 Threads | 16.00 GiB RAM | TSC: 1.000GHz | AVX+ | FMA3",
            "Operating system: Windows, Major: 10, Minor: 0, Build: 12345, Service Pack: none",
            Line('!', "0:00:21.066397", "SYS", $"Emulator::BootGame: path='X:/TestGames/{serial}', title_id='{serial}', direct=0"),
            Line('!', "0:00:21.073531", "SYS", "Title: Synthetic Game"),
            Line('!', "0:00:21.073532", "SYS", $"Serial: {serial}"),
            Line('!', "0:00:21.073535", "SYS", "Version: APP_VER=01.00 VERSION=01.00"),
            Line('S', "0:00:22.000000", "GUI", "Boot successful.")
        };
        lines.AddRange(tail);
        return lines;
    }

    internal static string Fatal(string time = "0:00:30.000000") =>
        Line('F', time, "SIG", "Thread terminated due to fatal error: Access violation reading location 0x0");

    internal static string AfterCrash(string time) =>
        Line('E', time, "RSX", "Synthetic line written after the crash");

    private static string CleanExit() =>
        Line('!', "0:00:40.000000", "SYS", "Quit with main_window::closeEvent. (autoexit=0)");

    private static void WriteLog(string install, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Path.Combine(install, "log"));
        File.WriteAllText(Path.Combine(install, "log", "RPCS3.log"), string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
    }

    private static string? Identify(string install, string? serial)
    {
        var descriptor = new Rpcs3InstallDescriptor(install, Path.Combine(install, "rpcs3.exe"), Path.Combine(install, "config"),
            Path.Combine(install, "config", "custom"), Path.Combine(install, "patches"), Path.Combine(install, "config", "patch_config.yml"), null);
        var result = new LogDiagnosticsUseCase().Analyze(descriptor, serial, null, null, DiagnosticKnowledgeBase.Empty);
        return LogCrashNotice.Identify(result.Report, serial);
    }

    // ------------------------------------------------------------ core rule

    private static void AssertIdentity(string root)
    {
        var install = Path.Combine(root, "identity");
        WriteLog(install, Log(Serial, Fatal()));
        var crash = Identify(install, Serial);
        Assert(crash is not null && crash.StartsWith("sha256:", StringComparison.Ordinal),
            "A rule-backed fatal crash in the selected game's session must yield its session identity (a SHA-256 of the crashing session).");
        Assert(Identify(install, Serial.ToLowerInvariant()) == crash,
            "The same log read again must yield the same session identity.");
        Assert(Identify(install, OtherSerial) is null,
            "A crash in another game's log must never yield a notice for the selected game.");
        Assert(Identify(install, null) is null,
            "Without a selected game there is no notice.");

        WriteLog(install, Log(Serial, Fatal(), AfterCrash("0:00:35.000000"), AfterCrash("0:00:36.500000")));
        Assert(Identify(install, Serial) == crash,
            "Lines RPCS3 appends to the same session after the crash must keep its identity, so a dismissed crash stays dismissed.");

        WriteLog(install, Log(Serial, Line('E', "0:00:30.000000", "PPU", "Unhandled exception in PPU thread"), CleanExit()));
        var errorCrash = Identify(install, Serial);
        Assert(errorCrash is not null && errorCrash != crash,
            "An error-level crash line is a crash even when RPCS3 later exits, and a new log is a new session.");

        WriteLog(install, Log(Serial, CleanExit()));
        Assert(Identify(install, Serial) is null, "A clean GameExit session is not a crash.");

        WriteLog(install, Log(Serial));
        Assert(Identify(install, Serial) is null, "A missing GameExit is never read as a crash.");

        WriteLog(install, Log(Serial, Line('E', "0:00:24.000000", "RSX", "nvNe::semaphore_acquire has timed out. semaphore_address=0x1234")));
        Assert(Identify(install, Serial) is null, "A problem that is not Crash or FatalError raises no crash notice.");

        // The current log is another game's crash; the rotated log holds the
        // selected game's clean session, which is the one diagnosed.
        WriteLog(install, Log(OtherSerial, Fatal()));
        using (var file = File.Create(Path.Combine(install, "log", "RPCS3.log.gz")))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, Log(Serial, CleanExit()))));
        }

        Assert(Identify(install, Serial) is null && Identify(install, OtherSerial) is not null,
            "Only the selected game's own session decides; another game's crash in the newer log does not leak into it.");
    }

    // ------------------------------------------------------------- settings

    private static void AssertSettings(string root)
    {
        var path = Path.Combine(root, "settings", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var exe = Path.GetFullPath(Path.Combine(root, "rpcs3", "rpcs3.exe"));
        File.WriteAllText(path, $"{{\n  \"LastRpcs3ExePath\": {System.Text.Json.JsonSerializer.Serialize(exe)}\n}}", new UTF8Encoding(false));

        var service = new SetSvc(path);
        var old = service.Load();
        Assert(old.LastExe == exe && old.SeenLogCrash(Serial) is null,
            "A settings.json written before the crash acknowledgement field must still load its RPCS3 path, with nothing acknowledged.");

        service.Update(current => current.WithSeenLogCrash(Serial.ToLowerInvariant(), "sha256:AA"));
        service.Update(current => current.WithSeenLogCrash(OtherSerial, "sha256:BB"));
        var loaded = new SetSvc(path).Load();
        Assert(loaded.LastExe == exe && loaded.SeenLogCrash(Serial) == "sha256:AA" && loaded.SeenLogCrash(OtherSerial) == "sha256:BB",
            "Acknowledged crash sessions round-trip per serial and keep the RPCS3 path.");

        service.Update(current => current.WithSeenLogCrash(Serial, "sha256:CC"));
        service.Update(current => current with { LastExe = null });
        loaded = service.Load();
        Assert(loaded.LastExe is null && loaded.SeenLogCrash(Serial) == "sha256:CC" && loaded.SeenLogCrash(OtherSerial) == "sha256:BB" &&
               loaded.SeenLogCrashes!.Count == 2,
            "A newer acknowledgement replaces the serial's older one, and saving the RPCS3 path keeps the acknowledgements.");

        File.WriteAllText(path,
            "{ \"LastRpcs3ExePath\": null, \"SeenLogCrashes\": [ { \"Serial\": \" \", \"Session\": \"sha256:DD\" }, " +
            "{ \"Serial\": \"BLUS90063\", \"Session\": \"  \" }, { \"Serial\": \"BLUS/../1\", \"Session\": \"sha256:EE\" }, " +
            "{ \"Serial\": \"BLUS90064\", \"Session\": \"sha256:FF\" } ] }",
            new UTF8Encoding(false));
        loaded = service.Load();
        Assert(loaded.SeenLogCrashes is { Count: 1 } && loaded.SeenLogCrash("BLUS90064") == "sha256:FF",
            "Malformed acknowledgement entries are dropped on load.");
    }

    // ------------------------------------------------------------ view model

    private static async Task AssertViewModel(string root)
    {
        var install = Path.Combine(root, "vm-rpcs3");
        var exe = Rpcs3(install, Serial, OtherSerial);
        var settingsPath = Path.Combine(root, "vm-settings", "settings.json");
        WriteLog(install, Log(Serial, Fatal()));

        var vm = await Analyze(exe, settingsPath, Serial);
        Assert(vm.CrashNotice.IsVisible && vm.CrashNotice.Serial == Serial && vm.CrashNotice.Session is not null,
            "A new rule-backed crash in the selected game's latest session shows the crash notice.");
        Assert(new SetSvc(settingsPath).Load().SeenLogCrash(Serial) is null,
            "Showing the notice must not mark the session as seen.");
        var session = vm.CrashNotice.Session;
        var diagRequests = 0;
        vm.DiagReq += (_, _) => diagRequests++;
        vm.Runtime.LogDiag.SelectedTab = LogDiagVm.TabLogs;
        vm.CrashNotice.OpenCmd.Execute(null);
        Assert(!vm.CrashNotice.IsVisible && diagRequests == 1 && vm.Runtime.LogDiag.SelectedTab == LogDiagVm.TabOverview &&
               new SetSvc(settingsPath).Load().SeenLogCrash(Serial) == session,
            "Open diagnosis requests the Runtime tab on the Overview, hides the notice and stores the session as seen.");
        vm.Dispose();

        vm = await Analyze(exe, settingsPath, Serial);
        Assert(!vm.CrashNotice.IsVisible && vm.Runtime.LogDiag.HasProblems,
            "The same acknowledged crash session never shows the notice again.");
        vm.Dispose();

        WriteLog(install, Log(Serial, Fatal(), AfterCrash("0:00:35.000000")));
        vm = await Analyze(exe, settingsPath, Serial);
        Assert(!vm.CrashNotice.IsVisible && vm.Runtime.LogDiag.HasProblems,
            "The acknowledged crash stays acknowledged after RPCS3 appends more lines to the same session.");
        vm.Dispose();

        WriteLog(install, Log(Serial, Fatal("0:00:31.000000")));
        vm = await Analyze(exe, settingsPath, Serial);
        Assert(vm.CrashNotice.IsVisible && vm.CrashNotice.Session != session,
            "A newer crash session of the same game shows the notice again.");
        var newer = vm.CrashNotice.Session;
        var diagRequestsAfterDismiss = 0;
        vm.DiagReq += (_, _) => diagRequestsAfterDismiss++;
        vm.Runtime.LogDiag.SelectedTab = LogDiagVm.TabLogs;
        vm.CrashNotice.DismissCmd.Execute(null);
        Assert(!vm.CrashNotice.IsVisible && diagRequestsAfterDismiss == 0 && vm.Runtime.LogDiag.SelectedTab == LogDiagVm.TabLogs &&
               new SetSvc(settingsPath).Load().SeenLogCrash(Serial) == newer,
            "Dismiss hides the notice, switches nothing and stores the session as seen.");
        vm.Dispose();

        vm = await Analyze(exe, settingsPath, OtherSerial);
        Assert(!vm.CrashNotice.IsVisible && vm.Runtime.LogDiag.IsReady,
            "Another game's crash log never shows the notice for the selected game.");
        vm.Dispose();

        WriteLog(install, Log(Serial, CleanExit()));
        vm = await Analyze(exe, Path.Combine(root, "vm-settings-clean", "settings.json"), Serial);
        Assert(!vm.CrashNotice.IsVisible && vm.Runtime.LogDiag.IsReady,
            "A clean GameExit session shows no notice even with nothing acknowledged.");
        vm.Dispose();
    }

    private static async Task<MainVm> Analyze(string exe, string settingsPath, string serial)
    {
        var settings = new SetSvc(settingsPath);
        settings.Update(current => current with { LastExe = exe });
        var vm = new MainVm(
            new AppRuntimeServices(new AppFlow(AppComposition.Build(new Rpcs3MutationGuard(() => false))), settings),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        await Until(() => vm.GameList.LibraryScanState == GameLibraryScanState.Completed && vm.GameList.Games.Count == 2,
            "The crash-notice RPCS3 fixture library scan did not complete.");
        vm.GameList.SelGame = vm.GameList.Games.Single(game => game.Serial == serial);
        var analyze = typeof(MainVm).GetMethod("DoAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainVm.DoAsync was not found.");
        await (Task)analyze.Invoke(vm, null)!;
        await Until(() => vm.Runtime.LogDiag.IsReady || vm.Runtime.LogDiag.State == LogDiagState.NoLog,
            "The automatic log diagnosis of the selected game did not finish.");
        return vm;
    }

    private static string Rpcs3(string root, params string[] serials)
    {
        Directory.CreateDirectory(Path.Combine(root, "portable", "config"));
        Directory.CreateDirectory(Path.Combine(root, "portable", "patches"));
        foreach (var serial in serials)
        {
            var game = Path.Combine(root, "portable", "dev_hdd0", "game", serial);
            Directory.CreateDirectory(game);
            SfoWrite(Path.Combine(game, "PARAM.SFO"), new Dictionary<string, string>
            {
                ["TITLE"] = $"Crash Notice {serial}",
                ["TITLE_ID"] = serial,
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });
        }

        var exe = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exe, []);
        return exe;
    }

    private static async Task Until(Func<bool> predicate, string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new InvalidOperationException(message);
            }

            await Task.Delay(25);
        }
    }
}
