using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public sealed class PatchMutationUseCase(
    PatchApplyPlanner planner,
    PatchApplyService applyService,
    BundledCanaryPatchService bundledCanary,
    BundledCheatPatchService bundledCheats,
    IRpcs3MutationGuard mutationGuard)
{
    public PatchApplyPlan BuildEnablePlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        bool expertMode)
    {
        return planner.CreatePlan(
            install,
            matches,
            expertMode);
    }

    public PatchApplyPlan BuildDisablePlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches)
    {
        return planner.CreateDisablePlan(
            install,
            matches);
    }

    public PatchApplyChange? CreateChange(
        Rpcs3PatchMatchResult match)
    {
        return planner.CreateChangeForMatch(match);
    }

    public PatchApplyResult ApplyTransaction(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        EnsurePlanBelongsToInstall(plan, install);
        mutationGuard.EnsureCanMutate(
            install,
            [plan.PatchConfigPath]);

        var preparedFiles = new List<PatchPreparedFile>();
        var messages = new List<string>();

        PrepareBundledCanary(
            install,
            selectedMatches,
            preparedFiles,
            messages);
        PrepareBundledCheats(
            install,
            selectedMatches,
            preparedFiles,
            messages);

        mutationGuard.EnsureCanMutate(
            install,
            [
                plan.PatchConfigPath,
                .. preparedFiles.Select(file => file.TargetPath)
            ]);

        var result = applyService.ApplyTransaction(
            plan,
            Rpcs3AppPaths.BackupRoot(install),
            preparedFiles,
            targets => mutationGuard.EnsureCanMutate(
                install,
                targets));

        return result with
        {
            PreparationMessages = messages
        };
    }

    public PatchApplyResult Apply(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        EnsurePlanBelongsToInstall(plan, install);
        mutationGuard.EnsureCanMutate(
            install,
            [plan.PatchConfigPath]);

        return applyService.Apply(
            plan,
            Rpcs3AppPaths.BackupRoot(install));
    }

    public PatchApplyResult Disable(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        EnsurePlanBelongsToInstall(plan, install);
        mutationGuard.EnsureCanMutate(
            install,
            [plan.PatchConfigPath]);

        return applyService.Disable(
            plan,
            Rpcs3AppPaths.BackupRoot(install));
    }

    public PatchLibraryWriteResult PrepareCheats(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        var targets = BundledCheatPatchService.GetManagedPaths(
            install.PatchesPath,
            selectedMatches);

        mutationGuard.EnsureCanMutate(
            install,
            targets);

        return bundledCheats.PrepareManagedPatch(
            install.PatchesPath,
            AppContext.BaseDirectory,
            selectedMatches);
    }

    private void PrepareBundledCanary(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches,
        ICollection<PatchPreparedFile> preparedFiles,
        ICollection<string> messages)
    {
        var matches = selectedMatches
            .Where(match =>
                match.Patch.Source ==
                PatchSourceOrigin.BundledCanary)
            .ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        var result = bundledCanary.PrepareImportedPatchFile(
            install.PatchesPath,
            AppContext.BaseDirectory,
            matches);

        AddPreparedResult(
            result.Succeeded,
            result.PreparedFile,
            result.Message,
            result.Error,
            preparedFiles,
            messages);
    }

    private void PrepareBundledCheats(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches,
        ICollection<PatchPreparedFile> preparedFiles,
        ICollection<string> messages)
    {
        var matches = selectedMatches
            .Where(match =>
                match.Patch.Source ==
                PatchSourceOrigin.BundledCheats)
            .ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        var result = bundledCheats.PrepareManagedPatchFile(
            install.PatchesPath,
            AppContext.BaseDirectory,
            matches);

        AddPreparedResult(
            result.Succeeded,
            result.PreparedFile,
            result.Message,
            result.Error,
            preparedFiles,
            messages);
    }

    private static void AddPreparedResult(
        bool succeeded,
        PatchPreparedFile? preparedFile,
        string message,
        string? error,
        ICollection<PatchPreparedFile> preparedFiles,
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

    private static void EnsurePlanBelongsToInstall(
        PatchApplyPlan plan,
        Rpcs3InstallDescriptor install)
    {
        if (!Path.GetFullPath(plan.PatchConfigPath).Equals(
                Path.GetFullPath(install.PatchConfigPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                Rpcs3MutationBlockReason.UnsafeTarget,
                "The patch plan target does not match its RPCS3 installation.");
        }
    }
}
