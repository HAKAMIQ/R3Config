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

public sealed partial class PatchSelectionCompiler
{
    private const string PatVer = "1.2";

    private readonly ReadSvc _reader = new();

    public SelRes Compile(
        string sourceText,
        IReadOnlyList<Mdl.PatMatch> selectedMatches,
        IReadOnlyList<Mdl.PatMatch>? requestedMatches = null)
    {
        return Compile(
            [sourceText],
            selectedMatches,
            requestedMatches);
    }

    public SelRes Compile(
        IReadOnlyList<string> sourceTexts,
        IReadOnlyList<Mdl.PatMatch> selectedMatches,
        IReadOnlyList<Mdl.PatMatch>? requestedMatches = null)
    {
        ArgumentNullException.ThrowIfNull(sourceTexts);
        ArgumentNullException.ThrowIfNull(selectedMatches);

        var availableSources = sourceTexts
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .ToArray();

        if (availableSources.Length == 0)
        {
            return SelRes.Failure(
                "The patch libraries are empty.");
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

        var integrityError = VldReq(
            requested,
            selected.Values.ToArray());

        if (integrityError is not null)
        {
            return SelRes.Failure(
                integrityError);
        }

        try
        {
            var candidates =
                new Dictionary<SelKey, List<SrcCand>>(
                    KeyCmp.Instance);

            for (var sourceIndex = 0;
                 sourceIndex < availableSources.Length;
                 sourceIndex++)
            {
                var yaml = R3Bound.LoadYaml(
                    ReadSvc.NormDup2(
                        availableSources[sourceIndex]),
                    $"Patch selection source {sourceIndex + 1}");

                if (yaml.Documents.Count == 0 ||
                    yaml.Documents[0].RootNode is not YamlMappingNode root)
                {
                    return SelRes.Failure(
                        $"Patch library {sourceIndex + 1} root is not a YAML mapping.");
                }

                AddCand(
                    candidates,
                    ReadCand(
                        root,
                        selected.Keys));
            }

            var resolved = new List<Resolved>(selected.Count);
            foreach (var (key, match) in selected)
            {
                if (!candidates.TryGetValue(
                        key,
                        out var available))
                {
                    return SelRes.Failure(
                        $"Selected patch '{match.Patch.Title}' for '{key.RunHash}' was not found in the bundled or managed source libraries.");
                }

                var candidate = Select(
                    available,
                    match.Patch.PatVer,
                    match.Patch.DefPrint);

                if (TryGet(candidate.Details, "Patch") is not YamlSequenceNode)
                {
                    return SelRes.Failure(
                        $"Selected patch '{match.Patch.Title}' has no executable RPCS3 Patch sequence.");
                }

                resolved.Add(new Resolved(
                    key,
                    match,
                    candidate,
                    (YamlMappingNode)CloneExpandedNode(
                        candidate.Details,
                        new HashSet<YamlNode>(ReferenceEqualityComparer.Instance))));
            }

            var conflictError = ValidateWriteConflicts(resolved);
            if (conflictError is not null)
            {
                return SelRes.Failure(conflictError);
            }

            var outputRoot = new YamlMappingNode
            {
                { "Version", PatVer }
            };


            var outputHashes =
                new Dictionary<string, YamlMappingNode>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var item in resolved)
            {
                var key = item.Key;
                var match = item.Match;

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
                    item.ExpandedDetails);
            }

            var compiledText = Save(outputRoot);

            var validation = _reader.ReadText(
                compiledText,
                PatSrc.ManagedArtemis,
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
        IReadOnlyList<Mdl.PatMatch> requested,
        IReadOnlyList<Mdl.PatMatch> active)
    {
        try
        {
            PatDepVal.ValidateEnable(
                requested,
                active);
        }
        catch (PatchDependencyException ex)
        {
            return ex.Message;
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

    private static string? ValidateWriteConflicts(IReadOnlyList<Resolved> resolved)
    {
        foreach (var target in resolved.GroupBy(item => item.Key.HashKey, StringComparer.OrdinalIgnoreCase))
        {
            var writes = target.SelectMany(ReadWrites).ToArray();
            for (var leftIndex = 0; leftIndex < writes.Length; leftIndex++)
            {
                var left = writes[leftIndex];
                for (var rightIndex = leftIndex + 1; rightIndex < writes.Length; rightIndex++)
                {
                    var right = writes[rightIndex];
                    if (left.Title.Equals(right.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var leftEnd = left.Offset + (ulong)left.Bytes.Length;
                    var rightEnd = right.Offset + (ulong)right.Bytes.Length;
                    if (leftEnd <= right.Offset || rightEnd <= left.Offset)
                    {
                        continue;
                    }
                    if (left.Offset == right.Offset && left.Bytes.SequenceEqual(right.Bytes))
                    {
                        continue;
                    }
                    return $"Selected patches '{left.Title}' and '{right.Title}' have conflicting overlapping writes for '{target.Key}'.";
                }
            }
        }
        return null;
    }

    private static IEnumerable<StaticWrite> ReadWrites(Resolved resolved)
    {
        if (TryGet(resolved.ExpandedDetails, "Patch") is not YamlSequenceNode operations)
        {
            yield break;
        }
        foreach (var operation in operations.Children.OfType<YamlSequenceNode>())
        {
            if (operation.Children.Count < 3 ||
                Scalar(operation.Children[0]) is not { } type ||
                !TryUnsigned(Scalar(operation.Children[1]), out var offset) ||
                !TryUnsigned(Scalar(operation.Children[2]), out var value) ||
                !TryBytes(type, value, out var bytes))
            {
                continue;
            }
            yield return new StaticWrite(resolved.Match.Patch.Title, offset, bytes);
        }
    }

    private static bool TryUnsigned(string? value, out ulong parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var text = value.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed)
            : ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryBytes(string type, ulong value, out byte[] bytes)
    {
        bytes = [];
        var normalized = type.Trim().ToLowerInvariant();
        var width = normalized switch
        {
            "byte" => 1,
            "be16" or "le16" => 2,
            "be32" or "le32" => 4,
            "be64" or "le64" => 8,
            _ => 0
        };
        if (width == 0)
        {
            return false;
        }
        bytes = new byte[width];
        for (var index = 0; index < width; index++)
        {
            var shift = normalized.StartsWith("le", StringComparison.Ordinal)
                ? index * 8
                : (width - 1 - index) * 8;
            bytes[index] = (byte)((value >> shift) & 0xffUL);
        }
        return true;
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

        foreach (var dependency in match.Patch.Dependencies
                     .SelectMany(group => group.Titles))
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

                var patchNode = TryGet(details, "Patch");
                list.Add(
                    new SrcCand(
                        details,
                        Scalar(
                            TryGet(
                                details,
                                "Patch Version")),
                        ReadSvc.CalcDef(patchNode)));
            }
        }

        return output;
    }

    private static void AddCand(
        Dictionary<SelKey, List<SrcCand>> destination,
        Dictionary<SelKey, List<SrcCand>> source)
    {
        foreach (var (key, candidates) in source)
        {
            if (!destination.TryGetValue(
                    key,
                    out var existing))
            {
                existing = [];
                destination.Add(key, existing);
            }

            existing.AddRange(candidates);
        }
    }

    private static SrcCand Select(
        IReadOnlyList<SrcCand> candidates,
        string? expectedPatchVersion,
        string? expectedDefPrint)
    {
        if (!string.IsNullOrWhiteSpace(expectedDefPrint))
        {
            var exactDefinition = candidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.DefPrint,
                    expectedDefPrint,
                    StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(expectedPatchVersion) ||
                 string.Equals(
                     candidate.PatVer,
                     expectedPatchVersion,
                     StringComparison.OrdinalIgnoreCase)));

            if (exactDefinition is not null)
            {
                return exactDefinition;
            }

            throw new InvalidOperationException(
                "The selected patch definition changed after preview and can no longer be prepared safely.");
        }

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

    private static YamlNode CloneExpandedNode(
        YamlNode node,
        ISet<YamlNode> activePath)
    {
        if (!activePath.Add(node))
        {
            throw new InvalidOperationException(
                "Selected patch contains a recursive YAML alias.");
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
                            CloneExpandedNode(
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
                            CloneExpandedNode(
                                key,
                                activePath),
                            CloneExpandedNode(
                                value,
                                activePath));
                    }

                    return clone;
                }

                default:
                    throw new InvalidOperationException(
                        "Unsupported YAML node in patch selection.");
            }
        }
        finally
        {
            activePath.Remove(node);
        }
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
        string? PatVer,
        string? DefPrint);

    private sealed record Resolved(
        SelKey Key,
        Mdl.PatMatch Match,
        SrcCand Candidate,
        YamlMappingNode ExpandedDetails);

    private sealed record StaticWrite(
        string Title,
        ulong Offset,
        byte[] Bytes);
}
