using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Safe link extraction for user-facing Wiki text. Only absolute http/https
// URLs become links; nothing else in the text is ever executable. Wiki
// external links written as [url label] become one segment whose Text is the
// label and whose Url is the address; the brackets and the url are dropped
// from the visible text. A bare URL is a segment whose Text is the url.
public static class WikiLinks
{
    public sealed record Segment(string Text, string? Url);

    private static readonly Regex LinkPattern = new(
        @"\[(?<labelUrl>https?://[^\s\[\]<>""']+)[ \t]+(?<label>[^\[\]\r\n]*?[^\s\[\]])[ \t]*\]|(?<bare>https?://[^\s<>""']+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsSafeUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https" &&
               !string.IsNullOrEmpty(uri.Host);
    }

    public static IReadOnlyList<Segment> Split(string text)
    {
        var segments = new List<Segment>();
        var position = 0;

        foreach (Match match in LinkPattern.Matches(text))
        {
            if (match.Groups["labelUrl"].Success)
            {
                var labelUrl = match.Groups["labelUrl"].Value;
                if (IsSafeUrl(labelUrl))
                {
                    AddText(segments, text, position, match.Index);
                    segments.Add(new Segment(match.Groups["label"].Value, labelUrl));
                    position = match.Index + match.Length;
                }

                continue;
            }

            var url = match.Value.TrimEnd('.', ',', ';', ':', ')', ']', '!', '?', '*');
            if (!IsSafeUrl(url))
            {
                continue;
            }

            AddText(segments, text, position, match.Index);
            segments.Add(new Segment(url, url));
            position = match.Index + url.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new Segment(text[position..], null));
        }

        return segments;
    }

    private static void AddText(List<Segment> segments, string text, int start, int end)
    {
        if (end > start)
        {
            segments.Add(new Segment(text[start..end], null));
        }
    }

    public static bool IsOfficialRpcs3Url(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Scheme == "https" &&
               (uri.Host.Equals("rpcs3.net", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".rpcs3.net", StringComparison.OrdinalIgnoreCase));
    }
}

#pragma warning restore IDE0130
