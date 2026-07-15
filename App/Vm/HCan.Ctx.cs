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
        var gameChanged = _game?.Serial != game?.Serial;
        _install = install;
        _game = game;
        LoadHash();
        LoadStor();
        OnChange(nameof(R3Path));
        OnChange(nameof(LogPath));
        OnChange(nameof(PatPath));
        OnChange(nameof(GameName));
        OnChange(nameof(GameId));
        OnChange(nameof(GameVer));
        if (gameChanged && _dirty)
        {
            SetMsg("HCanary.Message.DraftPreserved");
        }
        else if (game is not null && !_dirty && _curId is null)
        {
            NewDraft();
        }
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
        OnChange(nameof(Msg));
        OnChange(nameof(LogStat));
        RefreshValidationText();
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
