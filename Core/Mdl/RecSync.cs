using System;
using System.Collections.Generic;

namespace R3Config.Mdl;

// Where a recommendation came from. Baseline is the shipped, read-only
// cat.json; Sync is a local update layered on top of it. Sync changes are
// never auto-selected in a configuration plan.
public enum RecOrigin
{
    Baseline,
    Sync
}

public enum RecSyncSource
{
    ConfigApi,
    Wiki,
    GitHub
}

public enum RecSyncSourceStatus
{
    NeverChecked,
    Updated,
    Unchanged,
    Blocked,
    RateLimited,
    Unavailable,
    Invalid,
    Cancelled,
    Skipped,
    NotPublished
}

public enum RecSyncChangeKind
{
    Added,
    Updated,
    Removed
}

public enum RecSyncDisposition
{
    Applied,
    Quarantined
}

public enum RecSyncReason
{
    None,
    UnsupportedSetting,
    InvalidValue,
    UnknownSerial,
    NonPrimaryBinding,
    ScopeExcluded,
    BaselineConflict,
    SourceConflict,
    PageConflict,
    // The game's API config carries a setting R3Config cannot write, so the
    // rest of that config is not applied on its own.
    PartialConfig
}

public enum RecSyncRunStatus
{
    Completed,
    Partial,
    Failed,
    AlreadyRunning,
    Cancelled,
    StoreReadOnly
}

public enum RecSyncStoreHealth
{
    Fresh,
    Loaded,
    RecoveredFromBackup,
    Reset,
    NewerSchema
}

public enum RecSyncNotifications
{
    Silent,
    ImportantForMyGames
}

public sealed record RecSyncSetting(
    string Section,
    string Key,
    string Value);

public sealed record RecSyncApiGame(
    string Serial,
    IReadOnlyList<RecSyncSetting> Settings);

// One reported difference between a source and the effective catalog. The
// Id is derived from the content and the source revision, so the same
// change is recorded and notified once.
public sealed record RecSyncChange(
    string Id,
    RecSyncSource Source,
    RecSyncChangeKind Kind,
    string Serial,
    string? Title,
    string Section,
    string Key,
    string? OldValue,
    string? NewValue,
    RecSyncDisposition Disposition,
    RecSyncReason Reason,
    string? RevisionId,
    string SourceUrl,
    DateTimeOffset ObservedAtUtc);

public sealed record RecSyncSourceState(
    RecSyncSource Source,
    RecSyncSourceStatus Status,
    DateTimeOffset? LastAttemptUtc,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? NextAttemptUtc,
    int Failures,
    string? ETag,
    string? LastModified,
    string? Sha256,
    string? RevisionId,
    DateTimeOffset? Watermark,
    string? Detail)
{
    public static RecSyncSourceState Initial(RecSyncSource source) =>
        new(source, RecSyncSourceStatus.NeverChecked, null, null, null, 0, null, null, null, null, null, null);
}

// A confirmed setting state from the official config API. Value null is a
// confirmed removal (tombstone).
public sealed record RecSyncOverride(
    string Serial,
    string Section,
    string Key,
    string? Value,
    string? RevisionId,
    DateTimeOffset ObservedAtUtc);

// The parsed state of one Wiki page at a newer revision than the baseline.
public sealed record RecSyncWikiPage(
    string Title,
    long? PageId,
    string RevisionId,
    DateTimeOffset RevisionTimestamp,
    IReadOnlyList<WikiRec> Recommendations);

public sealed record RecSyncStoreData(
    int Schema,
    string BaselineHash,
    string BaselineVersion,
    IReadOnlyList<RecSyncSourceState> Sources,
    IReadOnlyList<RecSyncApiGame>? ApiSnapshot,
    IReadOnlyList<RecSyncOverride> Overrides,
    IReadOnlyList<RecSyncWikiPage> WikiPages,
    IReadOnlyList<RecSyncChange> History,
    IReadOnlyList<RecSyncChange> Quarantine,
    IReadOnlyList<string> NotifiedIds,
    IReadOnlyList<string> SeenWikiRevisions,
    RecSyncGitHubData? GitHubData = null)
{
    public const int CurrentSchema = 1;

    public static RecSyncStoreData Empty(string baselineHash, string baselineVersion) =>
        new(
            CurrentSchema,
            baselineHash,
            baselineVersion,
            [RecSyncSourceState.Initial(RecSyncSource.ConfigApi), RecSyncSourceState.Initial(RecSyncSource.Wiki), RecSyncSourceState.Initial(RecSyncSource.GitHub)],
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            null);

    public RecSyncSourceState State(RecSyncSource source)
    {
        foreach (var state in Sources)
        {
            if (state.Source == source)
            {
                return state;
            }
        }

        return RecSyncSourceState.Initial(source);
    }
}

public sealed record RecSyncGitHubNote(string SourceEnglish, string NoteArabic);

public sealed record RecSyncGitHubData(
    string Version,
    DateTimeOffset GeneratedAtUtc,
    string ManifestSha256,
    IReadOnlyList<WikiRec> Recommendations,
    IReadOnlyList<RecSyncGitHubNote> Notes);

public sealed record RecSyncPreferences(
    bool AutoUpdate,
    RecSyncNotifications Notifications)
{
    public static RecSyncPreferences Default { get; } = new(true, RecSyncNotifications.Silent);
}

public sealed record RecSyncSourceReport(
    RecSyncSource Source,
    RecSyncSourceStatus Status,
    string? Detail);

public sealed record RecSyncReport(
    RecSyncRunStatus Status,
    IReadOnlyList<RecSyncSourceReport> Sources,
    IReadOnlyList<RecSyncChange> NewChanges,
    int GamesBefore,
    int GamesAfter,
    int SettingsBefore,
    int SettingsAfter,
    DateTimeOffset FinishedAtUtc)
{
    public int Applied => Count(RecSyncDisposition.Applied);

    public int Quarantined => Count(RecSyncDisposition.Quarantined);

    private int Count(RecSyncDisposition disposition)
    {
        var count = 0;
        foreach (var change in NewChanges)
        {
            if (change.Disposition == disposition)
            {
                count++;
            }
        }

        return count;
    }
}
