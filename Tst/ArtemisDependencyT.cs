using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class ArtemisDependencyT
{
    private const string Hash = "1111111111111111111111111111111111111111";
    private const string OtherHash = "2222222222222222222222222222222222222222";
    private const string Serial = "BLUS00001";
    private const string Version = "01.00";

    public static void Run(string tempRoot)
    {
        ShippedOverlay_ProjectsExactly23ReviewedRulesTo23ExactArtemisSources();
        OverlayLoad_RejectsCorpusHashOrUpstreamRevisionDrift(tempRoot);
        OverlayLoad_RejectsMalformedNullsOrMode(tempRoot);
        Enable_AllOfAndAnyOfUseExactIdentity();
        DependencyIdentity_RequiresSameOwnershipChannel();
        Compiler_PreservesAndAcceptsPreparedPrerequisite();
        Disable_ProtectsDependenciesAndAllowsValidClosure();
        Catalog_DependencyCycleFailsClosedBeforePlanning();
        UiProjection_LocalizesDependencyGrammarAndPreservesCanonicalTitles();
    }

    private static void ShippedOverlay_ProjectsExactly23ReviewedRulesTo23ExactArtemisSources()
    {
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "Data");
        var artPath = Path.Combine(dataRoot, "art.yml");
        var dependencyPath = Path.Combine(dataRoot, ArtDepSvc.FileName);
        var beforeHash = FileHash(artPath);

        var service = new ArtDepSvc();
        var dependencies = service.Load(dependencyPath, artPath);
        var read = new ReadSvc().Read(artPath, PatSrc.Artemis);
        Assert(read.IsOk, "Expected the shipped Artemis catalog to parse: " + read.Error);

        var projection = service.Apply(dependencies, read.Catalog);
        Assert(dependencies.SourceRevision == ArtDepSvc.SourceRevision,
            "Expected the dependency overlay to pin the reviewed upstream revision.");
        Assert(projection.RuleCount == ArtDepSvc.ReviewedRuleCount,
            "Expected exactly 23 reviewed Artemis dependency rules.");
        Assert(projection.SourceCount == ArtDepSvc.ReviewedRuleCount,
            "Expected every reviewed rule to project to one distinct exact Artemis source.");
        Assert(projection.Catalog.Entries.Count(entry => entry.Dependencies.Count > 0) ==
               ArtDepSvc.ReviewedRuleCount,
            "Expected exactly 23 Artemis sources to expose projected dependencies.");
        Assert(FileHash(artPath).Equals(beforeHash, StringComparison.OrdinalIgnoreCase),
            "Dependency projection must not mutate art.yml or its patch payloads.");

        var prototype = projection.Catalog.Entries.Single(entry =>
            entry.RunKey.Equals(
                "PPU-38656a143a665d48c38ae914e7c826d059efd291",
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title.StartsWith("Everything Dies From Any Damage", StringComparison.Ordinal));
        Assert(prototype.Dependencies.Single().Titles.Single() == "Health Never Decreases",
            "Expected the reviewed PROTOTYPE dependency to project by exact identity.");

        var metalGear = projection.Catalog.Entries.Single(entry =>
            entry.Title == "Invincibility For Things Raiden Controls");
        Assert(metalGear.Dependencies.Single().Mode == PatDepMode.Any &&
               metalGear.Dependencies.Single().Titles.Count == 2,
            "Expected the Metal Gear Rising marker requirement to remain ANY-OF.");
    }

    private static void OverlayLoad_RejectsCorpusHashOrUpstreamRevisionDrift(
        string tempRoot)
    {
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "Data");
        var artPath = Path.Combine(dataRoot, "art.yml");
        var sourcePath = Path.Combine(dataRoot, ArtDepSvc.FileName);
        var driftPath = Path.Combine(tempRoot, "art_dep_drift.json");
        var root = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        root["sourceRevision"] = new string('0', 40);
        File.WriteAllText(driftPath, root.ToJsonString());

        Expect<InvalidDataException>(
            () => new ArtDepSvc().Load(driftPath, artPath),
            "Expected upstream-revision drift to fail closed.");

        var staleArtPath = Path.Combine(tempRoot, "art_stale.yml");
        File.Copy(artPath, staleArtPath, overwrite: true);
        File.AppendAllText(staleArtPath, "\n# stale corpus\n");
        Expect<InvalidDataException>(
            () => new ArtDepSvc().Load(sourcePath, staleArtPath),
            "Expected art.yml hash drift to fail closed.");
    }

    private static void OverlayLoad_RejectsMalformedNullsOrMode(
        string tempRoot)
    {
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "Data");
        var artPath = Path.Combine(dataRoot, "art.yml");
        var sourcePath = Path.Combine(dataRoot, ArtDepSvc.FileName);
        var metadataNames = new[]
        {
            "schemaVersion",
            "sourceIdentity",
            "sourceRevision",
            "artemisSha256"
        };

        foreach (var metadataName in metadataNames)
        {
            var root = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
            root[metadataName] = null;
            var malformedPath = Path.Combine(
                tempRoot,
                $"art_dep_null_{metadataName}.json");
            File.WriteAllText(malformedPath, root.ToJsonString());

            Expect<InvalidDataException>(
                () => new ArtDepSvc().Load(malformedPath, artPath),
                $"Expected null Artemis dependency metadata '{metadataName}' to fail closed.");
        }

        var nullRuleRoot = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        nullRuleRoot["rules"]!.AsArray()[0] = null;
        var nullRulePath = Path.Combine(tempRoot, "art_dep_null_rule.json");
        File.WriteAllText(nullRulePath, nullRuleRoot.ToJsonString());
        Expect<InvalidDataException>(
            () => new ArtDepSvc().Load(nullRulePath, artPath),
            "Expected a null Artemis dependency rule to fail closed.");

        var missingModeRoot = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        missingModeRoot["rules"]!.AsArray()[0]!.AsObject().Remove("mode");
        var missingModePath = Path.Combine(tempRoot, "art_dep_missing_mode.json");
        File.WriteAllText(missingModePath, missingModeRoot.ToJsonString());
        Expect<InvalidDataException>(
            () => new ArtDepSvc().Load(missingModePath, artPath),
            "Expected a missing Artemis dependency mode to fail closed.");

        var invalidModeRoot = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        invalidModeRoot["rules"]!.AsArray()[0]!.AsObject()["mode"] = 9;
        var invalidModePath = Path.Combine(tempRoot, "art_dep_invalid_mode.json");
        File.WriteAllText(invalidModePath, invalidModeRoot.ToJsonString());
        Expect<InvalidDataException>(
            () => new ArtDepSvc().Load(invalidModePath, artPath),
            "Expected an undefined numeric Artemis dependency mode to fail closed.");
    }

    private static void Enable_AllOfAndAnyOfUseExactIdentity()
    {
        var prerequisiteA = Match("Prerequisite A");
        var prerequisiteB = Match("Prerequisite B");
        var all = Match(
            "ALL dependent",
            [new PatDepGroup(PatDepMode.All, ["Prerequisite A", "Prerequisite B"])]);
        var any = Match(
            "ANY dependent",
            [new PatDepGroup(PatDepMode.Any, ["Prerequisite A", "Prerequisite B"])]);

        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable([all], [prerequisiteA]),
            "Expected ALL-OF to block until every prerequisite is active.");
        PatDepVal.ValidateEnable([all], [prerequisiteA, prerequisiteB]);
        PatDepVal.ValidateEnable([any], [prerequisiteB]);
        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable([any], []),
            "Expected ANY-OF to block when no alternative is active.");

        var crossHash = Match("Prerequisite A", hash: OtherHash);
        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable([any], [crossHash]),
            "Expected a cross-hash prerequisite to be rejected.");

        var crossSerial = Match("Prerequisite A", serial: "BLUS00002");
        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable([any], [crossSerial]),
            "Expected a cross-serial prerequisite to be rejected.");

        var crossVersion = Match("Prerequisite A", version: "01.01");
        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable([any], [crossVersion]),
            "Expected a cross-version prerequisite to be rejected.");
    }

    private static void DependencyIdentity_RequiresSameOwnershipChannel()
    {
        var dependent = Match(
            "Dependent",
            [new PatDepGroup(PatDepMode.All, ["Prerequisite"])]);
        var officialPrerequisite = Match(
            "Prerequisite",
            source: PatSrc.Official);
        var managedArtemisPrerequisite = Match(
            "Prerequisite",
            source: PatSrc.ManagedArtemis);

        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateEnable(
                [dependent],
                [officialPrerequisite]),
            "Expected an Official definition with the same runtime identity and title not to satisfy an Artemis dependency.");
        PatDepVal.ValidateEnable(
            [dependent],
            [managedArtemisPrerequisite]);

        var catalogDependent = Entry(
            "Catalog Dependent",
            [new PatDepGroup(PatDepMode.All, ["Catalog Prerequisite"])]);
        var officialCatalogPrerequisite = Entry(
            "Catalog Prerequisite",
            source: PatSrc.Official);
        var managedCatalogPrerequisite = Entry(
            "Catalog Prerequisite",
            source: PatSrc.ManagedArtemis);

        Expect<InvalidDataException>(
            () => PatDepVal.ValidateCatalog(
                [catalogDependent, officialCatalogPrerequisite]),
            "Expected catalog validation not to collapse Official and Artemis dependency identities.");
        PatDepVal.ValidateCatalog(
            [catalogDependent, managedCatalogPrerequisite]);

        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateDisable(
                [managedArtemisPrerequisite],
                [dependent, managedArtemisPrerequisite, officialPrerequisite]),
            "Expected an Official same-name definition not to mask removal of the required managed Artemis prerequisite.");
    }

    private static void Compiler_PreservesAndAcceptsPreparedPrerequisite()
    {
        var source =
            "Version: 1.2\n" +
            $"PPU-{Hash}:\n" +
            PatchYaml("Enable Codes", "0x00100000") +
            PatchYaml("Dependent Health", "0x00100004");
        var read = new ReadSvc().ReadText(source, PatSrc.Artemis, "art.yml");
        Assert(read.IsOk, "Expected compiler dependency fixture to parse: " + read.Error);

        var game = new Rpcs3GameDescriptor(
            Serial,
            "Dependency Game",
            @"C:\Games\BLUS00001",
            Version,
            Hash,
            null);
        var matches = read.Catalog.Entries
            .Select(entry => new PatchMatchService().Match(entry, [game]))
            .Select(match => match.Patch.Title == "Dependent Health"
                ? match with
                {
                    Patch = match.Patch with
                    {
                        DepGroups =
                        [
                            new PatDepGroup(PatDepMode.All, ["Enable Codes"])
                        ]
                    }
                }
                : match)
            .ToArray();
        var prerequisite = matches.Single(match => match.Patch.Title == "Enable Codes");
        var dependent = matches.Single(match => match.Patch.Title == "Dependent Health");
        var compiler = new PatchSelectionCompiler();

        var missing = compiler.Compile(source, [dependent]);
        Assert(!missing.IsOk &&
               missing.Error?.Contains("Enable Codes", StringComparison.Ordinal) == true,
            "Expected compiler planning to block a missing prerequisite.");

        var prepared = compiler.Compile(
            source,
            [prerequisite, dependent],
            [dependent]);
        Assert(prepared.IsOk && prepared.SelNum == 2,
            "Expected an already prepared prerequisite to satisfy dependency closure: " +
            prepared.Error);
    }

    private static void Disable_ProtectsDependenciesAndAllowsValidClosure()
    {
        var prerequisiteA = Match("Prerequisite A");
        var prerequisiteB = Match("Prerequisite B");
        var all = Match(
            "ALL dependent",
            [new PatDepGroup(PatDepMode.All, ["Prerequisite A"])]);
        var any = Match(
            "ANY dependent",
            [new PatDepGroup(PatDepMode.Any, ["Prerequisite A", "Prerequisite B"])]);

        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateDisable(
                [prerequisiteA],
                [prerequisiteA, all]),
            "Expected disabling a required prerequisite alone to be blocked.");
        PatDepVal.ValidateDisable(
            [prerequisiteA, all],
            [prerequisiteA, all]);

        PatDepVal.ValidateDisable(
            [prerequisiteA],
            [prerequisiteA, prerequisiteB, any]);
        Expect<PatchDependencyException>(
            () => PatDepVal.ValidateDisable(
                [prerequisiteA, prerequisiteB],
                [prerequisiteA, prerequisiteB, any]),
            "Expected removing every ANY-OF alternative while its dependent remains to be blocked.");
    }

    private static void Catalog_DependencyCycleFailsClosedBeforePlanning()
    {
        var first = Entry(
            "Cycle A",
            [new PatDepGroup(PatDepMode.All, ["Cycle B"])]);
        var second = Entry(
            "Cycle B",
            [new PatDepGroup(PatDepMode.All, ["Cycle A"])]);

        Expect<InvalidDataException>(
            () => PatDepVal.ValidateCatalog([first, second]),
            "Expected a dependency cycle to invalidate the catalog before planning.");
    }

    private static void UiProjection_LocalizesDependencyGrammarAndPreservesCanonicalTitles()
    {
        var originalLanguage = UiText.CurLang;
        var canonicalTitle = "Button Enable Code";
        var item = new PatItVm(Match(
            "Dependent",
            [new PatDepGroup(PatDepMode.Any, [canonicalTitle, "Marker B"])]));

        try
        {
            UiText.SetLang(UiText.EnLang);
            var english = item.Dependencies.Single();
            var englishLabel = english.Label;
            var englishTitles = english.Titles;
            UiText.SetLang(UiText.ArabLang);
            item.SetLang();
            var arabic = item.Dependencies.Single();

            Assert(englishLabel == "Requires one of:" &&
                   arabic.Label == "يتطلب أحد الخيارات:",
                "Expected dependency grammar to be localized in English and Arabic.");
            Assert(englishTitles == canonicalTitle + ", Marker B" &&
                   arabic.Titles == englishTitles,
                "Expected canonical dependency titles to remain untranslated and separate from localized prose.");
            Assert(arabic.TechnicalFlow == System.Windows.FlowDirection.LeftToRight,
                "Expected canonical dependency titles to remain in an LTR technical surface.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }

    private static PatMatch Match(
        string title,
        IReadOnlyList<PatDepGroup>? dependencies = null,
        string hash = Hash,
        string serial = Serial,
        string version = Version,
        PatSrc source = PatSrc.Artemis,
        PatClass patchClass = PatClass.Main)
    {
        var entry = Entry(
            title,
            dependencies,
            hash,
            serial,
            version,
            source,
            patchClass);
        return new PatMatch(
            entry,
            MatStat.Match,
            serial,
            version,
            HasMeta: true,
            Reason: "Exact dependency fixture.",
            TgtName: "Dependency Game",
            TgtSer: serial,
            TgtVer: version);
    }

    private static Rpcs3PatchEntry Entry(
        string title,
        IReadOnlyList<PatDepGroup>? dependencies = null,
        string hash = Hash,
        string serial = Serial,
        string version = Version,
        PatSrc source = PatSrc.Artemis,
        PatClass patchClass = PatClass.Main)
    {
        return new Rpcs3PatchEntry(
            title,
            HashKind.Ppu,
            hash,
            [new R3PatTgt("Dependency Game", serial, [version])],
            "Cheat",
            PatRisk.Cheat,
            Notes: null,
            Author: "R3Test",
            PatVer: "1.0",
            Group: null,
            Source: source,
            Class: patchClass,
            DepGroups: dependencies);
    }

    private static string PatchYaml(
        string title,
        string offset)
    {
        return
            $"  \"{title}\":\n" +
            "    Games:\n" +
            "      \"Dependency Game\":\n" +
            $"        {Serial}: [ {Version} ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            $"      - [ be32, {offset}, 0x60000000 ]\n";
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Expect<TException>(
        Action action,
        string message)
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

        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
