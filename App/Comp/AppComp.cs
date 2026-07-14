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
        var backup = new BkpSvc();
        var mutationGuard = new MutGuard();
        var plan = new PlanSvc();
        var patchReader = new ReadSvc();
        var patchConfigReader = new CfgRead();
        var canaryImport = new CanaryPatchImportService(
            patchReader);
        var patchTransaction = new PatTxn(
            backup,
            patchReader,
            patchConfigReader);
        var hCanary = new PatAuth(
            patchReader,
            patchTransaction);
        var patchCompatibility = new PatCmp();

        var patchCatalog = new PatCat(
            patchReader,
            new PatMerge(),
            new MatchSvc(patchCompatibility),
            patchConfigReader,
            hCanary,
            canaryImport,
            new LocalizedPatchCatalogMessageFormatter());
        var backupUseCase = new BkpCase(
            backup,
            mutationGuard);

        return new AppCases(
            new AnlzCase(
                new GmScan(),
                plan,
                new HwProbe(),
                new HashPrb(),
                patchCatalog,
                backupUseCase),
            new CfgCase(
                plan,
                new CfgApply(backup),
                mutationGuard),
            patchCatalog,
            new PatMut(
                new PatPlnr(),
                new PatApply(
                    backup,
                    new CfgWrite(patchConfigReader),
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
                new CatSvc()),
            backupUseCase,
            new HCanCase(
                hCanary,
                mutationGuard));
    }
}

public sealed record AppRun(
    AppFlow Flow,
    AppSettingsService Settings);
