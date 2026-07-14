using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private void OpenRpcs3PatchManager()
    {
        if (_install is null || !File.Exists(_install.ExePath))
        {
            UiDlg.Warn(
                UiText.T("Patch.SourceStatus.NoRpcs3"),
                UiText.T("Patch.SourceStatus.ManagerTitle"));
            return;
        }

        try
        {
            if (!Rpcs3InstallService.IsRpcs3Running())
            {
                Process.Start(new ProcessStartInfo(_install.ExePath)
                {
                    UseShellExecute = true
                });
            }

            UiDlg.Info(
                UiText.T("Patch.SourceStatus.ManagerInstructions"),
                UiText.T("Patch.SourceStatus.ManagerTitle"));
            Log(UiText.T("Msg.PatchManagerInstructions"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(
                ex.Message,
                UiText.T("Patch.SourceStatus.ManagerTitle"));
            Log(ex.Message);
        }
    }

    private void OpenPatchesFolder()
    {
        if (_install is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_install.PatchesPath)
            {
                UseShellExecute = true
            });
            Log(UiText.T("Msg.PatchesFolderOpened"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(
                ex.Message,
                UiText.T("Patch.SourceStatus.OpenFolder"));
            Log(ex.Message);
        }
    }

    public void ShowAdvanced()
    {
        RequestAdvancedDialog?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleLanguage()
    {
        var nextLang = UiText.IsRtl ? UiText.EnglishLang : UiText.ArabicLang;
        UiText.SetLang(nextLang);
        UiLayoutService.ApplyApplicationResources();
        UiPrefs.SaveLang(UiText.CurrentLang);
        RefreshLanguageText();
    }

    private void RemoveCanaryPatches()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Warn(UiText.T("Patch.ImportCanaryNoRpcs3Path"), UiText.T("Patch.RemoveCanary"));
                return;
            }

            RefreshRpcs3PresentationState();
            if (IsBusy)
            {
                return;
            }

            if (IsRpcs3RunningPresentation)
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            if (!_confirm(
                    UiText.T("Patch.RemoveCanaryConfirm"),
                    UiText.T("Patch.RemoveCanary")))
            {
                return;
            }

            var result = _flow.RemovePreparedCanaryPatch(_install);
            if (!result.Succeeded)
            {
                UiDlg.Error(result.Error ?? result.Message, UiText.T("Patch.RemoveCanary"));
                Log(result.Error ?? result.Message);
                return;
            }

            Log(result.Message);
            UiDlg.Info(result.Message, UiText.T("Patch.RemoveCanary"));
            RefreshCanaryState();
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Patch.RemoveCanary"));
            Log(ex.Message);
        }
        finally
        {
            RefreshRpcs3PresentationState();
        }
    }

    private void ImportCanaryPatch()
    {
        if (_install is null)
        {
            UiDlg.Warn(UiText.T("Patch.ImportCanaryNoRpcs3Path"), UiText.T("Patch.ImportCanaryTitle"));
            return;
        }

        RefreshRpcs3PresentationState();
        if (IsBusy)
        {
            return;
        }

        if (IsRpcs3RunningPresentation)
        {
            UiDlg.Warn(
                UiText.T("Msg.BlockedRpcs3"),
                UiText.T("Dialog.Rpcs3Running"));
            Log(UiText.T("Msg.BlockedApplyRunning"));
            return;
        }

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            ?? Application.Current?.MainWindow;

        var viewModel = new CanaryPatchImportVm(
            _install,
            _flow,
            _isRpcs3Running,
            _confirm);
        var dialog = new CanaryPatchImportDialog(owner, viewModel);

        try
        {
            if (dialog.ShowDialog() == true)
            {
                Log(viewModel.ResultMessage);
                UiDlg.Info(viewModel.ResultMessage, UiText.T("Patch.ImportCanaryTitle"));
                PatchPreview.ShowExpertPatches = true;
                RefreshCanaryState();
                LoadPatchCatalog();
                BuildCurrentPatchPreview();
            }
        }
        finally
        {
            RefreshRpcs3PresentationState();
        }
    }

    private void BrowseRpcs3()
    {
        var fileName = UiDlg.PickRpcs3Exe(Rpcs3ExePath);
        if (fileName is null)
        {
            return;
        }

        Rpcs3ExePath = fileName;
        _install = null;
        HCanary.SetContext(
            install: null,
            game: null);
        RefreshRpcs3PresentationState();
        Rpcs3Status = UiText.T("Status.Rpcs3Ready");
        SaveSettings();
        Log(UiText.F("Msg.SelectRpcs3", fileName));
    }

    private async Task AnalyzeAsync()
    {
        try
        {
            IsBusy = true;
            Log(UiText.T("Msg.StartAnalyze"));

            if (_catalog is null)
            {
                throw new InvalidOperationException(_catalogLoadError ?? CatalogMissingException.UserMessage);
            }

            var result = await _flow.AnalyzeAsync(Rpcs3ExePath, _catalog);
            _install = result.Install;
            RefreshRpcs3PresentationState();
            RefreshCanaryState();
            _hwInfo = result.Hw;
            _patchCatalog = result.PatchCat.Cat;
            _patchCatalogError = result.PatchCat.Error;
            _patchCatalogSources = result.PatchCat.SrcStat;
            Rpcs3Status = UiText.T("Status.Rpcs3Ready");
            SaveSettings();

            Advanced.ConfigPath = _install.ConfigPath;
            Advanced.CustomConfigsPath = _install.CustomConfigsPath;
            Advanced.PatchesPath = _install.PatchesPath;

            HardwareStatus = FormatHardwareStatus();
            HardwareTooltip = FormatHardwareTooltip();
            Log(UiText.F("Msg.HwScanned", _hwInfo.Tier));

            var gameItems = result.Games
                .Select(row => new GameItemVm(
                    game: row.Game,
                    recommendation: row.Rec,
                    recState: row.RecState,
                    serial: row.Game.Serial,
                    title: row.Rec?.WikiTitle ?? row.Game.Title,
                    isDetectedInRpcs3: row.IsDetectedInRpcs3))
                .ToArray();

            GameList.SelectedGame = null;
            GameList.Load(gameItems);
            GameList.SelectedGame = GameList.Games.FirstOrDefault();

            RefreshPatchSourceStatus();

            Advanced.RefreshBackups(result.Backups);
            BackupsStatus = UiText.F("Status.BkpCount", Advanced.Backups.Count);

            Log(UiText.F("Msg.CatLoaded", _catalog.DataSource, _catalog.DataVersion, _catalog.Count));
            Log(UiText.F("Msg.GamesFound", GameList.Games.Count));
            LogGameScanDiagnostics(result.GameScanDiagnostics);
        }
        catch (Exception ex)
        {
            Rpcs3Status = File.Exists(Rpcs3ExePath)
                ? UiText.T("Status.Rpcs3Ready")
                : UiText.T("Status.Rpcs3Missing");

            UiDlg.Error(ex.Message, UiText.T("Dialog.FailAnalyze"));
            Log(UiText.F("Msg.AnalyzeFailed", ex.Message));
            PatchPreview.LoadError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            RefreshRpcs3PresentationState();
            RaiseCommandStates();
        }
    }
}
