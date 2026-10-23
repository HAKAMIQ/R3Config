using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

// Full-corpus coverage gate for the Wiki -> Game Settings pipeline: settings
// coverage/classification (reusing the already-verified WikiSvc.Analyze
// pipeline), the free-text notes / online-RPCN evidence extraction
// (WikiNotesParser / WikiNotesCorpusReader), and proof that every supported
// setting reaches its Game Settings section through the same
// GameSettingsProjector the app uses. Read-only: it never writes to the
// shipped catalog, never touches RPCS3, and never promotes the supplied XML to
// canonical. Invoked as
// `R3Test.exe --wiki-notes-audit <xml> <dir> [candidate-sidecar.json]`; the
// optional third path receives a candidate Data/wiki-notes.json and is never
// written into the shipped Data folder by this tool.
internal static partial class Program
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };

    private static void WikiNotesAudit(string xmlPath, string reportDir, string? sidecarPath = null)
    {
        var analysis = WikiSvc.Analyze(xmlPath);
        var pages = WikiNotesCorpusReader.ReadAndParse(xmlPath);

        var byWikiKey = analysis.Recommendations
            .GroupBy(entry => entry.WikiKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var distinctSettingNames = byWikiKey.Length;
        var supportedCanonical = analysis.Recommendations.Count(entry => entry.ParserStatus == WikiParserStatus.Mapped);
        var unsupported = analysis.Recommendations.Count(entry => entry.ParserStatus == WikiParserStatus.Unsupported);
        var ambiguous = analysis.Recommendations.Count(entry => entry.ParserStatus == WikiParserStatus.Ambiguous);
        var unknown = analysis.Recommendations.Count(entry => entry.ParserStatus == WikiParserStatus.UnknownWikiKey);
        var rejected = analysis.Recommendations.Count(entry => entry.ParserStatus == WikiParserStatus.Rejected);
        var classifiedTotal = supportedCanonical + unsupported + ambiguous + unknown + rejected;
        var silentlyDropped = analysis.Stats.TotalConfigEntries - classifiedTotal;

        // DISCOVERED/SUPPORTED come from the parser classification; APPLIED_TO_GAME_SETTINGS
        // is measured by projecting each page's supported settings through the
        // real GameSettingsProjector and counting the rows that land in a
        // section. Anything supported but not wired is DROPPED.
        var rows = Enum.GetValues<Rpcs3SettingsSection>()
            .ToDictionary(section => section, _ => new SectionRow());

        // Wiki rows whose key is not a real RPCS3 setting (patch pointers such
        // as "recommended patch") have no Settings tab; they are never mapped
        // and are reported apart instead of inflating a section.
        var nonSettingRows = 0;
        var mappedWithoutExplicitSection = 0;
        foreach (var entry in analysis.Recommendations)
        {
            if (!SettingsSectionMap.IsExplicit(entry.WikiKey))
            {
                nonSettingRows++;
                if (entry.ParserStatus == WikiParserStatus.Mapped)
                {
                    mappedWithoutExplicitSection++;
                }

                continue;
            }

            var first = entry.MappedSettings.Count > 0 ? entry.MappedSettings[0] : null;
            var probe = new CfgChg(
                first?.Section ?? string.Empty,
                first?.Key ?? entry.WikiKey,
                string.Empty,
                entry.WikiKey,
                null,
                RecRisk.Unknown);
            var section = SettingsSectionMap.Classify(probe, out _);
            rows[section].Discovered++;
            if (entry.ParserStatus == WikiParserStatus.Mapped)
            {
                rows[section].Supported++;
                rows[section].SupportedSettings += entry.MappedSettings.Count;
            }
        }

        var unmappedKeys = byWikiKey
            .Select(group => group.Key)
            .Where(key => !SettingsSectionMap.IsExplicit(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var page in analysis.Pages.Where(page => page.Recommendations.Any(entry => entry.ParserStatus == WikiParserStatus.Mapped)))
        {
            var items = page.Recommendations
                .Where(entry => entry.ParserStatus == WikiParserStatus.Mapped)
                .SelectMany(entry => entry.MappedSettings.Select(setting =>
                    new CfgChg(setting.Section, setting.Key, setting.Value, entry.WikiKey, entry.Notes, RecRisk.Unknown)))
                .Select(change => new CfgEntry(
                    change,
                    null,
                    new RecItem(
                        "AUDIT00000",
                        change.Path,
                        null,
                        change.Value,
                        change.Risk,
                        RecState.Pending,
                        true,
                        true,
                        false,
                        string.Empty)))
                .ToArray();

            var plan = new CfgPlan(
                "AUDIT00000",
                page.Title,
                string.Empty,
                string.Empty,
                items,
                new RecSum("AUDIT00000", items.Length, 0, items.Length, 0, 0, 0, []),
                false);

            var model = GameSettingsProjector.Project(
                plan,
                null,
                new GameSettingsContext("AUDIT00000"));

            Assert(model.Sections.Sum(section => section.Rows.Count) == items.Length,
                $"Page '{page.Title}' lost supported settings while projecting into Game Settings sections.");

            foreach (var section in model.Sections)
            {
                rows[section.Section].WiredSettings += section.Rows.Count;
            }

            var projected = model.Sections
                .SelectMany(section => section.Rows.Select(row =>
                    (section.Section, row.Entry.Change.WikiKey, row.Entry.Section, row.Entry.Key, row.Entry.Val)))
                .ToHashSet();
            foreach (var entry in page.Recommendations.Where(entry =>
                         entry.ParserStatus == WikiParserStatus.Mapped &&
                         SettingsSectionMap.IsExplicit(entry.WikiKey)))
            {
                var first = entry.MappedSettings[0];
                var expected = SettingsSectionMap.Classify(
                    new CfgChg(first.Section, first.Key, first.Value, entry.WikiKey, null, RecRisk.Unknown),
                    out _);
                if (entry.MappedSettings.All(setting =>
                        projected.Contains((expected, entry.WikiKey, setting.Section, setting.Key, setting.Value))))
                {
                    rows[expected].Wired++;
                }
            }
        }

        var totalSupportedSettings = rows.Values.Sum(row => row.SupportedSettings);
        var totalWiredSettings = rows.Values.Sum(row => row.WiredSettings);
        var totalSupportedRows = rows.Values.Sum(row => row.Supported);
        var totalWiredRows = rows.Values.Sum(row => row.Wired);

        var freeTextNotes = pages.SelectMany(page => page.Notes).ToArray();
        var onlineEvidence = pages.SelectMany(page => page.Online).ToArray();

        var notesWired = -1;
        var onlineWired = -1;
        if (!string.IsNullOrWhiteSpace(sidecarPath))
        {
            WikiEvidenceCatalog
                .FromPages("RPCS3 Wiki snapshot", analysis.SnapshotDate, analysis.SourceSha256, pages)
                .Save(sidecarPath);
            Console.WriteLine($"WIKI_NOTES_CANDIDATE_SIDECAR path={sidecarPath}");

            // Consumer-path proof at corpus scale: reload the candidate exactly
            // as the app does and project every page through the projector.
            var reloaded = WikiEvidenceCatalog.Load(sidecarPath);
            notesWired = 0;
            onlineWired = 0;
            foreach (var page in reloaded.Pages)
            {
                var serial = page.Serials.Count > 0 ? page.Serials[0] : "AUDIT00000";
                var model = GameSettingsProjector.Project(
                    null,
                    reloaded.Find(serial, page.Page),
                    new GameSettingsContext(serial));
                notesWired += model.Notes.Count;
                onlineWired += model.Online.Count;
            }

            var duplicateTexts = pages.Sum(page => page.Notes.Count - page.Notes.Select(note => note.Text).Distinct(StringComparer.Ordinal).Count());
            Console.WriteLine($"NOTES_WIRED={notesWired} of {freeTextNotes.Length} (identical-text duplicates within a page: {duplicateTexts}) ONLINE_WIRED={onlineWired} of {onlineEvidence.Length}");
            Assert(notesWired + duplicateTexts == freeTextNotes.Length && onlineWired == onlineEvidence.Length,
                "Every discovered Wiki note and netplay table must reach the Game Settings model through the sidecar consumer path.");
        }

        // Independent cross-check straight from the raw XML text of the file
        // that was passed in: every page's raw {{netplay}}/{{config}} template
        // count must equal what the parsers extracted for that page.
        var sourceText = File.ReadAllText(xmlPath);
        var parsedOnline = pages.ToDictionary(page => page.Page, page => page.Online.Count, StringComparer.Ordinal);
        var parsedConfig = analysis.Pages
            .Where(page => page.Namespace == 0)
            .GroupBy(page => page.Title, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(page => page.ConfigTemplateCount), StringComparer.Ordinal);
        var rawNetplayOccurrences = 0;
        var rawConfigOccurrences = 0;
        var netplayMismatches = new List<string>();
        var configMismatches = new List<string>();
        foreach (var chunk in sourceText.Split("<page>").Skip(1))
        {
            var titleMatch = System.Text.RegularExpressions.Regex.Match(chunk, @"<title>(?<t>.*?)</title>");
            var nsMatch = System.Text.RegularExpressions.Regex.Match(chunk, @"<ns>(?<n>-?\d+)</ns>");
            if (!titleMatch.Success || nsMatch.Groups["n"].Value != "0" || chunk.Contains("<redirect", StringComparison.Ordinal))
            {
                continue;
            }

            var title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups["t"].Value);
            var netplay = System.Text.RegularExpressions.Regex
                .Matches(chunk, @"\{\{\s*netplay\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            var config = System.Text.RegularExpressions.Regex
                .Matches(chunk, @"\{\{\s*config\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            rawNetplayOccurrences += netplay;
            rawConfigOccurrences += config;

            if (netplay != parsedOnline.GetValueOrDefault(title))
            {
                netplayMismatches.Add($"{title}: raw={netplay} parsed={parsedOnline.GetValueOrDefault(title)}");
            }

            if (config != parsedConfig.GetValueOrDefault(title))
            {
                configMismatches.Add($"{title}: raw={config} parsed={parsedConfig.GetValueOrDefault(title)}");
            }
        }

        var report = new
        {
            SOURCE_XML = Path.GetFullPath(xmlPath),
            SOURCE_SHA256 = analysis.SourceSha256,
            SOURCE_SNAPSHOT = analysis.SnapshotDate,
            TOTAL_PAGES = analysis.Stats.TotalPages,
            TOTAL_REVISIONS = analysis.Stats.TotalRevisions,
            TOTAL_GAME_PAGES = analysis.Stats.GameContentPages,
            PAGES_WITH_CONFIG = analysis.Stats.PagesWithConfigTemplate,
            RAW_CONFIG_TEMPLATE_OCCURRENCES = rawConfigOccurrences,
            RAW_NETPLAY_TEMPLATE_OCCURRENCES = rawNetplayOccurrences,
            CONFIG_ROWS_DISCOVERED = analysis.Stats.TotalConfigEntries,
            DISTINCT_WIKI_SETTING_NAMES = distinctSettingNames,
            SUPPORTED_CANONICAL = supportedCanonical,
            SUPPORTED_ALIAS = byWikiKey.Count(group => IsAlias(group.Key, byWikiKey)),
            OBSOLETE = 0,
            UNSUPPORTED = unsupported,
            AMBIGUOUS = ambiguous,
            UNKNOWN_SETTING = unknown,
            REJECTED = rejected,
            SILENTLY_DROPPED_CONFIG_ROWS = silentlyDropped,
            WIKI_KEYS_WITHOUT_EXPLICIT_SECTION = unmappedKeys,
            NON_SETTING_WIKI_ROWS = nonSettingRows,
            NOT_APPLIED_ROWS = analysis.Recommendations
                .Where(entry => entry.ParserStatus != WikiParserStatus.Mapped)
                .GroupBy(entry => $"{entry.WikiKey} | {entry.ParserStatus} | {entry.Detail}")
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Count()}x {group.Key} | e.g. raw='{group.First().RawWikiValue}' page='{group.First().Page}'")
                .ToArray(),
            NETPLAY_RAW_VS_PARSED_MISMATCHES = netplayMismatches,
            CONFIG_RAW_VS_PARSED_MISMATCHES = configMismatches,
            NOTES_APPLIED_TO_GAME_SETTINGS = notesWired,
            ONLINE_APPLIED_TO_GAME_SETTINGS = onlineWired,
            SUPPORTED_ROWS_TOTAL = totalSupportedRows,
            WIRED_ROWS_TOTAL = totalWiredRows,
            SUPPORTED_SETTINGS_TOTAL = totalSupportedSettings,
            WIRED_SETTINGS_TOTAL = totalWiredSettings,
            SECTION_UI_COVERAGE = rows.ToDictionary(
                pair => SectionName(pair.Key),
                pair => new
                {
                    DISCOVERED = pair.Value.Discovered,
                    SUPPORTED = pair.Value.Supported,
                    APPLIED_TO_GAME_SETTINGS = pair.Value.Wired,
                    DROPPED = pair.Value.Supported - pair.Value.Wired,
                    SUPPORTED_SETTINGS = pair.Value.SupportedSettings,
                    WIRED_SETTINGS = pair.Value.WiredSettings
                }),
            PAGES_WITH_INSTRUCTIONS = pages.Count(page => page.Notes.Count > 0),
            INSTRUCTIONS_DISCOVERED = freeTextNotes.Length,
            WARNINGS_DISCOVERED = freeTextNotes.Count(note => note.Class == WikiNoteClass.Warning),
            VERSION_SPECIFIC_NOTES = freeTextNotes.Count(note => note.Class == WikiNoteClass.VersionSpecificNote),
            PLATFORM_CONDITIONAL_NOTES = freeTextNotes.Count(note => note.Class == WikiNoteClass.PlatformConditionalNote),
            NETWORK_RPCN_NOTES = freeTextNotes.Count(note => note.Class is WikiNoteClass.NetworkNote or WikiNoteClass.RpcnNote or WikiNoteClass.Workaround),
            NETPLAY_TABLES_DISCOVERED = onlineEvidence.Length,
            ONLINE_SUPPORTED = onlineEvidence.Count(evidence => evidence.State == WikiOnlineState.Supported),
            ONLINE_PARTIAL = onlineEvidence.Count(evidence => evidence.State == WikiOnlineState.TestedPartial),
            ONLINE_NOT_SUPPORTED = onlineEvidence.Count(evidence => evidence.State == WikiOnlineState.NotSupported),
            ONLINE_UNKNOWN = analysis.Stats.GameContentPages - pages.Count(page => page.Online.Count > 0)
        };

        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "wiki-notes-audit.json");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(report, ReportJsonOptions));

        Console.WriteLine($"WIKI_NOTES_AUDIT report={reportPath}");
        Console.WriteLine($"SOURCE_XML={report.SOURCE_XML}");
        Console.WriteLine($"SOURCE_SHA256={report.SOURCE_SHA256} SNAPSHOT={report.SOURCE_SNAPSHOT}");
        Console.WriteLine(
            $"TOTAL_PAGES={report.TOTAL_PAGES} REVISIONS={report.TOTAL_REVISIONS} TOTAL_GAME_PAGES={report.TOTAL_GAME_PAGES} " +
            $"RAW_CONFIG_TEMPLATES={rawConfigOccurrences} RAW_NETPLAY_TEMPLATES={rawNetplayOccurrences}");
        Console.WriteLine(
            $"CONFIG_ROWS_DISCOVERED={report.CONFIG_ROWS_DISCOVERED} " +
            $"DISTINCT_SETTINGS={distinctSettingNames} SUPPORTED_CANONICAL={supportedCanonical} " +
            $"UNSUPPORTED={unsupported} AMBIGUOUS={ambiguous} UNKNOWN={unknown} " +
            $"SILENTLY_DROPPED_CONFIG_ROWS={silentlyDropped}");
        Console.WriteLine(
            $"INSTRUCTIONS_DISCOVERED={report.INSTRUCTIONS_DISCOVERED} WARNINGS={report.WARNINGS_DISCOVERED} " +
            $"VERSION_SPECIFIC={report.VERSION_SPECIFIC_NOTES} PLATFORM_CONDITIONAL={report.PLATFORM_CONDITIONAL_NOTES} " +
            $"NETPLAY_TABLES={report.NETPLAY_TABLES_DISCOVERED} ONLINE_SUPPORTED={report.ONLINE_SUPPORTED} " +
            $"ONLINE_PARTIAL={report.ONLINE_PARTIAL} ONLINE_UNKNOWN={report.ONLINE_UNKNOWN}");
        Console.WriteLine($"WIKI_KEYS_WITHOUT_EXPLICIT_SECTION={unmappedKeys.Length} (rows={nonSettingRows}, mapped={mappedWithoutExplicitSection}): {string.Join(", ", unmappedKeys)}");
        Console.WriteLine($"NETPLAY_RAW_VS_PARSED_MISMATCHES={netplayMismatches.Count} {string.Join("; ", netplayMismatches.Take(10))}");
        Console.WriteLine($"CONFIG_RAW_VS_PARSED_MISMATCHES={configMismatches.Count} {string.Join("; ", configMismatches.Take(10))}");
        foreach (var section in Enum.GetValues<Rpcs3SettingsSection>())
        {
            var row = rows[section];
            Console.WriteLine(
                $"  SECTION {SectionName(section)}: DISCOVERED={row.Discovered} SUPPORTED={row.Supported} " +
                $"APPLIED_TO_GAME_SETTINGS={row.Wired} DROPPED={row.Supported - row.Wired} " +
                $"(settings: supported={row.SupportedSettings} wired={row.WiredSettings})");
        }

        Assert(silentlyDropped == 0,
            "SILENTLY_DROPPED_CONFIG_ROWS must be zero: every config row must land in a mapped/unsupported/ambiguous/unknown/rejected bucket.");
        Assert(mappedWithoutExplicitSection == 0,
            "A supported Wiki setting has no explicit RPCS3 section; add it to SettingsSectionMap instead of relying on the YAML-section fallback.");
        Assert(totalSupportedSettings == totalWiredSettings &&
               rows.Values.All(row =>
                   row.Supported == row.Wired &&
                   row.SupportedSettings == row.WiredSettings),
            "Every supported Wiki setting must reach its Game Settings section (DROPPED = 0 per section).");
        Assert(netplayMismatches.Count == 0 && configMismatches.Count == 0,
            "Every raw {{netplay}}/{{config}} template on every Wiki page must be parsed.");
    }

    // Generates the shipped Wiki data from ONE source XML: the Game Settings
    // catalog (cat.json) and the notes/online sidecar (wiki-notes.json). Both
    // are written to <outDir> only; the caller compares and then copies them
    // into App/Data. Invoked as
    // `R3Test.exe --gen-production-data <xml> <outDir>`.
    private static void WikiProductionData(string xmlPath, string outDir)
    {
        var fullPath = Path.GetFullPath(xmlPath);
        var catalog = R3Config.Application.RecommendationCatalogUseCase.Rebuild(fullPath);
        var analysis = WikiSvc.Analyze(fullPath);
        var pages = WikiNotesCorpusReader.ReadAndParse(fullPath);

        Directory.CreateDirectory(outDir);
        R3Config.Application.RecommendationCatalogUseCase.Export(catalog, Path.Combine(outDir, "cat.json"));
        WikiEvidenceCatalog
            .FromPages("RPCS3 Wiki snapshot", analysis.SnapshotDate, analysis.SourceSha256, pages, Path.GetFileName(fullPath))
            .Save(Path.Combine(outDir, WikiEvidenceCatalog.FileName));

        Console.WriteLine($"PRODUCTION_WIKI_SOURCE={fullPath}");
        Console.WriteLine($"PRODUCTION_WIKI_SHA256={analysis.SourceSha256} SNAPSHOT={analysis.SnapshotDate}");
        Console.WriteLine($"CATALOG_RECOMMENDATIONS={catalog.Count} outDir={outDir}");
    }

    private static string SectionName(Rpcs3SettingsSection section) => section switch
    {
        Rpcs3SettingsSection.Cpu => "CPU",
        Rpcs3SettingsSection.Gpu => "GPU",
        Rpcs3SettingsSection.Io => "I/O",
        Rpcs3SettingsSection.Gui => "GUI",
        _ => section.ToString()
    };

    // A WikiKey is an alias when its mapped (Section, Key) target is also
    // reachable through a different WikiKey name in the same corpus (e.g.
    // "xfloat accuracy" is an alias of "spu xfloat accuracy").
    private static bool IsAlias(
        string wikiKey,
        IReadOnlyCollection<IGrouping<string, WikiCorpusEntry>> byWikiKey)
    {
        var targets = byWikiKey
            .First(group => group.Key.Equals(wikiKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => entry.MappedSettings)
            .Select(setting => $"{setting.Section}\0{setting.Key}")
            .ToHashSet(StringComparer.Ordinal);

        if (targets.Count == 0)
        {
            return false;
        }

        return byWikiKey.Any(other =>
            !other.Key.Equals(wikiKey, StringComparison.OrdinalIgnoreCase) &&
            other.SelectMany(entry => entry.MappedSettings)
                .Any(setting => targets.Contains($"{setting.Section}\0{setting.Key}")));
    }

    private sealed class SectionRow
    {
        public int Discovered { get; set; }
        public int Supported { get; set; }
        public int Wired { get; set; }
        public int SupportedSettings { get; set; }
        public int WiredSettings { get; set; }
    }
}
