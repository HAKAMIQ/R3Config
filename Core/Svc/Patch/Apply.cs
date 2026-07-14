using System;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchApplyService
{
    private readonly BackupService _backupService;
    private readonly PatchConfigWriter _writer;
    private readonly PatchFileTransactionService _transaction;
    private readonly Action<string, IReadOnlyList<PatchApplyChange>> _writeChanges;

    public PatchApplyService(
        BackupService? backupService = null,
        PatchConfigWriter? writer = null,
        Action<string, IReadOnlyList<PatchApplyChange>>? writeChanges = null,
        PatchFileTransactionService? transaction = null)
    {
        _backupService = backupService ?? new BackupService();
        _writer = writer ?? new PatchConfigWriter();
        _transaction = transaction ?? new PatchFileTransactionService(_backupService);
        _writeChanges = writeChanges ?? ((path, changes) => _writer.Write(path, changes));
    }

    public PatchApplyResult Apply(
        PatchApplyPlan plan,
        string backupRootPath)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var backup = _backupService.CreateBackup(
            backupRootPath,
            [plan.PatchConfigPath]);

        try
        {
            _writeChanges(
                plan.PatchConfigPath,
                plan.Changes);

            return new PatchApplyResult(
                backup,
                plan.Changes);
        }
        catch (Exception writeException)
        {
            try
            {
                _backupService.Restore(backup);
            }
            catch (Exception restoreException)
            {
                throw new InvalidOperationException(
                    "Patch apply failed and automatic restore failed.",
                    new AggregateException(
                        writeException,
                        restoreException));
            }

            throw;
        }
    }

    public PatchApplyResult ApplyTransaction(
        PatchApplyPlan plan,
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        Action<IReadOnlyList<string>>? validateTargetsBeforeCommit = null)
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
            validateTargetsBeforeCommit);

        return new PatchApplyResult(
            backup,
            plan.Changes);
    }

    public PatchApplyResult Disable(
        PatchApplyPlan plan,
        string backupRootPath)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException(
                "Patch apply plan is empty.");
        }

        var backup = _backupService.CreateBackup(
            backupRootPath,
            [plan.PatchConfigPath]);

        try
        {
            _writer.Write(
                plan.PatchConfigPath,
                plan.Changes,
                enabled: false);

            return new PatchApplyResult(
                backup,
                plan.Changes);
        }
        catch (Exception writeException)
        {
            try
            {
                _backupService.Restore(backup);
            }
            catch (Exception restoreException)
            {
                throw new InvalidOperationException(
                    "Patch disable failed and automatic restore failed.",
                    new AggregateException(
                        writeException,
                        restoreException));
            }

            throw;
        }
    }
}
