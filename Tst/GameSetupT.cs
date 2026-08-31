using System.IO;
using System.Security.Cryptography;
using System.Text;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

public static class GameSetupT
{
    private static readonly string[] SpecificHashWarnings =
    [
        "HASHREQUIRED: Specific required A",
        "HASHREQUIRED: Specific required B"
    ];

    public static void Run(string tempRoot)
    {
        IdentityAndCompatibility(tempRoot);
        PatchRequirementCatalog(tempRoot);
        WarningScopeRegression(tempRoot);
        GlobalUnknownWarningProjection(tempRoot);
        GlobalExactHashProjection(tempRoot);
        SpecificHashRequiredProjection(tempRoot);
        OtherSerialProjection(tempRoot);
        MixedWildcardCatalogProjection(tempRoot);
        GlobalUnknownSafetyParity(tempRoot);
        WildcardWarningGameSwitch(tempRoot);
        ShippedArtemisPipeline(tempRoot);
        RealDataWarningScope(tempRoot);
        OfficialSnapshotRegression(tempRoot);
        CorpusParsersAndPlanConflicts(tempRoot);
        EndToEndPrepareAndIdempotency(tempRoot);
        ConcurrencyAndRollback(tempRoot);
        Console.WriteLine("Game Setup focused regressions PASS.");
    }

    public static void IdentityAndCompatibility(string tempRoot)
    {
        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-identity"));
        var canary = LoadCanaryEntry();
        var game = GameFor(canary, env.GamePath);
        var official = OfficialFor(canary, env.Install.PatPath);
        var recommendation = Recommendation(game.Serial);
        var catalog = RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            "2026-06-13",
            [recommendation],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('a', 64));
        var patchCatalog = new PatCat("1.2", [official, canary]);
        var useCase = AppComposition.Build(
            new Rpcs3MutationGuard(() => false)).GameSetup;
        var setup = useCase.Build(
            env.Install,
            game,
            catalog,
            patchCatalog);

        Assert(setup.Game.Serial == game.Serial &&
               setup.Game.Version == game.Version &&
               setup.Game.InstallPath == Path.GetFullPath(env.GamePath) &&
               setup.Game.CustomConfigPath == Path.Combine(
                   env.Install.CustPath,
                   $"config_{game.Serial}.yml"),
            "Game identity did not retain serial/version/install/config evidence.");
        Assert(setup.Game.PpuHashes.Count + setup.Game.SpuHashes.Count > 0 &&
               setup.Game.HashEvidence == SetupIdentityEvidence.RuntimeLog,
            "Game identity did not retain PPU/SPU runtime evidence.");
        Assert(setup.OfficialPatches.Count == 1 &&
               setup.CanaryPatches.Count == 1 &&
               setup.OfficialPatches[0].Provenance.Kind is
                   SetupPatchSourceKind.OfficialMain or
                   SetupPatchSourceKind.OfficialSpu &&
               setup.CanaryPatches[0].Provenance.Kind ==
                   SetupPatchSourceKind.Canary,
            "Official and Canary provenance was not kept separate.");
        Assert(setup.Patches.All(candidate => !candidate.DefaultSelected),
            "Patch candidates must never be selected by default.");

        var unknownVersion = game with { Version = null };
        var unknown = new PatchCompatibilityService().Evaluate(
            official,
            [unknownVersion]);
        Assert(unknown.Status == MatStat.VerUnk &&
               unknown.Cmp?.CanOn == false,
            "A version-scoped patch must fail closed when GameVersion is unresolved.");

        var mismatchHash = new string('f', 40);
        if (mismatchHash.Equals(canary.NormHash, StringComparison.OrdinalIgnoreCase))
        {
            mismatchHash = new string('e', 40);
        }

        var mismatchGame = game with
        {
            PpuHash = mismatchHash,
            PpuRaw = [mismatchHash],
            AuthPpu = [mismatchHash],
            HCanPpu = canary.HashKind == HashKind.Ppu
                ? [mismatchHash]
                : game.HCanPpu,
            HCanSpu = canary.HashKind == HashKind.Spu
                ? [mismatchHash]
                : game.HCanSpu
        };
        var officialMismatch = new PatchCompatibilityService().Evaluate(
            official with
            {
                HashKind = HashKind.Ppu,
                Hash = canary.NormHash!,
                KeySer = null
            },
            [mismatchGame]);
        var canaryMismatch = new PatchCompatibilityService().Evaluate(
            canary,
            [mismatchGame]);
        Assert(officialMismatch.Status == MatStat.HashBad &&
               officialMismatch.Cmp?.CanOn == false,
            "A reliable official main-PPU mismatch must block activation.");
        Assert(canaryMismatch.Status == MatStat.HashBad &&
               canaryMismatch.Cmp?.CanOn == false,
            "A discovered Canary executable mismatch must block activation.");

        var installed = canary with { Source = PatSrc.Imported };
        Assert(GameSetupPlanningService.ClassifyDuplicate(canary, [installed]) ==
               SetupDuplicateStatus.ExactDuplicate,
            "Exact Canary duplicates were not classified deterministically.");
        Assert(GameSetupPlanningService.ClassifyDuplicate(
                   canary with { PatVer = "2.0" },
                   [installed with { PatVer = "1.0" }]) ==
               SetupDuplicateStatus.NewerVersion,
            "A newer incoming patch version was not classified as an update.");
        Assert(GameSetupPlanningService.ClassifyDuplicate(
                   canary with { PatVer = "1.0" },
                   [installed with { PatVer = "2.0" }]) ==
               SetupDuplicateStatus.OlderVersion,
            "An older incoming patch version was not classified as a downgrade.");
        Assert(GameSetupPlanningService.ClassifyDuplicate(
                   canary with { DefPrint = "incoming" },
                   [installed with { DefPrint = "installed" }]) ==
               SetupDuplicateStatus.ConflictingPatch,
            "Equal-version conflicting patch bodies were not rejected.");
        Assert(GameSetupPlanningService.ClassifyDuplicate(
                   canary,
                   [installed with { Hash = mismatchHash }]) ==
               SetupDuplicateStatus.SameNameDifferentHash,
            "Same-name/different-hash patches were not distinguished.");
        Assert(GameSetupPlanningService.ClassifyDuplicate(
                   canary,
                   [installed with { Title = "Different title", PatVer = "9.0" }]) ==
               SetupDuplicateStatus.SameHashDifferentVersion,
            "Same-hash/different-version patches were not distinguished.");

        var collisionOfficial = canary with
        {
            Title = "Runtime identity collision",
            Source = PatSrc.Official,
            Class = PatClass.Main,
            Risk = PatRisk.SafeFix,
            DefPrint = "official-body"
        };
        var collisionCanary = canary with
        {
            Title = collisionOfficial.Title,
            Source = PatSrc.Can,
            Class = PatClass.Canary,
            Risk = PatRisk.Canary,
            DefPrint = "canary-body"
        };
        var collisionSetup = useCase.Build(
            env.Install,
            game,
            catalog,
            new PatCat("1.2", [collisionOfficial, collisionCanary]));
        var collisionPreview = useCase.Preview(
            collisionSetup,
            new GameSetupSelection(
                [],
                [GameSetupPlanningService.PatchIdentity(collisionCanary)]));
        Assert(!collisionPreview.CanApply &&
               collisionPreview.Conflicts.Any(conflict =>
                   conflict.Contains(
                       "runtime identity collision",
                       StringComparison.OrdinalIgnoreCase)),
            "Game Setup must fail closed when a selected Canary/Artemis source collides with a different resident RPCS3 body under the same runtime identity.");

        Console.WriteLine("Game Setup identity and compatibility PASS.");
    }

    public static void PatchRequirementCatalog(string tempRoot)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "patch_requirements.json");
        var catalog = new PatchRequirementCatalogService().Load(path);
        Assert(catalog.SnapshotDate == "2026-06-13" &&
               catalog.SourceIdentity.Contains("Canary", StringComparison.OrdinalIgnoreCase) &&
               catalog.Rules.Count == 2,
            "Pinned patch requirement catalog provenance or rule count is incorrect.");

        var canPath = BundledCanaryPatchService.GetPath(AppContext.BaseDirectory);
        var canText = R3Bound.ReadUtf8(canPath, R3Limits.PatCatBy, "can.yml");
        var normalized = new CanaryPatchImportService().Norm(canText);
        Assert(normalized.IsOk, "Pinned Canary catalog could not be normalized for requirement resolution.");
        var read = new ReadSvc().ReadText(normalized.NormText, PatSrc.Can, canPath);
        var gow = read.Catalog.Entries.FirstOrDefault(entry =>
            entry.Title == "Unlock FPS" && entry.Serials.Contains("BCUS98111") &&
            entry.GameVer.Contains("01.03"));
        Assert(gow is not null, "Pinned God of War 3 Canary patch was not found.");
        var resolved = new PatchRequirementCatalogService().Resolve(catalog, [gow!]);
        Assert(catalog.Rules.All(rule => rule.EvidenceStatus == "UNPROVEN_VALUE") &&
               resolved.Count == 0 &&
               catalog.Rules.All(rule => rule.Requirements.Single().Setting.Key == "VBlank Rate" &&
                   rule.Requirements.Single().Setting.Value == "120"),
            "Unproven VBlank value must remain evidence-only and never become an automatic requirement.");

        var badPath = Path.Combine(tempRoot, "bad-requirements.json");
        File.WriteAllText(badPath, "{\"schemaVersion\":\"1\",\"sourceIdentity\":\"x\",\"snapshotDate\":\"x\",\"rules\":[{\"patchTitle\":\"x\",\"requirements\":[]}]}");
        var rejected = false;
        try { _ = new PatchRequirementCatalogService().Load(badPath); }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Malformed patch requirement catalogs must fail closed.");
    }

    public static void WarningScopeRegression(string tempRoot)
    {
        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-warning-scope"));
        const string hashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string hashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var read = new ReadSvc();
        var patchA = read.ReadText(
            PatchYaml(hashA, "Game A hash-required patch", "BLUS30010", "01.00", "RPCS3", "A note", "A", "1.0"),
            PatSrc.Official,
            "game-a-patch.yml").Catalog.Entries.Single();
        var patchB = read.ReadText(
            PatchYaml(hashB, "Game B exact patch", "BLUS30011", "01.00", "RPCS3", "B note", "B", "1.0"),
            PatSrc.Official,
            "game-b-patch.yml").Catalog.Entries.Single();
        var gameA = new Rpcs3GameDescriptor(
            "BLUS30010", "Game A", env.GamePath, "01.00",
            "cccccccccccccccccccccccccccccccccccccccc", null,
            PpuRaw: ["cccccccccccccccccccccccccccccccccccccccc"],
            HashSrc: "RPCS3.log", HashEvi: HashEvi.PlainLog,
            AuthPpu: ["cccccccccccccccccccccccccccccccccccccccc"]);
        var gameB = new Rpcs3GameDescriptor(
            "BLUS30011", "Game B", env.GamePath, "01.00", hashB, null,
            PpuRaw: [hashB], HashSrc: "RPCS3.log", HashEvi: HashEvi.PlainLog,
            AuthPpu: [hashB]);
        var recommendations = RecSvc.FromRec(
            "Setup warning fixture",
            "2026-06-13",
            [
                new WikiRec(gameA.Serial, gameA.Title, []),
                new WikiRec(gameB.Serial, gameB.Title, [])
            ],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('e', 64));
        var catalog = new PatCat(
            "1.2",
            [patchA, patchB],
            [new PatDiag(
                "GLOBAL_FIXTURE_DIAG",
                "HASHREQUIRED: unrelated God of War: Ascension",
                PatSrc.Official,
                PatSrc.Can)]);
        var useCase = AppComposition.Build(
            new Rpcs3MutationGuard(() => false)).GameSetup;

        GameSetupPreview PreviewFor(Rpcs3GameDescriptor game)
        {
            var plan = useCase.Build(env.Install, game, recommendations, catalog);
            return useCase.Preview(plan, new GameSetupSelection([], []));
        }

        var a = PreviewFor(gameA);
        Assert(a.Warnings.Any(warning => warning.Contains("Game A", StringComparison.Ordinal)) &&
               a.Warnings.All(warning => !warning.Contains("Game B", StringComparison.Ordinal)) &&
               a.Warnings.All(warning => !warning.Contains("God of War", StringComparison.Ordinal)),
            "Game A warnings were not scoped to the selected serial or global diagnostics leaked.");

        var b = PreviewFor(gameB);
        Assert(b.Warnings.Count == 0,
            "Game A or global warnings leaked into a current game with no applicable warnings.");

        var sequence = new[] { gameA, gameB, gameA, gameB }
            .Select(PreviewFor)
            .ToArray();
        Assert(sequence[0].Warnings.Count > 0 && sequence[1].Warnings.Count == 0 &&
               sequence[2].Warnings.Count > 0 && sequence[3].Warnings.Count == 0,
            "Warnings leaked across four consecutive selected-game contexts.");

        var tenSwitches = Enumerable.Range(0, 10)
            .Select(index => PreviewFor(index % 2 == 0 ? gameA : gameB))
            .ToArray();
        Assert(tenSwitches.Select((preview, index) =>
                       index % 2 == 0
                           ? preview.Warnings.Count > 0
                           : preview.Warnings.Count == 0)
                   .All(result => result),
            "Warnings leaked across ten consecutive selected-game switches.");

        var mismatched = gameA with { Version = "02.00" };
        var mismatch = PreviewFor(mismatched);
        Assert(mismatch.Warnings.Count == 0,
            "A patch for another game version was projected as an applicable warning.");

        var selectedA = useCase.Build(env.Install, gameA, recommendations, catalog);
        var selectedPreview = useCase.Preview(
            selectedA,
            new GameSetupSelection(
                [],
                [selectedA.OfficialPatches.Single().Identity]));
        Assert(selectedPreview.Warnings.Any(warning => warning == "A note"),
            "Selected patch notes were not retained in the selected-only note projection.");
        Console.WriteLine("Game Setup warning scope and selection regressions PASS.");
    }

    public static void GlobalUnknownWarningProjection(string tempRoot)
    {
        const string hash = "1111111111111111111111111111111111111111";
        const string title = "Global unknown fixture";
        var read = new ReadSvc().ReadText(
            GlobalPatchYaml(hash, title),
            PatSrc.Official,
            "global-unknown.yml");
        Assert(read.IsOk, "The All/All warning fixture could not be parsed.");
        var patch = read.Catalog.Entries.Single();
        var game = SetupGame("BLUS31000", "01.00", ppuHash: new string('a', 40));
        var fixture = SetupPlan(
            tempRoot,
            "global-unknown-warning",
            game,
            [patch]);
        var candidate = fixture.Plan.Patches.Single();

        Assert(PatVer.IsAll(candidate.Match.TgtSer) &&
               PatVer.IsAll(candidate.Match.TgtVer) &&
               candidate.Match.Status == MatStat.Unknown &&
               candidate.Compatibility == SetupCompatibilityStatus.HashRequired,
            "The regression fixture did not produce an All/All HashRequired match.");
        Assert(candidate.Match.Cmp?.CanOn == false && !candidate.CanSelect,
            "The global unknown patch must remain blocked by compatibility safety.");
        Assert(fixture.Plan.Warnings.All(warning => !warning.Equals(
                $"HASHREQUIRED: {title}",
                StringComparison.Ordinal)),
            "An All/All patch without positive hash evidence leaked into Setup warnings.");
        Console.WriteLine("Game Setup global unknown warning projection PASS.");
    }

    public static void GlobalExactHashProjection(string tempRoot)
    {
        const string hash = "2222222222222222222222222222222222222222";
        var patch = SetupPatch(
            "Global exact fixture",
            PatVer.All,
            PatVer.All,
            hash,
            HashKind.Spu,
            PatSrc.Official);
        var game = SetupGame(
            "BLUS31001",
            "01.00",
            ppuHash: new string('a', 40),
            spuHash: hash);
        var fixture = SetupPlan(
            tempRoot,
            "global-exact-hash",
            game,
            [patch]);
        var candidate = fixture.Plan.Patches.Single();

        Assert(PatVer.IsAll(candidate.Match.TgtSer) &&
               candidate.Match.Status == MatStat.Match &&
               candidate.Compatibility == SetupCompatibilityStatus.ExactMatch &&
               candidate.Match.Cmp?.CanOn == true &&
               candidate.CanSelect,
            "A global patch with positive exact hash evidence was suppressed or blocked.");
        Console.WriteLine("Game Setup global exact hash projection PASS.");
    }

    public static void SpecificHashRequiredProjection(string tempRoot)
    {
        const string serial = "BLUS31002";
        const string title = "Specific HashRequired fixture";
        var patch = SetupPatch(
            title,
            serial,
            "01.00",
            "3333333333333333333333333333333333333333",
            HashKind.Spu,
            PatSrc.Unknown);
        var fixture = SetupPlan(
            tempRoot,
            "specific-hash-required",
            SetupGame(serial, "01.00", ppuHash: new string('a', 40)),
            [patch]);
        var candidate = fixture.Plan.Patches.Single();

        Assert(!PatVer.IsAll(candidate.Match.TgtSer) &&
               candidate.Match.TgtSer == serial &&
               candidate.Compatibility == SetupCompatibilityStatus.HashRequired &&
               candidate.Match.Cmp?.CanOn == false &&
               !candidate.CanSelect,
            "The specific current-game fixture did not remain a blocked HashRequired candidate.");
        Assert(fixture.Plan.Warnings.Contains(
                $"HASHREQUIRED: {title}",
                StringComparer.Ordinal),
            "A specific current-game HashRequired warning was incorrectly suppressed.");
        Console.WriteLine("Game Setup specific HashRequired warning projection PASS.");
    }

    public static void OtherSerialProjection(string tempRoot)
    {
        var patch = SetupPatch(
            "Other serial fixture",
            "BLUS31999",
            "01.00",
            "4444444444444444444444444444444444444444",
            HashKind.Spu,
            PatSrc.Unknown);
        var fixture = SetupPlan(
            tempRoot,
            "other-serial",
            SetupGame("BLUS31003", "01.00", ppuHash: new string('a', 40)),
            [patch]);

        Assert(fixture.Plan.Patches.Count == 0 &&
               fixture.Plan.Warnings.All(warning =>
                   !warning.Contains("Other serial fixture", StringComparison.Ordinal)),
            "A patch targeting another explicit serial entered the current setup plan.");
        Console.WriteLine("Game Setup other-serial exclusion PASS.");
    }

    public static void MixedWildcardCatalogProjection(string tempRoot)
    {
        const string serial = "BLUS31004";
        const string exactHash = "5555555555555555555555555555555555555555";
        var globalUnknown = Enumerable.Range(1, 20)
            .Select(index => SetupPatch(
                $"Global unknown {index:00}",
                PatVer.All,
                PatVer.All,
                index.ToString("x40", System.Globalization.CultureInfo.InvariantCulture),
                HashKind.Spu,
                PatSrc.Official))
            .ToArray();
        var specific = new[]
        {
            SetupPatch(
                "Specific required A",
                serial,
                "01.00",
                "6666666666666666666666666666666666666666",
                HashKind.Spu,
                PatSrc.Unknown),
            SetupPatch(
                "Specific required B",
                serial,
                "01.00",
                "7777777777777777777777777777777777777777",
                HashKind.Spu,
                PatSrc.Unknown)
        };
        var globalExact = SetupPatch(
            "Global exact selected-game evidence",
            PatVer.All,
            PatVer.All,
            exactHash,
            HashKind.Spu,
            PatSrc.Official);
        var fixture = SetupPlan(
            tempRoot,
            "mixed-wildcard-catalog",
            SetupGame(
                serial,
                "01.00",
                ppuHash: new string('a', 40),
                spuHash: exactHash),
            [.. globalUnknown, .. specific, globalExact]);
        var hashWarnings = fixture.Plan.Warnings.Where(warning =>
            warning.StartsWith("HASHREQUIRED:", StringComparison.Ordinal)).ToArray();

        Assert(fixture.Plan.Patches.Count == 23,
            "The mixed catalog did not retain all compatibility candidates.");
        Assert(hashWarnings.Order(StringComparer.Ordinal).SequenceEqual(
                SpecificHashWarnings,
                StringComparer.Ordinal),
            "Mixed catalog warnings were not limited to specific current-game HashRequired patches.");
        Assert(fixture.Plan.Patches.Single(candidate =>
                   candidate.Match.Patch.Title == "Global exact selected-game evidence").CanSelect,
            "The exact matching global patch was suppressed from compatibility selection.");
        Console.WriteLine("Game Setup mixed wildcard catalog projection PASS.");
    }

    public static void GlobalUnknownSafetyParity(string tempRoot)
    {
        var patch = SetupPatch(
            "Global safety fixture",
            PatVer.All,
            PatVer.All,
            "8888888888888888888888888888888888888888",
            HashKind.Spu,
            PatSrc.Official);
        var fixture = SetupPlan(
            tempRoot,
            "global-safety-parity",
            SetupGame("BLUS31005", "01.00", ppuHash: new string('a', 40)),
            [patch]);
        var candidate = fixture.Plan.Patches.Single();
        var beforeFiles = Directory.GetFiles(
            fixture.Environment.Install.RootPath,
            "*",
            SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var preview = fixture.UseCase.Preview(
            fixture.Plan,
            new GameSetupSelection([], [candidate.Identity]));

        Assert(candidate.Match.Status == MatStat.Unknown &&
               candidate.Match.Cmp?.CanOn == false &&
               !candidate.CanSelect,
            "Global unknown compatibility or enable eligibility expanded.");
        Assert(!preview.CanApply && preview.Conflicts.Count > 0,
            "Global unknown apply eligibility expanded.");
        ExpectFailure<InvalidOperationException>(() =>
            fixture.UseCase.Apply(fixture.Environment.Install, preview));
        var afterFiles = Directory.GetFiles(
            fixture.Environment.Install.RootPath,
            "*",
            SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Assert(beforeFiles.SequenceEqual(afterFiles, StringComparer.Ordinal),
            "A blocked global unknown patch wrote files during preview/apply rejection.");
        Console.WriteLine("Game Setup global unknown safety parity PASS.");
    }

    public static void WildcardWarningGameSwitch(string tempRoot)
    {
        var globalUnknown = SetupPatch(
            "Global switch noise",
            PatVer.All,
            PatVer.All,
            "9999999999999999999999999999999999999999",
            HashKind.Spu,
            PatSrc.Official);
        var games = new[]
        {
            SetupGame("BLUS31006", "01.00", ppuHash: new string('a', 40)),
            SetupGame("BLUS31007", "01.00", ppuHash: new string('b', 40)),
            SetupGame("BLUS31008", "01.00", ppuHash: new string('c', 40))
        };
        var sequence = Enumerable.Range(0, 12)
            .Select(index => SetupPlan(
                tempRoot,
                $"wildcard-switch-{index:00}",
                games[index % games.Length],
                [globalUnknown]).Plan)
            .ToArray();

        Assert(sequence.All(plan =>
                   plan.Patches.Count == 1 &&
                   plan.Patches.Single().Match.Status == MatStat.Unknown &&
                   !plan.Patches.Single().CanSelect &&
                   plan.Warnings.All(warning =>
                       !warning.Contains("Global switch noise", StringComparison.Ordinal))),
            "Global unknown warning noise appeared during repeated game switches.");
        Console.WriteLine("Game Setup wildcard game-switch projection PASS.");
    }

    public static void RealDataWarningScope(string tempRoot)
    {
        var realSerials = new[]
        {
            "BLUS30443",
            "BLES01128",
            "BCUS98125",
            "BCAS20272",
            "BCUS98232"
        };
        var baseDirectory = AppContext.BaseDirectory;
        var reader = new ReadSvc();
        var catalogs = new List<PatCat>();
        var canPath = BundledCanaryPatchService.GetPath(baseDirectory);
        if (File.Exists(canPath))
        {
            var normalized = new CanaryPatchImportService().Norm(
                R3Bound.ReadUtf8(canPath, R3Limits.PatCatBy, "can.yml"));
            Assert(normalized.IsOk, "The shipped Canary catalog could not be normalized.");
            var canary = reader.ReadText(normalized.NormText, PatSrc.Can, canPath);
            Assert(canary.IsOk, "The shipped Canary catalog could not be parsed.");
            catalogs.Add(canary.Catalog);
        }

        var artemisPath = BundledCheatPatchService.GetBndl(baseDirectory);
        if (File.Exists(artemisPath))
        {
            var artemis = reader.Read(artemisPath, PatSrc.Artemis);
            Assert(artemis.IsOk, "The shipped Artemis catalog could not be parsed.");
            catalogs.Add(artemis.Catalog);
        }

        var entries = catalogs
            .SelectMany(catalog => catalog.Entries)
            .Where(entry => entry.GameTgt.Any(target =>
                PatVer.IsAll(target.Serial) ||
                realSerials.Contains(target.Serial, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        Assert(entries.Length > 0, "The real-data warning scope fixture found no requested serials.");

        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-real-warning-scope"));
        var games = realSerials
            .Select(serial =>
            {
                var entry = entries.FirstOrDefault(candidate =>
                    candidate.Serials.Contains(serial, StringComparer.OrdinalIgnoreCase) &&
                    candidate.GameVer.Any(version => !PatVer.IsAll(version)));
                return new Rpcs3GameDescriptor(
                    serial,
                    entry?.GameTgt.FirstOrDefault(target => target.Serial.Equals(
                        serial, StringComparison.OrdinalIgnoreCase))?.Title ??
                        $"Real data {serial}",
                    env.GamePath,
                    entry?.GameVer.FirstOrDefault(version => !PatVer.IsAll(version)) ??
                        "01.00",
                    null,
                    null);
            })
            .Where(game => game is not null)
            .Cast<Rpcs3GameDescriptor>()
            .ToArray();
        Assert(games.Length == realSerials.Length,
            "The requested real-data serial regression set is incomplete.");

        var recommendations = RecSvc.FromRec(
            "Real Setup warning scope fixture",
            "2026-06-13",
            games.Select(game => new WikiRec(game.Serial, game.Title, [])).ToArray(),
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('f', 64));
        var useCase = AppComposition.Build(
            new Rpcs3MutationGuard(() => false)).GameSetup;
        var patchCatalog = new PatCat("1.2", entries);
        foreach (var game in games)
        {
            var plan = useCase.Build(env.Install, game, recommendations, patchCatalog);
            var preview = useCase.Preview(plan, new GameSetupSelection([], []));
            var globalUnknown = plan.Patches
                .Where(candidate =>
                    candidate.Compatibility == SetupCompatibilityStatus.HashRequired &&
                    PatVer.IsAll(candidate.Match.TgtSer))
                .ToArray();
            var specificHashRequired = plan.Patches
                .Where(candidate =>
                    candidate.Compatibility == SetupCompatibilityStatus.HashRequired &&
                    !PatVer.IsAll(candidate.Match.TgtSer))
                .Select(candidate =>
                    $"HASHREQUIRED: {candidate.Match.Patch.Title}")
                .ToHashSet(StringComparer.Ordinal);
            var hashRequiredWarnings = preview.Warnings
                .Where(warning => warning.StartsWith(
                    "HASHREQUIRED: ", StringComparison.Ordinal))
                .ToArray();
            var suppressed = globalUnknown.Count(candidate =>
                !hashRequiredWarnings.Contains(
                    $"HASHREQUIRED: {candidate.Match.Patch.Title}",
                    StringComparer.Ordinal));
            Assert(preview.Plan.Game.Serial == game.Serial &&
                   suppressed == globalUnknown.Length &&
                   hashRequiredWarnings.All(specificHashRequired.Contains),
                $"Real-data Setup warning escaped serial scope for {game.Serial}.");
            Console.WriteLine($"REAL_SETUP_WARNING_DUMP Serial={game.Serial}");
            Console.WriteLine($"Total Patch Candidates={plan.Patches.Count}");
            Console.WriteLine($"Specific HashRequired Warnings={specificHashRequired.Count}");
            Console.WriteLine($"Global Unknown Suppressed={suppressed}");
            Console.WriteLine(
                $"Final SetupWarnings={(preview.Warnings.Count == 0 ? "<none>" : string.Join(" | ", preview.Warnings))}");
        }

        Console.WriteLine("Game Setup real-data warning scope PASS.");
    }

    public static void ShippedArtemisPipeline(string tempRoot)
    {
        var environment = CreateEnvironment(
            Path.Combine(tempRoot, "setup-shipped-artemis"));
        var services = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var artemisPath = BundledCheatPatchService.GetBndl(
            AppContext.BaseDirectory);
        Assert(File.Exists(artemisPath),
            "The shipped Artemis catalog is missing from the runtime output.");

        var rawEntries = File.ReadLines(artemisPath)
            .Count(line =>
                line.Length > 1 &&
                !char.IsWhiteSpace(line[0]) &&
                line.EndsWith(':') &&
                !line.StartsWith("Version:", StringComparison.Ordinal));
        var parsed = new ReadSvc().Read(
            artemisPath,
            PatSrc.Artemis);
        Assert(parsed.IsOk && parsed.Catalog.Entries.Count > 0,
            "The shipped Artemis catalog did not parse into any entries.");

        var loaded = services.PatchCatalog.Load(environment.Install);
        Assert(loaded.Cat is not null,
            "PatchCatalogUseCase did not return the bundled patch catalog: " + loaded.Error);
        var artemisStatus = loaded.SrcStat.Single(status =>
            status.Source == PatSrc.Artemis &&
            status.Name == BundledCheatPatchService.BndlName);
        var mergedArtemis = loaded.Cat!.Entries
            .Where(entry => entry.Source == PatSrc.Artemis)
            .ToArray();
        Assert(artemisStatus is { Exists: true, Loaded: true, EntNum: > 0 } &&
               mergedArtemis.Length > 0,
            "PatchCatalogUseCase did not retain Artemis through metadata and merge.");

        var known = parsed.Catalog.Entries.First(entry =>
            entry.Source == PatSrc.Artemis &&
            entry.HashKind == HashKind.Ppu &&
            entry.NormHash is not null &&
            entry.GameTgt.Any(target =>
                !PatVer.IsAll(target.Serial) &&
                target.Versions.Any(version => !PatVer.IsAll(version))) &&
            mergedArtemis.Any(merged =>
                merged.RunKey.Equals(entry.RunKey, StringComparison.OrdinalIgnoreCase) &&
                merged.Title.Equals(entry.Title, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(merged.DefPrint, entry.DefPrint, StringComparison.OrdinalIgnoreCase)));
        var mergedKnown = mergedArtemis.Single(entry =>
            entry.RunKey.Equals(known.RunKey, StringComparison.OrdinalIgnoreCase) &&
            entry.Title.Equals(known.Title, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.DefPrint, known.DefPrint, StringComparison.OrdinalIgnoreCase));
        var target = mergedKnown.GameTgt.First(candidate =>
            !PatVer.IsAll(candidate.Serial) &&
            candidate.Versions.Any(version => !PatVer.IsAll(version)));
        var version = target.Versions.First(value => !PatVer.IsAll(value));
        var hash = mergedKnown.NormHash!;
        var game = new Rpcs3GameDescriptor(
            target.Serial,
            target.Title,
            environment.GamePath,
            version,
            hash,
            null,
            PpuRaw: [hash],
            HashSrc: "Shipped Artemis pipeline fixture",
            HashEvi: HashEvi.PlainLog,
            AuthPpu: [hash]);
        var recommendations = RecSvc.FromRec(
            "Shipped Artemis pipeline fixture",
            "2026-08-28",
            [new WikiRec(game.Serial, game.Title, [])],
            new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero),
            new string('e', 64));
        var matches = services.PatchCatalog.Build(loaded.Cat, game);
        var matchingArtemis = matches
            .Where(match => match.Patch.Source == PatSrc.Artemis)
            .ToArray();
        var plan = services.GameSetup.Build(
            environment.Install,
            game,
            recommendations,
            loaded.Cat);
        var candidate = plan.ArtemisPatches.Single(item =>
            item.Identity == GameSetupPlanningService.PatchIdentity(mergedKnown));
        Assert(candidate.Match.Patch.Source == PatSrc.Artemis &&
               candidate.Provenance.Kind == SetupPatchSourceKind.Artemis &&
               candidate.Compatibility == SetupCompatibilityStatus.ExactMatch &&
               candidate.CanSelect,
            "The known shipped Artemis entry did not retain source, provenance, and exact compatibility.");

        var preview = services.GameSetup.Preview(
            plan,
            new GameSetupSelection([], [candidate.Identity]));
        Assert(preview.SelectedPatches.Single().Identity == candidate.Identity &&
               preview.Files.Any(file => file.Kind == SetupFileKind.ArtemisTitlePatch),
            "The selected Artemis candidate did not reach Game Setup preview.");

        var patchPreview = new R3Config.Vm.PatPrev();
        patchPreview.Load(matchingArtemis);
        var uiCandidate = patchPreview.ChtItems.Single(item =>
            GameSetupPlanningService.PatchIdentity(item.Match.Patch) == candidate.Identity);
        uiCandidate.IsSel = true;
        Assert(patchPreview.ChtGrps.Count > 0 &&
               patchPreview.SelCht().Single().Patch.Source == PatSrc.Artemis,
            "The Artemis match did not reach the UI collection and selection path.");

        var wrongVersion = game with { Version = "99.99" };
        var wrongVersionPlan = services.GameSetup.Build(
            environment.Install,
            wrongVersion,
            recommendations,
            loaded.Cat);
        var wrongVersionCandidate = wrongVersionPlan.ArtemisPatches.Single(item =>
            item.Identity == candidate.Identity);
        var wrongHash = new string(hash[0] == 'f' ? 'e' : 'f', 40);
        var wrongHashGame = game with
        {
            PpuHash = wrongHash,
            PpuRaw = [wrongHash],
            AuthPpu = [wrongHash]
        };
        var wrongHashPlan = services.GameSetup.Build(
            environment.Install,
            wrongHashGame,
            recommendations,
            loaded.Cat);
        var wrongHashCandidate = wrongHashPlan.ArtemisPatches.Single(item =>
            item.Identity == candidate.Identity);
        var wrongSerialMatches = services.PatchCatalog.Build(
            loaded.Cat,
            game with { Serial = "ZZZZ99999" });
        Assert(wrongVersionCandidate.Compatibility == SetupCompatibilityStatus.VersionMismatch &&
               !wrongVersionCandidate.CanSelect &&
               wrongHashCandidate.Compatibility == SetupCompatibilityStatus.HashMismatch &&
               !wrongHashCandidate.CanSelect &&
               wrongSerialMatches.All(match =>
                   GameSetupPlanningService.PatchIdentity(match.Patch) != candidate.Identity),
            "An incompatible Artemis serial, version, or authoritative PPU hash became selectable.");

        Console.WriteLine(
            $"ARTEMIS_PIPELINE Raw={rawEntries}; Parsed={parsed.Catalog.Entries.Count}; " +
            $"Normalized={artemisStatus.EntNum}; AfterMerge={mergedArtemis.Length}; " +
            $"MatchingSelectedGame={matchingArtemis.Length}; " +
            $"GameSetupPlan.ArtemisPatches={plan.ArtemisPatches.Count}; " +
            $"UiArtemis={patchPreview.ChtItems.Count}");
        Console.WriteLine("Game Setup shipped Artemis pipeline PASS.");
    }

    public static void OfficialSnapshotRegression(string tempRoot)
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Official", "patch.yml");
        Assert(File.Exists(fixture), "Pinned official patch fixture is missing.");
        var before = File.ReadAllBytes(fixture);
        var read = new ReadSvc().Read(fixture, PatSrc.Official);
        Assert(read.IsOk && read.Catalog.Version == "1.2" && read.Catalog.Entries.Count == 1,
            "Pinned official patch fixture did not parse as RPCS3 Version 1.2.");
        var entry = read.Catalog.Entries.Single();
        Assert(entry.Source == PatSrc.Official && entry.HashKind == HashKind.Ppu &&
               entry.Serials.Contains("BCUS98111") && entry.GameVer.Contains("01.03") &&
               entry.PatVer == "1.0" && entry.Group == "Frame rate" &&
               entry.Notes?.Contains("Pinned official", StringComparison.Ordinal) == true,
            "Official snapshot metadata was not retained.");
        var fixtureHash = Convert.ToHexString(SHA256.HashData(before)).ToLowerInvariant();
        Assert(fixtureHash.Length == 64 && before.SequenceEqual(File.ReadAllBytes(fixture)),
            "Official fixture provenance/read regression changed the source bytes.");

        var env = CreateEnvironment(Path.Combine(tempRoot, "official-snapshot"));
        var officialPath = Path.Combine(env.Install.PatPath, "patch.yml");
        File.Copy(fixture, officialPath, overwrite: true);
        var officialBefore = File.ReadAllBytes(officialPath);
        var userImported = Path.Combine(env.Install.PatPath, "imported_patch.yml");
        File.WriteAllText(userImported, UserImportedYaml());
        Assert(new ReadSvc().Read(officialPath, PatSrc.Official).IsOk,
            "Copied official snapshot could not be read from a temp RPCS3 root.");
        Assert(officialBefore.SequenceEqual(File.ReadAllBytes(officialPath)),
            "Reading an official patch.yml modified the RPCS3-owned file.");
    }

    public static void CorpusParsersAndPlanConflicts(string tempRoot)
    {
        const string hash = "1234567890abcdef1234567890abcdef12345678";
        var officialText = PatchYaml(
            hash,
            "Official 60 FPS",
            "BLUS30000",
            "01.03",
            "Official author",
            "Requires a documented setting; shown as note only.",
            "Frame Rate",
            "1.5");
        var officialRead = new ReadSvc().ReadText(
            officialText,
            PatSrc.Official,
            "patch.yml");
        Assert(officialRead.IsOk && officialRead.Catalog.Entries.Count == 1,
            "Official patch corpus fixture did not parse.");
        var official = officialRead.Catalog.Entries[0];
        Assert(official.HashKind == HashKind.Ppu &&
               official.Hash == hash &&
               official.Title == "Official 60 FPS" &&
               official.Serials.SequenceEqual(["BLUS30000"]) &&
               official.GameVer.SequenceEqual(["01.03"]) &&
               official.Author == "Official author" &&
               official.Notes?.Contains("documented setting", StringComparison.Ordinal) == true &&
               official.Group == "Frame Rate" &&
               official.PatVer == "1.5" &&
               !string.IsNullOrWhiteSpace(official.DefPrint),
            "Official corpus metadata extraction was incomplete.");

        var canaryText = $$"""
            Version: 1.2
            Anchors:
              Shared: &shared
                - [ be32, 0x00000010, 0x60000000 ]
            PPU-{{hash}}:
              Canary Anchor Patch:
                Games:
                  Fixture Game:
                    BLUS30000: [ 01.03 ]
                Author: Canary author
                Notes: Experimental fixture
                Group: Frame Rate
                Patch Version: 2.0
                Patch:
                  - [ load, *shared ]
            """;
        var canaryRead = new HCanCat().ReadText(canaryText, "can.yml");
        Assert(canaryRead.IsOk &&
               canaryRead.Catalog.AncNum == 1 &&
               canaryRead.Catalog.DefList.Count == 1 &&
               canaryRead.Catalog.DefList[0].AncDeps.Contains("shared") &&
               canaryRead.Catalog.DefList[0].Group == "Frame Rate" &&
               canaryRead.Catalog.DefList[0].PatVer == "2.0",
            "Canary corpus did not retain its anchor dependency and metadata.");
        var unsupported = new HCanCat().ReadText(
            canaryText.Replace("*shared", "*missing", StringComparison.Ordinal),
            "can.yml");
        Assert(!unsupported.IsOk || unsupported.Catalog.HasErr,
            "A Canary patch with an unresolved anchor must fail closed.");

        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-conflicts"));
        var game = new Rpcs3GameDescriptor(
            "BLUS30000",
            "Fixture Game",
            env.GamePath,
            "01.03",
            hash,
            null,
            PpuRaw: [hash],
            HashSrc: "RPCS3.log",
            HashEvi: HashEvi.PlainLog,
            AuthPpu: [hash],
            HCanPpu: [hash]);
        var baseSetting = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "write color buffers",
            null,
            RecRisk.Required);
        var requirementSetting = new CfgChg(
            "Video",
            "VBlank Rate",
            "120",
            "patch requirement",
            "Explicit tested rule",
            RecRisk.PatDep);
        var recommendation = new WikiRec(
            game.Serial,
            game.Title,
            [baseSetting]);
        var catalog = RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            "2026-06-13",
            [recommendation],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('b', 64));
        var canary = official with
        {
            Title = "Canary 120 FPS",
            Source = PatSrc.Can,
            Class = PatClass.Canary,
            PatVer = "2.0"
        };
        var dependency = official with
        {
            Title = "Needs helper",
            Group = "Independent",
            DepList = ["Missing helper"]
        };
        var rules = new[]
        {
            new PatchRequirementRule(
                GameSetupPlanningService.PatchIdentity(canary),
                [new PatchRequirement(requirementSetting, "Pinned fixture rule")])
        };
        var setup = AppComposition.Build(
                new Rpcs3MutationGuard(() => false))
            .GameSetup.Build(
                env.Install,
                game,
                catalog,
                new PatCat("1.2", [official, canary, dependency]),
                requirementRules: rules);
        var canaryCandidate = setup.CanaryPatches.Single();
        Assert(setup.BaseConfiguration.Items.All(item =>
                   item.Key != "VBlank Rate") &&
               canaryCandidate.RequiredSettings.Single().Setting == requirementSetting,
            "Patch-specific requirements leaked into base game configuration.");

        var groupConflict = new GameSetupPlanningService().Preview(
            setup,
            new GameSetupSelection(
                [baseSetting],
                [setup.OfficialPatches.Single(candidate =>
                    candidate.Match.Patch.Title == "Official 60 FPS").Identity,
                 canaryCandidate.Identity]));
        Assert(!groupConflict.CanApply &&
               groupConflict.Conflicts.Any(message =>
                   message.Contains("mutually exclusive", StringComparison.Ordinal)),
            "Official/Canary options in the same Group were not blocked.");

        var enabledPeerPlan = setup with
        {
            OfficialPatches = setup.OfficialPatches
                .Select(candidate => candidate.Match.Patch.Title == "Official 60 FPS"
                    ? candidate with { IsEnabled = true }
                    : candidate)
                .ToArray()
        };
        var enabledPeerConflict = new GameSetupPlanningService().Preview(
            enabledPeerPlan,
            new GameSetupSelection([], [canaryCandidate.Identity]));
        Assert(!enabledPeerConflict.CanApply &&
               enabledPeerConflict.Conflicts.Any(message =>
                   message.Contains("mutually exclusive", StringComparison.Ordinal)),
            "A selected Canary patch did not conflict with an already-enabled official Group peer in preview.");

        Assert(!(canaryCandidate with
        {
            DuplicateStatus = SetupDuplicateStatus.OlderVersion
        }).CanSelect &&
               !(canaryCandidate with
               {
                   DuplicateStatus = SetupDuplicateStatus.ConflictingPatch
               }).CanSelect &&
               (canaryCandidate with
               {
                   DuplicateStatus = SetupDuplicateStatus.ExactDuplicate
               }).CanSelect,
            "Unsafe Canary duplicate states were selectable or an exact installed definition could not be enabled.");

        var dependencyConflict = new GameSetupPlanningService().Preview(
            setup,
            new GameSetupSelection(
                [],
                [setup.OfficialPatches.Single(candidate =>
                    candidate.Match.Patch.Title == "Needs helper").Identity]));
        Assert(!dependencyConflict.CanApply &&
               dependencyConflict.Conflicts.Any(message =>
                   message.Contains("requires 'Missing helper'", StringComparison.Ordinal)),
            "A missing patch dependency did not block the setup plan.");

        Console.WriteLine("Game Setup corpus parsers and plan conflicts PASS.");
    }

    public static void EndToEndPrepareAndIdempotency(string tempRoot)
    {
        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-e2e"));
        var canary = LoadCanaryEntry();
        var expectedGame = GameFor(canary, env.GamePath);
        WriteInstalledGame(env, expectedGame);
        File.WriteAllText(
            Path.Combine(env.Install.RootPath, "RPCS3.log"),
            $"SYS: Serial: {expectedGame.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{canary.NormHash}{Environment.NewLine}" +
            $"sys_spu: Loaded SPU image: SPU-{canary.NormHash} (<- 0){Environment.NewLine}",
            new UTF8Encoding(false));
        var discovered = new GameScanner()
            .Scan(env.Install)
            .Single(candidate => candidate.Serial == expectedGame.Serial);
        var game = new PatchHashProbeService().Enrich(
            env.Install,
            discovered);
        Assert(game.Title == expectedGame.Title &&
               game.Version == expectedGame.Version &&
               game.InstPath == Path.GetFullPath(env.GamePath) &&
               game.Hashes.Contains(canary.NormHash!, StringComparer.OrdinalIgnoreCase) &&
               game.SpuList.Contains(canary.NormHash!, StringComparer.OrdinalIgnoreCase),
            "The end-to-end fixture did not discover game identity/version and bounded RPCS3 log hashes.");
        var official = OfficialFor(canary, env.Install.PatPath) with
        {
            Group = "Official Quality"
        };
        File.WriteAllText(
            Path.Combine(env.Install.PatPath, "patch.yml"),
            PatchYaml(
                official.Hash,
                official.Title,
                game.Serial,
                game.Version!,
                "RPCS3",
                "Official fixture",
                official.Group!,
                "1.0"),
            new UTF8Encoding(false));
        var officialPath = Path.Combine(env.Install.PatPath, "patch.yml");
        var officialBefore = File.ReadAllBytes(officialPath);
        var parsedOfficial = new ReadSvc().Read(officialPath, PatSrc.Official);
        Assert(parsedOfficial.IsOk && parsedOfficial.Catalog.Entries.Count == 1,
            "The end-to-end official fixture did not retain a reviewable source definition.");
        official = parsedOfficial.Catalog.Entries.Single();
        var artemis = canary with
        {
            Title = "Artemis Collection Setup Fixture",
            Risk = PatRisk.Cheat,
            Notes = "RPCS3 Artemis collection fixture",
            Author = "Artemis",
            PatVer = "1.0",
            Group = "Artemis Setup",
            Source = PatSrc.Artemis,
            SrcPath = "art.yml",
            DepList = [],
            Class = PatClass.Main,
            PatType = "Cheat",
            DefPrint = null
        };
        var appBase = Path.Combine(env.Install.RootPath, "app-fixture");
        Directory.CreateDirectory(Path.Combine(appBase, "Data"));
        File.Copy(
            BundledCanaryPatchService.GetPath(AppContext.BaseDirectory),
            BundledCanaryPatchService.GetPath(appBase));
        File.WriteAllText(
            BundledCheatPatchService.GetBndl(appBase),
            PatchYaml(
                artemis.Hash,
                artemis.Title,
                game.Serial,
                game.Version!,
                artemis.Author!,
                artemis.Notes!,
                artemis.Group!,
                artemis.PatVer!),
            new UTF8Encoding(false));

        var customPath = Path.Combine(
            env.Install.CustPath,
            $"config_{game.Serial}.yml");
        File.WriteAllText(
            customPath,
            "Video:\r\n  Write Color Buffers: false\r\n  VBlank Rate: 60\r\nUser Section:\r\n  Keep Me: yes\r\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            Rpcs3AppPaths.ImpPat(env.Install),
            UserImportedYaml(),
            new UTF8Encoding(false));

        var baseSetting = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "write color buffers",
            "RPCS3 Wiki",
            RecRisk.Required);
        var patchSetting = new CfgChg(
            "Video",
            "VBlank Rate",
            "120",
            "patch requirement",
            "Pinned exact patch rule",
            RecRisk.PatDep);
        var recommendationCatalog = RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            "2026-06-13",
            [new WikiRec(game.Serial, game.Title, [baseSetting])],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('c', 64));
        var patchCatalog = new PatCat("1.2", [official, canary, artemis]);
        var rules = new[]
        {
            new PatchRequirementRule(
                GameSetupPlanningService.PatchIdentity(canary),
                [new PatchRequirement(patchSetting, "Pinned exact fixture")])
        };
        var backup = new BackupService();
        var reader = new ReadSvc();
        var cfgReader = new CfgRead();
        var useCase = BuildUseCase(
            backup,
            reader,
            cfgReader,
            new PatchFileTransactionService(backup, reader, cfgReader),
            new Rpcs3MutationGuard(() => false),
            appBase);
        var setup = useCase.Build(
            env.Install,
            game,
            recommendationCatalog,
            patchCatalog,
            requirementRules: rules);
        var preview = useCase.Preview(
            setup,
            new GameSetupSelection(
                [baseSetting],
                [setup.OfficialPatches.Single().Identity,
                 setup.CanaryPatches.Single().Identity,
                 setup.ArtemisPatches.Single().Identity]));
        Assert(preview.CanApply &&
               preview.EffectiveSettings.Count == 2 &&
               preview.Files.Select(file => file.Kind).ToHashSet().SetEquals(
               [
                   SetupFileKind.CustomConfig,
                   SetupFileKind.PatchEnablement,
                   SetupFileKind.ImportedPatch,
                   SetupFileKind.ArtemisTitlePatch
               ]),
            "Game Setup preview did not expose the complete configuration/patch file plan.");

        var reviewedCustomConfig = File.ReadAllBytes(customPath);
        File.AppendAllText(
            customPath,
            "# external edit after preview\r\n",
            new UTF8Encoding(false));
        var externallyEditedConfig = File.ReadAllBytes(customPath);
        ExpectFailure<IOException>(() => useCase.Apply(env.Install, preview));
        Assert(File.ReadAllBytes(customPath).SequenceEqual(externallyEditedConfig),
            "Game Setup stale-config rejection modified the external edit.");
        File.WriteAllBytes(customPath, reviewedCustomConfig);

        var reviewedOfficial = File.ReadAllBytes(officialPath);
        File.WriteAllText(
            officialPath,
            File.ReadAllText(officialPath, Encoding.UTF8).Replace(
                "0x60000000",
                "0x60000001",
                StringComparison.Ordinal),
            new UTF8Encoding(false));
        var externallyEditedOfficial = File.ReadAllBytes(officialPath);
        ExpectFailure<IOException>(() => useCase.Apply(env.Install, preview));
        Assert(File.ReadAllBytes(officialPath).SequenceEqual(externallyEditedOfficial),
            "Game Setup stale official-definition rejection modified RPCS3-owned patch.yml.");
        File.WriteAllBytes(officialPath, reviewedOfficial);

        var result = useCase.Apply(env.Install, preview);
        Assert(result.IsGameReady &&
               !result.AlreadyPrepared &&
               result.AppliedGameSettings == 2 &&
               result.EnabledOfficialPatches == 1 &&
               result.InstalledCanaryPatches == 1 &&
               result.EnabledCanaryPatches == 1 &&
               result.InstalledArtemisPatches == 1 &&
               result.EnabledArtemisPatches == 1 &&
               result.Backup?.Files.Count == 4,
            "End-to-end Prepare Game did not report the verified transaction result.");
        Assert(R3Yaml.ReadVal(customPath, "Video", "Write Color Buffers") == "true" &&
               R3Yaml.ReadVal(customPath, "Video", "VBlank Rate") == "120" &&
               R3Yaml.ReadVal(customPath, "User Section", "Keep Me") == "yes",
            "Prepare Game did not apply selected settings while preserving unrelated config content.");
        var importedText = File.ReadAllText(
            Rpcs3AppPaths.ImpPat(env.Install),
            Encoding.UTF8);
        Assert(importedText.Contains("# USER PATCH KEEP", StringComparison.Ordinal) &&
               importedText.Contains("User Owned Patch", StringComparison.Ordinal) &&
               importedText.Contains(canary.Title, StringComparison.Ordinal),
            "Safe imported_patch.yml merge did not preserve the user patch and install Canary.");
        var managedArtemisPath = BundledCheatPatchService.GetPath(
            env.Install.PatPath,
            game.Serial);
        var managedArtemis = new ReadSvc().Read(
            managedArtemisPath,
            PatSrc.ManagedArtemis);
        Assert(managedArtemis.IsOk &&
               managedArtemis.Catalog.Entries.Count == 1 &&
               managedArtemis.Catalog.Entries[0].Title == artemis.Title &&
               BundledCheatPatchService.IsMan(managedArtemisPath),
            "Game Setup did not install the selected Artemis definition in the managed title-specific RPCS3 file.");
        var patchState = new CfgRead().ReadSnap(env.Install.PatCfg);
        var planner = new PatPlnr();
        Assert(preview.SelectedPatches.All(candidate =>
               {
                   var change = planner.Build3(candidate.Match);
                   return change is not null && patchState.IsOn(change);
               }),
            "Canary installation and patch enablement were not represented as two verified operations.");
        Assert(File.ReadAllBytes(Path.Combine(env.Install.PatPath, "patch.yml"))
                   .SequenceEqual(officialBefore) &&
               result.Backup!.Files.All(file =>
                   !Path.GetFileName(file.TgtPath).Equals(
                       "patch.yml",
                       StringComparison.OrdinalIgnoreCase)),
            "RPCS3-owned patch.yml was modified or included as a restore target.");

        var preparedCustomConfig = File.ReadAllBytes(customPath);
        File.AppendAllText(
            customPath,
            "# external edit after successful prepare\r\n",
            new UTF8Encoding(false));
        var externallyEditedPreparedConfig = File.ReadAllBytes(customPath);
        ExpectFailure<IOException>(() => useCase.Apply(env.Install, preview));
        Assert(File.ReadAllBytes(customPath).SequenceEqual(externallyEditedPreparedConfig),
            "Game Setup accepted an externally edited prepared custom config that only preserved the desired values.");
        File.WriteAllBytes(customPath, preparedCustomConfig);

        var second = useCase.Apply(env.Install, preview);
        Assert(second.IsGameReady &&
               second.AlreadyPrepared &&
               second.Backup is null &&
               File.ReadAllText(Rpcs3AppPaths.ImpPat(env.Install), Encoding.UTF8)
                   .CountOccurrences(canary.Title) == 1 &&
               File.ReadAllText(managedArtemisPath, Encoding.UTF8)
                   .CountOccurrences(artemis.Title) == 1,
            "Preparing the same game twice was not idempotent.");

        var artemisMatch = preview.SelectedPatches.Single(candidate =>
            candidate.Provenance.Kind == SetupPatchSourceKind.Artemis).Match;
        var flow = AppComposition.BuildRun(new Rpcs3MutationGuard(() => false)).Flow;
        var disablePlan = flow.OffPlan(env.Install, [artemisMatch]);
        flow.OffPat(disablePlan, env.Install);
        var disabledState = new CfgRead().ReadSnap(env.Install.PatCfg);
        var artemisChange = new PatPlnr().Build3(artemisMatch)!;
        Assert(!disabledState.IsOn(artemisChange) && File.Exists(managedArtemisPath),
            "Artemis Disable did not clear activation while retaining the managed definition.");

        var importedBeforeRemove = File.ReadAllBytes(Rpcs3AppPaths.ImpPat(env.Install));
        flow.RemoveArtemis(
            flow.OffPlan(env.Install, [artemisMatch]),
            env.Install,
            [artemisMatch]);
        Assert(!File.Exists(managedArtemisPath) &&
               !new CfgRead().ReadSnap(env.Install.PatCfg).IsOn(artemisChange) &&
               File.ReadAllBytes(Rpcs3AppPaths.ImpPat(env.Install)).SequenceEqual(importedBeforeRemove) &&
               File.ReadAllBytes(Path.Combine(env.Install.PatPath, "patch.yml")).SequenceEqual(officialBefore),
            "Artemis Remove did not remove only its managed definition/state or preserve other patch domains.");

        Console.WriteLine("Game Setup end-to-end and idempotency PASS.");
    }

    public static void ConcurrencyAndRollback(string tempRoot)
    {
        ExternalEditBeforeCommit(tempRoot);
        Rpcs3StartsBeforeCommit(tempRoot);
        RollbackPreservesExternalEdit(tempRoot);
        Console.WriteLine("Game Setup concurrency and rollback PASS.");
    }

    private static void ExternalEditBeforeCommit(string tempRoot)
    {
        var fixture = BaseOnlyFixture(Path.Combine(tempRoot, "setup-race"));
        const string external = "Video:\r\n  Write Color Buffers: external-before-commit\r\n";
        ExpectFailure<IOException>(() => fixture.UseCase.Apply(
            fixture.Environment.Install,
            fixture.Preview,
            (phase, _, _) =>
            {
                if (phase == PatPhase.Commit)
                {
                    File.WriteAllText(
                        fixture.CustomPath,
                        external,
                        new UTF8Encoding(false));
                }
            }));
        Assert(File.ReadAllText(fixture.CustomPath, Encoding.UTF8) == external,
            "An external custom-config edit before commit was overwritten.");
    }

    private static void Rpcs3StartsBeforeCommit(string tempRoot)
    {
        var checks = 0;
        var guard = new Rpcs3MutationGuard(() => ++checks >= 5);
        var fixture = BaseOnlyFixture(
            Path.Combine(tempRoot, "setup-running"),
            guard);
        var before = File.ReadAllText(fixture.CustomPath, Encoding.UTF8);
        ExpectFailure<Rpcs3MutationBlockedException>(() => fixture.UseCase.Apply(
            fixture.Environment.Install,
            fixture.Preview));
        Assert(File.ReadAllText(fixture.CustomPath, Encoding.UTF8) == before,
            "RPCS3 starting before commit did not abort without mutation.");
    }

    private static void RollbackPreservesExternalEdit(string tempRoot)
    {
        const string external = "Video:\r\n  Write Color Buffers: external-during-commit\r\n";
        var env = CreateEnvironment(Path.Combine(tempRoot, "setup-rollback"));
        var backup = new BackupService();
        var reader = new ReadSvc();
        var cfgReader = new CfgRead();
        var transaction = new PatchFileTransactionService(
            backup,
            reader,
            cfgReader,
            commitFile: (_, target) =>
            {
                File.WriteAllText(target, external, new UTF8Encoding(false));
                throw new IOException("Injected commit failure after external edit.");
            });
        var guard = new Rpcs3MutationGuard(() => false);
        var useCase = BuildUseCase(
            backup,
            reader,
            cfgReader,
            transaction,
            guard);
        var fixture = BaseOnlyFixture(env, useCase);
        ExpectFailure<InvalidOperationException>(() => useCase.Apply(
            env.Install,
            fixture.Preview));
        Assert(File.ReadAllText(fixture.CustomPath, Encoding.UTF8) == external,
            "Rollback erased an unknown external state after a failed commit.");
    }

    private static BaseFixture BaseOnlyFixture(
        string root,
        IRpcs3MutationGuard? guard = null)
    {
        var env = CreateEnvironment(root);
        var actualGuard = guard ?? new Rpcs3MutationGuard(() => false);
        return BaseOnlyFixture(
            env,
            AppComposition.Build(actualGuard).GameSetup);
    }

    private static BaseFixture BaseOnlyFixture(
        SetupEnvironment env,
        GameSetupUseCase useCase)
    {
        var customPath = Path.Combine(env.Install.CustPath, "config_BLUS30001.yml");
        File.WriteAllText(
            customPath,
            "Video:\r\n  Write Color Buffers: false\r\n",
            new UTF8Encoding(false));
        var setting = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "write color buffers",
            null,
            RecRisk.Required);
        var game = new Rpcs3GameDescriptor(
            "BLUS30001",
            "Concurrency Fixture",
            env.GamePath,
            "01.00",
            null,
            null);
        var catalog = RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            "2026-06-13",
            [new WikiRec(game.Serial, game.Title, [setting])],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('d', 64));
        var plan = useCase.Build(
            env.Install,
            game,
            catalog,
            new PatCat("1.2", []));
        var preview = useCase.Preview(
            plan,
            new GameSetupSelection([setting], []));
        return new BaseFixture(env, useCase, preview, customPath);
    }

    private static GameSetupUseCase BuildUseCase(
        BackupService backup,
        ReadSvc reader,
        CfgRead cfgReader,
        PatchFileTransactionService transaction,
        IRpcs3MutationGuard guard,
        string? appBaseDirectory = null)
    {
        return new GameSetupUseCase(
            new PlanService(),
            new GameSetupPlanningService(),
            new PatPlnr(),
            cfgReader,
            new CfgWrite(cfgReader),
            new BundledCanaryPatchService(),
            new BundledCheatPatchService(),
            reader,
            transaction,
            guard,
            appBaseDirectory: appBaseDirectory);
    }

    public static SetupEnvironment CreateEnvironment(string root)
    {
        Directory.CreateDirectory(root);
        var exe = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exe, [0x4d, 0x5a]);
        var config = Path.Combine(root, "config");
        var custom = Path.Combine(config, "custom_configs");
        var patches = Path.Combine(root, "patches");
        var game = Path.Combine(root, "games", "fixture");
        Directory.CreateDirectory(custom);
        Directory.CreateDirectory(patches);
        Directory.CreateDirectory(game);
        return new SetupEnvironment(
            new Rpcs3InstallDescriptor(
                root,
                exe,
                config,
                custom,
                patches,
                Path.Combine(config, "patch_config.yml"),
                root),
            game);
    }

    public static void WriteInstalledGame(
        SetupEnvironment environment,
        Rpcs3GameDescriptor game)
    {
        var sfoDirectory = Path.Combine(
            environment.GamePath,
            "PS3_GAME");
        Directory.CreateDirectory(sfoDirectory);
        WriteSfo(
            Path.Combine(sfoDirectory, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = game.Title,
                ["TITLE_ID"] = game.Serial,
                ["APP_VER"] = game.Version ?? "01.00"
            });
        var escapedPath = environment.GamePath.Replace(
            "\\",
            "\\\\",
            StringComparison.Ordinal);
        File.WriteAllText(
            Path.Combine(environment.Install.CfgPath, "games.yml"),
            $"{game.Serial}: \"{escapedPath}\"{Environment.NewLine}",
            new UTF8Encoding(false));
    }

    private static void WriteSfo(
        string path,
        IReadOnlyDictionary<string, string> values)
    {
        var keys = values.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(
            stream,
            Encoding.UTF8,
            leaveOpen: false);
        using var keyTable = new MemoryStream();
        using var dataTable = new MemoryStream();
        var entries = new List<(
            ushort KeyOffset,
            ushort Format,
            uint Length,
            uint MaxLength,
            uint DataOffset)>();

        foreach (var key in keys)
        {
            var keyOffset = (ushort)keyTable.Position;
            keyTable.Write(Encoding.UTF8.GetBytes(key));
            keyTable.WriteByte(0);
            var dataOffset = (uint)dataTable.Position;
            var bytes = Encoding.UTF8.GetBytes(values[key] + "\0");
            dataTable.Write(bytes);
            entries.Add((
                keyOffset,
                0x0204,
                (uint)bytes.Length,
                (uint)bytes.Length,
                dataOffset));
        }

        var keyTableOffset = 20u + (uint)(entries.Count * 16);
        var dataTableOffset = keyTableOffset + (uint)keyTable.Length;
        writer.Write(0x46535000u);
        writer.Write(0x00000101u);
        writer.Write(keyTableOffset);
        writer.Write(dataTableOffset);
        writer.Write((uint)entries.Count);
        foreach (var entry in entries)
        {
            writer.Write(entry.KeyOffset);
            writer.Write(entry.Format);
            writer.Write(entry.Length);
            writer.Write(entry.MaxLength);
            writer.Write(entry.DataOffset);
        }

        keyTable.Position = 0;
        keyTable.CopyTo(stream);
        dataTable.Position = 0;
        dataTable.CopyTo(stream);
    }

    public static Rpcs3PatchEntry LoadCanaryEntry()
    {
        var path = BundledCanaryPatchService.GetPath(AppContext.BaseDirectory);
        var text = R3Bound.ReadUtf8(
            path,
            R3Limits.PatCatBy,
            BundledCanaryPatchService.BndlName);
        var normalized = new CanaryPatchImportService().Norm(text);
        Assert(normalized.IsOk, "Bundled Canary fixture could not be normalized.");
        var read = new ReadSvc().ReadText(
            normalized.NormText,
            PatSrc.Can,
            path);
        Assert(read.IsOk, "Bundled Canary fixture could not be parsed.");
        return read.Catalog.Entries.First(entry =>
            entry.HashKind is HashKind.Ppu or HashKind.Spu &&
            entry.NormHash is not null &&
            entry.Patches.Count == 0 &&
            entry.GameTgt.Any(target =>
                !PatVer.IsAll(target.Serial) &&
                target.Versions.Any(version => !PatVer.IsAll(version))));
    }

    public static Rpcs3GameDescriptor GameFor(
        Rpcs3PatchEntry canary,
        string installPath)
    {
        var target = canary.GameTgt.First(candidate =>
            !PatVer.IsAll(candidate.Serial) &&
            candidate.Versions.Any(version => !PatVer.IsAll(version)));
        var version = target.Versions.First(value => !PatVer.IsAll(value));
        var hash = canary.NormHash!;
        return new Rpcs3GameDescriptor(
            target.Serial,
            target.Title,
            installPath,
            version,
            canary.HashKind == HashKind.Ppu ? hash : null,
            canary.HashKind == HashKind.Spu ? hash : null,
            PpuRaw: canary.HashKind == HashKind.Ppu ? [hash] : [],
            SpuRaw: canary.HashKind == HashKind.Spu ? [hash] : [],
            HashSrc: "RPCS3.log",
            HashMsg: "Pinned Game Setup fixture",
            HashEvi: HashEvi.PlainLog,
            AuthPpu: canary.HashKind == HashKind.Ppu ? [hash] : [],
            HCanPpu: canary.HashKind == HashKind.Ppu ? [hash] : [],
            HCanSpu: canary.HashKind == HashKind.Spu ? [hash] : []);
    }

    private static Rpcs3PatchEntry OfficialFor(
        Rpcs3PatchEntry canary,
        string patchesPath)
    {
        return canary with
        {
            Title = "Official Setup Fixture",
            Risk = PatRisk.Fix,
            Notes = "Official fixture note",
            Author = "RPCS3",
            PatVer = "1.0",
            Group = "Official Setup",
            Source = PatSrc.Official,
            SrcPath = Path.Combine(patchesPath, "patch.yml"),
            DepList = [],
            Class = PatClass.Main
        };
    }

    public static WikiRec Recommendation(string serial)
    {
        return new WikiRec(
            serial,
            "Setup Fixture",
            [
                new CfgChg(
                    "Video",
                    "Write Color Buffers",
                    "true",
                    "write color buffers",
                    "RPCS3 Wiki",
                    RecRisk.Required)
            ]);
    }

    private static SetupPlanFixture SetupPlan(
        string tempRoot,
        string scenario,
        Rpcs3GameDescriptor game,
        IReadOnlyList<Rpcs3PatchEntry> patches)
    {
        var environment = CreateEnvironment(Path.Combine(
            tempRoot,
            scenario));
        var selectedGame = game with { InstPath = environment.GamePath };
        var recommendations = RecSvc.FromRec(
            "Setup warning projection fixture",
            "2026-08-26",
            [new WikiRec(selectedGame.Serial, selectedGame.Title, [])],
            new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero),
            new string('f', 64));
        var useCase = AppComposition.Build(
            new Rpcs3MutationGuard(() => false)).GameSetup;
        var plan = useCase.Build(
            environment.Install,
            selectedGame,
            recommendations,
            new PatCat("1.2", patches));
        return new SetupPlanFixture(
            environment,
            selectedGame,
            useCase,
            plan);
    }

    private static Rpcs3GameDescriptor SetupGame(
        string serial,
        string version,
        string? ppuHash = null,
        string? spuHash = null)
    {
        return new Rpcs3GameDescriptor(
            serial,
            $"Setup fixture {serial}",
            string.Empty,
            version,
            ppuHash,
            spuHash,
            PpuRaw: ppuHash is null ? [] : [ppuHash],
            SpuRaw: spuHash is null ? [] : [spuHash],
            HashSrc: "Setup warning fixture",
            HashEvi: HashEvi.PlainLog,
            AuthPpu: ppuHash is null ? [] : [ppuHash]);
    }

    private static Rpcs3PatchEntry SetupPatch(
        string title,
        string serial,
        string version,
        string hash,
        HashKind hashKind,
        PatSrc source)
    {
        return new Rpcs3PatchEntry(
            title,
            hashKind,
            hash,
            [new R3PatTgt(
                PatVer.IsAll(serial) ? PatVer.All : "Setup fixture",
                serial,
                [version])],
            "Patch",
            PatRisk.Fix,
            Notes: null,
            Author: "R3Config test",
            PatVer: "1.0",
            Group: null,
            Source: source,
            SrcPath: "setup-warning-fixture.yml",
            DepList: [],
            DefPrint: title);
    }

    private static string GlobalPatchYaml(string hash, string title)
    {
        return $$"""
            Version: 1.2
            SPU-{{hash}}:
              {{title}}:
                Games:
                  All:
                    All: [ All ]
                Author: RPCS3
                Notes: Global warning fixture
                Patch Version: 1.0
                Patch:
                  - [ be32, 0x00000010, 0x60000000 ]
            """ + "\r\n";
    }

    private static string PatchYaml(
        string hash,
        string title,
        string serial,
        string gameVersion,
        string author,
        string notes,
        string group,
        string patchVersion)
    {
        return $$"""
            Version: 1.2
            PPU-{{hash}}:
              {{title}}:
                Games:
                  Fixture Game:
                    {{serial}}: [ {{gameVersion}} ]
                Author: {{author}}
                Notes: {{notes}}
                Group: {{group}}
                Patch Version: {{patchVersion}}
                Patch:
                  - [ be32, 0x00000010, 0x60000000 ]
            """ + "\r\n";
    }

    private static string UserImportedYaml()
    {
        return """
            Version: 1.2
            # USER PATCH KEEP
            PPU-1111111111111111111111111111111111111111:
              User Owned Patch:
                Games:
                  User Game:
                    BLUS99999: [ 01.00 ]
                Author: User
                Notes: Preserve this custom patch
                Patch Version: 1.0
                Patch:
                  - [ be32, 0x00000020, 0x60000000 ]
            """ + "\r\n";
    }

    private static void ExpectFailure<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public sealed record SetupEnvironment(
        Rpcs3InstallDescriptor Install,
        string GamePath);

    private sealed record BaseFixture(
        SetupEnvironment Environment,
        GameSetupUseCase UseCase,
        GameSetupPreview Preview,
        string CustomPath);

    private sealed record SetupPlanFixture(
        SetupEnvironment Environment,
        Rpcs3GameDescriptor Game,
        GameSetupUseCase UseCase,
        GameSetupPlan Plan);

    private static int CountOccurrences(this string text, string value)
    {
        var count = 0;
        var start = 0;
        while ((start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }

        return count;
    }
}
