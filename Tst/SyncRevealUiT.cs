using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using R3Config.Lang;

namespace R3Config.Tests;

// Round 22 runtime render inside UiGate (called by SyncLinkUiT at every size,
// DPI scale and language): "Show link" in a Recommendation update source cell
// (the full name's button or the compact Sync.SourceIcon) reveals the address
// with Copy/Open in that row's details below its 36 px cells, never inside the
// cell and never drawn over the next row. Every other row stays 36 px, and
// "Hide link" collapses the details again. The row flag is read by reflection
// (IsLinkRevealed) so this gate compiles against any row model.
internal static class SyncRevealUiT
{
    private const double RowHeight = 36d;

    private static readonly MethodInfo? OnClick =
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);

    public static void Verify(Window window, DataGrid grid, string where, List<string> problems)
    {
        CheckRows(grid, null, $"{where} before reveal", problems);
        var row = VisibleRows(grid).FirstOrDefault(candidate => SyncLinkUiT.SourceCells(candidate).Any());
        if (row is null)
        {
            problems.Add($"{where}: no realized row with a source cell to reveal.");
            return;
        }

        var index = grid.ItemContainerGenerator.IndexFromContainer(row);
        var item = row.Item;
        var flag = item?.GetType().GetProperty("IsLinkRevealed", BindingFlags.Instance | BindingFlags.Public);
        if (flag is null || flag.PropertyType != typeof(bool))
        {
            problems.Add($"{where}: the change row model has no public bool IsLinkRevealed.");
            flag = null;
        }

        var (toggle, full) = Toggle(row);
        if (toggle is null)
        {
            problems.Add($"{where}: row {index} has no visible Show link toggle (LinkText.Link.Show or Sync.SourceIcon).");
            return;
        }

        try
        {
            ExpectFlag(item, flag, false, $"{where} before reveal", problems);
            ExpectLabel(toggle, full, "Link.Show", $"{where} before reveal", problems);
            Click(toggle, where, problems);
            window.UpdateLayout();
            ExpectFlag(item, flag, true, $"{where} after Show link", problems);
            CheckRevealed(grid, row, index, where, problems);
            CheckRows(grid, row, $"{where} revealed", problems);

            var (again, againFull) = Toggle(row);
            if (again is null)
            {
                problems.Add($"{where}: row {index} has no visible Hide link toggle after reveal.");
                return;
            }

            ExpectLabel(again, againFull, "Link.Hide", $"{where} revealed", problems);
            Click(again, where, problems);
            window.UpdateLayout();
            ExpectFlag(item, flag, false, $"{where} after Hide link", problems);
            var details = SyncLinkUiT.Descendants<DataGridDetailsPresenter>(row).FirstOrDefault();
            if (row.DetailsVisibility == Visibility.Visible || (details is not null && SyncLinkUiT.Shown(details) && details.ActualHeight > 0.5))
            {
                problems.Add($"{where}: row {index} still shows its details after Hide link.");
            }

            CheckRows(grid, null, $"{where} after hide", problems);
        }
        finally
        {
            // Leave the row collapsed for later UiGate steps.
            if (flag is not null && flag.GetValue(item) is true && flag.CanWrite)
            {
                flag.SetValue(item, false);
            }

            window.UpdateLayout();
        }
    }

    private static void CheckRevealed(DataGrid grid, DataGridRow row, int index, string where, List<string> problems)
    {
        var cells = SyncLinkUiT.Descendants<DataGridCellsPresenter>(row).FirstOrDefault();
        if (cells is null || Math.Abs(cells.ActualHeight - RowHeight) > 0.5)
        {
            problems.Add($"{where}: row {index} cells are {(cells?.ActualHeight ?? 0):0.#} px tall after Show link instead of {RowHeight:0} px.");
        }

        var scroller = SyncLinkUiT.Scroller(grid);
        if (scroller is not null && scroller.ExtentWidth > scroller.ViewportWidth + 0.5)
        {
            problems.Add($"{where}: the grid scrolls horizontally after Show link: extent {scroller.ExtentWidth:0.#} px > viewport {scroller.ViewportWidth:0.#} px.");
        }

        var details = SyncLinkUiT.Descendants<DataGridDetailsPresenter>(row).FirstOrDefault();
        if (row.DetailsVisibility != Visibility.Visible || details is null || !SyncLinkUiT.Shown(details) || details.ActualHeight < 0.5)
        {
            problems.Add($"{where}: row {index} details are not shown after Show link (DetailsVisibility={row.DetailsVisibility}).");
            return;
        }

        if (!SyncLinkUiT.Descendants<TextBlock>(details).Any(text => AutomationProperties.GetAutomationId(text) == "Sync.SourceAddress"))
        {
            problems.Add($"{where}: row {index} details have no Sync.SourceAddress.");
        }

        var revealed = SyncLinkUiT.Descendants<Border>(details)
            .FirstOrDefault(border => SyncLinkUiT.Shown(border) && AutomationProperties.GetAutomationId(border) == "LinkText.Revealed");
        if (revealed is null)
        {
            problems.Add($"{where}: row {index} details have no visible LinkText.Revealed address.");
            return;
        }

        var address = SyncLinkUiT.Descendants<TextBlock>(revealed)
            .FirstOrDefault(text => AutomationProperties.GetAutomationId(text) == "LinkText.RevealedText");
        if (string.IsNullOrWhiteSpace(address?.Text))
        {
            problems.Add($"{where}: row {index} revealed address has no LinkText.RevealedText.");
        }

        var parts = new List<(string Name, FrameworkElement? Element)> { ("LinkText.Revealed", revealed) };
        foreach (var id in new[] { "LinkText.Link.Copy", "LinkText.Link.Open" })
        {
            parts.Add((id, SyncLinkUiT.Descendants<Button>(revealed)
                .FirstOrDefault(button => SyncLinkUiT.Shown(button) && AutomationProperties.GetAutomationId(button) == id)));
        }

        var next = grid.ItemContainerGenerator.ContainerFromIndex(index + 1) as DataGridRow;
        double? nextTop = next is not null && SyncLinkUiT.Shown(next) ? next.TransformToAncestor(grid).Transform(new Point(0, 0)).Y : null;
        var rowBottom = row.TransformToAncestor(grid).Transform(new Point(0, row.ActualHeight)).Y;
        if (nextTop is not null && rowBottom > nextTop + 0.5)
        {
            problems.Add($"{where}: row {index} ends at {rowBottom:0.#} px, past the next row's top {nextTop:0.#} px.");
        }

        foreach (var (name, element) in parts)
        {
            if (element is null)
            {
                problems.Add($"{where}: row {index} revealed address has no visible {name}.");
                continue;
            }

            var bounds = element.TransformToAncestor(row).TransformBounds(new Rect(element.RenderSize));
            if (bounds.Left < -0.5 || bounds.Top < -0.5 || bounds.Right > row.ActualWidth + 0.5 || bounds.Bottom > row.ActualHeight + 0.5)
            {
                problems.Add($"{where}: {name} spans {bounds.Left:0.#},{bounds.Top:0.#}..{bounds.Right:0.#},{bounds.Bottom:0.#} px, outside its {row.ActualWidth:0.#}x{row.ActualHeight:0.#} px row {index}.");
            }

            var bottom = element.TransformToAncestor(grid).TransformBounds(new Rect(element.RenderSize)).Bottom;
            if (nextTop is not null && bottom > nextTop + 0.5)
            {
                problems.Add($"{where}: {name} reaches {bottom:0.#} px, past the next row's top {nextTop:0.#} px (drawn over the row below).");
            }
        }
    }

    private static void CheckRows(DataGrid grid, DataGridRow? except, string where, List<string> problems)
    {
        // The row adds its border around the cells, so a locked row is its
        // 36 px of cells with no details under them.
        foreach (var row in VisibleRows(grid).Where(row => !ReferenceEquals(row, except)))
        {
            var cells = SyncLinkUiT.Descendants<DataGridCellsPresenter>(row).FirstOrDefault();
            var details = SyncLinkUiT.Descendants<DataGridDetailsPresenter>(row).FirstOrDefault();
            var cellsHeight = cells?.ActualHeight ?? 0;
            var detailsHeight = details is not null && SyncLinkUiT.Shown(details) ? details.ActualHeight : 0;
            if (Math.Abs(cellsHeight - RowHeight) > 0.5 || detailsHeight > 0.5)
            {
                problems.Add($"{where}: row {grid.ItemContainerGenerator.IndexFromContainer(row)} has {cellsHeight:0.#} px of cells and {detailsHeight:0.#} px of details instead of {RowHeight:0} px and none.");
            }
        }
    }

    private static IEnumerable<DataGridRow> VisibleRows(DataGrid grid) =>
        SyncLinkUiT.Descendants<DataGridRow>(grid)
            .Where(row => SyncLinkUiT.Shown(row))
            .OrderBy(row => grid.ItemContainerGenerator.IndexFromContainer(row));

    // The visible toggle of the row's source cell: the full name's Show/Hide
    // link button, or the compact icon button.
    private static (Button? Toggle, bool Full) Toggle(DataGridRow row)
    {
        var cell = SyncLinkUiT.SourceCells(row).FirstOrDefault();
        if (cell is null || cell.Children.Count < 2)
        {
            return (null, false);
        }

        if (cell.Children[0] is TextBlock text && SyncLinkUiT.Shown(text))
        {
            return (SyncLinkUiT.ShowButtons(text).FirstOrDefault(), true);
        }

        return cell.Children[1] is Button icon && SyncLinkUiT.Shown(icon) && AutomationProperties.GetAutomationId(icon) == "Sync.SourceIcon"
            ? (icon, false)
            : (null, false);
    }

    private static void ExpectLabel(Button toggle, bool full, string key, string where, List<string> problems)
    {
        var expected = UiText.GetText(key);
        var shown = full ? toggle.Content as string : AutomationProperties.GetName(toggle);
        if (shown != expected)
        {
            problems.Add($"{where}: the {(full ? "LinkText.Link.Show button" : "Sync.SourceIcon name")} reads '{shown}' instead of '{expected}'.");
        }
    }

    private static void ExpectFlag(object? item, PropertyInfo? flag, bool expected, string where, List<string> problems)
    {
        if (flag is not null && flag.GetValue(item) is bool actual && actual != expected)
        {
            problems.Add($"{where}: IsLinkRevealed is {actual} instead of {expected}.");
        }
    }

    // OnClick raises ButtonBase.ClickEvent and also runs a bound Command
    // (ToggleLinkCmd), synchronously; RaiseEvent alone skips the command.
    private static void Click(Button button, string where, List<string> problems)
    {
        if (OnClick is null)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            return;
        }

        try
        {
            OnClick.Invoke(button, null);
        }
        catch (TargetInvocationException error)
        {
            problems.Add($"{where}: clicking {AutomationProperties.GetAutomationId(button)} threw {error.InnerException?.GetType().Name}: {error.InnerException?.Message}");
        }
    }
}
