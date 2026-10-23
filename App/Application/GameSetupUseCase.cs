using System.IO;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class GameSetupUseCase(
    PlanService configPlanner,
    GameSetupPlanningService setupPlanner,
    PatPlnr patchPlanner,
    CfgRead patchConfigReader,
    CfgWrite patchConfigWriter,
    BundledCanaryPatchService bundledCanary,
    BundledCheatPatchService bundledCheats,
    ReadSvc patchReader,
    PatchFileTransactionService transaction,
    IRpcs3MutationGuard mutationGuard,
    PatchRequirementCatalogService? requirementCatalogService = null,
    string? requirementCatalogPath = null,
    string? appBaseDirectory = null)
{
    private readonly PatchRequirementCatalogService _requirementCatalogService =
        requirementCatalogService ?? new PatchRequirementCatalogService();

    private readonly string _requirementCatalogPath = requirementCatalogPath ??
        Path.Combine(
            appBaseDirectory ?? AppContext.BaseDirectory,
            "Data",
            "patch_requirements.json");

    private readonly string _appBaseDirectory =
        appBaseDirectory ?? AppContext.BaseDirectory;

    public GameSetupPlan Build(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        RecSvc recommendationCatalog,
        PatCat patchCatalog,
        HwInfo? hardware = null,
        IReadOnlyList<PatchRequirementRule>? requirementRules = null,
        Rpcs3RuntimeBuild? build = null)
    {
        ArgumentNullException.ThrowIfNull(recommendationCatalog);

        var recommendation = recommendationCatalog.Find(game.Serial);

        // Wiki generation resolves ownership from this catalog's source snapshot
        // and excludes conflicting bindings. A serial-only blacklist cannot
        // represent evidence that differs between snapshots or resolved pages.
        var configStateBefore =
            PatchFileTransactionService.CaptureExpectedState(
                Path.Combine(
                    install.CustPath,
                    $"config_{game.Serial}.yml"),
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");

        var configPlan = configPlanner.BuildCfg(
            install,
            game,
            recommendation,
            hardware,
            build);

        var configState =
            PatchFileTransactionService.CaptureExpectedState(
                configPlan.CfgPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (!SameTargetState(configStateBefore, configState))
        {
            throw new IOException(
                "RPCS3 custom config changed while Game Setup was building its preview. Reopen Game Setup and try again.");
        }

        var authoredRules = requirementRules?.ToList() ?? [];

        if (File.Exists(_requirementCatalogPath))
        {
            var catalog = _requirementCatalogService.Load(
                _requirementCatalogPath);

            authoredRules.AddRange(
                _requirementCatalogService.Resolve(
                    catalog,
                    patchCatalog.Entries));
        }

        var plan = setupPlanner.Build(
            install,
            game,
            recommendationCatalog.Info,
            configPlan,
            patchCatalog,
            authoredRules);

        var enabled = patchConfigReader.ReadSnap(install.PatCfg);

        plan = plan with
        {
            ExpectedCustomConfigExists = configState.Exists,
            ExpectedCustomConfigSha256 = configState.Sha256,
            OfficialPatches = MarkEnabled(
                plan.OfficialPatches,
                enabled),
            CanaryPatches = MarkEnabled(
                plan.CanaryPatches,
                enabled),
            ArtemisPatches = MarkEnabled(
                plan.ArtemisPatches,
                enabled),
            CustomPatches = MarkEnabled(
                plan.CustomPatches,
                enabled)
        };

        return plan;
    }

    public GameSetupPreview Preview(
        GameSetupPlan plan,
        GameSetupSelection selection)
    {
        var preview = setupPlanner.Preview(
            plan,
            selection);

        if (preview.EffectiveSettings.Count == 0)
        {
            return preview;
        }

        var customConfigPath = plan.Game.CustomConfigPath;
        var currentState =
            PatchFileTransactionService.CaptureExpectedState(
                customConfigPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        var reviewedState = new PatchTargetState(
            plan.ExpectedCustomConfigExists ?? currentState.Exists,
            plan.ExpectedCustomConfigSha256,
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");

        if (plan.ExpectedCustomConfigExists is not null &&
            !SameTargetState(currentState, reviewedState))
        {
            throw new IOException(
                "RPCS3 custom config changed before Game Setup preview was finalized. Rebuild the plan and try again.");
        }

        var pendingSettings = ChangedSettings(preview);
        if (pendingSettings.Length == 0)
        {
            return preview with
            {
                ExpectedPreparedCustomConfigExists = currentState.Exists,
                ExpectedPreparedCustomConfigSha256 = currentState.Sha256
            };
        }

        if (!R3Yaml.CanRead(customConfigPath))
        {
            throw new InvalidDataException(
                "The current RPCS3 custom config is invalid or unreadable.");
        }

        var preparedText = R3Yaml.Build(
            customConfigPath,
            pendingSettings);
        R3Yaml.VldText(preparedText);

        var verifiedState =
            PatchFileTransactionService.CaptureExpectedState(
                customConfigPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (!SameTargetState(currentState, verifiedState))
        {
            throw new IOException(
                "RPCS3 custom config changed while Game Setup was finalizing its preview. Rebuild the plan and try again.");
        }

        return preview with
        {
            ExpectedPreparedCustomConfigExists = true,
            ExpectedPreparedCustomConfigSha256 = Sha256Utf8(preparedText)
        };
    }

    public GameSetupResult Apply(
        Rpcs3InstallDescriptor install,
        GameSetupPreview preview,
        Action<PatPhase, string, IReadOnlyList<string>>? phaseObserver = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(preview);

        return ApplyGuarded(install, preview, phaseObserver);
    }

    private GameSetupResult ApplyGuarded(
        Rpcs3InstallDescriptor install,
        GameSetupPreview preview,
        Action<PatPhase, string, IReadOnlyList<string>>? phaseObserver)
    {
        if (!preview.CanApply)
        {
            throw new InvalidOperationException(
                preview.Conflicts.Count == 0
                    ? "Game Setup has no selected changes."
                    : string.Join(
                        Environment.NewLine,
                        preview.Conflicts));
        }

        ValidateInstallIdentity(
            install,
            preview.Plan.Game);

        VerifyReviewedDefinitions(
            preview.SelectedPatches
                .Select(candidate => candidate.Match)
                .ToArray());

        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var officialPath = Path.Combine(
            install.PatPath,
            "patch.yml");

        var officialState =
            PatchFileTransactionService.CaptureExpectedState(
                officialPath,
                R3Limits.PatCatBy,
                "RPCS3-owned patch.yml");

        var declaredTargets = DeclaredTargets(
            install,
            preview);

        mutationGuard.CheckCan(
            install,
            declaredTargets,
            [backupRoot]);

        PatchFileTransactionService.ValidateExpectedState(
            officialPath,
            officialState);

        var preparedFiles = new List<PatchPreparedFile>();
        var customConfigPath = preview.Plan.Game.CustomConfigPath;
        var currentConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                customConfigPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        var reviewedConfigState = new PatchTargetState(
            preview.Plan.ExpectedCustomConfigExists ??
                currentConfigState.Exists,
            preview.Plan.ExpectedCustomConfigSha256,
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");
        var preparedConfigState = new PatchTargetState(
            preview.ExpectedPreparedCustomConfigExists ?? false,
            preview.ExpectedPreparedCustomConfigSha256,
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");
        var configMatchesReview =
            preview.Plan.ExpectedCustomConfigExists is null ||
            SameTargetState(
                currentConfigState,
                reviewedConfigState);
        var configMatchesPrepared =
            preview.ExpectedPreparedCustomConfigExists is not null &&
            SameTargetState(
                currentConfigState,
                preparedConfigState);

        if (!configMatchesReview && !configMatchesPrepared)
        {
            throw new IOException(
                "RPCS3 custom config changed after Game Setup preview. Reopen the preview and try again.");
        }

        var settings = configMatchesPrepared
            ? []
            : ChangedSettings(preview);

        var verifiedConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                customConfigPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (!SameTargetState(
                currentConfigState,
                verifiedConfigState))
        {
            throw new IOException(
                "RPCS3 custom config changed while Game Setup was validating its reviewed state. Reopen the preview and try again.");
        }

        if (settings.Length > 0)
        {
            if (!R3Yaml.CanRead(customConfigPath))
            {
                throw new InvalidDataException(
                    "The current RPCS3 custom config is invalid or unreadable.");
            }

            var content = R3Yaml.Build(
                customConfigPath,
                settings);

            R3Yaml.VldText(content);

            if (preview.ExpectedPreparedCustomConfigExists == true &&
                !string.Equals(
                    Sha256Utf8(content),
                    preview.ExpectedPreparedCustomConfigSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Game Setup custom config output no longer matches the reviewed preview.");
            }

            preparedFiles.Add(
                new PatchPreparedFile(
                    customConfigPath,
                    content,
                    PrepKind.CustomCfg,
                    ExpectedExists: currentConfigState.Exists,
                    ExpectedSha256: currentConfigState.Sha256));
        }

        var selectedMatches = preview.SelectedPatches
            .Select(candidate => candidate.Match)
            .ToArray();

        var beforeImported = ReadImported(install);

        PrepareCanary(
            install,
            selectedMatches,
            preparedFiles);

        var beforeArtemis = ReadManagedArtemis(
            install,
            preview.Plan.Game.Serial);

        PrepareArtemis(
            install,
            selectedMatches,
            preparedFiles);

        var patchConfigStateBefore =
            PatchFileTransactionService.CaptureExpectedState(
                install.PatCfg,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        var patchSnapshot =
            patchConfigReader.ReadSnap(install.PatCfg);
        var patchConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                install.PatCfg,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        if (!SameTargetState(
                patchConfigStateBefore,
                patchConfigState))
        {
            throw new IOException(
                "patch_config.yml changed while Game Setup was rebuilding live patch state. Reopen the preview and try again.");
        }

        var enabledMatches = preview.Plan.Patches
            .Select(candidate => candidate.Match)
            .Where(match =>
            {
                var change = patchPlanner.Build3(match);

                return change is not null &&
                       patchSnapshot.IsOn(change);
            })
            .ToArray();

        var patchPlan = patchPlanner.Build(
            install,
            selectedMatches,
            expertMode: false,
            enabledMatches) with
        {
            ExpectedPatCfgExists = patchConfigState.Exists,
            ExpectedPatCfgSha256 = patchConfigState.Sha256
        };

        var patchChanges = patchPlan.Changes
            .Where(change => !patchSnapshot.IsOn(change))
            .ToArray();

        if (patchChanges.Length > 0)
        {
            PatchFileTransactionService.ValidateExpectedState(
                install.PatCfg,
                patchConfigState);
            var patchConfigText = patchConfigWriter.Build(
                install.PatCfg,
                patchChanges);

            CfgWrite.VldText(patchConfigText);

            preparedFiles.Add(
                new PatchPreparedFile(
                    install.PatCfg,
                    patchConfigText,
                    PrepKind.PatCfg,
                    ExpectedExists: patchConfigState.Exists,
                    ExpectedSha256: patchConfigState.Sha256));
        }

        preparedFiles =
        [
            .. preparedFiles
                .GroupBy(
                    file => Path.GetFullPath(file.TgtPath),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Single())
        ];

        if (preparedFiles.Count == 0)
        {
            PatchFileTransactionService.ValidateExpectedState(
                officialPath,
                officialState);

            Verify(
                install,
                preview,
                settings,
                patchChanges,
                beforeImported,
                beforeArtemis,
                officialPath,
                officialState);

            return new GameSetupResult(
                IsGameReady: true,
                AlreadyPrepared: true,
                AppliedGameSettings: 0,
                EnabledOfficialPatches: 0,
                InstalledCanaryPatches: 0,
                EnabledCanaryPatches: 0,
                InstalledArtemisPatches: 0,
                EnabledArtemisPatches: 0,
                preview.Warnings,
                Backup: null);
        }

        var targets = preparedFiles
            .Select(file => file.TgtPath)
            .ToArray();

        mutationGuard.CheckCan(
            install,
            targets,
            [backupRoot]);

        PatchFileTransactionService.ValidateExpectedState(
            officialPath,
            officialState);

        var backup = transaction.Commit(
            backupRoot,
            preparedFiles,
            (phase, root, paths) =>
            {
                phaseObserver?.Invoke(
                    phase,
                    root,
                    paths);

                mutationGuard.CheckCan(
                    install,
                    paths,
                    [root]);

                PatchFileTransactionService.ValidateExpectedState(
                    officialPath,
                    officialState);
            });

        try
        {
            Verify(
                install,
                preview,
                settings,
                patchChanges,
                beforeImported,
                beforeArtemis,
                officialPath,
                officialState);
        }
        catch (Exception verificationException)
        {
            try
            {
                transaction.RollbackCommitted(
                    backup,
                    preparedFiles);
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Game Setup verification failed and rollback also failed.",
                    new AggregateException(
                        verificationException,
                        rollbackException));
            }

            throw;
        }

        var officialCount = preview.SelectedPatches.Count(candidate =>
            candidate.Provenance.Kind is
                SetupPatchSourceKind.OfficialMain or
                SetupPatchSourceKind.OfficialSpu &&
            patchChanges.Any(change =>
                Same(change, candidate.Match)));

        var canaryEnabled = preview.SelectedPatches.Count(candidate =>
            candidate.Provenance.Kind ==
                SetupPatchSourceKind.Canary &&
            patchChanges.Any(change =>
                Same(change, candidate.Match)));

        var canaryInstalled = CountNewCanary(
            preview.SelectedPatches,
            beforeImported);

        var artemisEnabled = preview.SelectedPatches.Count(candidate =>
            candidate.Provenance.Kind ==
                SetupPatchSourceKind.Artemis &&
            patchChanges.Any(change =>
                Same(change, candidate.Match)));

        var artemisInstalled = CountNewArtemis(
            preview.SelectedPatches,
            beforeArtemis);

        return new GameSetupResult(
            IsGameReady: true,
            AlreadyPrepared: false,
            settings.Length,
            officialCount,
            canaryInstalled,
            canaryEnabled,
            artemisInstalled,
            artemisEnabled,
            preview.Warnings,
            backup);
    }

    private void PrepareArtemis(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        List<PatchPreparedFile> preparedFiles)
    {
        var selected = selectedMatches
            .Where(match =>
                match.Patch.Source == PatSrc.Artemis)
            .ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        var staged = bundledCheats.Prep(
            install.PatPath,
            _appBaseDirectory,
            selected);

        if (!staged.IsOk)
        {
            throw new InvalidOperationException(
                staged.Error ?? staged.Message);
        }

        if (staged.PreparedFile is not null)
        {
            preparedFiles.Add(staged.PreparedFile);
        }

        foreach (var extra in staged.Extra)
        {
            preparedFiles.Add(extra);
        }
    }

    private PatCat ReadManagedArtemis(
        Rpcs3InstallDescriptor install,
        string serial)
    {
        var path = BundledCheatPatchService.GetPath(
            install.PatPath,
            serial);

        if (!File.Exists(path))
        {
            return new PatCat("1.2", []);
        }

        var read = patchReader.Read(
            path,
            PatSrc.ManagedArtemis);

        if (!read.IsOk)
        {
            throw new InvalidDataException(
                read.Error ??
                "Managed Artemis patch YAML is invalid.");
        }

        return read.Catalog;
    }

    private void PrepareCanary(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        List<PatchPreparedFile> preparedFiles)
    {
        var selected = selectedMatches
            .Where(match =>
                match.Patch.Source == PatSrc.Can)
            .ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        var staged = bundledCanary.Prep(
            install.PatPath,
            _appBaseDirectory,
            selected);

        if (!staged.IsOk)
        {
            throw new InvalidOperationException(
                staged.Error ?? staged.Message);
        }

        if (staged.PreparedFile is not null)
        {
            preparedFiles.Add(staged.PreparedFile);
        }
    }

    private PatCat ReadImported(
        Rpcs3InstallDescriptor install)
    {
        var path = Rpcs3AppPaths.ImpPat(install);

        if (!File.Exists(path))
        {
            return new PatCat("1.2", []);
        }

        var read = patchReader.Read(
            path,
            PatSrc.Imported);

        if (!read.IsOk)
        {
            throw new InvalidDataException(
                read.Error ??
                "imported_patch.yml is invalid.");
        }

        return read.Catalog;
    }

    private void Verify(
        Rpcs3InstallDescriptor install,
        GameSetupPreview preview,
        IReadOnlyList<CfgChg> settings,
        IReadOnlyList<PatChg> patchChanges,
        PatCat beforeImported,
        PatCat beforeArtemis,
        string officialPath,
        PatchTargetState officialState)
    {
        PatchFileTransactionService.ValidateExpectedState(
            officialPath,
            officialState);

        R3Yaml.VldText(
            File.Exists(
                preview.Plan.Game.CustomConfigPath)
                ? R3Bound.ReadUtf8(
                    preview.Plan.Game.CustomConfigPath,
                    R3Limits.CfgByte,
                    "RPCS3 custom config YAML")
                : "{}\r\n");

        foreach (var setting in settings)
        {
            var current = R3Yaml.ReadVal(
                preview.Plan.Game.CustomConfigPath,
                setting.Section,
                setting.Key);

            if (!string.Equals(
                    current,
                    NormalizeConfigValue(setting.Value),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Game Setup verification failed for setting '{setting.Path}'.");
            }
        }

        var patchSnapshot =
            patchConfigReader.ReadSnap(install.PatCfg);

        foreach (var change in patchChanges)
        {
            if (!patchSnapshot.IsOn(change))
            {
                throw new InvalidDataException(
                    $"Game Setup verification failed for patch '{change.PatTitle}'.");
            }
        }

        var afterImported = ReadImported(install);

        foreach (var existing in beforeImported.Entries)
        {
            if (!Contains(afterImported, existing))
            {
                throw new InvalidDataException(
                    $"Game Setup removed an existing user patch '{existing.Title}'.");
            }
        }

        foreach (var candidate in preview.SelectedPatches.Where(
                     candidate =>
                         candidate.Match.Patch.Source == PatSrc.Can))
        {
            if (!Contains(
                    afterImported,
                    candidate.Match.Patch))
            {
                throw new InvalidDataException(
                    $"Game Setup did not install Canary patch '{candidate.Match.Patch.Title}'.");
            }
        }

        var afterArtemis = ReadManagedArtemis(
            install,
            preview.Plan.Game.Serial);

        foreach (var existing in beforeArtemis.Entries)
        {
            if (!Contains(afterArtemis, existing))
            {
                throw new InvalidDataException(
                    $"Game Setup removed an existing managed Artemis patch '{existing.Title}'.");
            }
        }

        foreach (var candidate in preview.SelectedPatches.Where(
                     candidate =>
                         candidate.Match.Patch.Source ==
                         PatSrc.Artemis))
        {
            if (!Contains(
                    afterArtemis,
                    candidate.Match.Patch))
            {
                throw new InvalidDataException(
                    $"Game Setup did not install Artemis patch '{candidate.Match.Patch.Title}'.");
            }
        }

        PatchFileTransactionService.ValidateExpectedState(
            officialPath,
            officialState);
    }

    private static string[] DeclaredTargets(
        Rpcs3InstallDescriptor install,
        GameSetupPreview preview)
    {
        var targets = new List<string>();

        if (preview.EffectiveSettings.Count > 0)
        {
            targets.Add(
                preview.Plan.Game.CustomConfigPath);
        }

        if (preview.SelectedPatches.Count > 0)
        {
            targets.Add(install.PatCfg);
        }

        if (preview.SelectedPatches.Any(candidate =>
                candidate.Match.Patch.Source == PatSrc.Can))
        {
            targets.Add(
                Rpcs3AppPaths.ImpPat(install));
        }

        if (preview.SelectedPatches.Any(candidate =>
                candidate.Match.Patch.Source == PatSrc.Artemis))
        {
            targets.Add(
                BundledCheatPatchService.GetPath(
                    install.PatPath,
                    preview.Plan.Game.Serial));

            targets.Add(
                BundledCheatPatchService.GetPath(
                    install.PatPath));

            targets.Add(
                BundledCheatPatchService.OldPath(
                    install.PatPath));
        }

        return
        [
            .. targets.Distinct(
                StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string Sha256Utf8(string text)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static CfgChg[] ChangedSettings(
        GameSetupPreview preview)
    {
        return
        [
            .. preview.EffectiveSettings.Where(setting =>
                !string.Equals(
                    R3Yaml.ReadVal(
                        preview.Plan.Game.CustomConfigPath,
                        setting.Section,
                        setting.Key),
                    NormalizeConfigValue(setting.Value),
                    StringComparison.Ordinal))
        ];
    }

    private static void ValidateInstallIdentity(
        Rpcs3InstallDescriptor install,
        GameIdentity identity)
    {
        var expected = Path.GetFullPath(
            Path.Combine(
                install.CustPath,
                $"config_{identity.Serial}.yml"));

        if (!expected.Equals(
                Path.GetFullPath(
                    identity.CustomConfigPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "Game Setup custom configuration target does not match the selected RPCS3 installation.");
        }
    }

    private static bool Contains(
        PatCat catalog,
        Rpcs3PatchEntry expected)
    {
        return catalog.Entries.Any(candidate =>
            candidate.RunKey.Equals(
                expected.RunKey,
                StringComparison.OrdinalIgnoreCase) &&
            candidate.Title.Equals(
                expected.Title,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                candidate.PatVer,
                expected.PatVer,
                StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(expected.DefPrint) ||
             candidate.DefPrint?.Equals(
                 expected.DefPrint,
                 StringComparison.OrdinalIgnoreCase) == true));
    }

    private static int CountNewCanary(
        IReadOnlyList<SetupPatchCandidate> selected,
        PatCat beforeImported)
    {
        return selected.Count(candidate =>
            candidate.Match.Patch.Source == PatSrc.Can &&
            !Contains(
                beforeImported,
                candidate.Match.Patch));
    }

    private static int CountNewArtemis(
        IReadOnlyList<SetupPatchCandidate> selected,
        PatCat beforeManaged)
    {
        return selected.Count(candidate =>
            candidate.Match.Patch.Source == PatSrc.Artemis &&
            !Contains(
                beforeManaged,
                candidate.Match.Patch));
    }

    private static void VerifyReviewedDefinitions(
        IReadOnlyList<PatMatch> selectedMatches)
    {
        var reader = new ReadSvc();

        foreach (var group in selectedMatches
                     .Where(match =>
                         !string.IsNullOrWhiteSpace(match.Patch.DefPrint) &&
                         !string.IsNullOrWhiteSpace(match.Patch.SrcPath) &&
                         Path.IsPathRooted(match.Patch.SrcPath))
                     .GroupBy(
                         match => match.Patch.SrcPath!,
                         StringComparer.OrdinalIgnoreCase))
        {
            var path = group.Key;
            if (!File.Exists(path))
            {
                throw new IOException(
                    $"Patch source changed after Game Setup preview because '{path}' is no longer available.");
            }

            var source = group.First().Patch.Source;
            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCatBy,
                Path.GetFileName(path));
            var read = reader.ReadText(text, source, path);
            if (!read.IsOk)
            {
                throw new InvalidDataException(
                    "Patch source changed after Game Setup preview and is no longer valid: " +
                    (read.Error ?? path));
            }

            foreach (var match in group)
            {
                if (!read.Catalog.Entries.Any(entry =>
                        entry.RunKey.Equals(match.Patch.RunKey, StringComparison.OrdinalIgnoreCase) &&
                        entry.Title.Equals(match.Patch.Title, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(entry.PatVer, match.Patch.PatVer, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(entry.DefPrint, match.Patch.DefPrint, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new IOException(
                        $"Patch definition '{match.Patch.Title}' changed after Game Setup preview. Reopen Game Setup before applying it.");
                }
            }
        }
    }

    private static bool SameTargetState(
        PatchTargetState left,
        PatchTargetState right)
    {
        return left.Exists == right.Exists &&
               string.Equals(
                   left.Sha256,
                   right.Sha256,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool Same(
        PatChg change,
        PatMatch match)
    {
        return change.HashKey.Equals(
                   match.EffectiveRunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               change.PatTitle.Equals(
                   match.Patch.Title,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeConfigValue(
        string value)
    {
        if (value.EndsWith('%') &&
            int.TryParse(
                value[..^1],
                out var percent))
        {
            return percent.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return value;
    }

    private SetupPatchCandidate[] MarkEnabled(
        IReadOnlyList<SetupPatchCandidate> candidates,
        CfgSnap snapshot)
    {
        return
        [
            .. candidates.Select(candidate =>
            {
                var change =
                    patchPlanner.Build3(candidate.Match);

                return candidate with
                {
                    IsEnabled =
                        change is not null &&
                        snapshot.IsOn(change)
                };
            })
        ];
    }
}
