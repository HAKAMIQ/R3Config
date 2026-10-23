using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using R3Config;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// The shipped, generated knowledge file and its production consumption: strict validation
// (fail-closed, whole-file), exact page lookup, Inform-only decisions with provenance-only authority,
// no raw corpus in the file, and the Game Settings projection carrying it. Fixtures are synthetic
// except the shipped data checks, which read the build output's Data folder.
internal static class Rpcs3KnowledgeT
{
    private static readonly string Sha = new('a', 64);

    public static void Run(string tempRoot)
    {
        AssertShippedFile();
        AssertValidationFailsClosed();
        AssertLookupIsExactAndInformOnly();
        AssertLoaderIsOptionalAndFailClosed(tempRoot);
        AssertProjectionCarriesKnowledge();
        AssertViewModelLines();
        AssertGeneratorDeterminism(tempRoot);

        Console.WriteLine("Rpcs3 knowledge (compact shipped file, strict validation, exact page lookup, Inform-only, optional loader, production projection, generator determinism) PASS.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static string ShippedPath => Path.Combine(AppContext.BaseDirectory, "Data", Rpcs3KnowledgeCatalog.FileName);

    private static Rpcs3KnowledgeDocument Doc(
        HistoricalRemovalFact[]? removals = null,
        ThreadScalingFact[]? scaling = null,
        SettingLifecycleFact[]? settings = null,
        int schema = Rpcs3KnowledgeCatalog.SchemaVersion) =>
        new(schema, "t", [new KnowledgeSource("s", "k", "n", Sha, null, null)],
            settings ?? [], removals ?? [], scaling ?? [], []);

    private static HistoricalRemovalFact Removal(RemovalReason reason = RemovalReason.ClaimedFixed, string page = "Game A") =>
        new(page, "renderer", reason, "EDIT_COMMENT_CLAIM", "2020-01-01T00:00:00Z", "fixed");

    private static ThreadScalingFact Scaling(string? page, string? link = null) =>
        new("Game A", page, "60", "8C/8T", 8, 8, "GREAT", link, null);

    private static bool Rejects(Rpcs3KnowledgeDocument document)
    {
        try
        {
            Rpcs3KnowledgeCatalog.Validate(document);
            return false;
        }
        catch (Rpcs3KnowledgeRejectedException)
        {
            return true;
        }
    }

    private static void AssertShippedFile()
    {
        Assert(File.Exists(ShippedPath), "The build output must carry Data/rpcs3-knowledge.json.");
        var bytes = File.ReadAllBytes(ShippedPath);
        Assert(bytes.Length < 128 * 1024, "The knowledge file stays compact (no raw corpus).");

        var catalog = Rpcs3KnowledgeCatalog.Parse(bytes);
        var doc = catalog.Document;
        Assert(doc.Settings.Count == 7 && doc.HistoricalRemovals.Count == 40 && doc.ThreadScaling.Count == 43 && doc.CpuTiers.Count == 260,
            "The shipped knowledge carries the verified extraction counts.");
        Assert(doc.HistoricalRemovals.All(f => f.Reason is RemovalReason.ClaimedFixed or RemovalReason.SettingRemoved or RemovalReason.Replaced),
            "Only informative removal reasons ship; bulk cleanup and unknown do not.");
        Assert(doc.HistoricalRemovals.All(f => f.Reason != RemovalReason.VerifiedFixedUpstream),
            "A claimed fix is never shipped as verified.");
        Assert(doc.ThreadScaling.Count(f => f.WikiPage is not null) == 24 && doc.ThreadScaling.All(f => f.WikiPage is null || f.WikiPage == f.Game),
            "A workbook game is attached only by an exact Wiki page title.");
        Assert(doc.Settings.All(f => f.LastChangeBuild is null == (f.LastChangeCommit is null)),
            "A lifecycle build is proven together with its commit or absent.");
        Assert(doc.Settings.Count(f => f.LastChangeBuild is not null) == 4 &&
               doc.Settings.Where(f => f.LastChangeBuild is not null).All(f => f.Evidence == "LAST_TOUCH_IS_REMOVAL_COMMIT"),
            "Only four shipped lifecycle rows carry independently proven removal commits/builds.");
        Assert(doc.Sources.Count == 4 && doc.Sources.All(s => s.Sha256.Length == 64),
            "Every knowledge source keeps its SHA-256.");

        var text = Encoding.UTF8.GetString(bytes);
        Assert(Rpcs3KnowledgeCatalog.Serialize(doc) == text.Replace("\r\n", "\n", StringComparison.Ordinal),
            "The shipped file is exactly the deterministic serialization of its content.");
        Assert(!text.Contains("Contributor", StringComparison.OrdinalIgnoreCase) && !text.Contains("RPCS3 Bot", StringComparison.Ordinal),
            "Contributor names are not shipped.");
    }

    private static void AssertValidationFailsClosed()
    {
        Rpcs3KnowledgeCatalog.Validate(Doc([Removal()], [Scaling("Game A", "https://example.org/x")]));

        Assert(Rejects(Doc(schema: 2)), "An unknown schema is rejected.");
        foreach (var reason in new[] { RemovalReason.BulkCleanup, RemovalReason.Unknown, RemovalReason.VerifiedFixedUpstream, RemovalReason.FieldRenamed })
        {
            Assert(Rejects(Doc([Removal(reason)])), $"A {reason} removal must not ship.");
        }

        Assert(Rejects(Doc([Removal(), Removal()])), "Duplicate removals are rejected.");
        Assert(Rejects(Doc(scaling: [Scaling("Game A", "javascript:alert(1)")])), "A non-http link is rejected.");
        Assert(Rejects(Doc(scaling: [Scaling("Game A", "file:///C:/x")])), "A file link is rejected.");
        Assert(Rejects(new Rpcs3KnowledgeDocument(1, "t", [new KnowledgeSource("s", "k", "n", "xyz", null, null)], [], [], [], [])),
            "A malformed source hash is rejected.");
        Assert(Rejects(new Rpcs3KnowledgeDocument(1, "t", [], [], [], [], [])), "A file without a source is rejected.");
        Assert(Rejects(Doc(settings: [new SettingLifecycleFact("k", "K", false, 100, null, "e")])),
            "A build without its commit is rejected.");
        Assert(Rejects(Doc([Removal() with { WikiKey = "bad\nkey" }])), "Control characters are rejected.");

        try
        {
            Rpcs3KnowledgeCatalog.Parse(Encoding.UTF8.GetBytes("{ not json"));
            Assert(false, "Corrupt JSON must be rejected.");
        }
        catch (Rpcs3KnowledgeRejectedException)
        {
        }

        try
        {
            Rpcs3KnowledgeCatalog.Parse(new byte[Rpcs3KnowledgeCatalog.MaxBytes + 1]);
            Assert(false, "An oversized file must be rejected.");
        }
        catch (Rpcs3KnowledgeRejectedException)
        {
        }
    }

    private static void AssertLookupIsExactAndInformOnly()
    {
        var catalog = Rpcs3KnowledgeCatalog.Load(ShippedPath);
        var known = catalog.ForPage("Killzone 2");
        Assert(known.ThreadScaling.Count > 0, "Killzone 2 has attached thread-scaling evidence.");
        Assert(!catalog.ForPage("killzone 2").HasAny && !catalog.ForPage("Killzone").HasAny && !catalog.ForPage(" Killzone 2 ").HasAny &&
               !catalog.ForPage(null).HasAny && !catalog.ForPage("").HasAny,
            "Lookup is exact: no case folding, prefix or trimmed match.");
        Assert(!catalog.ForPage("Metal Gear Solid 4").HasAny,
            "A workbook game that is not an exact page title is never attached.");

        var withRemoval = catalog.Document.HistoricalRemovals[0].Page;
        var all = new[] { known, catalog.ForPage(withRemoval), catalog.ForPage("Lollipop Chainsaw") };
        Assert(all.SelectMany(k => k.Removals.Select(r => r.Decision)
                       .Concat(k.Lifecycles.Select(l => l.Decision))
                       .Concat(k.ThreadScaling.Select(s => s.Decision)))
                   .All(d => d.Action is SettingAction.Inform or SettingAction.Suggest && d.Action != SettingAction.Apply),
            "Knowledge is display evidence: never Apply.");
        Assert(known.ThreadScaling.All(s => s.Decision.Kind == EvidenceKind.HardwareData && s.Decision.Authority == EvidenceAuthority.Community &&
                                            s.Decision.Action == SettingAction.Inform),
            "Community CPU evidence is HardwareData/Community and only Inform.");
        var removals = catalog.ForPage(withRemoval).Removals;
        Assert(removals.Count > 0 && removals.All(r => r.Decision.Kind == EvidenceKind.HistoricalWiki &&
                                                       r.Decision.Authority == EvidenceAuthority.Wiki &&
                                                       r.Decision.Action == SettingAction.Inform),
            "Historical Wiki removals are HistoricalWiki/Wiki and Inform (a historical claim proves no build).");

        var tsxLifecycle = catalog.ForPage("Lollipop Chainsaw").Lifecycles;
        Assert(tsxLifecycle.Count == 1 &&
               tsxLifecycle[0].Fact.WikiKey == "tsx instructions" &&
               tsxLifecycle[0].Fact.LastChangeBuild == 18350 &&
               tsxLifecycle[0].Fact.LastChangeCommit == "5893851029134f303b933a6b4b42fc71bfbd0ce6" &&
               tsxLifecycle[0].Decision.Kind == EvidenceKind.Rpcs3Lifecycle &&
               tsxLifecycle[0].Decision.Authority == EvidenceAuthority.Official &&
               tsxLifecycle[0].Decision.Action == SettingAction.Inform,
            "A page removal may join the same exact WikiKey to independently proven RPCS3 lifecycle evidence, Inform-only.");
        Assert(catalog.ForPage("Gran Turismo 5 Prologue").Lifecycles.Count == 0,
            "A Wiki PR claim without an independently proven lifecycle row remains claim-only.");

        var synthetic = Doc(
            [Removal()],
            settings: [new SettingLifecycleFact("renderer", "Renderer", false, 123, new string('b', 40), "LAST_TOUCH_IS_REMOVAL_COMMIT")]);
        Rpcs3KnowledgeCatalog.Validate(synthetic);
        var syntheticLifecycle = new Rpcs3KnowledgeCatalog(synthetic).ForPage("Game A").Lifecycles;
        Assert(syntheticLifecycle.Count == 1 && syntheticLifecycle[0].Fact.WikiKey == "renderer",
            "Lifecycle attachment requires the exact historical removal key on the exact page.");
        var unrelated = Doc(
            [Removal()],
            settings: [new SettingLifecycleFact("other key", "Other", false, 124, new string('c', 40), "LAST_TOUCH_IS_REMOVAL_COMMIT")]);
        Rpcs3KnowledgeCatalog.Validate(unrelated);
        Assert(new Rpcs3KnowledgeCatalog(unrelated).ForPage("Game A").Lifecycles.Count == 0,
            "A global lifecycle row is never attached to a game without an exact page removal key relation.");

        Assert(Enum.GetNames<EvidenceAuthority>().SequenceEqual(["Community", "Wiki", "Developer", "Official"]) &&
               !Enum.GetNames<EvidenceKind>().Contains("Regression") && Enum.GetNames<EvidenceKind>().Contains("RegressionRecord"),
            "Authority is provenance only and RegressionRecord is a kind, not an authority.");
    }

    private static void AssertLoaderIsOptionalAndFailClosed(string tempRoot)
    {
        var useCase = new WikiEvidenceUseCase();
        var missing = Path.Combine(tempRoot, "knowledge-missing");
        Directory.CreateDirectory(missing);
        var none = useCase.LoadKnowledge(missing);
        Assert(none.Error is null && none.Catalog.IsEmpty && !none.Catalog.ForPage("Killzone 2").HasAny,
            "A missing knowledge file is the normal no-knowledge state.");

        var corrupt = Path.Combine(tempRoot, "knowledge-corrupt");
        Directory.CreateDirectory(Path.Combine(corrupt, "Data"));
        File.WriteAllText(Path.Combine(corrupt, "Data", Rpcs3KnowledgeCatalog.FileName), "{ not json");
        var bad = useCase.LoadKnowledge(corrupt);
        Assert(bad.Error is not null && bad.Catalog.IsEmpty, "A corrupt file loads as empty with a stated reason, never partially.");

        var valid = Doc([Removal()], [Scaling("Game A")]);
        var malformed = new[]
        {
            valid with { Schema = Rpcs3KnowledgeCatalog.SchemaVersion + 1 },
            valid with { Sources = null! },
            valid with { Settings = null! },
            valid with { HistoricalRemovals = null! },
            valid with { ThreadScaling = null! },
            valid with { CpuTiers = null! },
            valid with { Sources = [null!] },
            valid with { Settings = [null!] },
            valid with { HistoricalRemovals = [Removal(), null!] },
            valid with { ThreadScaling = [Scaling("Game A"), null!] },
            valid with { CpuTiers = [null!] }
        };
        foreach (var document in malformed)
        {
            File.WriteAllText(Path.Combine(corrupt, "Data", Rpcs3KnowledgeCatalog.FileName),
                JsonSerializer.Serialize(document, Rpcs3KnowledgeCatalog.Options));
            var rejected = useCase.LoadKnowledge(corrupt);
            Assert(rejected.Error is not null && rejected.Catalog.IsEmpty &&
                   !rejected.Catalog.ForPage("Game A").HasAny,
                "Unsupported schema, missing lists and null entries must reject the whole file through the production loader.");
        }

        var good = useCase.LoadKnowledge(AppContext.BaseDirectory);
        Assert(good.Error is null && !good.Catalog.IsEmpty, "The shipped file loads through the production use case.");
    }

    private static void AssertProjectionCarriesKnowledge()
    {
        var useCase = new WikiEvidenceUseCase();
        var loaded = useCase.LoadKnowledge(AppContext.BaseDirectory);
        Assert(loaded.Error is null && !loaded.Catalog.IsEmpty, "Production loads the shipped knowledge catalog.");
        var catalog = loaded.Catalog;
        var wiki = useCase.Load(AppContext.BaseDirectory);
        Assert(wiki.Error is null && !wiki.Catalog.IsEmpty, "Production loads the shipped Wiki page evidence.");
        const string serial = "BCUS98116";
        var recommendationLoad = new AppFlow().LoadCat(AppContext.BaseDirectory);
        if (recommendationLoad.Error is not null ||
            recommendationLoad.Cat?.Find(serial) is not
                { WikiName: "Killzone 2", Binding: WikiGameIdRole.Primary } primary)
        {
            throw new InvalidOperationException(
                "The shipped recommendation catalog must provide the canonical Primary Killzone 2 binding.");
        }

        var projected = useCase.Project(null,
            new Rpcs3GameDescriptor(serial, "Killzone 2", string.Empty, null, null, null),
            primary,
            wiki.Catalog, null, knowledge: catalog);
        Assert(projected.GameKnowledge.ThreadScaling.Count > 0 &&
               projected.GameKnowledge.ThreadScaling.All(item => item.Decision.Action == SettingAction.Inform) &&
               projected.Sections.Count == 0,
            "The production use case consumes shipped game knowledge as information without creating settings.");

        var evidence = new WikiPageEvidence("Killzone 2", [serial], [], []);
        var context = new GameSettingsContext(serial);

        Assert(catalog.ForGame(context.Serial, primary).ThreadScaling.Count > 0,
            "Exact Primary recommendation ownership attaches page-keyed knowledge.");
        Assert(!catalog.ForGame(context.Serial, null).HasAny &&
               !catalog.ForGame(context.Serial, primary with { Binding = WikiGameIdRole.RelatedEdition }).HasAny &&
               !catalog.ForGame(context.Serial, primary with { Serial = "BLUS99999" }).HasAny &&
               !catalog.ForGame(context.Serial, primary with { WikiName = "killzone 2" }).HasAny,
            "Missing, inherited, serial-mismatched, or non-exact page ownership stays unattached.");

        var killzone3 = recommendationLoad.Cat?.Find("BCUS98234");
        Assert(killzone3 is { WikiName: "Killzone 3", Binding: WikiGameIdRole.Primary } &&
               !string.Equals(killzone3.Serial, context.Serial, StringComparison.OrdinalIgnoreCase),
            "A different canonical Wiki page remains bound to its own catalog serial.");

        Assert(!GameSettingsProjector.Project(null, evidence, context).GameKnowledge.HasAny,
            "The projector does not invent cross-catalog knowledge ownership.");
    }

    private static void AssertViewModelLines()
    {
        var catalog = Rpcs3KnowledgeCatalog.Load(ShippedPath);
        var original = UiText.CurLang;
        try
        {
            foreach (var lang in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(lang);
                const string page = "Lollipop Chainsaw";
                var model = GameSettingsProjector.Project(
                    null,
                    new WikiPageEvidence(page, ["BLUS00000"], [], []),
                    new GameSettingsContext("BLUS00000")) with
                {
                    Knowledge = catalog.ForPage(page)
                };
                var vm = new GameEvidenceVm();
                vm.Load(model);
                Assert(vm.HasKnowledge && vm.HasAny && vm.KnowledgeLines.Count is > 0 and <= 8,
                    "A page with knowledge shows bounded informational lines.");
                Assert(vm.KnowledgeLines.All(line => !line.Contains('{', StringComparison.Ordinal) && !line.Contains("Knowledge.", StringComparison.Ordinal) &&
                                                     !line.Any(c => c is >= '٠' and <= '٩')),
                    "Lines are fully formatted, keys resolve, and digits are Western.");
                Assert(!vm.KnowledgeLines.Any(line => line.Contains("Apply", StringComparison.OrdinalIgnoreCase)),
                    "Knowledge lines never offer an action.");
                Assert(vm.KnowledgeLines.Any(line => line.Contains("18350", StringComparison.Ordinal) &&
                                                    line.Contains("58938510", StringComparison.Ordinal)),
                    "A proven setting lifecycle boundary is rendered as information with its build and commit.");

                vm.Load(GameSettingsProjector.Project(null, null, new GameSettingsContext("BLUS00000")));
                Assert(!vm.HasKnowledge && !vm.HasAny, "No page, no knowledge lines.");
            }
        }
        finally
        {
            UiText.SetLang(original);
        }
    }

    private static void AssertGeneratorDeterminism(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "knowledge-generator");
        Directory.CreateDirectory(root);
        var removals = Path.Combine(root, "removals.csv");
        var lifecycle = Path.Combine(root, "lifecycle.csv");
        var cat = Path.Combine(root, "cat.json");
        var notes = Path.Combine(root, "wiki-notes.json");
        File.WriteAllText(removals,
            "RemovalReason,Game,CanonicalFieldKey,Basis,WikiObservedUntil,EditComment\n" +
            "ClaimedFixed,Game A,renderer,EDIT_COMMENT_CLAIM,2020-01-01T00:00:00Z,fixed\n");
        File.WriteAllText(lifecycle,
            "WikiKey,ConfigDisplayName,DeclaredAtFrozenHead,LastChangeBuild,LastChangeCommit,Evidence\n" +
            "renderer,Renderer,True,,,No proven build boundary\n");
        File.WriteAllText(Path.Combine(root, "05-Thread-Scaling.csv"),
            "Game,FpsTarget,CoreThreadConfig,Grade,SourceLink,CellNote\n" +
            "Game A,60.0,8C/8T,GREAT,https://example.org/a,\n" +
            "Game,30,4C/8T,OK,,\n");
        const string tierHeader = "List,Block,Tier,CPU\n";
        File.WriteAllText(Path.Combine(root, "03-Desktop-Cpu-Tiers.csv"), tierHeader + "Desktop,Block A,A,Test CPU\n");
        File.WriteAllText(Path.Combine(root, "04-Mobile-Cpu-Tiers.csv"), tierHeader + "Mobile,Block B,B,Test Mobile CPU\n");
        File.WriteAllText(cat, "{\"Recommendations\":[{\"WikiTitle\":\"Game A\"}]}");
        File.WriteAllText(notes, "{\"Pages\":[]}");

        var first = Path.Combine(root, "first.json");
        var second = Path.Combine(root, "second.json");
        Rpcs3KnowledgeGen.Run(removals, lifecycle, root, cat, notes, first);
        Rpcs3KnowledgeGen.Run(removals, lifecycle, root, cat, notes, second);
        Assert(File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second)),
            "Two generator runs over identical fixture inputs must produce byte-identical files.");
        var generated = Rpcs3KnowledgeCatalog.Load(first).Document;
        Assert(generated.CpuTiers.Select(fact => fact.Tier).SequenceEqual(["A", "B"]) &&
               generated.ThreadScaling.Single(fact => fact.Game == "Game").WikiPage is null &&
               generated.ThreadScaling.Single(fact => fact.Game == "Game A").WikiPage == "Game A" &&
               generated.HistoricalRemovals.Single().Reason == RemovalReason.ClaimedFixed &&
               generated.Settings.Single().LastChangeBuild is null,
            "Generated tiers retain Tier, title matching stays exact, and Wiki timestamps/claims never prove a build or upstream fix.");
    }
}
