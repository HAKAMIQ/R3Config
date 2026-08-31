using System;
using System.IO;
using System.Threading.Tasks;
using R3Config.Application;
using R3Config.Lang;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private HashLaunchState _hashLaunchState;
    private string? _hashLaunchDetail;

    public bool NeedsHashEvidence =>
        GameList.SelGame is { InR3: true } selected &&
        !HasHash(selected.Game);

    public bool ShowHashLaunchAction =>
        NeedsHashEvidence &&
        !RunUi &&
        _hashLaunchState != HashLaunchState.Started;

    public bool ShowHashRefreshAction =>
        NeedsHashEvidence &&
        (RunUi || _hashLaunchState == HashLaunchState.Started);

    public string HashLaunchHint
    {
        get
        {
            var selected = GameList.SelGame;
            return _hashLaunchState switch
            {
                HashLaunchState.Started when selected is not null =>
                    UiText.Fmt(
                        "PatchReadiness.LaunchStarted",
                        selected.Title),
                HashLaunchState.AlreadyRunning =>
                    UiText.GetText("PatchReadiness.LaunchAlreadyRunning"),
                HashLaunchState.StillRunning =>
                    UiText.GetText("PatchReadiness.LaunchStillRunning"),
                HashLaunchState.Failed =>
                    UiText.Fmt(
                        "PatchReadiness.LaunchFailed",
                        _hashLaunchDetail ?? string.Empty),
                HashLaunchState.Ready when RunUi =>
                    UiText.GetText("PatchReadiness.LaunchAlreadyRunning"),
                _ => UiText.GetText("PatchReadiness.LaunchHint")
            };
        }
    }

    private bool CanLaunchSelectedGame()
    {
        return !_disposed &&
               _install is not null &&
               File.Exists(_install.ExePath) &&
               NeedsHashEvidence &&
               !IsBusy &&
               !RunUi;
    }

    private void LaunchSelectedGame()
    {
        if (_install is null || GameList.SelGame is null)
        {
            return;
        }

        try
        {
            if (_isRun())
            {
                SetHashLaunchState(HashLaunchState.AlreadyRunning);
                SetRunUi(isRunning: true);
                return;
            }
        }
        catch (Exception ex)
        {
            SetHashLaunchState(
                HashLaunchState.Failed,
                ex.Message);
            return;
        }

        var result = _launchGame(
            _install,
            GameList.SelGame.Game);
        switch (result.Error)
        {
            case GameLaunchError.None when result.Started:
                SetHashLaunchState(HashLaunchState.Started);
                SetRunUi(isRunning: true);
                Log(UiText.Fmt(
                    "PatchReadiness.LaunchStarted",
                    GameList.SelGame.Title));
                break;
            case GameLaunchError.Rpcs3Missing:
                SetHashLaunchState(
                    HashLaunchState.Failed,
                    UiText.GetText("PatchReadiness.LaunchRpcs3Missing"));
                break;
            case GameLaunchError.GameIdentityMissing:
                SetHashLaunchState(
                    HashLaunchState.Failed,
                    UiText.Fmt(
                        "PatchReadiness.LaunchGameIdentityMissing",
                        result.Detail ?? GameList.SelGame.Serial));
                break;
            default:
                SetHashLaunchState(
                    HashLaunchState.Failed,
                    result.Detail ?? UiText.GetText("Ui.Unknown"));
                break;
        }
    }

    private async Task RefreshHashEvidenceAsync()
    {
        try
        {
            if (_isRun())
            {
                SetHashLaunchState(HashLaunchState.StillRunning);
                SetRunUi(isRunning: true);
                return;
            }
        }
        catch (Exception ex)
        {
            SetHashLaunchState(
                HashLaunchState.Failed,
                ex.Message);
            return;
        }

        SetRunUi(isRunning: false);
        await DoAsync();
    }

    private async void OnHCanaryHashChanged(
        object? sender,
        EventArgs e)
    {
        if (_disposed ||
            IsBusy ||
            string.IsNullOrWhiteSpace(ExePath) ||
            !File.Exists(ExePath))
        {
            return;
        }

        await DoAsync();
    }

    private bool CanRefreshHashEvidence()
    {
        return !_disposed &&
               NeedsHashEvidence &&
               !IsBusy &&
               File.Exists(ExePath);
    }

    private void SetRunUi(bool isRunning)
    {
        if (Set(
                ref _runUi,
                isRunning,
                nameof(RunUi)))
        {
            OnChange(nameof(CanMut));
            OnChange(nameof(CanSelectGame));
        }

        HCanary.SetAvail(
            IsBusy,
            isRunning);
        OnHashLaunchStateChanged();
        RaiseCmd();
    }

    private void ResetHashLaunchState()
    {
        _hashLaunchState = HashLaunchState.Ready;
        _hashLaunchDetail = null;
        ResetFingerprintCaptureState();
        OnHashLaunchStateChanged();
    }

    private void SetHashLaunchState(
        HashLaunchState state,
        string? detail = null)
    {
        _hashLaunchState = state;
        _hashLaunchDetail = detail;
        OnHashLaunchStateChanged();
    }

    private void OnHashLaunchStateChanged()
    {
        OnChange(nameof(NeedsHashEvidence));
        OnChange(nameof(HasCanaryDefinition));
        OnChange(nameof(HasCanaryFingerprint));
        OnChange(nameof(HasUnmatchedCanaryFingerprint));
        OnChange(nameof(NeedsCanaryFingerprint));
        OnChange(nameof(ShowHashLaunchAction));
        OnChange(nameof(ShowHashRefreshAction));
        OnChange(nameof(HashLaunchHint));
        OnChange(nameof(ShowFingerprintCaptureAction));
        OnChange(nameof(ShowFingerprintCaptureStatus));
        OnChange(nameof(FingerprintCaptureStatus));
        LaunchGameCmd.RaiseCan();
        HashRefreshCmd.RaiseCan();
        FingerprintCaptureCmd.RaiseCan();
    }

    private enum HashLaunchState
    {
        Ready,
        Started,
        AlreadyRunning,
        StillRunning,
        Failed
    }
}
