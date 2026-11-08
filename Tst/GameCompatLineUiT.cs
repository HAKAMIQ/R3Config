using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Runtime render check for the game-row compatibility line: rows with each
// official RPCS3 status, an unrecognized status, rows without data and a very
// long title keep one-line technical text, stable row heights and no clipped
// title. Round 21 (B3): an official status (Playable, Ingame, Intro, Loadable,
// Nothing) shows without the "Compatibility:" prefix, its name in the
// Compat.<status> theme brush (WCAG AA on every row background) and the date
// after it; the full localized line stays the automation name. An
// unrecognized status keeps the previous line unchanged.
internal static class GameCompatLineUiT
{
    private const string LongTitle =
        "Metal Gear Solid 4: Guns of the Patriots - Limited Collector's Edition with Extra Long Title Suffix";

    private const string Date = "2026-10-23";

    internal static readonly string[] OfficialStatuses = ["Playable", "Ingame", "Intro", "Loadable", "Nothing"];

    // Every background a game row can have: the window, the rail, hover,
    // selected and selected-inactive.
    private static readonly string[] RowBackgrounds =
        ["Surface.Background", "Surface.Subtle", "Surface.CardHover", "Selection.Default", "Selection.Inactive"];

    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var rows = new[]
        {
            Row("BCES00009", "RIDGE RACER 7", Compat("BCES00009", "Playable")),
            Row("BCES00011", "Ingame Status", Compat("BCES00011", "Ingame")),
            Row("BCES00012", "Intro Status", Compat("BCES00012", "Intro")),
            Row("BCES00013", "Loadable Status", Compat("BCES00013", "Loadable")),
            Row("BCES00014", "Nothing Status", Compat("BCES00014", "Nothing")),
            Row("BCES00015", "Unrecognized Status", Compat("BCES00015", "Unknown")),
            Row("BLUS00001", "No Compat Data", null),
            Row("BLUS00002", "Another No Compat", null),
            Row("BCES00010", LongTitle, Compat("BCES00010", "Playable"))
        };

        var list = new ListVm();
        list.Load(rows);
        var view = new R3Config.Ctrl.GameList { DataContext = new Context(list) };
        var window = new Window
        {
            Content = view,
            Width = 360,
            Height = 900,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        Drain(window.Dispatcher);
        try
        {
            var box = Descendants<ListBox>(view).First(item => item.Items.Count == rows.Length);
            var heights = new Dictionary<string, double>();
            var colors = new Dictionary<string, Color>();
            for (var i = 0; i < rows.Length; i++)
            {
                var container = box.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem
                    ?? throw new InvalidOperationException($"Row {i} was not realized.");
                var row = container.DataContext as GameVm
                    ?? throw new InvalidOperationException($"Row {i} has no GameVm.");
                heights[row.Serial] = container.ActualHeight;
                static string Shown(TextBlock text) => text.Inlines.Count > 1 ? string.Concat(text.Inlines.OfType<Run>().Select(run => run.Text)) : text.Text;
                var texts = Descendants<TextBlock>(container)
                    .Where(text => text.DataContext == row && text.Name.Length == 0)
                    .ToArray();
                var serial = texts.Single(text => text.Text == row.Serial);
                var title = texts.Single(text => text.Text == row.Title);
                var shown = texts
                    .Where(text => text != serial && text != title && Shown(text).Length > 0 && !text.FontFamily.Source.Contains("Icons", StringComparison.Ordinal) && text.Visibility == Visibility.Visible && text.ActualHeight > 0)
                    .ToArray();
                Check(title.ActualWidth > 0 && title.ActualWidth <= container.ActualWidth,
                    $"{language} row {i}: title overflows its row.");
                if (row.Compatibility is null)
                {
                    Check(shown.Length == 0,
                        $"{language} row {i}: a row without compatibility data shows '{string.Join(" | ", shown.Select(text => text.Text))}'.");
                    continue;
                }

                Check(shown.Length == 1, $"{language} row {i}: expected one compatibility line, got {shown.Length}.");
                var line = shown[0];
                var status = row.Compatibility.Status;
                var full = UiText.Fmt("Games.CompatibilityLine", status, Date);
                var name = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(line)?.GetName();
                var lineColor = ((SolidColorBrush)line.Foreground).Color;
                if (OfficialStatuses.Contains(status, StringComparer.Ordinal))
                {
                    Check(Shown(line) == $"{status} ({Date})",
                        $"{language} row {i}: compatibility text '{Shown(line)}' must be '{status} ({Date})' (no prefix).");
                    Check(name == full, $"{language} row {i}: automation name '{name}' must stay '{full}'.");
                    Check(line.ToolTip as string == full, $"{language} row {i}: the ToolTip must stay '{full}', got '{line.ToolTip}'.");
                    var runs = line.Inlines.OfType<Run>().ToArray();
                    Check(runs.Length == 2 && runs[0].Text == status && runs[1].Text == $" ({Date})",
                        $"{language} row {i}: the status name must be its own Run before the date.");
                    var brush = (SolidColorBrush)view.FindResource($"Compat.{status}");
                    Check(runs[0].Foreground is SolidColorBrush statusBrush && statusBrush.Color == brush.Color,
                        $"{language} dark={isDark} row {i}: {status} must use the Compat.{status} brush.");
                    Check(runs[1].Foreground is SolidColorBrush dateBrush && dateBrush.Color == lineColor,
                        $"{language} row {i}: the date keeps the line's own color.");
                    foreach (var background in RowBackgrounds)
                    {
                        var ratio = Contrast(brush.Color, ((SolidColorBrush)view.FindResource(background)).Color);
                        Check(ratio >= 4.5,
                            $"{language} dark={isDark}: Compat.{status} has {ratio:0.00}:1 on {background} (WCAG AA needs 4.5:1).");
                    }

                    colors[status] = brush.Color;
                }
                else
                {
                    Check(line.Text == full, $"{language} row {i}: an unrecognized status keeps '{full}', got '{line.Text}'.");
                    Check(line.Inlines.Count == 1 &&
                          OfficialStatuses.All(official => ((SolidColorBrush)view.FindResource($"Compat.{official}")).Color != lineColor),
                        $"{language} row {i}: an unrecognized status keeps the plain line color.");
                }

                Check(line.FlowDirection == FlowDirection.LeftToRight,
                    $"{language} row {i}: compatibility line must be LTR.");
                Check(line.ActualHeight > 0 && line.ActualHeight <= serial.ActualHeight * 1.5,
                    $"{language} row {i}: compatibility line wrapped or clipped (h={line.ActualHeight}, serial h={serial.ActualHeight}).");
                Check(line.ActualWidth <= container.ActualWidth,
                    $"{language} row {i}: compatibility line overflows the row.");
            }

            Check(colors.Count == OfficialStatuses.Length && colors.Values.Distinct().Count() == OfficialStatuses.Length,
                $"{language} dark={isDark}: each official status needs its own color ({colors.Count} rendered).");

            // Rows of the same shape must have the same height; compatibility rows
            // may grow by exactly one technical line.
            Check(view.ActualWidth <= window.ActualWidth && box.ActualWidth <= view.ActualWidth,
                $"{language}: game list is wider than its host (view={view.ActualWidth}, box={box.ActualWidth}, window={window.ActualWidth}).");
            var withData = heights["BCES00009"];
            var without = heights["BLUS00001"];
            Check(without == heights["BLUS00002"],
                $"{language}: rows without data differ in height.");
            Check(rows.Where(row => row.Compatibility is not null).All(row => heights[row.Serial] == withData),
                $"{language}: rows with compatibility data differ in height ({string.Join(", ", heights.Select(pair => $"{pair.Key}={pair.Value}"))}).");
            Check(withData - without <= 20,
                $"{language}: compatibility line adds {withData - without}px (expected one line).");
            Console.WriteLine($"GameCompatLine {language} view={view.ActualWidth} box={box.ActualWidth} win={window.ActualWidth} dark={isDark} heights: with={withData} without={without} long={heights["BCES00010"]} statuses={colors.Count}");

            if (!string.IsNullOrWhiteSpace(shotDirectory))
            {
                Directory.CreateDirectory(shotDirectory);
                var bitmap = new RenderTargetBitmap(
                    (int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(
                    shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-GameCompatLine.png"));
                encoder.Save(stream);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static Rpcs3CompatibilityEntry Compat(string serial, string status) =>
        new(serial, status, DateOnly.ParseExact(Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), string.Empty, []);

    // WCAG 2.x contrast ratio.
    internal static double Contrast(Color first, Color second)
    {
        static double Channel(byte value)
        {
            var c = value / 255d;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color color) =>
            0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

        var (one, two) = (Luminance(first), Luminance(second));
        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    private static GameVm Row(string serial, string title, Rpcs3CompatibilityEntry? compat) =>
        new(
            new Rpcs3GameDescriptor(serial, title, string.Empty, "01.00", null, null),
            null,
            new RecSum(serial, 0, 0, 0, 0, 0, 0, []),
            serial,
            title,
            true,
            compat);

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

    private sealed record Context(ListVm GameList)
    {
        public bool CanSelectGame { get; } = true;
    }
}
