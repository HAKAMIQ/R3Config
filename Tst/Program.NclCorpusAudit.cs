using System;
using System.Collections.Generic;
using R3Config.Tests;

internal static partial class Program
{
    internal const string NclCorpusAuditFlag = "--ncl-corpus-audit";

    internal const string NclCorpusBuildFlag = "--ncl-corpus-build";

    internal const string NclIdentityCoverageFlag = "--ncl-identity-coverage";

    internal const string NclLookupParityFlag = "--ncl-lookup-parity";

    // --ncl-corpus-audit <outputDirectory> <SourceId=path> [<SourceId=path>...]
    // Reads every named corpus through the shipped NclCorpusReader and writes
    // the merge evidence to the output directory. Read only: it never touches
    // App/Data/ncl.zip. Argument shape is owned here so the entry point stays
    // a router.
    private static bool TryRunNclCorpusAuditMode(
        string[] args,
        TestRunner runner)
    {
        if (args.Length == 4 && args[0].Equals("--ncl-source-discovery", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclSourceDiscoveryT),
                () => NclSourceDiscoveryT.Run(args[1], args[2], args[3]));
            return true;
        }

        if (args.Length == 1 && args[0].Equals("--ncl-source-regression-only", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclSourceDiscoveryT.Regression), NclSourceDiscoveryT.Regression);
            return true;
        }

        // --ncl-lookup-parity <outputDirectory> <corpusZip>
        // Pushes every proven target binding through the production lookup and
        // reports the first stage that loses it. Read only.
        if (args.Length == 3 &&
            args[0].Equals(
                NclLookupParityFlag,
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(
                nameof(NclLookupParityT),
                () => NclLookupParityT.Run(args[1], args[2]));
            return true;
        }

        if (args.Length == 4 &&
            args[0].Equals(NclIdentityCoverageFlag, StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclIdentityCoverageT),
                () => NclIdentityCoverageT.Run(args[1], args[2], args[3]));
            return true;
        }

        if (args.Length >= 3 &&
            args[0].Equals(
                NclCorpusAuditFlag,
                StringComparison.OrdinalIgnoreCase))
        {
            var sources = ParseNclCorpusAuditSources(args, 2);

            runner.RunCase(
                nameof(NclCorpusAuditT),
                () => NclCorpusAuditT.Run(args[1], sources));

            return true;
        }

        // --ncl-corpus-build <outputDirectory> <candidateZip> <SourceId=path>...
        // Builds the unified candidate corpus and verifies it against the
        // definitions that went into it. It writes only the candidate path it
        // is given; replacing the shipped corpus is a separate, manual act.
        if (args.Length >= 4 &&
            args[0].Equals(
                NclCorpusBuildFlag,
                StringComparison.OrdinalIgnoreCase))
        {
            var sources = ParseNclCorpusAuditSources(args, 3);

            runner.RunCase(
                nameof(NclCorpusBuilderT),
                () => NclCorpusBuilderT.Run(args[1], args[2], sources));

            return true;
        }

        return false;
    }

    // Concrete return type per CA1859: the single caller consumes the list
    // directly, so an interface here buys nothing and costs a cast.
    private static List<(string Id, string Path)>
        ParseNclCorpusAuditSources(string[] args, int firstSource)
    {
        var sources = new List<(string Id, string Path)>(
            args.Length - firstSource);

        for (var index = firstSource; index < args.Length; index++)
        {
            var argument = args[index];
            var separator = argument.IndexOf(
                '=',
                StringComparison.Ordinal);

            if (separator <= 0 ||
                separator == argument.Length - 1)
            {
                throw new ArgumentException(
                    $"NCL corpus audit source must be written as SourceId=path: '{argument}'.",
                    nameof(args));
            }

            sources.Add((
                argument[..separator],
                argument[(separator + 1)..]));
        }

        return sources;
    }
}
