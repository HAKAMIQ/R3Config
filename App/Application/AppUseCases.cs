namespace R3Config.UseCases;

public sealed record AppUseCases(
    AnalysisUseCase Analysis,
    ConfigUseCase Config,
    PatchCatalogUseCase PatchCatalog,
    PatchMutationUseCase PatchMutation,
    CanaryImportUseCase CanaryImport,
    RecommendationCatalogUseCase RecommendationCatalog,
    BackupUseCase Backup,
    HCanaryUseCase HCanary);
