using System;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class ConfigApplyService
{
    private readonly BackupService _BkpSvc;
    private readonly Action<
        string,
        IReadOnlyList<CfgChg>,
        Action?> _Changes;

    public ConfigApplyService(
        BackupService? backupService = null,
        Action<string, IReadOnlyList<CfgChg>, Action?>? applyChanges = null)
    {
        _BkpSvc = backupService ?? new BackupService();
        _Changes = applyChanges ?? R3Yaml.Apply;
    }

    public CfgRes Apply(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        string backupRootPath,
        PathVld? validatePaths = null)
    {
        if (changes.Count == 0)
        {
            throw new InvalidOperationException("Config apply plan is empty.");
        }

        validatePaths?.Invoke(
            PatPhase.Bkp,
            backupRootPath,
            [plan.CfgPath]);
        var backup = _BkpSvc.BuildBkp(
            backupRootPath,
            [plan.CfgPath]);
        try
        {
            _Changes(
                plan.CfgPath,
                changes,
                () => validatePaths?.Invoke(
                    PatPhase.Commit,
                    backupRootPath,
                    [plan.CfgPath]));
            return new CfgRes(backup, changes);
        }
        catch (Exception writeException)
        {
            try
            {
                _BkpSvc.Restore(backup);
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


    public CfgRes Remove(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        string backupRootPath,
        PathVld? validatePaths = null)
    {
        if (changes.Count == 0)
        {
            throw new InvalidOperationException("Config remove plan is empty.");
        }

        validatePaths?.Invoke(
            PatPhase.Bkp,
            backupRootPath,
            [plan.CfgPath]);
        var backup = _BkpSvc.BuildBkp(
            backupRootPath,
            [plan.CfgPath]);
        try
        {
            R3Yaml.Del(
                plan.CfgPath,
                changes,
                () => validatePaths?.Invoke(
                    PatPhase.Commit,
                    backupRootPath,
                    [plan.CfgPath]));
            return new CfgRes(backup, changes);
        }
        catch (Exception writeException)
        {
            try
            {
                _BkpSvc.Restore(backup);
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
