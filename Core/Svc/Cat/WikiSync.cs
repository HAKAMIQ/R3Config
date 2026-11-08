using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

// One Wiki page revision fetched from the MediaWiki API.
public sealed record WikiLivePage(
    long? PageId,
    string Title,
    int Namespace,
    string RevisionId,
    DateTimeOffset RevisionTimestamp,
    string Text);

public sealed record WikiLivePageResult(
    WikiLivePage Page,
    bool IsGameContent,
    IReadOnlyList<WikiRec> Recommendations);

// Sync helpers that reuse the canonical Wiki parser and setting map, so a
// live page revision is interpreted exactly like the shipped snapshot.
public sealed partial class WikiSvc
{
    public const int MaxLivePageChars = 512 * 1024;
    public const int MaxLivePages = 10_000;
    private const int MaxSyncValueChars = 128;

    private static readonly Regex SyncLibrary = new(
        @"^[A-Za-z0-9_.\-]+\.sprx:lle$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Lazy<Dictionary<string, string>> SyncTargets = new(BuildSyncTargets);

    public static IReadOnlyList<WikiLivePageResult> ParseLivePages(
        IReadOnlyList<WikiLivePage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count > MaxLivePages)
        {
            throw new InvalidDataException(
                $"Live Wiki input exceeds the {MaxLivePages}-page limit.");
        }

        var findings = new List<WikiCorpusFinding>();
        var output = new List<WikiLivePageResult>(pages.Count);
        var order = 0;

        foreach (var page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);

            if (string.IsNullOrWhiteSpace(page.Title) ||
                page.Text.Length > MaxLivePageChars)
            {
                throw new InvalidDataException(
                    $"Live Wiki page '{page.Title}' is empty-titled or exceeds the {MaxLivePageChars}-character limit.");
            }

            var raw = new RawWikiPage(
                page.PageId,
                page.Title.Trim(),
                page.Namespace,
                ExtractRedirectTarget(page.Text),
                page.RevisionId,
                page.RevisionTimestamp,
                page.Text,
                order++);
            var parsed = ParsePage(raw, findings);
            var recommendations = parsed.IsGameContent
                ? BuildRecommendations([parsed], findings)
                : [];

            output.Add(new WikiLivePageResult(
                page,
                parsed.IsGameContent,
                recommendations));
        }

        return output;
    }

    // Maps an RPCS3 YAML setting to the Wiki key the shipped catalog uses for
    // it. Settings outside the map are not supported by R3Config.
    public static bool TryGetSyncTarget(
        string section,
        string key,
        out string wikiKey)
    {
        return SyncTargets.Value.TryGetValue(
            $"{section}\0{key}",
            out wikiKey!);
    }

    // A value is accepted only from a verified domain: the Wiki parser's value
    // sets, booleans, RPCS3 built-in default types, or the LLE library form.
    public static bool IsSyncValue(
        string section,
        string key,
        string value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > MaxSyncValueChars ||
            value.Any(char.IsControl) ||
            !value.Equals(value.Trim(), StringComparison.Ordinal) ||
            !TryGetSyncTarget(section, key, out var wikiKey))
        {
            return false;
        }

        if (section == "Core" && key == "Libraries Control")
        {
            var parts = value.Split(", ", StringSplitOptions.None);
            return parts.All(part => SyncLibrary.IsMatch(part)) &&
                   parts.SequenceEqual(
                       parts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
                       StringComparer.Ordinal);
        }

        if (Keys.Contains(wikiKey) ||
            wikiKey.Equals("zcull accuracy", StringComparison.OrdinalIgnoreCase))
        {
            return value is "true" or "false";
        }

        if (CorpusValues.TryGetValue(wikiKey, out var allowed))
        {
            // Wiki wording "Disabled" is "Null" in RPCS3's YAML (the same mapping
            // Wiki.Convert applies through NullHandlerKeys). A synced value is a
            // YAML value, so only "Null" is valid and "Disabled" is not.
            if (NullHandlerKeys.Contains(wikiKey))
            {
                return value.Equals("Null", StringComparison.Ordinal) ||
                       (!value.Equals("Disabled", StringComparison.Ordinal) &&
                        allowed.Contains(value, StringComparer.Ordinal));
            }

            return allowed.Contains(value, StringComparer.Ordinal);
        }

        string? builtIn;
        try
        {
            builtIn = PlanService.ReadBuiltIn(section, key);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        if (builtIn is "true" or "false")
        {
            return value is "true" or "false";
        }

        // Numeric settings: the limits are the same table the Wiki converters use.
        // A numeric built-in with no table entry has no verified limit, so it fails closed.
        if (!NumericBounds.TryGetValue(wikiKey, out var bound))
        {
            return false;
        }

        return bound.Fractional
            ? decimal.TryParse(
                  value,
                  NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                  CultureInfo.InvariantCulture,
                  out var fraction) &&
              fraction >= bound.Min && fraction <= bound.Max
            : int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) &&
              number >= bound.Min && number <= bound.Max;
    }

    private static Dictionary<string, string> BuildSyncTargets()
    {
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (wikiKey, target) in CfgMap)
        {
            targets.TryAdd($"{target.Section}\0{target.Key}", wikiKey);
        }

        // The Wiki's single "zcull accuracy" key writes these two settings.
        targets.TryAdd("Video\0Relaxed ZCULL Sync", "zcull accuracy");
        targets.TryAdd("Video\0Accurate ZCULL stats", "zcull accuracy");

        return targets;
    }
}
