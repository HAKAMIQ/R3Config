using System;
using System.IO;
using System.Text.Json;

namespace AppSvc;

public sealed record AppSettings(
    string? LastRpcs3ExePath);

public sealed class AppSettingsService
{
    private static readonly AppSettings DefaultSettings = new(null);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "R3Config",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return DefaultSettings;
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? DefaultSettings;
        }
        catch
        {
            return DefaultSettings;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var tempPath = _settingsPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOptions);

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch
        {
            // Settings persistence is best-effort.
            // The app should not fail because the last-used RPCS3 path could not be saved.
        }
    }
}