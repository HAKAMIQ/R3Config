using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// Renders the Domain 5 inner tabs (Overview, Hardware & environment, Log
// diagnostics, Evidence & solutions) against a real diagnosis produced by the
// production engine and the shipped knowledge base, in both languages. WPF
// allows one Application per process, so UiGate.RunUi calls this on its STA
// thread.
internal static class LogDiagViewUiT
{
    public static void Render()
    {
        var originalLanguage = UiText.CurLang;
        var listener = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        Window? window = null;

        try
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Data");
            var knowledge = DiagnosticKnowledgeBase.Load(Path.Combine(root, DiagnosticKnowledgeBase.FileName));
            var builds = Rpcs3BuildCatalog.Load(Path.Combine(root, Rpcs3BuildCatalog.FileName));

            // The documented macOS regression (issue 19381) hit by its exact signature.
            var crash = Event(20, 'F', "SIG", "Thread terminated due to fatal error: sys_ss_random_number_generator(): Failed to generate pseudo-random numbers");
            var minor = Event(30, 'E', "SYS", "'sys_mutex_lock' failed with 0x80010005 : CELL_ESRCH [1]");
            var session = LogSession.Empty with
            {
                Source = new LogSourceInfo(@"C:\Emu\RPCS3\log\RPCS3.log", LogSourceKind.Current, 1234, DateTime.UtcNow),
                Build = new Rpcs3BuildBanner("0.0.42", 19896, "a99fde6f", "Alpha", "master", "arm64"),
                CpuLine = "Apple M4 Max | 16 Threads",
                OsLine = "Operating system: macOS, Version: 15.7.3",
                GpuName = "Apple M4 Max",
                Renderer = "Vulkan",
                GameSerial = "BCUS98242",
                AppVersion = "01.06",
                PpuHashes = ["d39f3ed2aabbccddeeff00112233445566778899"],
                Settings = [new LogSetting("Video/Renderer", "Vulkan"), new LogSetting("Core/PPU Decoder", "Recompiler (LLVM)")],
                Events = [crash, minor],
                Timeline =
                [
                    new LogTimelineStep("GameIdentified", 5, 1, "BCUS98242"),
                    new LogTimelineStep("RendererInitialization", 10, 2, "Vulkan"),
                    new LogTimelineStep("Problem:FatalError", 20, 5, null)
                ],
                RawEventLines = ["20: SIG: Thread terminated due to fatal error: sys_ss_random_number_generator()", "30: SYS: 'sys_mutex_lock' failed"],
                TotalLines = 100,
                BootCount = 1,
                Run = new LogRunContext(
                    "4.87", "552.0.0.0", "01.00", "custom", LogBootMedia.IsoArchive, LogIsoKeyState.Missing, "NONE",
                    [new LogAppliedPatch("PPU-a1f33132af387c49d452e64fcb0704cb06b6d084", "Disable Motion blur")], 2, true)
            };
            var environment = new DiagnosticEnvironment("BCUS98242", "01.06", [], 19896, "macOS", "arm64", null, "Vulkan");
            var known = new LogDiagnosticsResult(DiagnosticEngine.Evaluate(session, environment, knowledge), [session], [], null);
            var unknownSession = session with { Events = [Event(20, 'E', "Foo", "never seen anywhere")] };
            var unknown = new LogDiagnosticsResult(
                DiagnosticEngine.Evaluate(unknownSession, environment, DiagnosticKnowledgeBase.Empty), [unknownSession], ["LogFromOtherBuild"], null);
            Assert(known.Report.Primary!.Kind == DiagnosisKind.KnownRegressionMatch && unknown.Report.Primary!.Kind == DiagnosisKind.UnknownCause,
                "The render fixtures must be a real known-regression diagnosis and a real unknown-cause diagnosis.");

            UiText.SetLang(UiText.EnLang);
            UiLaySvc.Apply();
            var vm = new RuntimeVm();
            vm.LogDiag.SetContext(RuntimeContextSnapshot.Empty, builds, "Windows");
            var view = new R3Config.Ctrl.RuntimeView { DataContext = new Host(vm), Width = 900, Height = 900 };
            window = new Window { Content = view, Width = 920, Height = 940 };
            window.Show();
            Settle(window);

            // Before any analysis.
            Assert(Texts(view).Contains(UiText.GetText("Diag.State.NotRun")) && vm.LogDiag.SelectedTab == LogDiagVm.TabOverview,
                "Before an analysis the Overview says the log has not been analyzed and starts on the Overview tab.");
            Assert(Tabs(view).SequenceEqual(["Overview", "Hardware & environment", "Log diagnostics", "Evidence & solutions"]),
                "Domain 5 exposes exactly the four conceptual views as inner tabs.");
            vm.LogDiag.MarkRunning();
            Settle(window);
            Assert(Texts(view).Contains("Reading the RPCS3 log..."), "A running analysis is shown.");
            vm.LogDiag.Load(new LogDiagnosticsResult(DiagnosticReport.Empty, [], ["NoLogFound"], null));
            Settle(window);
            Assert(Texts(view).Contains("No RPCS3 log was found for this installation."), "A missing log is reported plainly.");

            // Known regression: Overview.
            vm.LogDiag.Load(known);
            Settle(window);
            var overview = Texts(view);
            foreach (var expected in new[]
                     {
                         "Detected symptom", "Fatal error", "Known regression match - GitHub issue #19381", "Affected builds", "19896 - 19911",
                         "Fixed in build", "19912", "Confidence", "Documented action", "Test the documented fixed build 19912", "View evidence",
                         "Download exact build", "Game", "BCUS98242  01.06"
                     })
            {
                Assert(overview.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"The Overview must render '{expected}'. Rendered: " + string.Join(" / ", overview));
            }

            var englishEvidence = All<TextBlock>(view).Where(block => block.IsVisible && R3Config.Tests.NoteBidiT.Shown(block) == "Known regression match - GitHub issue #19381").ToList();
            Assert(englishEvidence.Count == 1 && englishEvidence[0].Inlines.Count == 1 &&
                   englishEvidence[0].Inlines.FirstInline is Run englishRun &&
                   englishRun.ReadLocalValue(Inline.FlowDirectionProperty) == DependencyProperty.UnsetValue &&
                   R3Config.Tests.NoteBidiT.Reversed(englishEvidence[0]).Count == 0,
                "The English Overview evidence line stays one plain Run that reads in order.");
            var links = All<TextBlock>(view).SelectMany(block => block.Inlines.OfType<Hyperlink>()).ToList();
            var evidence = links.SingleOrDefault(link => link.Inlines.OfType<Run>().Any(run => run.Text == "View evidence"));
            var download = links.SingleOrDefault(link => link.Inlines.OfType<Run>().Any(run => run.Text == "Download exact build"));
            Assert(evidence?.NavigateUri?.AbsoluteUri == "https://github.com/RPCS3/rpcs3/issues/19381" &&
                   download is { NavigateUri: { } downloadUri } && downloadUri.AbsoluteUri == builds.ResolveBuildNumber(19912, "Windows").DownloadUrl &&
                   downloadUri.Host == "github.com" && !links.Any(link => link.NavigateUri?.Host is "archive.org" or "rpcs3.net"),
                "'View evidence' opens the documented GitHub issue and 'Download exact build' the exact official binary of the documented fixed build.");

            // Log diagnostics tab.
            vm.LogDiag.SelectedTab = LogDiagVm.TabLogs;
            Settle(window);
            var logs = Texts(view);
            foreach (var expected in new[]
                     {
                         "Current log: RPCS3.log", "Serial", "BCUS98242", "APP_VER", "01.06", "PPU hash", "RPCS3 build", "0.0.42-19896", "Renderer", "Vulkan",
                         "PS3 firmware", "4.87", "GPU driver", "552.0.0.0", "Configuration used", "custom", "A custom configuration file was loaded",
                         "Boot source", "ISO archive", "ISO key file", "Not found (matters only for an encrypted ISO)",
                         "Patches applied by RPCS3", "Disable Motion blur",
                         "Timeline", "Game identified", "Renderer initialization", "Fatal error", "Observed problem events",
                         "Thread terminated due to fatal error: sys_ss_random_number_generator(): Failed to generate pseudo-random numbers"
                     })
            {
                Assert(logs.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"The Log diagnostics tab must render '{expected}'. Rendered: " + string.Join(" / ", logs));
            }

            Assert(!logs.Any(text => text.Contains("'sys_mutex_lock' failed", StringComparison.Ordinal)) &&
                   !logs.Any(text => text.StartsWith("20: SIG", StringComparison.Ordinal)),
                "Minor HLE failures and raw lines stay behind Details; the main list is only the meaningful events.");
            var expander = All<Expander>(view).Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "Runtime.Logs.Details");
            Assert(!expander.IsExpanded, "Details start collapsed.");
            expander.IsExpanded = true;
            Settle(window);
            logs = Texts(view);
            Assert(logs.Any(text => text.Contains("'sys_mutex_lock' failed", StringComparison.Ordinal)) &&
                   logs.Any(text => text.StartsWith("20: SIG", StringComparison.Ordinal)) &&
                   logs.Contains("Video/Renderer") && logs.Contains("Recompiler (LLVM)"),
                "Details reveal the minor events, the raw lines and the settings RPCS3 printed.");
            expander.IsExpanded = false;

            // Evidence & solutions tab.
            vm.LogDiag.SelectedTab = LogDiagVm.TabEvidence;
            Settle(window);
            var evidenceTexts = Texts(view);
            foreach (var expected in new[]
                     {
                         "Known regression match", "GitHub issue #19381", "Evidence class", "Developer-confirmed fix", "Match quality", "Exact log signature",
                         "Applies to", "macOS | arm64", "Documented fixed build", "19912", "Documented last working build", "19894", "Download exact build"
                     })
            {
                Assert(evidenceTexts.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"The Evidence tab must render '{expected}'. Rendered: " + string.Join(" / ", evidenceTexts));
            }

            // Unknown cause: never a fabricated diagnosis.
            vm.LogDiag.Load(unknown);
            vm.LogDiag.SelectedTab = LogDiagVm.TabOverview;
            Settle(window);
            var unknownTexts = Texts(view);
            Assert(unknownTexts.Any(text => text.Contains("Unknown - no documented evidence", StringComparison.Ordinal)) &&
                   unknownTexts.Contains("No documented solution found.") &&
                   unknownTexts.Contains("Documented action") &&
                   !unknownTexts.Any(text => text.Contains("Test the documented fixed build", StringComparison.Ordinal)) &&
                   !All<TextBlock>(view).SelectMany(block => block.Inlines.OfType<Hyperlink>()).Any(),
                "With no documented link the Overview says the cause is unknown, offers no fix, no evidence link and no download.");
            vm.LogDiag.SelectedTab = LogDiagVm.TabLogs;
            Settle(window);
            Assert(Texts(view).Contains("The log is from a different RPCS3 build than the installed one."),
                "A log from another build than the installed one is called out.");

            // Arabic.
            UiText.SetLang(UiText.ArabLang);
            UiLaySvc.Apply();
            vm.SetLang();
            vm.LogDiag.Load(known);
            foreach (var (tab, expectedTexts) in new (int, string[])[]
                     {
                         (LogDiagVm.TabOverview, ["العرض المكتشف", "خطأ فادح", "الإجراء الموثق", "عرض الدليل"]),
                         (LogDiagVm.TabLogs, ["الخط الزمني", "أحداث المشكلات المرصودة", "التفاصيل", "فيرموير PS3", "الإعدادات المستخدمة", "باتشات طبقها RPCS3", "غير موجود (يهم فقط مع ISO مشفر)"]),
                         (LogDiagVm.TabEvidence, ["مطابقة لتراجع معروف", "فئة الدليل", "إصلاح مؤكد من المطورين", "توقيع سجل مطابق تماما"])
                     })
            {
                vm.LogDiag.SelectedTab = tab;
                Settle(window);
                var arabic = Texts(view);
                foreach (var expected in expectedTexts)
                {
                    Assert(arabic.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                        $"The Arabic tab {tab} must render '{expected}'. Rendered: " + string.Join(" / ", arabic));
                }

                Assert(arabic.All(text => !text.Contains('\uFFFD') && !text.StartsWith("Diag.", StringComparison.Ordinal) && !text.StartsWith("Runtime.", StringComparison.Ordinal)),
                    "Arabic diagnostics text must have no mojibake and no unresolved resource keys.");
            }

            // Round 21 (B2): the Overview "Evidence" row ends with the GitHub
            // issue number; under Arabic it must read "GitHub #19381", never
            // "19381#", with the source text and automation name unchanged.
            vm.LogDiag.SelectedTab = LogDiagVm.TabOverview;
            Settle(window);
            var arabicEvidence = EvidenceLine(DiagnosisKind.KnownRegressionMatch, 19381);
            var arabicRows = All<TextBlock>(view).Where(block => block.IsVisible && R3Config.Tests.NoteBidiT.Shown(block) == arabicEvidence).ToList();
            Assert(arabicRows.Count == 1, $"The Arabic Overview must render the evidence line '{arabicEvidence}' once ({arabicRows.Count}).");
            var evidenceProblems = new List<string>();
            CheckEvidenceLine(arabicRows[0], arabicEvidence, 19381, evidenceProblems);
            CheckEvidenceLines(view, knowledge, evidenceProblems);
            Assert(evidenceProblems.Count == 0,
                $"Arabic diagnosis evidence lines: {evidenceProblems.Count} problem(s):{Environment.NewLine}{string.Join(Environment.NewLine, evidenceProblems.Take(12))}");

            vm.LogDiag.SelectedTab = LogDiagVm.TabLogs;
            Settle(window);
            foreach (var technical in new[] { "0.0.42-19896", "BCUS98242", "Vulkan" })
            {
                var block = Blocks(view).FirstOrDefault(item => item.Text == technical);
                Assert(block is not null && block.FlowDirection == FlowDirection.LeftToRight,
                    $"Technical value '{technical}' must render and stay left-to-right in the Arabic Log diagnostics tab.");
            }

            Assert(Blocks(view).Any(block => block.Text.Contains("Thread terminated due to fatal error", StringComparison.Ordinal) &&
                                             block.FlowDirection == FlowDirection.LeftToRight),
                "Raw log text stays left-to-right under Arabic.");

            Assert(listener.Errors.Count == 0, "Log diagnostics view produced data-binding errors: " + string.Join(" | ", listener.Errors));
            Console.WriteLine("Log diagnostics view render (4 inner tabs, known regression, unknown cause, EN + AR, Details, safe links) PASS.");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            window?.Close();
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
        }
    }

    private static LogEvent Event(int line, char level, string channel, string message) =>
        new(line, line, line, level, null, channel, message, [.. LogRules.Classify(level, channel, message)], LogRules.BuildSignature(level, channel, message));

    private static string EvidenceLine(DiagnosisKind kind, int number) =>
        $"{UiText.GetText($"Diag.Kind.{kind}")} - {UiText.GetText("Diag.Source.GitHubIssue")} #{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    // Every GitHub issue number of the shipped knowledge base, with every
    // diagnosis kind, through the real DiagLineTemplate (LogDiagVm builds the
    // Overview evidence line the same way).
    private static void CheckEvidenceLines(FrameworkElement view, DiagnosticKnowledgeBase knowledge, List<string> problems)
    {
        var template = (DataTemplate)view.FindResource("DiagLineTemplate");
        var numbers = knowledge.Entries.Select(entry => entry.Source.Number).OfType<int>().Distinct().Order().ToList();
        Assert(numbers.Count > 0, "The shipped knowledge base must have GitHub issue numbers.");
        var lines = numbers.SelectMany(number => Enum.GetValues<DiagnosisKind>().Select(kind => (Number: number, Text: EvidenceLine(kind, number)))).ToList();
        var panel = new StackPanel();
        foreach (var line in lines)
        {
            panel.Children.Add(new ContentPresenter
            {
                Content = new DiagLineVm(UiText.GetText("Diag.Overview.Evidence"), line.Text, false),
                ContentTemplate = template
            });
        }

        var host = new Window
        {
            Content = new ScrollViewer { Content = panel, FlowDirection = view.FlowDirection },
            Width = 920,
            Height = 900,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        host.Show();
        try
        {
            Settle(host);
            foreach (var line in lines)
            {
                var shown = All<TextBlock>(panel).Where(block => block.IsVisible && R3Config.Tests.NoteBidiT.Shown(block) == line.Text).ToList();
                if (shown.Count != 1)
                {
                    problems.Add($"'{line.Text}' rendered {shown.Count} times.");
                    continue;
                }

                CheckEvidenceLine(shown[0], line.Text, line.Number, problems);
            }
        }
        finally
        {
            host.Close();
        }

        Console.WriteLine($"LogDiagEvidenceBidi ar-SA lines={lines.Count} issues={numbers.Count}");
    }

    private static void CheckEvidenceLine(TextBlock block, string text, int number, List<string> problems)
    {
        var issue = $"#{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (text.Any(character => char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.Format))
        {
            problems.Add($"'{text}': the source line must have no format character.");
        }

        var name = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(block)?.GetName();
        if (name != text)
        {
            problems.Add($"'{text}': automation name is '{name}'.");
        }

        if (!R3Config.Tests.NoteBidiT.Segments(block).Any(segment => segment.Text.EndsWith(issue, StringComparison.Ordinal)))
        {
            problems.Add($"'{text}': no English segment ends with {issue}.");
        }

        var reversed = R3Config.Tests.NoteBidiT.Reversed(block);
        if (reversed.Count > 0)
        {
            problems.Add($"'{text}' reads out of order: {string.Join(" | ", reversed)}");
        }
    }

    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
        }
    }

    private static List<string> Tabs(DependencyObject root) =>
        [.. All<TabItem>(root).Where(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item).StartsWith("Runtime.", StringComparison.Ordinal))
            .Select(item => item.Header as string ?? string.Empty)];

    private static List<TextBlock> Blocks(DependencyObject root)
    {
        var found = new List<TextBlock>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
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
        Blocks(root).Select(Plain).Where(text => text.Length > 0).ToList();

    private sealed class Host(RuntimeVm runtime)
    {
        public RuntimeVm Runtime { get; } = runtime;
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
}
