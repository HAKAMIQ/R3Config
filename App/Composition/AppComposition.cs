using R3Config.UseCases;
using R3Config.Svc;

namespace R3Config.Composition;

public static class AppComposition
{
    public static AppRuntimeServices CreateRuntimeServices()
    {
        return new AppRuntimeServices(
            new AppFlow(CreateUseCases()),
            new AppSettingsService());
    }

    public static AppUseCases CreateUseCases()
    {
        var backup = new BackupService();
        var mutationGuard = new Rpcs3MutationGuard();
        var plan = new PlanService();
        var patchReader = new PatchCatalogReader();
        var patchConfigReader = new PatchConfigReader();
        var canaryImport = new CanaryPatchImportService(
            patchReader);
        var patchTransaction = new PatchFileTransactionService(
            backup,
            patchReader,
            patchConfigReader);
        var hCanary = new HCanaryPatchAuthoringService(
            patchReader,
            patchTransaction);
        var patchCompatibility = new PatchCompatibilityService();

        var patchCatalog = new PatchCatalogUseCase(
            patchReader,
            new PatchCatalogMergeService(),
            new PatchMatchService(patchCompatibility),
            patchConfigReader,
            hCanary,
            canaryImport,
            new LocalizedPatchCatalogMessageFormatter());
        var backupUseCase = new BackupUseCase(
            backup,
            mutationGuard);

        return new AppUseCases(
            new AnalysisUseCase(
                new GameScanner(),
                plan,
                new HwProbe(),
                new PatchHashProbeService(),
                patchCatalog,
                backupUseCase),
            new ConfigUseCase(
                plan,
                new ConfigApplyService(backup),
                mutationGuard),
            patchCatalog,
            new PatchMutationUseCase(
                new PatchApplyPlanner(),
                new PatchApplyService(
                    backup,
                    new PatchConfigWriter(patchConfigReader),
                    transaction: patchTransaction),
                new BundledCanaryPatchService(),
                new BundledCheatPatchService(),
                mutationGuard),
            new CanaryImportUseCase(
                canaryImport,
                mutationGuard,
                patchTransaction,
                backup,
                patchReader),
            new RecommendationCatalogUseCase(
                new CatInfoService()),
            backupUseCase,
            new HCanaryUseCase(
                hCanary,
                mutationGuard));
    }
}

public sealed record AppRuntimeServices(
    AppFlow Flow,
    AppSettingsService Settings);
