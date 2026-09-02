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

    private static readonly Regex VersionCondition = new(
        @"\bno longer required\b|\b(?:older|newer) versions?\b|" +
        @"\b(?:RPCS3|emulator|game) (?:versions?|builds?)\b|\bversions?\s+(?:above|below|before|after|prior to|through)\s+\d|" +
        @"\bversion\s+\d|\bbuild\s+\d|" +
        @"\buntil\s+RPCS3\s+(?:PR|pull request|build|version)\b|" +
        @"\b(?:fixed|regression|removed)\b[^.]{0,80}\b(?:RPCS3|emulator|version|build|PR)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchCondition = new(
        @"\bcanary\b|\bunpatched\b|\bunlock fps\b|" +
        @"\b(?:the|a|an|game|fps|mlaa|recommended|disable [a-z0-9 -]+)\s+patch(?:es)?\b|" +
        @"\bpatch(?:es)?\s+(?:is|are|enabled|disabled|required|available|notes)\b|" +
        @"\busing\b[^.]{0,80}\bpatch(?:es)?\b|\b(?:30|60|120)\s*fps patch\b",
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
            ["ppu decoder"] = Values("Interpreter (static)", "Interpreter (dynamic)", "Recompiler (LLVM)"),
            ["spu decoder"] = Values("Interpreter (static)", "Interpreter (dynamic)", "Recompiler (ASMJIT)", "Recompiler (LLVM)"),
            ["thread scheduler"] = Values("Operating System", "RPCS3 Scheduler", "RPCS3 Alternative Scheduler"),
            ["spu xfloat accuracy"] = Values("Accurate", "Approximate", "Relaxed"),
            ["xfloat accuracy"] = Values("Accurate", "Approximate", "Relaxed"),
            ["spu block size"] = Values("Safe", "Mega", "Giga"),
            ["rsx fifo accuracy"] = Values("Fast", "Atomic", "Ordered & Atomic"),
            ["sleep timers accuracy"] = Values("As Host", "Usleep Only", "All Timers"),
            ["renderer"] = Values("OpenGL", "Vulkan", "Disabled"),
            ["aspect ratio"] = Values("4:3", "16:9"),
            ["framelimit"] = Values("Off", "50", "60", "30", "Auto", "PS3 Native", "Infinite"),
            ["anti-aliasing"] = Values("Auto", "Disabled"),
            ["shader quality"] = Values("Ultra", "High", "Low", "Auto"),
            ["3d rendering mode"] = Values(
                "Disabled", "Anaglyph", "Anaglyph Red-Green", "Anaglyph Red-Blue", "Anaglyph Red-Cyan",
                "Anaglyph Magenta-Cyan", "Anaglyph Trioscopic", "Anaglyph Amber-Blue", "Side-by-Side",
                "Over-Under", "Interlaced"),
            ["default resolution"] = Values(
                "1920x1080", "1280x720", "720x480", "720x576", "1600x1080", "1440x1080", "1280x1080", "960x1080"),
            ["shader mode"] = Values(
                "Legacy Recompiler (single-threaded)", "Async Recompiler (multi-threaded)",
                "Async Recompiler with Shader Interpreter", "Shader Interpreter only"),
            ["vsync"] = Values("Disabled", "Adaptive", "Full"),
            ["audio out windows"] = Values("Cubeb", "XAudio2", "Disabled"),
            ["audio format"] = Values("Stereo", "Surround 5.1", "Surround 7.1", "Automatic", "Manual"),
            ["microphone type"] = Values("Disabled", "Standard", "SingStar", "Real SingStar", "Rocksmith"),
            ["keyboard handler"] = Values("Null", "Basic"),
            ["mouse handler"] = Values("Null", "Basic"),
            ["camera input"] = Values("Unknown", "EyeToy", "PS Eye", "UVC 1.1"),
            ["camera settings"] = Values("Null", "Fake", "Qt", "SDL"),
            ["move handler"] = Values("Null", "Real", "Fake", "Mouse", "Gun"),
            ["network status"] = Values("Disconnected", "Connected"),
            ["psn status"] = Values("Disconnected", "Simulated", "RPCN")
        };

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
        if (file.Length < 0 || file.Length > MaxWikiXmlBytes)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML exceeds the {MaxWikiXmlBytes}-byte input limit.");
        }

        var stopwatch = Stopwatch.StartNew();
        var sourceHash = HashFile(wikiXmlPath);
        var document = ReadWikiDocument(wikiXmlPath);
        var pageElements = document
            .Descendants()
            .Where(element => element.Name.LocalName == "page")
            .ToArray();

        if (pageElements.Length > MaxWikiPages)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML exceeds the {MaxWikiPages}-page input limit.");
        }

        var findings = new List<WikiCorpusFinding>();
        var pages = new List<WikiCorpusPage>(pageElements.Length);

        foreach (var pageElement in pageElements)
        {
            pages.Add(ParsePage(pageElement, findings));
        }

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
            .SelectMany(page => page.Serials.Select(serial => (Serial: serial, Page: page)))
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
        var resolvedSnapshotDate = ResolveSnapshotDate(
            wikiXmlPath,
            snapshotDate,
            pages);

        stopwatch.Stop();

        var stats = new WikiCorpusStats(
            TotalPages: pages.Count,
            ParsedPages: pages.Count(page => page.Parsed),
            ParseFailures: pages.Count(page => !page.Parsed),
            MainNamespacePages: pages.Count(page => page.Namespace == 0),
            GameContentPages: gamePages.Length,
            RedirectPages: pages.Count(page => page.Namespace == 0 && page.IsRedirect),
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
        XElement page,
        List<WikiCorpusFinding> findings)
    {
        var title = page.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value?.Trim();
        var namespaceText = page.Elements().FirstOrDefault(element => element.Name.LocalName == "ns")?.Value;
        var revision = page.Elements().FirstOrDefault(element => element.Name.LocalName == "revision");
        var text = revision?.Elements().FirstOrDefault(element => element.Name.LocalName == "text")?.Value ?? string.Empty;
        var revisionId = revision?.Elements().FirstOrDefault(element => element.Name.LocalName == "id")?.Value?.Trim();
        var timestampText = revision?.Elements().FirstOrDefault(element => element.Name.LocalName == "timestamp")?.Value;
        var parsedTimestamp = DateTimeOffset.TryParse(
            timestampText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : (DateTimeOffset?)null;
        var parsedNamespace = int.TryParse(namespaceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ns)
            ? ns
            : -1;
        var isRedirect = page.Elements().Any(element => element.Name.LocalName == "redirect");
        var isGameContent = parsedNamespace == 0 && !isRedirect;
        title ??= "<untitled>";

        var gameIdBlocks = FindTemplateBlocks(text, "gameid", title, findings);
        var configBlocks = FindTemplateBlocks(text, "config", title, findings);
        var serials = isGameContent
            ? ParseGameIds(title, gameIdBlocks, findings)
            : Array.Empty<string>();
        var (entries, notes) = isGameContent
            ? ParseConfig(title, serials, configBlocks, findings)
            : (Array.Empty<WikiCorpusEntry>(), Array.Empty<WikiCorpusNote>());
        var patchReferences = ExtractPatchReferences(text, entries);

        return new WikiCorpusPage(
            title,
            parsedNamespace,
            isRedirect,
            isGameContent,
            title != "<untitled>",
            revisionId,
            parsedTimestamp,
            serials,
            gameIdBlocks.Count,
            configBlocks.Count,
            patchReferences,
            entries,
            notes);
    }

    private static string[] ParseGameIds(
        string page,
        IReadOnlyList<TemplateBlock> blocks,
        List<WikiCorpusFinding> findings)
    {
        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sawCandidate = false;

        foreach (var block in blocks)
        {
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
                        serials.Add(cleaned);
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

        if (blocks.Count > 0 && (!sawCandidate || serials.Count == 0))
        {
            findings.Add(new WikiCorpusFinding(
                WikiFindingCode.EmptyGameId,
                page,
                null,
                null,
                "The game page has no valid GameID value."));
        }

        return serials.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static (IReadOnlyList<WikiCorpusEntry> Entries, IReadOnlyList<WikiCorpusNote> Notes) ParseConfig(
        string page,
        IReadOnlyList<string> serials,
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
                    : "No verified R3Config mapping.");
        }

        var classification = ClassifyRecommendation(wikiKey, note);
        var included = !regionAmbiguous &&
                       classification is WikiRecommendationClass.MandatoryGameConfig or
                           WikiRecommendationClass.GeneralRecommendation;
        return Entry(
            regionAmbiguous ? WikiRecommendationClass.Ambiguous : classification,
            regionAmbiguous ? WikiParserStatus.Ambiguous : WikiParserStatus.Mapped,
            included,
            targets,
            regionAmbiguous
                ? "The note refers to a serial outside the page's GameID set."
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
        if (PatchConfigKeys.Contains(wikiKey) ||
            (!string.IsNullOrWhiteSpace(note) && PatchCondition.IsMatch(note)))
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

    private static string? ConvertCorpusValue(
        string wikiKey,
        string value)
    {
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

        if (wikiKey.Equals("resolution scale", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 50, 800))
        {
            return null;
        }

        if (wikiKey.Equals("volume", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 0, 200))
        {
            return null;
        }

        if (wikiKey.Equals("audio buffer duration", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 4, 250))
        {
            return null;
        }

        if (wikiKey.Equals("time stretching threshold", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 0, 100))
        {
            return null;
        }

        if (wikiKey.Equals("driver wake-up delay", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 0, 7000))
        {
            return null;
        }

        if (wikiKey.Equals("vblank rate", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 1, 3000))
        {
            return null;
        }

        if (wikiKey.Equals("clocks scale", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 10, 3000))
        {
            return null;
        }

        if (wikiKey.Equals("maximum spurs threads", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Unlimited", StringComparison.OrdinalIgnoreCase) &&
            !TryNumeric(value, 0, 5))
        {
            return null;
        }

        return Convert(wikiKey, value);
    }

    private static bool TryNumeric(
        string value,
        int minimum,
        int maximum)
    {
        var normalized = value.Trim().TrimEnd('%');
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed >= minimum &&
               parsed <= maximum;
    }

    private static HashSet<string> Values(params string[] values)
    {
        return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveApplicableSerials(
        string page,
        IReadOnlyList<string> pageSerials,
        string wikiKey,
        string? note,
        List<WikiCorpusFinding> findings,
        out bool ambiguous)
    {
        ambiguous = false;
        if (string.IsNullOrWhiteSpace(note))
        {
            return pageSerials;
        }

        var referenced = SnapshotSerial.Matches(note)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (referenced.Length == 0)
        {
            return pageSerials;
        }

        var applicable = referenced
            .Where(serial => pageSerials.Contains(serial, StringComparer.OrdinalIgnoreCase))
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
                $"Note references serial(s) outside the page GameID set: {string.Join(", ", outside)}."));
            return [];
        }

        return applicable;
    }

    private static List<WikiRec> BuildRecommendations(
        WikiCorpusPage[] pages,
        List<WikiCorpusFinding> findings)
    {
        var serialGroups = pages
            .Where(page => page.IsGameContent)
            .SelectMany(page => page.Serials.Select(serial => (Serial: serial, Page: page)))
            .GroupBy(item => item.Serial, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var output = new List<WikiRec>();

        foreach (var group in serialGroups)
        {
            var candidates = group
                .Select(item => item.Page)
                .DistinctBy(page => page.Title, StringComparer.Ordinal)
                .OrderBy(page => page.Title, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length > 1 && !EquivalentPages(group.Key, candidates))
            {
                findings.Add(new WikiCorpusFinding(
                    WikiFindingCode.DuplicateSerialConflict,
                    string.Join(" | ", candidates.Select(page => page.Title)),
                    group.Key,
                    null,
                    "The same serial is assigned to different Wiki pages or recommendations; all candidates were excluded."));
                continue;
            }

            var page = candidates[0];
            var mapped = page.Recommendations
                .Where(entry => entry.IncludedInCatalog &&
                                entry.ApplicableSerials.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
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
                        selected.Entry.Notes)));
            }

            output.Add(new WikiRec(
                group.Key.ToUpperInvariant(),
                page.Title,
                changes
                    .OrderBy(change => change.Section, StringComparer.Ordinal)
                    .ThenBy(change => change.Key, StringComparer.Ordinal)
                    .ToArray()));
        }

        return output;
    }

    private static bool EquivalentPages(
        string serial,
        WikiCorpusPage[] pages)
    {
        var first = PageSignature(serial, pages[0]);
        return pages.Skip(1).All(page =>
            string.Equals(pages[0].Title, page.Title, StringComparison.Ordinal) &&
            first.SequenceEqual(PageSignature(serial, page), StringComparer.Ordinal));
    }

    private static string[] PageSignature(
        string serial,
        WikiCorpusPage page)
    {
        return page.Recommendations
            .Where(entry => entry.IncludedInCatalog &&
                            entry.ApplicableSerials.Contains(serial, StringComparer.OrdinalIgnoreCase))
            .SelectMany(entry => entry.MappedSettings.Select(target =>
                $"{entry.WikiKey}\0{target.Section}\0{target.Key}\0{target.Value}\0{entry.Notes}"))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
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
                blocks.Add(new TemplateBlock(name, text[start..], false));
                break;
            }

            blocks.Add(new TemplateBlock(name, text[start..end], true));
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

    private static XDocument ReadWikiDocument(string path)
    {
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
                MaxCharactersInDocument = MaxWikiXmlBytes,
                XmlResolver = null
            });
        return XDocument.Load(reader, LoadOptions.None);
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
        bool Complete);

    private sealed record TemplateParameter(
        string? Key,
        string Value);
}
