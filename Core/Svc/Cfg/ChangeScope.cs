using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Turns the scope a documented change states in its own Wiki note into structured
// conditions. Only fixed, anchored wordings are understood. Every wording that names a
// scope but cannot be compared (another edition, several vendors, a negation, a patch
// requirement) becomes an Unresolved condition, so it can only ever demote a change
// (Partial -> Inform), never promote or hide one. No token on its own decides anything.
public static class ChangeScope
{
    private static readonly Regex EditionNamed = new(
        @"\b(?<e>digital|psn|disc|physical|retail)\s+(?:edition|version|release|copy|only|games?|builds?)\b|\bonly\s+(?:for|on|in|with)\s+(?:the\s+)?(?<e>digital|disc)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EditionOther = new(
        @"\b(?:demo|trial|beta|alpha|preview|play\s?view)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Rpcs3Mention = new(
        @"(?<![\d.])(?<b>0\.0\.\d+(?:-\d+)?)(?![\d.]*\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GameVersion = new(
        @"(?:\b(?:version|ver\.?|update|patch|v)\s*(?<v>\d{1,2}\.\d{1,2})(?!\.?\d)|(?<![\d.])(?<v>\d{1,2}\.\d{1,2})(?!\.?\d)\s+(?:version|update|patch|or\s+(?:below|lower|older|earlier|above|higher|newer|later)|and\s+(?:below|lower|older|earlier|above|higher|newer|later)))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PlatformWord = new(
        @"\b(?<p>macOS|Mac OS|OS X|Linux|Windows)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // "only on/for/under Linux", "Linux only", "on/for Windows": a platform named anywhere else is free text.
    private static readonly Regex PlatformScope = new(
        @"\bonly\s+(?:on|for|in|under)\s+(?<p>macOS|Mac OS|OS X|Linux|Windows)\b|\b(?<p>macOS|Mac OS|OS X|Linux|Windows)\s+only\b|\b(?:on|for|under)\s+(?<p>macOS|Mac OS|OS X|Linux|Windows)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GpuVendor = new(
        @"\b(?<v>AMD|NVIDIA|Intel)\s+(?:GPUs?|graphics|drivers?|Radeon|GeForce|Arc|iGPUs?|cards?)\b|\b(?<v>Radeon|GeForce)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CpuVendor = new(
        @"\b(?<v>AMD|Intel)\s+(?:CPUs?|processors?|Ryzen|Core|Xeon|Threadripper)\b|\b(?<v>Ryzen|Threadripper)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RendererWord = new(
        @"\b(?<r>Vulkan|OpenGL)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // The only wordings that state a renderer as a scope: "only on/with/for Vulkan", "Vulkan only",
    // "on/with/under/using (the) Vulkan renderer|backend|API". A renderer named anywhere else is free
    // text (a recommendation, a comparison, a symptom) and stays Unresolved.
    private static readonly Regex RendererScope = new(
        @"\bonly\s+(?:on|with|for|in|when\s+using|using)\s+(?:the\s+)?(?<r>Vulkan|OpenGL)\b|\b(?<r>Vulkan|OpenGL)(?:\s+(?:renderer|backend|API))?\s+only\b|\b(?:on|with|under|using)\s+(?:the\s+)?(?<r>Vulkan|OpenGL)\s+(?:renderer|backend|API)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VendorWord = new(
        @"\b(?:AMD|NVIDIA|Intel)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchRequirement = new(
        @"\b(?:requires?|needs?|with|without|using|only\s+with)\s+(?:the\s+)?(?:[\w-]+\s+){0,3}patch(?:es)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Negation = new(
        @"\b(?:not|n't|never|avoid|except|instead of|unlike|other than)\b[^.;]{0,24}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<EvidenceScopeCondition> Conditions(string serial, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var list = new List<EvidenceScopeCondition>
        {
            new(EvidenceScopeKind.Serial, serial.Trim().ToUpperInvariant())
        };

        if (string.IsNullOrWhiteSpace(note))
        {
            return list;
        }

        var text = note;
        AddEdition(list, text);
        AddRpcs3Build(list, text);
        AddGameVersion(list, WithoutRpcs3(text));
        AddAnchored(list, text, PlatformWord, PlatformScope, "p", EvidenceScopeKind.Platform, NormalizePlatform);
        AddSingle(list, text, GpuVendor, "v", EvidenceScopeKind.GpuVendor, VendorKey);
        AddSingle(list, text, CpuVendor, "v", EvidenceScopeKind.CpuVendor, CpuVendorKey);
        AddAnchored(list, text, RendererWord, RendererScope, "r", EvidenceScopeKind.Renderer, RendererKey);
        AddBareVendors(list, text);

        if (PatchRequirement.IsMatch(text))
        {
            list.Add(Unresolved(EvidenceScopeKind.PatchState, "patch"));
        }

        return list;
    }

    private static EvidenceScopeCondition Unresolved(EvidenceScopeKind kind, string expected) =>
        new(kind, expected, ScopeRelation.Unresolved);

    private static string WithoutRpcs3(string text) => Rpcs3Mention.Replace(text, " ");

    private static bool IsNegated(string text, int index) =>
        Negation.IsMatch(text[Math.Max(0, index - 32)..index]);

    private static string NormalizePlatform(string value) =>
        value.Equals("Linux", StringComparison.OrdinalIgnoreCase)
            ? "Linux"
            : value.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                ? "Windows"
                : "macOS";

    private static string VendorKey(string value) => value.ToLowerInvariant() switch
    {
        "amd" or "radeon" => "amd",
        "nvidia" or "geforce" => "nvidia",
        _ => "intel"
    };

    private static string CpuVendorKey(string value) => value.ToLowerInvariant() switch
    {
        "amd" or "ryzen" or "threadripper" => "amd",
        _ => "intel"
    };

    // One distinct, non-negated mention is a comparable condition. Several distinct mentions or
    // a negated one name a scope this parser does not resolve.
    private static void AddSingle(
        List<EvidenceScopeCondition> list,
        string text,
        Regex regex,
        string group,
        EvidenceScopeKind kind,
        Func<string, string> normalize)
    {
        var matches = regex.Matches(text);
        if (matches.Count == 0)
        {
            return;
        }

        var values = matches.Select(m => normalize(m.Groups[group].Value)).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length != 1 || matches.Any(m => IsNegated(text, m.Index)))
        {
            list.Add(Unresolved(kind, string.Join('|', values)));
            return;
        }

        list.Add(new EvidenceScopeCondition(kind, values[0]));
    }

    private static string RendererKey(string value) =>
        value.Equals("vulkan", StringComparison.OrdinalIgnoreCase) ? "Vulkan" : "OpenGL";

    // A word is a comparable scope only through its anchored grammar and only when every mention of
    // that word class in the note is the same anchored, non-negated mention. Anything else is named
    // but Unresolved.
    private static void AddAnchored(
        List<EvidenceScopeCondition> list,
        string text,
        Regex word,
        Regex scope,
        string group,
        EvidenceScopeKind kind,
        Func<string, string> normalize)
    {
        var mentions = word.Matches(text);
        if (mentions.Count == 0)
        {
            return;
        }

        var all = mentions.Select(m => normalize(m.Groups[group].Value)).Distinct(StringComparer.Ordinal).ToArray();
        var anchored = scope.Matches(text);
        var scoped = anchored.Select(m => normalize(m.Groups[group].Value)).Distinct(StringComparer.Ordinal).ToArray();
        if (scoped.Length != 1 || all.Length != 1 || anchored.Any(m => IsNegated(text, m.Index)))
        {
            list.Add(Unresolved(kind, string.Join('|', all)));
            return;
        }

        list.Add(new EvidenceScopeCondition(kind, scoped[0]));
    }

    // A vendor name outside a recognised hardware phrase ("NVIDIA GPUs", "AMD Ryzen") does not say
    // which hardware it scopes; it is named but not comparable.
    private static void AddBareVendors(List<EvidenceScopeCondition> list, string text)
    {
        var covered = GpuVendor.Matches(text).Concat(CpuVendor.Matches(text))
            .Select(m => (m.Index, End: m.Index + m.Length))
            .ToArray();
        var bare = VendorWord.Matches(text)
            .Where(m => !covered.Any(c => m.Index >= c.Index && m.Index < c.End))
            .Select(m => VendorKey(m.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (bare.Length > 0)
        {
            list.Add(Unresolved(EvidenceScopeKind.GpuVendor, string.Join('|', bare)));
        }
    }

    private static void AddEdition(List<EvidenceScopeCondition> list, string text)
    {
        var named = EditionNamed.Matches(text);
        var other = EditionOther.IsMatch(text);
        if (named.Count == 0 && !other)
        {
            return;
        }

        var values = named
            .Select(m => m.Groups["e"].Value.ToLowerInvariant() is "digital" or "psn" ? "digital" : "disc")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Another edition (demo, trial, beta) named next to the game, several editions, or a negated
        // edition: which edition the sentence means is not comparable with the installed serial.
        if (other || values.Length != 1 || named.Any(m => IsNegated(text, m.Index)))
        {
            list.Add(Unresolved(EvidenceScopeKind.Edition, other ? "other-edition" : string.Join('|', values)));
            return;
        }

        list.Add(new EvidenceScopeCondition(EvidenceScopeKind.Edition, values[0]));
    }

    private static void AddRpcs3Build(List<EvidenceScopeCondition> list, string text)
    {
        var mentions = Rpcs3Mention.Matches(text).Select(m => m.Groups["b"].Value).Distinct(StringComparer.Ordinal).ToArray();
        if (mentions.Length == 0)
        {
            return;
        }

        if (mentions.Length > 1)
        {
            list.Add(Unresolved(EvidenceScopeKind.Rpcs3Build, string.Join('|', mentions)));
            return;
        }

        var relation = NoteScope.Rpcs3Relation(text, mentions[0]);
        // A bare version ("0.0.21") with no direction and no build number is not one build.
        if (relation == ScopeRelation.Equal && !mentions[0].Contains('-', StringComparison.Ordinal))
        {
            list.Add(Unresolved(EvidenceScopeKind.Rpcs3Build, mentions[0]));
            return;
        }

        list.Add(new EvidenceScopeCondition(EvidenceScopeKind.Rpcs3Build, mentions[0], relation));
    }

    private static void AddGameVersion(List<EvidenceScopeCondition> list, string text)
    {
        var versions = GameVersion.Matches(text).Select(m => m.Groups["v"].Value).Distinct(StringComparer.Ordinal).ToArray();
        if (versions.Length == 0)
        {
            return;
        }

        if (versions.Length > 1)
        {
            list.Add(Unresolved(EvidenceScopeKind.AppVersion, string.Join('|', versions)));
            return;
        }

        list.Add(new EvidenceScopeCondition(
            EvidenceScopeKind.AppVersion,
            versions[0],
            NoteScope.GameVersionRelation(text, versions[0])));
    }
}

#pragma warning restore IDE0130
