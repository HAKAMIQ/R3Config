using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class CfgCase(
    PlanService planService,
    ConfigApplyService applyService,
    IRpcs3MutationGuard mutationGuard)
{
    public ConfigPlan Build(
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
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatchTransactionPhase.BeforeStage,
            backupRoot,
            [plan.TargetConfigPath]);

        return applyService.Apply(
            plan,
            changes,
            backupRoot,
            validator);
    }

    public ConfigApplyResult Remove(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatchTransactionPhase.BeforeStage,
            backupRoot,
            [plan.TargetConfigPath]);

        return applyService.Remove(
            plan,
            changes,
            backupRoot,
            validator);
    }

    private static void Check(
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

    private PatchPathValidator VldFunc(
        Rpcs3InstallDescriptor install)
    {
        return (_, backupRoot, targets) =>
            mutationGuard.EnsureCanMutate(
                install,
                targets,
                [backupRoot]);
    }
}
