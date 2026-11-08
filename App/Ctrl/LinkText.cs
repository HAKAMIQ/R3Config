using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config.Ctrl;

// Attached behavior that renders note text (Wiki, RPCN, cat.json) and link
// values. Display only; the bound text is never changed.
//  - A link is always a word: [url label] shows the label, a bare address shows
//    "Wiki page" (wiki.rpcs3.net) or "Link". No raw address is shown, not even
//    in a ToolTip. Clicking the word opens it; a small "Show link" button
//    reveals the decoded address in an LTR block with "Copy" (the original
//    address) and "Open".
//  - RPCS3 IP/Host Switches text (host=IPv4 joined by &&) is replaced by "Show
//    IP/Host" (an LTR monospace block) and "Copy IP/Host" (the exact English
//    source text from Source, falling back to Text).
//  - A trailing Wiki "Category:..." is not shown.
//  - In Arabic text each whole English segment (see LtrSegments) is one LTR
//    en-US Run, so v0.0.21-13399 never shows as 13399-v0.0.21 and
//    "[Regression] Prototype [BLUS30145] (#19410)" keeps its order and brackets.
// Only absolute http/https addresses are ever handed to the shell.
public static class LinkText
{
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

    // The English original of Text when Text is a reviewed translation; IP/Host
    // copies always come from it.
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source",
            typeof(string),
            typeof(LinkText),
            new PropertyMetadata(null, Changed));

    // False only where nothing can be clicked (a ToolTip closes on the first
    // click inside it): a link shows its label or word as plain text, with no
    // Hyperlink, reveal, copy, or open button. Default true everywhere else.
    public static readonly DependencyProperty InteractiveProperty =
        DependencyProperty.RegisterAttached(
            "Interactive",
            typeof(bool),
            typeof(LinkText),
            new PropertyMetadata(true, Changed));

    // Which links/IP-Host parts of one block the user has revealed.
    private static readonly DependencyProperty RevealedProperty =
        DependencyProperty.RegisterAttached(
            "Revealed",
            typeof(HashSet<int>),
            typeof(LinkText),
            new PropertyMetadata(null));

    private static readonly IValueConverter WidthLimit = new WidthLimitConverter();

    private static readonly XmlLanguage SegmentLanguage = XmlLanguage.GetLanguage("en-US");

    // Never at the start or at the end of an English segment: closing
    // brackets and punctuation lead nothing, and the sentence's own period or
    // an opening bracket ends nothing.
    private const string SegmentLeadTrim = ",;:!?)]}";

    private const string SegmentTailTrim = ".,;:!?([{";

    private const string Openers = "([{";

    private const string Closers = ")]}";

    public static string? GetText(DependencyObject element) =>
        (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) =>
        element.SetValue(TextProperty, value);

    public static string? GetTarget(DependencyObject element) =>
        (string?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, string? value) =>
        element.SetValue(TargetProperty, value);

    public static string? GetSource(DependencyObject element) =>
        (string?)element.GetValue(SourceProperty);

    public static void SetSource(DependencyObject element, string? value) =>
        element.SetValue(SourceProperty, value);

    public static bool GetInteractive(DependencyObject element) =>
        (bool)element.GetValue(InteractiveProperty);

    public static void SetInteractive(DependencyObject element, bool value) =>
        element.SetValue(InteractiveProperty, value);

    // Replaceable for tests only, so no test touches the real clipboard or
    // starts a browser.
    public static Action<string> CopyText { get; set; } = CopyToClipboard;

    public static Func<string, bool> OpenUrl { get; set; } = Open;

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is TextBlock block)
        {
            block.ClearValue(RevealedProperty);
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

        var slot = 0;
        var arabic = NoteSvc.ContainsArabic(text);
        if (WikiLinks.IsSafeUrl(target))
        {
            // A value that is itself an address is named with a word.
            AddLink(block, WikiLinks.IsSafeUrl(text.Trim()) ? null : text, target!, slot, arabic);
            TrimTrailingBreak(block);
            return;
        }

        foreach (var part in NoteParts.Split(text, GetSource(block)))
        {
            switch (part.Kind)
            {
                case NoteParts.PartKind.Text:
                    block.Inlines.AddRange(TextRuns(part.Text, arabic));
                    break;
                case NoteParts.PartKind.Link:
                    AddLink(block, part.Text, part.Value!, slot++, arabic);
                    break;
                case NoteParts.PartKind.BareLink:
                    AddLink(block, null, part.Value!, slot++, arabic);
                    break;
                case NoteParts.PartKind.IpHost:
                    AddIpHost(block, part.Value!, slot++);
                    break;
            }
        }

        TrimTrailingBreak(block);
    }

    // Arabic text: WPF reorders English inside the RTL paragraph (with ar-SA
    // digits resolve as Arabic numbers, so v0.0.21-13399 shows as
    // 13399-v0.0.21, and a space or bracket between two English parts takes
    // the paragraph's direction), and LRM, LRI or LRE do not prevent that. Each
    // whole English segment becomes one LTR en-US Run instead; isolating each
    // token alone broke "[Regression] Prototype [BLUS30145] (#19410)" into
    // pieces read right to left. Only Run properties change: the text, its
    // automation name and every copy stay the source characters. Text without
    // Arabic, or without English, stays one plain Run.
    private static List<Run> TextRuns(string text, bool arabic)
    {
        var segments = arabic ? LtrSegments(text) : [];
        if (segments.Count == 0)
        {
            return [new Run(text)];
        }

        var runs = new List<Run>();
        var position = 0;
        foreach (var (start, length) in segments)
        {
            if (start > position)
            {
                runs.Add(new Run(text[position..start]));
            }

            runs.Add(new Run(text.Substring(start, length))
            {
                FlowDirection = FlowDirection.LeftToRight,
                Language = SegmentLanguage
            });
            position = start + length;
        }

        if (position < text.Length)
        {
            runs.Add(new Run(text[position..]));
        }

        return runs;
    }

    // English segments of Arabic text: the longest stretch with no Arabic
    // character (letters, Arabic punctuation and digits), line break or
    // guillemet, so Latin words, digits, spaces, dashes and the technical
    // symbols between them (# % / \ . :) stay together. Trimmed of spaces, of
    // closing punctuation (the Arabic sentence's period stays outside) and of
    // brackets or quotes it does not close; kept when it has a Latin letter or
    // an ASCII digit. A Hyperlink label is split on its own, so no segment
    // crosses a link.
    internal static IReadOnlyList<(int Start, int Length)> LtrSegments(string text)
    {
        var segments = new List<(int Start, int Length)>();
        var index = 0;
        while (index < text.Length)
        {
            if (EndsSegment(text[index]))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < text.Length && !EndsSegment(text[index]))
            {
                index++;
            }

            var end = index;
            TrimSegment(text, ref start, ref end);
            for (var at = start; at < end; at++)
            {
                if (char.IsAsciiLetterOrDigit(text[at]) || text[at] is >= 'À' and <= 'ɏ')
                {
                    segments.Add((start, end - start));
                    break;
                }
            }
        }

        return segments;
    }

    private static bool EndsSegment(char ch) =>
        ch is '\r' or '\n' or '«' or '»' || NoteSvc.ContainsArabic(ch.ToString());

    private static void TrimSegment(string text, ref int start, ref int end)
    {
        while (end > start)
        {
            if (char.IsWhiteSpace(text[start]) || SegmentLeadTrim.Contains(text[start], StringComparison.Ordinal) ||
                !IsClosed(text, start, end, start))
            {
                start++;
            }
            else if (char.IsWhiteSpace(text[end - 1]) || SegmentTailTrim.Contains(text[end - 1], StringComparison.Ordinal) ||
                     !IsClosed(text, start, end, end - 1))
            {
                end--;
            }
            else
            {
                return;
            }
        }
    }

    // Whether the character at an edge of text[start..end] is not a bracket or
    // quote, or has its partner inside the segment.
    private static bool IsClosed(string text, int start, int end, int at)
    {
        var ch = text[at];
        if (ch is '"' or '\'')
        {
            return text.AsSpan(start, end - start).Count(ch) % 2 == 0;
        }

        if (!Openers.Contains(ch, StringComparison.Ordinal) && !Closers.Contains(ch, StringComparison.Ordinal))
        {
            return true;
        }

        var open = new Stack<int>();
        for (var index = start; index < end; index++)
        {
            if (Openers.Contains(text[index], StringComparison.Ordinal))
            {
                open.Push(index);
            }
            else if (Closers.Contains(text[index], StringComparison.Ordinal) && open.Count > 0 &&
                     Openers[Closers.IndexOf(text[index], StringComparison.Ordinal)] == text[open.Peek()] &&
                     (open.Pop() == at || index == at))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddLink(TextBlock block, string? label, string url, int slot, bool arabic)
    {
        IReadOnlyList<Run> display;
        if (label is null)
        {
            var word = new Run();
            word.SetBinding(Run.TextProperty, Lang(NoteParts.IsWikiPage(url) ? "Link.WikiPage" : "Link.Generic"));
            display = [word];
        }
        else
        {
            display = TextRuns(label, arabic);
        }

        if (!GetInteractive(block))
        {
            block.Inlines.AddRange(display);
            return;
        }

        var link = new Hyperlink
        {
            NavigateUri = new Uri(url, UriKind.Absolute),
            Cursor = Cursors.Hand,
            TextDecorations = null
        };
        link.Inlines.AddRange(display);
        link.SetResourceReference(TextElement.ForegroundProperty, "Accent.Default");
        link.MouseEnter += (_, _) => link.TextDecorations = TextDecorations.Underline;
        link.MouseLeave += (_, _) => link.TextDecorations = null;
        link.RequestNavigate += (_, e) =>
        {
            e.Handled = true;
            OpenUrl(url);
        };
        block.Inlines.Add(link);

        var revealed = IsRevealed(block, slot);
        AddButton(block, revealed ? "Link.Hide" : "Link.Show", "LinkText.Link.Show", () => Toggle(block, slot));
        if (!revealed)
        {
            return;
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        actions.SetResourceReference(FrameworkElement.FlowDirectionProperty, "App.TextFlowDirection");
        actions.Children.Add(Button("Link.Copy", "LinkText.Link.Copy", () => CopyText(url), new Thickness(0)));
        actions.Children.Add(Button("Link.Open", "LinkText.Link.Open", () => OpenUrl(url), new Thickness(4, 0, 0, 0)));
        AddRevealed(block, Decoded(url), actions);
    }

    private static void AddIpHost(TextBlock block, string value, int slot)
    {
        // Without buttons the switches stay hidden; no IP/Host text is shown.
        if (!GetInteractive(block))
        {
            return;
        }

        var revealed = IsRevealed(block, slot);
        AddButton(block, revealed ? "IpHost.Hide" : "IpHost.Show", "LinkText.IpHost.Show", () => Toggle(block, slot));
        AddButton(block, "IpHost.Copy", "LinkText.IpHost.Copy", () => CopyText(value));
        if (revealed)
        {
            AddRevealed(block, value, null);
        }
    }

    // The revealed value on its own line: LTR, monospace, wrapped inside the
    // host block's width.
    private static void AddRevealed(TextBlock block, string value, UIElement? actions)
    {
        var text = new TextBlock
        {
            Text = value,
            FlowDirection = FlowDirection.LeftToRight,
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Language = XmlLanguage.GetLanguage("en-US"),
            FontFamily = block.TryFindResource("Type.Font.Technical") as FontFamily
                         ?? new FontFamily("Cascadia Mono, Consolas, Segoe UI Mono")
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Text.Primary");
        AutomationProperties.SetAutomationId(text, "LinkText.RevealedText");

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(text);
        if (actions is not null)
        {
            content.Children.Add(actions);
        }

        var border = new Border
        {
            Child = content,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 4, 0, 4)
        };
        border.SetResourceReference(Border.BackgroundProperty, "Surface.Subtle");
        border.SetResourceReference(Border.BorderBrushProperty, "Stroke.Subtle");
        border.SetBinding(FrameworkElement.MaxWidthProperty, new Binding(nameof(FrameworkElement.ActualWidth))
        {
            Source = block,
            Converter = WidthLimit
        });
        AutomationProperties.SetAutomationId(border, "LinkText.Revealed");

        block.Inlines.Add(new LineBreak());
        block.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Top });
        block.Inlines.Add(new LineBreak());
    }

    private static void AddButton(TextBlock block, string key, string id, Action click)
    {
        block.Inlines.Add(new InlineUIContainer(Button(key, id, click, new Thickness(4, 0, 0, 0)))
        {
            BaselineAlignment = BaselineAlignment.Center
        });
    }

    private static Button Button(string key, string id, Action click, Thickness margin)
    {
        // The design system's small button (Fluent compact size, gate-checked).
        var button = new Button
        {
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "FluentButton.Compact");
        // The label is UI language text: it reads in the UI direction even
        // inside an LTR English original or a revealed LTR block.
        button.SetResourceReference(FrameworkElement.FlowDirectionProperty, "App.TextFlowDirection");
        button.SetBinding(ContentControl.ContentProperty, Lang(key));
        button.SetBinding(AutomationProperties.NameProperty, Lang(key));
        AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, e) =>
        {
            e.Handled = true;
            click();
        };
        return button;
    }

    private static Binding Lang(string key) =>
        new($"[{key}]")
        {
            Source = UiText.Current,
            Mode = BindingMode.OneWay
        };

    private static bool IsRevealed(TextBlock block, int slot) =>
        block.GetValue(RevealedProperty) is HashSet<int> revealed && revealed.Contains(slot);

    private static void Toggle(TextBlock block, int slot)
    {
        var revealed = block.GetValue(RevealedProperty) as HashSet<int> ?? [];
        if (!revealed.Remove(slot))
        {
            revealed.Add(slot);
        }

        block.SetValue(RevealedProperty, revealed);
        Rebuild(block);
    }

    private static void TrimTrailingBreak(TextBlock block)
    {
        if (block.Inlines.LastInline is LineBreak last)
        {
            block.Inlines.Remove(last);
        }
    }

    // Decoded for reading (Demon%27s_Souls -> Demon's_Souls). An address whose
    // decoded form holds control or bidi format characters is shown encoded.
    private static string Decoded(string url)
    {
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(url);
        }
        catch (UriFormatException)
        {
            return url;
        }

        return decoded.Any(ch => CharUnicodeInfo.GetUnicodeCategory(ch) is UnicodeCategory.Control or UnicodeCategory.Format)
            ? url
            : decoded;
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // The clipboard is busy in another process; nothing is copied.
        }
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

    // Keeps a revealed block inside its host text; unlimited before layout.
    private sealed class WidthLimitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double width && width > 0 ? width : double.PositiveInfinity;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
