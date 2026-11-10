using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using R3Config;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;
using R3Config.Vm;

// Consumer/UI-model regression for the Wiki -> Game Settings wiring. It drives
// the real chain (sidecar file -> AppFlow/use case -> GameSettingsProjector ->
// PlanVm/GameEvidenceVm) with the real Minecraft Wiki page and the shipped
// cat.json settings, and asserts the exact model the Settings view binds to.
internal static class GameSettingsUiT
{
    private const string Serial = "BLES01976";

    // Synthetic official-shaped URL for the tested build 0.0.30-16004; the
    // shipped catalog is verified separately and must not be assumed to hold it.
    internal const string TestBuildUrl =
        "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-0123456789abcdef0123456789abcdef01234567/rpcs3-v0.0.30-16004-01234567_win64_msvc.7z";

    // The real frame-limiter note now has a reviewed Arabic translation. These UI
    // tests need one note without it, to prove the "translation unavailable" path,
    // so only that sentence is reworded; the rest of the page stays the real text.
    private static readonly string UnreviewedMinecraftPage =
        WikiNotesParserT.MinecraftPage.Replace(
            "while the game is running,",
            "while the game is running (fixture wording without a reviewed translation),",
            StringComparison.Ordinal);

    internal static Rpcs3BuildCatalog TestBuilds { get; } =
        new([new Rpcs3BuildEntry("0.0.30", 16004, "Windows", TestBuildUrl)]);

    public static void Run()
    {
        var originalLanguage = UiText.CurLang;
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3GameSettingsUiT-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            var flow = new AppFlow(AppComposition.Build());

            var parsed = WikiNotesParser.Parse(
                "Minecraft",
                ["BLES01976", "BLUS31426", "NPEB01899", "NPUB31419", "NPJB00549"],
                UnreviewedMinecraftPage);
            WikiEvidenceCatalog
                .FromPages("RPCS3 Wiki snapshot", "2026-09-07", "test", [parsed])
                .Save(Path.Combine(root, "Data", WikiEvidenceCatalog.FileName));

            var loaded = flow.LoadWikiEvidence(root);
            Assert(loaded.Error is null && !loaded.Catalog.IsEmpty,
                "The Wiki notes sidecar must load through the AppFlow/use-case consumer path.");

            var catalog = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json"));
            var rec = catalog.Find(Serial)
                ?? throw new InvalidOperationException("Shipped catalog must contain Minecraft BLES01976.");
            var game = new Rpcs3GameDescriptor(Serial, rec.WikiName, string.Empty, "1.00", null, null);
            var plan = PlanOf(Serial, rec.WikiName, rec.Changes);
            var model = flow.GameSettings(plan, game, rec, loaded.Catalog, null);
            var game184 = game with { Version = "1.84" };
            var model184 = flow.GameSettings(plan, game184, rec, loaded.Catalog, null);

            var model184Builds = flow.GameSettings(plan, game184, rec, loaded.Catalog, null, null, TestBuilds);
            Assert(model184Builds.ExactBuildUrl("0.0.30-16004") == TestBuildUrl &&
                   model184.ExactBuildUrl("0.0.30-16004") is null &&
                   flow.GameSettings(plan, game, rec, loaded.Catalog, null, null, TestBuilds)
                       .ExactBuildUrl("0.0.30-16004") is null,
                "The exact build resolves only for applicable (installed 1.84) RPCN evidence and only from a catalog row; the 1.00 install exposes nothing.");

            AssertSettingsModel(model, plan);
            AssertNotesModel(model);
            AssertOnlineModel(model, model184);
            AssertViewModel(plan, model184);
            AssertScopeAndAbsence(flow, loaded.Catalog, game184, rec, plan);
            AssertApplicabilityFiltering(flow, plan, rec, loaded.Catalog);
            AssertLinks();
            AssertSidecarFailureHandling(flow, root);
            AssertCatalogSectionCoverage(catalog);
            AssertWikiSettingsMapping(root);
            AssertShippedData(flow);

            Console.WriteLine("Game Settings UI wiring (Minecraft end-to-end: settings sections, Wiki notes, online/RPCN) PASS.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // Renders the real Settings view (Ctrl/Rec.xaml) against the Minecraft
    // model in both languages, so XAML resource/binding mistakes and the
    // technical-LTR / Western-digit rules are checked on the visual tree.
    // WPF allows one Application per process, so UiGate.RunUi (which owns the
    // App for the visual gates) calls this on its STA thread.
    internal static void RenderSettingsView()
    {
        var originalLanguage = UiText.CurLang;
        var listener = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        Window? window = null;

        try
        {
            var catalog = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json"));
            var rec = catalog.Find(Serial)!;
            var plan = PlanOf(Serial, rec.WikiName, rec.Changes);
            var evidence = WikiNotesParser.Parse(
                "Minecraft",
                ["BLES01976", "BLUS31426"],
                UnreviewedMinecraftPage);
            var model = GameSettingsProjector.Project(
                plan,
                evidence,
                new GameSettingsContext(Serial, "1.84"),
                TestBuilds);

            UiText.SetLang(UiText.EnLang);
            UiLaySvc.Apply();
            var vm = new PlanVm();
            vm.Load(plan, model);
            var view = new R3Config.Ctrl.RecView
            {
                DataContext = new RecHost(vm),
                Width = 900,
                Height = 900
            };
            window = new Window { Content = view, Width = 920, Height = 940 };
            window.Show();
            Settle(window);

            var english = Texts(view);
            foreach (var expected in new[]
                     {
                         "Anisotropic Filter Override", "Keyboard", "Basic",
                         "Wiki notes and warnings", "Online / RPCN",
                         "RPCN play", "Tested game version", "1.84",
                         "RPCS3 tested build", "0.0.30-16004", "Download build",
                         "Not required", "Workaround", "Warning", "Details"
                     })
            {
                Assert(english.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"English Settings view must render '{expected}'. Rendered: " + string.Join(" / ", english));
            }

            // Compact rows: no Setting / Recommended value / Role column titles.
            foreach (var header in new[] { "Setting", "Recommended value", "Role", "Enable / Disable", "Optional", "Required", "Conditional" })
            {
                Assert(!english.Contains(header),
                    $"The Settings list must not show the column title or role label '{header}'.");
            }

            // One clean list: no RPCS3 tab headers, and nothing scoped to another
            // platform, version, or edition (no "not applicable" card either).
            foreach (var tab in new[] { "CPU", "GPU", "Audio", "I/O", "System", "Network", "Advanced", "Emulator", "GUI", "Debug" })
            {
                Assert(!english.Contains(tab),
                    $"The Settings list must not show the RPCS3 tab header '{tab}'.");
            }

            Assert(!english.Any(text =>
                       text.Contains("MacOS", StringComparison.Ordinal) ||
                       text.Contains("pitched", StringComparison.Ordinal) ||
                       text.Contains("Other platform", StringComparison.Ordinal) ||
                       text.Contains("not applicable", StringComparison.OrdinalIgnoreCase)),
                "Notes scoped to macOS or to another game version/edition must not be rendered for a Windows disc install at 1.84.");
            Assert(!english.Any(text => text.Contains("Required game version", StringComparison.Ordinal)),
                "The Settings view must not render a Required game version for a tested-only netplay table.");
            Assert(english.Count(text => text == "Keyboard") == 1,
                "Keyboard must render exactly once (no duplicate tab entry).");

            // Actions first: once a setting is selected, Apply sits above the
            // settings list, which sits above notes, which sit above RPCN.
            vm.Rows[0].IsSel = true;
            Settle(window);
            var apply = All<Button>(view).Single(button =>
                button.Content as string == UiText.GetText("Plan.ApplySection"));
            var notesCard = All<Border>(view).Single(item => AutomationProperties.GetAutomationId(item) == "Settings.Notes");
            var onlineCard = All<Border>(view).Single(item => AutomationProperties.GetAutomationId(item) == "Settings.Online");
            var firstRow = Blocks(view).First(block => Plain(block) == "Anisotropic Filter Override");
            var applyY = apply.TranslatePoint(new Point(0, 0), view).Y;
            var rowY = firstRow.TranslatePoint(new Point(0, 0), view).Y;
            var notesY = notesCard.TranslatePoint(new Point(0, 0), view).Y;
            var onlineY = onlineCard.TranslatePoint(new Point(0, 0), view).Y;
            Assert(apply.ActualHeight > 0 && applyY < rowY && rowY < notesY && notesY < onlineY,
                $"Order must be actions ({applyY}), settings ({rowY}), notes ({notesY}), RPCN ({onlineY}).");

            // Restore Defaults closes the area: below settings, notes, and RPCN,
            // and it exists exactly once.
            var restoreVm = new PlanVm();
            var restorablePlan = plan with
            {
                Items = [.. plan.Items.Select(item => item with { CurVal = "custom", HasCustomOverride = true })]
            };
            restoreVm.Load(restorablePlan, model);
            view.DataContext = new RecHost(restoreVm);
            Settle(window);
            var restoreLabel = UiText.GetText("Plan.RemoveWikiSettings");
            var restoreButtons = All<Button>(view)
                .Where(button => button.Content as string == restoreLabel && button.IsVisible)
                .ToList();
            var lastRowY = Blocks(view).Last(block => Plain(block) == "Keyboard")
                .TranslatePoint(new Point(0, 0), view).Y;
            var restoreOnlineY = All<Border>(view)
                .Single(item => AutomationProperties.GetAutomationId(item) == "Settings.Online")
                .TranslatePoint(new Point(0, 0), view).Y;
            Assert(restoreButtons.Count == 1 &&
                   restoreButtons[0].TranslatePoint(new Point(0, 0), view).Y > restoreOnlineY &&
                   restoreOnlineY > lastRowY,
                "Restore Defaults must be a single button below the settings list and the RPCN block.");
            view.DataContext = new RecHost(vm);
            Settle(window);

            // The RPCN yes/no matrix and workaround text stay behind details.
            var rpcnDetails = All<Expander>(view).Single(expander =>
                (expander.Header as TextBlock)?.Text == UiText.GetText("Online.Details"));
            Assert(!rpcnDetails.IsExpanded,
                "RPCN secondary fields must sit behind a collapsed details toggle.");
            rpcnDetails.IsExpanded = true;
            Settle(window);
            english = Texts(view);
            Assert(english.Contains("Connects") && english.Contains("Match with others"),
                "Expanding the details must reveal the secondary RPCN fields.");

            // Clickable links: safe http/https only. The tested RPCS3 build stays plain
            // text; the separate "Download build" action targets exactly that build.
            var links = All<TextBlock>(view).SelectMany(block => block.Inlines.OfType<Hyperlink>()).ToList();
            Assert(!links.Any(link => link.Inlines.OfType<Run>().Any(run => run.Text.Contains("0.0.30-16004", StringComparison.Ordinal))) &&
                   english.Contains("0.0.30-16004"),
                "The tested RPCS3 build number must stay plain text.");
            var download = links.SingleOrDefault(link =>
                link.Inlines.OfType<Run>().Select(run => run.Text).SequenceEqual(["Download build"]));
            Assert(download is not null &&
                   download.NavigateUri?.AbsoluteUri == TestBuildUrl &&
                   download.Cursor == System.Windows.Input.Cursors.Hand &&
                   !links.Any(link => link.NavigateUri?.Host == "rpcs3.net"),
                "'Download build' must open the exact resolved build URL, never the generic rpcs3.net page.");
            var wikiLink = links.SingleOrDefault(link => link.NavigateUri?.Host == "i.imgur.com");
            Assert(wikiLink is not null &&
                   wikiLink.NavigateUri!.AbsoluteUri == "https://i.imgur.com/wJWi7Au.mp4" &&
                   links.All(link => link.NavigateUri is { Scheme: "http" or "https" }),
                "A URL inside an applicable Wiki note must render as a clickable http/https hyperlink.");
            wikiLink!.RaiseEvent(new System.Windows.Input.MouseEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount)
            {
                RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent
            });
            Assert(wikiLink.TextDecorations == TextDecorations.Underline,
                "A hyperlink must react visually when hovered.");

            // Unknown installed version: no version-scoped RPCN detail at all.
            var unknownModel = GameSettingsProjector.Project(
                plan,
                evidence,
                new GameSettingsContext(Serial, null));
            vm.Load(plan, unknownModel);
            Settle(window);
            var unknownTexts = Texts(view);
            Assert(unknownTexts.Contains(UiText.GetText("Online.UnknownVersion")) &&
                   !unknownTexts.Contains(UiText.GetText("Online.NoMatchingVersion")) &&
                   !unknownTexts.Any(text =>
                       text == "Tested game version" || text == "RPCS3 version tested" ||
                       text == "Connects" || text == "Create/join lobbies" ||
                       text == "Match with others" || text == "Online features working" ||
                       text == "0.0.30-16004" || text == "1.84" ||
                       text == "RPCS3 tested build" || text == "Download build" || text == "Details" ||
                       text.Contains("pre-load", StringComparison.Ordinal)),
                "With an unknown installed version only the 'cannot confirm RPCN' line shows; tested version, build, matrix and workaround stay hidden.");
            vm.Load(plan, model);
            Settle(window);

            UiText.SetLang(UiText.ArabLang);
            UiLaySvc.Apply();
            vm.SetLang();
            Settle(window);

            var arabic = Blocks(view);
            foreach (var fragment in new[] { "1.84", "0.0.30-16004", "Keyboard", "Basic" })
            {
                var block = arabic.FirstOrDefault(item => Plain(item).Contains(fragment, StringComparison.Ordinal));
                Assert(block is not null,
                    $"Arabic Settings view must render technical value '{fragment}'.");
                Assert(block!.FlowDirection == FlowDirection.LeftToRight,
                    $"Technical value '{fragment}' must stay left-to-right under Arabic (was {block.FlowDirection}).");
            }

            Assert(arabic.Any(block => Plain(block) == UiText.GetText("Wiki.Notes.Title")) &&
                   arabic.Any(block => Plain(block) == UiText.GetText("Online.Value.NotRequired")) &&
                   arabic.Any(block => Plain(block) == UiText.GetText("Online.Details")) &&
                   arabic.Any(block => Plain(block) == UiText.GetText("Wiki.ArabicUnavailable")) &&
                   arabic.Any(block => Plain(block) == UiText.GetText("Wiki.ShowOriginalDetails")) &&
                   !arabic.Any(block => Plain(block) == UiText.GetText("Wiki.OriginalText")),
                "Arabic Settings view must localize titles and detail toggles, explicitly mark untranslated Wiki notes, and hide the original-text caption when no original is shown inline.");
            var sourceDetails = All<Expander>(view).Single(expander =>
                expander.DataContext is GameNoteVm note &&
                note.Text.Contains("frame-limiter", StringComparison.Ordinal));
            Assert(!sourceDetails.IsExpanded,
                "Untranslated Wiki source details must start collapsed.");
            sourceDetails.IsExpanded = true;
            Settle(window);
            var englishSource = All<TextBlock>(view).Single(block =>
                Plain(block).Contains("frame-limiter", StringComparison.Ordinal));
            Assert(englishSource.FlowDirection == FlowDirection.LeftToRight &&
                   englishSource.Language.IetfLanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase),
                "Untranslated Wiki source must remain available verbatim in its collapsed English details.");
            var arabicTexts = Texts(view);
            foreach (var header in new[] { "الإعداد", "القيمة الموصى بها", "الدور" })
            {
                Assert(!arabicTexts.Contains(header),
                    $"The Arabic Settings list must not show the column title '{header}'.");
            }

            var arabicLabels = arabic.Where(block => Plain(block) == UiText.GetText("Online.Rpcs3Build") ||
                                                     Plain(block) == UiText.GetText("Online.DownloadBuild") ||
                                                     Plain(block) == UiText.GetText("Online.TestedVersion") ||
                                                     Plain(block) == UiText.GetText("Online.Support"));
            Assert(arabicLabels.Count() == 4 &&
                   arabicLabels.All(block => Plain(block).Any(ch => ch is >= '\u0600' and <= '\u06FF')) &&
                   UiText.GetText("Online.Support") == "اللعب عبر RPCN" &&
                   UiText.GetText("Online.Rpcs3Build") == "نسخة RPCS3 المختبرة" &&
                   UiText.GetText("Online.Rpcs3Current") == "نسخة RPCS3 الحالية" &&
                   UiText.GetText("Online.DownloadBuild") == "تنزيل النسخة" &&
                   UiText.GetText("Online.TestedVersion") == "إصدار اللعبة المختبر",
                "Fixed labels must be natural Arabic in Arabic mode.");
            Assert(arabic.All(block => !Plain(block).Any(ch => ch >= '٠' && ch <= '٩')),
                "The Settings view must use Western digits 0-9 in Arabic.");
            Assert(arabic.Where(block => Plain(block).Contains("frame-limiter", StringComparison.Ordinal))
                       .All(block => block.FlowDirection == FlowDirection.LeftToRight),
                "English Wiki note text must render left-to-right in the Arabic UI.");

            AssertNotesCardLinks(view, vm);
            AssertPlanRowTooltipLink(view, vm);
            AssertPlanRowTooltipSource(view, vm);

            Assert(listener.Errors.Count == 0,
                "Settings view produced data-binding errors: " + string.Join(" | ", listener.Errors));
            Console.WriteLine("Game Settings view render (EN + AR: sections, notes, online/RPCN, LTR technical values) PASS.");
        }
        finally
        {
            // A window left open when an assertion fails would receive messages
            // after its thread is gone and take the whole test process down.
            window?.Close();
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
        }
    }

    // Round-7b: the real Settings.Notes card for Demon's Souls must show the
    // [url label] note as its label only, with the url on a Hyperlink, in both
    // languages (the shipped wiki-notes.json note, not a fixture).
    private static void AssertNotesCardLinks(R3Config.Ctrl.RecView view, PlanVm vm)
    {
        const string modUrl = "https://www.nexusmods.com/demonssouls/mods/8";
        const string demonSerial = "BLUS30443";
        var evidence = WikiEvidenceCatalog
            .Load(Path.Combine(RepoRoot(), "App", "Data", WikiEvidenceCatalog.FileName))
            .Find(demonSerial, "Demon's Souls")
            ?? throw new InvalidOperationException("Shipped wiki-notes.json must contain Demon's Souls.");
        var plan = PlanOf(demonSerial, "Demon's Souls", []);
        var model = GameSettingsProjector.Project(plan, evidence, new GameSettingsContext(demonSerial, "1.00"));
        var window = Window.GetWindow(view)!;

        foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
        {
            UiText.SetLang(language);
            UiLaySvc.Apply();
            vm.Load(plan, model);
            vm.SetLang();
            Settle(window);

            var card = All<Border>(view).Single(item =>
                AutomationProperties.GetAutomationId(item) == "Settings.Notes");
            foreach (var expander in All<Expander>(card))
            {
                expander.IsExpanded = true;
            }

            Settle(window);
            var visible = string.Join(" / ", Texts(card));
            Assert(!visible.Contains('[') && !visible.Contains("nexusmods", StringComparison.Ordinal),
                $"[{language}] Settings.Notes must not show a raw [url label] or the url as text. Rendered: {visible}");
            var links = All<TextBlock>(card)
                .SelectMany(block => block.Inlines.OfType<Hyperlink>())
                .Where(item => item.NavigateUri?.AbsoluteUri == modUrl)
                .ToList();
            Assert(links.Count >= 1 && links.All(item => Flatten(item.Inlines) == "this mod"),
                $"[{language}] Settings.Notes must carry the mod label as a Hyperlink to {modUrl}.");
        }
    }

    // Round 13: a ToolTip closes on the first click inside it, so the Plan-row
    // tooltip body (shipped cat.json note for BLUS30948, RPCS3 issue 6861) shows
    // the [url label] label as plain text: no raw [url ...] syntax, no URL text,
    // and no Hyperlink, inline button, or other interactive element.
    private static void AssertPlanRowTooltipLink(R3Config.Ctrl.RecView view, PlanVm vm)
    {
        const string serial = "BLUS30948";
        const string label = "black field glitch during day games";
        var rec = RecSvc.Load(Path.Combine(RepoRoot(), "App", "Data", "cat.json")).Find(serial)
            ?? throw new InvalidOperationException("Shipped cat.json must contain BLUS30948.");
        Assert(rec.Changes.Any(change => (change.Notes ?? string.Empty).Contains("[https://github.com/RPCS3/rpcs3/issues/6861/ " + label + "]", StringComparison.Ordinal)),
            "Shipped cat.json BLUS30948 must still carry the [url label] note this regression covers.");
        var plan = PlanOf(serial, rec.WikiName, rec.Changes);
        var model = GameSettingsProjector.Project(plan, null, new GameSettingsContext(serial, "01.00"));
        var window = Window.GetWindow(view)!;

        foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
        {
            UiText.SetLang(language);
            UiLaySvc.Apply();
            vm.Load(plan, model);
            vm.SetLang();
            Settle(window);

            var row = All<Border>(view).Single(item => item.ToolTip is ToolTip &&
                                                       (item.DataContext as PlanItVm)?.Key == "Shader Mode");
            var tip = (ToolTip)row.ToolTip;
            tip.PlacementTarget = row;
            tip.IsOpen = true;
            try
            {
                tip.UpdateLayout();
                var body = All<TextBlock>(tip).Single(block => R3Config.Ctrl.LinkText.GetText(block) is { Length: > 0 });
                var shown = Flatten(body.Inlines);
                Assert(!shown.Contains('[') && !shown.Contains("http", StringComparison.OrdinalIgnoreCase) &&
                       !shown.Contains("github", StringComparison.OrdinalIgnoreCase) && !shown.Contains("6861", StringComparison.Ordinal),
                    $"[{language}] The Plan-row tooltip must not show raw [url ...] syntax or the URL. Rendered: {shown}");
                Assert(language != UiText.EnLang || shown.Contains(label, StringComparison.Ordinal),
                    $"[{language}] The Plan-row tooltip must show the link label as text. Rendered: {shown}");
                Assert(!Inlines(body.Inlines).Any(inline => inline is Hyperlink or InlineUIContainer) &&
                       All<ButtonBase>(tip).Count == 0,
                    $"[{language}] The Plan-row tooltip must hold no Hyperlink, inline UI, or button.");
            }
            finally
            {
                tip.IsOpen = false;
            }
        }
    }

    // The row ToolTip also shows where the row's current value comes from, the
    // value R3Config's copy of the RPCS3 database boots when it differs, where
    // the recommendation comes from, and the RPCS3 default from
    // R3Config's table ("Unknown" when the table has none). Values stay LTR;
    // labels and source lines follow the UI language.
    private static void AssertPlanRowTooltipSource(R3Config.Ctrl.RecView view, PlanVm vm)
    {
        const string serial = "BLUS30442";
        Assert(PlanService.TryReadBuiltIn("Video", "Shader Mode", out var shaderDefault) && shaderDefault is { Length: > 0 },
            "The built-in table must hold the Shader Mode default this test renders.");
        var changes = new[]
        {
            new CfgChg("Video", "Shader Mode", "Async Shader Recompiler", "shader mode", null, RecRisk.Rec),
            new CfgChg("Video", "Write Color Buffers", "true", "write color buffers", null, RecRisk.Rec) { Origin = RecOrigin.Sync }
        };
        var plan = PlanOf(serial, "Midnight Club: Los Angeles", changes);
        plan = plan with
        {
            Items =
            [
                plan.Items[0] with { CurVal = "Shader Interpreter only", RunVal = "Shader Interpreter only", CurSrc = CfgValSrc.Global, DefVal = shaderDefault },
                plan.Items[1] with { CurVal = "false", RunVal = "true", CurSrc = CfgValSrc.Global, DefVal = null }
            ]
        };
        var model = GameSettingsProjector.Project(plan, null, new GameSettingsContext(serial, "01.06"));
        var window = Window.GetWindow(view)!;

        foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
        {
            UiText.SetLang(language);
            UiLaySvc.Apply();
            vm.Load(plan, model);
            vm.SetLang();
            Settle(window);
            var textFlow = UiLaySvc.Current.TextFlow;
            Assert(language == UiText.EnLang || textFlow == FlowDirection.RightToLeft,
                "Arabic natural text must flow right to left.");

            Check("Shader Mode", "Shader Interpreter only", "Plan.CurSrc.Global", "Plan.RecSrc.Baseline", shaderDefault, null);
            Check("Write Color Buffers", "false", "Plan.CurSrc.Global", "Plan.RecSrc.Sync", null, "true");

            // "Current" is the row's current value (CurDsp, here "false") with its
            // own source; the database boot value ("true", equal to the proposed
            // value) is a separate line, so every check reads exact positions.
            void Check(string key, string current, string sourceKey, string originKey, string? defaultValue, string? dbBoot)
            {
                var row = All<Border>(view).Single(item => item.ToolTip is ToolTip &&
                                                           (item.DataContext as PlanItVm)?.Key == key);
                var tip = (ToolTip)row.ToolTip;
                tip.PlacementTarget = row;
                tip.IsOpen = true;
                try
                {
                    tip.UpdateLayout();
                    var blocks = Blocks(tip);
                    var shown = string.Join(" / ", blocks.Select(Plain));
                    TextBlock One(string text) =>
                        blocks.SingleOrDefault(block => Plain(block) == text)
                        ?? throw new InvalidOperationException($"[{language}] {key} ToolTip must show '{text}'. Rendered: {shown}");

                    foreach (var textKey in new[] { "Plan.Default", sourceKey, originKey })
                    {
                        var text = UiText.GetText(textKey);
                        Assert(One(text).FlowDirection == textFlow && NoteSvc.ContainsArabic(text) == (language == UiText.ArabLang),
                            $"[{language}] {key} ToolTip line {textKey} must be localized natural text in the UI direction. Rendered: {shown}");
                    }

                    var at = blocks.IndexOf(One(UiText.GetText("Plan.Current")));
                    Assert(at >= 0 && at + 2 < blocks.Count && Plain(blocks[at + 1]) == current &&
                           blocks[at + 1].FlowDirection == FlowDirection.LeftToRight &&
                           Plain(blocks[at + 2]) == UiText.GetText(sourceKey),
                        $"[{language}] {key} ToolTip must show the row's current value ({current}) left to right right after the Current label, then its source. Rendered: {shown}");
                    var bootLabel = UiText.GetText("Plan.DbBoot");
                    Assert(dbBoot is null
                            ? blocks.All(block => Plain(block) != bootLabel)
                            : at + 4 < blocks.Count && blocks[at + 3] == One(bootLabel) && blocks[at + 3].FlowDirection == textFlow &&
                              Plain(blocks[at + 4]) == dbBoot && blocks[at + 4].FlowDirection == FlowDirection.LeftToRight,
                        $"[{language}] {key} ToolTip must show the boot value from R3Config's database copy ({dbBoot ?? "none"}) only as its own line after the source. Rendered: {shown}");
                    var unknown = UiText.GetText("Ui.Unknown");
                    if (defaultValue is null)
                    {
                        Assert(One(unknown).FlowDirection == textFlow,
                            $"[{language}] {key} ToolTip must show Unknown, never a guess, when the table has no default. Rendered: {shown}");
                    }
                    else
                    {
                        Assert(One(defaultValue).FlowDirection == FlowDirection.LeftToRight && blocks.All(block => Plain(block) != unknown),
                            $"[{language}] {key} ToolTip must show the table default {defaultValue} left to right. Rendered: {shown}");
                    }
                }
                finally
                {
                    tip.IsOpen = false;
                }
            }
        }
    }

    private static IEnumerable<Inline> Inlines(InlineCollection inlines) =>
        inlines.SelectMany(inline => inline is Span span ? Inlines(span.Inlines).Prepend(inline) : [inline]);

    // Layout passes only: pumping the dispatcher would run the queued
    // Application.OnStartup, which resets the UI language and opens the real
    // main window.
    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
        }
    }

    private static List<TextBlock> Blocks(DependencyObject root)
    {
        var found = new List<TextBlock>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
            // Only what is actually rendered counts: collapsed elements stay out.
            if (node is TextBlock block && block.IsVisible)
            {
                found.Add(block);
            }

            if (node is not (Visual or System.Windows.Media.Media3D.Visual3D))
            {
                return;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                Walk(VisualTreeHelper.GetChild(node, index));
            }
        }
    }

    private static List<T> All<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
            if (node is T match)
            {
                found.Add(match);
            }

            if (node is not (Visual or System.Windows.Media.Media3D.Visual3D))
            {
                return;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                Walk(VisualTreeHelper.GetChild(node, index));
            }
        }
    }

    // A TextBlock built from inlines (links) has its text in the runs.
    private static string Plain(TextBlock block) =>
        block.Inlines.Count == 0 ? block.Text : Flatten(block.Inlines);

    private static string Flatten(InlineCollection inlines)
    {
        var text = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    text.Append(run.Text);
                    break;
                case Span span:
                    text.Append(Flatten(span.Inlines));
                    break;
            }
        }

        return text.ToString();
    }

    private static List<string> Texts(DependencyObject root) =>
        Blocks(root).Select(block => Plain(block)).Where(text => text.Length > 0).ToList();

    private static T? Ancestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(node); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class RecHost(PlanVm plan)
    {
        public PlanVm Plan { get; } = plan;

        public System.Windows.Input.ICommand? DelSet { get; }

        public System.Windows.Input.ICommand? ApplyCmd { get; }
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = [];

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }
    }

    // Explicit Wiki setting -> canonical RPCS3 key -> section -> applied to the
    // selected game. Covers the setting that R3Config previously dropped
    // (firmware libraries -> Core / Libraries Control, a YAML sequence) plus
    // the plain examples, and the settings that must stay evidence only.
    private static void AssertWikiSettingsMapping(string root)
    {
        var xmlPath = Path.Combine(root, "libraries-wiki.xml");
        File.WriteAllText(
            xmlPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <mediawiki>
              <page>
                <ns>0</ns>
                <title>Library Test Game</title>
                <revision>
                  <text xml:space="preserve">{{vginfobox
            |gameid = {{gameid|id=BLUS99001}}
            }}
            ==Configuration==
            {{config
            |ppu decoder = Recompiler (LLVM)
            |framelimit = 30
            |keyboard handler = Basic
            |keyboard handler notes = Optional
            |firmware libraries = libvdec.sprx, libsysutil.sprx:lle <!-- comment -->
            |delay each odd mfc command = On
            }}
            ==Known Issues==
            Do not use the Windows build.</text>
                </revision>
              </page>
              <page>
                <ns>0</ns>
                <title>Bad Library Game</title>
                <revision>
                  <text xml:space="preserve">{{vginfobox
            |gameid = {{gameid|id=BLUS99002}}
            }}
            ==Configuration==
            {{config
            |firmware libraries = every library
            }}</text>
                </revision>
              </page>
            </mediawiki>
            """,
            new System.Text.UTF8Encoding(false));

        var analysis = WikiSvc.Analyze(xmlPath, "2026-09-07");
        var game = analysis.CatalogRecommendations.Single(rec => rec.Serial == "BLUS99001");
        var byKey = game.Changes.ToDictionary(change => change.WikiKey);

        Assert(game.Changes.Count == 4,
            $"Expected ppu decoder, framelimit, keyboard handler, and firmware libraries; the MFC key must stay evidence only. Got {game.Changes.Count}.");
        Assert(byKey["ppu decoder"] is { Section: "Core", Key: "PPU Decoder", Value: "Recompiler (LLVM)" } &&
               byKey["framelimit"] is { Section: "Video", Key: "Frame limit", Value: "30" } &&
               byKey["keyboard handler"] is { Section: "Input/Output", Key: "Keyboard", Value: "Basic" },
            "Explicit Wiki settings must map to their canonical RPCS3 keys and values.");
        Assert(byKey["firmware libraries"] is
                   { Section: "Core", Key: "Libraries Control", Value: "libsysutil.sprx:lle, libvdec.sprx:lle" },
            "firmware libraries must map to Core / Libraries Control as the sorted <name>.sprx:lle list.");

        var sections = game.Changes.ToDictionary(
            change => change.WikiKey,
            change => SettingsSectionMap.Classify(change, out _));
        Assert(sections["ppu decoder"] == Rpcs3SettingsSection.Cpu &&
               sections["framelimit"] == Rpcs3SettingsSection.Gpu &&
               sections["keyboard handler"] == Rpcs3SettingsSection.Io &&
               sections["firmware libraries"] == Rpcs3SettingsSection.Advanced,
            "Each mapped setting must land in its RPCS3 section.");
        Assert(SettingsSectionMap.ClassifyRole(byKey["keyboard handler"]) == SettingRole.Optional,
            "Keyboard handler = Basic with an Optional note must keep the Optional role.");

        var mfc = analysis.Recommendations.Single(entry => entry.WikiKey == "delay each odd mfc command");
        Assert(mfc.ParserStatus == WikiParserStatus.Unsupported &&
               mfc.MappedSettings.Count == 0 &&
               mfc.RawWikiValue == "On" &&
               mfc.Detail is not null &&
               mfc.Detail.Contains("current RPCS3", StringComparison.Ordinal),
            "A setting absent from current RPCS3 must be preserved as evidence and not applied.");

        var bad = analysis.Recommendations.Single(entry => entry.Page == "Bad Library Game");
        Assert(bad.ParserStatus == WikiParserStatus.Rejected && bad.MappedSettings.Count == 0,
            "A firmware libraries value that is not a library list must be rejected, not guessed.");

        // The sequence-valued setting must round-trip through the real YAML
        // writer/reader and reach the plan through a built-in default.
        var config = Path.Combine(root, "config_BLUS99001.yml");
        var libraries = byKey["firmware libraries"];
        R3Yaml.Apply(config, [libraries, byKey["framelimit"]]);
        var written = File.ReadAllText(config);
        Assert(written.Contains("- libvdec.sprx:lle", StringComparison.Ordinal) &&
               R3Yaml.ReadVal(config, "Core", "Libraries Control") == "libsysutil.sprx:lle, libvdec.sprx:lle" &&
               R3Yaml.ReadVal(config, "Video", "Frame limit") == "30",
            "Libraries Control must be written as a YAML sequence and read back as the same canonical value.");
        R3Yaml.Del(config, [libraries]);
        Assert(R3Yaml.ReadVal(config, "Core", "Libraries Control") is null &&
               R3Yaml.ReadVal(config, "Video", "Frame limit") == "30",
            "Removing the libraries setting must leave the other settings intact.");

        var env = GameSetupT.CreateEnvironment(Path.Combine(root, "env"));
        var plan = new PlanService().BuildCfg(
            env.Install,
            new Rpcs3GameDescriptor("BLUS99001", "Library Test Game", env.GamePath, null, null, null),
            game);
        var libraryItem = plan.Items.Single(item => item.Key == "Libraries Control");
        Assert(libraryItem.CurVal == string.Empty &&
               libraryItem.State is RecState.Pending or RecState.Manual &&
               SettingsSectionMap.Classify(libraryItem.Change, out var fallback) == Rpcs3SettingsSection.Advanced &&
               !fallback,
            "Libraries Control must plan against its built-in default and show under Advanced.");

        var notes = WikiNotesParser.Parse("Library Test Game", ["BLUS99001"], File.ReadAllText(xmlPath));
        Assert(notes.Notes.Count == 1 && notes.Notes[0].PlatformCondition == "Windows",
            "Notes on the same page stay notes and keep their platform scope.");
    }

    // The SHIPPED application data (App/Data as copied next to the binary),
    // loaded through the normal AppFlow/AppComposition path: settings catalog
    // and Wiki evidence sidecar must come from the same F: snapshot, and
    // Minecraft plus a Libraries Control game must reach Game Settings from it.
    private static void AssertShippedData(AppFlow flow)
    {
        const string sourceHash = "7977CA26176E12DD167241CDE77D68E6CC64CE24CB39C2682FD5E686CA7936C5";

        foreach (var baseDirectory in new[] { AppContext.BaseDirectory, Path.Combine(RepoRoot(), "App") })
        {
            var catalogResult = flow.LoadCat(baseDirectory);
            var evidenceResult = flow.LoadWikiEvidence(baseDirectory);
            Assert(catalogResult.Cat is not null && catalogResult.Error is null &&
                   evidenceResult.Error is null && !evidenceResult.Catalog.IsEmpty,
                $"Shipped cat.json and wiki-notes.json must load through AppFlow from {baseDirectory}.");
            Assert(catalogResult.Cat!.Info.SrcHash == sourceHash &&
                   catalogResult.Cat.Info.DataVer == "2026-09-20" &&
                   evidenceResult.Catalog.Document.SourceHash == sourceHash &&
                   evidenceResult.Catalog.Document.DataVersion == "2026-09-20" &&
                   evidenceResult.Catalog.Document.SourceFile == "RPCS3+Wiki-20260920160231.xml",
                "cat.json and wiki-notes.json must carry the same F: Wiki source hash, snapshot date, and file name.");
        }

        var shippedCat = File.ReadAllBytes(Path.Combine(RepoRoot(), "App", "Data", "cat.json"));
        var shippedNotes = File.ReadAllBytes(Path.Combine(RepoRoot(), "App", "Data", WikiEvidenceCatalog.FileName));
        Assert(shippedCat.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "cat.json"))) &&
               shippedNotes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", WikiEvidenceCatalog.FileName))),
            "The build output must carry the exact App/Data cat.json and wiki-notes.json.");

        var catalog = flow.LoadCat(AppContext.BaseDirectory).Cat!;
        var evidence = flow.LoadWikiEvidence(AppContext.BaseDirectory).Catalog;

        // Minecraft, from shipped data only.
        var rec = catalog.Find(Serial)!;
        var game = new Rpcs3GameDescriptor(Serial, rec.WikiName, string.Empty, "1.00", null, null);
        var plan = PlanOf(Serial, rec.WikiName, rec.Changes);
        var model = flow.GameSettings(plan, game, rec, evidence, null);
        var keyboard = model.Sections.Single(section => section.Section == Rpcs3SettingsSection.Io).Rows.Single();
        Assert(keyboard.Entry.Key == "Keyboard" && keyboard.Entry.Val == "Basic" && keyboard.Role == SettingRole.Optional,
            "Shipped data must give Minecraft Keyboard = Basic (Optional) under I/O.");
        Assert(model.Notes.Count == 5 &&
               model.Notes.Any(note => note.Note.Class == WikiNoteClass.Warning) &&
               model.Notes.Any(note => note.Note.Class == WikiNoteClass.Workaround) &&
               model.Notes.Any(note => note.Applicability == WikiNoteApplicability.OtherPlatform),
            "Shipped data must carry Minecraft's five Wiki notes, macOS one scoped away.");
        var online = model.Online.Single();
        Assert(online.TestedGameVersion == "1.84" &&
               online.RequiredGameVersion is null &&
               online.Rpcs3VersionTested == "0.0.30-16004" &&
               online.Connects == "Yes" && online.CreateJoinLobbies == "Yes" &&
               online.MatchWithOthers == "Yes" && online.OnlineFeaturesWorking == "Yes" &&
               online.RequiresCustomServers == "Not required",
            "Shipped data must give Minecraft tested 1.84 (no invented Required version), RPCS3 0.0.30-16004, all Yes.");

        // Real production control end to end: shipped Wiki evidence, shipped
        // build catalog and the runtime context of an installed 1.84 game give
        // the tested build's exact official URL (no synthetic catalog).
        var shippedBuilds = flow.LoadRpcs3Builds(AppContext.BaseDirectory);
        var shippedContext = flow.RuntimeContext(
            null,
            null,
            null,
            new Rpcs3GameDescriptor(Serial, rec.WikiName, string.Empty, "1.84", null, null),
            flow.GameSettings(plan, new Rpcs3GameDescriptor(Serial, rec.WikiName, string.Empty, "1.84", null, null), rec, evidence, null, null, shippedBuilds),
            shippedBuilds);
        Assert(flow.GameSettings(plan, new Rpcs3GameDescriptor(Serial, rec.WikiName, string.Empty, "1.84", null, null), rec, evidence, null, null, shippedBuilds)
                   .ExactBuildUrl("0.0.30-16004") ==
               "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-0d1fbfb755aa7856a8b94c74be2e116a0ca743cb/rpcs3-v0.0.30-16004-0d1fbfb7_win64.7z" &&
               shippedContext.Facts.Any(fact => fact.Key == "TestedBuild" && fact.Value == "0.0.30-16004" && fact.Code == "OfficialGitHub"),
            "The shipped Wiki evidence and shipped build catalog must resolve Minecraft's tested build 0.0.30-16004 to its exact official GitHub asset.");

        // A Libraries Control game, from shipped data only.
        var lair = catalog.Find("BCAS20014")!;
        var libraries = lair.Changes.Single(change => change.Key == "Libraries Control");
        Assert(libraries.Section == "Core" && libraries.WikiKey == "firmware libraries" &&
               libraries.Value == "libvdec.sprx:lle",
            "The shipped catalog must carry Lair's firmware libraries as Core / Libraries Control.");
        var lairModel = flow.GameSettings(
            PlanOf("BCAS20014", lair.WikiName, lair.Changes),
            new Rpcs3GameDescriptor("BCAS20014", lair.WikiName, string.Empty, null, null, null),
            lair,
            evidence,
            null);
        var lairRow = lairModel.Sections.Single(section => section.Section == Rpcs3SettingsSection.Advanced)
            .Rows.Single(row => row.Entry.Key == "Libraries Control");
        Assert(lairRow.Entry.Val == "libvdec.sprx:lle" && lairRow.Role == SettingRole.Required,
            "Lair's Libraries Control must reach the Advanced section as a Required Wiki setting.");

        var libraryRecs = catalog.Rec
            .Where(item => item.Changes.Any(change => change.Key == "Libraries Control"))
            .ToArray();
        Assert(libraryRecs.Length == 105 &&
               libraryRecs.All(item => !item.IsInherited) &&
               !catalog.Rec.Where(item => item.WikiName == "Journey")
                   .Any(item => item.Changes.Any(change => change.Key == "Libraries Control")),
            "All 105 shipped Libraries Control rows are exact-bound, and Journey (unresolved GameID scope) is not applied.");

        // Every shipped setting still reaches a section (no fallback, none dropped).
        var total = 0;
        foreach (var change in catalog.Rec.SelectMany(item => item.Changes))
        {
            total++;
            SettingsSectionMap.Classify(change, out var fallback);
            Assert(!fallback, $"Shipped Wiki key '{change.WikiKey}' has no explicit RPCS3 section.");
        }

        // 7794 = the settings count proven by --wiki-verify against the
        // canonical RPCS3+Wiki-20260920160231.xml (0 differences); it
        // includes the 105 Libraries Control rows asserted above.
        Assert(total == 7794,
            "Shipped catalog must hold exactly the 7794 canonical September 20 Wiki settings.");
    }

    // Notes and RPCN evidence are filtered by the detected runtime context
    // (serial/edition, installed APP_VER, platform, hardware) before they reach
    // the view; everything else stays in the model as hidden evidence.
    private static void AssertApplicabilityFiltering(
        AppFlow flow,
        CfgPlan plan,
        WikiRec rec,
        WikiEvidenceCatalog evidenceCatalog)
    {
        _ = flow;
        _ = rec;
        UiText.SetLang(UiText.EnLang);
        var minecraft = evidenceCatalog.Find(Serial, "Minecraft")
            ?? throw new InvalidOperationException("Minecraft evidence must be present.");

        GameEvidenceVm Shown(string serial, string? version, string platform = "Windows", string? gpu = null)
        {
            var model = GameSettingsProjector.Project(
                plan,
                minecraft,
                new GameSettingsContext(serial, version, platform, null, gpu));
            var evidence = new GameEvidenceVm();
            evidence.Load(model);
            return evidence;
        }

        static bool Has(GameEvidenceVm evidence, string fragment) =>
            evidence.Notes.Any(note => note.Text.Contains(fragment, StringComparison.Ordinal));

        var windowsDisc = Shown("BLES01976", "1.00");
        Assert(windowsDisc.Notes.Count == 3 && !Has(windowsDisc, "MacOS") && !Has(windowsDisc, "pitched"),
            "Windows disc 1.00: neither the macOS note nor the digital-1.00 note is shown.");

        var windowsDigital100 = Shown("NPUB31419", "1.00");
        Assert(windowsDigital100.Notes.Count == 4 && Has(windowsDigital100, "pitched") && !Has(windowsDigital100, "MacOS"),
            "Windows digital 1.00: the digital 1.00 music note applies; the macOS note still does not.");

        var windowsDigital184 = Shown("NPUB31419", "1.84");
        Assert(windowsDigital184.Notes.Count == 3 && !Has(windowsDigital184, "pitched"),
            "Windows digital 1.84: a note scoped to version 1.00 is not shown.");

        Assert(Has(Shown("BLES01976", "1.05", "macOS"), "MacOS") &&
               !Has(Shown("BLES01976", "1.00", "macOS"), "MacOS"),
            "macOS host: the 'past 1.00' note appears only for an installed version newer than 1.00.");

        var unknownVersion = Shown("NPUB31419", null);
        Assert(unknownVersion.Notes.Count == 3 && !Has(unknownVersion, "pitched") && unknownVersion.OtherNotes.Count == 2,
            "An unknown installed version cannot confirm a version-scoped note, so it is hidden rather than guessed.");

        // Hardware and serial scope.
        WikiFreeTextNote Scoped(string text, string? hardware = null, string? serial = null) =>
            new("P", "Known Issues", WikiNoteClass.HardwareConditionalNote, text, null, null,
                HardwareCondition: hardware, SerialCondition: serial);
        var amd = Scoped("On AMD GPUs enable it.", hardware: "AMD");
        Assert(GameSettingsProjector.Applicability(amd, new GameSettingsContext(Serial, GpuName: "NVIDIA GeForce RTX 4070")) ==
                   WikiNoteApplicability.OtherHardware &&
               GameSettingsProjector.Applicability(amd, new GameSettingsContext(Serial, GpuName: "AMD Radeon RX 7800")) ==
                   WikiNoteApplicability.General &&
               GameSettingsProjector.Applicability(amd, new GameSettingsContext(Serial)) ==
                   WikiNoteApplicability.Unresolved &&
               GameSettingsProjector.Applicability(Scoped("GPU note.", hardware: "GPU"), new GameSettingsContext(Serial)) ==
                   WikiNoteApplicability.General,
            "A vendor-specific note applies only to matching hardware; a generic GPU/CPU note applies everywhere.");
        Assert(GameSettingsProjector.Applicability(Scoped("Other region.", serial: "BLUS31426"), new GameSettingsContext(Serial)) ==
                   WikiNoteApplicability.OtherSerial &&
               GameSettingsProjector.Applicability(Scoped("This region.", serial: Serial), new GameSettingsContext(Serial)) ==
                   WikiNoteApplicability.General,
            "A note for another serial is hidden; a note for this serial applies.");

        // The Wiki's default "no issues" line and duplicate evidence add no value.
        var boilerplate = new GameEvidenceVm();
        boilerplate.Load(new GameSettingsModel(
            Serial, "P", [],
            [new GameNote(Scoped("There are no reported issues with this title."), WikiNoteApplicability.General)],
            []));
        Assert(!boilerplate.HasNotes && !boilerplate.HasAny,
            "The 'no reported issues' boilerplate must not occupy the page.");

        // RPCN respects the installed version.
        var rpcn184 = Shown(Serial, "1.84");
        var rpcn100 = Shown(Serial, "1.00");
        var rpcnUnknown = Shown(Serial, null);
        var rpcnPadded = Shown(Serial, "01.84");
        Assert(rpcn184.HasOnline && !rpcn184.HasOnlineMismatch && !rpcn184.HasOnlineUnknownVersion &&
               rpcnPadded.HasOnline,
            "RPCN shows when the tested version equals the installed one (01.84 == 1.84).");
        Assert(!rpcnUnknown.HasOnline && rpcnUnknown.Online.Count == 0 &&
               !rpcnUnknown.HasOnlineMismatch && rpcnUnknown.HasOnlineUnknownVersion && rpcnUnknown.HasAny &&
               rpcnUnknown.OnlineUnknownVersionText == UiText.GetText("Online.UnknownVersion"),
            "An unknown installed version confirms nothing: no RPCN detail, only the cannot-confirm line.");
        Assert(!rpcn100.HasOnline && rpcn100.HasOnlineMismatch && !rpcn100.HasOnlineUnknownVersion && rpcn100.HasAny &&
               rpcn100.OnlineMismatchText == UiText.GetText("Online.NoMatchingVersion") &&
               rpcn100.Online.Count == 0,
            "RPCN evidence for another game version must collapse to one informational line, never the detail block.");
        var noEvidence = new GameEvidenceVm();
        noEvidence.Load(GameSettingsModel.Empty(Serial));
        Assert(!noEvidence.HasOnline && !noEvidence.HasOnlineMismatch && !noEvidence.HasOnlineUnknownVersion,
            "A game with no RPCN evidence shows no RPCN information at all.");
    }

    private static void AssertLinks()
    {
        var parts = WikiLinks.Split("See https://forums.rpcs3.net/thread?id=1&x=2, and (http://a.example/b). Done");
        Assert(parts.Where(part => part.Url is not null).Select(part => part.Url)
                   .SequenceEqual(["https://forums.rpcs3.net/thread?id=1&x=2", "http://a.example/b"]) &&
               string.Concat(parts.Select(part => part.Text)) ==
                   "See https://forums.rpcs3.net/thread?id=1&x=2, and (http://a.example/b). Done",
            "Only the http/https URLs become links; surrounding punctuation stays text and no character is lost.");

        foreach (var unsafeUrl in new[] { "javascript:alert(1)", "file:///C:/Windows/notepad.exe", "ftp://x.example/a", "//x.example", "https:///nohost", "cmd.exe /c calc" })
        {
            Assert(!WikiLinks.IsSafeUrl(unsafeUrl) &&
                   !WikiLinks.Split($"open {unsafeUrl} now").Any(part => part.Url is not null),
                $"'{unsafeUrl}' must never become a link.");
            Assert(!R3Config.Ctrl.LinkText.Open(unsafeUrl),
                $"'{unsafeUrl}' must never be handed to the shell.");
        }

        Assert(WikiLinks.IsOfficialRpcs3Url("https://rpcs3.net/download") &&
               !WikiLinks.IsOfficialRpcs3Url("https://rpcs3.net.evil.example/") &&
               !WikiLinks.IsOfficialRpcs3Url("http://rpcs3.net/download") &&
               !WikiLinks.IsOfficialRpcs3Url("https://evilrpcs3.net/"),
            "Official-domain matching must stay strict.");
    }


    private static void AssertSettingsModel(GameSettingsModel model, CfgPlan plan)
    {
        Assert(model.Sections.Select(section => section.Section)
                   .SequenceEqual([Rpcs3SettingsSection.Gpu, Rpcs3SettingsSection.Io]),
            "Only sections with real Wiki evidence may be projected: Minecraft has GPU and I/O settings and nothing else.");

        var io = model.Sections.Single(section => section.Section == Rpcs3SettingsSection.Io);
        var keyboard = io.Rows.Single();
        Assert(keyboard.Entry.Key == "Keyboard" && keyboard.Entry.Val == "Basic",
            "Keyboard handler = Basic must appear under I/O.");
        Assert(keyboard.Role == SettingRole.Optional,
            $"The Wiki's 'Optional:' keyboard setting must stay Optional, got {keyboard.Role}.");
        Assert(!keyboard.UsedSectionFallback,
            "Keyboard handler must reach I/O through the explicit Wiki-key map, not the YAML-section fallback.");

        Assert(model.Sections
                   .Where(section => section.Section != Rpcs3SettingsSection.Io)
                   .SelectMany(section => section.Rows)
                   .All(row => row.Entry.Key != "Keyboard"),
            "Keyboard handler must not be duplicated under another tab.");
        Assert(model.Sections.SelectMany(section => section.Rows).Count() == plan.Items.Count,
            "Every setting must appear in exactly one section, and notes must never add settings.");
    }

    private static void AssertNotesModel(GameSettingsModel model)
    {
        Assert(model.Notes.Count == 5,
            $"Expected the five Minecraft Wiki notes, got {model.Notes.Count}.");

        var rpcn = Note(model, "Split-screen");
        Assert(rpcn.Note.Class == WikiNoteClass.Workaround &&
               rpcn.Applicability == WikiNoteApplicability.General,
            "RPCN split-screen crash workaround must be a general Workaround note.");

        var framerate = Note(model, "Change Skin menu");
        Assert(framerate.Note.Class == WikiNoteClass.InformationalNote,
            "Framerate/menu note must stay a note, not become a Frame Limit setting.");

        var limiter = Note(model, "frame-limiter");
        Assert(limiter.Note.Class == WikiNoteClass.Warning,
            "Frame-limiter runtime note must be a Warning.");

        var digital = Note(model, "pitched");
        Assert(digital.Note.Class == WikiNoteClass.VersionSpecificNote &&
               digital.Note.VersionCondition == "1.00" &&
               digital.Note.EditionCondition == "digital" &&
               digital.Applicability == WikiNoteApplicability.OtherEdition,
            "The digital 1.00 music note keeps its version + edition scope and is hidden for a disc serial (BLES01976).");

        var mac = Note(model, "MacOS");
        Assert(mac.Note.Class == WikiNoteClass.PlatformConditionalNote &&
               mac.Note.PlatformCondition == "MacOS" &&
               mac.Note.VersionCondition == "1.00" &&
               mac.Applicability == WikiNoteApplicability.OtherPlatform,
            "macOS 1.00 freeze note must keep platform + version scope and not apply to a Windows host.");

        // "past 1.00" applies to a macOS install newer than 1.00 only.
        Assert(GameSettingsProjector.Applicability(mac.Note, new GameSettingsContext(Serial, "1.00", "macOS")) ==
                   WikiNoteApplicability.OtherVersion &&
               GameSettingsProjector.Applicability(mac.Note, new GameSettingsContext(Serial, "1.05", "macOS")) ==
                   WikiNoteApplicability.General,
            "The macOS note applies on macOS only when the installed version is past 1.00.");

        Assert(model.Notes.Where(note => note.Applicability == WikiNoteApplicability.General)
                   .Select(note => note.Note.Class)
                   .Order()
                   .SequenceEqual(new[] { WikiNoteClass.Warning, WikiNoteClass.Workaround, WikiNoteClass.InformationalNote }.Order()),
            "For a Windows disc install of 1.00 exactly the three unscoped notes apply.");

        Assert(model.Notes.Select(note => note.Applicability)
                   .SequenceEqual(model.Notes.Select(note => note.Applicability).OrderBy(state => state)),
            "General notes must be listed before conditional and out-of-scope notes.");
    }

    private static void AssertOnlineModel(GameSettingsModel model, GameSettingsModel model184)
    {
        Assert(model.Online.Count == 1 &&
               model.OnlineForInstalled.Count == 0 &&
               model.HasOnlineForOtherVersionOnly,
            "Installed 1.00 vs RPCN tested 1.84: the evidence is preserved but is not applicable to the installed version.");
        Assert(model184.OnlineForInstalled.Count == 1 && !model184.HasOnlineForOtherVersionOnly,
            "Installed 1.84 matches the RPCN tested version, so the table applies.");

        var online = model.Online.Single();
        Assert(model.OnlineState == WikiOnlineState.Supported &&
               online.TestedGameVersion == "1.84" &&
               online.RequiredGameVersion is null &&
               online.Rpcs3VersionTested == "0.0.30-16004" &&
               online.Connects == "Yes" &&
               online.CreateJoinLobbies == "Yes" &&
               online.MatchWithOthers == "Yes" &&
               online.OnlineFeaturesWorking == "Yes" &&
               online.RequiresCustomServers == "Not required",
            "Minecraft online evidence must reach the model intact: tested 1.84 (not required), RPCS3 0.0.30-16004, all Yes, custom servers not required.");
    }

    private static void AssertViewModel(CfgPlan plan, GameSettingsModel model)
    {
        UiText.SetLang(UiText.EnLang);
        var vm = new PlanVm();
        vm.Load(plan, model);

        Assert(vm.Items.Count == 3 &&
               vm.Evidence.Notes.Count == 3 &&
               vm.Evidence.OtherNotes.Count == 2 &&
               vm.Evidence.OtherNotes.Any(note => note.Text.Contains("MacOS", StringComparison.Ordinal)) &&
               vm.Evidence.OtherNotes.Any(note => note.Text.Contains("pitched", StringComparison.Ordinal)) &&
               vm.Evidence.Notes.All(note => !note.Text.Contains("MacOS", StringComparison.Ordinal) &&
                                              !note.Text.Contains("pitched", StringComparison.Ordinal)),
            "Only the three notes applicable to a Windows disc install at 1.84 are shown; the macOS and digital-1.00 notes stay as hidden evidence, and the settings list keeps exactly the three settings.");
        Assert(vm.Rows.Select(row => row.Key).SequenceEqual(["Anisotropic Filter Override", "Multithreaded RSX", "Keyboard"]) &&
               vm.Rows.All(row => row.Role is SettingRole.Optional or SettingRole.Other or SettingRole.Conditional or SettingRole.Recommended or SettingRole.Required),
            "The view binds one flat list in RPCS3 tab order (GPU rows, then I/O); tab names are not part of it.");
        Assert(vm.Sections.Select(section => section.Section)
                   .SequenceEqual([Rpcs3SettingsSection.Gpu, Rpcs3SettingsSection.Io]),
            "The Settings view must bind GPU and I/O sections only.");

        var ioSection = vm.Sections.Single(section => section.Section == Rpcs3SettingsSection.Io);
        var keyboard = ioSection.Items.Single();
        Assert(ioSection.Title == "I/O" &&
               keyboard.Key == "Keyboard" &&
               keyboard.Val == "Basic" &&
               keyboard.Role == SettingRole.Optional,
            "Keyboard = Basic is internally an I/O Optional setting; the role is metadata, not a visible column.");
        Assert(vm.Sections.Where(section => section.Section != Rpcs3SettingsSection.Io)
                   .SelectMany(section => section.Items)
                   .All(item => item.Key != "Keyboard"),
            "Keyboard = Basic must not appear under any other tab.");
        Assert(vm.Sections.SelectMany(section => section.Items).Count() == vm.Items.Count,
            "Grouped sections must contain exactly the flat list's items so selection/apply stay consistent.");
        Assert(!vm.ShowEmpty && vm.Evidence.HasNotes && vm.Evidence.HasOnline,
            "A game with settings, notes, and online evidence must not show the empty state.");

        AssertOnlineRows(vm, "Yes", "Not required", "Supported");
        Assert(vm.Evidence.Online.Single().ShowTestedHint &&
               vm.Evidence.Online.Single().Rows.All(row => row.Id != "RequiredGameVersion"),
            "1.84 is a TESTED version: no Required Game Version row, and the tested-not-required hint must show.");
        Assert(vm.Evidence.OtherNotes.Single(note => note.Text.Contains("pitched", StringComparison.Ordinal)).Scope
                   .Any(scope => scope.Contains("digital", StringComparison.Ordinal)) &&
               vm.Evidence.OtherNotes.Single(note => note.Text.Contains("MacOS", StringComparison.Ordinal)).Scope
                   .Any(scope => scope.Contains("MacOS", StringComparison.Ordinal)),
            "Hidden notes keep their edition and platform scope as evidence.");
        var online = vm.Evidence.Online.Single();
        Assert(online.SummaryRows.Select(row => row.Id)
                   .SequenceEqual(["State", "TestedGameVersion", "Rpcs3VersionTested", "RequiresCustomServers"]) &&
               online.DetailRows.Select(row => row.Id)
                   .SequenceEqual(["Connects", "CreateJoinLobbies", "MatchWithOthers", "OnlineFeaturesWorking"]) &&
               online.HasDetails &&
               online.SummaryRows.All(row => row.Url is null) &&
               online.ExactBuildUrl is null &&
               online.SummaryRows.All(row => !row.HasAction),
            "RPCN is compact: four plain summary lines with the yes/no matrix in details; with no catalog row there is no download action.");

        // Arabic: labels/values localize and a Wiki note without a reviewed
        // translation gets an explicit unavailable label. The original source
        // remains available verbatim in the note details.
        UiText.SetLang(UiText.ArabLang);
        vm.SetLang();
        AssertOnlineRows(vm, UiText.GetText("Online.Value.Yes"), UiText.GetText("Online.Value.NotRequired"), UiText.GetText("Online.State.Supported"));
        var untranslatedWarning = vm.Evidence.Notes.Single(note =>
            note.Text.Contains("frame-limiter", StringComparison.Ordinal));
        Assert(untranslatedWarning.DisplayText == UiText.GetText("Wiki.ArabicUnavailable") &&
               untranslatedWarning.OriginalText == untranslatedWarning.Text &&
               !untranslatedWarning.IsOriginalText &&
               !vm.Evidence.ShowOriginalCaption,
            "An unreviewed Wiki note must say its Arabic translation is unavailable, retain the exact English source in details, and never claim that the source is translated.");
        Assert(UiText.GetText("Online.Details") == "التفاصيل" &&
               UiText.GetText("Online.NoMatchingVersion") == "لا توجد بيانات RPCN مطابقة لإصدار اللعبة المثبت.",
            "Arabic must localize the details toggle and the RPCN no-match line.");
        Assert(vm.Evidence.Notes.Any(note =>
                   note.Model.Note.Class == WikiNoteClass.Warning &&
                   note.Title == UiText.GetText("Note.Class.Warning") &&
                   note.Title != "Warning"),
            "Arabic note headers must be localized.");
        UiText.SetLang(UiText.EnLang);
    }

    private static void AssertOnlineRows(PlanVm vm, string yes, string notRequired, string state)
    {
        var rows = vm.Evidence.Online.Single().Rows.ToDictionary(row => row.Id, row => row.Value);
        Assert(rows["State"] == state &&
               rows["TestedGameVersion"] == "1.84" &&
               rows["Rpcs3VersionTested"] == "0.0.30-16004" &&
               rows["Connects"] == yes &&
               rows["CreateJoinLobbies"] == yes &&
               rows["MatchWithOthers"] == yes &&
               rows["OnlineFeaturesWorking"] == yes &&
               rows["RequiresCustomServers"] == notRequired,
            "Online rows must show tested 1.84, RPCS3 0.0.30-16004, Connects/Lobbies/Match/Features = Yes, custom servers not required.");
    }

    private static void AssertScopeAndAbsence(
        AppFlow flow,
        WikiEvidenceCatalog evidence,
        Rpcs3GameDescriptor game,
        WikiRec rec,
        CfgPlan plan)
    {
        Assert(evidence.Find("BLUS31426", null)?.Page == "Minecraft" &&
               evidence.Find(Serial, "minecraft")?.Page == "Minecraft",
            "A sibling edition serial and a case-different page name must both resolve the Minecraft page.");
        Assert(evidence.Find("BLES02214", "Minecraft Story Mode") is null,
            "A different game must not inherit Minecraft's notes or online evidence.");

        var otherGame = game with { Serial = "BLES02214", Title = "Minecraft Story Mode" };
        var otherModel = flow.GameSettings(plan, otherGame, null, evidence, null);
        Assert(otherModel.Notes.Count == 0 &&
               otherModel.Online.Count == 0 &&
               otherModel.OnlineState == WikiOnlineState.Unknown,
            "No evidence must read as Unknown online state, never NotSupported.");

        UiText.SetLang(UiText.EnLang);
        var emptyPlan = plan with { Items = [], RecState = new RecSum(Serial, 0, 0, 0, 0, 0, 0, []) };
        var noEvidenceVm = new PlanVm();
        noEvidenceVm.Load(emptyPlan, GameSettingsModel.Empty(Serial));
        Assert(noEvidenceVm.ShowEmpty && !noEvidenceVm.Evidence.HasAny,
            "No settings and no evidence must show the empty state and no online/notes cards.");

        var notesOnlyVm = new PlanVm();
        notesOnlyVm.Load(emptyPlan, flow.GameSettings(emptyPlan, game, rec, evidence, null));
        Assert(!notesOnlyVm.ShowEmpty && notesOnlyVm.Evidence.HasNotes && notesOnlyVm.Evidence.HasOnline,
            "A game with Wiki notes but no settings must still show its notes and online evidence.");

        var sparse = new WikiOnlineEvidence(
            "Sparse", WikiOnlineState.Unknown, null, null, null, null, null, null, null, null, null);
        var sparseRows = new GameOnlineVm(sparse).Rows.ToDictionary(row => row.Id, row => row.Value);
        var unknown = UiText.GetText("Online.Value.Unknown");
        Assert(sparseRows["TestedGameVersion"] == unknown &&
               sparseRows["Rpcs3VersionTested"] == unknown &&
               sparseRows["Connects"] == unknown &&
               sparseRows["RequiresCustomServers"] == unknown &&
               sparseRows["State"] == UiText.GetText("Online.State.Unknown") &&
               !sparseRows.ContainsKey("RequiredGameVersion") &&
               !new GameOnlineVm(sparse).ShowTestedHint,
            "Missing online fields must display as Unknown, never No / Not supported.");

        var required = new GameOnlineVm(sparse with { RequiredGameVersion = "1.02", TestedGameVersion = "1.02" });
        Assert(required.Rows.Any(row => row.Id == "RequiredGameVersion" && row.Value == "1.02") &&
               !required.ShowTestedHint,
            "An explicitly stated required version must be shown as required.");

        Assert(SettingsSectionMap.ClassifyRole(new CfgChg(
                   "Core", "PPU Decoder", "x", "ppu decoder", "Optional: something",
                   RecRisk.Required, WikiRecommendationClass.MandatoryGameConfig)) == SettingRole.Required,
            "A mandatory setting must never be downgraded to Optional by note wording.");
    }

    private static void AssertSidecarFailureHandling(AppFlow flow, string root)
    {
        var missingRoot = Path.Combine(root, "Missing");
        var missing = flow.LoadWikiEvidence(missingRoot);
        Assert(missing.Error is null && missing.Catalog.IsEmpty,
            "A missing sidecar is the normal no-evidence state, not an error.");

        var badRoot = Path.Combine(root, "Bad");
        Directory.CreateDirectory(Path.Combine(badRoot, "Data"));
        File.WriteAllText(Path.Combine(badRoot, "Data", WikiEvidenceCatalog.FileName), "{ not json");
        var bad = flow.LoadWikiEvidence(badRoot);
        Assert(bad.Error is not null && bad.Catalog.IsEmpty,
            "An invalid sidecar must report an error and fall back to no evidence without touching settings.");
    }

    private static void AssertCatalogSectionCoverage(RecSvc catalog)
    {
        var total = 0;
        foreach (var change in catalog.Rec.SelectMany(rec => rec.Changes))
        {
            total++;
            var section = SettingsSectionMap.Classify(change, out var fallback);
            Assert(!fallback,
                $"Shipped Wiki key '{change.WikiKey}' has no explicit RPCS3 section.");
            Assert(section is not (Rpcs3SettingsSection.System or Rpcs3SettingsSection.Gui or Rpcs3SettingsSection.Emulator),
                $"'{change.WikiKey}' landed in {section}, a tab with no Wiki setting evidence.");
        }

        Assert(total > 7000,
            "Shipped catalog section coverage must run over every shipped setting.");
    }

    private static GameNote Note(GameSettingsModel model, string fragment) =>
        model.Notes.Single(note =>
            note.Note.Text.Contains(fragment, StringComparison.Ordinal) ||
            note.Note.SourceSection.Contains(fragment, StringComparison.Ordinal));

    private static CfgPlan PlanOf(string serial, string title, IReadOnlyList<CfgChg> changes)
    {
        var items = changes
            .Select(change => new CfgEntry(
                change,
                null,
                new RecItem(
                    serial,
                    change.Path,
                    null,
                    change.Value,
                    change.Risk,
                    RecState.Pending,
                    true,
                    true,
                    false,
                    change.Notes ?? string.Empty,
                    change.Class)))
            .ToArray();

        return new CfgPlan(
            serial,
            title,
            "config.yml",
            string.Empty,
            items,
            new RecSum(serial, items.Length, 0, items.Length, 0, 0, 0, items.Select(item => item.StateIt).ToArray()),
            false);
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx")) &&
                File.Exists(Path.Combine(current.FullName, "App", "Data", "cat.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate R3Config repo root for the Game Settings UI test.");
    }
}
