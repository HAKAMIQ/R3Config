using R3Config.Comp;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Case;

namespace R3Config;

/// <summary>
/// Compatibility facade for the WPF layer. Business workflows live in
/// Application use cases and are composed in <see cref="AppComp"/>.
/// </summary>
public sealed class AppFlow
{
    private readonly AppCases _useCases;

    public AppFlow()
        : this(AppComp.Build())
    {
    }

    public AppFlow(AppCases useCases)
    {
        _useCases = useCases ??
            throw new ArgumentNullException(nameof(useCases));
    }

    public CatLoad LoadBuiltInCat(string baseDir)
    {
        return _useCases.RecommendationCatalog.LoadData(baseDir);
    }

    public Task<AnlzRes> AnalyzeAsync(
        string rpcs3ExePath,
        Svc.RecSvc cat)
    {
        return _useCases.Analysis.RunAsync(
            rpcs3ExePath,
            cat);
    }

    public CfgPlan BuildCfgPlan(
        R3Inst install,
        R3Game game,
        WikiRec? rec,
        HwInfo? hw)
    {
        return _useCases.Config.Build(
            install,
            game,
            rec,
            hw);
    }

    public CfgRes ApplyCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        R3Inst install)
    {
        return _useCases.Config.Apply(
            plan,
            changes,
            install);
    }

    public CfgRes RemoveCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        R3Inst install)
    {
        return _useCases.Config.Remove(
            plan,
            changes,
            install);
    }

    public PatCatRs LoadPatchCat(
        R3Inst install)
    {
        return _useCases.PatchCatalog.Load(install);
    }

    public CanaryPatchRemoveResult RemovePreparedCanaryPatch(
        R3Inst install)
    {
        return _useCases.CanaryImport.Remove(install);
    }

    public CanaryPatchNormalizeResult NormalizeCanaryPatch(
        string? sourceText)
    {
        return _useCases.CanaryImport.Norm(sourceText);
    }

    public bool HasManagedImportedPatch(
        R3Inst install)
    {
        return _useCases.CanaryImport.HasPat(
            install);
    }

    public CanaryPatchWriteResult ImportCanaryPatch(
        R3Inst install,
        string sourceText)
    {
        return _useCases.CanaryImport.Import(
            install,
            sourceText);
    }

    public IReadOnlyList<PatMatch> BuildPatchPreview(
        Mdl.PatCat patchCat,
        R3Game game)
    {
        return _useCases.PatchCatalog.Build(
            patchCat,
            game);
    }

    public PatPlan BuildPatchPlan(
        R3Inst install,
        IReadOnlyList<PatMatch> matches,
        bool expertMode)
    {
        return _useCases.PatchMutation.BuildOn(
            install,
            matches,
            expertMode);
    }

    public PatPlan BuildPatchDisablePlan(
        R3Inst install,
        IReadOnlyList<PatMatch> matches)
    {
        return _useCases.PatchMutation.BuildOff(
            install,
            matches);
    }

    public PatChg? CreatePatchChange(
        PatMatch match)
    {
        return _useCases.PatchMutation.BuildChg(match);
    }

    public PatRes ApplyPatchTransaction(
        PatPlan plan,
        R3Inst install,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        return _useCases.PatchMutation.ApplyTxn(
            plan,
            install,
            selectedMatches);
    }

    public PatRes ApplyPatch(
        PatPlan plan,
        R3Inst install)
    {
        return _useCases.PatchMutation.Apply(
            plan,
            install);
    }

    public PatRes DisablePatch(PatPlan plan,
        R3Inst install)
    {
        return _useCases.PatchMutation.Disable(
            plan,
            install);
    }

    public bool CanReadPatchCfg(
        R3Inst install)
    {
        return _useCases.PatchCatalog.CanRead(install);
    }

    public CfgSnap ReadPatchCfg(
        R3Inst install)
    {
        return _useCases.PatchCatalog.ReadCfg(install);
    }

    public Svc.RecSvc RebuildCat(string xmlPath)
    {
        return _useCases.RecommendationCatalog.Rebuild(xmlPath);
    }

    public string CatInfoText(CatInfo info)
    {
        return _useCases.RecommendationCatalog.FmtInf(info);
    }

    public void ExportCat(
        Svc.RecSvc cat,
        string path)
    {
        Case.RecCat.Export(cat, path);
    }

    public IReadOnlyList<BkpSnap> ListBackups(
        R3Inst install)
    {
        return _useCases.Backup.List(install);
    }

    public void Restore(
        R3Inst install,
        BkpSnap backup)
    {
        _useCases.Backup.Restore(install, backup);
    }

    public static string BkpRoot(
        R3Inst install)
    {
        return R3Paths.BkpRoot(install);
    }

    public IReadOnlyList<HCanStor> LoadHCanaryPatches(
        R3Inst install)
    {
        return _useCases.HCanary.Load(install);
    }

    public SaveRes SaveHCanaryPatch(
        R3Inst install,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        return _useCases.HCanary.Save(
            install,
            draft,
            originalIdentity,
            saveAsNew);
    }

    public SaveRes DeleteHCanaryPatch(
        R3Inst install,
        string identity)
    {
        return _useCases.HCanary.Delete(
            install,
            identity);
    }
}
