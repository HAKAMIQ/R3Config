using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

public static class Rpcs3GameDatabaseT
{
    public static void Run(string baseDirectory)
    {
        var load = new AppFlow().LoadGameDatabase(baseDirectory);
        TestAssert.Assert(load.Error is null,
            "The production AppFlow path must load the shipped November game database.");
        var database = load.Database;
        TestAssert.Assert(database.CompatibilityEntries == 6798,
            "The shipped compatibility database must contain 6798 Game IDs.");
        TestAssert.Assert(database.ConfigEntries == 2294,
            "The shipped game-config database must contain 2294 Game IDs.");
        TestAssert.Assert(database.CompatibilitySerials.Distinct(StringComparer.Ordinal).Count() == 6798 &&
                          database.ConfigSerials.Distinct(StringComparer.Ordinal).Count() == 2294,
            "The shipped RPCS3 databases must not contain duplicate Game IDs.");

        var compatibility = database.FindCompatibility("BCAS25016")
            ?? throw new InvalidOperationException("The November compatibility addition BCAS25016 is missing.");
        var config = database.FindConfig("BCAS25016")
            ?? throw new InvalidOperationException("The November config addition BCAS25016 is missing.");
        TestAssert.Assert(
            compatibility is { Status.Length: > 0, Patchsets.Count: > 0 } &&
            compatibility.Title == "God of War: Ascension™",
            "A November compatibility addition must retain status, title and nested patchsets.");
        TestAssert.Assert(
            config.Settings.Any(setting => setting.Section == "Video" && setting.Key == "Shader Precision" && setting.Value == "Ultra"),
            "A November config addition must retain its nested Video settings.");

        var descriptor = new Rpcs3GameDescriptor(
            "BCAS25016",
            "BCAS25016",
            string.Empty,
            null,
            null,
            null);
        var title = GameVm.ResolveTitle(descriptor, null, compatibility);
        var row = new GameVm(
            descriptor,
            null,
            new RecSum("BCAS25016", 0, 0, 0, 0, 0, 0, []),
            descriptor.Serial,
            title,
            true,
            compatibility,
            config);
        TestAssert.Assert(
            row.Title == "God of War: Ascension™" &&
            row.Compatibility?.Status == compatibility.Status &&
            row.Config?.Settings.Count == config.Settings.Count,
            "The actual game-row model must consume the shipped November compatibility and config records.");

        TestAssert.Assert(
            database.CompatibilitySerials.All(serial =>
                database.FindCompatibility(serial)?.Title is not { } shipped ||
                !shipped.Any(char.IsControl)),
            "Compatibility titles used as game-row titles must be single-line.");
        var ridge = database.FindCompatibility("BCES00009")
            ?? throw new InvalidOperationException("BCES00009 compatibility is missing.");
        TestAssert.Assert(
            ridge.Status == "Playable" && ridge.Date == new DateOnly(2026, 10, 23) &&
            ridge.Title == "RIDGE RACER 7 (UPDATE)",
            "BCES00009 must expose its latest Playable status, 2026-10-23 and a single-line title.");

        var tolerant = Rpcs3GameDatabase.ParseCompatibility(Encoding.UTF8.GetBytes(
            """
            {"return_code":0,"unknown_root":true,"results":{"BLUS00001":{"status":"Playable","date":"2026-11-07","update":"","patchsets":[],"unknown_optional":{"value":1}}}}
            """));
        TestAssert.Assert(
            tolerant.Single().Value.Update.Length == 0 && tolerant.Single().Value.Patchsets.Count == 0,
            "Unknown properties and empty optional update/patchset values must not break compatibility parsing.");

        Console.WriteLine("RPCS3 November game database runtime integration PASS. Passed: 7, Failed: 0, Skipped: 0");
    }

    public static void Audit(string oldDirectory, string newDirectory)
    {
        var oldCompatibilityPath = Path.Combine(oldDirectory, Rpcs3GameDatabase.CompatibilityFileName);
        var newCompatibilityPath = Path.Combine(newDirectory, Rpcs3GameDatabase.CompatibilityFileName);
        var oldConfigPath = Path.Combine(oldDirectory, Rpcs3GameDatabase.ConfigFileName);
        var newConfigPath = Path.Combine(newDirectory, Rpcs3GameDatabase.ConfigFileName);

        RequireHash(newCompatibilityPath, Rpcs3GameDatabase.CompatibilitySha256);
        RequireHash(newConfigPath, Rpcs3GameDatabase.ConfigSha256);

        using var oldCompatibility = JsonDocument.Parse(File.ReadAllBytes(oldCompatibilityPath));
        using var newCompatibility = JsonDocument.Parse(File.ReadAllBytes(newCompatibilityPath));
        using var oldConfigs = JsonDocument.Parse(File.ReadAllBytes(oldConfigPath));
        using var newConfigs = JsonDocument.Parse(File.ReadAllBytes(newConfigPath));

        var compatibility = Compare(
            oldCompatibility.RootElement.GetProperty("results"),
            newCompatibility.RootElement.GetProperty("results"));
        var configs = Compare(
            oldConfigs.RootElement.GetProperty("games"),
            newConfigs.RootElement.GetProperty("games"));

        TestAssert.Assert(
            compatibility.OldCount == 6754 && compatibility.NewCount == 6798 &&
            compatibility.Added.Count == 44 && compatibility.Missing.Count == 0 && compatibility.Changed.Count == 403,
            "Compatibility semantic diff must be 6754 -> 6798, +44, -0, changed 403.");
        TestAssert.Assert(
            configs.OldCount == 2194 && configs.NewCount == 2294 &&
            configs.Added.Count == 100 && configs.Missing.Count == 0 && configs.Changed.Count == 77,
            "Game-config semantic diff must be 2194 -> 2294, +100, -0, changed 77.");

        var compatibilityFields = ChangedFields(
            oldCompatibility.RootElement.GetProperty("results"),
            newCompatibility.RootElement.GetProperty("results"),
            compatibility.Changed);
        var configFields = ChangedFields(
            oldConfigs.RootElement.GetProperty("games"),
            newConfigs.RootElement.GetProperty("games"),
            configs.Changed);
        TestAssert.Assert(
            compatibilityFields.GetValueOrDefault("date") == 263 &&
            compatibilityFields.GetValueOrDefault("status") == 158 &&
            compatibilityFields.GetValueOrDefault("patchsets") == 141 &&
            compatibilityFields.Count == 3,
            "Compatibility changes must be limited to date=263, status=158 and patchsets=141.");
        TestAssert.Assert(
            configFields.Count == 1 && configFields.GetValueOrDefault("config") == 77,
            "Game-config changes must be limited to config=77.");

        var overlap = compatibility.Added.Intersect(configs.Added, StringComparer.Ordinal).Count();
        var uniqueAdded = compatibility.Added.Union(configs.Added, StringComparer.Ordinal).Count();
        TestAssert.Assert(overlap == 15 && uniqueAdded == 129,
            "The 144 added records must resolve to 129 unique Game IDs with 15 present in both datasets.");

        _ = Rpcs3GameDatabase.Load(newDirectory);
        Console.WriteLine("RPCS3 September-to-November dataset audit PASS. Passed: 7, Failed: 0, Skipped: 0");
    }

    private static Difference Compare(JsonElement oldItems, JsonElement newItems)
    {
        var oldMap = oldItems.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        var newMap = newItems.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        var added = newMap.Keys.Except(oldMap.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var missing = oldMap.Keys.Except(newMap.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var changed = oldMap.Keys.Intersect(newMap.Keys, StringComparer.Ordinal)
            .Where(key => !Equivalent(oldMap[key], newMap[key]))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new Difference(oldMap.Count, newMap.Count, added, missing, changed);
    }

    private static Dictionary<string, int> ChangedFields(
        JsonElement oldItems,
        JsonElement newItems,
        IReadOnlyCollection<string> changed)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in changed)
        {
            var oldItem = oldItems.GetProperty(id);
            var newItem = newItems.GetProperty(id);
            var fields = oldItem.EnumerateObject().Select(item => item.Name)
                .Union(newItem.EnumerateObject().Select(item => item.Name), StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var hasOld = oldItem.TryGetProperty(field, out var oldValue);
                var hasNew = newItem.TryGetProperty(field, out var newValue);
                if (hasOld != hasNew || (hasOld && !Equivalent(oldValue, newValue)))
                {
                    counts[field] = counts.GetValueOrDefault(field) + 1;
                }
            }
        }

        return counts;
    }

    private static void RequireHash(string path, string expected)
    {
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        TestAssert.Assert(actual == expected, $"Unexpected SHA-256 for {path}: {actual}.");
    }

    private static bool Equivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.ValueKind == JsonValueKind.Object)
        {
            var leftProperties = left.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
            var rightProperties = right.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
            return leftProperties.Count == rightProperties.Count &&
                   leftProperties.All(item => rightProperties.TryGetValue(item.Key, out var value) && Equivalent(item.Value, value));
        }

        if (left.ValueKind == JsonValueKind.Array)
        {
            var leftItems = left.EnumerateArray().ToArray();
            var rightItems = right.EnumerateArray().ToArray();
            return leftItems.Length == rightItems.Length &&
                   leftItems.Zip(rightItems).All(pair => Equivalent(pair.First, pair.Second));
        }

        return left.GetRawText().Equals(right.GetRawText(), StringComparison.Ordinal);
    }

    private sealed record Difference(
        int OldCount,
        int NewCount,
        IReadOnlyCollection<string> Added,
        IReadOnlyCollection<string> Missing,
        IReadOnlyCollection<string> Changed);
}
