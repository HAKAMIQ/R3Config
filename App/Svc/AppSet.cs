using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace R3Config.Svc;

public sealed record AppSet(
    [property: JsonPropertyName("LastRpcs3ExePath")]
    string? LastExe);

public sealed class SetSvc
{
    private const string AppDirectoryName = "R3Config";
    private const string SettingsFileName = "settings.json";

    private static readonly object SyncRoot = new();

    private static readonly AppSet DefSet = new(null);

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _setPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData),
        AppDirectoryName,
        SettingsFileName);

    public AppSet Load()
    {
        lock (SyncRoot)
        {
            try
            {
                if (!File.Exists(_setPath))
                {
                    return DefSet;
                }

                using var stream = new FileStream(
                    _setPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                return JsonSerializer.Deserialize<AppSet>(
                           stream,
                           JsonOpt)
                       ?? DefSet;
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                JsonException or
                NotSupportedException)
            {
                Trace.TraceWarning(
                    $"R3Config settings load failed: {ex.Message}");

                return DefSet;
            }
        }
    }

    public void Save(AppSet settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (SyncRoot)
        {
            string? tempPath = null;

            try
            {
                var directory = Path.GetDirectoryName(_setPath);

                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                Directory.CreateDirectory(directory);

                tempPath = Path.Combine(
                    directory,
                    $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");

                using (var stream = new FileStream(
                           tempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    JsonSerializer.Serialize(
                        stream,
                        settings,
                        JsonOpt);

                    stream.Flush(flushToDisk: true);
                }

                File.Move(
                    tempPath,
                    _setPath,
                    overwrite: true);

                tempPath = null;
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                JsonException or
                NotSupportedException)
            {
                Trace.TraceWarning(
                    $"R3Config settings save failed: {ex.Message}");
            }
            finally
            {
                if (tempPath is not null)
                {
                    try
                    {
                        File.Delete(tempPath);
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
        }
    }
}