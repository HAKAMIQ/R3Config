using System;
using System.Buffers.Binary;
using System.Linq;

namespace R3Config.Mdl;

public enum PatchHashKind
{
    Ppu,
    Spu,
    Prx,
    Ovl,
    Unknown
}

public enum PatchHashEvidenceKind
{
    None,
    ExistingMetadata,
    PlainLog,
    CompressedLog,
    GameCache
}

public static class Rpcs3ExecutableHash
{
    private const string Base57Alphabet =
        "0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (TryNormalizeHex(candidate, out normalized))
        {
            return true;
        }

        if (candidate.Length != 28 ||
            !TryDecodeBase57Sha1(candidate, out var bytes) ||
            bytes.All(valueByte => valueByte == 0))
        {
            return false;
        }

        normalized = Convert.ToHexString(bytes).ToLowerInvariant();
        return true;
    }

    public static bool TryNormalizeHex(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length != 40 ||
            !candidate.All(Uri.IsHexDigit) ||
            candidate.All(character => character == '0'))
        {
            return false;
        }

        normalized = candidate.ToLowerInvariant();
        return true;
    }

    private static bool TryDecodeBase57Sha1(string value, out byte[] bytes)
    {
        bytes = [];

        try
        {
            var decoded = new byte[20];
            var outputOffset = 0;
            Span<byte> blockBytes = stackalloc byte[8];

            for (var inputOffset = 0; inputOffset < value.Length; inputOffset += 11)
            {
                var blockLength = Math.Min(11, value.Length - inputOffset);
                var outputLength = blockLength == 11
                    ? 8
                    : TailByteCount(blockLength);

                if (outputLength == 0)
                {
                    return false;
                }

                ulong blockValue = 0;
                ulong multiplier = 0;

                for (var position = 10; position >= 0; position--)
                {
                    multiplier = multiplier == 0
                        ? 1
                        : checked(multiplier * 57);

                    if (position >= blockLength)
                    {
                        continue;
                    }

                    var digit = Base57Alphabet.IndexOf(value[inputOffset + position]);
                    if (digit < 0)
                    {
                        return false;
                    }

                    blockValue = checked(
                        blockValue +
                        checked((ulong)digit * multiplier));
                }

                blockBytes.Clear();
                BinaryPrimitives.WriteUInt64BigEndian(blockBytes, blockValue);
                blockBytes[..outputLength].CopyTo(
                    decoded.AsSpan(outputOffset, outputLength));

                outputOffset += outputLength;
            }

            if (outputOffset != decoded.Length)
            {
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static int TailByteCount(int encodedLength)
    {
        return encodedLength switch
        {
            2 => 1,
            3 => 2,
            5 => 3,
            6 => 4,
            7 => 5,
            9 => 6,
            10 => 7,
            _ => 0
        };
    }
}

public enum PatchSourceOrigin
{
    Official,
    Imported,
    BundledCanary,
    BundledCheats,
    ManagedCheats,
    TitleSpecific,
    Unknown
}

public enum PatchRisk
{
    SafeFix,
    VisualFix,
    Performance,
    Fps60,
    Cheat,
    Experimental,
    Canary,
    Unknown
}

public enum PatchMatchStatus
{
    ExactMatch,
    SerialVersionMatchHashUnknown,
    VersionUnknown,
    VersionMismatch,
    HashMismatch,
    SerialMismatch,
    NoMetadata
}

public sealed record Rpcs3PatchGameTarget(
    string Title,
    string Serial,
    IReadOnlyList<string> Versions)
{
    public string VersionDisplay =>
        Versions.Count == 0
            ? "Any"
            : string.Join(", ", Versions);
}

public sealed record Rpcs3PatchEntry(
    string Title,
    PatchHashKind HashKind,
    string Hash,
    IReadOnlyList<Rpcs3PatchGameTarget> GameTargets,
    string PatchType,
    PatchRisk Risk,
    string? Notes,
    string? Author,
    string? PatchVersion,
    string? Group,
    PatchSourceOrigin Source = PatchSourceOrigin.Unknown,
    string? SourcePath = null,
    IReadOnlyList<string>? Dependencies = null,
    string? DefinitionFingerprint = null,
    string? RuntimeKeySerial = null)
{
    public IReadOnlyList<string> RequiredPatches =>
        Dependencies ?? [];

    public string? NormalizedExecutableHash =>
        Rpcs3PatchRuntimeKey.TryNormalizeHash(
            HashKind,
            Hash,
            out var normalized)
            ? normalized
            : null;

    public string RuntimeHashKey =>
        Rpcs3PatchRuntimeKey.Build(
            HashKind,
            Hash,
            RuntimeKeySerial);

    public IReadOnlyList<string> GameSerials =>
    [
        .. GameTargets
            .Select(target => target.Serial)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
    ];

    public IReadOnlyList<string> GameVersions =>
    [
        .. GameTargets
            .SelectMany(target => target.Versions)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
    ];
}

public sealed record Rpcs3PatchCatalog(
    string? Version,
    IReadOnlyList<Rpcs3PatchEntry> Entries,
    IReadOnlyList<PatchCatalogDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PatchCatalogDiagnostic> MergeDiagnostics =>
        Diagnostics ?? [];
}

public sealed record PatchCatalogDiagnostic(
    string Code,
    string Message,
    PatchSourceOrigin KeptSource,
    PatchSourceOrigin DroppedSource);

public sealed record PatchCatalogReadResult(
    bool Succeeded,
    Rpcs3PatchCatalog Catalog,
    string? Error)
{
    public static PatchCatalogReadResult Success(
        Rpcs3PatchCatalog catalog,
        string? warning = null)
    {
        return new PatchCatalogReadResult(true, catalog, warning);
    }

    public static PatchCatalogReadResult Failure(string error)
    {
        return new PatchCatalogReadResult(
            false,
            new Rpcs3PatchCatalog(null, []),
            error);
    }
}

public sealed record Rpcs3PatchMatchResult(
    Rpcs3PatchEntry Patch,
    PatchMatchStatus Status,
    string? MatchedSerial,
    string? MatchedVersion,
    bool HasHashMetadata,
    string MatchReason,
    string? MatchedTargetTitle = null,
    string? MatchedTargetSerial = null,
    string? MatchedTargetVersion = null,
    IReadOnlyList<string>? DetectedHashes = null,
    string? HashEvidenceSource = null,
    string? HashEvidenceMessage = null,
    PatchHashEvidenceKind HashEvidenceKind = PatchHashEvidenceKind.None,
    DateTime? HashEvidenceTimestampUtc = null,
    PatchCompatibilityDecision? Compatibility = null)
{
    public bool IsSafePreviewMatch =>
        Status == PatchMatchStatus.ExactMatch;

    public IReadOnlyList<string> AllDetectedHashes =>
        DetectedHashes ?? [];
}

public sealed record PatchApplyChange(
    string HashKey,
    string PatchTitle,
    string GameTitle,
    string Serial,
    string Version,
    PatchRisk Risk);

public sealed record PatchApplyPlan(
    string PatchConfigPath,
    IReadOnlyList<PatchApplyChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;
}

public enum PatchPreparedFileKind
{
    PatchCatalog,
    PatchConfig,
    JsonMetadata
}

public sealed record PatchPreparedFile(
    string TargetPath,
    string Content,
    PatchPreparedFileKind Kind,
    PatchSourceOrigin CatalogSource = PatchSourceOrigin.Unknown,
    int? ExpectedEntryCount = null,
    bool DeleteTarget = false);

public sealed record PatchApplyResult(
    BackupManifest Backup,
    IReadOnlyList<PatchApplyChange> AppliedChanges,
    IReadOnlyList<string>? PreparationMessages = null)
{
    public IReadOnlyList<string> Messages =>
        PreparationMessages ?? [];
}

public sealed record PatchHashProbeResult(
    string? PpuHash,
    string? SpuHash,
    PatchHashEvidenceKind EvidenceKind,
    string? SourcePath,
    DateTime? SourceTimestampUtc,
    bool HashAvailable,
    string Message,
    IReadOnlyList<string>? PpuHashes = null,
    IReadOnlyList<string>? SpuHashes = null,
    IReadOnlyList<string>? PrxHashes = null,
    IReadOnlyList<string>? OvlHashes = null,
    IReadOnlyList<string>? AuthoritativePpuHashes = null)
{
    public IReadOnlyList<string> AllPpuHashes =>
        PpuHashes ??
        (PpuHash is null
            ? []
            : [PpuHash]);

    public IReadOnlyList<string> AllSpuHashes =>
        SpuHashes ??
        (SpuHash is null
            ? []
            : [SpuHash]);

    public IReadOnlyList<string> AllPrxHashes =>
        PrxHashes ?? [];

    public IReadOnlyList<string> AllOvlHashes =>
        OvlHashes ?? [];

    public IReadOnlyList<string> AllAuthoritativePpuHashes =>
        AuthoritativePpuHashes ?? [];

    public static PatchHashProbeResult Unknown(string message)
    {
        return new PatchHashProbeResult(
            PpuHash: null,
            SpuHash: null,
            EvidenceKind: PatchHashEvidenceKind.None,
            SourcePath: null,
            SourceTimestampUtc: null,
            HashAvailable: false,
            Message: message);
    }
}
