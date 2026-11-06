using System.IO;
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
        IRpcs3MutationGuard? mutationGuard = null,
        string? recommendationSyncFolder = null)
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

        var bundledCheats = new BundledCheatPatchService();

        var patchMutation = new PatchMutationUseCase(
            new PatPlnr(),
            new PatchApplyService(
                backup,
                new CfgWrite(patchConfigReader),
                transaction: patchTransaction),
            new BundledCanaryPatchService(),
            bundledCheats,
            mutationGuard);

        return new AppUseCases(
            new AnalysisUseCase(
                gameScanner,
                plan,
                new HwProbe(),
                patchHashProbe,
                bindingEvidence,
                patchCatalog,
                backupUseCase,
                new Rpcs3BuildIdentityProbe()),
            new Rpcs3GameLibraryUseCase(
                gameScanner),
            new ConfigUseCase(
                plan,
                new ConfigApplyService(backup),
                mutationGuard),
            patchCatalog,
            patchMutation,
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
                mutationGuard),
            NclManagedPatch: new NclManagedPatchUseCase(
                bundledCheats,
                patchMutation,
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Data",
                    NclManagedPatchUseCase.BundledCorpusName)),
            WikiEvidence: new WikiEvidenceUseCase(),
            RuntimeContext: new RuntimeContextUseCase(),
            LogDiagnostics: new LogDiagnosticsUseCase(),
            RecommendationSync: BuildRecommendationSync(
                recommendationSyncFolder ?? RecommendationSyncUseCase.DefaultFolder),
            GameDatabase: new GameDatabaseUseCase());
    }

    // One HTTP client and one store for the application lifetime; the sync
    // service serializes runs, so the startup check, the daily check and the
    // manual check never overlap.
    private static RecommendationSyncUseCase BuildRecommendationSync(
        string folder)
    {
        var version = typeof(AppComposition).Assembly.GetName().Version?.ToString(3) ?? "0";
        var http = new RecSyncHttp(
            RecSyncHttp.CreateHandler(),
            version);

        return new RecommendationSyncUseCase(
            new RecSyncService(
                new RecSyncStore(folder),
                http,
                new WikiRevisionSource(http)));
    }
}

public sealed record AppRuntimeServices(
    AppFlow Flow,
    SetSvc Settings);
