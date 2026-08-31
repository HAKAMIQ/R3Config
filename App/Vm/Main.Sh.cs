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
    private void OpenOfcl()
    {
        if (_install is null || !File.Exists(_install.ExePath))
        {
            UiDlg.Warn(
                UiText.GetText("Patch.SourceStatus.NoRpcs3"),
                UiText.GetText("Patch.SourceStatus.ManagerTitle"));
            return;
        }

        try
        {
            if (!Rpcs3InstallService.IsR3())
            {
                Process.Start(new ProcessStartInfo(_install.ExePath)
                {
                    UseShellExecute = true
                });
            }

            UiDlg.Info(
                UiText.GetText("Patch.SourceStatus.ManagerInstructions"),
                UiText.GetText("Patch.SourceStatus.ManagerTitle"));
            Log(UiText.GetText("Msg.OfficialPatchManagerInstructions"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(
                ex.Message,
                UiText.GetText("Patch.SourceStatus.ManagerTitle"));
            Log(ex.Message);
        }
    }

    private void OpenDir()
    {
        if (_install is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_install.PatPath)
            {
                UseShellExecute = true
            });
            Log(UiText.GetText("Msg.PatchesFolderOpened"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(
                ex.Message,
                UiText.GetText("Patch.SourceStatus.OpenFolder"));
            Log(ex.Message);
        }
    }

    public void ShowAdv()
    {
        AdvReq?.Invoke(this, EventArgs.Empty);
    }

    private void TogLang()
    {
        var nextLang = UiText.IsRtl ? UiText.EnLang : UiText.ArabLang;
        UiText.SetLang(nextLang);
        UiLaySvc.Apply();
        UiPrefs.SaveLang(UiText.CurLang);
        LoadLang();
    }

    private void DelCan()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Warn(UiText.GetText("Patch.ImportCanaryNoRpcs3Path"), UiText.GetText("Patch.RemoveCanary"));
                return;
            }

            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRpcs3"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedApplyRunning"));
                return;
            }

            if (!_confirm(
                    UiText.GetText("Patch.RemoveCanaryConfirm"),
                    UiText.GetText("Patch.RemoveCanary")))
            {
                return;
            }

            var result = _flow.DelCan(_install);
            if (!result.IsOk)
            {
                UiDlg.Error(result.Error ?? result.Message, UiText.GetText("Patch.RemoveCanary"));
                Log(result.Error ?? result.Message);
                return;
            }

            Log(result.Message);
            UiDlg.Info(result.Message, UiText.GetText("Patch.RemoveCanary"));
            LoadCan();
            LoadPat();
            MakePrev();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Patch.RemoveCanary"));
            Log(ex.Message);
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void ImpCan()
    {
        if (_install is null)
        {
            UiDlg.Warn(UiText.GetText("Patch.ImportCanaryNoRpcs3Path"), UiText.GetText("Patch.ImportCanaryTitle"));
            return;
        }

        LoadR3Ui();
        if (IsBusy)
        {
            return;
        }

        if (RunUi)
        {
            UiDlg.Warn(
                UiText.GetText("Msg.BlockedRpcs3"),
                UiText.GetText("Dialog.Rpcs3Running"));
            Log(UiText.GetText("Msg.BlockedApplyRunning"));
            return;
        }

        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;

        var viewModel = new CanImpVm(
            _install,
            _flow,
            _isRun,
            _confirm);
        var dialog = new CanDlg(owner, viewModel);

        try
        {
            if (dialog.ShowDialog() == true)
            {
                Log(viewModel.ResMsg);
                UiDlg.Info(viewModel.ResMsg, UiText.GetText("Patch.ImportCanaryTitle"));
                Preview.ShowExp = true;
                LoadCan();
                LoadPat();
                MakePrev();
            }
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void FindR3()
    {
        var fileName = UiDlg.PickExe(ExePath);
        if (fileName is null)
        {
            return;
        }

        ExePath = fileName;
        _install = null;
        HCanary.SetCtx(
            install: null,
            game: null);
        LoadR3Ui();
        R3Status = UiText.GetText("Status.Rpcs3Ready");
        SaveSet();
        Log(UiText.Fmt("Msg.SelectRpcs3", fileName));
    }

    private async Task DoAsync()
    {
        var exePath = ExePath;
        var operation = BeginAnalysis();
        try
        {
            SetBusyStage(AnalyzeStage.ReadRpcs3);
            IsBusy = true;
            Log(UiText.GetText("Msg.StartAnalyze"));
            await Task.Yield();

            if (_catalog is null)
            {
                throw new InvalidOperationException(_loadErr ?? MissingCatalogException.UserMsg);
            }

            var progress = new Progress<AnalyzeStage>(stage =>
            {
                if (IsCurrentAnalysis(operation, exePath))
                {
                    SetBusyStage(stage);
                }
            });
            var result = await _flow.DoAsync(
                exePath,
                _catalog,
                progress,
                operation.Token);
            if (!IsCurrentAnalysis(operation, exePath))
            {
                return;
            }

            _install = result.Install;
            LoadR3Ui();
            LoadCan();
            _hwInfo = result.Hw;
            _patCat = result.PatchCat.Cat;
            _catErr = result.PatchCat.Error;
            _catSrc = result.PatchCat.SrcStat;
            R3Status = UiText.GetText("Status.Rpcs3Ready");
            SaveSet();

            Advanced.CfgPath = _install.CfgPath;
            Advanced.CustCfg = _install.CustPath;
            Advanced.PatPath = _install.PatPath;

            HwStatus = FmtHw();
            HwTip = FmtHwTip();
            Log(UiText.Fmt("Msg.HwScanned", _hwInfo.Tier));

            var gameItems = result.Games
                .Select(row => new GameVm(
                    game: row.Game,
                    recommendation: row.Rec,
                    recState: row.RecState,
                    serial: row.Game.Serial,
                    title: GameVm.ResolveTitle(
                        row.Game,
                        row.Rec),
                    isDetectedInRpcs3: row.InR3))
                .ToArray();

            GameList.SelGame = null;
            GameList.Load(gameItems);
            GameList.SelGame = GameList.Games.FirstOrDefault();

            LoadSrc();

            Advanced.LoadBkp(result.Backups);
            BkpStat = UiText.Fmt("Status.BkpCount", Advanced.Backups.Count);

            Log(UiText.Fmt("Msg.CatLoaded", _catalog.DataSrc, _catalog.DataVer, _catalog.Count));
            Log(UiText.Fmt("Msg.GamesFound", GameList.Games.Count));
            LogGame(result.GameScanDiagnostic);
        }
        catch (OperationCanceledException)
            when (operation.Token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsCurrentAnalysis(operation, exePath))
            {
                return;
            }

            InvalidateAnalyzedContext();
            R3Status = File.Exists(ExePath)
                ? UiText.GetText("Status.Rpcs3Ready")
                : UiText.GetText("Status.Rpcs3Missing");

            Log(UiText.Fmt("Msg.AnalyzeFailed", ex.Message));
            Preview.LoadErr(ex.Message);
            await Task.Yield();
            if (IsCurrentAnalysis(operation, exePath))
            {
                UiDlg.Error(
                    ex.Message,
                    UiText.GetText("Dialog.FailAnalyze"));
            }
        }
        finally
        {
            if (EndAnalysis(operation))
            {
                IsBusy = false;
                LoadR3Ui();
                RaiseCmd();
            }
        }
    }

    private AnalysisOperation BeginAnalysis()
    {
        AnalysisOperation operation;
        lock (_operationSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _analysisCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _analysisCancellation = cancellation;
            operation = new AnalysisOperation(
                ++_analysisVersion,
                cancellation,
                cancellation.Token);
        }

        return operation;
    }

    private void CancelAnalysis()
    {
        lock (_operationSync)
        {
            _analysisCancellation?.Cancel();
        }
    }

    private bool IsCurrentAnalysis(
        AnalysisOperation operation,
        string exePath)
    {
        lock (_operationSync)
        {
            return !_disposed &&
                   !operation.Token.IsCancellationRequested &&
                   operation.Version == _analysisVersion &&
                   ReferenceEquals(
                       _analysisCancellation,
                       operation.Cancellation) &&
                   string.Equals(
                       ExePath,
                       exePath,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool EndAnalysis(AnalysisOperation operation)
    {
        var isCurrent = false;
        lock (_operationSync)
        {
            if (operation.Version == _analysisVersion &&
                ReferenceEquals(
                    _analysisCancellation,
                    operation.Cancellation))
            {
                _analysisCancellation = null;
                isCurrent = true;
            }
        }

        operation.Cancellation.Dispose();
        return isCurrent;
    }

    private sealed record AnalysisOperation(
        long Version,
        CancellationTokenSource Cancellation,
        CancellationToken Token);
}
