using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

public sealed record CatalogLoadResult(
    RecSvc? Cat,
    string? Error);

public sealed record PatchCatalogResult(
    PatCat? Cat,
    string? Error,
    IReadOnlyList<PatchCatalogSourceStatus>? Sources = null)
{
    public IReadOnlyList<PatchCatalogSourceStatus> SrcStat => Sources ?? [];
}

public sealed record PatchCatalogSourceStatus(
    string Name,
    PatSrc Source,
    string Path,
    bool Exists,
    bool Loaded,
    int EntNum,
    string? Diag = null);


public sealed record GamePlanRow(
    Rpcs3GameDescriptor Game,
    WikiRec? Rec,
    RecSum RecState,
    bool InR3);

public enum AnalyzeStage
{
    ReadRpcs3,
    ScanGames,
    AnalyzeSettings,
    LoadPatches,
    PrepareResults
}

public sealed record AnalyzeResult(
    Rpcs3InstallDescriptor Install,
    HwInfo Hw,
    IReadOnlyList<GamePlanRow> Games,
    PatchCatalogResult PatchCat,
    IReadOnlyList<BackupSnapshot> Backups,
    IReadOnlyList<GameScanDiagnostic> GameScanDiagnostic,
    Rpcs3BuildIdentity? BuildIdentity = null);
