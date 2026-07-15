using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class ConfigUseCase(
    PlanService planService,
    ConfigApplyService applyService,
    IRpcs3MutationGuard mutationGuard)
{
    public CfgPlan Build(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        HwInfo? hardware)
    {
        return planService.BuildCfg(
            install,
            game,
            recommendation,
            hardware);
    }

    public CfgRes Apply(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        Rpcs3InstallDescriptor install)
    {
        Check(plan, install);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatPhase.Stg,
            backupRoot,
            [plan.CfgPath]);

        return applyService.Apply(
            plan,
            changes,
            backupRoot,
            validator);
    }

    public CfgRes Remove(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        Rpcs3InstallDescriptor install)
    {
        Check(plan, install);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var validator = VldFunc(install);
        validator(
            PatPhase.Stg,
            backupRoot,
            [plan.CfgPath]);

        return applyService.Remove(
            plan,
            changes,
            backupRoot,
            validator);
    }

    private static void Check(
        CfgPlan plan,
        Rpcs3InstallDescriptor install)
    {
        var expected = Path.Combine(
            install.CustPath,
            $"config_{plan.GameSer}.yml");

        if (!Path.GetFullPath(plan.CfgPath).Equals(
                Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new MutBlkEx(
                MutBlock.Tgt,
                "The config plan target does not match its RPCS3 installation and game serial.");
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
