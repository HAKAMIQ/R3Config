using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// Renders the real Runtime Environment & Compatibility view (Ctrl/Rtm.xaml) in
// both languages and at a normal and a narrow size: labels, states, LTR
// technical values, collapsed details, the empty state and binding health.
// WPF allows one Application per process, so UiGate.RunUi (which owns the App
// for the visual gates) calls this on its STA thread.
internal static class RuntimeViewUiT
{
    private const string Serial = "TEST00003";

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
            var hardware = new HwProbe(
                readCpuName: () => "Test CPU 9000",
                readLogicalProcessors: () => 16,
                readTotalMemoryBytes: () => 32UL * 1024 * 1024 * 1024,
                readGpuName: () => "NVIDIA GeForce Test",
                readGpuAdapters: () => [new GpuAdapterInfo("NVIDIA GeForce Test", HwVendor.Nvidia, "10DE", "2504", true, true)],
                readCpuVendor: () => (HwVendor.Intel, "GenuineIntel"),
                readOs: () => ("Windows 10.0.26200", "26200", "X64", "X64")).Probe();
            var install = new Rpcs3InstallDescriptor(
                @"C:\Emu\RPCS3", @"C:\Emu\RPCS3\rpcs3.exe", @"C:\Emu\RPCS3\config", @"C:\Emu\RPCS3\config\custom",
                @"C:\Emu\RPCS3\patches", @"C:\Emu\RPCS3\config\patch_config.yml", @"C:\Emu\RPCS3");
            var partial = new Rpcs3BuildIdentity("0.0.42", 19843, null, null, null, null, Rpcs3BuildIdentityState.Partial, []);
            var game = new Rpcs3GameDescriptor(Serial, "Synthetic Game", string.Empty, "1.05", null, null);
            var snapshot = RuntimeContextBuilder.Build(hardware, install, partial, game, null, null);

            UiText.SetLang(UiText.EnLang);
            UiLaySvc.Apply();
            var vm = new RuntimeVm();
            vm.Load(RuntimeContextSnapshot.Empty);
            var view = new R3Config.Ctrl.RuntimeView
            {
                DataContext = new Host(vm),
                Width = 900,
                Height = 900
            };
            window = new Window { Content = view, Width = 920, Height = 940 };
            window.Show();

            // The hardware/environment groups live on the second inner tab.
            vm.LogDiag.SelectedTab = LogDiagVm.TabHardware;
            Settle(window);

            var empty = Texts(view);
            Assert(empty.Contains(UiText.GetText("Runtime.Empty")) && !Blocks(view).Any(block => block.Text == "CPU"),
                "Before any analysis the Runtime view shows only its empty state.");

            vm.Load(snapshot);
            Settle(window);
            var english = Texts(view);
            foreach (var expected in new[]
                     {
                         "Hardware", "CPU", "Test CPU 9000", "GPU", "NVIDIA GeForce Test (NVIDIA)", "Memory", "32 GB", "Windows 10.0.26200",
                         "RPCS3 installation", "RPCS3 executable", @"C:\Emu\RPCS3\rpcs3.exe", "RPCS3 build", "Partial",
                         "The build identity is incomplete", "Selected game", "Serial", Serial, "Installed version (APP_VER)", "1.05",
                         "Game identity", "Documented evidence", "Details"
                     })
            {
                Assert(english.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"English Runtime view must render '{expected}'. Rendered: " + string.Join(" / ", english));
            }

            Assert(!english.Contains(UiText.GetText("Runtime.Empty")) &&
                   !english.Any(text => text is "Setting" or "Role" or "Recommended value"),
                "The populated Runtime view hides the empty state and shows no settings column titles.");
            Assert(!english.Any(text => text is "Logical processors" or "Commit" or "Branch"),
                "Secondary evidence stays behind the collapsed Details toggles.");

            var expanders = All<Expander>(view).ToList();
            Assert(expanders.Count >= 4 && expanders.All(expander => !expander.IsExpanded),
                "Every group keeps its Details toggle collapsed by default.");
            foreach (var expander in expanders)
            {
                expander.IsExpanded = true;
            }

            Settle(window);
            english = Texts(view);
            Assert(english.Contains("Logical processors") && english.Contains("16") && english.Contains("Build number") && english.Contains("19843"),
                "Expanding the details reveals the secondary evidence.");
            foreach (var expander in expanders)
            {
                expander.IsExpanded = false;
            }

            Settle(window);

            // Technical values are LTR in both languages; labels follow the shell.
            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();
                vm.SetLang();
                Settle(window);
                var blocks = Blocks(view);
                foreach (var technical in new[] { "Test CPU 9000", "NVIDIA GeForce Test (NVIDIA)", "32 GB", "Windows 10.0.26200", @"C:\Emu\RPCS3\rpcs3.exe", Serial })
                {
                    var block = blocks.FirstOrDefault(item => item.Text == technical);
                    Assert(block is not null && block.FlowDirection == FlowDirection.LeftToRight,
                        $"Runtime technical value '{technical}' must render and stay left-to-right under {language}.");
                }

                Assert(blocks.All(block => !block.Text.Any(ch => ch >= '٠' && ch <= '٩')),
                    "The Runtime view must use Western digits 0-9 in Arabic.");
            }

            var arabic = Texts(view);
            foreach (var expected in new[] { "العتاد", "المعالج", "بطاقة الرسوميات", "الذاكرة", "تثبيت RPCS3", "جزئي", "اللعبة المحددة", "الأدلة الموثقة", "هوية النسخة غير مكتملة" })
            {
                Assert(arabic.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                    $"Arabic Runtime view must render '{expected}'. Rendered: " + string.Join(" / ", arabic));
            }

            Assert(arabic.All(text => !text.Contains('\uFFFD') && !text.StartsWith("Runtime.", StringComparison.Ordinal)),
                "Arabic Runtime view must have no mojibake or unresolved resource keys.");

            var evidenceTimer = Stopwatch.StartNew();
            // Source-separated RPCN evidence (Domain 5 evidence slice): an official
            // dated browser observation and a community-only claim, both from the
            // shipped artifact, rendered in both languages with LTR raw values.
            var evidence = DiagnosticEvidenceCatalog.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", DiagnosticEvidenceCatalog.FileName));
            var page = evidence.Records
                .Where(record => record.Applicability == EvidenceApplicability.GameScoped)
                .GroupBy(record => record.Subject.WikiPage!, StringComparer.Ordinal)
                .First(group => group.Any(record => record.Role == EvidenceSourceRole.OfficialCurrentBrowser) &&
                                group.Any(record => record.Claim == EvidenceClaimType.CommunityWorkingOnline && record.Value is not null))
                .ToArray();
            var commId = page.First(record => record.Claim == EvidenceClaimType.CurrentBrowserListing).Subject.CommId!;
            var community = page.First(record => record.Claim == EvidenceClaimType.CommunityWorkingOnline).Value!;
            var evidenceSnapshot = RuntimeContextBuilder.Build(
                hardware, install, partial, game,
                new GameSettingsModel(Serial, page[0].Subject.WikiPage, [], [], []),
                null,
                evidence: evidence);

            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();
                vm.SetLang();
                vm.Load(evidenceSnapshot);
                Settle(window);
                var rendered = Texts(view);
                var expectedLabels = language == UiText.EnLang
                    ? new[] { "Official RPCN browser listing", "Community private-server index", "not official evidence", "Partial" }
                    : new[] { "إدراج اللعبة في متصفح RPCN الرسمي", "فهرس الخوادم الخاصة المجتمعي", "ليس دليلا رسميا", "جزئي" };
                foreach (var expected in expectedLabels)
                {
                    Assert(rendered.Any(text => text.Contains(expected, StringComparison.Ordinal)),
                        $"The RPCN evidence rows must render '{expected}' under {language}. Rendered: " + string.Join(" / ", rendered));
                }

                var blocks = Blocks(view);
                Assert(blocks.Any(block => block.Text.StartsWith(commId, StringComparison.Ordinal) && block.FlowDirection == FlowDirection.LeftToRight) &&
                       blocks.Any(block => block.Text == community && block.FlowDirection == FlowDirection.LeftToRight),
                    $"The CommId and the community value are raw technical text and stay left-to-right under {language}.");
                Assert(rendered.All(text => !text.StartsWith("Runtime.", StringComparison.Ordinal) && !text.Contains('\uFFFD')),
                    $"The RPCN evidence rows must have no unresolved resource keys or mojibake under {language}.");
            }

            UiText.SetLang(UiText.EnLang);
            UiLaySvc.Apply();
            vm.SetLang();

            Console.WriteLine($"Runtime view RPCN evidence rows rendered in {evidenceTimer.ElapsedMilliseconds} ms (EN + AR).");

            // Narrow window: the content scrolls vertically only.
            window.Width = 560;
            window.Height = 460;
            view.Width = 540;
            view.Height = 440;
            Settle(window);
            var scroll = All<ScrollViewer>(view).Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "Runtime.Scroll");
            Assert(scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled &&
                   scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                "The Runtime view must not need horizontal scrolling at a narrow width.");

            Assert(listener.Errors.Count == 0,
                "Runtime view produced data-binding errors: " + string.Join(" | ", listener.Errors));
            Console.WriteLine("Runtime view render (EN + AR: groups, states, LTR technical values, collapsed details, empty state) PASS.");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            window?.Close();
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
        }
    }

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

    private static List<string> Texts(DependencyObject root) =>
        Blocks(root).Select(block => block.Text).Where(text => text.Length > 0).ToList();

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
