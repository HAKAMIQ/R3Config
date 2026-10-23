using System;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.View;

namespace R3Config.Vm;

public sealed partial class MainVm : VmBase
{
    private bool CanOpenNclImport()
    {
        return !_disposed &&
               _install is not null &&
               GameList.SelGame is { InR3: true } &&
               !IsBusy &&
               !IsFingerprintCaptureRunning;
    }

    private void OpenNclImport()
    {
        var install = _install;
        var selected = GameList.SelGame;

        if (install is null ||
            selected is null)
        {
            return;
        }

        LoadR3Ui();

        var viewModel = new NclImportVm(
            _flow,
            install,
            selected.Game,
            selected.Game.Title,
            Preview.Enabled(),
            _isRun,
            _confirm);

        var dialog = new NclImportDlg(
            System.Windows.Application.Current?.MainWindow,
            viewModel);

        try
        {
            if (dialog.ShowDialog() == true)
            {
                if (!string.IsNullOrWhiteSpace(
                        viewModel.ResMsg))
                {
                    Log(viewModel.ResMsg);
                }

                LoadBkp();
                LoadPat();
                MakePrev();
                RefreshGameSetupPlan();
            }
        }
        catch (Exception ex)
        {
            UiDlg.Error(
                ex.Message,
                UiText.GetText("Patch.Ncl.Title"));
        }
        finally
        {
            LoadR3Ui();
        }
    }
}
