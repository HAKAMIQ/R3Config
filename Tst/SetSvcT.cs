using System.IO;
using System.Text;
using R3Config.Svc;

internal static class SetSvcT
{
    private const long MaxSettingsBytes = 256L * 1024;

    public static void Run(string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "SettingsStore");
        Directory.CreateDirectory(root);

        RoundTripAndNormalization(root);
        CorruptAndOversizedInput(root);
        RoamingMigration(root);
        ConcurrentWriters(root);

        Console.WriteLine("Settings persistence hardening PASS.");
    }

    private static void RoundTripAndNormalization(string root)
    {
        var path = Path.Combine(
            root,
            "roundtrip",
            "settings.json");
        var service = new SetSvc(path);
        var rawExe = Path.Combine(
            root,
            "rpc",
            ".",
            "rpcs3.exe");
        var expected = Path.GetFullPath(rawExe);

        service.Save(new AppSet($"  {rawExe}  "));

        Assert(
            service.Load().LastExe == expected,
            "Settings should persist a canonical absolute RPCS3 path.");

        service.Save(new AppSet("relative\\rpcs3.exe"));
        Assert(
            service.Load().LastExe is null,
            "Settings should reject relative RPCS3 paths.");

        service.Save(new AppSet("   "));
        Assert(
            service.Load().LastExe is null,
            "Whitespace RPCS3 paths should normalize to null.");
    }

    private static void CorruptAndOversizedInput(string root)
    {
        var dir = Path.Combine(
            root,
            "corrupt");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(
            dir,
            "settings.json");
        var service = new SetSvc(path);

        File.WriteAllBytes(
            path,
            [0xC3, 0x28]);
        Assert(
            service.Load().LastExe is null,
            "Invalid UTF-8 settings should fail closed to defaults.");

        File.WriteAllText(
            path,
            "{not-json",
            new UTF8Encoding(false));
        Assert(
            service.Load().LastExe is null,
            "Malformed settings JSON should fail closed to defaults.");

        using (var oversized = new FileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversized.SetLength(MaxSettingsBytes + 1);
        }

        Assert(
            service.Load().LastExe is null,
            "Oversized settings should fail closed before whole-file allocation.");
    }

    private static void RoamingMigration(string root)
    {
        var localPath = Path.Combine(
            root,
            "migration",
            "local",
            "settings.json");
        var roamingPath = Path.Combine(
            root,
            "migration",
            "roaming",
            "settings.json");
        var legacy = new SetSvc(roamingPath);
        var legacyExe = Path.Combine(
            root,
            "legacy-rpcs3",
            "rpcs3.exe");
        var expected = Path.GetFullPath(legacyExe);

        legacy.Save(new AppSet(legacyExe));

        var current = new SetSvc(
            localPath,
            roamingPath);
        var migrated = current.Load();

        Assert(
            migrated.LastExe == expected,
            "Settings should read the legacy roaming value when local settings are absent.");
        Assert(
            File.Exists(localPath),
            "Legacy roaming settings should be migrated to the local settings file.");
        Assert(
            new SetSvc(localPath).Load().LastExe == expected,
            "Migrated local settings should preserve the canonical RPCS3 path.");

        legacy.Save(new AppSet(
            Path.Combine(
                root,
                "other-rpcs3",
                "rpcs3.exe")));
        Assert(
            current.Load().LastExe == expected,
            "Existing local settings must take precedence over stale roaming settings.");
    }

    private static void ConcurrentWriters(string root)
    {
        var path = Path.Combine(
            root,
            "concurrent",
            "settings.json");
        var expected = Enumerable.Range(0, 24)
            .Select(index => Path.GetFullPath(Path.Combine(
                root,
                "rpc-concurrent",
                index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                "rpcs3.exe")))
            .ToArray();

        var tasks = expected
            .Select(value => Task.Run(() =>
                new SetSvc(path).Save(new AppSet(value))))
            .ToArray();

        Task.WaitAll(tasks);

        var loaded = new SetSvc(path).Load().LastExe;
        Assert(
            loaded is not null &&
            expected.Contains(
                loaded,
                StringComparer.OrdinalIgnoreCase),
            "Concurrent independent settings writers should leave one complete valid value.");

        var leftovers = Directory.EnumerateFiles(
                Path.GetDirectoryName(path)!,
                ".settings.json.*.tmp",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        Assert(
            leftovers.Length == 0,
            "Concurrent settings writes should not leave temporary files behind.");
    }
}
