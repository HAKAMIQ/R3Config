using System;
using System.Text.Json;
using R3Config.Tests;

internal static partial class Program
{
    private static bool TryRunWikiMode(
        string[] args,
        TestRunner runner,
        TestContext context)
    {
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
}
