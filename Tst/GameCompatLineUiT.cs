using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Runtime render check for the game-row CompatibilityLine: a row with
// compatibility data, a row without it, and a very long title must keep
// one-line technical text, stable row heights and no clipped title.
internal static class GameCompatLineUiT
{
    private const string LongTitle =
        "Metal Gear Solid 4: Guns of the Patriots - Limited Collector's Edition with Extra Long Title Suffix";

    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var compat = new Rpcs3CompatibilityEntry(
            "BCES00009", "Playable", new DateOnly(2026, 10, 23), string.Empty, []);
        var rows = new[]
        {
            Row("BCES00009", "RIDGE RACER 7", compat),
            Row("BLUS00001", "No Compat Data", null),
            Row("BLUS00002", "Another No Compat", null),
            Row("BCES00010", LongTitle, compat)
        };

        var list = new ListVm();
        list.Load(rows);
        var view = new R3Config.Ctrl.GameList { DataContext = new Context(list) };
        var window = new Window
        {
            Content = view,
            Width = 360,
            Height = 560,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        Drain(window.Dispatcher);
        try
        {
            var box = Descendants<ListBox>(view).First(item => item.Items.Count == rows.Length);
            var heights = new Dictionary<string, double>();
            for (var i = 0; i < rows.Length; i++)
            {
                var container = box.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem
                    ?? throw new InvalidOperationException($"Row {i} was not realized.");
                var row = container.DataContext as GameVm
                    ?? throw new InvalidOperationException($"Row {i} has no GameVm.");
                heights[row.Serial] = container.ActualHeight;
                var texts = Descendants<TextBlock>(container)
                    .Where(text => text.DataContext == row && text.Name.Length == 0)
                    .ToArray();
                var serial = texts.Single(text => text.Text == row.Serial);
                var title = texts.Single(text => text.Text == row.Title);
                var line = texts.Single(text => text != serial && text != title && text.Text != "");
                var expected = row.Compatibility is null
                    ? string.Empty
                    : UiText.Fmt("Games.CompatibilityLine", "Playable", "2026-10-23");

                Check(line.Text == expected, $"{language} row {i}: compatibility text '{line.Text}' != '{expected}'.");
                Check(title.ActualWidth > 0 && title.ActualWidth <= container.ActualWidth,
                    $"{language} row {i}: title overflows its row.");
                if (row.Compatibility is null)
                {
                    Check(line.ActualHeight == 0 || line.Visibility != Visibility.Visible,
                        $"{language} row {i}: empty compatibility line still occupies {line.ActualHeight}px.");
                }
                else
                {
                    Check(line.FlowDirection == FlowDirection.LeftToRight,
                        $"{language} row {i}: compatibility line must be LTR.");
                    Check(line.ActualHeight > 0 && line.ActualHeight <= serial.ActualHeight * 1.5,
                        $"{language} row {i}: compatibility line wrapped or clipped (h={line.ActualHeight}, serial h={serial.ActualHeight}).");
                    Check(line.ActualWidth <= container.ActualWidth,
                        $"{language} row {i}: compatibility line overflows the row.");
                }
            }

            // Rows of the same shape must have the same height; compatibility rows
            // may grow by exactly one technical line.
            Check(view.ActualWidth <= window.ActualWidth && box.ActualWidth <= view.ActualWidth,
                $"{language}: game list is wider than its host (view={view.ActualWidth}, box={box.ActualWidth}, window={window.ActualWidth}).");
            var withData = heights["BCES00009"];
            var without = heights["BLUS00001"];
            Check(without == heights["BLUS00002"],
                $"{language}: rows without data differ in height.");
            Check(withData == heights["BCES00010"],
                $"{language}: long-title row changed height ({withData} vs {heights["BCES00010"]}).");
            Check(withData - without <= 20,
                $"{language}: compatibility line adds {withData - without}px (expected one line).");
            Console.WriteLine($"GameCompatLine {language} view={view.ActualWidth} box={box.ActualWidth} win={window.ActualWidth} dark={isDark} heights: with={withData} without={without} long={heights["BCES00010"]}");

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
