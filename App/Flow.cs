using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.UseCases;

namespace R3Config;

/// <summary>
/// Compatibility facade for the WPF layer. Business workflows live in
/// Application use cases and are composed in <see cref="AppComposition"/>.
/// </summary>
public sealed class AppFlow
{
    private readonly AppUseCases _useCases;

    public AppFlow()
        : this(AppComposition.CreateUseCases())
    {
    }

    public AppFlow(AppUseCases useCases)
    {
        _useCases = useCases ??
            throw new ArgumentNullException(nameof(useCases));
    }

    public CatLoadResult LoadBuiltInCat(string baseDir)
    {
        return _useCases.RecommendationCatalog.LoadBuiltIn(baseDir);
    }

    public Task<AnalyzeResult> AnalyzeAsync(
        string rpcs3ExePath,
        RecommendationCatalogService cat)
    {
        return _useCases.Analysis.ExecuteAsync(
            rpcs3ExePath,
            cat);
    }

    public ConfigPlan BuildCfgPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiGameRecommendation? rec,
        HwInfo? hw)
    {
        return _useCases.Config.BuildPlan(
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

    public PatchCatResult LoadPatchCat(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchCatalog.Load(install);
    }

    public CanaryPatchWriteResult PrepareBundledCanaryPatch(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        return _useCases.PatchMutation.PrepareCanary(
            install,
            selectedMatches);
    }

    public CanaryPatchRemoveResult RemovePreparedCanaryPatch(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchMutation.RemovePreparedCanary(install);
    }

    public PatchLibraryWriteResult PrepareBundledCheatPatch(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        return _useCases.PatchMutation.PrepareCheats(
            install,
            selectedMatches);
    }

    public CanaryPatchNormalizeResult NormalizeCanaryPatch(
        string? sourceText)
    {
        return _useCases.CanaryImport.Normalize(sourceText);
    }

    public bool HasManagedImportedPatch(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.CanaryImport.HasManagedImportedPatch(
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
        return _useCases.PatchCatalog.BuildPreview(
            patchCat,
            game);
    }

    public PatchApplyPlan BuildPatchPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        bool expertMode)
    {
        return _useCases.PatchMutation.BuildEnablePlan(
            install,
            matches,
            expertMode);
    }

    public PatchApplyPlan BuildPatchDisablePlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches)
    {
        return _useCases.PatchMutation.BuildDisablePlan(
            install,
            matches);
    }

    public PatchApplyChange? CreatePatchChange(
        Rpcs3PatchMatchResult match)
    {
        return _useCases.PatchMutation.CreateChange(match);
    }

    public PatchApplyResult ApplyPatchTransaction(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        return _useCases.PatchMutation.ApplyTransaction(
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
        return _useCases.PatchCatalog.CanReadPatchConfig(install);
    }

    public PatchConfigSnapshot ReadPatchCfg(
        Rpcs3InstallDescriptor install)
    {
        return _useCases.PatchCatalog.ReadPatchConfig(install);
    }

    public RecommendationCatalogService RebuildCat(string xmlPath)
    {
        return _useCases.RecommendationCatalog.Rebuild(xmlPath);
    }

    public string CatInfoText(CatalogInfo info)
    {
        return _useCases.RecommendationCatalog.FormatInfo(info);
    }

    public void ExportCat(
        RecommendationCatalogService cat,
        string path)
    {
        RecommendationCatalogUseCase.Export(cat, path);
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
        return Rpcs3AppPaths.BackupRoot(install);
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
