using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Application;

namespace R3Config;

/// <summary>
/// Compatibility facade for the WPF layer. Business workflows live in
/// Application use cases and are composed in <see cref="AppComposition"/>.
/// </summary>
public sealed class AppFlow
{
    private readonly AppUseCases _cases;

    public AppFlow()
        : this(AppComposition.Build())
    {
    }

    public AppFlow(AppUseCases useCases)
    {
        _cases = useCases ??
            throw new ArgumentNullException(nameof(useCases));
    }

    public CatalogLoadResult LoadCat(string baseDir)
    {
        return _cases.Recommendations.LoadData(baseDir);
    }

    public Task<AnalyzeResult> DoAsync(
        string rpcs3ExePath,
        Svc.RecSvc cat)
    {
        return _cases.Analysis.RunAsync(
            rpcs3ExePath,
            cat);
    }

    public CfgPlan CfgPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiRec? rec,
        HwInfo? hw)
    {
        return _cases.Config.Build(
            install,
            game,
            rec,
            hw);
    }

    public CfgRes ApplyCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        Rpcs3InstallDescriptor install)
    {
        return _cases.Config.Apply(
            plan,
            changes,
            install);
    }

    public CfgRes DelCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        Rpcs3InstallDescriptor install)
    {
        return _cases.Config.Remove(
            plan,
            changes,
            install);
    }

    public PatchCatalogResult LoadPat(
        Rpcs3InstallDescriptor install)
    {
        return _cases.PatchCatalog.Load(install);
    }

    public CanDel DelCan(
        Rpcs3InstallDescriptor install)
    {
        return _cases.CanaryImport.Remove(install);
    }

    public CanNorm NormCan(
        string? sourceText)
    {
        return _cases.CanaryImport.Norm(sourceText);
    }

    public bool HasImp(
        Rpcs3InstallDescriptor install)
    {
        return _cases.CanaryImport.HasPat(
            install);
    }

    public CanWrite ImpCan(
        Rpcs3InstallDescriptor install,
        string sourceText)
    {
        return _cases.CanaryImport.Import(
            install,
            sourceText);
    }

    public IReadOnlyList<PatMatch> PatPrev(
        Mdl.PatCat patchCat,
        Rpcs3GameDescriptor game)
    {
        return _cases.PatchCatalog.Build(
            patchCat,
            game);
    }

    public PatchApplyPlan BuildPatchPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches,
        IReadOnlyList<PatMatch> enabledMatches,
        bool expertMode)
    {
        return _cases.PatchMutation.BuildOn(
            install,
            matches,
            enabledMatches,
            expertMode);
    }

    public PatchApplyPlan OffPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches)
    {
        return _cases.PatchMutation.BuildOff(
            install,
            matches);
    }

    public PatChg? PatChg(
        PatMatch match)
    {
        return _cases.PatchMutation.BuildChg(match);
    }

    public PatRes ApplyTxn(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        return _cases.PatchMutation.ApplyTxn(
            plan,
            install,
            selectedMatches);
    }

    public PatRes ApplyPat(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        return _cases.PatchMutation.Apply(
            plan,
            install);
    }

    public PatRes OffPat(PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        return _cases.PatchMutation.Disable(
            plan,
            install);
    }

    public bool CanRead(
        Rpcs3InstallDescriptor install)
    {
        return _cases.PatchCatalog.CanRead(install);
    }

    public CfgSnap ReadCfg(
        Rpcs3InstallDescriptor install)
    {
        return _cases.PatchCatalog.ReadCfg(install);
    }

    public Svc.RecSvc BuildCat(string xmlPath)
    {
        return _cases.Recommendations.Rebuild(xmlPath);
    }

    public string CatText(CatInfo info)
    {
        return _cases.Recommendations.FmtInf(info);
    }

    public void ExpCat(
        Svc.RecSvc cat,
        string path)
    {
        Application.RecommendationCatalogUseCase.Export(cat, path);
    }

    public IReadOnlyList<BackupSnapshot> ListBkp(
        Rpcs3InstallDescriptor install)
    {
        return _cases.Backup.List(install);
    }

    public void Restore(
        Rpcs3InstallDescriptor install,
        BackupSnapshot backup)
    {
        _cases.Backup.Restore(install, backup);
    }

    public static string BkpRoot(
        Rpcs3InstallDescriptor install)
    {
        return Rpcs3AppPaths.BkpRoot(install);
    }

    public IReadOnlyList<HCanStor> LoadHCan(
        Rpcs3InstallDescriptor install)
    {
        return _cases.HCanary.Load(install);
    }

    public SaveRes SaveHCan(
        Rpcs3InstallDescriptor install,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        return _cases.HCanary.Save(
            install,
            draft,
            originalIdentity,
            saveAsNew);
    }

    public SaveRes DelHCan(
        Rpcs3InstallDescriptor install,
        string identity)
    {
        return _cases.HCanary.Delete(
            install,
            identity);
    }

    public GameLaunchResult LaunchGame(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return _cases.GameLaunch.Launch(
            install,
            game);
    }
}
