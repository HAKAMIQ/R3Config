using System;
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

public enum PatchSourceOrigin
{
    Official,
    Imported,
    BundledCanary,
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
    public string VersionDisplay => Versions.Count == 0 ? "Any" : string.Join(", ", Versions);
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
    string? SourcePath = null)
{
    public IReadOnlyList<string> GameSerials => GameTargets
        .Select(target => target.Serial)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> GameVersions => GameTargets
        .SelectMany(target => target.Versions)
        .Where(version => !string.IsNullOrWhiteSpace(version))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed record Rpcs3PatchCatalog(
    string? Version,
    IReadOnlyList<Rpcs3PatchEntry> Entries,
    IReadOnlyList<PatchCatalogDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PatchCatalogDiagnostic> MergeDiagnostics => Diagnostics ?? [];
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
    public static PatchCatalogReadResult Success(Rpcs3PatchCatalog catalog)
    {
        return new PatchCatalogReadResult(true, catalog, null);
    }

    public static PatchCatalogReadResult Failure(string error)
    {
        return new PatchCatalogReadResult(false, new Rpcs3PatchCatalog(null, []), error);
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
    string? MatchedTargetVersion = null)
{
    public bool IsSafePreviewMatch => Status == PatchMatchStatus.ExactMatch;
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

public sealed record PatchApplyResult(
    BackupManifest Backup,
    IReadOnlyList<PatchApplyChange> AppliedChanges);

public sealed record PatchHashProbeResult(
    string? PpuHash,
    string? SpuHash,
    string Source,
    bool HashAvailable,
    string Message,
    IReadOnlyList<string>? PpuHashes = null,
    IReadOnlyList<string>? SpuHashes = null)
{
    public IReadOnlyList<string> AllPpuHashes => PpuHashes ?? (PpuHash is null ? [] : [PpuHash]);

    public IReadOnlyList<string> AllSpuHashes => SpuHashes ?? (SpuHash is null ? [] : [SpuHash]);

    public static PatchHashProbeResult Unknown(string message)
    {
        return new PatchHashProbeResult(null, null, "None", false, message);
    }
}
