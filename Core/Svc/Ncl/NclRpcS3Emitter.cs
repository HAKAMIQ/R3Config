using System.Globalization;
using R3Config.Mdl;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public static class NclRpcS3Emitter
{
    private const string RpcS3PatchVersion = "1.2";
    private const string DefinitionVersion = "1.0";

    public static NclRpcS3EmitResult Emit(
        NclRpcS3EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Binding);

        if (!request.Binding.IsOk ||
            request.Binding.Code != NclAddressBindCode.Success)
        {
            return Failure(
                NclRpcS3EmitCode.BindingRejected,
                request.Binding.Error ??
                "Address binding did not produce a verified RPCS3 write set.");
        }

        if (request.Binding.Writes.Count == 0)
        {
            return Failure(
                NclRpcS3EmitCode.InvalidWrite,
                "A successful address binding must contain at least one write.");
        }

        if (!TryMetadata(
                request,
                out var title,
                out var gameTitle,
                out var serial,
                out var version,
                out var metadataError))
        {
            return Failure(
                NclRpcS3EmitCode.InvalidMetadata,
                metadataError);
        }

        var canonicalRunKey = string.Empty;
        var expected = new List<ByteEffect>();
        foreach (var write in request.Binding.Writes)
        {
            if (!RunKey.TryParse(
                    write.RunKey,
                    out var parsedRunKey))
            {
                return Failure(
                    NclRpcS3EmitCode.InvalidRunKey,
                    $"Bound write from source line {write.SourceLine} has invalid RPCS3 run key '{write.RunKey}'.");
            }

            if (!string.IsNullOrWhiteSpace(parsedRunKey.SerScope) &&
                !parsedRunKey.SerScope.Equals(
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    NclRpcS3EmitCode.InvalidMetadata,
                    $"Bound RPCS3 run key serial scope '{parsedRunKey.SerScope}' does not match emitted game serial '{serial}'.");
            }

            var currentRunKey = RunKey.Build(
                parsedRunKey.Kind,
                parsedRunKey.NormHash,
                parsedRunKey.SerScope);
            if (string.IsNullOrWhiteSpace(currentRunKey))
            {
                return Failure(
                    NclRpcS3EmitCode.InvalidRunKey,
                    $"Bound write from source line {write.SourceLine} could not be normalized to an RPCS3 run key.");
            }

            if (canonicalRunKey.Length == 0)
            {
                canonicalRunKey = currentRunKey;
            }
            else if (!canonicalRunKey.Equals(
                         currentRunKey,
                         StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    NclRpcS3EmitCode.MultipleRunKeys,
                    "One NCL cheat resolves to more than one RPCS3 run key. Cross-module activation is not emitted until atomic multi-run-key ownership is implemented.");
            }

            if (write.Bytes.Count == 0)
            {
                return Failure(
                    NclRpcS3EmitCode.InvalidWrite,
                    $"Bound write from source line {write.SourceLine} has an empty byte payload.");
            }

            for (var index = 0; index < write.Bytes.Count; index++)
            {
                var address = (ulong)write.PatchAddress + (ulong)index;
                if (address > uint.MaxValue)
                {
                    return Failure(
                        NclRpcS3EmitCode.InvalidWrite,
                        $"Bound write from source line {write.SourceLine} exceeds RPCS3's UInt32 patch-address range.");
                }

                expected.Add(
                    new ByteEffect(
                        (uint)address,
                        write.Bytes[index]));
            }
        }

        if (expected.Count == 0)
        {
            return Failure(
                NclRpcS3EmitCode.InvalidWrite,
                "The bound write set contains no concrete bytes.");
        }

        if (expected.Count > R3Limits.YamlSeq)
        {
            return Failure(
                NclRpcS3EmitCode.InstructionLimit,
                $"Byte-exact emission requires {expected.Count:N0} RPCS3 instructions, exceeding the {R3Limits.YamlSeq:N0}-instruction YAML sequence limit.");
        }

        var patch = new YamlSequenceNode();
        foreach (var effect in expected)
        {
            patch.Add(
                new YamlSequenceNode(
                    new YamlScalarNode("byte"),
                    new YamlScalarNode(
                        $"0x{effect.Address:X8}"),
                    new YamlScalarNode(
                        $"0x{effect.Value:X2}")));
        }

        var versions = new YamlSequenceNode(
            new YamlScalarNode(version));
        var serials = new YamlMappingNode
        {
            {
                new YamlScalarNode(serial),
                versions
            }
        };
        var games = new YamlMappingNode
        {
            {
                new YamlScalarNode(gameTitle),
                serials
            }
        };

        var details = new YamlMappingNode
        {
            { "Games", games },
            { "Patch Version", DefinitionVersion },
            { "Patch", patch }
        };

        AddOptional(
            details,
            "Author",
            "HAKAMIQ");
        AddOptional(
            details,
            "Source Author",
            request.Author);
        AddOptional(
            details,
            "Notes",
            request.Notes);

        var root = new YamlMappingNode
        {
            { "Version", RpcS3PatchVersion },
            {
                new YamlScalarNode(canonicalRunKey),
                new YamlMappingNode
                {
                    {
                        new YamlScalarNode(title),
                        details
                    }
                }
            }
        };

        var patchText = Save(root);
        var read = new ReadSvc().ReadText(
            patchText,
            PatSrc.ManagedArtemis,
            sourcePath: null);

        if (!read.IsOk)
        {
            return Failure(
                NclRpcS3EmitCode.ValidationFailed,
                "Generated RPCS3 Patch 1.2 YAML failed ReadSvc validation: " +
                (read.Error ?? "unknown validation error"));
        }

        if (read.Catalog.Entries.Count != 1)
        {
            return Failure(
                NclRpcS3EmitCode.ValidationFailed,
                $"Generated RPCS3 Patch 1.2 YAML expected one catalog entry but ReadSvc returned {read.Catalog.Entries.Count}.");
        }

        var entry = read.Catalog.Entries[0];
        if (!entry.RunKey.Equals(
                canonicalRunKey,
                StringComparison.OrdinalIgnoreCase) ||
            !entry.Title.Equals(
                title,
                StringComparison.Ordinal) ||
            entry.GameTgt.Count != 1 ||
            !entry.GameTgt[0].Title.Equals(
                gameTitle,
                StringComparison.Ordinal) ||
            !entry.GameTgt[0].Serial.Equals(
                serial,
                StringComparison.OrdinalIgnoreCase) ||
            entry.GameTgt[0].Versions.Count != 1 ||
            !entry.GameTgt[0].Versions[0].Equals(
                version,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                NclRpcS3EmitCode.RoundTripMismatch,
                "ReadSvc round-trip changed the emitted run key, patch identity, or game target metadata.");
        }

        if (!TryReadByteEffects(
                patchText,
                canonicalRunKey,
                title,
                out var actual,
                out var roundTripError))
        {
            return Failure(
                NclRpcS3EmitCode.RoundTripMismatch,
                roundTripError);
        }

        if (actual.Count != expected.Count)
        {
            return Failure(
                NclRpcS3EmitCode.RoundTripMismatch,
                $"Byte round-trip count mismatch. Expected={expected.Count}, Actual={actual.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                return Failure(
                    NclRpcS3EmitCode.RoundTripMismatch,
                    $"Byte round-trip mismatch at instruction {index + 1}: expected 0x{expected[index].Address:X8}=0x{expected[index].Value:X2}, actual 0x{actual[index].Address:X8}=0x{actual[index].Value:X2}.");
            }
        }

        return new NclRpcS3EmitResult(
            true,
            NclRpcS3EmitCode.Success,
            patchText,
            canonicalRunKey,
            expected.Count,
            expected.Count,
            null);
    }

    private static bool TryMetadata(
        NclRpcS3EmitRequest request,
        out string title,
        out string gameTitle,
        out string serial,
        out string version,
        out string error)
    {
        title = request.PatchTitle?.Trim() ?? string.Empty;
        gameTitle = request.GameTitle?.Trim() ?? string.Empty;
        serial = request.Serial?.Trim().ToUpperInvariant() ?? string.Empty;
        version = request.Version?.Trim() ?? string.Empty;
        error = string.Empty;

        if (title.Length == 0)
        {
            error = "Patch title is required.";
            return false;
        }

        if (gameTitle.Length == 0)
        {
            error = "Game title is required.";
            return false;
        }

        if (!ValidSerial(serial))
        {
            error = "RPCS3 game serial must contain exactly nine ASCII letters or digits.";
            return false;
        }

        if (!ValidVersion(version))
        {
            error = "RPCS3 game version must use NN.NN or All.";
            return false;
        }

        return true;
    }

    private static bool ValidSerial(string serial)
    {
        return serial.Length == 9 &&
               serial.All(character =>
                   char.IsAsciiLetterOrDigit(character));
    }

    private static bool ValidVersion(string version)
    {
        if (version.Equals(
                PatVer.All,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return version.Length == 5 &&
               char.IsAsciiDigit(version[0]) &&
               char.IsAsciiDigit(version[1]) &&
               version[2] == '.' &&
               char.IsAsciiDigit(version[3]) &&
               char.IsAsciiDigit(version[4]);
    }

    private static void AddOptional(
        YamlMappingNode details,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            details.Add(
                new YamlScalarNode(key),
                new YamlScalarNode(value.Trim()));
        }
    }

    private static string Save(
        YamlMappingNode root)
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

        return string.Join("\r\n", lines).TrimEnd() +
               "\r\n";
    }

    private static bool TryReadByteEffects(
        string patchText,
        string runKey,
        string title,
        out IReadOnlyList<ByteEffect> effects,
        out string error)
    {
        effects = [];
        error = string.Empty;

        try
        {
            var yaml = R3Bound.LoadYaml(
                patchText,
                "NCL RPCS3 emitter round-trip");

            if (yaml.Documents.Count != 1 ||
                yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                error = "Round-trip YAML root is not one mapping document.";
                return false;
            }

            YamlMappingNode? patchMap = null;
            foreach (var (keyNode, valueNode) in root.Children)
            {
                var candidate = ReadSvc.Decode2(
                    Scalar(keyNode));

                if (!candidate.Equals(
                        runKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                patchMap = valueNode as YamlMappingNode;
                break;
            }

            if (patchMap is null)
            {
                error = "Round-trip YAML lost the expected RPCS3 run key.";
                return false;
            }

            YamlMappingNode? details = null;
            foreach (var (titleNode, detailsNode) in patchMap.Children)
            {
                var candidateTitle = ReadSvc.Decode(
                    Scalar(titleNode));

                if (!candidateTitle.Equals(
                        title,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                details = detailsNode as YamlMappingNode;
                break;
            }

            if (details is null ||
                TryGet(
                    details,
                    "Patch") is not YamlSequenceNode sequence)
            {
                error = "Round-trip YAML lost the expected Patch sequence.";
                return false;
            }

            var output = new List<ByteEffect>(
                sequence.Children.Count);

            foreach (var child in sequence.Children)
            {
                if (child is not YamlSequenceNode instruction ||
                    instruction.Children.Count != 3 ||
                    !Scalar(instruction.Children[0]).Equals(
                        "byte",
                        StringComparison.Ordinal) ||
                    !TryHexUInt32(
                        Scalar(instruction.Children[1]),
                        out var address) ||
                    !TryHexByte(
                        Scalar(instruction.Children[2]),
                        out var value))
                {
                    error = "Round-trip Patch sequence contains a non-canonical byte instruction.";
                    return false;
                }

                output.Add(
                    new ByteEffect(
                        address,
                        value));
            }

            effects = output;
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
                InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
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

    private static string Scalar(
        YamlNode node)
    {
        return node is YamlScalarNode scalar
            ? scalar.Value?.Trim() ?? string.Empty
            : node.ToString()?.Trim() ?? string.Empty;
    }

    private static bool TryHexUInt32(
        string text,
        out uint value)
    {
        value = 0;
        var candidate = text.Trim();
        return candidate.StartsWith(
                   "0x",
                   StringComparison.OrdinalIgnoreCase) &&
               uint.TryParse(
                   candidate.AsSpan(2),
                   NumberStyles.AllowHexSpecifier,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private static bool TryHexByte(
        string text,
        out byte value)
    {
        value = 0;
        var candidate = text.Trim();
        return candidate.StartsWith(
                   "0x",
                   StringComparison.OrdinalIgnoreCase) &&
               byte.TryParse(
                   candidate.AsSpan(2),
                   NumberStyles.AllowHexSpecifier,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private static NclRpcS3EmitResult Failure(
        NclRpcS3EmitCode code,
        string error)
    {
        return new NclRpcS3EmitResult(
            false,
            code,
            string.Empty,
            null,
            0,
            0,
            error);
    }

    private readonly record struct ByteEffect(
        uint Address,
        byte Value);
}
