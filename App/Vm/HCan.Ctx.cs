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
    public void Load(string baseDirectory) => LoadStor();

    public void SetCtx(Rpcs3InstallDescriptor? install, Rpcs3GameDescriptor? game)
    {
        var gameChanged =
            _game?.Serial != game?.Serial ||
            !string.Equals(
                Svc.PatVer.Norm(_game?.Version),
                Svc.PatVer.Norm(game?.Version),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                _game?.InstPath,
                game?.InstPath,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                _install?.ExePath,
                install?.ExePath,
                StringComparison.OrdinalIgnoreCase);
        if (gameChanged)
        {
            CancelRead();
        }

        _install = install;
        _game = game;
        LoadStor();
        OnChange(nameof(R3Path));
        OnChange(nameof(LogPath));
        OnChange(nameof(PatPath));
        OnChange(nameof(GameName));
        OnChange(nameof(GameId));
        OnChange(nameof(GameVer));
        OnChange(nameof(HasGame));
        OnChange(nameof(SaveHint));
        if (gameChanged && _dirty)
        {
            SetMsg("HCanary.Message.DraftPreserved");
        }
        else if (game is not null && !_dirty && _curId is null)
        {
            NewDraft();
        }
        LoadHash();
        LoadMut();
    }

    public void Analyze(Rpcs3GameDescriptor? game) => SetCtx(_install, game);

    public void SetAvail(
        bool isBusy,
        bool isRpcs3Running)
    {
        _isBusy = isBusy;
        _runUi = isRpcs3Running;
        OnChange(nameof(CanMut));
        OnChange(nameof(SaveHint));
        RaiseCmd();
    }

    public void LoadMut()
    {
        var isRunning = false;
        if (_install is not null)
        {
            try
            {
                isRunning = _isRun();
            }
            catch
            {
                isRunning = true;
            }
        }

        SetAvail(
            _isBusy,
            isRunning);
    }

    public void SetLang()
    {
        OnChange(nameof(R3Path));
        OnChange(nameof(GameName));
        OnChange(nameof(GameId));
        OnChange(nameof(GameVer));
        OnChange(nameof(DraftTxt));
        OnChange(nameof(Category));
        OnChange(nameof(SaveHint));
        OnChange(nameof(Msg));
        OnChange(nameof(LogStat));
        OnChange(nameof(HashHead));
        OnChange(nameof(SelectedOperationHelp));
        OnChange(nameof(SelectedAnchorOperationHelp));
        foreach (var status in Statuses) status.SetLang();
        RefreshValidationText();
    }

    public void Dispose()
    {
        lock (_readSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _readVersion++;
            _readCancellation?.Cancel();
            _readCancellation = null;
        }

        OnChange(nameof(CanMut));
        OnChange(nameof(SaveHint));
        RaiseCmd();
        GC.SuppressFinalize(this);
    }

    private void SetMsg(string key, params object?[] args)
    {
        _msgKey = key;
        _msgArgs = args;
        _msgRaw = string.Empty;
        OnChange(nameof(Msg));
    }

    private void SetRawMsg(string value)
    {
        _msgKey = null;
        _msgArgs = [];
        _msgRaw = value;
        OnChange(nameof(Msg));
    }

    private void SetLogStat(string key, params object?[] args)
    {
        _logStatKey = key;
        _logStatArgs = args;
        OnChange(nameof(LogStat));
    }

    private void ClearLogStat()
    {
        _logStatKey = null;
        _logStatArgs = [];
        OnChange(nameof(LogStat));
    }

    private static string ResolveLocalizedText(
        string? key,
        object?[] args,
        string rawValue)
    {
        if (key is null)
        {
            return rawValue;
        }

        return args.Length == 0
            ? UiText.GetText(key)
            : UiText.Fmt(key, args);
    }
}
