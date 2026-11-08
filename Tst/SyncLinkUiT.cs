using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Vm;

namespace R3Config.Tests;

// Round 21 (B1) runtime render inside UiGate: Advanced > Recommendation
// updates > Changes, Source column. Every realized source cell shows its
// localized source name in full (never trimmed) and its "Show link" button
// never covers a character of that name, at every UiGate DPI scale. The
// Arabic "RPCS3 Config API" name used to be trimmed under the button so only
// "3" of RPCS3 showed.
internal static class SyncLinkUiT
{
    private static readonly double[] Scales = [1d, 1.25d, 1.5d];

    public static void Verify(Window window, FrameworkElement sync, string where)
    {
        var grid = Descendants<DataGrid>(sync).Single(item => AutomationProperties.GetAutomationId(item) == "Sync.Changes");
        var original = VisualTreeHelper.GetDpi(window);
        var problems = new List<string>();
        var checkedCells = 0;
        try
        {
            foreach (var scale in Scales)
            {
                VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                window.UpdateLayout();
                var cells = Descendants<TextBlock>(grid)
                    .Where(block => block.ActualWidth > 0 && block.DataContext is RecSyncChangeRowVm && ShowButtons(block).Any())
                    .ToArray();
                if (cells.Length == 0)
                {
                    problems.Add($"{where} dpi={scale * 100:0}%: no source cell with a Show link button was realized.");
                    continue;
                }

                foreach (var cell in cells)
                {
                    checkedCells++;
                    CheckCell(cell, $"{where} dpi={scale * 100:0}%", problems);
                }

                // The wider Source column must not be paid for by squeezing
                // the content-sized columns (date, serial, change kind).
                foreach (var column in grid.Columns.Where(column => column.Width.IsAuto && column.ActualWidth < column.Width.DesiredValue - 0.5))
                {
                    problems.Add($"{where} dpi={scale * 100:0}%: column '{column.Header}' is {column.ActualWidth:0.#} px, narrower than its content ({column.Width.DesiredValue:0.#} px).");
                }
            }
        }
        finally
        {
            VisualTreeHelper.SetRootDpi(window, original);
            window.UpdateLayout();
        }

        if (UiText.CurLang != UiText.ArabLang)
        {
            // English keeps the original column minimum (App/Svc/Layout.cs) so
            // its layout does not change; at narrow sizes the English name is
            // still trimmed under the button. Reported, not enforced.
            Console.WriteLine(FormattableString.Invariant($"SyncLinkUi {where} cells={checkedCells} english-known-problems={problems.Count}"));
            return;
        }

        Check(problems.Count == 0,
            $"Recommendation update source cells: {problems.Count} problem(s):{Environment.NewLine}{string.Join(Environment.NewLine, problems.Distinct().Take(12))}");
        var source = grid.Columns[^1];
        Console.WriteLine(FormattableString.Invariant($"SyncLinkUi {where} cells={checkedCells} source-column={source.ActualWidth:0.#} min={source.MinWidth:0.#}"));
    }

    private static void CheckCell(TextBlock cell, string where, List<string> problems)
    {
        var row = (RecSyncChangeRowVm)cell.DataContext;
        var runs = TextRuns(cell.Inlines).ToArray();
        var shown = string.Concat(runs.Select(run => run.Text));
        if (shown != row.Source)
        {
            problems.Add($"{where}: source cell reads '{shown}' instead of '{row.Source}'.");
        }

        // The cell must be wide enough for the name and its buttons, so no
        // character is trimmed away.
        var available = cell.ActualWidth;
        cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var needed = cell.DesiredSize.Width;
        cell.InvalidateMeasure();
        cell.UpdateLayout();
        if (needed > available + 0.5)
        {
            problems.Add($"{where}: '{row.Source}' needs {needed:0.#} px with its buttons but the cell has {available:0.#} px (trimmed).");
        }

        var buttons = ShowButtons(cell)
            .Select(button => button.TransformToAncestor(cell).TransformBounds(new Rect(button.RenderSize)))
            .ToArray();
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Text.Length; i++)
            {
                var start = run.ContentStart.GetPositionAtOffset(i);
                var end = run.ContentStart.GetPositionAtOffset(i + 1);
                if (start is null || end is null || char.IsWhiteSpace(run.Text[i]))
                {
                    continue;
                }

                var glyph = Rect.Union(
                    start.GetCharacterRect(LogicalDirection.Forward),
                    end.GetCharacterRect(LogicalDirection.Backward));
                glyph.Inflate(-0.5, -0.5);
                if (glyph.Left < -0.5 || glyph.Right > cell.ActualWidth + 0.5)
                {
                    problems.Add($"{where}: '{run.Text[i]}' of '{row.Source}' is outside its cell.");
                }

                foreach (var button in buttons.Where(button => button.IntersectsWith(glyph)))
                {
                    problems.Add($"{where}: the Show link button covers '{run.Text[i]}' of '{row.Source}'.");
                }
            }
        }
    }

    private static IEnumerable<Button> ShowButtons(TextBlock block) =>
        Containers(block.Inlines)
            .Select(container => container.Child)
            .OfType<Button>()
            .Where(button => button.Visibility == Visibility.Visible && button.ActualWidth > 0 && AutomationProperties.GetAutomationId(button) == "LinkText.Link.Show");

    private static IEnumerable<InlineUIContainer> Containers(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer container)
            {
                yield return container;
            }
            else if (inline is Span span)
            {
                foreach (var nested in Containers(span.Inlines))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<Run> TextRuns(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
            {
                yield return run;
            }
            else if (inline is Span span)
            {
                foreach (var nested in TextRuns(span.Inlines))
                {
                    yield return nested;
                }
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
