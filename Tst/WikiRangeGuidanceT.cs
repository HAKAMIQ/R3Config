using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// A Wiki page that writes a range instead of one value ("preferred spu threads
// = 1 - 3") is ambiguous: R3Config must not pick a number from it and must not
// apply anything. Before this test the range was dropped entirely, so the user
// never saw what the Wiki actually recommended. It is now carried to the screen
// as the Wiki's own advice: text with no value, no row and no checkbox, while
// the parser keeps rejecting the value for application.
internal static class WikiRangeGuidanceT
{
    private const string Serial = "BLUS31000";
    private const string OutOfBoundsSerial = "BLUS31001";
    private const string ReversedSerial = "BLUS31002";
    private const string WikiKey = "preferred spu threads";
    private const string Section = "Core";
    private const string Key = "Preferred SPU Threads";

    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigWikiRange-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var analysis = Analyze(root);
            GuardStillRejectsTheValue(analysis);
            var recommendation = CatalogCarriesTheRangeAsText(analysis);
            OutOfRangeAndReversedAreNotQuoted(analysis);
            CatalogRoundTripKeepsIt(root, recommendation);
            LoaderRejectsAnUntrustedRange(root);
            var plan = PlanCarriesItWithoutARow(root, recommendation);
            ProjectionCarriesIt(plan);
            ScreenQuotesItInBothLanguages(plan);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Console.WriteLine(
            "Wiki range guidance (value rejected, range quoted, no row, AR + EN) PASS.");
    }

    // Runs inside UiGate, where an Application exists: the guidance card has
    // to be visible in the real Settings view for a shipped serial that has an
    // ambiguous range, with the RPCS3 setting path LTR and no settings row for
    // it. BLUS30852 (Plants vs. Zombies) is one of the 13 shipped serials whose
    // Wiki page writes "preferred spu threads = 1 - 3".
    public static void Render()
    {
        const string shipped = "BLUS30852";
        var original = UiText.CurLang;
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigWikiRangeRender-" + Guid.NewGuid().ToString("N"));
        Window? window = null;
        try
        {
            var catalog = RecSvc.Load(Path.Combine(AppContext.BaseDirectory, "Data", "cat.json"));
            var recommendation = catalog.Find(shipped)
                ?? throw new InvalidOperationException($"The shipped catalog must carry {shipped}.");
            Assert(recommendation.Guidance.Count == 1,
                $"{shipped} must carry exactly one ambiguous Wiki range in the shipped catalog.");

            var env = GameSetupT.CreateEnvironment(Path.Combine(root, "install"));
            var game = new Rpcs3GameDescriptor(shipped, recommendation.WikiName, env.GamePath, null, null, null);
            var plan = new PlanService().BuildCfg(env.Install, game, recommendation);

            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();
                var vm = new PlanVm();
                vm.Load(plan, GameSettingsProjector.Project(plan, null, new GameSettingsContext(shipped)));

                var view = new R3Config.Ctrl.RecView { DataContext = new RenderHost(vm) };
                window = new Window { Content = view, Width = 920, Height = 940 };
                window.Show();
                for (var pass = 0; pass < 4; pass++)
                {
                    window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                    window.UpdateLayout();
                }

                var card = Descendants<FrameworkElement>(view).SingleOrDefault(item =>
                    AutomationProperties.GetAutomationId(item) == "Settings.WikiRangeGuidance")
                    ?? throw new InvalidOperationException($"{language}: the Wiki guidance card was not rendered.");
                Assert(card.IsVisible && card.ActualHeight > 0,
                    $"{language}: the Wiki guidance card must be visible for a serial that has a range.");

                var path = Text(view, "Settings.WikiRangePath");
                var text = Text(view, "Settings.WikiRangeText");
                Assert(path.Text == $"{Section} / {Key}" && path.FlowDirection == FlowDirection.LeftToRight,
                    $"{language}: the RPCS3 setting path must be shown verbatim and LTR (got '{path.Text}' {path.FlowDirection}).");
                Assert(text.Text.Contains('1', StringComparison.Ordinal) &&
                       text.Text.Contains('3', StringComparison.Ordinal),
                    $"{language}: the rendered sentence must carry both ends of the range (got '{text.Text}').");
                Assert(Descendants<TextBlock>(view).All(block =>
                        !(block.Text ?? string.Empty).Contains(Key, StringComparison.Ordinal) ||
                        ReferenceEquals(block, path)),
                    $"{language}: the ambiguous setting must appear only as guidance, never as a settings row.");

                Shot(view, language);
                window.Close();
                window = null;
            }

            Console.WriteLine(
                "Wiki range guidance card render (shipped BLUS30852, LTR setting path, AR + EN) PASS.");
        }
        finally
        {
            window?.Close();
            UiText.SetLang(original);
            UiLaySvc.Apply();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // Same hook and the same WPF visual render the rest of the gate uses
    // (R3_UI_SHOTS), so an RTL capture is not mirrored. No variable, no file.
    private static void Shot(FrameworkElement view, string language)
    {
        var outDir = Environment.GetEnvironmentVariable("R3_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(outDir))
        {
            return;
        }

        Directory.CreateDirectory(outDir);
        var size = new Size(900, Math.Max(360, Math.Ceiling(view.ActualHeight)));
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)size.Width,
            (int)size.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(view);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(
            outDir,
            $"wiki-range-{(language == UiText.ArabLang ? "ar" : "en")}.png"));
        encoder.Save(file);
    }

    private static TextBlock Text(DependencyObject view, string automationId) =>
        Descendants<TextBlock>(view).Single(item =>
            AutomationProperties.GetAutomationId(item) == automationId);

    private static List<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                var child = VisualTreeHelper.GetChild(node, index);
                if (child is T match)
                {
                    found.Add(match);
                }

                Walk(child);
            }
        }
    }

    private sealed class RenderHost(PlanVm plan)
    {
        public PlanVm Plan { get; } = plan;

        public System.Windows.Input.ICommand? DelSet { get; }

        public System.Windows.Input.ICommand? ApplyCmd { get; }
    }

    private static WikiCorpusAnalysis Analyze(string root)
    {
        // Three pages: a readable range beside a real setting, a range outside
        // the RPCS3 limit for the key (0-6), and a reversed one.
        var xml = new StringBuilder()
            .Append("<mediawiki>")
            .Append("<page><title>Range Guidance</title><ns>0</ns><revision><id>1</id>")
            .Append("<timestamp>2026-06-12T00:00:00Z</timestamp><text><![CDATA[")
            .Append("{{gameid|id=").Append(Serial).Append("}}")
            .Append("{{config|preferred spu threads=1 - 3")
            .Append("|preferred spu threads notes=May improve performance on weaker CPUs.")
            .Append("|ppu decoder=Recompiler (LLVM)}}")
            .Append("]]></text></revision></page>")
            .Append("<page><title>Range Out Of Bounds</title><ns>0</ns><revision><id>2</id>")
            .Append("<timestamp>2026-06-12T00:00:00Z</timestamp><text><![CDATA[")
            .Append("{{gameid|id=").Append(OutOfBoundsSerial).Append("}}")
            .Append("{{config|preferred spu threads=5 - 9}}")
            .Append("]]></text></revision></page>")
            .Append("<page><title>Range Reversed</title><ns>0</ns><revision><id>3</id>")
            .Append("<timestamp>2026-06-12T00:00:00Z</timestamp><text><![CDATA[")
            .Append("{{gameid|id=").Append(ReversedSerial).Append("}}")
            .Append("{{config|preferred spu threads=3 - 1}}")
            .Append("]]></text></revision></page>")
            .Append("</mediawiki>")
            .ToString();

        var path = Path.Combine(root, "wiki-range-guidance.xml");
        File.WriteAllText(path, xml, new UTF8Encoding(false));
        return WikiSvc.Analyze(path);
    }

    // The guard that refuses to serialize a range must not move.
    private static void GuardStillRejectsTheValue(WikiCorpusAnalysis analysis)
    {
        var entry = analysis.Recommendations.Single(item =>
            item.Page == "Range Guidance" && item.WikiKey == WikiKey);

        Assert(entry.ParserStatus == WikiParserStatus.Ambiguous &&
               entry.Classification == WikiRecommendationClass.Ambiguous &&
               !entry.IncludedInCatalog &&
               entry.MappedSettings.Count == 0,
            "A Wiki thread-count range must stay ambiguous, map to nothing and never enter the catalog as a setting.");
    }

    private static WikiRec CatalogCarriesTheRangeAsText(WikiCorpusAnalysis analysis)
    {
        var recommendation = analysis.CatalogRecommendations.Single(item => item.Serial == Serial);

        Assert(recommendation.Changes.All(change =>
                   !change.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)),
            $"'{Section} / {Key}' must have no change row: a range names no value to write.");
        Assert(recommendation.Changes.Any(change => change.WikiKey == "ppu decoder"),
            "The unambiguous settings on the same page must still be recommended.");

        var hint = recommendation.Guidance.SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"The catalog carries no Wiki guidance for {Serial}; the range '1 - 3' was dropped instead of quoted.");

        Assert(hint.Section == Section &&
               hint.Key == Key &&
               hint.WikiKey == WikiKey &&
               hint.Min == 1 &&
               hint.Max == 3 &&
               hint.RawValue == "1 - 3",
            $"The guidance must name the RPCS3 setting and both ends of the Wiki range (got {hint.Path}={hint.RawValue} {hint.Min}-{hint.Max}).");
        Assert(hint.Notes is not null &&
               hint.Notes.Contains("weaker CPUs", StringComparison.Ordinal),
            "The Wiki's own note for the ambiguous key must travel with its range.");

        return recommendation;
    }

    // R3Config must never invent the bounds: a range outside the one RPCS3
    // limit for the key, or written backwards, is quoted to nobody.
    private static void OutOfRangeAndReversedAreNotQuoted(WikiCorpusAnalysis analysis)
    {
        foreach (var serial in new[] { OutOfBoundsSerial, ReversedSerial })
        {
            var recommendation = analysis.CatalogRecommendations
                .SingleOrDefault(item => item.Serial == serial);
            Assert(recommendation is null || recommendation.Guidance.Count == 0,
                $"{serial}: an unreadable Wiki range must not be quoted as guidance.");
        }
    }

    private static void CatalogRoundTripKeepsIt(string root, WikiRec recommendation)
    {
        var path = Path.Combine(root, "cat.json");
        RecSvc.Save(path, "2026-06-12", [recommendation]);
        var reloaded = RecSvc.Load(path).Find(Serial)
            ?? throw new InvalidOperationException($"{Serial} is missing from the written catalog.");
        var hint = reloaded.Guidance.SingleOrDefault()
            ?? throw new InvalidOperationException(
                "The written catalog lost the Wiki guidance, so a shipped catalog could never show it.");

        Assert(hint == recommendation.Guidance[0],
            "The Wiki guidance must survive a catalog write and read unchanged.");

        // A serial with no ambiguous guidance keeps the bytes it had: the field
        // is written only where there is something to quote.
        var plainPath = Path.Combine(root, "cat-plain.json");
        RecSvc.Save(plainPath, "2026-06-12", [new WikiRec("BLUS31009", "Plain", [])]);
        Assert(!File.ReadAllText(plainPath).Contains("Hints", StringComparison.Ordinal),
            "A catalog entry without guidance must not gain a Hints field.");
    }

    // A catalog does not have to be one R3Config generated: the published data
    // bundle is deserialized by this same loader
    // (GitHubRecDataSource.ParseRecommendations hands it to RecSvc.Load). So a
    // hint that names a setting R3Config does not map, or bounds outside the one
    // RPCS3 limit for that key, must be refused at load instead of reaching the
    // screen as a range no RPCS3 version stands behind.
    private static void LoaderRejectsAnUntrustedRange(string root)
    {
        // 0 - 6 is the real limit for preferred spu threads, so 0 - 9 is outside
        // it while still parsing as an ordered, distinct pair of whole numbers.
        AssertLoadRejects(
            root,
            "out-of-limit",
            new CfgHint(Section, Key, WikiKey, "0 - 9", 0, 9),
            "A range outside the RPCS3 limit for its key must be refused at load.");

        // A well-formed range whose Wiki key R3Config maps to no setting at all.
        AssertLoadRejects(
            root,
            "unmapped-key",
            new CfgHint(Section, Key, "invented wiki key", "1 - 3", 1, 3),
            "A range whose Wiki key maps to no R3Config setting must be refused at load.");

        // The right bounds and the right key, pinned to a different setting.
        AssertLoadRejects(
            root,
            "mismatched-target",
            new CfgHint("Video", "Resolution Scale", WikiKey, "1 - 3", 1, 3),
            "A range pinned to a setting its Wiki key does not map to must be refused at load.");
    }

    // RecSvc.Save normalizes through the same guard, so the rejected catalog is
    // written as raw JSON to prove the loader itself refuses it.
    private static void AssertLoadRejects(
        string root,
        string name,
        CfgHint hint,
        string because)
    {
        var path = Path.Combine(root, $"cat-{name}.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    DataSource = "test",
                    DataVersion = "2026-06-12",
                    Recommendations = new[]
                    {
                        new
                        {
                            Serial,
                            WikiTitle = "Untrusted",
                            Changes = Array.Empty<CfgChg>(),
                            Hints = new[] { hint }
                        }
                    }
                }));

        var rejected = false;
        try
        {
            RecSvc.Load(path);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        Assert(rejected, because);
    }

    private static CfgPlan PlanCarriesItWithoutARow(string root, WikiRec recommendation)
    {
        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "install"));
        var game = new Rpcs3GameDescriptor(Serial, "Range Guidance", env.GamePath, null, null, null);
        var plan = new PlanService().BuildCfg(env.Install, game, recommendation);

        Assert(plan.Items.All(item =>
                   !item.Change.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)),
            "The plan must hold no row for a setting the Wiki only gave a range for.");
        Assert(plan.Hints.Count == 1 && plan.Hints[0].Min == 1 && plan.Hints[0].Max == 3,
            "The plan must carry the Wiki range as evidence beside its rows.");
        Assert(plan.RecState.Items.All(item => !item.Path.Contains(Key, StringComparison.OrdinalIgnoreCase)),
            "Nothing in the change state may be derived from a range.");

        return plan;
    }

    private static void ProjectionCarriesIt(CfgPlan plan)
    {
        var model = GameSettingsProjector.Project(
            plan,
            null,
            new GameSettingsContext(Serial));

        Assert(model.Guidance.Count == 1 &&
               model.Guidance[0].Key == Key &&
               model.Sections.All(section =>
                   section.Rows.All(row =>
                       !row.Entry.Change.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))),
            "The Game Settings projection must show the range as guidance and never as a settings row.");
    }

    private static void ScreenQuotesItInBothLanguages(CfgPlan plan)
    {
        var original = UiText.CurLang;
        try
        {
            string[] texts = [];
            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                var vm = new PlanVm();
                vm.Load(plan);

                Assert(vm.HasWikiRange && vm.WikiRanges.Count == 1,
                    $"{language}: the Settings screen must show the Wiki range.");
                var line = vm.WikiRanges[0];
                Assert(line.Path == $"{Section} / {Key}",
                    $"{language}: the guidance must name the RPCS3 setting verbatim (got '{line.Path}').");
                Assert(line.Text.Contains('1', StringComparison.Ordinal) &&
                       line.Text.Contains('3', StringComparison.Ordinal) &&
                       !line.Text.Contains("Plan.WikiRange", StringComparison.Ordinal),
                    $"{language}: the guidance must be a localized sentence carrying both ends (got '{line.Text}').");
                Assert(vm.WikiRangeTitle.Length > 0 &&
                       !vm.WikiRangeTitle.Contains("Plan.WikiRange", StringComparison.Ordinal),
                    $"{language}: the guidance card needs a localized title.");
                Assert(vm.Items.All(item => !item.IsSel),
                    $"{language}: a range must never select anything.");
                texts = [.. texts, line.Text];
            }

            Assert(texts[0] != texts[1],
                "The English and Arabic guidance sentences must differ.");
            Assert(texts[1].Contains("بين", StringComparison.Ordinal),
                $"The Arabic guidance must read as the Wiki recommending a value between the two ends (got '{texts[1]}').");
        }
        finally
        {
            UiText.SetLang(original);
        }
    }
}
