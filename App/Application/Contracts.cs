using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

public sealed record CatLoadResult(
    RecommendationCatalogService? Cat,
    string? Error);

public sealed record PatchCatResult(
    Rpcs3PatchCatalog? Cat,
    string? Error,
    IReadOnlyList<PatchCatalogSourceStatus>? Sources = null)
{
    public IReadOnlyList<PatchCatalogSourceStatus> SourceStatuses => Sources ?? [];
}

public sealed record PatchCatalogSourceStatus(
    string Name,
    PatchSourceOrigin Source,
    string Path,
    bool Exists,
    bool Loaded,
    int EntryCount,
    string? Diagnostic = null);

public sealed record GamePlanRow(
    Rpcs3GameDescriptor Game,
    WikiGameRecommendation? Rec,
    RecStateSummary RecState,
    bool IsDetectedInRpcs3);

public sealed record AnalyzeResult(
    Rpcs3InstallDescriptor Install,
    HwInfo Hw,
    IReadOnlyList<GamePlanRow> Games,
    PatchCatResult PatchCat,
    IReadOnlyList<BackupSnapshot> Backups,
    IReadOnlyList<GameScanDiagnostic> GameScanDiagnostics);
