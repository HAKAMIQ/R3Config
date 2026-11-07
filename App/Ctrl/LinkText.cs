using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using R3Config.Svc;

namespace R3Config.Ctrl;

// Attached behavior that renders plain text with safe http/https URLs as
// clickable hyperlinks, and can turn a whole value (for example an RPCS3 build
// number) into one link to an official target. Links open in the system
// browser; no other text is ever executable.
public static class LinkText
{
    private const string LeftToRightMark = "\u200E";

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(LinkText),
            new PropertyMetadata(null, Changed));

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.RegisterAttached(
            "Target",
            typeof(string),
            typeof(LinkText),
            new PropertyMetadata(null, Changed));

    public static string? GetText(DependencyObject element) =>
        (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) =>
        element.SetValue(TextProperty, value);

    public static string? GetTarget(DependencyObject element) =>
        (string?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, string? value) =>
        element.SetValue(TargetProperty, value);

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is TextBlock block)
        {
            Rebuild(block);
        }
    }

    internal static void Rebuild(TextBlock block)
    {
        var text = GetText(block) ?? string.Empty;
        var target = GetTarget(block);

        block.Inlines.Clear();
        if (text.Length == 0)
        {
            return;
        }

        if (WikiLinks.IsSafeUrl(target))
        {
            block.Inlines.Add(Link(text, target!));
            return;
        }

        foreach (var segment in WikiLinks.Split(text))
        {
            if (segment.Url is null)
            {
                block.Inlines.Add(new Run(segment.Text));
            }
            else if (segment.Text == segment.Url)
            {
                // A bare address is LTR; the marks keep its neighbours from
                // reordering around it inside RTL (Arabic) text. WPF ignores
                // bidi isolates, so LRM is the working tool here.
                block.Inlines.Add(new Run(LeftToRightMark));
                block.Inlines.Add(Link(segment.Text, segment.Url));
                block.Inlines.Add(new Run(LeftToRightMark));
            }
            else
            {
                // [url label]: only the label is visible; the url is the ToolTip.
                block.Inlines.Add(Link(segment.Text, segment.Url));
            }
        }
    }

    private static Hyperlink Link(string display, string url)
    {
        var link = new Hyperlink(new Run(display))
        {
            NavigateUri = new Uri(url, UriKind.Absolute),
            ToolTip = url,
            Cursor = Cursors.Hand,
            TextDecorations = null
        };
        link.SetResourceReference(TextElement.ForegroundProperty, "Accent.Default");
        link.MouseEnter += (_, _) => link.TextDecorations = TextDecorations.Underline;
        link.MouseLeave += (_, _) => link.TextDecorations = null;
        link.RequestNavigate += (_, e) =>
        {
            e.Handled = true;
            Open(e.Uri.AbsoluteUri);
        };
        return link;
    }

    // Only absolute http/https addresses are ever handed to the shell.
    public static bool Open(string url)
    {
        if (!WikiLinks.IsSafeUrl(url))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
