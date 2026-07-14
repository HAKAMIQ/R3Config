using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

public sealed record CatLoad(
    RecommendationCatalogService? Cat,
    string? Error);

public sealed record PatCatRs(
    Rpcs3PatchCatalog? Cat,
    string? Error,
    IReadOnlyList<PatSrcSt>? Sources = null)
{
    public IReadOnlyList<PatSrcSt> SrcStat => Sources ?? [];
}

public sealed record PatSrcSt(
    string Name,
    PatchSourceOrigin Source,
    string Path,
    bool Exists,
    bool Loaded,
    int EntryCount,
    string? Diagnostic = null);

public sealed record GameRow(
    Rpcs3GameDescriptor Game,
    WikiGameRecommendation? Rec,
    RecStateSummary RecState,
    bool IsDetectedInRpcs3);

public sealed record AnlzRes(
    Rpcs3InstallDescriptor Install,
    HwInfo Hw,
    IReadOnlyList<GameRow> Games,
    PatCatRs PatchCat,
    IReadOnlyList<BackupSnapshot> Backups,
    IReadOnlyList<GameScanDiagnostic> GameScanDiagnostics);
