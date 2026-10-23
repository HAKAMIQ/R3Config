using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

// Log Diagnostics (Domain 5): the read-only log parser, the deterministic
// signatures, the diagnosis rules and the shipped knowledge base. Fixtures are
// synthetic but use the exact line shapes of real RPCS3 logs.
internal static class LogDiagT
{
    private const string Ppu = "38656a143a665d48c38ae914e7c826d059efd291";

    public static void Run(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "LogDiag");
        Directory.CreateDirectory(root);

        AssertParser();
        AssertPendingAndMultiBoot();
        AssertSignatures();
        AssertBlackScreenIndicator();
        AssertLocatorAndReadOnly(root);
        AssertEngine();
        AssertUseCase(root);
        AssertShippedKnowledgeBase();
        AssertNoDevelopmentDependency();

        Console.WriteLine("Log Diagnostics (parser, timeline, signatures, diagnosis rules, read-only locator, shipped knowledge base) PASS.");
    }

    // ------------------------------------------------------------- fixtures

    private static string Line(char level, string time, string channel, string message, string? thread = null) =>
        $"\u00B7{level} {time} {(thread is null ? string.Empty : "{" + thread + "} ")}{channel}: {message}";

    private static List<string> Header(int build = 19900, string commit = "abcdef12") =>
    [
        $"\uFEFFRPCS3 v0.0.42-{build}-{commit} Alpha | master",
        "Architecture: x64",
        "12th Gen Intel(R) Core(TM) i5-12600KF | 16 Threads | 31.82 GiB RAM | TSC: 3.686GHz | AVX+ | FMA3",
        "Operating system: Windows, Major: 10, Minor: 0, Build: 26200, Service Pack: none",
        "Qt version: Compiled against Qt 6.11.2 | Run-time uses Qt 6.11.2",
        "Current Time: 2026-09-18T20:18:15"
    ];

    private static List<string> Boot(string serial = "TEST00001", string appVer = "01.05", bool renderer = true)
    {
        var lines = new List<string>
        {
            Line('A', "0:00:00.121256", "RSX", "Found Vulkan-compatible GPU: 'NVIDIA GeForce RTX 3060' running on driver 616.64.0.0", "Vulkan Device Enumeration Thread"),
            Line('!', "0:00:21.066397", "SYS", $"Emulator::BootGame: path='D:/games/{serial}', title_id='{serial}', direct=0"),
            Line('!', "0:00:21.073531", "SYS", "Title: Synthetic Game"),
            Line('!', "0:00:21.073532", "SYS", $"Serial: {serial}"),
            Line('!', "0:00:21.073534", "SYS", "Category: DG"),
            Line('!', "0:00:21.073535", "SYS", $"Version: APP_VER={appVer} VERSION={appVer}"),
            Line('!', "0:00:21.076292", "SYS", "Used configuration:"),
            "Core:",
            "  PPU Decoder: Recompiler (LLVM)",
            "Video:",
            "  Renderer: Vulkan",
            "  Resolution Scale: 100",
            "  Vulkan:",
            "    Adapter: NVIDIA GeForce RTX 3060",
            Line('!', "0:00:21.212410", "ppu_loader", $"PPU executable hash: PPU-{Ppu} (<- 1234)")
        };
        if (renderer)
        {
            lines.Add(Line('!', "0:00:21.305877", "RSX", "Vulkan: Renderer initialized on device 'NVIDIA GeForce RTX 3060'"));
        }

        lines.Add(Line('S', "0:00:22.000000", "GUI", "Boot successful."));
        return lines;
    }

    private static LogSession Parse(IEnumerable<string> lines, string? serial = null) =>
        Rpcs3LogParser.Parse(lines, null, serial);

    // ---------------------------------------------------------------- tests

    private static void AssertParser()
    {
        var lines = Header();
        lines.AddRange(Boot());
        lines.AddRange(
        [
            Line('W', "0:00:23.000000", "sys_fs", "sys_fs_fstat(fd=1, sb=*0x1)"),
            Line('E', "0:00:24.000000", "RSX", "nvNe::semaphore_acquire has timed out. semaphore_address=0x1234"),
            Line('E', "0:00:25.000000", "sys_fs", "'sys_fs_open' failed with 0x8001002c : CELL_ENOENT, \"/dev_hdd0/game/TEST00001/USRDIR/a.bin\" [1]"),
            Line('E', "0:00:26.000000", "sys_fs", "'sys_fs_open' failed with 0x8001002c : CELL_ENOENT, \"/dev_hdd0/game/TEST00001/USRDIR/b.bin\" [2]"),
            Line('E', "0:00:26.500000", "CFG", "cfg::try_to_enum_value('X', 'Y'): invalid enum or integer"),
            Line('E', "0:00:26.600000", "SIG", "Thread [rsx::thread] is too sleepy. Waiting for it 19445.582us already!"),
            Line('F', "0:00:27.000000", "SIG", "Thread terminated due to fatal error: Failed to compile fragment shader", "Thread (main_thread)"),
            "RSX: fragment shader dump follows",
            "  line two of the dump",
            Line('!', "0:00:28.000000", "SYS", "Quit with main_window::closeEvent. (autoexit=0)")
        ]);

        var session = Parse(lines);
        Assert(session.Build is { VersionText: "0.0.42", BuildNumber: 19900, Commit: "abcdef12", Branch: "master" } &&
               session.CpuLine!.Contains("i5-12600KF", StringComparison.Ordinal) &&
               session.OsLine!.Contains("Build: 26200", StringComparison.Ordinal),
            "The log header supplies the build identity, CPU and OS lines.");
        Assert(session.GameSerial == "TEST00001" && session.GameTitle == "Synthetic Game" && session.AppVersion == "01.05" &&
               session.GameCategory == "DG" && session.PpuHashes.SequenceEqual([Ppu]) &&
               session.Renderer == "Vulkan" && session.GpuName == "NVIDIA GeForce RTX 3060",
            "Serial, title, APP_VER, category, PPU hash, renderer and GPU come from the log's own lines.");
        Assert(session.Settings.Any(item => item is { Path: "Video/Renderer", Value: "Vulkan" }) &&
               session.Settings.Any(item => item is { Path: "Core/PPU Decoder", Value: "Recompiler (LLVM)" }) &&
               session.Settings.Any(item => item is { Path: "Video/Vulkan/Adapter" }),
            "The 'Used configuration' block is read exactly as RPCS3 printed it, with its section path.");

        var timeout = session.Events.Single(item => item.Categories.Contains(LogCategory.Timeout));
        Assert(timeout.Categories.SequenceEqual([LogCategory.Timeout, LogCategory.FreezeOrStallIndicator, LogCategory.RsxError]) && !timeout.IsMinor,
            "An RSX timeout is Timeout + stall indicator + RSX error (its own text), not a guessed cause.");
        var fs = session.Events.Single(item => item.Categories.Contains(LogCategory.FilesystemError));
        Assert(fs.Count == 2 && fs.IsMinor && fs.Signature!.ErrorCode == "CELL_ENOENT" && fs.Line < fs.LastLine,
            "Repeated HLE failures with different paths are one signature with a count, ranked minor, with the CELL code kept.");
        var fatal = session.Events.Single(item => item.Categories.Contains(LogCategory.FatalError));
        Assert(fatal.Categories.SequenceEqual([LogCategory.FatalError, LogCategory.ShaderCompilationEvent, LogCategory.RenderingError]),
            "A fatal whose text names a shader compile failure carries the shader and rendering categories.");
        Assert(session.Events.Any(item => item.Categories.SequenceEqual([LogCategory.UnknownError])) &&
               session.Events.All(item => item.Channel != "sys_fs" || item.Level == 'E') &&
               !session.Events.Any(item => item.Message.Contains("too sleepy", StringComparison.Ordinal)),
            "An unexplained error is UnknownError, warning traces are not events, and the slow-thread notice is not an error.");
        Assert(session.RawEventLines.Any(text => text.Contains("fragment shader dump follows", StringComparison.Ordinal)) &&
               session.RawEventLines.Count < session.TotalLines,
            "Only bounded raw lines around problems are kept, including the unprefixed continuation of a fatal.");

        var stages = session.Timeline.Select(step => step.Stage).ToArray();
        Assert(stages.SequenceEqual(
                   ["GameBoot", "GameIdentified", "ConfigurationLoaded", "PpuInitialization", "RendererInitialization", "BootSuccessful",
                    "Problem:Timeout", "Problem:UnknownError", "Problem:FatalError", "GameExit"]) &&
               session.Timeline.Zip(session.Timeline.Skip(1)).All(pair => pair.First.Line <= pair.Second.Line),
            "The timeline is the compact ordered story: boot, identity, config, PPU, renderer, boot success, problems, exit.");
        Assert(!stages.Contains("Problem:FilesystemError"),
            "Minor HLE call failures do not flood the timeline.");

        Assert(Parse([]).Events.Count == 0 && Parse(["garbage", "", "\u00B7! not a log line"]).Events.Count == 0,
            "Empty or foreign text parses to an empty session, never an exception.");
    }

    private static void AssertPendingAndMultiBoot()
    {
        var pending = Header();
        pending.AddRange(Boot());
        pending.Add(Line('E', "0:00:30.000000", "RSX", string.Empty));
        pending.Add("ERROR: 0:875: 'round_to_8bit' : no matching overloaded function found");
        var session = Parse(pending);
        Assert(session.Events.Any(item => item.Message.StartsWith("ERROR: 0:875", StringComparison.Ordinal) && item.Categories.Contains(LogCategory.RsxError)),
            "An error header printed with an empty message takes its text from the next unprefixed line.");

        var two = Header();
        two.AddRange(Boot("AAAA00001", "01.00"));
        two.Add(Line('E', "0:01:00.000000", "PPU", "first game failure"));
        two.AddRange(Boot("BBBB00002", "02.00"));
        two.Add(Line('E', "0:02:00.000000", "SPU", "second game failure"));
        Assert(Parse(two).GameSerial == "BBBB00002" && Parse(two).Events.Any(item => item.Categories.Contains(LogCategory.SpuError)) &&
               !Parse(two).Events.Any(item => item.Categories.Contains(LogCategory.PpuError)),
            "By default the session is the last game that booted, with only its own events.");
        Assert(Parse(two, "AAAA00001").GameSerial == "AAAA00001" && Parse(two, "AAAA00001").Events.Any(item => item.Categories.Contains(LogCategory.PpuError)) &&
               Parse(two, "ZZZZ00000").GameSerial == "BBBB00002" && Parse(two).BootCount == 2,
            "The selected game's own boot is chosen when it is in the log; otherwise the last boot, never a merge of both.");
    }

    private static void AssertSignatures()
    {
        var a = LogRules.BuildSignature('E', "sys_fs", "'sys_fs_open' failed with 0x8001002c : CELL_ENOENT, \"/dev_hdd0/game/AAA/USRDIR/a.bin\" [1]");
        var b = LogRules.BuildSignature('E', "sys_fs", "'sys_fs_open' failed with 0x80010099 : CELL_ENOENT, \"D:\\games\\BBB\\x.bin\" [7]");
        Assert(a.Id == b.Id && a.Id.StartsWith("s1:", StringComparison.Ordinal) && a.ErrorCode == "CELL_ENOENT" && a.Pattern.Contains("<path>", StringComparison.Ordinal),
            "Addresses, paths and counters are normalized away: the same failure has the same deterministic signature on every machine.");
        Assert(LogRules.BuildSignature('E', "sys_fs", "'sys_fs_open' failed with 0x1 : CELL_EPERM, \"/x\" [1]").Id != a.Id &&
               LogRules.BuildSignature('F', "sys_fs", "'sys_fs_open' failed with 0x1 : CELL_ENOENT, \"/x\" [1]").Id != a.Id &&
               LogRules.BuildSignature('E', "SYS", "'sys_fs_open' failed with 0x1 : CELL_ENOENT, \"/x\" [1]").Id != a.Id,
            "A different error code, level or subsystem is a different signature.");
        Assert(LogRules.NormalizeMessage("Failed to bind to 204.204.204.204:3658, falling back to 0.0.0.0:3658") ==
               "Failed to bind to <ip>:#, falling back to <ip>:#" &&
               LogRules.BuildSignature('E', "RSX", "PPU-38656a143a665d48c38ae914e7c826d059efd291 bad").Pattern == "PPU-# bad",
            "IP addresses and PPU hashes are normalized; the failure wording is what remains.");
        Assert(LogRules.BuildSignature('E', "RSX", "alpha beta").Id == LogRules.BuildSignature('E', "RSX", "alpha beta").Id &&
               LogRules.BuildSignature('E', "RSX", "alpha beta").Id != LogRules.BuildSignature('E', "RSX", "alpha gamma").Id,
            "Signature equality is exact: similar wording is a different signature.");
        Assert(LogRules.Classify('W', "sys_fs", "sys_fs_fstat(fd=1)").Count == 0 && LogRules.Classify('!', "SYS", "anything").Count == 0 &&
               LogRules.Classify('E', "Foo", "never seen").SequenceEqual([LogCategory.UnknownError]),
            "Only errors and fatals without a specific rule become UnknownError; traces and notices are not events.");
        Assert(LogRules.Classify('E', "SYS", "'cellNetCtlInit' failed with 0x80130102 : CELL_NET_CTL_ERROR_NOT_TERMINATED [1]").Contains(LogCategory.NetworkError) &&
               LogRules.Classify('E', "SYS", "'_sys_prx_get_module_id_by_name' failed with 0x8001112e : CELL_PRX_ERROR_UNKNOWN_MODULE [1]").Contains(LogCategory.PrxError) &&
               LogRules.Classify('E', "Foo", "Access violation writing location 0x0").Contains(LogCategory.Crash) &&
               LogRules.Classify('F', "VM", "out of memory").Contains(LogCategory.OutOfMemory),
            "Categories follow the call name or the failure text itself.");
    }

    private static void AssertBlackScreenIndicator()
    {
        var without = Header();
        without.AddRange(Boot(renderer: false));
        without.Add(Line('E', "0:00:24.000000", "RSX", "Vulkan: vkCreateSwapchainKHR failed"));
        Assert(Parse(without).Events.Any(item => item.Categories.Contains(LogCategory.BlackScreenIndicator)),
            "Booted, graphics error logged and no renderer initialization: a black-screen indicator, from log evidence only.");

        var with = Header();
        with.AddRange(Boot(renderer: true));
        with.Add(Line('E', "0:00:24.000000", "RSX", "Vulkan: vkCreateSwapchainKHR failed"));
        var quiet = Header();
        quiet.AddRange(Boot(renderer: false));
        Assert(!Parse(with).Events.Any(item => item.Categories.Contains(LogCategory.BlackScreenIndicator)) &&
               !Parse(quiet).Events.Any(item => item.Categories.Contains(LogCategory.BlackScreenIndicator)),
            "No indicator when the renderer initialized, or when no graphics problem was logged.");
    }

    private static void AssertLocatorAndReadOnly(string root)
    {
        var install = Path.Combine(root, "install");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(install, "log"));
        Directory.CreateDirectory(Path.Combine(data, "log"));
        var text = string.Join("\r\n", Header().Concat(Boot("LOC00001")).Append(Line('F', "0:00:30.000000", "SIG", "Thread terminated due to fatal error: Verification failed (object: 0x0)")));
        var current = Path.Combine(install, "log", "RPCS3.log");
        File.WriteAllText(current, text, new UTF8Encoding(false));

        var rotated = Path.Combine(install, "log", "RPCS3.log.gz");
        using (var file = File.Create(rotated))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text.Replace("LOC00001", "OLD00002", StringComparison.Ordinal)));
        }

        File.WriteAllText(Path.Combine(data, "log", "RPCS3.log"), text.Replace("LOC00001", "DAT00003", StringComparison.Ordinal), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(install, "log", "notes.txt"), "not a log");
        File.WriteAllText(Path.Combine(install, "log", "RPCS3.old.log"), "not a supported rotation");

        var descriptor = new Rpcs3InstallDescriptor(install, Path.Combine(install, "rpcs3.exe"), Path.Combine(install, "config"),
            Path.Combine(install, "config", "custom"), Path.Combine(install, "patches"), Path.Combine(install, "config", "patch_config.yml"), data);
        var sources = Rpcs3LogService.Locate(descriptor);
        Assert(sources.Count == 3 &&
               sources.Count(item => item.Kind == LogSourceKind.Current) == 2 &&
               sources.Any(item => item.Kind == LogSourceKind.RotatedGzip && item.Path == Path.GetFullPath(rotated)) &&
               sources.All(item => item.Path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) &&
               sources.All(item => Path.GetFileName(item.Path) is "RPCS3.log" or "RPCS3.log.gz"),
            "Logs come only from the install and data roots' own log layout: RPCS3.log and its RPCS3.log.gz rotation, nothing else.");

        var before = (File.ReadAllBytes(current), File.GetLastWriteTimeUtc(current), File.ReadAllBytes(rotated));
        var result = Rpcs3LogService.Read(descriptor, "OLD00002");
        Assert(result.Sessions.Count == 3 && result.Sessions.Any(item => item.GameSerial == "OLD00002" && item.Source!.Kind == LogSourceKind.RotatedGzip) &&
               result.Sessions.Any(item => item.GameSerial == "LOC00001") && result.Sessions.Any(item => item.GameSerial == "DAT00003") &&
               result.Sessions.All(item => item.Problems.Any(problem => problem.Categories.Contains(LogCategory.Assertion))),
            "Current, rotated and data-root logs are all read, including the gzip rotation.");
        Assert(File.ReadAllBytes(current).AsSpan().SequenceEqual(before.Item1) && File.GetLastWriteTimeUtc(current) == before.Item2 &&
               File.ReadAllBytes(rotated).AsSpan().SequenceEqual(before.Item3) && File.Exists(current) && File.Exists(rotated),
            "Reading a log never modifies, truncates or deletes it.");

        // A log RPCS3 is still writing (or that another process holds) stays readable / is reported, never fatal.
        using (var writer = new FileStream(current, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            Assert(Rpcs3LogService.Read(descriptor).Sessions.Count == 3, "A log open for writing by RPCS3 is still readable.");
        }

        using (var locked = new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var lockedResult = Rpcs3LogService.Read(descriptor);
            Assert(lockedResult.Notes.Any(note => note.StartsWith("Unreadable:", StringComparison.Ordinal)) && lockedResult.Sessions.Count == 2,
                "An exclusively locked log is reported unreadable and the others are still read.");
        }

        var empty = new Rpcs3InstallDescriptor(Path.Combine(root, "none"), Path.Combine(root, "none", "rpcs3.exe"), "c", "c", "p", "p", null);
        Assert(Rpcs3LogService.Read(empty).Notes.Contains("NoLogFound") && Rpcs3LogService.Locate(empty).Count == 0,
            "Without a log the result is 'no log found', not an error and not a guessed path.");
    }

    // ---------------------------------------------------------------- engine

    private static LogEvent Problem(string channel, string message, char level = 'F', params LogCategory[] categories)
    {
        var cats = categories.Length > 0 ? categories : [.. LogRules.Classify(level, channel, message)];
        return new LogEvent(10, 10, 1, level, null, channel, message, cats, LogRules.BuildSignature(level, channel, message));
    }

    private static LogSession Session(int build, string serial, params LogEvent[] events) =>
        LogSession.Empty with
        {
            Build = new Rpcs3BuildBanner("0.0.42", build, "abcdef12", "Alpha", "master", "x64"),
            GameSerial = serial,
            Events = events
        };

    private static DiagnosticEnvironment Env(string? serial = "TEST00001", int? build = 19900, string? platform = "Windows", string? arch = "x64") =>
        new(serial, "01.05", [], build, platform, arch, null, "Vulkan");

    private static DiagnosticEntry Entry(
        string id, DiagnosticEntryKind kind = DiagnosticEntryKind.Regression, DiagnosticEvidenceClass evidence = DiagnosticEvidenceClass.DeveloperConfirmedFix,
        int? good = 19899, int? broken = 19900, int? fix = 19950, string[]? titles = null, string? platform = "Windows",
        LogEvent? signatureOf = null, LogCategory[]? categories = null, DiagnosticSolution[]? solutions = null) =>
        new(id, kind, evidence,
            new DiagnosticSource("GITHUB_ISSUE", "https://github.com/RPCS3/rpcs3/issues/1", "Synthetic issue", 1, "2026-01-01"),
            "GAME_SPECIFIC", new DiagnosticScope(titles ?? ["TEST00001"], platform, "x64"), good, broken, fix, 11, 12,
            categories ?? [],
            signatureOf is null ? [] : [new DiagnosticSignatureRef(signatureOf.Signature!.Id, signatureOf.Signature.Subsystem, signatureOf.Signature.Level, signatureOf.Signature.Pattern)],
            solutions ?? []);

    private static void AssertEngine()
    {
        var fatal = Problem("SIG", "Thread terminated due to fatal error: Failed to compile fragment shader");
        var knowledge = new DiagnosticKnowledgeBase([Entry("reg", signatureOf: fatal)]);

        var hit = DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(), knowledge);
        var regression = hit.Findings.Single(item => item.Kind == DiagnosisKind.KnownRegressionMatch);
        Assert(hit.Primary == regression && regression.Quality == MatchQuality.SignatureExact && regression.Entry!.Source.Number == 1 &&
               regression.Solutions.Any(item => item is { Kind: DiagnosticSolutionKind.FixedBuild, Build: 19950 }) &&
               regression.Solutions.Any(item => item is { Kind: DiagnosticSolutionKind.KnownGoodBuild, Build: 19899 }) &&
               !hit.Findings.Any(item => item.Kind == DiagnosisKind.UnknownCause),
            "Exact signature + title + build inside the documented range = known regression, with the documented fixed and known-good builds.");

        var fixedAlready = DiagnosticEngine.Evaluate(Session(19950, "TEST00001", fatal), Env(build: 19950), knowledge);
        Assert(fixedAlready.Findings.Any(item => item.Kind == DiagnosisKind.KnownFixMatch && item.Solutions.Count == 0) &&
               !fixedAlready.Findings.Any(item => item.Kind == DiagnosisKind.KnownRegressionMatch),
            "At or after the documented fixed build the same signature reports that the fix is already included, not a regression.");

        var before = DiagnosticEngine.Evaluate(Session(19899, "TEST00001", fatal), Env(build: 19899), knowledge);
        Assert(before.Findings.Any(item => item.Kind == DiagnosisKind.UnknownCause) &&
               !before.Findings.Any(item => item.Kind is DiagnosisKind.KnownRegressionMatch or DiagnosisKind.KnownFixMatch),
            "A build before the first broken build is outside the regression: the cause stays unknown.");

        foreach (var (name, environment) in new (string, DiagnosticEnvironment)[]
                 {
                     ("other title", Env(serial: "OTHER0002")),
                     ("unknown title", Env(serial: null)),
                     ("other platform", Env(platform: "macOS")),
                     ("unknown platform", Env(platform: null)),
                     ("other architecture", Env(arch: "arm64"))
                 })
        {
            var session = name == "other title" ? Session(19900, "OTHER0002", fatal) : Session(19900, name == "unknown title" ? null! : "TEST00001", fatal);
            var result = DiagnosticEngine.Evaluate(session, environment, knowledge);
            Assert(!result.Findings.Any(item => item.Kind is DiagnosisKind.KnownRegressionMatch or DiagnosisKind.KnownFixMatch or DiagnosisKind.KnownIssueMatch) &&
                   result.Findings.Any(item => item.Kind == DiagnosisKind.UnknownCause),
                $"A documented condition that the environment does not prove ({name}) never matches.");
        }

        // Scope alone is similarity, never a cause.
        var scoped = Entry("scope", categories: [LogCategory.ShaderCompilationEvent, LogCategory.FatalError]);
        var similar = DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(), new DiagnosticKnowledgeBase([scoped]));
        Assert(similar.Findings.Single(item => item.Entry is not null).Kind == DiagnosisKind.SimilarCommunityReport &&
               similar.Findings.Single(item => item.Entry is not null).Quality == MatchQuality.ScopeCompatible &&
               similar.Findings.Single(item => item.Entry is not null).Solutions.Count == 0,
            "A link through title and problem kind only is a similar community report with no solution attached.");
        Assert(!DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(), new DiagnosticKnowledgeBase([Entry("open", titles: [], platform: null, categories: [LogCategory.FatalError])]))
                   .Findings.Any(item => item.Entry is not null),
            "An unrestricted entry with no signature never matches anything.");

        var workaround = Entry("wk", DiagnosticEntryKind.Workaround, DiagnosticEvidenceClass.CorroboratedRegression, signatureOf: fatal,
            solutions: [new DiagnosticSolution(DiagnosticSolutionKind.Setting, "Disable the option", Setting: "Video/Resolution Scale", Applies: "Windows x64")]);
        var documented = DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(), new DiagnosticKnowledgeBase([workaround])).Findings.Single(item => item.Entry is not null);
        Assert(documented.Kind == DiagnosisKind.DocumentedWorkaround && documented.Solutions.Single().Setting == "Video/Resolution Scale" &&
               documented.Entry!.EvidenceClass == DiagnosticEvidenceClass.CorroboratedRegression && documented.Entry.Source.Date == "2026-01-01",
            "A documented workaround keeps its source, evidence class, date, applicable environment and match quality.");

        var open = Entry("open2", fix: null, signatureOf: fatal);
        var unresolved = DiagnosticEngine.Evaluate(Session(19960, "TEST00001", fatal), Env(build: 19960), new DiagnosticKnowledgeBase([open])).Findings.Single(item => item.Entry is not null);
        Assert(unresolved.Kind == DiagnosisKind.KnownRegressionMatch && unresolved.Solutions.Any(item => item.Kind == DiagnosticSolutionKind.KnownUnresolved) &&
               !unresolved.Solutions.Any(item => item.Kind == DiagnosticSolutionKind.FixedBuild),
            "An open regression exposes 'known unresolved' and never an invented fix.");

        var unknownBuild = DiagnosticEngine.Evaluate(LogSession.Empty with { GameSerial = "TEST00001", Events = [fatal] }, Env(build: null), knowledge);
        Assert(unknownBuild.Findings.Any(item => item.Kind == DiagnosisKind.KnownIssueMatch && item.Reasons.Contains("build-unproven")) &&
               !unknownBuild.Findings.Any(item => item.Kind == DiagnosisKind.KnownRegressionMatch),
            "With no proven build a signature match is only a known issue: the range is not applied.");

        var clean = DiagnosticEngine.Evaluate(Session(19900, "TEST00001"), Env(), knowledge);
        Assert(clean.Findings.Count == 0 && clean.Notes.Contains("NoProblemEvents") && clean.Primary is null,
            "A log without problem events yields no diagnosis at all (no fabricated cause).");
        var unknown = DiagnosticEngine.Evaluate(Session(19900, "TEST00001", Problem("Foo", "never seen", 'E')), Env(), DiagnosticKnowledgeBase.Empty);
        Assert(unknown.Primary!.Kind == DiagnosisKind.UnknownCause && unknown.Findings.Any(item => item.Kind == DiagnosisKind.ObservedEvent),
            "Problems with no documented link are Unknown Cause and the observed events stay visible.");
        Assert(DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(build: 19999, serial: "OTHER0002"), knowledge).Notes.SequenceEqual(["LogFromOtherBuild", "LogFromOtherGame"]),
            "A log from another build or another game than the current context is called out.");
        var otherGame = DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(serial: "OTHER0002"), knowledge);
        Assert(otherGame.Findings.All(item => item.Kind == DiagnosisKind.ObservedEvent) && otherGame.Findings.Count == 1 && otherGame.Notes.Contains("LogFromOtherGame"),
            "A log of another game than the selected one keeps its observed events but matches no documented case and claims no cause.");
        Assert(DiagnosticEngine.Evaluate(Session(19900, "TEST00001", fatal), Env(), knowledge).Findings.Select(item => item.Kind).Distinct().Count() >= 2 &&
               new DiagnosticKnowledgeBase([Entry("a"), Entry("a")]).Entries.Count == 2,
            "Findings distinguish observed events from documented matches.");
    }

    // The application use case: which log/session is chosen, and what it feeds the engine.
    private static void AssertUseCase(string root)
    {
        var install = Path.Combine(root, "usecase");
        Directory.CreateDirectory(Path.Combine(install, "log"));
        var failing = Header(19900).Concat(Boot("TEST00001")).Append(Line('F', "0:00:30.000000", "SIG", "Thread terminated due to fatal error: Failed to compile fragment shader"));
        File.WriteAllText(Path.Combine(install, "log", "RPCS3.log"), string.Join(Environment.NewLine, failing), new UTF8Encoding(false));
        var other = Header(19800).Concat(Boot("OTHER0002"));
        using (var file = File.Create(Path.Combine(install, "log", "RPCS3.log.gz")))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, other)));
        }

        var descriptor = new Rpcs3InstallDescriptor(install, Path.Combine(install, "rpcs3.exe"), Path.Combine(install, "config"),
            Path.Combine(install, "config", "custom"), Path.Combine(install, "patches"), Path.Combine(install, "config", "patch_config.yml"), null);
        var useCase = new R3Config.Application.LogDiagnosticsUseCase();
        var fatal = Problem("SIG", "Thread terminated due to fatal error: Failed to compile fragment shader");
        var knowledge = new DiagnosticKnowledgeBase([Entry("uc", signatureOf: fatal, broken: 19900, fix: 19950)]);
        var hardware = new HwProbe(
            readGpuAdapters: () => [new GpuAdapterInfo("NVIDIA GeForce Test", HwVendor.Nvidia, "10DE", "0001", true, true)],
            readOs: () => ("Windows 10.0.26200", "26200", "X64", "X64"),
            readCpuName: () => "cpu", readLogicalProcessors: () => 4, readTotalMemoryBytes: () => 8UL << 30, readGpuName: () => "g").Probe();
        var exact = new Rpcs3BuildIdentity("0.0.42", 19900, "abcdef12", "Alpha", "master", "x64", Rpcs3BuildIdentityState.Exact, []);

        var result = useCase.Analyze(descriptor, "TEST00001", hardware, exact, knowledge);
        Assert(result.Sessions.Count == 2 && result.Report.Session.GameSerial == "TEST00001" && result.Report.Session.Source!.Kind == LogSourceKind.Current &&
               result.Report.Primary!.Kind == DiagnosisKind.KnownRegressionMatch && !result.Notes.Contains("LogFromOtherGame"),
            "The current log of the selected game is analyzed and matched against the documented case.");

        var rotated = useCase.Analyze(descriptor, "OTHER0002", hardware, exact, knowledge);
        Assert(rotated.Report.Session.GameSerial == "OTHER0002" && rotated.Report.Session.Source!.Kind == LogSourceKind.RotatedGzip,
            "The rotated log is used when it holds the selected game's boot.");

        var absent = useCase.Analyze(descriptor, "NONE00003", hardware, exact, knowledge);
        Assert(absent.Notes.Contains("LogFromOtherGame") && absent.Report.Session.Source!.Kind == LogSourceKind.Current &&
               !absent.Report.Findings.Any(item => item.Kind == DiagnosisKind.KnownRegressionMatch),
            "A game absent from every log falls back to the current log, is called out, and never matches another game's case.");

        var unknownBuild = useCase.Analyze(descriptor, "TEST00001", hardware, null, knowledge);
        Assert(unknownBuild.Report.Primary!.Kind == DiagnosisKind.KnownRegressionMatch, "The log's own banner decides the build when the runtime context has none.");

        var none = useCase.Analyze(new Rpcs3InstallDescriptor(Path.Combine(root, "empty"), "x", "c", "c", "p", "p", null), null, null, null, knowledge);
        Assert(none.Sessions.Count == 0 && none.Notes.Contains("NoLogFound") && none.Report.Findings.Count == 0,
            "No log is 'no log found' with no diagnosis.");

        var quiet = Path.Combine(root, "quiet");
        Directory.CreateDirectory(Path.Combine(quiet, "log"));
        File.WriteAllText(Path.Combine(quiet, "log", "RPCS3.log"), string.Join(Environment.NewLine, Header()), new UTF8Encoding(false));
        var noBoot = useCase.Analyze(new Rpcs3InstallDescriptor(quiet, Path.Combine(quiet, "rpcs3.exe"), "c", "c", "p", "p", null), "TEST00001", hardware, exact, knowledge);
        Assert(noBoot.Sessions.Count == 1 && noBoot.Notes.Contains("LogHasNoGameBoot") && noBoot.Report.Findings.Count == 0,
            "A log without any game boot says so and yields no diagnosis for the selected game.");

        var kbRoot = Path.Combine(root, "kb");
        Directory.CreateDirectory(Path.Combine(kbRoot, "Data"));
        Assert(useCase.LoadKnowledge(kbRoot, out var missingError).Entries.Count == 0 && missingError is null,
            "A missing knowledge base is the normal 'no documented cases' state.");
        File.WriteAllText(Path.Combine(kbRoot, "Data", DiagnosticKnowledgeBase.FileName), "{ not json");
        Assert(useCase.LoadKnowledge(kbRoot, out var badError).Entries.Count == 0 && badError == R3Config.Application.LogDiagnosticsUseCase.InvalidKnowledgeMessage,
            "A corrupt knowledge base falls back to no documented cases and reports it.");
    }

    // ------------------------------------------------ shipped knowledge base

    private static void AssertShippedKnowledgeBase()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DiagnosticKnowledgeBase.FileName);
        Assert(File.Exists(path), "The build output must carry Data/diagnostics-kb.json.");
        var knowledge = DiagnosticKnowledgeBase.Load(path);
        Assert(knowledge.Entries.Count > 0 && knowledge.Entries.Select(item => item.Id).Distinct().Count() == knowledge.Entries.Count,
            "The shipped knowledge base loads with unique entry ids.");
        foreach (var entry in knowledge.Entries)
        {
            Assert(WikiLinks.IsSafeUrl(entry.Source.Url) && entry.Source.Url.StartsWith("https://github.com/RPCS3/rpcs3/issues/", StringComparison.Ordinal) &&
                   entry.Source.Number is > 0 && entry.LastGoodBuild < entry.FirstBrokenBuild && (entry.FixedBuild is null || entry.FixedBuild > entry.FirstBrokenBuild) &&
                   entry.Signatures.All(item => item.Id.StartsWith("s1:", StringComparison.Ordinal)) &&
                   (entry.Signatures.Count == 0 ? entry.Categories.Count == 0 : entry.Categories.Count > 0) &&
                   entry.Solutions.Count == 0,
                $"Shipped entry {entry.Id} must have a safe GitHub source, ordered build boundaries, signatures only when proven, and no invented solution.");
        }

        var text = File.ReadAllText(path);
        Assert(!text.Contains("Downloaded-Websites", StringComparison.OrdinalIgnoreCase) &&
               !System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]"),
            "The shipped knowledge base carries no development path.");

        // Real production data end to end: the macOS random-number-generator
        // crash of the documented regression, on and off the documented platform.
        var crash = Problem("SIG", "Thread terminated due to fatal error: sys_ss_random_number_generator(): Failed to generate pseudo-random numbers");
        var mac = DiagnosticEngine.Evaluate(Session(19896, "BCUS98242", crash), Env("BCUS98242", 19896, "macOS", "arm64"), knowledge);
        var match = mac.Findings.Single(item => item.Kind == DiagnosisKind.KnownRegressionMatch);
        Assert(match.Entry!.Source.Number == 19381 && match.Quality == MatchQuality.SignatureExact &&
               match.Solutions.Any(item => item is { Kind: DiagnosticSolutionKind.FixedBuild, Build: 19912 }),
            "The shipped documented case matches its exact signature, platform and build range and exposes its documented fixed build.");
        Assert(!DiagnosticEngine.Evaluate(Session(19896, "BCUS98242", crash), Env("BCUS98242", 19896, "Windows", "x64"), knowledge).Findings.Any(item => item.Kind == DiagnosisKind.KnownRegressionMatch) &&
               !DiagnosticEngine.Evaluate(Session(19913, "BCUS98242", crash), Env("BCUS98242", 19913, "macOS", "arm64"), knowledge).Findings.Any(item => item.Kind == DiagnosisKind.KnownRegressionMatch),
            "The same signature does not match off the documented platform or after the documented fix.");

        // Loader hardening.
        var root = Path.Combine(Path.GetTempPath(), "R3DiagKb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Bad(string json, string name)
            {
                var file = Path.Combine(root, name + ".json");
                File.WriteAllText(file, json);
                Assert(MustFailLoad(file), $"A malformed knowledge base ({name}) must be rejected.");
            }

            Bad("{ not json", "corrupt");
            Bad("{\"schema\":2,\"source\":\"x\",\"entries\":[]}", "schema");
            var good = text;
            Bad(good.Replace("https://github.com/RPCS3/rpcs3/issues/8170", "javascript:alert(1)", StringComparison.Ordinal), "unsafe-url");
            Bad(good.Replace("\"id\": \"gh-15411-15912\"", "\"id\": \"gh-8170-10307\"", StringComparison.Ordinal), "duplicate-id");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        // Fixed/known-good builds resolve exactly through the shipped build catalog.
        var builds = Rpcs3BuildCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Data", Rpcs3BuildCatalog.FileName));
        var resolved = builds.ResolveBuildNumber(16303, "Windows");
        Assert(resolved.State == Rpcs3BuildResolutionState.ResolvedOfficialGitHub && resolved.DownloadUrl!.Contains("16303", StringComparison.Ordinal) &&
               builds.ResolveBuildNumber(16304, "Windows").State == Rpcs3BuildResolutionState.Unresolved &&
               builds.ResolveBuildNumber(16303, "macOS").State == Rpcs3BuildResolutionState.Unresolved &&
               new Rpcs3BuildCatalog([new Rpcs3BuildEntry("0.0.1", 5, "Windows", "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z"),
                                      new Rpcs3BuildEntry("0.0.2", 5, "Windows", "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-2/b.7z")])
                   .ResolveBuildNumber(5, "Windows").State == Rpcs3BuildResolutionState.Unresolved,
            "A documented build number resolves to its exact official GitHub binary; a missing, ambiguous or non-Windows build resolves to nothing.");
    }

    private static bool MustFailLoad(string file)
    {
        try
        {
            DiagnosticKnowledgeBase.Load(file);
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static void AssertNoDevelopmentDependency()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "R3Cfg.slnx")))
        {
            directory = directory.Parent;
        }

        var files = Directory.EnumerateFiles(Path.Combine(directory!.FullName, "Core", "Svc", "Log"), "*.cs");
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert(!System.Text.RegularExpressions.Regex.IsMatch(source, "\"[A-Za-z]:[\\\\/]") && !source.Contains("Downloaded-Websites", StringComparison.OrdinalIgnoreCase) &&
                   !source.Contains("HAKAMIQ", StringComparison.OrdinalIgnoreCase) && !source.Contains("File.Delete", StringComparison.Ordinal) &&
                   !source.Contains("File.WriteAll", StringComparison.Ordinal) && !source.Contains("FileMode.Create", StringComparison.Ordinal) &&
                   !source.Contains("FileAccess.Write", StringComparison.Ordinal) && !source.Contains("Process.", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} must be read-only and must not embed a developer path.");
        }
    }
}
