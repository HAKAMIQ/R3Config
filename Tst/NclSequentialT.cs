using System.IO;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;
using YamlDotNet.RepresentationModel;

namespace R3Config.Tests;

internal static class NclSequentialT
{
    private static readonly string[] FieldTitles = ["Ammo Never Decreases", "Infinite Health"];

    // The collision must reject identically whether or not managed A is already
    // resident, so the case runs once for each starting state. Hoisted per
    // CA1861: the array is a constant argument to a repeatedly executed loop.
    private static readonly bool[] ExistingManagedA = [false, true];

    public static void Run()
    {
        var failures = new List<string>();
        var passed = 0;
        Check("SequentialApply_A_Then_B_PreservesBoth", f => { f.Apply(0); f.Apply(1); f.Expect("A", "B"); });
        Check("SequentialApply_A_B_C_PreservesAll", f => { f.Apply(0); f.Apply(1); f.Apply(2); f.Expect("A", "B", "C"); });
        Check("ReapplySameCheat_IsIdempotent", f =>
        {
            f.Apply(0); var before = f.Bytes(); f.Apply(0);
            Require(before.SequenceEqual(f.Bytes()), "Reapply changed the definitions or activation bytes.");
            f.Expect("A");
        });
        Check("FreshPreviewAfterFirstApply_UsesReadBackState", f =>
        {
            var stale = f.Preview(1); f.Apply(0); var fresh = f.Preview(1);
            Require(stale.Plan!.ExpectedPatCfgExists == false && fresh.Plan!.ExpectedPatCfgExists == true,
                "Fresh preview retained the missing-file state.");
            Require(fresh.ManagedPreparation!.PreparedFile!.ExpectedSha256 ==
                PatchFileTransactionService.CaptureExpectedState(f.Managed, R3Limits.PatCatBy, "test").Sha256,
                "Fresh preview did not capture the committed managed bytes.");
            Fixture.UseCase().Apply(fresh, f.Install); f.Expect("A", "B");
        });
        Check("StalePreviewAfterExternalMutation_Aborts", f =>
        {
            f.Apply(0);
            foreach (var path in new[] { f.Managed, f.Install.PatCfg })
            {
                var preview = f.Preview(1); File.AppendAllText(path, "\n# external edit\n");
                var before = f.Bytes(); ExpectAbort(() => Fixture.UseCase().Apply(preview, f.Install));
                Require(before.SequenceEqual(f.Bytes()), "Stale apply changed external content.");
            }
        });
        Check("ExistingManagedDefinitions_AreMergedNotReplaced", f => { f.Apply(0); f.Apply(2); f.Apply(1); f.Expect("A", "B", "C"); });
        Check("UnrelatedManagedSibling_IsPreserved", f =>
        {
            f.Apply(0);
            File.AppendAllText(f.Managed, "Future Metadata:\n  Owner: another-writer\n  Values: [one, two]\n");
            var before = Node(f.Managed, "Future Metadata"); f.Apply(1);
            Require(before == Node(f.Managed, "Future Metadata"), "Merge dropped unknown resident YAML content.");
            f.Expect("A", "B");
        });
        Check("ManualPatch_IsPreserved", f =>
        {
            f.Apply(0);
            var resident = File.ReadAllText(f.Managed).Replace("Author: Source Author", "Author: Manual Author", StringComparison.Ordinal)
                .Replace("Author: HAKAMIQ", "Author: Manual Author", StringComparison.Ordinal);
            File.WriteAllText(f.Managed, resident);
            var before = Node(f.Managed, Fixture.Key, "A"); f.Apply(1);
            Require(before == Node(f.Managed, Fixture.Key, "A"), "Manual sibling metadata changed.");
            File.WriteAllText(f.Managed, resident[BundledCheatPatchService.ManMark.Length..]);
            var bytes = f.Bytes(); var blocked = f.Preview(2, requireSuccess: false);
            Require(!blocked.IsOk && bytes.SequenceEqual(f.Bytes()), "Unmarked manual file was accepted.");
        });
        foreach (var state in new[] { "Canary", "Artemis", "Official", "Manual", "Unknown" })
        {
            Check(state + "PatchConfigState_IsPreserved", f =>
            {
                File.WriteAllText(f.Install.PatCfg, $"{Fixture.OtherKey}:\n  {state}:\n    Other Game:\n      BLES99997:\n        01.10:\n          Enabled: true\n          Configurable Values:\n            Choice: 17\n          Future: [true, text]\n");
                var before = Node(f.Install.PatCfg, Fixture.OtherKey); f.Apply(0); f.Apply(1);
                Require(before == Node(f.Install.PatCfg, Fixture.OtherKey), state + " state changed.");
            });
        }
        Check("DisabledFirstCheat_RemainsDisabled", f =>
        {
            var a = f.Apply(0);
            new PatchApplyService().Disable(new PatPlnr().Build2(f.Install, [a.Match!]), Path.Combine(f.Root, "disable-backups"));
            f.Apply(1);
            Require(!new CfgRead().ReadSnap(f.Install.PatCfg).IsOn(a.Plan!.Changes[0]), "Adding B enabled A again.");
        });
        Check("DifferentSerial_IsIsolated", f =>
        {
            f.Apply(0, serial: "BLES99998"); f.Apply(1, serial: "BLES99998");
            var other = BundledCheatPatchService.GetPath(f.Install.PatPath, "BLES99998");
            var before = File.ReadAllBytes(other);
            var state = Node(f.Install.PatCfg, Fixture.Key);
            f.Apply(0); f.Apply(1);
            Require(before.SequenceEqual(File.ReadAllBytes(other)), "Other serial file changed.");
            Require(Node(f.Install.PatCfg, Fixture.Key, "A", "Synthetic", "BLES99998").Contains("true", StringComparison.Ordinal), "Other serial state lost.");
            Require(!string.IsNullOrEmpty(state), "Other serial state was not seeded.");
        });
        Check("DifferentVersion_IsIsolated", f =>
        {
            f.Apply(0); f.Apply(1, version: "01.10");
            var entries = f.Entries();
            Require(entries.Single(e => e.Title == "A").GameTgt.Single().Versions.SequenceEqual(["01.00"]), "A moved versions.");
            Require(entries.Single(e => e.Title == "B").GameTgt.Single().Versions.SequenceEqual(["01.10"]), "B moved versions.");
            var before = f.Bytes(); var collision = f.Preview(0, version: "01.10", requireSuccess: false);
            Require(!collision.IsOk && before.SequenceEqual(f.Bytes()), "Same runtime identity silently reused another version's definition.");
        });
        Check("DifferentPpuKey_IsIsolated", f =>
        {
            f.Apply(0); f.Apply(0, key: Fixture.OtherKey); f.Apply(1);
            Require(f.Entries().Count(e => e.RunKey == Fixture.Key) == 2 &&
                f.Entries().Single(e => e.RunKey == Fixture.OtherKey).Title == "A", "Cheat moved between PPU keys.");
        });
        Check("ReloadBetweenSequentialApplies_PreservesFirstCheat", f =>
        {
            f.Apply(0); var read = new ReadSvc().ReadText(File.ReadAllText(f.Managed), PatSrc.ManagedArtemis);
            Require(read.Catalog.Entries.Single().Title == "A", "Reload lost A.");
            f.Apply(1); f.Expect("A", "B"); // Every call constructs new use cases/readers/stores.
        });
        Check("SecondCommitFailure_RollsBackOnlyOwnedAttempt", f =>
        {
            f.Apply(0); var preview = f.Preview(1); var before = f.Bytes(); var commits = 0;
            var unrelated = Path.Combine(f.Install.PatPath, "patch.yml"); File.WriteAllText(unrelated, "Version: 1.2\n");
            var txn = new PatchFileTransactionService(commitFile: (stage, target) =>
            {
                if (++commits == 2) throw new IOException("Injected second commit failure.");
                File.Move(stage, target, true);
            });
            ExpectAbort(() => Fixture.UseCase(new PatchApplyService(transaction: txn)).Apply(preview, f.Install));
            Require(commits == 2 && before.SequenceEqual(f.Bytes()) && File.ReadAllText(unrelated) == "Version: 1.2\n", "Selective rollback did not restore the prior transaction.");
        });
        Check("DuplicateIdentityConflict_FailsClosed", f =>
        {
            f.Apply(0); var before = f.Bytes();
            var conflict = f.Preview(0, word: "39290000", requireSuccess: false);
            Require(!conflict.IsOk && before.SequenceEqual(f.Bytes()), "Conflicting bytes were silently overwritten.");
        });
        Check("DuplicateResidentIdentity_FailsClosed", f =>
        {
            f.Apply(0);
            var resident = File.ReadAllText(f.Managed);
            var hashStart = resident.IndexOf(Fixture.Key + ":", StringComparison.Ordinal);
            File.AppendAllText(f.Managed, resident[hashStart..]);
            var before = f.Bytes(); var rejected = f.Preview(1, requireSuccess: false);
            Require(!rejected.IsOk && before.SequenceEqual(f.Bytes()), "Duplicate resident identity was silently selected.");
        });
        Check("CrossSourceRuntimeIdentity_RejectsBeforeCommit_WithOrWithoutA", f =>
        {
            var official = Path.Combine(f.Install.PatPath, "patch.yml");
            File.WriteAllText(official, $"Version: 1.2\n{Fixture.Key}:\n  B:\n    Games:\n      Synthetic:\n        BLES99999: [01.00]\n    Author: Official Author\n    Patch Version: 1.0\n    Patch:\n    - [be32, 0x00010020, 0x60000000]\n");
            var officialBytes = File.ReadAllBytes(official);
            foreach (var existingA in ExistingManagedA)
            {
                if (existingA) f.Apply(0);
                var preview = f.Preview(1); var before = f.Bytes(); var rejected = false;
                try { Fixture.UseCase().Apply(preview, f.Install); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("runtime identity collision", StringComparison.Ordinal)) { rejected = true; }
                Require(rejected && before.SequenceEqual(f.Bytes()) && officialBytes.SequenceEqual(File.ReadAllBytes(official)),
                    "Cross-source identity guard failed or changed files before rejecting.");
            }
        });
        Check("IdempotentPreviewAfterExternalDefinitionMutation_Aborts", f =>
        {
            f.Apply(0); var preview = f.Preview(0); File.AppendAllText(f.Managed, "\n# drift after no-op preview\n");
            var before = f.Bytes(); ExpectAbort(() => Fixture.UseCase().Apply(preview, f.Install));
            Require(before.SequenceEqual(f.Bytes()), "No-op preview bypassed definition concurrency.");
        });
        Check("OrderingIndependence_ProducesSameDefinitions", f =>
        {
            f.Apply(0); f.Apply(1); var first = f.Entries().OrderBy(e => e.Title).Select(e => e.Title + e.DefPrint).ToArray();
            using var reverse = new Fixture(); reverse.Apply(1); reverse.Apply(0);
            Require(first.SequenceEqual(reverse.Entries().OrderBy(e => e.Title).Select(e => e.Title + e.DefPrint)), "Definition set depends on apply order.");
        });
        Check("GeneratedAuthor_IsHAKAMIQ_SourceAuthorPreserved", f =>
        {
            var preview = f.Preview(0, dialect: NclDialect.NetCheat453);
            Fixture.UseCase().Apply(preview, f.Install);
            Require(f.Entries().Single().Author == "HAKAMIQ", "Generated Author is not HAKAMIQ.");
            Require(Node(f.Managed, Fixture.Key, "A", "Source Author") == "Source Author", "Source provenance was lost.");
        });
        Check("BCUS98103_BundledAmmoThenHealth_PreservesBoth", f =>
        {
            var repo = FindRepo(); var bundled = new BundledCheatPatchService();
            var useCase = new NclManagedPatchUseCase(bundled, Fixture.Mutation(bundled, new PatchApplyService()), Path.Combine(repo, "App", "Data", "ncl.zip"));
            var lookup = useCase.DiscoverBundled("BCUS98103", "01.10");
            foreach (var name in FieldTitles)
            {
                var candidate = lookup.Cheats.First(c => c.Name == name);
                var target = new NclPpuConversionTarget("PPU-8363904e0b8fc276380a8f0e158dd81d7a9cefc5", name, "Uncharted: Drake's Fortune", "BCUS98103", "01.10");
                var log = f.Log(target.Serial, target.RunKey);
                var preview = useCase.PrepareBundled(new NclBundledManagedPatchRequest(candidate, log, target), f.Install);
                Require(preview.IsOk, preview.Error ?? "Bundled preview failed."); useCase.Apply(preview, f.Install);
                Require(new CfgRead().ReadSnap(f.Install.PatCfg).IsOn(preview.Plan!.Changes.Single()), "Field identity was not activated.");
            }
            var read = new ReadSvc().ReadText(File.ReadAllText(BundledCheatPatchService.GetPath(f.Install.PatPath, "BCUS98103")), PatSrc.ManagedArtemis);
            Require(read.Catalog.Entries.Select(e => e.Title).Order().SequenceEqual(FieldTitles), "Field definitions were lost.");
        });
        Console.WriteLine($"NCL sequential: Passed: {passed}, Failed: {failures.Count}, Skipped: 0");
        Require(failures.Count == 0, string.Join(Environment.NewLine, failures));

        void Check(string name, Action<Fixture> test)
        {
            using var fixture = new Fixture();
            try { test(fixture); passed++; Console.WriteLine(name + ": PASS"); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); Console.WriteLine(name + ": FAIL " + ex.Message); }
        }
    }

    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "R3Cfg.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Repository not found.");
    }

    private static string Node(string path, params string[] keys)
    {
        var yaml = new YamlStream(); yaml.Load(new StringReader(File.ReadAllText(path)));
        var node = yaml.Documents[0].RootNode;
        foreach (var key in keys)
        {
            Require(node is YamlMappingNode map && map.Children.ContainsKey(new YamlScalarNode(key)), "Missing YAML node: " + key);
            node = ((YamlMappingNode)node).Children[new YamlScalarNode(key)];
        }
        return node.ToString();
    }

    private static void ExpectAbort(Action apply)
    {
        try { apply(); } catch (IOException) { return; }
        throw new InvalidOperationException("Expected Apply to abort with an I/O conflict/failure.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Key = "PPU-1111111111111111111111111111111111111111";
        public const string OtherKey = "PPU-2222222222222222222222222222222222222222";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "R3NclSequential_" + Guid.NewGuid().ToString("N"));
        public Rpcs3InstallDescriptor Install { get; }
        public string Managed => BundledCheatPatchService.GetPath(Install.PatPath, "BLES99999");
        public Fixture()
        {
            var config = Path.Combine(Root, "config"); var patches = Path.Combine(Root, "patches");
            Directory.CreateDirectory(config); Directory.CreateDirectory(patches);
            Install = new Rpcs3InstallDescriptor(Root, Path.Combine(Root, "rpcs3.exe"), config, Path.Combine(config, "custom_configs"), patches, Path.Combine(config, "patch_config.yml"), DataRoot: Root);
        }
        public static PatchMutationUseCase Mutation(BundledCheatPatchService bundled, PatchApplyService apply) =>
            new(new PatPlnr(), apply, new BundledCanaryPatchService(), bundled, new AllowGuard());
        public static NclManagedPatchUseCase UseCase(PatchApplyService? apply = null)
        {
            var bundled = new BundledCheatPatchService(); return new(bundled, Mutation(bundled, apply ?? new PatchApplyService()));
        }
        public string Log(string serial, string key)
        {
            var path = Path.Combine(Root, serial + "-" + key + ".log");
            File.WriteAllText(path, $"X SYS: Serial: {serial}\nX ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x800000, p_memsz=0x800000, flags=0x400005\nX ppu_loader: PPU executable hash: {key}\n");
            return path;
        }
        public NclManagedPatchPreview Preview(int index, string serial = "BLES99999", string version = "01.00", string key = Key, string word = "60000000", bool requireSuccess = true, NclDialect dialect = NclDialect.ArtemisPs3)
        {
            var path = Path.Combine(Root, $"Synthetic {serial} {version}.ncl");
            File.WriteAllText(path, $"A\n0\nSource Author\n0 00010010 {word}\n#\nB\n0\nSource Author\n0 00010020 60000000\n#\nC\n0\nSource Author\n0 00010030 60000000\n#\n");
            var target = new NclPpuConversionTarget(key, ((char)('A' + index)).ToString(), "Synthetic", serial, version, "Untrusted Caller Author");
            var preview = UseCase().Prepare(new NclPpuConversionFileRequest(path, Log(serial, key), dialect, index, target), Install);
            if (requireSuccess) Require(preview.IsOk, preview.Code + ": " + preview.Error);
            return preview;
        }
        public NclManagedPatchPreview Apply(int index, string serial = "BLES99999", string version = "01.00", string key = Key)
        {
            var preview = Preview(index, serial, version, key); UseCase().Apply(preview, Install); return preview;
        }
        public IReadOnlyList<Rpcs3PatchEntry> Entries()
        {
            var read = new ReadSvc().ReadText(File.ReadAllText(Managed), PatSrc.ManagedArtemis);
            Require(read.IsOk, read.Error ?? "Catalog invalid."); return read.Catalog.Entries;
        }
        public void Expect(params string[] titles)
        {
            var entries = Entries(); Require(entries.Select(e => e.Title).Order().SequenceEqual(titles.Order()), "Unexpected definition set.");
            var snapshot = new CfgRead().ReadSnap(Install.PatCfg);
            foreach (var title in titles)
                Require(snapshot.IsOn(new PatChg(Key, title, "Synthetic", "BLES99999", "01.00", PatRisk.Cheat)), title + " enabled state missing.");
        }
        public byte[] Bytes() => [.. File.Exists(Managed) ? File.ReadAllBytes(Managed) : [], .. File.Exists(Install.PatCfg) ? File.ReadAllBytes(Install.PatCfg) : []];
        public void Dispose() => Directory.Delete(Root, true);
    }
    private sealed class AllowGuard : IRpcs3MutationGuard
    {
        public void CheckCan(Rpcs3InstallDescriptor install, IReadOnlyList<string> targetPaths, IReadOnlyList<string>? workPaths = null) { }
    }
}
