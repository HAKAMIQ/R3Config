using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Renders the main-window crash notice in ar-SA and en-US through the real
// MainWin resources: it sits under the game identity row, follows the
// interface direction, keeps its text and both actions inside narrow and wide
// hosts, and "Open diagnosis" selects the Runtime tab on its Overview while
// "Dismiss" switches nothing. Settings live under a temp root only.
internal static class LogCrashNoticeUiT
{
    public static void Render()
    {
        var originalLanguage = UiText.CurLang;
        var context = new TestContext();
        var root = context.CreateTempRoot("R3ConfigCrashNoticeUi");
        try
        {
            foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();
                Verify(language, Path.Combine(root, language, "settings.json"));
            }

            Console.WriteLine("Log crash notice render (identity row host, ar-SA RTL + en-US LTR, 560/900 px, Open diagnosis -> Runtime Overview, Dismiss) PASS.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
            context.Cleanup();
        }
    }

    private static void Verify(string language, string settingsPath)
    {
        var settings = new SetSvc(settingsPath);
        var vm = new MainVm(
            new AppRuntimeServices(new AppFlow(AppComposition.Build(new Rpcs3MutationGuard(() => false))), settings),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        var main = new MainWin(vm);
        try
        {
            var host = main.FindName("CrashNoticeHost") as ContentPresenter
                ?? throw new InvalidDataException("The crash notice host was not found in MainWin.");
            var identity = LogicalTreeHelper.GetChildren(host.Parent).OfType<ContentControl>()
                .Single(item => ReferenceEquals(item.ContentTemplate, main.FindResource("GameIdentity")));
            var tabs = main.FindName("Tabs") as TabControl ?? throw new InvalidDataException("Main tabs were not found.");
            var runtimeTab = main.FindName("RtmTab") as TabItem ?? throw new InvalidDataException("The Runtime tab was not found.");
            Assert(ReferenceEquals(host.Parent, identity.Parent) && Grid.GetRow(identity) == 0 && Grid.GetRow(host) == 1 && Grid.GetRow(tabs) == 2,
                $"{language}: the crash notice must sit directly under the game identity row and above the tabs.");
            Assert(ReferenceEquals(host.Content, vm.CrashNotice) || host.GetBindingExpression(ContentPresenter.ContentProperty) is not null,
                $"{language}: the crash notice host must be bound to MainVm.CrashNotice.");

            var template = (DataTemplate)main.FindResource("LogCrashNotice");
            foreach (var width in new[] { 560d, 900d })
            {
                var presenter = new ContentPresenter { Content = vm.CrashNotice, ContentTemplate = template };
                var window = new Window { Content = presenter, Width = width, Height = 240, ShowActivated = false, ShowInTaskbar = false };
                window.SetResourceReference(FrameworkElement.FlowDirectionProperty, "App.FlowDirection");
                window.Show();
                try
                {
                    Settle(window);
                    var notice = All<Border>(presenter).FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == "Shell.LogCrashNotice")
                        ?? throw new InvalidDataException($"{language}: the crash notice Border was not rendered.");
                    Assert(notice.Visibility == Visibility.Collapsed && notice.ActualHeight == 0,
                        $"{language}: without an unseen crash the notice takes no space.");

                    vm.CrashNotice.Show("BLUS90061", "sha256:" + new string('A', 64));
                    Settle(window);
                    var expectedFlow = language == UiText.ArabLang ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                    Assert(notice.IsVisible && notice.FlowDirection == expectedFlow,
                        $"{language} {width}: the notice must be visible and follow the interface direction ({notice.FlowDirection}).");
                    var text = All<TextBlock>(notice).SingleOrDefault(block => block.Text == UiText.GetText("Diag.CrashNotice.Text"))
                        ?? throw new InvalidDataException($"{language}: the crash notice text is not the localized Diag.CrashNotice.Text.");
                    Assert(NoteBidiT.Reversed(text).Count == 0, $"{language}: the notice text reads out of order.");
                    var open = All<Button>(notice).Single(button => AutomationProperties.GetAutomationId(button) == "Shell.LogCrashNotice.Open");
                    var dismiss = All<Button>(notice).Single(button => AutomationProperties.GetAutomationId(button) == "Shell.LogCrashNotice.Dismiss");
                    Assert(Equals(open.Content, UiText.GetText("Diag.CrashNotice.Open")) && Equals(dismiss.Content, UiText.GetText("Diag.CrashNotice.Dismiss")),
                        $"{language}: the notice actions must use the localized Open diagnosis and Dismiss labels.");
                    foreach (var element in new FrameworkElement[] { text, open, dismiss })
                    {
                        var box = element.TransformToAncestor(notice).TransformBounds(new Rect(element.RenderSize));
                        Assert(element.ActualWidth > 0 && element.ActualHeight > 0 && box.Left >= -0.5 && box.Top >= -0.5 &&
                               box.Right <= notice.ActualWidth + 0.5 && box.Bottom <= notice.ActualHeight + 0.5,
                            $"{language} {width}: '{element}' is clipped or outside the notice ({box}, notice {notice.ActualWidth}x{notice.ActualHeight}).");
                    }

                    Assert(notice.ActualWidth <= presenter.ActualWidth + 0.5, $"{language} {width}: the notice overflows its host.");
                    vm.CrashNotice.Hide();
                    Settle(window);
                }
                finally
                {
                    window.Close();
                }
            }

            // Open diagnosis: Runtime tab on its Overview; Dismiss switches nothing.
            tabs.SelectedIndex = 0;
            vm.Runtime.LogDiag.SelectedTab = LogDiagVm.TabEvidence;
            vm.CrashNotice.Show("BLUS90061", "sha256:" + new string('B', 64));
            vm.CrashNotice.OpenCmd.Execute(null);
            Assert(ReferenceEquals(tabs.SelectedItem, runtimeTab) && vm.Runtime.LogDiag.SelectedTab == LogDiagVm.TabOverview && !vm.CrashNotice.IsVisible,
                $"{language}: Open diagnosis must select the Runtime tab and the log diagnosis Overview.");
            tabs.SelectedIndex = 0;
            vm.CrashNotice.Show("BLUS90061", "sha256:" + new string('C', 64));
            vm.CrashNotice.DismissCmd.Execute(null);
            Assert(tabs.SelectedIndex == 0 && !vm.CrashNotice.IsVisible && settings.Load().SeenLogCrash("BLUS90061") == "sha256:" + new string('C', 64),
                $"{language}: Dismiss must hide the notice, keep the current tab and store the session as seen.");
        }
        finally
        {
            main.Close();
            vm.Dispose();
        }
    }

    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
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

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                Walk(VisualTreeHelper.GetChild(node, index));
            }
        }
    }
}
