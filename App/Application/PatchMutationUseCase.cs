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
        IReadOnlyList<PatMatch> selectedMatches)
    {
        Check(plan, install);
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

    public PatRes Apply(
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
                entry.RunKey.Equals(selected.Patch.RunKey, StringComparison.OrdinalIgnoreCase) &&
                entry.Title.Equals(selected.Patch.Title, StringComparison.OrdinalIgnoreCase) &&
                Channel(entry) != selectedChannel &&
                !string.Equals(entry.DefPrint, selected.Patch.DefPrint, StringComparison.OrdinalIgnoreCase) &&
                (!allowSelectedNewer || CompareVersion(selected.Patch.PatVer, entry.PatVer) <= 0));

            if (collision is not null)
            {
                throw new InvalidOperationException(
                    $"RPCS3 runtime identity collision for '{selected.Patch.Title}' ({selected.Patch.RunKey}) between {selected.Patch.Source} and {collision.Source}. The definitions differ, so the requested source cannot be enabled or disabled safely.");
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
