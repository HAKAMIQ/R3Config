using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// ---------------------------------------------------------------------------
// Local game-update metadata contract. R3Config never looks updates up at
// runtime. A dated, sourced snapshot file supplied locally is the only source
// of "known versions"; there is no shipped data yet. "Latest" always means
// "latest in the snapshot dated X", and old snapshots are reported as stale.
// Identity (ProductCode) and title metadata are separate: a title is never used
// to find a product.
// ---------------------------------------------------------------------------

public sealed record GameUpdateVersion(string Version, string? SystemVersion = null, long? SizeBytes = null, string? Sha1 = null);

public sealed record GameUpdateEntry(
    string ProductCode,
    string? ContentId,
    string? Title,
    IReadOnlyList<GameUpdateVersion> Versions);

public sealed record GameUpdateSnapshotDocument(
    int Schema,
    string Source,
    string SnapshotDate,
    IReadOnlyList<GameUpdateEntry> Entries);

public enum GameUpdateState
{
    // No snapshot loaded, or the product is not in it: nothing is known.
    NotChecked,

    // The installed version is at or above the newest version in the snapshot.
    NotOlderThanSnapshot,

    // The snapshot lists a newer version than the installed one.
    NewerInSnapshot,

    // The installed version is unknown or not comparable.
    InstalledVersionUnknown
}

public sealed record GameUpdateEvidence(
    GameUpdateState State,
    string? NewestInSnapshot,
    string? SnapshotDate,
    string? Source,
    bool Stale);

public static partial class GameUpdateSnapshot
{
    public const int SchemaVersion = 1;
    public const int StaleAfterDays = 90;

    // A PS3 product code: four letters and five digits, upper case, exactly.
    [GeneratedRegex(@"^[A-Z]{4}[0-9]{5}\z", RegexOptions.CultureInvariant)]
    public static partial Regex ProductCodePattern();

    // A content id: "UP0001-NPUB30000_00-0123456789ABCDEF" (region+issuer, product, label).
    [GeneratedRegex(@"^[A-Z]{2}[0-9]{4}-[A-Z]{4}[0-9]{5}_[0-9]{2}-[A-Z0-9]{16}\z", RegexOptions.CultureInvariant)]
    public static partial Regex ContentIdPattern();

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static GameUpdateSnapshotDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 32 * 1024 * 1024)
        {
            throw new InvalidDataException("The update snapshot is missing or larger than the limit.");
        }

        GameUpdateSnapshotDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<GameUpdateSnapshotDocument>(File.ReadAllBytes(path), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The update snapshot JSON is invalid: " + ex.Message, ex);
        }

        Validate(document);
        return document!;
    }

    public static void Validate(GameUpdateSnapshotDocument? document)
    {
        if (document is null || document.Schema != SchemaVersion || string.IsNullOrWhiteSpace(document.Source) ||
            !DateOnly.TryParseExact(document.SnapshotDate, "yyyy-MM-dd", out _) || document.Entries is null)
        {
            throw new InvalidDataException("The update snapshot needs schema 1, a source and a yyyy-MM-dd snapshot date.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
        {
            if (entry is null || !ProductCodePattern().IsMatch(entry.ProductCode ?? string.Empty) || !seen.Add(entry.ProductCode!) ||
                (entry.ContentId is not null && !ContentIdPattern().IsMatch(entry.ContentId)) ||
                entry.Versions is null || entry.Versions.Any(version => !Version.TryParse(version?.Version, out _)))
            {
                throw new InvalidDataException("An update entry has an invalid, duplicate or non-exact product code / content id / version.");
            }
        }
    }

    // What the snapshot proves about one installed game. asOf is the runtime date
    // (never stored in the snapshot); a snapshot older than StaleAfterDays is Stale.
    public static GameUpdateEvidence Evaluate(GameUpdateSnapshotDocument? snapshot, string? productCode, string? installedVersion, DateOnly asOf)
    {
        if (snapshot is null || productCode is null ||
            snapshot.Entries.FirstOrDefault(entry => string.Equals(entry.ProductCode, productCode, StringComparison.Ordinal)) is not { Versions.Count: > 0 } entry)
        {
            return new GameUpdateEvidence(GameUpdateState.NotChecked, null, snapshot?.SnapshotDate, snapshot?.Source, snapshot is not null && IsStale(snapshot, asOf));
        }

        var newest = entry.Versions.Select(item => item.Version).OrderByDescending(Version.Parse).First();
        var stale = IsStale(snapshot, asOf);
        if (!Version.TryParse(installedVersion, out var installed))
        {
            return new GameUpdateEvidence(GameUpdateState.InstalledVersionUnknown, newest, snapshot.SnapshotDate, snapshot.Source, stale);
        }

        return new GameUpdateEvidence(
            installed >= Version.Parse(newest) ? GameUpdateState.NotOlderThanSnapshot : GameUpdateState.NewerInSnapshot,
            newest,
            snapshot.SnapshotDate,
            snapshot.Source,
            stale);
    }

    private static bool IsStale(GameUpdateSnapshotDocument snapshot, DateOnly asOf) =>
        DateOnly.ParseExact(snapshot.SnapshotDate, "yyyy-MM-dd").AddDays(StaleAfterDays) < asOf;

    public static string Serialize(GameUpdateSnapshotDocument document) =>
        JsonSerializer.Serialize(document, WriteOptions);

    public static byte[] ToBytes(GameUpdateSnapshotDocument document) => new UTF8Encoding(false).GetBytes(Serialize(document));
}

#pragma warning restore IDE0130
