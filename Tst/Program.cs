using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using R3Config;
using AppSvc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;
using R3Config.View;

var tempRoot = Path.Combine(Path.GetTempPath(), "R3ConfigSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);

try
{
    RunLangKeyTests();
    RunAppPatchPresentationSourceTests();
    RunPatchCatalogReaderDuplicateTopLevelHashTests();
    RunGameListSearchTests();
    RunAdvancedDialogXamlSmokeTest();
    var env = CreateEnv(tempRoot);
    var hw = RunHwTests();
    var game = RunGameScanTests(env);
    var cat = RunCatTests(env);
    var cfg = RunRecStateTests(env.Install, game.Rogue, cat.Recommendation, hw.LowEnd);
    RunPlanVmVisibilityTests();
    RunBackupTests(env.Install, cfg.Plan, cfg.SelectedChanges);
    RunCfgApplyTests(env.Install, game.Rogue, cat.Recommendation, cfg.SelectedChanges);
    RunPatchTests(env, game.Rogue);
    if (args.Length > 0)
    {
        RunReadOnlyRpcs3InstallContract(args[0]);
    }

    Console.WriteLine("Smoke tests passed.");
}
finally
{
    if (Directory.Exists(tempRoot))
    {
        Directory.Delete(tempRoot, recursive: true);
    }
}


static void RunLangKeyTests()
{
    var repoRoot = FindRepoRoot();
    var langDir = Path.Combine(repoRoot, "App", "Lang");
    var arPath = Path.Combine(langDir, "ar-SA.json");
    var enPath = Path.Combine(langDir, "en-US.json");
    Assert(File.Exists(arPath), "Expected ar-SA.json to exist.");
    Assert(File.Exists(enPath), "Expected en-US.json to exist.");

    var ar = ReadLangFile(arPath);
    var en = ReadLangFile(enPath);
    Assert(new HashSet<string>(ar.Keys, StringComparer.Ordinal).SetEquals(en.Keys), "Expected ar-SA and en-US language keys to match exactly.");

    var requiredKeys = new[]
    {
        "App.Title",
        "Lang.Toggle",
        "Lang.Current",
        "Lang.ToggleTip",
        "Games.Title",
        "Games.Empty",
        "Games.FilterAll",
        "Games.FilterInstalled",
        "Games.FilterDetectedOnly",
        "Games.FilterDetectedOnlyTip",
        "Games.EmptyInstalled",
        "Patch.Source",
        "Patch.Source.Official",
        "Patch.Source.Canary",
        "Patch.Source.Local",
        "Patch.Source.Custom",
        "Patch.Source.Missing",
        "Patch.Source.Unknown",
        "Patch.Source.Tooltip",
        "Patch.CanaryNotice",
        "Patch.NoneForGame",
        "Patch.Localized.FileMissing",
        "Patch.Localized.RunAnalysisFirst",
        "Patch.Localized.NoPatchesForGame",
        "Patch.Localized.DuplicateKey",
        "Patch.Localized.ParseError",
        "Patch.Localized.UnsupportedPatchNote",
        "Shell.SettingsSection",
        "Shell.PatchesSection",
        "Plan.ApplySection",
        "Plan.ApplySectionTip",
        "Patch.ApplySection",
        "Patch.ApplySectionTip",
        "Patch.ImportCanaryTip",
        "Patch.SectionHint",
        "Handling.UserSelected",
        "Plan.ShowApplied",
        "Plan.SummaryFormat",
        "Patch.Empty",
        "Dialog.Ok",
        "Dialog.Cancel",
        "Patch.PrepareCanary",
        "Patch.PrepareCanaryTip",
        "Patch.PrepareCanarySuccess",
        "Patch.PrepareCanarySelectedSuccess",
        "Patch.PrepareCanaryAlreadyReady",
        "Patch.PrepareCanaryNoSelection",
        "Patch.PrepareCanaryAlreadyInImported",
        "Patch.RemoveCanary",
        "Patch.RemoveCanaryTip",
        "Patch.RemoveCanaryConfirm",
        "Patch.RemoveCanarySuccess",
        "Patch.RemoveCanaryNothing",
        "Patch.BundledCanaryMissing",
        "Patch.ImportCanary",
        "Patch.ImportCanaryManual",
        "Patch.ImportCanaryManualTip",
        "Patch.ImportCanaryTitle",
        "Patch.ImportCanaryPasteLabel",
        "Patch.ImportCanaryChooseFile",
        "Patch.ImportCanaryCreateFile",
        "Patch.ImportCanarySuccess",
        "Patch.ImportCanaryNoRpcs3Path",
        "Patch.ImportCanaryInvalidYaml",
        "Patch.ImportCanaryDuplicatePpuMerged",
        "Patch.ImportCanaryDuplicatePatchName",
        "Patch.ImportCanaryUnsafePath",
        "Patch.ImportCanaryBackupCreated",
        "Patch.ImportedFileInvalid",
        "Patch.OfficialReadWarning",
        "Patch.ImportedReadWarning",
        "Patch.ImportedDuplicateKey",
        "Patch.ImportedDuplicateKeyReadable",
        "Patch.ImportedDuplicateKeySuggestion",
        "PatchReadiness.HashReady",
        "PatchReadiness.HashReadyTip",
        "PatchReadiness.NeedsColdBoot",
        "PatchReadiness.NeedsColdBootTip",
        "PatchReadiness.NeedsColdBootWarning",
        "PatchReadiness.NotDetected",
        "PatchReadiness.NotDetectedTip",
        "Patch.DuplicateKeyReadable",
        "Patch.DuplicateKeySuggestion",
        "Patch.Note.NoLocalTranslation",
        "Patch.Note.OriginalText",
        "Patch.Note.WikiNote",
        "Patch.Note.Translation",
        "Patch.Note.Type",
        "Patch.Note.Source",
        "Msg.PatchCatWarning"
    };

    foreach (var key in requiredKeys)
    {
        Assert(ar.TryGetValue(key, out var arValue) && !string.IsNullOrWhiteSpace(arValue), "Required Arabic language value is empty: " + key);
        Assert(en.TryGetValue(key, out var enValue) && !string.IsNullOrWhiteSpace(enValue), "Required English language value is empty: " + key);
    }

    Console.WriteLine($"Lang keys: ar-SA/en-US parity PASS ({ar.Count}).");
}

static Dictionary<string, string> ReadLangFile(string path)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    Assert(document.RootElement.ValueKind == JsonValueKind.Object, Path.GetFileName(path) + " must be a JSON object.");

    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var property in document.RootElement.EnumerateObject())
    {
        Assert(values.TryAdd(property.Name, property.Value.GetString() ?? string.Empty), Path.GetFileName(path) + " has a duplicate key: " + property.Name);
    }

    return values;
}

static string FindRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "R3Config.slnx")) &&
            Directory.Exists(Path.Combine(current.FullName, "App", "Lang")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate R3Config repo root for language smoke tests.");
}

static void RunAppPatchPresentationSourceTests()
{
    var repoRoot = FindRepoRoot();
    var appRoot = Path.Combine(repoRoot, "App");
    var noteServicePath = Path.Combine(appRoot, "Svc", "PNote.cs");
    var importServicePath = Path.Combine(appRoot, "Svc", "CImp.cs");
    var bundledCanaryServicePath = Path.Combine(appRoot, "Svc", "BCan.cs");
    var bundledCanaryDataPath = Path.Combine(appRoot, "Data", "can.yml");
    var planVmPath = Path.Combine(appRoot, "Vm", "Plan.cs");
    var patchViewPath = Path.Combine(appRoot, "Ctrl", "Pat.xaml");
    var appDialogPath = Path.Combine(appRoot, "View", "Dlg", "AppDialog.xaml");
    Assert(File.Exists(noteServicePath), "Expected PatchNoteTranslationService to exist in App layer.");
    Assert(File.Exists(importServicePath), "Expected CanaryPatchImportService to exist in App layer.");
    Assert(File.Exists(bundledCanaryServicePath), "Expected BundledCanaryPatchService to exist in App layer.");
    Assert(File.Exists(bundledCanaryDataPath), "Expected bundled Canary patch data to exist in App/Data.");
    Assert(File.ReadAllText(planVmPath).Contains("_showApplied = true", StringComparison.Ordinal),
        "Expected plan view to show applied recommendations by default.");
    Assert(File.ReadAllText(appDialogPath).Contains("AllowsTransparency=\"True\"", StringComparison.Ordinal),
        "Expected custom app dialogs to avoid black rounded-corner artifacts.");
    var patchView = File.ReadAllText(patchViewPath);
    Assert(patchView.Contains("ApplyOfficialPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("ApplyCanaryPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("DisableOfficialPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("DisableCanaryPatchesCommand", StringComparison.Ordinal),
        "Expected each patch tab to execute only its own selected items.");
    Assert(!patchView.Contains("Command=\"{Binding PrepareCanaryPatchesCommand}\"", StringComparison.Ordinal),
        "Expected Canary preparation to be an internal enable step instead of a competing primary workflow.");
    Assert(patchView.Contains("DirectStatusText", StringComparison.Ordinal) &&
           patchView.Contains("InstalledVersionDisplay", StringComparison.Ordinal) == false,
        "Expected patch rows to render a direct actionable status without leaking internal property scaffolding.");

    var noteService = File.ReadAllText(noteServicePath);
    Assert(noteService.Contains("Prevents random freezing/crashing while playing the game", StringComparison.Ordinal),
        "Expected exact wiki note phrase mapping source.");
    Assert(noteService.Contains("يمنع التجمّد أو الانهيار العشوائي أثناء اللعب", StringComparison.Ordinal),
        "Expected exact Arabic wiki note translation source.");
    Assert(noteService.Contains("No local translation", StringComparison.Ordinal) || noteService.Contains("Patch.Note.NoLocalTranslation", StringComparison.Ordinal),
        "Expected note fallback source.");

    var installServicePath = Path.Combine(repoRoot, "Core", "Svc", "Scan", "Inst.cs");
    var installService = File.ReadAllText(installServicePath);
    Assert(installService.Contains("Path.Combine(rootPath, \"config\")", StringComparison.Ordinal),
        "Expected Rpcs3InstallService to anchor config to root config.");
    Assert(installService.Contains("Path.Combine(configPath, \"custom_configs\")", StringComparison.Ordinal),
        "Expected Rpcs3InstallService to anchor custom_configs below root config.");

    var importService = File.ReadAllText(importServicePath);
    Assert(importService.Contains("Version: 1.2", StringComparison.Ordinal),
        "Expected imported_patch.yml Version header normalization source.");
    Assert(importService.Contains("\"\\r\\n\"", StringComparison.Ordinal),
        "Expected imported_patch.yml normalization to force Windows CRLF line endings.");
    Assert(importService.Contains("DuplicatePpuMerged", StringComparison.Ordinal),
        "Expected duplicate PPU merge presentation result source.");
    Assert(importService.Contains("FilterDuplicatePatchBlocks", StringComparison.Ordinal),
        "Expected duplicate Canary patch names to be handled by a deterministic skip path.");
    Assert(importService.Contains("WriteManagedImportedPatch", StringComparison.Ordinal),
        "Expected managed Canary writes to preserve manual imported_patch.yml content.");
    Assert(importService.Contains("RemoveManagedImportedPatch", StringComparison.Ordinal),
        "Expected managed Canary removal path.");
    Assert(importService.Contains("ExtractNormalizedPpuSections", StringComparison.Ordinal),
        "Expected managed Canary compose to merge inside existing hash blocks.");
    Assert(importService.Contains("RemovePatchBlocksAlreadyPresent", StringComparison.Ordinal),
        "Expected managed Canary writes to skip duplicate patch names without failing.");
    Assert(importService.Contains("IsPathInsideDirectory", StringComparison.Ordinal),
        "Expected safe path write guard source.");

    var bundledService = File.ReadAllText(bundledCanaryServicePath);
    Assert(bundledService.Contains("can.yml", StringComparison.Ordinal),
        "Expected bundled Canary service to reference the bundled data file.");
    Assert(bundledService.Contains("PrepareImportedPatch", StringComparison.Ordinal),
        "Expected bundled Canary service to prepare imported_patch.yml.");
    Assert(bundledService.Contains("BuildSelectedCanaryText", StringComparison.Ordinal),
        "Expected bundled Canary service to write only selected Canary patches.");

    var bundledCanaryText = File.ReadAllText(bundledCanaryDataPath);
    Assert(bundledCanaryText.StartsWith("# R3Config bundled RPCS3 Canary patches", StringComparison.Ordinal),
        "Expected bundled Canary data source marker.");
    Assert(bundledCanaryText.Contains("Version: 1.2", StringComparison.Ordinal),
        "Expected bundled Canary data to use RPCS3 patch version 1.2.");
    Assert(bundledCanaryText.Contains("PPU-", StringComparison.Ordinal),
        "Expected bundled Canary data to include PPU patch entries.");

    var normalizedCanary = new CanaryPatchImportService().Normalize(bundledCanaryText);
    Assert(normalizedCanary.Succeeded, "Expected bundled Canary data to normalize without duplicate-name failures: " + normalizedCanary.Error);

    var canaryRead = new PatchCatalogReader().ReadText(normalizedCanary.NormalizedText, isCanarySource: true);
    Assert(canaryRead.Succeeded, "Expected bundled Canary data to be parseable after normalization: " + canaryRead.Error);
    Assert(canaryRead.Catalog.Entries.Count > 0, "Expected bundled Canary data to include patch catalog entries.");
    Assert(canaryRead.Catalog.Entries.All(entry => entry.Risk == PatchRisk.Canary),
        "Expected bundled Canary entries to be classified as Canary.");

    Console.WriteLine("App patch presentation source checks passed.");
}

static void RunPatchCatalogReaderDuplicateTopLevelHashTests()
{
    var reader = new PatchCatalogReader();
    var read = reader.ReadText(BuildDuplicateTopLevelHashPatchYml());

    Assert(read.Succeeded, "Expected duplicate top-level RPCS3 hash sections to read successfully: " + read.Error);
    Assert(read.Catalog.Entries.Count == 2, "Expected both duplicate hash sections to contribute patches.");
    Assert(read.Catalog.Entries.All(patch => patch.Hash == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
        "Expected duplicate hash sections to preserve their original PPU hash.");
    Assert(read.Catalog.Entries.Any(patch => patch.Title == "First Patch"),
        "Expected first duplicate hash section to remain visible.");
    Assert(read.Catalog.Entries.Any(patch => patch.Title == "Second Patch"),
        "Expected second duplicate hash section to remain visible.");

    Console.WriteLine("Patch catalog duplicate top-level hash smoke PASS.");
}

static void RunGameListSearchTests()
{
    var list = new GameListVm();
    list.Load(
    [
        MakeGame("NPEA00137", ".detuned", isDetectedInRpcs3: true),
        MakeGame("BLUS30443", "Demon's Souls", isDetectedInRpcs3: false),
        MakeGame("BCES00569", "Gran Turismo 5", isDetectedInRpcs3: true)
    ]);

    Assert(list.Games.Count == 2, "Expected detected-only filter to show detected games by default.");
    Assert(list.IsSearchEmpty, "Expected initial game search to be empty.");

    list.SearchText = "gran";
    Assert(list.Games.Count == 1 && list.Games[0].Serial == "BCES00569", "Expected game search to match title.");

    list.SearchText = "NPEA00137";
    Assert(list.Games.Count == 1 && list.Games[0].Title == ".detuned", "Expected game search to match serial.");

    list.SearchText = "souls";
    Assert(list.Games.Count == 0 && list.EmptyStateText == UiText.T("Games.EmptySearch"),
        "Expected search with no visible detected result to show search empty state.");

    list.ShowOnlyDetectedInRpcs3 = false;
    Assert(list.Games.Count == 1 && list.Games[0].Serial == "BLUS30443",
        "Expected search to include catalog-only games when detected-only filter is disabled.");

    Console.WriteLine("Game list search/filter smoke PASS.");
}

static GameItemVm MakeGame(string serial, string title, bool isDetectedInRpcs3)
{
    var descriptor = new Rpcs3GameDescriptor(serial, title, @"C:\Games\" + serial, "01.00", null, null);
    var summary = new RecStateSummary(serial, 0, 0, 0, 0, 0, 0, []);
    return new GameItemVm(descriptor, null, summary, serial, title, isDetectedInRpcs3);
}

static void RunAdvancedDialogXamlSmokeTest()
{
    Exception? threadException = null;

    var thread = new Thread(() =>
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            UiText.SetLang(UiText.ArabicLang);
            UiLayoutService.ApplyApplicationResources();

            var dialog = new AdvancedDialog();
            dialog.Close();

            Console.WriteLine("Advanced dialog XAML smoke PASS.");
        }
        catch (Exception ex)
        {
            threadException = ex;
        }
        finally
        {
            Application.Current?.Shutdown();
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (threadException is not null)
    {
        throw new InvalidOperationException("AdvancedDialog XAML smoke failed.", threadException);
    }
}

static SmokeEnv CreateEnv(string tempRoot)
{
    var fakeRpcs3Root = Path.Combine(tempRoot, "rpcs3");
    var configPath = Path.Combine(fakeRpcs3Root, "config");
    var gameFolder = Path.Combine(tempRoot, "games", "Rogue");
    Directory.CreateDirectory(configPath);
    Directory.CreateDirectory(Path.Combine(gameFolder, "PS3_GAME"));
    var hddGameFolder = Path.Combine(fakeRpcs3Root, "dev_hdd0", "game", "NPUB00001");
    Directory.CreateDirectory(hddGameFolder);
    Directory.CreateDirectory(Path.Combine(fakeRpcs3Root, "patches"));
    File.WriteAllText(Path.Combine(fakeRpcs3Root, "rpcs3.exe"), string.Empty);
    File.WriteAllText(Path.Combine(configPath, "games.yml"), $"BLUS31465: \"{EscapeYamlPath(gameFolder)}\"{Environment.NewLine}");
    WriteParamSfo(Path.Combine(gameFolder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
    {
        ["TITLE"] = "Assassin's Creed Rogue SFO",
        ["TITLE_ID"] = "BLUS31465",
        ["APP_VER"] = "01.02"
    });
    WriteParamSfo(Path.Combine(hddGameFolder, "PARAM.SFO"), new Dictionary<string, string>
    {
        ["TITLE"] = "Non Portable HDD Game",
        ["TITLE_ID"] = "NPUB00001",
        ["APP_VER"] = "01.00"
    });

    var wikiXmlPath = Path.Combine(tempRoot, "wiki.xml");
    File.WriteAllText(wikiXmlPath, BuildWikiXml());

    var install = Rpcs3InstallService.FromExePath(Path.Combine(fakeRpcs3Root, "rpcs3.exe"));
    Assert(install.ConfigPath == configPath, "Expected config path detection.");
    Assert(install.PatchesPath == Path.Combine(fakeRpcs3Root, "patches"),
        "Expected patches folder to be anchored to the RPCS3 executable folder.");
    Console.WriteLine($"Config: {install.ConfigPath}");

    return new SmokeEnv(
        TempRoot: tempRoot,
        FakeRpcs3Root: fakeRpcs3Root,
        ConfigPath: configPath,
        GameFolder: gameFolder,
        WikiXmlPath: wikiXmlPath,
        Install: install);
}

static HwState RunHwTests()
{
    var highEndHardware = new HwProbe(
        readCpuName: () => "Intel Core i5-12600KF",
        readLogicalProcessors: () => 16,
        readTotalMemoryBytes: () => 32UL * 1024UL * 1024UL * 1024UL,
        readGpuName: () => "NVIDIA GeForce RTX 3060").Probe();
    Assert(highEndHardware.Tier == HwTier.High,
        "Expected high-end hardware profile from CPU/RAM/GPU probe data.");
    Assert(highEndHardware.CpuName.Contains("12600KF", StringComparison.OrdinalIgnoreCase),
        "Expected CPU name to be preserved.");
    Assert(highEndHardware.TotalMemoryGiB >= 31.9,
        "Expected total RAM to be reported in GiB.");
    Assert(highEndHardware.GpuName.Contains("RTX 3060", StringComparison.OrdinalIgnoreCase),
        "Expected GPU name to be preserved.");

    var balancedHardware = new HwProbe(
        readCpuName: () => "Ryzen 5 Test CPU",
        readLogicalProcessors: () => 6,
        readTotalMemoryBytes: () => 16UL * 1024UL * 1024UL * 1024UL,
        readGpuName: () => "Test GPU").Probe();
    Assert(balancedHardware.Tier == HwTier.Mid,
        "Expected balanced hardware profile from moderate CPU/RAM data.");

    var lowEndHardware = new HwProbe(
        readCpuName: () => "Dual Core Test CPU",
        readLogicalProcessors: () => 2,
        readTotalMemoryBytes: () => 4UL * 1024UL * 1024UL * 1024UL,
        readGpuName: () => "Basic Display Adapter").Probe();
    Assert(lowEndHardware.Tier == HwTier.Low,
        "Expected low-end hardware profile from low CPU/RAM data.");

    var failedHardware = new HwProbe(
        readCpuName: () => throw new IOException("Simulated CPU probe failure."),
        readLogicalProcessors: () => 0,
        readTotalMemoryBytes: () => null,
        readGpuName: () => null).Probe();
    Assert(failedHardware.Tier == HwTier.Unknown && failedHardware.IsFallback,
        "Expected hardware probe failure to return Unknown safely.");

    return new HwState(lowEndHardware);
}

static GameScanState RunGameScanTests(SmokeEnv env)
{
    var games = new GameScanner().Scan(env.Install);
    Assert(games.Count == 2, "Expected one game from games.yml and one non-portable dev_hdd0 game.");
    var rogue = games.Single(game => game.Serial == "BLUS31465");
    Assert(rogue.Title == "Assassin's Creed Rogue SFO", "Expected title from PARAM.SFO.");
    Assert(rogue.Version == "01.02", "Expected app version from PARAM.SFO.");
    Assert(games.Any(game => game.Serial == "NPUB00001"), "Expected non-portable dev_hdd0 game.");
    Console.WriteLine($"Game: {rogue.Serial} {rogue.Title} {rogue.Version}");

    var updateFolder = Path.Combine(env.FakeRpcs3Root, "dev_hdd0", "game", rogue.Serial);
    Directory.CreateDirectory(updateFolder);
    WriteParamSfo(Path.Combine(updateFolder, "PARAM.SFO"), new Dictionary<string, string>
    {
        ["TITLE"] = rogue.Title,
        ["TITLE_ID"] = rogue.Serial,
        ["APP_VER"] = "01.03"
    });
    var updatedRogue = new GameScanner().Scan(env.Install).Single(game => game.Serial == rogue.Serial);
    Assert(updatedRogue.Version == "01.03",
        "Expected the installed dev_hdd0 update version to override the older disc PARAM.SFO version.");
    Directory.Delete(updateFolder, recursive: true);

    var portableRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-portable");
    var portableGameFolder = Path.Combine(portableRpcs3Root, "portable", "dev_hdd0", "game", "NPEB00002");
    Directory.CreateDirectory(Path.Combine(portableRpcs3Root, "portable", "config"));
    Directory.CreateDirectory(portableGameFolder);
    File.WriteAllText(Path.Combine(portableRpcs3Root, "rpcs3.exe"), string.Empty);
    WriteParamSfo(Path.Combine(portableGameFolder, "PARAM.SFO"), new Dictionary<string, string>
    {
        ["TITLE"] = "Portable HDD Game",
        ["TITLE_ID"] = "NPEB00002",
        ["APP_VER"] = "02.00"
    });

    var portableInstall = Rpcs3InstallService.FromExePath(Path.Combine(portableRpcs3Root, "rpcs3.exe"));
    Assert(portableInstall.ConfigPath == Path.Combine(portableRpcs3Root, "config"),
        "Expected config path to stay anchored at <RPCS3 folder>\\config for per-game custom configs.");
    Assert(portableInstall.CustomConfigsPath == Path.Combine(portableRpcs3Root, "config", "custom_configs"),
        "Expected custom configs to stay anchored at <RPCS3 folder>\\config\\custom_configs.");
    Assert(portableInstall.PatchesPath == Path.Combine(portableRpcs3Root, "patches"),
        "Expected portable installs to still use <RPCS3 folder>\\patches for imported_patch.yml.");
    var portableGames = new GameScanner().Scan(portableInstall);
    Assert(portableGames.Any(game => game.Serial == "NPEB00002"), "Expected portable dev_hdd0 game.");

    var legacyRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-legacy-games-yml");
    var legacyConfigPath = Path.Combine(legacyRpcs3Root, "config");
    var legacyGameFolder = Path.Combine(env.TempRoot, "legacy-games", "BLUS00003");
    Directory.CreateDirectory(legacyConfigPath);
    Directory.CreateDirectory(Path.Combine(legacyGameFolder, "PS3_GAME"));
    File.WriteAllText(Path.Combine(legacyRpcs3Root, "rpcs3.exe"), string.Empty);
    WriteParamSfo(Path.Combine(legacyGameFolder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
    {
        ["TITLE"] = "Legacy Root Games YML",
        ["TITLE_ID"] = "BLUS00003",
        ["APP_VER"] = "01.00"
    });
    File.WriteAllText(Path.Combine(legacyRpcs3Root, "games.yml"), $"BLUS00003: \"{EscapeYamlPath(legacyGameFolder)}\"{Environment.NewLine}");
    var legacyInstall = Rpcs3InstallService.FromExePath(Path.Combine(legacyRpcs3Root, "rpcs3.exe"));
    Assert(new GameScanner().Scan(legacyInstall).Any(game => game.Serial == "BLUS00003"),
        "Expected legacy root-level games.yml to be scanned for older RPCS3 layouts.");

    var missingConfigRoot = Path.Combine(env.TempRoot, "rpcs3-missing-config");
    Directory.CreateDirectory(missingConfigRoot);
    File.WriteAllText(Path.Combine(missingConfigRoot, "rpcs3.exe"), string.Empty);
    var missingConfigInstall = Rpcs3InstallService.FromExePath(Path.Combine(missingConfigRoot, "rpcs3.exe"));
    Assert(new GameScanner().Scan(missingConfigInstall).Count == 0,
        "Expected missing config folder to produce an empty game list without crashing.");

    File.WriteAllText(Path.Combine(env.ConfigPath, "games.yml"), "BLUS31465: [broken");
    var corruptGamesYmlScan = new GameScanner().Scan(env.Install);
    Assert(corruptGamesYmlScan.Any(game => game.Serial == "NPUB00001"),
        "Expected corrupt games.yml not to block dev_hdd0 scanning.");
    File.WriteAllText(Path.Combine(env.ConfigPath, "games.yml"), $"BLUS31465: \"{EscapeYamlPath(env.GameFolder)}\"{Environment.NewLine}");

    var largeRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-large-list");
    var largeConfigPath = Path.Combine(largeRpcs3Root, "config");
    var largeGamesRoot = Path.Combine(env.TempRoot, "large-games");
    Directory.CreateDirectory(largeConfigPath);
    Directory.CreateDirectory(largeGamesRoot);
    File.WriteAllText(Path.Combine(largeRpcs3Root, "rpcs3.exe"), string.Empty);
    var largeGamesYml = new StringBuilder();
    const int largeGameCount = 150;
    for (var index = 0; index < largeGameCount; index++)
    {
        var serial = $"BLUS{index:00000}";
        var folder = Path.Combine(largeGamesRoot, serial);
        Directory.CreateDirectory(Path.Combine(folder, "PS3_GAME"));
        WriteParamSfo(Path.Combine(folder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = $"Large List Game {index}",
            ["TITLE_ID"] = serial,
            ["APP_VER"] = "01.00"
        });
        largeGamesYml.AppendLine($"{serial}: \"{EscapeYamlPath(folder)}\"");
    }
    File.WriteAllText(Path.Combine(largeConfigPath, "games.yml"), largeGamesYml.ToString());
    var largeInstall = Rpcs3InstallService.FromExePath(Path.Combine(largeRpcs3Root, "rpcs3.exe"));
    Assert(new GameScanner().Scan(largeInstall).Count == largeGameCount,
        "Expected large games list to scan completely.");

    var truncatedSfo = Path.Combine(env.TempRoot, "truncated.sfo");
    File.WriteAllBytes(truncatedSfo, [0x00, 0x50, 0x53, 0x46]);
    Assert(ParamSfoReader.TryRead(truncatedSfo) is null, "Expected truncated PARAM.SFO to fail safely.");

    var invalidOffsetSfo = Path.Combine(env.TempRoot, "invalid-offset.sfo");
    WriteInvalidOffsetParamSfo(invalidOffsetSfo);
    Assert(ParamSfoReader.TryRead(invalidOffsetSfo) is null, "Expected invalid PARAM.SFO offsets to fail safely.");

    return new GameScanState(rogue);
}

static CatState RunCatTests(SmokeEnv env)
{
    var wiki = WikiRecommendationService.Load(env.WikiXmlPath);
    Assert(wiki.Find("BLUS31465") is not null, "Expected BLUS31465 wiki recommendation.");
    Assert(wiki.Find("BLES02061") is not null, "Expected second gameid serial to be indexed.");
    Assert(wiki.Find("NPUB31547") is not null, "Expected serial from a second gameid template to be indexed.");

    var catalogPath = Path.Combine(env.TempRoot, "cat.json");
    RecommendationCatalogService.Save(catalogPath, "2099-01-02", wiki.Recommendations);
    var catalogInfoService = new CatInfoService();
    var catalog = catalogInfoService.LoadCatalog(catalogPath);
    Assert(catalog.DataSource == "Built-in", "Expected built-in catalog source.");
    Assert(catalog.DataVersion == "2099-01-02", "Expected catalog data version.");
    Assert(catalog.Info.RecommendationCount == catalog.Count, "Expected catalog info count to be computed from loaded recommendations.");
    Assert(catalog.SourceHash.Length == 64, "Expected catalog source hash to be a SHA-256 hex string.");
    Assert(catalog.GeneratedAt is not null, "Expected catalog generated timestamp to be available.");
    Assert(catalog.Find("BLUS31465") is not null, "Expected catalog lookup to work without Wiki XML.");

    try
    {
        catalogInfoService.LoadCatalog(Path.Combine(env.TempRoot, "missing-cat.json"));
        throw new InvalidOperationException("Expected missing recommendation catalog to fail safely.");
    }
    catch (CatalogMissingException ex)
    {
        Assert(ex.Message == "Recommendation data is missing", "Expected missing catalog error message contract.");
    }

    var invalidCatalogPath = Path.Combine(env.TempRoot, "invalid-cat.json");
    File.WriteAllText(invalidCatalogPath, "{ invalid json");
    try
    {
        catalogInfoService.LoadCatalog(invalidCatalogPath);
        throw new InvalidOperationException("Expected invalid recommendation catalog to fail safely.");
    }
    catch (CatalogInvalidException ex)
    {
        Assert(ex.Message == "Recommendation data is invalid", "Expected invalid catalog error message contract.");
    }

    var wrongRiskCatalogPath = Path.Combine(env.TempRoot, "wrong-risk-cat.json");
    File.WriteAllText(wrongRiskCatalogPath, BuildWrongRiskCatalogJson());
    var wrongRiskCatalog = catalogInfoService.LoadCatalog(wrongRiskCatalogPath);
    Assert(wrongRiskCatalog.Info.RecommendationCount == 1, "Expected stored RecommendationCount to be ignored and recomputed.");
    Assert(wrongRiskCatalog.Find("TEST00001")!.Changes.Single().Risk == RecommendationRisk.PerformanceCost,
        "Expected catalog load to reapply RecommendationRiskPolicy instead of trusting stored Risk.");

    var recommendation = catalog.Find("BLUS31465")!;
    Assert(recommendation.Changes.Any(change => change.Key == "PPU Decoder" && change.Value == "Interpreter (static)"),
        "Expected PPU Decoder recommendation.");
    Assert(recommendation.Changes.Any(change => change.Key == "PPU Decoder" && change.Risk == RecommendationRisk.PerformanceCost),
        "Expected interpreter decoder changes to be classified as PerformanceCost.");
    Assert(recommendation.Changes.Any(change => change.Key == "Write Color Buffers" && change.Value == "true"),
        "Expected Write Color Buffers recommendation.");
    Assert(recommendation.Changes.Any(change => change.Key == "Write Color Buffers" && change.Risk == RecommendationRisk.Recommended),
        "Expected safe visual fix to remain Recommended.");
    Assert(recommendation.Changes.Any(change => change.Key == "Resolution Scale" && change.Value == "150"),
        "Expected percentage values to be normalized.");
    Assert(recommendation.Changes.Any(change => change.Key == "Resolution Scale" && change.Risk == RecommendationRisk.HardwareDependent),
        "Expected resolution scale to be classified as HardwareDependent.");
    Assert(recommendation.Changes.Any(change => change.Key == "Vblank Rate" && change.Value == "120" && change.Risk == RecommendationRisk.ManualTiming),
        "Expected Vblank Rate to be classified as ManualTiming.");
    Assert(recommendation.Changes.Any(change => change.Key == "Read Color Buffers" && change.Risk == RecommendationRisk.Conditional),
        "Expected may-cause notes to be classified as Conditional.");
    Assert(recommendation.Changes.Any(change => change.Key == "Write Depth Buffer" && change.Risk == RecommendationRisk.PatchDependent),
        "Expected patch-dependent notes to be classified as PatchDependent.");
    Assert(recommendation.Changes.Any(change => change.Key == "Strict Rendering Mode" && change.Risk == RecommendationRisk.PerformanceCost),
        "Expected performance-cost notes to be classified as PerformanceCost.");

    return new CatState(recommendation);
}

static CfgPlanState RunRecStateTests(
    Rpcs3InstallDescriptor install,
    Rpcs3GameDescriptor rogue,
    WikiGameRecommendation recommendation,
    HwInfo lowEndHardware)
{
    var plan = new PlanService().CreateConfigPlan(install, rogue, recommendation);
    var vblank = plan.Items.Single(item => item.Key == "Vblank Rate");
    var resolutionScale = plan.Items.Single(item => item.Key == "Resolution Scale");
    Assert(!RecommendationRiskPolicy.ShouldSelectByDefault(vblank), "Vblank must not be selected by default.");
    Assert(!RecommendationRiskPolicy.ShouldSelectByDefault(resolutionScale), "Hardware-dependent setting must not be selected by default.");

    var selectedChanges = plan.Items
        .Where(RecommendationRiskPolicy.ShouldSelectByDefault)
        .Select(item => item.Change)
        .ToArray();
    Assert(selectedChanges.Length == 1, "Expected only safe/recommended changes to be selected.");
    Assert(selectedChanges.Single().Key == "Write Color Buffers", "Expected only Write Color Buffers to be selected by default.");

    var recStateService = new RecStateService();
    var recStateRecommendation = BuildRecStateRecommendation();
    var emptyState = recStateService.Evaluate(rogue, [recStateRecommendation], (_, _) => null);
    Assert(emptyState.Serial == "BLUS31465", "Expected RecState summary to keep the game serial.");
    Assert(emptyState.Total == 3, "Expected BLUS31465 RecState total to be 3.");
    Assert(emptyState.Applied == 0 && emptyState.Pending == 3,
        "Expected empty config to have 0 applied and 3 pending recommendations.");

    var appliedValues = new Dictionary<string, string>
    {
        ["Video/Write Color Buffers"] = "On",
        ["Video/Read Color Buffers"] = "true",
        ["Video/Resolution Scale"] = "100%"
    };
    var appliedState = recStateService.Evaluate(
        rogue,
        [recStateRecommendation],
        (section, key) => appliedValues.TryGetValue($"{section}/{key}", out var value) ? value : null);
    Assert(appliedState.Total == 3 && appliedState.Applied == 3 && appliedState.Pending == 0,
        "Expected normalized current values to count as fully applied.");

    var manualState = recStateService.Evaluate(
        rogue,
        [BuildManualTimingRecommendation()],
        (_, _) => "60");
    var manualItem = manualState.Items.Single();
    Assert(manualItem.State == RecState.Manual && !manualItem.IsAutoSelectable,
        "Expected ManualTiming to be Manual and not auto-selectable.");

    var lowEndState = recStateService.Evaluate(
        rogue,
        [BuildLowEndBlockedRecommendation()],
        (_, _) => "100",
        lowEndHardware);
    var lowEndItem = lowEndState.Items.Single();
    Assert(lowEndItem.State is RecState.Blocked or RecState.Manual,
        "Expected LowEnd visual/performance recommendation to be blocked or manual.");
    Assert(!lowEndItem.IsAutoSelectable,
        "Expected LowEnd visual/performance recommendation not to be auto-selectable.");

    return new CfgPlanState(plan, selectedChanges);
}

static void RunPlanVmVisibilityTests()
{
    var change = new ConfigChange(
        "Video",
        "Write Color Buffers",
        "true",
        "write color buffers",
        null,
        RecommendationRisk.Recommended);

    var state = new RecStateItem(
        "TEST00001",
        change.DisplayPath,
        "true",
        "true",
        RecommendationRisk.Recommended,
        RecState.Applied,
        false,
        false,
        false,
        string.Empty);

    var plan = new ConfigPlan(
        "TEST00001",
        "Applied Smoke Game",
        "config.yml",
        [new ConfigPlanEntry(change, "true", state)],
        new RecStateSummary("TEST00001", 1, 1, 0, 0, 0, 0, [state]),
        false);

    var viewModel = new PlanVm();
    viewModel.Load(plan);

    Assert(viewModel.Items.Count == 1,
        "Expected fully applied recommendations to remain visible instead of disappearing.");
    Assert(!viewModel.IsEmpty,
        "Expected fully applied recommendation view not to enter the empty state.");
    Assert(viewModel.Items.Single().IsSelected,
        "Expected applied recommendations to remain visibly checked as proof that they are already configured.");
    viewModel.ClearSelection();
    Assert(viewModel.Items.Single().IsSelected,
        "Expected applied recommendations to stay checked even when clearing pending selections.");
}

static void RunBackupTests(
    Rpcs3InstallDescriptor install,
    ConfigPlan plan,
    IReadOnlyList<ConfigChange> selectedChanges)
{
    var backupService = new BackupService();
    var backup = backupService.CreateBackup(Path.Combine(install.ConfigPath, "R3ConfigBackups"), [plan.TargetConfigPath]);
    var secondBackup = backupService.CreateBackup(Path.Combine(install.ConfigPath, "R3ConfigBackups"), [plan.TargetConfigPath]);
    Assert(backupService.ListBackups(Path.Combine(install.ConfigPath, "R3ConfigBackups")).Count == 2,
        "Expected two backups created within the same second to have unique directories.");
    Assert(backup.Files[0].BackupPath != secondBackup.Files[0].BackupPath,
        "Expected backup file paths to be unique.");
    Rpcs3ConfigYaml.ApplyChanges(plan.TargetConfigPath, selectedChanges);

    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Core", "PPU Decoder") is null,
        "Expected performance-cost PPU Decoder not to be written by default.");
    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Video", "Write Color Buffers") == "true",
        "Expected written Write Color Buffers.");
    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Video", "Read Color Buffers") is null,
        "Expected conditional Read Color Buffers not to be written by default.");
    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Video", "Write Depth Buffer") is null,
        "Expected patch-dependent Write Depth Buffer not to be written by default.");
    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Video", "Strict Rendering Mode") is null,
        "Expected performance-cost Strict Rendering Mode not to be written by default.");
    Assert(Rpcs3ConfigYaml.ReadValue(plan.TargetConfigPath, "Video", "Vblank Rate") is null,
        "Expected unchecked Vblank not to be written.");

    var backups = backupService.ListBackups(Path.Combine(install.ConfigPath, "R3ConfigBackups"));
    Assert(backups.Count == 2, "Expected persisted backups to be discoverable.");

    backupService.Restore(backup);
    Assert(!File.Exists(plan.TargetConfigPath), "Expected restore to remove a file that did not exist before backup.");
}

static void RunCfgApplyTests(
    Rpcs3InstallDescriptor install,
    Rpcs3GameDescriptor rogue,
    WikiGameRecommendation recommendation,
    IReadOnlyList<ConfigChange> selectedChanges)
{
    var plan = new PlanService().CreateConfigPlan(install, rogue, recommendation);
    Directory.CreateDirectory(Path.GetDirectoryName(plan.TargetConfigPath)!);
    File.WriteAllText(plan.TargetConfigPath, $"Video:{Environment.NewLine}  Write Color Buffers: false{Environment.NewLine}");
    var existingCustomPlan = new PlanService().CreateConfigPlan(install, rogue, recommendation);
    Assert(existingCustomPlan.Items.Single(item => item.Key == "Write Color Buffers").CurrentValue == "false",
        "Expected existing custom config value to be detected.");
    var originalCustomConfigText = File.ReadAllText(plan.TargetConfigPath);
    var failingConfigApply = new ConfigApplyService(applyChanges: (path, changes) =>
    {
        File.WriteAllText(path, "Video: [partial write");
        throw new UnauthorizedAccessException("Simulated permission denied.");
    });
    try
    {
        failingConfigApply.Apply(existingCustomPlan, selectedChanges, Path.Combine(install.ConfigPath, "R3ConfigBackups"));
        throw new InvalidOperationException("Expected config apply failure to be surfaced.");
    }
    catch (UnauthorizedAccessException)
    {
        Assert(File.ReadAllText(plan.TargetConfigPath) == originalCustomConfigText,
            "Expected failed config write to restore the previous custom config.");
    }

    File.WriteAllText(plan.TargetConfigPath, "Video: [broken");
    var corruptCustomConfigPlan = new PlanService().CreateConfigPlan(install, rogue, recommendation);
    Assert(corruptCustomConfigPlan.Items.Any(item => item.CurrentValue is null),
        "Expected corrupt custom config to be tolerated during plan preview.");
    Assert(corruptCustomConfigPlan.Items.All(item => item.State == RecState.Unknown),
        "Expected unreadable current config to mark RecState as Unknown.");
    File.Delete(plan.TargetConfigPath);
}

static void RunCanaryImportFileContractTests(SmokeEnv env, string officialPatchYmlPath)
{
    var importService = new CanaryPatchImportService();
    var officialBefore = File.ReadAllText(officialPatchYmlPath);
    var importedPath = Path.Combine(env.Install.PatchesPath, CanaryPatchImportService.ImportedPatchFileName);

    var canaryPatchText =
        "PPU-1234567890abcdef1234567890abcdef12345678:\n" +
        "  \"Canary Smoke Patch\":\n" +
        "    Games:\n" +
        "      \"Smoke Game\":\n" +
        "        BLUS31465: [ 01.02 ]\n" +
        "    Author: \"Smoke\"\n" +
        "    Notes: \"Canary smoke test.\"\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000000, 0x60000000 ]\n";

    var result = importService.WriteImportedPatch(env.Install.PatchesPath, canaryPatchText);

    Assert(result.Succeeded, "Expected Canary import to write imported_patch.yml: " + result.Error);
    Assert(result.TargetPath == importedPath, "Expected Canary import target to be imported_patch.yml in <RPCS3 folder>\\patches.");
    Assert(File.Exists(importedPath), "Expected imported_patch.yml to exist after Canary import.");
    Assert(File.ReadAllText(importedPath).StartsWith(CanaryPatchImportService.RequiredVersionHeader, StringComparison.Ordinal),
        "Expected imported_patch.yml to start with Version: 1.2.");
    Assert(File.ReadAllText(officialPatchYmlPath) == officialBefore,
        "Expected Canary import not to modify official patch.yml.");

    var officialRead = new PatchCatalogReader().Read(officialPatchYmlPath);
    var importedRead = new PatchCatalogReader().Read(importedPath, isCanarySource: true);
    Assert(officialRead.Succeeded, "Expected official patch.yml to remain readable after Canary import.");
    Assert(importedRead.Succeeded, "Expected imported_patch.yml to be readable after Canary import: " + importedRead.Error);
    Assert(importedRead.Catalog.Entries.Any(patch => patch.Title == "Canary Smoke Patch" && patch.Risk == PatchRisk.Canary),
        "Expected imported Canary patch to be classified as Canary.");

    var secondWrite = importService.WriteImportedPatch(env.Install.PatchesPath, canaryPatchText);
    Assert(secondWrite.Succeeded, "Expected repeated Canary import to remain idempotent: " + secondWrite.Error);
    Assert(secondWrite.BackupPath is null, "Expected repeated identical imported_patch.yml write not to create a needless backup.");

    var manualAndManagedText =
        CanaryPatchImportService.RequiredVersionHeader + "\r\n\r\n" +
        "PPU-1234567890abcdef1234567890abcdef12345678:\r\n" +
        "  \"Manual Existing Patch\":\r\n" +
        "    Games:\r\n" +
        "      \"Smoke Game\":\r\n" +
        "        BLUS31465: [ 01.02 ]\r\n" +
        "    Author: \"Manual\"\r\n" +
        "    Notes: \"Manual smoke test.\"\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\r\n";
    File.WriteAllText(importedPath, manualAndManagedText);
    var managedWrite = importService.WriteManagedImportedPatch(env.Install.PatchesPath, canaryPatchText);
    Assert(managedWrite.Succeeded, "Expected managed Canary write to merge into existing imported_patch.yml: " + managedWrite.Error);
    var managedText = File.ReadAllText(importedPath);
    Assert(managedText.Contains("Manual Existing Patch", StringComparison.Ordinal),
        "Expected existing manual imported_patch.yml entries to be preserved.");
    Assert(managedText.Contains("R3Config managed Canary patches - begin", StringComparison.Ordinal),
        "Expected managed Canary block to be written inside imported_patch.yml.");
    var managedRead = new PatchCatalogReader().Read(importedPath, isCanarySource: true);
    Assert(managedRead.Succeeded, "Expected managed imported_patch.yml to stay parseable: " + managedRead.Error);

    Console.WriteLine("Canary imported_patch.yml contract smoke PASS.");
}

static void RunPatchTests(SmokeEnv env, Rpcs3GameDescriptor rogue)
{
    var patchYmlPath = Path.Combine(env.FakeRpcs3Root, "patches", "patch.yml");
    File.WriteAllText(patchYmlPath, BuildPatchYml());
    var patchConfigPreviewPath = Path.Combine(env.ConfigPath, "patch_config.yml");
    var patchLastWrite = File.GetLastWriteTimeUtc(patchYmlPath);

    RunCanaryImportFileContractTests(env, patchYmlPath);

    var patchReader = new PatchCatalogReader();
    var patchRead = patchReader.Read(patchYmlPath);
    Assert(patchRead.Succeeded, $"Expected valid patch.yml to read successfully: {patchRead.Error}");
    Assert(patchRead.Catalog.Version == "1.2", "Expected patch catalog version.");
    Assert(patchRead.Catalog.Entries.Count == 8, "Expected multiple patch entries to be parsed.");
    Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "60 FPS" && patch.Risk == PatchRisk.Fps60),
        "Expected 60 FPS patch to be classified as Fps60.");
    Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Infinite Health Cheat" && patch.Risk == PatchRisk.Cheat),
        "Expected cheat patch to be classified as Cheat.");
    Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "SPU Timing Patch" && patch.Risk != PatchRisk.Canary),
        "Expected SPU game patch from patch.yml not to be classified as Canary by title text.");
    Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Experimental Test Patch" && patch.Risk == PatchRisk.Experimental),
        "Expected experimental patch to be classified as Experimental.");
    Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Unknown Memory Patch" && patch.Risk == PatchRisk.Unknown),
        "Expected unknown patch to remain Unknown.");
    Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").GameSerials.Contains("BLUS31465"),
        "Expected patch game serials to be parsed.");
    Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").GameVersions.Contains("01.02"),
        "Expected patch game versions to be parsed.");
    Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").HashKind == PatchHashKind.Ppu,
        "Expected PPU hash kind.");
    Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "SPU Timing Patch").HashKind == PatchHashKind.Spu,
        "Expected SPU hash kind.");

    var patchHashProbe = new PatchHashProbeService();
    var noHashProbeResult = patchHashProbe.Probe(env.Install, rogue);
    Assert(!noHashProbeResult.HashAvailable,
        "Expected missing RPCS3 log metadata to keep patch hash unknown.");
    var noHashGame = patchHashProbe.Enrich(env.Install, rogue);
    Assert(noHashGame.PpuHash is null && noHashGame.SpuHash is null,
        "Expected missing patch hash probe data not to invent hashes.");

    var gameCacheDir = Path.Combine(env.FakeRpcs3Root, "cache", rogue.Serial);
    Directory.CreateDirectory(gameCacheDir);
    Directory.CreateDirectory(Path.Combine(gameCacheDir, "ppu-0000000000000000000000000000000000000000-EBOOT.BIN"));
    var zeroCacheHashGame = patchHashProbe.Enrich(env.Install, rogue);
    Assert(zeroCacheHashGame.PpuHash is null && zeroCacheHashGame.SpuHash is null,
        "Expected all-zero placeholder PPU cache hash not to be treated as ready.");
    Directory.Delete(gameCacheDir, recursive: true);

    Directory.CreateDirectory(gameCacheDir);
    Directory.CreateDirectory(Path.Combine(gameCacheDir, "ppu-2U7JvM8h8No5MzWwXWrGcd1CPvdJ-EBOOT.BIN"));
    var cacheHashGame = patchHashProbe.Enrich(env.Install, rogue);
    Assert(cacheHashGame.PpuHash == "2u7jvm8h8no5mzwwxwrgcd1cpvdj",
        "Expected PatchHashProbeService to read alphanumeric PPU hash from RPCS3 per-game cache.");
    Directory.Delete(gameCacheDir, recursive: true);

    var metadataZeroHashResult = patchHashProbe.Probe(env.Install, rogue with
    {
        PpuHash = "0000000000000000000000000000000000000000"
    });
    Assert(!metadataZeroHashResult.HashAvailable,
        "Expected all-zero game metadata PPU hash not to be treated as ready.");

    var metadataHashResult = patchHashProbe.Probe(env.Install, rogue with
    {
        PpuHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    });
    Assert(metadataHashResult.HashAvailable && metadataHashResult.PpuHash == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "Expected existing game metadata hash to be used before log probing.");

    File.WriteAllText(Path.Combine(env.FakeRpcs3Root, "RPCS3.log"),
        $"Booting {rogue.Serial} with patch metadata PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa{Environment.NewLine}");
    var probedGame = patchHashProbe.Enrich(env.Install, rogue);
    Assert(probedGame.PpuHash == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "Expected PatchHashProbeService to read PPU hash from RPCS3.log near the serial.");

    var movedLogRoot = Path.Combine(env.TempRoot, "rpcs3-log-folder");
    Directory.CreateDirectory(Path.Combine(movedLogRoot, "config"));
    Directory.CreateDirectory(Path.Combine(movedLogRoot, "log"));
    File.WriteAllText(Path.Combine(movedLogRoot, "rpcs3.exe"), string.Empty);
    File.WriteAllText(Path.Combine(movedLogRoot, "log", "RPCS3.log"),
        $"Booting {rogue.Serial} with moved log metadata PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb{Environment.NewLine}");
    var movedLogInstall = Rpcs3InstallService.FromExePath(Path.Combine(movedLogRoot, "rpcs3.exe"));
    var movedLogGame = patchHashProbe.Enrich(movedLogInstall, rogue);
    Assert(movedLogGame.PpuHash == "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "Expected PatchHashProbeService to read PPU hash from the modern log/RPCS3.log path.");

    var matchedGame = probedGame;
    var patchMatches = new PatchMatchService().Match(patchRead.Catalog, [matchedGame]);
    Assert(patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.ExactMatch,
        "Expected matching serial/version/PPU hash to be ExactMatch.");
    Assert(patchMatches.Single(match => match.Patch.Title == "SPU Timing Patch").Status == PatchMatchStatus.SerialMismatch,
        "Expected unmatched serial to stay SerialMismatch.");

    var missingHashMatches = new PatchMatchService().Match(patchRead.Catalog, [rogue]);
    Assert(missingHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.SerialVersionMatchHashUnknown,
        "Expected missing hash to be SerialVersionMatchHashUnknown.");

    var wrongHashMatches = new PatchMatchService().Match(patchRead.Catalog, [rogue with
    {
        PpuHash = "cccccccccccccccccccccccccccccccccccccccc"
    }]);
    Assert(wrongHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.HashMismatch,
        "Expected mismatched hash to be HashMismatch.");

    var wrongVersionMatches = new PatchMatchService().Match(patchRead.Catalog, [rogue with
    {
        Version = "99.99",
        PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    }]);
    Assert(wrongVersionMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.VersionMismatch,
        "Expected mismatched version to be VersionMismatch.");

    var unknownVersionMatches = new PatchMatchService().Match(patchRead.Catalog, [rogue with
    {
        Version = null,
        PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    }]);
    Assert(unknownVersionMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.VersionUnknown,
        "Expected missing version to be VersionUnknown.");

    var serialMismatchMatches = new PatchMatchService().Match(patchRead.Catalog, [rogue with
    {
        Serial = "BLUS99999",
        PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    }]);
    Assert(serialMismatchMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.SerialMismatch,
        "Expected missing serial to be SerialMismatch.");
    var noMetadataMatch = new PatchMatchService().Match(
        new Rpcs3PatchEntry(
            Title: "No Metadata Patch",
            HashKind: PatchHashKind.Ppu,
            Hash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            GameTargets: [],
            PatchType: "Unknown",
            Risk: PatchRisk.Unknown,
            Notes: null,
            Author: null,
            PatchVersion: null,
            Group: null),
        [rogue]);
    Assert(noMetadataMatch.Status == PatchMatchStatus.NoMetadata,
        "Expected patch without game metadata to be NoMetadata.");

    var exactSafeFix = patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
    var hashUnknownSafeFix = missingHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
    var fpsPatch = patchMatches.Single(match => match.Patch.Title == "60 FPS");
    var cheatPatch = patchMatches.Single(match => match.Patch.Title == "Infinite Health Cheat");
    var experimentalPatch = patchMatches.Single(match => match.Patch.Title == "Experimental Test Patch");
    var unknownPatch = patchMatches.Single(match => match.Patch.Title == "Unknown Memory Patch");
    var canaryPatch = new PatchMatchService().Match(
        new Rpcs3PatchEntry(
            Title: "Canary Timing Patch",
            HashKind: PatchHashKind.Spu,
            Hash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            GameTargets: [new Rpcs3PatchGameTarget("Canary Game", "BLES00001", ["01.00"])],
            PatchType: "Canary",
            Risk: PatchRisk.Canary,
            Notes: "Canary-only timing experiment.",
            Author: "Example",
            PatchVersion: "0.1",
            Group: null),
        [matchedGame]);

    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(exactSafeFix.Patch.Risk, exactSafeFix.Status),
        "Expected exact SafeFix patch to require an explicit user selection.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(hashUnknownSafeFix.Patch.Risk, hashUnknownSafeFix.Status),
        "Expected HashUnknown patch not to be selected by default.");
    Assert(!PatchRiskPolicy.CanSelectInPreview(hashUnknownSafeFix.Patch.Risk, hashUnknownSafeFix.Status),
        "Expected HashUnknown patch not to be user-selectable for enable.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(fpsPatch.Patch.Risk, PatchMatchStatus.ExactMatch),
        "Expected 60 FPS patch not to be selected by default.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(cheatPatch.Patch.Risk, PatchMatchStatus.ExactMatch),
        "Expected Cheat patch not to be selected by default.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(experimentalPatch.Patch.Risk, PatchMatchStatus.ExactMatch),
        "Expected Experimental patch not to be selected by default.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(unknownPatch.Patch.Risk, PatchMatchStatus.ExactMatch),
        "Expected Unknown patch not to be selected by default.");
    Assert(PatchRiskPolicy.IsVisibleInPreview(canaryPatch.Patch.Risk, expertMode: false),
        "Expected Canary patch to be visible for the selected game without an expert-only gate.");
    Assert(!PatchRiskPolicy.CanSelectInPreview(canaryPatch.Patch.Risk, canaryPatch.Status),
        "Expected Canary patches for a different serial not to be user-selectable.");
    Assert(!PatchRiskPolicy.ShouldSelectByDefaultInPreview(canaryPatch.Patch.Risk, canaryPatch.Status),
        "Expected Canary patches not to be auto-selected.");

    var selectedGamePreviewMatches = patchRead.Catalog.Entries
        .Select(entry => new PatchMatchService().Match(entry, [matchedGame]))
        .Where(match => match.Status is not PatchMatchStatus.SerialMismatch and not PatchMatchStatus.NoMetadata)
        .ToArray();
    Assert(selectedGamePreviewMatches.Length == 7,
        "Expected selected-game preview to include only patches for the selected game's serial.");
    Assert(selectedGamePreviewMatches.All(match => match.MatchedSerial == "BLUS31465"),
        "Expected selected-game preview to exclude patches for other serials.");

    Assert(File.GetLastWriteTimeUtc(patchYmlPath) == patchLastWrite,
        "Expected patch preview read not to modify patch.yml.");
    Assert(!File.Exists(patchConfigPreviewPath), "Expected patch preview read not to create patch_config.yml.");

    var missingPatchConfigApplyPlan = new PatchApplyPlanner().CreatePlan(
        env.Install,
        [exactSafeFix],
        expertMode: false);
    var backupService = new BackupService();
    var missingPatchConfigApplyResult = new PatchApplyService().Apply(
        missingPatchConfigApplyPlan,
        Path.Combine(env.Install.ConfigPath, "R3ConfigBackups"));
    Assert(!missingPatchConfigApplyResult.Backup.Files.Single().Existed,
        "Expected missing patch_config.yml backup entry to record Existed=false.");
    Assert(new PatchConfigReader().IsEnabled(patchConfigPreviewPath, missingPatchConfigApplyPlan.Changes.Single()),
        "Expected missing patch_config.yml to be created only during checked patch apply.");
    backupService.Restore(missingPatchConfigApplyResult.Backup);
    Assert(!File.Exists(patchConfigPreviewPath),
        "Expected restore to remove patch_config.yml that did not exist before apply.");

    File.WriteAllText(patchConfigPreviewPath, BuildExistingPatchConfigYml());
    var canaryExact = new PatchMatchService().Match(canaryPatch.Patch, [new Rpcs3GameDescriptor(
        Serial: "BLES00001",
        Title: "Canary Game",
        InstallPath: env.TempRoot,
        Version: "01.00",
        PpuHash: null,
        SpuHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]);
    Assert(canaryExact.Status == PatchMatchStatus.ExactMatch,
        "Expected canary test patch to be exact for apply filtering test.");
    Assert(PatchRiskPolicy.CanSelectInPreview(canaryExact.Patch.Risk, canaryExact.Status),
        "Expected exact Canary patches to be user-selectable.");

    var patchApplyPlanner = new PatchApplyPlanner();
    var hashUnknownApplyPlan = patchApplyPlanner.CreatePlan(env.Install, [hashUnknownSafeFix], expertMode: false);
    Assert(!hashUnknownApplyPlan.HasChanges,
        "Expected HashUnknown patch not to be planned for enable.");

    var hashUnknownDisablePlan = patchApplyPlanner.CreateDisablePlan(env.Install, [hashUnknownSafeFix]);
    Assert(hashUnknownDisablePlan.HasChanges,
        "Expected HashUnknown patch to be planned for disable when serial and version match.");
    Assert(hashUnknownDisablePlan.Changes.Any(change => change.PatchTitle == "Skip Intro Videos"),
        "Expected HashUnknown patch to create a disable patch_config change.");

    var patchApplyPlan = patchApplyPlanner.CreatePlan(
        env.Install,
        [
            exactSafeFix,
            fpsPatch,
            cheatPatch,
            canaryExact,
            hashUnknownSafeFix,
            experimentalPatch,
            unknownPatch
        ],
        expertMode: false);
    Assert(patchApplyPlan.Changes.Count == 6,
        "Expected all explicitly checked exact selected-game patches to be included in the patch apply plan, with hash-unknown entries excluded.");
    Assert(patchApplyPlan.Changes.Any(change => change.PatchTitle == "Skip Intro Videos"),
        "Expected Skip Intro Videos to be planned for apply.");
    Assert(patchApplyPlan.Changes.Any(change => change.PatchTitle == "Canary Timing Patch"),
        "Expected exact Canary patches to be planned when explicitly selected by the user.");

    var patchApplyService = new PatchApplyService();
    var patchBackupRoot = Path.Combine(env.Install.ConfigPath, "R3ConfigBackups");
    var patchApplyResult = patchApplyService.Apply(patchApplyPlan, patchBackupRoot);
    Assert(patchApplyResult.Backup.Files.Single().TargetPath == patchConfigPreviewPath,
        "Expected patch_config.yml to be backed up before write.");
    Assert(patchApplyResult.Backup.Files.Single().Existed,
        "Expected backup to record existing patch_config.yml.");

    var patchConfigReader = new PatchConfigReader();
    Assert(patchConfigReader.IsEnabled(patchConfigPreviewPath, patchApplyPlan.Changes.Single(change => change.PatchTitle == "Skip Intro Videos")),
        "Expected selected SafeFix patch to be enabled in patch_config.yml.");
    var patchConfigText = File.ReadAllText(patchConfigPreviewPath);
    Assert(File.ReadAllText(patchConfigPreviewPath, Encoding.UTF8).Contains("\r\n", StringComparison.Ordinal),
        "Expected generated patch_config.yml to use Windows CRLF line endings.");
    Assert(patchConfigText.Contains("Existing Patch", StringComparison.Ordinal),
        "Expected unrelated patch_config entries to be preserved.");
    Assert(patchConfigText.Contains("60 FPS", StringComparison.Ordinal),
        "Expected explicitly selected 60FPS patch to be written.");
    Assert(patchConfigText.Contains("Infinite Health Cheat", StringComparison.Ordinal),
        "Expected explicitly selected Cheat patch to be written.");
    Assert(patchConfigText.Contains("Canary Timing Patch", StringComparison.Ordinal),
        "Expected explicitly selected Canary patch to be written.");
    Assert(patchConfigText.Contains("Experimental Test Patch", StringComparison.Ordinal),
        "Expected explicitly selected Experimental patch to be written.");
    Assert(patchConfigText.Contains("Unknown Memory Patch", StringComparison.Ordinal),
        "Expected explicitly selected Unknown patch to be written.");

    backupService.Restore(patchApplyResult.Backup);
    var restoredPatchConfigText = File.ReadAllText(patchConfigPreviewPath);
    Assert(restoredPatchConfigText.Contains("Existing Patch", StringComparison.Ordinal),
        "Expected restore to keep original patch_config entry.");
    Assert(!restoredPatchConfigText.Contains("Skip Intro Videos", StringComparison.Ordinal),
        "Expected restore to remove applied patch entry.");

    var failingPatchApplyService = new PatchApplyService(writeChanges: (path, changes) =>
    {
        File.WriteAllText(path, "partial patch_config write");
        throw new UnauthorizedAccessException("Simulated permission denied.");
    });
    try
    {
        failingPatchApplyService.Apply(patchApplyPlan, patchBackupRoot);
        throw new InvalidOperationException("Expected patch apply failure to be surfaced.");
    }
    catch (UnauthorizedAccessException)
    {
        Assert(File.ReadAllText(patchConfigPreviewPath) == restoredPatchConfigText,
            "Expected failed patch write to restore the previous patch_config.yml.");
    }

    File.WriteAllText(patchConfigPreviewPath, "PPU-invalid: [broken");
    try
    {
        patchApplyService.Apply(patchApplyPlan, patchBackupRoot);
        throw new InvalidOperationException("Expected invalid patch_config.yml to fail safely.");
    }
    catch (InvalidDataException)
    {
        Assert(File.ReadAllText(patchConfigPreviewPath) == "PPU-invalid: [broken",
            "Expected invalid patch_config.yml to remain unchanged after failed apply.");
    }

    var invalidPatchYmlPath = Path.Combine(env.FakeRpcs3Root, "patches", "invalid_patch.yml");
    File.WriteAllText(invalidPatchYmlPath, "Version: [invalid");
    var invalidPatchRead = patchReader.Read(invalidPatchYmlPath);
    Assert(!invalidPatchRead.Succeeded, "Expected invalid patch YAML to be rejected safely.");
    Assert(invalidPatchRead.Catalog.Entries.Count == 0, "Expected invalid patch YAML to return an empty catalog.");
    File.Delete(invalidPatchYmlPath);

    var missingPatchRead = patchReader.Read(Path.Combine(env.TempRoot, "missing-patches", "patch.yml"));
    Assert(!missingPatchRead.Succeeded, "Expected missing patches folder/patch.yml to fail safely.");
    Assert(missingPatchRead.Catalog.Entries.Count == 0,
        "Expected missing patch.yml to return an empty patch catalog.");

    RunRpcs3PatchContractTests(env, rogue);
    RunAppFlowPatchCatalogLoadSmokeTests(env, patchYmlPath);
}

static void RunRpcs3PatchContractTests(SmokeEnv env, Rpcs3GameDescriptor rogue)
{
    const string primaryHash = "1111111111111111111111111111111111111111";
    const string secondaryHash = "2222222222222222222222222222222222222222";
    var matchService = new PatchMatchService();

    var multiTargetPatch = new Rpcs3PatchEntry(
        Title: "Best Target Patch",
        HashKind: PatchHashKind.Ppu,
        Hash: secondaryHash,
        GameTargets:
        [
            new Rpcs3PatchGameTarget("Contract Game", rogue.Serial, ["01.00"]),
            new Rpcs3PatchGameTarget("Contract Game", rogue.Serial, ["01.02"])
        ],
        PatchType: "Fix",
        Risk: PatchRisk.SafeFix,
        Notes: "Contract test.",
        Author: "R3Config",
        PatchVersion: "1.0",
        Group: "Fixes",
        Source: PatchSourceOrigin.Official,
        SourcePath: "patch.yml");

    var normalizedVersionMatch = matchService.Match(multiTargetPatch, [rogue with
    {
        Version = "v1.2 ",
        PpuHash = primaryHash,
        PpuHashes = [primaryHash, secondaryHash]
    }]);
    Assert(normalizedVersionMatch.Status == PatchMatchStatus.ExactMatch,
        "Expected matcher to evaluate every same-serial target and choose the exact version/hash match.");
    Assert(normalizedVersionMatch.MatchedTargetVersion == "01.02",
        "Expected RPCS3 version normalization to map v1.2 to target version 01.02.");

    var allTargetPatch = multiTargetPatch with
    {
        Title = "All Target Patch",
        GameTargets = [new Rpcs3PatchGameTarget("All", "All", ["All"])]
    };
    var allTargetMatch = matchService.Match(allTargetPatch, [rogue with
    {
        Version = null,
        PpuHash = secondaryHash
    }]);
    Assert(allTargetMatch.Status == PatchMatchStatus.ExactMatch,
        "Expected RPCS3 All/All/All targets to match an installed game even when its version is unknown.");
    var allTargetPlan = new PatchApplyPlanner().CreatePlan(env.Install, [allTargetMatch], expertMode: false);
    Assert(allTargetPlan.Changes.Single() is { GameTitle: "All", Serial: "All", Version: "All" },
        "Expected patch_config.yml keys to preserve RPCS3's literal All target path.");

    const string extendedHashCatalog = """
Version: 1.2
PRX-3333333333333333333333333333333333333333:
  "PRX Contract Patch":
    Games:
      All:
        All: [ All ]
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
OVL-4444444444444444444444444444444444444444:
  "OVL Contract Patch":
    Games:
      All:
        All: [ All ]
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000020, 0x60000000 ]
""";
    var reader = new PatchCatalogReader();

    var missingVersionRead = reader.ReadText(
        """
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Missing Version":
    Games:
      Contract Game:
        BLUS31465: [ 01.02 ]
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(!missingVersionRead.Succeeded &&
           missingVersionRead.Error?.Contains("Version", StringComparison.Ordinal) == true,
        "Expected RPCS3 patch files without Version: 1.2 to be rejected.");

    var unsupportedVersionRead = reader.ReadText(
        """
Version: 1.1
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Unsupported Version":
    Games:
      Contract Game:
        BLUS31465: [ 01.02 ]
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(!unsupportedVersionRead.Succeeded &&
           unsupportedVersionRead.Error?.Contains("1.2", StringComparison.Ordinal) == true,
        "Expected patch engine versions other than 1.2 to be rejected.");

    var scalarVersionRead = reader.ReadText(
        """
Version: 1.2
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Scalar Version":
    Games:
      Contract Game:
        BLUS31465: 01.02
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(scalarVersionRead.Succeeded &&
           scalarVersionRead.Catalog.Entries.Count == 0 &&
           scalarVersionRead.Error?.Contains("sequence", StringComparison.OrdinalIgnoreCase) == true,
        "Expected scalar app versions to be excluded and surfaced as RPCS3 contract warnings.");

    var invalidAllRead = reader.ReadText(
        """
Version: 1.2
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Invalid All Pair":
    Games:
      Contract Game:
        All: [ All ]
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(invalidAllRead.Succeeded &&
           invalidAllRead.Catalog.Entries.Count == 0 &&
           invalidAllRead.Error?.Contains("serial All", StringComparison.Ordinal) == true,
        "Expected serial All to be rejected outside the All title.");

    var alphanumericSerialRead = reader.ReadText(
        """
Version: 1.2
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Alphanumeric Serial":
    Games:
      Contract Game:
        AB12C34D5: [ 01.02 ]
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(alphanumericSerialRead.Succeeded &&
           alphanumericSerialRead.Catalog.Entries.Single().GameTargets.Single().Serial == "AB12C34D5" &&
           string.IsNullOrWhiteSpace(alphanumericSerialRead.Error),
        "Expected RPCS3's nine-character alphanumeric serial contract to be accepted.");

    var invalidInstructionRead = reader.ReadText(
        """
Version: 1.2
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Invalid Instruction":
    Games:
      Contract Game:
        BLUS31465: [ 01.02 ]
    Patch:
      - [ not_a_patch_type, 0x00000010, 0x60000000 ]
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(invalidInstructionRead.Succeeded &&
           invalidInstructionRead.Catalog.Entries.Count == 0 &&
           invalidInstructionRead.Error?.Contains("not_a_patch_type", StringComparison.Ordinal) == true,
        "Expected patches containing unsupported RPCS3 instruction types to be excluded with a warning.");

    var invalidPatchShapeRead = reader.ReadText(
        """
Version: 1.2
PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
  "Invalid Patch Shape":
    Games:
      Contract Game:
        BLUS31465: [ 01.02 ]
    Patch: be32
""",
        PatchSourceOrigin.Official,
        "patch.yml");
    Assert(invalidPatchShapeRead.Succeeded &&
           invalidPatchShapeRead.Catalog.Entries.Count == 0 &&
           invalidPatchShapeRead.Error?.Contains("Patch must be a YAML sequence", StringComparison.Ordinal) == true,
        "Expected non-sequence Patch nodes to be excluded with a warning.");

    var extendedRead = reader.ReadText(extendedHashCatalog, PatchSourceOrigin.Official, "patch.yml");
    Assert(extendedRead.Succeeded, "Expected PRX/OVL RPCS3 patch keys to parse.");
    Assert(extendedRead.Catalog.Entries.Any(entry => entry.HashKind == PatchHashKind.Prx) &&
           extendedRead.Catalog.Entries.Any(entry => entry.HashKind == PatchHashKind.Ovl),
        "Expected PRX and OVL entries not to be silently dropped.");
    Assert(extendedRead.Catalog.Entries.All(entry => entry.Source == PatchSourceOrigin.Official),
        "Expected explicit patch provenance to survive parsing.");
    Assert(extendedRead.Catalog.Entries.All(entry => entry.GameTargets.Single().Serial == "All"),
        "Expected global RPCS3 patch targets to survive parsing.");

    var imported = multiTargetPatch with { PatchVersion = "1.1", Source = PatchSourceOrigin.Imported };
    var bundled = multiTargetPatch with { PatchVersion = "2.0", Source = PatchSourceOrigin.BundledCanary };
    var merged = new PatchCatalogMergeService().Merge(
        new Rpcs3PatchCatalog("1.2", [multiTargetPatch]),
        new Rpcs3PatchCatalog("1.2", [imported]),
        new Rpcs3PatchCatalog("1.2", [bundled]));
    Assert(merged.Entries.Single().Source == PatchSourceOrigin.Imported,
        "Expected the RPCS3-resident imported patch to remain authoritative over a bundled preview duplicate.");
    Assert(merged.MergeDiagnostics.Count == 2,
        "Expected every dropped or superseded duplicate patch to be reported.");

    var duplicateHashPatch = multiTargetPatch with { Hash = primaryHash };
    var previewMatches = new[]
    {
        matchService.Match(multiTargetPatch, [rogue with { Version = "01.02", PpuHashes = [primaryHash, secondaryHash] }]),
        matchService.Match(duplicateHashPatch, [rogue with { Version = "01.02", PpuHashes = [primaryHash, secondaryHash] }])
    };
    var preview = new PatchPreviewVm();
    preview.Load(
        previewMatches,
        "RPCS3 patch catalog",
        warningMessage: "Contract warning",
        isEnabledResolver: match => match.Patch.Hash == primaryHash);
    Assert(preview.HasWarning && preview.WarningText == "Contract warning",
        "Expected real patch catalog warnings to be visible in the preview surface.");
    Assert(preview.OfficialGroups.Count == 1 && preview.OfficialGroups.Single().Items.Count == 2,
        "Expected RPCS3 hierarchy to group different hashes under the same game/serial/version node.");
    Assert(preview.OfficialItems.All(item => !item.IsSelected),
        "Expected no patch to be preselected.");
    Assert(preview.OfficialItems.Count(item => item.CanEnable) == 1 &&
           preview.OfficialItems.Count(item => item.CanDisable) == 1,
        "Expected enabled-state resolution to separate enable and disable actions.");
    Assert(preview.OfficialItems.Select(item => item.DisplayTitle).Distinct().Count() == 2,
        "Expected duplicate patch titles for different hashes to receive distinct RPCS3-style display ordinals.");
    var enableItem = preview.OfficialItems.Single(item => item.CanEnable);
    enableItem.IsSelected = true;
    Assert(preview.HasSelectedOfficialApplyablePatches && preview.SelectedOfficialMatches().Count == 1,
        "Expected official-tab selection state to be isolated and executable.");

    var configPath = Path.Combine(env.TempRoot, "contract-patch-config.yml");
    var change = allTargetPlan.Changes.Single();
    var writer = new PatchConfigWriter();
    var configReader = new PatchConfigReader();
    writer.Write(configPath, [change]);
    Assert(configReader.IsEnabled(configPath, change),
        "Expected RPCS3-compatible Enabled mapping to be readable after write.");
    writer.Write(configPath, [change], enabled: false);
    Assert(!configReader.IsEnabled(configPath, change),
        "Expected disabling a patch to remove its enabled state.");
    Assert(!File.ReadAllText(configPath).Contains("Enabled: false", StringComparison.Ordinal),
        "Expected disable to mirror RPCS3 and avoid stale Enabled: false leaves.");

    File.WriteAllText(configPath, $"""
{change.HashKey}:
  {change.PatchTitle}:
    All:
      All:
        All:
          Enabled: true
          Configurable Values:
            Scale: 2
""");
    writer.Write(configPath, [change], enabled: false);
    var configurableText = File.ReadAllText(configPath);
    Assert(!configurableText.Contains("Enabled:", StringComparison.Ordinal) &&
           configurableText.Contains("Configurable Values", StringComparison.Ordinal),
        "Expected disable to preserve RPCS3 configurable patch values while removing Enabled.");

    File.WriteAllText(configPath, $"""
{change.HashKey}:
  {change.PatchTitle}:
    All:
      All:
        All: true
""");
    Assert(configReader.IsEnabled(configPath, change),
        "Expected legacy RPCS3 scalar patch_config.yml entries to remain readable.");
    writer.Write(configPath, [change], enabled: false);
    Assert(!File.ReadAllText(configPath).Contains(change.PatchTitle, StringComparison.Ordinal),
        "Expected legacy scalar disable to prune the empty patch path.");

    var hashProbeRoot = Path.Combine(env.TempRoot, "multi-hash-log");
    Directory.CreateDirectory(Path.Combine(hashProbeRoot, "config"));
    Directory.CreateDirectory(Path.Combine(hashProbeRoot, "log"));
    File.WriteAllText(Path.Combine(hashProbeRoot, "rpcs3.exe"), string.Empty);
    File.WriteAllText(
        Path.Combine(hashProbeRoot, "log", "RPCS3.log"),
        $"Booting {rogue.Serial}{new string('x', 20000)} PPU-{primaryHash}\nPPU-{secondaryHash}\nSPU-{primaryHash}\n");
    var probeInstall = Rpcs3InstallService.FromExePath(Path.Combine(hashProbeRoot, "rpcs3.exe"));
    var multiHashGame = new PatchHashProbeService().Enrich(probeInstall, rogue);
    Assert(multiHashGame.AllPpuHashes.Count == 2 && multiHashGame.AllSpuHashes.Count == 1,
        "Expected bounded RPCS3 log probing to retain every relevant PPU/SPU hash in the game session.");
    Assert(matchService.Match(multiTargetPatch, [multiHashGame]).Status == PatchMatchStatus.ExactMatch,
        "Expected patch matching to use the complete discovered hash set, not only the first hash.");

    Console.WriteLine("RPCS3 patch engine contract tests passed.");
}

static void RunAppFlowPatchCatalogLoadSmokeTests(SmokeEnv env, string patchYmlPath)
{
    var flow = new AppFlow();
    var importedPatchPath = Path.Combine(env.FakeRpcs3Root, "patches", "imported_patch.yml");

    File.WriteAllText(patchYmlPath, BuildPatchYml());
    if (File.Exists(importedPatchPath))
    {
        File.Delete(importedPatchPath);
    }

    var officialOnly = flow.LoadPatchCat(env.Install);
    Assert(officialOnly.Cat is not null, "Expected patch.yml-only AppFlow load to return a catalog.");
    Assert(string.IsNullOrWhiteSpace(officialOnly.Error), "Expected patch.yml-only AppFlow load not to report an error: " + officialOnly.Error);
    Assert(officialOnly.Cat!.Entries.Any(patch => patch.Title == "Skip Intro Videos"),
        "Expected patch.yml-only AppFlow load to include official/local patches.");

    File.Delete(patchYmlPath);
    File.WriteAllText(importedPatchPath, BuildImportedPatchYml());
    var importedOnly = flow.LoadPatchCat(env.Install);
    Assert(importedOnly.Cat is not null, "Expected imported_patch.yml-only AppFlow load to return a catalog.");
    Assert(string.IsNullOrWhiteSpace(importedOnly.Error), "Expected imported_patch.yml-only AppFlow load not to report patch.yml missing.");
    Assert(importedOnly.Cat!.Entries.Any(patch => patch.Title == "Imported Canary Display Patch" && patch.Risk == PatchRisk.Canary),
        "Expected imported_patch.yml entries to load as Canary patches.");

    var importService = new CanaryPatchImportService();
    var importResult = importService.WriteImportedPatch(env.Install.PatchesPath, BuildImportedPatchYml());
    Assert(importResult.Succeeded, "Expected Canary import service to write imported_patch.yml successfully: " + importResult.Error);
    Assert(File.ReadAllText(importedPatchPath, Encoding.UTF8).Contains("\r\n", StringComparison.Ordinal),
        "Expected imported_patch.yml writes to use Windows CRLF line endings.");

    File.WriteAllText(patchYmlPath, BuildPatchYml());
    var merged = flow.LoadPatchCat(env.Install);
    Assert(merged.Cat is not null, "Expected merged AppFlow load to return a catalog.");
    Assert(merged.Cat!.Entries.Any(patch => patch.Title == "Skip Intro Videos"),
        "Expected merged AppFlow load to include patch.yml entries.");
    Assert(merged.Cat.Entries.Any(patch => patch.Title == "Imported Canary Display Patch" && patch.Risk == PatchRisk.Canary),
        "Expected merged AppFlow load to include imported_patch.yml entries as Canary.");

    File.WriteAllText(patchYmlPath, "Version: [invalid");
    var invalidOfficialWithImported = flow.LoadPatchCat(env.Install);
    Assert(invalidOfficialWithImported.Cat is not null,
        "Expected valid imported_patch.yml to remain visible when patch.yml is invalid.");
    Assert(invalidOfficialWithImported.Cat!.Entries.Any(patch => patch.Title == "Imported Canary Display Patch"),
        "Expected imported patches to be preserved when patch.yml fails to parse.");
    Assert(invalidOfficialWithImported.Error?.Contains("patch.yml", StringComparison.OrdinalIgnoreCase) == true,
        "Expected invalid patch.yml to produce a readable warning while imported patches load.");

    File.Delete(patchYmlPath);
    File.Delete(importedPatchPath);
    var none = flow.LoadPatchCat(env.Install);
    Assert(none.Cat is not null && none.Cat.Entries.Any(entry => entry.Source == PatchSourceOrigin.BundledCanary),
        "Expected bundled Canary preview data to remain available when optional RPCS3 patch files are missing.");
    Assert(string.IsNullOrWhiteSpace(none.Error),
        "Expected missing optional RPCS3 patch files not to be reported as corrupt data.");

    File.WriteAllText(importedPatchPath, BuildImportedPatchDuplicateKeyYml());
    var duplicateImported = flow.LoadPatchCat(env.Install);
    Assert(duplicateImported.Cat is not null && duplicateImported.Cat.Entries.Any(entry => entry.Source == PatchSourceOrigin.BundledCanary),
        "Expected valid bundled Canary data to stay visible when imported_patch.yml is invalid.");
    Assert(duplicateImported.Error?.Contains("imported_patch.yml", StringComparison.OrdinalIgnoreCase) == true,
        "Expected duplicate imported_patch.yml error to name imported_patch.yml.");
    Assert(duplicateImported.Error?.Contains("Canary", StringComparison.OrdinalIgnoreCase) == true,
        "Expected duplicate imported_patch.yml error to suggest the Canary import tool.");

    Console.WriteLine("AppFlow patch catalog load smoke tests passed.");
}

static void RunReadOnlyRpcs3InstallContract(string rootOrExePath)
{
    var exePath = Directory.Exists(rootOrExePath)
        ? Path.Combine(rootOrExePath, "rpcs3.exe")
        : rootOrExePath;
    Assert(File.Exists(exePath), "RPCS3 integration path must contain rpcs3.exe: " + exePath);

    var install = Rpcs3InstallService.FromExePath(exePath);
    var officialPath = Path.Combine(install.PatchesPath, "patch.yml");
    var importedPath = Path.Combine(install.PatchesPath, "imported_patch.yml");
    var trackedPaths = new[] { officialPath, importedPath, install.PatchConfigPath }
        .Where(File.Exists)
        .ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.OrdinalIgnoreCase);

    var games = new GameScanner().Scan(install);
    Assert(games.Count > 0, "Expected the supplied RPCS3 installation to expose installed games.");

    var officialRead = new PatchCatalogReader().Read(officialPath, PatchSourceOrigin.Official);
    Assert(officialRead.Succeeded && officialRead.Catalog.Entries.Count > 0,
        "Expected the supplied RPCS3 patch.yml to parse: " + officialRead.Error);
    Assert(officialRead.Catalog.Entries.Any(entry => entry.HashKind == PatchHashKind.Prx) &&
           officialRead.Catalog.Entries.Any(entry => entry.HashKind == PatchHashKind.Ovl),
        "Expected the real RPCS3 catalog to retain PRX and OVL patches.");

    var patchConfig = new PatchConfigReader().Load(install.PatchConfigPath);
    Assert(patchConfig.Documents.Count > 0, "Expected the supplied RPCS3 patch_config.yml to be readable.");

    var flow = new AppFlow();
    var flowResult = flow.LoadPatchCat(install);
    Assert(flowResult.Cat is not null && flowResult.Cat.Entries.Count > 0,
        "Expected AppFlow to load the supplied RPCS3 patch sources: " + flowResult.Error);

    var logPath = Path.Combine(install.RootPath, "log", "RPCS3.log");
    if (File.Exists(logPath))
    {
        var logText = File.ReadAllText(logPath);
        var loggedGame = games.LastOrDefault(game => logText.Contains(game.Serial, StringComparison.OrdinalIgnoreCase));
        if (loggedGame is not null)
        {
            var enriched = new PatchHashProbeService().Enrich(install, loggedGame);
            Assert(enriched.AllPpuHashes.Count > 0 || enriched.AllSpuHashes.Count > 0,
                "Expected the real RPCS3 log to expose at least one bounded hash for " + loggedGame.Serial);
            var preview = new AppFlow().BuildPatchPreview(flowResult.Cat!, enriched);
            Assert(preview.All(match => match.MatchedSerial == enriched.Serial),
                "Expected real-install preview results to stay scoped to the selected game.");
        }
    }

    var builtInCatalog = flow.LoadBuiltInCat(AppContext.BaseDirectory);
    Assert(builtInCatalog.Cat is not null,
        "Expected the bundled recommendation catalog to load during real-install analysis.");
    var analysisTimer = System.Diagnostics.Stopwatch.StartNew();
    var analysis = flow.AnalyzeAsync(exePath, builtInCatalog.Cat!).GetAwaiter().GetResult();
    analysisTimer.Stop();
    Assert(analysis.Games.Count == games.Count,
        "Expected full R3Config analysis to retain every discovered RPCS3 game.");
    var exactSamples = analysis.Games
        .SelectMany(row => flow.BuildPatchPreview(analysis.PatchCat.Cat!, row.Game)
            .Where(match => match.Status == PatchMatchStatus.ExactMatch)
            .Select(match => $"{row.Game.Serial} {row.Game.Version}: {match.Patch.Title} [{match.Patch.Risk}]"))
        .Take(8)
        .ToArray();

    foreach (var (path, timestamp) in trackedPaths)
    {
        Assert(File.GetLastWriteTimeUtc(path) == timestamp,
            "Read-only integration verification modified RPCS3 data: " + path);
    }

    Console.WriteLine($"Real RPCS3 read-only contract passed: {games.Count} games, {officialRead.Catalog.Entries.Count} official patches, {flowResult.Cat!.Entries.Count} merged patches, full analysis {analysisTimer.ElapsedMilliseconds} ms.");
    if (exactSamples.Length > 0)
    {
        Console.WriteLine("Real exact patch samples: " + string.Join(" | ", exactSamples));
    }
}

static WikiGameRecommendation BuildRecStateRecommendation()
{
    return new WikiGameRecommendation(
        Serial: "BLUS31465",
        WikiTitle: "Assassin's Creed Rogue",
        Changes:
        [
            new ConfigChange(
                Section: "Video",
                Key: "Write Color Buffers",
                Value: "true",
                WikiKey: "write color buffers",
                Notes: "Fixes ocean colors",
                Risk: RecommendationRisk.Recommended),
            new ConfigChange(
                Section: "Video",
                Key: "Read Color Buffers",
                Value: "true",
                WikiKey: "read color buffers",
                Notes: "Fixes readback issue",
                Risk: RecommendationRisk.Recommended),
            new ConfigChange(
                Section: "Video",
                Key: "Resolution Scale",
                Value: "100",
                WikiKey: "resolution scale",
                Notes: "Safe baseline",
                Risk: RecommendationRisk.Recommended)
        ]);
}

static WikiGameRecommendation BuildManualTimingRecommendation()
{
    return new WikiGameRecommendation(
        Serial: "BLUS31465",
        WikiTitle: "Assassin's Creed Rogue",
        Changes:
        [
            new ConfigChange(
                Section: "Video",
                Key: "Vblank Rate",
                Value: "120",
                WikiKey: "vblank rate",
                Notes: "60 FPS timing change",
                Risk: RecommendationRisk.ManualTiming)
        ]);
}

static WikiGameRecommendation BuildLowEndBlockedRecommendation()
{
    return new WikiGameRecommendation(
        Serial: "BLUS31465",
        WikiTitle: "Assassin's Creed Rogue",
        Changes:
        [
            new ConfigChange(
                Section: "Video",
                Key: "Resolution Scale",
                Value: "150",
                WikiKey: "resolution scale",
                Notes: "Hardware-dependent visual setting",
                Risk: RecommendationRisk.HardwareDependent)
        ]);
}

static string BuildWikiXml()
{
    return """
<?xml version="1.0" encoding="utf-8"?>
<mediawiki>
  <page>
    <title>Assassin's Creed Rogue</title>
    <revision>
      <text xml:space="preserve">{{vginfobox
|gameid = {{gameid|id=BLUS31465, BLES02061|oid=BLJM61208}}
|demoids = {{gameid|id=NPUB31547}}
}}
==Configuration==
{{config
|ppu decoder = Interpreter (static) <!-- Interpreter (static), Interpreter (dynamic), Recompiler (LLVM) -->
|ppu decoder notes = Fixes freezes
|write color buffers = On <!-- On, Off -->
|write color buffers notes = Fixes ocean colors
|read color buffers = On <!-- On, Off -->
|read color buffers notes = May cause flickering on some systems
|write depth buffers = On <!-- On, Off -->
|write depth buffers notes = Required unless using FPS Patch
|strict rendering mode = On <!-- On, Off -->
|strict rendering mode notes = Reduces performance but fixes geometry
|resolution scale = 150% <!-- Value between 50% to 800% -->
|resolution scale notes = Optional quality setting
|vblank rate = 120 <!-- Value between 1 to 500 -->
|vblank rate notes = 60 FPS timing change
}}</text>
    </revision>
  </page>
</mediawiki>
""";
}

static string BuildWrongRiskCatalogJson()
{
    return """
{
  "DataSource": "Built-in",
  "DataVersion": "2099-01-03",
  "RecommendationCount": 999,
  "Recommendations": [
    {
      "Serial": "TEST00001",
      "WikiTitle": "Risk Contract Test",
      "Changes": [
        {
          "Section": "Core",
          "Key": "PPU Decoder",
          "Value": "Interpreter (static)",
          "WikiKey": "ppu decoder",
          "Notes": "Stored risk is deliberately wrong.",
          "Risk": "Recommended"
        }
      ]
    }
  ]
}
""";
}

static string BuildDuplicateTopLevelHashPatchYml()
{
    return """
Version: 1.2

Anchors:
  first_games: &first_games
    "First Game":
      BLUS00001: [ 01.00 ]

PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
  "First Patch":
    Games: *first_games
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]

Anchors:
  second_games: &second_games
    "Second Game":
      BLUS00002: [ 01.01 ]

PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
  "Second Patch":
    Games: *second_games
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000020, 0x60000000 ]
""";
}

static string BuildPatchYml()
{
    return """
Version: 1.2

Anchors:
  sample_games: &sample_games
    "Assassin's Creed Rogue":
      BLUS31465: [ 01.02 ]

PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:
  "Skip Intro Videos":
    Games: *sample_games
    Author: "Example"
    Notes: "Fixes intro hang."
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000010, 0x60000000 ]
  "60 FPS":
    Games: *sample_games
    Notes: "Forces 60 FPS and may require Vblank."
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000020, 0x60000000 ]
  "Infinite Health Cheat":
    Games: *sample_games
    Notes: "Cheat for testing."
    Patch Version: 1.0
    Patch:
      - [ be32, 0x00000030, 0x60000000 ]
  "Disable Motion Blur":
    Games: *sample_games
    Notes: "Disables motion blur."
    Patch Version: 1.0
    Patch:
      - [ byte, 0x00000040, 0x00 ]
  "Performance Shadows":
    Games: *sample_games
    Notes: "Improves performance by disabling expensive shadows."
    Patch Version: 1.0
    Patch:
      - [ byte, 0x00000050, 0x00 ]
  "Experimental Test Patch":
    Games: *sample_games
    Notes: "Experimental unstable test patch."
    Patch Version: 0.1
    Patch:
      - [ byte, 0x00000058, 0x01 ]
  "Unknown Memory Patch":
    Games: *sample_games
    Notes:
    Patch Version: 1.0
    Patch:
      - [ byte, 0x00000060, 0x01 ]

Anchors:
  canary_games: &canary_games
    "Canary Game":
      BLES00001: [ 01.00 ]

SPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:
  "SPU Timing Patch":
    Games: *canary_games
    Author: "Example"
    Notes: "SPU timing test patch."
    Patch Version: 0.1
    Patch:
      - [ be32, 0x00000070, 0x60000000 ]
""";
}

static string BuildImportedPatchYml()
{
    return """
Version: 1.2

PPU-dddddddddddddddddddddddddddddddddddddddd:
  "Imported Canary Display Patch":
    Games:
      "Assassin's Creed Rogue":
        BLUS31465: [ 01.02 ]
    Author: "Canary Import"
    Notes: "Imported Canary test patch."
    Patch Version: 0.1
    Patch:
      - [ be32, 0x00000080, 0x60000000 ]
""";
}

static string BuildImportedPatchDuplicateKeyYml()
{
    return """
Version: 1.2

PPU-dddddddddddddddddddddddddddddddddddddddd:
  "Imported Canary Display Patch":
    Games:
      "Assassin's Creed Rogue":
        BLUS31465: [ 01.02 ]
    Patch:
      - [ be32, 0x00000080, 0x60000000 ]
  "Imported Canary Display Patch":
    Games:
      "Assassin's Creed Rogue":
        BLUS31465: [ 01.02 ]
    Patch:
      - [ be32, 0x00000084, 0x60000000 ]
""";
}

static string BuildExistingPatchConfigYml()
{
    return """
PPU-existingexistingexistingexistingexistingexisting:
  Existing Patch:
    Existing Game:
      BLUS00000:
        01.00:
          Enabled: true
""";
}

static void WriteInvalidOffsetParamSfo(string path)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

    writer.Write(0x46535000u);
    writer.Write(0x00000101u);
    writer.Write(1024u);
    writer.Write(2048u);
    writer.Write(1u);
}

static void WriteParamSfo(string path, IReadOnlyDictionary<string, string> values)
{
    var keys = values.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

    var keyTable = new MemoryStream();
    var dataTable = new MemoryStream();
    var entries = new List<(ushort KeyOffset, ushort Format, uint Length, uint MaxLength, uint DataOffset)>();

    foreach (var key in keys)
    {
        var keyOffset = (ushort)keyTable.Position;
        keyTable.Write(Encoding.UTF8.GetBytes(key));
        keyTable.WriteByte(0);

        var dataOffset = (uint)dataTable.Position;
        var bytes = Encoding.UTF8.GetBytes(values[key] + "\0");
        dataTable.Write(bytes);

        entries.Add((keyOffset, 0x0204, (uint)bytes.Length, (uint)bytes.Length, dataOffset));
    }

    var keyTableOffset = 20u + (uint)(entries.Count * 16);
    var dataTableOffset = keyTableOffset + (uint)keyTable.Length;

    writer.Write(0x46535000u);
    writer.Write(0x00000101u);
    writer.Write(keyTableOffset);
    writer.Write(dataTableOffset);
    writer.Write((uint)entries.Count);

    foreach (var entry in entries)
    {
        writer.Write(entry.KeyOffset);
        writer.Write(entry.Format);
        writer.Write(entry.Length);
        writer.Write(entry.MaxLength);
        writer.Write(entry.DataOffset);
    }

    keyTable.Position = 0;
    keyTable.CopyTo(stream);
    dataTable.Position = 0;
    dataTable.CopyTo(stream);
}

static string EscapeYamlPath(string path)
{
    return path.Replace("\\", "\\\\");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed record SmokeEnv(
    string TempRoot,
    string FakeRpcs3Root,
    string ConfigPath,
    string GameFolder,
    string WikiXmlPath,
    Rpcs3InstallDescriptor Install);

sealed record HwState(HwInfo LowEnd);

sealed record GameScanState(Rpcs3GameDescriptor Rogue);

sealed record CatState(WikiGameRecommendation Recommendation);

sealed record CfgPlanState(ConfigPlan Plan, IReadOnlyList<ConfigChange> SelectedChanges);
