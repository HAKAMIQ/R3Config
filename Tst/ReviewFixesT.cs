using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;
using R3Config;
using R3Config.Vm;

internal static class ReviewFixesT
{
    public static void Run(TestRunner runner, string root, string? finding)
    {
        if (finding is not (null or "1" or "2" or "3" or "4" or "5" or "6"))
            throw new ArgumentException("Unknown review finding.", nameof(finding));
        if (finding is null or "1") runner.RunCase(nameof(RendererCase), RendererCase);
        if (finding is null or "2") runner.RunCase(nameof(NclDataRoot), () => NclDataRoot(root));
        if (finding is null or "3") runner.RunCase(nameof(LogBounds), () => LogBounds(root));
        if (finding is null or "4") runner.RunCase(nameof(LogCancellation), () => LogCancellation(root));
        if (finding is null or "5") runner.RunCase(nameof(PatchWriteSemanticsT), PatchWriteSemanticsT.Run);
        if (finding is null or "6")
        {
            runner.RunCase(nameof(CustomOverridePresence), () => CustomOverridePresence(root));
            runner.RunCase(nameof(CfgBaselineT), () => CfgBaselineT.Run(root));
        }
        Console.WriteLine("Review fixes targeted tests PASS.");
    }

    private static void RendererCase()
    {
        foreach (var renderer in new[] { "Vulkan", "VULKAN", "vulkan", "OpenGL", "OPENGL" })
        {
            var condition = ChangeScope.Conditions("BLES00001", $"Only on {renderer}").Single(c => c.Kind == EvidenceScopeKind.Renderer);
            Assert(condition.Relation == ScopeRelation.Equal &&
                   condition.Expected == (renderer.StartsWith('V') || renderer.StartsWith('v') ? "Vulkan" : "OpenGL"),
                "Renderer scope must preserve case-insensitive canonical names.");
            Assert(ChangeScope.Conditions("BLES00001", renderer).Single(c => c.Kind == EvidenceScopeKind.Renderer).Relation == ScopeRelation.Unresolved,
                "A bare renderer word is not an anchored scope.");
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static Rpcs3InstallDescriptor Install(string root, string? dataRoot = null) =>
        new(root, Path.Combine(root, "rpcs3.exe"), Path.Combine(dataRoot ?? root, "config"),
            Path.Combine(root, "custom_configs"), Path.Combine(root, "patches"),
            Path.Combine(root, "patch_config.yml"), dataRoot);

    private static void LogBounds(string root)
    {
        foreach (var gzip in new[] { false, true })
        {
            var path = Path.Combine(root, gzip ? "oversized.log.gz" : "oversized.log");
            using (var file = File.Create(path))
            using (var output = gzip ? (Stream)new GZipStream(file, CompressionMode.Compress) : file)
            {
                var block = Encoding.UTF8.GetBytes(new string('x', 4096));
                for (var i = 0; i < 512; i++) output.Write(block);
            }
            var info = new LogSourceInfo(path, gzip ? LogSourceKind.RotatedGzip : LogSourceKind.Current, new FileInfo(path).Length, DateTime.UtcNow);
            var session = Rpcs3LogService.ReadOne(info);
            Assert(session.Truncated && session.TotalLines == 0, "Oversized plain/gzip lines must be discarded before materialization.");
        }

        var source = new BytesSource(Encoding.UTF8.GetBytes("éé\néé\néé\n"));
        var limited = Rpcs3LogService.Read(source, maxDecompressedBytes: 8);
        Assert(limited.Truncated && limited.TotalLines == 1 && source.LastStream!.BytesRead == 8,
            "The budget must count decompressed UTF-8 bytes and discard the incomplete line without reading past the limit.");
        var complete = Rpcs3LogService.Read(new BytesSource(Encoding.UTF8.GetBytes("one\r\ntwo\rthree\nfour")));
        Assert(!complete.Truncated && complete.TotalLines == 4, "CRLF, CR, LF and unterminated final lines must retain ReadLine semantics.");
    }

    private sealed class BytesSource(byte[] bytes, Action? onRead = null) : ILogSource
    {
        public string Name => "memory.log";
        public LogSourceType Type => LogSourceType.PlainText;
        public IReadOnlyList<string> Warnings => [];
        public CountedStream? LastStream { get; private set; }
        public Stream OpenRead(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return LastStream = new CountedStream(bytes, onRead);
        }
    }

    private sealed class CountedStream(byte[] bytes, Action? onRead = null) : MemoryStream(bytes)
    {
        public long BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;
            onRead?.Invoke();
            return read;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static void LogCancellation(string root)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var source = new BytesSource(new byte[128 * 1024], cancellation.Cancel);
        Canceled(() => Rpcs3LogService.Read(source, cancellationToken: token));
        Assert(source.LastStream is { BytesRead: > 0 and < 128 * 1024, Disposed: true },
            "Cancellation during a read must stop further I/O and dispose the source.");
        Canceled(() => new AppFlow().AnalyzeLogs(Install(root), null, null, null, DiagnosticKnowledgeBase.Empty, cancellationToken: token));
        Canceled(() => Rpcs3LogService.Read(Install(root), cancellationToken: token));
        Canceled(() => Rpcs3LogService.ReadOne(new LogSourceInfo("missing.log", LogSourceKind.Current, 0, DateTime.UtcNow), cancellationToken: token));

        using var parsing = new CancellationTokenSource();
        Canceled(() => Rpcs3LogParser.Parse(Lines(), cancellationToken: parsing.Token));
        IEnumerable<string> Lines()
        {
            yield return "SYS: Serial: BLES00001";
            parsing.Cancel();
            yield return "SYS: Title: must not be parsed";
            throw new InvalidOperationException("The parser continued enumeration after cancellation.");
        }
    }

    private static void Canceled(Action action)
    {
        try { action(); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Expected cancellation to propagate.");
    }

    private static void NclDataRoot(string root)
    {
        foreach (var layout in new[] { "portable", "external" })
        {
            var exeRoot = Path.Combine(root, layout, "exe");
            var dataRoot = layout == "portable" ? Path.Combine(exeRoot, "portable") : Path.Combine(root, layout, "data");
            var install = Install(exeRoot, dataRoot);
            var game = new Rpcs3GameDescriptor("BLES00001", "Test", root, "01.00", null, null);
            var vm = new NclImportVm(new AppFlow(), install, game, game.Title, [], () => false, (_, _) => false, () => null);
            var logProperty = typeof(NclImportVm).GetProperty("LogPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Directory.CreateDirectory(dataRoot);
            var log = Path.Combine(dataRoot, "RPCS3.log");
            File.WriteAllText(log, "SYS: Serial: BLES00001\n");
            var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var newer = older.AddMinutes(1);
            File.SetLastWriteTimeUtc(log, newer);
            Assert((string?)logProperty.GetValue(vm) == log, "NCL must find logs created in the resolved data root after the dialog opens.");
            var executableLog = Path.Combine(exeRoot, "log", "RPCS3.log");
            Directory.CreateDirectory(Path.GetDirectoryName(executableLog)!);
            File.WriteAllText(executableLog, "SYS: Serial: BLES00001\n");
            File.SetLastWriteTimeUtc(executableLog, older);
            Assert((string?)logProperty.GetValue(vm) == log,
                "A stale executable-root log must not override the newer active data-root log.");

            File.SetLastWriteTimeUtc(executableLog, newer.AddMinutes(1));
            Assert((string?)logProperty.GetValue(vm) == executableLog,
                "The newest existing NCL log candidate must be selected regardless of root kind.");

            File.SetLastWriteTimeUtc(executableLog, newer);
            Assert((string?)logProperty.GetValue(vm) == executableLog,
                "Equal NCL log timestamps must retain deterministic candidate-order precedence.");

            File.SetLastWriteTimeUtc(executableLog, newer.AddMinutes(2));
            using (new FileStream(
                       executableLog,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None))
            {
                Assert((string?)logProperty.GetValue(vm) == log,
                    "An unreadable newest NCL log candidate must not displace the newest readable candidate.");
            }
        }

        var missingRoot = Path.Combine(root, "missing");
        var missingInstall = Install(missingRoot);
        Assert(
            Rpcs3LogLocator.PreferredExistingPlainLogPath(missingInstall) ==
            Rpcs3LogLocator.PreferredPlainLogPath(missingInstall),
            "When every NCL log candidate is missing, selection must retain the deterministic preferred-path fallback.");
    }

    private static void CustomOverridePresence(string root)
    {
        var install = Install(Path.Combine(root, "remove"));
        Directory.CreateDirectory(install.CfgPath);
        Directory.CreateDirectory(install.CustPath);
        var game = new Rpcs3GameDescriptor("BLES00001", "Test", root, "01.00", null, null);
        var recommendation = new WikiRec(game.Serial, game.Title,
            [new CfgChg("Video", "Write Color Buffers", "true", "test", null, RecRisk.Rec)]);
        var planner = new PlanService();
        var vm = new PlanVm();
        var builtIn = planner.BuildCfg(install, game, recommendation);
        vm.Load(builtIn);
        Assert(builtIn.Items.Single().CurVal == "false" && !vm.CanDel && !builtIn.Items.Single().HasCustomOverride,
            "A built-in effective value must not enable removal.");
        File.WriteAllText(Path.Combine(install.CfgPath, "config.yml"), "Video:\n  Write Color Buffers: true\n");
        var inherited = planner.BuildCfg(install, game, recommendation);
        vm.Load(inherited);
        Assert(inherited.Items.Single().CurVal == "true" && !vm.CanDel,
            "A global inherited value must not enable removal.");
        File.WriteAllText(inherited.CfgPath, "Video:\n  Write Color Buffers: true\n  Anisotropic Filter Override: 16\n");
        var custom = planner.BuildCfg(install, game, recommendation);
        vm.Load(custom);
        Assert(custom.Items.Single().HasCustomOverride && vm.CanDel,
            "A physical custom override must remain removable even when equal to the global value.");
        var removed = custom.Items.Where(item => item.HasCustomOverride).Select(item => item.Change).ToArray();
        new ConfigApplyService().Remove(custom, removed, Path.Combine(root, "remove-backups"));
        var after = planner.BuildCfg(install, game, recommendation);
        vm.Load(after);
        Assert(!vm.CanDel && after.Items.Single().CurVal == "true" && !after.Items.Single().HasCustomOverride,
            "After removal the inherited effective value must remain visible without allowing another removal.");
        Assert(R3Yaml.ReadVal(after.CfgPath, "Video", "Anisotropic Filter Override") == "16",
            "Removing reviewed overrides must preserve unrelated user settings.");
        File.WriteAllText(after.CfgPath, "Video:\n  Write Color Buffers:\n");
        Assert(planner.BuildCfg(install, game, recommendation).Items.Single().HasCustomOverride,
            "Key presence must be tracked independently of a scalar's value.");
    }
}
