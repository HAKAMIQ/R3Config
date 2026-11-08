using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class WikiSvc
{
    private const long MaxWikiHistoryXmlBytes = 1024L * 1024 * 1024;
    private const int MaxHistoricalPageBlocks = 100_000;
    private const int MaxWikiRevisions = 250_000;
    private const int MaxWikiPages = 10_000;
    private const int MaxTemplatesPerPage = 64;
    private const int MaxParametersPerTemplate = 512;

    private static readonly Regex SnapshotSerial = new(
        @"(?<![A-Z0-9])[BNM][A-Z]{3}\d{5}(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SnapshotDateToken = new(
        @"(?<!\d)(?<date>\d{8})(?:\d{6})?(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTag = new(
        @"</?[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VariantLabel = new(
        @"'{2,5}\s*(?<label>[^'\r\n<>]{1,128}?)\s*'{2,5}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RedirectText = new(
        @"^\s*#redirect\s*\[\[(?<target>[^\]|#]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VersionCondition = new(
        @"\bno longer required\b|\b(?:older|newer) versions?\b|" +
        @"\b(?:RPCS3|emulator|game) (?:versions?|builds?)\b|\bversions?\s+(?:above|below|before|after|prior to|through)\s+\d|" +
        @"\bversions?\s+\d|\bbuild\s+\d|" +
        @"\buntil\s+RPCS3\s+(?:PR|pull request|build|version)\b|" +
        @"\b(?:fixed|regression|removed)\b[^.]{0,80}\b(?:RPCS3|emulator|version|build|PR)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchCondition = new(
        @"\bcanary\b|\bunpatched\b|\bunlock fps\b|\bpatch\s+[^.;]{1,64}\s+only\b|" +
        @"\b(?:the|a|an|fps|mlaa|recommended|disable [a-z0-9 -]+)\s+patch(?:es)?\b|" +
        @"\bpatch(?:es)?\s+(?:is|are|enabled|disabled|required|available|notes)\b|" +
        @"\busing\b[^.]{0,80}\bpatch(?:es)?\b|\b(?:30|60|120)\s*fps patch\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchDependencyCondition = new(
        @"\b(?:required|needed)\s+unless\s+using\b[^.]{0,80}\bpatch(?:es)?\b|" +
        @"\b(?:required|needed)\s+if\s+not\s+using\b[^.]{0,80}\bpatch(?:es)?\b|" +
        @"\bonly\s+required\s+if\s+not\s+using\b[^.]{0,80}\bpatch(?:es)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MandatoryCondition = new(
        @"\brequired\b|\brequires\b|\bmust (?:be|use|enable|disable|set)\b|\bneeded to\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ActiveConfigKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ppu decoder",
            "spu decoder",
            "preferred spu threads",
            "thread scheduler",
            "enable spu loop detection",
            "spu xfloat accuracy",
            "spu block size",
            "renderer",
            "graphics device",
            "aspect ratio",
            "framelimit",
            "anisotropic filter",
            "anti-aliasing",
            "zcull accuracy",
            "shader quality",
            "3d rendering mode",
            "default resolution",
            "resolution scale",
            "resolution scale threshold",
            "output scaling",
            "shader mode",
            "write color buffers",
            "strict rendering mode",
            "vsync",
            "stretch to display area",
            "multithreaded rsx",
            "asynchronous texture streaming",
            "audio out windows",
            "audio out linux",
            "audio format",
            "dump to file",
            "convert to 16-bit",
            "volume",
            "enable buffering",
            "audio buffer duration",
            "enable time stretching",
            "time stretching threshold",
            "microphone type",
            "keyboard handler",
            "mouse handler",
            "camera input",
            "camera settings",
            "move handler",
            "network status",
            "psn status",
            "debug console mode",
            "accurate rsx reservation access",
            "accurate spu dma",
            "disable spu getllar spin optimization",
            "enable spu events busy loop",
            "ppu llvm java mode handling",
            "silence all logs",
            "sleep timers accuracy",
            "maximum spurs threads",
            "firmware libraries",
            "read depth buffers",
            "write depth buffers",
            "read color buffers",
            "handle rsx memory tiling",
            "disable on-disk shader cache",
            "disable vertex cache",
            "allow host gpu labels",
            "force hardware msaa resolve",
            "rsx fifo accuracy",
            "driver wake-up delay",
            "vblank rate",
            "vblank ntsc fixup",
            "clocks scale",
            "delay each odd mfc command",
            "emulate special depth comparison",
            "disable zcull occlusion queries",
            "force cpu blit emulation",
            "force gpu texture scaling",
            "hook static functions",
            "accurate ppu 128 reservations",
            "ppu thread count",
            "lod bias offset",
            "disable asynchronous memory manager",
            "ppu llvm accurate vector nans",
            "accurate dfma"
        };

    // Wiki keys whose setting does not exist in current RPCS3 (verified in
    // docs/WikiParityAudit-2026-09-07.md), so there is nothing to apply.
    private static readonly HashSet<string> NoRpcs3Setting =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "delay each odd mfc command",
            "enable spu events busy loop"
        };

    private static readonly HashSet<string> LegacyConfigKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bind spu threads to secondary cores",
            "invalidate cache every frame",
            "disable async shader compiler",
            "downmix to stereo",
            "accurate putlluc",
            "enable thread scheduler",
            "firmware settings",
            "relaxed zcull sync",
            "accurate xfloat",
            "lower spu thread priority",
            "spu cache",
            "audio provider",
            "disable native float16 support",
            "enable 3d",
            "audio channels",
            "use gpu texture scaling",
            "accurate getllar",
            "tsx instructions"
        };

    private static readonly HashSet<string> PatchConfigKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "highly recommended patch",
            "recommended patch"
        };

    private static readonly Dictionary<string, HashSet<string>> CorpusValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu decoder"] = Values("Interpreter (static)", "Recompiler (LLVM)"),
            ["spu decoder"] = Values("Interpreter (static)", "Interpreter (dynamic)", "Recompiler (ASMJIT)", "Recompiler (LLVM)"),
            ["thread scheduler"] = Values("Operating System", "RPCS3 Scheduler", "RPCS3 Alternative Scheduler"),
            ["spu xfloat accuracy"] = Values("Accurate", "Approximate", "Relaxed", "Inaccurate"),
            ["xfloat accuracy"] = Values("Accurate", "Approximate", "Relaxed", "Inaccurate"),
            ["spu block size"] = Values("Safe", "Mega", "Giga"),
            ["rsx fifo accuracy"] = Values("Fast", "Atomic", "Ordered & Atomic", "PS3"),
            ["sleep timers accuracy"] = Values("As Host", "Usleep Only", "All Timers"),
            ["renderer"] = Values("OpenGL", "Vulkan", "Disabled"),
            ["aspect ratio"] = Values("4:3", "16:9"),
            ["framelimit"] = Values("Off", "50", "60", "30", "120", "Display", "Auto", "PS3 Native", "Infinite"),
            ["anti-aliasing"] = Values("Auto", "Disabled"),
            ["shader quality"] = Values("Ultra", "High", "Low", "Auto"),
            ["3d rendering mode"] = Values(
                "Disabled", "Anaglyph Red-Green", "Anaglyph Red-Blue", "Anaglyph Red-Cyan",
                "Anaglyph Magenta-Cyan", "Anaglyph Trioscopic", "Anaglyph Amber-Blue", "Anaglyph Custom",
                "Side-by-Side", "Over-Under", "Interlaced"),
            ["default resolution"] = Values(
                "1920x1080", "1920x1080i", "1280x720", "720x480", "720x480i", "720x576", "720x576i",
                "1600x1080", "1440x1080", "1280x1080", "960x1080"),
            ["shader mode"] = Values(
                "Legacy Recompiler (single-threaded)", "Async Recompiler (multi-threaded)",
                "Async Recompiler with Shader Interpreter", "Shader Interpreter only"),
            ["vsync"] = Values("Disabled", "Adaptive", "Full"),
            ["output scaling"] = Values("Nearest", "Bilinear", "FidelityFX Super Resolution"),
            ["audio out windows"] = Values("Cubeb", "XAudio2", "FAudio", "Disabled"),
            ["audio format"] = Values("Stereo", "Surround 5.1", "Surround 7.1", "Automatic", "Manual"),
            ["microphone type"] = Values("Disabled", "Standard", "SingStar", "Real SingStar", "Rocksmith", "Eye Toy", "PS Eye"),
            ["keyboard handler"] = Values("Null", "Basic"),
            ["mouse handler"] = Values("Null", "Basic", "Raw"),
            ["camera input"] = Values("Unknown", "EyeToy", "PS Eye", "UVC 1.1"),
            ["camera settings"] = Values("Null", "Fake", "Qt", "SDL"),
            ["move handler"] = Values("Null", "Real", "Fake", "Mouse", "Raw Mouse", "Gun"),
            ["network status"] = Values("Disconnected", "Connected"),
            ["psn status"] = Values("Disconnected", "Simulated", "RPCN")
        };

    // The one numeric-limit table, taken from system_config.h of the RPCS3 source
    // (commit 54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6, cfg::_int/uint/_float
    // template bounds; unchanged at b94078bed10ccdb86c5e4b9f73bff8f479089fa4,
    // the SyncBoundsT snapshot). The Wiki converters and the RecSync value gate
    // (IsSyncValue) must both read their limits from here.
    internal readonly record struct NumericBound(decimal Min, decimal Max, bool Fractional = false);

    internal static readonly IReadOnlyDictionary<string, NumericBound> NumericBounds =
        new Dictionary<string, NumericBound>(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu thread count"] = new(1, 8),
            ["preferred spu threads"] = new(0, 6),
            ["accurate ppu 128 reservations"] = new(-1, 14),
            ["maximum spurs threads"] = new(1, 6),
            ["clocks scale"] = new(10, 3000),
            ["resolution scale"] = new(25, 800),
            ["resolution scale threshold"] = new(1, 1024),
            ["anisotropic filter"] = new(0, 16),
            ["lod bias offset"] = new(-32, 32, Fractional: true),
            ["volume"] = new(0, 200),
            ["audio buffer duration"] = new(4, 250),
            ["time stretching threshold"] = new(0, 100),
            ["driver wake-up delay"] = new(0, 16667),
            ["vblank rate"] = new(1, 6000)
        };

    // Integer keys range-checked by ConvertCorpusValue itself; the remaining
    // NumericBounds keys are parsed from their Wiki wording by Convert.
    private static readonly string[] CorpusIntegerKeys =
    [
        "resolution scale", "volume", "audio buffer duration", "time stretching threshold",
        "driver wake-up delay", "vblank rate", "clocks scale", "maximum spurs threads", "ppu thread count"
    ];

    public static IReadOnlyList<string> CanonicalConfigSettingNames =>
    [
        .. CfgMap.Values
            .Select(target => target.Key)
            .Concat(["Relaxed ZCULL Sync", "Accurate ZCULL stats"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
    ];

    public static IReadOnlyList<string> CanonicalConfigValues =>
    [
        .. CorpusValues.Values
            .SelectMany(values => values)
            .Concat(["On", "true", "false", "LLVM"])
            // Library selections the shipped catalog carries for Libraries Control.
            .Concat(["libaudio.sprx:lle", "libvdec.sprx:lle"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
    ];

    public static WikiCorpusAnalysis Analyze(
        string wikiXmlPath,
        string? snapshotDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wikiXmlPath);

        if (!File.Exists(wikiXmlPath))
        {
            throw new FileNotFoundException(
                "RPCS3 wiki XML was not found.",
                wikiXmlPath);
        }

        var file = new FileInfo(wikiXmlPath);
        if (file.Length < 0 || file.Length > MaxWikiHistoryXmlBytes)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML exceeds the {MaxWikiHistoryXmlBytes}-byte history input limit.");
        }

        var stopwatch = Stopwatch.StartNew();
        var sourceHash = HashFile(wikiXmlPath);
        var history = ReadLatestWikiPages(wikiXmlPath);

        if (history.Pages.Count > MaxWikiPages)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML exceeds the {MaxWikiPages}-page input limit.");
        }

        var findings = new List<WikiCorpusFinding>();
        var pages = new List<WikiCorpusPage>(history.Pages.Count);

        foreach (var page in history.Pages)
        {
            pages.Add(ParsePage(page, findings));
        }

        var canonicalTitles = pages
            .Where(page => page.IsGameContent)
            .Select(page => page.Title)
            .ToHashSet(StringComparer.Ordinal);
        var redirectAliases = pages
            .Where(page => page.Namespace == 0 &&
                           page.IsRedirect &&
                           !string.IsNullOrWhiteSpace(page.RedirectTarget))
            .Select(page => new WikiRedirectAlias(
                page.Title,
                page.RedirectTarget!,
                page.PageId,
                canonicalTitles.Contains(page.RedirectTarget!)))
            .OrderBy(alias => alias.Alias, StringComparer.Ordinal)
            .ToArray();

        var allEntries = pages
            .SelectMany(page => page.Recommendations)
            .ToArray();
        var allNotes = pages
            .SelectMany(page => page.Notes)
            .ToArray();
        var catalogRecommendations = BuildRecommendations(
            pages.ToArray(),
            findings);
        var duplicateSerialGroups = pages
            .Where(page => page.IsGameContent)
            .SelectMany(page => page.GameIdBindings.Select(binding => (binding.Serial, Page: page)))
            .GroupBy(item => item.Serial, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Page.Title).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();
        var conflictingSerials = findings
            .Where(finding => finding.Code == WikiFindingCode.DuplicateSerialConflict)
            .Select(finding => finding.Serial)
            .Where(serial => !string.IsNullOrWhiteSpace(serial))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var gamePages = pages.Where(page => page.IsGameContent).ToArray();
        var serialOccurrences = gamePages.Sum(page => page.Serials.Count);
        var gameAliases = redirectAliases.Count(alias => alias.TargetsGame);
        var resolvedSnapshotDate = ResolveSnapshotDate(
            wikiXmlPath,
            snapshotDate,
            pages);

        stopwatch.Stop();

        var stats = new WikiCorpusStats(
            HistoricalPageBlocks: history.HistoricalPageBlocks,
            TotalRevisions: history.TotalRevisions,
            TotalPages: pages.Count,
            ParsedPages: pages.Count(page => page.Parsed),
            ParseFailures: pages.Count(page => !page.Parsed),
            MainNamespacePages: pages.Count(page => page.Namespace == 0),
            GameContentPages: gamePages.Length,
            RedirectPages: pages.Count(page => page.Namespace == 0 && page.IsRedirect),
            GameRedirectAliases: gameAliases,
            NonGameRedirects: redirectAliases.Length - gameAliases,
            UtilityPages: pages.Count(page => page.Namespace == 0 &&
                                              !page.IsRedirect &&
                                              !page.IsGameContent),
            NonGamePages: pages.Count(page => page.Namespace != 0),
            PagesWithGameIdTemplate: pages.Count(page => page.GameIdTemplateCount > 0),
            PagesWithSerials: gamePages.Count(page => page.Serials.Count > 0),
            PagesWithConfigTemplate: pages.Count(page => page.ConfigTemplateCount > 0),
            PagesWithRecommendations: gamePages.Count(page => page.Recommendations.Count > 0),
            PagesWithoutRecommendations: gamePages.Count(page => page.Recommendations.Count == 0),
            UniqueGameTitles: gamePages.Select(page => page.Title).Distinct(StringComparer.Ordinal).Count(),
            SerialOccurrences: serialOccurrences,
            UniqueSerials: gamePages.SelectMany(page => page.Serials).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            RegionVariants: serialOccurrences - gamePages.Count(page => page.Serials.Count > 0),
            TotalConfigEntries: allEntries.Length,
            TotalNotes: allNotes.Length,
            MappedRecommendations: allEntries.Count(entry => entry.ParserStatus == WikiParserStatus.Mapped),
            UnsupportedRecommendations: allEntries.Count(entry =>
                entry.ParserStatus == WikiParserStatus.Unsupported &&
                entry.Classification == WikiRecommendationClass.Unsupported),
            AmbiguousRecommendations: allEntries.Count(entry => entry.ParserStatus == WikiParserStatus.Ambiguous),
            RejectedRecommendations: allEntries.Count(entry => entry.ParserStatus == WikiParserStatus.Rejected),
            VersionSpecificRecommendations: allEntries.Count(entry => entry.Classification == WikiRecommendationClass.VersionSpecific),
            PatchSpecificRecommendations: allEntries.Count(entry => entry.Classification == WikiRecommendationClass.PatchSpecific),
            UnknownRecommendations: allEntries.Count(entry => entry.ParserStatus == WikiParserStatus.UnknownWikiKey),
            DuplicateSerials: duplicateSerialGroups.Length,
            ConflictingSerials: conflictingSerials,
            ConflictingSettings: findings.Count(finding => finding.Code == WikiFindingCode.DuplicateSettingConflict));

        return new WikiCorpusAnalysis(
            Path.GetFileName(wikiXmlPath),
            sourceHash,
            resolvedSnapshotDate,
            stopwatch.Elapsed,
            stats,
            pages,
            allEntries,
            allNotes,
            findings,
            redirectAliases,
            catalogRecommendations);
    }

    public static WikiCatalogComparison Compare(
        RecSvc oldCatalog,
        WikiCorpusAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(oldCatalog);
        ArgumentNullException.ThrowIfNull(analysis);

        var oldBySerial = oldCatalog.Rec.ToDictionary(
            item => item.Serial,
            StringComparer.OrdinalIgnoreCase);
        var generatedBySerial = analysis.CatalogRecommendations.ToDictionary(
            item => item.Serial,
            StringComparer.OrdinalIgnoreCase);
        var differences = new List<WikiCatalogDiff>();
        var added = 0;
        var removed = 0;
        var changed = 0;
        var unchanged = 0;
        var exactChanges = 0;
        var missingChanges = 0;
        var extraChanges = 0;
        var differentChanges = 0;
        var generatedTitles = analysis.CatalogRecommendations
            .GroupBy(item => item.WikiName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Serial).ToArray(), StringComparer.Ordinal);
        var oldTitles = oldCatalog.Rec
            .GroupBy(item => item.WikiName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Serial).ToArray(), StringComparer.Ordinal);

        foreach (var serial in oldBySerial.Keys
                     .Union(generatedBySerial.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var hasOld = oldBySerial.TryGetValue(serial, out var oldRecommendation);
            var hasGenerated = generatedBySerial.TryGetValue(serial, out var generatedRecommendation);

            if (!hasOld)
            {
                added++;
                missingChanges += generatedRecommendation!.Changes.Count;
                var kind = oldTitles.ContainsKey(generatedRecommendation.WikiName)
                    ? WikiCatalogDiffKind.RegionMismatch
                    : WikiCatalogDiffKind.MissingInCatalog;
                differences.Add(new WikiCatalogDiff(
                    serial,
                    generatedRecommendation.WikiName,
                    kind,
                    "The generated snapshot catalog contains this serial, but the old catalog does not."));
                continue;
            }

            if (!hasGenerated)
            {
                removed++;
                extraChanges += oldRecommendation!.Changes.Count;
                var kind = generatedTitles.ContainsKey(oldRecommendation.WikiName)
                    ? WikiCatalogDiffKind.RegionMismatch
                    : WikiCatalogDiffKind.ExtraInCatalog;
                differences.Add(new WikiCatalogDiff(
                    serial,
                    oldRecommendation.WikiName,
                    kind,
                    "The old catalog contains this serial, but it is excluded from the generated snapshot catalog."));
                continue;
            }

            var serialDiffStart = differences.Count;
            var oldChanges = oldRecommendation!.Changes.ToDictionary(ChangePath, StringComparer.OrdinalIgnoreCase);
            var generatedChanges = generatedRecommendation!.Changes.ToDictionary(ChangePath, StringComparer.OrdinalIgnoreCase);

            foreach (var path in oldChanges.Keys
                         .Union(generatedChanges.Keys, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                var oldHasChange = oldChanges.TryGetValue(path, out var oldChange);
                var generatedHasChange = generatedChanges.TryGetValue(path, out var generatedChange);

                if (!oldHasChange)
                {
                    missingChanges++;
                    differences.Add(new WikiCatalogDiff(
                        serial,
                        generatedRecommendation.WikiName,
                        WikiCatalogDiffKind.KeyMismatch,
                        $"Missing old catalog setting '{generatedChange!.Path}' from Wiki key '{generatedChange.WikiKey}'."));
                    continue;
                }

                if (!generatedHasChange)
                {
                    extraChanges++;
                    var sourceEntry = analysis.Recommendations.FirstOrDefault(entry =>
                        entry.ApplicableSerials.Contains(serial, StringComparer.OrdinalIgnoreCase) &&
                        entry.WikiKey.Equals(oldChange!.WikiKey, StringComparison.OrdinalIgnoreCase));
                    var kind = sourceEntry?.Classification switch
                    {
                        WikiRecommendationClass.PatchSpecific => WikiCatalogDiffKind.PatchSpecificMismatch,
                        WikiRecommendationClass.VersionSpecific => WikiCatalogDiffKind.VersionAmbiguous,
                        _ => WikiCatalogDiffKind.KeyMismatch
                    };
                    differences.Add(new WikiCatalogDiff(
                        serial,
                        oldRecommendation.WikiName,
                        kind,
                        $"Old catalog setting '{oldChange!.Path}' is not present in safe generated output."));
                    continue;
                }

                if (!string.Equals(oldChange!.WikiKey, generatedChange!.WikiKey, StringComparison.OrdinalIgnoreCase))
                {
                    differentChanges++;
                    differences.Add(new WikiCatalogDiff(
                        serial,
                        generatedRecommendation.WikiName,
                        WikiCatalogDiffKind.KeyMismatch,
                        $"Setting '{path.Replace('\0', '/')}' changed Wiki key from '{oldChange.WikiKey}' to '{generatedChange.WikiKey}'."));
                }
                else if (!string.Equals(oldChange.Value, generatedChange.Value, StringComparison.Ordinal))
                {
                    differentChanges++;
                    differences.Add(new WikiCatalogDiff(
                        serial,
                        generatedRecommendation.WikiName,
                        WikiCatalogDiffKind.ValueMismatch,
                        $"Setting '{path.Replace('\0', '/')}' changed from '{oldChange.Value}' to '{generatedChange.Value}'."));
                }
                else if (!string.Equals(oldChange.Notes, generatedChange.Notes, StringComparison.Ordinal))
                {
                    differentChanges++;
                    differences.Add(new WikiCatalogDiff(
                        serial,
                        generatedRecommendation.WikiName,
                        WikiCatalogDiffKind.NotesMismatch,
                        $"Setting '{path.Replace('\0', '/')}' has different notes."));
                }
                else
                {
                    exactChanges++;
                }
            }

            if (!string.Equals(oldRecommendation.WikiName, generatedRecommendation.WikiName, StringComparison.Ordinal))
            {
                differences.Add(new WikiCatalogDiff(
                    serial,
                    generatedRecommendation.WikiName,
                    WikiCatalogDiffKind.SerialMismatch,
                    $"Wiki title changed from '{oldRecommendation.WikiName}' to '{generatedRecommendation.WikiName}'."));
            }

            if (differences.Count == serialDiffStart)
            {
                unchanged++;
            }
            else
            {
                changed++;
            }
        }

        return new WikiCatalogComparison(
            oldCatalog.Count,
            analysis.CatalogRecommendations.Count,
            added,
            removed,
            changed,
            unchanged,
            exactChanges,
            missingChanges,
            extraChanges,
            differentChanges,
            differences);
    }

    private static WikiCorpusPage ParsePage(
        RawWikiPage page,
        List<WikiCorpusFinding> findings)
    {
        var title = string.IsNullOrWhiteSpace(page.Title) ? "<untitled>" : page.Title;
        var gameIdBlocks = FindTemplateBlocks(page.Text, "gameid", title, findings);
        var configBlocks = FindTemplateBlocks(page.Text, "config", title, findings);
        var isGameContent = page.Namespace == 0 &&
                            page.RedirectTarget is null &&
                            gameIdBlocks.Count > 0 &&
                            configBlocks.Count > 0;
        var gameIds = page.Namespace == 0 && page.RedirectTarget is null && gameIdBlocks.Count > 0
            ? ParseGameIds(page.PageId, title, page.Text, gameIdBlocks, findings)
            : GameIdParseResult.Empty;
        var (entries, notes) = isGameContent
            ? ParseConfig(title, gameIds.Serials, gameIds.Bindings, configBlocks, findings)
            : (Array.Empty<WikiCorpusEntry>(), Array.Empty<WikiCorpusNote>());
        var patchReferences = ExtractPatchReferences(page.Text, entries);

        return new WikiCorpusPage(
            page.PageId,
            title,
            page.Namespace,
            page.RedirectTarget is not null,
            page.RedirectTarget,
            isGameContent,
            title != "<untitled>",
            page.RevisionId,
            page.RevisionTimestamp,
            gameIds.Serials,
            gameIds.Bindings,
            gameIds.RawReferences,
            gameIdBlocks.Count,
            configBlocks.Count,
            patchReferences,
            entries,
            notes);
    }

    private static GameIdParseResult ParseGameIds(
        long? pageId,
        string page,
        string text,
        IReadOnlyList<TemplateBlock> blocks,
        List<WikiCorpusFinding> findings)
    {
        var bindings = new Dictionary<string, WikiGameIdBinding>(StringComparer.OrdinalIgnoreCase);
        var sawCandidate = false;
        var rawReferences = 0;
        string? previousLabel = null;

        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            var blockSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var label = blockIndex == 0
                ? null
                : FindVariantLabel(text, blocks[blockIndex - 1].End, block.Start);
            // Adjacent GameID templates separated only by a comma/line break
            // are still under the same explicit release heading (e.g. EyePet).
            if (label is null && blockIndex > 0 && Regex.IsMatch(
                    text[blocks[blockIndex - 1].End..block.Start], @"^[\s,]*(?:<br\s*/?>[\s,]*)*$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                label = previousLabel;
            }
            previousLabel = label;
            var role = ClassifyGameIdRole(blockIndex, label);
            foreach (var parameter in SplitTemplateParameters(block, page, findings))
            {
                if (parameter.Key is not null &&
                    !parameter.Key.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                    !parameter.Key.Equals("oid", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var rawItem in parameter.Value.Split(','))
                {
                    var raw = rawItem.Trim();
                    if (raw.Length == 0)
                    {
                        continue;
                    }

                    sawCandidate = true;
                    var cleaned = HtmlTag.Replace(raw, " ");
                    cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim().ToUpperInvariant();
                    if (SnapshotSerial.IsMatch(cleaned) &&
                        SnapshotSerial.Match(cleaned).Value.Length == cleaned.Length)
                    {
                        if (!blockSerials.Add(cleaned))
                        {
                            continue;
                        }

                        rawReferences++;
                        var candidate = new WikiGameIdBinding(
                            cleaned,
                            pageId,
                            page,
                            role,
                            label,
                            blockIndex);
                        if (!bindings.TryGetValue(cleaned, out var existing) ||
                            RolePriority(candidate.Role) < RolePriority(existing.Role))
                        {
                            bindings[cleaned] = candidate;
                        }

                        if (!string.Equals(raw, cleaned, StringComparison.OrdinalIgnoreCase))
                        {
                            findings.Add(new WikiCorpusFinding(
                                WikiFindingCode.MalformedGameId,
                                page,
                                cleaned,
                                null,
                                $"GameID token '{raw}' required markup/whitespace normalization."));
                        }

                        continue;
                    }

                    findings.Add(new WikiCorpusFinding(
                        WikiFindingCode.MalformedGameId,
                        page,
                        null,
                        null,
                        $"Invalid GameID token '{raw}'."));
                }
            }
        }

        if (blocks.Count > 0 && (!sawCandidate || bindings.Count == 0))
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.EmptyGameId,
                page,
                null,
                null,
                "The game page has no valid GameID value."));
        }

        var orderedBindings = bindings.Values
            .OrderBy(binding => binding.Serial, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new GameIdParseResult(
            orderedBindings.Select(binding => binding.Serial).ToArray(),
            orderedBindings,
            rawReferences);
    }

    private static string? FindVariantLabel(
        string text,
        int from,
        int to)
    {
        if (from < 0 || to <= from || to > text.Length)
        {
            return null;
        }

        var matches = VariantLabel.Matches(text[from..to]);
        if (matches.Count == 0)
        {
            return null;
        }

        var label = CleanVal(matches[^1].Groups["label"].Value);
        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private static WikiGameIdRole ClassifyGameIdRole(
        int templateIndex,
        string? label)
    {
        if (templateIndex == 0 || string.IsNullOrWhiteSpace(label))
        {
            return WikiGameIdRole.Primary;
        }

        var normalized = Regex.Replace(label, @"\s+", " ").Trim();
        if (normalized.Contains("trade demo", StringComparison.OrdinalIgnoreCase))
        {
            return WikiGameIdRole.TradeDemo;
        }

        if (normalized.Contains("playview", StringComparison.OrdinalIgnoreCase))
        {
            return WikiGameIdRole.PlayView;
        }

        if (Regex.IsMatch(normalized, @"\bdemo\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return WikiGameIdRole.Demo;
        }

        if (normalized.Contains("xtreme legends", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("empires", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("edition", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("version", StringComparison.OrdinalIgnoreCase))
        {
            return WikiGameIdRole.RelatedEdition;
        }

        return WikiGameIdRole.Unknown;
    }

    private static int RolePriority(WikiGameIdRole role)
    {
        return role switch
        {
            WikiGameIdRole.Primary => 0,
            WikiGameIdRole.RelatedEdition => 1,
            WikiGameIdRole.PlayView => 2,
            WikiGameIdRole.TradeDemo => 3,
            WikiGameIdRole.Demo => 4,
            _ => 5
        };
    }

    private static (IReadOnlyList<WikiCorpusEntry> Entries, IReadOnlyList<WikiCorpusNote> Notes) ParseConfig(
        string page,
        IReadOnlyList<string> serials,
        IReadOnlyList<WikiGameIdBinding> bindings,
        IReadOnlyList<TemplateBlock> blocks,
        List<WikiCorpusFinding> findings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var notes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var conflicted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in blocks)
        {
            foreach (var parameter in SplitTemplateParameters(block, page, findings))
            {
                if (parameter.Key is null)
                {
                    continue;
                }

                var key = NormWiki(parameter.Key);
                var value = parameter.Value.Trim();
                if (key.Length == 0 ||
                    value.Length == 0 ||
                    CleanVal(value).Length == 0)
                {
                    continue;
                }

                if (key.EndsWith(" notes", StringComparison.OrdinalIgnoreCase))
                {
                    AddCorpusNote(
                        notes,
                        key[..^6].Trim(),
                        value);
                }
                else
                {
                    AddCorpusValue(
                        values,
                        conflicted,
                        key,
                        value,
                        page,
                        findings);
                }
            }
        }

        var extractedNotes = notes
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(item => item.Value.Select(value => new WikiCorpusNote(
                page,
                item.Key,
                value,
                CleanVal(value),
                WikiRecommendationClass.InformationalNote,
                values.ContainsKey(item.Key))))
            .ToArray();

        foreach (var note in extractedNotes.Where(item => !item.AssociatedWithSetting))
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.OrphanNote,
                page,
                null,
                note.WikiKey,
                "A config note has no non-empty setting value."));
        }

        var entries = new List<WikiCorpusEntry>(values.Count);
        foreach (var (wikiKey, rawValue) in values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var rawNote = notes.TryGetValue(wikiKey, out var noteValues)
                ? string.Join(
                    " | ",
                    noteValues
                        .Select(CleanVal)
                        .Distinct(StringComparer.Ordinal))
                : null;
            entries.Add(ClassifyEntry(
                page,
                serials,
                bindings,
                wikiKey,
                rawValue,
                rawNote,
                conflicted.Contains(wikiKey),
                findings));
        }

        return (entries, extractedNotes);
    }

    private static WikiCorpusEntry ClassifyEntry(
        string page,
        IReadOnlyList<string> pageSerials,
        IReadOnlyList<WikiGameIdBinding> bindings,
        string wikiKey,
        string rawValue,
        string? rawNote,
        bool conflicted,
        List<WikiCorpusFinding> findings)
    {
        var cleanedValue = CleanVal(rawValue);
        var note = string.IsNullOrWhiteSpace(rawNote)
            ? null
            : CleanVal(rawNote);
        var applicableSerials = ResolveApplicableSerials(
            page,
            pageSerials,
            bindings,
            wikiKey,
            note,
            findings,
            out var regionAmbiguous);

        if (conflicted)
        {
            return Entry(
                WikiRecommendationClass.Ambiguous,
                WikiParserStatus.Rejected,
                false,
                [],
                "The page contains conflicting values for this config key.");
        }

        if (PatchConfigKeys.Contains(wikiKey))
        {
            return Entry(
                WikiRecommendationClass.PatchSpecific,
                WikiParserStatus.Unsupported,
                false,
                [],
                "Patch recommendations are retained as evidence and are not serialized as game config.");
        }

        if (!ActiveConfigKeys.Contains(wikiKey) &&
            !LegacyConfigKeys.Contains(wikiKey) &&
            !CfgMap.ContainsKey(wikiKey) &&
            !wikiKey.Equals("zcull accuracy", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.UnknownWikiKey,
                page,
                null,
                wikiKey,
                $"Unknown Wiki config key with value '{cleanedValue}'."));
            return Entry(
                WikiRecommendationClass.Unsupported,
                WikiParserStatus.UnknownWikiKey,
                false,
                [],
                "UNKNOWN_WIKI_KEY");
        }

        IReadOnlyList<WikiMappedSetting> targets;
        if (wikiKey.Equals("zcull accuracy", StringComparison.OrdinalIgnoreCase))
        {
            var zcull = ConvertZCull(cleanedValue);
            if (zcull is null)
            {
                findings.Add(new WikiCorpusFinding(
                    WikiFindingCode.InvalidWikiValue,
                    page,
                    null,
                    wikiKey,
                    $"Unsupported ZCULL value '{cleanedValue}'."));
                return Entry(
                    WikiRecommendationClass.Ambiguous,
                    WikiParserStatus.Rejected,
                    false,
                    [],
                    "The ZCULL value cannot be serialized safely.");
            }

            targets =
            [
                new WikiMappedSetting("Video", "Relaxed ZCULL Sync", zcull.Value.Relaxed),
                new WikiMappedSetting("Video", "Accurate ZCULL stats", zcull.Value.Precise)
            ];
        }
        else if (CfgMap.TryGetValue(wikiKey, out var target))
        {
            var converted = ConvertCorpusValue(wikiKey, cleanedValue);
            if (converted is null)
            {
                var ambiguous = wikiKey.Equals("preferred spu threads", StringComparison.OrdinalIgnoreCase) &&
                                Range.IsMatch(cleanedValue);
                findings.Add(new WikiCorpusFinding(
                    ambiguous ? WikiFindingCode.AmbiguousWikiValue : WikiFindingCode.InvalidWikiValue,
                    page,
                    null,
                    wikiKey,
                    $"Wiki value '{cleanedValue}' cannot be serialized to '{target.Section} / {target.Key}'."));
                return Entry(
                    WikiRecommendationClass.Ambiguous,
                    ambiguous ? WikiParserStatus.Ambiguous : WikiParserStatus.Rejected,
                    false,
                    [],
                    "The Wiki value does not identify one deterministic RPCS3 value.");
            }

            targets = [new WikiMappedSetting(target.Section, target.Key, converted)];
        }
        else
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.UnsupportedWikiKey,
                page,
                null,
                wikiKey,
                LegacyConfigKeys.Contains(wikiKey)
                    ? "Legacy Wiki key is not supported by the verified R3Config schema."
                    : "Active Wiki key is not supported by the verified R3Config schema."));
            return Entry(
                WikiRecommendationClass.Unsupported,
                WikiParserStatus.Unsupported,
                false,
                [],
                LegacyConfigKeys.Contains(wikiKey)
                    ? "Legacy Wiki key."
                    : NoRpcs3Setting.Contains(wikiKey)
                        ? "No matching setting exists in current RPCS3; the evidence is preserved and not applied."
                        : "No verified R3Config mapping.");
        }

        var classification = ClassifyRecommendation(wikiKey, note);
        var included = !regionAmbiguous && applicableSerials.Length > 0 &&
                       classification is WikiRecommendationClass.MandatoryGameConfig or
                           WikiRecommendationClass.GeneralRecommendation;
        return Entry(
            regionAmbiguous ? WikiRecommendationClass.Ambiguous : classification,
            regionAmbiguous ? WikiParserStatus.Ambiguous : WikiParserStatus.Mapped,
            included,
            targets,
            regionAmbiguous
                ? "The recommendation scope could not be resolved to explicit page GameIDs."
                : null);

        WikiCorpusEntry Entry(
            WikiRecommendationClass classification,
            WikiParserStatus status,
            bool includedInCatalog,
            IReadOnlyList<WikiMappedSetting> mapped,
            string? detail)
        {
            return new WikiCorpusEntry(
                page,
                pageSerials,
                applicableSerials,
                wikiKey,
                rawValue.Trim(),
                note,
                mapped,
                classification,
                status,
                includedInCatalog,
                detail);
        }
    }

    private static WikiRecommendationClass ClassifyRecommendation(
        string wikiKey,
        string? note)
    {
        if (PatchConfigKeys.Contains(wikiKey))
        {
            return WikiRecommendationClass.PatchSpecific;
        }

        if (!string.IsNullOrWhiteSpace(note) &&
            PatchDependencyCondition.IsMatch(note))
        {
            return WikiRecommendationClass.PatchSpecific;
        }

        if (!string.IsNullOrWhiteSpace(note) &&
            PatchCondition.IsMatch(note))
        {
            return WikiRecommendationClass.PatchSpecific;
        }

        if (!string.IsNullOrWhiteSpace(note) && VersionCondition.IsMatch(note))
        {
            return WikiRecommendationClass.VersionSpecific;
        }

        if (!string.IsNullOrWhiteSpace(note) && MandatoryCondition.IsMatch(note))
        {
            return WikiRecommendationClass.MandatoryGameConfig;
        }

        return WikiRecommendationClass.GeneralRecommendation;
    }

    private static readonly Regex LibraryName = new(
        @"^(?<name>[A-Za-z0-9_.\-]+\.sprx)(?::lle)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // The Wiki lists the LLE libraries to select, comma separated. The
    // canonical value is the sorted, de-duplicated "<name>.sprx:lle" list that
    // RPCS3 writes to Libraries Control; anything else is not deterministic.
    private static string? ConvertLibraries(string value)
    {
        var libraries = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = LibraryName.Match(part);
            if (!match.Success)
            {
                return null;
            }

            libraries.Add(match.Groups["name"].Value + ":lle");
        }

        return libraries.Count == 0 ? null : string.Join(", ", libraries);
    }

    private static string? ConvertCorpusValue(
        string wikiKey,
        string value)
    {
        if (wikiKey.Equals("firmware libraries", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertLibraries(value);
        }

        if (CorpusValues.TryGetValue(wikiKey, out var allowed) && !allowed.Contains(value))
        {
            return null;
        }

        if (Keys.Contains(wikiKey) &&
            !value.Equals("On", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Off", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (CorpusIntegerKeys.Contains(wikiKey, StringComparer.OrdinalIgnoreCase) &&
            !TryNumeric(value, wikiKey))
        {
            return null;
        }

        return Convert(wikiKey, value);
    }

    private static bool TryNumeric(
        string value,
        string wikiKey)
    {
        var bound = NumericBounds[wikiKey];
        var normalized = value.Trim().TrimEnd('%');
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed >= bound.Min &&
               parsed <= bound.Max;
    }

    private static HashSet<string> Values(params string[] values)
    {
        return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ResolveApplicableSerials(
        string page,
        IReadOnlyList<string> pageSerials,
        IReadOnlyList<WikiGameIdBinding> bindings,
        string wikiKey,
        string? note,
        List<WikiCorpusFinding> findings,
        out bool ambiguous)
    {
        ambiguous = false;
        // GameID role describes the release label, not the scope of the page's
        // configuration. Explicit GameIDs share an unrestricted recommendation.
        // Non-game media listed beside releases is not executable game evidence.
        var explicitSerials = bindings
            .Where(binding => !Regex.IsMatch(binding.VariantLabel ?? string.Empty,
                @"\b(?:soundtrack|concept movie|video|theme|avatar)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(binding => binding.Serial)
            .ToArray();
        if (string.IsNullOrWhiteSpace(note))
        {
            return explicitSerials;
        }

        var referenced = SnapshotSerial.Matches(note)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var labelApplicable = explicitSerials;
        var scopeMatches = Regex.Matches(note,
            @"\b(?:for|on|in)\s+(?:the\s+)?(?<scope>(?:[\w'-]+\s+){0,3}(?:edition|version|region))\b|" +
            @"\b(?<scope>(?:[\w'-]+\s+){0,3}(?:editions?|versions?|regions?))\s+only\b|" +
            @"\b(?<scope>PAL|NTSC|US|EU|USA|Europe|Japan|Japanese|European|North American)\s+only\b|" +
            @"\b(?<scope>(?:edition|region)\s+[\w'-]+)\s+only\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (scopeMatches.Count > 1)
        {
            ambiguous = true;
            findings.Add(new WikiCorpusFinding(WikiFindingCode.RegionAmbiguous, page, null, wikiKey,
                "Multiple release scope clauses require explicit resolution."));
            return [];
        }
        if (scopeMatches.Count == 1 && !VersionCondition.IsMatch(note) && !PatchCondition.IsMatch(note))
        {
            var label = scopeMatches[0].Groups["scope"].Value.Trim();
            var scoped = bindings.Where(binding => MatchesReleaseScope(label, binding))
                .Select(binding => binding.Serial)
                .Intersect(explicitSerials, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (scoped.Length > 0)
            {
                labelApplicable = Regex.IsMatch(note, @"\b(?:not(?:\s+required)?|except|excluding)\s+(?:for\s+)?(?:the\s+)?" + Regex.Escape(label),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? explicitSerials.Except(scoped, StringComparer.OrdinalIgnoreCase).ToArray()
                    : scoped;
            }

            else
            {
                ambiguous = true;
                findings.Add(new WikiCorpusFinding(WikiFindingCode.RegionAmbiguous, page, null, wikiKey,
                    $"Recommendation scope '{label}' has no explicit matching GameID release label."));
                return [];
            }
        }

        if (referenced.Length == 0) return labelApplicable;

        var applicable = referenced
            .Where(serial => explicitSerials.Contains(serial, StringComparer.OrdinalIgnoreCase))
            .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var outside = referenced
            .Where(serial => !pageSerials.Contains(serial, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (outside.Length > 0 || applicable.Length == 0)
        {
            ambiguous = true;
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.RegionAmbiguous,
                page,
                null,
                wikiKey,
                outside.Length > 0
                    ? $"Note references serial(s) outside the page GameID set: {string.Join(", ", outside)}."
                    : "Note references a non-game media GameID binding."));
            return [];
        }

        var excluded = applicable.Where(serial => Regex.IsMatch(note,
            @"\b(?:not(?:\s+required)?\s+for|except|excluding)\s+(?:the\s+)?" + Regex.Escape(serial) + @"\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();
        if (excluded.Length > 0 && excluded.Length != applicable.Length)
        {
            ambiguous = true;
            findings.Add(new WikiCorpusFinding(WikiFindingCode.RegionAmbiguous, page, null, wikiKey,
                "Mixed positive and negative serial scope requires manual resolution."));
            return [];
        }
        var serialApplicable = excluded.Length > 0
            ? explicitSerials.Except(applicable, StringComparer.OrdinalIgnoreCase).ToArray()
            : applicable;
        return serialApplicable.Intersect(labelApplicable, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeScopeLabel(string? label)
    {
        var normalized = Regex.Replace(label ?? string.Empty, @"\s+", " ").Trim();
        return normalized.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ? normalized[4..] : normalized;
    }

    private static bool MatchesReleaseScope(string scope, WikiGameIdBinding binding)
    {
        var normalizedScope = NormalizeScopeLabel(scope);
        var normalizedLabel = NormalizeScopeLabel(binding.VariantLabel);
        if (string.Equals(normalizedLabel, normalizedScope, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedScope.EndsWith(" Edition", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedLabel, normalizedScope + " Demo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(normalizedScope, @"^digital (?:edition|versions?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return binding.Serial.StartsWith("N", StringComparison.OrdinalIgnoreCase);
        }

        if (Regex.IsMatch(normalizedScope, @"^(?:disc|blu-ray) (?:edition|versions?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return binding.Serial.StartsWith("B", StringComparison.OrdinalIgnoreCase) ||
                   binding.Serial.StartsWith("M", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static List<WikiRec> BuildRecommendations(
        WikiCorpusPage[] pages,
        List<WikiCorpusFinding> findings)
    {
        var serialGroups = pages
            .Where(page => page.IsGameContent)
            .SelectMany(page => page.GameIdBindings.Select(binding => (Binding: binding, Page: page)))
            .GroupBy(item => item.Binding.Serial, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var output = new List<WikiRec>();

        foreach (var group in serialGroups)
        {
            var candidates = group
                .Select(item => item.Page)
                .DistinctBy(PageIdentity, StringComparer.Ordinal)
                .OrderBy(page => page.Title, StringComparer.Ordinal)
                .ToArray();
            var primaryCandidates = group
                .Where(item => item.Binding.Role == WikiGameIdRole.Primary)
                .Select(item => item.Page)
                .DistinctBy(PageIdentity, StringComparer.Ordinal)
                .OrderBy(page => page.Title, StringComparer.Ordinal)
                .ToArray();
            if (primaryCandidates.Length > 1)
            {
                findings.Add(new WikiCorpusFinding(
                    WikiFindingCode.DuplicateSerialConflict,
                    string.Join(" | ", primaryCandidates.Select(page => page.Title)),
                    group.Key,
                    null,
                    "The same serial has multiple primary Wiki pages; all candidates were excluded."));
                continue;
            }

            if (primaryCandidates.Length == 0 && candidates.Length > 1)
            {
                findings.Add(new WikiCorpusFinding(
                    WikiFindingCode.DuplicateSerialConflict,
                    string.Join(" | ", candidates.Select(page => page.Title)),
                    group.Key,
                    null,
                    "The same non-primary serial is referenced by multiple Wiki pages; no winner was selected."));
                continue;
            }

            // Page ownership and configuration evidence are separate. A dedicated
            // page with no settings must not hide a uniquely evidenced shared page.
            var applicablePages = candidates.Where(candidate => candidate.Recommendations.Any(entry =>
                entry.IncludedInCatalog && entry.ApplicableSerials.Contains(group.Key, StringComparer.OrdinalIgnoreCase))).ToArray();
            var page = applicablePages.Length == 1 ? applicablePages[0]
                : primaryCandidates.Length == 1 ? primaryCandidates[0] : candidates[0];
            var binding = group.First(item => PageIdentity(item.Page) == PageIdentity(page)).Binding;
            var mapped = page.Recommendations
                .Where(entry => entry.IncludedInCatalog &&
                                entry.ApplicableSerials.Contains(
                                    group.Key,
                                    StringComparer.OrdinalIgnoreCase))
                .SelectMany(entry => entry.MappedSettings.Select(target => (Entry: entry, Target: target)))
                .ToArray();
            var changes = new List<CfgChg>();

            foreach (var targetGroup in mapped.GroupBy(
                         item => $"{item.Target.Section}\0{item.Target.Key}",
                         StringComparer.OrdinalIgnoreCase))
            {
                var values = targetGroup
                    .Select(item => item.Target.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (values.Length > 1)
                {
                    findings.Add(new WikiCorpusFinding(
                        WikiFindingCode.DuplicateSettingConflict,
                        page.Title,
                        group.Key,
                        string.Join(", ", targetGroup.Select(item => item.Entry.WikiKey).Distinct(StringComparer.OrdinalIgnoreCase)),
                        $"Multiple Wiki recommendations map to '{targetGroup.Key.Replace('\0', '/')}' with conflicting values."));
                    continue;
                }

                var selected = targetGroup
                    .OrderBy(item => item.Entry.WikiKey, StringComparer.OrdinalIgnoreCase)
                    .First();
                changes.Add(new CfgChg(
                    selected.Target.Section,
                    selected.Target.Key,
                    selected.Target.Value,
                    selected.Entry.WikiKey,
                    selected.Entry.Notes,
                    RecPol.Classify(
                        selected.Entry.WikiKey,
                        selected.Target.Value,
                        selected.Entry.Notes),
                    selected.Entry.Classification));
            }

            output.Add(new WikiRec(
                group.Key.ToUpperInvariant(),
                page.Title,
                changes
                    .OrderBy(change => change.Section, StringComparer.Ordinal)
                    .ThenBy(change => change.Key, StringComparer.Ordinal)
                    .ToArray(),
                changes.Count > 0 ? WikiGameIdRole.Primary : binding.Role,
                binding.VariantLabel));
        }

        return output;
    }

    private static string PageIdentity(WikiCorpusPage page)
    {
        return page.PageId?.ToString(CultureInfo.InvariantCulture) ?? $"{page.Namespace}\0{page.Title}";
    }

    private static void AddCorpusValue(
        Dictionary<string, string> values,
        HashSet<string> conflicted,
        string key,
        string value,
        string page,
        List<WikiCorpusFinding> findings)
    {
        if (!values.TryGetValue(key, out var existing))
        {
            values.Add(key, value);
            return;
        }

        if (string.Equals(CleanVal(existing), CleanVal(value), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        conflicted.Add(key);
        findings.Add(new WikiCorpusFinding(
            WikiFindingCode.DuplicateSettingConflict,
            page,
            null,
            key,
            $"Conflicting values '{CleanVal(existing)}' and '{CleanVal(value)}'."));
    }

    private static void AddCorpusNote(
        Dictionary<string, List<string>> notes,
        string key,
        string value)
    {
        if (!notes.TryGetValue(key, out var values))
        {
            values = [];
            notes.Add(key, values);
        }

        if (!values.Any(existing =>
                string.Equals(CleanVal(existing), CleanVal(value), StringComparison.Ordinal)))
        {
            values.Add(value);
        }
    }

    private static List<TemplateBlock> FindTemplateBlocks(
        string text,
        string name,
        string page,
        List<WikiCorpusFinding> findings)
    {
        var blocks = new List<TemplateBlock>();
        var needle = "{{" + name;
        var from = 0;

        while (from < text.Length)
        {
            var start = text.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                break;
            }

            var commentStart = text.LastIndexOf("<!--", start, StringComparison.Ordinal);
            if (commentStart >= 0 && text.LastIndexOf("-->", start, StringComparison.Ordinal) < commentStart)
            {
                var commentEnd = text.IndexOf("-->", start, StringComparison.Ordinal);
                if (commentEnd < 0) break;
                from = commentEnd + 3;
                continue;
            }

            var afterName = start + needle.Length;
            if (afterName < text.Length &&
                text[afterName] is not ('|' or '}' or '\r' or '\n' or ' ' or '\t'))
            {
                from = afterName;
                continue;
            }

            var depth = 0;
            var index = start;
            var end = -1;
            while (index < text.Length - 1)
            {
                if (text[index] == '{' && text[index + 1] == '{')
                {
                    depth++;
                    index += 2;
                    continue;
                }

                if (text[index] == '}' && text[index + 1] == '}')
                {
                    depth--;
                    index += 2;
                    if (depth == 0)
                    {
                        end = index;
                        break;
                    }

                    continue;
                }

                index++;
            }

            if (end < 0)
            {
                findings.Add(new WikiCorpusFinding(
                    WikiFindingCode.InvalidConfigTemplate,
                    page,
                    null,
                    name,
                    $"Unterminated '{{{{{name}}}' template."));
                blocks.Add(new TemplateBlock(name, text[start..], false, start, text.Length));
                break;
            }

            blocks.Add(new TemplateBlock(name, text[start..end], true, start, end));
            if (blocks.Count > MaxTemplatesPerPage)
            {
                throw new InvalidDataException(
                    $"Wiki page '{page}' exceeds the {MaxTemplatesPerPage}-template limit for '{name}'.");
            }

            from = end;
        }

        return blocks;
    }

    private static List<TemplateParameter> SplitTemplateParameters(
        TemplateBlock block,
        string page,
        List<WikiCorpusFinding> findings)
    {
        if (!block.Complete)
        {
            return [];
        }

        var prefixLength = 2 + block.Name.Length;
        var bodyLength = block.Text.Length - prefixLength - 2;
        if (bodyLength < 0)
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.InvalidConfigTemplate,
                page,
                null,
                block.Name,
                "Template body is shorter than its delimiters."));
            return [];
        }

        var body = block.Text.AsSpan(prefixLength, bodyLength);
        var pieces = new List<string>();
        var start = 0;
        var templateDepth = 0;
        var linkDepth = 0;

        for (var index = 0; index < body.Length; index++)
        {
            if (index + 1 < body.Length && body[index] == '{' && body[index + 1] == '{')
            {
                templateDepth++;
                index++;
                continue;
            }

            if (index + 1 < body.Length && body[index] == '}' && body[index + 1] == '}')
            {
                templateDepth = Math.Max(0, templateDepth - 1);
                index++;
                continue;
            }

            if (index + 1 < body.Length && body[index] == '[' && body[index + 1] == '[')
            {
                linkDepth++;
                index++;
                continue;
            }

            if (index + 1 < body.Length && body[index] == ']' && body[index + 1] == ']')
            {
                linkDepth = Math.Max(0, linkDepth - 1);
                index++;
                continue;
            }

            if (body[index] == '|' && templateDepth == 0 && linkDepth == 0)
            {
                pieces.Add(body[start..index].ToString());
                start = index + 1;
            }
        }

        pieces.Add(body[start..].ToString());
        if (pieces.Count > MaxParametersPerTemplate)
        {
            throw new InvalidDataException(
                $"Wiki page '{page}' exceeds the {MaxParametersPerTemplate}-parameter limit for '{block.Name}'.");
        }

        var parameters = new List<TemplateParameter>(pieces.Count);
        foreach (var piece in pieces)
        {
            var trimmed = piece.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var equals = trimmed.IndexOf('=', StringComparison.Ordinal);
            parameters.Add(equals < 0
                ? new TemplateParameter(null, trimmed)
                : new TemplateParameter(
                    trimmed[..equals].Trim(),
                    trimmed[(equals + 1)..].Trim()));
        }

        return parameters;
    }

    private static string[] ExtractPatchReferences(
        string text,
        IReadOnlyList<WikiCorpusEntry> entries)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => entry.Classification == WikiRecommendationClass.PatchSpecific))
        {
            references.Add(entry.RawWikiValue);
        }

        foreach (Match match in WikiLink.Matches(text))
        {
            var body = CleanVal(match.Value);
            if ((body.Contains("patch", StringComparison.OrdinalIgnoreCase) ||
                 body.Contains("canary", StringComparison.OrdinalIgnoreCase)) &&
                body.Length <= 512)
            {
                references.Add(body);
            }
        }

        return references.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static WikiHistoryReadResult ReadLatestWikiPages(string path)
    {
        var latest = new Dictionary<string, RawWikiPage>(StringComparer.Ordinal);
        var historicalPageBlocks = 0;
        var totalRevisions = 0;
        var encounterOrder = 0;

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxWikiHistoryXmlBytes,
                XmlResolver = null
            });

        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element ||
                !reader.LocalName.Equals("page", StringComparison.Ordinal))
            {
                reader.Read();
                continue;
            }

            historicalPageBlocks++;
            if (historicalPageBlocks > MaxHistoricalPageBlocks)
            {
                throw new InvalidDataException(
                    $"RPCS3 wiki XML exceeds the {MaxHistoricalPageBlocks}-page-block history limit.");
            }

            var page = (XElement)XNode.ReadFrom(reader);
            var title = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "title")?
                .Value
                .Trim() ?? string.Empty;
            var namespaceText = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "ns")?
                .Value;
            var parsedNamespace = string.IsNullOrWhiteSpace(namespaceText)
                ? 0
                : int.TryParse(
                    namespaceText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var ns)
                    ? ns
                    : -1;
            var pageIdText = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "id")?
                .Value;
            var pageId = long.TryParse(
                pageIdText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedPageId)
                ? parsedPageId
                : (long?)null;
            var redirectTarget = page.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "redirect")?
                .Attribute("title")?
                .Value
                .Trim();
            var revisions = page.Elements()
                .Where(element => element.Name.LocalName == "revision")
                .ToArray();
            totalRevisions += revisions.Length;
            if (totalRevisions > MaxWikiRevisions)
            {
                throw new InvalidDataException(
                    $"RPCS3 wiki XML exceeds the {MaxWikiRevisions}-revision history limit.");
            }

            if (revisions.Length == 0)
            {
                StoreLatest(new RawWikiPage(
                    pageId,
                    title,
                    parsedNamespace,
                    redirectTarget,
                    null,
                    null,
                    string.Empty,
                    encounterOrder++));
                continue;
            }

            foreach (var revision in revisions)
            {
                var revisionId = revision.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "id")?
                    .Value
                    .Trim();
                var timestampText = revision.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "timestamp")?
                    .Value;
                var revisionTimestamp = DateTimeOffset.TryParse(
                    timestampText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp)
                    ? timestamp
                    : (DateTimeOffset?)null;
                var text = revision.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "text")?
                    .Value ?? string.Empty;
                var selectedRedirectTarget = redirectTarget ?? ExtractRedirectTarget(text);
                StoreLatest(new RawWikiPage(
                    pageId,
                    title,
                    parsedNamespace,
                    selectedRedirectTarget,
                    revisionId,
                    revisionTimestamp,
                    text,
                    encounterOrder++));
            }
        }

        return new WikiHistoryReadResult(
            latest.Values
                .OrderBy(page => page.Namespace)
                .ThenBy(page => page.Title, StringComparer.Ordinal)
                .ToArray(),
            historicalPageBlocks,
            totalRevisions);

        void StoreLatest(RawWikiPage candidate)
        {
            var identity = candidate.PageId is not null
                ? $"id:{candidate.PageId.Value.ToString(CultureInfo.InvariantCulture)}"
                : $"title:{candidate.Namespace.ToString(CultureInfo.InvariantCulture)}\0{candidate.Title}";
            if (!latest.TryGetValue(identity, out var current) ||
                IsNewerRevision(candidate, current))
            {
                latest[identity] = candidate;
            }
        }
    }

    private static string? ExtractRedirectTarget(string text)
    {
        var match = RedirectText.Match(text);
        return match.Success ? match.Groups["target"].Value.Trim() : null;
    }

    private static bool IsNewerRevision(
        RawWikiPage candidate,
        RawWikiPage current)
    {
        if (candidate.RevisionTimestamp is not null &&
            current.RevisionTimestamp is not null &&
            candidate.RevisionTimestamp != current.RevisionTimestamp)
        {
            return candidate.RevisionTimestamp > current.RevisionTimestamp;
        }

        var candidateRevisionId = long.TryParse(
            candidate.RevisionId,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedCandidateRevisionId)
            ? parsedCandidateRevisionId
            : (long?)null;
        var currentRevisionId = long.TryParse(
            current.RevisionId,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedCurrentRevisionId)
            ? parsedCurrentRevisionId
            : (long?)null;
        if (candidateRevisionId is not null &&
            currentRevisionId is not null &&
            candidateRevisionId != currentRevisionId)
        {
            return candidateRevisionId > currentRevisionId;
        }

        if ((candidate.RevisionTimestamp is not null) != (current.RevisionTimestamp is not null))
        {
            return candidate.RevisionTimestamp is not null;
        }

        if ((candidateRevisionId is not null) != (currentRevisionId is not null))
        {
            return candidateRevisionId is not null;
        }

        return candidate.EncounterOrder > current.EncounterOrder;
    }

    private static string ResolveSnapshotDate(
        string path,
        string? requested,
        IReadOnlyList<WikiCorpusPage> pages)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (!DateOnly.TryParseExact(
                    requested.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var explicitDate))
            {
                throw new ArgumentException(
                    "Snapshot date must use yyyy-MM-dd format.",
                    nameof(requested));
            }

            return explicitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var match = SnapshotDateToken.Match(Path.GetFileNameWithoutExtension(path));
        if (match.Success &&
            DateOnly.TryParseExact(
                match.Groups["date"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fileDate))
        {
            return fileDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var latestRevision = pages
            .Where(page => page.RevisionTimestamp is not null)
            .Select(page => page.RevisionTimestamp!.Value)
            .DefaultIfEmpty()
            .Max();
        if (latestRevision != default)
        {
            return latestRevision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        throw new InvalidDataException(
            "Wiki snapshot date could not be derived from the file name or revision timestamps.");
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return System.Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ChangePath(CfgChg change)
    {
        return $"{change.Section}\0{change.Key}";
    }

    private sealed record TemplateBlock(
        string Name,
        string Text,
        bool Complete,
        int Start,
        int End);

    private sealed record TemplateParameter(
        string? Key,
        string Value);

    private sealed record GameIdParseResult(
        IReadOnlyList<string> Serials,
        IReadOnlyList<WikiGameIdBinding> Bindings,
        int RawReferences)
    {
        public static GameIdParseResult Empty { get; } = new([], [], 0);
    }

    private sealed record RawWikiPage(
        long? PageId,
        string Title,
        int Namespace,
        string? RedirectTarget,
        string? RevisionId,
        DateTimeOffset? RevisionTimestamp,
        string Text,
        int EncounterOrder);

    private sealed record WikiHistoryReadResult(
        IReadOnlyList<RawWikiPage> Pages,
        int HistoricalPageBlocks,
        int TotalRevisions);
}
