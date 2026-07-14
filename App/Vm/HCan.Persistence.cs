using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class HCanaryVm : VmBase
{
    private void Save(bool saveAsNew)
    {
        if (_install is null)
        {
            return;
        }

        RefreshMutationState();
        if (!CanMutateCurrentInstall)
        {
            if (_isRpcs3RunningPresentation)
            {
                UiDlg.Warn(
                    UiText.T("HCanary.Error.Rpcs3Running"),
                    UiText.T("HCanary.Title"));
            }
            return;
        }

        if (!_confirm(
                UiText.T(
                    saveAsNew || _currentIdentity is null
                        ? "HCanary.Confirm.SaveNew"
                        : "HCanary.Confirm.SaveReplace"),
                UiText.T("HCanary.Title")))
        {
            return;
        }

        try
        {
            var result = _flow.SaveHCanaryPatch(_install, BuildDraft(), _currentIdentity, saveAsNew);
            Status = DraftSt.Saved; _dirty = false;
            _currentIdentity = ExecutableHash.Trim() + "\0" + PatchName.Trim();
            StatusMessage = UiText.F("HCanary.Message.Saved", result.Path, result.PatNum);
            RefreshStoredPatches(); CatalogChanged?.Invoke(this, EventArgs.Empty); RefreshDraft(markDirty: false);
        }
        catch (Exception ex) { UiDlg.Error(UiText.F("HCanary.Error.Save", ex.Message), UiText.T("HCanary.Title")); StatusMessage = ex.Message; }
        finally { RefreshMutationState(); }
    }

    private void DeleteStored()
    {
        if (_install is null || _currentIdentity is null)
        {
            return;
        }

        RefreshMutationState();
        if (!CanMutateCurrentInstall)
        {
            if (_isRpcs3RunningPresentation)
            {
                UiDlg.Warn(
                    UiText.T("HCanary.Error.Rpcs3Running"),
                    UiText.T("HCanary.Title"));
            }
            return;
        }

        if (!_confirm(
                UiText.T("HCanary.Confirm.Delete"),
                UiText.T("HCanary.Title")))
        {
            return;
        }

        try
        {
            _flow.DeleteHCanaryPatch(_install, _currentIdentity);
            CatalogChanged?.Invoke(this, EventArgs.Empty); RefreshStoredPatches(); NewDraft(); StatusMessage = UiText.T("HCanary.Message.Deleted");
        }
        catch (Exception ex) { UiDlg.Error(UiText.F("HCanary.Error.Delete", ex.Message), UiText.T("HCanary.Title")); }
        finally { RefreshMutationState(); }
    }

    private void OpenPatchesFolder() { if (_install is not null) OpenPath(_install.PatPath); }
    private void OpenImportedFile() { if (File.Exists(ImportedPatchPath)) OpenPath(ImportedPatchPath); }
    private void OpenRpcs3() { if (_install is not null) Process.Start(new ProcessStartInfo(_install.ExePath) { UseShellExecute = true }); }
    private static void OpenPath(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { ReadLogCommand, UseSelectedGameCommand, DeleteGameCommand, DeleteInstructionCommand,
                     DuplicateInstructionCommand, MoveInstructionUpCommand, MoveInstructionDownCommand, DeleteAnchorCommand,
                     AddAnchorInstructionCommand, DeleteAnchorInstructionCommand, AddAnchorLoadCommand, OpenStoredCommand,
                     SaveCommand, SaveAsCommand, DeleteStoredCommand, OpenPatchesFolderCommand, OpenImportedFileCommand,
                     OpenRpcs3Command, CopyYamlCommand }) command.RaiseCanExecuteChanged();
    }
}
