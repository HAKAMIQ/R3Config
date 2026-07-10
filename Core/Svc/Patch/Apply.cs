using System;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchApplyService
{
    private readonly BackupService _backupService;
    private readonly PatchConfigWriter _writer;
    private readonly Action<string, IReadOnlyList<PatchApplyChange>> _writeChanges;

    public PatchApplyService(
        BackupService? backupService = null,
        PatchConfigWriter? writer = null,
        Action<string, IReadOnlyList<PatchApplyChange>>? writeChanges = null)
    {
        _backupService = backupService ?? new BackupService();
        _writer = writer ?? new PatchConfigWriter();
        _writeChanges = writeChanges ?? ((path, changes) => _writer.Write(path, changes));
    }

    public PatchApplyResult Apply(PatchApplyPlan plan, string backupRootPath)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException("Patch apply plan is empty.");
        }

        var backup = _backupService.CreateBackup(backupRootPath, [plan.PatchConfigPath]);
        try
        {
            _writeChanges(plan.PatchConfigPath, plan.Changes);
            return new PatchApplyResult(backup, plan.Changes);
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
                    new AggregateException(writeException, restoreException));
            }

            throw;
        }
    }

    public PatchApplyResult Disable(PatchApplyPlan plan, string backupRootPath)
    {
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException("Patch apply plan is empty.");
        }

        var backup = _backupService.CreateBackup(backupRootPath, [plan.PatchConfigPath]);
        try
        {
            _writer.Write(plan.PatchConfigPath, plan.Changes, enabled: false);
            return new PatchApplyResult(backup, plan.Changes);
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
                    new AggregateException(writeException, restoreException));
            }

            throw;
        }
    }
}
