using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Display-only split of a note (Wiki, RPCN, cat.json) into what the view may
// show. Nothing here changes stored notes or translation tables.
//  - RPCS3 IP/Host Switches text (host=IPv4, several joined by &&) becomes one
//    IpHost part; its Value is the exact text of the English source, so a copy
//    never carries a translation, an LRM or a line break.
//  - A Wiki "Category:..." stuck at the end of a line is dropped; in a reviewed
//    Arabic text the translated tail ("الفئة: ...") is dropped only when the
//    English source had a Category tail.
//  - Links follow WikiLinks: only http/https; [url label] is a Link with its
//    label, a bare address is a BareLink the view names with a word.
public static class NoteParts
{
    public enum PartKind
    {
        Text,
        Link,
        BareLink,
        IpHost
    }

    // Text: the visible words (Link: the label; BareLink: the address; IpHost:
    // empty). Value: the address for links, the exact switch text for IpHost.
    public sealed record Part(PartKind Kind, string Text, string? Value);

    private const string Host =
        @"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+";

    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])";

    private const string Entry = Host + "=" + Octet + @"(?:\." + Octet + "){3}";

    // Strict: a dotted host name, '=', a dotted-quad IPv4 with no leading
    // zeros, repeated with "&&". Starts after whitespace or an opening mark and
    // ends before whitespace, a closing mark or a sentence period.
    private static readonly Regex IpHostPattern = new(
        @"(?<=^|[\s(\[""'])" + Entry + "(?:&&" + Entry + @")*(?=$|[\s)\]""',;!?]|\.(?![0-9A-Za-z]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex CategoryTail = new(
        @"(?:^|[ \t]+)Category:[^\r\n]*(?=\r?$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ArabicCategoryTail = new(
        @"(?:^|[ \t]+)الفئة:[^\r\n]*(?=\r?$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(1));

    public static IReadOnlyList<string> IpHostSwitches(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : [.. IpHostPattern.Matches(text).Select(match => match.Value)];

    // display: what the view would show (the source or its reviewed
    // translation). source: the English original, when display may differ.
    public static IReadOnlyList<Part> Split(string text, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var english = source ?? text;
        var visible = CategoryTail.Replace(text, string.Empty);
        if (source is not null && CategoryTail.IsMatch(english))
        {
            visible = ArabicCategoryTail.Replace(visible, string.Empty);
        }

        var exact = IpHostSwitches(english);
        var parts = new List<Part>();
        var position = 0;
        var index = 0;
        foreach (Match match in IpHostPattern.Matches(visible))
        {
            AddLinks(parts, visible[position..match.Index]);
            parts.Add(new Part(PartKind.IpHost, string.Empty, index < exact.Count ? exact[index] : match.Value));
            index++;
            position = match.Index + match.Length;
        }

        AddLinks(parts, visible[position..]);
        return parts;
    }

    public static bool IsWikiPage(string? url) =>
        WikiLinks.IsSafeUrl(url) &&
        new Uri(url!, UriKind.Absolute).Host.Equals("wiki.rpcs3.net", StringComparison.OrdinalIgnoreCase);

    private static void AddLinks(List<Part> parts, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        foreach (var segment in WikiLinks.Split(text))
        {
            parts.Add(segment.Url is null
                ? new Part(PartKind.Text, segment.Text, null)
                : new Part(segment.Text == segment.Url ? PartKind.BareLink : PartKind.Link, segment.Text, segment.Url));
        }
    }
}

#pragma warning restore IDE0130
