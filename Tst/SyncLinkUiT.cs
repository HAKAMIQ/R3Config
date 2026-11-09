using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using R3Config.Vm;

namespace R3Config.Tests;

// Round 21 (B1) runtime render inside UiGate: Advanced > Recommendation
// updates > Changes, Source column. Round 22: the Source column never pushes
// the grid into horizontal overflow (Advanced default 960x640 included) in
// either language. Each source cell is a swap panel (Sync.SourceCell): the
// full localized name with its "Show link" button when it fits, otherwise an
// icon-only button (Sync.SourceIcon) whose tooltip is the name. Exactly one
// child is visible. The full name is never trimmed and its button never
// covers a character of it, at every UiGate DPI scale. The revealed address
// lives in the row details (SyncRevealUiT), never inside the 36 px cell.
internal static class SyncLinkUiT
{
    private static readonly double[] Scales = [1d, 1.25d, 1.5d];

    // The widest source name with its Show link button: the Arabic RPCS3
    // Config API name needs 232 px (Round 21), the English one 206 px.
    private const double WideNeed = 240;

    public static void Verify(Window window, FrameworkElement sync, string where)
    {
        var grid = Descendants<DataGrid>(sync).SingleOrDefault(item => AutomationProperties.GetAutomationId(item) == "Sync.Changes")
            ?? throw new InvalidOperationException($"Recommendation update source cells: {where}: the Sync.Changes grid was not found.");
        var original = VisualTreeHelper.GetDpi(window);
        Relayout(window, grid);
        var problems = new List<string>();
        var checkedCells = 0;
        var fullCells = 0;
        var compactCells = 0;
        var viewport = 0d;
        var extent = 0d;
        var worstOverflow = double.NegativeInfinity;
        try
        {
            foreach (var scale in Scales)
            {
                VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                window.UpdateLayout();
                var at = $"{where} dpi={scale * 100:0}%";
                var scroller = Scroller(grid);
                if (scroller is null)
                {
                    problems.Add($"{at}: the Sync.Changes grid has no ScrollViewer.");
                    continue;
                }

                if (scroller.ExtentWidth - scroller.ViewportWidth > worstOverflow)
                {
                    worstOverflow = scroller.ExtentWidth - scroller.ViewportWidth;
                    viewport = scroller.ViewportWidth;
                    extent = scroller.ExtentWidth;
                }

                if (scroller.ExtentWidth > scroller.ViewportWidth + 0.5)
                {
                    problems.Add($"{at}: the grid scrolls horizontally: extent {scroller.ExtentWidth:0.#} px > viewport {scroller.ViewportWidth:0.#} px.");
                }

                var cells = SourceCells(grid).ToArray();
                if (cells.Length == 0)
                {
                    problems.Add($"{at}: no source cell (Sync.SourceCell) was realized.");
                    continue;
                }

                var content = Descendants<ScrollContentPresenter>(scroller).FirstOrDefault();
                foreach (var cell in cells)
                {
                    checkedCells++;
                    CheckInViewport(cell, content, at, problems);
                    var mode = CheckSwap(cell, at, problems);
                    fullCells += mode == true ? 1 : 0;
                    compactCells += mode == false ? 1 : 0;
                }

                // The Source column must not be paid for by squeezing the
                // content-sized columns (date, serial, change kind).
                foreach (var column in grid.Columns.Where(column => column.Width.IsAuto && column.ActualWidth < column.Width.DesiredValue - 0.5))
                {
                    problems.Add($"{at}: column '{column.Header}' is {column.ActualWidth:0.#} px, narrower than its content ({column.Width.DesiredValue:0.#} px).");
                }

                SyncRevealUiT.Verify(window, grid, at, problems);
            }

            // UiGate never widens star columns, so the full form (name with
            // its Show link button, revealed through LinkText.RevealedOutside)
            // is forced here by giving the Source column all the free width,
            // so the grid still fits its viewport. Sizes without room for the
            // widest name (WideNeed) are skipped; 1280x800 always has it.
            VisualTreeHelper.SetRootDpi(window, original);
            window.UpdateLayout();
            var wideCells = VerifyWide(window, grid, where, problems);
            if (wideCells is int wideFull)
            {
                checkedCells += wideFull;
                fullCells += wideFull;
                if (wideFull == 0 || compactCells == 0)
                {
                    problems.Add($"{where}: the source cells were never shown both ways (full={wideFull}, compact={compactCells}).");
                }
            }
        }
        finally
        {
            VisualTreeHelper.SetRootDpi(window, original);
            window.UpdateLayout();
        }

        var sourceWidth = grid.Columns.Count > 0 ? grid.Columns[^1].ActualWidth : 0d;
        var sourceMin = grid.Columns.Count > 0 ? grid.Columns[^1].MinWidth : 0d;
        Console.WriteLine(FormattableString.Invariant(
            $"SyncLinkUi {where} cells={checkedCells} full={fullCells} compact={compactCells} viewport={viewport:0.#} extent={extent:0.#} source-column={sourceWidth:0.#} min={sourceMin:0.#} cols={string.Join(",", grid.Columns.Select(column => FormattableString.Invariant($"{column.ActualWidth:0.#}/{column.MinWidth:0}")))}"));
        Check(problems.Count == 0,
            $"Recommendation update source cells: {problems.Count} problem(s):{Environment.NewLine}{string.Join(Environment.NewLine, problems.Distinct().Take(16))}");
    }

    // Returns the number of full cells checked, or null when the size has no
    // room for the widest source name with its button.
    private static int? VerifyWide(Window window, DataGrid grid, string where, List<string> problems)
    {
        var source = grid.Columns[^1];

        // Every column's width, so later sizes start from the grid's own
        // layout again (Relayout).
        var widths = grid.Columns.Select(column => column.Width).ToArray();
        var full = 0;
        try
        {
            // The other star columns give way down to their minimum and the
            // Source column takes all the rest as the only star column.
            foreach (var column in grid.Columns.Take(grid.Columns.Count - 1).Where(column => column.Width.IsStar))
            {
                column.Width = new DataGridLength(column.MinWidth);
            }

            source.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            window.UpdateLayout();
            if (source.ActualWidth < WideNeed)
            {
                Console.WriteLine(FormattableString.Invariant($"SyncLinkUi {where} wide skipped free={source.ActualWidth:0.#} need={WideNeed}"));
                return null;
            }

            var at = $"{where} wide";
            var cells = SourceCells(grid).ToArray();
            if (cells.Length == 0)
            {
                problems.Add($"{at}: no source cell (Sync.SourceCell) was realized.");
            }

            var content = Scroller(grid) is { } scroller ? Descendants<ScrollContentPresenter>(scroller).FirstOrDefault() : null;
            foreach (var cell in cells)
            {
                CheckInViewport(cell, content, at, problems);
                var mode = CheckSwap(cell, at, problems);
                full += mode == true ? 1 : 0;
                if (mode == false)
                {
                    problems.Add($"{at}: '{((RecSyncChangeRowVm)cell.DataContext).Source}' stays an icon in a {cell.ActualWidth:0.#} px cell.");
                }
            }

            var before = Scroller(grid)?.ExtentWidth ?? 0;
            SyncRevealUiT.Verify(window, grid, at, problems);
            Console.WriteLine(FormattableString.Invariant($"SyncLinkUi {where} wide cells={cells.Length} full={full} source-column={source.ActualWidth:0.#} extent={before:0.#} viewport={Scroller(grid)?.ViewportWidth:0.#}"));
        }
        finally
        {
            for (var index = 0; index < widths.Length; index++)
            {
                if (widths[index].IsStar)
                {
                    grid.Columns[index].Width = widths[index];
                }
            }

            Relayout(window, grid);
        }

        return full;
    }

    // UiGate lays the same window out at each size, and a DataGrid shares its
    // star width only when a column width changes (the app's own resize does
    // it), so each check starts by sharing it again for the current size.
    // Star columns only: an Auto column (the English "Change" header) set
    // again would grow back only after the share, as on first load.
    private static void Relayout(Window window, DataGrid grid)
    {
        foreach (var column in grid.Columns.Where(column => column.Width.IsStar))
        {
            column.Width = new DataGridLength(column.Width.Value, column.Width.UnitType);
        }

        window.UpdateLayout();
    }

    internal static ScrollViewer? Scroller(DataGrid grid) =>
        grid.Template?.FindName("DG_ScrollViewer", grid) as ScrollViewer
        ?? Descendants<ScrollViewer>(grid).FirstOrDefault();

    internal static IEnumerable<Panel> SourceCells(DependencyObject root) =>
        Descendants<Panel>(root).Where(panel =>
            AutomationProperties.GetAutomationId(panel) == "Sync.SourceCell" &&
            SyncLinkUiT.Shown(panel) &&
            panel.ActualWidth > 0 &&
            panel.DataContext is RecSyncChangeRowVm);

    // The cell (and its DataGridCell) must lie inside the visible content
    // area of the grid horizontally; a cell past the viewport is overflow.
    private static void CheckInViewport(Panel cell, ScrollContentPresenter? content, string where, List<string> problems)
    {
        var row = (RecSyncChangeRowVm)cell.DataContext;
        if (content is null)
        {
            problems.Add($"{where}: the grid has no ScrollContentPresenter to measure '{row.Source}' against.");
            return;
        }

        foreach (var element in new FrameworkElement?[] { cell, Ancestor<DataGridCell>(cell) })
        {
            if (element is null)
            {
                continue;
            }

            if (!element.IsDescendantOf(content))
            {
                problems.Add($"{where}: the {element.GetType().Name} of '{row.Source}' is not inside the grid's scroll content.");
                continue;
            }

            var bounds = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
            if (bounds.Left < -0.5 || bounds.Right > content.ActualWidth + 0.5)
            {
                problems.Add($"{where}: the {element.GetType().Name} of '{row.Source}' spans {bounds.Left:0.#}..{bounds.Right:0.#} px, outside the visible viewport 0..{content.ActualWidth:0.#} px.");
            }
        }
    }

    // Returns true for full mode, false for compact mode, null when broken.
    private static bool? CheckSwap(Panel cell, string where, List<string> problems)
    {
        var row = (RecSyncChangeRowVm)cell.DataContext;
        if (cell.Children.Count < 2)
        {
            problems.Add($"{where}: the source cell of '{row.Source}' has {cell.Children.Count} child(ren), not full + icon.");
            return null;
        }

        var full = cell.Children[0];
        var icon = cell.Children[1];
        if (SyncLinkUiT.Shown(full) == SyncLinkUiT.Shown(icon))
        {
            problems.Add($"{where}: the source cell of '{row.Source}' shows {(SyncLinkUiT.Shown(full) ? "both" : "neither")} the full name and the icon.");
            return null;
        }

        if (SyncLinkUiT.Shown(full))
        {
            if (full is not TextBlock text || AutomationProperties.GetAutomationId(text) != "Sync.SourceLink")
            {
                problems.Add($"{where}: the full child of '{row.Source}' is not the Sync.SourceLink TextBlock.");
                return null;
            }

            CheckFull(cell, text, where, problems);
            return true;
        }

        if (icon is not Button button || AutomationProperties.GetAutomationId(button) != "Sync.SourceIcon")
        {
            problems.Add($"{where}: the compact child of '{row.Source}' is not the Sync.SourceIcon button.");
            return null;
        }

        var bounds = button.TransformToAncestor(cell).TransformBounds(new Rect(button.RenderSize));
        if (bounds.Left < -0.5 || bounds.Top < -0.5 || bounds.Right > cell.ActualWidth + 0.5 || bounds.Bottom > cell.ActualHeight + 0.5)
        {
            problems.Add($"{where}: the Sync.SourceIcon of '{row.Source}' spans {bounds.Left:0.#}..{bounds.Right:0.#} px, outside its {cell.ActualWidth:0.#} px cell.");
        }

        var tip = button.ToolTip as string ?? (button.ToolTip as ToolTip)?.Content as string;
        if (tip != row.Source)
        {
            problems.Add($"{where}: the Sync.SourceIcon tooltip reads '{tip}' instead of '{row.Source}'.");
        }

        if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
        {
            problems.Add($"{where}: the Sync.SourceIcon of '{row.Source}' has no automation name.");
        }

        return false;
    }

    private static void CheckFull(Panel panel, TextBlock cell, string where, List<string> problems)
    {
        var row = (RecSyncChangeRowVm)cell.DataContext;
        var runs = TextRuns(cell.Inlines).ToArray();
        var shown = string.Concat(runs.Select(run => run.Text));
        if (shown != row.Source)
        {
            problems.Add($"{where}: source cell reads '{shown}' instead of '{row.Source}'.");
        }

        var buttons = ShowButtons(cell).ToArray();
        if (buttons.Length != 1)
        {
            problems.Add($"{where}: the full source cell of '{row.Source}' has {buttons.Length} Show link button(s), not 1.");
        }

        // The cell must be wide enough for the name and its button, so no
        // character is trimmed away.
        var inPanel = cell.TransformToAncestor(panel).TransformBounds(new Rect(cell.RenderSize));
        var available = Math.Min(cell.ActualWidth, panel.ActualWidth);
        cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var needed = cell.DesiredSize.Width;
        cell.InvalidateMeasure();
        cell.UpdateLayout();
        if (needed > available + 0.5)
        {
            problems.Add($"{where}: '{row.Source}' needs {needed:0.#} px with its button but the cell has {available:0.#} px (trimmed).");
        }

        if (inPanel.Left < -0.5 || inPanel.Right > panel.ActualWidth + 0.5)
        {
            problems.Add($"{where}: the full name of '{row.Source}' spans {inPanel.Left:0.#}..{inPanel.Right:0.#} px, outside its {panel.ActualWidth:0.#} px cell.");
        }

        var buttonBounds = buttons
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

                foreach (var button in buttonBounds.Where(button => button.IntersectsWith(glyph)))
                {
                    problems.Add($"{where}: the Show link button covers '{run.Text[i]}' of '{row.Source}'.");
                }
            }
        }
    }

    internal static IEnumerable<Button> ShowButtons(TextBlock block) =>
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

    // UiGate never shows its window, so IsVisible is false everywhere; an
    // element is shown when it and every visual ancestor are Visible.
    internal static bool Shown(UIElement element)
    {
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement item && item.Visibility != Visibility.Visible)
            {
                return false;
            }
        }

        return true;
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
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

    internal static T? Ancestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(start); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
