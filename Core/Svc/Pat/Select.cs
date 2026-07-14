using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed record SelRes(
    bool IsOk,
    string PatText,
    int SelNum,
    string? Error)
{
    public static SelRes Failure(string error)
    {
        return new SelRes(
            false,
            string.Empty,
            0,
            error);
    }
}

public sealed partial class SelComp
{
    private const string PatVer = "1.2";

    private readonly ReadSvc _reader = new();

    public SelRes Compile(
        string sourceText,
        IReadOnlyList<Mdl.PatMatch> selectedMatches,
        IReadOnlyList<Mdl.PatMatch>? requestedMatches = null)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return SelRes.Failure(
                "The patch library is empty.");
        }

        var selected = selectedMatches
            .Where(PatAct.Can)
            .GroupBy(
                SelKey.From,
                KeyCmp.Instance)
            .Select(group => group.First())
            .ToDictionary(
                SelKey.From,
                KeyCmp.Instance);

        if (selected.Count == 0)
        {
            return SelRes.Failure(
                "No compatible serial/version target with an RPCS3 executable-hash guard was selected.");
        }

        Mdl.PatMatch[] requested =
        [
            .. (requestedMatches ?? selectedMatches)
                .Where(PatAct.Can)
                .GroupBy(
                    SelKey.From,
                    KeyCmp.Instance)
                .Select(group => group.First())
        ];

        var integrityError = VldReq(requested);

        if (integrityError is not null)
        {
            return SelRes.Failure(
                integrityError);
        }

        try
        {
            var yaml = R3Bound.LoadYaml(
                ReadSvc.NormDup2(
                    sourceText),
                "Patch selection source");

            if (yaml.Documents.Count == 0 ||
                yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return SelRes.Failure(
                    "The patch library root is not a YAML mapping.");
            }

            var candidates = ReadCand(
                root,
                selected.Keys);

            var outputRoot = new YamlMappingNode
            {
                { "Version", PatVer }
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
                    return SelRes.Failure(
                        $"Selected patch '{match.Patch.Title}' was not found in its source library.");
                }

                var candidate = Select(
                    available,
                    match.Patch.PatVer);

                var patchNode = TryGet(
                    candidate.Details,
                    "Patch");

                if (patchNode is not YamlSequenceNode)
                {
                    return SelRes.Failure(
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

                if (Contain2(
                        outputPatchMap,
                        match.Patch.Title))
                {
                    continue;
                }

                outputPatchMap.Add(
                    new YamlScalarNode(match.Patch.Title),
                    Build(
                        match,
                        candidate.Details,
                        patchNode));
            }

            var compiledText = Save(outputRoot);

            var validation = _reader.ReadText(
                compiledText,
                PatSrc.ManCht,
                sourcePath: null);

            if (!validation.IsOk)
            {
                return SelRes.Failure(
                    "The selected patch document failed RPCS3 validation: " +
                    validation.Error);
            }

            if (validation.Catalog.Entries.Count != selected.Count)
            {
                return SelRes.Failure(
                    $"Selection validation expected {selected.Count} patch(es) but found {validation.Catalog.Entries.Count}.");
            }

            return new SelRes(
                true,
                compiledText,
                selected.Count,
                null);
        }
        catch (YamlException ex)
        {
            return SelRes.Failure(
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return SelRes.Failure(
                ex.Message);
        }
    }

    private static string? VldReq(
        IReadOnlyList<Mdl.PatMatch> requested)
    {
        foreach (var match in requested)
        {
            foreach (var dependency in match.Patch.Patches)
            {
                if (!Contains(
                        requested,
                        match.Patch.RunKey,
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
                    $"{match.Patch.RunKey}|{match.Patch.Group!.Trim()}",
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
                match => Valid(
                    match.Patch.RunKey,
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
            if (HasDep(
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

    private static bool Contains(
        IReadOnlyList<Mdl.PatMatch> matches,
        string runtimeHashKey,
        string title)
    {
        return matches.Any(match =>
            string.Equals(
                match.Patch.RunKey,
                runtimeHashKey,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                match.Patch.Title,
                title,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDep(
        string identity,
        IReadOnlyDictionary<string, Mdl.PatMatch> requested,
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

        foreach (var dependency in match.Patch.Patches)
        {
            var dependencyIdentity = Valid(
                match.Patch.RunKey,
                dependency);

            if (!requested.ContainsKey(dependencyIdentity))
            {
                continue;
            }

            if (HasDep(
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

    private static string Valid(
        string runtimeHashKey,
        string title)
    {
        return $"{runtimeHashKey}|{title}";
    }

    private static Dictionary<SelKey, List<SrcCand>>
        ReadCand(
            YamlMappingNode root,
            IEnumerable<SelKey> selectedKeys)
    {
        var wanted = selectedKeys.ToHashSet(
            KeyCmp.Instance);

        var output =
            new Dictionary<SelKey, List<SrcCand>>(
                KeyCmp.Instance);

        foreach (var (hashNode, valueNode) in root.Children)
        {
            var normalizedHashKey =
                ReadSvc.Decode2(
                    Scalar2(hashNode));

            if (!RunKey.TryParse(
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
                    ReadSvc.Decode(
                        Scalar2(titleNode));

                var key =
                    new SelKey(
                        runtimeKey.RunKey,
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
                    new SrcCand(
                        details,
                        Scalar(
                            TryGet(
                                details,
                                "Patch Version"))));
            }
        }

        return output;
    }

    private static SrcCand Select(
        IReadOnlyList<SrcCand> candidates,
        string? expectedPatchVersion)
    {
        if (!string.IsNullOrWhiteSpace(expectedPatchVersion))
        {
            var exact = candidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.PatVer,
                    expectedPatchVersion,
                    StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
            {
                return exact;
            }
        }

        return candidates[0];
    }

    private static YamlMappingNode Build(
        Mdl.PatMatch match,
        YamlMappingNode sourceDetails,
        YamlNode patchNode)
    {
        var firstTarget = match.Patch.GameTgt.Count > 0
            ? match.Patch.GameTgt[0]
            : null;

        var firstSerial = match.Patch.Serials.Count > 0
            ? match.Patch.Serials[0]
            : null;

        var title =
            match.TgtName ??
            firstTarget?.Title ??
            "Unknown";

        var serial =
            match.TgtSer ??
            match.MatSer ??
            firstSerial ??
            throw new InvalidOperationException(
                "Selected patch has no target serial.");

        var version =
            Svc.PatVer.Norm(
                match.TgtVer) ??
            Svc.PatVer.Norm(
                match.MatVer) ??
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

        Copy(
            sourceDetails,
            details,
            "Author");

        Copy(
            sourceDetails,
            details,
            "Notes");

        Copy(
            sourceDetails,
            details,
            "Group");

        Copy(
            sourceDetails,
            details,
            "Patch Version");

        var dependencies =
            TryGet(sourceDetails, "Dependencies") ??
            TryGet(sourceDetails, "Depends On");

        if (dependencies is not null)
        {
            details.Add(
                "Dependencies",
                CloneAnc(dependencies));
        }

        var configurableValues =
            TryGet(
                sourceDetails,
                "Configurable Values");

        if (configurableValues is not null)
        {
            details.Add(
                "Configurable Values",
                CloneAnc(configurableValues));
        }

        details.Add(
            "Patch",
            CloneAnc(patchNode));

        return details;
    }

    private static void Copy(
        YamlMappingNode source,
        YamlMappingNode destination,
        string key)
    {
        var node = TryGet(source, key);

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

    private static YamlNode CloneAnc(
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
                        CloneAnc)),

            YamlMappingNode mapping =>
                Clone(mapping),

            _ => throw new InvalidOperationException(
                "Unsupported YAML node in patch selection.")
        };
    }

    private static YamlMappingNode Clone(
        YamlMappingNode mapping)
    {
        var clone = new YamlMappingNode();

        foreach (var (key, value) in mapping.Children)
        {
            clone.Add(
                CloneAnc(key),
                CloneAnc(value));
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

    private static bool Contain2(
        YamlMappingNode mapping,
        string key)
    {
        return mapping.Children.Keys.Any(node =>
            Scalar2(node).Equals(
                key,
                StringComparison.OrdinalIgnoreCase));
    }

    private static YamlNode? TryGet(
        YamlMappingNode mapping,
        string key)
    {
        foreach (var (candidate, value) in mapping.Children)
        {
            if (Scalar2(candidate).Equals(
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? Scalar(
        YamlNode? node)
    {
        return node is YamlScalarNode scalar &&
               !string.IsNullOrWhiteSpace(scalar.Value)
            ? scalar.Value.Trim()
            : null;
    }

    private static string Scalar2(
        YamlNode node)
    {
        return node is YamlScalarNode scalar
            ? scalar.Value?.Trim() ?? string.Empty
            : node.ToString()?.Trim() ?? string.Empty;
    }

    private readonly record struct SelKey(
        string RunHash,
        string Title)
    {
        public string HashKey => RunHash;

        public static SelKey From(
            Mdl.PatMatch match)
        {
            return new SelKey(
                match.Patch.RunKey,
                match.Patch.Title);
        }
    }

    private sealed class KeyCmp :
        IEqualityComparer<SelKey>
    {
        public static KeyCmp Instance { get; } =
            new();

        public bool Equals(
            SelKey left,
            SelKey right)
        {
            return string.Equals(
                       left.HashKey,
                       right.HashKey,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       left.Title,
                       right.Title,
                       StringComparison.OrdinalIgnoreCase);
        }

        int IEqualityComparer<SelKey>.GetHashCode(
            SelKey value)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.HashKey),
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.Title));
        }
    }

    private sealed record SrcCand(
        YamlMappingNode Details,
        string? PatVer);
}
