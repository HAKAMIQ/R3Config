using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class CfgCase(
    PlanSvc planService,
    CfgApply applyService,
    IMutGd mutationGuard)
{
    public CfgPlan Build(
        R3Inst install,
        R3Game game,
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
        R3Inst install)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
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
        R3Inst install)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
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
        R3Inst install)
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
        R3Inst install)
    {
        return (_, backupRoot, targets) =>
            mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]);
    }
}
