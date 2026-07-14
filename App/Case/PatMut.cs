using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class PatMut(
    Svc.PatPlnr planner,
    PatApply applyService,
    CanBndl bundledCanary,
    ChtBndl bundledCheats,
    IMutGd mutationGuard)
{
    public Mdl.PatPlan BuildOn(
        R3Inst install,
        IReadOnlyList<PatMatch> matches,
        bool expertMode)
    {
        return planner.Build(
            install,
            matches,
            expertMode);
    }

    public Mdl.PatPlan BuildOff(
        R3Inst install,
        IReadOnlyList<PatMatch> matches)
    {
        return planner.Build2(
            install,
            matches);
    }

    public PatChg? BuildChg(
        PatMatch match)
    {
        return planner.Build3(match);
    }

    public PatRes ApplyTxn(
        Mdl.PatPlan plan,
        R3Inst install,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
        var declaredTargets = new List<string>
        {
            plan.PatCfg
        };
        if (selectedMatches.Any(match =>
                match.Patch.Source ==
                PatSrc.Can))
        {
            declaredTargets.Add(
                R3Paths.ImpPat(install));
        }

        declaredTargets.AddRange(
            ChtBndl.GetPaths(
                install.PatPath,
                selectedMatches));
        if (selectedMatches.Any(match =>
                match.Patch.Source ==
                PatSrc.Cht))
        {
            declaredTargets.Add(
                ChtBndl.GetPath(install.PatPath));
            declaredTargets.Add(
                ChtBndl.OldPath(install.PatPath));
        }
        mutationGuard.CheckCan(
            install,
            declaredTargets,
            [backupRoot]);

        var preparedFiles = new List<PrepFile>();
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
        Mdl.PatPlan plan,
        R3Inst install)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
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
        Mdl.PatPlan plan,
        R3Inst install)
    {
        Check(plan, install);
        var backupRoot = R3Paths.BkpRoot(install);
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

    private void PrepCan(
        R3Inst install,
        IReadOnlyList<PatMatch> selectedMatches,
        ICollection<PrepFile> preparedFiles,
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
            result.PrepFile,
            result.Message,
            result.Error,
            preparedFiles,
            messages);
    }

    private void PrepCht(
        R3Inst install,
        IReadOnlyList<PatMatch> selectedMatches,
        ICollection<PrepFile> preparedFiles,
        ICollection<string> messages)
    {
        var matches = selectedMatches
            .Where(match =>
                match.Patch.Source ==
                PatSrc.Cht)
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
            result.PrepFile,
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
        PrepFile? preparedFile,
        string message,
        string? error,
        ICollection<PrepFile> preparedFiles,
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

    private static void Check(
        Mdl.PatPlan plan,
        R3Inst install)
    {
        if (!Path.GetFullPath(plan.PatCfg).Equals(
                Path.GetFullPath(install.PatCfg),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new MutBlkEx(
                MutBlock.Tgt,
                "The patch plan target does not match its RPCS3 installation.");
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
