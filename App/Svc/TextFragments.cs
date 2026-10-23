using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using R3Config.Lang;

namespace R3Config.Svc;

// Presentation metadata only. Text is never normalized or decorated with
// direction-control characters; the caller identifies canonical fragments.
public sealed record TextFragment(string Text, bool IsTechnical = false);

public static class TextFragments
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.RegisterAttached(
            "Content",
            typeof(IReadOnlyList<TextFragment>),
            typeof(TextFragments),
            new PropertyMetadata(null, ContentChanged));

    public static IReadOnlyList<TextFragment>? GetContent(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (IReadOnlyList<TextFragment>?)element.GetValue(ContentProperty);
    }

    public static void SetContent(
        DependencyObject element,
        IReadOnlyList<TextFragment>? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ContentProperty, value);
    }

    public static IReadOnlyList<TextFragment> TechnicalList(
        string localizedPrefix,
        IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(localizedPrefix);
        ArgumentNullException.ThrowIfNull(values);
        var fragments = new List<TextFragment> { new(localizedPrefix) };
        var first = true;
        foreach (var value in values)
        {
            fragments.Add(new TextFragment(
                first ? " " : UiText.GetText("Ui.ListSeparator")));
            fragments.Add(new TextFragment(value, IsTechnical: true));
            first = false;
        }

        return fragments.ToArray();
    }

    public static string PlainText(IEnumerable<TextFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        return string.Concat(fragments.Select(fragment => fragment.Text));
    }

    private static void ContentChanged(
        DependencyObject element,
        DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock textBlock)
        {
            throw new InvalidOperationException(
                "TextFragments.Content requires a TextBlock.");
        }

        textBlock.Inlines.Clear();
        if (args.NewValue is not IReadOnlyList<TextFragment> fragments)
        {
            return;
        }

        foreach (var fragment in fragments)
        {
            var run = new Run(fragment.Text);
            if (fragment.IsTechnical)
            {
                // Span creates a separate WPF BiDi scope even when adjacent
                // fragments share a direction. Separators remain prose.
                var span = new Span(run);
                span.SetResourceReference(
                    FrameworkContentElement.StyleProperty,
                    "TechnicalTextSpan");
                textBlock.Inlines.Add(span);
            }
            else
            {
                textBlock.Inlines.Add(run);
            }
        }
    }
}
