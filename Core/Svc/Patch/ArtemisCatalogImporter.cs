using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed record ArtemisSourceFile(
    string Name,
    string Text);

public sealed record ArtemisRejectedEntry(
    string SourceName,
    string RuntimeKey,
    string Title,
    string Code,
    string Reason);

public sealed record ArtemisImportResult(
    bool IsOk,
    string CatalogText,
    int CandidateEntries,
    int AcceptedEntries,
    IReadOnlyList<ArtemisRejectedEntry> RejectedEntries,
    string? Error)
{
    public static ArtemisImportResult Failure(
        IReadOnlyList<ArtemisRejectedEntry> rejectedEntries,
        string error)
    {
        return new ArtemisImportResult(
            false,
            string.Empty,
            0,
            0,
            rejectedEntries,
            error);
    }
}

public sealed class ArtemisCatalogImporter
{
    private const int MaxSourceFiles = 512;
    private const string PatchVersion = "1.2";

    private static readonly Regex RuntimeKeyLine = new(
        @"^(?<key>(?:(?:[A-Z0-9]{9})-)?(?:PPU|SPU|PRX|OVL)-[^:\r\n]+):\s*(?:#.*)?$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex PatchTitleLine = new(
        @"^ {2}(?! )(?:(?:\""(?<double>(?:[^\""\\]|\\.)*)\"")|(?:'(?<single>[^']*)')|(?<plain>[^:#]+?))\s*:",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

    private readonly ReadSvc _reader = new();
    private readonly PatchCatalogMergeService _merge = new();

    public ArtemisImportResult Import(
        IReadOnlyList<ArtemisSourceFile> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);

        if (sourceFiles.Count == 0)
        {
            return ArtemisImportResult.Failure(
                [],
                "The Artemis source archive contains no patch files.");
        }

        if (sourceFiles.Count > MaxSourceFiles)
        {
            return ArtemisImportResult.Failure(
                [],
                $"The Artemis source archive exceeds the {MaxSourceFiles}-file input limit.");
        }

        var rejected = new List<ArtemisRejectedEntry>();
        var candidates = new List<Candidate>();
        var seenNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        var ordinal = 0;

        foreach (var source in sourceFiles
                     .OrderBy(
                         source => source.Name,
                         StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(source.Name) ||
                !seenNames.Add(source.Name))
            {
                rejected.Add(
                    RejectFile(
                        source.Name,
                        "ARTEMIS_SOURCE_NAME",
                        "The source file name is empty or duplicated."));
                continue;
            }

            try
            {
                R3Bound.Check(
                    source.Text,
                    R3Limits.PatCatBy,
                    source.Name);
                totalBytes = checked(
                    totalBytes +
                    Encoding.UTF8.GetByteCount(source.Text));
                if (totalBytes > R3Limits.PatCatBy)
                {
                    return ArtemisImportResult.Failure(
                        Sort(rejected),
                        $"The Artemis source archive exceeds the {R3Limits.PatCatBy}-byte expanded input limit.");
                }

                ImportFile(
                    source,
                    candidates,
                    rejected,
                    ref ordinal);
            }
            catch (Exception ex) when (
                ex is YamlException or
                    InvalidDataException or
                    InvalidOperationException or
                    OverflowException)
            {
                RejectInvalidSource(
                    source,
                    ex.Message,
                    rejected);
            }
        }

        if (candidates.Count == 0)
        {
            return ArtemisImportResult.Failure(
                Sort(rejected),
                "No valid Artemis patch entries were found.");
        }

        var candidateCount = candidates.Count;

        candidates = ResolveDuplicates(
            candidates,
            rejected);
        candidates = RejectCycles(
            candidates,
            rejected);
        candidates = RejectMissingDependencies(
            candidates,
            rejected);

        if (candidates.Count == 0)
        {
            return ArtemisImportResult.Failure(
                Sort(rejected),
                "Every Artemis patch entry was rejected during catalog validation.");
        }

        var catalogText = BuildCatalog(candidates);
        var validation = _reader.ReadText(
            catalogText,
            PatSrc.Artemis,
            "art.yml");

        if (!validation.IsOk)
        {
            return ArtemisImportResult.Failure(
                Sort(rejected),
                "The generated Artemis catalog failed validation: " +
                validation.Error);
        }

        if (validation.Catalog.Entries.Count != candidates.Count)
        {
            return ArtemisImportResult.Failure(
                Sort(rejected),
                $"The generated Artemis catalog expected {candidates.Count} entries but the reader found {validation.Catalog.Entries.Count}.");
        }

        return new ArtemisImportResult(
            true,
            catalogText,
            candidateCount,
            candidates.Count,
            Sort(rejected),
            null);
    }

    private void ImportFile(
        ArtemisSourceFile source,
        ICollection<Candidate> candidates,
        List<ArtemisRejectedEntry> rejected,
        ref int ordinal)
    {
        var preparedText = Prepare(source.Text);
        var yaml = R3Bound.LoadYaml(
            ReadSvc.NormDup2(preparedText),
            source.Name);

        if (yaml.Documents.Count != 1 ||
            yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException(
                "The Artemis source root is not a single YAML mapping.");
        }

        foreach (var (keyNode, valueNode) in root.Children)
        {
            var rawKey = Scalar(keyNode);
            if (rawKey.Equals(
                    "Version",
                    StringComparison.OrdinalIgnoreCase) ||
                rawKey.StartsWith(
                    "Anchors",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var runtimeKey = ReadSvc.Decode2(rawKey);
            if (valueNode is not YamlMappingNode patchMap)
            {
                rejected.Add(
                    new ArtemisRejectedEntry(
                        source.Name,
                        runtimeKey,
                        "<mapping>",
                        "ARTEMIS_PATCH_MAP",
                        "The runtime-key value is not a patch mapping."));
                continue;
            }

            if (!RunKey.TryParse(
                    runtimeKey,
                    out var parsedRuntimeKey))
            {
                RejectChildren(
                    source.Name,
                    runtimeKey,
                    patchMap,
                    "ARTEMIS_RUNTIME_KEY",
                    "The key is not generated by RPCS3.",
                    rejected);
                continue;
            }

            foreach (var (titleNode, detailsNode) in patchMap.Children)
            {
                var title = ReadSvc.Decode(
                    Scalar(titleNode));
                if (string.IsNullOrWhiteSpace(title))
                {
                    rejected.Add(
                        new ArtemisRejectedEntry(
                            source.Name,
                            parsedRuntimeKey.RunKey,
                            "<empty>",
                            "ARTEMIS_TITLE",
                            "The patch title is empty."));
                    continue;
                }

                if (detailsNode is not YamlMappingNode details)
                {
                    rejected.Add(
                        new ArtemisRejectedEntry(
                            source.Name,
                            parsedRuntimeKey.RunKey,
                            title,
                            "ARTEMIS_DETAILS",
                            "The patch details are not a YAML mapping."));
                    continue;
                }

                var expandedDetails = (YamlMappingNode)CloneExpanded(
                    details,
                    new HashSet<YamlNode>(
                        ReferenceEqualityComparer.Instance));
                if (TryGet(
                        expandedDetails,
                        "Patch") is not YamlSequenceNode patch ||
                    patch.Children.Count == 0)
                {
                    rejected.Add(
                        new ArtemisRejectedEntry(
                            source.Name,
                            parsedRuntimeKey.RunKey,
                            title,
                            "ARTEMIS_PATCH_MISSING",
                            "The definition has no executable RPCS3 Patch sequence."));
                    continue;
                }

                var candidateText = BuildCandidate(
                    parsedRuntimeKey.RunKey,
                    title,
                    expandedDetails);
                var validation = _reader.ReadText(
                    candidateText,
                    PatSrc.Artemis,
                    source.Name);

                if (!validation.IsOk ||
                    validation.Catalog.Entries.Count != 1)
                {
                    rejected.Add(
                        new ArtemisRejectedEntry(
                            source.Name,
                            parsedRuntimeKey.RunKey,
                            title,
                            "ARTEMIS_ENTRY_INVALID",
                            validation.Error ??
                            "The definition did not produce exactly one valid patch entry."));
                    continue;
                }

                candidates.Add(
                    new Candidate(
                        source.Name,
                        ordinal++,
                        parsedRuntimeKey.RunKey,
                        title,
                        expandedDetails,
                        validation.Catalog.Entries[0]));
            }
        }
    }

    private List<Candidate> ResolveDuplicates(
        IReadOnlyList<Candidate> candidates,
        List<ArtemisRejectedEntry> rejected)
    {
        var merged = _merge.Merge(
            new PatCat(
                PatchVersion,
                candidates
                    .Select(candidate => candidate.Entry)
                    .ToArray()));
        var selected = candidates
            .Where(candidate =>
                merged.Entries.Any(entry =>
                    ReferenceEquals(
                        entry,
                        candidate.Entry)))
            .ToList();

        foreach (var candidate in candidates.Where(candidate =>
                     !selected.Contains(candidate)))
        {
            var hasWinner = merged.Entries.Any(entry =>
                SameIdentity(
                    entry,
                    candidate.Entry));
            rejected.Add(
                Reject(
                    candidate,
                    hasWinner
                        ? "ARTEMIS_DUPLICATE_SUPERSEDED"
                        : "ARTEMIS_DUPLICATE_CONFLICT",
                    hasWinner
                        ? "A deterministic merge kept another definition or version for the same runtime key and title."
                        : "Equivalent patch versions contain conflicting definitions, so every conflicting variant was rejected."));
        }

        return selected;
    }

    private static List<Candidate> RejectCycles(
        IReadOnlyList<Candidate> candidates,
        List<ArtemisRejectedEntry> rejected)
    {
        var byIdentity = candidates.ToDictionary(
            candidate => Identity(candidate.Entry),
            StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var cycles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var identity in byIdentity.Keys)
        {
            Visit(
                identity,
                byIdentity,
                states,
                stack,
                cycles);
        }

        foreach (var identity in cycles
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            rejected.Add(
                Reject(
                    byIdentity[identity],
                    "ARTEMIS_DEPENDENCY_CYCLE",
                    "The patch participates in a dependency cycle."));
        }

        return candidates
            .Where(candidate =>
                !cycles.Contains(
                    Identity(candidate.Entry)))
            .ToList();
    }

    private static List<Candidate> RejectMissingDependencies(
        IReadOnlyList<Candidate> candidates,
        List<ArtemisRejectedEntry> rejected)
    {
        var remaining = candidates.ToList();

        while (true)
        {
            var identities = remaining
                .Select(candidate =>
                    Identity(candidate.Entry))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
            var invalid = remaining
                .Where(candidate =>
                    candidate.Entry.Patches.Any(dependency =>
                        !identities.Contains(
                            Identity(
                                candidate.Entry.RunKey,
                                dependency))))
                .ToArray();

            if (invalid.Length == 0)
            {
                return remaining;
            }

            foreach (var candidate in invalid)
            {
                var missing = candidate.Entry.Patches
                    .First(dependency =>
                        !identities.Contains(
                            Identity(
                                candidate.Entry.RunKey,
                                dependency)));
                rejected.Add(
                    Reject(
                        candidate,
                        "ARTEMIS_DEPENDENCY_MISSING",
                        $"Required patch '{missing}' is missing or was rejected for the same runtime key."));
                remaining.Remove(candidate);
            }
        }
    }

    private static void Visit(
        string identity,
        IReadOnlyDictionary<string, Candidate> candidates,
        IDictionary<string, int> states,
        IList<string> stack,
        ISet<string> cycles)
    {
        if (states.TryGetValue(
                identity,
                out var state) &&
            state == 2)
        {
            return;
        }

        if (state == 1)
        {
            var start = stack.IndexOf(identity);
            for (var index = Math.Max(0, start);
                 index < stack.Count;
                 index++)
            {
                cycles.Add(stack[index]);
            }
            return;
        }

        states[identity] = 1;
        stack.Add(identity);
        var candidate = candidates[identity];

        foreach (var dependency in candidate.Entry.Patches)
        {
            var dependencyIdentity = Identity(
                candidate.Entry.RunKey,
                dependency);
            if (candidates.ContainsKey(dependencyIdentity))
            {
                Visit(
                    dependencyIdentity,
                    candidates,
                    states,
                    stack,
                    cycles);
            }
        }

        stack.RemoveAt(stack.Count - 1);
        states[identity] = 2;
    }

    private static string BuildCatalog(
        IReadOnlyList<Candidate> candidates)
    {
        var root = new YamlMappingNode
        {
            { "Version", PatchVersion }
        };
        var counts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates
                     .OrderBy(
                         candidate => candidate.SourceName,
                         StringComparer.Ordinal)
                     .ThenBy(candidate => candidate.Ordinal))
        {
            var count = counts.TryGetValue(
                candidate.RuntimeKey,
                out var current)
                ? current + 1
                : 1;
            counts[candidate.RuntimeKey] = count;
            var outputKey = count == 1
                ? candidate.RuntimeKey
                : candidate.RuntimeKey +
                  "__r3config_duplicate_" +
                  count.ToString(CultureInfo.InvariantCulture);
            var patchMap = new YamlMappingNode
            {
                {
                    new YamlScalarNode(candidate.Title),
                    CloneExpanded(
                        candidate.Details,
                        new HashSet<YamlNode>(
                            ReferenceEqualityComparer.Instance))
                }
            };
            root.Add(
                new YamlScalarNode(outputKey),
                patchMap);
        }

        return Save(root);
    }

    private static string BuildCandidate(
        string runtimeKey,
        string title,
        YamlMappingNode details)
    {
        var root = new YamlMappingNode
        {
            { "Version", PatchVersion },
            {
                runtimeKey,
                new YamlMappingNode
                {
                    { title, details }
                }
            }
        };
        return Save(root);
    }

    private static string Prepare(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sourceLines = text
            .TrimStart('\uFEFF')
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal)
            .Split('\n');
        var lines = new List<string>(
            sourceLines.Length);
        var hasPayload = false;

        foreach (var line in sourceLines)
        {
            var trimmed = line.Trim();
            if (trimmed is "---" or "..." ||
                line.StartsWith(
                    "Version:",
                    StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(
                    "version 1.2",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!hasPayload &&
                (RuntimeKeyLine.IsMatch(line) ||
                 line.StartsWith(
                     "Anchors:",
                     StringComparison.OrdinalIgnoreCase)))
            {
                hasPayload = true;
            }

            if (!hasPayload &&
                (trimmed.StartsWith(
                     "Title:",
                     StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith(
                     "Serial:",
                     StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith(
                     "Category:",
                     StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            lines.Add(line);
        }

        return "Version: " +
               PatchVersion +
               "\n" +
               string.Join("\n", lines).TrimEnd() +
               "\n";
    }

    private static string Save(YamlMappingNode root)
    {
        var yaml = new YamlStream(
            new YamlDocument(root));
        using var writer = new StringWriter(
            CultureInfo.InvariantCulture);
        yaml.Save(
            writer,
            assignAnchors: false);

        var lines = writer
            .ToString()
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal)
            .Split('\n')
            .Where(line =>
                line.Trim() is not "---" and not "...");
        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    private static YamlNode CloneExpanded(
        YamlNode node,
        ISet<YamlNode> activePath)
    {
        if (!activePath.Add(node))
        {
            throw new InvalidOperationException(
                "The Artemis definition contains a recursive YAML alias.");
        }

        try
        {
            switch (node)
            {
                case YamlScalarNode scalar:
                    return new YamlScalarNode(scalar.Value)
                    {
                        Style = scalar.Style
                    };

                case YamlSequenceNode sequence:
                    {
                        var clone = new YamlSequenceNode();
                        foreach (var child in sequence.Children)
                        {
                            clone.Add(
                                CloneExpanded(
                                    child,
                                    activePath));
                        }
                        return clone;
                    }

                case YamlMappingNode mapping:
                    {
                        var clone = new YamlMappingNode();
                        foreach (var (key, value) in mapping.Children)
                        {
                            clone.Add(
                                CloneExpanded(
                                    key,
                                    activePath),
                                CloneExpanded(
                                    value,
                                    activePath));
                        }
                        return clone;
                    }

                default:
                    throw new InvalidOperationException(
                        "The Artemis definition contains an unsupported YAML node.");
            }
        }
        finally
        {
            activePath.Remove(node);
        }
    }

    private static void RejectChildren(
        string sourceName,
        string runtimeKey,
        YamlMappingNode patchMap,
        string code,
        string reason,
        List<ArtemisRejectedEntry> rejected)
    {
        if (patchMap.Children.Count == 0)
        {
            rejected.Add(
                new ArtemisRejectedEntry(
                    sourceName,
                    runtimeKey,
                    "<empty>",
                    code,
                    reason));
            return;
        }

        foreach (var titleNode in patchMap.Children.Keys)
        {
            rejected.Add(
                new ArtemisRejectedEntry(
                    sourceName,
                    runtimeKey,
                    ReadSvc.Decode(
                        Scalar(titleNode)),
                    code,
                    reason));
        }
    }

    private static void RejectInvalidSource(
        ArtemisSourceFile source,
        string reason,
        List<ArtemisRejectedEntry> rejected)
    {
        string? runtimeKey = null;
        var rejectedCount = 0;
        var lines = source.Text
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal)
            .Split('\n');

        foreach (var line in lines)
        {
            var runtimeMatch = RuntimeKeyLine.Match(line);
            if (runtimeMatch.Success)
            {
                runtimeKey = runtimeMatch.Groups["key"].Value.Trim();
                continue;
            }

            if (runtimeKey is null)
            {
                continue;
            }

            var titleMatch = PatchTitleLine.Match(line);
            if (titleMatch.Success)
            {
                var title = titleMatch.Groups["double"].Success
                    ? titleMatch.Groups["double"].Value
                    : titleMatch.Groups["single"].Success
                        ? titleMatch.Groups["single"].Value
                        : titleMatch.Groups["plain"].Value.Trim();
                rejected.Add(
                    new ArtemisRejectedEntry(
                        source.Name,
                        runtimeKey,
                        title,
                        "ARTEMIS_SOURCE_INVALID",
                        reason));
                rejectedCount++;
                continue;
            }

            if (line.Length > 0 &&
                !char.IsWhiteSpace(line[0]))
            {
                runtimeKey = null;
            }
        }

        if (rejectedCount == 0)
        {
            rejected.Add(
                RejectFile(
                    source.Name,
                    "ARTEMIS_SOURCE_INVALID",
                    reason));
        }
    }

    private static ArtemisRejectedEntry Reject(
        Candidate candidate,
        string code,
        string reason)
    {
        return new ArtemisRejectedEntry(
            candidate.SourceName,
            candidate.RuntimeKey,
            candidate.Title,
            code,
            reason);
    }

    private static ArtemisRejectedEntry RejectFile(
        string? sourceName,
        string code,
        string reason)
    {
        return new ArtemisRejectedEntry(
            string.IsNullOrWhiteSpace(sourceName)
                ? "<unknown>"
                : sourceName,
            "<file>",
            "<file>",
            code,
            reason);
    }

    private static ArtemisRejectedEntry[] Sort(
        IEnumerable<ArtemisRejectedEntry> rejected)
    {
        return rejected
            .OrderBy(
                entry => entry.SourceName,
                StringComparer.Ordinal)
            .ThenBy(
                entry => entry.RuntimeKey,
                StringComparer.Ordinal)
            .ThenBy(
                entry => entry.Title,
                StringComparer.Ordinal)
            .ThenBy(
                entry => entry.Code,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SameIdentity(
        Rpcs3PatchEntry left,
        Rpcs3PatchEntry right)
    {
        return string.Equals(
                   left.RunKey,
                   right.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.Title,
                   right.Title,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Identity(
        Rpcs3PatchEntry entry)
    {
        return Identity(
            entry.RunKey,
            entry.Title);
    }

    private static string Identity(
        string runtimeKey,
        string title)
    {
        return runtimeKey + "\0" + title;
    }

    private static YamlNode? TryGet(
        YamlMappingNode mapping,
        string key)
    {
        foreach (var (candidate, value) in mapping.Children)
        {
            if (Scalar(candidate).Equals(
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string Scalar(YamlNode node)
    {
        return node is YamlScalarNode scalar
            ? scalar.Value?.Trim() ?? string.Empty
            : node.ToString()?.Trim() ?? string.Empty;
    }

    private sealed record Candidate(
        string SourceName,
        int Ordinal,
        string RuntimeKey,
        string Title,
        YamlMappingNode Details,
        Rpcs3PatchEntry Entry);
}
