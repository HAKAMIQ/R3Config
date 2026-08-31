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

        var (patchConfigState, patchConfigText) =
            BuildReviewedPatchConfig(
                plan,
                enabled: true);

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
            var (state, text) = BuildReviewedPatchConfig(
                plan,
                enabled: false);
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

    private (PatchTargetState State, string Text) BuildReviewedPatchConfig(
        Mdl.PatchApplyPlan plan,
        bool enabled)
    {
        var state = ExpectedState(plan);

        var text = _writer.Build(
            plan.PatCfg,
            plan.Changes,
            enabled);
        CfgWrite.VldText(text);

        PatchFileTransactionService.ValidateExpectedState(
            plan.PatCfg,
            state);

        return (state, text);
    }

    private static PatchTargetState ExpectedState(Mdl.PatchApplyPlan plan)
    {
        if (plan.ExpectedPatCfgExists is bool expectedExists)
        {
            return new PatchTargetState(
                expectedExists,
                plan.ExpectedPatCfgSha256,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        }

        return PatchFileTransactionService.CaptureExpectedState(
            plan.PatCfg,
            R3Limits.PatCfgBy,
            "patch_config.yml");
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

        var (patchConfigState, patchConfigText) =
            BuildReviewedPatchConfig(
                plan,
                enabled);

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
