using R3Config.Application;
using R3Config.Svc;

namespace R3Config.Composition;

public static class AppComposition
{
    public static AppRuntimeServices BuildRun(
        IRpcs3MutationGuard? mutationGuard = null)
    {
        return new AppRuntimeServices(
            new AppFlow(Build(mutationGuard)),
            new SetSvc());
    }

    public static AppUseCases Build(
        IRpcs3MutationGuard? mutationGuard = null)
    {
        var backup = new BackupService();
        mutationGuard ??= new Rpcs3MutationGuard();
        var plan = new PlanService();
        var gameScanner = new GameScanner();
        var patchReader = new ReadSvc();
        var patchConfigReader = new CfgRead();

        var canaryImport = new Svc.CanaryPatchImportService(
            patchReader);

        var patchTransaction = new PatchFileTransactionService(
            backup,
            patchReader,
            patchConfigReader);

        var hCanary = new HCanaryPatchAuthoringService(
            patchReader,
            patchTransaction);

        var patchCompatibility = new PatchCompatibilityService();
        var canaryRuntimeBinding = new CanaryRuntimeBindingService();
        var patchHashProbe = new PatchHashProbeService();
        var discoveryStore = new HCanDiscoveryStore();
        var hashDiscovery = new HCanHashDiscoveryService();

        var bindingEvidence = new HCanBindingEvidenceService(
            discoveryStore);

        var hashEvidence = new HashEvidenceUseCase(
            hashDiscovery,
            discoveryStore,
            new Rpcs3ProcessProbe());

        var gameLaunch = new GameLaunchUseCase(
            new Rpcs3BootPathResolver(),
            new Rpcs3GameProcessLauncher());

        var patchCatalog = new PatchCatalogUseCase(
            patchReader,
            new PatchCatalogMergeService(),
            new PatchMatchService(
                patchCompatibility,
                canaryRuntimeBinding),
            patchConfigReader,
            hCanary,
            canaryImport,
            new ArtDepSvc(),
            new LocalizedPatchCatalogMessageFormatter());

        var backupUseCase = new BackupUseCase(
            backup,
            mutationGuard);

        return new AppUseCases(
            new AnalysisUseCase(
                gameScanner,
                plan,
                new HwProbe(),
                patchHashProbe,
                bindingEvidence,
                patchCatalog,
                backupUseCase),
            new Rpcs3GameLibraryUseCase(
                gameScanner),
            new ConfigUseCase(
                plan,
                new ConfigApplyService(backup),
                mutationGuard),
            patchCatalog,
            new PatchMutationUseCase(
                new PatPlnr(),
                new PatchApplyService(
                    backup,
                    new CfgWrite(patchConfigReader),
                    transaction: patchTransaction),
                new BundledCanaryPatchService(),
                new BundledCheatPatchService(),
                mutationGuard),
            new CanaryImportUseCase(
                canaryImport,
                mutationGuard,
                patchTransaction,
                patchReader),
            new RecommendationCatalogUseCase(
                new CatSvc()),
            backupUseCase,
            hashEvidence,
            new HCanaryUseCase(
                hCanary,
                mutationGuard),
            gameLaunch,
            new GameFingerprintCaptureUseCase(
                hashEvidence,
                gameLaunch,
                new Rpcs3OwnedProcessController()),
            new GameSetupUseCase(
                plan,
                new GameSetupPlanningService(
                    compatibilityService: patchCompatibility),
                new PatPlnr(),
                patchConfigReader,
                new CfgWrite(patchConfigReader),
                new BundledCanaryPatchService(),
                new BundledCheatPatchService(),
                patchReader,
                patchTransaction,
                mutationGuard));
    }
}

public sealed record AppRuntimeServices(
    AppFlow Flow,
    SetSvc Settings);
