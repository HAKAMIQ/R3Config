using System;
using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

// Tab headers in one row that always fits the available width. When the
// natural widths do not fit, the widest headers shrink first (never below
// their MinWidth) and trim with an ellipsis; a trimmed header shows its full
// text as a ToolTip. The row never scrolls.
public sealed class FitTabPanel : Panel
{
    private const double TrimTolerance = 0.5;

    // Marks a ToolTip this panel set, so a ToolTip declared on the tab is kept.
    private static readonly DependencyProperty OwnsTipProperty =
        DependencyProperty.RegisterAttached(
            "OwnsTip",
            typeof(bool),
            typeof(FitTabPanel),
            new PropertyMetadata(false));

    private double[] _natural = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = InternalChildren;
        _natural = new double[children.Count];

        for (var index = 0; index < children.Count; index++)
        {
            children[index].Measure(
                new Size(double.PositiveInfinity, availableSize.Height));
            _natural[index] = children[index].DesiredSize.Width;
        }

        var widths = Fit(availableSize.Width);
        var total = 0.0;
        var height = 0.0;

        for (var index = 0; index < children.Count; index++)
        {
            children[index].Measure(
                new Size(widths[index], availableSize.Height));
            total += widths[index];
            height = Math.Max(height, children[index].DesiredSize.Height);
        }

        return new Size(total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren;

        if (_natural.Length != children.Count)
        {
            return finalSize;
        }

        var widths = Fit(finalSize.Width);
        var left = 0.0;

        for (var index = 0; index < children.Count; index++)
        {
            children[index].Arrange(
                new Rect(left, 0, widths[index], finalSize.Height));
            left += widths[index];

            UpdateTip(
                children[index],
                widths[index] < _natural[index] - TrimTolerance);
        }

        return finalSize;
    }

    // Lowers one common cap on header width until the row fits; a header
    // narrower than the cap keeps its natural width.
    private double[] Fit(double available)
    {
        var count = _natural.Length;
        var widths = (double[])_natural.Clone();
        var mins = new double[count];
        var natural = 0.0;
        var widest = 0.0;

        for (var index = 0; index < count; index++)
        {
            natural += _natural[index];
            widest = Math.Max(widest, _natural[index]);
            mins[index] = InternalChildren[index] is FrameworkElement element
                ? Math.Min(element.MinWidth, _natural[index])
                : 0;
        }

        if (double.IsInfinity(available) || natural <= available)
        {
            return widths;
        }

        var low = 0.0;
        var high = widest;

        for (var step = 0; step < 32; step++)
        {
            var cap = (low + high) / 2;

            if (Total(cap, mins) <= available)
            {
                low = cap;
            }
            else
            {
                high = cap;
            }
        }

        for (var index = 0; index < count; index++)
        {
            widths[index] = Math.Max(mins[index], Math.Min(_natural[index], low));
        }

        return widths;
    }

    private double Total(double cap, double[] mins)
    {
        var total = 0.0;

        for (var index = 0; index < _natural.Length; index++)
        {
            total += Math.Max(mins[index], Math.Min(_natural[index], cap));
        }

        return total;
    }

    private static void UpdateTip(UIElement child, bool trimmed)
    {
        if (child is not HeaderedContentControl tab)
        {
            return;
        }

        var owned = (bool)tab.GetValue(OwnsTipProperty);

        if (trimmed && (owned || tab.ToolTip is null))
        {
            tab.ToolTip = tab.Header;
            tab.SetValue(OwnsTipProperty, true);
        }
        else if (!trimmed && owned)
        {
            tab.ClearValue(ToolTipProperty);
            tab.ClearValue(OwnsTipProperty);
        }
    }
}
