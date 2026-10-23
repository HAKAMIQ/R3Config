using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class PatchMutationUseCase(
    Svc.PatPlnr planner,
    PatchApplyService applyService,
    BundledCanaryPatchService bundledCanary,
    BundledCheatPatchService bundledCheats,
    IRpcs3MutationGuard mutationGuard)
{
    public Mdl.PatchApplyPlan BuildOn(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches,
        IReadOnlyList<PatMatch> enabledMatches,
        bool expertMode,
        IReadOnlyList<PatMatch>? allMatches = null)
    {
        VerifyCanaryArtemisEnable(matches, allMatches);
        var (liveEnabled, state) = LiveEnabled(
            install,
            allMatches,
            enabledMatches);
        var plan = planner.Build(
            install,
            matches,
            expertMode,
            liveEnabled);
        return plan with
        {
            ExpectedPatCfgExists = state.Exists,
            ExpectedPatCfgSha256 = state.Sha256
        };
    }

    public Mdl.PatchApplyPlan BuildOff(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches,
        IReadOnlyList<PatMatch>? enabledMatches = null,
        IReadOnlyList<PatMatch>? allMatches = null)
    {
        var (liveEnabled, state) = LiveEnabled(
            install,
            allMatches,
            enabledMatches ?? []);
        var plan = planner.Build2(
            install,
            matches,
            liveEnabled);
        return plan with
        {
            ExpectedPatCfgExists = state.Exists,
            ExpectedPatCfgSha256 = state.Sha256
        };
    }

    private (IReadOnlyList<PatMatch> Enabled, PatchTargetState State) LiveEnabled(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? allMatches,
        IReadOnlyList<PatMatch> fallbackEnabled)
    {
        var before = PatchFileTransactionService.CaptureExpectedState(
            install.PatCfg,
            R3Limits.PatCfgBy,
            "patch_config.yml");

        if (allMatches is null)
        {
            return (fallbackEnabled, before);
        }

        var snapshot = new CfgRead().ReadSnap(install.PatCfg);
        var after = PatchFileTransactionService.CaptureExpectedState(
            install.PatCfg,
            R3Limits.PatCfgBy,
            "patch_config.yml");
        if (!Same(before, after))
        {
            throw new IOException(
                "patch_config.yml changed while the patch plan was being built. Reload patches and try again.");
        }

        var enabled = allMatches
            .Where(match => planner.Build3(match) is { } change && snapshot.IsOn(change))
            .ToArray();
        return (enabled, after);
    }

    private static bool Same(PatchTargetState left, PatchTargetState right)
    {
        return left.Exists == right.Exists &&
               string.Equals(
                   left.Sha256,
                   right.Sha256,
                   StringComparison.OrdinalIgnoreCase);
    }

    public PatChg? BuildChg(
        PatMatch match)
    {
        return planner.Build3(match);
    }

    public PatRes ApplyTxn(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        IReadOnlyList<PatMatch>? allMatches = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedMatches);

        return ApplyTxnGuarded(plan, install, selectedMatches, allMatches);
    }

    private PatRes ApplyTxnGuarded(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        IReadOnlyList<PatMatch>? allMatches)
    {
        Check(plan, install);
        VerifyCanaryArtemisEnable(selectedMatches, allMatches);
        CheckSrc(selectedMatches);
        VerifyReviewedDefinitions(selectedMatches);
        VerifyRuntimeIdentityCollisions(install, selectedMatches, allowSelectedNewer: true);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var declaredTargets = new List<string>
        {
            plan.PatCfg
        };
        if (selectedMatches.Any(match =>
                match.Patch.Source ==
                PatSrc.Can))
        {
            declaredTargets.Add(
                Rpcs3AppPaths.ImpPat(install));
        }

        declaredTargets.AddRange(
            BundledCheatPatchService.GetPaths(
                install.PatPath,
                selectedMatches));
        if (selectedMatches.Any(match =>
                match.Patch.Source ==
                PatSrc.Artemis))
        {
            declaredTargets.Add(
                BundledCheatPatchService.GetPath(install.PatPath));
            declaredTargets.Add(
                BundledCheatPatchService.OldPath(install.PatPath));
        }
        mutationGuard.CheckCan(
            install,
            declaredTargets,
            [backupRoot]);

        var preparedFiles = new List<PatchPreparedFile>();
        var messages = new List<string>();

        PrepCan(
            install,
            selectedMatches,
            preparedFiles,
            messages);
        PrepCht(
            install,
            selectedMatches,
            preparedFiles,
            messages);

        mutationGuard.CheckCan(
            install,
            [
                plan.PatCfg,
                .. preparedFiles.Select(file => file.TgtPath)
            ],
            [backupRoot]);

        var result = applyService.ApplyTxn(
            plan,
            backupRoot,
            preparedFiles,
            VldFunc(install));

        return result with
        {
            PrepMsgs = messages
        };
    }

    public PatRes ApplyPreparedManaged(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        PatMatch selectedMatch,
        LibStRes preparation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedMatch);

        return ApplyPreparedManagedGuarded(plan, install, selectedMatch, preparation);
    }

    private PatRes ApplyPreparedManagedGuarded(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        PatMatch selectedMatch,
        LibStRes preparation)
    {
        Check(plan, install);
        ArgumentNullException.ThrowIfNull(selectedMatch);
        ArgumentNullException.ThrowIfNull(preparation);

        if (!preparation.IsOk)
        {
            throw new InvalidOperationException(
                preparation.Error ??
                preparation.Message);
        }

        if (selectedMatch.Patch.Source !=
            PatSrc.ManagedArtemis)
        {
            throw new InvalidOperationException(
                "Prepared NCL application accepts only ManagedArtemis definitions.");
        }

        CheckSrc([selectedMatch]);
        VerifyRuntimeIdentityCollisions(
            install,
            [selectedMatch],
            allowSelectedNewer: true);

        if (preparation.Extra.Count != 0)
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "Generated NCL preparation cannot mutate shared managed cheat-library files.");
        }

        var serial =
            selectedMatch.TgtSer ??
            selectedMatch.MatSer ??
            throw new InvalidOperationException(
                "The generated NCL match has no exact title ID.");

        var expectedManagedPath =
            BundledCheatPatchService.GetPath(
                install.PatPath,
                serial);

        if (!Path.GetFullPath(
                preparation.TgtPath).Equals(
                    Path.GetFullPath(
                        expectedManagedPath),
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "The prepared NCL managed patch target does not match its exact title-specific RPCS3 path.");
        }

        var preparedFiles =
            preparation.PreparedFile is null
                ? Array.Empty<PatchPreparedFile>()
                : [preparation.PreparedFile];

        foreach (var file in preparedFiles)
        {
            if (file.Kind != PrepKind.PatCat ||
                file.CatSrc != PatSrc.ManagedArtemis ||
                !Path.GetFullPath(
                    file.TgtPath).Equals(
                        Path.GetFullPath(
                            expectedManagedPath),
                        StringComparison.OrdinalIgnoreCase))
            {
                throw new Rpcs3MutationBlockedException(
                    MutBlock.Tgt,
                    "Generated NCL preparation attempted an unexpected RPCS3 mutation target.");
            }
        }

        if (!plan.HasChg)
        {
            throw new InvalidOperationException(
                "The generated NCL patch activation plan is empty.");
        }

        var backupRoot =
            Rpcs3AppPaths.BkpRoot(install);

        mutationGuard.CheckCan(
            install,
            [
                plan.PatCfg,
                .. preparedFiles.Select(
                    file => file.TgtPath)
            ],
            [backupRoot]);

        var result = applyService.ApplyTxn(
            plan,
            backupRoot,
            preparedFiles,
            VldFunc(install));

        return result with
        {
            PrepMsgs =
                string.IsNullOrWhiteSpace(
                    preparation.Message)
                    ? []
                    : [preparation.Message]
        };
    }
    public PatRes Apply(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ApplyGuarded(plan, install);
    }

    private PatRes ApplyGuarded(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        Check(plan, install);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatPhase.Stg,
            backupRoot,
            [plan.PatCfg]);

        return applyService.Apply(
            plan,
            backupRoot,
            validator);
    }

    // Disable and RemoveArtemis only remove R3Config-enabled or R3Config-owned
    // state. They are recovery operations and are intentionally not gated.
    public PatRes Disable(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? selectedMatches = null)
    {
        Check(plan, install);
        if (selectedMatches is not null)
        {
            VerifyRuntimeIdentityCollisions(install, selectedMatches, allowSelectedNewer: false);
        }
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatPhase.Stg,
            backupRoot,
            [plan.PatCfg]);

        return applyService.Disable(
            plan,
            backupRoot,
            validator);
    }

    public PatRes RemoveArtemis(
        Mdl.PatchApplyPlan disablePlan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        Check(disablePlan, install);
        if (selectedMatches.Any(match =>
                match.Patch.Source is not PatSrc.Artemis and not PatSrc.ManagedArtemis))
        {
            throw new InvalidOperationException("Only managed Artemis definitions can be removed here.");
        }

        VerifyRuntimeIdentityCollisions(install, selectedMatches, allowSelectedNewer: false);

        var prepared = bundledCheats.PrepRemove(install.PatPath, selectedMatches);
        if (!prepared.IsOk)
        {
            throw new InvalidOperationException(prepared.Error ?? prepared.Message);
        }
        var files = prepared.PreparedFile is null
            ? Array.Empty<PatchPreparedFile>()
            : [prepared.PreparedFile];
        if (!disablePlan.HasChg && files.Length == 0)
        {
            throw new InvalidOperationException("The selected Artemis definition is already absent and disabled.");
        }

        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var targets = files.Select(file => file.TgtPath)
            .Concat(disablePlan.HasChg ? [disablePlan.PatCfg] : [])
            .ToArray();
        mutationGuard.CheckCan(install, targets, [backupRoot]);
        var result = applyService.RemoveTxn(
            disablePlan,
            backupRoot,
            files,
            VldFunc(install));
        return result with { PrepMsgs = [prepared.Message] };
    }

    private void PrepCan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        List<PatchPreparedFile> preparedFiles,
        ICollection<string> messages)
    {
        var matches = selectedMatches
            .Where(match =>
                match.Patch.Source ==
                PatSrc.Can)
            .ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        var result = bundledCanary.Prep(
            install.PatPath,
            AppContext.BaseDirectory,
            matches);

        AddPrep(
            result.IsOk,
            result.PreparedFile,
            result.Message,
            result.Error,
            preparedFiles,
            messages);
    }

    private void PrepCht(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        List<PatchPreparedFile> preparedFiles,
        ICollection<string> messages)
    {
        var matches = selectedMatches
            .Where(match =>
                match.Patch.Source ==
                PatSrc.Artemis)
            .ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        var result = bundledCheats.Prep(
            install.PatPath,
            AppContext.BaseDirectory,
            matches);

        AddPrep(
            result.IsOk,
            result.PreparedFile,
            result.Message,
            result.Error,
            preparedFiles,
            messages);
        foreach (var file in result.Extra)
        {
            preparedFiles.Add(file);
        }
    }

    private static void AddPrep(
        bool succeeded,
        PatchPreparedFile? preparedFile,
        string message,
        string? error,
        List<PatchPreparedFile> preparedFiles,
        ICollection<string> messages)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException(
                error ?? message);
        }

        if (preparedFile is not null)
        {
            preparedFiles.Add(preparedFile);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            messages.Add(message);
        }
    }

    private static void VerifyRuntimeIdentityCollisions(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> selectedMatches,
        bool allowSelectedNewer)
    {
        var reader = new ReadSvc();
        var resident = new List<Rpcs3PatchEntry>();
        var officialPath = Path.Combine(install.PatPath, "patch.yml");
        var importedPath = Rpcs3AppPaths.ImpPat(install);

        ReadResident(officialPath, PatSrc.Official);
        ReadResident(importedPath, PatSrc.Imported);

        if (Directory.Exists(install.PatPath))
        {
            foreach (var path in Directory.EnumerateFiles(
                         install.PatPath,
                         "*_patch.yml",
                         SearchOption.TopDirectoryOnly))
            {
                if (path.Equals(importedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var source = BundledCheatPatchService.IsMan(path)
                    ? PatSrc.ManagedArtemis
                    : PatSrc.TitlePat;
                ReadResident(path, source);
            }
        }

        foreach (var selected in selectedMatches)
        {
            var selectedChannel = Channel(selected.Patch);
            var collision = resident.FirstOrDefault(entry =>
                entry.RunKey.Equals(selected.EffectiveRunKey, StringComparison.OrdinalIgnoreCase) &&
                entry.Title.Equals(selected.Patch.Title, StringComparison.OrdinalIgnoreCase) &&
                Channel(entry) != selectedChannel &&
                !string.Equals(entry.DefPrint, selected.Patch.DefPrint, StringComparison.OrdinalIgnoreCase) &&
                (!allowSelectedNewer || CompareVersion(selected.Patch.PatVer, entry.PatVer) <= 0));

            if (collision is not null)
            {
                throw new InvalidOperationException(
                    $"RPCS3 runtime identity collision for '{selected.Patch.Title}' ({selected.EffectiveRunKey}) between {selected.Patch.Source} and {collision.Source}. The definitions differ, so the requested source cannot be enabled or disabled safely.");
            }
        }

        void ReadResident(string path, PatSrc source)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCatBy,
                Path.GetFileName(path));
            var read = reader.ReadText(text, source, path);
            if (!read.IsOk)
            {
                throw new InvalidDataException(
                    $"Resident RPCS3 patch source '{Path.GetFileName(path)}' is invalid: {read.Error}");
            }

            resident.AddRange(read.Catalog.Entries);
        }
    }

    private static int CompareVersion(string? left, string? right)
    {
        if (Version.TryParse(left?.Trim(), out var leftVersion) &&
            Version.TryParse(right?.Trim(), out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return 0;
    }

    private static int Channel(Rpcs3PatchEntry entry)
    {
        if (entry.Source == PatSrc.Official)
        {
            return 0;
        }

        if (entry.Class == PatClass.Canary)
        {
            return 1;
        }

        if (entry.Source is PatSrc.Artemis or PatSrc.ManagedArtemis)
        {
            return 2;
        }

        return 3;
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
                    $"Patch source changed after preview because '{path}' is no longer available.");
            }

            var source = group.First().Patch.Source;
            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCatBy,
                Path.GetFileName(path));
            var read = reader.ReadText(
                text,
                source,
                path);

            if (!read.IsOk)
            {
                throw new InvalidDataException(
                    "Patch source changed after preview and is no longer valid: " +
                    (read.Error ?? path));
            }

            foreach (var match in group)
            {
                var current = read.Catalog.Entries.FirstOrDefault(entry =>
                    entry.RunKey.Equals(
                        match.Patch.RunKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    entry.Title.Equals(
                        match.Patch.Title,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        entry.PatVer,
                        match.Patch.PatVer,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        entry.DefPrint,
                        match.Patch.DefPrint,
                        StringComparison.OrdinalIgnoreCase));

                if (current is null)
                {
                    throw new IOException(
                        $"Patch definition '{match.Patch.Title}' changed after preview. Reload patches before applying it.");
                }
            }
        }
    }

    private static void CheckSrc(
        IReadOnlyList<PatMatch> selectedMatches)
    {
        var sections = selectedMatches
            .Select(match => match.Patch.Source == PatSrc.Official
                ? 0
                : match.Patch.Class == PatClass.Canary
                    ? 1
                    : match.Patch.Source is PatSrc.Artemis or
                        PatSrc.ManagedArtemis
                        ? 2
                        : 3)
            .Distinct()
            .Take(2)
            .Count();

        if (sections > 1)
        {
            throw new InvalidOperationException(
                "Official, Canary, Artemis, and local patch sources must be applied separately.");
        }
    }

    private static void VerifyCanaryArtemisEnable(
        IReadOnlyList<PatMatch> selectedMatches,
        IReadOnlyList<PatMatch>? allMatches)
    {
        if (allMatches is null)
        {
            if (selectedMatches.Any(match =>
                    match.Patch.Source == PatSrc.Artemis))
            {
                throw new InvalidOperationException(
                    "Artemis enable cannot validate Canary equivalents because the current match set is unavailable. Reload patches and try again.");
            }

            return;
        }

        foreach (var selected in selectedMatches)
        {
            if (CanaryArtemisPolicy.HasApplicableEquivalent(
                    selected,
                    allMatches))
            {
                throw new InvalidOperationException(
                    $"Artemis patch '{selected.Patch.Title}' has an applicable Canary equivalent and cannot be enabled through Artemis. Use the Canary version.");
            }
        }
    }

    private static void Check(
        Mdl.PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        if (!Path.GetFullPath(plan.PatCfg).Equals(
                Path.GetFullPath(install.PatCfg),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "The patch plan target does not match its RPCS3 installation.");
        }
    }

    private PathVld VldFunc(
        Rpcs3InstallDescriptor install)
    {
        return (_, backupRoot, targets) =>
            mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]);
    }
}
