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

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true
    };

    public static string LoadLang()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return UiText.ArabicLang;
            }

            using var stream = File.OpenRead(path);
            var prefs = JsonSerializer.Deserialize<PrefData>(
                stream,
                JsonOpt);

            return UiText.NormalizeLang(prefs?.Lang);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config UI preferences load failed: {ex.Message}");

            return UiText.ArabicLang;
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
                Lang = UiText.NormalizeLang(lang)
            };

            var json = JsonSerializer.Serialize(
                prefs,
                JsonOpt);

            tempPath = path + ".tmp";

            File.WriteAllText(
                tempPath,
                json);

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