using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using R3Config;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.View;

namespace R3Config.Vm;

public sealed partial class MainVm : VmBase
{
    private void ApplyChecked()
    {
        try
        {
            if (_install is null || _currentPlan is null)
            {
                UiDlg.Info(UiText.T("Msg.SelectGameFirst"), UiText.T("Dialog.NoGame"));
                return;
            }

            RefreshRpcs3PresentationState();
            if (IsBusy)
            {
                return;
            }

            var selectedItems = Plan.Items
                .Where(item => item.IsSelected && item.IsDifferent && item.IsActionable)
                .ToArray();

            if (selectedItems.Length == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoCfgSelected"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            if (IsRpcs3RunningPresentation)
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var manualCount = selectedItems.Count(item => Svc.RecPol.Requires((Mdl.RecRisk)item.Risk));
            var warning = manualCount > 0
                ? UiText.F("Msg.ManualWarning", manualCount)
                : string.Empty;

            var confirmed = _confirm(
                UiText.F("Msg.ApplyCfgConfirm", selectedItems.Length, _currentPlan.GameSer, warning),
                UiText.T("Dialog.ApplyCfgTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelApply"));
                return;
            }

            var selectedChanges = selectedItems.Select(item => item.Change).ToArray();
            var result = _flow.ApplyCfg(_currentPlan, selectedChanges, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var item in selectedItems)
            {
                Log(UiText.F("Msg.CfgApplied", _currentPlan.GameSer, item.DisplayPath, item.CurrentDisplay, item.ProposedValue));
            }

            Log(UiText.F("Msg.CfgWritten", _currentPlan.CfgPath));

            RefreshBackups();
            BuildCurrentPlan();
            UiDlg.Info(
                UiText.F("Msg.CfgApplySuccess", result.ChgList.Count),
                UiText.T("Dialog.ApplyCfgTitle"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailApply"));
            Log(UiText.F("Msg.ApplyFailed", ex.Message));
        }
        finally
        {
            RefreshRpcs3PresentationState();
        }
    }

    private void RemoveWikiSettings()
    {
        try
        {
            if (_install is null || _currentPlan is null)
            {
                UiDlg.Info(UiText.T("Msg.SelectGameFirst"), UiText.T("Dialog.NoGame"));
                return;
            }

            RefreshRpcs3PresentationState();
            if (IsBusy)
            {
                return;
            }

            var removableChanges = _currentPlan.Items
                .Where(item => item.CurVal is not null)
                .Select(item => item.Change)
                .ToArray();

            if (removableChanges.Length == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoWikiSettingsToRemove"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            if (IsRpcs3RunningPresentation)
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var confirmed = _confirm(
                UiText.F("Msg.RemoveWikiSettingsConfirm", removableChanges.Length, _currentPlan.GameSer),
                UiText.T("Dialog.RemoveWikiSettingsTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelRemoveWikiSettings"));
                return;
            }

            var result = _flow.RemoveCfg(_currentPlan, removableChanges, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var change in removableChanges)
            {
                Log(UiText.F("Msg.WikiSettingRemoved", _currentPlan.GameSer, change.Path));
            }

            RefreshBackups();
            BuildCurrentPlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRemoveWikiSettings"));
            Log(UiText.F("Msg.RemoveWikiSettingsFailed", ex.Message));
        }
        finally
        {
            RefreshRpcs3PresentationState();
        }
    }

    private void BuildCurrentPlan()
    {
        if (_install is null || GameList.SelectedGame is null)
        {
            _currentPlan = null;
            Plan.Clear();
            PatchPreview.Clear();
            Advanced.TargetConfigPath = UiText.T("Ui.EmptyValue");
            RefreshSelectedGameDisplay();
            RaiseCommandStates();
            return;
        }

        _currentPlan = _flow.BuildCfgPlan(
            _install,
            GameList.SelectedGame.Game,
            GameList.SelectedGame.Recommendation,
            _hwInfo);

        GameList.SelectedGame.RecState = _currentPlan.RecState;
        Plan.Load(_currentPlan);
        Advanced.TargetConfigPath = _currentPlan.CfgPath;

        BuildCurrentPatchPreview();
        RefreshSelectedGameDisplay();
        RaiseCommandStates();
    }
}
