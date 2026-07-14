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
    private static readonly LocalizedGameScanDiagnosticFormatter
        GameScanDiagnosticFormatter = new();

    private void LogGameScanDiagnostics(
        IReadOnlyList<GameScanDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Log(GameScanDiagnosticFormatter.Format(diagnostic));
        }

        if (diagnostics.Count > 0)
        {
            Log(UiText.F(
                "Msg.GameScanDiagnostics",
                diagnostics.Count));
        }
    }

    private void RestoreBackup()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.T("Msg.AnalyzeRpcs3First"), UiText.T("Dialog.NoRpcs3"));
                return;
            }

            var backup = Advanced.SelectedBackup;
            if (backup is null)
            {
                UiDlg.Info(UiText.T("Msg.NoBkp"), UiText.T("Dialog.NoBkp"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRestoreRunning"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedRestoreLog"));
                return;
            }

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.RestoreConfirm", backup.CreatedAt.LocalDateTime),
                UiText.T("Dialog.RestoreTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelRestore"));
                return;
            }

            _flow.Restore(_install, backup);
            Log(UiText.F("Msg.Restored", backup.DirectoryPath));

            BuildCurrentPlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRestore"));
            Log(UiText.F("Msg.RestoreFailed", ex.Message));
        }
    }

    private void ImportWikiXml()
    {
        var fileName = UiDlg.PickWikiXml();
        if (fileName is null)
        {
            return;
        }

        Advanced.DeveloperWikiPath = fileName;
        RebuildCatalog();
    }

    private void RebuildCatalog()
    {
        try
        {
            if (!File.Exists(Advanced.DeveloperWikiPath))
            {
                UiDlg.Info(UiText.T("Msg.ImportWikiFirst"), UiText.T("Dialog.NoWiki"));
                return;
            }

            _catalog = _flow.RebuildCat(Advanced.DeveloperWikiPath);
            _catalogLoadError = null;

            UpdateCatalogStatus();
            Log(UiText.F("Msg.CatRebuilt", _catalog.Count));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRebuildCat"));
            Log(UiText.F("Msg.CatRebuildFailed", ex.Message));
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void ShowCatalogInfo()
    {
        if (_catalog is null)
        {
            UiDlg.Info(_catalogLoadError ?? CatalogMissingException.UserMessage, UiText.T("Dialog.CatInfo"));
            return;
        }

        UiDlg.Info(_flow.CatInfoText(_catalog.Info), UiText.T("Dialog.CatInfo"));
    }

    private void ExportCatalog()
    {
        try
        {
            if (_catalog is null)
            {
                UiDlg.Info(_catalogLoadError ?? CatalogMissingException.UserMessage, UiText.T("Dialog.NoCat"));
                return;
            }

            var fileName = UiDlg.PickCatExportPath();
            if (fileName is null)
            {
                return;
            }

            _flow.ExportCat(_catalog, fileName);
            Log(UiText.F("Msg.CatExported", fileName));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailExportCat"));
            Log(UiText.F("Msg.CatExportFailed", ex.Message));
        }
    }

    private void CopyLog()
    {
        if (!string.IsNullOrWhiteSpace(Advanced.LogText))
        {
            UiDlg.CopyText(Advanced.LogText);
        }
    }

    private void RefreshBackups()
    {
        if (_install is null)
        {
            Advanced.RefreshBackups([]);
            BackupsStatus = UiText.F("Status.BkpCount", 0);
            RaiseCommandStates();
            return;
        }

        var backups = _flow.ListBackups(_install);

        Advanced.RefreshBackups(backups);
        BackupsStatus = UiText.F("Status.BkpCount", Advanced.Backups.Count);

        RaiseCommandStates();
    }
}
