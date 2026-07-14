using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed record PatchSelectionCompileResult(
    bool Succeeded,
    string PatchText,
    int SelectedCount,
    string? Error)
{
    public static PatchSelectionCompileResult Failure(string error)
    {
        return new PatchSelectionCompileResult(
            false,
            string.Empty,
            0,
            error);
    }
}

public sealed partial class PatchSelectionCompiler
{
    private const string PatchEngineVersion = "1.2";

    private readonly PatchCatalogReader _reader = new();

    public PatchSelectionCompileResult Compile(
        string sourceText,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches,
        IReadOnlyList<Rpcs3PatchMatchResult>? requestedMatches = null)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return PatchSelectionCompileResult.Failure(
                "The patch library is empty.");
        }

        var selected = selectedMatches
            .Where(PatchActivationPolicy.CanEnable)
            .GroupBy(
                SelectionKey.FromMatch,
                SelectionKeyComparer.Instance)
            .Select(group => group.First())
            .ToDictionary(
                SelectionKey.FromMatch,
                SelectionKeyComparer.Instance);

        if (selected.Count == 0)
        {
            return PatchSelectionCompileResult.Failure(
                "No compatible serial/version target with an RPCS3 executable-hash guard was selected.");
        }

        Rpcs3PatchMatchResult[] requested =
        [
            .. (requestedMatches ?? selectedMatches)
                .Where(PatchActivationPolicy.CanEnable)
                .GroupBy(
                    SelectionKey.FromMatch,
                    SelectionKeyComparer.Instance)
                .Select(group => group.First())
        ];

        var integrityError = ValidateRequestedSelection(requested);

        if (integrityError is not null)
        {
            return PatchSelectionCompileResult.Failure(
                integrityError);
        }

        try
        {
            var yaml = new YamlStream();

            yaml.Load(
                new StringReader(
                    PatchCatalogReader.NormalizeDuplicateKeysForRead(
                        sourceText)));

            if (yaml.Documents.Count == 0 ||
                yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return PatchSelectionCompileResult.Failure(
                    "The patch library root is not a YAML mapping.");
            }

            var candidates = ReadCandidates(
                root,
                selected.Keys);

            var outputRoot = new YamlMappingNode
            {
                { "Version", PatchEngineVersion }
            };

            var outputHashes =
                new Dictionary<string, YamlMappingNode>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var (key, match) in selected)
            {
                if (!candidates.TryGetValue(
                        key,
                        out var available))
                {
                    return PatchSelectionCompileResult.Failure(
                        $"Selected patch '{match.Patch.Title}' was not found in its source library.");
                }

                var candidate = SelectCandidate(
                    available,
                    match.Patch.PatchVersion);

                var patchNode = TryGetNode(
                    candidate.Details,
                    "Patch");

                if (patchNode is not YamlSequenceNode)
                {
                    return PatchSelectionCompileResult.Failure(
                        $"Selected patch '{match.Patch.Title}' has no executable RPCS3 Patch sequence.");
                }

                var hashKey = key.HashKey;

                if (!outputHashes.TryGetValue(
                        hashKey,
                        out var outputPatchMap))
                {
                    outputPatchMap = [];
                    outputHashes.Add(hashKey, outputPatchMap);
                    outputRoot.Add(hashKey, outputPatchMap);
                }

                if (ContainsKey(
                        outputPatchMap,
                        match.Patch.Title))
                {
                    continue;
                }

                outputPatchMap.Add(
                    new YamlScalarNode(match.Patch.Title),
                    BuildDetails(
                        match,
                        candidate.Details,
                        patchNode));
            }

            var compiledText = Save(outputRoot);

            var validation = _reader.ReadText(
                compiledText,
                PatchSourceOrigin.ManagedCheats,
                sourcePath: null);

            if (!validation.Succeeded)
            {
                return PatchSelectionCompileResult.Failure(
                    "The selected patch document failed RPCS3 validation: " +
                    validation.Error);
            }

            if (validation.Catalog.Entries.Count != selected.Count)
            {
                return PatchSelectionCompileResult.Failure(
                    $"Selection validation expected {selected.Count} patch(es) but found {validation.Catalog.Entries.Count}.");
            }

            return new PatchSelectionCompileResult(
                true,
                compiledText,
                selected.Count,
                null);
        }
        catch (YamlException ex)
        {
            return PatchSelectionCompileResult.Failure(
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return PatchSelectionCompileResult.Failure(
                ex.Message);
        }
    }

    private static string? ValidateRequestedSelection(
        IReadOnlyList<Rpcs3PatchMatchResult> requested)
    {
        foreach (var match in requested)
        {
            foreach (var dependency in match.Patch.RequiredPatches)
            {
                if (!ContainsPatch(
                        requested,
                        match.Patch.RuntimeHashKey,
                        dependency))
                {
                    return
                        $"Patch '{match.Patch.Title}' requires '{dependency}' for the same executable hash. " +
                        "Select the required patch in the same enable operation.";
                }
            }
        }

        var groupConflict = requested
            .Where(match =>
                !string.IsNullOrWhiteSpace(match.Patch.Group))
            .GroupBy(
                match =>
                    $"{match.Patch.RuntimeHashKey}|{match.Patch.Group!.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (groupConflict is not null)
        {
            var titles = string.Join(
                ", ",
                groupConflict
                    .Select(match => match.Patch.Title)
                    .Order(StringComparer.OrdinalIgnoreCase));

            var groupName =
                groupConflict.First().Patch.Group!.Trim();

            return
                $"Patch group '{groupName}' is mutually exclusive for this executable hash. " +
                $"Select only one of: {titles}.";
        }

        var requestedByIdentity = requested
            .GroupBy(
                match => IntegrityIdentity(
                    match.Patch.RuntimeHashKey,
                    match.Patch.Title),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var visiting =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var visited =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var identity in requestedByIdentity.Keys)
        {
            if (HasDependencyCycle(
                    identity,
                    requestedByIdentity,
                    visiting,
                    visited,
                    out var cycle))
            {
                return
                    $"Patch dependency cycle detected: {cycle}.";
            }
        }

        return null;
    }

    private static bool ContainsPatch(
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        string runtimeHashKey,
        string title)
    {
        return matches.Any(match =>
            string.Equals(
                match.Patch.RuntimeHashKey,
                runtimeHashKey,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                match.Patch.Title,
                title,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDependencyCycle(
        string identity,
        IReadOnlyDictionary<string, Rpcs3PatchMatchResult> requested,
        ISet<string> visiting,
        ISet<string> visited,
        out string cycle)
    {
        cycle = string.Empty;

        if (visited.Contains(identity))
        {
            return false;
        }

        if (!visiting.Add(identity))
        {
            cycle = requested.TryGetValue(
                identity,
                out var repeated)
                ? repeated.Patch.Title
                : identity;

            return true;
        }

        var match = requested[identity];

        foreach (var dependency in match.Patch.RequiredPatches)
        {
            var dependencyIdentity = IntegrityIdentity(
                match.Patch.RuntimeHashKey,
                dependency);

            if (!requested.ContainsKey(dependencyIdentity))
            {
                continue;
            }

            if (HasDependencyCycle(
                    dependencyIdentity,
                    requested,
                    visiting,
                    visited,
                    out var nestedCycle))
            {
                cycle =
                    match.Patch.Title +
                    " -> " +
                    nestedCycle;

                return true;
            }
        }

        visiting.Remove(identity);
        visited.Add(identity);
        return false;
    }

    private static string IntegrityIdentity(
        string runtimeHashKey,
        string title)
    {
        return $"{runtimeHashKey}|{title}";
    }

    private static Dictionary<SelectionKey, List<SourceCandidate>>
        ReadCandidates(
            YamlMappingNode root,
            IEnumerable<SelectionKey> selectedKeys)
    {
        var wanted = selectedKeys.ToHashSet(
            SelectionKeyComparer.Instance);

        var output =
            new Dictionary<SelectionKey, List<SourceCandidate>>(
                SelectionKeyComparer.Instance);

        foreach (var (hashNode, valueNode) in root.Children)
        {
            var normalizedHashKey =
                PatchCatalogReader.DecodeNormalizedHashKey(
                    ScalarValue(hashNode));

            if (!Rpcs3PatchRuntimeKey.TryParse(
                    normalizedHashKey,
                    out var runtimeKey) ||
                valueNode is not YamlMappingNode patchMap)
            {
                continue;
            }

            foreach (var (titleNode, detailsNode) in patchMap.Children)
            {
                if (detailsNode is not YamlMappingNode details)
                {
                    continue;
                }

                var title =
                    PatchCatalogReader.DecodeNormalizedMappingKey(
                        ScalarValue(titleNode));

                var key =
                    new SelectionKey(
                        runtimeKey.RuntimeKey,
                        title);

                if (!wanted.Contains(key))
                {
                    continue;
                }

                if (!output.TryGetValue(key, out var list))
                {
                    list = [];
                    output.Add(key, list);
                }

                list.Add(
                    new SourceCandidate(
                        details,
                        ScalarText(
                            TryGetNode(
                                details,
                                "Patch Version"))));
            }
        }

        return output;
    }

    private static SourceCandidate SelectCandidate(
        IReadOnlyList<SourceCandidate> candidates,
        string? expectedPatchVersion)
    {
        if (!string.IsNullOrWhiteSpace(expectedPatchVersion))
        {
            var exact = candidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.PatchVersion,
                    expectedPatchVersion,
                    StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
            {
                return exact;
            }
        }

        return candidates[0];
    }

    private static YamlMappingNode BuildDetails(
        Rpcs3PatchMatchResult match,
        YamlMappingNode sourceDetails,
        YamlNode patchNode)
    {
        var firstTarget = match.Patch.GameTargets.Count > 0
            ? match.Patch.GameTargets[0]
            : null;

        var firstSerial = match.Patch.GameSerials.Count > 0
            ? match.Patch.GameSerials[0]
            : null;

        var title =
            match.MatchedTargetTitle ??
            firstTarget?.Title ??
            "Unknown";

        var serial =
            match.MatchedTargetSerial ??
            match.MatchedSerial ??
            firstSerial ??
            throw new InvalidOperationException(
                "Selected patch has no target serial.");

        var version =
            Rpcs3PatchVersion.Normalize(
                match.MatchedTargetVersion) ??
            Rpcs3PatchVersion.Normalize(
                match.MatchedVersion) ??
            throw new InvalidOperationException(
                "Selected patch has no exact target version.");

        var versions = new YamlSequenceNode(
            new YamlScalarNode(version));

        var serials = new YamlMappingNode
        {
            { new YamlScalarNode(serial), versions }
        };

        var games = new YamlMappingNode
        {
            { new YamlScalarNode(title), serials }
        };

        var details = new YamlMappingNode
        {
            { "Games", games }
        };

        CopyOptionalScalar(
            sourceDetails,
            details,
            "Author");

        CopyOptionalScalar(
            sourceDetails,
            details,
            "Notes");

        CopyOptionalScalar(
            sourceDetails,
            details,
            "Group");

        CopyOptionalScalar(
            sourceDetails,
            details,
            "Patch Version");

        var dependencies =
            TryGetNode(sourceDetails, "Dependencies") ??
            TryGetNode(sourceDetails, "Depends On");

        if (dependencies is not null)
        {
            details.Add(
                "Dependencies",
                CloneWithoutAnchors(dependencies));
        }

        var configurableValues =
            TryGetNode(
                sourceDetails,
                "Configurable Values");

        if (configurableValues is not null)
        {
            details.Add(
                "Configurable Values",
                CloneWithoutAnchors(configurableValues));
        }

        details.Add(
            "Patch",
            CloneWithoutAnchors(patchNode));

        return details;
    }

    private static void CopyOptionalScalar(
        YamlMappingNode source,
        YamlMappingNode destination,
        string key)
    {
        var node = TryGetNode(source, key);

        if (node is YamlScalarNode scalar &&
            scalar.Value is not null)
        {
            destination.Add(
                key,
                new YamlScalarNode(scalar.Value)
                {
                    Style = scalar.Style
                });
        }
    }

    private static YamlNode CloneWithoutAnchors(
        YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar =>
                new YamlScalarNode(scalar.Value)
                {
                    Style = scalar.Style
                },

            YamlSequenceNode sequence =>
                new YamlSequenceNode(
                    sequence.Children.Select(
                        CloneWithoutAnchors)),

            YamlMappingNode mapping =>
                CloneMapping(mapping),

            _ => throw new InvalidOperationException(
                "Unsupported YAML node in patch selection.")
        };
    }

    private static YamlMappingNode CloneMapping(
        YamlMappingNode mapping)
    {
        var clone = new YamlMappingNode();

        foreach (var (key, value) in mapping.Children)
        {
            clone.Add(
                CloneWithoutAnchors(key),
                CloneWithoutAnchors(value));
        }

        return clone;
    }

    private static string Save(
        YamlMappingNode root)
    {
        var yaml =
            new YamlStream(
                new YamlDocument(root));

        using var writer =
            new StringWriter(
                CultureInfo.InvariantCulture);

        yaml.Save(
            writer,
            assignAnchors: false);

        string[] lines =
        [
            .. writer
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
                    line.Trim() is not "---" and not "...")
        ];

        return
            string.Join("\r\n", lines).TrimEnd() +
            "\r\n";
    }

    private static bool ContainsKey(
        YamlMappingNode mapping,
        string key)
    {
        return mapping.Children.Keys.Any(node =>
            ScalarValue(node).Equals(
                key,
                StringComparison.OrdinalIgnoreCase));
    }

    private static YamlNode? TryGetNode(
        YamlMappingNode mapping,
        string key)
    {
        foreach (var (candidate, value) in mapping.Children)
        {
            if (ScalarValue(candidate).Equals(
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ScalarText(
        YamlNode? node)
    {
        return node is YamlScalarNode scalar &&
               !string.IsNullOrWhiteSpace(scalar.Value)
            ? scalar.Value.Trim()
            : null;
    }

    private static string ScalarValue(
        YamlNode node)
    {
        return node is YamlScalarNode scalar
            ? scalar.Value?.Trim() ?? string.Empty
            : node.ToString()?.Trim() ?? string.Empty;
    }

    private readonly record struct SelectionKey(
        string RuntimeHashKey,
        string Title)
    {
        public string HashKey => RuntimeHashKey;

        public static SelectionKey FromMatch(
            Rpcs3PatchMatchResult match)
        {
            return new SelectionKey(
                match.Patch.RuntimeHashKey,
                match.Patch.Title);
        }
    }

    private sealed class SelectionKeyComparer :
        IEqualityComparer<SelectionKey>
    {
        public static SelectionKeyComparer Instance { get; } =
            new();

        public bool Equals(
            SelectionKey left,
            SelectionKey right)
        {
            return string.Equals(
                       left.RuntimeHashKey,
                       right.RuntimeHashKey,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       left.Title,
                       right.Title,
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(
            SelectionKey value)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.RuntimeHashKey),
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.Title));
        }
    }

    private sealed record SourceCandidate(
        YamlMappingNode Details,
        string? PatchVersion);
}
