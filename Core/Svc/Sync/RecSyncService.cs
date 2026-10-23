using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Mdl;

namespace R3Config.Svc;

// Runs one recommendation sync at a time. Each source is fetched and merged
// independently: a blocked or failing source keeps its previous data and
// state, while the other sources can still update. The store, including
// every cursor and watermark, is written once, after all processing
// succeeded; a cancelled or failed run leaves the last good store.
// This service only reads the network and writes its own LocalAppData
// store. Of RPCS3's files it only reads GuiConfigs/config_database.dat, as a
// fallback copy of the config API; it never writes any RPCS3 file and never
// opens a configuration, patch or game file.
public sealed class RecSyncService : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(24);
    public static readonly TimeSpan MinRateLimitWait = TimeSpan.FromMinutes(5);
    public const int MaxNotified = 5000;
    public const int MaxSeenRevisions = 5000;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly RecSyncStore _store;
    private readonly RecSyncHttp _http;
    private readonly WikiRevisionSource _wiki;
    private readonly GitHubRecDataSource _github;
    private readonly Func<DateTimeOffset> _clock;

    public RecSyncService(
        RecSyncStore store,
        RecSyncHttp http,
        WikiRevisionSource wiki,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _wiki = wiki ?? throw new ArgumentNullException(nameof(wiki));
        _github = new GitHubRecDataSource(http);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsRunning => _lock.CurrentCount == 0;

    public RecSyncStore Store => _store;

    public void Dispose()
    {
        _lock.Dispose();
        _http.Dispose();
    }

    // True when an automatic run should start: the last successful check of
    // any source is older than the interval and no source asked to wait.
    public static bool IsDue(RecSyncStoreData? store, DateTimeOffset now)
    {
        if (store is null)
        {
            return true;
        }

        return store.Sources.Any(state =>
            (state.NextAttemptUtc is null || state.NextAttemptUtc <= now) &&
            (state.LastAttemptUtc is null || now - state.LastAttemptUtc.Value >= Interval));
    }

    public Task<RecSyncReport> SyncAsync(
        RecSvc baseline,
        bool manual,
        CancellationToken cancellationToken) =>
        SyncAsync(baseline, manual, null, cancellationToken);

    // localDatabase is RPCS3's own copy of the config API response
    // (GuiConfigs/config_database.dat). It is opened read-only and used only
    // when the official request yields no usable response, and only if its
    // timestamp is newer than the accepted snapshot.
    public async Task<RecSyncReport> SyncAsync(
        RecSvc baseline,
        bool manual,
        string? localDatabase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        if (!await _lock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Report(RecSyncRunStatus.AlreadyRunning, [], [], baseline, baseline);
        }

        try
        {
            var load = _store.Load();
            if (load.Health == RecSyncStoreHealth.NewerSchema)
            {
                return Report(RecSyncRunStatus.StoreReadOnly, [], [], baseline, baseline);
            }

            var store = Prepare(load.Data, baseline);
            var before = RecSyncMerge.BuildEffective(baseline, store);
            var history = store.History.Select(change => change.Id).ToHashSet(StringComparer.Ordinal);
            var reports = new List<RecSyncSourceReport>();
            var changes = new List<RecSyncChange>();

            store = await SyncConfigApiAsync(baseline, store, manual, localDatabase, reports, changes, cancellationToken).ConfigureAwait(false);
            store = await SyncWikiAsync(baseline, store, manual, reports, changes, cancellationToken).ConfigureAwait(false);
            store = await SyncGitHubAsync(store, manual, reports, cancellationToken).ConfigureAwait(false);

            var fresh = changes
                .Where(change => history.Add(change.Id))
                .ToArray();
            store = store with
            {
                History = [.. store.History.Concat(fresh).TakeLast(RecSyncMerge.MaxHistory)]
            };

            cancellationToken.ThrowIfCancellationRequested();
            _store.Save(store, cancellationToken);

            var after = RecSyncMerge.BuildEffective(baseline, store);
            var ok = reports.Count(report => report.Status is RecSyncSourceStatus.Updated or RecSyncSourceStatus.Unchanged);
            var failed = reports.Count(report => report.Status is RecSyncSourceStatus.Blocked or RecSyncSourceStatus.RateLimited or RecSyncSourceStatus.Unavailable or RecSyncSourceStatus.Invalid or RecSyncSourceStatus.NotPublished);
            var status = failed == 0
                ? RecSyncRunStatus.Completed
                : ok > 0 ? RecSyncRunStatus.Partial : RecSyncRunStatus.Failed;

            return Report(status, reports, fresh, before, after);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Report(RecSyncRunStatus.Cancelled, [], [], baseline, baseline);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Reads the local layer without any network access. A store from an older
    // baseline is re-targeted in memory; the file is updated on the next run.
    public (RecSyncStoreData? Data, RecSyncStoreHealth Health) Read(RecSvc baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var load = _store.Load();
        return load.Data is null
            ? (null, load.Health)
            : (Prepare(load.Data, baseline), load.Health);
    }

    public async Task<bool> MarkNotifiedAsync(
        RecSvc baseline,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0 || !await _lock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            var load = _store.Load();
            if (load.Data is null)
            {
                return false;
            }

            var store = Prepare(load.Data, baseline);
            _store.Save(store with
            {
                NotifiedIds = [.. store.NotifiedIds.Concat(ids).Distinct(StringComparer.Ordinal).TakeLast(MaxNotified)]
            }, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static RecSyncStoreData Prepare(RecSyncStoreData? data, RecSvc baseline)
    {
        if (data is null)
        {
            return RecSyncStoreData.Empty(baseline.Info.SrcHash, baseline.Info.DataVer);
        }

        return string.Equals(data.BaselineHash, baseline.Info.SrcHash, StringComparison.OrdinalIgnoreCase)
            ? data
            : RecSyncMerge.Rebase(data, baseline);
    }

    private async Task<RecSyncStoreData> SyncConfigApiAsync(
        RecSvc baseline,
        RecSyncStoreData store,
        bool manual,
        string? localDatabase,
        List<RecSyncSourceReport> reports,
        List<RecSyncChange> changes,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        var state = store.State(RecSyncSource.ConfigApi);
        if (ShouldWait(state, manual, now))
        {
            reports.Add(new RecSyncSourceReport(RecSyncSource.ConfigApi, RecSyncSourceStatus.Skipped, state.Detail));
            return store;
        }

        var result = await _http
            .GetAsync(new Uri(RecSyncMerge.ConfigApiUrl), ConfigApiParser.MaxResponseBytes, "application/json", state.ETag, state.LastModified, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == RecSyncHttpStatus.NotModified)
        {
            return Succeeded(store, state, RecSyncSourceStatus.Unchanged, now, result.ETag, result.LastModified, state.Sha256, state.RevisionId, state.Watermark, "Not modified.", reports);
        }

        if (result.Status != RecSyncHttpStatus.Ok)
        {
            return FailedOrLocal(baseline, store, state, result.Status, result.Detail, result.RetryAfter, now, localDatabase, reports, changes);
        }

        var hash = Convert.ToHexString(SHA256.HashData(result.Body));
        if (store.ApiSnapshot is not null && string.Equals(hash, state.Sha256, StringComparison.Ordinal))
        {
            return Succeeded(store, state, RecSyncSourceStatus.Unchanged, now, result.ETag, result.LastModified, hash, state.RevisionId, state.Watermark, "Same content.", reports);
        }

        if (!TryAccept(baseline, store, state, result.Body, requireNewer: false, now, out var snapshot, out var merged, out var rejected))
        {
            return FailedOrLocal(baseline, store, state, RecSyncHttpStatus.Rejected, rejected, null, now, localDatabase, reports, changes);
        }

        changes.AddRange(merged.Applied);
        changes.AddRange(merged.Quarantine);
        return Succeeded(
            merged.Store,
            state,
            merged.Applied.Count > 0 ? RecSyncSourceStatus.Updated : RecSyncSourceStatus.Unchanged,
            now,
            result.ETag,
            result.LastModified,
            hash,
            snapshot.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.FromUnixTimeSeconds(snapshot.Timestamp),
            snapshot.Issues.Count == 0 ? "OK." : $"{snapshot.Issues.Count} game entries were unreadable and kept unchanged.",
            reports);
    }

    private async Task<RecSyncStoreData> SyncWikiAsync(
        RecSvc baseline,
        RecSyncStoreData store,
        bool manual,
        List<RecSyncSourceReport> reports,
        List<RecSyncChange> changes,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        var state = store.State(RecSyncSource.Wiki);
        if (ShouldWait(state, manual, now))
        {
            reports.Add(new RecSyncSourceReport(RecSyncSource.Wiki, RecSyncSourceStatus.Skipped, state.Detail));
            return store;
        }

        // The shipped catalog is a snapshot of the Wiki at its generation
        // time, so recent changes start there on the first run.
        var watermark = state.Watermark ?? baseline.Info.GenAt;
        var fetch = await _wiki
            .FetchAsync(watermark, store.SeenWikiRevisions, now, cancellationToken)
            .ConfigureAwait(false);

        if (fetch.Status != RecSyncHttpStatus.Ok)
        {
            return Failed(store, state, fetch.Status, fetch.Detail, fetch.RetryAfter, now, reports);
        }

        IReadOnlyList<WikiLivePageResult> parsed;
        try
        {
            parsed = WikiSvc.ParseLivePages(fetch.Pages);
        }
        catch (InvalidDataException ex)
        {
            return Failed(store, state, RecSyncHttpStatus.Rejected, ex.Message, null, now, reports);
        }

        var merged = RecSyncMerge.ApplyWikiPages(baseline, store, parsed, now);
        changes.AddRange(merged.Applied);
        changes.AddRange(merged.Quarantine);

        var next = merged.Store with
        {
            SeenWikiRevisions = fetch.FullReconciliation
                ? []
                : [.. store.SeenWikiRevisions.Concat(fetch.ProcessedRevisions).Distinct(StringComparer.Ordinal).TakeLast(MaxSeenRevisions)]
        };

        return Succeeded(
            next,
            state,
            merged.Applied.Count > 0 ? RecSyncSourceStatus.Updated : RecSyncSourceStatus.Unchanged,
            now,
            null,
            null,
            null,
            fetch.Pages.Count == 0
                ? state.RevisionId
                : fetch.Pages
                    .Select(page => long.Parse(page.RevisionId, System.Globalization.CultureInfo.InvariantCulture))
                    .Max()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
            fetch.Watermark ?? watermark,
            $"{fetch.Detail} {fetch.Pages.Count} pages checked.",
            reports);
    }

    private async Task<RecSyncStoreData> SyncGitHubAsync(
        RecSyncStoreData store,
        bool manual,
        List<RecSyncSourceReport> reports,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        var state = store.State(RecSyncSource.GitHub);
        if (ShouldWait(state, manual, now))
        {
            reports.Add(new RecSyncSourceReport(RecSyncSource.GitHub, RecSyncSourceStatus.Skipped, state.Detail));
            return store;
        }

        var fetched = await _github.FetchAsync(state.Sha256, state.RevisionId, cancellationToken).ConfigureAwait(false);
        if (fetched.Status is RecSyncSourceStatus.Updated or RecSyncSourceStatus.Unchanged)
        {
            var next = fetched.Data is null ? store : store with { GitHubData = fetched.Data };
            return Succeeded(next, state, fetched.Status, now, null, null, fetched.ManifestSha256 ?? state.Sha256,
                fetched.Data?.Version ?? state.RevisionId, null, fetched.Detail, reports);
        }

        if (fetched.Status == RecSyncSourceStatus.NotPublished)
        {
            var failures = Math.Min(state.Failures + 1, 16);
            var wait = TimeSpan.FromHours(Math.Min(Math.Pow(2, failures - 1), MaxBackoff.TotalHours));
            const string detail = "GitHub recommendation data has not been published yet.";
            reports.Add(new RecSyncSourceReport(RecSyncSource.GitHub, RecSyncSourceStatus.NotPublished, detail));
            return WithState(store, state with
            {
                Status = RecSyncSourceStatus.NotPublished,
                LastAttemptUtc = now,
                NextAttemptUtc = now + wait,
                Failures = failures,
                Detail = detail
            });
        }

        var httpStatus = fetched.Status switch
        {
            RecSyncSourceStatus.Blocked => RecSyncHttpStatus.Blocked,
            RecSyncSourceStatus.RateLimited => RecSyncHttpStatus.RateLimited,
            RecSyncSourceStatus.Invalid => RecSyncHttpStatus.Rejected,
            _ => RecSyncHttpStatus.NetworkError
        };
        return Failed(store, state, httpStatus, fetched.Detail, null, now, reports);
    }

    // Parses and merges one complete config API body. The response timestamp
    // is the export time, so a body older than the accepted snapshot (or not
    // newer, when requireNewer) is refused instead of rolling recommendations
    // back; so is a sudden mass removal, which is more likely a broken export
    // than real edits. A refusal keeps the last good state.
    private static bool TryAccept(
        RecSvc baseline,
        RecSyncStoreData store,
        RecSyncSourceState state,
        byte[] body,
        bool requireNewer,
        DateTimeOffset now,
        [NotNullWhen(true)] out ConfigApiSnapshot? snapshot,
        [NotNullWhen(true)] out RecSyncMergeResult? merged,
        out string rejected)
    {
        snapshot = null;
        merged = null;
        rejected = string.Empty;

        ConfigApiSnapshot parsed;
        try
        {
            parsed = ConfigApiParser.Parse(body);
        }
        catch (InvalidDataException ex)
        {
            rejected = ex.Message;
            return false;
        }

        if (store.ApiSnapshot is not null &&
            long.TryParse(state.RevisionId, NumberStyles.None, CultureInfo.InvariantCulture, out var accepted) &&
            (parsed.Timestamp < accepted || (requireNewer && parsed.Timestamp == accepted)))
        {
            rejected = $"The response timestamp {parsed.Timestamp.ToString(CultureInfo.InvariantCulture)} is not newer than the accepted snapshot {accepted.ToString(CultureInfo.InvariantCulture)}; it was not applied.";
            return false;
        }

        // Without an accepted snapshot, a copy that must prove itself newer
        // has to be newer than the shipped baseline.
        if (requireNewer &&
            store.ApiSnapshot is null &&
            baseline.Info.GenAt is { } generated &&
            DateTimeOffset.FromUnixTimeSeconds(parsed.Timestamp) <= generated)
        {
            rejected = "The copy is not newer than the shipped baseline; it was not applied.";
            return false;
        }

        var result = RecSyncMerge.ApplyConfigApi(baseline, store, parsed, now);
        var limit = Math.Max(50, (store.ApiSnapshot?.Sum(game => game.Settings.Count) ?? 0) / 20);
        if (result.Removals > limit)
        {
            rejected = $"The response removes {result.Removals} settings at once; it was not applied.";
            return false;
        }

        snapshot = parsed;
        merged = result;
        return true;
    }

    // The official request gave no usable response. RPCS3 stores the last
    // response it validated verbatim in GuiConfigs/config_database.dat; that
    // copy is read (never written) and used only when it differs from the
    // accepted snapshot and its own timestamp is strictly newer. The source
    // still reports the failure and keeps its retry schedule.
    private static RecSyncStoreData FailedOrLocal(
        RecSvc baseline,
        RecSyncStoreData store,
        RecSyncSourceState state,
        RecSyncHttpStatus http,
        string detail,
        TimeSpan? retryAfter,
        DateTimeOffset now,
        string? localDatabase,
        List<RecSyncSourceReport> reports,
        List<RecSyncChange> changes)
    {
        if (ReadLocalDatabase(localDatabase) is { } body)
        {
            var hash = Convert.ToHexString(SHA256.HashData(body));
            if (!(store.ApiSnapshot is not null && string.Equals(hash, state.Sha256, StringComparison.Ordinal)) &&
                TryAccept(baseline, store, state, body, requireNewer: true, now, out var snapshot, out var merged, out _))
            {
                changes.AddRange(merged.Applied);
                changes.AddRange(merged.Quarantine);
                var exported = DateTimeOffset.FromUnixTimeSeconds(snapshot.Timestamp);
                return Failed(
                    merged.Store,
                    state with
                    {
                        Sha256 = hash,
                        RevisionId = snapshot.Timestamp.ToString(CultureInfo.InvariantCulture),
                        Watermark = exported
                    },
                    http,
                    $"{detail} Used RPCS3's local {ConfigApiParser.LocalDatabaseFileName} exported {exported.ToString("u", CultureInfo.InvariantCulture)}.",
                    retryAfter,
                    now,
                    reports);
            }
        }

        return Failed(store, state, http, detail, retryAfter, now, reports);
    }

    private static byte[]? ReadLocalDatabase(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return R3Bound.ReadBytes(path, ConfigApiParser.MaxResponseBytes, ConfigApiParser.LocalDatabaseFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool ShouldWait(RecSyncSourceState state, bool manual, DateTimeOffset now)
    {
        if (state.NextAttemptUtc is not { } next || next <= now)
        {
            return false;
        }

        // A manual check skips local back-off, but a source that asked us to
        // slow down (429 / Retry-After) is always respected.
        return !manual || state.Status == RecSyncSourceStatus.RateLimited;
    }

    private static RecSyncStoreData Succeeded(
        RecSyncStoreData store,
        RecSyncSourceState state,
        RecSyncSourceStatus status,
        DateTimeOffset now,
        string? etag,
        string? lastModified,
        string? hash,
        string? revision,
        DateTimeOffset? watermark,
        string detail,
        List<RecSyncSourceReport> reports)
    {
        reports.Add(new RecSyncSourceReport(state.Source, status, detail));
        return WithState(store, state with
        {
            Status = status,
            LastAttemptUtc = now,
            LastSuccessUtc = now,
            NextAttemptUtc = null,
            Failures = 0,
            ETag = etag,
            LastModified = lastModified,
            Sha256 = hash,
            RevisionId = revision,
            Watermark = watermark,
            Detail = Trim(detail)
        });
    }

    private static RecSyncStoreData Failed(
        RecSyncStoreData store,
        RecSyncSourceState state,
        RecSyncHttpStatus http,
        string detail,
        TimeSpan? retryAfter,
        DateTimeOffset now,
        List<RecSyncSourceReport> reports)
    {
        var status = http switch
        {
            RecSyncHttpStatus.Blocked => RecSyncSourceStatus.Blocked,
            RecSyncHttpStatus.RateLimited => RecSyncSourceStatus.RateLimited,
            RecSyncHttpStatus.Rejected or RecSyncHttpStatus.TooLarge => RecSyncSourceStatus.Invalid,
            _ => RecSyncSourceStatus.Unavailable
        };
        var failures = Math.Min(state.Failures + 1, 16);
        var backoff = TimeSpan.FromHours(Math.Min(Math.Pow(2, failures - 1), MaxBackoff.TotalHours));
        var wait = status switch
        {
            RecSyncSourceStatus.RateLimited => retryAfter is { } after && after > MinRateLimitWait ? after : MinRateLimitWait,
            RecSyncSourceStatus.Blocked => MaxBackoff,
            _ => backoff
        };

        reports.Add(new RecSyncSourceReport(state.Source, status, Trim(detail)));
        return WithState(store, state with
        {
            Status = status,
            LastAttemptUtc = now,
            NextAttemptUtc = now + wait,
            Failures = failures,
            Detail = Trim(detail)
        });
    }

    private static RecSyncStoreData WithState(RecSyncStoreData store, RecSyncSourceState state) =>
        store with
        {
            Sources = [.. store.Sources
                .Where(item => item.Source != state.Source)
                .Append(state)
                .OrderBy(item => item.Source)]
        };

    private static string Trim(string detail) =>
        detail.Length <= 500 ? detail : detail[..500];

    private RecSyncReport Report(
        RecSyncRunStatus status,
        IReadOnlyList<RecSyncSourceReport> sources,
        IReadOnlyList<RecSyncChange> changes,
        RecSvc before,
        RecSvc after) =>
        new(
            status,
            sources,
            changes,
            before.Count,
            after.Count,
            RecSyncMerge.SettingCount(before),
            RecSyncMerge.SettingCount(after),
            _clock());
}
