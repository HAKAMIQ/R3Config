using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Lang;

namespace R3Config.Svc;

public static class UiPrefs
{
    private const string FileName = "ui.json";
    private const long MaxPreferencesBytes = 64L * 1024;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        MaxDepth = 16
    };

    public static string LoadLang()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return UiText.ArabLang;
            }

            var json = R3Bound.ReadUtf8(
                path,
                MaxPreferencesBytes,
                "R3Config UI preferences");
            var prefs = JsonSerializer.Deserialize<PrefData>(
                json,
                JsonOpt);

            return UiText.NormLang(prefs?.Lang);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config UI preferences load failed: {ex.Message}");

            return UiText.ArabLang;
        }
    }

    public static void SaveLang(string lang)
    {
        string? tempPath = null;

        try
        {
            var path = GetPath();
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var prefs = new PrefData
            {
                Lang = UiText.NormLang(lang)
            };

            tempPath = Path.Combine(
                directory,
                $".{FileName}.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(
                    stream,
                    prefs,
                    JsonOpt);
                stream.Flush(flushToDisk: true);
            }

            File.Move(
                tempPath,
                path,
                overwrite: true);

            tempPath = null;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                TryDel(tempPath);
            }

            Trace.TraceWarning(
                $"R3Config UI preferences save failed: {ex.Message}");
        }
    }

    private static string GetPath()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            root,
            "R3Config",
            FileName);
    }

    private static void TryDel(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class PrefData
    {
        [JsonPropertyName("lang")]
        public string? Lang { get; init; }

    }
}
