using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using R3Config.Mdl;
using R3Config.Svc;

// Typed log observations, OS error decoding, performance samples, multi-boot
// isolation, log completeness, the analysis manifest, the local log source
// abstraction (plain / gzip / zip) and the parser throughput guard.
//
// Fixture provenance: RealLog lines are verbatim lines of real RPCS3 logs from
// the local issue corpus; RpcsSource lines are the exact format string of the
// RPCS3 source call named in LogObservationRules.Rule.Source with sample values.
internal static class LogObservationT
{
    private const string Dot = "\u00b7";
    private const string Banner = "RPCS3 v0.0.42-19984-961c86fb Alpha | master";
    private const string CpuLine = "12th Gen Test CPU | 16 Threads | 31.78 GiB RAM | TSC: 3.187GHz | AVX+";

    private static string L(char level, string channel, string message, string time = "00:10.000000", string thread = "") =>
        $"{Dot}{level} 0:{time} {(thread.Length == 0 ? string.Empty : "{" + thread + "} ")}{channel}: {message}";

    private static string[] Boot(string serial, params string[] body) =>
    [
        Banner,
        CpuLine,
        L('!', "SYS", "Emulator::BootGame: path='X:/games/" + serial + "', title", "00:02.000000"),
        L('!', "SYS", "Serial: " + serial, "00:02.100000"),
        L('!', "SYS", "Version: APP_VER=01.00 VERSION=01.00", "00:02.100100"),
        ..body
    ];

    public static void Run(string root)
    {
        AssertEveryKindHasARule();
        AssertObservationFixtures();
        AssertNoObservationFromSimilarText();
        AssertCountsAndBounds();
        AssertWin32Decoding();
        AssertPerformanceSamples();
        AssertMultiBootIsolation();
        AssertCompleteness();
        AssertSourcesAndManifest(root);
        AssertThroughput();

        Console.WriteLine("Log observations (22 typed rules, no similar-text match, counts, Win32 decode, performance, multi-boot, completeness, manifest, gz/zip sources, throughput) PASS.");
    }

    private static void AssertEveryKindHasARule()
    {
        var kinds = LogObservationRules.Rules.Select(rule => rule.Kind).ToArray();
        Assert(Enum.GetValues<LogObservationKind>().All(kind => kinds.Count(item => item == kind) == 1),
            "Every observation kind has exactly one rule.");
        Assert(LogObservationRules.Rules.All(rule => rule.Source.Contains(':', StringComparison.Ordinal) &&
                                                     rule.Evidence is "RealLog" or "RpcsSource"),
            "Every rule names the RPCS3 source line it comes from and the kind of evidence (RealLog / RpcsSource).");
    }

    private static void AssertObservationFixtures()
    {
        // (level, channel, message, kind, expected value, evidence)
        var fixtures = new (char Level, string Channel, string Message, LogObservationKind Kind, string? Value)[]
        {
            ('E', "ppu_loader", "PS3 firmware is not installed or the installed firmware is invalid.", LogObservationKind.FirmwareNotInstalled, null),
            ('E', "cellSsl", "Can't find certificate file /dev_flash/data/cert/CA01.cer, do you have the PS3 firmware installed?", LogObservationKind.FirmwareCertificateMissing, "CA01.cer"),
            ('F', "PAT", "Failed to load patch file C:/rpcs3/patches/patch.yml:", LogObservationKind.PatchFileLoadFailed, "patch.yml"),
            ('E', "ISO", "iso_archive: Corrupt ISO file 'E:/games/x.iso': Volume Descriptor Set Terminator not found", LogObservationKind.IsoCorrupt, "Volume Descriptor Set Terminator not found"),
            // RealLog
            ('E', "SYS", "Invalid or unsupported file format: /vfsv0_virtual_iso_overlay_fs_dev/PS3_GAME/USRDIR/EBOOT.BIN", LogObservationKind.InvalidFileFormat, "EBOOT.BIN"),
            ('E', "sceNp", "npDrmIsAvailable(): Failed to verify sce file \u201c/dev_hdd0/game/TEST00002/USRDIR/x.sce\u201d", LogObservationKind.NpDrmVerifyFailed, "x.sce"),
            ('E', "SYS", "verify_c00_unlock_edat(): Failed to decrypt '/dev_hdd0/game/TEST00002/dlc.edat'", LogObservationKind.EdatDecryptFailed, "dlc.edat"),
            ('E', "EDAT", "Block at offset 0x1a2b has invalid hash!", LogObservationKind.EdatBlockInvalidHash, "0x1a2b"),
            ('E', "sceNp", "npDrmIsAvailable(): Rap file not found: 'C:/rpcs3/dev_hdd0/home/00000001/exdata/EP0000-TEST00002_00-DLC.rap' (Not found)", LogObservationKind.RapNotFound, "EP0000-TEST00002_00-DLC.rap"),
            ('F', "SELF", "Failed to locate the game license file: C:/rpcs3/dev_hdd0/home/00000001/exdata/game.rif.", LogObservationKind.GameLicenseNotFound, "game.rif"),
            // RealLog
            ('E', "RSX", "Your GPU does not support alpha-to-one for multisampling. Graphics may be inaccurate when MSAA is enabled.", LogObservationKind.GpuFeatureUnsupported, "alpha-to-one for multisampling"),
            // RealLog
            ('!', "RSX", "GPU/driver lacks support for float16 data types. All float16_t arithmetic will be emulated with float32_t.", LogObservationKind.DriverFeatureLacking, "float16 data types"),
            ('W', "RSX", "Unsupported device: Test GPU 9000", LogObservationKind.UnsupportedDevice, "Test GPU 9000"),
            ('W', "RSX", "Descriptor pool creation failed with fragmentation error. Will attempt to recover.", LogObservationKind.DescriptorPoolFragmentation, null),
            ('E', "GUI", "gui_pad_thread: Failed to bind device 'Test Pad' to handler Xinput.", LogObservationKind.PadBindFailed, "Test Pad"),
            ('E', "SYS", "Audio renderer XAudio2 could not be initialized, using a Null renderer instead. Make sure that no other application is running.", LogObservationKind.AudioBackendInitFailed, "XAudio2"),
            // RealLog
            ('U', "PPU TODO", "Unimplemented syscall syscall_988 -> CELL_OK (r3=0x1, r4=0x0, r5=0x0, r6=0x300ee284, r7=0x600, r8=0x0, r9=0x14c0ccc, r10=0xee294)", LogObservationKind.UnimplementedSyscall, "syscall_988"),
            ('E', "sceNpTrophy", "sceNpTrophyRegisterContext(): Failed to install trophy context '/dev_hdd0/home/00000001/trophy/NPWR00000_00' (File not found)", LogObservationKind.TrophyInstallFailed, "NPWR00000_00"),
            ('F', "SYS", "Failed to remove save data backup: /dev_hdd0/home/00000001/savedata/TEST00002-BACKUP (File not found)", LogObservationKind.SaveDataRemoveFailed, "TEST00002-BACKUP"),
            ('F', "RSX", "Compilation failed: 0:12(3): error: syntax error", LogObservationKind.ShaderCompileFailed, "0:12(3): error: syntax error"),
            ('F', "RSX", "Linkage failed: unresolved symbol", LogObservationKind.ShaderLinkFailed, "unresolved symbol"),
            // RealLog
            ('!', "SYS", "Title was set from Motor\u00aeStorm: Pacific Rift Update to MOTOR\u00aeSTORM: PACIFIC RIFT", LogObservationKind.TitleRenamedOnBoot, "Motor\u00aeStorm: Pacific Rift Update to MOTOR\u00aeSTORM: PACIFIC RIFT")
        };

        Assert(fixtures.Select(item => item.Kind).Distinct().Count() == Enum.GetValues<LogObservationKind>().Length,
            "There is a positive fixture for every observation kind.");

        foreach (var (level, channel, message, kind, value) in fixtures)
        {
            var session = Rpcs3LogParser.Parse(Boot("TEST00002", L(level, channel, message)));
            var found = session.RunContext.ObservationList.Where(item => item.Kind == kind).ToArray();
            Assert(found.Length == 1 && found[0].Value == value && found[0].Count == 1 && found[0].Level == level,
                $"{kind}: expected value '{value}' from the exact line, got " +
                string.Join(" | ", session.RunContext.ObservationList.Select(item => $"{item.Kind}='{item.Value}'")));
        }
    }

    private static void AssertNoObservationFromSimilarText()
    {
        var similar = new[]
        {
            L('W', "ppu_loader", "PS3 firmware is not installed or the installed firmware is invalid."),   // wrong level
            L('E', "ppu_loader2", "PS3 firmware is not installed or the installed firmware is invalid."),   // wrong channel
            L('E', "ppu_loader", "The PS3 firmware seems not installed."),                                   // similar wording
            L('E', "RSX", "Your GPU seems not to support alpha-to-one."),
            L('U', "PPU", "Unimplemented syscall syscall_988 -> CELL_OK"),                                   // channel lacks TODO
            L('E', "SYS", "Invalid file format: X"),
            L('!', "SYS", "Title set from A to B")
        };
        var session = Rpcs3LogParser.Parse(Boot("TEST00002", similar));
        Assert(session.RunContext.ObservationList.Count == 0,
            "A different level, channel or wording is not an observation: similar text is not evidence.");
    }

    private static void AssertCountsAndBounds()
    {
        var body = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            body.Add(L('U', "PPU TODO", "Unimplemented syscall syscall_988 -> CELL_OK (r3=0x1)", $"00:{10 + index}.000000"));
        }

        body.Add(L('U', "PPU TODO", "Unimplemented syscall syscall_999 -> CELL_OK (r3=0x1)", "00:20.000000"));
        var session = Rpcs3LogParser.Parse(Boot("TEST00002", [.. body]));
        var syscalls = session.RunContext.ObservationList.Where(item => item.Kind == LogObservationKind.UnimplementedSyscall).ToArray();
        Assert(syscalls.Length == 2 &&
               syscalls.Single(item => item.Value == "syscall_988") is { Count: 3, FirstLine: 6, LastLine: 8 } &&
               syscalls.Single(item => item.Value == "syscall_999").Count == 1,
            "The same statement is one observation with its count and first/last line; a different subject is a separate observation.");

        var flood = Boot("TEST00002", [.. Enumerable.Range(0, 2000).Select(index => L('U', "PPU TODO", $"Unimplemented syscall syscall_{index} -> CELL_OK"))]);
        Assert(Rpcs3LogParser.Parse(flood).RunContext.ObservationList.Count == 300,
            "Distinct observations are bounded (300), so a noisy log cannot grow memory without limit.");

        var different = Rpcs3LogParser.Parse(Boot("TEST00002", L('U', "PPU TODO", "Unimplemented syscall syscall_988 -> CELL_OK")));
        Assert(!different.Events.Any(item => item.Categories.Contains(LogCategory.UnknownError)) && !different.Problems.Any(),
            "An unimplemented syscall is an observation only: it creates no problem event and no conclusion.");
    }

    private static void AssertWin32Decoding()
    {
        Assert(Win32ErrorSymbols.Count > 1500 && Win32ErrorSymbols.Lookup(23) == "ERROR_CRC" && Win32ErrorSymbols.Lookup(5) == "ERROR_ACCESS_DENIED" &&
               Win32ErrorSymbols.Lookup(2) == "ERROR_FILE_NOT_FOUND" && Win32ErrorSymbols.Lookup(0x7FFFFFF0) is null,
            "The symbol table comes from the Windows SDK header; unknown codes have no symbol.");

        var lines = new[]
        {
            L('E', "SYS", "file::read failed (error=Data error (cyclic redundancy check). (0x17))", "00:11.000000"),
            L('E', "SYS", "open failed (error=0x5)", "00:12.000000"),
            L('E', "SYS", "odd failure (error=0xfffffff0)", "00:13.000000"),
            L('E', "SYS", "no os error here (errno=2=No such file or directory)", "00:14.000000")
        };
        var errors = Rpcs3LogParser.Parse(Boot("TEST00002", lines)).RunContext.Win32ErrorList;
        Assert(errors.Count == 3 &&
               errors[0] is { Code: 0x17, Symbol: "ERROR_CRC" } && errors[0].Description == "Data error (cyclic redundancy check)." &&
               errors[0].RawText.Contains("(0x17)", StringComparison.Ordinal) &&
               errors[1] is { Code: 5, Symbol: "ERROR_ACCESS_DENIED", Description: null } &&
               errors[2].Symbol is null,
            "An OS error keeps its code, the symbol from the SDK table, the log's own text and the raw text; an unknown code has no symbol; errno lines are not Win32.");
        Assert(!Rpcs3LogParser.Parse(Boot("TEST00002", lines)).Problems.Any(item => item.Message.Contains("ERROR_CRC", StringComparison.Ordinal)),
            "The decoded symbol is an observation, not part of any diagnosis text.");
    }

    private static void AssertPerformanceSamples()
    {
        var lines = new List<string>();
        for (var index = 0; index < 20; index++)
        {
            var cpu = index == 7 ? 100.0 : 50.0 + index % 5;
            lines.Add(L('!', "PERF", $"CPU Usage: Total: {cpu:0.0}%, Cores: 1.0%, 2.0%, RAM Usage: {1000 + index}MB (Peak: {2000 + index}MB)", $"00:{10 + index}.000000", "Performance Sensor"));
        }

        var performance = Rpcs3LogParser.Parse(Boot("TEST00002", [.. lines])).RunContext.Performance;
        Assert(performance is { Samples: 20, RamSamples: 20, CpuMax: 100, CpuMin: 50, RamPeakMb: 2019 } &&
               performance.CpuMedian is >= 50 and <= 54 && performance.CpuP95 is >= 53 && performance.RamMedianMb is >= 1009 and <= 1011,
            "Performance samples are kept whole (count, min, max, peak) and summarized by median and 5th/95th percentile, so one spike is not the summary.");

        var noRam = Rpcs3LogParser.Parse(Boot("TEST00002", L('!', "PERF", "CPU Usage: Total: 72.5%, Cores: 89.1%, 70.3%", "00:10.000000", "Performance Sensor")));
        Assert(noRam.RunContext.Performance is { Samples: 1, RamSamples: 0, RamMedianMb: null, RamPeakMb: null },
            "Builds that print no RAM figure give no RAM summary (absence stays absence).");
        Assert(Rpcs3LogParser.Parse(Boot("TEST00002", L('!', "SIG", "CPU Usage: Total: 72.5%"))).RunContext.Performance is null,
            "Only the PERF channel is a performance sample.");
    }

    private static void AssertMultiBootIsolation()
    {
        var lines = new List<string>
        {
            Banner,
            CpuLine,
            L('A', "RSX", "{x} Found Vulkan-compatible GPU: 'GPU One' running on driver 551.61.0.0", "00:01.000000"),
            L('A', "SYS", "Firmware version: 4.93", "00:01.100000"),
            L('!', "SYS", "Emulator::BootGame: path='X:/A', title", "00:02.000000"),
            L('!', "SYS", "Selected config: mode=custom config, path=\"\"", "00:02.010000"),
            L('!', "SYS", "Serial: GAMEA0001", "00:02.100000"),
            L('!', "SYS", "Used configuration:", "00:02.200000"),
            "Video:",
            "  Renderer: OpenGL",
            L('S', "PAT", "Applied patch (hash='PPU-a1f33132af387c49d452e64fcb0704cb06b6d084', description='Patch A', author='x', patch_version='1.0', file_version='1.2') (<- 1)", "00:03.000000"),
            L('U', "PPU TODO", "Unimplemented syscall syscall_111 -> CELL_OK", "00:03.100000"),
            L('E', "RSX", "Vulkan error VK_ERROR_DEVICE_LOST failed", "00:03.200000"),
            L('!', "SYS", "Quit with exit code 0", "00:04.000000"),
            L('!', "SYS", "Emulator::BootGame: path='X:/B', title", "00:10.000000"),
            L('!', "SYS", "Serial: GAMEB0002", "00:10.100000"),
            L('!', "SYS", "Used configuration:", "00:10.200000"),
            "Video:",
            "  Renderer: Vulkan",
            L('S', "PAT", "Applied patch (hash='PPU-b1f33132af387c49d452e64fcb0704cb06b6d084', description='Patch B', author='x', patch_version='1.0', file_version='1.2') (<- 1)", "00:11.000000"),
            L('U', "PPU TODO", "Unimplemented syscall syscall_222 -> CELL_OK", "00:11.100000"),
            L('!', "RSX", "Vulkan: Renderer initialized on device 'GPU One'", "00:11.200000")
        };

        var a = Rpcs3LogParser.Parse(lines, preferSerial: "GAMEA0001");
        var b = Rpcs3LogParser.Parse(lines, preferSerial: "GAMEB0002");
        Assert(a.BootCount == 2 && b.BootCount == 2 && a.GameSerial == "GAMEA0001" && b.GameSerial == "GAMEB0002",
            "Two boots of two games are two sessions.");
        Assert(a.Settings.Single(item => item.Path == "Video/Renderer").Value == "OpenGL" &&
               b.Settings.Single(item => item.Path == "Video/Renderer").Value == "Vulkan" &&
               a.RunContext.AppliedPatches.Single().Description == "Patch A" &&
               b.RunContext.AppliedPatches.Single().Description == "Patch B" &&
               a.RunContext.ObservationList.Single().Value == "syscall_111" &&
               b.RunContext.ObservationList.Single().Value == "syscall_222",
            "Settings, patches and observations of game A never appear in game B's session, and vice versa.");
        Assert(a.Problems.Any(item => item.Categories.Contains(LogCategory.VulkanError)) && b.Problems.Count == 0,
            "A problem event belongs to the boot that printed it.");
        Assert(a.RunContext.FirmwareVersion == "4.93" && b.RunContext.FirmwareVersion == "4.93" &&
               b.RunContext.GpuDriver == "551.61.0.0" && a.RunContext.GpuDriver is null,
            "Host-level facts (firmware) are shared; the driver is attached only to the session whose renderer initialized on that exact adapter.");
        Assert(a.RunContext.ConfigMode == "custom" && b.RunContext.ConfigMode is null,
            "A configuration mode stated in boot A is not inherited by boot B.");
    }

    private static void AssertCompleteness()
    {
        var exit = L('!', "SYS", "Quit with exit code 0", "00:20.000000");
        var complete = Rpcs3LogParser.Parse(Boot("TEST00002", exit));
        var noExit = Rpcs3LogParser.Parse(Boot("TEST00002"));
        var fragment = Rpcs3LogParser.Parse([L('!', "SYS", "Serial: TEST00002"), exit]);
        Assert(complete.RunContext.Completeness == LogCompleteness.Complete &&
               noExit.RunContext.Completeness == LogCompleteness.Unknown &&
               fragment.RunContext.Completeness == LogCompleteness.Partial &&
               Rpcs3LogParser.Parse(Boot("TEST00002", exit), truncated: true).RunContext.Completeness == LogCompleteness.Partial,
            "Header + boot + exit is Complete; header + boot without exit is Unknown; a header-less fragment and a truncated log are Partial.");

        var graphics = L('E', "RSX", "Vulkan error VK_ERROR_DEVICE_LOST failed");
        var fromFragment = Rpcs3LogParser.Parse([L('!', "SYS", "Serial: TEST00002"), graphics]);
        var fromWhole = Rpcs3LogParser.Parse(Boot("TEST00002", graphics));
        Assert(!fromFragment.Events.Any(item => item.Categories.Contains(LogCategory.BlackScreenIndicator)) &&
               fromWhole.Events.Any(item => item.Categories.Contains(LogCategory.BlackScreenIndicator)),
            "The absence of a renderer-initialized line proves something only in a log that has its header; a fragment draws no whole-run conclusion.");
    }

    private static void AssertSourcesAndManifest(string root)
    {
        var directory = Path.Combine(root, "LogSources");
        Directory.CreateDirectory(directory);
        var text = string.Join("\n", Boot("TEST00002", L('!', "SYS", "Quit with exit code 0", "00:20.000000"))) + "\n";
        var bytes = new UTF8Encoding(false).GetBytes(text);

        var plain = Path.Combine(directory, "RPCS3.log");
        File.WriteAllBytes(plain, bytes);
        var gz = Path.Combine(directory, "RPCS3.log.gz");
        using (var file = File.Create(gz))
        using (var zip = new GZipStream(file, CompressionLevel.Optimal))
        {
            zip.Write(bytes);
        }

        string MakeZip(string name, params (string Name, byte[] Data)[] entries)
        {
            var path = Path.Combine(directory, name);
            using var file = File.Create(path);
            using var archive = new ZipArchive(file, ZipArchiveMode.Create);
            foreach (var (entryName, data) in entries)
            {
                var entry = archive.CreateEntry(entryName);
                using var stream = entry.Open();
                stream.Write(data);
            }

            return path;
        }

        var single = MakeZip("single.zip", ("logs/RPCS3.log", bytes), ("notes.md", [1, 2, 3]));
        var before = Directory.GetFileSystemEntries(directory).Length;

        var fromPlain = Rpcs3LogService.Read(LogSourceResolver.FromPath(plain));
        var fromGz = Rpcs3LogService.Read(LogSourceResolver.FromPath(gz));
        var fromZip = Rpcs3LogService.Read(LogSourceResolver.FromPath(single));
        var manifestPlain = fromPlain.Analysis!;
        var manifestGz = fromGz.Analysis!;
        var manifestZip = fromZip.Analysis!;
        Assert(manifestPlain.LogSha256 is { Length: 64 } && manifestPlain.LogSha256 == manifestGz.LogSha256 &&
               manifestGz.LogSha256 == manifestZip.LogSha256,
            "The same log hashes the same whether it was plain, gzip or zipped (hash of the decompressed text).");
        Assert(manifestPlain.SourceType == "PlainText" && manifestGz.SourceType == "Gzip" && manifestZip.SourceType == "ZipEntry" &&
               manifestPlain.ParserVersion == Rpcs3LogParser.ParserVersion && manifestPlain.RuleSetVersion == LogRules.RuleSetVersion &&
               manifestPlain.RuleSetVersion.StartsWith("c-", StringComparison.Ordinal) &&
               manifestPlain.Completeness == LogCompleteness.Complete && !manifestPlain.IsPartial &&
               manifestPlain.BootSessionCount == 1 && manifestPlain.TotalLines == manifestGz.TotalLines,
            "The manifest carries source type, parser and rule-set versions, completeness, boot count and line count.");
        var again = Rpcs3LogService.Read(LogSourceResolver.FromPath(plain)).Analysis!;
        Assert(again.LogSha256 == manifestPlain.LogSha256 && again.RuleSetVersion == manifestPlain.RuleSetVersion &&
               again.TotalLines == manifestPlain.TotalLines && again.Warnings.SequenceEqual(manifestPlain.Warnings) &&
               fromPlain.AnalysisDuration is not null && manifestPlain.EvidenceSnapshots.Count == 0,
            "The manifest is deterministic; the duration is separate runtime metadata, not part of it.");
        Assert(Directory.GetFileSystemEntries(directory).Length == before,
            "Reading an archive extracts nothing and leaves no temp file next to it.");

        // Fail-closed archive handling.
        var two = MakeZip("two.zip", ("a.log", bytes), ("b/RPCS3.log", bytes));
        var ambiguous = Expect(() => LogSourceResolver.FromPath(two));
        Assert(ambiguous.Candidates.SequenceEqual(["a.log", "b/RPCS3.log"]),
            "More than one log in an archive fails closed and lists the candidates.");
        Assert(LogSourceResolver.FromPath(two, "b/RPCS3.log").Name.EndsWith("!b/RPCS3.log", StringComparison.Ordinal) &&
               Expect(() => LogSourceResolver.FromPath(two, "c.log")).Candidates.Count == 2,
            "A named candidate entry opens; a name that is not a candidate fails closed.");

        var unsafeZip = MakeZip("unsafe.zip", ("../evil.log", bytes), ("C:/x.log", bytes), ("/abs.log", bytes), ("inner.zip", bytes), ("ok/RPCS3.log", bytes));
        var safe = LogSourceResolver.FromPath(unsafeZip);
        Assert(safe.Name.EndsWith("!ok/RPCS3.log", StringComparison.Ordinal) &&
               safe.Warnings.Count(item => item.StartsWith("UnsafeEntryIgnored:", StringComparison.Ordinal)) == 3 &&
               safe.Warnings.Any(item => item.StartsWith("NestedArchiveIgnored:", StringComparison.Ordinal)),
            "Path-traversal, rooted and drive-qualified entry names are never candidates and a nested archive is never opened.");

        Assert(Expect(() => LogSourceResolver.FromPath(MakeZip("empty.zip", ("readme.md", [1])))).Candidates.Count == 0,
            "An archive without a log entry fails closed.");
        var notZip = Path.Combine(directory, "fake.zip");
        File.WriteAllText(notZip, "not a zip");
        Assert(Expect(() => LogSourceResolver.FromPath(notZip)).Message.Contains("not a readable zip", StringComparison.Ordinal),
            "A file that is not a zip is refused with a reason.");
        var many = Path.Combine(directory, "many.zip");
        using (var file = File.Create(many))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            for (var index = 0; index <= LocalArchiveLogSource.MaxEntries; index++)
            {
                archive.CreateEntry("e" + index);
            }
        }

        Assert(Expect(() => LogSourceResolver.FromPath(many)).Message.Contains("more than", StringComparison.Ordinal),
            "An archive with an excessive number of entries is refused.");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var threw = false;
        try
        {
            Rpcs3LogService.Read(LogSourceResolver.FromPath(plain), null, null, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert(threw, "A cancelled read stops with OperationCanceledException.");
    }

    private static LogSourceException Expect(Action action)
    {
        try
        {
            action();
        }
        catch (LogSourceException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected a LogSourceException.");
    }

    private static void AssertThroughput()
    {
        // A guard, not a benchmark: ~11 MB parsed in about 0.3 s on the reference
        // machine. The limit is deliberately generous; it fails only if the parser
        // becomes an order of magnitude slower, which is what would justify the
        // bot's prefilter design.
        var lines = new List<string>(Boot("TEST00002"));
        for (var index = 0; index < 250_000; index++)
        {
            lines.Add(L('!', "PPU", $"{{PPU[0x1000000] Thread (main_thread) [0x0054263c]}} cellSpurs: value {index} processed", "01:00.000000"));
        }

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var session = Rpcs3LogParser.Parse(lines);
        timer.Stop();
        Assert(session.TotalLines >= 250_000 && timer.Elapsed < TimeSpan.FromSeconds(15),
            $"Parsing 250k lines must stay far below the throughput limit (took {timer.ElapsedMilliseconds} ms).");
    }
}
