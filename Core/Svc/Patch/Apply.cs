using System;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchApplyService
{
    private readonly PatchConfigWriter _writer;
    private readonly PatchFileTransactionService _transaction;

    public PatchApplyService(
        BackupService? backupService = null,
        PatchConfigWriter? writer = null,
        PatchFileTransactionService? transaction = null)
    {
        var backup = backupService ?? new BackupService();
        _writer = writer ?? new PatchConfigWriter();
        _transaction = transaction ?? new PatchFileTransactionService(backup);
    }

    public PatchApplyResult Apply(
        PatchApplyPlan plan,
        string backupRootPath,
        PatchPathValidator? validatePaths = null)
    {
        return ApplyState(
            plan,
            backupRootPath,
            enabled: true,
            validatePaths);
    }

    public PatchApplyResult ApplyTransaction(
        PatchApplyPlan plan,
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        PatchPathValidator? validatePaths = null)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigText = _writer.BuildText(
            plan.PatchConfigPath,
            plan.Changes);

        PatchConfigWriter.ValidateText(patchConfigText);

        var transactionFiles = preparedFiles
            .Append(new PatchPreparedFile(
                plan.PatchConfigPath,
                patchConfigText,
                PatchPreparedFileKind.PatchConfig))
            .ToArray();

        var backup = _transaction.Commit(
            backupRootPath,
            transactionFiles,
            validatePaths);

        return new PatchApplyResult(
            backup,
            plan.Changes);
    }

    public PatchApplyResult Disable(
        PatchApplyPlan plan,
        string backupRootPath,
        PatchPathValidator? validatePaths = null)
    {
        return ApplyState(
            plan,
            backupRootPath,
            enabled: false,
            validatePaths);
    }

    private PatchApplyResult ApplyState(
        PatchApplyPlan plan,
        string backupRootPath,
        bool enabled,
        PatchPathValidator? validatePaths)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var patchConfigText = _writer.BuildText(
            plan.PatchConfigPath,
            plan.Changes,
            enabled);
        PatchConfigWriter.ValidateText(patchConfigText);

        var backup = _transaction.Commit(
            backupRootPath,
            [
                new PatchPreparedFile(
                    plan.PatchConfigPath,
                    patchConfigText,
                    PatchPreparedFileKind.PatchConfig)
            ],
            validatePaths);

        return new PatchApplyResult(
            backup,
            plan.Changes);
    }
}
