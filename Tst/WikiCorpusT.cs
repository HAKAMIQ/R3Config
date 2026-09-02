using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

internal static class WikiCorpusT
{
    private static readonly HashSet<string> ExpectedZipHashes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CF42617F57C76BB17DCAB8859141ACBF5C446A0A7D1B7114C60E3C596AA0BC61",
            "C8DF0F2998310104DD3B30ED115D7A41CE1BDA7C1B594B2C4B5DA684BD1E36A1"
        };
    private const string ExpectedXmlHash =
        "254A0A41870C54048C94F61B2FF5AE9CEEFE7CC55F246C331DF9382B564BE452";
    private const string EntryName = "RPCS3+Wiki-20260613075133.xml";
    private const long MaxEntryBytes = 128L * 1024 * 1024;

    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Fixture(string tempRoot)
    {
        var path = Path.Combine(tempRoot, "wiki-corpus-fixture.xml");
        File.WriteAllText(
            path,
            """
            <mediawiki>
              <page><title>Fixture Game</title><ns>0</ns><revision><id>10</id><timestamp>2026-06-12T00:00:00Z</timestamp><text><![CDATA[
            {{gameid|id=BLUS30000, BLES30000|ird=Yes}}
            {{config
            |zcull accuracy = Relaxed
            |zcull accuracy notes = Required to fix rendering.
            |vblank rate = 120
            |vblank rate notes = Use only with the 60 FPS patch.
            |write color buffers = On
            |write color buffers notes = No longer required since build 12345.
            |read color buffers = On
            |read color buffers notes = Required for game versions above 1.05.
            |graphics device = NA
            |mystery option = On
            |renderer = Metal
            |orphan notes = Informational prose only.
            }}
            ]]></text></revision></page>
              <page><title>Conflicting Game</title><ns>0</ns><revision><id>11</id><timestamp>2026-06-12T00:00:00Z</timestamp><text><![CDATA[
            {{gameid|id=BLUS30000|ird=Yes}}
            {{config|write color buffers=Off}}
            ]]></text></revision></page>
              <page><title>Alias</title><ns>0</ns><redirect title="Fixture Game"/><revision><id>12</id><timestamp>2026-06-12T00:00:00Z</timestamp><text>#REDIRECT [[Fixture Game]]</text></revision></page>
              <page><title>Template:Dependency</title><ns>10</ns><revision><id>13</id><timestamp>2026-06-12T00:00:00Z</timestamp><text>dependency</text></revision></page>
            </mediawiki>
            """,
            new UTF8Encoding(false));

        var analysis = WikiSvc.Analyze(path);

        Assert(analysis.Stats.TotalPages == 4 &&
               analysis.Stats.ParsedPages == 4 &&
               analysis.Stats.GameContentPages == 2 &&
               analysis.Stats.RedirectPages == 1 &&
               analysis.Stats.NonGamePages == 1,
            "Expected the corpus analyzer to account for content, redirect, and dependency pages.");
        Assert(analysis.Stats.TotalConfigEntries == 8 && analysis.Stats.TotalNotes == 5,
            "Expected every non-empty config entry and note in the fixture to be retained.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "zcull accuracy").MappedSettings.Count == 2,
            "Expected one logical ZCULL recommendation to retain both RPCS3 target settings.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "vblank rate").Classification ==
               WikiRecommendationClass.PatchSpecific,
            "Expected patch-dependent VBlank advice to be separated from general game config.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "write color buffers" &&
                                                entry.Page == "Fixture Game").Classification ==
               WikiRecommendationClass.VersionSpecific,
            "Expected time-bounded advice to be classified as version-specific.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "read color buffers" &&
                                                entry.Page == "Fixture Game").Classification ==
               WikiRecommendationClass.VersionSpecific,
            "Expected game-version-bounded advice to be classified as version-specific.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "graphics device").ParserStatus ==
               WikiParserStatus.Unsupported,
            "Expected an active but unmapped Template:Config key to be explicit unsupported evidence.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "mystery option").ParserStatus ==
               WikiParserStatus.UnknownWikiKey &&
               analysis.Findings.Any(finding => finding.Code == WikiFindingCode.UnknownWikiKey),
            "Expected unknown Wiki keys to be reported rather than silently skipped.");
        Assert(analysis.Recommendations.Single(entry => entry.WikiKey == "renderer").ParserStatus ==
               WikiParserStatus.Rejected &&
               analysis.Findings.Any(finding => finding.Code == WikiFindingCode.InvalidWikiValue),
            "Expected invalid values for recognized Wiki keys to fail closed.");
        Assert(analysis.Notes.Single(note => note.WikiKey == "orphan").AssociatedWithSetting is false &&
               analysis.Findings.Any(finding => finding.Code == WikiFindingCode.OrphanNote),
            "Expected note-only prose to remain informational and not become a config setting.");
        Assert(analysis.Stats.ConflictingSerials == 1 &&
               analysis.CatalogRecommendations.Count == 1 &&
               analysis.CatalogRecommendations.All(item => item.Serial != "BLUS30000"),
            "Expected a conflicting duplicate serial to fail closed while preserving the page's independent region serial.");
    }

    public static WikiParityRun RunCorpus(
        string zipPath,
        string oldCatalogPath,
        string candidatePath,
        string? reportPath)
    {
        Assert(File.Exists(zipPath), $"Wiki snapshot ZIP does not exist: {zipPath}");
        Assert(File.Exists(oldCatalogPath), $"Old recommendation catalog does not exist: {oldCatalogPath}");

        var zipHash = HashFile(zipPath);
        Assert(ExpectedZipHashes.Contains(zipHash),
            $"Wiki snapshot ZIP hash mismatch. Actual {zipHash} is not an approved packaging identity.");

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigWikiCorpus",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var xmlPath = Path.Combine(tempRoot, EntryName);
            ExtractBounded(zipPath, xmlPath);
            var xmlHash = HashFile(xmlPath);
            Assert(xmlHash == ExpectedXmlHash,
                $"Wiki snapshot XML hash mismatch. Expected {ExpectedXmlHash}, actual {xmlHash}.");
            var analysis = WikiSvc.Analyze(xmlPath);
            var oldCatalog = RecSvc.Load(oldCatalogPath);
            var comparison = WikiSvc.Compare(oldCatalog, analysis);
            var generatedAt = new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);
            var firstPath = Path.Combine(tempRoot, "candidate-1.json");
            var secondPath = Path.Combine(tempRoot, "candidate-2.json");
            var generated = RecSvc.FromRec(
                "RPCS3 Wiki snapshot",
                "2026-06-13",
                analysis.CatalogRecommendations,
                generatedAt,
                analysis.SourceSha256);
            generated.Save(firstPath);
            generated.Save(secondPath);

            var rebuiltPath = Path.Combine(tempRoot, "candidate-app-rebuild.json");
            var rebuilt = RecommendationCatalogUseCase.Rebuild(xmlPath);
            rebuilt.Save(rebuiltPath);

            var firstBytes = File.ReadAllBytes(firstPath);
            var secondBytes = File.ReadAllBytes(secondPath);
            var deterministic = firstBytes.AsSpan().SequenceEqual(secondBytes);
            Assert(deterministic, "Generated catalog is not byte-for-byte deterministic.");
            Assert(firstBytes.AsSpan().SequenceEqual(File.ReadAllBytes(rebuiltPath)),
                "Application catalog rebuild differs from the deterministic safe corpus generator.");

            var loaded = RecSvc.Load(firstPath);
            Assert(loaded.Count == analysis.CatalogRecommendations.Count,
                "Generated catalog round-trip changed the serial count.");
            Assert(analysis.CatalogRecommendations.All(item => loaded.Find(item.Serial) is not null),
                "At least one generated serial is not searchable after RecSvc round-trip.");
            var yamlPath = Path.Combine(tempRoot, "catalog-roundtrip.yml");
            foreach (var change in loaded.Rec
                         .SelectMany(item => item.Changes)
                         .DistinctBy(
                             change => $"{change.Section}\0{change.Key}\0{change.Value}",
                             StringComparer.Ordinal))
            {
                R3Yaml.Apply(yamlPath, [change]);
                Assert(R3Yaml.ReadVal(yamlPath, change.Section, change.Key) == change.Value,
                    $"Generated setting failed YAML round-trip: {change.Path} = {change.Value}.");
            }

            Assert(R3Yaml.CanRead(yamlPath),
                "Generated recommendation settings did not produce valid readable YAML.");
            Assert(analysis.Stats.TotalPages == 2315 &&
                   analysis.Stats.ParsedPages == 2315 &&
                   analysis.Stats.ParseFailures == 0,
                "Expected every page in the pinned snapshot to parse successfully.");
            Assert(analysis.Stats.GameContentPages == 2254 &&
                   analysis.Stats.PagesWithGameIdTemplate == 2255 &&
                   analysis.Stats.PagesWithSerials == 2248 &&
                   analysis.Stats.PagesWithConfigTemplate == 2254 &&
                   analysis.Stats.PagesWithRecommendations == 526 &&
                   analysis.Stats.PagesWithoutRecommendations == 1728,
                "Expected the pinned snapshot's stable game-content/config page counts.");
            Assert(analysis.Stats.SerialOccurrences == 8540 &&
                   analysis.Stats.UniqueSerials == 8535 &&
                   analysis.Stats.RegionVariants == 6292 &&
                   analysis.Stats.TotalConfigEntries == 1142 &&
                   analysis.Stats.TotalNotes == 1149,
                "Expected the pinned snapshot's stable serial, recommendation, and note counts.");
            Assert(analysis.Stats.MappedRecommendations == 1045 &&
                   analysis.Stats.UnsupportedRecommendations == 38 &&
                   analysis.Stats.AmbiguousRecommendations == 3 &&
                   analysis.Stats.RejectedRecommendations == 0 &&
                   analysis.Stats.VersionSpecificRecommendations == 5 &&
                   analysis.Stats.PatchSpecificRecommendations == 77 &&
                   analysis.Stats.UnknownRecommendations == 0,
                "Expected the pinned snapshot's stable parser classification counts.");
            Assert(analysis.Stats.DuplicateSerials == 5 &&
                   analysis.Stats.ConflictingSerials == 5 &&
                   analysis.Stats.ConflictingSettings == 0 &&
                   analysis.CatalogRecommendations.Count == 8530 &&
                   analysis.CatalogRecommendations.Sum(item => item.Changes.Count) == 7450,
                "Expected every known snapshot conflict to remain explicit and excluded from safe generated output.");
            Assert(analysis.Findings.Count(finding => finding.Code == WikiFindingCode.MalformedGameId) == 3 &&
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.EmptyGameId) == 6 &&
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.OrphanNote) == 36 &&
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.UnsupportedWikiKey) == 38 &&
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.AmbiguousWikiValue) == 3 &&
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.DuplicateSerialConflict) == 5,
                "Expected every pinned malformed, empty, unsupported, ambiguous, and conflicting case to remain explicit.");
            Assert(analysis.Pages.All(page =>
                       !string.IsNullOrWhiteSpace(page.RevisionId) &&
                       page.RevisionTimestamp is not null) &&
                   analysis.Pages.Count(page => page.PatchReferences.Count > 0) == 57 &&
                   analysis.Pages.Sum(page => page.PatchReferences.Count) == 84,
                "Expected every page revision identity and every structured patch reference to be retained.");
            Assert(analysis.Stats.UnknownRecommendations ==
                   analysis.Findings.Count(finding => finding.Code == WikiFindingCode.UnknownWikiKey),
                "Unknown Wiki key entries and findings must have a one-to-one count.");
            Assert(analysis.Recommendations.All(entry =>
                    entry.ParserStatus == WikiParserStatus.Mapped ||
                    !string.IsNullOrWhiteSpace(entry.Detail)),
                "Every non-mapped recommendation must carry an explicit exclusion detail.");
            Assert(analysis.Recommendations
                    .Where(entry => entry.WikiKey == "zcull accuracy" &&
                                    entry.ParserStatus == WikiParserStatus.Mapped)
                    .All(entry => entry.MappedSettings.Count == 2),
                "Every mapped ZCULL recommendation must remain one logical entry with two targets.");

            var ridgeRacer = loaded.Find("BCAS20001")
                ?? throw new InvalidOperationException("Known Ridge Racer 7 serial is missing.");
            Assert(ridgeRacer.Changes.Any(change =>
                       change.Key == "PPU Decoder" && change.Value == "Interpreter (static)") &&
                   ridgeRacer.Changes.Any(change =>
                       change.Key == "Minimum Scalable Dimension" && change.Value == "256") &&
                   ridgeRacer.Changes.Any(change =>
                       change.Key == "Write Color Buffers" && change.Value == "true"),
                "Expected the known Ridge Racer 7 configuration tuple from the snapshot.");
            var deadNation = loaded.Find("BCET70025")
                ?? throw new InvalidOperationException("Known Dead Nation serial is missing.");
            Assert(deadNation.Changes.Any(change =>
                       change.WikiKey == "xfloat accuracy" &&
                       change.Key == "SPU XFloat Accuracy" &&
                       change.Value == "Accurate"),
                "Expected the snapshot's legacy XFloat alias to map to the verified SPU XFloat setting.");
            var killzone2 = loaded.Find("BCAS20066")
                ?? throw new InvalidOperationException("Known Killzone 2 serial is missing.");
            Assert(killzone2.Changes.Count(change => change.WikiKey == "zcull accuracy") == 2,
                "Expected the known Killzone 2 ZCULL recommendation to retain both physical targets.");
            var resistance = loaded.Find("BCAS20003")
                ?? throw new InvalidOperationException("Known Resistance serial is missing.");
            Assert(resistance.Changes.All(change => change.WikiKey != "vblank rate"),
                "Expected patch-dependent Resistance VBlank advice to stay out of default game config.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(candidatePath))!);
            File.Copy(firstPath, candidatePath, overwrite: true);

            var run = new WikiParityRun(
                ZipFile: Path.GetFileName(zipPath),
                ZipSha256: zipHash,
                CorpusFile: analysis.SourceFile,
                CorpusSha256: analysis.SourceSha256,
                SnapshotDate: analysis.SnapshotDate,
                ParseMilliseconds: analysis.ParseDuration.TotalMilliseconds,
                Stats: analysis.Stats,
                OldCatalogChanges: oldCatalog.Rec.Sum(item => item.Changes.Count),
                GeneratedCatalogChanges: analysis.CatalogRecommendations.Sum(item => item.Changes.Count),
                Comparison: comparison,
                Deterministic: deterministic,
                CatalogLoadCount: loaded.Count,
                Prefixes: analysis.Pages
                    .Where(page => page.IsGameContent)
                    .SelectMany(page => page.Serials)
                    .GroupBy(serial => serial[..4], StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                KeySummary: analysis.Recommendations
                    .GroupBy(entry => entry.WikiKey, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new WikiKeySummary(
                        group.Key,
                        group.Count(),
                        group.Count(entry => entry.ParserStatus == WikiParserStatus.Mapped),
                        group.Count(entry => entry.IncludedInCatalog),
                        group.Select(entry => entry.Classification).Distinct().OrderBy(item => item).ToArray(),
                        group.Select(entry => entry.ParserStatus).Distinct().OrderBy(item => item).ToArray(),
                        group.Select(entry => entry.RawWikiValue).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray()))
                    .ToArray(),
                PagesWithoutSerials: analysis.Pages
                    .Where(page => page.IsGameContent && page.Serials.Count == 0)
                    .Select(page => page.Title)
                    .OrderBy(title => title, StringComparer.Ordinal)
                    .ToArray(),
                PagesWithoutRecommendations: analysis.Pages
                    .Where(page => page.IsGameContent && page.Recommendations.Count == 0)
                    .Select(page => page.Title)
                    .OrderBy(title => title, StringComparer.Ordinal)
                    .ToArray(),
                Findings: analysis.Findings,
                Pages: analysis.Pages);

            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                File.WriteAllText(
                    reportPath,
                    JsonSerializer.Serialize(run, ReportOptions),
                    new UTF8Encoding(false));
            }

            return run;
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void ExtractBounded(string zipPath, string xmlPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry(EntryName)
            ?? throw new InvalidDataException($"Wiki snapshot ZIP is missing '{EntryName}'.");
        Assert(entry.Length is > 0 and <= MaxEntryBytes,
            $"Wiki XML entry length {entry.Length} is outside the allowed bound.");

        using var source = entry.Open();
        using var destination = new FileStream(
            xmlPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            total += read;
            Assert(total <= MaxEntryBytes, "Wiki XML expanded beyond the allowed bound.");
            destination.Write(buffer, 0, read);
        }

        Assert(total == entry.Length,
            $"Wiki XML extraction length mismatch. Expected {entry.Length}, actual {total}.");
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed record WikiKeySummary(
    string WikiKey,
    int Count,
    int Mapped,
    int Included,
    IReadOnlyList<WikiRecommendationClass> Classifications,
    IReadOnlyList<WikiParserStatus> Statuses,
    IReadOnlyList<string> SampleValues);

internal sealed record WikiParityRun(
    string ZipFile,
    string ZipSha256,
    string CorpusFile,
    string CorpusSha256,
    string SnapshotDate,
    double ParseMilliseconds,
    WikiCorpusStats Stats,
    int OldCatalogChanges,
    int GeneratedCatalogChanges,
    WikiCatalogComparison Comparison,
    bool Deterministic,
    int CatalogLoadCount,
    IReadOnlyDictionary<string, int> Prefixes,
    IReadOnlyList<WikiKeySummary> KeySummary,
    IReadOnlyList<string> PagesWithoutSerials,
    IReadOnlyList<string> PagesWithoutRecommendations,
    IReadOnlyList<WikiCorpusFinding> Findings,
    IReadOnlyList<WikiCorpusPage> Pages);
