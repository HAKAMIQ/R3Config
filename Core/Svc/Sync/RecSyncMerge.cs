using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record RecSyncMergeResult(
    RecSyncStoreData Store,
    IReadOnlyList<RecSyncChange> Applied,
    IReadOnlyList<RecSyncChange> Quarantine,
    int Removals);

// Deterministic merge of the read-only baseline catalog with the local sync
// layer. The baseline is never edited: the effective catalog starts from the
// accepted GitHub snapshot when available, then applies confirmed Wiki page
// revisions and config API states.
// Anything that cannot be matched to a known Primary serial, a supported
// setting and a verified value domain stays quarantined and is not applied.
public static class RecSyncMerge
{
    public const string ConfigApiUrl = "https://api.rpcs3.net/config/?api=v1";
    public const string WikiPageUrl = "https://wiki.rpcs3.net/index.php?title=";
    public const int MaxHistory = 5000;

    public static RecSvc BuildEffective(
        RecSvc baseline,
        RecSyncStoreData? store)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        if (store is null ||
            (store.Overrides.Count == 0 && store.WikiPages.Count == 0 && store.GitHubData is null))
        {
            return baseline;
        }

        var sourceBase = store.GitHubData is { } github
            ? RecSvc.FromRec("HAKAMIQ/rpcs3", github.Version,
                github.Recommendations.Select(rec => rec with
                {
                    Changes = [.. rec.Changes.Select(change => change with { Origin = RecOrigin.Sync })]
                }), github.GeneratedAtUtc, github.ManifestSha256)
            : baseline;
        var map = Effective(sourceBase, store);
        return RecSvc.FromRec(
            sourceBase.Info.DataSrc,
            sourceBase.Info.DataVer,
            map.Values,
            sourceBase.Info.GenAt,
            sourceBase.Info.SrcHash);
    }

    public static RecSyncMergeResult ApplyConfigApi(
        RecSvc baseline,
        RecSyncStoreData store,
        ConfigApiSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(snapshot);

        var revision = snapshot.Timestamp.ToString(CultureInfo.InvariantCulture);
        var before = Effective(baseline, store);
        var baseMap = Index(baseline.Rec);
        var wiki = WikiOwned(baseline, store);
        var scope = PageKeys(baseline);
        var prev = store.ApiSnapshot is null ? null : ToMap(store.ApiSnapshot);
        var cur = ToMap(snapshot.Games);

        // A game the API listed with an unreadable config is unknown for this
        // run, not removed: keep the previously accepted state for it.
        if (prev is not null)
        {
            foreach (var issue in snapshot.Issues)
            {
                if (prev.TryGetValue(issue.Serial, out var kept))
                {
                    cur[issue.Serial] = kept;
                }
            }
        }

        // RPCS3 validates a game's config as a whole (cfg_root::validate): one
        // key the installed build does not know drops all of it. A config with
        // a setting R3Config cannot write is therefore never applied in part;
        // it is quarantined for review and its earlier overrides are dropped,
        // so the game falls back to the baseline.
        var partial = cur
            .Where(item => IsPartial(item.Value))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);

        var overrides = store.Overrides
            .Where(item => !partial.Contains(item.Serial))
            .ToDictionary(
                item => Id(item.Serial, item.Section, item.Key),
                StringComparer.Ordinal);
        var removals = 0;

        if (prev is null)
        {
            // First reconciliation against the baseline. The API omits values
            // the Wiki scopes to other serials and settings it does not carry,
            // so a missing value is never a removal and a different value is a
            // conflict for review, not an update.
            foreach (var (serial, settings) in cur)
            {
                if (partial.Contains(serial))
                {
                    continue;
                }

                foreach (var (path, value) in settings)
                {
                    var (section, key) = Split(path);
                    var baseValue = Value(baseMap, serial, section, key);
                    if (baseValue is not null ||
                        !WikiSvc.IsSyncValue(section, key, value) ||
                        Gate(before, wiki, scope, serial, section, key, value) != RecSyncReason.None)
                    {
                        continue;
                    }

                    overrides[Id(serial, section, key)] = new RecSyncOverride(serial, section, key, value, revision, now);
                }
            }
        }
        else
        {
            foreach (var serial in prev.Keys.Union(cur.Keys, StringComparer.Ordinal))
            {
                if (partial.Contains(serial))
                {
                    continue;
                }

                prev.TryGetValue(serial, out var oldSettings);
                cur.TryGetValue(serial, out var newSettings);
                if (oldSettings is not null && IsPartial(oldSettings))
                {
                    // Nothing of a partial config was applied, so compare the
                    // now complete config as new rather than as unchanged.
                    oldSettings = null;
                }

                var paths = (oldSettings?.Keys ?? Enumerable.Empty<string>())
                    .Union(newSettings?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

                foreach (var path in paths)
                {
                    string? previous = null;
                    string? current = null;
                    oldSettings?.TryGetValue(path, out previous);
                    newSettings?.TryGetValue(path, out current);
                    if (string.Equals(previous, current, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var (section, key) = Split(path);
                    if (!WikiSvc.TryGetSyncTarget(section, key, out _))
                    {
                        continue;
                    }

                    var id = Id(serial, section, key);
                    var baseValue = Value(baseMap, serial, section, key);

                    if (current is null)
                    {
                        removals++;
                        if (baseValue is not null && !string.Equals(baseValue, previous, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (baseValue is null)
                        {
                            overrides.Remove(id);
                        }
                        else if (before.ContainsKey(serial))
                        {
                            overrides[id] = new RecSyncOverride(serial, section, key, null, revision, now);
                        }

                        continue;
                    }

                    if (!WikiSvc.IsSyncValue(section, key, current) ||
                        Gate(before, wiki, scope, serial, section, key, current) != RecSyncReason.None ||
                        (baseValue is not null &&
                         !string.Equals(baseValue, previous, StringComparison.Ordinal) &&
                         !string.Equals(baseValue, current, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    if (string.Equals(baseValue, current, StringComparison.Ordinal))
                    {
                        overrides.Remove(id);
                    }
                    else
                    {
                        overrides[id] = new RecSyncOverride(serial, section, key, current, revision, now);
                    }
                }
            }
        }

        var next = store with
        {
            ApiSnapshot = [.. cur
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new RecSyncApiGame(
                    item.Key,
                    [.. item.Value
                        .OrderBy(setting => setting.Key, StringComparer.Ordinal)
                        .Select(setting =>
                        {
                            var (section, key) = Split(setting.Key);
                            return new RecSyncSetting(section, key, setting.Value);
                        })]))],
            Overrides = [.. overrides.Values
                .OrderBy(item => item.Serial, StringComparer.Ordinal)
                .ThenBy(item => item.Section, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal)]
        };

        var after = Effective(baseline, next);
        var applied = Diff(before, after, RecSyncSource.ConfigApi, _ => revision, _ => ConfigApiUrl, now);

        var quarantine = new List<RecSyncChange>();
        foreach (var (serial, settings) in cur)
        {
            foreach (var (path, value) in settings)
            {
                var (section, key) = Split(path);
                var effective = Value(after, serial, section, key);
                if (string.Equals(effective, value, StringComparison.Ordinal))
                {
                    continue;
                }

                var reason = !WikiSvc.TryGetSyncTarget(section, key, out _)
                    ? RecSyncReason.UnsupportedSetting
                    : !WikiSvc.IsSyncValue(section, key, value)
                        ? RecSyncReason.InvalidValue
                        : partial.Contains(serial)
                            ? RecSyncReason.PartialConfig
                            : Gate(after, wiki, scope, serial, section, key, value) is var gate && gate != RecSyncReason.None
                                ? gate
                                : RecSyncReason.BaselineConflict;

                quarantine.Add(Change(
                    RecSyncSource.ConfigApi,
                    effective is null ? RecSyncChangeKind.Added : RecSyncChangeKind.Updated,
                    serial,
                    Title(after, serial),
                    section,
                    key,
                    effective,
                    value,
                    RecSyncDisposition.Quarantined,
                    reason,
                    null,
                    ConfigApiUrl,
                    now));
            }
        }

        next = next with
        {
            Quarantine = [.. store.Quarantine
                .Where(item => item.Source != RecSyncSource.ConfigApi)
                .Concat(quarantine)
                .OrderBy(item => item.Serial, StringComparer.Ordinal)
                .ThenBy(item => item.Section, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal)]
        };

        return new RecSyncMergeResult(next, applied, quarantine, removals);
    }

    public static RecSyncMergeResult ApplyWikiPages(
        RecSvc baseline,
        RecSyncStoreData store,
        IReadOnlyList<WikiLivePageResult> pages,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(pages);

        var before = Effective(baseline, store);
        var baseMap = Index(baseline.Rec);
        var stored = store.WikiPages.ToDictionary(page => page.Title, StringComparer.Ordinal);
        var quarantine = store.Quarantine
            .Where(item => item.Source == RecSyncSource.Wiki)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var touched = new Dictionary<string, (string Revision, string Title)>(StringComparer.Ordinal);

        foreach (var result in pages)
        {
            var page = result.Page;
            if (!result.IsGameContent || page.Namespace != 0)
            {
                // A page without both gameid and config templates cannot be
                // mapped; its absence is not evidence that settings were removed.
                continue;
            }

            var title = page.Title.Trim();
            if (stored.TryGetValue(title, out var known) &&
                string.Equals(known.RevisionId, page.RevisionId, StringComparison.Ordinal))
            {
                continue;
            }

            var accepted = new List<WikiRec>();
            foreach (var rec in result.Recommendations)
            {
                if (baseMap.TryGetValue(rec.Serial, out var owner) &&
                    owner.Binding == WikiGameIdRole.Primary &&
                    owner.Changes.Count > 0 &&
                    !string.Equals(owner.WikiName, title, StringComparison.Ordinal))
                {
                    var conflict = Change(
                        RecSyncSource.Wiki,
                        RecSyncChangeKind.Updated,
                        rec.Serial,
                        title,
                        string.Empty,
                        string.Empty,
                        owner.WikiName,
                        title,
                        RecSyncDisposition.Quarantined,
                        RecSyncReason.PageConflict,
                        null,
                        WikiPageUrl + Uri.EscapeDataString(title),
                        now);
                    quarantine[conflict.Id] = conflict;
                    continue;
                }

                accepted.Add(rec);
            }

            var baselinePage = baseline.Rec
                .Where(rec => string.Equals(rec.WikiName, title, StringComparison.Ordinal))
                .ToArray();
            if (SameRecs(baselinePage, accepted))
            {
                // A text-only edit: no semantic change from the baseline.
                stored.Remove(title);
            }
            else
            {
                stored[title] = new RecSyncWikiPage(title, page.PageId, page.RevisionId, page.RevisionTimestamp, [.. accepted]);
            }

            foreach (var serial in accepted.Select(rec => rec.Serial).Concat(baselinePage.Select(rec => rec.Serial)))
            {
                touched[serial] = (page.RevisionId, title);
            }
        }

        var next = store with
        {
            WikiPages = [.. stored.Values.OrderBy(page => page.Title, StringComparer.Ordinal)],
            Quarantine = [.. store.Quarantine
                .Where(item => item.Source != RecSyncSource.Wiki)
                .Concat(quarantine.Values)
                .OrderBy(item => item.Serial, StringComparer.Ordinal)
                .ThenBy(item => item.Section, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal)]
        };

        var after = Effective(baseline, next);
        var applied = Diff(
            before,
            after,
            RecSyncSource.Wiki,
            serial => touched.TryGetValue(serial, out var item) ? item.Revision : null,
            serial => WikiPageUrl + Uri.EscapeDataString(
                touched.TryGetValue(serial, out var item) ? item.Title : Title(after, serial) ?? serial),
            now);

        return new RecSyncMergeResult(
            next,
            applied,
            [.. quarantine.Values],
            applied.Count(change => change.Kind == RecSyncChangeKind.Removed));
    }

    // Re-targets the local layer at a newer shipped baseline: entries the new
    // baseline already contains are dropped, the rest are kept.
    public static RecSyncStoreData Rebase(
        RecSyncStoreData store,
        RecSvc baseline)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(baseline);

        var baseMap = Index(baseline.Rec);
        var generated = baseline.Info.GenAt;

        return store with
        {
            BaselineHash = baseline.Info.SrcHash,
            BaselineVersion = baseline.Info.DataVer,
            Overrides = [.. store.Overrides.Where(item =>
            {
                var baseValue = Value(baseMap, item.Serial, item.Section, item.Key);
                return !string.Equals(baseValue, item.Value, StringComparison.Ordinal);
            })],
            WikiPages = [.. store.WikiPages.Where(page => generated is null || page.RevisionTimestamp > generated.Value)],
            Quarantine = [],
            // Re-read the config API in full next time so its quarantine is
            // recomputed against the new baseline.
            Sources = [.. store.Sources.Select(state => state.Source == RecSyncSource.ConfigApi
                ? state with { ETag = null, LastModified = null, Sha256 = null }
                : state)]
        };
    }

    public static int SettingCount(RecSvc catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Rec.Sum(rec => rec.Changes.Count);
    }

    public static RecSyncChange Change(
        RecSyncSource source,
        RecSyncChangeKind kind,
        string serial,
        string? title,
        string section,
        string key,
        string? oldValue,
        string? newValue,
        RecSyncDisposition disposition,
        RecSyncReason reason,
        string? revisionId,
        string sourceUrl,
        DateTimeOffset now)
    {
        var identity = string.Join(
            '\u001f',
            source.ToString(),
            kind.ToString(),
            disposition.ToString(),
            reason.ToString(),
            serial,
            section,
            key,
            oldValue ?? "∅",
            newValue ?? "∅",
            revisionId ?? string.Empty);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)), 0, 16);

        return new RecSyncChange(
            id,
            source,
            kind,
            serial,
            title,
            section,
            key,
            oldValue,
            newValue,
            disposition,
            reason,
            revisionId,
            sourceUrl,
            now);
    }

    private static Dictionary<string, WikiRec> Effective(
        RecSvc baseline,
        RecSyncStoreData store)
    {
        var map = Index(baseline.Rec);
        var wiki = WikiOwned(baseline, store);

        foreach (var (serial, rec) in wiki)
        {
            map.TryGetValue(serial, out var original);
            map[serial] = Mark(rec, original);
        }

        // Overrides saved before partial configs were quarantined may still
        // sit next to a partial snapshot; they are never applied.
        var partial = (store.ApiSnapshot ?? [])
            .Where(game => game.Settings.Any(setting => !WikiSvc.TryGetSyncTarget(setting.Section, setting.Key, out _)))
            .Select(game => game.Serial)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in store.Overrides)
        {
            if (partial.Contains(item.Serial) ||
                !map.TryGetValue(item.Serial, out var rec) ||
                !WikiSvc.TryGetSyncTarget(item.Section, item.Key, out var wikiKey))
            {
                continue;
            }

            if (wiki.TryGetValue(item.Serial, out var wikiRec))
            {
                var wikiValue = Find(wikiRec, item.Section, item.Key)?.Value;
                if (!string.Equals(wikiValue, item.Value, StringComparison.Ordinal))
                {
                    // Both sources changed this setting differently; keep the
                    // baseline value until they agree.
                    var original = baseline.Find(item.Serial);
                    var baseChange = original is null ? null : Find(original, item.Section, item.Key);
                    map[item.Serial] = Set(map[item.Serial], item.Section, item.Key, baseChange);
                    continue;
                }
            }

            CfgChg? replacement = null;
            if (item.Value is not null)
            {
                var existing = Find(rec, item.Section, item.Key);
                replacement = existing is not null && string.Equals(existing.Value, item.Value, StringComparison.Ordinal)
                    ? existing
                    : new CfgChg(
                        item.Section,
                        item.Key,
                        item.Value,
                        existing?.WikiKey ?? wikiKey,
                        null,
                        RecPol.Classify(existing?.WikiKey ?? wikiKey, item.Value, null),
                        WikiRecommendationClass.GeneralRecommendation)
                    {
                        Origin = RecOrigin.Sync
                    };
            }

            map[item.Serial] = Set(rec, item.Section, item.Key, replacement);
        }

        return map;
    }

    private static Dictionary<string, WikiRec> WikiOwned(
        RecSvc baseline,
        RecSyncStoreData store)
    {
        var owned = new Dictionary<string, WikiRec>(StringComparer.Ordinal);
        if (store.WikiPages.Count == 0)
        {
            return owned;
        }

        foreach (var page in store.WikiPages)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rec in page.Recommendations)
            {
                owned[rec.Serial] = rec;
                present.Add(rec.Serial);
            }

            // Serials the newer revision no longer lists lose this page's
            // settings; the game itself stays known.
            foreach (var rec in baseline.Rec)
            {
                if (string.Equals(rec.WikiName, page.Title, StringComparison.Ordinal) &&
                    !present.Contains(rec.Serial) &&
                    !owned.ContainsKey(rec.Serial))
                {
                    owned[rec.Serial] = rec with { Changes = [] };
                }
            }
        }

        return owned;
    }

    private static RecSyncReason Gate(
        Dictionary<string, WikiRec> effective,
        Dictionary<string, WikiRec> wiki,
        Dictionary<string, HashSet<string>> scope,
        string serial,
        string section,
        string key,
        string value)
    {
        if (!effective.TryGetValue(serial, out var rec))
        {
            return RecSyncReason.UnknownSerial;
        }

        if (rec.Binding != WikiGameIdRole.Primary)
        {
            return RecSyncReason.NonPrimaryBinding;
        }

        if (wiki.TryGetValue(serial, out var wikiRec) &&
            !string.Equals(Find(wikiRec, section, key)?.Value, value, StringComparison.Ordinal))
        {
            return RecSyncReason.SourceConflict;
        }

        if (Find(rec, section, key) is null &&
            scope.TryGetValue(rec.WikiName, out var pageKeys) &&
            pageKeys.Contains(Path(section, key)))
        {
            // The Wiki page carries this setting for other serials only.
            return RecSyncReason.ScopeExcluded;
        }

        return RecSyncReason.None;
    }

    private static Dictionary<string, HashSet<string>> PageKeys(RecSvc baseline)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var rec in baseline.Rec)
        {
            if (!map.TryGetValue(rec.WikiName, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                map[rec.WikiName] = keys;
            }

            foreach (var change in rec.Changes)
            {
                keys.Add(Path(change.Section, change.Key));
            }
        }

        return map;
    }

    private static List<RecSyncChange> Diff(
        Dictionary<string, WikiRec> before,
        Dictionary<string, WikiRec> after,
        RecSyncSource source,
        Func<string, string?> revision,
        Func<string, string> url,
        DateTimeOffset now)
    {
        var output = new List<RecSyncChange>();

        foreach (var serial in after.Keys.Union(before.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            before.TryGetValue(serial, out var oldRec);
            after.TryGetValue(serial, out var newRec);
            if (ReferenceEquals(oldRec, newRec))
            {
                continue;
            }

            if (oldRec is null && newRec is not null && newRec.Changes.Count == 0)
            {
                output.Add(Change(source, RecSyncChangeKind.Added, serial, newRec.WikiName, string.Empty, string.Empty, null, newRec.WikiName, RecSyncDisposition.Applied, RecSyncReason.None, revision(serial), url(serial), now));
                continue;
            }

            var oldChanges = (oldRec?.Changes ?? []).ToDictionary(change => Path(change.Section, change.Key), StringComparer.Ordinal);
            var newChanges = (newRec?.Changes ?? []).ToDictionary(change => Path(change.Section, change.Key), StringComparer.Ordinal);

            foreach (var path in oldChanges.Keys.Union(newChanges.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                oldChanges.TryGetValue(path, out var oldChange);
                newChanges.TryGetValue(path, out var newChange);
                if (string.Equals(oldChange?.Value, newChange?.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                var kind = oldChange is null
                    ? RecSyncChangeKind.Added
                    : newChange is null
                        ? RecSyncChangeKind.Removed
                        : RecSyncChangeKind.Updated;
                var (section, key) = Split(path);
                output.Add(Change(
                    source,
                    kind,
                    serial,
                    newRec?.WikiName ?? oldRec?.WikiName,
                    section,
                    key,
                    oldChange?.Value,
                    newChange?.Value,
                    RecSyncDisposition.Applied,
                    RecSyncReason.None,
                    revision(serial),
                    url(serial),
                    now));
            }
        }

        return output;
    }

    private static WikiRec Mark(WikiRec rec, WikiRec? original)
    {
        var originalChanges = (original?.Changes ?? []).ToDictionary(change => Path(change.Section, change.Key), StringComparer.Ordinal);
        return rec with
        {
            Changes = [.. rec.Changes.Select(change =>
                originalChanges.TryGetValue(Path(change.Section, change.Key), out var known) &&
                string.Equals(known.Value, change.Value, StringComparison.Ordinal)
                    ? change
                    : change with { Origin = RecOrigin.Sync })]
        };
    }

    private static WikiRec Set(WikiRec rec, string section, string key, CfgChg? change)
    {
        var changes = rec.Changes
            .Where(item => !(item.Section == section && item.Key == key))
            .ToList();
        if (change is not null)
        {
            changes.Add(change);
        }

        return rec with
        {
            Changes = [.. changes
                .OrderBy(item => item.Section, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal)]
        };
    }

    private static bool SameRecs(IReadOnlyList<WikiRec> left, IReadOnlyList<WikiRec> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var map = left.ToDictionary(rec => rec.Serial, StringComparer.Ordinal);
        foreach (var rec in right)
        {
            if (!map.TryGetValue(rec.Serial, out var other) ||
                other.Binding != rec.Binding ||
                !string.Equals(other.WikiName, rec.WikiName, StringComparison.Ordinal) ||
                other.Changes.Count != rec.Changes.Count)
            {
                return false;
            }

            var changes = other.Changes.ToDictionary(change => Path(change.Section, change.Key), StringComparer.Ordinal);
            foreach (var change in rec.Changes)
            {
                if (!changes.TryGetValue(Path(change.Section, change.Key), out var known) ||
                    !string.Equals(known.Value, change.Value, StringComparison.Ordinal) ||
                    known.Class != change.Class ||
                    !string.Equals(known.Notes, change.Notes, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static Dictionary<string, WikiRec> Index(IEnumerable<WikiRec> recs) =>
        recs.ToDictionary(rec => rec.Serial, StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> ToMap(IEnumerable<RecSyncApiGame> games)
    {
        var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var game in games)
        {
            var settings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var setting in game.Settings)
            {
                settings[Path(setting.Section, setting.Key)] = setting.Value;
            }

            map[game.Serial] = settings;
        }

        return map;
    }

    private static bool IsPartial(Dictionary<string, string> settings) =>
        settings.Keys.Any(path =>
        {
            var (section, key) = Split(path);
            return !WikiSvc.TryGetSyncTarget(section, key, out _);
        });

    private static CfgChg? Find(WikiRec rec, string section, string key) =>
        rec.Changes.FirstOrDefault(change => change.Section == section && change.Key == key);

    private static string? Value(Dictionary<string, WikiRec> map, string serial, string section, string key) =>
        map.TryGetValue(serial, out var rec) ? Find(rec, section, key)?.Value : null;

    private static string? Title(Dictionary<string, WikiRec> map, string serial) =>
        map.TryGetValue(serial, out var rec) ? rec.WikiName : null;

    private static string Id(string serial, string section, string key) => $"{serial}\0{section}\0{key}";

    private static string Path(string section, string key) => $"{section}\0{key}";

    private static (string Section, string Key) Split(string path)
    {
        var index = path.IndexOf('\0', StringComparison.Ordinal);
        return (path[..index], path[(index + 1)..]);
    }
}
