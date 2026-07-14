using System;

namespace R3Config.Mdl;

public static class Rpcs3InputLimits
{
    public const long ParamSfoFileBytes = 16L * 1024 * 1024;
    public const uint ParamSfoEntries = 4_096;
    public const int ParamSfoKeyBytes = 256;
    public const uint ParamSfoValueBytes = 4U * 1024 * 1024;
    public const long GamesYamlBytes = 32L * 1024 * 1024;
    public const long PatchCatalogYamlBytes = 128L * 1024 * 1024;
    public const long PatchConfigYamlBytes = 16L * 1024 * 1024;
    public const long CustomConfigYamlBytes = 16L * 1024 * 1024;
    public const int DirectoryEntries = 100_000;
    public const int YamlDocuments = 1;
}

public enum GameScanDiagnosticCode
{
    GamesYmlMissing,
    GamesYmlMalformed,
    GamesYmlTooLarge,
    GameDirectoryAccessDenied,
    ParamSfoMissing,
    ParamSfoMalformed,
    ParamSfoTooLarge,
    DirectoryEntryLimitReached,
    CustomConfigUnreadable
}

public enum GameScanDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum GameScanSourceKind
{
    GamesYaml,
    GameDirectory,
    ParamSfo,
    CustomConfigDirectory
}

public sealed record GameScanDiagnostic(
    GameScanDiagnosticCode Code,
    GameScanDiagnosticSeverity Severity,
    GameScanSourceKind SourceKind,
    string SourcePath,
    string TechnicalMessage);

public sealed record GameScanResult(
    IReadOnlyList<Rpcs3GameDescriptor> Games,
    IReadOnlyList<GameScanDiagnostic> Diagnostics,
    IReadOnlyList<string> CustomConfigSerials);

public sealed record Rpcs3InstallDescriptor(
    string RootPath,
    string ExePath,
    string ConfigPath,
    string CustomConfigsPath,
    string PatchesPath,
    string PatchConfigPath,
    string? DataRootPath = null);

public sealed record Rpcs3GameDescriptor(
    string Serial,
    string Title,
    string InstallPath,
    string? Version,
    string? PpuHash,
    string? SpuHash,
    IReadOnlyList<string>? PpuHashes = null,
    IReadOnlyList<string>? SpuHashes = null,
    string? HashSource = null,
    string? HashMessage = null,
    PatchHashEvidenceKind HashEvidenceKind = PatchHashEvidenceKind.None,
    DateTime? HashEvidenceTimestampUtc = null,
    IReadOnlyList<string>? PrxHashes = null,
    IReadOnlyList<string>? OvlHashes = null,
    IReadOnlyList<string>? AuthoritativePpuHashes = null)
{
    public IReadOnlyList<string> AllPpuHashes => MergeHashes(PpuHash, PpuHashes);

    public IReadOnlyList<string> AllSpuHashes => MergeHashes(SpuHash, SpuHashes);

    public IReadOnlyList<string> AllPrxHashes => MergeRuntimeHashes(
        PatchHashKind.Prx,
        PrxHashes);

    public IReadOnlyList<string> AllOvlHashes => MergeRuntimeHashes(
        PatchHashKind.Ovl,
        OvlHashes);

    public IReadOnlyList<string> AllAuthoritativePpuHashes =>
        AuthoritativePpuHashes is not null
            ? MergeHashes(null, AuthoritativePpuHashes)
            : HashEvidenceKind == PatchHashEvidenceKind.GameCache
                ? []
                : AllPpuHashes;

    public IReadOnlyList<string> HashesFor(PatchHashKind kind)
    {
        return kind switch
        {
            PatchHashKind.Ppu => AllPpuHashes,
            PatchHashKind.Spu => AllSpuHashes,
            PatchHashKind.Prx => AllPrxHashes,
            PatchHashKind.Ovl => AllOvlHashes,
            _ => []
        };
    }

    private static IReadOnlyList<string> MergeHashes(
        string? primary,
        IReadOnlyList<string>? hashes)
    {
        return
        [
            .. (hashes ?? [])
                .Append(primary)
                .Select(NormalizeExecutableHash)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string? NormalizeExecutableHash(string? hash)
    {
        return Rpcs3ExecutableHash.TryNormalize(hash, out var normalized)
            ? normalized
            : null;
    }

    private static IReadOnlyList<string> MergeRuntimeHashes(
        PatchHashKind kind,
        IReadOnlyList<string>? hashes)
    {
        return
        [
            .. (hashes ?? [])
                .Select(hash =>
                    Rpcs3PatchRuntimeKey.TryNormalizeEvidence(
                        kind,
                        hash,
                        out var normalized)
                        ? normalized
                        : null)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}

public sealed record ParamSfoInfo(
    string? Title,
    string? TitleId,
    string? AppVersion);
