using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class HCanaryCatalogService
{
    private const string SupportedVersion = "1.2";
    private const string DuplicateKeySuffix = "__r3config_duplicate_";

    private static readonly Regex HashKeyPattern = new(
        @"^(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PossibleModuleKeyPattern = new(
        @"^[A-Z]{2,8}-",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WordPattern = new(
        @"[\p{L}\p{Nd}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VersionLinePattern = new(
        @"^Version\s*:\s*(?<value>[^#\s]+)(?:\s*#.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> KnownInstructionKinds = new(
        [
            "alloc", "calloc", "jump", "jumpl", "jumpf", "load", "byte", "be8",
            "le16", "le32", "le64", "bef32", "bef64", "be16", "be32", "bd32",
            "be64", "bd64", "lef32", "lef64", "bpex", "utf8", "cutf8",
            "move_file", "hide_file"
        ],
        StringComparer.Ordinal);

    private static readonly HashSet<string> AutomaticKinds = new(
        ["byte", "be8", "be16", "be32", "be64", "bef32", "bef64"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> CodeCaveKinds = new(
        ["alloc", "calloc", "jump", "jumpl", "jumpf"],
        StringComparer.Ordinal);

    private readonly PatchCatalogReader _catalogReader = new();

    public CanaryCatalogResult Read(string path)
    {
        if (!File.Exists(path))
        {
            return Failure("HCAT001", $"Canary catalog was not found: {path}");
        }

        try
        {
            return ReadText(File.ReadAllText(path), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure("HCAT002", ex.Message);
        }
    }

    public CanaryCatalogResult ReadText(string text, string? sourcePath = null)
    {
        var diagnostics = new List<CanaryCatalogDiagnostic>();
        if (!TryNormalizeVersionHeaders(text, out var normalizedText, out var duplicateVersions, out var versionError))
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT006",
                versionError));
            return new CanaryCatalogResult(
                false,
                new CanaryCatalog(null, [], diagnostics, 0, 0));
        }

        if (duplicateVersions > 0)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Warning,
                "HCAT018",
                $"Normalized {duplicateVersions} duplicate top-level Version: {SupportedVersion} header(s) before catalog parsing."));
        }

        var baseline = _catalogReader.ReadText(
            normalizedText,
            PatchSourceOrigin.BundledCanary,
            sourcePath);

        if (!baseline.Succeeded)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT003",
                $"Existing patch catalog validation failed: {baseline.Error}"));
            return new CanaryCatalogResult(
                false,
                new CanaryCatalog(null, [], diagnostics, 0, 0));
        }

        if (!string.IsNullOrWhiteSpace(baseline.Error))
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Warning,
                "HCAT004",
                $"Existing patch catalog diagnostics: {baseline.Error}"));
        }

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(PatchCatalogReader.NormalizeDuplicateKeysForRead(normalizedText)));

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                diagnostics.Add(new CanaryCatalogDiagnostic(
                    CanaryDiagnosticSeverity.Error,
                    "HCAT005",
                    "Canary YAML must contain exactly one mapping document."));
                return new CanaryCatalogResult(
                    false,
                    new CanaryCatalog(null, [], diagnostics, 0, 0));
            }

            var version = Scalar(TryGetNode(root, "Version"));
            if (!string.Equals(version, SupportedVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(new CanaryCatalogDiagnostic(
                    CanaryDiagnosticSeverity.Error,
                    "HCAT006",
                    $"Canary YAML requires Version: {SupportedVersion}; found '{version ?? "<missing>"}'."));
                return new CanaryCatalogResult(
                    false,
                    new CanaryCatalog(version, [], diagnostics, 0, CountAnchors(root)));
            }

            var baselineEntries = BuildBaselineQueues(baseline.Catalog.Entries);
            var definitions = new List<CanaryPatchDefinition>();
            var rejected = 0;

            foreach (var (keyNode, valueNode) in root.Children)
            {
                var encodedKey = Scalar(keyNode) ?? string.Empty;
                var rawKey = DecodeDuplicateKey(encodedKey);
                var hashMatch = HashKeyPattern.Match(rawKey);

                if (!hashMatch.Success)
                {
                    if (PossibleModuleKeyPattern.IsMatch(rawKey))
                    {
                        var rejectedHere = CountDefinitions(valueNode);
                        rejected += rejectedHere;
                        diagnostics.Add(new CanaryCatalogDiagnostic(
                            CanaryDiagnosticSeverity.Error,
                            "HCAT007",
                            $"Unsupported Canary module key '{rawKey}' rejected with {rejectedHere} definition(s).",
                            rawKey));
                    }

                    continue;
                }

                var moduleKind = ParseModuleKind(hashMatch.Groups["kind"].Value);
                var moduleHash = hashMatch.Groups["hash"].Value.Trim();
                var moduleKey = $"{hashMatch.Groups["kind"].Value.ToUpperInvariant()}-{moduleHash}";
                string? normalizedHash = null;

                if (moduleKind is PatchHashKind.Ppu or PatchHashKind.Spu &&
                    !Rpcs3ExecutableHash.TryNormalize(moduleHash, out normalizedHash))
                {
                    var rejectedHere = CountDefinitions(valueNode);
                    rejected += rejectedHere;
                    diagnostics.Add(new CanaryCatalogDiagnostic(
                        CanaryDiagnosticSeverity.Error,
                        "HCAT008",
                        $"{moduleKey} is not an exact 40-hex or valid RPCS3 Base57 executable hash; {rejectedHere} definition(s) rejected.",
                        moduleKey));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(moduleHash) || valueNode is not YamlMappingNode patchMap)
                {
                    rejected++;
                    diagnostics.Add(new CanaryCatalogDiagnostic(
                        CanaryDiagnosticSeverity.Error,
                        "HCAT009",
                        $"{moduleKey} must contain a non-empty hash and patch mapping.",
                        moduleKey));
                    continue;
                }

                foreach (var (titleNode, detailsNode) in patchMap.Children)
                {
                    var title = DecodeDuplicateKey(Scalar(titleNode) ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title) || detailsNode is not YamlMappingNode details)
                    {
                        rejected++;
                        diagnostics.Add(new CanaryCatalogDiagnostic(
                            CanaryDiagnosticSeverity.Error,
                            "HCAT010",
                            $"{moduleKey} contains an empty title or non-mapping patch definition.",
                            moduleKey,
                            string.IsNullOrWhiteSpace(title) ? null : title));
                        continue;
                    }

                    var baselineKey = BaselineKey(moduleKind, moduleHash, title);
                    if (!baselineEntries.TryGetValue(baselineKey, out var queue) ||
                        queue.Count == 0 ||
                        queue.Peek().GameTargets.Count == 0)
                    {
                        rejected++;
                        diagnostics.Add(new CanaryCatalogDiagnostic(
                            CanaryDiagnosticSeverity.Error,
                            "HCAT011",
                            "Definition was rejected because the existing patch catalog could not validate a non-empty Games target mapping.",
                            moduleKey,
                            title));
                        continue;
                    }

                    var baselineEntry = queue.Dequeue();
                    var author = Scalar(TryGetNode(details, "Author"))?.Trim();
                    var patchVersion = Scalar(TryGetNode(details, "Patch Version"))?.Trim();
                    var patchNode = TryGetNode(details, "Patch");

                    if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(patchVersion))
                    {
                        rejected++;
                        diagnostics.Add(new CanaryCatalogDiagnostic(
                            CanaryDiagnosticSeverity.Error,
                            "HCAT012",
                            "Definition requires non-empty Author and Patch Version fields.",
                            moduleKey,
                            title));
                        continue;
                    }

                    if (patchNode is not YamlSequenceNode patchSequence || patchSequence.Children.Count == 0)
                    {
                        rejected++;
                        diagnostics.Add(new CanaryCatalogDiagnostic(
                            CanaryDiagnosticSeverity.Error,
                            "HCAT013",
                            "Definition requires a non-empty Patch instruction sequence.",
                            moduleKey,
                            title));
                        continue;
                    }

                    var anchors = new HashSet<string>(StringComparer.Ordinal);
                    AddAnchor(anchors, TryGetNode(details, "Games"));
                    AddAnchor(anchors, TryGetNode(details, "Author"));
                    AddAnchor(anchors, TryGetNode(details, "Notes"));
                    AddAnchor(anchors, TryGetNode(details, "Patch Version"));
                    AddAnchor(anchors, TryGetNode(details, "Configurable Values"));

                    if (!TryParseConfigValues(
                            TryGetNode(details, "Configurable Values"),
                            moduleKey,
                            title,
                            diagnostics,
                            out var configValues))
                    {
                        rejected++;
                        continue;
                    }

                    var instructions = new List<CanaryInstruction>();
                    var usedInstructionAnchor = false;
                    if (!TryParseInstructions(
                            patchSequence,
                            moduleKey,
                            title,
                            anchors,
                            instructions,
                            diagnostics,
                            ref usedInstructionAnchor))
                    {
                        rejected++;
                        continue;
                    }

                    var fingerprint = BuildInstructionFingerprint(instructions, configValues);
                    var (complexity, isCandidate, reason) = Classify(
                        moduleKind,
                        instructions,
                        configValues,
                        usedInstructionAnchor);

                    definitions.Add(new CanaryPatchDefinition(
                        title,
                        Normalize(title),
                        moduleKind,
                        moduleHash,
                        normalizedHash,
                        baselineEntry.GameTargets,
                        patchVersion,
                        author,
                        Scalar(TryGetNode(details, "Notes")),
                        Scalar(TryGetNode(details, "Group")),
                        anchors.Order(StringComparer.Ordinal).ToArray(),
                        configValues,
                        instructions,
                        fingerprint,
                        complexity,
                        isCandidate,
                        reason,
                        PatchSourceOrigin.BundledCanary,
                        sourcePath));
                }
            }

            var catalog = new CanaryCatalog(
                version,
                definitions
                    .OrderBy(item => item.NormalizedTitle, StringComparer.Ordinal)
                    .ThenBy(item => item.ModuleKind)
                    .ThenBy(item => item.ModuleHash, StringComparer.Ordinal)
                    .ThenBy(item => item.Title, StringComparer.Ordinal)
                    .ToArray(),
                diagnostics,
                rejected,
                CountAnchors(root));

            return new CanaryCatalogResult(true, catalog);
        }
        catch (YamlException ex)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT014",
                ex.Message));
            return new CanaryCatalogResult(
                false,
                new CanaryCatalog(null, [], diagnostics, 0, 0));
        }
        catch (InvalidOperationException ex)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT015",
                ex.Message));
            return new CanaryCatalogResult(
                false,
                new CanaryCatalog(null, [], diagnostics, 0, 0));
        }
    }

    private static bool TryParseInstructions(
        YamlSequenceNode sequence,
        string moduleKey,
        string title,
        ISet<string> anchors,
        ICollection<CanaryInstruction> output,
        ICollection<CanaryCatalogDiagnostic> diagnostics,
        ref bool usedInstructionAnchor,
        string? inheritedAnchor = null)
    {
        foreach (var node in sequence.Children)
        {
            if (node is not YamlSequenceNode instruction ||
                instruction.Children.Count == 0 ||
                instruction.Children[0] is not YamlScalarNode kindNode ||
                string.IsNullOrWhiteSpace(kindNode.Value))
            {
                return InstructionError("Patch instruction must be a non-empty sequence.");
            }

            var kind = kindNode.Value.Trim();
            if (!KnownInstructionKinds.Contains(kind))
            {
                return InstructionError($"Unknown RPCS3 patch instruction kind '{kind}'.");
            }

            if (kind.Equals("load", StringComparison.Ordinal))
            {
                if (instruction.Children.Count < 2 || instruction.Children[1] is not YamlSequenceNode loaded)
                {
                    return InstructionError("load requires a resolved instruction sequence.");
                }

                var anchor = AnchorName(loaded);
                if (string.IsNullOrWhiteSpace(anchor))
                {
                    return InstructionError("load must resolve to a named YAML anchor.");
                }

                anchors.Add(anchor);
                usedInstructionAnchor = true;
                if (!TryParseInstructions(
                        loaded,
                        moduleKey,
                        title,
                        anchors,
                        output,
                        diagnostics,
                        ref usedInstructionAnchor,
                        anchor))
                {
                    return false;
                }

                continue;
            }

            if (instruction.Children.Count < 3 ||
                instruction.Children[1] is not YamlScalarNode addressNode ||
                instruction.Children[2] is not YamlScalarNode valueNode ||
                string.IsNullOrWhiteSpace(addressNode.Value) ||
                valueNode.Value is null)
            {
                return InstructionError($"Instruction '{kind}' requires scalar address and value fields.");
            }

            var address = addressNode.Value.Trim();
            if (UsesNumericAddress(kind) && !TryParseUnsigned(address, out _))
            {
                return InstructionError($"Instruction '{kind}' has invalid address '{address}'.");
            }

            output.Add(new CanaryInstruction(
                kind,
                address,
                valueNode.Value,
                instruction.Children
                    .Skip(3)
                    .Select(Scalar)
                    .Select(value => value ?? string.Empty)
                    .ToArray(),
                inheritedAnchor));
        }

        return true;

        bool InstructionError(string message)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT016",
                message,
                moduleKey,
                title));
            return false;
        }
    }

    private static bool TryParseConfigValues(
        YamlNode? node,
        string moduleKey,
        string title,
        ICollection<CanaryCatalogDiagnostic> diagnostics,
        out IReadOnlyList<CanaryConfigValue> values)
    {
        values = [];
        if (node is null)
        {
            return true;
        }

        if (node is not YamlMappingNode mapping)
        {
            return ConfigError("Configurable Values must be a mapping.");
        }

        var parsed = new List<CanaryConfigValue>();
        foreach (var (nameNode, detailsNode) in mapping.Children)
        {
            var name = Scalar(nameNode)?.Trim();
            if (string.IsNullOrWhiteSpace(name) || detailsNode is not YamlMappingNode details)
            {
                return ConfigError("Each configurable value requires a name and mapping body.");
            }

            var type = Scalar(TryGetNode(details, "Type"))?.Trim();
            var defaultValue = Scalar(TryGetNode(details, "Value"))?.Trim();
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(defaultValue))
            {
                return ConfigError($"Configurable value '{name}' requires Type and Value fields.");
            }

            var allowed = new Dictionary<string, string>(StringComparer.Ordinal);
            var allowedNode = TryGetNode(details, "Allowed Values");
            if (allowedNode is not null)
            {
                if (allowedNode is not YamlMappingNode allowedMapping)
                {
                    return ConfigError($"Allowed Values for '{name}' must be a mapping.");
                }

                foreach (var (labelNode, valueNode) in allowedMapping.Children)
                {
                    var label = Scalar(labelNode)?.Trim();
                    var allowedValue = Scalar(valueNode)?.Trim();
                    if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(allowedValue) ||
                        !allowed.TryAdd(label, allowedValue))
                    {
                        return ConfigError($"Configurable value '{name}' contains an invalid or duplicate allowed value.");
                    }
                }
            }

            parsed.Add(new CanaryConfigValue(name, type, defaultValue, allowed));
        }

        values = parsed.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        return true;

        bool ConfigError(string message)
        {
            diagnostics.Add(new CanaryCatalogDiagnostic(
                CanaryDiagnosticSeverity.Error,
                "HCAT017",
                message,
                moduleKey,
                title));
            return false;
        }
    }

    private static (CanaryComplexity Complexity, bool IsCandidate, string Reason) Classify(
        PatchHashKind moduleKind,
        IReadOnlyList<CanaryInstruction> instructions,
        IReadOnlyList<CanaryConfigValue> configValues,
        bool usedInstructionAnchor)
    {
        if (moduleKind is PatchHashKind.Prx or PatchHashKind.Ovl or PatchHashKind.Unknown)
        {
            return (CanaryComplexity.Unsupported, false, "Module bytes require a proven PRX/OVL resolver before automatic porting.");
        }

        if (instructions.Any(item => item.Kind == "bpex"))
        {
            return (CanaryComplexity.Dynamic, false, "Dynamic bpex/runtime scanning is outside the automatic H-Canary subset.");
        }

        if (instructions.Any(item => CodeCaveKinds.Contains(item.Kind)) || LooksLikeCodeCave(instructions))
        {
            return (CanaryComplexity.CodeCave, false, "Allocation, injected control flow, or a code-cave write run requires manual review.");
        }

        if (usedInstructionAnchor)
        {
            return (CanaryComplexity.AnchorBased, false, "Patch instructions depend on a resolved YAML anchor.");
        }

        if (instructions.Any(item => !AutomaticKinds.Contains(item.Kind)))
        {
            return (CanaryComplexity.Unsupported, false, "One or more instruction kinds are outside the phase-one automatic write subset.");
        }

        if (instructions.Any(IsBranchInstruction))
        {
            return (CanaryComplexity.BranchPatch, true, "Branch replacement is a candidate only after full target and displacement verification in H2.");
        }

        if (instructions.Count > 1)
        {
            return (CanaryComplexity.MultiInstruction, true, "Multiple bounded writes require every site to resolve uniquely in H2.");
        }

        if (configValues.Count > 0 || instructions[0].Kind is "bef32" or "bef64")
        {
            return (CanaryComplexity.ConstantChange, true, "Single constant replacement is within the bounded H2 candidate subset.");
        }

        if (instructions[0].Kind == "be32")
        {
            return (CanaryComplexity.SingleInstruction, true, "Single big-endian 32-bit replacement requires instruction preimage proof in H2.");
        }

        return (CanaryComplexity.SimpleWrite, true, "Single bounded big-endian write is within the H2 candidate subset.");
    }

    private static bool LooksLikeCodeCave(IReadOnlyList<CanaryInstruction> instructions)
    {
        if (instructions.Count < 8 || !instructions.Any(IsBranchInstruction))
        {
            return false;
        }

        var addresses = instructions
            .Where(item => item.Kind == "be32" && TryParseUnsigned(item.Address, out _))
            .Select(item =>
            {
                TryParseUnsigned(item.Address, out var address);
                return address;
            })
            .Distinct()
            .Order()
            .ToArray();

        var run = 1;
        for (var index = 1; index < addresses.Length; index++)
        {
            run = addresses[index] == addresses[index - 1] + 4 ? run + 1 : 1;
            if (run >= 6)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBranchInstruction(CanaryInstruction instruction)
    {
        if (instruction.Kind != "be32" || !TryParseUnsigned(instruction.Value, out var parsed) || parsed > uint.MaxValue)
        {
            return false;
        }

        var value = (uint)parsed;
        var primaryOpcode = value >> 26;
        if (primaryOpcode is 16 or 18)
        {
            return true;
        }

        if (primaryOpcode != 19)
        {
            return false;
        }

        var extendedOpcode = (value >> 1) & 0x3ff;
        return extendedOpcode is 16 or 528;
    }

    private static string BuildInstructionFingerprint(
        IReadOnlyList<CanaryInstruction> instructions,
        IReadOnlyList<CanaryConfigValue> configValues)
    {
        var canonical = new StringBuilder();
        foreach (var instruction in instructions)
        {
            canonical.Append(instruction.Kind)
                .Append('|')
                .Append(NormalizeScalar(instruction.Value))
                .Append('|')
                .Append(instruction.AnchorName is null ? "direct" : "anchor")
                .Append('|')
                .AppendJoin(',', instruction.ExtraArguments.Select(NormalizeScalar))
                .Append(';');
        }

        foreach (var value in configValues)
        {
            canonical.Append("cfg|")
                .Append(Normalize(value.Name))
                .Append('|')
                .Append(NormalizeScalar(value.Type))
                .Append('|')
                .Append(NormalizeScalar(value.DefaultValue))
                .Append('|');
            foreach (var allowed in value.AllowedValues.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                canonical.Append(Normalize(allowed.Key))
                    .Append('=')
                    .Append(NormalizeScalar(allowed.Value))
                    .Append(',');
            }

            canonical.Append(';');
        }

        return "HCI-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..16];
    }

    private static string NormalizeScalar(string value)
    {
        var trimmed = value.Trim();
        if (TryParseUnsigned(trimmed, out var numeric))
        {
            return $"0x{numeric:X}";
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
        {
            return floating.ToString("R", CultureInfo.InvariantCulture);
        }

        return Normalize(trimmed);
    }

    private static Dictionary<string, Queue<Rpcs3PatchEntry>> BuildBaselineQueues(
        IEnumerable<Rpcs3PatchEntry> entries)
    {
        var output = new Dictionary<string, Queue<Rpcs3PatchEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = BaselineKey(entry.HashKind, entry.Hash, entry.Title);
            if (!output.TryGetValue(key, out var queue))
            {
                queue = new Queue<Rpcs3PatchEntry>();
                output.Add(key, queue);
            }

            queue.Enqueue(entry);
        }

        return output;
    }

    private static bool TryNormalizeVersionHeaders(
        string text,
        out string normalized,
        out int duplicateCount,
        out string error)
    {
        normalized = string.Empty;
        duplicateCount = 0;
        error = string.Empty;
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');
        var output = new List<string>(lines.Length);
        var found = false;

        foreach (var line in lines)
        {
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                output.Add(line);
                continue;
            }

            var match = VersionLinePattern.Match(line);
            if (!match.Success)
            {
                output.Add(line);
                continue;
            }

            var value = match.Groups["value"].Value.Trim('"', '\'');
            if (!value.Equals(SupportedVersion, StringComparison.Ordinal))
            {
                error = $"Canary YAML requires every top-level Version header to be {SupportedVersion}; found '{value}'.";
                return false;
            }

            if (!found)
            {
                output.Add($"Version: {SupportedVersion}");
                found = true;
            }
            else
            {
                duplicateCount++;
            }
        }

        if (!found)
        {
            error = $"Canary YAML is missing the required Version: {SupportedVersion} header.";
            return false;
        }

        normalized = string.Join('\n', output);
        return true;
    }

    private static string BaselineKey(PatchHashKind kind, string hash, string title)
    {
        return $"{kind}\0{hash.Trim()}\0{title.Trim()}";
    }

    private static int CountDefinitions(YamlNode node)
    {
        return node is YamlMappingNode mapping ? Math.Max(1, mapping.Children.Count) : 1;
    }

    private static int CountAnchors(YamlNode root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<YamlNode>(ReferenceEqualityComparer.Instance);
        Visit(root);
        return names.Count;

        void Visit(YamlNode node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            var name = AnchorName(node);
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }

            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var (key, value) in mapping.Children)
                    {
                        Visit(key);
                        Visit(value);
                    }
                    break;
                case YamlSequenceNode sequence:
                    foreach (var child in sequence.Children)
                    {
                        Visit(child);
                    }
                    break;
            }
        }
    }

    private static string? AnchorName(YamlNode node)
    {
        return node.Anchor.IsEmpty ? null : node.Anchor.Value;
    }

    private static void AddAnchor(ISet<string> anchors, YamlNode? node)
    {
        if (node is null)
        {
            return;
        }

        var name = AnchorName(node);
        if (!string.IsNullOrWhiteSpace(name))
        {
            anchors.Add(name);
        }
    }

    private static bool UsesNumericAddress(string kind)
    {
        return kind is not "move_file" and not "hide_file";
    }

    private static bool TryParseUnsigned(string value, out ulong parsed)
    {
        var candidate = value.Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(candidate[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsed);
        }

        return ulong.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return string.Join(' ', WordPattern.Matches(normalized).Select(match => match.Value));
    }

    private static PatchHashKind ParseModuleKind(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "PPU" => PatchHashKind.Ppu,
            "SPU" => PatchHashKind.Spu,
            "PRX" => PatchHashKind.Prx,
            "OVL" => PatchHashKind.Ovl,
            _ => PatchHashKind.Unknown
        };
    }

    private static string DecodeDuplicateKey(string value)
    {
        var suffix = value.LastIndexOf(DuplicateKeySuffix, StringComparison.Ordinal);
        if (suffix < 0)
        {
            return value;
        }

        var number = value[(suffix + DuplicateKeySuffix.Length)..];
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? value[..suffix]
            : value;
    }

    private static YamlNode? TryGetNode(YamlMappingNode mapping, string key)
    {
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            if (string.Equals(
                    DecodeDuplicateKey(Scalar(keyNode) ?? string.Empty),
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return valueNode;
            }
        }

        return null;
    }

    private static string? Scalar(YamlNode? node)
    {
        return node is YamlScalarNode scalar ? scalar.Value : null;
    }

    private static CanaryCatalogResult Failure(string code, string message)
    {
        var diagnostic = new CanaryCatalogDiagnostic(
            CanaryDiagnosticSeverity.Error,
            code,
            message);
        return new CanaryCatalogResult(
            false,
            new CanaryCatalog(null, [], [diagnostic], 0, 0));
    }
}

public sealed class HCanaryFamilyIndex
{
    public IReadOnlyList<CanaryPatchFamily> Build(CanaryCatalog catalog)
    {
        return catalog.Definitions
            .GroupBy(FamilyKey, StringComparer.Ordinal)
            .Select(BuildFamily)
            .OrderBy(family => family.NormalizedTitle, StringComparer.Ordinal)
            .ThenBy(family => family.FamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    public CanaryCatalogReport BuildReport(CanaryCatalog catalog)
    {
        var families = Build(catalog);
        var moduleCounts = Enum.GetValues<PatchHashKind>()
            .Where(kind => kind != PatchHashKind.Unknown)
            .ToDictionary(
                kind => kind,
                kind => catalog.Definitions.Count(item => item.ModuleKind == kind));

        var simpleCount = catalog.Definitions.Count(item => item.IsAutomaticCandidate);
        return new CanaryCatalogReport(
            catalog.Definitions.Count,
            moduleCounts,
            catalog.RejectedDefinitions,
            families.Count,
            families.Count(family => family.ReferenceEntries.Count > 1),
            simpleCount,
            catalog.Definitions.Count - simpleCount,
            families,
            catalog.Diagnostics);
    }

    private static CanaryPatchFamily BuildFamily(IGrouping<string, CanaryPatchDefinition> group)
    {
        var entries = group
            .OrderBy(item => item.ModuleKind)
            .ThenBy(item => item.ModuleHash, StringComparer.Ordinal)
            .ThenBy(item => item.PatchVersion, StringComparer.Ordinal)
            .ToArray();
        var complexities = entries.Select(item => item.Complexity).Distinct().ToArray();
        var complexity = entries.Select(item => item.ModuleKind).Distinct().Count() > 1
            ? CanaryComplexity.MultiModule
            : complexities.OrderByDescending(ComplexityRank).First();

        return new CanaryPatchFamily(
            "HCF-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key)))[..16],
            entries[0].Title,
            entries[0].NormalizedTitle,
            entries,
            entries.SelectMany(item => item.Serials)
                .Select(Region)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            entries.SelectMany(item => item.Versions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            entries.Select(item => item.NormalizedModuleHash ?? item.ModuleHash)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            entries.Select(item => item.InstructionFingerprint)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            complexity);
    }

    private static string FamilyKey(CanaryPatchDefinition definition)
    {
        var gameTitles = definition.GameTargets
            .Select(target => Normalize(target.Title))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var anchorStructure = definition.AnchorDependencies.Count == 0
            ? "none"
            : string.Join(',', definition.Instructions
                .Where(item => item.AnchorName is not null)
                .GroupBy(item => item.AnchorName, StringComparer.Ordinal)
                .Select(group => group.Count())
                .Order());

        return string.Join(
            "\0",
            definition.NormalizedTitle,
            definition.InstructionFingerprint,
            Normalize(definition.Group),
            Normalize(definition.Notes),
            string.Join('|', gameTitles),
            anchorStructure,
            definition.GameTargets.Count == 0 ? "invalid-targets" : "validated-targets");
    }

    private static int ComplexityRank(CanaryComplexity complexity)
    {
        return complexity switch
        {
            CanaryComplexity.Unsupported => 100,
            CanaryComplexity.Dynamic => 90,
            CanaryComplexity.MultiModule => 80,
            CanaryComplexity.CodeCave => 70,
            CanaryComplexity.AnchorBased => 60,
            CanaryComplexity.BranchPatch => 50,
            CanaryComplexity.MultiInstruction => 40,
            CanaryComplexity.SingleInstruction => 30,
            CanaryComplexity.ConstantChange => 20,
            _ => 10
        };
    }

    private static string Region(string serial)
    {
        if (serial.Equals(Rpcs3PatchVersion.All, StringComparison.Ordinal))
        {
            return "All";
        }

        if (serial.StartsWith("BLUS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BCUS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPUB", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPUA", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPUD", StringComparison.OrdinalIgnoreCase))
        {
            return "US";
        }

        if (serial.StartsWith("BLES", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BCES", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPEB", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPEA", StringComparison.OrdinalIgnoreCase))
        {
            return "EU";
        }

        if (serial.StartsWith("BLJM", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BLJS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BCJS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPJB", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPJA", StringComparison.OrdinalIgnoreCase))
        {
            return "JP";
        }

        if (serial.StartsWith("BCAS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BLAS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPHA", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPHB", StringComparison.OrdinalIgnoreCase))
        {
            return "ASIA";
        }

        if (serial.StartsWith("BCKS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("BLKS", StringComparison.OrdinalIgnoreCase) ||
            serial.StartsWith("NPKA", StringComparison.OrdinalIgnoreCase))
        {
            return "KR";
        }

        return "Unknown";
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{Nd}]+")
                .Select(match => match.Value));
    }
}

public sealed class HCanaryPortEvidenceService
{
    private static readonly Regex WordPattern = new(
        @"[\p{L}\p{Nd}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<CanaryPortAnalysis> Analyze(
        CanaryCatalog catalog,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(game);

        if (string.IsNullOrWhiteSpace(game.Serial))
        {
            return [];
        }

        var families = new HCanaryFamilyIndex().Build(catalog);
        var targetTitle = Normalize(game.Title);
        var output = new List<CanaryPortAnalysis>();

        foreach (var family in families)
        {
            var serialReferences = family.ReferenceEntries
                .Where(reference => HasSerialTarget(reference, game.Serial))
                .ToArray();
            var titleReferences = family.ReferenceEntries
                .Where(reference => HasTitleTarget(reference, targetTitle))
                .ToArray();

            if (serialReferences.Length == 0 && titleReferences.Length == 0)
            {
                continue;
            }

            var candidateReferences = serialReferences.Length > 0
                ? serialReferences
                : titleReferences;

            if (serialReferences.Length > 0)
            {
                var versionReferences = serialReferences
                    .Where(reference => SupportsVersion(reference, game.Serial, game.Version))
                    .ToArray();

                if (versionReferences.Length == 0)
                {
                    output.Add(Create(
                        family,
                        game,
                        CanaryPortStatus.VersionUnsupported,
                        candidateReferences,
                        [],
                        false,
                        $"The selected serial is referenced, but version '{DisplayVersion(game.Version)}' is not declared by this family."));
                    continue;
                }

                candidateReferences = versionReferences;
            }

            if (RequiresManualModuleEvidence(candidateReferences))
            {
                output.Add(Create(
                    family,
                    game,
                    CanaryPortStatus.ComplexManualReview,
                    candidateReferences,
                    [],
                    false,
                    "PRX, OVL, unknown, and multi-module families require explicit per-module evidence outside the H2 PPU/SPU subset."));
                continue;
            }

            var targetHashes = TargetHashes(candidateReferences, game);
            if (targetHashes.Count == 0)
            {
                output.Add(Create(
                    family,
                    game,
                    CanaryPortStatus.HashEvidenceMissing,
                    candidateReferences,
                    [],
                    false,
                    "No exact PPU/SPU executable hash evidence is available for the selected game."));
                continue;
            }

            var exactReferences = candidateReferences
                .Where(reference => ReferenceHashMatches(reference, game))
                .ToArray();

            if (exactReferences.Length > 0)
            {
                if (HasConflictingLayouts(exactReferences))
                {
                    output.Add(Create(
                        family,
                        game,
                        CanaryPortStatus.ConflictingReferences,
                        candidateReferences,
                        exactReferences,
                        false,
                        "The exact executable hash resolves to multiple incompatible reference layouts."));
                    continue;
                }

                var declaredTarget = exactReferences.Any(reference =>
                    HasSerialTarget(reference, game.Serial) &&
                    SupportsVersion(reference, game.Serial, game.Version));
                var reason = declaredTarget
                    ? "The target executable hash matches a declared reference for the selected serial and version."
                    : "The target executable hash matches a verified reference for the same normalized game title.";

                output.Add(Create(
                    family,
                    game,
                    CanaryPortStatus.ExactReference,
                    candidateReferences,
                    exactReferences,
                    true,
                    reason));
                continue;
            }

            if (!IsAutomaticH2Family(family))
            {
                output.Add(Create(
                    family,
                    game,
                    CanaryPortStatus.ComplexManualReview,
                    candidateReferences,
                    [],
                    false,
                    $"Family complexity '{family.Complexity}' is outside the bounded automatic H2 subset."));
                continue;
            }

            output.Add(Create(
                family,
                game,
                CanaryPortStatus.NotPortable,
                candidateReferences,
                [],
                false,
                "The target executable hash is not present in the verified reference family; H2 cannot infer new addresses without bounded binary preimage proof."));
        }

        return output
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CanaryPortAnalysis Create(
        CanaryPatchFamily family,
        Rpcs3GameDescriptor game,
        CanaryPortStatus status,
        IReadOnlyList<CanaryPatchDefinition> candidateReferences,
        IReadOnlyList<CanaryPatchDefinition> exactReferences,
        bool hasDeterministicEvidence,
        string reason)
    {
        return new CanaryPortAnalysis(
            family.FamilyId,
            family.Title,
            status,
            game.Serial,
            Rpcs3PatchVersion.Normalize(game.Version),
            game.HashEvidenceKind,
            game.HashSource,
            TargetHashes(candidateReferences, game),
            candidateReferences,
            exactReferences,
            hasDeterministicEvidence,
            reason);
    }

    private static bool HasSerialTarget(
        CanaryPatchDefinition reference,
        string serial)
    {
        return reference.GameTargets.Any(target =>
            target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) ||
            Rpcs3PatchVersion.IsAll(target.Serial));
    }

    private static bool HasTitleTarget(
        CanaryPatchDefinition reference,
        string normalizedTitle)
    {
        return normalizedTitle.Length > 0 && reference.GameTargets.Any(target =>
            Normalize(target.Title).Equals(normalizedTitle, StringComparison.Ordinal));
    }

    private static bool SupportsVersion(
        CanaryPatchDefinition reference,
        string serial,
        string? version)
    {
        var targets = reference.GameTargets.Where(target =>
            target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) ||
            Rpcs3PatchVersion.IsAll(target.Serial));

        foreach (var target in targets)
        {
            if (target.Versions.Count == 0 || target.Versions.Any(Rpcs3PatchVersion.IsAll))
            {
                return true;
            }

            if (version is not null && target.Versions.Any(candidate => Rpcs3PatchVersion.Equals(candidate, version)))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> TargetHashes(
        IReadOnlyList<CanaryPatchDefinition> references,
        Rpcs3GameDescriptor game)
    {
        var kinds = references
            .Select(reference => reference.ModuleKind)
            .Distinct()
            .ToHashSet();
        var hashes = new List<string>();

        if (kinds.Contains(PatchHashKind.Ppu))
        {
            hashes.AddRange(game.AllPpuHashes);
        }

        if (kinds.Contains(PatchHashKind.Spu))
        {
            hashes.AddRange(game.AllSpuHashes);
        }

        return hashes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ReferenceHashMatches(
        CanaryPatchDefinition reference,
        Rpcs3GameDescriptor game)
    {
        var normalized = reference.NormalizedModuleHash;
        if (normalized is null)
        {
            return false;
        }

        var targetHashes = reference.ModuleKind switch
        {
            PatchHashKind.Ppu => game.AllPpuHashes,
            PatchHashKind.Spu => game.AllSpuHashes,
            _ => []
        };

        return targetHashes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasConflictingLayouts(
        IReadOnlyList<CanaryPatchDefinition> references)
    {
        return references
            .Select(LayoutFingerprint)
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any();
    }

    private static string LayoutFingerprint(CanaryPatchDefinition reference)
    {
        var canonical = new StringBuilder();
        foreach (var instruction in reference.Instructions)
        {
            canonical.Append(instruction.Kind)
                .Append('|')
                .Append(NormalizeLayoutScalar(instruction.Address))
                .Append('|')
                .Append(NormalizeLayoutScalar(instruction.Value))
                .Append('|')
                .Append(instruction.AnchorName ?? string.Empty)
                .Append('|')
                .AppendJoin(',', instruction.ExtraArguments.Select(NormalizeLayoutScalar))
                .Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string NormalizeLayoutScalar(string value)
    {
        var trimmed = value.Trim();
        var digits = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;
        var style = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? NumberStyles.AllowHexSpecifier
            : NumberStyles.Integer;

        return ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out var numeric)
            ? $"0x{numeric:X}"
            : trimmed.ToLowerInvariant();
    }

    private static bool RequiresManualModuleEvidence(
        IReadOnlyList<CanaryPatchDefinition> references)
    {
        var kinds = references
            .Select(reference => reference.ModuleKind)
            .Distinct()
            .ToArray();

        return kinds.Length != 1 || kinds[0] is not (PatchHashKind.Ppu or PatchHashKind.Spu);
    }

    private static bool IsAutomaticH2Family(CanaryPatchFamily family)
    {
        if (family.ReferenceEntries.Any(reference => !reference.IsAutomaticCandidate))
        {
            return false;
        }

        return family.Complexity is
            CanaryComplexity.SimpleWrite or
            CanaryComplexity.ConstantChange or
            CanaryComplexity.SingleInstruction or
            CanaryComplexity.MultiInstruction;
    }

    private static string DisplayVersion(string? version)
    {
        return Rpcs3PatchVersion.Normalize(version) ?? "<unknown>";
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            WordPattern.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
                .Select(match => match.Value));
    }
}

public sealed class HCanaryHashExtractionService
{
    private static readonly Regex ExecutableHashPattern = new(
        @"\b(?<kind>PPU|SPU) executable hash:\s*(?<key>(?:PPU|SPU)-(?<hash>[0-9a-f]{40}))(?![0-9a-f])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public HCanaryHashReadResult Read(string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return new HCanaryHashReadResult(false, string.Empty, [], "RPCS3.log path is empty.");
        }

        var fullPath = Path.GetFullPath(logPath);
        if (!File.Exists(fullPath))
        {
            return new HCanaryHashReadResult(false, fullPath, [], "RPCS3.log was not found.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Parse(reader.ReadToEnd(), fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new HCanaryHashReadResult(false, fullPath, [], ex.Message);
        }
    }

    public HCanaryHashReadResult Parse(string text, string sourcePath = "RPCS3.log")
    {
        ArgumentNullException.ThrowIfNull(text);
        var hashes = new List<HCanaryHash>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ExecutableHashPattern.Matches(text))
        {
            var prefix = match.Groups["kind"].Value.ToUpperInvariant();
            var hash = match.Groups["hash"].Value.ToLowerInvariant();
            var identity = prefix + "-" + hash;
            if (!seen.Add(identity))
            {
                continue;
            }

            var line = 1;
            for (var index = 0; index < match.Index; index++)
            {
                if (text[index] == '\n')
                {
                    line++;
                }
            }

            hashes.Add(new HCanaryHash(
                prefix == "PPU" ? PatchHashKind.Ppu : PatchHashKind.Spu,
                hash,
                sourcePath,
                line));
        }

        return new HCanaryHashReadResult(true, sourcePath, hashes);
    }
}

public sealed class HCanaryPatchAuthoringService
{
    public const string PatchEngineVersion = "1.2";
    public const string MetadataFileName = "r3config_hcanary.json";

    private static readonly Regex ExecutableHashPattern = new(
        @"^(?<kind>PPU|SPU)-(?<hash>[0-9a-fA-F]{40})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HexPattern = new(
        @"^0x[0-9a-fA-F]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GameIdPattern = new(
        @"^[A-Za-z0-9]{9}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VersionPattern = new(
        @"^(?:[0-9]{2}\.[0-9]{2}|All)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PatchVersionPattern = new(
        @"^[0-9]+(?:\.[0-9]+){1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnchorPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly IReadOnlyList<string> Operations =
    [
        "be32", "be16", "be64", "be8", "byte", "le16", "le32", "le64",
        "bef32", "bef64", "lef32", "lef64", "bd32", "bd64", "bpex",
        "alloc", "calloc", "jump", "jumpl", "jumpf", "utf8", "cutf8",
        "move_file", "hide_file", "load"
    ];

    private static readonly HashSet<string> OperationSet = new(Operations, StringComparer.Ordinal);
    private readonly PatchCatalogReader _reader;
    private readonly PatchFileTransactionService _transaction;

    public HCanaryPatchAuthoringService(
        PatchCatalogReader? reader = null,
        PatchFileTransactionService? transaction = null)
    {
        _reader = reader ?? new PatchCatalogReader();
        _transaction = transaction ?? new PatchFileTransactionService();
    }

    public HCanaryValidationResult Validate(HCanaryPatchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var issues = new List<HCanaryValidationIssue>();
        var hashMatch = ExecutableHashPattern.Match(draft.ExecutableHash.Trim());
        if (!hashMatch.Success)
        {
            Add("Hash.Invalid", "ExecutableHash", "Executable Hash must use PPU-/SPU- followed by 40 hexadecimal characters.");
        }
        else if (hashMatch.Groups["hash"].Value.All(character => character == '0'))
        {
            Add("Hash.Zero", "ExecutableHash", "Executable Hash cannot be all zeroes.");
        }
        else if (draft.PatchType == HCanaryPatchType.Ppu && !hashMatch.Groups["kind"].Value.Equals("PPU", StringComparison.Ordinal))
        {
            Add("Hash.Kind", "ExecutableHash", "PPU patch type requires a PPU Hash.");
        }
        else if (draft.PatchType == HCanaryPatchType.Spu && !hashMatch.Groups["kind"].Value.Equals("SPU", StringComparison.Ordinal))
        {
            Add("Hash.Kind", "ExecutableHash", "SPU patch type requires an SPU Hash.");
        }

        Required(draft.PatchName, "PatchName", "PatchName.Required", "Patch Name is required.");
        Required(draft.PatchVersion, "PatchVersion", "PatchVersion.Required", "Patch Version is required.");
        if (!string.IsNullOrWhiteSpace(draft.PatchVersion) && !PatchVersionPattern.IsMatch(draft.PatchVersion.Trim()))
        {
            Add("PatchVersion.Invalid", "PatchVersion", "Patch Version must use western digits, for example 1.0.");
        }

        if (draft.Games.Count == 0)
        {
            Add("Games.Required", "Games", "At least one game target is required.");
        }

        for (var index = 0; index < draft.Games.Count; index++)
        {
            var target = draft.Games[index];
            Required(target.GameName, $"Games[{index}].GameName", "GameName.Required", "Game Name is required.");
            Required(target.GameId, $"Games[{index}].GameId", "GameId.Required", "Game ID is required.");
            Required(target.GameVersion, $"Games[{index}].GameVersion", "GameVersion.Required", "Game Version is required.");
            if (!string.IsNullOrWhiteSpace(target.GameId) && !GameIdPattern.IsMatch(target.GameId.Trim()))
            {
                Add("GameId.Invalid", $"Games[{index}].GameId", "Game ID must contain exactly nine Latin letters or digits.");
            }
            if (!string.IsNullOrWhiteSpace(target.GameVersion) && !VersionPattern.IsMatch(target.GameVersion.Trim()))
            {
                Add("GameVersion.Invalid", $"Games[{index}].GameVersion", "Game Version must be NN.NN or All.");
            }
        }

        if (draft.Instructions.Count == 0)
        {
            Add("Instructions.Required", "Instructions", "At least one Patch instruction is required.");
        }

        var anchorNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < draft.Anchors.Count; index++)
        {
            var anchor = draft.Anchors[index];
            if (!AnchorPattern.IsMatch(anchor.Name.Trim()))
            {
                Add("Anchor.Invalid", $"Anchors[{index}].Name", "Anchor name must be a valid ASCII YAML anchor name.");
            }
            else if (!anchorNames.Add(anchor.Name.Trim()))
            {
                Add("Anchor.Duplicate", $"Anchors[{index}].Name", "Anchor name is duplicated.");
            }
            if (anchor.Instructions.Count == 0)
            {
                Add("Anchor.Empty", $"Anchors[{index}].Instructions", "Anchor requires at least one instruction.");
            }
            ValidateInstructions(anchor.Instructions, $"Anchors[{index}].Instructions", allowLoad: false);
        }

        ValidateInstructions(draft.Instructions, "Instructions", allowLoad: true);

        string yaml;
        try
        {
            yaml = GenerateYaml(draft);
            var read = _reader.ReadText(yaml, PatchSourceOrigin.Imported);
            if (!read.Succeeded)
            {
                Add("Yaml.Invalid", "Yaml", read.Error ?? "Generated YAML is invalid.");
            }
            if (!yaml.StartsWith("Version: 1.2\r\n", StringComparison.Ordinal))
            {
                Add("Yaml.Version", "Yaml", "Version: 1.2 must be the first line.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            yaml = string.Empty;
            Add("Yaml.Invalid", "Yaml", ex.Message);
        }

        return new HCanaryValidationResult(issues, yaml);

        void Required(string value, string field, string code, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(code, field, message);
            }
        }

        void Add(string code, string field, string message) =>
            issues.Add(new HCanaryValidationIssue(code, field, message));

        void ValidateInstructions(IReadOnlyList<HCanaryInstruction> instructions, string field, bool allowLoad)
        {
            for (var index = 0; index < instructions.Count; index++)
            {
                var instruction = instructions[index];
                var itemField = $"{field}[{index}]";
                var operation = instruction.Operation.Trim();
                if (!OperationSet.Contains(operation))
                {
                    Add("Operation.Invalid", itemField + ".Operation", "Unknown RPCS3 Patch operation.");
                    continue;
                }
                if (operation == "load")
                {
                    if (!allowLoad)
                    {
                        Add("Anchor.NestedLoad", itemField + ".Operation", "Anchors cannot contain load instructions.");
                    }
                    var reference = instruction.Address.Trim().TrimStart('*');
                    if (!anchorNames.Contains(reference))
                    {
                        Add("Anchor.Missing", itemField + ".Address", "Referenced Anchor does not exist.");
                    }
                    continue;
                }
                if (!HexPattern.IsMatch(instruction.Address.Trim()))
                {
                    Add("Address.Invalid", itemField + ".Address", "Address must be hexadecimal and start with 0x.");
                }
                if (string.IsNullOrWhiteSpace(instruction.Value) ||
                    (UsesHexValue(operation) && !HexPattern.IsMatch(instruction.Value.Trim())))
                {
                    Add("Value.Invalid", itemField + ".Value", "Value is invalid for this operation.");
                }
            }
        }
    }

    public string GenerateYaml(HCanaryPatchDraft draft)
    {
        var lines = new List<string> { "Version: 1.2", string.Empty };
        if (draft.Anchors.Count > 0)
        {
            lines.Add("Anchors:");
            foreach (var anchor in draft.Anchors)
            {
                lines.Add($"  {anchor.Name.Trim()}: &{anchor.Name.Trim()}");
                foreach (var instruction in anchor.Instructions)
                {
                    lines.Add("    " + InstructionYaml(instruction));
                }
            }
            lines.Add(string.Empty);
        }

        lines.Add(draft.ExecutableHash.Trim() + ":");
        lines.Add("  " + Quote(draft.PatchName.Trim()) + ":");
        lines.Add("    Games:");
        foreach (var titleGroup in draft.Games.GroupBy(item => item.GameName.Trim(), StringComparer.Ordinal))
        {
            lines.Add("      " + Quote(titleGroup.Key) + ":");
            foreach (var idGroup in titleGroup.GroupBy(item => item.GameId.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            {
                var versions = idGroup.Select(item => NormalizeVersion(item.GameVersion))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                lines.Add($"        {idGroup.Key}: [ {string.Join(", ", versions)} ]");
            }
        }
        Optional("Author", draft.Author);
        Optional("Notes", draft.Notes);
        Optional("Group", draft.Group);
        lines.Add("    Patch Version: " + draft.PatchVersion.Trim());
        lines.Add("    Patch:");
        foreach (var instruction in draft.Instructions)
        {
            lines.Add("      " + InstructionYaml(instruction));
        }

        return string.Join("\r\n", lines) + "\r\n";

        void Optional(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"    {key}: {Quote(value)}");
            }
        }
    }

    public IReadOnlyList<HCanaryStoredPatch> Load(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return [];
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        var catalog = _reader.ReadText(text, PatchSourceOrigin.Imported, path);
        if (!catalog.Succeeded)
        {
            throw new InvalidDataException(catalog.Error ?? "imported_patch.yml is invalid.");
        }

        var yaml = LoadYaml(text);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var anchors = ReadAnchors(root);
        var output = new List<HCanaryStoredPatch>();
        var metadata = ReadMetadata(MetadataPath(path));
        foreach (var (keyNode, valueNode) in root.Children)
        {
            var hashKey = Scalar(keyNode);
            var match = ExecutableHashPattern.Match(hashKey);
            if (!match.Success || valueNode is not YamlMappingNode patches)
            {
                continue;
            }
            foreach (var (titleNode, detailsNode) in patches.Children)
            {
                if (detailsNode is not YamlMappingNode details)
                {
                    continue;
                }
                var title = Scalar(titleNode);
                var identity = Identity(hashKey, title);
                metadata.TryGetValue(identity, out var localMetadata);
                var inferredPatchType = match.Groups["kind"].Value == "PPU"
                    ? HCanaryPatchType.Ppu
                    : HCanaryPatchType.Spu;
                var isLegacyCanary =
                    localMetadata?.PatchType == HCanaryPatchType.LegacyCanary;
                var draft = new HCanaryPatchDraft
                {
                    PatchType = isLegacyCanary
                        ? inferredPatchType
                        : localMetadata?.PatchType ?? inferredPatchType,
                    IsExperimental =
                        localMetadata?.IsExperimental == true ||
                        isLegacyCanary,
                    ExecutableHash = hashKey,
                    PatchName = title,
                    Author = Scalar(TryGet(details, "Author")),
                    Notes = Scalar(TryGet(details, "Notes")),
                    Group = Scalar(TryGet(details, "Group")),
                    PatchVersion = Scalar(TryGet(details, "Patch Version")),
                    Status = localMetadata?.Status ?? HCanaryDraftStatus.Saved,
                    Games = ReadGames(TryGet(details, "Games")),
                    Instructions = ReadInstructions(TryGet(details, "Patch"), out var dependencies)
                };
                draft.Anchors = dependencies.Distinct(StringComparer.Ordinal)
                    .Where(anchors.ContainsKey)
                    .Select(name => new HCanaryAnchor
                    {
                        Name = name,
                        Instructions = ReadInstructions(anchors[name], out _)
                    }).ToList();
                output.Add(new HCanaryStoredPatch(identity, $"{title} — {hashKey}", draft));
            }
        }
        return output;
    }

    public HCanarySaveResult Save(
        string importedPatchPath,
        string backupRootPath,
        HCanaryPatchDraft draft,
        string? originalIdentity = null,
        bool saveAsNew = false,
        Action<IReadOnlyList<string>>? validateTargetsBeforeCommit = null)
    {
        var validation = Validate(draft);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        }

        var existing = File.Exists(importedPatchPath) && new FileInfo(importedPatchPath).Length > 0
            ? File.ReadAllText(importedPatchPath, Encoding.UTF8)
            : "Version: 1.2\r\n";
        var yaml = LoadYaml(existing);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var newIdentity = Identity(draft.ExecutableHash.Trim(), draft.PatchName.Trim());
        var replaced = false;

        if (!saveAsNew && !string.IsNullOrWhiteSpace(originalIdentity))
        {
            replaced = RemovePatch(root, originalIdentity);
        }
        if (FindPatch(root, newIdentity) is not null)
        {
            throw new InvalidOperationException("A Patch with the same name and executable Hash already exists.");
        }

        var anchorNodes = MergeAnchors(root, draft.Anchors);
        var hashMap = GetOrCreateHashMap(root, draft.ExecutableHash.Trim());
        hashMap.Add(new YamlScalarNode(draft.PatchName.Trim()), BuildDetailsNode(draft, anchorNodes));
        var text = Serialize(yaml);
        var read = _reader.ReadText(text, PatchSourceOrigin.Imported, importedPatchPath);
        if (!read.Succeeded)
        {
            throw new InvalidDataException(read.Error ?? "Merged imported_patch.yml is invalid.");
        }

        var metadataPath = MetadataPath(importedPatchPath);
        var metadata = ReadMetadata(metadataPath);
        if (!saveAsNew && !string.IsNullOrWhiteSpace(originalIdentity)) metadata.Remove(originalIdentity);
        metadata[newIdentity] = new LocalMetadata(
            draft.PatchType,
            draft.Status == HCanaryDraftStatus.Draft ? HCanaryDraftStatus.Saved : draft.Status,
            draft.IsExperimental);
        var metadataText = SerializeMetadata(metadata);
        var backup = _transaction.Commit(backupRootPath,
        [
            new PatchPreparedFile(importedPatchPath, text, PatchPreparedFileKind.PatchCatalog,
                PatchSourceOrigin.Imported, read.Catalog.Entries.Count),
            new PatchPreparedFile(metadataPath, metadataText, PatchPreparedFileKind.JsonMetadata)
        ], validateTargetsBeforeCommit);
        return new HCanarySaveResult(importedPatchPath, backup, read.Catalog.Entries.Count, replaced);
    }

    public HCanarySaveResult Delete(
        string importedPatchPath,
        string backupRootPath,
        string identity,
        Action<IReadOnlyList<string>>? validateTargetsBeforeCommit = null)
    {
        if (!File.Exists(importedPatchPath))
        {
            throw new FileNotFoundException("imported_patch.yml was not found.", importedPatchPath);
        }
        var yaml = LoadYaml(File.ReadAllText(importedPatchPath, Encoding.UTF8));
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        if (!RemovePatch(root, identity))
        {
            throw new InvalidOperationException("The selected custom Patch no longer exists.");
        }
        var text = Serialize(yaml);
        var read = _reader.ReadText(text, PatchSourceOrigin.Imported, importedPatchPath);
        if (!read.Succeeded)
        {
            throw new InvalidDataException(read.Error ?? "Updated imported_patch.yml is invalid.");
        }
        var metadataPath = MetadataPath(importedPatchPath);
        var metadata = ReadMetadata(metadataPath);
        metadata.Remove(identity);
        var backup = _transaction.Commit(backupRootPath,
        [
            new PatchPreparedFile(importedPatchPath, text, PatchPreparedFileKind.PatchCatalog,
                PatchSourceOrigin.Imported, read.Catalog.Entries.Count),
            new PatchPreparedFile(metadataPath, SerializeMetadata(metadata), PatchPreparedFileKind.JsonMetadata)
        ], validateTargetsBeforeCommit);
        return new HCanarySaveResult(importedPatchPath, backup, read.Catalog.Entries.Count, true);
    }

    private static bool UsesHexValue(string operation) => operation is not
        ("utf8" or "cutf8" or "move_file" or "hide_file" or "alloc" or "calloc" or "jump" or "jumpl" or "jumpf");

    private static string NormalizeVersion(string value) =>
        value.Trim().Equals("All", StringComparison.OrdinalIgnoreCase) ? "All" : value.Trim();

    private static string InstructionYaml(HCanaryInstruction instruction)
    {
        if (instruction.Operation.Trim() == "load")
        {
            return $"- [ load, *{instruction.Address.Trim().TrimStart('*')} ]";
        }
        return $"- [ {instruction.Operation.Trim()}, {instruction.Address.Trim()}, {instruction.Value.Trim()} ]";
    }

    private static string Quote(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string Identity(string hashKey, string title) => hashKey.Trim() + "\0" + title.Trim();

    public Rpcs3PatchCatalog ApplyLocalMetadata(Rpcs3PatchCatalog catalog, string importedPatchPath)
    {
        var metadata = ReadMetadata(MetadataPath(importedPatchPath));
        if (metadata.Count == 0) return catalog;
        return catalog with
        {
            Entries = catalog.Entries.Select(entry =>
            {
                var key = $"{entry.HashKind.ToString().ToUpperInvariant()}-{entry.Hash}\0{entry.Title}";
                if (!metadata.TryGetValue(key, out var value) ||
                    (!value.IsExperimental &&
                     value.PatchType != HCanaryPatchType.LegacyCanary))
                {
                    return entry;
                }
                return entry with { Risk = PatchRisk.Canary, PatchType = "Canary" };
            }).ToArray()
        };
    }

    private static string MetadataPath(string importedPatchPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(importedPatchPath))!, MetadataFileName);

    private static Dictionary<string, LocalMetadata> ReadMetadata(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new Dictionary<string, LocalMetadata>(StringComparer.Ordinal);
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, LocalMetadata>>(File.ReadAllText(path, Encoding.UTF8))
                   ?? new Dictionary<string, LocalMetadata>(StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("R3Config H-Canary metadata is invalid.", ex);
        }
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, LocalMetadata> metadata)
    {
        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n";
    }

    public sealed record LocalMetadata(
        HCanaryPatchType PatchType,
        HCanaryDraftStatus Status,
        bool IsExperimental = false);

    private static YamlStream LoadYaml(string text)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                throw new InvalidDataException("imported_patch.yml must contain one YAML mapping document.");
            }
            if (!Scalar(TryGet(root, "Version")).Equals(PatchEngineVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("imported_patch.yml must begin with Version: 1.2.");
            }
            return yaml;
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException("imported_patch.yml is invalid YAML.", ex);
        }
    }

    private static string Serialize(YamlStream yaml)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        yaml.Save(writer, assignAnchors: false);
        var text = writer.ToString().Replace("\r\r\n", "\r\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal)
            .Replace("\r\r\n", "\r\n", StringComparison.Ordinal);
        return text.TrimEnd('\r', '\n') + "\r\n";
    }

    private static Dictionary<string, YamlSequenceNode> ReadAnchors(YamlMappingNode root)
    {
        var output = new Dictionary<string, YamlSequenceNode>(StringComparer.Ordinal);
        foreach (var (key, value) in root.Children)
        {
            if (!Scalar(key).StartsWith("Anchors", StringComparison.OrdinalIgnoreCase) || value is not YamlMappingNode map)
            {
                continue;
            }
            foreach (var (_, anchorValue) in map.Children)
            {
                if (anchorValue is YamlSequenceNode sequence && !sequence.Anchor.IsEmpty)
                {
                    output[sequence.Anchor.Value] = sequence;
                }
            }
        }
        return output;
    }

    private static List<HCanaryGameTarget> ReadGames(YamlNode? node)
    {
        var output = new List<HCanaryGameTarget>();
        if (node is not YamlMappingNode games)
        {
            return output;
        }
        foreach (var (titleNode, idsNode) in games.Children)
        {
            if (idsNode is not YamlMappingNode ids) continue;
            foreach (var (idNode, versionsNode) in ids.Children)
            {
                var versions = versionsNode is YamlSequenceNode sequence
                    ? sequence.Children.Select(Scalar)
                    : [Scalar(versionsNode)];
                output.AddRange(versions.Select(version => new HCanaryGameTarget
                {
                    GameName = Scalar(titleNode), GameId = Scalar(idNode), GameVersion = version
                }));
            }
        }
        return output;
    }

    private static List<HCanaryInstruction> ReadInstructions(YamlNode? node, out List<string> dependencies)
    {
        dependencies = [];
        var output = new List<HCanaryInstruction>();
        if (node is not YamlSequenceNode patch) return output;
        foreach (var child in patch.Children.OfType<YamlSequenceNode>())
        {
            var operation = child.Children.Count > 0 ? Scalar(child.Children[0]) : string.Empty;
            if (operation == "load" && child.Children.Count > 1 && child.Children[1] is YamlSequenceNode loaded)
            {
                var name = loaded.Anchor.IsEmpty ? string.Empty : loaded.Anchor.Value;
                dependencies.Add(name);
                output.Add(new HCanaryInstruction { Operation = "load", Address = name, Value = string.Empty });
            }
            else if (child.Children.Count >= 3)
            {
                output.Add(new HCanaryInstruction
                {
                    Operation = operation,
                    Address = Scalar(child.Children[1]),
                    Value = Scalar(child.Children[2])
                });
            }
        }
        return output;
    }

    private static Dictionary<string, YamlSequenceNode> MergeAnchors(YamlMappingNode root, IReadOnlyList<HCanaryAnchor> anchors)
    {
        var nodes = ReadAnchors(root);
        YamlMappingNode? anchorMap = root.Children.FirstOrDefault(pair => Scalar(pair.Key).Equals("Anchors", StringComparison.OrdinalIgnoreCase)).Value as YamlMappingNode;
        if (anchors.Count > 0 && anchorMap is null)
        {
            anchorMap = new YamlMappingNode();
            root.Add(new YamlScalarNode("Anchors"), anchorMap);
        }
        if (anchorMap is not null)
        {
            MovePrimaryAnchorsBeforePatchHashes(root, anchorMap);
        }
        foreach (var anchor in anchors)
        {
            var name = anchor.Name.Trim();
            var newNode = new YamlSequenceNode(anchor.Instructions.Select(BuildInstructionNode)) { Anchor = name };
            if (nodes.TryGetValue(name, out var existing))
            {
                if (!NodeFingerprint(existing).Equals(NodeFingerprint(newNode), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Anchor '{name}' already exists with different instructions.");
                }
                continue;
            }
            anchorMap!.Add(new YamlScalarNode(name), newNode);
            nodes[name] = newNode;
        }
        return nodes;
    }

    private static void MovePrimaryAnchorsBeforePatchHashes(YamlMappingNode root, YamlMappingNode anchorMap)
    {
        var entries = root.Children.ToArray();
        var anchorPair = entries.First(pair => ReferenceEquals(pair.Value, anchorMap));
        root.Children.Clear();
        foreach (var pair in entries.Where(pair => Scalar(pair.Key).Equals("Version", StringComparison.OrdinalIgnoreCase)))
        {
            root.Add(pair.Key, pair.Value);
        }
        root.Add(anchorPair.Key, anchorPair.Value);
        foreach (var pair in entries.Where(pair =>
                     !Scalar(pair.Key).Equals("Version", StringComparison.OrdinalIgnoreCase) &&
                     !ReferenceEquals(pair.Value, anchorMap)))
        {
            root.Add(pair.Key, pair.Value);
        }
    }

    private static YamlMappingNode BuildDetailsNode(HCanaryPatchDraft draft, IReadOnlyDictionary<string, YamlSequenceNode> anchors)
    {
        var details = new YamlMappingNode();
        var games = new YamlMappingNode();
        foreach (var titleGroup in draft.Games.GroupBy(item => item.GameName.Trim(), StringComparer.Ordinal))
        {
            var ids = new YamlMappingNode();
            foreach (var idGroup in titleGroup.GroupBy(item => item.GameId.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            {
                ids.Add(idGroup.Key, new YamlSequenceNode(idGroup.Select(item => new YamlScalarNode(NormalizeVersion(item.GameVersion)))));
            }
            games.Add(titleGroup.Key, ids);
        }
        details.Add("Games", games);
        AddOptional(details, "Author", draft.Author);
        AddOptional(details, "Notes", draft.Notes);
        AddOptional(details, "Group", draft.Group);
        details.Add("Patch Version", draft.PatchVersion.Trim());
        var patch = new YamlSequenceNode();
        foreach (var instruction in draft.Instructions)
        {
            if (instruction.Operation.Trim() == "load")
            {
                var name = instruction.Address.Trim().TrimStart('*');
                patch.Add(new YamlSequenceNode(new YamlScalarNode("load"), anchors[name]));
            }
            else
            {
                patch.Add(BuildInstructionNode(instruction));
            }
        }
        details.Add("Patch", patch);
        return details;
    }

    private static YamlSequenceNode BuildInstructionNode(HCanaryInstruction instruction) => new(
        new YamlScalarNode(instruction.Operation.Trim()),
        new YamlScalarNode(instruction.Address.Trim()),
        new YamlScalarNode(instruction.Value.Trim()));

    private static void AddOptional(YamlMappingNode map, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) map.Add(key, value);
    }

    private static YamlMappingNode GetOrCreateHashMap(YamlMappingNode root, string hash)
    {
        if (TryGet(root, hash) is YamlMappingNode existing) return existing;
        var created = new YamlMappingNode();
        root.Add(hash, created);
        return created;
    }

    private static YamlNode? FindPatch(YamlMappingNode root, string identity)
    {
        var split = identity.Split('\0', 2);
        return split.Length == 2 && TryGet(root, split[0]) is YamlMappingNode map ? TryGet(map, split[1]) : null;
    }

    private static bool RemovePatch(YamlMappingNode root, string identity)
    {
        var split = identity.Split('\0', 2);
        if (split.Length != 2 || TryGetPair(root, split[0]) is not { } hashPair || hashPair.Value is not YamlMappingNode map ||
            TryGetPair(map, split[1]) is not { } patchPair)
        {
            return false;
        }
        map.Children.Remove(patchPair.Key);
        if (map.Children.Count == 0) root.Children.Remove(hashPair.Key);
        return true;
    }

    private static KeyValuePair<YamlNode, YamlNode>? TryGetPair(YamlMappingNode map, string key)
    {
        foreach (var pair in map.Children)
        {
            if (Scalar(pair.Key).Equals(key, StringComparison.Ordinal)) return pair;
        }
        return null;
    }

    private static YamlNode? TryGet(YamlMappingNode map, string key) => TryGetPair(map, key)?.Value;
    private static string Scalar(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;

    private static string NodeFingerprint(YamlNode node)
    {
        var stream = new YamlStream(new YamlDocument(node));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        stream.Save(writer, assignAnchors: false);
        return writer.ToString().Replace("&" + (node.Anchor.IsEmpty ? string.Empty : node.Anchor.Value), string.Empty, StringComparison.Ordinal);
    }
}
