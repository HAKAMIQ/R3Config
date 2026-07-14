using System;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatApply
{
    private readonly CfgWrite _writer;
    private readonly PatTxn _Txn;

    public PatApply(
        BkpSvc? backupService = null,
        CfgWrite? writer = null,
        PatTxn? transaction = null)
    {
        var backup = backupService ?? new BkpSvc();
        _writer = writer ?? new CfgWrite();
        _Txn = transaction ?? new PatTxn(backup);
    }

    public PatRes Apply(
        Mdl.PatPlan plan,
        string backupRootPath,
        PathVld? validatePaths = null)
    {
        return Apply(
            plan,
            backupRootPath,
            enabled: true,
            validatePaths);
    }

    public PatRes ApplyTxn(
        Mdl.PatPlan plan,
        string backupRootPath,
        IReadOnlyList<PrepFile> preparedFiles,
        PathVld? validatePaths = null)
    {
        if (!plan.HasChg)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigText = _writer.Build(
            plan.PatCfg,
            plan.Changes);

        CfgWrite.VldText(patchConfigText);

        var transactionFiles = preparedFiles
            .Append(new PrepFile(
                plan.PatCfg,
                patchConfigText,
                PrepKind.PatCfg))
            .ToArray();

        var backup = _Txn.Commit(
            backupRootPath,
            transactionFiles,
            validatePaths);

        return new PatRes(
            backup,
            plan.Changes);
    }

    public PatRes Disable(
        Mdl.PatPlan plan,
        string backupRootPath,
        PathVld? validatePaths = null)
    {
        return Apply(
            plan,
            backupRootPath,
            enabled: false,
            validatePaths);
    }

    private PatRes Apply(
        Mdl.PatPlan plan,
        string backupRootPath,
        bool enabled,
        PathVld? validatePaths)
    {
        if (!plan.HasChg)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigText = _writer.Build(
            plan.PatCfg,
            plan.Changes,
            enabled);
        CfgWrite.VldText(patchConfigText);

        var backup = _Txn.Commit(
            backupRootPath,
            [
                new PrepFile(
                    plan.PatCfg,
                    patchConfigText,
                    PrepKind.PatCfg)
            ],
            validatePaths);

        return new PatRes(
            backup,
            plan.Changes);
    }
}
