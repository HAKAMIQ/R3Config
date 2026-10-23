using System.IO;
using System.Security.Cryptography;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;
using R3Config.Vm;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

internal static partial class Program
{
    private static void WikiExplicitSerial_Runtime(string root)
    {
        var fixtures = Path.Combine(RepoRoot(), "Tst", "Fixtures", "Wiki");
        var mirror = WikiSvc.Analyze(Path.Combine(fixtures, "GtaIV-20241214.xml"));
        var page = mirror.Pages.Single();
        Assert(page.PageId == 829 && page.RevisionId == "47638" &&
               page.GameIdBindings.Single(item => item.Serial == "BLES01128").Role == WikiGameIdRole.RelatedEdition &&
               mirror.CatalogRecommendations.Single(item => item.Serial == "BLES01128").Changes.Count == 8,
            "The 2024 mirror explicitly covers BLES01128 and its eight mapped settings.");
        var current = R3Config.Application.RecommendationCatalogUseCase.Rebuild(Path.Combine(fixtures, "GtaIV-20260907.xml"));
        var gta = current.Find("BLES01128")!;
        Assert(gta.Binding == WikiGameIdRole.Primary && gta.Changes.Count == 9,
            "Direct Wiki configuration scope must generate nine applicable BLES01128 settings.");
        var shipped = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json")).Find("BLES01128")!;
        Assert(shipped.Binding == WikiGameIdRole.Primary && shipped.Changes.SequenceEqual(gta.Changes),
            "Shipped GTA IV classification/values must agree with the exact source fixture.");
        var demons = R3Config.Application.RecommendationCatalogUseCase.Rebuild(Path.Combine(fixtures, "DemonsSouls-20241214.xml"));
        foreach (var catalog in new[] { current, demons })
        {
            var serial = ReferenceEquals(catalog, current) ? "BLES01128" : "BLUS30443";
            var rec = catalog.Find(serial)!;
            var env = GameSetupT.CreateEnvironment(Path.Combine(root, serial));
            var customPath = Path.Combine(env.Install.CustPath, $"config_{serial}.yml");
            File.WriteAllText(customPath, "Video:\n  Write Color Buffers: true\n");
            var game = new Rpcs3GameDescriptor(serial, rec.WikiName, env.GamePath, null, null, null);
            var useCases = AppComposition.Build(new Rpcs3MutationGuard(() => false));
            var setup = useCases.GameSetup.Build(env.Install, game, catalog, new PatCat("1.2", []));
            var plan = setup.BaseConfiguration;
            Assert(plan.Items.Count == rec.Changes.Count, "Game Setup lost explicit Wiki settings.");
            var configured = plan.Items.Single(item => item.Key == "Write Color Buffers");
            var pending = plan.Items.Single(item => item.Key == "Minimum Scalable Dimension");
            Assert(configured.CurVal == "true" && configured.State == RecState.Applied && !configured.IsAction &&
                   pending.CurVal == "16" && pending.IsAction && pending.IsDiff,
                "Normal current-value comparison must work identically for GTA IV and Demon's Souls.");
            var language = UiText.CurLang;
            try
            {
                foreach (var lang in new[] { "ar-SA", "en-US" })
                {
                    UiText.SetLang(lang);
                    var vm = new PlanVm();
                    vm.Load(plan);
                    var matched = vm.Items.Single(item => item.Key == configured.Key);
                    var selectable = vm.Items.Single(item => item.Key == pending.Key);
                    selectable.IsSel = true;
                    Assert(matched.IsApply && !matched.ShowSelection && matched.StatDsp == UiText.GetText("RecState.Applied") &&
                           selectable.ShowSelection && selectable.IsSel && vm.HasChg && !vm.IsEmpty &&
                           vm.Items.All(item => item.BadgeDsp != "Plan.RelatedEdition"),
                        "Applicable Wiki settings must use ordinary configured/checkbox UI in both languages.");
                }
            }
            finally { UiText.SetLang(language); }
            var preview = useCases.GameSetup.Preview(setup, new GameSetupSelection([pending.Change], []));
            Assert(preview.CanApply && preview.EffectiveSettings.Count == 1 &&
                   preview.EffectiveSettings.Single() == pending.Change,
                "Normal Game Setup Preview must retain the selected exact-scope Wiki setting.");
            var result = useCases.Config.Apply(plan, [pending.Change], env.Install);
            Assert(result.ChgList.Count == 1 && R3Yaml.ReadVal(customPath, pending.Section, pending.Key) == pending.Val,
                "Normal config Apply failed in the isolated test installation.");
            Assert(useCases.Config.Build(env.Install, game, rec, null).Items.Single(item => item.Key == pending.Key).State == RecState.Applied,
                "Applied Wiki setting must compare as configured on refresh.");
            var stale = rec with { Binding = WikiGameIdRole.RelatedEdition };
            var stalePlan = new PlanService().BuildCfg(env.Install, game, stale);
            Assert(stalePlan.Items.Count == 0 && !stalePlan.NeedBkp,
                "A supplied RelatedEdition row without generated applicability must remain fail-closed.");
            Assert(!File.Exists(env.Install.PatCfg), "Wiki tests must not write patch_config.yml.");
        }
        Console.WriteLine("WikiExplicitSerial_Runtime PASS: BLES01128=9; BLUS30443 comparison/checkbox/Preview/isolated Apply parity.");
    }
    private static void Settings_AllCatalogDiscovery(string root)
    {
        var catalog = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json"));
        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "settings-scan"));
        var planner = new PlanService();
        var discoverable = 0;
        var runtime = 0;
        var missing = 0;
        var related = 0;
        var excluded = 0;
        foreach (var source in catalog.Rec)
        {
            var rec = catalog.Find(source.Serial)!;
            var game = new Rpcs3GameDescriptor(source.Serial, source.WikiName, env.GamePath, null, null, null);
            var plan = planner.BuildCfg(env.Install, game, rec);
            var expected = source.Binding == WikiGameIdRole.Primary
                ? source.Changes.Count : 0;
            Assert(plan.Items.Count == expected && plan.RecState.Total == expected,
                $"Catalog/runtime item parity failed for {source.Serial} ({source.Binding}).");
            Assert(plan.Items.All(item => item.StateIt.Binding == source.Binding && item.Class == item.StateIt.Class),
                $"Binding/class evidence changed for {source.Serial}.");
            var valid = expected > 0 && source.Changes.Any(change => change.Class is not
                WikiRecommendationClass.Ambiguous and not WikiRecommendationClass.Unsupported);
            if (!valid)
            {
                if (source.Changes.Count > 0) excluded++;
                continue;
            }
            discoverable++;
            if (source.Binding == WikiGameIdRole.RelatedEdition) related++;
            var vm = new PlanVm();
            vm.Load(plan);
            if (plan.Items.Count > 0 && !vm.IsEmpty && vm.EmptyTxt != UiText.GetText("Plan.NoRec")) runtime++;
            else missing++;
            if (source.Binding == WikiGameIdRole.RelatedEdition)
                Assert(plan.Items.All(item => !item.IsAction && !item.IsSel), $"Inherited action for {source.Serial}.");
        }
        Console.WriteLine($"TOTAL_SERIALS={catalog.Count}\nSERIALS_WITH_DISCOVERABLE_SETTINGS={discoverable}\nRUNTIME_DISCOVERABLE={runtime}\nSOURCE_PRESENT_RUNTIME_MISSING={missing}\nUNRESOLVED_RELATED_DISCOVERABLE={related}\nSEMANTICALLY_EXCLUDED_SERIALS_WITH_SETTINGS={excluded}");
        foreach (var group in catalog.Rec.SelectMany(rec => rec.Changes).GroupBy(change => change.Class))
            Console.WriteLine($"SETTINGS_CLASS_{group.Key}={group.Count()}");
        foreach (var group in catalog.Rec.Where(rec => rec.Changes.Count > 0).GroupBy(rec => rec.Binding))
            Console.WriteLine($"SETTINGS_BINDING_{group.Key}={group.Count()}");
        Assert(missing == 0, "Discoverable source evidence was lost by runtime planning/projection.");
    }

    private static void Patch_CorpusDiscovery(string root, string? officialPath)
    {
        officialPath ??= Path.Combine(AppContext.BaseDirectory, "Fixtures", "Official", "patch.yml");
        ScanPatchDiscovery(root, officialPath, PatSrc.Official, "PATCH");
        ScanPatchDiscovery(root, Path.Combine(RepoRoot(), "App", "Data", "can.yml"), PatSrc.Can, "CANARY");
    }

    private static void ScanPatchDiscovery(string root, string path, PatSrc source, string label)
    {
        var before = SHA256.HashData(File.ReadAllBytes(path));
        var text = File.ReadAllText(path);
        if (source == PatSrc.Can)
        {
            var normalized = new CanaryPatchImportService().Norm(text);
            Assert(normalized.IsOk, "Canary normalization failed: " + normalized.Error);
            text = normalized.NormText;
        }
        var read = new ReadSvc().ReadText(text, source, path);
        Assert(read.IsOk && read.Catalog.Entries.Count > 0, $"{label} parse failed: {read.Error}");
        // Event inventory accepts the repeated top-level Anchors blocks used by RPCS3.
        // Keep this independent of ReadSvc's source splitting and normalization.
        var parser = new Parser(new StringReader(text));
        var stack = new List<(bool Mapping, bool IsKey)>();
        var sourceDefinitions = new List<(string Key, string Title)>();
        var runtimeKey = string.Empty;
        var definitions = 0;
        var rejected = 0;
        var anchors = 0;
        var auxiliary = 0;
        while (parser.MoveNext())
        {
            var current = parser.Current;
            if (current is MappingEnd or SequenceEnd)
            {
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            if (current is not Scalar and not AnchorAlias and not MappingStart and not SequenceStart) continue;
            if (stack.Count > 0 && stack[^1].Mapping)
            {
                if (stack[^1].IsKey && current is Scalar scalar)
                {
                    if (stack.Count == 1) runtimeKey = scalar.Value;
                    else if (stack.Count == 2)
                    {
                        if (runtimeKey == "Anchors") anchors++;
                        else if (runtimeKey.StartsWith("PPU-", StringComparison.OrdinalIgnoreCase) ||
                                 runtimeKey.StartsWith("SPU-", StringComparison.OrdinalIgnoreCase) ||
                                 runtimeKey.StartsWith("PRX-", StringComparison.OrdinalIgnoreCase) ||
                                 runtimeKey.StartsWith("OVL-", StringComparison.OrdinalIgnoreCase) ||
                                 RunKey.TryParse(runtimeKey, out _))
                            sourceDefinitions.Add((runtimeKey, scalar.Value));
                        else auxiliary++;
                    }
                }
                stack[^1] = (true, !stack[^1].IsKey);
            }
            if (current is MappingStart or SequenceStart) stack.Add((current is MappingStart, true));
        }
        foreach (var (key, title) in sourceDefinitions)
        {
            definitions++;
            var normalizedKey = RunKey.TryParse(key, out var parsedKey)
                ? RunKey.Build(parsedKey.Kind, parsedKey.Hash, parsedKey.SerScope) : key;
            if (read.Catalog.Entries.Any(entry => entry.RunKey.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase) &&
                    entry.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))) continue;
            var rejectedRuntimeKey = !RunKey.TryParse(key, out _) &&
                read.Error?.Contains($"Skipping '{key}': the key is not generated by RPCS3.", StringComparison.Ordinal) == true;
            Assert(rejectedRuntimeKey || read.Error?.Split('\n').Any(line =>
                    line.Contains(key, StringComparison.OrdinalIgnoreCase) &&
                    line.Contains(title, StringComparison.OrdinalIgnoreCase)) == true,
                $"{label} source definition disappeared without diagnostics: {key}/{title}.");
            rejected++;
        }
        var env = GameSetupT.CreateEnvironment(Path.Combine(root, label));
        var useCases = AppComposition.Build(new Rpcs3MutationGuard(() => false));
        var catalog = read.Catalog;
        var targets = catalog.Entries.SelectMany(entry => entry.GameTgt).Where(target => !PatVer.IsAll(target.Serial))
            .Select(target => target.Serial).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var discovered = 0;
        var missing = 0;
        var previewMissing = 0;
        var setupMissing = 0;
        var bindings = 0;
        var provenance = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json")).Info;
        foreach (var serial in targets)
        {
            var game = new Rpcs3GameDescriptor(serial, serial, env.GamePath, null, null, null);
            var matches = useCases.PatchCatalog.Build(catalog, game);
            var expected = catalog.Entries.Where(entry => entry.GameTgt.Any(target => target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase))).ToArray();
            var vm = new PatPrev();
            vm.Load(matches);
            var setup = new GameSetupPlanningService().Build(env.Install, game, provenance,
                new PlanService().BuildCfg(env.Install, game, null), catalog);
            var found = 0;
            foreach (var entry in expected)
            {
                bindings++;
                if (!matches.Any(match => SameDiscoveryDefinition(match.Patch, entry))) { missing++; continue; }
                found++;
                if (!vm.Items.Any(item => SameDiscoveryDefinition(item.Match.Patch, entry))) previewMissing++;
                if (!setup.Patches.Any(item => SameDiscoveryDefinition(item.Match.Patch, entry))) setupMissing++;
            }
            if (found > 0) discovered++;
            Assert(matches.All(match => match.Status != MatStat.Match), "Serial-only evidence became Exact.");
        }
        var checkedTargets = 0;
        var exactTargets = 0;
        foreach (var entry in catalog.Entries)
        foreach (var target in entry.GameTgt.Where(target => !PatVer.IsAll(target.Serial)))
        foreach (var version in target.Versions.DefaultIfEmpty(PatVer.All))
        {
            var game = new Rpcs3GameDescriptor(target.Serial, target.Title, env.GamePath,
                PatVer.IsAll(version) ? null : version, null, null);
            var cmp = new PatchCompatibilityService();
            var exact = game with { PpuHash = entry.Hash, AuthPpu = [entry.Hash], SpuHash = entry.Hash,
                HCanPpu = [entry.Hash], HCanSpu = [entry.Hash], PrxRaw = [entry.Hash], OvlRaw = [entry.Hash] };
            var exactMatch = new PatchMatchService().Match(new PatCat(catalog.Version, [entry]), exact).Single();
            Assert(exactMatch.Status == MatStat.Match, $"Exact source target was not matched: {entry.RunKey}/{target.Serial}/{version}.");
            Assert(exactMatch.Cmp?.CanOn == true, "A valid exact source identity was blocked.");
            exactTargets++;
            var noHash = cmp.Evaluate(entry, [game]);
            Assert(noHash.Status != MatStat.Match, "Serial/version alone became Exact.");
            var wrongHash = new string(entry.NormHash?.StartsWith('a') == true ? 'b' : 'a', 40);
            var wrong = game with { PpuHash = wrongHash, AuthPpu = [wrongHash], SpuHash = wrongHash,
                HCanPpu = [wrongHash], HCanSpu = [wrongHash] };
            var wrongMatch = cmp.Evaluate(entry, [wrong]);
            Assert(wrongMatch.Status != MatStat.Match, $"Wrong hash became Exact: {entry.RunKey}.");
            if (entry.HashKind == HashKind.Ppu || entry.Class == PatClass.Canary)
                Assert(!PatPol.Can(wrongMatch), $"Definitive hash mismatch became actionable: {entry.RunKey}.");
            else
                Assert(wrongMatch.Status == MatStat.Unknown && wrongMatch.Cmp?.UseGuard == true,
                    "Incomplete module hash evidence must retain the existing RPCS3 guard policy.");
            var otherSerial = cmp.Evaluate(entry, [game with { Serial = "ZZZZ99999" }]);
            Assert(otherSerial.Status != MatStat.Match && !PatPol.Can(otherSerial), "Wrong serial became actionable.");
            if (target.Versions.Count > 0 && target.Versions.All(value => !PatVer.IsAll(value)))
            {
                var wrongVersion = cmp.Evaluate(entry, [game with { Version = "99.99" }]);
                Assert(wrongVersion.Status != MatStat.Match && !PatPol.Can(wrongVersion), "Wrong version became actionable.");
            }
            checkedTargets++;
        }
        Assert(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Patch source was mutated.");
        var globalChecked = 0;
        foreach (var entry in catalog.Entries.Where(entry => entry.GameTgt.Any(target => PatVer.IsAll(target.Serial))))
        {
            // A wildcard target explicitly accepts this synthetic game identity.
            var game = new Rpcs3GameDescriptor("TEST00001", "Wildcard fixture", env.GamePath, null,
                entry.Hash, entry.Hash, AuthPpu: [entry.Hash], HCanPpu: [entry.Hash], HCanSpu: [entry.Hash],
                PrxRaw: [entry.Hash], OvlRaw: [entry.Hash]);
            var singleton = new PatCat(catalog.Version, [entry]);
            var vm = new PatPrev();
            var exact = useCases.PatchCatalog.Build(singleton, game);
            vm.Load(exact);
            Assert(exact.Count == 1 && exact[0].Status == MatStat.Match && vm.Items.Count == 1,
                $"Wildcard source with exact hash disappeared: {entry.RunKey}.");
            var unknown = useCases.PatchCatalog.Build(singleton, game with { PpuHash = null, SpuHash = null,
                AuthPpu = [], HCanPpu = [], HCanSpu = [], PrxRaw = [], OvlRaw = [] });
            vm.Load(unknown);
            Assert(unknown.Count == 1 && !PatPol.Can(unknown[0]) && vm.Items.Count == 0,
                "Unbound wildcard evidence must remain non-actionable and hidden from unrelated games.");
            globalChecked++;
        }
        Assert(!File.Exists(env.Install.PatCfg), "Patch discovery wrote patch_config.yml.");
        Console.WriteLine($"{label}_EXACT_SOURCE_TARGETS={exactTargets}");
        Console.WriteLine($"{label}_AUXILIARY_MAPPING_VALUES={auxiliary}");
        Console.WriteLine($"{label}_GAME_SETUP_MISSING={setupMissing}\n{label}_GLOBAL_TARGETS_VERIFIED={globalChecked}");
        Console.WriteLine($"{label}_SOURCE={path}\n{label}_SOURCE_DEFINITIONS={definitions}\n{label}_ANCHORS={anchors}\n{label}_PARSED={catalog.Entries.Count}\n{label}_EXPLICIT_REJECTIONS={rejected}\n{label}_TOTAL_SERIALS={targets.Length}\n{label}_RUNTIME_DISCOVERABLE={discovered}\n{label}_SOURCE_BINDINGS={bindings}\n{label}_SOURCE_PRESENT_RUNTIME_MISSING={missing}\n{label}_PREVIEW_MISSING={previewMissing}\n{label}_IDENTITY_NEGATIVE_CASES={checkedTargets}\n{label}_GLOBAL_DEFINITIONS={catalog.Entries.Count(entry => entry.GameTgt.Any(target => PatVer.IsAll(target.Serial)))}");
        Assert(missing == 0 && previewMissing == 0 && setupMissing == 0, $"{label} source evidence disappeared during runtime discovery/preview.");
    }

    private static bool SameDiscoveryDefinition(Rpcs3PatchEntry left, Rpcs3PatchEntry right) =>
        left.RunKey.Equals(right.RunKey, StringComparison.OrdinalIgnoreCase) &&
        left.Title.Equals(right.Title, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.DefPrint, right.DefPrint, StringComparison.Ordinal);
}
