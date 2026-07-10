using System;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class ConfigApplyService
{
    private readonly BackupService _backupService;
    private readonly Action<string, IReadOnlyList<ConfigChange>> _applyChanges;

    public ConfigApplyService(
        BackupService? backupService = null,
        Action<string, IReadOnlyList<ConfigChange>>? applyChanges = null)
    {
        _backupService = backupService ?? new BackupService();
        _applyChanges = applyChanges ?? Rpcs3ConfigYaml.ApplyChanges;
    }

    public ConfigApplyResult Apply(ConfigPlan plan, IReadOnlyList<ConfigChange> changes, string backupRootPath)
    {
        if (changes.Count == 0)
        {
            throw new InvalidOperationException("Config apply plan is empty.");
        }

        var backup = _backupService.CreateBackup(backupRootPath, [plan.TargetConfigPath]);
        try
        {
            _applyChanges(plan.TargetConfigPath, changes);
            return new ConfigApplyResult(backup, changes);
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
                    "Config apply failed and automatic restore failed.",
                    new AggregateException(writeException, restoreException));
            }

            throw;
        }
    }


    public ConfigApplyResult Remove(ConfigPlan plan, IReadOnlyList<ConfigChange> changes, string backupRootPath)
    {
        if (changes.Count == 0)
        {
            throw new InvalidOperationException("Config remove plan is empty.");
        }

        var backup = _backupService.CreateBackup(backupRootPath, [plan.TargetConfigPath]);
        try
        {
            Rpcs3ConfigYaml.RemoveChanges(plan.TargetConfigPath, changes);
            return new ConfigApplyResult(backup, changes);
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
                    "Config remove failed and automatic restore failed.",
                    new AggregateException(writeException, restoreException));
            }

            throw;
        }
    }
}
