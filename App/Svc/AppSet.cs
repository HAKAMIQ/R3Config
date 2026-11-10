using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace R3Config.Svc;

// One RPCS3 log crash session the user already opened or dismissed in the
// main-window crash notice (LogCrashNotice.SessionId), per game serial.
public sealed record LogCrashAck(
    [property: JsonPropertyName("Serial")]
    string Serial,
    [property: JsonPropertyName("Session")]
    string Session);

// SeenLogCrashes is optional: a settings.json written before it existed loads
// with nothing acknowledged, and the field is omitted while empty.
public sealed record AppSet(
    [property: JsonPropertyName("LastRpcs3ExePath")]
    string? LastExe,
    [property: JsonPropertyName("SeenLogCrashes")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LogCrashAck>? SeenLogCrashes = null)
{
    internal const int MaxSeenLogCrashes = 200;

    public string? SeenLogCrash(string serial)
    {
        var key = SetSvc.NormalizeSerial(serial);
        return key is null
            ? null
            : SeenLogCrashes?.LastOrDefault(item =>
                string.Equals(item.Serial, key, StringComparison.Ordinal))?.Session;
    }

    // The newest acknowledgement of a serial replaces its older one and moves
    // to the end, so the bounded list drops the longest-unseen games first.
    public AppSet WithSeenLogCrash(string serial, string session)
    {
        var key = SetSvc.NormalizeSerial(serial);
        var value = SetSvc.NormalizeSession(session);
        if (key is null || value is null)
        {
            return this;
        }

        return this with
        {
            SeenLogCrashes =
            [
                .. (SeenLogCrashes ?? [])
                    .Where(item => !string.Equals(item.Serial, key, StringComparison.Ordinal)),
                new LogCrashAck(key, value)
            ]
        };
    }
}

public sealed class SetSvc
{
    private const string AppDirectoryName = "R3Config";
    private const string SettingsFileName = "settings.json";
    private const string MutexPrefix = @"Local\R3Config.SetSvc.";
    private const int MaxPathChars = 32_767;
    private const long MaxSettingsBytes = 256L * 1024;

    private static readonly TimeSpan MutexWaitTimeout =
        TimeSpan.FromSeconds(10);

    private static readonly AppSet DefSet = new(null);

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16
    };

    private readonly string _setPath;
    private readonly string? _legacyPath;
    private readonly string _mutexName;

    public SetSvc()
        : this(
            BuildDefaultPath(
                Environment.SpecialFolder.LocalApplicationData),
            BuildDefaultPath(
                Environment.SpecialFolder.ApplicationData))
    {
    }

    public SetSvc(string path)
        : this(path, null)
    {
    }

    internal SetSvc(
        string path,
        string? legacyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _setPath = Path.GetFullPath(path);
        ValidateStorePath(_setPath, nameof(path));
        _legacyPath = NormalizeLegacyPath(
            legacyPath,
            _setPath);
        _mutexName = BuildMutexName(_setPath);
    }

    public AppSet Load()
    {
        try
        {
            return ExecuteLocked(LoadLocked);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            DecoderFallbackException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config settings load failed: {ex.Message}");

            return DefSet;
        }
    }

    public void Save(AppSet settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);

        try
        {
            ExecuteLocked(() => SaveLocked(normalized));
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config settings save failed: {ex.Message}");
        }
    }

    // Read-modify-write under the same cross-process lock, so saving one field
    // (the RPCS3 path, a crash acknowledgement) never drops another. Only
    // unreadable content (as Load treats it: invalid UTF-8, oversized or bad
    // JSON) starts from defaults, as Save always replaced it. An IO or access
    // failure while reading may be transient, so the update is abandoned
    // without writing and traced like any other settings write failure; the
    // saved fields stay as they are.
    public void Update(Func<AppSet, AppSet> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        try
        {
            ExecuteLocked(() =>
            {
                AppSet current;
                try
                {
                    current = LoadLocked();
                }
                catch (Exception ex) when (
                    ex is InvalidDataException or
                    DecoderFallbackException or
                    JsonException or
                    NotSupportedException)
                {
                    Trace.TraceWarning(
                        $"R3Config settings load before update failed: {ex.Message}");
                    current = DefSet;
                }

                SaveLocked(Normalize(change(current) ?? current));
            });
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config settings update failed: {ex.Message}");
        }
    }

    private AppSet LoadLocked()
    {
        if (File.Exists(_setPath))
        {
            return LoadPath(_setPath);
        }

        if (_legacyPath is null ||
            !File.Exists(_legacyPath))
        {
            return DefSet;
        }

        var migrated = LoadPath(_legacyPath);
        if (migrated.LastExe is null)
        {
            return DefSet;
        }

        try
        {
            SaveLocked(migrated);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config settings migration save failed: {ex.Message}");
        }

        return migrated;
    }

    private static AppSet LoadPath(string path)
    {
        var json = R3Bound.ReadUtf8(
            path,
            MaxSettingsBytes,
            "R3Config settings");

        return Normalize(
            JsonSerializer.Deserialize<AppSet>(
                json,
                JsonOpt) ?? DefSet);
    }

    private void SaveLocked(AppSet settings)
    {
        string? tempPath = null;

        try
        {
            var directory = Path.GetDirectoryName(_setPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidDataException(
                    "R3Config settings directory is unavailable.");
            }

            Directory.CreateDirectory(directory);

            var payload = JsonSerializer.SerializeToUtf8Bytes(
                settings,
                JsonOpt);
            if (payload.LongLength > MaxSettingsBytes)
            {
                throw new InvalidDataException(
                    $"R3Config settings exceed the {MaxSettingsBytes}-byte output limit.");
            }

            var destinationName = Path.GetFileName(_setPath);
            tempPath = Path.Combine(
                directory,
                $".{destinationName}.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(
                tempPath,
                _setPath,
                overwrite: true);

            tempPath = null;
        }
        finally
        {
            if (tempPath is not null)
            {
                TryDeleteTemp(tempPath);
            }
        }
    }

    private T ExecuteLocked<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var mutex = new Mutex(
            initiallyOwned: false,
            name: _mutexName);
        var acquired = false;

        try
        {
            try
            {
                acquired = mutex.WaitOne(MutexWaitTimeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
                Trace.TraceWarning(
                    "R3Config settings recovered an abandoned cross-process lock.");
            }

            if (!acquired)
            {
                throw new IOException(
                    "Timed out waiting for the R3Config settings lock.");
            }

            return action();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private void ExecuteLocked(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        ExecuteLocked(() =>
        {
            action();
            return true;
        });
    }

    private static AppSet Normalize(AppSet settings)
    {
        return new AppSet(
            NormalizeExecutablePath(settings.LastExe),
            NormalizeSeenLogCrashes(settings.SeenLogCrashes));
    }

    // Keeps well-formed entries only, one per serial (the last wins), bounded.
    private static List<LogCrashAck>? NormalizeSeenLogCrashes(
        IReadOnlyList<LogCrashAck>? entries)
    {
        if (entries is null)
        {
            return null;
        }

        var kept = new List<LogCrashAck>();
        foreach (var entry in entries)
        {
            var serial = NormalizeSerial(entry?.Serial);
            var session = NormalizeSession(entry?.Session);
            if (serial is null || session is null)
            {
                continue;
            }

            kept.RemoveAll(item => string.Equals(item.Serial, serial, StringComparison.Ordinal));
            kept.Add(new LogCrashAck(serial, session));
        }

        if (kept.Count > AppSet.MaxSeenLogCrashes)
        {
            kept.RemoveRange(0, kept.Count - AppSet.MaxSeenLogCrashes);
        }

        return kept.Count == 0 ? null : kept;
    }

    // A game serial as RPCS3 names its folder (letters and digits only).
    internal static string? NormalizeSerial(string? serial)
    {
        var trimmed = serial?.Trim().ToUpperInvariant();
        return trimmed is { Length: > 0 and <= 32 } &&
               trimmed.All(character => character is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            ? trimmed
            : null;
    }

    // A LogCrashNotice.SessionId value (sha256:HEX).
    internal static string? NormalizeSession(string? session)
    {
        var trimmed = session?.Trim();
        return trimmed is { Length: > 0 and <= 160 } &&
               trimmed.All(character => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or ':')
            ? trimmed
            : null;
    }

    private static string? NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();
        if (trimmed.Length > MaxPathChars ||
            !Path.IsPathFullyQualified(trimmed))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            return fullPath.Length <= MaxPathChars
                ? fullPath
                : null;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return null;
        }
    }

    private static string? NormalizeLegacyPath(
        string? legacyPath,
        string currentPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(legacyPath);
            ValidateStorePath(fullPath, nameof(legacyPath));

            return fullPath.Equals(
                currentPath,
                StringComparison.OrdinalIgnoreCase)
                ? null
                : fullPath;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return null;
        }
    }

    private static void ValidateStorePath(
        string path,
        string paramName)
    {
        if (path.Length > MaxPathChars ||
            string.IsNullOrWhiteSpace(Path.GetFileName(path)) ||
            string.IsNullOrWhiteSpace(Path.GetDirectoryName(path)))
        {
            throw new ArgumentException(
                "R3Config settings path must identify a file in a directory.",
                paramName);
        }
    }

    private static string BuildDefaultPath(
        Environment.SpecialFolder folder)
    {
        return Path.Combine(
            Environment.GetFolderPath(folder),
            AppDirectoryName,
            SettingsFileName);
    }

    private static string BuildMutexName(string path)
    {
        var canonical = path.ToUpperInvariant();
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical));

        return MutexPrefix + Convert.ToHexString(digest);
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                $"R3Config temporary settings cleanup failed: {ex.Message}");
        }
    }
}
