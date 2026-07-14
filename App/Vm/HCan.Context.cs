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
    public void Load(string baseDirectory) => RefreshStoredPatches();

    public void SetContext(Rpcs3InstallDescriptor? install, Rpcs3GameDescriptor? game)
    {
        var gameChanged = _contextGame?.Serial != game?.Serial;
        _install = install;
        _contextGame = game;
        LoadContextHashes();
        RefreshStoredPatches();
        OnPropertyChanged(nameof(Rpcs3Path));
        OnPropertyChanged(nameof(LogPath));
        OnPropertyChanged(nameof(ImportedPatchPath));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentGameId));
        OnPropertyChanged(nameof(CurrentGameVersion));
        if (gameChanged && _dirty)
        {
            StatusMessage = UiText.T("HCanary.Message.DraftPreserved");
        }
        else if (game is not null && !_dirty && _currentIdentity is null)
        {
            NewDraft();
        }
        RefreshMutationState();
    }

    public void Analyze(Rpcs3GameDescriptor? game) => SetContext(_install, game);

    public void UpdateMutationAvailability(
        bool isBusy,
        bool isRpcs3Running)
    {
        _isBusy = isBusy;
        _isRpcs3RunningPresentation = isRpcs3Running;
        OnPropertyChanged(nameof(CanMutateCurrentInstall));
        RaiseCommandStates();
    }

    public void RefreshMutationState()
    {
        var isRunning = false;
        if (_install is not null)
        {
            try
            {
                isRunning = _isRpcs3Running();
            }
            catch
            {
                isRunning = true;
            }
        }

        UpdateMutationAvailability(
            _isBusy,
            isRunning);
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Rpcs3Path));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentGameId));
        OnPropertyChanged(nameof(CurrentGameVersion));
        OnPropertyChanged(nameof(DraftStateText));
        OnPropertyChanged(nameof(PatchCategory));
        RefreshDraft(markDirty: false);
    }
}
