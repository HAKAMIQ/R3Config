using System;
using System.IO;
using System.Linq;
using System.Text;
using R3Config;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;
using R3Config.Vm;

internal static class CpuEvidenceRuntimeT
{
    private const string Serial = "BCUS98116";
    private const string Page = "Killzone 2";
    private static readonly AppFlow Flow = new();
    private static Rpcs3KnowledgeCatalog Shipped => Flow.LoadKnowledge(AppContext.BaseDirectory).Catalog;
    private static WikiEvidenceCatalog Wiki => Flow.LoadWikiEvidence(AppContext.BaseDirectory).Catalog;
    private static Rpcs3GameDescriptor Game => new(Serial, Page, string.Empty, null, null, null);
    private static RecSvc RecommendationCatalog => Flow.LoadCat(AppContext.BaseDirectory).Cat ??
        throw new InvalidOperationException("Shipped recommendation catalog failed to load.");
    private static WikiRec Recommendation => RecommendationCatalog.Find(Serial) ??
        throw new InvalidOperationException($"Shipped recommendation catalog is missing {Serial}.");

    public static void Run(TestRunner runner, string root)
    {
        runner.RunCase(nameof(CommunityNeverApply), CommunityNeverApply);
        runner.RunCase(nameof(CpuTiersStayDataOnly), CpuTiersStayDataOnly);
        runner.RunCase(nameof(ExactGameAttachmentOnly), ExactGameAttachmentOnly);
        runner.RunCase(nameof(ThreadScalingReachesPresentation), ThreadScalingReachesPresentation);
        runner.RunCase(nameof(NoTierGatingOrAutomaticSettings), () => NoTierGatingOrAutomaticSettings(root));
        runner.RunCase(nameof(NormalizedKnowledgeOnly), () => NormalizedKnowledgeOnly(root));
        Console.WriteLine("CPU evidence runtime PASS: 6 groups; Community Apply leaks=0; unmatched attachments=0; generic tier gating=0.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static HwInfo Hardware(string cpu, HwTier tier = HwTier.Unknown) =>
        new(cpu, 16, null, "Unknown GPU", tier, false, string.Empty);

    private static void CommunityNeverApply()
    {
        foreach (var applicability in Enum.GetValues<ApplicabilityLevel>())
        foreach (var mismatch in new[] { false, true })
        foreach (var historical in new[] { false, true })
        foreach (var proven in new[] { false, true })
        foreach (var deterministic in new[] { false, true })
        {
            var action = EvidencePolicy.Decide(new EvidenceAssessment(applicability, mismatch, []),
                EvidenceAuthority.Community, historical, proven, deterministic);
            Assert(action is SettingAction.Inform or SettingAction.Blocked,
                "Community evidence must never become Apply, even with Exact identity and a deterministic value.");
        }

        var catalog = Shipped;
        var threadDecisions = catalog.Document.ThreadScaling
            .Where(fact => fact.WikiPage is not null)
            .SelectMany(fact => catalog.ForPage(fact.WikiPage).ThreadScaling.Select(item => item.Decision))
            .ToArray();
        var tierDecisions = catalog.Document.CpuTiers
            .SelectMany(fact => catalog.ForCpu(fact.Cpu).Select(item => item.Decision))
            .ToArray();

        Assert(threadDecisions.Length > 0 && tierDecisions.Length > 0 &&
               threadDecisions.Concat(tierDecisions).All(decision =>
                   decision.Action == SettingAction.Inform &&
                   decision.Kind == EvidenceKind.HardwareData &&
                   decision.Authority == EvidenceAuthority.Community),
            "Normalized CPU evidence remains Inform/HardwareData/Community.");
    }

    private static void CpuTiersStayDataOnly()
    {
        var catalog = Shipped;
        Assert(catalog.Document.CpuTiers.Count(fact => fact.List == "DESKTOP") == 156 &&
               catalog.Document.CpuTiers.Count(fact => fact.List == "MOBILE_WIP") == 104,
            "The shipped normalized lists preserve the verified Desktop/Mobile counts.");

        var exact = catalog.ForCpu("i5-12600K");
        Assert(exact.Count == 2 &&
               exact.Any(item => item.Fact.Block == "Stock" && item.Fact.Tier == "B") &&
               exact.Any(item => item.Fact.Block == "Overclocked" && item.Fact.Tier == "A"),
            "Exact catalog lookup preserves the workbook's separate operating contexts.");

        foreach (var cpu in new string?[] { null, "", "i5-12600", "I5-12600K", " i5-12600K", "Intel i5-12600K" })
        {
            Assert(catalog.ForCpu(cpu).Count == 0, "Unproven CPU aliases must not select a tier.");
        }

        var conflict = new Rpcs3KnowledgeCatalog(catalog.Document with
        {
            CpuTiers =
            [
                new("DESKTOP", "Stock", "A", "Test CPU"),
                new("DESKTOP", "Stock", "B", "Test CPU")
            ]
        });
        Assert(conflict.ForCpu("Test CPU").Count == 0,
            "Conflicting grades for the same list/context remain unresolved.");

        foreach (var tier in Enum.GetValues<HwTier>())
        {
            var snapshot = Flow.RuntimeContext(Hardware("i5-12600K", tier), null, null, null, null, null);
            Assert(snapshot.Find("CpuCommunityTier") is null,
                "Community CPU tier classifications are not Runtime Context facts or performance recommendations.");
        }
    }

    private static void ExactGameAttachmentOnly()
    {
        var catalog = Shipped;

        GameKnowledge Project(Rpcs3GameDescriptor game, WikiRec? recommendation, WikiEvidenceCatalog? evidence = null) =>
            Flow.GameSettings(null, game, recommendation, evidence ?? Wiki, null, knowledge: catalog).GameKnowledge;

        Assert(Project(Game, Recommendation).ThreadScaling.Count > 0,
            "An exact Primary recommendation may attach page-keyed thread-scaling evidence.");
        Assert(!Project(Game, null).HasAny,
            "A title or sidecar serial without a Primary recommendation cannot attach game knowledge.");
        Assert(Project(Game with { Title = "Untrusted display name" }, Recommendation).ThreadScaling.Count > 0,
            "The display title is not an identity input.");

        foreach (var wikiName in new[] { "killzone 2", "Killzone", " Killzone 2 " })
        {
            Assert(!Project(Game, Recommendation with { WikiName = wikiName }).HasAny,
                "A non-exact canonical page name cannot attach CPU evidence.");
        }

        var killzone3 = RecommendationCatalog.Find("BCUS98234");
        Assert(killzone3 is { WikiName: "Killzone 3", Binding: WikiGameIdRole.Primary } &&
               !string.Equals(killzone3.Serial, Serial, StringComparison.OrdinalIgnoreCase),
            "A different canonical page is owned by its own catalog serial, not by the selected game.");

        Assert(!Project(Game, Recommendation with { Serial = "BLUS99999" }).HasAny &&
               !Project(Game with { Serial = "BLUS99999" }, Recommendation).HasAny &&
               !Project(Game with { Serial = "" }, Recommendation).HasAny,
            "Serial disagreement or absence fails closed.");

        foreach (var role in new[] { WikiGameIdRole.RelatedEdition, WikiGameIdRole.Demo })
        {
            Assert(!Project(Game, Recommendation with { Binding = role }).HasAny,
                "RelatedEdition and Demo bindings cannot attach game CPU evidence.");
        }

        var misleading = WikiEvidenceCatalog.FromPages(
            "test",
            "test",
            new string('a', 64),
            [new WikiPageEvidence(
                Page,
                [Serial],
                [new WikiFreeTextNote(Page, "Special Notes", WikiNoteClass.InformationalNote, "Sidecar-only evidence.", null, null)],
                [])]);
        Assert(!Project(Game, null, misleading).HasAny,
            "Wiki sidecar serial mentions are not a substitute for canonical Primary ownership.");

        var invalid = catalog.Document with
        {
            ThreadScaling = [new("Different game", Page, "60", "8C/8T", 8, 8, "GREAT", null, null)]
        };
        try
        {
            Rpcs3KnowledgeCatalog.Validate(invalid);
            throw new InvalidOperationException("A normalized fact with a forged page association was accepted.");
        }
        catch (Rpcs3KnowledgeRejectedException)
        {
        }
    }

    private static void ThreadScalingReachesPresentation()
    {
        var catalog = Shipped;
        Assert(catalog.Document.ThreadScaling.Count == 43 &&
               catalog.Document.ThreadScaling.Select(fact => fact.Game).Distinct(StringComparer.Ordinal).Count() == 26,
            "All 43 normalized scaling cells for 26 games remain available.");

        var model = Flow.GameSettings(null, Game, Recommendation, Wiki, null, knowledge: catalog);
        var expected = catalog.Document.ThreadScaling.Where(fact => fact.WikiPage == Page).ToArray();
        Assert(expected.Length > 0 &&
               model.GameKnowledge.ThreadScaling.Select(item => item.Fact).SequenceEqual(expected) &&
               model.Sections.Count == 0,
            "The production path carries exact shipped observations without creating settings.");

        var language = UiText.CurLang;
        try
        {
            foreach (var lang in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(lang);
                var vm = new GameEvidenceVm();
                vm.Load(model);
                Assert(vm.HasKnowledge && expected.All(fact => vm.KnowledgeLines.Any(line =>
                        line.Contains(fact.CoreThreadConfig, StringComparison.Ordinal) &&
                        line.Contains(fact.Grade, StringComparison.Ordinal))),
                    "Thread-scaling observations reach GameKnowledge/UI in both languages.");

                vm.Load(Flow.GameSettings(
                    null,
                    Game with { Serial = "BLUS99999" },
                    Recommendation,
                    Wiki,
                    null,
                    knowledge: catalog));
                Assert(!vm.HasKnowledge,
                    "Changing to a serial that does not match the Primary recommendation clears CPU evidence.");

                vm.Load(Flow.GameSettings(null, Game, null, Wiki, null, knowledge: catalog));
                Assert(!vm.HasKnowledge,
                    "Removing canonical Primary ownership clears CPU evidence.");
            }
        }
        finally
        {
            UiText.SetLang(language);
        }

        foreach (var fact in catalog.Document.ThreadScaling.Where(fact => fact.WikiPage is null))
        {
            Assert(catalog.ForPage(fact.Game).ThreadScaling.Count == 0,
                "Unresolved workbook games stay unattached.");
        }
    }

    private static void NoTierGatingOrAutomaticSettings(string root)
    {
        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "cpu-settings"));
        var recommendation = Recommendation with
        {
            Changes =
            [
                new(
                    "Core",
                    "Preferred SPU Threads",
                    "2",
                    "preferred spu threads",
                    "Recommended.",
                    RecRisk.Rec)
            ]
        };
        var catalog = Shipped;
        var planner = new PlanService();
        CfgEntry? baseline = null;

        foreach (var tier in Enum.GetValues<HwTier>())
        foreach (var cpu in new[] { "i5-12600K", "Unmatched CPU" })
        {
            var hardware = Hardware(cpu, tier);
            var plan = planner.BuildCfg(env.Install, Game, recommendation, hardware);
            var without = Flow.GameSettings(plan, Game, recommendation, Wiki, null, hardware);
            var with = Flow.GameSettings(plan, Game, recommendation, Wiki, null, hardware, knowledge: catalog);
            var rows = with.Sections.SelectMany(section => section.Rows).ToArray();

            var entry = plan.Items.Single();
            baseline ??= entry;
            Assert(entry.Change == baseline.Change &&
                   string.Equals(entry.CurVal, baseline.CurVal, StringComparison.Ordinal) &&
                   entry.State == baseline.State &&
                   entry.IsDiff == baseline.IsDiff &&
                   entry.IsAction == baseline.IsAction &&
                   entry.IsSel == baseline.IsSel &&
                   entry.HasCustomOverride == baseline.HasCustomOverride &&
                   rows.Select(row => row.Entry).SequenceEqual(plan.Items) &&
                   rows.SequenceEqual(without.Sections.SelectMany(section => section.Rows)) &&
                   rows.Single().Entry is { Val: "2", IsSel: true } &&
                   with.GameKnowledge.ThreadScaling.Count > 0 &&
                   !without.GameKnowledge.HasAny,
                "HwTier, CPU-name lookup and thread scaling cannot gate, select, rewrite or add a setting.");

            Assert(Flow.GameSettings(
                       null,
                       Game,
                       recommendation,
                       Wiki,
                       null,
                       hardware,
                       knowledge: catalog).Sections.Count == 0,
                "CPU evidence without a configuration plan never generates settings.");
        }
    }

    private static void NormalizedKnowledgeOnly(string root)
    {
        var isolated = Path.Combine(root, "normalized-only");
        var data = Path.Combine(isolated, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "RPCS3 CPU DATA.xlsx"), "not a workbook");
        File.WriteAllText(Path.Combine(data, "03-Desktop-Cpu-Tiers.csv"), "CPU,Tier\ni5-12600K,S");
        File.WriteAllText(Path.Combine(data, "05-Thread-Scaling.csv"), "Game,Grade\nKillzone 2,GREAT");

        Assert(Flow.LoadKnowledge(isolated).Catalog.IsEmpty,
            "Raw CPU files never substitute for the normalized catalog.");

        var path = Path.Combine(data, Rpcs3KnowledgeCatalog.FileName);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", Rpcs3KnowledgeCatalog.FileName), path);
        var loaded = Flow.LoadKnowledge(isolated);
        Assert(loaded.Error is null &&
               loaded.Catalog.Document.CpuTiers.Count == 260 &&
               loaded.Catalog.Document.ThreadScaling.Count == 43 &&
               loaded.Catalog.ForCpu("i5-12600K").All(fact => fact.Fact.Tier != "S"),
            "The production loader consumes compact Knowledge alone and ignores raw files beside it.");

        File.WriteAllText(path, "{invalid", Encoding.UTF8);
        var rejected = Flow.LoadKnowledge(isolated);
        Assert(rejected.Error is not null &&
               rejected.Catalog.IsEmpty &&
               rejected.Catalog.ForCpu("i5-12600K").Count == 0,
            "Invalid normalized data fails closed without falling back to raw CPU files.");

        Assert(!Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Data"))
            .Any(path => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)),
            "Runtime Data ships no raw XLSX or CSV.");
    }
}
