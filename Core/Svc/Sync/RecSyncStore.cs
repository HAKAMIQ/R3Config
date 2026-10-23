using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record RecSyncStoreLoad(
    RecSyncStoreData? Data,
    RecSyncStoreHealth Health,
    string? Detail);

public sealed record RecSyncPreferencesLoad(
    RecSyncPreferences Preferences,
    bool Recovered);

// Durable local layer under the R3Config LocalAppData folder. It never
// touches the shipped catalog or any RPCS3 file. Writes go to a temporary
// file first and replace the store in one step, keeping the previous good
// store as store.lkg.json; a damaged store falls back to that copy, and a
// store written by a newer R3Config is left untouched.
public sealed partial class RecSyncStore
{
    public const string FileName = "store.json";
    public const string BackupFileName = "store.lkg.json";
    public const string PreferencesFileName = "prefs.json";
    public const long MaxStoreBytes = 32L * 1024 * 1024;
    private const long MaxPreferencesBytes = 16L * 1024;
    private const int MaxCorruptCopies = 3;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory;
    private readonly object _gate = new();

    public RecSyncStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = System.IO.Path.GetFullPath(directory);
    }

    public string Folder => _directory;

    private string MainPath => System.IO.Path.Combine(_directory, FileName);

    private string BackupPath => System.IO.Path.Combine(_directory, BackupFileName);

    private string PreferencesPath => System.IO.Path.Combine(_directory, PreferencesFileName);

    public RecSyncStoreLoad Load()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
            {
                return new RecSyncStoreLoad(null, RecSyncStoreHealth.Fresh, null);
            }

            DeleteTemporaryFiles();

            var mainExists = File.Exists(MainPath);
            if (mainExists)
            {
                var main = TryRead(MainPath, out var mainSchema, out var mainError);
                if (main is not null)
                {
                    return new RecSyncStoreLoad(main, RecSyncStoreHealth.Loaded, null);
                }

                if (mainSchema > RecSyncStoreData.CurrentSchema)
                {
                    return new RecSyncStoreLoad(null, RecSyncStoreHealth.NewerSchema, mainError);
                }

                SetAside(MainPath);
                var backup = TryRead(BackupPath, out _, out var backupError);
                return backup is not null
                    ? new RecSyncStoreLoad(backup, RecSyncStoreHealth.RecoveredFromBackup, mainError)
                    : new RecSyncStoreLoad(null, RecSyncStoreHealth.Reset, $"{mainError} {backupError}".Trim());
            }

            var lastGood = TryRead(BackupPath, out var schema, out var error);
            if (lastGood is not null)
            {
                return new RecSyncStoreLoad(lastGood, RecSyncStoreHealth.RecoveredFromBackup, "The main store was missing.");
            }

            if (schema > RecSyncStoreData.CurrentSchema)
            {
                return new RecSyncStoreLoad(null, RecSyncStoreHealth.NewerSchema, error);
            }

            return new RecSyncStoreLoad(null, RecSyncStoreHealth.Fresh, null);
        }
    }

    public void Save(
        RecSyncStoreData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        Validate(data);

        var payload = JsonSerializer.SerializeToUtf8Bytes(data, JsonOpt);
        if (payload.LongLength > MaxStoreBytes)
        {
            throw new InvalidDataException("The recommendation sync store exceeds its size limit.");
        }

        // Never persist anything that would not load back.
        Validate(JsonSerializer.Deserialize<RecSyncStoreData>(payload, JsonOpt)
                 ?? throw new InvalidDataException("The recommendation sync store did not round-trip."));

        lock (_gate)
        {
            Directory.CreateDirectory(_directory);

            if (File.Exists(MainPath) &&
                TryRead(MainPath, out var existingSchema, out _) is null &&
                existingSchema > RecSyncStoreData.CurrentSchema)
            {
                throw new InvalidOperationException(
                    "The recommendation sync store was written by a newer R3Config and is left unchanged.");
            }

            WriteReplace(MainPath, BackupPath, payload, keepBackup: true, cancellationToken);
        }
    }

    public RecSyncPreferencesLoad LoadPreferences()
    {
        lock (_gate)
        {
            if (!File.Exists(PreferencesPath))
            {
                return new RecSyncPreferencesLoad(RecSyncPreferences.Default, false);
            }

            try
            {
                var json = R3Bound.ReadUtf8(PreferencesPath, MaxPreferencesBytes, "recommendation sync preferences");
                var dto = JsonSerializer.Deserialize<PreferencesDto>(json, JsonOpt);
                if (dto?.Schema == 1 &&
                    dto.AutoUpdate is { } autoUpdate &&
                    dto.Notifications is { } notifications &&
                    Enum.IsDefined(notifications))
                {
                    return new RecSyncPreferencesLoad(new RecSyncPreferences(autoUpdate, notifications), false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException)
            {
            }

            // Fail closed: an unreadable preference file may have held a
            // disabled sync, so no automatic network request is made.
            return new RecSyncPreferencesLoad(
                RecSyncPreferences.Default with { AutoUpdate = false },
                true);
        }
    }

    public void SavePreferences(RecSyncPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new PreferencesDto
            {
                Schema = 1,
                AutoUpdate = preferences.AutoUpdate,
                Notifications = preferences.Notifications
            },
            JsonOpt);

        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            WriteReplace(PreferencesPath, null, payload, keepBackup: false, CancellationToken.None);
        }
    }

    public static void Validate(RecSyncStoreData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Require(data.Schema == RecSyncStoreData.CurrentSchema, "schema");
        Require(data.BaselineHash is not null && data.BaselineHash.Length <= 128, "baseline hash");
        Require(data.BaselineVersion is not null && data.BaselineVersion.Length <= 64, "baseline version");
        Require(data.Sources is not null && data.Sources.Count <= 8 && data.Sources.All(item => item is not null && Enum.IsDefined(item.Source) && Enum.IsDefined(item.Status) && Text(item.Detail, 512) && Text(item.ETag, 256) && Text(item.LastModified, 64) && Text(item.RevisionId, 64) && Text(item.Sha256, 64)), "sources");
        Require(data.Sources!.Select(item => item.Source).Distinct().Count() == data.Sources!.Count, "duplicate sources");
        Require(data.ApiSnapshot is null || (data.ApiSnapshot.Count <= ConfigApiParser.MaxGames && data.ApiSnapshot.All(ValidGame)), "config API snapshot");
        Require(data.Overrides is not null && data.Overrides.Count <= 200_000 && data.Overrides.All(item => item is not null && IsSerial(item.Serial) && Name(item.Section) && Name(item.Key) && (item.Value is null || Text(item.Value, 128))), "overrides");
        Require(data.WikiPages is not null && data.WikiPages.Count <= WikiSvc.MaxLivePages && data.WikiPages.All(ValidPage), "Wiki pages");
        Require(data.History is not null && data.History.Count <= RecSyncMerge.MaxHistory && data.History.All(ValidChange), "history");
        Require(data.Quarantine is not null && data.Quarantine.Count <= 200_000 && data.Quarantine.All(ValidChange), "quarantine");
        Require(data.NotifiedIds is not null && data.NotifiedIds.Count <= 20_000 && data.NotifiedIds.All(id => Text(id, 64)), "notified ids");
        Require(data.SeenWikiRevisions is not null && data.SeenWikiRevisions.Count <= 20_000 && data.SeenWikiRevisions.All(id => Text(id, 32)), "seen Wiki revisions");
        Require(data.GitHubData is null || ValidGitHubData(data.GitHubData), "GitHub data");
    }

    private static RecSyncStoreData? TryRead(string path, out int schema, out string? error)
    {
        schema = 0;
        error = null;

        if (!File.Exists(path))
        {
            error = $"{System.IO.Path.GetFileName(path)} is missing.";
            return null;
        }

        try
        {
            var bytes = R3Bound.ReadBytes(path, MaxStoreBytes, "recommendation sync store");
            using (var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 }))
            {
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    TryGetSchema(document.RootElement, out var parsedSchema))
                {
                    schema = parsedSchema;
                }
            }

            if (schema > RecSyncStoreData.CurrentSchema)
            {
                error = $"{System.IO.Path.GetFileName(path)} uses schema {schema.ToString(CultureInfo.InvariantCulture)}, newer than this R3Config.";
                return null;
            }

            var data = JsonSerializer.Deserialize<RecSyncStoreData>(bytes, JsonOpt)
                       ?? throw new InvalidDataException("The store is empty.");
            Validate(data);
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException or ArgumentException)
        {
            error = $"{System.IO.Path.GetFileName(path)} is unreadable: {ex.Message}";
            return null;
        }
    }

    private static bool TryGetSchema(JsonElement root, out int schema)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("Schema", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt32(out schema))
            {
                return true;
            }
        }

        schema = 0;
        return false;
    }

    private void WriteReplace(
        string target,
        string? backup,
        byte[] payload,
        bool keepBackup,
        CancellationToken cancellationToken)
    {
        var temp = System.IO.Path.Combine(
            _directory,
            $".{System.IO.Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            // Cancellation is honoured up to this point; the commit below is
            // a single replace, so a cancelled save leaves the old store.
            cancellationToken.ThrowIfCancellationRequested();

            if (keepBackup && backup is not null && File.Exists(target) && TryRead(target, out _, out _) is not null)
            {
                File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, target, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private void DeleteTemporaryFiles()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, ".*.tmp"))
        {
            if (TempName().IsMatch(System.IO.Path.GetFileName(path)))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private void SetAside(string path)
    {
        try
        {
            var copy = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(path, copy);

            foreach (var old in Directory.EnumerateFiles(_directory, $"{System.IO.Path.GetFileName(path)}.corrupt-*")
                         .Order(StringComparer.Ordinal)
                         .SkipLast(MaxCorruptCopies))
            {
                File.Delete(old);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool ValidGame(RecSyncApiGame game) =>
        game is not null &&
        IsSerial(game.Serial) &&
        game.Settings is not null &&
        game.Settings.Count <= 64 &&
        game.Settings.All(setting => setting is not null && Name(setting.Section) && Name(setting.Key) && Text(setting.Value, 128));

    private static bool ValidPage(RecSyncWikiPage page) =>
        page is not null &&
        Text(page.Title, 512) &&
        Text(page.RevisionId, 32) &&
        page.Recommendations is not null &&
        page.Recommendations.Count <= 256 &&
        page.Recommendations.All(rec => rec is not null && IsSerial(rec.Serial) && Text(rec.WikiName, 512) && rec.Changes is not null && rec.Changes.Count <= 128 && rec.Changes.All(change => change is not null && Name(change.Section) && Name(change.Key) && Text(change.Value, 128) && Text(change.WikiKey, 128) && Text(change.Notes, 4096)));

    private static bool ValidGitHubData(RecSyncGitHubData data)
    {
        if (data.Version is null || !Regex.IsMatch(data.Version, @"^[0-9]{14}$", RegexOptions.CultureInvariant) ||
            data.ManifestSha256 is null || !Regex.IsMatch(data.ManifestSha256, @"^[0-9A-F]{64}$", RegexOptions.CultureInvariant) ||
            data.Recommendations is null || data.Recommendations.Count is 0 or > 20_000 ||
            data.Notes is null || data.Notes.Count > 20_000 ||
            data.Notes.Any(note => note is null || !Text(note.SourceEnglish, 4096) || !Text(note.NoteArabic, 4096)))
        {
            return false;
        }

        try
        {
            _ = RecSvc.FromRec("HAKAMIQ/rpcs3", data.Version, data.Recommendations, data.GeneratedAtUtc, data.ManifestSha256);
            return data.Notes.Select(note => note.SourceEnglish).Distinct(StringComparer.Ordinal).Count() == data.Notes.Count;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool ValidChange(RecSyncChange change) =>
        change is not null &&
        Text(change.Id, 64) &&
        IsSerial(change.Serial) &&
        Text(change.Title, 512) &&
        Text(change.Section, 64) &&
        Text(change.Key, 128) &&
        Text(change.OldValue, 512) &&
        Text(change.NewValue, 512) &&
        Text(change.RevisionId, 64) &&
        change.SourceUrl is not null &&
        (change.SourceUrl.StartsWith(RecSyncMerge.ConfigApiUrl, StringComparison.Ordinal) ||
         change.SourceUrl.StartsWith(RecSyncMerge.WikiPageUrl, StringComparison.Ordinal));

    private static bool IsSerial(string? value) => value is not null && SerialRule().IsMatch(value);

    private static bool Name(string? value) => value is { Length: > 0 and <= 128 } && !value.Any(char.IsControl);

    private static bool Text(string? value, int max) => value is null || (value.Length <= max && !value.Any(ch => char.IsControl(ch) && ch != '\n'));

    private static void Require(bool condition, string part)
    {
        if (!condition)
        {
            throw new InvalidDataException($"The recommendation sync store has an invalid {part}.");
        }
    }

    [GeneratedRegex(@"^[A-Z0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex SerialRule();

    [GeneratedRegex(@"^\.(?:store\.json|store\.lkg\.json|prefs\.json)\.[0-9a-f]{32}\.tmp$", RegexOptions.CultureInvariant)]
    private static partial Regex TempName();

    private sealed class PreferencesDto
    {
        public int Schema { get; set; }

        public bool? AutoUpdate { get; set; }

        public RecSyncNotifications? Notifications { get; set; }
    }
}
