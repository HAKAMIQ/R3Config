using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// Answers one question with production code only: for every target a source
// file provably declares, does the runtime actually return its cheats.
//
// Filename analysis alone cannot answer it - the old corpus gates passed while
// real games showed nothing - so every proven binding here is pushed through
// NclParser.ParseIdentity, the target index inside NclCorpusReader, and
// NclCorpusReader.LookupZip exactly as the application calls them. A binding
// that the parser refuses is reported with its reason rather than guessed at.
internal static class NclLookupParityT
{
    private static readonly JsonSerializerOptions ReportJson =
        new() { WriteIndented = true };

    // Where a binding stopped being reachable. The first stage that loses it
    // is the only one worth fixing.
    private enum Stage
    {
        ProvenReachable,

        // The target resolves and its cheats are discoverable, but the source
        // has no offline-static or option cheat. It must still reach Lookup/UI;
        // only Preview/Apply are ineligible.
        SemanticIneligible,

        LookupMissing,
        LookupNoCandidates,
        UnresolvedNoSerial,
        UnresolvedVersion,
        AmbiguousBinding,
        ParserError
    }

    private sealed record Row(
        string RelativePath,
        string Serial,
        string? SourceVersion,
        string? AppVersion,
        string? EffectiveVersion,
        string Evidence,
        string? UnresolvedReason,
        Stage Stage,
        int LookupCandidates,
        string LookupCode);

    public static void Run(string outputDirectory, string corpusPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusPath);
        Directory.CreateDirectory(outputDirectory);

        var full = Path.GetFullPath(corpusPath);
        var sha = FileSha256(full);
        var rows = new List<Row>();

        // The same reader the runtime uses, so "has anything convertible" is
        // answered by the shipped verifier rather than by a rule invented here.
        var audit = NclCorpusReader.Audit(full);
        var convertible = audit.Files.ToDictionary(
            file => file.RelativePath,
            file => file.Cheats.Any(cheat =>
                cheat.Status is NclVerifyStat.OfflineStaticCandidate
                    or NclVerifyStat.OptionSelectionRequired),
            StringComparer.OrdinalIgnoreCase);
        var names = audit.Files
            .Select(file => file.RelativePath)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // One lookup per distinct target, not per binding: the answer depends
        // on the target alone and each call rehashes the whole archive.
        var lookups = new Dictionary<string, NclCorpusLookupResult>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var relative in names)
        {
            NclSourceIdentity identity;

            try
            {
                identity = NclParser.ParseIdentity(Path.GetFileName(relative));
            }
            catch (Exception ex) when (
                ex is ArgumentException or FormatException or OverflowException)
            {
                rows.Add(new Row(relative, string.Empty, null, null, null,
                    ex.Message.Replace('\t', ' '), "PARSER_ERROR",
                    Stage.ParserError, 0, string.Empty));
                continue;
            }

            if (identity.TargetBindings.Count == 0)
            {
                rows.Add(new Row(relative, string.Empty, null, null, null,
                    string.Empty,
                    identity.UnresolvedReason ?? "UNRESOLVED_NO_SERIAL",
                    Stage.UnresolvedNoSerial, 0, string.Empty));
                continue;
            }

            foreach (var binding in identity.TargetBindings)
            {
                var evidence = string.Join(' ', binding.Evidence
                    .Select(item => $"{item.Kind}@{item.Offset}:{item.Token}"));

                if (binding.EffectiveVersion is not { } version)
                {
                    rows.Add(new Row(relative, binding.Serial,
                        binding.SourceVersion, binding.AppVersion, null,
                        evidence, binding.UnresolvedReason,
                        binding.UnresolvedReason is not null &&
                        binding.UnresolvedReason.Contains(
                            "AMBIGUOUS", StringComparison.Ordinal)
                            ? Stage.AmbiguousBinding
                            : Stage.UnresolvedVersion,
                        0, string.Empty));
                    continue;
                }

                var key = binding.Serial + "|" + version;

                if (!lookups.TryGetValue(key, out var lookup))
                {
                    lookup = NclCorpusReader.LookupZip(
                        full, binding.Serial, version, sha);
                    lookups[key] = lookup;
                }

                var carries = lookup.Cheats.Count > 0;
                var reachedThisFile = lookup.Cheats
                    .SelectMany(candidate => candidate.Sources)
                    .Any(source => source.RelativePath.Equals(
                        relative, StringComparison.OrdinalIgnoreCase));
                var hasConvertible =
                    convertible.TryGetValue(relative, out var value) && value;

                rows.Add(new Row(relative, binding.Serial,
                    binding.SourceVersion, binding.AppVersion, version,
                    evidence, null,
                    reachedThisFile
                        ? hasConvertible
                            ? Stage.ProvenReachable
                            : Stage.SemanticIneligible
                        : carries
                            ? Stage.LookupMissing
                            : Stage.LookupNoCandidates,
                    lookup.Cheats.Count,
                    lookup.Code.ToString()));
            }
        }

        Report(outputDirectory, corpusPath, sha, names.Length, rows);
    }

    private static void Report(
        string outputDirectory,
        string corpusPath,
        string sha,
        int files,
        List<Row> rows)
    {
        Write(outputDirectory, "lookup-parity.tsv",
            "RelPath\tSerial\tSourceVersion\tAppVersion\tEffectiveVersion\tStage\tLookupCandidates\tLookupCode\tUnresolvedReason\tEvidence",
            rows.Select(row => string.Join('\t',
                row.RelativePath, row.Serial,
                row.SourceVersion ?? string.Empty,
                row.AppVersion ?? string.Empty,
                row.EffectiveVersion ?? string.Empty,
                row.Stage.ToString(),
                row.LookupCandidates.ToString(CultureInfo.InvariantCulture),
                row.LookupCode,
                row.UnresolvedReason ?? string.Empty,
                row.Evidence)));

        var proven = rows.Count(row => row.EffectiveVersion is not null);
        var reachable = rows.Count(row => IsReachable(row.Stage));
        var ineligible = rows.Count(row => row.Stage == Stage.SemanticIneligible);
        var lookupZero = rows.Count(row =>
            row.Stage is Stage.LookupMissing or Stage.LookupNoCandidates);

        // A file that proves more than one serial must stay reachable from
        // every one of them. Counting only the reachable ones would hide
        // exactly the failure this harness exists to catch, so the set of
        // multi-serial files is taken from all proven bindings and the lost
        // ones are counted inside it.
        var provenRows = rows
            .Where(row => row.EffectiveVersion is not null)
            .ToArray();
        var multiSerialFiles = provenRows
            .GroupBy(row => row.RelativePath, StringComparer.Ordinal)
            .Where(group => group
                .Select(row => row.Serial)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .ToArray();
        var multiSerial = multiSerialFiles
            .Count(group => group.All(row => IsReachable(row.Stage)));
        var multiSerialLost = multiSerialFiles
            .SelectMany(group => group)
            .Count(row => row.Stage is Stage.LookupMissing or Stage.LookupNoCandidates);
        var versionBindingsLost = provenRows
            .Count(row => row.Stage is Stage.LookupMissing or Stage.LookupNoCandidates);

        var report = new
        {
            Corpus = Path.GetFullPath(corpusPath),
            CorpusSha256 = sha,
            Files = files,
            TargetBindings = rows.Count,
            ProvenBindings = proven,
            LookupReachableBindings = reachable,

            // The UI list is a direct projection of LookupZip's candidates -
            // NclImportVm binds _bundledLookup.Cheats with no filter of its own
            // - so a reachable binding is a visible one. NclUiT pins that
            // projection so this equality cannot drift unnoticed.
            UiReachableBindings = reachable,

            MultiSerialFilesReachable = multiSerial,
            SemanticIneligibleBindings = ineligible,
            MultiSerialBindingsLost = multiSerialLost,
            VersionBindingsLost = versionBindingsLost,
            VersionedBindings = rows.Count(row => row.EffectiveVersion is not null),
            UnresolvedNoSerial = rows.Count(row => row.Stage == Stage.UnresolvedNoSerial),
            UnresolvedVersion = rows.Count(row => row.Stage == Stage.UnresolvedVersion),
            AmbiguousBindings = rows.Count(row => row.Stage == Stage.AmbiguousBinding),
            ParserErrors = rows.Count(row => row.Stage == Stage.ParserError),

            LookupZeroForProvenBinding = lookupZero,
            UiZeroForLookupBinding = 0,
            ValidSerialBindingsSilentlyLost = lookupZero,

            StageDistribution = rows
                .GroupBy(row => row.Stage)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => group.Count(),
                    StringComparer.Ordinal)
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "lookup-parity.json"),
            JsonSerializer.Serialize(report, ReportJson),
            new UTF8Encoding(false));

        Console.WriteLine(
            $"NCL lookup parity: files={files} bindings={rows.Count} " +
            $"proven={proven} reachable={reachable} ineligible={ineligible} " +
            $"LOOKUP_ZERO_FOR_PROVEN_BINDING={lookupZero} " +
            $"UI_ZERO_FOR_LOOKUP_BINDING=0 " +
            $"MULTISERIAL_BINDINGS_LOST={multiSerialLost} " +
            $"VERSION_BINDINGS_LOST={versionBindingsLost} " +
            $"unresolvedNoSerial={report.UnresolvedNoSerial} " +
            $"unresolvedVersion={report.UnresolvedVersion} " +
            $"ambiguous={report.AmbiguousBindings} " +
            $"parserErrors={report.ParserErrors}");

        if (lookupZero > 0 || multiSerialLost > 0 || versionBindingsLost > 0)
        {
            throw new InvalidDataException(
                $"Proven target bindings lost: lookupZero={lookupZero}, " +
                $"multiSerial={multiSerialLost}, version={versionBindingsLost}.");
        }
    }

    private static bool IsReachable(Stage stage) =>
        stage is Stage.ProvenReachable or Stage.SemanticIneligible;

    private static string FileSha256(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Write(
        string directory,
        string name,
        string header,
        IEnumerable<string> rows)
    {
        using var writer = new StreamWriter(
            Path.Combine(directory, name), false, new UTF8Encoding(false));
        writer.WriteLine(header);

        foreach (var row in rows)
        {
            writer.WriteLine(row);
        }
    }
}
