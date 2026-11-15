using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

// Recommendation database sync: local overlay on catalog load, a quiet
// background check after start-up and once a day, and the manual check.
// Sync results only replace the in-memory catalog; nothing here writes an
// RPCS3 file. The selected install is passed only so the sync can read
// RPCS3's config_database.dat as a fallback.
public sealed partial class MainVm
{
    private static readonly TimeSpan SyncStartDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SyncCheckEvery = TimeSpan.FromHours(1);

    private readonly RecSyncVm _sync = new();
    private RecSvc? _baseCatalog;
    private CancellationTokenSource? _syncStop;
    private AsyncCmd? _syncNowCmd;
    private bool _syncWired;

    public RecSyncVm Sync => _sync;

    public AsyncCmd SyncNowCmd => _syncNowCmd ??= new AsyncCmd(
        () => RunRecSyncAsync(manual: true),
        () => !_disposed && _baseCatalog is not null && !_sync.IsBusy && _flow.IsRecSyncAvailable);

    // Called when the shipped catalog is (re)loaded. Reads the local layer
    // only; no network request is made.
    private void ApplyLocalRecSync()
    {
        WireRecSync();
        _baseCatalog = _catalog;
        if (_baseCatalog is null)
        {
            return;
        }

        var local = _flow.ReadRecSync(_baseCatalog);
        if (local is null)
        {
            return;
        }

        _catalog = local.Effective;
        NoteSvc.SetSyncedWikiArabic(local.Data?.GitHubData?.Notes);
        _sync.Load(local.Data, local.Health, _catalog.Count, RecSyncMerge.SettingCount(_catalog), null);
    }

    public void StartRecSync()
    {
        if (_disposed || _syncStop is not null || !_flow.IsRecSyncAvailable)
        {
            return;
        }

        _syncStop = new CancellationTokenSource();
        var token = _syncStop.Token;
        _ = Task.Run(() => RecSyncLoopAsync(token), CancellationToken.None);
    }

    private void StopRecSync()
    {
        var stop = Interlocked.Exchange(ref _syncStop, null);
        if (stop is null)
        {
            return;
        }

        stop.Cancel();
        stop.Dispose();
    }

    private void WireRecSync()
    {
        if (_syncWired)
        {
            return;
        }

        _syncWired = true;
        _sync.LoadPreferences(_flow.LoadRecSyncPreferences());
        _sync.PrefChg += (_, _) =>
        {
            if (!_flow.SaveRecSyncPreferences(_sync.Preferences))
            {
                Log(UiText.GetText("Sync.PrefSaveFailed"));
            }
        };
    }

    private async Task RecSyncLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SyncStartDelay, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                if (RecommendationSyncUseCase.IsAutoSyncAllowed(_sync.Preferences) &&
                    RecommendationSyncUseCase.IsDue(_sync.Data, DateTimeOffset.UtcNow))
                {
                    // No UI thread means the window is gone and there is no
                    // bound state left to refresh, so the loop ends.
                    if (!await UiThread.InvokeAsync(
                            () => RunRecSyncAsync(manual: false))
                        .ConfigureAwait(false))
                    {
                        return;
                    }
                }

                await Task.Delay(SyncCheckEvery, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunRecSyncAsync(bool manual)
    {
        if (_disposed || _baseCatalog is not { } baseline || _sync.IsBusy)
        {
            return;
        }

        _sync.IsBusy = true;
        SyncNowCmd.RaiseCan();
        var token = _syncStop?.Token ?? CancellationToken.None;
        var install = _install;

        try
        {
            var report = await Task.Run(() => _flow.SyncRecommendationsAsync(baseline, manual, install, token), token);
            var local = await Task.Run(() => _flow.ReadRecSync(baseline), token);
            if (_disposed || local is null || !ReferenceEquals(baseline, _baseCatalog))
            {
                return;
            }

            _catalog = local.Effective;
            NoteSvc.SetSyncedWikiArabic(local.Data?.GitHubData?.Notes);
            SetCat();
            _sync.Load(local.Data, local.Health, _catalog.Count, RecSyncMerge.SettingCount(_catalog), report);
            Log(UiText.Fmt(
                $"Sync.Run.{report.Status}",
                report.Applied.ToString(System.Globalization.CultureInfo.CurrentCulture),
                report.Quarantined.ToString(System.Globalization.CultureInfo.CurrentCulture)));

            var notices = RecommendationSyncUseCase.SelectNotifications(
                _sync.Preferences,
                local.Data,
                report.NewChanges,
                GameList.Games.Select(game => game.Serial));
            if (notices.Count > 0)
            {
                Status = UiText.Fmt("Sync.Notice", notices.Select(change => change.Serial).Distinct(StringComparer.Ordinal).Count());
                await _flow.MarkRecSyncNotifiedAsync(baseline, [.. notices.Select(change => change.Id)], token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!_disposed)
            {
                Log(UiText.Fmt("Sync.Failed", ex.Message));
            }
        }
        finally
        {
            _sync.IsBusy = false;
            SyncNowCmd.RaiseCan();
        }
    }
}
