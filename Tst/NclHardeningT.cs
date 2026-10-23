using System.IO;
using System.Text;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// Fail-closed regressions for the NCL PPU evidence, numeric and managed Apply
// boundaries that the pipeline suites do not exercise directly.
internal static class NclHardeningT
{
    private const string Serial = "BLES01128";
    private const string Hash = "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
    private const string OtherHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Key = "PPU-" + Hash;
    private const string Seg1 =
        "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n";
    private const string Seg2 =
        "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n";

    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigNclHardening_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            EvidenceSessionIdentity();
            EvidenceKeyAndLayoutAmbiguity();
            EvidenceFileBounds(root);
            RunKeyCandidateLimit(root);
            FloatAndHexBoundaries();
            ApplyFailureModes(root);
            Console.WriteLine("NCL hardening (evidence/numeric/apply failure modes): PASS");
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static NclPpuDomainEvidenceResult Collect(string log, string serial = Serial, string key = Key) =>
        NclPpuAddressDomainEvidenceCollector.CollectText(log, serial, key, "RPCS3.log");

    private static string Session(string serial, string hash, params string[] segments) =>
        $"X SYS: Serial: {serial}\n" + string.Concat(segments) +
        $"X ppu_loader: PPU executable hash: PPU-{hash}\n";

    private static void EvidenceSessionIdentity()
    {
        var unrelated = Collect(Session("BLUS30000", Hash, Seg1));
        Assert(unrelated.Code == NclPpuDomainEvidenceCode.SessionNotFound && unrelated.Domains.Count == 0,
            "A log that only proves another game's session must not bind the selected game.");

        var foreignTail = Collect(Session(Serial, OtherHash, Seg1) + Session("BLUS30000", Hash, Seg1));
        Assert(foreignTail.Code == NclPpuDomainEvidenceCode.HashNotFound,
            "A matching hash inside a later session of another game must not be inherited.");

        var stale = Collect(Session(Serial, Hash, Seg1) + Session(Serial, OtherHash, Seg2));
        Assert(stale.Code == NclPpuDomainEvidenceCode.HashNotFound,
            "Only the newest session of the selected serial is current evidence; an older session is stale.");

        var serialScoped = Collect(Session(Serial, Hash, Seg1), Serial, "BLUS30000-PPU-" + Hash);
        Assert(serialScoped.Code == NclPpuDomainEvidenceCode.SerialScopeMismatch,
            "A serial-scoped run key for another game must be rejected.");

        var twoExecutables = Session(Serial, OtherHash, Seg2) +
            Seg1 + $"X ppu_loader: PPU executable hash: PPU-{Hash}\n";
        var second = Collect(twoExecutables);
        var first = Collect(twoExecutables, Serial, "PPU-" + OtherHash);
        Assert(second.IsOk && second.Domains.Count == 1 && second.Domains[0].GuestStart == 0x10000 &&
               first.IsOk && first.Domains.Count == 1 && first.Domains[0].GuestStart == 0xED0000,
            "Two executables in one session must each keep only their own PT_LOAD evidence.");
    }

    private static void EvidenceKeyAndLayoutAmbiguity()
    {
        var log = Session(Serial, Hash, Seg1);
        Assert(Collect(log, Serial, "SPU-" + Hash).Code == NclPpuDomainEvidenceCode.NotPpuRunKey,
            "An SPU run key is not a PPU address domain.");
        Assert(Collect(log, Serial, "PPU-XYZ").Code == NclPpuDomainEvidenceCode.InvalidRunKey,
            "A malformed run key must be rejected.");
        Assert(Collect(log, "BLES0112", Key).Code == NclPpuDomainEvidenceCode.InvalidInput,
            "A malformed serial must be rejected.");

        var overlapping = Collect(Session(Serial, Hash, Seg1,
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x20000, p_filesz=0x100, p_memsz=0x100, flags=0x400005\n"));
        Assert(overlapping.Code == NclPpuDomainEvidenceCode.AmbiguousSegment,
            "Overlapping PT_LOAD ranges cannot form an unambiguous address domain.");

        var outOfDomain = Collect(Session(Serial, Hash,
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xFFFFFF00, p_filesz=0x200, p_memsz=0x200, flags=0x400005\n"));
        Assert(outOfDomain.Code == NclPpuDomainEvidenceCode.InvalidSegment,
            "A segment that leaves the UInt32 patch-address domain must be rejected.");

        var identicalReload = Collect(Session(Serial, Hash, Seg1, Seg2) +
            Seg1 + Seg2 + $"X ppu_loader: PPU executable hash: PPU-{Hash}\n");
        Assert(identicalReload.IsOk && identicalReload.Domains.Count == 2,
            "A repeated load of the same executable with an identical layout remains exact evidence.");

        var contradictoryReload = Collect(Session(Serial, Hash, Seg1, Seg2) +
            Seg1 + $"X ppu_loader: PPU executable hash: PPU-{Hash}\n");
        Assert(contradictoryReload.Code == NclPpuDomainEvidenceCode.AmbiguousSegment &&
               contradictoryReload.Domains.Count == 0,
            "The same PPU hash with contradictory PT_LOAD evidence must fail closed instead of first-wins.");
    }

    private static void EvidenceFileBounds(string root)
    {
        var large = Path.Combine(root, "large.log");
        using (var stream = File.Create(large))
        {
            stream.SetLength(64L * 1024 * 1024 + 1);
        }

        Assert(NclPpuAddressDomainEvidenceCollector.CollectFile(large, Serial, Key).Code ==
               NclPpuDomainEvidenceCode.LogTooLarge,
            "The PPU evidence log read must stay bounded.");

        var locked = Path.Combine(root, "locked.log");
        File.WriteAllText(locked, Session(Serial, Hash, Seg1));
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert(NclPpuAddressDomainEvidenceCollector.CollectFile(locked, Serial, Key).Code ==
                   NclPpuDomainEvidenceCode.ReadFailed,
                "An unreadable log must fail closed.");
        }

        Assert(NclPpuAddressDomainEvidenceCollector.CollectFile(
                   Path.Combine(root, "missing.log"), Serial, Key).Code ==
               NclPpuDomainEvidenceCode.ReadFailed,
            "A missing log must fail closed.");
    }

    private static void RunKeyCandidateLimit(string root)
    {
        var nclPath = Path.Combine(root, $"GTA {Serial} 01.00.ncl");
        File.WriteAllText(nclPath, "Cheat\n0\nx\n0 0066C908 419EFF40\n#\n", new UTF8Encoding(false));
        var logPath = Path.Combine(root, "limit.log");
        File.WriteAllText(logPath, Session(Serial, Hash, Seg1), new UTF8Encoding(false));
        var request = new NclPpuConversionTextFileRequest(
            File.ReadAllText(nclPath), logPath, NclDialect.ArtemisPs3, 0,
            new NclPpuConversionTarget(string.Empty, "Cheat", "GTA", Serial, "01.00"));

        var tooMany = Enumerable.Range(0, 65)
            .Select(index => "PPU-" + index.ToString("x40", System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Assert(NclPpuConversionService.DescribeEligibleRunKeys(request, tooMany).Code ==
               NclRunKeyEligibilityCode.CandidateLimit,
            "PPU candidate measurement must stay bounded.");

        var measured = NclPpuConversionService.DescribeEligibleRunKeys(
            request, [Key, "PPU-" + OtherHash]);
        Assert(measured.IsOk && measured.EligibleRunKeys.SequenceEqual([Key]),
            "Only a candidate proven by the newest session can become eligible.");
    }

    private static void FloatAndHexBoundaries()
    {
        foreach (var value in new[] { "1e39", "-1e39", "NaN", "Infinity", "-Infinity", "0x3F800000", "1,5" })
        {
            var cheat = NclParser.ReadText($"F\n0\nx\n2 00100010 {value}\n#\n", "f.ncl").Cheats.Single();
            Assert(cheat.Operations.Single() is NclUnsupportedOp &&
                   cheat.Diags.Any(item => item.Code == "NCL013"),
                $"Float literal '{value}' must fail closed instead of writing a non-finite or reinterpreted value.");
        }

        var finite = (NclFloatWrite)NclParser.ReadText("F\n0\nx\n2 00100010 3.4028235e38\n#\n", "f.ncl")
            .Cheats.Single().Operations.Single();
        Assert(finite.BigEndianBytes.SequenceEqual(new byte[] { 0x7F, 0x7F, 0xFF, 0xFF }),
            "The largest finite Float32 must remain exact big-endian bytes.");

        foreach (var line in new[] { "0 10000000000000000 00", "0 00100000 0", "0 00100000 GG", "0 0010000Z 00" })
        {
            var cheat = NclParser.ReadText($"H\n0\nx\n{line}\n#\n", "h.ncl").Cheats.Single();
            Assert(cheat.Operations.Single() is NclUnsupportedOp,
                $"Malformed hexadecimal operand '{line}' must not become a write.");
        }
    }

    private static void ApplyFailureModes(string root)
    {
        // Guard refusal (RPCS3 running / concurrent writer) happens before any file is touched.
        var blocked = Fixture(Path.Combine(root, "blocked"), new DenyGuard());
        var blockedPreview = blocked.Prepare();
        Assert(Throws(() => blocked.UseCase.Apply(blockedPreview, blocked.Install)) &&
               !File.Exists(blocked.ManagedPath) && !File.Exists(blocked.Install.PatCfg),
            "A refused mutation guard must leave no managed NCL state.");

        // Read-only patch_config.yml: nothing may remain half-applied.
        var readOnly = Fixture(Path.Combine(root, "readonly"), new AllowGuard());
        File.WriteAllText(readOnly.Install.PatCfg, "Version: 1.2\n", new UTF8Encoding(false));
        var readOnlyPreview = readOnly.Prepare();
        var configBefore = File.ReadAllBytes(readOnly.Install.PatCfg);
        File.SetAttributes(readOnly.Install.PatCfg, FileAttributes.ReadOnly);
        Assert(Throws(() => readOnly.UseCase.Apply(readOnlyPreview, readOnly.Install)) &&
               !File.Exists(readOnly.ManagedPath) &&
               File.ReadAllBytes(readOnly.Install.PatCfg).SequenceEqual(configBefore),
            "A read-only patch_config.yml must abort Apply without a partial managed patch.");
        File.SetAttributes(readOnly.Install.PatCfg, FileAttributes.Normal);

        // Locked patch_config.yml (another writer holds it exclusively).
        var locked = Fixture(Path.Combine(root, "locked"), new AllowGuard());
        File.WriteAllText(locked.Install.PatCfg, "Version: 1.2\n", new UTF8Encoding(false));
        var lockedPreview = locked.Prepare();
        using (new FileStream(locked.Install.PatCfg, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert(Throws(() => locked.UseCase.Apply(lockedPreview, locked.Install)) &&
                   !File.Exists(locked.ManagedPath),
                "A locked patch_config.yml must abort Apply without a partial managed patch.");
        }

        // Double execution of one reviewed Preview is rejected by the stale preimage.
        var twice = Fixture(Path.Combine(root, "twice"), new AllowGuard());
        var twicePreview = twice.Prepare();
        _ = twice.UseCase.Apply(twicePreview, twice.Install);
        var managedAfter = File.ReadAllBytes(twice.ManagedPath);
        var configAfter = File.ReadAllBytes(twice.Install.PatCfg);
        Assert(Throws(() => twice.UseCase.Apply(twicePreview, twice.Install)) &&
               File.ReadAllBytes(twice.ManagedPath).SequenceEqual(managedAfter) &&
               File.ReadAllBytes(twice.Install.PatCfg).SequenceEqual(configAfter),
            "Re-applying an already-consumed Preview must fail closed without changing committed state.");

        // A patch_config.yml edited by the user after Preview is never overwritten blindly.
        var drift = Fixture(Path.Combine(root, "drift"), new AllowGuard());
        var driftPreview = drift.Prepare();
        var unrelated = new PatChg("PPU-" + new string('c', 40), "User Patch", "Other", "BLUS99999", "01.00", PatRisk.Cheat);
        File.WriteAllText(drift.Install.PatCfg,
            new CfgWrite().Build(drift.Install.PatCfg, [unrelated], enabled: true), new UTF8Encoding(false));
        var userConfig = File.ReadAllBytes(drift.Install.PatCfg);
        Assert(Throws(() => drift.UseCase.Apply(driftPreview, drift.Install)) &&
               File.ReadAllBytes(drift.Install.PatCfg).SequenceEqual(userConfig) &&
               new CfgRead().ReadSnap(drift.Install.PatCfg).IsOn(unrelated) &&
               !File.Exists(drift.ManagedPath),
            "A patch_config.yml change after Preview must abort Apply, preserve the user's file and leave no managed patch.");
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or
                                   InvalidDataException or UnauthorizedAccessException or
                                   AggregateException)
        {
            return true;
        }
    }

    private sealed record Case(
        NclManagedPatchUseCase UseCase,
        Rpcs3InstallDescriptor Install,
        string NclPath,
        string LogPath,
        string ManagedPath)
    {
        public NclManagedPatchPreview Prepare()
        {
            var target = new NclPpuConversionTarget(Key, "1P Invincible Mode", "GTA IV", Serial, "01.00");
            var inspection = UseCase.Inspect(NclPath, NclDialect.ArtemisPs3, Serial, "01.00");
            var preview = UseCase.Prepare(
                new NclPpuConversionFileRequest(NclPath, LogPath, NclDialect.ArtemisPs3, 0, target,
                    inspection.SourceSha256),
                Install);
            Assert(preview.IsOk && !File.Exists(ManagedPath),
                "Fixture Preview must succeed without writing RPCS3 state.");
            return preview;
        }
    }

    private static Case Fixture(string caseRoot, IRpcs3MutationGuard guard)
    {
        var config = Path.Combine(caseRoot, "config");
        var patches = Path.Combine(caseRoot, "patches");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(patches);
        Directory.CreateDirectory(Path.Combine(caseRoot, "custom_configs"));
        var install = new Rpcs3InstallDescriptor(
            caseRoot, Path.Combine(caseRoot, "rpcs3.exe"), config,
            Path.Combine(caseRoot, "custom_configs"), patches,
            Path.Combine(config, "patch_config.yml"), caseRoot);
        var nclPath = Path.Combine(caseRoot, $"GTA IV {Serial} 01.00.ncl");
        File.WriteAllText(nclPath, "1P Invincible Mode\n0\nxtatu\n0 0066C908 419EFF40\n#\n", new UTF8Encoding(false));
        var logPath = Path.Combine(caseRoot, "RPCS3.log");
        File.WriteAllText(logPath, Session(Serial, Hash, Seg1, Seg2), new UTF8Encoding(false));
        var bundled = new BundledCheatPatchService();
        var mutation = new PatchMutationUseCase(
            new PatPlnr(), new PatchApplyService(), new BundledCanaryPatchService(), bundled, guard);
        return new Case(
            new NclManagedPatchUseCase(bundled, mutation), install, nclPath, logPath,
            BundledCheatPatchService.GetPath(patches, Serial));
    }

    private sealed class AllowGuard : IRpcs3MutationGuard
    {
        public void CheckCan(Rpcs3InstallDescriptor install, IReadOnlyList<string> targetPaths,
            IReadOnlyList<string>? workPaths = null)
        {
        }
    }

    private sealed class DenyGuard : IRpcs3MutationGuard
    {
        public void CheckCan(Rpcs3InstallDescriptor install, IReadOnlyList<string> targetPaths,
            IReadOnlyList<string>? workPaths = null) =>
            throw new InvalidOperationException("RPCS3 is running.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
