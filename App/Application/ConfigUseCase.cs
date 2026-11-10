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
        HwInfo? hardware,
        Rpcs3RuntimeBuild? build = null,
        RecSyncApiGame? databaseConfig = null,
        bool databaseLoaded = false)
    {
        return planService.BuildCfg(
            install,
            game,
            recommendation,
            hardware,
            build,
            databaseConfig: databaseConfig,
            databaseLoaded: databaseLoaded);
    }

    public CfgRes Apply(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ApplyGuarded(plan, changes, install);
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

    private CfgRes ApplyGuarded(
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
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "The config plan target does not match its RPCS3 installation and game serial.");
        }

        // The plan resolves inherited values against this installation's global
        // config, and Apply re-resolves them the same way, so the baseline is
        // part of the plan's target ownership rather than an incidental path.
        var expectedGlobal = Path.Combine(
            install.CfgPath,
            "config.yml");

        if (!Path.GetFullPath(plan.GlobalCfgPath).Equals(
                Path.GetFullPath(expectedGlobal),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "The config plan global baseline does not match its RPCS3 installation.");
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
