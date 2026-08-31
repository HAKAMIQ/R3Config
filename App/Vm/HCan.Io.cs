using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class HCanVm : VmBase
{
    private void Save(bool saveAsNew)
    {
        if (_install is null)
        {
            return;
        }

        LoadMut();
        if (!CanMut)
        {
            if (_runUi)
            {
                UiDlg.Warn(
                    UiText.GetText("HCanary.Error.Rpcs3Running"),
                    UiText.GetText("HCanary.Title"));
            }
            return;
        }

        if (!_confirm(
                UiText.GetText(
                    saveAsNew || _curId is null
                        ? "HCanary.Confirm.SaveNew"
                        : "HCanary.Confirm.SaveReplace"),
                UiText.GetText("HCanary.Title")))
        {
            return;
        }

        try
        {
            var result = _flow.SaveHCan(_install, Build(), _curId, saveAsNew);
            Status = DraftSt.Saved; _dirty = false; OnChange(nameof(IsDirty));
            _curId = ExeHash.Trim() + "\0" + PatName.Trim();
            OnChange(nameof(HasStoredPatch));
            OnChange(nameof(SaveHint));
            SetMsg("HCanary.Message.Saved", result.Path, result.PatNum);
            LoadStor(); CatChg?.Invoke(this, EventArgs.Empty); LoadDrft(markDirty: false);
        }
        catch (Exception ex) { UiDlg.Error(UiText.Fmt("HCanary.Error.Save", ex.Message), UiText.GetText("HCanary.Title")); SetRawMsg(ex.Message); }
        finally { LoadMut(); }
    }

    private void DelSave()
    {
        if (_install is null || _curId is null)
        {
            return;
        }

        LoadMut();
        if (!CanMut)
        {
            if (_runUi)
            {
                UiDlg.Warn(
                    UiText.GetText("HCanary.Error.Rpcs3Running"),
                    UiText.GetText("HCanary.Title"));
            }
            return;
        }

        if (!_confirm(
                UiText.GetText("HCanary.Confirm.Delete"),
                UiText.GetText("HCanary.Title")))
        {
            return;
        }

        try
        {
            _flow.DelHCan(_install, _curId);
            CatChg?.Invoke(this, EventArgs.Empty); LoadStor(); NewDraft(); SetMsg("HCanary.Message.Deleted");
        }
        catch (Exception ex) { UiDlg.Error(UiText.Fmt("HCanary.Error.Delete", ex.Message), UiText.GetText("HCanary.Title")); }
        finally { LoadMut(); }
    }

    private void OpenDir() { if (_install is not null) OpenPath(_install.PatPath); }
    private void OpenFile() { if (File.Exists(PatPath)) OpenPath(PatPath); }
    private void OpenR3() { if (_install is not null) Process.Start(new ProcessStartInfo(_install.ExePath) { UseShellExecute = true }); }
    private static void OpenPath(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void LaunchGame()
    {
        if (_install is null || _game is null)
        {
            return;
        }

        HashReadCode running;
        try
        {
            running = _hashOps.CheckRunning(
                _install,
                _game);
        }
        catch (Exception ex)
        {
            SetLogStat(
                "HCanary.Launch.Failed",
                ex.Message);
            return;
        }

        if (running == HashReadCode.SelectedGameRunning)
        {
            SetLogStat(
                "HCanary.Log.SelectedGameRunning");
            return;
        }

        if (running == HashReadCode.Rpcs3Running)
        {
            SetLogStat(
                "HCanary.Log.Rpcs3Running");
            return;
        }

        HCanDiscoverySession session;
        try
        {
            session = _hashOps.Begin(
                _install,
                _game);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException or
            InvalidOperationException or
            NotSupportedException or
            System.Text.Json.JsonException)
        {
            SetLogStat(
                "HCanary.Launch.Failed",
                ex.Message);
            return;
        }

        var result = _launchGame(_install, _game);
        switch (result.Error)
        {
            case GameLaunchError.None when result.Started:
                _hashOps.Attach(
                    session,
                    result.ProcessId);
                SetLogStat(
                    "HCanary.Launch.Started",
                    _game.Title);
                SetAvail(
                    _isBusy,
                    isRpcs3Running: true);
                break;

            case GameLaunchError.Rpcs3Missing:
                _hashOps.Cancel(session);
                SetLogStat(
                    "HCanary.Launch.Rpcs3Missing");
                break;

            case GameLaunchError.GameIdentityMissing:
                _hashOps.Cancel(session);
                SetLogStat(
                    "HCanary.Launch.GameIdentityMissing",
                    result.Detail ?? _game.Serial);
                break;

            default:
                _hashOps.Cancel(session);
                SetLogStat(
                    "HCanary.Launch.Failed",
                    result.Detail ?? string.Empty);
                break;
        }
    }

    private void DeleteHashBinding()
    {
        if (_install is null ||
            _game is null ||
            !HasSavedBinding)
        {
            return;
        }

        if (!_confirm(
                UiText.GetText("HCanary.Hash.DeleteConfirm"),
                UiText.GetText("HCanary.Title")))
        {
            return;
        }

        try
        {
            if (_hashOps.DeleteSaved(
                    _install,
                    _game))
            {
                Hashes.Clear();
                SelHash = null;
                HasSavedBinding = false;
                _hashOld = false;
                OnChange(nameof(HashHead));
                NotifyHashSummary();
                SetLogStat(
                    "HCanary.Hash.Deleted");
                HashChg?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException)
        {
            SetLogStat(
                "HCanary.Log.Error",
                ex.Message);
        }
    }

    private void RaiseCmd()
    {
        ReadCmd.RaiseCan();
        foreach (var command in new[] { LaunchCmd, RediscoverCmd, DeleteHashCmd, UseCmd, DelGmCmd, DelICmd,
                     DupICmd, InsUp, InsDown, DelACmd,
                     AncAdd, AncDel, AncUp, AncDown, AncLoad, OpenCmd,
                     SaveCmd, SaveAs, DelCmd, PatDir, ImpFile,
                     R3Cmd, CopyCmd }) command.RaiseCan();
    }
}
