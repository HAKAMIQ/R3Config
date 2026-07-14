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
        RecommendationCatalogService cat)
    {
        return _useCases.Analysis.RunAsync(
            rpcs3ExePath,
            cat);
    }

    public ConfigPlan BuildCfgPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiGameRecommendation? rec,
        HwInfo? hw)
    {
        return _useCases.Config.Build(
            install,
            game,
            rec,
            hw);
    }

    public ConfigApplyResult ApplyCfg(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        return _useCases.Config.Apply(
            plan,
            changes,
            install);
    }

    public ConfigApplyResult RemoveCfg(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        return _useCases.Config.Remove(
            plan,
            changes,
            install);
    }

    public PatCatRs LoadPatchCat(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchCatalog.Load(install);
    }

    public CanaryPatchRemoveResult RemovePreparedCanaryPatch(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.CanaryImport.Remove(install);
    }

    public CanaryPatchNormalizeResult NormalizeCanaryPatch(
        string? sourceText)
    {
        return _useCases.CanaryImport.Norm(sourceText);
    }

    public bool HasManagedImportedPatch(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.CanaryImport.HasPat(
            install);
    }

    public CanaryPatchWriteResult ImportCanaryPatch(
        Rpcs3InstallDescriptor install,
        string sourceText)
    {
        return _useCases.CanaryImport.Import(
            install,
            sourceText);
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> BuildPatchPreview(
        Rpcs3PatchCatalog patchCat,
        Rpcs3GameDescriptor game)
    {
        return _useCases.PatchCatalog.Build(
            patchCat,
            game);
    }

    public PatchApplyPlan BuildPatchPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        bool expertMode)
    {
        return _useCases.PatchMutation.BuildOn(
            install,
            matches,
            expertMode);
    }

    public PatchApplyPlan BuildPatchDisablePlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches)
    {
        return _useCases.PatchMutation.BuildOff(
            install,
            matches);
    }

    public PatchApplyChange? CreatePatchChange(
        Rpcs3PatchMatchResult match)
    {
        return _useCases.PatchMutation.BuildChg(match);
    }

    public PatchApplyResult ApplyPatchTransaction(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        return _useCases.PatchMutation.ApplyTxn(
            plan,
            install,
            selectedMatches);
    }

    public PatchApplyResult ApplyPatch(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchMutation.Apply(
            plan,
            install);
    }

    public PatchApplyResult DisablePatch(PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchMutation.Disable(
            plan,
            install);
    }

    public bool CanReadPatchCfg(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchCatalog.CanRead(install);
    }

    public PatchConfigSnapshot ReadPatchCfg(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchCatalog.ReadCfg(install);
    }

    public RecommendationCatalogService RebuildCat(string xmlPath)
    {
        return _useCases.RecommendationCatalog.Rebuild(xmlPath);
    }

    public string CatInfoText(CatalogInfo info)
    {
        return _useCases.RecommendationCatalog.FmtInf(info);
    }

    public void ExportCat(
        RecommendationCatalogService cat,
        string path)
    {
        RecCat.Export(cat, path);
    }

    public IReadOnlyList<BackupSnapshot> ListBackups(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.Backup.List(install);
    }

    public void Restore(
        Rpcs3InstallDescriptor install,
        BackupSnapshot backup)
    {
        _useCases.Backup.Restore(install, backup);
    }

    public static string BkpRoot(
        Rpcs3InstallDescriptor install)
    {
        return R3Paths.BkpRoot(install);
    }

    public IReadOnlyList<HCanaryStoredPatch> LoadHCanaryPatches(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.HCanary.Load(install);
    }

    public HCanarySaveResult SaveHCanaryPatch(
        Rpcs3InstallDescriptor install,
        HCanaryPatchDraft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        return _useCases.HCanary.Save(
            install,
            draft,
            originalIdentity,
            saveAsNew);
    }

    public HCanarySaveResult DeleteHCanaryPatch(
        Rpcs3InstallDescriptor install,
        string identity)
    {
        return _useCases.HCanary.Delete(
            install,
            identity);
    }
}
