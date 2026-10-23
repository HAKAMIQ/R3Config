using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// Measures what a unified NCL corpus would actually contain, using nothing but
// the shipped pipeline: NclCorpusReader reads and audits every source,
// NclSemanticVerifier classifies, NclStaticLowerer reduces to concrete writes,
// and PatchWriteSemantics decides every relationship between two definitions.
// This type owns no parser, no decoder and no comparator of its own.
//
// The question it answers is not "how many cheats are there". It is: for every
// (serial, application version, cheat name) the runtime will look up, do the
// sources agree, and when they disagree is the disagreement recoverable
// without inventing a merge rule nothing has proven.
internal static class NclCorpusAuditT
{
    // How the shipped lookup path treats one (serial, version, name) group,
    // and - when that path would lose the cheat - whether the loss is
    // recoverable from proven evidence alone.
    private enum GroupVerdict
    {
        // One definition, or several that hash identically. The lookup keeps it.
        Agreed,

        // The lookup sees more than one semantic hash and drops the whole
        // group, but PatchWriteSemantics proves every member writes exactly the
        // same bytes. Choosing one member loses nothing.
        RecoverableByCanonicalPick,

        // The members provably write different bytes. Keeping both needs a
        // naming decision, which is not this harness's to make.
        TrueDifferent,

        // At least one relationship could not be proven. Fail closed.
        UnknownFailClosed
    }

    // Hoisted per CA1869: the serializer caches per-options state, so a fresh
    // instance at the call site throws that cache away on every run.
    private static readonly JsonSerializerOptions SummaryJson =
        new() { WriteIndented = true };

    private sealed record Instance(
        string SourceId,
        string RelPath,
        string Serial,
        string Version,
        string Title,
        string Name,
        string? Author,
        NclVerifyStat Status,
        NclStaticLowerCode LowerCode,
        string? SemanticSha256,
        string? WritePrint,
        long WrittenBytes);

    public static void Run(
        string outputDirectory,
        IReadOnlyList<(string Id, string Path)> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0)
        {
            throw new ArgumentException(
                "At least one corpus source is required.",
                nameof(sources));
        }

        Directory.CreateDirectory(outputDirectory);

        var instances = new List<Instance>();
        var sourceRows = new List<string>();
        var fileRows = new List<string>();
        var unidentifiedFiles = 0;
        var unidentifiedCheats = 0;

        foreach (var (id, path) in sources)
        {
            NclCorpusReport report;

            try
            {
                report = NclCorpusReader.Audit(path);
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException or
                    InvalidDataException or
                    ArgumentException or
                    OverflowException or
                    DecoderFallbackException)
            {
                sourceRows.Add(string.Join('\t',
                    id, path, "AUDIT_FAILED", "0", "0", "0", "0", "0", "0",
                    ex.Message.Replace('\t', ' ')));
                Console.WriteLine($"  {id}: AUDIT_FAILED {ex.Message}");
                continue;
            }

            foreach (var file in report.Files)
            {
                fileRows.Add(string.Join('\t',
                    id,
                    file.RelativePath,
                    file.Format.ToString(),
                    file.Dialect.ToString(),
                    file.UsedWindows1252Fallback ? "1" : "0",
                    file.HasExactIdentity ? "1" : "0",
                    Count(file.CheatCount),
                    Count(file.StaticCandidates),
                    Count(file.StaticLowered),
                    Count(file.StaticLowerRejected),
                    file.ErrorCodes.Count == 0
                        ? string.Empty
                        : string.Join(',', file.ErrorCodes)));

                if (!file.HasExactIdentity)
                {
                    // No proven serial+version pair in the filename. The file is
                    // still counted; it is simply not comparable against any
                    // other source, and saying so is the point.
                    unidentifiedFiles++;
                    unidentifiedCheats += file.Cheats.Count;
                    continue;
                }

                foreach (var target in file.Identity.Targets)
                {
                    foreach (var cheat in file.Cheats)
                    {
                        instances.Add(new Instance(
                            id,
                            file.RelativePath,
                            target.Serial.ToUpperInvariant(),
                            target.Version,
                            file.Identity.Title ?? string.Empty,
                            cheat.Name,
                            cheat.Author,
                            cheat.Status,
                            cheat.LowerCode,
                            cheat.SemanticSha256,
                            cheat.WritePrint,
                            cheat.WrittenByteCount));
                    }
                }
            }

            sourceRows.Add(string.Join('\t',
                id,
                path,
                report.SourceKind.ToString(),
                Count(report.FileCount),
                Count(report.CheatCount),
                Count(report.StaticCandidates),
                Count(report.StaticLowered),
                Count(report.OptionSelectionRequired),
                Count(report.FilesWithWindows1252Fallback),
                report.SourceSha256 ?? string.Empty));

            Console.WriteLine(
                $"  {id}: files={report.FileCount} cheats={report.CheatCount} " +
                $"static={report.StaticCandidates} lowered={report.StaticLowered} " +
                $"options={report.OptionSelectionRequired} " +
                $"exactIdentity={report.ExactIdentityFiles} " +
                $"parserErrors={report.FilesWithParserErrors}");
        }

        Write(outputDirectory, "sources.tsv",
            "SourceId\tPath\tKind\tFiles\tCheats\tStatic\tLowered\tOptionRequired\tW1252\tSha256",
            sourceRows);
        Write(outputDirectory, "files.tsv",
            "SourceId\tRelPath\tFormat\tDialect\tW1252\tExactIdentity\tCheats\tStatic\tLowered\tLowerRejected\tErrorCodes",
            fileRows);
        Write(outputDirectory, "cheats.tsv",
            "SourceId\tSerial\tVersion\tName\tAuthor\tStatus\tLowerCode\tWrittenBytes\tSemanticSha256\tWritePrint\tRelPath",
            instances.Select(item => string.Join('\t',
                item.SourceId, item.Serial, item.Version,
                item.Name, item.Author ?? string.Empty,
                item.Status.ToString(), item.LowerCode.ToString(),
                item.WrittenBytes.ToString(CultureInfo.InvariantCulture),
                item.SemanticSha256 ?? string.Empty,
                item.WritePrint ?? string.Empty,
                item.RelPath)));

        Merge(outputDirectory, instances, unidentifiedFiles, unidentifiedCheats);
    }

    // The shipped lookup groups by (serial, version) then by cheat name, and
    // drops any name whose members do not share one semantic hash. That is the
    // exact rule a merged corpus has to survive, so it is the rule measured
    // here rather than a friendlier one.
    private static void Merge(
        string outputDirectory,
        List<Instance> instances,
        int unidentifiedFiles,
        int unidentifiedCheats)
    {
        var groupRows = new List<string>();
        var agreed = 0;
        var recoverable = 0;
        var trueDifferent = 0;
        var unknown = 0;
        var droppedByLookup = 0;
        var lostCheats = 0;

        var groups = instances
            .GroupBy(item => (item.Serial, item.Version, Name: item.Name),
                SerialVersionNameComparer.Instance)
            .ToArray();

        foreach (var group in groups)
        {
            var members = group.ToArray();
            var hashes = members
                .Select(item => item.SemanticSha256 ?? "<none>")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var sources = members
                .Select(item => item.SourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            var verdict = hashes.Length == 1
                ? GroupVerdict.Agreed
                : Relate(members);

            switch (verdict)
            {
                case GroupVerdict.Agreed:
                    agreed++;
                    break;
                case GroupVerdict.RecoverableByCanonicalPick:
                    recoverable++;
                    droppedByLookup++;
                    break;
                case GroupVerdict.TrueDifferent:
                    trueDifferent++;
                    droppedByLookup++;
                    lostCheats += hashes.Length;
                    break;
                default:
                    unknown++;
                    droppedByLookup++;
                    lostCheats += hashes.Length;
                    break;
            }

            if (verdict == GroupVerdict.Agreed)
            {
                continue;
            }

            groupRows.Add(string.Join('\t',
                members[0].Serial,
                members[0].Version,
                members[0].Name,
                verdict.ToString(),
                hashes.Length.ToString(CultureInfo.InvariantCulture),
                members.Length.ToString(CultureInfo.InvariantCulture),
                string.Join(',', sources),
                members[0].Status.ToString(),
                string.Join(',', members
                    .Select(item => item.LowerCode.ToString())
                    .Distinct(StringComparer.Ordinal)),
                members[0].RelPath));
        }

        Write(outputDirectory, "namegroups.tsv",
            "Serial\tVersion\tName\tVerdict\tSemanticHashes\tInstances\tSources\tStatus\tLowerCodes\tFirstPath",
            groupRows);

        var summary = new
        {
            CheatInstances = instances.Count,
            UnidentifiedFiles = unidentifiedFiles,
            UnidentifiedCheats = unidentifiedCheats,
            Serials = instances
                .Select(item => item.Serial)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            TargetPairs = instances
                .Select(item => item.Serial + "|" + item.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            NameGroups = groups.Length,
            Agreed = agreed,
            RecoverableByCanonicalPick = recoverable,
            TrueDifferent = trueDifferent,
            UnknownFailClosed = unknown,

            // Historical only. This counts the name groups the PRE-VARIANT
            // lookup model would have suppressed, because it grouped by cheat
            // name and dropped any group holding more than one semantic hash.
            // The current reader emits one candidate per proven variant and
            // suppresses nothing, so this number does not describe production
            // behaviour. It is kept, under a name that says so, because it is
            // the measure of how much the old model was losing.
            LegacyNameCollisionGroups_PreVariantLookupModel = droppedByLookup,
            UnsafeAutoMerges = 0,
            RequiresDecision = trueDifferent + unknown,
            LostDefinitionsIfConcatenated = lostCheats,

            Provability = new
            {
                Provable = instances.Count(item =>
                    PatchWriteSemantics.IsProven(item.WritePrint)),
                Incomplete = instances.Count(item =>
                    item.WritePrint is not null &&
                    !PatchWriteSemantics.IsProven(item.WritePrint)),
                NotLowered = instances.Count(item => item.WritePrint is null)
            },

            StatusDistribution = instances
                .GroupBy(item => item.Status)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => group.Count(),
                    StringComparer.Ordinal)
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "summary.json"),
            JsonSerializer.Serialize(summary, SummaryJson),
            new UTF8Encoding(false));

        Console.WriteLine(
            $"NCL corpus merge analysis: instances={instances.Count} " +
            $"nameGroups={groups.Length} agreed={agreed} " +
            $"recoverable={recoverable} trueDifferent={trueDifferent} " +
            $"unknown={unknown} legacyNameCollisionGroups={droppedByLookup} " +
            $"requiresDecision={trueDifferent + unknown} " +
            $"unidentifiedFiles={unidentifiedFiles}");
    }

    // Every pair inside the group, decided by PatchWriteSemantics alone. One
    // unproven pair makes the whole group unproven: the weakest relationship
    // decides, never the friendliest one.
    private static GroupVerdict Relate(Instance[] members)
    {
        var distinct = members
            .GroupBy(item => item.SemanticSha256 ?? "<none>",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var verdict = GroupVerdict.RecoverableByCanonicalPick;

        for (var i = 0; i < distinct.Length; i++)
        {
            for (var j = i + 1; j < distinct.Length; j++)
            {
                switch (PatchWriteSemantics.Classify(
                            distinct[i].WritePrint,
                            distinct[j].WritePrint))
                {
                    case PatchWriteRelation.SemanticallyEquivalent:
                        continue;
                    case PatchWriteRelation.OverlappingDifferent:
                    case PatchWriteRelation.Disjoint:
                        // Proven to write different bytes under one name.
                        verdict = GroupVerdict.TrueDifferent;
                        continue;
                    default:
                        return GroupVerdict.UnknownFailClosed;
                }
            }
        }

        return verdict;
    }

    private static string Count(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Write(
        string directory,
        string name,
        string header,
        IEnumerable<string> rows)
    {
        using var writer = new StreamWriter(
            Path.Combine(directory, name),
            false,
            new UTF8Encoding(false));
        writer.WriteLine(header);

        foreach (var row in rows)
        {
            writer.WriteLine(row);
        }
    }

    private sealed class SerialVersionNameComparer
        : IEqualityComparer<(string Serial, string Version, string Name)>
    {
        public static readonly SerialVersionNameComparer Instance = new();

        public bool Equals(
            (string Serial, string Version, string Name) x,
            (string Serial, string Version, string Name) y)
        {
            return string.Equals(x.Serial, y.Serial, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Serial, string Version, string Name) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Serial),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }
    }
}
