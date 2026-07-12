using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

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
                var draft = new HCanaryPatchDraft
                {
                    PatchType = localMetadata?.PatchType ??
                        (match.Groups["kind"].Value == "PPU" ? HCanaryPatchType.Ppu : HCanaryPatchType.Spu),
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
        bool saveAsNew = false)
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
            draft.Status == HCanaryDraftStatus.Draft ? HCanaryDraftStatus.Saved : draft.Status);
        var metadataText = SerializeMetadata(metadata);
        var backup = _transaction.Commit(backupRootPath,
        [
            new PatchPreparedFile(importedPatchPath, text, PatchPreparedFileKind.PatchCatalog,
                PatchSourceOrigin.Imported, read.Catalog.Entries.Count),
            new PatchPreparedFile(metadataPath, metadataText, PatchPreparedFileKind.JsonMetadata)
        ]);
        return new HCanarySaveResult(importedPatchPath, backup, read.Catalog.Entries.Count, replaced);
    }

    public HCanarySaveResult Delete(string importedPatchPath, string backupRootPath, string identity)
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
        ]);
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
                if (!metadata.TryGetValue(key, out var value) || value.PatchType != HCanaryPatchType.Canary)
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

    public sealed record LocalMetadata(HCanaryPatchType PatchType, HCanaryDraftStatus Status);

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
