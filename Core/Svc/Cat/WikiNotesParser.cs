using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Extracts free-text Wiki evidence (Known Issues / Special Notes / platform
// subsections) and the {{netplay}} template from raw MediaWiki page text.
// This is deliberately independent of WikiSvc/WikiCorpus.cs: it never turns
// a note into a setting, never writes to the settings catalog, and reads
// only the raw wikitext the caller supplies. It classifies by explicit
// textual cues only — it does not infer meaning the source text does not
// state (see the task examples this mirrors: framerate/menu tie-in stays a
// compatibility note, frame-limiter timing stays a warning, macOS/version
// combinations stay platform+version conditional, RPCN crashes stay
// network/RPCN evidence).
public static class WikiNotesParser
{
    private static readonly HashSet<string> RelevantHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "known issues",
        "special notes",
        "notes",
        "note",
        "issues",
        "bugs",
        "bug",
        "compatibility",
        "compatibility notes",
        "workarounds"
    };

    private static readonly Regex HeadingLine = new(
        @"^(?<level>==+)\s*(?<title>.+?)\s*=+\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BulletLine = new(
        @"^\*+\s*(?<text>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NetplayBlock = new(
        @"\{\{\s*netplay\s*(?<body>.*?)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex NetplayParam = new(
        @"^\s*\|\s*(?<key>[a-z0-9 ]+?)\s*=\s*(?<value>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex TemplateOpen = new(
        @"\{\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateClose = new(
        @"\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlComment = new(
        @"<!--.*?--!?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex BreakTag = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlTag = new(
        @"</?[a-z][^>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex IssueRef = new(
        @"\{\{issue\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PrRef = new(
        @"\{\{pr\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Lines that survive Clean() as pure MediaWiki residue: an image reduced
    // to its size option ([[File:x.png|200px]] -> "200px") or a category tag.
    // Nothing else is dropped (IPs, hosts entries, links and RPCN lines are
    // user-facing information).
    private static readonly Regex WikiResidueLine = new(
        @"^(?:\d+px|Category:.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WikiLink = new(
        @"\[\[(?<body>.*?)\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex PlatformWord = new(
        @"\b(macOS|Mac OS|OS X|Linux|Windows)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VersionNumber = new(
        @"\b\d+\.\d+(?:\.\d+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A dotted number inside an IPv4 address (bare or host=IPv4) is never a
    // game version, e.g. 206.189.232 in "ds-eu-c.scej-online.jp=206.189.232.242".
    private static readonly Regex Ipv4Address = new(
        @"\b\d{1,3}(?:\.\d{1,3}){3}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A dotted number inside RPCS3 build wording (v0.0.N-NNNN or rpcs3-v0.0.N)
    // is never a game version, e.g. 0.21 in "after v0.0.21-13399" or 0.5 in
    // "rpcs3-v0.0.5-6664". Build scope still comes only from Rpcs3BuildNumber.
    private static readonly Regex Rpcs3BuildWording = new(
        @"\brpcs3-v0\.0\.\d+(?:-\d+)?\b|\bv0\.0\.\d+-\d+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VersionContextWord = new(
        @"\b(version|update|past|below|above|prior to|before|after|newer|older)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HardwareWord = new(
        @"\b(AMD|Nvidia|NVIDIA|Intel|Vulkan|OpenGL|GPU|CPU)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RpcnWord = new(
        @"\bRPCN\b|\bnetplay\b|\bmultiplayer\b|\bonline\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NetworkContextWord = new(
        @"\bsplit-screen\b|\blobby\b|\blobbies\b|\bserver\b|\bconnect\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EditionWord = new(
        @"\b(digital|disc|physical|retail)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Rpcs3BuildNumber = new(
        @"\b0\.0\.\d+(?:-\d+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SerialNumber = new(
        @"(?<![A-Z0-9])[BNM][A-Z]{3}\d{5}(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchWord = new(
        @"\bpatch(?:es)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WarningWord = new(
        @"\bdo not\b|\bdon't\b|\bmust not\b|\bavoid\b|\bcrash\b|\bfreeze[s]?\b|\bcorrupt",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static WikiPageEvidence Parse(
        string page,
        IReadOnlyList<string> serials,
        string rawWikiText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(rawWikiText);

        return new WikiPageEvidence(
            page,
            serials,
            ParseNotes(page, rawWikiText),
            ParseOnline(page, rawWikiText));
    }

    private static List<WikiFreeTextNote> ParseNotes(
        string page,
        string rawWikiText)
    {
        var notes = new List<WikiFreeTextNote>();
        var lines = rawWikiText.Replace("\r\n", "\n").Split('\n');

        var inRelevantSection = false;
        var sourceSection = string.Empty;
        var buffer = new List<string>();

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            var text = Clean(string.Join(' ', buffer));
            buffer.Clear();

            if (!string.IsNullOrWhiteSpace(text) && !WikiResidueLine.IsMatch(text))
            {
                notes.Add(Classify(page, sourceSection, text));
            }
        }

        var templateDepth = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // A multi-line template ({{patch ...}}, {{netplay ...}}) is data,
            // never prose: its body lines (YAML patch content, table rows)
            // must not become notes. Single-line {{issue|123}} style
            // references open and close on the same line and stay in prose.
            var visible = HtmlComment.Replace(line, string.Empty);
            var opens = TemplateOpen.Matches(visible).Count;
            var closes = TemplateClose.Matches(visible).Count;
            if (templateDepth > 0 || opens > closes)
            {
                Flush();
                templateDepth = Math.Max(0, templateDepth + opens - closes);
                continue;
            }

            var heading = HeadingLine.Match(line);

            if (heading.Success)
            {
                Flush();
                var level = heading.Groups["level"].Value.Length;
                var title = heading.Groups["title"].Value.Trim();

                if (level <= 2)
                {
                    inRelevantSection = RelevantHeadings.Contains(title);
                    sourceSection = title;
                }
                else if (inRelevantSection)
                {
                    sourceSection = title;
                }

                continue;
            }

            if (!inRelevantSection)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            var trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith("{{", StringComparison.Ordinal) ||
                trimmedLine.StartsWith("}}", StringComparison.Ordinal) ||
                trimmedLine.StartsWith('|'))
            {
                // Template invocations (e.g. {{netplay ...}}, {{patch ...}})
                // can appear interleaved with prose sections without a
                // heading boundary. They are never note prose; ParseOnline
                // handles {{netplay}} separately regardless of position.
                Flush();
                continue;
            }

            var bullet = BulletLine.Match(line);
            if (bullet.Success)
            {
                Flush();
                buffer.Add(bullet.Groups["text"].Value);
                Flush();
                continue;
            }

            buffer.Add(line);
        }

        Flush();
        return notes;
    }

    private static WikiFreeTextNote Classify(
        string page,
        string sourceSection,
        string text)
    {
        var edition = EditionWord.Match(text);
        var hardware = HardwareWord.Match(text);
        var build = Rpcs3BuildNumber.Match(text);
        var serial = SerialNumber.Match(text);

        // Scope is preserved verbatim from explicit cues only; it never
        // changes the class chosen by ClassifyCore.
        return ClassifyCore(page, sourceSection, text) with
        {
            EditionCondition = edition.Success ? edition.Value.ToLowerInvariant() : null,
            HardwareCondition = hardware.Success ? hardware.Value : null,
            Rpcs3BuildCondition = build.Success ? build.Value : null,
            SerialCondition = serial.Success ? serial.Value.ToUpperInvariant() : null,
            PatchDependent = PatchWord.IsMatch(text)
        };
    }

    private static WikiFreeTextNote ClassifyCore(
        string page,
        string sourceSection,
        string text)
    {
        var platformMatch = PlatformWord.Match(text);
        var excluded = Ipv4Address.Matches(text).Concat(Rpcs3BuildWording.Matches(text)).ToArray();
        var versionMatch = VersionNumber.Matches(text).FirstOrDefault(number =>
            !excluded.Any(span => number.Index >= span.Index && number.Index < span.Index + span.Length));
        var hasVersionContext = versionMatch is not null && VersionContextWord.IsMatch(text);
        var platform = platformMatch.Success ? platformMatch.Value : null;
        var version = hasVersionContext ? versionMatch?.Value : null;

        if (RpcnWord.IsMatch(text) && (NetworkContextWord.IsMatch(text) || WarningWord.IsMatch(text)))
        {
            var noteClass = text.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
                             text.Contains("workaround", StringComparison.OrdinalIgnoreCase) ||
                             text.Contains("disconnect", StringComparison.OrdinalIgnoreCase) ||
                             text.Contains("prevent", StringComparison.OrdinalIgnoreCase)
                ? WikiNoteClass.Workaround
                : WikiNoteClass.RpcnNote;
            return new WikiFreeTextNote(page, sourceSection, noteClass, text, version, platform);
        }

        if (platform is not null && version is not null)
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.PlatformConditionalNote, text, version, platform);
        }

        if (version is not null)
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.VersionSpecificNote, text, version, null);
        }

        if (platform is not null)
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.PlatformConditionalNote, text, null, platform);
        }

        if (HardwareWord.IsMatch(text))
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.HardwareConditionalNote, text, null, null);
        }

        if (PatchWord.IsMatch(text))
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.PatchSpecificNote, text, null, null);
        }

        if (RpcnWord.IsMatch(text))
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.NetworkNote, text, null, null);
        }

        if (WarningWord.IsMatch(text))
        {
            return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.Warning, text, null, null);
        }

        return new WikiFreeTextNote(page, sourceSection, WikiNoteClass.InformationalNote, text, null, null);
    }

    private static List<WikiOnlineEvidence> ParseOnline(
        string page,
        string rawWikiText)
    {
        var results = new List<WikiOnlineEvidence>();

        foreach (Match block in NetplayBlock.Matches(rawWikiText))
        {
            var body = block.Groups["body"].Value;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match param in NetplayParam.Matches(body))
            {
                var key = param.Groups["key"].Value.Trim();
                var value = Clean(param.Groups["value"].Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[key] = value;
                }
            }

            if (values.Count == 0)
            {
                continue;
            }

            values.TryGetValue("gamever", out var testedVersion);
            values.TryGetValue("rpcs3 version", out var rpcs3Version);
            values.TryGetValue("connect", out var connects);
            values.TryGetValue("lobbies", out var lobbies);
            values.TryGetValue("match", out var match);
            values.TryGetValue("features", out var features);
            values.TryGetValue("custom servers", out var customServers);
            values.TryGetValue("notes", out var notes);

            results.Add(new WikiOnlineEvidence(
                page,
                State(connects, lobbies, match, features),
                testedVersion,
                RequiredGameVersion: null,
                rpcs3Version,
                connects,
                lobbies,
                match,
                features,
                customServers,
                notes));
        }

        return results;
    }

    private static WikiOnlineState State(
        string? connects,
        string? lobbies,
        string? match,
        string? features)
    {
        var fields = new[] { connects, lobbies, match, features }
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field!.Trim())
            .ToArray();

        if (fields.Length == 0)
        {
            return WikiOnlineState.Unknown;
        }

        if (fields.Any(field => field.Equals("No", StringComparison.OrdinalIgnoreCase)))
        {
            return WikiOnlineState.NotSupported;
        }

        if (fields.All(field => field.Equals("Yes", StringComparison.OrdinalIgnoreCase)))
        {
            return WikiOnlineState.Supported;
        }

        if (fields.Any(field =>
                field.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                field.Equals("Partial", StringComparison.OrdinalIgnoreCase)))
        {
            return WikiOnlineState.TestedPartial;
        }

        return WikiOnlineState.Unknown;
    }

    private static string Clean(string value)
    {
        value = HtmlComment.Replace(value, string.Empty);
        value = BreakTag.Replace(value, " ");
        value = IssueRef.Replace(value, "RPCS3 issue #${id}");
        value = PrRef.Replace(value, "RPCS3 PR #${id}");
        value = WikiLink.Replace(value, match =>
        {
            var body = match.Groups["body"].Value;
            var separator = body.LastIndexOf('|');
            return separator >= 0
                ? body[(separator + 1)..].Trim()
                : body.Replace('_', ' ').Trim();
        });
        value = HtmlTag.Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value);
        value = value.Replace("'''", string.Empty).Replace("''", string.Empty);
        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}

#pragma warning restore IDE0130
