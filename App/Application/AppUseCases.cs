namespace R3Config.Application;

public sealed record AppUseCases(
    AnalysisUseCase Analysis,
    Rpcs3GameLibraryUseCase GameLibrary,
    ConfigUseCase Config,
    PatchCatalogUseCase PatchCatalog,
    PatchMutationUseCase PatchMutation,
    CanaryImportUseCase CanaryImport,
    RecommendationCatalogUseCase Recommendations,
    BackupUseCase Backup,
    HashEvidenceUseCase HashEvidence,
    HCanaryUseCase HCanary,
    GameLaunchUseCase GameLaunch,
    GameFingerprintCaptureUseCase FingerprintCapture,
    GameSetupUseCase GameSetup);
