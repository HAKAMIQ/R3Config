using System;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchApplyService
{
    private readonly CfgWrite _writer;
    private readonly PatchFileTransactionService _Txn;

    public PatchApplyService(
        BackupService? backupService = null,
        CfgWrite? writer = null,
        PatchFileTransactionService? transaction = null)
    {
        var backup = backupService ?? new BackupService();
        _writer = writer ?? new CfgWrite();
        _Txn = transaction ?? new PatchFileTransactionService(backup);
    }

    public PatRes Apply(
        Mdl.PatchApplyPlan plan,
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
        Mdl.PatchApplyPlan plan,
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        PathVld? validatePaths = null)
    {
        if (!plan.HasChg)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                plan.PatCfg,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        var patchConfigText = _writer.Build(
            plan.PatCfg,
            plan.Changes);

        CfgWrite.VldText(patchConfigText);

        var transactionFiles = preparedFiles
            .Append(new PatchPreparedFile(
                plan.PatCfg,
                patchConfigText,
                PrepKind.PatCfg,
                ExpectedExists: patchConfigState.Exists,
                ExpectedSha256: patchConfigState.Sha256))
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
        Mdl.PatchApplyPlan plan,
        string backupRootPath,
        PathVld? validatePaths = null)
    {
        return Apply(
            plan,
            backupRootPath,
            enabled: false,
            validatePaths);
    }

    public PatRes RemoveTxn(
        Mdl.PatchApplyPlan plan,
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        PathVld? validatePaths = null)
    {
        if (!plan.HasChg && preparedFiles.Count == 0)
        {
            throw new InvalidOperationException("Patch removal plan is empty.");
        }

        var transactionFiles = preparedFiles.ToList();
        if (plan.HasChg)
        {
            var state = PatchFileTransactionService.CaptureExpectedState(
                plan.PatCfg,
                R3Limits.PatCfgBy,
                "patch_config.yml");
            var text = _writer.Build(plan.PatCfg, plan.Changes, enabled: false);
            CfgWrite.VldText(text);
            transactionFiles.Add(new PatchPreparedFile(
                plan.PatCfg,
                text,
                PrepKind.PatCfg,
                ExpectedExists: state.Exists,
                ExpectedSha256: state.Sha256));
        }

        var backup = _Txn.Commit(backupRootPath, transactionFiles, validatePaths);
        return new PatRes(backup, plan.Changes);
    }

    private PatRes Apply(
        Mdl.PatchApplyPlan plan,
        string backupRootPath,
        bool enabled,
        PathVld? validatePaths)
    {
        if (!plan.HasChg)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                plan.PatCfg,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        var patchConfigText = _writer.Build(
            plan.PatCfg,
            plan.Changes,
            enabled);
        CfgWrite.VldText(patchConfigText);

        var backup = _Txn.Commit(
            backupRootPath,
            [
                new PatchPreparedFile(
                    plan.PatCfg,
                    patchConfigText,
                    PrepKind.PatCfg,
                    ExpectedExists: patchConfigState.Exists,
                    ExpectedSha256: patchConfigState.Sha256)
            ],
            validatePaths);

        return new PatRes(
            backup,
            plan.Changes);
    }
}
