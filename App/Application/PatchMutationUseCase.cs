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
        bool expertMode)
    {
        return planner.Build(
            install,
            matches,
            expertMode,
            enabledMatches);
    }

    public Mdl.PatchApplyPlan BuildOff(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch> matches,
        IReadOnlyList<PatMatch>? enabledMatches = null)
    {
        return planner.Build2(
            install,
            matches,
            enabledMatches);
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
        Rpcs3InstallDescriptor install)
    {
        Check(plan, install);
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
