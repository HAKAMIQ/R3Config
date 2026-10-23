using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using R3Config.Application;
using R3Config.Svc;
using R3Config.Tests;

internal static partial class Program
{
    private static bool TryRunWikiMode(
        string[] args,
        TestRunner runner,
        TestContext context)
    {
        if (args.Length == 3 &&
            args[0].Equals(
                "--wiki-rebuild",
                StringComparison.OrdinalIgnoreCase))
        {
            var rebuilt = RecommendationCatalogUseCase.Rebuild(args[1]);
            // Rebuild the current source in place without silently changing its
            // provenance. A new output still requires the canonical snapshot.
            if (File.Exists(args[2]))
            {
                var existing = RecSvc.Load(args[2]);
                Assert(existing.SrcHash.Equals(rebuilt.SrcHash, StringComparison.OrdinalIgnoreCase) &&
                       existing.DataVer == rebuilt.DataVer && existing.DataSrc == rebuilt.DataSrc,
                    "Wiki rebuild must use the existing catalog's exact source and snapshot date.");
            }
            else
            {
                AssertCanonicalCatalogProvenance(rebuilt, "Wiki rebuild input");
            }
            RecommendationCatalogUseCase.Export(rebuilt, args[2]);
            Console.WriteLine(
                $"Wiki catalog rebuilt: Snapshot={rebuilt.DataVer}, " +
                $"SourceHash={rebuilt.SrcHash}, Serials={rebuilt.Count}, " +
                $"Changes={rebuilt.Rec.Sum(item => item.Changes.Count)}, " +
                $"Output={Path.GetFullPath(args[2])}");
            return true;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--wiki-verify",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(
                "WikiCatalogParity",
                () => VerifyShippedWikiCatalog(args[1]));
            Console.WriteLine("Shipped Wiki catalog parity PASS.");
            return true;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--wiki-delta",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(
                "WikiNewerEvidenceDelta",
                () => ReportWikiEvidenceDelta(args[1]));
            Console.WriteLine(
                "Newer Wiki evidence delta completed; canonical provenance was not changed.");
            return true;
        }

        if (args.Length is 4 or 5 &&
            args[0].Equals(
                "--wiki-corpus",
                StringComparison.OrdinalIgnoreCase))
        {
            var corpusRun = WikiCorpusT.RunCorpus(
                args[1],
                args[2],
                args[3],
                args.Length == 5 ? args[4] : null);
            Console.WriteLine(JsonSerializer.Serialize(
                new
                {
                    corpusRun.CorpusFile,
                    corpusRun.SnapshotDate,
                    corpusRun.ParseMilliseconds,
                    corpusRun.Stats,
                    corpusRun.OldCatalogChanges,
                    corpusRun.GeneratedCatalogChanges,
                    corpusRun.Comparison.OldCatalogEntries,
                    corpusRun.Comparison.GeneratedCatalogEntries,
                    corpusRun.Comparison.Added,
                    corpusRun.Comparison.Removed,
                    corpusRun.Comparison.Changed,
                    corpusRun.Comparison.Unchanged,
                    corpusRun.Deterministic,
                    corpusRun.CatalogLoadCount
                },
                TestJson.Indented));
            Console.WriteLine("Wiki corpus regression passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--wiki-parser-only",
                StringComparison.OrdinalIgnoreCase))
        {
            var wikiRoot = context.CreateTempRoot("R3ConfigWikiParser");
            runner.RunCase(nameof(WikiCorpusT), () => WikiCorpusT.Fixture(wikiRoot));
            Console.WriteLine("Wiki parser focused tests passed.");
            return true;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--wiki-history-audit",
                StringComparison.OrdinalIgnoreCase))
        {
            var audit = WikiCorpusT.RunHistoryAudit(args[1]);
            Console.WriteLine(JsonSerializer.Serialize(audit, TestJson.Indented));
            Console.WriteLine("Wiki history full-dump audit passed.");
            return true;
        }

        return false;
    }

    private static void VerifyShippedWikiCatalog(string wikiXmlPath)
    {
        Assert(File.Exists(wikiXmlPath),
            $"Wiki snapshot XML does not exist: {wikiXmlPath}");

        var catalogPath = Path.Combine(
            RepoRoot(),
            "App",
            "Data",
            "cat.json");
        var shipped = RecSvc.Load(catalogPath);
        AssertCanonicalCatalogProvenance(
            shipped,
            "App/Data/cat.json");

        var analysis = WikiSvc.Analyze(wikiXmlPath);
        Assert(analysis.SourceSha256.Equals(
                   WikiSvc.CanonicalSnapshotSha256,
                   StringComparison.OrdinalIgnoreCase) &&
               analysis.SnapshotDate.Equals(
                   WikiSvc.CanonicalSnapshotDate,
                   StringComparison.Ordinal),
            "CANONICAL_WIKI_IDENTITY_MISMATCH: --wiki-verify accepts only the pinned September 2026 snapshot. " +
            $"Expected {WikiSvc.CanonicalSnapshotFileName} / {WikiSvc.CanonicalSnapshotSha256}; " +
            $"actual {Path.GetFileName(wikiXmlPath)} / {analysis.SourceSha256}.");

        Assert(analysis.SourceSha256.Equals(
                shipped.SrcHash,
                StringComparison.OrdinalIgnoreCase),
            "The shipped canonical catalog was generated from a different snapshot. " +
            $"cat.json SourceHash={shipped.SrcHash}, " +
            $"{Path.GetFileName(wikiXmlPath)} SHA256={analysis.SourceSha256}.");

        var comparison = WikiSvc.Compare(shipped, analysis);
        Console.WriteLine(
            $"Wiki catalog parity: ShippedSerials={comparison.OldCatalogEntries}, " +
            $"GeneratedSerials={comparison.GeneratedCatalogEntries}, " +
            $"Added={comparison.Added}, Removed={comparison.Removed}, " +
            $"Changed={comparison.Changed}, Unchanged={comparison.Unchanged}, " +
            $"ExactSettings={comparison.ExactRecommendationMatches}, " +
            $"MissingSettings={comparison.MissingRecommendations}, " +
            $"ExtraSettings={comparison.ExtraRecommendations}, " +
            $"DifferentSettings={comparison.DifferentRecommendations}, " +
            $"Differences={comparison.Differences.Count}.");

        foreach (var difference in comparison.Differences.Take(25))
        {
            Console.WriteLine(
                $"  {difference.Kind}: {difference.Serial} " +
                $"({difference.WikiTitle}) - {difference.Detail}");
        }

        Assert(comparison.Differences.Count == 0,
            "App/Data/cat.json does not match the catalog generated from " +
            $"{Path.GetFileName(wikiXmlPath)}: " +
            $"{comparison.Differences.Count} difference(s).");

        VerifyShippedWikiNotes(wikiXmlPath, analysis);
    }

    // App/Data/wiki-notes.json must carry the same pinned production source as
    // cat.json and equal, byte for byte, what that source produces.
    private static void VerifyShippedWikiNotes(string wikiXmlPath, R3Config.Mdl.WikiCorpusAnalysis analysis)
    {
        var notesPath = Path.Combine(
            RepoRoot(),
            "App",
            "Data",
            WikiEvidenceCatalog.FileName);
        var shipped = WikiEvidenceCatalog.Load(notesPath).Document;
        Assert(shipped.DataSource == "RPCS3 Wiki snapshot" &&
               shipped.DataVersion == WikiSvc.CanonicalSnapshotDate &&
               shipped.SourceHash.Equals(
                   WikiSvc.CanonicalSnapshotSha256,
                   StringComparison.OrdinalIgnoreCase) &&
               shipped.SourceFile == WikiSvc.CanonicalSnapshotFileName,
            "CANONICAL_WIKI_NOTES_PROVENANCE_MISMATCH: App/Data/wiki-notes.json is not the pinned September snapshot. " +
            $"DataVersion={shipped.DataVersion}, SourceHash={shipped.SourceHash}, SourceFile={shipped.SourceFile}.");

        var expectedPath = Path.Combine(Path.GetTempPath(), "r3-wiki-notes-verify-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            WikiEvidenceCatalog
                .FromPages(
                    "RPCS3 Wiki snapshot",
                    analysis.SnapshotDate,
                    analysis.SourceSha256,
                    WikiNotesCorpusReader.ReadAndParse(wikiXmlPath),
                    WikiSvc.CanonicalSnapshotFileName)
                .Save(expectedPath);
            Assert(File.ReadAllBytes(expectedPath).AsSpan().SequenceEqual(File.ReadAllBytes(notesPath)),
                "App/Data/wiki-notes.json does not match the notes generated from " +
                $"{Path.GetFileName(wikiXmlPath)}.");
        }
        finally
        {
            File.Delete(expectedPath);
        }

        Console.WriteLine(
            $"Wiki notes parity: Pages={shipped.Pages.Count}, SourceHash={shipped.SourceHash}, SourceFile={shipped.SourceFile}.");
    }

    private static void ReportWikiEvidenceDelta(string wikiXmlPath)
    {
        Assert(File.Exists(wikiXmlPath),
            $"Wiki evidence XML does not exist: {wikiXmlPath}");

        var catalogPath = Path.Combine(
            RepoRoot(),
            "App",
            "Data",
            "cat.json");
        var shipped = RecSvc.Load(catalogPath);
        AssertCanonicalCatalogProvenance(
            shipped,
            "App/Data/cat.json");

        var evidence = WikiSvc.Analyze(wikiXmlPath);
        Assert(!evidence.SourceSha256.Equals(
                   WikiSvc.CanonicalSnapshotSha256,
                   StringComparison.OrdinalIgnoreCase),
            "The supplied evidence is the canonical snapshot; use --wiki-verify instead.");

        var canonicalDate = DateTime.ParseExact(
            WikiSvc.CanonicalSnapshotDate,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        var evidenceDate = DateTime.ParseExact(
            evidence.SnapshotDate,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert(evidenceDate > canonicalDate,
            "--wiki-delta accepts only evidence newer than the pinned canonical snapshot.");

        var comparison = WikiSvc.Compare(shipped, evidence);
        Console.WriteLine(
            $"Wiki newer-evidence delta: Canonical={shipped.DataVer}/{shipped.SrcHash}, " +
            $"Evidence={evidence.SnapshotDate}/{evidence.SourceSha256}, " +
            $"CanonicalSerials={comparison.OldCatalogEntries}, " +
            $"EvidenceSerials={comparison.GeneratedCatalogEntries}, " +
            $"Added={comparison.Added}, Removed={comparison.Removed}, " +
            $"Changed={comparison.Changed}, Unchanged={comparison.Unchanged}, " +
            $"Differences={comparison.Differences.Count}.");
    }

    private static void AssertCanonicalCatalogProvenance(
        RecSvc catalog,
        string sourceName)
    {
        var generatedDate = catalog.GenAt?.UtcDateTime.ToString(
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert(
            catalog.DataSrc.Equals(
                "RPCS3 Wiki snapshot",
                StringComparison.Ordinal) &&
            catalog.DataVer.Equals(
                WikiSvc.CanonicalSnapshotDate,
                StringComparison.Ordinal) &&
            catalog.SrcHash.Equals(
                WikiSvc.CanonicalSnapshotSha256,
                StringComparison.OrdinalIgnoreCase) &&
            generatedDate == WikiSvc.CanonicalSnapshotDate,
            "CANONICAL_WIKI_PROVENANCE_MISMATCH: " +
            $"{sourceName} is not the pinned September catalog. " +
            $"DataSource={catalog.DataSrc}, DataVersion={catalog.DataVer}, " +
            $"GeneratedAt={generatedDate ?? "-"}, SourceHash={catalog.SrcHash}.");
    }
}
