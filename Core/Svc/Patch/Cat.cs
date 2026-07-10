using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class PatchCatalogReader
{
    private const string DuplicateKeySuffix = "__r3config_duplicate_";

    private static readonly Regex HashKeyPattern = new(
        @"^(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SerialPattern = new(
        @"^[A-Z]{4}\d{5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MappingKeyPattern = new(
        @"^(?<spaces>\s*)(?:(?:\""(?<doubleQuoted>(?:[^\""\\]|\\.)*)\"")|(?:'(?<singleQuoted>[^']*)')|(?<plain>[^\s:#][^:#]*?))\s*:(?:\s.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public PatchCatalogReadResult Read(string patchYmlPath, bool isCanarySource = false)
    {
        return Read(
            patchYmlPath,
            isCanarySource ? PatchSourceOrigin.BundledCanary : InferSource(patchYmlPath));
    }

    public PatchCatalogReadResult Read(string patchYmlPath, PatchSourceOrigin source)
    {
        if (!File.Exists(patchYmlPath))
        {
            return PatchCatalogReadResult.Failure($"Patch file not found: {patchYmlPath}");
        }

        try
        {
            using var stream = File.OpenRead(patchYmlPath);
            return Read(stream, source, patchYmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
    }

    public PatchCatalogReadResult Read(Stream patchYmlStream, bool isCanarySource = false)
    {
        return Read(
            patchYmlStream,
            isCanarySource ? PatchSourceOrigin.BundledCanary : PatchSourceOrigin.Unknown,
            sourcePath: null);
    }

    public PatchCatalogReadResult Read(Stream patchYmlStream, PatchSourceOrigin source, string? sourcePath = null)
    {
        try
        {
            using var reader = new StreamReader(patchYmlStream, leaveOpen: true);
            return ReadText(reader.ReadToEnd(), source, sourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
    }

    public PatchCatalogReadResult ReadText(string patchYmlText, bool isCanarySource = false)
    {
        return ReadText(
            patchYmlText,
            isCanarySource ? PatchSourceOrigin.BundledCanary : PatchSourceOrigin.Unknown,
            sourcePath: null);
    }

    public PatchCatalogReadResult ReadText(
        string patchYmlText,
        PatchSourceOrigin source,
        string? sourcePath = null)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(NormalizeDuplicatePatchKeys(patchYmlText)));

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return PatchCatalogReadResult.Failure("Patch YAML root is not a mapping.");
            }

            var version = TryGetScalar(root, "Version");
            var entries = new List<Rpcs3PatchEntry>();

            foreach (var (keyNode, valueNode) in root.Children)
            {
                var hashKey = DecodeDuplicateTopLevelHashKey(ScalarValue(keyNode));
                var match = HashKeyPattern.Match(hashKey);
                if (!match.Success || valueNode is not YamlMappingNode patchMap)
                {
                    continue;
                }

                var hashKind = ParseHashKind(match.Groups["kind"].Value);
                var hash = match.Groups["hash"].Value.Trim();
                foreach (var entry in ParseHashSection(patchMap, hashKind, hash, source, sourcePath))
                {
                    entries.Add(entry);
                }
            }

            return PatchCatalogReadResult.Success(new Rpcs3PatchCatalog(version, entries));
        }
        catch (YamlException ex)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
    }

    private static IEnumerable<Rpcs3PatchEntry> ParseHashSection(
        YamlMappingNode patchMap,
        PatchHashKind hashKind,
        string hash,
        PatchSourceOrigin source,
        string? sourcePath)
    {
        foreach (var (titleNode, detailsNode) in patchMap.Children)
        {
            if (detailsNode is not YamlMappingNode details)
            {
                continue;
            }

            if (!HasPatchShape(details))
            {
                continue;
            }

            var title = ScalarValue(titleNode);
            var notes = NodeText(TryGetNode(details, "Notes"));
            var author = NodeText(TryGetNode(details, "Author"));
            var patchVersion = NodeText(TryGetNode(details, "Patch Version"));
            var group = NodeText(TryGetNode(details, "Group"));
            var risk = PatchRiskPolicy.Classify(
                title,
                group,
                notes,
                author,
                source == PatchSourceOrigin.BundledCanary);
            var patchType = PatchRiskPolicy.InferPatchType(title, group, risk);
            var targets = ParseGameTargets(TryGetNode(details, "Games"));

            yield return new Rpcs3PatchEntry(
                Title: title,
                HashKind: hashKind,
                Hash: hash,
                GameTargets: targets,
                PatchType: patchType,
                Risk: risk,
                Notes: notes,
                Author: author,
                PatchVersion: patchVersion,
                Group: group,
                Source: source,
                SourcePath: sourcePath);
        }
    }

    private static IReadOnlyList<Rpcs3PatchGameTarget> ParseGameTargets(YamlNode? gamesNode)
    {
        var targets = new List<Rpcs3PatchGameTarget>();
        if (gamesNode is not YamlMappingNode games)
        {
            return targets;
        }

        foreach (var (gameTitleNode, serialsNode) in games.Children)
        {
            var gameTitle = DecodeDuplicateMappingKey(ScalarValue(gameTitleNode));
            if (serialsNode is YamlMappingNode serials)
            {
                foreach (var (serialNode, versionNode) in serials.Children)
                {
                    var serial = DecodeDuplicateMappingKey(ScalarValue(serialNode));
                    if (!IsSupportedSerial(serial))
                    {
                        continue;
                    }

                    targets.Add(new Rpcs3PatchGameTarget(gameTitle, serial, ParseVersions(versionNode)));
                }
            }
            else
            {
                var serial = DecodeDuplicateMappingKey(ScalarValue(gameTitleNode));
                if (IsSupportedSerial(serial))
                {
                    targets.Add(new Rpcs3PatchGameTarget(serial, serial, ParseVersions(serialsNode)));
                }
            }
        }

        return targets
            .GroupBy(target => $"{target.Title}\0{target.Serial}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new Rpcs3PatchGameTarget(
                group.First().Title,
                group.First().Serial,
                group.SelectMany(target => target.Versions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(target => target.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.Serial, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseVersions(YamlNode versionNode)
    {
        if (versionNode is YamlSequenceNode sequence)
        {
            return sequence.Children
                .Select(ScalarValue)
                .Select(Rpcs3PatchVersion.Normalize)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var scalar = ScalarValue(versionNode);
        if (string.IsNullOrWhiteSpace(scalar))
        {
            return [];
        }

        return [Rpcs3PatchVersion.Normalize(scalar)!];
    }

    private static bool HasPatchShape(YamlMappingNode details)
    {
        return TryGetNode(details, "Patch") is not null ||
               TryGetNode(details, "Games") is not null ||
               TryGetNode(details, "Patch Version") is not null;
    }

    private static PatchHashKind ParseHashKind(string value)
    {
        if (value.Equals("PPU", StringComparison.OrdinalIgnoreCase))
        {
            return PatchHashKind.Ppu;
        }

        if (value.Equals("SPU", StringComparison.OrdinalIgnoreCase))
        {
            return PatchHashKind.Spu;
        }

        if (value.Equals("PRX", StringComparison.OrdinalIgnoreCase))
        {
            return PatchHashKind.Prx;
        }

        if (value.Equals("OVL", StringComparison.OrdinalIgnoreCase))
        {
            return PatchHashKind.Ovl;
        }

        return PatchHashKind.Unknown;
    }

    private static bool IsSupportedSerial(string value)
    {
        return value.Equals(Rpcs3PatchVersion.All, StringComparison.OrdinalIgnoreCase) ||
               SerialPattern.IsMatch(value);
    }

    private static PatchSourceOrigin InferSource(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Equals("patch.yml", StringComparison.OrdinalIgnoreCase))
        {
            return PatchSourceOrigin.Official;
        }

        if (fileName.Equals("imported_patch.yml", StringComparison.OrdinalIgnoreCase))
        {
            return PatchSourceOrigin.Imported;
        }

        if (fileName.EndsWith("_patch.yml", StringComparison.OrdinalIgnoreCase))
        {
            return PatchSourceOrigin.TitleSpecific;
        }

        return PatchSourceOrigin.Unknown;
    }

    private static YamlNode? TryGetNode(YamlMappingNode mapping, string key)
    {
        foreach (var (candidateKey, value) in mapping.Children)
        {
            if (ScalarValue(candidateKey).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryGetScalar(YamlMappingNode mapping, string key)
    {
        var value = TryGetNode(mapping, key);
        var scalar = value is null ? null : ScalarValue(value);
        return string.IsNullOrWhiteSpace(scalar) ? null : scalar;
    }

    private static string? NodeText(YamlNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is YamlScalarNode scalar)
        {
            return string.IsNullOrWhiteSpace(scalar.Value) ? null : scalar.Value.Trim();
        }

        if (node is YamlSequenceNode sequence)
        {
            var parts = sequence.Children
                .Select(NodeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return parts.Length == 0 ? null : string.Join("; ", parts);
        }

        return null;
    }

    private static string ScalarValue(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            return scalar.Value?.Trim() ?? string.Empty;
        }

        return node.ToString()?.Trim() ?? string.Empty;
    }

    private static string NormalizeDuplicatePatchKeys(string yaml)
    {
        using var source = new StringReader(yaml);
        using var normalized = new StringWriter(CultureInfo.InvariantCulture);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scope = new List<MappingScope>();

        while (source.ReadLine() is { } line)
        {
            if (!TryReadMappingKey(line, out var indent, out var key))
            {
                normalized.WriteLine(line);
                continue;
            }

            while (scope.Count > 0 && scope[^1].Indent >= indent)
            {
                scope.RemoveAt(scope.Count - 1);
            }

            var parentPath = string.Join("/", scope.Select(item => item.Key));
            var countKey = string.Concat(parentPath, "\0", indent.ToString(CultureInfo.InvariantCulture), "\0", key);
            var count = counts.TryGetValue(countKey, out var currentCount)
                ? currentCount + 1
                : 1;
            counts[countKey] = count;

            var normalizedKey = key;
            if (count > 1 && ShouldNormalizeDuplicateKey(key, indent, scope))
            {
                normalizedKey = key.Equals("Anchors", StringComparison.OrdinalIgnoreCase)
                    ? $"Anchors_{count.ToString(CultureInfo.InvariantCulture)}"
                    : key + DuplicateKeySuffix + count.ToString(CultureInfo.InvariantCulture);
                line = RewriteMappingKey(line, key, normalizedKey);
            }

            normalized.WriteLine(line);
            scope.Add(new MappingScope(indent, normalizedKey));
        }

        return normalized.ToString();
    }

    private static bool TryReadMappingKey(string line, out int indent, out string key)
    {
        indent = 0;
        key = string.Empty;

        if (string.IsNullOrWhiteSpace(line) ||
            line.TrimStart().StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        var match = MappingKeyPattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        indent = match.Groups["spaces"].Value.Length;
        if (match.Groups["doubleQuoted"].Success)
        {
            key = match.Groups["doubleQuoted"].Value.Trim();
        }
        else if (match.Groups["singleQuoted"].Success)
        {
            key = match.Groups["singleQuoted"].Value.Trim();
        }
        else
        {
            key = match.Groups["plain"].Value.Trim();
        }

        return !string.IsNullOrWhiteSpace(key);
    }

    private static bool ShouldNormalizeDuplicateKey(string key, int indent, IReadOnlyList<MappingScope> scope)
    {
        if (indent == 0)
        {
            return key.Equals("Anchors", StringComparison.OrdinalIgnoreCase) ||
                   HashKeyPattern.IsMatch(key);
        }

        if (scope.Count > 0 && scope[0].Key.StartsWith("Anchors", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsPatchMetadataKey(key) || IsGameSerialKey(key))
        {
            return true;
        }

        return scope.Count > 0 &&
               scope[^1].Key.Equals("Games", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPatchMetadataKey(string key)
    {
        return key.Equals("Games", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Author", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Notes", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Patch Version", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Group", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Configurable Values", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("Patch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameSerialKey(string key)
    {
        return SerialPattern.IsMatch(key) ||
               key.Equals("All", StringComparison.OrdinalIgnoreCase);
    }

    private static string RewriteMappingKey(string line, string originalKey, string normalizedKey)
    {
        var keyIndex = line.IndexOf(originalKey, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return line;
        }

        return line[..keyIndex] + normalizedKey + line[(keyIndex + originalKey.Length)..];
    }

    private static string DecodeDuplicateTopLevelHashKey(string key)
    {
        var originalKey = DecodeDuplicateMappingKey(key);
        if (!string.Equals(originalKey, key, StringComparison.Ordinal) &&
            HashKeyPattern.IsMatch(originalKey))
        {
            return originalKey;
        }

        return key;
    }

    private static string DecodeDuplicateMappingKey(string key)
    {
        var markerIndex = key.LastIndexOf(DuplicateKeySuffix, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return key;
        }

        var suffix = key[(markerIndex + DuplicateKeySuffix.Length)..];
        var originalKey = key[..markerIndex];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? originalKey
            : key;
    }

    private sealed record MappingScope(int Indent, string Key);
}
