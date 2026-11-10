using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Runtime render check for the "effective configuration source" line at the
// top of the game Settings tab: one quiet line in the UI direction, above the
// database warning card and the list, with an LTR tip listing database keys.
internal static class CfgPlanSourceUiT
{
    public static void VerifyRuntime(string language, bool isDark)
    {
        var root = Path.Combine(Path.GetTempPath(), $"R3Test-CfgPlanSourceUi-{Guid.NewGuid():N}");
        var plan = new PlanVm();
        plan.Load(DbConfigLayerT.FixturePlan(root));
        plan.Items.Single(item => item.Key == "Write Color Buffers").IsSel = true;

        var view = new R3Config.Ctrl.RecView { DataContext = new Context(plan) };
        var window = new Window
        {
            Content = view,
            Width = 560,
            Height = 720,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        Drain(window.Dispatcher);
        try
        {
            var line = Descendants<TextBlock>(view)
                .Single(item => AutomationProperties.GetAutomationId(item) == "Settings.BootSource");
            var warning = Descendants<Border>(view)
                .Single(item => AutomationProperties.GetAutomationId(item) == "Settings.DbConfigWarning");
            var rtl = UiText.IsRtl;
            Check(line.IsVisible && line.ActualHeight > 0 && line.Text == plan.BootSrcText && line.Text.Length > 0,
                $"{language}: the configuration source line must render the plan's source text.");
            Check(line.FlowDirection == (rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight),
                $"{language}: the configuration source line must follow the UI direction.");
            Check(NoteSvc.ContainsArabic(line.Text) == rtl,
                $"{language}: the configuration source line must be in the UI language.");
            Check(warning.IsVisible && line.TranslatePoint(default, view).Y < warning.TranslatePoint(default, view).Y,
                $"{language}: the configuration source line must sit above the database warning card.");
            Check(line.ActualWidth > 0 && line.ActualWidth <= warning.ActualWidth + 1,
                $"{language}: the configuration source line overflows the settings pane.");

            Check(ToolTipService.GetIsEnabled(line) && line.ToolTip is ToolTip,
                $"{language}: a database source must offer the key tip.");
            var tip = (ToolTip)line.ToolTip;
            tip.PlacementTarget = line;
            tip.IsOpen = true;
            Drain(window.Dispatcher);
            try
            {
                var keys = Descendants<TextBlock>(tip).Single(item => item.Text == plan.BootSrcTip);
                Check(keys.FlowDirection == FlowDirection.LeftToRight && !NoteSvc.ContainsArabic(keys.Text) &&
                      keys.Text.Split('\n').Length == 6,
                    $"{language}: the key tip must list the six database settings LTR ({keys.FlowDirection}).");
            }
            finally
            {
                tip.IsOpen = false;
            }

            Console.WriteLine($"CfgPlanSourceUi {language} dark={isDark} line={line.ActualWidth}x{line.ActualHeight}");

            plan.Clear();
            Drain(window.Dispatcher);
            Check(!line.IsVisible, $"{language}: no configuration source line without a selected game.");
        }
        finally
        {
            window.Close();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // The settings panel binds these two commands of MainVm; inert here.
    private sealed record Context(PlanVm Plan)
    {
        public System.Windows.Input.ICommand ApplyCmd { get; } = new Inert();

        public System.Windows.Input.ICommand DelSet { get; } = new Inert();
    }

    private sealed class Inert : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => false;

        public void Execute(object? parameter)
        {
        }
    }
}
