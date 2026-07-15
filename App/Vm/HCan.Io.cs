using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
            Status = DraftSt.Saved; _dirty = false;
            _curId = ExeHash.Trim() + "\0" + PatName.Trim();
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

    private void RaiseCmd()
    {
        foreach (var command in new[] { ReadCmd, UseCmd, DelGmCmd, DelICmd,
                     DupICmd, InsUp, InsDown, DelACmd,
                     AncAdd, AncDel, AncLoad, OpenCmd,
                     SaveCmd, SaveAs, DelCmd, PatDir, ImpFile,
                     R3Cmd, CopyCmd }) command.RaiseCan();
    }
}
