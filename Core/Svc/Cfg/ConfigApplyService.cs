using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class ConfigApplyService
{
    private readonly BackupService _BkpSvc;
    private readonly Action<
        string,
        string?,
        Action?> _Changes;

    public ConfigApplyService(
        BackupService? backupService = null,
        Action<string, string?, Action?>? applyChanges = null)
    {
        _BkpSvc = backupService ?? new BackupService();
        _Changes = applyChanges ?? R3Yaml.Commit;
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

        var expectedState =
            PatchFileTransactionService.CaptureExpectedState(
                plan.CfgPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (!R3Yaml.CanRead(plan.CfgPath))
        {
            throw new InvalidDataException(
                "The current RPCS3 custom config is invalid or unreadable.");
        }
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        ValidateReviewedValues(plan, changes);
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        var preparedText = R3Yaml.Build(
            plan.CfgPath,
            changes);
        var preparedState = PreparedState(preparedText);

        validatePaths?.Invoke(
            PatPhase.Bkp,
            backupRootPath,
            [plan.CfgPath]);
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        var backup = _BkpSvc.BuildBkp(
            backupRootPath,
            [plan.CfgPath]);
        var commitAttempted = false;
        try
        {
            _Changes(
                plan.CfgPath,
                preparedText,
                () =>
                {
                    validatePaths?.Invoke(
                        PatPhase.Commit,
                        backupRootPath,
                        [plan.CfgPath]);
                    PatchFileTransactionService.ValidateExpectedState(
                        plan.CfgPath,
                        expectedState);
                    commitAttempted = true;
                });
            return new CfgRes(backup, changes);
        }
        catch (Exception writeException)
        {
            if (!commitAttempted)
            {
                throw;
            }

            try
            {
                RollbackCommitted(
                    backup,
                    plan.CfgPath,
                    expectedState,
                    preparedState);
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

        var expectedState =
            PatchFileTransactionService.CaptureExpectedState(
                plan.CfgPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (!R3Yaml.CanRead(plan.CfgPath))
        {
            throw new InvalidDataException(
                "The current RPCS3 custom config is invalid or unreadable.");
        }
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        ValidateReviewedValues(plan, changes);
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        var preparedText = R3Yaml.BuildDel(
            plan.CfgPath,
            changes);
        var preparedState = PreparedState(preparedText);

        validatePaths?.Invoke(
            PatPhase.Bkp,
            backupRootPath,
            [plan.CfgPath]);
        PatchFileTransactionService.ValidateExpectedState(
            plan.CfgPath,
            expectedState);
        var backup = _BkpSvc.BuildBkp(
            backupRootPath,
            [plan.CfgPath]);
        var commitAttempted = false;
        try
        {
            _Changes(
                plan.CfgPath,
                preparedText,
                () =>
                {
                    validatePaths?.Invoke(
                        PatPhase.Commit,
                        backupRootPath,
                        [plan.CfgPath]);
                    PatchFileTransactionService.ValidateExpectedState(
                        plan.CfgPath,
                        expectedState);
                    commitAttempted = true;
                });
            return new CfgRes(backup, changes);
        }
        catch (Exception writeException)
        {
            if (!commitAttempted)
            {
                throw;
            }

            try
            {
                RollbackCommitted(
                    backup,
                    plan.CfgPath,
                    expectedState,
                    preparedState);
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

    private static void ValidateReviewedValues(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes)
    {
        foreach (var change in changes)
        {
            var reviewed = plan.Items.FirstOrDefault(item =>
                item.Section.Equals(change.Section, StringComparison.Ordinal) &&
                item.Key.Equals(change.Key, StringComparison.Ordinal));

            if (reviewed is null)
            {
                throw new InvalidOperationException(
                    $"Config change '{change.Section}/{change.Key}' was not part of the reviewed plan.");
            }

            var current = R3Yaml.ReadVal(
                plan.CfgPath,
                change.Section,
                change.Key);

            if (!string.Equals(
                    current,
                    reviewed.CurVal,
                    StringComparison.Ordinal))
            {
                throw new IOException(
                    $"RPCS3 config changed after preview at '{change.Section}/{change.Key}'. Rebuild the plan before applying changes.");
            }
        }
    }

    private void RollbackCommitted(
        BackupManifest backup,
        string targetPath,
        PatchTargetState originalState,
        PatchTargetState preparedState)
    {
        var currentState =
            PatchFileTransactionService.CaptureExpectedState(
                targetPath,
                R3Limits.CfgByte,
                "RPCS3 custom config YAML");
        if (Same(currentState, originalState))
        {
            return;
        }

        if (!Same(currentState, preparedState))
        {
            throw new IOException(
                "Config rollback was skipped because the target contains an unknown external state.");
        }

        _BkpSvc.RestoreTargets(
            backup,
            [targetPath],
            () => PatchFileTransactionService.ValidateExpectedState(
                targetPath,
                preparedState));
    }

    private static PatchTargetState PreparedState(
        string? preparedText)
    {
        return new PatchTargetState(
            preparedText is not null,
            preparedText is null
                ? null
                : Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(preparedText))),
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");
    }

    private static bool Same(
        PatchTargetState left,
        PatchTargetState right)
    {
        return left.Exists == right.Exists &&
               string.Equals(
                   left.Sha256,
                   right.Sha256,
                   StringComparison.OrdinalIgnoreCase);
    }
}
