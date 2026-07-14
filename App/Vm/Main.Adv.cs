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
    private static readonly GmDiag
        DiagFmt = new();

    private void LogGame(
        IReadOnlyList<ScanDiag> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Log(DiagFmt.Format(diagnostic));
        }

        if (diagnostics.Count > 0)
        {
            Log(UiText.Fmt(
                "Msg.GameScanDiagnostics",
                diagnostics.Count));
        }
    }

    private void RstBkp()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.GetText("Msg.AnalyzeRpcs3First"), UiText.GetText("Dialog.NoRpcs3"));
                return;
            }

            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            var backup = Advanced.SelBkp;
            if (backup is null)
            {
                UiDlg.Info(UiText.GetText("Msg.NoBkp"), UiText.GetText("Dialog.NoBkp"));
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRestoreRunning"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedRestoreLog"));
                return;
            }

            var confirmed = _confirm(
                UiText.Fmt(
                    "Msg.RestoreConfirm",
                    backup.MadeAt.LocalDateTime,
                    backup.Files.Count),
                UiText.GetText("Dialog.RestoreTitle"));

            if (!confirmed)
            {
                Log(UiText.GetText("Msg.UserCancelRestore"));
                return;
            }

            _flow.Restore(_install, backup);
            Log(UiText.Fmt("Msg.Restored", backup.DirPath));

            MakePlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailRestore"));
            Log(UiText.Fmt("Msg.RestoreFailed", ex.Message));
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void ImpWiki()
    {
        var fileName = UiDlg.PickWiki();
        if (fileName is null)
        {
            return;
        }

        Advanced.WikiPath = fileName;
        BuildCat();
    }

    private void BuildCat()
    {
        try
        {
            if (!File.Exists(Advanced.WikiPath))
            {
                UiDlg.Info(UiText.GetText("Msg.ImportWikiFirst"), UiText.GetText("Dialog.NoWiki"));
                return;
            }

            _catalog = _flow.BuildCat(Advanced.WikiPath);
            _loadErr = null;

            SetCat();
            Log(UiText.Fmt("Msg.CatRebuilt", _catalog.Count));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailRebuildCat"));
            Log(UiText.Fmt("Msg.CatRebuildFailed", ex.Message));
        }
        finally
        {
            RaiseCmd();
        }
    }

    private void ShowCat()
    {
        if (_catalog is null)
        {
            UiDlg.Info(_loadErr ?? MissEx.UserMsg, UiText.GetText("Dialog.CatInfo"));
            return;
        }

        UiDlg.Info(_flow.CatText(_catalog.Info), UiText.GetText("Dialog.CatInfo"));
    }

    private void ExpCat()
    {
        try
        {
            if (_catalog is null)
            {
                UiDlg.Info(_loadErr ?? MissEx.UserMsg, UiText.GetText("Dialog.NoCat"));
                return;
            }

            var fileName = UiDlg.PickExp();
            if (fileName is null)
            {
                return;
            }

            _flow.ExpCat(_catalog, fileName);
            Log(UiText.Fmt("Msg.CatExported", fileName));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailExportCat"));
            Log(UiText.Fmt("Msg.CatExportFailed", ex.Message));
        }
    }

    private void CopyLog()
    {
        if (!string.IsNullOrWhiteSpace(Advanced.LogText))
        {
            UiDlg.CopyText(Advanced.LogText);
        }
    }

    private void LoadBkp()
    {
        if (_install is null)
        {
            Advanced.LoadBkp([]);
            BkpStat = UiText.Fmt("Status.BkpCount", 0);
            RaiseCmd();
            return;
        }

        var backups = _flow.ListBkp(_install);

        Advanced.LoadBkp(backups);
        BkpStat = UiText.Fmt("Status.BkpCount", Advanced.Backups.Count);

        RaiseCmd();
    }
}
