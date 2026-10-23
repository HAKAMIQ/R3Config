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
    private void Apply()
    {
        try
        {
            if (_install is null || _curPlan is null)
            {
                UiDlg.Info(UiText.GetText("Msg.SelectGameFirst"), UiText.GetText("Dialog.NoGame"));
                return;
            }

            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            var selectedItems = Plan.Items
                .Where(item => item.IsSel && item.IsDiff && item.IsAction)
                .ToArray();

            if (selectedItems.Length == 0)
            {
                UiDlg.Info(UiText.GetText("Msg.NoCfgSelected"), UiText.GetText("Dialog.NothingToApply"));
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRpcs3"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedApplyRunning"));
                return;
            }

            var manualCount = selectedItems.Count(item => Svc.RecPol.Requires((Mdl.RecRisk)item.Risk));
            var warning = manualCount > 0
                ? UiText.Fmt("Msg.ManualWarning", manualCount)
                : string.Empty;

            var confirmed = _confirm(
                UiText.Fmt("Msg.ApplyCfgConfirm", selectedItems.Length, _curPlan.GameSer, warning),
                UiText.GetText("Dialog.ApplyCfgTitle"));

            if (!confirmed)
            {
                Log(UiText.GetText("Msg.UserCancelApply"));
                return;
            }

            var selectedChanges = selectedItems.Select(item => item.Change).ToArray();
            var result = _flow.ApplyCfg(_curPlan, selectedChanges, _install);

            Log(UiText.Fmt("Msg.BkpSaved", UiDate.Inline(result.Backup.MadeAt)));

            foreach (var item in selectedItems)
            {
                Log(UiText.Fmt("Msg.CfgApplied", _curPlan.GameSer, item.DspPath, item.CurDsp, item.Val));
            }

            Log(UiText.Fmt("Msg.CfgWritten", _curPlan.CfgPath));

            LoadBkp();
            MakePlan();
            UiDlg.Info(
                UiText.Fmt("Msg.CfgApplySuccess", result.ChgList.Count),
                UiText.GetText("Dialog.ApplyCfgTitle"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailApply"));
            Log(UiText.Fmt("Msg.ApplyFailed", ex.Message));
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void DelWiki()
    {
        try
        {
            if (_install is null || _curPlan is null)
            {
                UiDlg.Info(UiText.GetText("Msg.SelectGameFirst"), UiText.GetText("Dialog.NoGame"));
                return;
            }

            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            var removableChanges = _curPlan.Items
                .Where(item => item.HasCustomOverride)
                .Select(item => item.Change)
                .ToArray();

            if (removableChanges.Length == 0)
            {
                UiDlg.Info(UiText.GetText("Msg.NoWikiSettingsToRemove"), UiText.GetText("Dialog.NothingToApply"));
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRpcs3"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedApplyRunning"));
                return;
            }

            var confirmed = _confirm(
                UiText.Fmt("Msg.RemoveWikiSettingsConfirm", removableChanges.Length, _curPlan.GameSer),
                UiText.GetText("Dialog.RemoveWikiSettingsTitle"));

            if (!confirmed)
            {
                Log(UiText.GetText("Msg.UserCancelRemoveWikiSettings"));
                return;
            }

            var result = _flow.DelCfg(_curPlan, removableChanges, _install);

            Log(UiText.Fmt("Msg.BkpSaved", UiDate.Inline(result.Backup.MadeAt)));

            foreach (var change in removableChanges)
            {
                Log(UiText.Fmt("Msg.WikiSettingRemoved", _curPlan.GameSer, change.Path));
            }

            LoadBkp();
            MakePlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailRemoveWikiSettings"));
            Log(UiText.Fmt("Msg.RemoveWikiSettingsFailed", ex.Message));
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void MakePlan()
    {
        if (_install is null || GameList.SelGame is null)
        {
            _curPlan = null;
            Plan.Clear();
            Preview.Clear();
            ClearGameSetup();
            Advanced.TgtCfg = UiText.GetText("Ui.EmptyValue");
            _gameSettings = null;
            RefreshRuntimeContext();
            LoadGame();
            RaiseCmd();
            return;
        }

        _curPlan = _flow.CfgPlan(
            _install,
            GameList.SelGame.Game,
            GameList.SelGame.Rec,
            _hwInfo,
            _buildIdentity);

        GameList.SelGame.RecState = _curPlan.RecState;
        Plan.Load(
            _curPlan,
            ProjectGameSettings(GameList.SelGame, _curPlan));
        Advanced.TgtCfg = _curPlan.CfgPath;
        RefreshRuntimeContext();

        MakePrev();
        RefreshGameSetupPlan();
        LoadGame();
        RaiseCmd();
    }
}
