using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

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
        RecSvc cat,
        IProgress<AnalyzeStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _cases.Analysis.RunAsync(
            rpcs3ExePath,
            cat,
            progress,
            cancellationToken);
    }

    public Task<GameScanResult> ScanGameLibraryAsync(
        string rpcs3ExePath,
        CancellationToken cancellationToken = default)
    {
        return _cases.GameLibrary.ScanAsync(
            rpcs3ExePath,
            cancellationToken);
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
        PatCat patchCat,
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
        bool expertMode,
        IReadOnlyList<PatMatch>? allMatches = null)
    {
        return _cases.PatchMutation.BuildOn(
            install,
            matches,
            enabledMatches,
            expertMode,
            allMatches);
    }

    public PatchApplyPlan OffPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches,
        IReadOnlyList<PatMatch>? enabledMatches = null,
        IReadOnlyList<PatMatch>? allMatches = null)
    {
        return _cases.PatchMutation.BuildOff(
            install,
            matches,
            enabledMatches,
            allMatches);
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

    public PatRes OffPat(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? selectedMatches = null)
    {
        return _cases.PatchMutation.Disable(
            plan,
            install,
            selectedMatches);
    }

    public PatRes RemoveArtemis(
        PatchApplyPlan disablePlan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches)
    {
        return _cases.PatchMutation.RemoveArtemis(disablePlan, install, matches);
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

#pragma warning disable CA1822
    public RecSvc BuildCat(string xmlPath)
    {
        return RecommendationCatalogUseCase.Rebuild(xmlPath);
    }
#pragma warning restore CA1822

    public string CatText(CatInfo info)
    {
        return _cases.Recommendations.FmtInf(info);
    }

#pragma warning disable CA1822
    public void ExpCat(
        RecSvc cat,
        string path)
    {
        RecommendationCatalogUseCase.Export(cat, path);
    }
#pragma warning restore CA1822

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

    public bool DeleteBkp(
        Rpcs3InstallDescriptor install,
        BackupSnapshot backup)
    {
        return _cases.Backup.Delete(install, backup);
    }

    public int DeleteOldBkp(
        Rpcs3InstallDescriptor install,
        int keepCount = BackupUseCase.DefaultRetentionCount)
    {
        return _cases.Backup.DeleteOld(install, keepCount);
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

    public HCanDiscoverySession BeginHashDiscovery(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return _cases.HashEvidence.Begin(
            install,
            game);
    }

    public void AttachHashDiscovery(
        HCanDiscoverySession session,
        int processId)
    {
        _cases.HashEvidence.AttachProcess(
            session,
            processId);
    }

    public void CancelHashDiscovery(
        HCanDiscoverySession session)
    {
        _cases.HashEvidence.Cancel(session);
    }

    public HashReadCode CheckHashProcess(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return _cases.HashEvidence.CheckRunning(
            install,
            game);
    }

    public HashRead ReadHashes(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        return _cases.HashEvidence.Read(
            install,
            game,
            cancellationToken);
    }

    public HashRead LoadSavedHashes(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return _cases.HashEvidence.LoadSaved(
            install,
            game);
    }

    public bool DeleteSavedHashes(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return _cases.HashEvidence.DeleteSaved(
            install,
            game);
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

    public Task<GameFingerprintCaptureResult> CaptureFingerprintAsync(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        IProgress<GameFingerprintCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return CaptureFingerprintAsync(
            install,
            game,
            CanaryHashExpectation.Empty,
            progress,
            cancellationToken);
    }

    public Task<GameFingerprintCaptureResult> CaptureFingerprintAsync(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation,
        IProgress<GameFingerprintCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _cases.FingerprintCapture.CaptureAsync(
            install,
            game,
            expectation,
            progress,
            cancellationToken);
    }

    public GameSetupPlan BuildGameSetup(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        RecSvc recommendationCatalog,
        PatCat patchCatalog,
        HwInfo? hardware = null,
        IReadOnlyList<PatchRequirementRule>? requirementRules = null)
    {
        return _cases.GameSetup.Build(
            install,
            game,
            recommendationCatalog,
            patchCatalog,
            hardware,
            requirementRules);
    }

    public GameSetupPreview PreviewGameSetup(
        GameSetupPlan plan,
        GameSetupSelection selection)
    {
        return _cases.GameSetup.Preview(plan, selection);
    }

    public GameSetupResult ApplyGameSetup(
        Rpcs3InstallDescriptor install,
        GameSetupPreview preview)
    {
        return _cases.GameSetup.Apply(
            install,
            preview);
    }
}
