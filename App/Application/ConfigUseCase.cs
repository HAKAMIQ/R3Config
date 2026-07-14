using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public sealed class ConfigUseCase(
    PlanService planService,
    ConfigApplyService applyService,
    IRpcs3MutationGuard mutationGuard)
{
    public ConfigPlan BuildPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiGameRecommendation? recommendation,
        HwInfo? hardware)
    {
        return planService.CreateConfigPlan(
            install,
            game,
            recommendation,
            hardware);
    }

    public ConfigApplyResult Apply(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        EnsurePlanBelongsToInstall(plan, install);
        mutationGuard.EnsureCanMutate(
            install,
            [plan.TargetConfigPath]);

        return applyService.Apply(
            plan,
            changes,
            Rpcs3AppPaths.BackupRoot(install));
    }

    public ConfigApplyResult Remove(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        EnsurePlanBelongsToInstall(plan, install);
        mutationGuard.EnsureCanMutate(
            install,
            [plan.TargetConfigPath]);

        return applyService.Remove(
            plan,
            changes,
            Rpcs3AppPaths.BackupRoot(install));
    }

    private static void EnsurePlanBelongsToInstall(
        ConfigPlan plan,
        Rpcs3InstallDescriptor install)
    {
        var expected = Path.Combine(
            install.CustomConfigsPath,
            $"config_{plan.GameSerial}.yml");

        if (!Path.GetFullPath(plan.TargetConfigPath).Equals(
                Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                Rpcs3MutationBlockReason.UnsafeTarget,
                "The config plan target does not match its RPCS3 installation and game serial.");
        }
    }
}
