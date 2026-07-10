using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Lang;

namespace AppSvc;

public static class UiPrefs
{
    private const string FileName = "ui.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string LoadLang()
    {
        try
        {
            var path = ResolvePath();
            if (!File.Exists(path))
            {
                return UiText.ArabicLang;
            }

            using var stream = File.OpenRead(path);
            var prefs = JsonSerializer.Deserialize<UiPrefsData>(stream, JsonOptions);

            return UiText.NormalizeLang(prefs?.Lang);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Trace.TraceWarning($"R3Config UI preferences load failed: {ex.Message}");
            return UiText.ArabicLang;
        }
    }

    public static void SaveLang(string lang)
    {
        string? tempPath = null;

        try
        {
            var path = ResolvePath();
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var prefs = new UiPrefsData
            {
                Lang = UiText.NormalizeLang(lang)
            };

            var json = JsonSerializer.Serialize(prefs, JsonOptions);
            tempPath = path + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
            tempPath = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                TryDeleteFile(tempPath);
            }

            Trace.TraceWarning($"R3Config UI preferences save failed: {ex.Message}");
        }
    }

    private static string ResolvePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "R3Config", FileName);
    }

    private static void TryDeleteFile(string path)
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
            // Best-effort cleanup for temporary UI preference files.
        }
    }

    private sealed class UiPrefsData
    {
        [JsonPropertyName("lang")]
        public string? Lang { get; init; }
    }
}