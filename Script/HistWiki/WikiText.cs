using System.Text;
using System.Text.RegularExpressions;

namespace HistWiki;

public sealed record ConfigParam(string RawKey, string Key, string RawValue, string CleanValue, bool Positional, bool Inline = false, bool CommentSpansLines = false);

public sealed class ConfigParse
{
    public int TemplateCount;
    public List<ConfigParam> Params = [];
    public List<string> Issues = [];
    public bool Ok => TemplateCount == 1 && Issues.Count == 0;
}

public sealed record SchemaRef(string Key, string RawName, string? Section);

// Deterministic wikitext scanners. Nothing here guesses: an input the scanner
// cannot parse unambiguously is reported as an issue and the caller fails closed.
public static class WikiText
{
    private static readonly Regex Comment = new(@"<!--.*?(?:-->|$)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    // A comment that opens in this value and closes on a later line (for example a
    // "->>" typo): MediaWiki hides everything up to the next "-->".
    public static bool SpansLines(string text)
    {
        var i = 0;
        while ((i = text.IndexOf("<!--", i, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
            var stop = end < 0 ? text.Length : end;
            if (text.IndexOf('\n', i, stop - i) >= 0) return true;
            if (end < 0) break;
            i = end + 3;
        }

        return false;
    }

    public static string StripComments(string text) => Comment.Replace(text, string.Empty);

    public static string Collapse(string text) => Spaces.Replace(text, " ").Trim();

    public static string NormalizeKey(string raw) => Collapse(StripComments(raw)).ToLowerInvariant();

    private static bool At(string s, int i, string token) =>
        i + token.Length <= s.Length && string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

    private static bool AtIgnoreCase(string s, int i, string token) =>
        i + token.Length <= s.Length && string.Compare(s, i, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;

    // Skips a comment or nowiki span starting at i; returns the new index, or -1 when i is not one.
    private static int SkipOpaque(string s, int i, List<string>? issues)
    {
        if (At(s, i, "<!--"))
        {
            var end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
            if (end < 0)
            {
                issues?.Add("UNTERMINATED_COMMENT");
                return s.Length;
            }

            return end + 3;
        }

        if (AtIgnoreCase(s, i, "<nowiki>"))
        {
            var end = s.IndexOf("</nowiki>", i + 8, StringComparison.OrdinalIgnoreCase);
            return end < 0 ? s.Length : end + 9;
        }

        return -1;
    }

    private sealed record Segment(int Start, int End, int Eq);

    // Scans one {{...}} starting at i (which points at "{{"). Splits on top-level
    // pipes and records the first top-level '=' of each segment.
    private static bool ScanTemplate(string s, int i, List<string>? issues, out int end, out List<Segment> segments)
    {
        segments = [];
        end = s.Length;
        var depth = 0;
        var link = 0;
        var pos = i + 2;
        var segStart = pos;
        var eq = -1;
        while (pos < s.Length)
        {
            var skip = SkipOpaque(s, pos, issues);
            if (skip >= 0) { pos = skip; continue; }

            if (At(s, pos, "{{")) { depth++; pos += 2; continue; }
            if (At(s, pos, "}}"))
            {
                if (depth == 0)
                {
                    segments.Add(new Segment(segStart, pos, eq));
                    end = pos + 2;
                    return true;
                }

                depth--;
                pos += 2;
                continue;
            }

            if (At(s, pos, "[[")) { link++; pos += 2; continue; }
            if (At(s, pos, "]]")) { if (link > 0) link--; pos += 2; continue; }
            var c = s[pos];
            if (c == '|' && depth == 0 && link == 0)
            {
                segments.Add(new Segment(segStart, pos, eq));
                segStart = pos + 1;
                eq = -1;
            }
            else if (c == '=' && depth == 0 && link == 0 && eq < 0)
            {
                eq = pos;
            }

            pos++;
        }

        return false;
    }

    // Finds the matching "}}" of any template starting at i without splitting.
    // An unmatched "{{" is literal text in MediaWiki, so it yields i + 2 and the
    // scan continues inside it instead of swallowing the rest of the page.
    private static int SkipTemplate(string s, int i)
    {
        return ScanTemplate(s, i, null, out var end, out _) ? end : i + 2;
    }

    private static string TemplateName(string s, int i)
    {
        var pos = i + 2;
        var sb = new StringBuilder();
        while (pos < s.Length && s[pos] != '|' && !At(s, pos, "}}") && !At(s, pos, "{{"))
        {
            var skip = SkipOpaque(s, pos, null);
            if (skip >= 0) { pos = skip; continue; }
            sb.Append(s[pos]);
            pos++;
        }

        return Collapse(sb.ToString()).ToLowerInvariant();
    }

    public static ConfigParse ParseConfig(string text)
    {
        var result = new ConfigParse();
        var i = 0;
        while (i < text.Length)
        {
            var skip = SkipOpaque(text, i, result.Issues);
            if (skip >= 0) { i = skip; continue; }

            if (!At(text, i, "{{")) { i++; continue; }

            var name = TemplateName(text, i);
            if (name != "config" && name != "template:config")
            {
                i = SkipTemplate(text, i);
                continue;
            }

            result.TemplateCount++;
            if (!ScanTemplate(text, i, result.Issues, out var end, out var segments))
            {
                result.Issues.Add("UNTERMINATED_CONFIG_TEMPLATE");
                break;
            }

            if (result.TemplateCount == 1)
            {
                var positional = 0;
                foreach (var seg in segments.Skip(1))
                {
                    var body = text[seg.Start..seg.End];
                    if (seg.Eq < 0)
                    {
                        positional++;
                        if (Collapse(StripComments(body)).Length > 0)
                        {
                            result.Params.Add(new ConfigParam(string.Empty, "#" + positional, body, Collapse(StripComments(body)), true));
                            result.Issues.Add("POSITIONAL_PARAMETER");
                        }

                        continue;
                    }

                    var lineStart = text.LastIndexOf('\n', Math.Max(0, seg.Start - 2)) + 1;
                    var inline = seg.Start >= 1 && lineStart < seg.Start - 1 && StripComments(text[lineStart..(seg.Start - 1)]).Contains('=', StringComparison.Ordinal);
                    var rawKey = text[seg.Start..seg.Eq];
                    var rawValue = text[(seg.Eq + 1)..seg.End];
                    var key = NormalizeKey(rawKey);
                    if (key.Length == 0)
                    {
                        result.Issues.Add("EMPTY_PARAMETER_KEY");
                        continue;
                    }

                    result.Params.Add(new ConfigParam(rawKey.Trim(), key, rawValue, Collapse(StripComments(rawValue)), false, inline, SpansLines(rawValue)));
                }
            }

            i = end;
        }

        if (result.TemplateCount > 1)
        {
            result.Issues.Add("MULTIPLE_CONFIG_TEMPLATES");
        }

        return result;
    }

    // Every {{{parameter|default}}} reference in a template revision, with the
    // "<!-- ... configuration -->" section it appears under in the first
    // {{#if: block (null when that block does not carry sections).
    public static List<SchemaRef> ParseSchema(string templateText)
    {
        var refs = new List<SchemaRef>();
        var firstIf = templateText.IndexOf("{{#if:", StringComparison.OrdinalIgnoreCase);
        var blockEnd = -1;
        if (firstIf >= 0 && ScanTemplate(templateText, firstIf, null, out _, out var segs) && segs.Count > 0)
        {
            blockEnd = segs[0].End;
        }

        string? section = null;
        var i = 0;
        var s = templateText;
        while (i < s.Length)
        {
            if (At(s, i, "<!--"))
            {
                var end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                var stop = end < 0 ? s.Length : end;
                if (firstIf >= 0 && i > firstIf && i < blockEnd)
                {
                    section = Collapse(s[(i + 4)..stop]);
                }

                i = end < 0 ? s.Length : end + 3;
                continue;
            }

            if (At(s, i, "{{{"))
            {
                var pos = i + 3;
                var start = pos;
                while (pos < s.Length && s[pos] != '|' && s[pos] != '}' && s[pos] != '{') pos++;
                var raw = s[start..pos];
                var inBlock = firstIf >= 0 && i > firstIf && i < blockEnd;
                if (pos < s.Length && s[pos] != '{' && (s[pos] == '|' || At(s, pos, "}}}")))
                {
                    var key = NormalizeKey(raw);
                    if (key.Length > 0)
                    {
                        refs.Add(new SchemaRef(key, Collapse(raw), inBlock ? section : null));
                    }
                }

                i = start;
                continue;
            }

            i++;
        }

        return refs;
    }

    // Named parameters of the first {{gameid|...}} on a page revision.
    public static Dictionary<string, string>? ParseGameId(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var skip = SkipOpaque(text, i, null);
            if (skip >= 0) { i = skip; continue; }
            if (!At(text, i, "{{")) { i++; continue; }
            if (TemplateName(text, i) == "gameid" && ScanTemplate(text, i, null, out _, out var segs))
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var seg in segs.Skip(1))
                {
                    if (seg.Eq < 0) continue;
                    map[NormalizeKey(text[seg.Start..seg.Eq])] = Collapse(StripComments(text[(seg.Eq + 1)..seg.End]));
                }

                return map;
            }

            i += 2;
        }

        return null;
    }
}
