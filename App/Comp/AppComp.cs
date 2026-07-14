using R3Config.Case;
using R3Config.Svc;

namespace R3Config.Comp;

public static class AppComp
{
    public static AppRun BuildRun()
    {
        return new AppRun(
            new AppFlow(Build()),
            new AppSettingsService());
    }

    public static AppCases Build()
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

        var patchCatalog = new PatCat(
            patchReader,
            new PatchCatalogMergeService(),
            new PatchMatchService(patchCompatibility),
            patchConfigReader,
            hCanary,
            canaryImport,
            new LocalizedPatchCatalogMessageFormatter());
        var backupUseCase = new BkpCase(
            backup,
            mutationGuard);

        return new AppCases(
            new AnlzCase(
                new GameScanner(),
                plan,
                new HwProbe(),
                new PatchHashProbeService(),
                patchCatalog,
                backupUseCase),
            new CfgCase(
                plan,
                new ConfigApplyService(backup),
                mutationGuard),
            patchCatalog,
            new PatMut(
                new PatchApplyPlanner(),
                new PatchApplyService(
                    backup,
                    new PatchConfigWriter(patchConfigReader),
                    transaction: patchTransaction),
                new BundledCanaryPatchService(),
                new BundledCheatPatchService(),
                mutationGuard),
            new CanImp(
                canaryImport,
                mutationGuard,
                patchTransaction,
                backup,
                patchReader),
            new RecCat(
                new CatInfoService()),
            backupUseCase,
            new HCanCase(
                hCanary,
                mutationGuard));
    }
}

public sealed record AppRun(
    AppFlow Flow,
    AppSettingsService Settings);
