using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace R3Config.Svc;

public sealed record AppSet(
    [property: JsonPropertyName("LastRpcs3ExePath")] string? LastExe);

public sealed class SetSvc
{
    private static readonly AppSet DefSet = new(null);

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true
    };

    private readonly string _setPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "R3Config",
        "settings.json");

    public AppSet Load()
    {
        try
        {
            if (!File.Exists(_setPath))
            {
                return DefSet;
            }

            var json = File.ReadAllText(_setPath);
            return JsonSerializer.Deserialize<AppSet>(json, JsonOpt)
                   ?? DefSet;
        }
        catch
        {
            return DefSet;
        }
    }

    public void Save(AppSet settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_setPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var tempPath = _setPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOpt);

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _setPath, overwrite: true);
        }
        catch
        {
        }
    }
}
