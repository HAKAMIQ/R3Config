using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed record RecSyncLocalState(
    RecSyncStoreData? Data,
    RecSyncStoreHealth Health,
    RecSvc Effective);

// Keeps the recommendation catalog current from the official RPCS3 sources.
// It reads the network and writes only R3Config's own LocalAppData store; it
// never applies a recommendation to a game. Applying still goes through the
// configuration plan, preview, mutation guard, backup and the user's choice.
public sealed class RecommendationSyncUseCase(RecSyncService service)
{
    // Set to 1 to stop every automatic sync request (tests, managed machines).
    // A manual check started by the user is still allowed.
    public const string AutoSyncKillSwitch = "R3CONFIG_DISABLE_AUTO_SYNC";

    public static string DefaultFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "R3Config",
            "RecSync");

    public RecSyncPreferences LoadPreferences()
    {
        try
        {
            return service.Store.LoadPreferences().Preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return RecSyncPreferences.Default with { AutoUpdate = false };
        }
    }

    public bool SavePreferences(RecSyncPreferences preferences)
    {
        try
        {
            service.Store.SavePreferences(preferences);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    // Local read only: no network request is made here.
    public RecSyncLocalState ReadLocal(RecSvc baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        try
        {
            var (data, health) = service.Read(baseline);
            return new RecSyncLocalState(data, health, RecSyncMerge.BuildEffective(baseline, data));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException or ArgumentException)
        {
            return new RecSyncLocalState(null, RecSyncStoreHealth.Reset, baseline);
        }
    }

    public static bool IsAutoSyncAllowed(RecSyncPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return preferences.AutoUpdate &&
               Environment.GetEnvironmentVariable(AutoSyncKillSwitch) != "1";
    }

    public static bool IsDue(RecSyncStoreData? data, DateTimeOffset now) =>
        RecSyncService.IsDue(data, now);

    public bool IsRunning => service.IsRunning;

    // install only locates RPCS3's config_database.dat, read as a fallback
    // copy of the config API when the official request fails.
    public Task<RecSyncReport> SyncAsync(
        RecSvc baseline,
        bool manual,
        Rpcs3InstallDescriptor? install,
        CancellationToken cancellationToken)
    {
        return service.SyncAsync(
            baseline,
            manual,
            ConfigApiParser.LocalDatabasePath(install),
            cancellationToken);
    }

    // Applied changes for games the user has, not shown before. Silent mode
    // never returns anything.
    public static IReadOnlyList<RecSyncChange> SelectNotifications(
        RecSyncPreferences preferences,
        RecSyncStoreData? data,
        IReadOnlyList<RecSyncChange> changes,
        IEnumerable<string> installedSerials)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(installedSerials);

        if (preferences.Notifications != RecSyncNotifications.ImportantForMyGames)
        {
            return [];
        }

        var installed = installedSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shown = (data?.NotifiedIds ?? []).ToHashSet(StringComparer.Ordinal);
        return [.. changes.Where(change =>
            change.Disposition == RecSyncDisposition.Applied &&
            installed.Contains(change.Serial) &&
            !shown.Contains(change.Id))];
    }

    public Task<bool> MarkNotifiedAsync(
        RecSvc baseline,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        return service.MarkNotifiedAsync(baseline, ids, cancellationToken);
    }
}
