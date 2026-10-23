using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Svc;
using R3Config.Mdl;
using R3Config.Lang;
using R3Config.Vm;
using R3Config.Application;
using R3Config.Tests;

// Snapshot folder names are opaque identifiers: Gregorian and invariant for
// every interface language and Windows Region. Older builds named them with
// the current culture, so Hijri-named folders exist on disk; they are never
// renamed and stay listed, restorable and ordered by manifest CreatedAt.
internal static partial class Program
{
    private static readonly Regex BkpNamePattern = new(
        "^[0-9]{8}_[0-9]{6}_[0-9]{3}_[0-9a-f]{32}$",
        RegexOptions.CultureInvariant);

    private static readonly CultureInfo BkpHijri = CreateHijriCulture();

    static void Bkp_NameGregorian(string tempRoot)
    {
        var (install, target) = BkpNameInstall(tempRoot, "backup-name-gregorian");
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        // UiText first: its static startup Apply switches every culture.
        var lang = UiText.CurLang;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var threadCulture = CultureInfo.DefaultThreadCurrentCulture;
        var threadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        var name = string.Empty;
        var before = DateTime.MinValue;
        var after = DateTime.MinValue;
        WithHijriCulture(() =>
        {
            before = DateTime.Now;
            var snapshot = BackupSnapshot(new BackupService(), backupRoot, [target]);
            after = DateTime.Now;
            BkpInTemp(tempRoot, snapshot.DirPath, Path.Combine(snapshot.DirPath, "manifest.json"));
            name = Path.GetFileName(snapshot.DirPath);
        });

        Assert(ReferenceEquals(CultureInfo.CurrentCulture, culture) &&
               ReferenceEquals(CultureInfo.CurrentUICulture, uiCulture) &&
               ReferenceEquals(CultureInfo.DefaultThreadCurrentCulture, threadCulture) &&
               ReferenceEquals(CultureInfo.DefaultThreadCurrentUICulture, threadUiCulture) &&
               UiText.CurLang == lang,
            "Expected the Hijri culture scope to restore every culture and the interface language.");
        Assert(BkpGregorian(name, out var stamp),
            $"Expected an invariant Gregorian snapshot folder name under ar-SA/UmAlQura, got '{name}'.");
        Assert(stamp >= before.AddSeconds(-1) && stamp <= after.AddSeconds(1) &&
               !name.StartsWith(before.ToString("yyyy", BkpHijri), StringComparison.Ordinal),
            $"Expected the snapshot folder name '{name}' to carry the Gregorian creation time, not the Hijri year.");

        Console.WriteLine("Backup snapshot folder name invariant Gregorian PASS.");
    }

    static void Bkp_LegacyHijri(string tempRoot)
    {
        var (install, target) = BkpNameInstall(tempRoot, "backup-name-legacy-hijri");
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var original = File.ReadAllBytes(target);
        var useCase = new BackupUseCase(
            new BackupService(),
            new Rpcs3MutationGuard(() => false));

        WithHijriCulture(() =>
        {
            var madeAt = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
            var legacy = HijriSnapshot(tempRoot, backupRoot, target, madeAt);
            var legacyFiles = BkpFiles(legacy.DirPath);
            File.WriteAllText(target, "Video:\r\n  Write Color Buffers: true\r\n", Encoding.UTF8);

            var listed = useCase.List(install)
                .Single(item => BkpSame(item.DirPath, legacy.DirPath));
            Assert(!listed.IsLegacy &&
                   listed.MadeAt == madeAt &&
                   listed.Files.Count == 1,
                "Expected a Hijri-named R3Bkp snapshot to be listed from its manifest.");

            useCase.Restore(install, listed);
            Assert(File.ReadAllBytes(target).SequenceEqual(original),
                "Expected a Hijri-named snapshot to restore its target byte-for-byte.");
            Assert(Directory.Exists(legacy.DirPath) &&
                   BkpSameFiles(legacyFiles, BkpFiles(legacy.DirPath)),
                "Expected restore to keep the Hijri-named snapshot folder, its name and its files unchanged.");

            var all = useCase.List(install);
            var preRestore = all[0];
            Assert(all.Count == 2 &&
                   !BkpSame(preRestore.DirPath, legacy.DirPath) &&
                   BkpGregorian(Path.GetFileName(preRestore.DirPath), out _) &&
                   BkpSame(all[1].DirPath, legacy.DirPath),
                "Expected the pre-restore snapshot to get a Gregorian name and list first by CreatedAt.");
            BkpInTemp(tempRoot, preRestore.DirPath);
        });

        Console.WriteLine("Hijri-named legacy backup list and restore PASS.");
    }

    static void Bkp_MixedNames(string tempRoot)
    {
        var (install, target) = BkpNameInstall(tempRoot, "backup-name-mixed");
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var service = new BackupService();
        var useCase = new BackupUseCase(
            service,
            new Rpcs3MutationGuard(() => false));

        WithHijriCulture(() =>
        {
            // Hijri names sort before Gregorian ones and later Gregorian
            // folders sort after earlier ones; CreatedAt says the opposite.
            var hijriNew = HijriSnapshot(
                tempRoot,
                backupRoot,
                target,
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
            var hijriOld = HijriSnapshot(
                tempRoot,
                backupRoot,
                target,
                new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
            var gregorian = Enumerable.Range(0, 11)
                .Select(index =>
                {
                    var built = BackupSnapshot(service, backupRoot, [target]);
                    Assert(BkpGregorian(Path.GetFileName(built.DirPath), out _),
                        $"Expected a new snapshot to get a Gregorian name, got '{built.DirPath}'.");
                    var manifest = new BackupManifest(
                        new DateTimeOffset(2026, 8, 1, 0, 10 - index, 0, TimeSpan.Zero),
                        built.Files);
                    BackupManifest(built.DirPath, manifest);
                    BkpInTemp(tempRoot, built.DirPath);
                    return new BackupSnapshot(built.DirPath, manifest.MadeAt, manifest.Files);
                })
                .ToArray();
            BackupSnapshot[] all = [hijriNew, hijriOld, .. gregorian];

            var expected = all
                .OrderByDescending(item => item.MadeAt)
                .Select(item => Path.GetFullPath(item.DirPath))
                .ToArray();
            var byName = all
                .OrderByDescending(item => Path.GetFileName(item.DirPath), StringComparer.Ordinal)
                .Select(item => Path.GetFullPath(item.DirPath))
                .ToArray();
            Assert(!expected.SequenceEqual(byName, StringComparer.OrdinalIgnoreCase) &&
                   !BkpSame(byName[0], hijriNew.DirPath),
                "The mixed fixture must order differently by folder name than by CreatedAt.");

            var listed = useCase.List(install);
            Assert(listed
                    .Select(item => Path.GetFullPath(item.DirPath))
                    .SequenceEqual(expected, StringComparer.OrdinalIgnoreCase),
                "Expected mixed Hijri and Gregorian snapshots to list by CreatedAt, not by folder name.");

            var advanced = new AdvVm();
            advanced.LoadBkp(listed);
            Assert(advanced.SelBkp is { } selected && BkpSame(selected.DirPath, hijriNew.DirPath),
                "Expected the newest snapshot by CreatedAt to be selected, even with a Hijri folder name.");

            var kept = expected
                .Take(BackupUseCase.DefaultRetentionCount)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keptByName = byName
                .Take(BackupUseCase.DefaultRetentionCount)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert(!kept.SetEquals(keptByName) &&
                   kept.Contains(Path.GetFullPath(hijriNew.DirPath)),
                "The mixed fixture must keep a different set by folder name than by CreatedAt.");

            var deleted = useCase.DeleteOld(install);
            var remaining = useCase.List(install)
                .Select(item => Path.GetFullPath(item.DirPath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert(deleted == all.Length - kept.Count &&
                   remaining.SetEquals(kept) &&
                   all.All(item => Directory.Exists(item.DirPath) ==
                                   kept.Contains(Path.GetFullPath(item.DirPath))),
                "Expected retention to keep the newest snapshots by CreatedAt, not by folder name.");
        });

        Console.WriteLine("Mixed Hijri and Gregorian backup ordering and retention PASS.");
    }

    static (Rpcs3InstallDescriptor Install, string Target) BkpNameInstall(
        string tempRoot,
        string name)
    {
        var root = Path.Combine(tempRoot, name);
        var customConfigsPath = Path.Combine(root, "config", "custom_configs");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var target = Path.Combine(customConfigsPath, "config_BLUS10001.yml");
        File.WriteAllText(
            target,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        BkpInTemp(
            tempRoot,
            install.CfgPath,
            Rpcs3AppPaths.BkpRoot(install),
            Rpcs3AppPaths.OldBkp(install),
            target);
        return (install, target);
    }

    // Reproduces a snapshot written by the culture-dependent builds: the
    // folder carries the Hijri stamp and the manifest points into it.
    static BackupSnapshot HijriSnapshot(
        string tempRoot,
        string backupRoot,
        string target,
        DateTimeOffset madeAt)
    {
        var built = BackupSnapshot(new BackupService(), backupRoot, [target]);
        var hijriDir = Path.Combine(
            backupRoot,
            string.Create(
                BkpHijri,
                $"{madeAt.LocalDateTime:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"));
        var hijriName = Path.GetFileName(hijriDir);
        Assert(BkpNamePattern.IsMatch(hijriName) &&
               !hijriName.StartsWith(
                   madeAt.LocalDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                   StringComparison.Ordinal),
            $"The Hijri fixture name '{hijriName}' must not be the Gregorian creation time.");
        BkpInTemp(tempRoot, built.DirPath, hijriDir);

        Directory.Move(built.DirPath, hijriDir);
        var manifest = new BackupManifest(
            madeAt,
            [
                .. built.Files.Select(entry => entry with
                {
                    BkpPath = Path.Combine(hijriDir, Path.GetFileName(entry.BkpPath))
                })
            ]);
        BackupManifest(hijriDir, manifest);
        return new BackupSnapshot(hijriDir, madeAt, manifest.Files);
    }

    // A Hijri stamp such as 14480512 also parses as a Gregorian date, so a
    // new snapshot name must also be close to the current Gregorian time.
    static bool BkpGregorian(
        string name,
        out DateTime stamp)
    {
        stamp = default;
        return BkpNamePattern.IsMatch(name) &&
               DateTime.TryParseExact(
                   name[..19],
                   "yyyyMMdd_HHmmss_fff",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out stamp) &&
               (DateTime.Now - stamp).Duration() < TimeSpan.FromHours(1);
    }

    // Interface in Arabic and Windows Region ar-SA with the Umm al-Qura
    // calendar; every culture and the interface language come back after.
    static void WithHijriCulture(Action action)
    {
        // UiText first: its static startup Apply switches every culture.
        var lang = UiText.CurLang;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var threadCulture = CultureInfo.DefaultThreadCurrentCulture;
        var threadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            UiText.SetLang(UiText.ArabLang);
            CultureInfo.CurrentCulture = BkpHijri;
            CultureInfo.DefaultThreadCurrentCulture = BkpHijri;
            var now = DateTime.Now;
            Assert(UiText.CurLang == UiText.ArabLang &&
                   CultureInfo.CurrentUICulture.Name == UiText.ArabLang &&
                   CultureInfo.CurrentCulture.DateTimeFormat.Calendar is UmAlQuraCalendar &&
                   now.ToString("yyyy", CultureInfo.CurrentCulture) !=
                   now.ToString("yyyy", CultureInfo.InvariantCulture),
                "The Hijri scope must run with an Arabic interface and an ar-SA Umm al-Qura culture.");
            action();
        }
        finally
        {
            try
            {
                UiText.SetLang(lang);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
                CultureInfo.DefaultThreadCurrentCulture = threadCulture;
                CultureInfo.DefaultThreadCurrentUICulture = threadUiCulture;
            }
        }
    }

    static CultureInfo CreateHijriCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(UiText.ArabLang).Clone();
        culture.DateTimeFormat.Calendar = new UmAlQuraCalendar();
        return CultureInfo.ReadOnly(culture);
    }

    // Every fixture path must stay under this run's temp root: never the
    // real R3Config LocalAppData folder or a real RPCS3 installation.
    static void BkpInTemp(
        string tempRoot,
        params string[] paths)
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        var root = Path.GetFullPath(tempRoot);
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "R3Config");
        Assert(BkpUnder(temp, root),
            $"The backup name fixture root '{root}' is outside the system temp directory.");
        foreach (var path in paths)
        {
            Assert(BkpUnder(root, path) && !BkpUnder(appData, path),
                $"The backup name fixture touched '{path}' outside its temp root.");
        }
    }

    static bool BkpUnder(
        string parentPath,
        string childPath)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(parentPath),
            Path.GetFullPath(childPath));
        return !Path.IsPathRooted(relative) &&
               relative != "." &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    static bool BkpSame(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    static Dictionary<string, byte[]> BkpFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);
    }

    static bool BkpSameFiles(
        Dictionary<string, byte[]> left,
        Dictionary<string, byte[]> right)
    {
        return left.Count == right.Count &&
               left.All(pair => right.TryGetValue(pair.Key, out var bytes) &&
                                bytes.SequenceEqual(pair.Value));
    }
}
