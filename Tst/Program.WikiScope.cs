using System.Globalization;
using System.IO;
using System.Text.Json;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;
using R3Config.Vm;

internal static partial class Program
{
    private static readonly string[] PrimaryScopeKeys = ["PPU Decoder", "Read Color Buffers"];
    private static readonly string[] CompleteScopeKeys = ["SPU Decoder", "Write Color Buffers"];
    private static readonly string[] AdjacentEditionSerials = ["BLES10001", "BLES10002"];
    private static readonly string[] DigitalScopeSerials = ["NPEB10000", "NPJB10000", "NPUB10000"];
    private static readonly string[] DiscScopeSerials = ["BLES10001", "BLJM10001", "BLUS10001"];
    private static readonly string[] EditionDemoScopeSerials = ["BCUS10003", "BLES10003", "NPEA10003", "NPUA10003"];
    private static readonly string[] BattlePrincessDigitalSerials = ["NPEB02001", "NPJB00391", "NPUB31486"];
    private static readonly string[] ZoneDiscSerials = ["BLES01756", "BLJM60451", "BLUS30937"];

    private static void WikiScope_Restrictions(string root)
    {
        var path = Path.Combine(root, "scope-20260907.xml");
        File.WriteAllText(path, """
            <mediawiki>
            <page><title>Scope fixture</title><ns>0</ns><id>1</id><revision><id>1</id><text><![CDATA[
            <!-- {{gameid|id=BLES19998}} -->
            {{gameid|id=BLES10000}}<br>'''''The Complete Edition'''''<br>{{gameid|id=BLES10001}}
            <br>'''''Soundtrack'''''<br>{{gameid|id=NPUB10000}}
            {{config
            |write color buffers=On
            |write color buffers notes=Required for Complete Edition.
            |read color buffers=On
            |read color buffers notes=Required except BLES10001.
            |ppu decoder=Recompiler (LLVM)
            |ppu decoder notes=Not for Complete Edition.
            |spu decoder=Recompiler (LLVM)
            |spu decoder notes=Only for BLES10001.
            |resolution scale threshold=640x640
            |resolution scale threshold notes=Version 1.02 only.
            |anti-aliasing=Disabled
            |anti-aliasing notes=Use with the 60 FPS patch.
            |shader quality=High
            |shader quality notes=European region only.
            |vblank rate=120
            |vblank rate notes=Only for BLES19999.
            |orphan notes=This is informational prose only.
            }}
            ]]></text></revision></page>
            <page><title>Scope fixture alias</title><ns>0</ns><id>2</id><redirect title="Scope fixture"/>
            <revision><id>2</id><text>#REDIRECT [[Scope fixture]]</text></revision></page>
            <page><title>Scope fixture similar title</title><ns>0</ns><id>3</id><revision><id>3</id>
            <text>{{config|write color buffers=On}}</text></revision></page>
            </mediawiki>
            """);
        var analysis = WikiSvc.Analyze(path);
        var page = analysis.Pages.Single(item => item.Title == "Scope fixture");
        Assert(page.Serials.Count == 3 && !page.Serials.Contains("BLES19998"),
            "Commented GameIDs are not explicit Wiki page coverage.");
        var primary = analysis.CatalogRecommendations.Single(item => item.Serial == "BLES10000");
        var complete = analysis.CatalogRecommendations.Single(item => item.Serial == "BLES10001");
        Assert(primary.Changes.Select(item => item.Key).Order().SequenceEqual(PrimaryScopeKeys) &&
               complete.Changes.Select(item => item.Key).Order().SequenceEqual(CompleteScopeKeys),
            "Positive/negative edition and serial scope must select only the explicitly covered settings.");
        Assert(complete.Binding == WikiGameIdRole.Primary &&
               page.GameIdBindings.Single(item => item.Serial == complete.Serial).Role == WikiGameIdRole.RelatedEdition,
            "Generated applicability must not rewrite the source release-role evidence.");
        Assert(analysis.CatalogRecommendations.Single(item => item.Serial == "NPUB10000").Changes.Count == 0 &&
               analysis.CatalogRecommendations.All(item => item.WikiName == "Scope fixture"),
            "Non-game media, redirects and similar titles must not acquire game configuration.");
        Assert(page.Recommendations.Single(item => item.WikiKey == "resolution scale threshold").Classification == WikiRecommendationClass.VersionSpecific &&
               page.Recommendations.Single(item => item.WikiKey == "anti-aliasing").Classification == WikiRecommendationClass.PatchSpecific &&
               page.Recommendations.Single(item => item.WikiKey == "shader quality").Classification == WikiRecommendationClass.Ambiguous &&
               page.Recommendations.Single(item => item.WikiKey == "vblank rate").Classification == WikiRecommendationClass.Ambiguous &&
               page.Notes.Single(item => item.WikiKey == "orphan").Classification == WikiRecommendationClass.InformationalNote,
            "Version, patch, unknown region, absent serial and informational scope must remain distinct and fail-closed.");

        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "restricted"));
        var game = new Rpcs3GameDescriptor(primary.Serial, primary.WikiName, env.GamePath, null, null, null);
        var plan = new PlanService().BuildCfg(env.Install, game, primary);
        var rejected = false;
        try
        {
            new ConfigApplyService().Apply(plan, complete.Changes, Path.Combine(root, "restricted-backups"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not part of the reviewed plan", StringComparison.Ordinal))
        {
            rejected = true;
        }
        Assert(rejected && !Directory.Exists(Path.Combine(root, "restricted-backups")),
            "Settings scoped to a different edition must be rejected before mutation.");

        foreach (var (note, expectedSerials) in new[]
        {
            ("Not required for BLES10001.", new[] { "BLES10000" }),
            ("Required for Complete Edition; tested on BLES10000.", Array.Empty<string>()),
            ("Required for Complete Edition; only for BLES10001.", new[] { "BLES10001" }),
            ("For Complete Edition. European region only.", Array.Empty<string>()),
            ("Patch X only.", Array.Empty<string>()),
            ("Versions 1.02 only.", Array.Empty<string>())
        })
        {
            var scopedPath = Path.Combine(root, "combined-scope-20260907.xml");
            File.WriteAllText(scopedPath, "<mediawiki><page><title>Combined scope</title><ns>0</ns><revision><text><![CDATA[" +
                "{{gameid|id=BLES10000}}<br>'''''Complete Edition'''''<br>{{gameid|id=BLES10001}}" +
                "{{config|write color buffers=On|write color buffers notes=" + note + "}}]]></text></revision></page></mediawiki>");
            var scopedAnalysis = WikiSvc.Analyze(scopedPath);
            Assert(scopedAnalysis.CatalogRecommendations.Where(rec => rec.Changes.Count > 0)
                    .Select(rec => rec.Serial).Order().SequenceEqual(expectedSerials),
                "Combined/negative restriction expanded applicability: " + note);
        }

        var releaseKindPath = Path.Combine(root, "release-kind-scope-20260907.xml");
        File.WriteAllText(releaseKindPath, """
            <mediawiki>
            <page><title>Digital scope</title><ns>0</ns><id>10</id><revision><text><![CDATA[
            {{gameid|id=BLES10000, NPUB10000, NPEB10000|oid=NPJB10000|ird=Yes}}
            {{config|accurate rsx reservation access=On|accurate rsx reservation access notes=Needed for digital edition to not crash on boot.}}
            ]]></text></revision></page>
            <page><title>Disc scope</title><ns>0</ns><id>11</id><revision><text><![CDATA[
            {{gameid|id=BLES10001, BLUS10001, NPEB10001|oid=BLJM10001|ird=Yes}}
            <br>'''''Standalone Edition'''''<br>{{gameid|id=NPUB10001|ird=NA}}
            {{config|ppu decoder=Interpreter (static)|ppu decoder notes=Prevents hanging on boot. (Disc Versions only)}}
            ]]></text></revision></page>
            <page><title>Edition demo scope</title><ns>0</ns><id>12</id><revision><text><![CDATA[
            {{gameid|id=BLES10002}}<br>'''''Move Edition'''''<br>{{gameid|id=BLES10003}}, {{gameid|id=BCUS10003}}
            <br>'''''Move Edition Demo'''''<br>{{gameid|id=NPEA10003|oid=NPUA10003|ird=NA}}
            <br>'''''Beta'''''<br>{{gameid|oid=BCET10003|ird=NA}}
            {{config|camera input=PS Eye|camera input notes=To be able to get ingame for move edition.}}
            ]]></text></revision></page>
            </mediawiki>
            """);
        var releaseKinds = WikiSvc.Analyze(releaseKindPath);
        Assert(releaseKinds.CatalogRecommendations.Where(rec => rec.WikiName == "Digital scope" && rec.Changes.Count > 0)
                   .Select(rec => rec.Serial).Order().SequenceEqual(DigitalScopeSerials) &&
               releaseKinds.CatalogRecommendations.Where(rec => rec.WikiName == "Disc scope" && rec.Changes.Count > 0)
                   .Select(rec => rec.Serial).Order().SequenceEqual(DiscScopeSerials) &&
               releaseKinds.CatalogRecommendations.Where(rec => rec.WikiName == "Edition demo scope" && rec.Changes.Count > 0)
                   .Select(rec => rec.Serial).Order().SequenceEqual(EditionDemoScopeSerials),
            "Digital/disc GameID kind and edition-demo scope must resolve only explicitly covered releases.");
        var ownershipPath = Path.Combine(root, "ownership-20260907.xml");
        File.WriteAllText(ownershipPath, """
            <mediawiki>
            <page><title>Dedicated page</title><ns>0</ns><id>1</id><revision><text>{{gameid|id=BLES10001}}{{config}}</text></revision></page>
            <page><title>Shared page</title><ns>0</ns><id>2</id><revision><text><![CDATA[
            {{gameid|id=BLES10000}}<br>'''''Complete Edition'''''<br>{{gameid|id=BLES10001}}, {{gameid|id=BLES10002}}
            {{config|write color buffers=On|write color buffers notes=Required for Complete Edition.}}
            ]]></text></revision></page>
            </mediawiki>
            """);
        var ownership = WikiSvc.Analyze(ownershipPath);
        Assert(ownership.CatalogRecommendations.Where(rec => rec.Changes.Count > 0).Select(rec => rec.Serial)
                   .SequenceEqual(AdjacentEditionSerials) &&
               ownership.CatalogRecommendations.Single(rec => rec.Serial == "BLES10001").WikiName == "Shared page" &&
               ownership.Pages.Single(item => item.Title == "Shared page").GameIdBindings
                   .Single(binding => binding.Serial == "BLES10002").VariantLabel == "Complete Edition",
            "An empty dedicated page or adjacent GameID template must not discard explicit release-scoped settings.");

        File.WriteAllText(ownershipPath, """
            <mediawiki>
            <page><title>Owner A</title><ns>0</ns><id>1</id><revision><text>{{gameid|id=BLES10000}}{{config|write color buffers=On}}</text></revision></page>
            <page><title>Owner B</title><ns>0</ns><id>2</id><revision><text>{{gameid|id=BLES10000}}{{config|write color buffers=Off}}</text></revision></page>
            </mediawiki>
            """);
        var conflicting = WikiSvc.Analyze(ownershipPath);
        var conflictingCatalog = RecommendationCatalogUseCase.Rebuild(ownershipPath);
        var useCases = R3Config.Composition.AppComposition.Build(new Rpcs3MutationGuard(() => false));
        var conflictingSetup = useCases.GameSetup.Build(env.Install, game, conflictingCatalog, new PatCat("1.2", []));
        Assert(conflicting.Findings.Any(finding => finding.Code == WikiFindingCode.DuplicateSerialConflict) &&
               conflictingCatalog.Find(game.Serial) is null && conflictingSetup.BaseConfiguration.Items.Count == 0,
            "Source ownership conflicts must stay excluded through generated catalog and GameSetup without a serial blacklist.");
        var conflictPreview = useCases.GameSetup.Preview(conflictingSetup, new GameSetupSelection(primary.Changes, []));
        Assert(!conflictPreview.CanApply && conflictPreview.EffectiveSettings.Count == 0,
            "A caller cannot inject ambiguous Wiki settings into Preview/Apply.");
        Console.WriteLine("WikiScope_Restrictions PASS.");
    }

    private static void WikiScope_SourceParity(string root, string mirrorPath, string currentPath,
        string beforeCatalogPath, string reportRoot)
    {
        Directory.CreateDirectory(reportRoot);
        var before = RecSvc.Load(beforeCatalogPath);
        var shipped = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json"));
        WikiCorpusAnalysis? mirrorAnalysis = null;
        foreach (var (path, label) in new[] { (mirrorPath, "mirror-20241214"), (currentPath, "catalog-source") })
        {
            var analysis = WikiSvc.Analyze(path);
            if (label == "mirror-20241214") mirrorAnalysis = analysis;
            var gtaPage = analysis.Pages.Single(page => page.Title == "Grand Theft Auto IV");
            Assert(gtaPage.GameIdBindings.Any(binding => binding.Serial == "BLES01128") &&
                   analysis.CatalogRecommendations.Single(rec => rec.Serial == "BLES01128").Changes.Count ==
                       (label == "mirror-20241214" ? 8 : 9),
                "The complete source scan must confirm GTA IV fixture coverage and snapshot-specific settings.");
            if (label == "catalog-source")
            {
                var eyePet = analysis.Pages.Single(page => page.Title == "EyePet");
                var eyePetSettings = eyePet.Recommendations
                    .Where(entry => entry.WikiKey is "camera input" or "camera settings" or "move handler")
                    .ToArray();
                var expectedEyePet = new[] { "BCAS20140", "BCES00864", "NPEA90063", "NPHA80111", "NPHA80122", "NPJA90142", "NPUA70103" };
                Assert(eyePetSettings.Length == 3 &&
                       eyePetSettings.All(entry => entry.ApplicableSerials.Order().SequenceEqual(expectedEyePet)),
                    "EyePet Move Edition scope must include its explicitly labeled demo and exclude base/beta releases.");

                var battlePrincess = analysis.Pages.Single(page => page.Title == "Battle Princess of Arcadias");
                Assert(battlePrincess.Recommendations.Single(entry => entry.WikiKey == "accurate rsx reservation access")
                           .ApplicableSerials.Order().SequenceEqual(BattlePrincessDigitalSerials),
                    "Digital edition scope must follow the Wiki GameID digital/PSN kind.");

                var zone = analysis.Pages.Single(page => page.Title == "Zone of the Enders HD Collection");
                Assert(zone.Recommendations.Single(entry => entry.WikiKey == "ppu decoder")
                           .ApplicableSerials.Order().SequenceEqual(ZoneDiscSerials),
                    "Disc Versions scope must follow the Wiki GameID disc kind.");

                var fuse = analysis.Pages.Single(page => page.Title == "Fuse");
                var fuseFrameLimit = fuse.Recommendations.Single(entry => entry.WikiKey == "framelimit");
                Assert(fuseFrameLimit.Classification == WikiRecommendationClass.PatchSpecific && !fuseFrameLimit.IncludedInCatalog,
                    "Patch-dependent frame limit must remain conditional rather than an unconditional catalog default.");
            }

            var generated = RecSvc.FromRec("RPCS3 Wiki snapshot", analysis.SnapshotDate, analysis.CatalogRecommendations,
                DateTimeOffset.ParseExact(analysis.SnapshotDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), analysis.SourceSha256);
            var generatedPath = Path.Combine(root, label + ".json");
            generated.Save(generatedPath);
            var loaded = RecSvc.Load(generatedPath);
            if (label == "catalog-source")
            {
                Assert(shipped.SrcHash == analysis.SourceSha256 && shipped.DataVer == analysis.SnapshotDate &&
                       shipped.Count == loaded.Count && shipped.Rec.All(rec =>
                           loaded.Find(rec.Serial) is { } expected && rec.Binding == expected.Binding &&
                           rec.WikiName == expected.WikiName && rec.VariantLabel == expected.VariantLabel &&
                           rec.Changes.SequenceEqual(expected.Changes)),
                    "Shipped catalog must be generated from its exact declared source with current scope semantics.");
            }
            var allSerials = analysis.Pages.SelectMany(page => page.Serials).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var candidates = analysis.Pages.SelectMany(page => page.Recommendations)
                .Where(entry => entry.IncludedInCatalog).SelectMany(entry => entry.ApplicableSerials)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var env = GameSetupT.CreateEnvironment(Path.Combine(root, label));
            var useCases = R3Config.Composition.AppComposition.Build(new Rpcs3MutationGuard(() => false));
            var missing = new List<string>();
            var conflicts = new List<string>();
            var actual = 0;
            foreach (var serial in candidates)
            {
                var rec = loaded.Find(serial);
                if (rec is null || rec.Changes.Count == 0)
                {
                    var conflict = analysis.Findings.Any(finding => finding.Serial == serial &&
                        finding.Code is WikiFindingCode.DuplicateSerialConflict or WikiFindingCode.DuplicateSettingConflict);
                    if (conflict) conflicts.Add(serial);
                    else missing.Add(serial);
                    continue;
                }
                var sourcePages = analysis.Pages.Where(page => page.IsGameContent && page.Serials.Contains(serial)).ToArray();
                var expected = sourcePages.SelectMany(page => page.Recommendations).Where(entry => entry.IncludedInCatalog && entry.ApplicableSerials.Contains(serial))
                    .SelectMany(entry => entry.MappedSettings).Select(item => (item.Section, item.Key, item.Value)).Distinct().ToArray();
                Assert(rec.Binding == WikiGameIdRole.Primary &&
                       sourcePages.Any(page => page.GameIdBindings.Any(binding => binding.Serial == serial)) &&
                       rec.Changes.Select(item => (item.Section, item.Key, item.Value)).ToHashSet().SetEquals(expected),
                    $"Generated settings lost or expanded explicit Wiki scope for {serial}.");
                var game = new Rpcs3GameDescriptor(serial, rec.WikiName, env.GamePath, null, null, null);
                var plan = useCases.GameSetup.Build(env.Install, game, loaded, new PatCat("1.2", [])).BaseConfiguration;
                var vm = new PlanVm();
                vm.Load(plan);
                Assert(vm.Items.Select(item => (item.Section, item.Key, item.Val)).ToHashSet().SetEquals(expected) &&
                       vm.Items.All(item => item.IsApply || item.ShowSelection),
                    $"GameSetup/UI lost normal configured or selectable settings for {serial}.");
                if (plan.Items.Count != rec.Changes.Count || vm.Items.Count != rec.Changes.Count || vm.IsEmpty)
                    missing.Add(serial);
                else actual++;
            }
            var related = analysis.Pages.SelectMany(page => page.GameIdBindings)
                .Where(binding => binding.Role == WikiGameIdRole.RelatedEdition)
                .Select(binding => binding.Serial).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var unresolved = related.Where(serial => loaded.Find(serial) is not { Binding: WikiGameIdRole.Primary, Changes.Count: > 0 })
                .Select(serial => new
                {
                    Serial = serial,
                    Pages = analysis.Pages.Where(page => page.Serials.Contains(serial)).Select(page => page.Title).ToArray(),
                    Reason = analysis.Findings.FirstOrDefault(finding => finding.Serial == serial)?.Detail ??
                        (analysis.Pages.Where(page => page.Serials.Contains(serial)).All(page => page.Recommendations.Count == 0)
                            ? "NO_CONFIG_VALUES: source pages contain no non-empty configuration recommendations."
                            : "SCOPED_OR_UNSUPPORTED: no applicable mapped setting for this serial."),
                    Evidence = analysis.Pages.Where(page => page.Serials.Contains(serial)).SelectMany(page => page.Recommendations)
                        .Select(entry => new { entry.Page, entry.WikiKey, entry.Notes, Classification = entry.Classification.ToString(),
                            entry.ApplicableSerials, entry.Detail }).ToArray()
                }).ToArray();
            var corrected = before.Rec.Where(rec => rec.Binding != WikiGameIdRole.Primary && rec.Changes.Count > 0 &&
                loaded.Find(rec.Serial) is { Binding: WikiGameIdRole.Primary, Changes.Count: > 0 }).ToArray();
            var previousRelated = before.Rec.Where(rec => rec.Binding == WikiGameIdRole.RelatedEdition && rec.Changes.Count > 0).ToArray();
            var counts = new
            {
                TOTAL_WIKI_PAGES = analysis.Stats.TotalPages,
                TOTAL_EXPLICIT_SERIALS = allSerials.Length,
                SERIALS_WITH_SETTINGS = candidates.Length,
                EXACT_APPLICABLE_SERIALS = actual,
                VERSION_SCOPED_SERIALS = Scoped(WikiRecommendationClass.VersionSpecific),
                PATCH_SCOPED_SERIALS = Scoped(WikiRecommendationClass.PatchSpecific),
                INFORMATIONAL_ONLY = analysis.Pages.Where(page => page.Notes.Count > 0 && !page.Recommendations.Any(entry => entry.IncludedInCatalog))
                    .SelectMany(page => page.Serials).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                UNRESOLVED_RELATED = unresolved.Length,
                SOURCE_PRESENT_RUNTIME_MISSING = missing.Count,
                INTENTIONAL_CONFLICTS = conflicts.Count,
                CORRECTED_PREVIOUS_NONPRIMARY = corrected.Length,
                CORRECTED_PREVIOUS_RELATED = corrected.Count(rec => rec.Binding == WikiGameIdRole.RelatedEdition),
                PREVIOUS_RELATED_STILL_UNRESOLVED = previousRelated.Count(rec =>
                    loaded.Find(rec.Serial) is not { Binding: WikiGameIdRole.Primary, Changes.Count: > 0 })
            };
            File.WriteAllText(Path.Combine(reportRoot, label + ".json"), JsonSerializer.Serialize(new
            {
                analysis.SourceFile, analysis.SourceSha256, analysis.SnapshotDate, Counts = counts,
                UnresolvedRelated = unresolved, IntentionalConflicts = conflicts, Missing = missing,
                CorrectedSerials = corrected.Select(rec => rec.Serial).ToArray(),
                PreviousRelatedUnresolved = previousRelated.Where(rec => loaded.Find(rec.Serial) is not
                    { Binding: WikiGameIdRole.Primary, Changes.Count: > 0 }).Select(rec => rec.Serial).ToArray(),
                Pages = analysis.Pages.Select(page => new
                {
                    page.Title, page.PageId, page.RevisionId, page.RevisionTimestamp, page.Serials,
                    page.GameIdBindings, page.Recommendations, page.Notes, page.PatchReferences
                }), analysis.Findings
            }, TestJson.Indented));
            Console.WriteLine(label + " " + JsonSerializer.Serialize(counts));
            Assert(missing.Count == 0, "Wiki source evidence disappeared in generated catalog/runtime: " + string.Join(", ", missing));

            if (label == "catalog-source" && mirrorAnalysis is not null)
            {
                // Historical values/IDs removed by the newer source are source
                // deltas, not runtime losses and must not be silently resurrected.
                var historical = mirrorAnalysis.Recommendations.Where(entry => entry.IncludedInCatalog)
                    .SelectMany(entry => entry.ApplicableSerials).Distinct(StringComparer.OrdinalIgnoreCase);
                var sourceDelta = historical.Where(serial => shipped.Find(serial) is not { Changes.Count: > 0 })
                    .Select(serial => new
                    {
                        Serial = serial,
                        Reason = analysis.Findings.Any(finding => finding.Serial == serial && finding.Code == WikiFindingCode.DuplicateSerialConflict)
                            ? "CURRENT_SOURCE_CONFLICT" : "CURRENT_SOURCE_REMOVED_OR_RESTRICTED",
                        Historical = mirrorAnalysis.Recommendations.Where(entry => entry.IncludedInCatalog && entry.ApplicableSerials.Contains(serial)),
                        Current = analysis.Pages.Where(page => page.Serials.Contains(serial)).Select(page => new
                            { page.Title, page.GameIdBindings, page.Recommendations, page.Notes })
                    }).ToArray();
                File.WriteAllText(Path.Combine(reportRoot, "historical-to-shipped-delta.json"),
                    JsonSerializer.Serialize(sourceDelta, TestJson.Indented));
                Assert(sourceDelta.All(row => !candidates.Contains(row.Serial) || conflicts.Contains(row.Serial)),
                    "A currently applicable historical serial disappeared from the shipped catalog.");
                Console.WriteLine($"HISTORICAL_TO_SHIPPED_EMPTY={sourceDelta.Length}; CURRENT_SOURCE_CONFLICT={sourceDelta.Count(row => row.Reason == "CURRENT_SOURCE_CONFLICT")}; CURRENT_SOURCE_REMOVED_OR_RESTRICTED={sourceDelta.Count(row => row.Reason == "CURRENT_SOURCE_REMOVED_OR_RESTRICTED")}");
            }

            int Scoped(WikiRecommendationClass classification) => analysis.Recommendations
                .Where(entry => entry.Classification == classification).SelectMany(entry => entry.ApplicableSerials)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
    }
}
