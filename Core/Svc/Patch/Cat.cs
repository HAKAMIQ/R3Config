using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class PatchCatalogReader
{
    private const string SupportedPatchEngineVersion = "1.2";
    private const string DuplicateKeySuffix = "__r3config_duplicate_";

    private static readonly Regex HashKeyPattern = new(
        @"^(?:(?<serial>[A-Z0-9]{9})-)?(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SerialPattern = new(
        @"^[A-Za-z0-9]{9}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AppVersionPattern = new(
        @"^[0-9]{2}\.[0-9]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SupportedPatchTypes = new(
        [
            "alloc",
            "calloc",
            "jump",
            "jumpl",
            "jumpf",
            "load",
            "byte",
            "le16",
            "le32",
            "le64",
            "bef32",
            "bef64",
            "be16",
            "be32",
            "bd32",
            "be64",
            "bd64",
            "lef32",
            "lef64",
            "bpex",
            "utf8",
            "cutf8",
            "move_file",
            "hide_file"
        ],
        StringComparer.Ordinal);

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
            if (new FileInfo(patchYmlPath).Length >
                Rpcs3InputLimits.PatchCatalogYamlBytes)
            {
                return PatchCatalogReadResult.Failure(
                    $"Patch catalog exceeds the {Rpcs3InputLimits.PatchCatalogYamlBytes}-byte input limit.");
            }

            using var stream = File.OpenRead(patchYmlPath);
            return Read(stream, source, patchYmlPath);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
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
            var text = Rpcs3BoundedInput.ReadUtf8Text(
                patchYmlStream,
                Rpcs3InputLimits.PatchCatalogYamlBytes,
                "Patch catalog");
            return ReadText(text, source, sourcePath);
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                InvalidDataException)
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
            Rpcs3BoundedInput.EnsureTextLength(
                patchYmlText,
                Rpcs3InputLimits.PatchCatalogYamlBytes,
                "Patch catalog");

            var yaml = new YamlStream();
            yaml.Load(new StringReader(NormalizeDuplicatePatchKeys(
                patchYmlText,
                allowDuplicatePatchTitles: source == PatchSourceOrigin.BundledCheats)));

            if (yaml.Documents.Count == 0 ||
                yaml.Documents.Count > Rpcs3InputLimits.YamlDocuments ||
                yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return PatchCatalogReadResult.Failure("Patch YAML root is not a mapping.");
            }

            var versionNode = TryGetNode(root, "Version");
            if (versionNode is not YamlScalarNode versionScalar ||
                string.IsNullOrWhiteSpace(versionScalar.Value))
            {
                return PatchCatalogReadResult.Failure(
                    $"Patch YAML is missing the required Version: {SupportedPatchEngineVersion} entry.");
            }

            var version = versionScalar.Value.Trim();
            if (!version.Equals(SupportedPatchEngineVersion, StringComparison.Ordinal))
            {
                return PatchCatalogReadResult.Failure(
                    $"Patch YAML version '{version}' does not match RPCS3 patch engine version '{SupportedPatchEngineVersion}'.");
            }

            var entries = new List<Rpcs3PatchEntry>();
            var diagnostics = new List<string>();

            foreach (var (keyNode, valueNode) in root.Children)
            {
                var rawKey = ScalarValue(keyNode);
                if (rawKey.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
                    rawKey.StartsWith("Anchors", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var hashKey = DecodeDuplicateTopLevelHashKey(rawKey);
                var match = HashKeyPattern.Match(hashKey);
                if (!match.Success)
                {
                    continue;
                }

                if (!Rpcs3PatchRuntimeKey.TryParse(
                        hashKey,
                        out var runtimeKey))
                {
                    diagnostics.Add(
                        $"Skipping '{hashKey}': the key is not generated by RPCS3. PPU/SPU/OVL require a non-zero 40-hex SHA-1; PRX requires a 28-character RPCS3 Base57 SHA-1 followed by a segment number.");
                    continue;
                }

                if (valueNode is not YamlMappingNode patchMap)
                {
                    diagnostics.Add($"Skipping '{hashKey}': expected a patch mapping.");
                    continue;
                }

                var hashKind = runtimeKey.Kind;
                var hash = runtimeKey.Hash;
                if (string.IsNullOrWhiteSpace(hash))
                {
                    diagnostics.Add($"Skipping '{hashKey}': hash value is empty.");
                    continue;
                }

                foreach (var entry in ParseHashSection(
                             patchMap,
                             hashKind,
                             hash,
                             runtimeKey.SerialScope,
                             source,
                             sourcePath,
                             diagnostics))
                {
                    entries.Add(entry);
                }
            }

            return PatchCatalogReadResult.Success(
                new Rpcs3PatchCatalog(version, entries),
                CombineDiagnostics(diagnostics));
        }
        catch (YamlException ex)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return PatchCatalogReadResult.Failure(ex.Message);
        }
    }

    private static IEnumerable<Rpcs3PatchEntry> ParseHashSection(
        YamlMappingNode patchMap,
        PatchHashKind hashKind,
        string hash,
        string? runtimeKeySerial,
        PatchSourceOrigin source,
        string? sourcePath,
        ICollection<string> diagnostics)
    {
        foreach (var (titleNode, detailsNode) in patchMap.Children)
        {
            var title = DecodeDuplicateMappingKey(ScalarValue(titleNode));
            if (string.IsNullOrWhiteSpace(title))
            {
                diagnostics.Add($"Skipping {hashKind}-{hash}: patch title is empty.");
                continue;
            }

            if (detailsNode is not YamlMappingNode details)
            {
                diagnostics.Add($"Skipping '{title}': expected a patch mapping.");
                continue;
            }

            if (!TryParseGameTargets(
                    TryGetNode(details, "Games"),
                    title,
                    diagnostics,
                    out var targets))
            {
                continue;
            }

            var patchNode = TryGetNode(details, "Patch");
            if (patchNode is not null &&
                !ValidatePatchNode(patchNode, title, diagnostics))
            {
                continue;
            }

            if (!TryParseDependencies(
                    TryGetNode(details, "Dependencies") ?? TryGetNode(details, "Depends On"),
                    title,
                    diagnostics,
                    out var dependencies))
            {
                continue;
            }

            var notes = NodeText(TryGetNode(details, "Notes"));
            var author = NodeText(TryGetNode(details, "Author"));
            var patchVersion = NodeText(TryGetNode(details, "Patch Version"));
            var group = NodeText(TryGetNode(details, "Group"));
            var risk = source is PatchSourceOrigin.BundledCheats or PatchSourceOrigin.ManagedCheats
                ? PatchRisk.Cheat
                : PatchRiskPolicy.Classify(
                    title,
                    group,
                    notes,
                    author,
                    source == PatchSourceOrigin.BundledCanary);
            var patchType = PatchRiskPolicy.InferPatchType(title, group, risk);

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
                SourcePath: sourcePath,
                Dependencies: dependencies,
                DefinitionFingerprint: ComputeDefinitionFingerprint(patchNode),
                RuntimeKeySerial: runtimeKeySerial);
        }
    }

    private static bool TryParseDependencies(
        YamlNode? dependenciesNode,
        string patchTitle,
        ICollection<string> diagnostics,
        out IReadOnlyList<string> dependencies)
    {
        dependencies = [];
        if (dependenciesNode is null)
        {
            return true;
        }

        IEnumerable<YamlNode> nodes = dependenciesNode switch
        {
            YamlScalarNode => [dependenciesNode],
            YamlSequenceNode sequence => sequence.Children,
            _ => []
        };

        if (dependenciesNode is not YamlScalarNode and not YamlSequenceNode)
        {
            diagnostics.Add($"Skipping '{patchTitle}': Dependencies must be a scalar or YAML sequence.");
            return false;
        }

        var parsed = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add($"Skipping '{patchTitle}': Dependencies contains an empty or non-scalar patch title.");
                return false;
            }

            var dependency = scalar.Value.Trim();
            if (dependency.Equals(patchTitle, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Skipping '{patchTitle}': a patch cannot depend on itself.");
                return false;
            }

            if (seen.Add(dependency))
            {
                parsed.Add(dependency);
            }
        }

        dependencies = parsed
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return true;
    }

    private static string? ComputeDefinitionFingerprint(YamlNode? patchNode)
    {
        if (patchNode is null)
        {
            return null;
        }

        var canonical = new StringBuilder();
        AppendCanonicalNode(canonical, patchNode);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendCanonicalNode(StringBuilder output, YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
            {
                var value = scalar.Value ?? string.Empty;
                output.Append('S')
                    .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
                break;
            }
            case YamlSequenceNode sequence:
                output.Append("Q[");
                foreach (var child in sequence.Children)
                {
                    AppendCanonicalNode(output, child);
                    output.Append(';');
                }

                output.Append(']');
                break;
            case YamlMappingNode mapping:
            {
                output.Append("M{");
                var pairs = mapping.Children
                    .Select(pair => new
                    {
                        Key = CanonicalText(pair.Key),
                        Value = CanonicalText(pair.Value)
                    })
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Value, StringComparer.Ordinal);

                foreach (var pair in pairs)
                {
                    output.Append(pair.Key.Length.ToString(CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(pair.Key)
                        .Append('=')
                        .Append(pair.Value.Length.ToString(CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(pair.Value)
                        .Append(';');
                }

                output.Append('}');
                break;
            }
            default:
            {
                var value = node.ToString() ?? string.Empty;
                output.Append('N')
                    .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
                break;
            }
        }
    }

    private static string CanonicalText(YamlNode node)
    {
        var output = new StringBuilder();
        AppendCanonicalNode(output, node);
        return output.ToString();
    }

    private static bool TryParseGameTargets(
        YamlNode? gamesNode,
        string patchTitle,
        ICollection<string> diagnostics,
        out IReadOnlyList<Rpcs3PatchGameTarget> targets)
    {
        targets = [];

        if (gamesNode is null)
        {
            return true;
        }

        if (gamesNode is not YamlMappingNode games)
        {
            diagnostics.Add($"Skipping '{patchTitle}': Games must be a mapping.");
            return false;
        }

        var parsedTargets = new List<Rpcs3PatchGameTarget>();

        foreach (var (gameTitleNode, serialsNode) in games.Children)
        {
            var gameTitle = DecodeDuplicateMappingKey(ScalarValue(gameTitleNode));
            if (string.IsNullOrWhiteSpace(gameTitle))
            {
                diagnostics.Add($"Skipping an empty game title in '{patchTitle}'.");
                continue;
            }

            if (serialsNode is not YamlMappingNode serials)
            {
                diagnostics.Add($"Skipping game '{gameTitle}' in '{patchTitle}': expected a serial mapping.");
                continue;
            }

            var titleIsAll = gameTitle.Equals(Rpcs3PatchVersion.All, StringComparison.Ordinal);

            foreach (var (serialNode, versionsNode) in serials.Children)
            {
                var serial = DecodeDuplicateMappingKey(ScalarValue(serialNode));
                if (!TryValidateSerial(gameTitle, titleIsAll, serial, patchTitle, diagnostics))
                {
                    continue;
                }

                var versions = ParseVersions(
                    versionsNode,
                    gameTitle,
                    serial,
                    patchTitle,
                    diagnostics);
                if (versions.Count == 0)
                {
                    continue;
                }

                parsedTargets.Add(new Rpcs3PatchGameTarget(gameTitle, serial, versions));
            }
        }

        targets = parsedTargets
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

        if (targets.Count == 0)
        {
            diagnostics.Add($"Skipping '{patchTitle}': Games does not contain any valid RPCS3 targets.");
            return false;
        }

        return true;
    }

    private static bool TryValidateSerial(
        string gameTitle,
        bool titleIsAll,
        string serial,
        string patchTitle,
        ICollection<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            diagnostics.Add($"Skipping an empty serial in '{patchTitle}'.");
            return false;
        }

        var serialIsAll = serial.Equals(Rpcs3PatchVersion.All, StringComparison.Ordinal);
        if (serialIsAll && !titleIsAll)
        {
            diagnostics.Add(
                $"Skipping '{gameTitle}/{serial}' in '{patchTitle}': serial All is only valid under title All.");
            return false;
        }

        if (titleIsAll && !serialIsAll)
        {
            diagnostics.Add(
                $"Skipping '{gameTitle}/{serial}' in '{patchTitle}': title All requires serial All.");
            return false;
        }

        if (!serialIsAll && !SerialPattern.IsMatch(serial))
        {
            diagnostics.Add(
                $"Skipping serial '{serial}' in '{patchTitle}': RPCS3 requires exactly nine alphanumeric characters.");
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> ParseVersions(
        YamlNode versionsNode,
        string gameTitle,
        string serial,
        string patchTitle,
        ICollection<string> diagnostics)
    {
        if (versionsNode is not YamlSequenceNode sequence)
        {
            diagnostics.Add(
                $"Skipping '{gameTitle}/{serial}' in '{patchTitle}': versions must be a YAML sequence.");
            return [];
        }

        var versions = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var versionNode in sequence.Children)
        {
            if (versionNode is not YamlScalarNode scalar ||
                string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add(
                    $"Skipping an invalid app version in '{patchTitle}' for '{gameTitle}/{serial}'.");
                continue;
            }

            var version = scalar.Value.Trim();
            if (!version.Equals(Rpcs3PatchVersion.All, StringComparison.Ordinal) &&
                !AppVersionPattern.IsMatch(version))
            {
                diagnostics.Add(
                    $"Skipping app version '{version}' in '{patchTitle}': expected NN.NN or All.");
                continue;
            }

            if (!seen.Add(version))
            {
                diagnostics.Add(
                    $"Skipping duplicate app version '{version}' in '{patchTitle}' for '{gameTitle}/{serial}'.");
                continue;
            }

            versions.Add(version);
        }

        if (versions.Count == 0)
        {
            diagnostics.Add(
                $"Skipping '{gameTitle}/{serial}' in '{patchTitle}': the version sequence is empty.");
        }

        return versions
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ValidatePatchNode(
        YamlNode patchNode,
        string patchTitle,
        ICollection<string> diagnostics)
    {
        if (patchNode is not YamlSequenceNode patchSequence)
        {
            diagnostics.Add($"Skipping '{patchTitle}': Patch must be a YAML sequence.");
            return false;
        }

        var isValid = true;
        foreach (var instructionNode in patchSequence.Children)
        {
            if (!ValidatePatchInstruction(instructionNode, patchTitle, diagnostics))
            {
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool ValidatePatchInstruction(
        YamlNode instructionNode,
        string patchTitle,
        ICollection<string> diagnostics)
    {
        if (instructionNode is not YamlSequenceNode instruction ||
            instruction.Children.Count == 0 ||
            instruction.Children[0] is not YamlScalarNode typeNode ||
            string.IsNullOrWhiteSpace(typeNode.Value))
        {
            diagnostics.Add($"Skipping '{patchTitle}': patch instruction must be a sequence with a valid type.");
            return false;
        }

        var type = typeNode.Value.Trim();
        if (!SupportedPatchTypes.Contains(type))
        {
            diagnostics.Add($"Skipping '{patchTitle}': patch instruction type '{type}' is not supported by RPCS3.");
            return false;
        }

        if (type.Equals("load", StringComparison.Ordinal))
        {
            if (instruction.Children.Count < 2 ||
                instruction.Children[1] is not YamlSequenceNode loadedSequence)
            {
                diagnostics.Add($"Skipping '{patchTitle}': load requires a resolved instruction sequence.");
                return false;
            }

            if (instruction.Children.Count > 2 &&
                instruction.Children[2] is not YamlScalarNode)
            {
                diagnostics.Add($"Skipping '{patchTitle}': load address modifier must be a scalar.");
                return false;
            }

            var isValid = true;
            foreach (var loadedInstruction in loadedSequence.Children)
            {
                if (!ValidatePatchInstruction(loadedInstruction, patchTitle, diagnostics))
                {
                    isValid = false;
                }
            }

            return isValid;
        }

        if (instruction.Children.Count < 3 ||
            instruction.Children[1] is not YamlScalarNode addressNode ||
            instruction.Children[2] is not YamlScalarNode valueNode ||
            string.IsNullOrWhiteSpace(addressNode.Value) ||
            valueNode.Value is null)
        {
            diagnostics.Add($"Skipping '{patchTitle}': patch instruction '{type}' requires scalar address and value fields.");
            return false;
        }

        if (UsesHexOffset(type) &&
            !addressNode.Value.Trim().StartsWith("0x", StringComparison.Ordinal))
        {
            diagnostics.Add($"Skipping '{patchTitle}': patch instruction '{type}' requires a hexadecimal 0x address.");
            return false;
        }

        return true;
    }

    private static bool UsesHexOffset(string patchType)
    {
        return !patchType.Equals("load", StringComparison.Ordinal) &&
               !patchType.Equals("move_file", StringComparison.Ordinal) &&
               !patchType.Equals("hide_file", StringComparison.Ordinal);
    }

    private static string? CombineDiagnostics(IReadOnlyCollection<string> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return null;
        }

        const int maxVisibleDiagnostics = 8;
        var uniqueDiagnostics = diagnostics
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var visible = uniqueDiagnostics
            .Take(maxVisibleDiagnostics)
            .ToArray();
        var remaining = uniqueDiagnostics.Length - visible.Length;
        var text = string.Join(Environment.NewLine, visible);

        return remaining > 0
            ? $"{text}{Environment.NewLine}Additional RPCS3 contract warnings: {remaining}."
            : text;
    }

    private static bool IsSupportedSerial(string value)
    {
        return value.Equals(Rpcs3PatchVersion.All, StringComparison.Ordinal) ||
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

    private static string NormalizeDuplicatePatchKeys(string yaml, bool allowDuplicatePatchTitles = false)
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
            if (count > 1 && ShouldNormalizeDuplicateKey(key, indent, scope, allowDuplicatePatchTitles))
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

    public static string NormalizeDuplicateKeysForRead(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return NormalizeDuplicatePatchKeys(yaml, allowDuplicatePatchTitles: true);
    }

    public static string DecodeNormalizedMappingKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return DecodeDuplicateMappingKey(key);
    }

    public static string DecodeNormalizedHashKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return DecodeDuplicateTopLevelHashKey(key);
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

    private static bool ShouldNormalizeDuplicateKey(
        string key,
        int indent,
        IReadOnlyList<MappingScope> scope,
        bool allowDuplicatePatchTitles)
    {
        if (indent == 0)
        {
            return key.Equals("Anchors", StringComparison.OrdinalIgnoreCase) ||
                   HashKeyPattern.IsMatch(key);
        }

        // Community patch expansions are commonly assembled by concatenating
        // sources. Some contain the same patch title twice inside one hash
        // section. YamlDotNet must be allowed to load the document so the merge
        // policy can keep one deterministically and report the duplicate.
        if (allowDuplicatePatchTitles &&
            indent == 2 &&
            scope.Count > 0 &&
            HashKeyPattern.IsMatch(DecodeDuplicateTopLevelHashKey(scope[0].Key)))
        {
            return true;
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
