using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Safe link extraction for user-facing Wiki text. Only absolute http/https
// URLs become links; nothing else in the text is ever executable.
public static class WikiLinks
{
    public sealed record Segment(string Text, string? Url);

    private static readonly Regex UrlPattern = new(
        @"https?://[^\s<>""']+",
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

        foreach (Match match in UrlPattern.Matches(text))
        {
            var url = match.Value.TrimEnd('.', ',', ';', ':', ')', ']', '!', '?', '*');
            if (!IsSafeUrl(url))
            {
                continue;
            }

            if (match.Index > position)
            {
                segments.Add(new Segment(text[position..match.Index], null));
            }

            segments.Add(new Segment(url, url));
            position = match.Index + url.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new Segment(text[position..], null));
        }

        return segments;
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
