using System;
using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

// Two forms of one thing for a narrow slot: the first child is the full form
// (a name and its button), the second the compact form (an icon button that
// names the same thing in its ToolTip). The full form shows whenever its
// natural width fits; otherwise the compact form shows. The other child is
// Hidden, so it is neither drawn nor hit-tested, and it is still measured, so
// the choice never feeds back into the width it is made on.
public sealed class CompactSwapPanel : Panel
{
    private const double FitTolerance = 0.5;

    private double _natural;

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = InternalChildren;
        if (children.Count != 2)
        {
            throw new InvalidOperationException("CompactSwapPanel needs exactly a full and a compact child.");
        }

        // Only at infinite width: the full form is never shown narrower than
        // it is, and its last measure constraint stays unbounded, so a new
        // text in a recycled row always re-measures this panel.
        children[0].Measure(new Size(double.PositiveInfinity, availableSize.Height));
        _natural = children[0].DesiredSize.Width;
        children[1].Measure(availableSize);

        var shown = children[Fits(availableSize.Width) ? 0 : 1];
        return new Size(
            Math.Min(shown.DesiredSize.Width, availableSize.Width),
            shown.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren;

        // Decided on the final width: a column can narrow between the passes.
        var compact = !Fits(finalSize.Width);
        Show(children[0], !compact);
        Show(children[1], compact);

        foreach (UIElement child in children)
        {
            child.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    private bool Fits(double width) =>
        double.IsInfinity(width) || _natural <= width + FitTolerance;

    private static void Show(UIElement child, bool shown)
    {
        var visibility = shown ? Visibility.Visible : Visibility.Hidden;

        if (child.Visibility != visibility)
        {
            child.Visibility = visibility;
        }
    }
}
