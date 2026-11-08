using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using R3Config.Lang;
using R3Config.Vm;

namespace R3Config.Tests;

// Runtime render check for the G1 database-config warning card in the game
// settings panel: visible only while a write is selected, title follows the
// UI direction, every technical line stays LTR and inside the card.
internal static class DbWarnUiT
{
    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var root = Path.Combine(Path.GetTempPath(), $"R3Test-DbWarnUi-{Guid.NewGuid():N}");
        var plan = new PlanVm();
        plan.Load(DbConfigLayerT.FixturePlan(root));
        plan.ClearSel();

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
            var card = Descendants<Border>(view)
                .Single(item => AutomationProperties.GetAutomationId(item) == "Settings.DbConfigWarning");
            Check(card.Visibility != Visibility.Visible,
                $"{language}: the database warning must stay hidden while nothing is selected.");

            plan.Items.Single(item => item.Key == "Write Color Buffers").IsSel = true;
            Drain(window.Dispatcher);
            Check(card.Visibility == Visibility.Visible && card.ActualHeight > 0,
                $"{language}: the database warning must show once a write is selected.");

            var texts = Descendants<TextBlock>(card).Where(text => text.Text.Length > 0).ToArray();
            var title = texts.Single(text => text.Text == plan.DbWarnTitle);
            var lines = texts.Where(text => plan.DbWarnLines.Contains(text.Text)).ToArray();
            var rtl = UiText.IsRtl;
            Check(title.FlowDirection == (rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight),
                $"{language}: the warning title must follow the UI direction.");
            Check(lines.Length == 4 && lines.All(line => line.FlowDirection == FlowDirection.LeftToRight),
                $"{language}: every lost-setting line must render LTR ({lines.Length} lines).");
            Check(lines.Append(title).All(text => text.ActualWidth > 0 && text.ActualWidth <= card.ActualWidth),
                $"{language}: warning text overflows the card.");
            Console.WriteLine($"DbWarnUi {language} dark={isDark} card={card.ActualWidth}x{card.ActualHeight} lines={lines.Length}");
            Check(Visible(view, "Settings.DbConfigKeepTitle") is null && Visible(view, "Settings.DbConfigSelectKeep") is null &&
                  Visible(view, "Settings.DbConfigLostTitle") is TextBlock { Text: { Length: > 0 } },
                $"{language}: with no keepable setting only the 'cannot write' group renders.");

            // Round 12: (a) keepable rows with the select button, (b+d) lost rows.
            var keepPlan = new PlanVm();
            keepPlan.Load(DbConfigLayerT.FixturePlan(Path.Combine(root, "Keep"), withKeeper: true));
            keepPlan.Select();
            var keeper = keepPlan.Items.Single(item => item.Key == "Relaxed ZCULL Sync");
            Check(!keeper.IsSel, $"{language}: automatic selection must not select a database-keeping row.");
            keepPlan.ClearSel();
            keepPlan.Items.Single(item => item.Key == "Write Color Buffers").IsSel = true;
            view.DataContext = new Context(keepPlan);
            Drain(window.Dispatcher);
            card = Descendants<Border>(view)
                .Single(item => AutomationProperties.GetAutomationId(item) == "Settings.DbConfigWarning");
            var keepTitle = Visible(view, "Settings.DbConfigKeepTitle") as TextBlock;
            var lostTitle = Visible(view, "Settings.DbConfigLostTitle") as TextBlock;
            var select = Visible(view, "Settings.DbConfigSelectKeep") as Button;
            var keepLines = Descendants<TextBlock>(card).Where(text => text.IsVisible && keepPlan.DbKeepLines.Contains(text.Text)).ToArray();
            var lostLines = Descendants<TextBlock>(card).Where(text => text.IsVisible && keepPlan.DbLostLines.Contains(text.Text)).ToArray();
            Check(keepTitle?.Text == UiText.GetText("Plan.DbWarnKeep") && lostTitle?.Text == UiText.GetText("Plan.DbWarnLost") &&
                  keepLines.Length == 1 && lostLines.Length == 3 &&
                  keepLines.Concat(lostLines).All(line => line.FlowDirection == FlowDirection.LeftToRight) &&
                  keepTitle.FlowDirection == (rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight) &&
                  select is { IsEnabled: true } && select.Content as string == UiText.GetText("Plan.DbWarnSelectKeep") &&
                  !keeper.IsSel,
                $"{language}: the warning must render (a) {keepLines.Length} and (b+d) {lostLines.Length} groups with the select button, nothing selected.");
            Check(new FrameworkElement[] { keepTitle!, lostTitle!, select! }.Concat(keepLines).Concat(lostLines)
                      .All(item => item.ActualWidth > 0 && item.ActualWidth <= card.ActualWidth),
                $"{language}: grouped warning content overflows the card.");

            if (!string.IsNullOrWhiteSpace(shotDirectory))
            {
                Shot(view, shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-DbConfigWarningGroups.png");
            }

            // A real click path: the automation peer invokes the button and its Command.
            ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(select!)
                .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
            Drain(window.Dispatcher);
            Check(keeper.IsSel &&
                  keepPlan.Items.Where(item => item.IsSel).Select(item => item.Key).Order(StringComparer.Ordinal)
                      .SequenceEqual(["Relaxed ZCULL Sync", "Write Color Buffers"], StringComparer.Ordinal) &&
                  Visible(view, "Settings.DbConfigKeepTitle") is null && Visible(view, "Settings.DbConfigSelectKeep") is null,
                $"{language}: Select database settings must select only the (a) rows, after which the (a) group disappears.");
            view.DataContext = new Context(plan);
            Drain(window.Dispatcher);

            if (!string.IsNullOrWhiteSpace(shotDirectory))
            {
                Directory.CreateDirectory(shotDirectory);
                var bitmap = new RenderTargetBitmap(
                    (int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(
                    shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-DbConfigWarning.png"));
                encoder.Save(stream);
            }
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

    private static FrameworkElement? Visible(DependencyObject root, string id) =>
        Descendants<FrameworkElement>(root)
            .FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == id && item.IsVisible);

    private static void Shot(FrameworkElement view, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
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
