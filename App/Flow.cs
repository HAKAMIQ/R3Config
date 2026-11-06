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

    public RecSyncLocalState? ReadRecSync(RecSvc baseline)
    {
        return _cases.RecommendationSync?.ReadLocal(baseline);
    }

    public RecSyncPreferences LoadRecSyncPreferences()
    {
        return _cases.RecommendationSync?.LoadPreferences() ??
            RecSyncPreferences.Default with { AutoUpdate = false };
    }

    public bool SaveRecSyncPreferences(RecSyncPreferences preferences)
    {
        return _cases.RecommendationSync?.SavePreferences(preferences) ?? false;
    }

    public bool IsRecSyncAvailable => _cases.RecommendationSync is not null;

    public bool IsRecSyncRunning => _cases.RecommendationSync?.IsRunning ?? false;

    public Task<RecSyncReport> SyncRecommendationsAsync(
        RecSvc baseline,
        bool manual,
        Rpcs3InstallDescriptor? install,
        CancellationToken cancellationToken = default)
    {
        return _cases.RecommendationSync?.SyncAsync(
                baseline,
                manual,
                install,
                cancellationToken) ??
            Task.FromResult(new RecSyncReport(
                RecSyncRunStatus.Failed,
                [],
                [],
                baseline.Count,
                baseline.Count,
                0,
                0,
                DateTimeOffset.UtcNow));
    }

    public Task<bool> MarkRecSyncNotifiedAsync(
        RecSvc baseline,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        return _cases.RecommendationSync?.MarkNotifiedAsync(
                baseline,
                ids,
                cancellationToken) ??
            Task.FromResult(false);
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
        HwInfo? hw,
        Rpcs3BuildIdentity? build = null)
    {
        return _cases.Config.Build(
            install,
            game,
            rec,
            hw,
            Rpcs3RuntimeBuild.From(build));
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
        IReadOnlyList<PatMatch> selectedMatches,
        IReadOnlyList<PatMatch> allMatches)
    {
        return _cases.PatchMutation.ApplyTxn(
            plan,
            install,
            selectedMatches,
            allMatches);
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

    public NclCorpusLookupResult DiscoverBundledNcl(
        string serial,
        string version,
        string? title = null)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.DiscoverBundled(
            serial,
            version,
            title);
    }

    public NclManagedPatchInspection InspectNcl(
        string nclPath,
        NclDialect dialect,
        string serial,
        string version)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.Inspect(
            nclPath,
            dialect,
            serial,
            version);
    }

    public NclManagedPatchPreview PrepareNclManagedPatch(
        NclPpuConversionFileRequest request,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? enabledMatches = null)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.Prepare(
            request,
            install,
            enabledMatches);
    }

    public NclManagedPatchPreview PrepareBundledNclManagedPatch(
        NclBundledManagedPatchRequest request,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? enabledMatches = null)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.PrepareBundled(
            request,
            install,
            enabledMatches);
    }

    public NclRunKeyEligibilityResult DescribeBundledNclRunKeys(
        NclBundledManagedPatchRequest request,
        IReadOnlyList<string> candidateRunKeys)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.DescribeBundledRunKeys(
            request,
            candidateRunKeys);
    }

    public NclRunKeyEligibilityResult DescribeNclRunKeys(
        NclPpuConversionFileRequest request,
        IReadOnlyList<string> candidateRunKeys)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.DescribeRunKeys(
            request,
            candidateRunKeys);
    }

    public PatRes ApplyNclManagedPatch(
        NclManagedPatchPreview preview,
        Rpcs3InstallDescriptor install)
    {
        var useCase = _cases.NclManagedPatch ??
            throw new InvalidOperationException(
                "NCL managed patch services are not configured.");

        return useCase.Apply(
            preview,
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

    public WikiEvidenceLoadResult LoadWikiEvidence(string baseDir)
    {
        return WikiEvidenceCases().Load(baseDir);
    }

    public Rpcs3BuildCatalog LoadRpcs3Builds(string baseDir)
    {
        return WikiEvidenceCases().LoadBuildCatalog(baseDir);
    }

    public DiagnosticEvidenceLoadResult LoadDiagnosticEvidence(string baseDir)
    {
        return WikiEvidenceCases().LoadDiagnosticEvidence(baseDir);
    }

    public KnowledgeLoadResult LoadKnowledge(string baseDir)
    {
        return WikiEvidenceCases().LoadKnowledge(baseDir);
    }

    public GameDatabaseLoadResult LoadGameDatabase(string baseDir)
    {
        return (_cases.GameDatabase ?? new GameDatabaseUseCase()).Load(baseDir);
    }

    public GameSettingsModel GameSettings(
        CfgPlan? plan,
        Rpcs3GameDescriptor game,
        WikiRec? rec,
        WikiEvidenceCatalog evidence,
        Rpcs3BuildIdentity? build,
        HwInfo? hardware = null,
        Rpcs3BuildCatalog? builds = null,
        Rpcs3KnowledgeCatalog? knowledge = null)
    {
        return WikiEvidenceCases().Project(
            plan,
            game,
            rec,
            evidence,
            build,
            hardware,
            builds,
            knowledge);
    }

    public RuntimeContextSnapshot RuntimeContext(
        HwInfo? hardware,
        Rpcs3InstallDescriptor? install,
        Rpcs3BuildIdentity? build,
        Rpcs3GameDescriptor? game,
        GameSettingsModel? settings,
        Rpcs3BuildCatalog? builds,
        DiagnosticEvidenceCatalog? evidence = null)
    {
        // Stateless and read-only, like the Wiki evidence use case.
        return (_cases.RuntimeContext ?? new RuntimeContextUseCase()).Build(
            hardware,
            install,
            build,
            game,
            settings,
            builds,
            evidence);
    }

    public DiagnosticKnowledgeBase LoadDiagnosticsKnowledge(string baseDir, out string? error)
    {
        return LogDiagnosticsCases().LoadKnowledge(baseDir, out error);
    }

    public LogDiagnosticsResult AnalyzeLogs(
        Rpcs3InstallDescriptor install,
        string? selectedSerial,
        HwInfo? hardware,
        Rpcs3BuildIdentity? build,
        DiagnosticKnowledgeBase knowledge,
        string? knowledgeError = null,
        CancellationToken cancellationToken = default)
    {
        return LogDiagnosticsCases().Analyze(install, selectedSerial, hardware, build, knowledge, knowledgeError, cancellationToken);
    }

    private LogDiagnosticsUseCase LogDiagnosticsCases()
    {
        // Stateless and read-only, like the other evidence use cases.
        return _cases.LogDiagnostics ?? new LogDiagnosticsUseCase();
    }

    private WikiEvidenceUseCase WikiEvidenceCases()
    {
        // Stateless and read-only, so a composition root that predates this
        // use case can still serve notes.
        return _cases.WikiEvidence ??
            new WikiEvidenceUseCase();
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
        return CaptureFingerprintAsync(
            install,
            game,
            expectation,
            replaceExisting: false,
            progress: progress,
            cancellationToken: cancellationToken);
    }

    public Task<GameFingerprintCaptureResult> CaptureFingerprintAsync(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation,
        bool replaceExisting,
        IProgress<GameFingerprintCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _cases.FingerprintCapture.CaptureAsync(
            install,
            game,
            expectation,
            replaceExisting,
            progress,
            cancellationToken);
    }

    public GameSetupPlan BuildGameSetup(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        RecSvc recommendationCatalog,
        PatCat patchCatalog,
        HwInfo? hardware = null,
        IReadOnlyList<PatchRequirementRule>? requirementRules = null,
        Rpcs3BuildIdentity? buildIdentity = null)
    {
        return _cases.GameSetup.Build(
            install,
            game,
            recommendationCatalog,
            patchCatalog,
            hardware,
            requirementRules,
            Rpcs3RuntimeBuild.From(buildIdentity));
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
