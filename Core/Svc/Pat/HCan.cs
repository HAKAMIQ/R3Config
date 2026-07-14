using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class HCanCat
{
    private const string ValidVer = "1.2";
    private const string Suffix = "__r3config_duplicate_";

    private static readonly Regex Pattern = new(
        @"^(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Pattern2 = new(
        @"^[A-Z]{2,8}-",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Pattern3 = new(
        @"[\p{L}\p{Nd}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Pattern4 = new(
        @"^Version\s*:\s*(?<value>[^#\s]+)(?:\s*#.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> Kinds = new(
        [
            "alloc", "calloc", "jump", "jumpl", "jumpf", "load", "byte", "be8",
            "le16", "le32", "le64", "bef32", "bef64", "be16", "be32", "bd32",
            "be64", "bd64", "lef32", "lef64", "bpex", "utf8", "cutf8",
            "move_file", "hide_file"
        ],
        StringComparer.Ordinal);

    private static readonly HashSet<string> Kinds2 = new(
        ["byte", "be8", "be16", "be32", "be64", "bef32", "bef64"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> Kinds3 = new(
        ["alloc", "calloc", "jump", "jumpl", "jumpf"],
        StringComparer.Ordinal);

    private readonly ReadSvc _Reader = new();

    public CanCatRs Read(string path)
    {
        if (!File.Exists(path))
        {
            return Failure("HCAT001", $"Canary catalog was not found: {path}");
        }

        try
        {
            return ReadText(
                R3Bound.ReadUtf8(
                    path,
                    R3Limits.PatCatBy,
                    "Canary catalog"),
                path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure("HCAT002", ex.Message);
        }
    }

    public CanCatRs ReadText(string text, string? sourcePath = null)
    {
        var diagnostics =
            new DiagList<CanDiag>(
                R3Limits.Diag);
        if (!TryNorm(text, out var normalizedText, out var duplicateVersions, out var versionError))
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT006",
                versionError));
            return new CanCatRs(
                false,
                new CanCat(null, [], diagnostics, 0, 0));
        }

        if (duplicateVersions > 0)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Warning,
                "HCAT018",
                $"Normalized {duplicateVersions} duplicate top-level Version: {ValidVer} header(s) before catalog parsing."));
        }

        var baseline = _Reader.ReadText(
            normalizedText,
            PatSrc.Can,
            sourcePath);

        if (!baseline.IsOk)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT003",
                $"Existing patch catalog validation failed: {baseline.Error}"));
            return new CanCatRs(
                false,
                new CanCat(null, [], diagnostics, 0, 0));
        }

        if (!string.IsNullOrWhiteSpace(baseline.Error))
        {
            diagnostics.Add(new CanDiag(
                CanSev.Warning,
                "HCAT004",
                $"Existing patch catalog diagnostics: {baseline.Error}"));
        }

        try
        {
            var yaml = R3Bound.LoadYaml(
                ReadSvc.NormDup2(
                    normalizedText),
                "Canary catalog");

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                diagnostics.Add(new CanDiag(
                    CanSev.Error,
                    "HCAT005",
                    "Canary YAML must contain exactly one mapping document."));
                return new CanCatRs(
                    false,
                    new CanCat(null, [], diagnostics, 0, 0));
            }

            var version = Scalar(TryGet(root, "Version"));
            if (!string.Equals(version, ValidVer, StringComparison.Ordinal))
            {
                diagnostics.Add(new CanDiag(
                    CanSev.Error,
                    "HCAT006",
                    $"Canary YAML requires Version: {ValidVer}; found '{version ?? "<missing>"}'."));
                return new CanCatRs(
                    false,
                    new CanCat(version, [], diagnostics, 0, NumAnc(root)));
            }

            var baselineEntries = Build(baseline.Catalog.Entries);
            var definitions = new List<CanDef>();
            var rejected = 0;

            foreach (var (keyNode, valueNode) in root.Children)
            {
                var encodedKey = Scalar(keyNode) ?? string.Empty;
                var rawKey = Decode(encodedKey);
                var hashMatch = Pattern.Match(rawKey);

                if (!hashMatch.Success)
                {
                    if (Pattern2.IsMatch(rawKey))
                    {
                        var rejectedHere = NumDef(valueNode);
                        rejected += rejectedHere;
                        diagnostics.Add(new CanDiag(
                            CanSev.Error,
                            "HCAT007",
                            $"Unsupported Canary module key '{rawKey}' rejected with {rejectedHere} definition(s).",
                            rawKey));
                    }

                    continue;
                }

                var moduleKind = Parse(hashMatch.Groups["kind"].Value);
                var moduleHash = hashMatch.Groups["hash"].Value.Trim();
                var moduleKey = $"{hashMatch.Groups["kind"].Value.ToUpperInvariant()}-{moduleHash}";
                string? normalizedHash = null;

                if (moduleKind is HashKind.Ppu or HashKind.Spu &&
                    !ExeHash.TryNorm(moduleHash, out normalizedHash))
                {
                    var rejectedHere = NumDef(valueNode);
                    rejected += rejectedHere;
                    diagnostics.Add(new CanDiag(
                        CanSev.Error,
                        "HCAT008",
                        $"{moduleKey} is not an exact 40-hex or valid RPCS3 Base57 executable hash; {rejectedHere} definition(s) rejected.",
                        moduleKey));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(moduleHash) || valueNode is not YamlMappingNode patchMap)
                {
                    rejected++;
                    diagnostics.Add(new CanDiag(
                        CanSev.Error,
                        "HCAT009",
                        $"{moduleKey} must contain a non-empty hash and patch mapping.",
                        moduleKey));
                    continue;
                }

                foreach (var (titleNode, detailsNode) in patchMap.Children)
                {
                    var title = Decode(Scalar(titleNode) ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title) || detailsNode is not YamlMappingNode details)
                    {
                        rejected++;
                        diagnostics.Add(new CanDiag(
                            CanSev.Error,
                            "HCAT010",
                            $"{moduleKey} contains an empty title or non-mapping patch definition.",
                            moduleKey,
                            string.IsNullOrWhiteSpace(title) ? null : title));
                        continue;
                    }

                    var baselineKey = Baseline(moduleKind, moduleHash, title);
                    if (!baselineEntries.TryGetValue(baselineKey, out var queue) ||
                        queue.Count == 0 ||
                        queue.Peek().GameTgt.Count == 0)
                    {
                        rejected++;
                        diagnostics.Add(new CanDiag(
                            CanSev.Error,
                            "HCAT011",
                            "Definition was rejected because the existing patch catalog could not validate a non-empty Games target mapping.",
                            moduleKey,
                            title));
                        continue;
                    }

                    var baselineEntry = queue.Dequeue();
                    var author = Scalar(TryGet(details, "Author"))?.Trim();
                    var patchVersion = Scalar(TryGet(details, "Patch Version"))?.Trim();
                    var patchNode = TryGet(details, "Patch");

                    if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(patchVersion))
                    {
                        rejected++;
                        diagnostics.Add(new CanDiag(
                            CanSev.Error,
                            "HCAT012",
                            "Definition requires non-empty Author and Patch Version fields.",
                            moduleKey,
                            title));
                        continue;
                    }

                    if (patchNode is not YamlSequenceNode patchSequence || patchSequence.Children.Count == 0)
                    {
                        rejected++;
                        diagnostics.Add(new CanDiag(
                            CanSev.Error,
                            "HCAT013",
                            "Definition requires a non-empty Patch instruction sequence.",
                            moduleKey,
                            title));
                        continue;
                    }

                    var anchors = new HashSet<string>(StringComparer.Ordinal);
                    AddAnc(anchors, TryGet(details, "Games"));
                    AddAnc(anchors, TryGet(details, "Author"));
                    AddAnc(anchors, TryGet(details, "Notes"));
                    AddAnc(anchors, TryGet(details, "Patch Version"));
                    AddAnc(anchors, TryGet(details, "Configurable Values"));

                    if (!TryPars2(
                            TryGet(details, "Configurable Values"),
                            moduleKey,
                            title,
                            diagnostics,
                            out var configValues))
                    {
                        rejected++;
                        continue;
                    }

                    var instructions = new List<CanInst>();
                    var usedInstructionAnchor = false;
                    if (!TryParse(
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

                    var fingerprint = BuildIns(instructions, configValues);
                    var (complexity, isCandidate, reason) = Classify(
                        moduleKind,
                        instructions,
                        configValues,
                        usedInstructionAnchor);

                    definitions.Add(new CanDef(
                        title,
                        Norm2(title),
                        moduleKind,
                        moduleHash,
                        normalizedHash,
                        baselineEntry.GameTgt,
                        patchVersion,
                        author,
                        Scalar(TryGet(details, "Notes")),
                        Scalar(TryGet(details, "Group")),
                        anchors.Order(StringComparer.Ordinal).ToArray(),
                        configValues,
                        instructions,
                        fingerprint,
                        complexity,
                        isCandidate,
                        reason,
                        PatSrc.Can,
                        sourcePath));
                }
            }

            var catalog = new CanCat(
                version,
                definitions
                    .OrderBy(item => item.NormName, StringComparer.Ordinal)
                    .ThenBy(item => item.ModKind)
                    .ThenBy(item => item.ModHash, StringComparer.Ordinal)
                    .ThenBy(item => item.Title, StringComparer.Ordinal)
                    .ToArray(),
                diagnostics,
                rejected,
                NumAnc(root));

            return new CanCatRs(true, catalog);
        }
        catch (YamlException ex)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT014",
                ex.Message));
            return new CanCatRs(
                false,
                new CanCat(null, [], diagnostics, 0, 0));
        }
        catch (InvalidOperationException ex)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT015",
                ex.Message));
            return new CanCatRs(
                false,
                new CanCat(null, [], diagnostics, 0, 0));
        }
    }

    private static bool TryParse(
        YamlSequenceNode sequence,
        string moduleKey,
        string title,
        ISet<string> anchors,
        ICollection<CanInst> output,
        ICollection<CanDiag> diagnostics,
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
                return InsErr("Patch instruction must be a non-empty sequence.");
            }

            var kind = kindNode.Value.Trim();
            if (!Kinds.Contains(kind))
            {
                return InsErr($"Unknown RPCS3 patch instruction kind '{kind}'.");
            }

            if (kind.Equals("load", StringComparison.Ordinal))
            {
                if (instruction.Children.Count < 2 || instruction.Children[1] is not YamlSequenceNode loaded)
                {
                    return InsErr("load requires a resolved instruction sequence.");
                }

                var anchor = AncName(loaded);
                if (string.IsNullOrWhiteSpace(anchor))
                {
                    return InsErr("load must resolve to a named YAML anchor.");
                }

                anchors.Add(anchor);
                usedInstructionAnchor = true;
                if (!TryParse(
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
                return InsErr($"Instruction '{kind}' requires scalar address and value fields.");
            }

            var address = addressNode.Value.Trim();
            if (Numeric(kind) && !TryPars3(address, out _))
            {
                return InsErr($"Instruction '{kind}' has invalid address '{address}'.");
            }

            output.Add(new CanInst(
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

        bool InsErr(string message)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT016",
                message,
                moduleKey,
                title));
            return false;
        }
    }

    private static bool TryPars2(
        YamlNode? node,
        string moduleKey,
        string title,
        ICollection<CanDiag> diagnostics,
        out IReadOnlyList<CanVal> values)
    {
        values = [];
        if (node is null)
        {
            return true;
        }

        if (node is not YamlMappingNode mapping)
        {
            return CfgErr("Configurable Values must be a mapping.");
        }

        var parsed = new List<CanVal>();
        foreach (var (nameNode, detailsNode) in mapping.Children)
        {
            var name = Scalar(nameNode)?.Trim();
            if (string.IsNullOrWhiteSpace(name) || detailsNode is not YamlMappingNode details)
            {
                return CfgErr("Each configurable value requires a name and mapping body.");
            }

            var type = Scalar(TryGet(details, "Type"))?.Trim();
            var defaultValue = Scalar(TryGet(details, "Value"))?.Trim();
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(defaultValue))
            {
                return CfgErr($"Configurable value '{name}' requires Type and Value fields.");
            }

            var allowed = new Dictionary<string, string>(StringComparer.Ordinal);
            var allowedNode = TryGet(details, "Allowed Values");
            if (allowedNode is not null)
            {
                if (allowedNode is not YamlMappingNode allowedMapping)
                {
                    return CfgErr($"Allowed Values for '{name}' must be a mapping.");
                }

                foreach (var (labelNode, valueNode) in allowedMapping.Children)
                {
                    var label = Scalar(labelNode)?.Trim();
                    var allowedValue = Scalar(valueNode)?.Trim();
                    if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(allowedValue) ||
                        !allowed.TryAdd(label, allowedValue))
                    {
                        return CfgErr($"Configurable value '{name}' contains an invalid or duplicate allowed value.");
                    }
                }
            }

            parsed.Add(new CanVal(name, type, defaultValue, allowed));
        }

        values = parsed.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        return true;

        bool CfgErr(string message)
        {
            diagnostics.Add(new CanDiag(
                CanSev.Error,
                "HCAT017",
                message,
                moduleKey,
                title));
            return false;
        }
    }

    private static (CanCmp Complexity, bool IsCandidate, string Reason) Classify(
        HashKind moduleKind,
        IReadOnlyList<CanInst> instructions,
        IReadOnlyList<CanVal> configValues,
        bool usedInstructionAnchor)
    {
        if (moduleKind is HashKind.Prx or HashKind.Ovl or HashKind.Unknown)
        {
            return (CanCmp.NoSupp, false, "Module bytes require a proven PRX/OVL resolver before automatic porting.");
        }

        if (instructions.Any(item => item.Kind == "bpex"))
        {
            return (CanCmp.Dynamic, false, "Dynamic bpex/runtime scanning is outside the automatic H-Canary subset.");
        }

        if (instructions.Any(item => Kinds3.Contains(item.Kind)) || Looks(instructions))
        {
            return (CanCmp.CodeCave, false, "Allocation, injected control flow, or a code-cave write run requires manual review.");
        }

        if (usedInstructionAnchor)
        {
            return (CanCmp.AncBased, false, "Patch instructions depend on a resolved YAML anchor.");
        }

        if (instructions.Any(item => !Kinds2.Contains(item.Kind)))
        {
            return (CanCmp.NoSupp, false, "One or more instruction kinds are outside the phase-one automatic write subset.");
        }

        if (instructions.Any(IsBranch))
        {
            return (CanCmp.Pat, true, "Branch replacement is a candidate only after full target and displacement verification in H2.");
        }

        if (instructions.Count > 1)
        {
            return (CanCmp.MultiIns, true, "Multiple bounded writes require every site to resolve uniquely in H2.");
        }

        if (configValues.Count > 0 || instructions[0].Kind is "bef32" or "bef64")
        {
            return (CanCmp.Change, true, "Single constant replacement is within the bounded H2 candidate subset.");
        }

        if (instructions[0].Kind == "be32")
        {
            return (CanCmp.Ins, true, "Single big-endian 32-bit replacement requires instruction preimage proof in H2.");
        }

        return (CanCmp.Write, true, "Single bounded big-endian write is within the H2 candidate subset.");
    }

    private static bool Looks(IReadOnlyList<CanInst> instructions)
    {
        if (instructions.Count < 8 || !instructions.Any(IsBranch))
        {
            return false;
        }

        var addresses = instructions
            .Where(item => item.Kind == "be32" && TryPars3(item.Address, out _))
            .Select(item =>
            {
                TryPars3(item.Address, out var address);
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

    private static bool IsBranch(CanInst instruction)
    {
        if (instruction.Kind != "be32" || !TryPars3(instruction.Value, out var parsed) || parsed > uint.MaxValue)
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

    private static string BuildIns(
        IReadOnlyList<CanInst> instructions,
        IReadOnlyList<CanVal> configValues)
    {
        var canonical = new StringBuilder();
        foreach (var instruction in instructions)
        {
            canonical.Append(instruction.Kind)
                .Append('|')
                .Append(Norm(instruction.Value))
                .Append('|')
                .Append(instruction.AncName is null ? "direct" : "anchor")
                .Append('|')
                .AppendJoin(',', instruction.ExtraArg.Select(Norm))
                .Append(';');
        }

        foreach (var value in configValues)
        {
            canonical.Append("cfg|")
                .Append(Norm2(value.Name))
                .Append('|')
                .Append(Norm(value.Type))
                .Append('|')
                .Append(Norm(value.DefVal))
                .Append('|');
            foreach (var allowed in value.AllowVal.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                canonical.Append(Norm2(allowed.Key))
                    .Append('=')
                    .Append(Norm(allowed.Value))
                    .Append(',');
            }

            canonical.Append(';');
        }

        return "HCI-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..16];
    }

    private static string Norm(string value)
    {
        var trimmed = value.Trim();
        if (TryPars3(trimmed, out var numeric))
        {
            return $"0x{numeric:X}";
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
        {
            return floating.ToString("R", CultureInfo.InvariantCulture);
        }

        return Norm2(trimmed);
    }

    private static Dictionary<string, Queue<PatEntry>> Build(
        IEnumerable<PatEntry> entries)
    {
        var output = new Dictionary<string, Queue<PatEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = Baseline(entry.HashKind, entry.Hash, entry.Title);
            if (!output.TryGetValue(key, out var queue))
            {
                queue = new Queue<PatEntry>();
                output.Add(key, queue);
            }

            queue.Enqueue(entry);
        }

        return output;
    }

    private static bool TryNorm(
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

            var match = Pattern4.Match(line);
            if (!match.Success)
            {
                output.Add(line);
                continue;
            }

            var value = match.Groups["value"].Value.Trim('"', '\'');
            if (!value.Equals(ValidVer, StringComparison.Ordinal))
            {
                error = $"Canary YAML requires every top-level Version header to be {ValidVer}; found '{value}'.";
                return false;
            }

            if (!found)
            {
                output.Add($"Version: {ValidVer}");
                found = true;
            }
            else
            {
                duplicateCount++;
            }
        }

        if (!found)
        {
            error = $"Canary YAML is missing the required Version: {ValidVer} header.";
            return false;
        }

        normalized = string.Join('\n', output);
        return true;
    }

    private static string Baseline(HashKind kind, string hash, string title)
    {
        return $"{kind}\0{hash.Trim()}\0{title.Trim()}";
    }

    private static int NumDef(YamlNode node)
    {
        return node is YamlMappingNode mapping ? Math.Max(1, mapping.Children.Count) : 1;
    }

    private static int NumAnc(YamlNode root)
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

            var name = AncName(node);
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

    private static string? AncName(YamlNode node)
    {
        return node.Anchor.IsEmpty ? null : node.Anchor.Value;
    }

    private static void AddAnc(ISet<string> anchors, YamlNode? node)
    {
        if (node is null)
        {
            return;
        }

        var name = AncName(node);
        if (!string.IsNullOrWhiteSpace(name))
        {
            anchors.Add(name);
        }
    }

    private static bool Numeric(string kind)
    {
        return kind is not "move_file" and not "hide_file";
    }

    private static bool TryPars3(string value, out ulong parsed)
    {
        var candidate = value.Trim();
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(candidate[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsed);
        }

        return ulong.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    }

    private static string Norm2(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return string.Join(' ', Pattern3.Matches(normalized).Select(match => match.Value));
    }

    private static HashKind Parse(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "PPU" => HashKind.Ppu,
            "SPU" => HashKind.Spu,
            "PRX" => HashKind.Prx,
            "OVL" => HashKind.Ovl,
            _ => HashKind.Unknown
        };
    }

    private static string Decode(string value)
    {
        var suffix = value.LastIndexOf(Suffix, StringComparison.Ordinal);
        if (suffix < 0)
        {
            return value;
        }

        var number = value[(suffix + Suffix.Length)..];
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? value[..suffix]
            : value;
    }

    private static YamlNode? TryGet(YamlMappingNode mapping, string key)
    {
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            if (string.Equals(
                    Decode(Scalar(keyNode) ?? string.Empty),
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

    private static CanCatRs Failure(string code, string message)
    {
        var diagnostic = new CanDiag(
            CanSev.Error,
            code,
            message);
        return new CanCatRs(
            false,
            new CanCat(null, [], [diagnostic], 0, 0));
    }
}

public sealed class FamIndex
{
    public IReadOnlyList<CanFam> ListFam(CanCat catalog)
    {
        return catalog.DefList
            .GroupBy(FamKey, StringComparer.Ordinal)
            .Select(BuildFam)
            .OrderBy(family => family.NormName, StringComparer.Ordinal)
            .ThenBy(family => family.FamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    public CanRpt BuildRpt(CanCat catalog)
    {
        var families = ListFam(catalog);
        var moduleCounts = Enum.GetValues<HashKind>()
            .Where(kind => kind != HashKind.Unknown)
            .ToDictionary(
                kind => kind,
                kind => catalog.DefList.Count(item => item.ModKind == kind));

        var simpleCount = catalog.DefList.Count(item => item.IsAuto);
        return new CanRpt(
            catalog.DefList.Count,
            moduleCounts,
            catalog.BadDefs,
            families.Count,
            families.Count(family => family.RefList.Count > 1),
            simpleCount,
            catalog.DefList.Count - simpleCount,
            families,
            catalog.Diags);
    }

    private static CanFam BuildFam(IGrouping<string, CanDef> group)
    {
        var entries = group
            .OrderBy(item => item.ModKind)
            .ThenBy(item => item.ModHash, StringComparer.Ordinal)
            .ThenBy(item => item.PatVer, StringComparer.Ordinal)
            .ToArray();
        var complexities = entries.Select(item => item.Cpx).Distinct().ToArray();
        var complexity = entries.Select(item => item.ModKind).Distinct().Count() > 1
            ? CanCmp.Module
            : complexities.OrderByDescending(CpxRank).First();

        return new CanFam(
            "HCF-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key)))[..16],
            entries[0].Title,
            entries[0].NormName,
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
            entries.Select(item => item.NormHash ?? item.ModHash)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            entries.Select(item => item.InsPrint)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            complexity);
    }

    private static string FamKey(CanDef definition)
    {
        var gameTitles = definition.GameTgt
            .Select(target => Norm(target.Title))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var anchorStructure = definition.AncDeps.Count == 0
            ? "none"
            : string.Join(',', definition.InsList
                .Where(item => item.AncName is not null)
                .GroupBy(item => item.AncName, StringComparer.Ordinal)
                .Select(group => group.Count())
                .Order());

        return string.Join(
            "\0",
            definition.NormName,
            definition.InsPrint,
            Norm(definition.Group),
            Norm(definition.Notes),
            string.Join('|', gameTitles),
            anchorStructure,
            definition.GameTgt.Count == 0 ? "invalid-targets" : "validated-targets");
    }

    private static int CpxRank(CanCmp complexity)
    {
        return complexity switch
        {
            CanCmp.NoSupp => 100,
            CanCmp.Dynamic => 90,
            CanCmp.Module => 80,
            CanCmp.CodeCave => 70,
            CanCmp.AncBased => 60,
            CanCmp.Pat => 50,
            CanCmp.MultiIns => 40,
            CanCmp.Ins => 30,
            CanCmp.Change => 20,
            _ => 10
        };
    }

    private static string Region(string serial)
    {
        if (serial.Equals(PatVer.All, StringComparison.Ordinal))
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

    private static string Norm(string? value)
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

public sealed class PortEvi
{
    private static readonly Regex Pattern = new(
        @"[\p{L}\p{Nd}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<PortAnlz> Analyze(
        CanCat catalog,
        R3Game game)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(game);

        if (string.IsNullOrWhiteSpace(game.Serial))
        {
            return [];
        }

        var families = new FamIndex().ListFam(catalog);
        var targetTitle = Norm2(game.Title);
        var output = new List<PortAnlz>();

        foreach (var family in families)
        {
            var serialReferences = family.RefList
                .Where(reference => HasTgt(reference, game.Serial))
                .ToArray();
            var titleReferences = family.RefList
                .Where(reference => HasTitle(reference, targetTitle))
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
                    .Where(reference => Supports(reference, game.Serial, game.Version))
                    .ToArray();

                if (versionReferences.Length == 0)
                {
                    output.Add(Create(
                        family,
                        game,
                        PortStat.NoSupp,
                        candidateReferences,
                        [],
                        false,
                        $"The selected serial is referenced, but version '{Display(game.Version)}' is not declared by this family."));
                    continue;
                }

                candidateReferences = versionReferences;
            }

            if (Requires(candidateReferences))
            {
                output.Add(Create(
                    family,
                    game,
                    PortStat.Review,
                    candidateReferences,
                    [],
                    false,
                    "PRX, OVL, unknown, and multi-module families require explicit per-module evidence outside the H2 PPU/SPU subset."));
                continue;
            }

            var targetHashes = Tgt(candidateReferences, game);
            if (targetHashes.Count == 0)
            {
                output.Add(Create(
                    family,
                    game,
                    PortStat.Missing,
                    candidateReferences,
                    [],
                    false,
                    "No exact PPU/SPU executable hash evidence is available for the selected game."));
                continue;
            }

            var exactReferences = candidateReferences
                .Where(reference => RefHash(reference, game))
                .ToArray();

            if (exactReferences.Length > 0)
            {
                if (HasClash(exactReferences))
                {
                    output.Add(Create(
                        family,
                        game,
                        PortStat.ClashRef,
                        candidateReferences,
                        exactReferences,
                        false,
                        "The exact executable hash resolves to multiple incompatible reference layouts."));
                    continue;
                }

                var declaredTarget = exactReferences.Any(reference =>
                    HasTgt(reference, game.Serial) &&
                    Supports(reference, game.Serial, game.Version));
                var reason = declaredTarget
                    ? "The target executable hash matches a declared reference for the selected serial and version."
                    : "The target executable hash matches a verified reference for the same normalized game title.";

                output.Add(Create(
                    family,
                    game,
                    PortStat.ExactRef,
                    candidateReferences,
                    exactReferences,
                    true,
                    reason));
                continue;
            }

            if (!IsAuto(family))
            {
                output.Add(Create(
                    family,
                    game,
                    PortStat.Review,
                    candidateReferences,
                    [],
                    false,
                    $"Family complexity '{family.Cpx}' is outside the bounded automatic H2 subset."));
                continue;
            }

            output.Add(Create(
                family,
                game,
                PortStat.NoPort,
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

    private static PortAnlz Create(
        CanFam family,
        R3Game game,
        PortStat status,
        IReadOnlyList<CanDef> candidateReferences,
        IReadOnlyList<CanDef> exactReferences,
        bool hasDeterministicEvidence,
        string reason)
    {
        return new PortAnlz(
            family.FamilyId,
            family.Title,
            status,
            game.Serial,
            PatVer.Norm(game.Version),
            game.HashEvi,
            game.HashSrc,
            Tgt(candidateReferences, game),
            candidateReferences,
            exactReferences,
            hasDeterministicEvidence,
            reason);
    }

    private static bool HasTgt(
        CanDef reference,
        string serial)
    {
        return reference.GameTgt.Any(target =>
            target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) ||
            PatVer.IsAll(target.Serial));
    }

    private static bool HasTitle(
        CanDef reference,
        string normalizedTitle)
    {
        return normalizedTitle.Length > 0 && reference.GameTgt.Any(target =>
            Norm2(target.Title).Equals(normalizedTitle, StringComparison.Ordinal));
    }

    private static bool Supports(
        CanDef reference,
        string serial,
        string? version)
    {
        var targets = reference.GameTgt.Where(target =>
            target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) ||
            PatVer.IsAll(target.Serial));

        foreach (var target in targets)
        {
            if (target.Versions.Count == 0 || target.Versions.Any(PatVer.IsAll))
            {
                return true;
            }

            if (version is not null && target.Versions.Any(candidate => PatVer.Equals(candidate, version)))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> Tgt(
        IReadOnlyList<CanDef> references,
        R3Game game)
    {
        var kinds = references
            .Select(reference => reference.ModKind)
            .Distinct()
            .ToHashSet();
        var hashes = new List<string>();

        if (kinds.Contains(HashKind.Ppu))
        {
            hashes.AddRange(game.Hashes);
        }

        if (kinds.Contains(HashKind.Spu))
        {
            hashes.AddRange(game.SpuList);
        }

        return hashes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool RefHash(
        CanDef reference,
        R3Game game)
    {
        var normalized = reference.NormHash;
        if (normalized is null)
        {
            return false;
        }

        var targetHashes = reference.ModKind switch
        {
            HashKind.Ppu => game.Hashes,
            HashKind.Spu => game.SpuList,
            _ => []
        };

        return targetHashes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasClash(
        IReadOnlyList<CanDef> references)
    {
        return references
            .Select(Layout)
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any();
    }

    private static string Layout(CanDef reference)
    {
        var canonical = new StringBuilder();
        foreach (var instruction in reference.InsList)
        {
            canonical.Append(instruction.Kind)
                .Append('|')
                .Append(Norm(instruction.Address))
                .Append('|')
                .Append(Norm(instruction.Value))
                .Append('|')
                .Append(instruction.AncName ?? string.Empty)
                .Append('|')
                .AppendJoin(',', instruction.ExtraArg.Select(Norm))
                .Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string Norm(string value)
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

    private static bool Requires(
        IReadOnlyList<CanDef> references)
    {
        var kinds = references
            .Select(reference => reference.ModKind)
            .Distinct()
            .ToArray();

        return kinds.Length != 1 || kinds[0] is not (HashKind.Ppu or HashKind.Spu);
    }

    private static bool IsAuto(CanFam family)
    {
        if (family.RefList.Any(reference => !reference.IsAuto))
        {
            return false;
        }

        return family.Cpx is
            CanCmp.Write or
            CanCmp.Change or
            CanCmp.Ins or
            CanCmp.MultiIns;
    }

    private static string Display(string? version)
    {
        return PatVer.Norm(version) ?? "<unknown>";
    }

    private static string Norm2(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            Pattern.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
                .Select(match => match.Value));
    }
}

public sealed class HashExt
{
    private static readonly Regex Pattern = new(
        @"\b(?<kind>PPU|SPU) executable hash:\s*(?<key>(?:PPU|SPU)-(?<hash>[0-9a-f]{40}))(?![0-9a-f])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public HashRead Read(string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return new HashRead(false, string.Empty, [], "RPCS3.log path is empty.");
        }

        var fullPath = Path.GetFullPath(logPath);
        if (!File.Exists(fullPath))
        {
            return new HashRead(false, fullPath, [], "RPCS3.log was not found.");
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
            return new HashRead(false, fullPath, [], ex.Message);
        }
    }

    public HashRead Parse(string text, string sourcePath = "RPCS3.log")
    {
        ArgumentNullException.ThrowIfNull(text);
        var hashes = new List<HCanHash>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Pattern.Matches(text))
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

            hashes.Add(new HCanHash(
                prefix == "PPU" ? HashKind.Ppu : HashKind.Spu,
                hash,
                sourcePath,
                line));
        }

        return new HashRead(true, sourcePath, hashes);
    }
}

public sealed class PatAuth
{
    public const string PatVer = "1.2";
    public const string MetaName = "r3hcan.json";
    public const string OldName = "r3config_hcanary.json";

    private static readonly Regex Pattern = new(
        @"^(?<kind>PPU|SPU)-(?<hash>[0-9a-fA-F]{40})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Pattern2 = new(
        @"^0x[0-9a-fA-F]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Pattern3 = new(
        @"^[A-Za-z0-9]{9}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Pattern4 = new(
        @"^(?:[0-9]{2}\.[0-9]{2}|All)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Pattern5 = new(
        @"^[0-9]+(?:\.[0-9]+){1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Pattern6 = new(
        @"^[A-Za-z_][A-Za-z0-9_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly IReadOnlyList<string> Op =
    [
        "be32", "be16", "be64", "be8", "byte", "le16", "le32", "le64",
        "bef32", "bef64", "lef32", "lef64", "bd32", "bd64", "bpex",
        "alloc", "calloc", "jump", "jumpl", "jumpf", "utf8", "cutf8",
        "move_file", "hide_file", "load"
    ];

    private static readonly HashSet<string> OpSet = new(Op, StringComparer.Ordinal);
    private readonly ReadSvc _reader;
    private readonly PatTxn _Txn;

    public PatAuth(
        ReadSvc? reader = null,
        PatTxn? transaction = null)
    {
        _reader = reader ?? new ReadSvc();
        _Txn = transaction ?? new PatTxn();
    }

    public VldRes Validate(HCanDrft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var issues = new List<VldIssue>();
        var hashMatch = Pattern.Match(draft.ExeHash.Trim());
        if (!hashMatch.Success)
        {
            Add("Hash.Invalid", "ExecutableHash", "Executable Hash must use PPU-/SPU- followed by 40 hexadecimal characters.");
        }
        else if (hashMatch.Groups["hash"].Value.All(character => character == '0'))
        {
            Add("Hash.Zero", "ExecutableHash", "Executable Hash cannot be all zeroes.");
        }
        else if (draft.PatType == HCanType.Ppu && !hashMatch.Groups["kind"].Value.Equals("PPU", StringComparison.Ordinal))
        {
            Add("Hash.Kind", "ExecutableHash", "PPU patch type requires a PPU Hash.");
        }
        else if (draft.PatType == HCanType.Spu && !hashMatch.Groups["kind"].Value.Equals("SPU", StringComparison.Ordinal))
        {
            Add("Hash.Kind", "ExecutableHash", "SPU patch type requires an SPU Hash.");
        }

        Required(draft.PatName, "PatchName", "PatchName.Required", "Patch Name is required.");
        Required(draft.PatVer, "PatchVersion", "PatchVersion.Required", "Patch Version is required.");
        if (!string.IsNullOrWhiteSpace(draft.PatVer) && !Pattern5.IsMatch(draft.PatVer.Trim()))
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
            Required(target.GameVer, $"Games[{index}].GameVersion", "GameVersion.Required", "Game Version is required.");
            if (!string.IsNullOrWhiteSpace(target.GameId) && !Pattern3.IsMatch(target.GameId.Trim()))
            {
                Add("GameId.Invalid", $"Games[{index}].GameId", "Game ID must contain exactly nine Latin letters or digits.");
            }
            if (!string.IsNullOrWhiteSpace(target.GameVer) && !Pattern4.IsMatch(target.GameVer.Trim()))
            {
                Add("GameVersion.Invalid", $"Games[{index}].GameVersion", "Game Version must be NN.NN or All.");
            }
        }

        if (draft.Ins.Count == 0)
        {
            Add("Instructions.Required", "Instructions", "At least one Patch instruction is required.");
        }

        var anchorNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < draft.Anchors.Count; index++)
        {
            var anchor = draft.Anchors[index];
            if (!Pattern6.IsMatch(anchor.Name.Trim()))
            {
                Add("Anchor.Invalid", $"Anchors[{index}].Name", "Anchor name must be a valid ASCII YAML anchor name.");
            }
            else if (!anchorNames.Add(anchor.Name.Trim()))
            {
                Add("Anchor.Duplicate", $"Anchors[{index}].Name", "Anchor name is duplicated.");
            }
            if (anchor.Ins.Count == 0)
            {
                Add("Anchor.Empty", $"Anchors[{index}].Instructions", "Anchor requires at least one instruction.");
            }
            VldIns(anchor.Ins, $"Anchors[{index}].Instructions", allowLoad: false);
        }

        VldIns(draft.Ins, "Instructions", allowLoad: true);

        string yaml;
        try
        {
            yaml = Generate(draft);
            var read = _reader.ReadText(yaml, PatSrc.Imported);
            if (!read.IsOk)
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

        return new VldRes(issues, yaml);

        void Required(string value, string field, string code, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(code, field, message);
            }
        }

        void Add(string code, string field, string message) =>
            issues.Add(new VldIssue(code, field, message));

        void VldIns(IReadOnlyList<HCanInst> instructions, string field, bool allowLoad)
        {
            for (var index = 0; index < instructions.Count; index++)
            {
                var instruction = instructions[index];
                var itemField = $"{field}[{index}]";
                var operation = instruction.Op.Trim();
                if (!OpSet.Contains(operation))
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
                if (!Pattern2.IsMatch(instruction.Address.Trim()))
                {
                    Add("Address.Invalid", itemField + ".Address", "Address must be hexadecimal and start with 0x.");
                }
                if (string.IsNullOrWhiteSpace(instruction.Value) ||
                    (HexVal(operation) && !Pattern2.IsMatch(instruction.Value.Trim())))
                {
                    Add("Value.Invalid", itemField + ".Value", "Value is invalid for this operation.");
                }
            }
        }
    }

    public string Generate(HCanDrft draft)
    {
        var lines = new List<string> { "Version: 1.2", string.Empty };
        if (draft.Anchors.Count > 0)
        {
            lines.Add("Anchors:");
            foreach (var anchor in draft.Anchors)
            {
                lines.Add($"  {anchor.Name.Trim()}: &{anchor.Name.Trim()}");
                foreach (var instruction in anchor.Ins)
                {
                    lines.Add("    " + InsYaml(instruction));
                }
            }
            lines.Add(string.Empty);
        }

        lines.Add(draft.ExeHash.Trim() + ":");
        lines.Add("  " + Quote(draft.PatName.Trim()) + ":");
        lines.Add("    Games:");
        foreach (var titleGroup in draft.Games.GroupBy(item => item.GameName.Trim(), StringComparer.Ordinal))
        {
            lines.Add("      " + Quote(titleGroup.Key) + ":");
            foreach (var idGroup in titleGroup.GroupBy(item => item.GameId.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            {
                var versions = idGroup.Select(item => NormVer(item.GameVer))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                lines.Add($"        {idGroup.Key}: [ {string.Join(", ", versions)} ]");
            }
        }
        Optional("Author", draft.Author);
        Optional("Notes", draft.Notes);
        Optional("Group", draft.Group);
        lines.Add("    Patch Version: " + draft.PatVer.Trim());
        lines.Add("    Patch:");
        foreach (var instruction in draft.Ins)
        {
            lines.Add("      " + InsYaml(instruction));
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

    public IReadOnlyList<HCanStor> Load(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return [];
        }

        var text = R3Bound.ReadUtf8(
            path,
            R3Limits.PatCatBy,
            "imported_patch.yml");
        var catalog = _reader.ReadText(text, PatSrc.Imported, path);
        if (!catalog.IsOk)
        {
            throw new InvalidDataException(catalog.Error ?? "imported_patch.yml is invalid.");
        }

        var yaml = LoadYaml(text);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var anchors = ReadAnc(root);
        var output = new List<HCanStor>();
        var metadata = ReadMeta(path);
        foreach (var (keyNode, valueNode) in root.Children)
        {
            var hashKey = Scalar(keyNode);
            var match = Pattern.Match(hashKey);
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
                    ? HCanType.Ppu
                    : HCanType.Spu;
                var isLegacyCanary =
                    localMetadata?.PatType == HCanType.Can;
                var draft = new HCanDrft
                {
                    PatType = isLegacyCanary
                        ? inferredPatchType
                        : localMetadata?.PatType ?? inferredPatchType,
                    IsExp =
                        localMetadata?.IsExp == true ||
                        isLegacyCanary,
                    ExeHash = hashKey,
                    PatName = title,
                    Author = Scalar(TryGet(details, "Author")),
                    Notes = Scalar(TryGet(details, "Notes")),
                    Group = Scalar(TryGet(details, "Group")),
                    PatVer = Scalar(TryGet(details, "Patch Version")),
                    Status = localMetadata?.Status ?? DraftSt.Saved,
                    Games = Read(TryGet(details, "Games")),
                    Ins = ReadIns(TryGet(details, "Patch"), out var dependencies)
                };
                draft.Anchors = dependencies.Distinct(StringComparer.Ordinal)
                    .Where(anchors.ContainsKey)
                    .Select(name => new HCanAnc
                    {
                        Name = name,
                        Ins = ReadIns(anchors[name], out _)
                    }).ToList();
                output.Add(new HCanStor(identity, $"{title} — {hashKey}", draft));
            }
        }
        return output;
    }

    public SaveRes Save(
        string importedPatchPath,
        string backupRootPath,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false,
        PathVld? validatePaths = null)
    {
        var validation = Validate(draft);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        }

        var existing = File.Exists(importedPatchPath) && new FileInfo(importedPatchPath).Length > 0
            ? R3Bound.ReadUtf8(
                importedPatchPath,
                R3Limits.PatCatBy,
                "imported_patch.yml")
            : "Version: 1.2\r\n";
        var yaml = LoadYaml(existing);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var newIdentity = Identity(draft.ExeHash.Trim(), draft.PatName.Trim());
        var replaced = false;

        if (!saveAsNew && !string.IsNullOrWhiteSpace(originalIdentity))
        {
            replaced = DelPat(root, originalIdentity);
        }
        if (FindPat(root, newIdentity) is not null)
        {
            throw new InvalidOperationException("A Patch with the same name and executable Hash already exists.");
        }

        var anchorNodes = MergeAnc(root, draft.Anchors);
        var hashMap = GetOr(root, draft.ExeHash.Trim());
        hashMap.Add(new YamlScalarNode(draft.PatName.Trim()), Build(draft, anchorNodes));
        var text = Write2(yaml);
        var read = _reader.ReadText(text, PatSrc.Imported, importedPatchPath);
        if (!read.IsOk)
        {
            throw new InvalidDataException(read.Error ?? "Merged imported_patch.yml is invalid.");
        }

        var metadataPath = MetaPath(importedPatchPath);
        var metadata = ReadMeta(importedPatchPath);
        if (!saveAsNew && !string.IsNullOrWhiteSpace(originalIdentity)) metadata.Remove(originalIdentity);
        metadata[newIdentity] = new LocMeta(
            draft.PatType,
            draft.Status == DraftSt.Draft ? DraftSt.Saved : draft.Status,
            draft.IsExp);
        var metadataText = Write(metadata);
        var backup = _Txn.Commit(backupRootPath,
        [
            new PrepFile(importedPatchPath, text, PrepKind.PatCat,
                PatSrc.Imported, read.Catalog.Entries.Count),
            new PrepFile(metadataPath, metadataText, PrepKind.JsonMeta)
        ], validatePaths);
        return new SaveRes(importedPatchPath, backup, read.Catalog.Entries.Count, replaced);
    }

    public SaveRes Delete(
        string importedPatchPath,
        string backupRootPath,
        string identity,
        PathVld? validatePaths = null)
    {
        if (!File.Exists(importedPatchPath))
        {
            throw new FileNotFoundException("imported_patch.yml was not found.", importedPatchPath);
        }
        var yaml = LoadYaml(
            R3Bound.ReadUtf8(
                importedPatchPath,
                R3Limits.PatCatBy,
                "imported_patch.yml"));
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        if (!DelPat(root, identity))
        {
            throw new InvalidOperationException("The selected custom Patch no longer exists.");
        }
        var text = Write2(yaml);
        var read = _reader.ReadText(text, PatSrc.Imported, importedPatchPath);
        if (!read.IsOk)
        {
            throw new InvalidDataException(read.Error ?? "Updated imported_patch.yml is invalid.");
        }
        var metadataPath = MetaPath(importedPatchPath);
        var metadata = ReadMeta(importedPatchPath);
        metadata.Remove(identity);
        var backup = _Txn.Commit(backupRootPath,
        [
            new PrepFile(importedPatchPath, text, PrepKind.PatCat,
                PatSrc.Imported, read.Catalog.Entries.Count),
            new PrepFile(metadataPath, Write(metadata), PrepKind.JsonMeta)
        ], validatePaths);
        return new SaveRes(importedPatchPath, backup, read.Catalog.Entries.Count, true);
    }

    private static bool HexVal(string operation) => operation is not
        ("utf8" or "cutf8" or "move_file" or "hide_file" or "alloc" or "calloc" or "jump" or "jumpl" or "jumpf");

    private static string NormVer(string value) =>
        value.Trim().Equals("All", StringComparison.OrdinalIgnoreCase) ? "All" : value.Trim();

    private static string InsYaml(HCanInst instruction)
    {
        if (instruction.Op.Trim() == "load")
        {
            return $"- [ load, *{instruction.Address.Trim().TrimStart('*')} ]";
        }
        return $"- [ {instruction.Op.Trim()}, {instruction.Address.Trim()}, {instruction.Value.Trim()} ]";
    }

    private static string Quote(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string Identity(string hashKey, string title) => hashKey.Trim() + "\0" + title.Trim();

    public PatCat Apply(PatCat catalog, string importedPatchPath)
    {
        var metadata = ReadMeta(importedPatchPath);
        if (metadata.Count == 0) return catalog;
        return catalog with
        {
            Entries = catalog.Entries.Select(entry =>
            {
                var key = $"{entry.HashKind.ToString().ToUpperInvariant()}-{entry.Hash}\0{entry.Title}";
                if (!metadata.TryGetValue(key, out var value) ||
                    (!value.IsExp &&
                     value.PatType != HCanType.Can))
                {
                    return entry;
                }
                return entry with { Risk = Mdl.PatRisk.Canary, PatType = "Canary" };
            }).ToArray()
        };
    }

    private static string MetaPath(string importedPatchPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(importedPatchPath))!, MetaName);

    private static string OldMeta(string importedPatchPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(importedPatchPath))!, OldName);

    private static Dictionary<string, LocMeta> ReadMeta(string importedPatchPath)
    {
        var current = MetaPath(importedPatchPath);
        return File.Exists(current)
            ? ReadOne(current)
            : ReadOne(OldMeta(importedPatchPath));
    }

    private static Dictionary<string, LocMeta> ReadOne(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new Dictionary<string, LocMeta>(StringComparer.Ordinal);
        }
        try
        {
            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.HBytes,
                "R3Config H-Canary metadata");
            return JsonSerializer.Deserialize<Dictionary<string, LocMeta>>(text)
                   ?? new Dictionary<string, LocMeta>(StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("R3Config H-Canary metadata is invalid.", ex);
        }
    }

    private static string Write(IReadOnlyDictionary<string, LocMeta> metadata)
    {
        var text = JsonSerializer.Serialize(
                metadata,
                new JsonSerializerOptions { WriteIndented = true })
            .Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n";
        R3Bound.Check(
            text,
            R3Limits.HBytes,
            "R3Config H-Canary metadata");
        return text;
    }

    public sealed record LocMeta(
        [property: JsonPropertyName("PatchType")] HCanType PatType,
        DraftSt Status,
        [property: JsonPropertyName("IsExperimental")] bool IsExp = false);

    private static YamlStream LoadYaml(string text)
    {
        try
        {
            var yaml = R3Bound.LoadYaml(
                text,
                "imported_patch.yml");
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                throw new InvalidDataException("imported_patch.yml must contain one YAML mapping document.");
            }
            if (!Scalar(TryGet(root, "Version")).Equals(PatVer, StringComparison.Ordinal))
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

    private static string Write2(YamlStream yaml)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        yaml.Save(writer, assignAnchors: false);
        var text = writer.ToString().Replace("\r\r\n", "\r\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal)
            .Replace("\r\r\n", "\r\n", StringComparison.Ordinal);
        return text.TrimEnd('\r', '\n') + "\r\n";
    }

    private static Dictionary<string, YamlSequenceNode> ReadAnc(YamlMappingNode root)
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

    private static List<HCanTgt> Read(YamlNode? node)
    {
        var output = new List<HCanTgt>();
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
                output.AddRange(versions.Select(version => new HCanTgt
                {
                    GameName = Scalar(titleNode), GameId = Scalar(idNode), GameVer = version
                }));
            }
        }
        return output;
    }

    private static List<HCanInst> ReadIns(YamlNode? node, out List<string> dependencies)
    {
        dependencies = [];
        var output = new List<HCanInst>();
        if (node is not YamlSequenceNode patch) return output;
        foreach (var child in patch.Children.OfType<YamlSequenceNode>())
        {
            var operation = child.Children.Count > 0 ? Scalar(child.Children[0]) : string.Empty;
            if (operation == "load" && child.Children.Count > 1 && child.Children[1] is YamlSequenceNode loaded)
            {
                var name = loaded.Anchor.IsEmpty ? string.Empty : loaded.Anchor.Value;
                dependencies.Add(name);
                output.Add(new HCanInst { Op = "load", Address = name, Value = string.Empty });
            }
            else if (child.Children.Count >= 3)
            {
                output.Add(new HCanInst
                {
                    Op = operation,
                    Address = Scalar(child.Children[1]),
                    Value = Scalar(child.Children[2])
                });
            }
        }
        return output;
    }

    private static Dictionary<string, YamlSequenceNode> MergeAnc(YamlMappingNode root, IReadOnlyList<HCanAnc> anchors)
    {
        var nodes = ReadAnc(root);
        YamlMappingNode? anchorMap = root.Children.FirstOrDefault(pair => Scalar(pair.Key).Equals("Anchors", StringComparison.OrdinalIgnoreCase)).Value as YamlMappingNode;
        if (anchors.Count > 0 && anchorMap is null)
        {
            anchorMap = new YamlMappingNode();
            root.Add(new YamlScalarNode("Anchors"), anchorMap);
        }
        if (anchorMap is not null)
        {
            Move(root, anchorMap);
        }
        foreach (var anchor in anchors)
        {
            var name = anchor.Name.Trim();
            var newNode = new YamlSequenceNode(anchor.Ins.Select(BuildIns)) { Anchor = name };
            if (nodes.TryGetValue(name, out var existing))
            {
                if (!Node(existing).Equals(Node(newNode), StringComparison.Ordinal))
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

    private static void Move(YamlMappingNode root, YamlMappingNode anchorMap)
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

    private static YamlMappingNode Build(HCanDrft draft, IReadOnlyDictionary<string, YamlSequenceNode> anchors)
    {
        var details = new YamlMappingNode();
        var games = new YamlMappingNode();
        foreach (var titleGroup in draft.Games.GroupBy(item => item.GameName.Trim(), StringComparer.Ordinal))
        {
            var ids = new YamlMappingNode();
            foreach (var idGroup in titleGroup.GroupBy(item => item.GameId.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            {
                ids.Add(idGroup.Key, new YamlSequenceNode(idGroup.Select(item => new YamlScalarNode(NormVer(item.GameVer)))));
            }
            games.Add(titleGroup.Key, ids);
        }
        details.Add("Games", games);
        Add(details, "Author", draft.Author);
        Add(details, "Notes", draft.Notes);
        Add(details, "Group", draft.Group);
        details.Add("Patch Version", draft.PatVer.Trim());
        var patch = new YamlSequenceNode();
        foreach (var instruction in draft.Ins)
        {
            if (instruction.Op.Trim() == "load")
            {
                var name = instruction.Address.Trim().TrimStart('*');
                patch.Add(new YamlSequenceNode(new YamlScalarNode("load"), anchors[name]));
            }
            else
            {
                patch.Add(BuildIns(instruction));
            }
        }
        details.Add("Patch", patch);
        return details;
    }

    private static YamlSequenceNode BuildIns(HCanInst instruction) => new(
        new YamlScalarNode(instruction.Op.Trim()),
        new YamlScalarNode(instruction.Address.Trim()),
        new YamlScalarNode(instruction.Value.Trim()));

    private static void Add(YamlMappingNode map, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) map.Add(key, value);
    }

    private static YamlMappingNode GetOr(YamlMappingNode root, string hash)
    {
        if (TryGet(root, hash) is YamlMappingNode existing) return existing;
        var created = new YamlMappingNode();
        root.Add(hash, created);
        return created;
    }

    private static YamlNode? FindPat(YamlMappingNode root, string identity)
    {
        var split = identity.Split('\0', 2);
        return split.Length == 2 && TryGet(root, split[0]) is YamlMappingNode map ? TryGet(map, split[1]) : null;
    }

    private static bool DelPat(YamlMappingNode root, string identity)
    {
        var split = identity.Split('\0', 2);
        if (split.Length != 2 || TryPair(root, split[0]) is not { } hashPair || hashPair.Value is not YamlMappingNode map ||
            TryPair(map, split[1]) is not { } patchPair)
        {
            return false;
        }
        map.Children.Remove(patchPair.Key);
        if (map.Children.Count == 0) root.Children.Remove(hashPair.Key);
        return true;
    }

    private static KeyValuePair<YamlNode, YamlNode>? TryPair(YamlMappingNode map, string key)
    {
        foreach (var pair in map.Children)
        {
            if (Scalar(pair.Key).Equals(key, StringComparison.Ordinal)) return pair;
        }
        return null;
    }

    private static YamlNode? TryGet(YamlMappingNode map, string key) => TryPair(map, key)?.Value;
    private static string Scalar(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;

    private static string Node(YamlNode node)
    {
        var stream = new YamlStream(new YamlDocument(node));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        stream.Save(writer, assignAnchors: false);
        return writer.ToString().Replace("&" + (node.Anchor.IsEmpty ? string.Empty : node.Anchor.Value), string.Empty, StringComparison.Ordinal);
    }
}
