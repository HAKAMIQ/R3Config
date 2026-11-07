using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class RecSyncSourceRowVm(
    string name,
    string status,
    string lastSuccess,
    string detail)
{
    public string Name { get; } = name;

    public string Status { get; } = status;

    public string LastSuccess { get; } = lastSuccess;

    public string Detail { get; } = detail;
}

public sealed class RecSyncChangeRowVm(RecSyncChange change)
{
    public string Serial { get; } = change.Serial;

    public string Title { get; } = change.Title ?? string.Empty;

    public string Setting { get; } = change.Key.Length == 0
        ? UiText.GetText("Sync.GameEntry")
        : $"{change.Section} / {change.Key}";

    public string OldValue { get; } = change.OldValue ?? "—";

    public string NewValue { get; } = change.NewValue ?? "—";

    public string Kind { get; } = UiText.GetText($"Sync.Kind.{change.Kind}");

    public string State { get; } = change.Disposition == RecSyncDisposition.Applied
        ? UiText.GetText("Sync.Disposition.Applied")
        : $"{UiText.GetText("Sync.Disposition.Quarantined")}: {UiText.GetText($"Sync.Reason.{change.Reason}")}";

    public bool IsQuarantined { get; } = change.Disposition == RecSyncDisposition.Quarantined;

    public string Source { get; } = UiText.GetText($"Sync.Source.{change.Source}");

    public string SourceUrl { get; } = change.SourceUrl;

    public string Observed { get; } = UiDate.Text(change.ObservedAtUtc);
}

// Presentation state for Advanced > Recommendation updates. It holds no
// service: MainVm loads it from AppFlow results.
public sealed class RecSyncVm : VmBase
{
    public const int MaxRows = 400;

    private bool _autoUpdate = true;
    private bool _notifyMyGames;
    private bool _isBusy;
    private bool _loading;
    private string _lastSuccess = "-";
    private string _runStatus = string.Empty;
    private string _counts = string.Empty;
    private string _storeNote = string.Empty;
    private RecSyncStoreData? _data;
    private RecSyncReport? _report;
    private RecSyncStoreHealth _health = RecSyncStoreHealth.Fresh;
    private int _games;
    private int _settings;

    public event EventHandler? PrefChg;

    // Clock used for the Wiki freshness state; replaced only by tests.
    public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    public ObservableCollection<RecSyncSourceRowVm> Sources { get; } = [];

    public ObservableCollection<RecSyncChangeRowVm> Changes { get; } = [];

    public bool AutoUpdate
    {
        get => _autoUpdate;
        set
        {
            if (Set(ref _autoUpdate, value) && !_loading)
            {
                PrefChg?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool NotifyMyGames
    {
        get => _notifyMyGames;
        set
        {
            if (Set(ref _notifyMyGames, value) && !_loading)
            {
                PrefChg?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public RecSyncPreferences Preferences =>
        new(_autoUpdate, _notifyMyGames ? RecSyncNotifications.ImportantForMyGames : RecSyncNotifications.Silent);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
            {
                OnChange(nameof(CheckText));
            }
        }
    }

    public string CheckText => UiText.GetText(_isBusy ? "Sync.Checking" : "Sync.CheckNow");

    public string LastSuccess
    {
        get => _lastSuccess;
        private set => Set(ref _lastSuccess, value);
    }

    public string RunStatus
    {
        get => _runStatus;
        private set => Set(ref _runStatus, value);
    }

    public string Counts
    {
        get => _counts;
        private set => Set(ref _counts, value);
    }

    public string StoreNote
    {
        get => _storeNote;
        private set
        {
            if (Set(ref _storeNote, value))
            {
                OnChange(nameof(HasStoreNote));
            }
        }
    }

    public bool HasStoreNote => _storeNote.Length > 0;

    public bool HasChanges => Changes.Count > 0;

    public bool HasNoChanges => Changes.Count == 0;

    public RecSyncStoreData? Data => _data;

    public void LoadPreferences(RecSyncPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        _loading = true;
        try
        {
            AutoUpdate = preferences.AutoUpdate;
            NotifyMyGames = preferences.Notifications == RecSyncNotifications.ImportantForMyGames;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Load(
        RecSyncStoreData? data,
        RecSyncStoreHealth health,
        int games,
        int settings,
        RecSyncReport? report)
    {
        _data = data;
        _health = health;
        _games = games;
        _settings = settings;
        _report = report ?? _report;
        Refresh();
    }

    public void SetLang()
    {
        Refresh();
        OnChange(nameof(CheckText));
    }

    private void Refresh()
    {
        var states = new[] { RecSyncSource.ConfigApi, RecSyncSource.Wiki, RecSyncSource.GitHub }
            .Select(source => _data?.State(source) ?? RecSyncSourceState.Initial(source))
            .ToArray();

        Sources.Clear();
        foreach (var state in states)
        {
            Sources.Add(new RecSyncSourceRowVm(
                UiText.GetText($"Sync.Source.{state.Source}"),
                StatusText(state),
                Time(state.LastSuccessUtc),
                state.Status == RecSyncSourceStatus.NotPublished ? string.Empty : state.Detail ?? string.Empty));
        }

        LastSuccess = Time(states
            .Select(state => state.LastSuccessUtc)
            .Where(value => value is not null)
            .DefaultIfEmpty(null)
            .Max());

        var applied = _data?.Overrides.Count ?? 0;
        var pages = _data?.WikiPages.Count ?? 0;
        var quarantined = _data?.Quarantine.Count ?? 0;
        Counts = UiText.Fmt(
            "Sync.Counts",
            _games.ToString(CultureInfo.CurrentCulture),
            _settings.ToString(CultureInfo.CurrentCulture),
            applied.ToString(CultureInfo.CurrentCulture),
            pages.ToString(CultureInfo.CurrentCulture),
            quarantined.ToString(CultureInfo.CurrentCulture));

        RunStatus = _report is null
            ? string.Empty
            : UiText.Fmt(
                $"Sync.Run.{_report.Status}",
                _report.Applied.ToString(CultureInfo.CurrentCulture),
                _report.Quarantined.ToString(CultureInfo.CurrentCulture));

        StoreNote = _health is RecSyncStoreHealth.RecoveredFromBackup or RecSyncStoreHealth.Reset or RecSyncStoreHealth.NewerSchema
            ? UiText.GetText($"Sync.Store.{_health}")
            : string.Empty;

        Changes.Clear();
        foreach (var change in (_data?.History ?? []).Reverse().Take(MaxRows))
        {
            Changes.Add(new RecSyncChangeRowVm(change));
        }

        OnChange(nameof(HasChanges));
        OnChange(nameof(HasNoChanges));
    }

    private const int StaleAfterDays = 30;

    // The Wiki row adds a Stale state with its date when the last successful
    // sync is older than 30 days. With no successful sync the date of the
    // embedded data (BaselineVersion, yyyy-MM-dd) is used; an unparsable
    // version never produces a Stale claim.
    private string StatusText(RecSyncSourceState state)
    {
        var status = UiText.GetText($"Sync.Status.{state.Status}");
        if (state.Source != RecSyncSource.Wiki)
        {
            return status;
        }

        var last = state.LastSuccessUtc;
        var embedded = last is null;
        last ??= EmbeddedDataDate();
        return last is { } value && Now() - value > TimeSpan.FromDays(StaleAfterDays)
            ? status + " | " + UiText.Fmt(
                "Sync.Wiki.Stale",
                embedded ? UiDate.DateInline(value) : UiDate.Inline(value))
            : status;
    }

    private DateTimeOffset? EmbeddedDataDate() =>
        _data is not null &&
        DateTime.TryParseExact(
            _data.BaselineVersion,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local))
            : null;

    private static string Time(DateTimeOffset? value) =>
        value is null
            ? UiText.GetText("Sync.Never")
            : UiDate.Text(value.Value);
}
