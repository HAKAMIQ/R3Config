using System;

namespace R3Config.Mdl;

public static class R3Limits
{
    public const long Bytes = 16L * 1024 * 1024;
    public const uint Entries = 4_096;
    public const int SfoKeyBy = 256;
    public const uint SfoValBy = 4U * 1024 * 1024;
    public const long GameByte = 32L * 1024 * 1024;
    public const long PatCatBy = 128L * 1024 * 1024;
    public const long PatCfgBy = 16L * 1024 * 1024;
    public const long CfgByte = 16L * 1024 * 1024;
    public const long BkpByte = 4L * 1024 * 1024;
    public const long HBytes = 8L * 1024 * 1024;
    public const int DirEnt = 100_000;
    public const int YamlDoc = 1;
    public const int Depth = 64;
    public const int Nodes = 250_000;
    public const int Aliases = 4_096;
    public const int YamlMap = 100_000;
    public const int YamlSeq = 100_000;
    public const int Diag = 1_000;
}

public enum ScanCode
{
    Missing,
    GameBad,
    Large,
    Denied,
    SfoMiss,
    SfoBad,
    SfoBig,
    Reached,
    NoRead
}

public enum ScanSev
{
    Info,
    Warning,
    Error
}

public enum ScanSrc
{
    Yaml,
    GameDir,
    ParamSfo,
    Dir
}

public sealed record GameScanDiagnostic(
    ScanCode Code,
    ScanSev Severity,
    ScanSrc SrcKind,
    string SrcPath,
    string TechMsg);

public sealed record GameScanResult(
    IReadOnlyList<Rpcs3GameDescriptor> Games,
    IReadOnlyList<GameScanDiagnostic> Diags,
    IReadOnlyList<string> CfgSer);

public sealed record Rpcs3InstallDescriptor(
    string RootPath,
    string ExePath,
    string CfgPath,
    string CustPath,
    string PatPath,
    string PatCfg,
    string? DataRoot = null);

public sealed record Rpcs3GameDescriptor(
    string Serial,
    string Title,
    string InstPath,
    string? Version,
    string? PpuHash,
    string? SpuHash,
    IReadOnlyList<string>? PpuRaw = null,
    IReadOnlyList<string>? SpuRaw = null,
    string? HashSrc = null,
    string? HashMsg = null,
    HashEvi HashEvi = HashEvi.None,
    DateTime? HashTime = null,
    IReadOnlyList<string>? PrxRaw = null,
    IReadOnlyList<string>? OvlRaw = null,
    IReadOnlyList<string>? AuthPpu = null,
    IReadOnlyList<string>? HCanPpu = null,
    IReadOnlyList<string>? HCanSpu = null)
{
    public IReadOnlyList<string> Hashes => Merge(PpuHash, PpuRaw);

    public IReadOnlyList<string> SpuList => Merge(SpuHash, SpuRaw);

    public IReadOnlyList<string> PrxList => MergeRun(
        HashKind.Prx,
        PrxRaw);

    public IReadOnlyList<string> OvlList => MergeRun(
        HashKind.Ovl,
        OvlRaw);

    public IReadOnlyList<string> PpuList =>
        AuthPpu is not null
            ? Merge(null, AuthPpu)
            : HashEvi == HashEvi.Cache
                ? []
                : Hashes;

    public IReadOnlyList<string> HCanPpuList =>
        HCanPpu is null
            ? []
            : Merge(null, HCanPpu);

    public IReadOnlyList<string> HCanSpuList =>
        HCanSpu is null
            ? []
            : Merge(null, HCanSpu);

    public IReadOnlyList<string> GetHash(HashKind kind)
    {
        return kind switch
        {
            HashKind.Ppu => Hashes,
            HashKind.Spu => SpuList,
            HashKind.Prx => PrxList,
            HashKind.Ovl => OvlList,
            _ => []
        };
    }

    private static IReadOnlyList<string> Merge(
        string? primary,
        IReadOnlyList<string>? hashes)
    {
        return
        [
            .. (hashes ?? [])
                .Append(primary)
                .Select(NormExe)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string? NormExe(string? hash)
    {
        return ExeHash.TryNorm(hash, out var normalized)
            ? normalized
            : null;
    }

    private static IReadOnlyList<string> MergeRun(
        HashKind kind,
        IReadOnlyList<string>? hashes)
    {
        return
        [
            .. (hashes ?? [])
                .Select(hash =>
                    RunKey.TryNorm2(
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

public sealed record SfoInfo(
    string? Title,
    string? TitleId,
    string? AppVer,
    string? DiscVer = null)
{
    // RPCS3 uses APP_VER when present and falls back to VERSION.
    public string? EffectiveVersion =>
        !string.IsNullOrWhiteSpace(AppVer) &&
        !string.Equals(
            AppVer,
            "Unknown",
            StringComparison.OrdinalIgnoreCase)
            ? AppVer
            : DiscVer;
}
