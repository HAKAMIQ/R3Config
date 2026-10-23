using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.NclAudit;

// Full-coverage cross-reference: every Serial+Version identity declared by any
// of the three Artemis source trees, checked end to end through the shipped
// Audit/LookupZip pipeline against the production corpus. Read only; never
// writes into a source directory or App/Data.
internal static class FullScan
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] NegativeProbeSerials = ["BLUS99998", "BLES99998", "NPUB99998", "BCUS99998"];

    public static int Run(string[] args)
    {
        if (args.Length != 7)
        {
            Console.Error.WriteLine(
                "Usage: R3NclAudit --full-scan <artemisSiteDownloadRoot> <artemisPs3Dir1> <artemisPs3Dir2> <productionNclZip> <productionSha256> <outputDir>");
            return 2;
        }

        try
        {
            Execute(args[1], args[2], args[3], args[4], args[5], args[6]);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidDataException)
        {
            Console.Error.WriteLine($"FULL_SCAN=FAIL {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private sealed record DownloadRow(int RowOrder, string TitleID, string Title, string Version,
        string ArtemisFileName, string Status, long Bytes, string SHA256, string? LocalFile);

    private sealed record Identity(string Serial, string Version)
    {
        public override string ToString() => Serial + "|" + Version;
    }

    private sealed record FileBinding(
        string SourceLabel, string RelativePath, string? Title,
        NclCorpusFileReport Report);

    private sealed record IdentityAggregate(
        Identity Id,
        List<FileBinding> Bindings,
        HashSet<string> DefinitionKeys,
        Dictionary<string, HashSet<string>> ContentByName);

    private sealed record IdentityRow(
        string Serial, string Version, string GameTitle,
        int SourceFileCount, int SourceDefinitions,
        bool CorpusFound, int CorpusDefinitions,
        bool LookupOk, int LookupDefinitions,
        string Classification, string Detail,
        int SameSerialOtherVersionCount);

    private sealed record SameSerialOtherVersionRow(
        string Serial, string SourceVersion, string CurrentVersion,
        int DefinitionCount, string CompatibilityStatus, string EvidenceKind);

    private static void Execute(
        string siteDownloadRoot, string artemisDir1, string artemisDir2,
        string productionZip, string productionSha256, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        Console.WriteLine("Loading source 1 (site download, manifest-driven)...");
        var src1 = LoadSiteDownloadSource(siteDownloadRoot, "AR_SITE_CODELIST_2026");
        Console.WriteLine($"  files={src1.Count}");

        Console.WriteLine("Loading source 2 (ArtemisPS3-master (1) docs/codes)...");
        var src2 = LoadPlainDirectorySource(artemisDir1, "ARTEMISPS3_1_DOCS_CODES");
        Console.WriteLine($"  files={src2.Count}");

        Console.WriteLine("Loading source 3 (ArtemisPS3-master (2) USERLIST)...");
        var src3 = LoadPlainDirectorySource(artemisDir2, "ARTEMISPS3_2_USERLIST");
        Console.WriteLine($"  files={src3.Count}");

        var allSourceFiles = src1.Concat(src2).Concat(src3).ToList();

        var titleOnlyUnresolved = allSourceFiles.Count(f => !f.Report.HasExactIdentity);

        // Bind every (Serial,Version) target every file declares.
        var byIdentity = new Dictionary<Identity, IdentityAggregate>();
        foreach (var file in allSourceFiles)
        {
            foreach (var target in file.Report.Identity.Targets)
            {
                var id = new Identity(target.Serial.ToUpperInvariant(), PatVer.Norm(target.Version)!);
                if (!byIdentity.TryGetValue(id, out var agg))
                {
                    agg = new IdentityAggregate(id, [], [], new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
                    byIdentity[id] = agg;
                }

                agg.Bindings.Add(new FileBinding(file.SourceLabel, file.RelativePath, file.Title, file.Report));
                foreach (var cheat in file.Report.Cheats)
                {
                    var key = cheat.Name.ToUpperInvariant() + "|" + cheat.RawSha256;
                    agg.DefinitionKeys.Add(key);
                    if (!agg.ContentByName.TryGetValue(cheat.Name, out var hashes))
                    {
                        hashes = new HashSet<string>(StringComparer.Ordinal);
                        agg.ContentByName[cheat.Name] = hashes;
                    }
                    hashes.Add(cheat.RawSha256);
                }
            }
        }

        Console.WriteLine($"Distinct Serial+Version identities across all 3 sources: {byIdentity.Count}");

        // Cross-source pairwise file comparison for provenance classification.
        var crossSource = CompareCrossSource(src1, src2, src3);

        // Audit the production corpus once.
        Console.WriteLine("Auditing production corpus...");
        var corpusReport = NclCorpusReader.Audit(productionZip);
        var corpusByIdentity = new Dictionary<Identity, (List<string> Files, HashSet<string> DefinitionKeys)>();
        foreach (var file in corpusReport.Files)
        {
            foreach (var target in file.Identity.Targets)
            {
                var id = new Identity(target.Serial.ToUpperInvariant(), PatVer.Norm(target.Version)!);
                if (!corpusByIdentity.TryGetValue(id, out var entry))
                {
                    entry = ([], []);
                    corpusByIdentity[id] = entry;
                }
                entry.Files.Add(file.RelativePath);
                foreach (var cheat in file.Cheats)
                {
                    entry.DefinitionKeys.Add(cheat.Name.ToUpperInvariant() + "|" + cheat.RawSha256);
                }
            }
        }

        Console.WriteLine($"Distinct Serial+Version identities in production corpus: {corpusByIdentity.Count}");
        Console.WriteLine("Running LookupZip for every scanned identity (this is the slow step)...");

        var rows = new List<IdentityRow>();
        var sameSerialOtherVersionRows = new List<SameSerialOtherVersionRow>();
        var differentSerialLeaks = new List<string>();
        var counts = new Dictionary<string, int>
        {
            ["EXACT_MATCH_OK"] = 0,
            ["NO_CHEAT_SOURCE"] = 0,
            ["SOURCE_PRESENT_CORPUS_MISSING"] = 0,
            ["CORPUS_PRESENT_LOOKUP_MISSING"] = 0,
            ["SOURCE_DEFINITION_LOSS"] = 0,
            ["DUPLICATE_CONFLICT"] = 0,
        };
        var exactSerialVersionMatch = 0;
        var sameSerialOtherVersionIdentities = 0;

        var processed = 0;
        foreach (var (id, agg) in byIdentity.OrderBy(p => p.Key.Serial, StringComparer.Ordinal).ThenBy(p => p.Key.Version, StringComparer.Ordinal))
        {
            processed++;
            if (processed % 200 == 0)
            {
                Console.WriteLine($"  ...{processed}/{byIdentity.Count}");
            }

            var conflict = agg.ContentByName.Any(pair => pair.Value.Count > 1);
            var sourceDefCount = agg.DefinitionKeys.Count;
            var title = agg.Bindings.Select(b => b.Title).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";

            var corpusFound = corpusByIdentity.TryGetValue(id, out var corpusEntry);
            var corpusDefCount = corpusFound ? corpusEntry.DefinitionKeys.Count : 0;

            var lookup = NclCorpusReader.LookupZip(productionZip, id.Serial, id.Version, productionSha256, NclDialect.ArtemisPs3, null);
            var lookupOk = lookup.IsOk;
            var lookupDefCount = lookup.Cheats.Count;
            if (lookupOk)
            {
                exactSerialVersionMatch++;
            }

            var otherVersionCheats = lookup.OtherVersionCheats;
            if (otherVersionCheats.Count > 0)
            {
                sameSerialOtherVersionIdentities++;
                foreach (var group in otherVersionCheats.GroupBy(c => c.Version, StringComparer.OrdinalIgnoreCase))
                {
                    sameSerialOtherVersionRows.Add(new SameSerialOtherVersionRow(
                        id.Serial, group.Key, id.Version, group.Count(),
                        "UnverifiedVersion", "None"));
                }

                var leaks = otherVersionCheats.Where(c => !c.Serial.Equals(id.Serial, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (leaks.Length > 0)
                {
                    differentSerialLeaks.Add($"{id}: {string.Join(", ", leaks.Select(c => c.Serial))}");
                }
            }

            string classification;
            string detail;
            if (conflict)
            {
                classification = "DUPLICATE_CONFLICT";
                detail = "Same cheat name has different raw content across sources bound to this identity.";
            }
            else if (!corpusFound)
            {
                classification = "SOURCE_PRESENT_CORPUS_MISSING";
                detail = $"Source has {sourceDefCount} definitions; identity absent from production corpus entirely.";
            }
            else if (corpusDefCount < sourceDefCount)
            {
                classification = "SOURCE_DEFINITION_LOSS";
                detail = $"Source={sourceDefCount}, Corpus={corpusDefCount}: builder lost definitions.";
            }
            else if (!lookupOk || lookupDefCount < corpusDefCount)
            {
                classification = "CORPUS_PRESENT_LOOKUP_MISSING";
                detail = $"Corpus={corpusDefCount}, LookupOk={lookupOk}, Lookup={lookupDefCount}: runtime lookup lost or rejected definitions. Code={lookup.Code}";
            }
            else
            {
                classification = "EXACT_MATCH_OK";
                detail = "";
            }

            counts[classification] = counts.GetValueOrDefault(classification) + 1;
            rows.Add(new IdentityRow(id.Serial, id.Version, title, agg.Bindings.Count, sourceDefCount,
                corpusFound, corpusDefCount, lookupOk, lookupDefCount, classification, detail,
                otherVersionCheats.Count));
        }

        // Multi-serial / multi-version index bug detection: for a single file
        // that declares several targets, every target must return the same
        // LookupDefinitions count (it is the same cheat set).
        var multiSerialBugs = new List<string>();
        var multiVersionBugs = new List<string>();
        var lookupByIdentity = rows.ToDictionary(r => new Identity(r.Serial, r.Version), r => r.LookupDefinitions);
        foreach (var file in allSourceFiles)
        {
            var targets = file.Report.Identity.Targets;
            if (targets.Count < 2)
            {
                continue;
            }

            var counts2 = targets
                .Select(t => new Identity(t.Serial.ToUpperInvariant(), PatVer.Norm(t.Version)!))
                .Distinct()
                .Select(id => (Id: id, Lookup: lookupByIdentity.GetValueOrDefault(id, -1)))
                .ToArray();

            if (counts2.Select(c => c.Lookup).Distinct().Count() > 1)
            {
                var bySerial = counts2.Select(c => c.Id.Serial).Distinct().Count();
                var byVersion = counts2.Select(c => c.Id.Version).Distinct().Count();
                var line = $"{file.SourceLabel}/{file.RelativePath}: " + string.Join(", ", counts2.Select(c => $"{c.Id}={c.Lookup}"));
                if (bySerial > 1)
                {
                    multiSerialBugs.Add(line);
                }
                if (byVersion > 1)
                {
                    multiVersionBugs.Add(line);
                }
            }
        }

        // Negative controls: serials no source declares must return nothing in
        // either tier - never a fuzzy/nearest match, never leaked content.
        var knownSerials = byIdentity.Keys.Select(id => id.Serial).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var probeSerials = NegativeProbeSerials
            .Where(s => !knownSerials.Contains(s)).ToArray();
        var noSerialSourceViolations = 0;
        foreach (var probe in probeSerials)
        {
            var probeLookup = NclCorpusReader.LookupZip(productionZip, probe, "01.00", productionSha256, NclDialect.ArtemisPs3, null);
            if (probeLookup.Cheats.Count > 0 || probeLookup.OtherVersionCheats.Count > 0)
            {
                noSerialSourceViolations++;
            }
        }

        var totalSourceDefinitions = byIdentity.Values.SelectMany(a => a.DefinitionKeys).Distinct(StringComparer.Ordinal).Count();
        var totalArtemisFiles = allSourceFiles.Count;

        var silentlyDropped = counts.GetValueOrDefault("SOURCE_PRESENT_CORPUS_MISSING") > 0 ||
            counts.GetValueOrDefault("SOURCE_DEFINITION_LOSS") > 0 ||
            counts.GetValueOrDefault("CORPUS_PRESENT_LOOKUP_MISSING") > 0
                ? rows.Where(r => r.Classification is "SOURCE_PRESENT_CORPUS_MISSING" or "SOURCE_DEFINITION_LOSS")
                    .Sum(r => r.SourceDefinitions - r.CorpusDefinitions) +
                  rows.Where(r => r.Classification == "CORPUS_PRESENT_LOOKUP_MISSING")
                    .Sum(r => r.CorpusDefinitions - r.LookupDefinitions)
                : 0;

        var summary = new
        {
            TOTAL_ARTEMIS_SOURCE_FILES = totalArtemisFiles,
            TOTAL_SERIAL_VERSION_PAIRS = byIdentity.Count,
            TOTAL_SOURCE_DEFINITIONS = totalSourceDefinitions,
            SRC1_AR_SITE_FILES = src1.Count,
            SRC2_ARTEMISPS3_1_FILES = src2.Count,
            SRC3_ARTEMISPS3_2_FILES = src3.Count,
            EXACT_MATCH_OK = counts.GetValueOrDefault("EXACT_MATCH_OK"),
            SOURCE_PRESENT_CORPUS_MISSING = counts.GetValueOrDefault("SOURCE_PRESENT_CORPUS_MISSING"),
            CORPUS_PRESENT_LOOKUP_MISSING = counts.GetValueOrDefault("CORPUS_PRESENT_LOOKUP_MISSING"),
            SOURCE_DEFINITION_LOSS = counts.GetValueOrDefault("SOURCE_DEFINITION_LOSS"),
            DUPLICATE_CONFLICT = counts.GetValueOrDefault("DUPLICATE_CONFLICT"),
            MULTI_SERIAL_INDEX_BUG = multiSerialBugs.Count,
            MULTI_VERSION_INDEX_BUG = multiVersionBugs.Count,
            TITLE_ONLY_UNRESOLVED_FILES = titleOnlyUnresolved,
            SILENTLY_DROPPED_DEFINITIONS = silentlyDropped,
            EXACT_SERIAL_VERSION_MATCH = exactSerialVersionMatch,
            SAME_SERIAL_OTHER_VERSION = sameSerialOtherVersionIdentities,
            NO_SERIAL_SOURCE = probeSerials.Length,
            NO_SERIAL_SOURCE_VIOLATIONS = noSerialSourceViolations,
            DIFFERENT_SERIAL_EXCLUDED_VIOLATIONS = differentSerialLeaks.Count,
            CrossSourceComparison = crossSource,
        };

        File.WriteAllText(Path.Combine(outputDir, "full-scan-same-serial-other-version.json"),
            JsonSerializer.Serialize(sameSerialOtherVersionRows, JsonOptions), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, "full-scan-different-serial-leaks.txt"),
            string.Join(Environment.NewLine, differentSerialLeaks));

        File.WriteAllText(Path.Combine(outputDir, "full-scan-summary.json"), JsonSerializer.Serialize(summary, JsonOptions), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, "full-scan-rows.json"), JsonSerializer.Serialize(rows, JsonOptions), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, "full-scan-multi-serial-bugs.txt"), string.Join(Environment.NewLine, multiSerialBugs));
        File.WriteAllText(Path.Combine(outputDir, "full-scan-multi-version-bugs.txt"), string.Join(Environment.NewLine, multiVersionBugs));
        File.WriteAllText(Path.Combine(outputDir, "full-scan-non-ok-rows.tsv"),
            "Serial\tVersion\tTitle\tSourceDefs\tCorpusFound\tCorpusDefs\tLookupOk\tLookupDefs\tClassification\tDetail\n" +
            string.Join("\n", rows.Where(r => r.Classification != "EXACT_MATCH_OK")
                .Select(r => $"{r.Serial}\t{r.Version}\t{r.GameTitle}\t{r.SourceDefinitions}\t{r.CorpusFound}\t{r.CorpusDefinitions}\t{r.LookupOk}\t{r.LookupDefinitions}\t{r.Classification}\t{r.Detail}")));

        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
    }

    private static object CompareCrossSource(List<FileBinding> src1, List<FileBinding> src2, List<FileBinding> src3)
    {
        // Identity-level presence per source (ignores exact byte equality; that
        // is diagnostic-only since raw bytes routinely differ by CRLF/BOM).
        var idsOf = (List<FileBinding> src) => src
            .SelectMany(f => f.Report.Identity.Targets)
            .Select(t => new Identity(t.Serial.ToUpperInvariant(), PatVer.Norm(t.Version)!))
            .ToHashSet();

        var s1 = idsOf(src1);
        var s2 = idsOf(src2);
        var s3 = idsOf(src3);

        return new
        {
            SOURCE_ONLY_1 = s1.Except(s2).Except(s3).Count(),
            SOURCE_ONLY_2 = s2.Except(s1).Except(s3).Count(),
            SOURCE_ONLY_3 = s3.Except(s1).Except(s2).Count(),
            IN_ALL_THREE = s1.Intersect(s2).Intersect(s3).Count(),
            Note = "Identity-level (Serial+Version) presence per raw source tree. Byte/content-level duplicate classification is folded into DUPLICATE_CONFLICT in the main summary.",
        };
    }

    private static List<FileBinding> LoadPlainDirectorySource(string dir, string label)
    {
        var report = NclCorpusReader.Audit(dir);
        return report.Files.Select(f => new FileBinding(label, f.RelativePath, f.Identity.Title, f)).ToList();
    }

    private static List<FileBinding> LoadSiteDownloadSource(string root, string label)
    {
        var manifestBytes = File.ReadAllBytes(Path.Combine(root, "NclDownloadManifest.json"));
        var rows = JsonSerializer.Deserialize<DownloadRow[]>(manifestBytes, JsonOptions)
            ?? throw new InvalidDataException("Download manifest is empty.");
        var items = Path.Combine(root, "Items");
        var raw = NclCorpusReader.Audit(items);
        var rawByPath = raw.Files.ToDictionary(f => Path.GetFullPath(Path.Combine(items, f.RelativePath)), StringComparer.OrdinalIgnoreCase);

        var result = new List<FileBinding>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.LocalFile))
            {
                continue;
            }
            var local = Path.GetFullPath(Path.IsPathFullyQualified(row.LocalFile) ? row.LocalFile : Path.Combine(root, row.LocalFile));
            if (!rawByPath.TryGetValue(local, out var rawReport))
            {
                continue;
            }

            var filename = LogicalFilename(row.ArtemisFileName);
            var identity = NclParser.ParseIdentity(filename);
            // Re-project the raw parsed report (which has no filename identity,
            // since every physical entry is named source.ncl) onto the manifest
            // row's declared identity - the exact same substitution the shipped
            // reader performs at Lookup time via NclCorpusManifest.
            var projected = rawReport with { Identity = identity };
            result.Add(new FileBinding(label, "Items/" + row.RowOrder.ToString(CultureInfo.InvariantCulture) + "/source.ncl", identity.Title ?? row.Title, projected));
        }

        return result;
    }

    private static string LogicalFilename(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['/', '\\', '\0', ':']) >= 0 || value is "." or "..")
        {
            throw new InvalidDataException("Unsafe logical NCL filename.");
        }
        return value.EndsWith(".ncl", StringComparison.OrdinalIgnoreCase) ? value : value + ".ncl";
    }
}
