namespace R3Config.Application;

public sealed record AppUseCases(
    AnalysisUseCase Analysis,
    ConfigUseCase Config,
    PatchCatalogUseCase PatchCatalog,
    PatchMutationUseCase PatchMutation,
    CanaryImportUseCase CanaryImport,
    RecommendationCatalogUseCase Recommendations,
    BackupUseCase Backup,
    HCanaryUseCase HCanary);
