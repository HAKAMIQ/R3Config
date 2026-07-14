using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.UseCases;

var tempRoot = Path.Combine(Path.GetTempPath(), "R3ConfigSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);

try
{
    RunLangKeyTests();
    RunArchitectureBoundaryTests();
    RunAppPatchPresentationSourceTests();
    RunPatchCatalogReaderDuplicateTopLevelHashTests();
    RunPatchCompatibilityArchitectureTests(tempRoot);
    HCanTests.Run();
    RunBundledCheatCompilerTests(tempRoot);
    RunPatchTransactionTests(tempRoot);
    RunCanaryImportTransactionTests(tempRoot);
    RunBackupRestoreTransactionTests(tempRoot);
    RunMutationGuardTests(tempRoot);
    RunGameListSearchTests();
    RunAdvancedDialogXamlSmokeTest();
    var env = CreateEnv(tempRoot);
    var hw = RunHwTests();
    var game = RunGameScanTests(env);
    var cat = RunCatTests(env);
    RunAnalysisReadPathTests(env, cat);
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
        "Patch.Source.CheatLibrary",
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
        "Patch.ApplyOfficialSection",
        "Patch.ApplyOfficialSectionTip",
        "Patch.ApplyCanarySection",
        "Patch.ApplyCanarySectionTip",
        "Patch.ApplyCheatSection",
        "Patch.ApplyCheatSectionTip",
        "Patch.Direct.Disabled",
        "Patch.Direct.MergedDisabled",
        "Patch.Direct.PreparedDisabled",
        "Patch.State.Disabled",
        "Patch.Definition.Merged",
        "Patch.Definition.NotMerged",
        "Patch.Definition.Prepared",
        "Patch.Definition.NotPrepared",
        "Patch.Definition.Existing",
        "Patch.SourceStatus.OfficialLoadedWithWarnings",
        "Patch.SourceStatus.CanaryLoadedWithWarnings",
        "Patch.SourceStatus.CanaryMatching",
        "Patch.SourceStatus.PartialBadge",
        "Msg.CfgApplySuccess",
        "Msg.PatchApplyOfficialSuccess",
        "Msg.PatchApplyCanarySuccess",
        "Msg.PatchApplyCheatSuccess",
        "Msg.PatchApplySuccess",
        "Patch.ImportCanaryTip",
        "Patch.SectionHint",
        "Handling.UserSelected",
        "Plan.ShowApplied",
        "Plan.SummaryFormat",
        "Patch.Empty",
        "Dialog.Ok",
        "Dialog.Cancel",
        "Patch.PrepareCanary",
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
        "Patch.CheatLibrary.Title",
        "Patch.CheatLibrary.Missing",
        "Patch.CheatLibrary.UnmanagedConflict",
        "Patch.CheatLibrary.Prepared",
        "Patch.CheatLibrary.Notice",
        "Tab.HCanary",
        "Tab.Advanced",
        "HCanary.Title",
        "HCanary.Description",
        "HCanary.Definitions",
        "HCanary.Families",
        "HCanary.SimplePatches",
        "HCanary.ComplexPatches",
        "HCanary.MultiFamilies",
        "HCanary.ScaffoldStatus",
        "HCanary.CatalogReady",
        "HCanary.CatalogError",
        "HCanary.AnalysisTitle",
        "HCanary.AnalysisUnavailable",
        "HCanary.SelectGame",
        "HCanary.NoMatches",
        "HCanary.AnalysisSummary",
        "HCanary.Evidence.None",
        "HCanary.Evidence.Count",
        "HCanary.Field.Version",
        "HCanary.Field.Hash",
        "HCanary.ReferenceSummary",
        "HCanary.Status.ExactReference",
        "HCanary.Status.HashEvidenceMissing",
        "HCanary.Status.VersionUnsupported",
        "HCanary.Status.ConflictingReferences",
        "HCanary.Status.ComplexManualReview",
        "HCanary.Status.NotPortable",
        "HCanary.Reason.ExactReference",
        "HCanary.Reason.HashEvidenceMissing",
        "HCanary.Reason.VersionUnsupported",
        "HCanary.Reason.ConflictingReferences",
        "HCanary.Reason.ComplexManualReview",
        "HCanary.Reason.NotPortable",
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

    Assert(ar["Plan.ApplySection"] == "تطبيق الإعدادات",
        "Expected the Arabic settings action to describe an explicit apply operation.");
    Assert(ar["Patch.ApplyOfficialSection"] == "تفعيل المحدد" &&
           ar["Patch.ApplyCanarySection"] == "إضافة وتفعيل المحدد" &&
           ar["Patch.ApplyCheatSection"] == "تجهيز وتفعيل المحدد",
        "Expected Arabic patch actions to distinguish enable-only, add-and-enable, and prepare-and-enable workflows.");
    Assert(ar["Tab.Canary"] == "باتشات Canary" &&
           ar["Patch.SourceStatus.CanaryTitle"] == "قاعدة باتشات Canary" &&
           ar["Patch.Source.Canary"] == "Canary",
        "Expected the player-facing Arabic page to use the direct Canary name instead of the ambiguous experimental-patches label.");
    Assert(!ar.ContainsKey("Games.Need") &&
           !ar.ContainsKey("Games.NeedDisplay") &&
           !ar.ContainsKey("Games.NeedTip") &&
           !en.ContainsKey("Games.Need") &&
           !en.ContainsKey("Games.NeedDisplay") &&
           !en.ContainsKey("Games.NeedTip"),
        "Expected the ambiguous per-game settings counter language contract to be removed.");

    Console.WriteLine($"Lang keys: ar-SA/en-US parity PASS ({ar.Count}).");
}

static void RunMutationGuardTests(string tempRoot)
{
    var root = Path.Combine(tempRoot, "mutation-guard", "rpcs3");
    var config = Path.Combine(root, "config");
    var patches = Path.Combine(root, "patches");
    Directory.CreateDirectory(config);
    Directory.CreateDirectory(patches);

    var install = new Rpcs3InstallDescriptor(
        root,
        Path.Combine(root, "rpcs3.exe"),
        config,
        Path.Combine(config, "custom_configs"),
        patches,
        Path.Combine(config, "patch_config.yml"));
    var safeGuard = new Rpcs3MutationGuard(() => false);

    safeGuard.EnsureCanMutate(
        install,
        [
            Path.Combine(config, "custom_configs", "config_TEST00001.yml"),
            Path.Combine(patches, "imported_patch.yml")
        ]);

    AssertMutationBlocked(
        () => safeGuard.EnsureCanMutate(
            install,
            [Path.Combine(tempRoot, "outside.yml")]),
        Rpcs3MutationBlockReason.UnsafeTarget,
        "Expected targets outside RPCS3 config/patches to be rejected.");
    AssertMutationBlocked(
        () => safeGuard.EnsureCanMutate(
            install,
            [install.PatchConfigPath, install.PatchConfigPath]),
        Rpcs3MutationBlockReason.UnsafeTarget,
        "Expected duplicate transaction targets to be rejected before writing.");
    AssertMutationBlocked(
        () => new Rpcs3MutationGuard(() => true).EnsureCanMutate(
            install,
            [install.PatchConfigPath]),
        Rpcs3MutationBlockReason.Rpcs3Running,
        "Expected every mutation boundary to block while RPCS3 is running.");

    var blockedImportReader = new PatchCatalogReader();
    var blockedImportBackup = new BackupService();
    var blockedImport = new CanaryImportUseCase(
        new CanaryPatchImportService(blockedImportReader),
        new Rpcs3MutationGuard(() => true),
        new PatchFileTransactionService(
            blockedImportBackup,
            blockedImportReader,
            new PatchConfigReader()),
        blockedImportBackup,
        blockedImportReader);
    AssertMutationBlocked(
        () => blockedImport.Import(
            install,
            "Version: 1.2\r\n"),
        Rpcs3MutationBlockReason.Rpcs3Running,
        "Expected the Canary import use case to enforce the central guard.");
    Assert(
        !File.Exists(Path.Combine(patches, "imported_patch.yml")),
        "A blocked use case must not create its target file.");

    var externalDataRoot = Path.Combine(
        tempRoot,
        "mutation-guard-external-data");
    var externalConfig = Path.Combine(externalDataRoot, "config");
    var externalPatches = Path.Combine(externalDataRoot, "patches");
    var externalInstall = install with
    {
        ConfigPath = externalConfig,
        CustomConfigsPath = Path.Combine(
            externalConfig,
            "custom_configs"),
        PatchesPath = externalPatches,
        PatchConfigPath = Path.Combine(
            externalConfig,
            "patch_config.yml"),
        DataRootPath = externalDataRoot
    };
    safeGuard.EnsureCanMutate(
        externalInstall,
        [
            externalInstall.PatchConfigPath,
            Path.Combine(externalPatches, "imported_patch.yml")
        ]);
    AssertMutationBlocked(
        () => safeGuard.EnsureCanMutate(
            externalInstall with
            {
                PatchesPath = Path.Combine(tempRoot, "spoofed-patches")
            },
            [Path.Combine(tempRoot, "spoofed-patches", "patch.yml")]),
        Rpcs3MutationBlockReason.InvalidInstall,
        "Expected a descriptor path outside its declared RPCS3 data-root layout to be rejected.");

    Console.WriteLine("Central RPCS3 mutation guard smoke PASS.");
}

static void RunArchitectureBoundaryTests()
{
    var repoRoot = FindRepoRoot();
    var coreRoot = Path.Combine(repoRoot, "Core");
    var applicationRoot = Path.Combine(repoRoot, "App", "Application");
    var viewModelRoot = Path.Combine(repoRoot, "App", "Vm");

    Assert(Directory.Exists(applicationRoot),
        "Expected an explicit App/Application use-case layer.");

    var coreSource = string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
    Assert(!coreSource.Contains("System.Windows", StringComparison.Ordinal) &&
           !coreSource.Contains("R3Config.Lang", StringComparison.Ordinal) &&
           !coreSource.Contains("R3Config.Vm", StringComparison.Ordinal) &&
           !coreSource.Contains("R3Config.View", StringComparison.Ordinal),
        "Core must remain independent of WPF, localization, and presentation namespaces.");

    var applicationSource = string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
    Assert(!applicationSource.Contains("System.Windows", StringComparison.Ordinal) &&
           !applicationSource.Contains("R3Config.Vm", StringComparison.Ordinal) &&
           !applicationSource.Contains("R3Config.View", StringComparison.Ordinal) &&
           !applicationSource.Contains("UiDlg", StringComparison.Ordinal) &&
           !applicationSource.Contains("UiText", StringComparison.Ordinal),
        "Application use cases must not depend directly on WPF, dialogs, or localized presentation text.");

    var viewModelSource = string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(viewModelRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
    Assert(!viewModelSource.Contains("File.WriteAllText", StringComparison.Ordinal) &&
           !viewModelSource.Contains("File.Move", StringComparison.Ordinal) &&
           !viewModelSource.Contains("File.Replace", StringComparison.Ordinal) &&
           !viewModelSource.Contains("File.Delete", StringComparison.Ordinal),
        "ViewModels must route file mutations through use cases instead of writing directly.");

    var mainParts = Directory.EnumerateFiles(
            viewModelRoot,
            "Main*.cs",
            SearchOption.TopDirectoryOnly)
        .ToArray();
    Assert(mainParts.Length >= 6 &&
           mainParts.All(path => File.ReadLines(path).Count() < 500),
        "Expected MainVm to remain split into bounded responsibility-based partial files.");

    Console.WriteLine("Architecture dependency boundaries PASS.");
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
    var bundledCheatServicePath = Path.Combine(appRoot, "Svc", "BCheat.cs");
    var bundledCheatDataPath = Path.Combine(appRoot, "Data", "cheats.yml");
    var transactionServicePath = Path.Combine(
        repoRoot,
        "Core",
        "Svc",
        "Patch",
        "Txn.cs");
    var appFlowPath = Path.Combine(appRoot, "Flow.cs");
    var patchMutationUseCasePath = Path.Combine(
        appRoot,
        "Application",
        "PatchMutationUseCase.cs");
    var compositionPath = Path.Combine(
        appRoot,
        "Composition",
        "AppComposition.cs");
    var mutationGuardPath = Path.Combine(
        repoRoot,
        "Core",
        "Svc",
        "Mutation",
        "Guard.cs");
    var planVmPath = Path.Combine(appRoot, "Vm", "Plan.cs");
    var mainVmPath = Path.Combine(appRoot, "Vm", "Main.cs");
    var mainWindowPath = Path.Combine(appRoot, "Main.xaml.cs");
    var mainWindowXamlPath = Path.Combine(appRoot, "Main.xaml");
    var gamesViewPath = Path.Combine(appRoot, "Ctrl", "Games.xaml");
    var configViewPath = Path.Combine(appRoot, "Ctrl", "Rec.xaml");
    var patchViewPath = Path.Combine(appRoot, "Ctrl", "Pat.xaml");
    var typeThemePath = Path.Combine(appRoot, "Thm", "Type.xaml");
    var hCanaryViewPath = Path.Combine(appRoot, "Ctrl", "HCan.xaml");
    var advancedViewPath = Path.Combine(appRoot, "View", "Adv.xaml");
    var appDialogPath = Path.Combine(appRoot, "View", "Dlg", "AppDialog.xaml");
    Assert(File.Exists(noteServicePath), "Expected PatchNoteTranslationService to exist in App layer.");
    Assert(File.Exists(importServicePath), "Expected CanaryPatchImportService to exist in App layer.");
    Assert(File.Exists(bundledCanaryServicePath), "Expected BundledCanaryPatchService to exist in App layer.");
    Assert(File.Exists(bundledCanaryDataPath), "Expected bundled Canary patch data to exist in App/Data.");
    Assert(File.Exists(bundledCheatServicePath), "Expected the safe bundled cheat preparation service to exist.");
    Assert(File.Exists(bundledCheatDataPath), "Expected the local converted cheat library to exist in App/Data.");
    Assert(File.Exists(transactionServicePath),
        "Expected the multi-file patch transaction service to exist in Core.");
    Assert(File.Exists(appFlowPath),
        "Expected AppFlow source to exist.");
    Assert(File.Exists(patchMutationUseCasePath),
        "Expected patch mutation orchestration to live in an Application use case.");
    Assert(File.Exists(compositionPath),
        "Expected one application composition root.");
    Assert(File.Exists(mutationGuardPath),
        "Expected RPCS3 mutations to have a Core safety guard.");
    var bundledCheatText = File.ReadAllText(bundledCheatDataPath);
    Assert(!bundledCheatText.Contains(
               "PPU-03a0b43e4a5f4bfea64f53612ee7c5d990f88129c:",
               StringComparison.OrdinalIgnoreCase),
        "Expected the malformed 41-character God of War Ascension executable hash keys to be removed.");
    var bundledCheatStrictRead = new PatchCatalogReader().Read(
        bundledCheatDataPath,
        PatchSourceOrigin.BundledCheats);
    Assert(
        bundledCheatStrictRead.Succeeded &&
        string.IsNullOrWhiteSpace(bundledCheatStrictRead.Error),
        "Expected the bundled cheat library to satisfy strict executable-hash validation: " +
        bundledCheatStrictRead.Error);
    Assert(
        bundledCheatStrictRead.Catalog.Entries.Count(entry => entry.RequiredPatches.Count > 0) >= 3,
        "Expected explicit cheat dependency metadata to be present in the bundled library.");
    var bundledCanaryRead = new PatchCatalogReader().Read(
        bundledCanaryDataPath,
        PatchSourceOrigin.BundledCanary);
    Assert(
        bundledCanaryRead.Succeeded,
        "Expected bundled Canary data to parse: " + bundledCanaryRead.Error);
    var gowAscensionCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
        entry.Hash.Equals(
            "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c",
            StringComparison.OrdinalIgnoreCase) &&
        entry.Title == "Unlock FPS");
    Assert(gowAscensionCanary.GameTargets.Count == 7,
        "Expected the God of War Ascension Canary definition to retain all seven declared regional editions.");
    var gowAscensionGame = new Rpcs3GameDescriptor(
        "BCUS98232",
        "God of War: Ascension",
        @"C:\Games\BCUS98232",
        "01.12",
        null,
        null);
    var gowAscensionCanaryMatch = new PatchMatchService().Match(
        gowAscensionCanary,
        [gowAscensionGame]);
    Assert(
        gowAscensionCanaryMatch.Status == PatchMatchStatus.SerialVersionMatchHashUnknown &&
        PatchActivationPolicy.CanEnable(gowAscensionCanaryMatch),
        "Expected the bundled God of War Ascension v01.12 Unlock FPS Canary patch to be visible and runtime-guarded when no contradictory local hash exists.");
    foreach (var target in gowAscensionCanary.GameTargets)
    {
        foreach (var version in target.Versions)
        {
            var regionalMatch = new PatchMatchService().Match(
                gowAscensionCanary,
                [new Rpcs3GameDescriptor(
                    target.Serial,
                    target.Title,
                    @"C:\Games\" + target.Serial,
                    version,
                    gowAscensionCanary.Hash,
                    null)]);
            Assert(
                regionalMatch.Status == PatchMatchStatus.ExactMatch &&
                regionalMatch.MatchedTargetSerial == target.Serial &&
                regionalMatch.MatchedTargetVersion == version,
                $"Expected Canary region/version coverage to match {target.Serial} v{version} using the declared runtime hash.");
        }
    }
    var gowWrongVersionMatch = new PatchMatchService().Match(
        gowAscensionCanary,
        [gowAscensionGame with
        {
            Version = "01.11",
            PpuHash = gowAscensionCanary.Hash
        }]);
    var gowWrongVersionItem = new PatchItemVm(
        gowWrongVersionMatch,
        "bundled Canary");
    Assert(
        gowWrongVersionMatch.Status == PatchMatchStatus.VersionMismatch &&
        gowWrongVersionItem.StateDisplay == UiText.T("Patch.State.Blocked") &&
        gowWrongVersionItem.DirectStatusText == UiText.F(
            "Patch.Direct.VersionMismatch",
            "01.11",
            "01.12"),
        "Expected an unsupported Canary game version to stay visible with the installed and required versions explained.");
    var gowAscensionCompiled = new PatchSelectionCompiler().Compile(
        File.ReadAllText(bundledCanaryDataPath),
        [gowAscensionCanaryMatch],
        [gowAscensionCanaryMatch]);
    Assert(
        gowAscensionCompiled.Succeeded &&
        gowAscensionCompiled.PatchText.Contains("0x003a52b4", StringComparison.OrdinalIgnoreCase) &&
        gowAscensionCompiled.PatchText.Contains("0x002d024c", StringComparison.OrdinalIgnoreCase),
        "Expected the God of War Ascension Unlock FPS Canary definition to retain both official Wiki operations.");

    var bundledCanaryItem = new PatchItemVm(
        gowAscensionCanaryMatch,
        "bundled Canary");
    Assert(
        bundledCanaryItem.HasDefinitionState &&
        !bundledCanaryItem.IsDefinitionResident &&
        bundledCanaryItem.DefinitionStateDisplay == UiText.T("Patch.Definition.NotMerged") &&
        bundledCanaryItem.StateDisplay == UiText.T("Patch.State.Ready") &&
        !bundledCanaryItem.IsSelected,
        "Expected a bundled Canary option to remain explicitly not merged and never preselected.");
    var importedCanaryMatch = new PatchMatchService().Match(
        gowAscensionCanary with { Source = PatchSourceOrigin.Imported },
        [gowAscensionGame]);
    var importedCanaryItem = new PatchItemVm(
        importedCanaryMatch,
        "imported_patch.yml");
    Assert(
        importedCanaryItem.IsDefinitionResident &&
        importedCanaryItem.DefinitionStateDisplay == UiText.T("Patch.Definition.Merged") &&
        importedCanaryItem.StateDisplay == UiText.T("Patch.State.Disabled") &&
        importedCanaryItem.DirectStatusText == UiText.T("Patch.Direct.MergedDisabled"),
        "Expected a resident Canary definition to remain visibly merged after its selection checkmark clears.");
    var enabledImportedCanaryItem = new PatchItemVm(
        importedCanaryMatch,
        "imported_patch.yml",
        isEnabled: true);
    Assert(
        enabledImportedCanaryItem.DefinitionStateDisplay == UiText.T("Patch.Definition.Merged") &&
        enabledImportedCanaryItem.StateDisplay == UiText.T("Patch.State.Enabled") &&
        !enabledImportedCanaryItem.IsSelected,
        "Expected Canary merge state and enable state to remain independently visible without retaining an action checkmark.");

    var gowAscensionCheat = bundledCheatStrictRead.Catalog.Entries.Single(entry =>
        entry.Hash.Equals(
            "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c",
            StringComparison.OrdinalIgnoreCase) &&
        entry.Title == "(B) Button Enable Code");
    var gowAscensionFpsCheat = bundledCheatStrictRead.Catalog.Entries.Single(entry =>
        entry.Hash.Equals(
            gowAscensionCanary.Hash,
            StringComparison.OrdinalIgnoreCase) &&
        entry.Title == "Unlock FPS");
    var crossChannelMerge = new PatchCatalogMergeService().Merge(
        new Rpcs3PatchCatalog("1.2", [gowAscensionCanary]),
        new Rpcs3PatchCatalog("1.2", [gowAscensionFpsCheat]));
    Assert(
        crossChannelMerge.Entries.Count == 2 &&
        crossChannelMerge.Entries.Any(entry => entry.Risk == PatchRisk.Canary) &&
        crossChannelMerge.Entries.Any(entry => entry.Risk == PatchRisk.Cheat),
        "Expected same-title Canary and Artemis definitions to remain independent instead of being rejected as a duplicate conflict.");
    var crossChannelPreview = new PatchPreviewVm();
    crossChannelPreview.Load(
        crossChannelMerge.Entries
            .Select(entry => new PatchMatchService().Match(entry, [gowAscensionGame]))
            .ToArray(),
        "Canary and Artemis catalogs");
    Assert(
        crossChannelPreview.CanaryItems.Any(item => item.Title == "Unlock FPS") &&
        crossChannelPreview.CheatItems.Any(item => item.Title == "Unlock FPS"),
        "Expected the God of War Ascension Unlock FPS Canary option to remain visible beside the distinct Artemis cheat.");
    var gowAscensionCheatMatch = new PatchMatchService().Match(
        gowAscensionCheat,
        [gowAscensionGame]);
    var bundledCheatItem = new PatchItemVm(
        gowAscensionCheatMatch,
        "bundled cheats");
    Assert(
        bundledCheatItem.DefinitionStateDisplay == UiText.T("Patch.Definition.NotPrepared") &&
        !bundledCheatItem.IsDefinitionResident,
        "Expected a bundled Artemis cheat to say that its game patch definition is not prepared yet.");
    var managedCheatMatch = new PatchMatchService().Match(
        gowAscensionCheat with { Source = PatchSourceOrigin.ManagedCheats },
        [gowAscensionGame]);
    var managedCheatItem = new PatchItemVm(
        managedCheatMatch,
        "BCUS98232_patch.yml");
    Assert(
        managedCheatItem.DefinitionStateDisplay == UiText.T("Patch.Definition.Prepared") &&
        managedCheatItem.StateDisplay == UiText.T("Patch.State.Disabled") &&
        managedCheatItem.DirectStatusText == UiText.T("Patch.Direct.PreparedDisabled"),
        "Expected a resident Artemis cheat to remain visibly prepared even while disabled.");
    var infamousCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
        entry.Hash.Equals(
            "d0c3e2a107c47d20d3316325ce6f75464a8f4f1d",
            StringComparison.OrdinalIgnoreCase) &&
        entry.Title == "(Experimental) Disable visual effects");
    var infamousEuropeanMatch = new PatchMatchService().Match(
        infamousCanary,
        [new Rpcs3GameDescriptor(
            "BCES00220",
            "inFamous",
            @"C:\Games\BCES00220",
            "01.00",
            "d0c3e2a107c47d20d3316325ce6f75464a8f4f1d",
            null)]);
    Assert(
        infamousEuropeanMatch.Status == PatchMatchStatus.ExactMatch,
        "Expected the d0c3 inFamous Canary definition to retain its declared European serial scope.");
    var infamousCrossRegionMatch = new PatchMatchService().Match(
        infamousCanary,
        [new Rpcs3GameDescriptor(
            "BCUS98119",
            "inFamous",
            @"C:\Games\BCUS98119",
            "01.00",
            "d0c3e2a107c47d20d3316325ce6f75464a8f4f1d",
            null)]);
    Assert(
        infamousCrossRegionMatch.Status == PatchMatchStatus.SerialMismatch,
        "Expected a matching hash not to broaden a Canary definition across undeclared game regions.");
    var infamousAmericanCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
        entry.Hash.Equals(
            "412a9bf323b4743de21658a286f71dbe82cad807",
            StringComparison.OrdinalIgnoreCase) &&
        entry.Title == "(Experimental) Disable visual effects");
    var infamousAmericanMatch = new PatchMatchService().Match(
        infamousAmericanCanary,
        [new Rpcs3GameDescriptor(
            "BCUS98119",
            "inFamous",
            @"C:\Games\BCUS98119",
            "01.00",
            "412a9bf323b4743de21658a286f71dbe82cad807",
            null)]);
    Assert(
        infamousAmericanMatch.Status == PatchMatchStatus.ExactMatch,
        "Expected BCUS98119 to use the separate 412a Canary runtime identity declared by the source.");
    var infamousCanaryCompiled = new PatchSelectionCompiler().Compile(
        File.ReadAllText(bundledCanaryDataPath),
        [infamousEuropeanMatch],
        [infamousEuropeanMatch]);
    Assert(
        infamousCanaryCompiled.Succeeded &&
        !infamousCanaryCompiled.PatchText.Contains(
            "*Infam1_",
            StringComparison.Ordinal),
        "Expected selected Canary output to expand YAML anchors before imported_patch.yml merge: " +
        infamousCanaryCompiled.Error);
    Assert(File.Exists(mainVmPath), "Expected MainVm source to exist in App/Vm.");
    Assert(File.Exists(mainWindowPath), "Expected MainWindow source to exist in App.");
    Assert(File.Exists(mainWindowXamlPath), "Expected MainWindow XAML to exist in App.");
    Assert(File.Exists(gamesViewPath), "Expected game list view source to exist in App/Ctrl.");
    Assert(File.Exists(configViewPath), "Expected config recommendation view source to exist in App/Ctrl.");
    Assert(File.Exists(hCanaryViewPath), "Expected H-Canary view source to exist in App/Ctrl.");
    Assert(File.Exists(advancedViewPath), "Expected the advanced tools view to exist in App/View.");
    Assert(File.ReadAllText(planVmPath).Contains("_showApplied = true", StringComparison.Ordinal),
        "Expected plan view to show applied recommendations by default.");
    Assert(File.ReadAllText(appDialogPath).Contains("AllowsTransparency=\"True\"", StringComparison.Ordinal),
        "Expected custom app dialogs to avoid black rounded-corner artifacts.");
    var mainWindowXaml = File.ReadAllText(mainWindowXamlPath);
    Assert(mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Settings}\"", StringComparison.Ordinal) &&
           mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Patches}\"", StringComparison.Ordinal) &&
           mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Canary}\"", StringComparison.Ordinal) &&
           mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Cheats}\"", StringComparison.Ordinal),
        "Expected the main navigation to expose separate settings, regular patch, experimental patch, and cheat pages.");
    Assert(!mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Official}\"", StringComparison.Ordinal) &&
           !mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.Advanced}\"", StringComparison.Ordinal) &&
           !mainWindowXaml.Contains("Header=\"{lang:Text Key=Tab.HCanary}\"", StringComparison.Ordinal),
        "Expected advanced authoring tools to stay out of the game patch navigation.");
    Assert(mainWindowXaml.Contains("Mode=\"Patches\"", StringComparison.Ordinal) &&
           mainWindowXaml.Contains("Mode=\"Canary\"", StringComparison.Ordinal) &&
           mainWindowXaml.Contains("Mode=\"Cheat\"", StringComparison.Ordinal) &&
           !mainWindowXaml.Contains("<controls:HCanaryView", StringComparison.Ordinal),
        "Expected each player-facing patch class to have one explicit page.");

    var gamesView = File.ReadAllText(gamesViewPath);
    Assert(!gamesView.Contains("GameNeedBadge", StringComparison.Ordinal) &&
           !gamesView.Contains("NeedDisplay", StringComparison.Ordinal) &&
           !gamesView.Contains("NeedTip", StringComparison.Ordinal),
        "Expected the game rail to omit the ambiguous recommendation count badge.");
    var configView = File.ReadAllText(configViewPath);
    Assert(configView.Contains("Key=Plan.ApplySection", StringComparison.Ordinal) &&
           configView.Contains("Command=\"{Binding ApplyCheckedCommand}\"", StringComparison.Ordinal),
        "Expected game recommendations to expose an explicit guarded apply action.");

    var patchView = File.ReadAllText(patchViewPath);
    Assert(!patchView.Contains("<TabControl", StringComparison.Ordinal) &&
           !patchView.Contains("OfficialSelector", StringComparison.Ordinal) &&
           !patchView.Contains("CanarySelector", StringComparison.Ordinal),
        "Expected patch category navigation to remain at the main page level without a nested selector.");
    Assert(patchView.Contains("ApplyOfficialPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("ApplyCanaryPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("DisableOfficialPatchesCommand", StringComparison.Ordinal) &&
           patchView.Contains("DisableCanaryPatchesCommand", StringComparison.Ordinal),
        "Expected each patch section to execute only its own selected items.");
    Assert(patchView.Contains("Key=Patch.ApplyOfficialSection", StringComparison.Ordinal) &&
           patchView.Contains("Key=Patch.ApplyCanarySection", StringComparison.Ordinal) &&
           patchView.Contains("Key=Patch.ApplyCheatSection", StringComparison.Ordinal) &&
           !patchView.Contains("Key=Patch.ApplySection}", StringComparison.Ordinal),
        "Expected official, Canary, and cheat actions to describe their distinct target-file workflows.");
    Assert(!patchView.Contains("ToolTip", StringComparison.Ordinal),
        "Expected player patch cards and labeled actions to avoid automatic hover popups.");
    var typeTheme = File.ReadAllText(typeThemePath);
    var captionStyleStart = typeTheme.IndexOf(
        "<Style x:Key=\"ArabicCaptionText\"",
        StringComparison.Ordinal);
    var captionStyleEnd = typeTheme.IndexOf(
        "</Style>",
        captionStyleStart,
        StringComparison.Ordinal);
    var techStyleStart = typeTheme.IndexOf(
        "<Style x:Key=\"TechText\"",
        StringComparison.Ordinal);
    var techStyleEnd = typeTheme.IndexOf(
        "</Style>",
        techStyleStart,
        StringComparison.Ordinal);
    Assert(captionStyleStart >= 0 && captionStyleEnd > captionStyleStart &&
           techStyleStart >= 0 && techStyleEnd > techStyleStart &&
           !typeTheme[captionStyleStart..captionStyleEnd].Contains(
               "Property=\"ToolTip\"",
               StringComparison.Ordinal) &&
           !typeTheme[techStyleStart..techStyleEnd].Contains(
               "Property=\"ToolTip\"",
               StringComparison.Ordinal),
        "Expected ordinary caption and technical text to avoid implicit copy-of-text hover popups; intentional controls can still opt in explicitly.");
    Assert(patchView.Contains("Text=\"{lang:Text Key=Patch.ApplyOfficialSectionTip}\"", StringComparison.Ordinal) &&
           patchView.Contains("Text=\"{lang:Text Key=Patch.ApplyCanarySectionTip}\"", StringComparison.Ordinal) &&
           patchView.Contains("Text=\"{lang:Text Key=Patch.ApplyCheatSectionTip}\"", StringComparison.Ordinal),
        "Expected each patch workflow explanation to remain visible instead of hiding in a tooltip.");
    Assert(patchView.Contains("DefinitionStateDisplay", StringComparison.Ordinal) &&
           patchView.Contains("HasDefinitionState", StringComparison.Ordinal),
        "Expected patch rows to keep a persistent merged/prepared definition badge separate from the action checkbox.");
    Assert(!patchView.Contains("Key=Patch.SourceStatus.CanaryMatching", StringComparison.Ordinal),
        "Expected the Canary page to avoid a permanent technical matching paragraph on the player surface.");
    Assert(patchView.Split(
               "Style=\"{StaticResource PrimaryButton}\"",
               StringSplitOptions.None).Length == 4,
        "Expected the three positive patch actions to use the primary action style.");
    Assert(patchView.Contains("OfficialPatchSourceStatusText", StringComparison.Ordinal) &&
           patchView.Contains("CanaryPatchSourceStatusText", StringComparison.Ordinal) &&
           patchView.Contains("OpenRpcs3PatchManagerCommand", StringComparison.Ordinal) &&
           !patchView.Contains("OfficialPatchSourcePath", StringComparison.Ordinal) &&
           !patchView.Contains("CanaryPatchSourcePath", StringComparison.Ordinal) &&
           !patchView.Contains("OpenPatchesFolderCommand", StringComparison.Ordinal),
        "Expected player patch pages to expose database readiness and the RPCS3-owned update workflow without file-system details.");
    Assert(!patchView.Contains("Command=\"{Binding PrepareCanaryPatchesCommand}\"", StringComparison.Ordinal) &&
           !patchView.Contains("Command=\"{Binding ImportCanaryPatchCommand}\"", StringComparison.Ordinal) &&
           !patchView.Contains("Command=\"{Binding RemoveCanaryPatchesCommand}\"", StringComparison.Ordinal),
        "Expected Canary preparation and authoring to stay automatic or inside advanced tools instead of the player page.");
    var hCanaryView = File.ReadAllText(hCanaryViewPath);
    Assert(hCanaryView.Contains("Text=\"{lang:Text Key=HCanary.Title}\"", StringComparison.Ordinal) &&
           !hCanaryView.Contains("Text=\"إنشاء باتشات H-Canary\"", StringComparison.Ordinal),
        "Expected the H-Canary editor title to use localization instead of hardcoded Arabic text.");
    var advancedView = File.ReadAllText(advancedViewPath);
    Assert(advancedView.Contains("<controls:HCanaryView", StringComparison.Ordinal) &&
           advancedView.Contains("Key=Adv.PatchDevelopment", StringComparison.Ordinal) &&
           advancedView.Contains("Key=Adv.PatchDev.Template", StringComparison.Ordinal) &&
           advancedView.Contains("Key=Adv.Recovery", StringComparison.Ordinal) &&
           advancedView.Contains("Key=Adv.Diagnostics", StringComparison.Ordinal),
        "Expected patch development, authoring, recovery, and diagnostics to be grouped in the advanced tools window.");

    var mainVm = string.Join(
        Environment.NewLine,
        Directory
            .EnumerateFiles(
                Path.Combine(appRoot, "Vm"),
                "Main*.cs",
                SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(File.ReadAllText));
    Assert(!mainVm.Contains("PrepareCanaryPatchesCommand", StringComparison.Ordinal) &&
           !mainVm.Contains("private void PrepareCanaryPatches()", StringComparison.Ordinal),
        "Expected the obsolete standalone Canary preparation command and handler to be removed.");
    Assert(mainVm.Contains("ApplyPatchTransaction", StringComparison.Ordinal) &&
           !mainVm.Contains("_flow.PrepareBundledCanaryPatch(_install", StringComparison.Ordinal) &&
           !mainVm.Contains("_flow.PrepareBundledCheatPatch(_install", StringComparison.Ordinal),
        "Expected patch enable to use one staged transaction instead of sequential source writes.");
    Assert(mainVm.Contains("Msg.CfgApplySuccess", StringComparison.Ordinal) &&
           mainVm.Contains("Msg.PatchApplyOfficialSuccess", StringComparison.Ordinal) &&
           mainVm.Contains("Msg.PatchApplyCanarySuccess", StringComparison.Ordinal) &&
           mainVm.Contains("Msg.PatchApplyCheatSuccess", StringComparison.Ordinal),
        "Expected successful settings and patch writes to provide a visible confirmation after disk refresh.");
    Assert(!mainVm.Contains(
               "CombinePatchWarnings(\r\n                _patchCatalogError",
               StringComparison.Ordinal) &&
           !mainVm.Contains(
               "CombinePatchWarnings(\n                _patchCatalogError",
               StringComparison.Ordinal),
        "Expected raw catalog parser diagnostics to stay in source status/logging instead of the global player warning banner.");
    var mainWindow = File.ReadAllText(mainWindowPath);
    Assert(mainVm.Contains("LanguageChanged?.Invoke", StringComparison.Ordinal) &&
           mainWindow.Contains("RebuildPatchViews", StringComparison.Ordinal),
        "Expected a live language switch to recreate patch visuals after preserving their view-model state.");
    Assert(patchView.Contains("DirectStatusText", StringComparison.Ordinal) &&
           patchView.Contains("PlayerInstructionText", StringComparison.Ordinal) &&
           !patchView.Contains("HashEvidenceSummary", StringComparison.Ordinal) &&
           !patchView.Contains("TechnicalTargetDisplay", StringComparison.Ordinal) &&
           !patchView.Contains("InstalledVersionDisplay", StringComparison.Ordinal),
        "Expected player patch rows to render only an actionable state and relevant instructions while keeping technical evidence in optional details.");
    Assert(patchView.Contains("FlowDirection=\"{Binding PatchPreview.TextFlow}\"", StringComparison.Ordinal) &&
           patchView.Contains("FlowDirection=\"{Binding NoteFlow}\"", StringComparison.Ordinal) &&
           !patchView.Contains("DynamicResource App.FlowDirection", StringComparison.Ordinal),
        "Expected live patch templates to bind direction to language-aware view models instead of retaining stale dynamic resources.");

    var noteService = File.ReadAllText(noteServicePath);
    Assert(noteService.Contains("Prevents random freezing/crashing while playing the game", StringComparison.Ordinal),
        "Expected exact wiki note phrase mapping source.");
    Assert(noteService.Contains("يمنع التجمّد أو الانهيار العشوائي أثناء اللعب", StringComparison.Ordinal),
        "Expected exact Arabic wiki note translation source.");
    Assert(noteService.Contains("No local translation", StringComparison.Ordinal) || noteService.Contains("Patch.Note.NoLocalTranslation", StringComparison.Ordinal),
        "Expected note fallback source.");

    var installRoot = Path.Combine(
        Path.GetTempPath(),
        "R3ConfigInstallSmoke",
        Guid.NewGuid().ToString("N"));

    Directory.CreateDirectory(installRoot);

    try
    {
        var exePath = Path.Combine(installRoot, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExePath(exePath);
        var expectedConfigPath = Path.Combine(installRoot, "config");
        var expectedCustomConfigsPath = Path.Combine(expectedConfigPath, "custom_configs");

        Assert(string.Equals(
                install.ConfigPath,
                expectedConfigPath,
                StringComparison.OrdinalIgnoreCase),
            "Expected Rpcs3InstallService to anchor config to root config.");
        Assert(string.Equals(
                install.CustomConfigsPath,
                expectedCustomConfigsPath,
                StringComparison.OrdinalIgnoreCase),
            "Expected Rpcs3InstallService to anchor custom_configs below root config.");
    }
    finally
    {
        Directory.Delete(installRoot, recursive: true);
    }

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
    Assert(importService.Contains("PrepareManagedImportedPatchFile", StringComparison.Ordinal),
        "Expected managed Canary output to support write-free transaction staging.");
    Assert(importService.Contains("RemoveManagedImportedPatch", StringComparison.Ordinal),
        "Expected managed Canary removal path.");
    Assert(importService.Contains("ExtractNormalizedHashSections", StringComparison.Ordinal),
        "Expected managed Canary compose to merge inside existing PPU/SPU/PRX/OVL hash blocks.");
    Assert(importService.Contains("RemoveEquivalentPatchBlocksOrThrow", StringComparison.Ordinal),
        "Expected managed Canary writes to keep equivalent definitions but reject conflicting identities explicitly.");
    Assert(importService.Contains("IsPathInsideDirectory", StringComparison.Ordinal),
        "Expected safe path write guard source.");

    var bundledService = File.ReadAllText(bundledCanaryServicePath);
    Assert(bundledService.Contains("can.yml", StringComparison.Ordinal),
        "Expected bundled Canary service to reference the bundled data file.");
    Assert(bundledService.Contains("PrepareImportedPatch", StringComparison.Ordinal),
        "Expected bundled Canary service to prepare imported_patch.yml.");
    Assert(bundledService.Contains("PrepareImportedPatchFile", StringComparison.Ordinal),
        "Expected bundled Canary selection to support transaction staging.");
    Assert(bundledService.Contains("BuildSelectedCanaryText", StringComparison.Ordinal),
        "Expected bundled Canary service to write only selected Canary patches.");

    var bundledCheatService = File.ReadAllText(bundledCheatServicePath);
    Assert(bundledCheatService.Contains("PrepareManagedPatchFile", StringComparison.Ordinal),
        "Expected bundled cheat output to support write-free transaction staging.");

    var transactionService = File.ReadAllText(transactionServicePath);
    Assert(transactionService.Contains("StageAndValidate", StringComparison.Ordinal) &&
           transactionService.Contains("_backupService.CreateBackup", StringComparison.Ordinal) &&
           transactionService.Contains("_backupService.Restore", StringComparison.Ordinal),
        "Expected staged validation, one backup manifest, and full rollback in the transaction service.");

    var appFlow = File.ReadAllText(appFlowPath);
    var patchMutationUseCase = File.ReadAllText(patchMutationUseCasePath);
    var composition = File.ReadAllText(compositionPath);
    var mutationGuard = File.ReadAllText(mutationGuardPath);
    Assert(appFlow.Contains("_useCases.PatchMutation.ApplyTransaction", StringComparison.Ordinal) &&
           !appFlow.Contains("PrepareImportedPatchFile", StringComparison.Ordinal) &&
           !appFlow.Contains("PrepareManagedPatchFile", StringComparison.Ordinal),
        "Expected AppFlow to remain a thin compatibility facade.");
    Assert(patchMutationUseCase.Contains("PrepareImportedPatchFile", StringComparison.Ordinal) &&
           patchMutationUseCase.Contains("PrepareManagedPatchFile", StringComparison.Ordinal) &&
           patchMutationUseCase.Contains("applyService.ApplyTransaction", StringComparison.Ordinal),
        "Expected the patch mutation use case to stage Canary/Cheat outputs before one transaction.");
    Assert(composition.Contains("new PatchFileTransactionService", StringComparison.Ordinal) &&
           composition.Contains("new Rpcs3MutationGuard", StringComparison.Ordinal),
        "Expected the composition root to share transaction and mutation-guard dependencies.");
    Assert(mutationGuard.Contains("IsRpcs3Running", StringComparison.Ordinal) &&
           mutationGuard.Contains("IsChildPath", StringComparison.Ordinal),
        "Expected the central mutation guard to enforce process and path boundaries.");

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
    Assert(canaryRead.Catalog.Entries
            .Select(entry => $"{entry.HashKind}|{entry.Hash}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 86,
        "Expected the bundled Canary database to retain all 86 executable Hash keys from the supplied RPCS3 Wiki snapshot.");
    Assert(canaryRead.Catalog.Entries.All(entry => entry.Risk == PatchRisk.Canary),
        "Expected bundled Canary entries to be classified as Canary.");

    var normalizedSpuCanary = new CanaryPatchImportService().Normalize(
        "Version: 1.2\n" +
        "SPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:\n" +
        "  \"SPU Canary\":\n" +
        "    Games:\n" +
        "      \"SPU Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n");
    Assert(normalizedSpuCanary.Succeeded,
        "Expected the custom import path to support SPU executable hashes.");
    var spuCanaryRead = new PatchCatalogReader().ReadText(
        normalizedSpuCanary.NormalizedText,
        isCanarySource: true);
    Assert(spuCanaryRead.Succeeded &&
           spuCanaryRead.Catalog.Entries.Single().HashKind == PatchHashKind.Spu,
        "Expected imported SPU Canary data to remain SPU-scoped.");

    var bundledCheatRead = new PatchCatalogReader().Read(
        bundledCheatDataPath,
        PatchSourceOrigin.BundledCheats);
    Assert(bundledCheatRead.Succeeded,
        "Expected the bundled converted cheat library to be readable: " + bundledCheatRead.Error);
    Assert(bundledCheatRead.Catalog.Entries.Count >= 2500,
        "Expected the converted cheat expansion to expose its full local inventory.");
    Assert(bundledCheatRead.Catalog.Entries.All(entry => entry.Risk == PatchRisk.Cheat),
        "Expected every bundled expansion entry to be handled as a user-selected cheat.");

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

    Assert(Rpcs3ExecutableHash.TryNormalize(
               "QoiFRPF8j00N996aQMthtWqEUG0Y",
               out var normalizedBase57Hash) &&
           normalizedBase57Hash.Equals(
               "728fc4496a4c347a654ab54d3e3f0e44ed3c9af5",
               StringComparison.Ordinal),
        "Expected RPCS3's 28-character Base57 SHA-1 key to normalize to the canonical 40-hex executable hash.");
    Assert(!Rpcs3ExecutableHash.TryNormalize(
               "QoiFRPF8j00N996aQMthtWqEUG0B",
               out _),
        "Expected invalid RPCS3 Base57 alphabet characters to be rejected.");

    var strictHashText =
        "Version: 1.2\n" +
        $"PPU-{new string('a', 39)}:\n" +
        "  \"Short Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        $"PPU-{new string('b', 41)}:\n" +
        "  \"Long Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        "SPU-gggggggggggggggggggggggggggggggggggggggg:\n" +
        "  \"Non Hex Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        "PPU-0000000000000000000000000000000000000000:\n" +
        "  \"Zero Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        "PPU-QoiFRPF8j00N996aQMthtWqEUG0Y:\n" +
        "  \"Invalid Base57 PPU Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        "PRX-QoiFRPF8j00N996aQMthtWqEUG0Y-0:\n" +
        "  \"RPCS3 PRX Base57 Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n" +
        "PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:\n" +
        "  \"Exact Hash\":\n" +
        "    Games:\n" +
        "      \"Hash Game\":\n" +
        "        BLUS00001: [ 01.00 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\n";

    var strictHashRead = reader.ReadText(strictHashText);
    Assert(strictHashRead.Succeeded,
        "Expected malformed executable hash sections to be skipped without invalidating the full catalog.");
    Assert(strictHashRead.Catalog.Entries.Count == 2 &&
           strictHashRead.Catalog.Entries.Any(entry => entry.Title == "Exact Hash") &&
           strictHashRead.Catalog.Entries.Any(entry =>
               entry.Title == "RPCS3 PRX Base57 Hash" &&
               entry.NormalizedExecutableHash ==
                   "728fc4496a4c347a654ab54d3e3f0e44ed3c9af5-0"),
        "Expected PPU/SPU/OVL 40-hex keys and RPCS3 PRX Base57-plus-segment keys to enter the catalog.");
    Assert(!string.IsNullOrWhiteSpace(strictHashRead.Error) &&
           strictHashRead.Error.Contains("40-hex", StringComparison.OrdinalIgnoreCase) &&
           strictHashRead.Error.Contains("Base57", StringComparison.OrdinalIgnoreCase),
        "Expected malformed executable hash sections to produce reviewable diagnostics.");

    var base57Patch = strictHashRead.Catalog.Entries.Single(entry =>
        entry.Title == "RPCS3 PRX Base57 Hash");
    var base57Game = new Rpcs3GameDescriptor(
        Serial: "BLUS00001",
        Title: "Hash Game",
        InstallPath: @"C:\Games\HashGame",
        Version: "01.00",
        PpuHash: null,
        SpuHash: null,
        HashSource: "RPCS3.log",
        HashMessage: "Base57 normalization regression.",
        PrxHashes: ["728fc4496a4c347a654ab54d3e3f0e44ed3c9af5-0"]);
    var base57Match = new PatchMatchService().Match(base57Patch, [base57Game]);
    Assert(base57Match.Status == PatchMatchStatus.ExactMatch,
        "Expected RPCS3 PRX Base57 runtime keys to match canonical module evidence.");

    Console.WriteLine("Patch catalog duplicate top-level hash smoke PASS.");
}

static void RunPatchCompatibilityArchitectureTests(string tempRoot)
{
    const string serial = "BLUS00001";
    const string version = "01.00";
    const string exactHash = "728fc4496a4c347a654ab54d3e3f0e44ed3c9af5";
    const string otherHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string prxBase57 = "QoiFRPF8j00N996aQMthtWqEUG0Y";
    const string olderCacheHash = "83681f6110d33442329073b72b8dc88a2f677171";
    const string olderCacheBase57 = "U8pStuqfrkhA3fGiHfGZz99SofxM";

    Assert(Rpcs3PatchRuntimeKey.TryParse(
               $"PPU-{exactHash}",
               out var ppuKey) &&
           ppuKey.Kind == PatchHashKind.Ppu &&
           ppuKey.NormalizedHash == exactHash,
        "Expected a 40-hex PPU key to match RPCS3's runtime naming contract.");
    Assert(!Rpcs3PatchRuntimeKey.TryParse(
               $"PPU-{prxBase57}",
               out _),
        "Expected a Base57 PPU patch key to be rejected because RPCS3 emits hexadecimal PPU runtime keys.");
    Assert(Rpcs3PatchRuntimeKey.TryParse(
               $"PRX-{prxBase57}-0",
               out var prxKey) &&
           prxKey.Kind == PatchHashKind.Prx &&
           prxKey.NormalizedHash == exactHash + "-0",
        "Expected RPCS3's Base57-plus-segment PRX key to normalize for comparison.");
    Assert(Rpcs3PatchRuntimeKey.TryParse(
               $"{serial}-PPU-{exactHash}",
               out var scopedKey) &&
           scopedKey.SerialScope == serial,
        "Expected RPCS3 TITLE_ID-prefixed alternative runtime keys to be recognized.");
    Assert(Rpcs3PatchRuntimeKey.TryParse(
               $"OVL-{exactHash}",
               out var ovlKey) &&
           ovlKey.Kind == PatchHashKind.Ovl,
        "Expected hexadecimal OVL runtime keys to be recognized.");

    var compatibility = new PatchCompatibilityService();
    var target = new Rpcs3PatchGameTarget(
        "Compatibility Game",
        serial,
        [version]);
    var exactGame = new Rpcs3GameDescriptor(
        Serial: serial,
        Title: "Compatibility Game",
        InstallPath: tempRoot,
        Version: version,
        PpuHash: exactHash,
        SpuHash: null,
        HashEvidenceKind: PatchHashEvidenceKind.ExistingMetadata);

    foreach (var source in new[]
             {
                 PatchSourceOrigin.Official,
                 PatchSourceOrigin.BundledCanary,
                 PatchSourceOrigin.Imported,
                 PatchSourceOrigin.BundledCheats,
                 PatchSourceOrigin.ManagedCheats,
                 PatchSourceOrigin.TitleSpecific
             })
    {
        var patch = CreatePatch(
            "Unified source " + source,
            PatchHashKind.Ppu,
            exactHash,
            target,
            source);
        var exact = compatibility.Evaluate(patch, [exactGame]);
        Assert(exact.Compatibility is
               {
                   Verdict: PatchCompatibilityVerdict.Compatible,
                   ReasonCode: PatchCompatibilityReasonCode.ExactRuntimeIdentity,
                   CanEnable: true
               },
            $"Expected {source} to use the same exact compatibility decision.");

        var runtimeGuarded = compatibility.Evaluate(
            patch,
            [exactGame with
            {
                PpuHash = null,
                HashEvidenceKind = PatchHashEvidenceKind.None
            }]);
        Assert(runtimeGuarded.Compatibility is
               {
                   Verdict: PatchCompatibilityVerdict.RuntimeGuarded,
                   ReasonCode: PatchCompatibilityReasonCode.Rpcs3RuntimeHashGuard,
                   CanEnable: true,
                   UsesRpcs3RuntimeHashGuard: true
               },
            $"Expected {source} to use RPCS3's runtime hash guard when local PPU evidence is absent.");
    }

    var officialPatch = CreatePatch(
        "Official compatibility",
        PatchHashKind.Ppu,
        exactHash,
        target,
        PatchSourceOrigin.Official);

    var wrongVersion = compatibility.Evaluate(
        officialPatch,
        [exactGame with { Version = "02.00" }]);
    Assert(wrongVersion.Compatibility is
           {
               Verdict: PatchCompatibilityVerdict.Blocked,
               ReasonCode: PatchCompatibilityReasonCode.VersionMismatch,
               CanEnable: false,
               CanDisable: true
            },
        "Expected an installed-version mismatch to block activation while preserving safe removal of an old enabled entry.");

    var authoritativeHashMismatch = compatibility.Evaluate(
        officialPatch,
        [exactGame with { PpuHash = otherHash }]);
    Assert(authoritativeHashMismatch.Status == PatchMatchStatus.HashMismatch &&
           authoritativeHashMismatch.Compatibility is
           {
               ReasonCode: PatchCompatibilityReasonCode.ExecutableHashMismatch,
               CanEnable: false,
               CanDisable: true
            },
        "Expected an authoritative main PPU mismatch to block activation while preserving safe removal of an old enabled entry.");

    var staleCacheOnly = compatibility.Evaluate(
        officialPatch,
        [exactGame with
        {
            PpuHash = otherHash,
            HashEvidenceKind = PatchHashEvidenceKind.GameCache
        }]);
    Assert(staleCacheOnly.Status ==
               PatchMatchStatus.SerialVersionMatchHashUnknown &&
           staleCacheOnly.Compatibility is
           {
               Verdict: PatchCompatibilityVerdict.RuntimeGuarded,
               CanEnable: true
           },
        "Expected a non-matching stale cache entry not to become false incompatibility; RPCS3 remains the runtime guard.");

    var spuPatch = CreatePatch(
        "Partial SPU evidence",
        PatchHashKind.Spu,
        exactHash,
        target,
        PatchSourceOrigin.BundledCanary);
    var partialSpu = compatibility.Evaluate(
        spuPatch,
        [exactGame with
        {
            PpuHash = null,
            SpuHash = otherHash
        }]);
    Assert(partialSpu.Status ==
               PatchMatchStatus.SerialVersionMatchHashUnknown &&
           partialSpu.Compatibility?.UsesRpcs3RuntimeHashGuard == true,
        "Expected a different observed SPU module not to reject an SPU that may load later.");

    var prxPatch = CreatePatch(
        "PRX exact",
        PatchHashKind.Prx,
        prxBase57 + "-0",
        target,
        PatchSourceOrigin.Official);
    var prxExact = compatibility.Evaluate(
        prxPatch,
        [exactGame with
        {
            PpuHash = null,
            PrxHashes = [exactHash + "-0"]
        }]);
    Assert(prxExact.Compatibility?.Verdict ==
               PatchCompatibilityVerdict.Compatible,
        "Expected PRX runtime evidence to produce an exact compatibility decision.");

    var ovlPatch = CreatePatch(
        "OVL exact",
        PatchHashKind.Ovl,
        exactHash,
        target,
        PatchSourceOrigin.Official);
    var ovlExact = compatibility.Evaluate(
        ovlPatch,
        [exactGame with
        {
            PpuHash = null,
            OvlHashes = [exactHash]
        }]);
    Assert(ovlExact.Compatibility?.Verdict ==
               PatchCompatibilityVerdict.Compatible,
        "Expected OVL runtime evidence to produce an exact compatibility decision.");

    var scopedToAnotherGame = officialPatch with
    {
        RuntimeKeySerial = "BLUS99999"
    };
    var scopeMismatch = compatibility.Evaluate(
        scopedToAnotherGame,
        [exactGame]);
    Assert(scopeMismatch.Compatibility is
           {
               ReasonCode: PatchCompatibilityReasonCode.RuntimeKeySerialMismatch,
               CanEnable: false
           },
        "Expected a TITLE_ID-prefixed runtime key for another game to be blocked.");

    var unknownSource = compatibility.Evaluate(
        officialPatch with { Source = PatchSourceOrigin.Unknown },
        [exactGame with { PpuHash = null }]);
    Assert(unknownSource.Compatibility is
           {
               ReasonCode: PatchCompatibilityReasonCode.UntrustedRuntimeGuard,
               CanEnable: false
           },
        "Expected an unknown source to require exact local hash evidence.");

    var globalPatch = officialPatch with
    {
        GameTargets =
        [
            new Rpcs3PatchGameTarget(
                "All",
                Rpcs3PatchVersion.All,
                [Rpcs3PatchVersion.All])
        ]
    };
    var globalWithoutHash = compatibility.Evaluate(
        globalPatch,
        [exactGame with { PpuHash = null }]);
    Assert(globalWithoutHash.Compatibility is
           {
               ReasonCode: PatchCompatibilityReasonCode.GlobalTargetNeedsExactHash,
               CanEnable: false
           },
        "Expected All/All definitions to require an exact local hash before showing for a selected game.");

    var layoutRoot = Path.Combine(tempRoot, "compat-layout");
    Directory.CreateDirectory(layoutRoot);
    var portableExe = Path.Combine(layoutRoot, "rpcs3.exe");
    File.WriteAllText(portableExe, string.Empty);
    var portableData = Path.Combine(layoutRoot, "portable");
    Directory.CreateDirectory(portableData);
    var portableInstall = Rpcs3InstallService.FromExePath(portableExe);
    Assert(portableInstall.DataRootPath == portableData &&
           portableInstall.ConfigPath == Path.Combine(portableData, "config") &&
           portableInstall.PatchesPath == Path.Combine(portableData, "patches"),
        "Expected R3Config to mirror RPCS3's portable data-root precedence.");

    var envLayoutRoot = Path.Combine(tempRoot, "compat-env-layout");
    Directory.CreateDirectory(envLayoutRoot);
    var envExe = Path.Combine(envLayoutRoot, "rpcs3.exe");
    File.WriteAllText(envExe, string.Empty);
    var configuredData = Path.Combine(tempRoot, "compat-configured-data");
    var previousConfigDir = Environment.GetEnvironmentVariable(
        "RPCS3_CONFIG_DIR");
    try
    {
        Environment.SetEnvironmentVariable(
            "RPCS3_CONFIG_DIR",
            configuredData);
        var configuredInstall = Rpcs3InstallService.FromExePath(envExe);
        Assert(configuredInstall.DataRootPath == configuredData &&
               configuredInstall.PatchConfigPath == Path.Combine(
                   configuredData,
                   "config",
                   "patch_config.yml"),
            "Expected R3Config to mirror RPCS3_CONFIG_DIR when portable mode is absent.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(
            "RPCS3_CONFIG_DIR",
            previousConfigDir);
    }

    var cacheProofRoot = Path.Combine(
        tempRoot,
        "compat-cache-proof");
    var cacheProofGamePath = Path.Combine(
        cacheProofRoot,
        "games",
        "Compatibility Game");
    var cacheProofEboot = Path.Combine(
        cacheProofGamePath,
        "PS3_GAME",
        "USRDIR",
        "EBOOT.BIN");
    Directory.CreateDirectory(Path.GetDirectoryName(cacheProofEboot)!);
    File.WriteAllText(cacheProofEboot, "test executable identity");
    File.SetLastWriteTimeUtc(
        cacheProofEboot,
        DateTime.UtcNow.AddMinutes(-10));
    var cacheProofExe = Path.Combine(
        cacheProofRoot,
        "rpcs3.exe");
    File.WriteAllText(cacheProofExe, string.Empty);
    var cacheProofEntry = Path.Combine(
        cacheProofRoot,
        "cache",
        serial,
        $"ppu-{prxBase57}-EBOOT.BIN");
    Directory.CreateDirectory(cacheProofEntry);
    Directory.SetLastWriteTimeUtc(
        cacheProofEntry,
        DateTime.UtcNow.AddMinutes(-1));
    var olderCacheProofEntry = Path.Combine(
        cacheProofRoot,
        "cache",
        serial,
        $"ppu-{olderCacheBase57}-EBOOT.BIN");
    Directory.CreateDirectory(olderCacheProofEntry);
    Directory.SetLastWriteTimeUtc(
        olderCacheProofEntry,
        DateTime.UtcNow.AddMinutes(-5));
    var cacheProofInstall = Rpcs3InstallService.FromExePath(
        cacheProofExe);
    var cacheProofIdentity = new PatchHashProbeService().Enrich(
        cacheProofInstall,
        exactGame with
        {
            InstallPath = cacheProofGamePath,
            PpuHash = null,
            HashEvidenceKind = PatchHashEvidenceKind.None
        });
    Assert(cacheProofIdentity.AllAuthoritativePpuHashes.Count == 1 &&
           cacheProofIdentity.AllAuthoritativePpuHashes.Contains(
               exactHash) &&
           cacheProofIdentity.AllPpuHashes.Contains(olderCacheHash),
        "Expected only the newest RPCS3 EBOOT cache entry to become authoritative while older valid entries remain historical evidence.");
    var currentCacheMismatch = compatibility.Evaluate(
        CreatePatch(
            "Different current PPU",
            PatchHashKind.Ppu,
            otherHash,
            target,
            PatchSourceOrigin.Official),
        [cacheProofIdentity]);
    Assert(currentCacheMismatch.Status == PatchMatchStatus.HashMismatch &&
           currentCacheMismatch.Compatibility?.CanEnable == false,
        "Expected a verified current EBOOT cache hash to block a different PPU patch key.");

    var moduleExecutable = Path.Combine(
        cacheProofGamePath,
        "PS3_GAME",
        "USRDIR",
        "GOWA.SELF");
    File.WriteAllText(moduleExecutable, "secondary executable identity");
    File.SetLastWriteTimeUtc(
        moduleExecutable,
        DateTime.UtcNow.AddMinutes(-10));
    var moduleCacheProofEntry = Path.Combine(
        cacheProofRoot,
        "cache",
        serial,
        $"ppu-{otherHash}-GOWA.SELF");
    Directory.CreateDirectory(moduleCacheProofEntry);
    Directory.SetLastWriteTimeUtc(
        moduleCacheProofEntry,
        DateTime.UtcNow.AddMinutes(-1));
    var multiModuleIdentity = new PatchHashProbeService().Enrich(
        cacheProofInstall,
        exactGame with
        {
            InstallPath = cacheProofGamePath,
            PpuHash = null,
            HashEvidenceKind = PatchHashEvidenceKind.None
        });
    Assert(multiModuleIdentity.AllAuthoritativePpuHashes.Contains(exactHash) &&
           multiModuleIdentity.AllAuthoritativePpuHashes.Contains(otherHash),
        "Expected bounded cache validation to compare each PPU cache entry with its matching installed module instead of treating every PPU hash as EBOOT.BIN.");
    Directory.Delete(moduleCacheProofEntry, recursive: true);
    File.Delete(moduleExecutable);

    Directory.SetLastWriteTimeUtc(
        cacheProofEntry,
        DateTime.UtcNow.AddMinutes(-20));
    File.SetLastWriteTimeUtc(
        cacheProofEboot,
        DateTime.UtcNow.AddMinutes(-1));
    var staleCacheIdentity = new PatchHashProbeService().Enrich(
        cacheProofInstall,
        exactGame with
        {
            InstallPath = cacheProofGamePath,
            PpuHash = null,
            HashEvidenceKind = PatchHashEvidenceKind.None
        });
    Assert(staleCacheIdentity.AllAuthoritativePpuHashes.Count == 0,
        "Expected a cache older than EBOOT.BIN to remain positive-only historical evidence.");

    var probeRoot = Path.Combine(tempRoot, "compat-probe");
    Directory.CreateDirectory(Path.Combine(probeRoot, "log"));
    var probeExe = Path.Combine(probeRoot, "rpcs3.exe");
    File.WriteAllText(probeExe, string.Empty);
    File.WriteAllText(
        Path.Combine(probeRoot, "log", "RPCS3.log"),
        $"SYS: Serial: {serial}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{exactHash}{Environment.NewLine}" +
        $"sys_spu: Loaded SPU image: SPU-{otherHash} (<- 0){Environment.NewLine}" +
        $"ppu_loader: PRX hash of module[0]: PRX-{prxBase57}-0{Environment.NewLine}" +
        $"ppu_loader: OVL hash of overlay: OVL-{otherHash}{Environment.NewLine}");
    var probeInstall = Rpcs3InstallService.FromExePath(probeExe);
    var probedIdentity = new PatchHashProbeService().Enrich(
        probeInstall,
        exactGame with
        {
            PpuHash = null,
            SpuHash = null,
            HashEvidenceKind = PatchHashEvidenceKind.None,
            PrxHashes = null,
            OvlHashes = null
        });
    Assert(probedIdentity.AllPpuHashes.Contains(exactHash) &&
           probedIdentity.AllSpuHashes.Contains(otherHash) &&
           probedIdentity.AllPrxHashes.Contains(exactHash + "-0") &&
           probedIdentity.AllOvlHashes.Contains(otherHash),
        "Expected the RPCS3 log probe to collect PPU, SPU, PRX, and OVL runtime identity evidence from one serial-scoped boot session.");

    Console.WriteLine("Unified patch compatibility architecture PASS.");

    static Rpcs3PatchEntry CreatePatch(
        string title,
        PatchHashKind kind,
        string hash,
        Rpcs3PatchGameTarget target,
        PatchSourceOrigin source)
    {
        return new Rpcs3PatchEntry(
            Title: title,
            HashKind: kind,
            Hash: hash,
            GameTargets: [target],
            PatchType: "Compatibility",
            Risk: PatchRisk.Unknown,
            Notes: null,
            Author: "Test",
            PatchVersion: "1.0",
            Group: null,
            Source: source);
    }
}

static void RunBundledCheatCompilerTests(string tempRoot)
{
    const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    var sourceText =
        "Version: 1.2\n" +
        "Anchors:\n" +
        "  OldCode: &OldCode\n" +
        "    - [ be32, 0x00100000, 0x11111111 ]\n" +
        "  NewCode: &NewCode\n" +
        "    - [ be32, 0x00100000, 0x60000000 ]\n" +
        $"PPU-{hash}:\n" +
        "  \"Infinite Health\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Author: Old\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ load, *OldCode ]\n" +
        "  \"Infinite Health\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Author: New\n" +
        "    Patch Version: 1.1\n" +
        "    Patch:\n" +
        "      - [ load, *NewCode ]\n" +
        "  \"Max Orbs\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100004, 0x000F423F ]\n";

    var reader = new PatchCatalogReader();
    var read = reader.ReadText(sourceText, PatchSourceOrigin.BundledCheats, "cheats.yml");
    Assert(read.Succeeded, "Expected duplicate community cheat titles to be readable: " + read.Error);
    var merged = new PatchCatalogMergeService().Merge(read.Catalog);
    Assert(merged.Entries.Count == 2, "Expected duplicate cheat versions to merge deterministically.");
    Assert(merged.Entries.All(entry => entry.Risk == PatchRisk.Cheat),
        "Expected bundled community entries to be forced into explicit Cheat handling.");
    Assert(merged.Entries.Single(entry => entry.Title == "Infinite Health").PatchVersion == "1.1",
        "Expected the newer duplicate cheat patch version to win.");

    var game = new Rpcs3GameDescriptor(
        "BLUS00001",
        "Compiler Game",
        @"C:\Games\BLUS00001",
        "01.01",
        hash,
        null);
    var matches = merged.Entries
        .Select(entry => new PatchMatchService().Match(entry, [game]))
        .ToArray();
    Assert(matches.All(match => match.Status == PatchMatchStatus.ExactMatch),
        "Expected compiler smoke selections to require exact serial/version/hash evidence.");

    var compiler = new PatchSelectionCompiler();
    var compiled = compiler.Compile(
        sourceText,
        [matches.Single(match => match.Patch.Title == "Infinite Health")]);
    Assert(compiled.Succeeded, "Expected an anchored selected cheat to compile: " + compiled.Error);
    Assert(compiled.PatchText.StartsWith("Version: 1.2", StringComparison.Ordinal),
        "Expected compiled cheat text to start with the RPCS3 version header.");
    Assert(!compiled.PatchText.Contains("*NewCode", StringComparison.Ordinal) &&
           !compiled.PatchText.Contains("Anchors:", StringComparison.Ordinal),
        "Expected compiler output to expand aliases and have no external anchor dependency.");
    Assert(compiled.PatchText.Contains("0x60000000", StringComparison.OrdinalIgnoreCase),
        "Expected the compiler to select the newer duplicate patch body.");

    var runtimeGuardedGame = game with
    {
        PpuHash = null,
        PpuHashes = []
    };
    var runtimeGuardedMatch = new PatchMatchService().Match(
        merged.Entries.Single(entry => entry.Title == "Infinite Health"),
        [runtimeGuardedGame]);
    Assert(runtimeGuardedMatch.Status == PatchMatchStatus.SerialVersionMatchHashUnknown &&
           PatchActivationPolicy.IsRuntimeGuardedMatch(runtimeGuardedMatch),
        "Expected a trusted cheat with exact serial/version and no local Hash to use RPCS3's runtime Hash guard.");
    var runtimeGuardedCompile = compiler.Compile(
        sourceText,
        [runtimeGuardedMatch]);
    Assert(runtimeGuardedCompile.Succeeded &&
           runtimeGuardedCompile.SelectedCount == 1,
        "Expected a runtime-guarded bundled cheat to compile into its exact executable-hash block.");

    var conflictSourceText =
        "Version: 1.2\n" +
        $"PPU-{hash}:\n" +
        "  \"Conflicting Cheat\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100008, 0x11111111 ]\n" +
        "  \"Conflicting Cheat\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Patch Version: 1.0.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100008, 0x22222222 ]\n";
    var conflictRead = reader.ReadText(
        conflictSourceText,
        PatchSourceOrigin.BundledCheats,
        "cheats.yml");
    Assert(conflictRead.Succeeded,
        "Expected same-version conflict source to parse before merge validation: " + conflictRead.Error);
    var conflictMerge = new PatchCatalogMergeService().Merge(conflictRead.Catalog);
    Assert(conflictMerge.Entries.Count == 0,
        "Expected every same-version conflicting duplicate definition to be rejected.");
    Assert(conflictMerge.MergeDiagnostics.Any(diagnostic =>
            diagnostic.Code == "PATCH_DUPLICATE_CONFLICT"),
        "Expected same-version conflicting duplicates to produce an explicit conflict diagnostic.");

    var integritySourceText =
        "Version: 1.2\n" +
        $"PPU-{hash}:\n" +
        "  \"Enable Codes\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100010, 0x60000000 ]\n" +
        "  \"Dependent Health\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Dependencies: [ \"Enable Codes\" ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100014, 0x60000000 ]\n" +
        "  \"Choice A\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Group: Movement\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100018, 0x60000000 ]\n" +
        "  \"Choice B\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Group: Movement\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x0010001C, 0x60000000 ]\n" +
        "  \"Cycle A\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Dependencies: [ \"Cycle B\" ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100020, 0x60000000 ]\n" +
        "  \"Cycle B\":\n" +
        "    Games:\n" +
        "      \"Compiler Game\":\n" +
        "        BLUS00001: [ 01.01 ]\n" +
        "    Dependencies: [ \"Cycle A\" ]\n" +
        "    Patch Version: 1.0\n" +
        "    Patch:\n" +
        "      - [ be32, 0x00100024, 0x60000000 ]\n";
    var integrityRead = reader.ReadText(
        integritySourceText,
        PatchSourceOrigin.BundledCheats,
        "cheats.yml");
    Assert(integrityRead.Succeeded,
        "Expected dependency/group integrity source to parse: " + integrityRead.Error);
    var integrityMatches = integrityRead.Catalog.Entries
        .Select(entry => new PatchMatchService().Match(entry, [game]))
        .ToArray();
    var dependencyOnly = compiler.Compile(
        integritySourceText,
        [integrityMatches.Single(match => match.Patch.Title == "Dependent Health")]);
    Assert(!dependencyOnly.Succeeded &&
           dependencyOnly.Error?.Contains("Enable Codes", StringComparison.Ordinal) == true,
        "Expected a dependent cheat to be rejected when its required patch is not selected.");
    var dependencyPair = compiler.Compile(
        integritySourceText,
        [
            integrityMatches.Single(match => match.Patch.Title == "Enable Codes"),
            integrityMatches.Single(match => match.Patch.Title == "Dependent Health")
        ]);
    Assert(dependencyPair.Succeeded && dependencyPair.SelectedCount == 2,
        "Expected an explicit dependency pair to compile together: " + dependencyPair.Error);
    var groupConflict = compiler.Compile(
        integritySourceText,
        [
            integrityMatches.Single(match => match.Patch.Title == "Choice A"),
            integrityMatches.Single(match => match.Patch.Title == "Choice B")
        ]);
    Assert(!groupConflict.Succeeded &&
           groupConflict.Error?.Contains("mutually exclusive", StringComparison.OrdinalIgnoreCase) == true,
        "Expected same-hash selections in one Group to be rejected as mutually exclusive.");
    var dependencyCycle = compiler.Compile(
        integritySourceText,
        [
            integrityMatches.Single(match => match.Patch.Title == "Cycle A"),
            integrityMatches.Single(match => match.Patch.Title == "Cycle B")
        ]);
    Assert(!dependencyCycle.Succeeded &&
           dependencyCycle.Error?.Contains("cycle", StringComparison.OrdinalIgnoreCase) == true,
        "Expected cyclic cheat dependencies to be rejected.");

    var libraryRoot = Path.Combine(tempRoot, "cheat-library-smoke");
    var dataPath = Path.Combine(libraryRoot, "Data");
    var patchesPath = Path.Combine(libraryRoot, "rpcs3", "patches");
    Directory.CreateDirectory(dataPath);
    Directory.CreateDirectory(patchesPath);
    File.WriteAllText(Path.Combine(dataPath, BundledCheatPatchService.BundledFileName), sourceText);

    var service = new BundledCheatPatchService();
    var managedPath = Path.Combine(
        patchesPath,
        "BLUS00001_patch.yml");
    Assert(
        managedPath.Equals(
            BundledCheatPatchService.GetManagedPath(
                patchesPath,
                "BLUS00001"),
            StringComparison.OrdinalIgnoreCase),
        "Expected managed cheats to use RPCS3's runtime-loaded <TITLE_ID>_patch.yml filename.");
    var stagedFirst = service.PrepareManagedPatchFile(
        patchesPath,
        libraryRoot,
        [matches.Single(match => match.Patch.Title == "Infinite Health")]);
    Assert(stagedFirst.Succeeded &&
           stagedFirst.PreparedFile is not null &&
           !File.Exists(managedPath),
        "Expected Cheat preparation to produce validated staged content without touching the target.");

    var first = service.PrepareManagedPatch(
        patchesPath,
        libraryRoot,
        [matches.Single(match => match.Patch.Title == "Infinite Health")]);
    Assert(first.Succeeded, "Expected first exact cheat preparation to succeed: " + first.Error);
    Assert(first.BackupPath is null, "Expected first managed cheat creation not to need a backup.");
    Assert(!File.Exists(Path.Combine(
            patchesPath,
            BundledCheatPatchService.ManagedFileName)),
        "Expected Cheat preparation not to create the legacy filename that RPCS3 ignores at boot.");

    var second = service.PrepareManagedPatch(
        patchesPath,
        libraryRoot,
        [matches.Single(match => match.Patch.Title == "Max Orbs")]);
    Assert(second.Succeeded, "Expected second exact cheat preparation to succeed: " + second.Error);
    Assert(second.BackupPath is not null && File.Exists(second.BackupPath),
        "Expected an existing managed cheat file to be backed up before extension.");

    var managedRead = reader.Read(managedPath, PatchSourceOrigin.ManagedCheats);
    Assert(managedRead.Succeeded, "Expected managed cheat file to read back: " + managedRead.Error);
    Assert(managedRead.Catalog.Entries.Count == 2,
        "Expected adding a second cheat to preserve the first managed definition.");

    Console.WriteLine("Bundled cheat selection compiler smoke PASS.");
}

static void RunPatchTransactionTests(string tempRoot)
{
    const string hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    var root = Path.Combine(tempRoot, "patch-transaction-smoke");
    var patchesPath = Path.Combine(root, "patches");
    var configPath = Path.Combine(root, "config");
    var backupRoot = Path.Combine(configPath, "R3ConfigBackups");
    Directory.CreateDirectory(patchesPath);
    Directory.CreateDirectory(configPath);

    var importedPath = Path.Combine(
        patchesPath,
        CanaryPatchImportService.ImportedPatchFileName);
    var cheatPath = Path.Combine(
        patchesPath,
        BundledCheatPatchService.ManagedFileName);
    var patchConfigPath = Path.Combine(
        configPath,
        "patch_config.yml");

    const string originalImported =
        "Version: 1.2\r\n" +
        "PPU-1111111111111111111111111111111111111111:\r\n" +
        "  \"Original Canary\":\r\n" +
        "    Games:\r\n" +
        "      \"Original Game\":\r\n" +
        "        BLUS10000: [ 01.00 ]\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        "      - [ be32, 0x00000000, 0x60000000 ]\r\n";
    const string originalCheat =
        "Version: 1.2\r\n" +
        "PPU-2222222222222222222222222222222222222222:\r\n" +
        "  \"Original Cheat\":\r\n" +
        "    Games:\r\n" +
        "      \"Original Game\":\r\n" +
        "        BLUS10000: [ 01.00 ]\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        "      - [ be32, 0x00000004, 0x60000000 ]\r\n";
    const string originalPatchConfig =
        "PPU-existing:\r\n" +
        "  Existing Patch:\r\n" +
        "    Existing Game:\r\n" +
        "      BLUS10000:\r\n" +
        "        01.00:\r\n" +
        "          Enabled: true\r\n";

    File.WriteAllText(importedPath, originalImported, Encoding.UTF8);
    File.WriteAllText(cheatPath, originalCheat, Encoding.UTF8);
    File.WriteAllText(patchConfigPath, originalPatchConfig, Encoding.UTF8);

    var canaryText = BuildTransactionPatchCatalog(
        hash,
        "Transaction Canary",
        "Transaction Game",
        "BLUS20000",
        "01.00",
        "0x00001000");
    var cheatText = BuildTransactionPatchCatalog(
        hash,
        "Transaction Cheat",
        "Transaction Game",
        "BLUS20000",
        "01.00",
        "0x00001004");

    var canaryPrepared = new PatchPreparedFile(
        importedPath,
        canaryText,
        PatchPreparedFileKind.PatchCatalog,
        PatchSourceOrigin.Imported,
        ExpectedEntryCount: 1);
    var cheatPrepared = new PatchPreparedFile(
        cheatPath,
        cheatText,
        PatchPreparedFileKind.PatchCatalog,
        PatchSourceOrigin.ManagedCheats,
        ExpectedEntryCount: 1);
    var change = new PatchApplyChange(
        "PPU-" + hash,
        "Transaction Canary",
        "Transaction Game",
        "BLUS20000",
        "01.00",
        PatchRisk.Canary);
    var plan = new PatchApplyPlan(
        patchConfigPath,
        [change]);

    var service = new PatchApplyService();
    var success = service.ApplyTransaction(
        plan,
        backupRoot,
        [canaryPrepared, cheatPrepared]);

    Assert(success.Backup.Files.Count == 3,
        "Expected one transaction backup manifest to include Canary, Cheat, and patch_config.yml.");
    Assert(success.Backup.Files.Select(file => file.TargetPath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals([importedPath, cheatPath, patchConfigPath]),
        "Expected the transaction backup to cover every affected target path.");
    Assert(File.ReadAllText(importedPath, Encoding.UTF8) == canaryText,
        "Expected staged Canary content to commit.");
    Assert(File.ReadAllText(cheatPath, Encoding.UTF8) == cheatText,
        "Expected staged Cheat content to commit.");
    Assert(new PatchConfigReader().IsEnabled(patchConfigPath, change),
        "Expected patch_config.yml to commit in the same transaction.");

    var enabledImportedText = File.ReadAllText(importedPath, Encoding.UTF8);
    var enabledCheatText = File.ReadAllText(cheatPath, Encoding.UTF8);
    var disableResult = service.Disable(
        plan,
        backupRoot);
    Assert(!new PatchConfigReader().IsEnabled(patchConfigPath, change),
        "Expected disabling to remove only the enabled state from patch_config.yml.");
    Assert(File.ReadAllText(importedPath, Encoding.UTF8) == enabledImportedText &&
           File.ReadAllText(cheatPath, Encoding.UTF8) == enabledCheatText,
        "Expected disabling Canary or Artemis state not to delete or rewrite their resident definitions.");
    new BackupService().Restore(disableResult.Backup);
    Assert(new PatchConfigReader().IsEnabled(patchConfigPath, change),
        "Expected restoring the disable backup to recover the previous enabled state.");

    new BackupService().Restore(success.Backup);
    Assert(File.ReadAllText(importedPath, Encoding.UTF8) == originalImported,
        "Expected transaction backup restore to recover imported_patch.yml.");
    Assert(File.ReadAllText(cheatPath, Encoding.UTF8) == originalCheat,
        "Expected transaction backup restore to recover the managed Cheat file.");
    Assert(File.ReadAllText(patchConfigPath, Encoding.UTF8) == originalPatchConfig,
        "Expected transaction backup restore to recover patch_config.yml.");

    var backupCountBeforeInvalid = Directory.Exists(backupRoot)
        ? Directory.GetDirectories(backupRoot).Length
        : 0;
    var invalidStagePath = Path.Combine(
        patchesPath,
        "invalid_stage_patch.yml");
    var invalidPrepared = new PatchPreparedFile(
        invalidStagePath,
        "Version: [broken",
        PatchPreparedFileKind.PatchCatalog,
        PatchSourceOrigin.Imported);

    try
    {
        service.ApplyTransaction(
            plan,
            backupRoot,
            [canaryPrepared, invalidPrepared]);
        throw new InvalidOperationException(
            "Expected invalid staged YAML to fail before backup and commit.");
    }
    catch (InvalidDataException)
    {
        Assert(File.ReadAllText(importedPath, Encoding.UTF8) == originalImported &&
               File.ReadAllText(cheatPath, Encoding.UTF8) == originalCheat &&
               File.ReadAllText(patchConfigPath, Encoding.UTF8) == originalPatchConfig &&
               !File.Exists(invalidStagePath),
            "Expected pre-commit validation failure to leave all targets unchanged.");

        var backupCountAfterInvalid = Directory.Exists(backupRoot)
            ? Directory.GetDirectories(backupRoot).Length
            : 0;
        Assert(backupCountAfterInvalid == backupCountBeforeInvalid,
            "Expected all staged files to validate before creating the transaction backup.");
    }

    var commitCount = 0;
    var failingTransaction = new PatchFileTransactionService(
        commitFile: (stagedPath, targetPath) =>
        {
            commitCount++;
            if (commitCount == 2)
            {
                throw new IOException(
                    "Simulated second-file transaction failure.");
            }

            if (File.Exists(targetPath))
            {
                File.Replace(
                    stagedPath,
                    targetPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(stagedPath, targetPath);
            }
        });
    var failingService = new PatchApplyService(
        transaction: failingTransaction);

    try
    {
        failingService.ApplyTransaction(
            plan,
            backupRoot,
            [canaryPrepared, cheatPrepared]);
        throw new InvalidOperationException(
            "Expected multi-file commit failure to be surfaced.");
    }
    catch (IOException)
    {
        Assert(File.ReadAllText(importedPath, Encoding.UTF8) == originalImported,
            "Expected a failed second commit to roll back the first committed file.");
        Assert(File.ReadAllText(cheatPath, Encoding.UTF8) == originalCheat,
            "Expected a failed transaction to preserve the second target.");
        Assert(File.ReadAllText(patchConfigPath, Encoding.UTF8) == originalPatchConfig,
            "Expected a failed transaction to preserve patch_config.yml.");
    }

    Assert(!Directory.EnumerateFiles(
            patchesPath,
            "*.r3stage",
            SearchOption.TopDirectoryOnly).Any() &&
           !Directory.EnumerateFiles(
            configPath,
            "*.r3stage",
            SearchOption.TopDirectoryOnly).Any(),
        "Expected transaction staging files to be cleaned after success and rollback.");

    Console.WriteLine("Multi-file patch transaction smoke PASS.");
}

static void RunCanaryImportTransactionTests(string tempRoot)
{
    const string oldHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string newHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string manualHash = "cccccccccccccccccccccccccccccccccccccccc";

    var root = Path.Combine(tempRoot, "canary-import-transaction");
    var configPath = Path.Combine(root, "config");
    var patchesPath = Path.Combine(root, "patches");
    var exePath = Path.Combine(root, "rpcs3.exe");
    Directory.CreateDirectory(configPath);
    Directory.CreateDirectory(patchesPath);
    File.WriteAllBytes(exePath, []);

    var install = Rpcs3InstallService.FromExePath(exePath);
    var importedPath = Rpcs3AppPaths.ImportedPatch(install);
    var officialPath = Path.Combine(patchesPath, "patch.yml");
    var officialBytes = Encoding.UTF8.GetBytes(
        "Version: 1.2\r\n# RPCS3-owned test catalog\r\n");
    File.WriteAllBytes(officialPath, officialBytes);

    var oldManaged = BuildCanaryImportPatch(
        oldHash,
        "Old Managed Canary",
        "0x00000100");
    var newManaged = BuildCanaryImportPatch(
        newHash,
        "New Managed Canary",
        "0x00000200");
    var manual = BuildCanaryImportPatch(
        manualHash,
        "Manual Existing Patch",
        "0x00000300");

    var normalizeOnlyRoot = Path.Combine(root, "normalize-read-only");
    var normalize = new CanaryPatchImportService().Normalize(newManaged);
    Assert(normalize.Succeeded && !Directory.Exists(normalizeOnlyRoot),
        "Expected Canary normalization to remain deterministic and free of filesystem side effects.");

    var reader = new PatchCatalogReader();
    var backup = new BackupService();
    var transaction = new PatchFileTransactionService(
        backup,
        reader,
        new PatchConfigReader());
    var useCase = new CanaryImportUseCase(
        new CanaryPatchImportService(reader),
        new Rpcs3MutationGuard(() => false),
        transaction,
        backup,
        reader);

    var missingResult = useCase.Import(install, oldManaged);
    Assert(missingResult.Succeeded && File.Exists(importedPath),
        "Expected a missing imported_patch.yml to be created transactionally: " + missingResult.Error);
    var missingText = File.ReadAllText(importedPath, Encoding.UTF8);
    Assert(missingText.StartsWith(
               CanaryPatchImportService.RequiredVersionHeader,
               StringComparison.Ordinal),
        "Expected a newly imported Canary catalog to start with Version: 1.2.");
    var missingSnapshots = backup.ListBackups(
        Rpcs3AppPaths.BackupRoot(install));
    Assert(missingSnapshots.Count == 1 &&
           missingSnapshots[0].Files.Count == 1 &&
           !missingSnapshots[0].Files[0].Existed &&
           string.Equals(
               missingResult.BackupPath,
               missingSnapshots[0].DirectoryPath,
               StringComparison.OrdinalIgnoreCase),
        "Expected the first import to create a central snapshot recording Existed=false.");
    Assert(File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
        "Expected manual Canary import never to modify RPCS3-owned patch.yml.");

    File.WriteAllText(importedPath, manual, Encoding.UTF8);
    var preservingResult = useCase.Import(install, oldManaged);
    Assert(preservingResult.Succeeded,
        "Expected a valid manual imported patch to be preserved: " + preservingResult.Error);
    var preservingText = File.ReadAllText(importedPath, Encoding.UTF8);
    Assert(preservingText.Contains("Manual Existing Patch", StringComparison.Ordinal) &&
           preservingText.Contains("Old Managed Canary", StringComparison.Ordinal) &&
           new PatchCatalogReader().Read(
               importedPath,
               PatchSourceOrigin.Imported).Succeeded,
        "Expected manual and managed definitions to coexist in one readable imported_patch.yml.");

    var replacingResult = useCase.Import(install, newManaged);
    Assert(replacingResult.Succeeded,
        "Expected a previous R3Config-managed block to be replaced: " + replacingResult.Error);
    var replacingText = File.ReadAllText(importedPath, Encoding.UTF8);
    Assert(replacingText.Contains("Manual Existing Patch", StringComparison.Ordinal) &&
           replacingText.Contains("New Managed Canary", StringComparison.Ordinal) &&
           !replacingText.Contains("Old Managed Canary", StringComparison.Ordinal) &&
           CountOccurrences(
               replacingText,
               "R3Config managed Canary patches - begin") == 1,
        "Expected replacement to keep manual content and emit exactly one managed marker.");

    var snapshotsBeforeReplay = backup.ListBackups(
        Rpcs3AppPaths.BackupRoot(install)).Count;
    var bytesBeforeReplay = File.ReadAllBytes(importedPath);
    var replayResult = useCase.Import(install, newManaged);
    Assert(replayResult.Succeeded &&
           replayResult.BackupPath is null &&
           File.ReadAllBytes(importedPath).SequenceEqual(bytesBeforeReplay) &&
           backup.ListBackups(Rpcs3AppPaths.BackupRoot(install)).Count == snapshotsBeforeReplay,
        "Expected identical Canary import replay to be byte-idempotent without a new backup.");

    File.WriteAllText(importedPath, "Version: [broken", Encoding.UTF8);
    var invalidBefore = File.ReadAllBytes(importedPath);
    var snapshotsBeforeInvalid = backup.ListBackups(
        Rpcs3AppPaths.BackupRoot(install)).Count;
    var invalidResult = useCase.Import(install, newManaged);
    Assert(!invalidResult.Succeeded &&
           File.ReadAllBytes(importedPath).SequenceEqual(invalidBefore) &&
           backup.ListBackups(Rpcs3AppPaths.BackupRoot(install)).Count == snapshotsBeforeInvalid &&
           File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
        "Expected malformed existing YAML to block import with zero target or backup changes.");

    var conflictingManual = BuildCanaryImportPatch(
        newHash,
        "New Managed Canary",
        "0x00009999");
    File.WriteAllText(importedPath, conflictingManual, Encoding.UTF8);
    var conflictBefore = File.ReadAllBytes(importedPath);
    var conflictResult = useCase.Import(install, newManaged);
    Assert(!conflictResult.Succeeded &&
           File.ReadAllBytes(importedPath).SequenceEqual(conflictBefore),
        "Expected a manual/managed patch identity conflict to fail without silently choosing a definition.");

    File.WriteAllText(importedPath, manual, Encoding.UTF8);
    var stageBefore = File.ReadAllBytes(importedPath);
    var snapshotsBeforeStageFailure = backup.ListBackups(
        Rpcs3AppPaths.BackupRoot(install)).Count;
    try
    {
        transaction.Commit(
            Rpcs3AppPaths.BackupRoot(install),
            [
                new PatchPreparedFile(
                    importedPath,
                    "Version: [broken",
                    PatchPreparedFileKind.PatchCatalog,
                    PatchSourceOrigin.Imported)
            ]);
        throw new InvalidOperationException(
            "Expected invalid staged Canary YAML to fail validation.");
    }
    catch (InvalidDataException)
    {
        Assert(File.ReadAllBytes(importedPath).SequenceEqual(stageBefore) &&
               backup.ListBackups(Rpcs3AppPaths.BackupRoot(install)).Count == snapshotsBeforeStageFailure,
            "Expected stage validation failure before backup or target mutation.");
    }

    var commitFailureBackup = new BackupService();
    var commitFailureReader = new PatchCatalogReader();
    var commitFailureTransaction = new PatchFileTransactionService(
        commitFailureBackup,
        commitFailureReader,
        new PatchConfigReader(),
        commitFile: (stagedPath, targetPath) =>
        {
            File.Copy(stagedPath, targetPath, overwrite: true);
            throw new IOException("Simulated Canary commit failure.");
        });
    var commitFailureUseCase = new CanaryImportUseCase(
        new CanaryPatchImportService(commitFailureReader),
        new Rpcs3MutationGuard(() => false),
        commitFailureTransaction,
        commitFailureBackup,
        commitFailureReader);
    var commitBefore = File.ReadAllBytes(importedPath);
    var commitFailureResult = commitFailureUseCase.Import(
        install,
        newManaged);
    Assert(!commitFailureResult.Succeeded &&
           File.ReadAllBytes(importedPath).SequenceEqual(commitBefore),
        "Expected a commit failure after backup to roll imported_patch.yml back byte-for-byte.");

    var readBackFailureBackup = new BackupService();
    var readBackFailureReader = new PatchCatalogReader();
    var readBackFailureTransaction = new PatchFileTransactionService(
        readBackFailureBackup,
        readBackFailureReader,
        new PatchConfigReader(),
        commitFile: (stagedPath, targetPath) =>
        {
            File.Move(stagedPath, targetPath, overwrite: true);
            File.WriteAllText(targetPath, "Version: [broken", Encoding.UTF8);
        });
    var readBackFailureUseCase = new CanaryImportUseCase(
        new CanaryPatchImportService(readBackFailureReader),
        new Rpcs3MutationGuard(() => false),
        readBackFailureTransaction,
        readBackFailureBackup,
        readBackFailureReader);
    var readBackBefore = File.ReadAllBytes(importedPath);
    var readBackFailureResult = readBackFailureUseCase.Import(
        install,
        newManaged);
    Assert(!readBackFailureResult.Succeeded &&
           File.ReadAllBytes(importedPath).SequenceEqual(readBackBefore),
        "Expected post-commit read-back failure to trigger central-backup rollback.");

    File.Delete(importedPath);
    var snapshotsBeforeBlocked = backup.ListBackups(
        Rpcs3AppPaths.BackupRoot(install)).Count;
    var blockedReader = new PatchCatalogReader();
    var blockedBackup = new BackupService();
    var blockedUseCase = new CanaryImportUseCase(
        new CanaryPatchImportService(blockedReader),
        new Rpcs3MutationGuard(() => true),
        new PatchFileTransactionService(
            blockedBackup,
            blockedReader,
            new PatchConfigReader()),
        blockedBackup,
        blockedReader);
    AssertMutationBlocked(
        () => blockedUseCase.Import(install, newManaged),
        Rpcs3MutationBlockReason.Rpcs3Running,
        "Expected a running RPCS3 process to block Canary import before normalization or writes.");
    Assert(!File.Exists(importedPath) &&
           !Directory.EnumerateFiles(
               patchesPath,
               "*.r3stage",
               SearchOption.TopDirectoryOnly).Any() &&
           backup.ListBackups(Rpcs3AppPaths.BackupRoot(install)).Count == snapshotsBeforeBlocked &&
           File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
        "Expected blocked Canary import to create no target, stage, backup, or patch.yml change.");

    Console.WriteLine("Canary preserving transaction tests PASS.");
}

static string BuildCanaryImportPatch(
    string hash,
    string title,
    string address)
{
    return
        "Version: 1.2\r\n\r\n" +
        $"PPU-{hash}:\r\n" +
        $"  \"{title}\":\r\n" +
        "    Games:\r\n" +
        "      \"Canary Transaction Game\":\r\n" +
        "        BLUS31465: [ 01.02 ]\r\n" +
        "    Author: \"R3Config test\"\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        $"      - [ be32, {address}, 0x60000000 ]\r\n";
}

static int CountOccurrences(
    string text,
    string value)
{
    var count = 0;
    var offset = 0;

    while ((offset = text.IndexOf(
               value,
               offset,
               StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += value.Length;
    }

    return count;
}

static void RunBackupRestoreTransactionTests(string tempRoot)
{
    var root = Path.Combine(tempRoot, "backup-restore-transaction");
    var configPath = Path.Combine(root, "config");
    var customConfigsPath = Path.Combine(configPath, "custom_configs");
    var patchesPath = Path.Combine(root, "patches");
    var exePath = Path.Combine(root, "rpcs3.exe");
    Directory.CreateDirectory(customConfigsPath);
    Directory.CreateDirectory(patchesPath);
    File.WriteAllBytes(exePath, []);

    var install = Rpcs3InstallService.FromExePath(exePath);
    var backupRoot = Rpcs3AppPaths.BackupRoot(install);
    var firstTarget = Path.Combine(
        customConfigsPath,
        "config_BLUS10001.yml");
    var secondTarget = Path.Combine(
        patchesPath,
        "imported_patch.yml");
    var missingTarget = Path.Combine(
        configPath,
        "patch_config.yml");

    File.WriteAllBytes(firstTarget, Encoding.UTF8.GetBytes("backup-first"));
    File.WriteAllBytes(secondTarget, Encoding.UTF8.GetBytes("backup-second"));
    var backupService = new BackupService();
    var snapshot = CreateBackupSnapshot(
        backupService,
        backupRoot,
        [firstTarget, secondTarget, missingTarget]);

    Assert(snapshot.Files.Count == 3 &&
           snapshot.Files.Where(file => file.Existed).All(file =>
               file.Length is > 0 &&
               file.Sha256 is { Length: 64 }) &&
           snapshot.Files.Single(file => !file.Existed) is
           { Length: null, Sha256: null },
        "Expected new backup manifests to record length and SHA-256 for every existing file.");

    File.WriteAllBytes(firstTarget, Encoding.UTF8.GetBytes("current-first"));
    File.WriteAllBytes(secondTarget, Encoding.UTF8.GetBytes("current-second"));
    File.WriteAllBytes(missingTarget, Encoding.UTF8.GetBytes("created-after-backup"));
    var restoreUseCase = new BackupUseCase(
        backupService,
        new Rpcs3MutationGuard(() => false));
    restoreUseCase.Restore(install, snapshot);

    Assert(File.ReadAllText(firstTarget, Encoding.UTF8) == "backup-first" &&
           File.ReadAllText(secondTarget, Encoding.UTF8) == "backup-second" &&
           !File.Exists(missingTarget),
        "Expected one transactional restore to recover multiple files and remove a target that was originally absent.");

    var replayFirst = File.ReadAllBytes(firstTarget);
    var replaySecond = File.ReadAllBytes(secondTarget);
    restoreUseCase.Restore(install, snapshot);
    Assert(File.ReadAllBytes(firstTarget).SequenceEqual(replayFirst) &&
           File.ReadAllBytes(secondTarget).SequenceEqual(replaySecond) &&
           !File.Exists(missingTarget),
        "Expected restoring the same snapshot again to be idempotent.");

    File.WriteAllText(firstTarget, "before-first-commit-failure", Encoding.UTF8);
    File.WriteAllText(secondTarget, "before-first-commit-failure-2", Encoding.UTF8);
    var firstFailureBefore = new[]
    {
        File.ReadAllBytes(firstTarget),
        File.ReadAllBytes(secondTarget)
    };
    var firstCommitFailure = new BackupService(
        commitRestoreOperation: (_, _, _) =>
            throw new IOException("Simulated first restore commit failure."));
    try
    {
        new BackupUseCase(
            firstCommitFailure,
            new Rpcs3MutationGuard(() => false))
            .Restore(install, snapshot);
        throw new InvalidOperationException(
            "Expected the first restore commit to fail.");
    }
    catch (IOException ex) when (ex.Message.Contains(
        "first restore commit",
        StringComparison.Ordinal))
    {
        Assert(File.ReadAllBytes(firstTarget).SequenceEqual(firstFailureBefore[0]) &&
               File.ReadAllBytes(secondTarget).SequenceEqual(firstFailureBefore[1]),
            "Expected first-commit failure to preserve every current target byte-for-byte.");
    }

    File.WriteAllText(firstTarget, "before-middle-failure", Encoding.UTF8);
    File.WriteAllText(secondTarget, "before-middle-failure-2", Encoding.UTF8);
    var middleFailureBefore = new[]
    {
        File.ReadAllBytes(firstTarget),
        File.ReadAllBytes(secondTarget)
    };
    var commitIndex = 0;
    var middleCommitFailure = new BackupService(
        commitRestoreOperation: (stagePath, targetPath, shouldExist) =>
        {
            commitIndex++;
            if (commitIndex == 2)
            {
                throw new IOException(
                    "Simulated middle restore commit failure.");
            }

            ApplyRestoreOperationForTest(
                stagePath,
                targetPath,
                shouldExist);
        });
    try
    {
        new BackupUseCase(
            middleCommitFailure,
            new Rpcs3MutationGuard(() => false))
            .Restore(install, snapshot);
        throw new InvalidOperationException(
            "Expected the middle restore commit to fail.");
    }
    catch (IOException ex) when (ex.Message.Contains(
        "middle restore commit",
        StringComparison.Ordinal))
    {
        Assert(File.ReadAllBytes(firstTarget).SequenceEqual(middleFailureBefore[0]) &&
               File.ReadAllBytes(secondTarget).SequenceEqual(middleFailureBefore[1]),
            "Expected successful rollback to undo every target after a middle commit failure.");
    }

    var rollbackFailureCommitIndex = 0;
    var rollbackFailureService = new BackupService(
        commitRestoreOperation: (stagePath, targetPath, shouldExist) =>
        {
            rollbackFailureCommitIndex++;
            if (rollbackFailureCommitIndex == 2)
            {
                throw new IOException(
                    "Simulated restore commit error for aggregate test.");
            }

            ApplyRestoreOperationForTest(
                stagePath,
                targetPath,
                shouldExist);
        },
        rollbackRestoreOperation: (_, _, _) =>
            throw new UnauthorizedAccessException(
                "Simulated restore rollback error."));
    try
    {
        new BackupUseCase(
            rollbackFailureService,
            new Rpcs3MutationGuard(() => false))
            .Restore(install, snapshot);
        throw new InvalidOperationException(
            "Expected restore rollback failure to be surfaced.");
    }
    catch (AggregateException ex)
    {
        var messages = ex.Flatten().InnerExceptions
            .Select(error => error.Message)
            .ToArray();
        Assert(messages.Any(message => message.Contains(
                   "commit error",
                   StringComparison.Ordinal)) &&
               messages.Any(message => message.Contains(
                   "rollback error",
                   StringComparison.Ordinal)),
            "Expected aggregate restore failure to retain both commit and rollback errors.");
    }

    var validationTarget = Path.Combine(
        customConfigsPath,
        "config_BLUS10002.yml");
    File.WriteAllText(validationTarget, "validation-backup", Encoding.UTF8);

    var outsideSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [validationTarget]);
    var outsideBackupFile = Path.Combine(root, "outside-backup.bin");
    File.Copy(
        outsideSnapshot.Files.Single().BackupPath,
        outsideBackupFile,
        overwrite: true);
    var outsideManifest = new BackupManifest(
        outsideSnapshot.CreatedAt,
        [outsideSnapshot.Files.Single() with { BackupPath = outsideBackupFile }]);
    WriteBackupManifest(outsideSnapshot.DirectoryPath, outsideManifest);
    var outsideRequest = new BackupSnapshot(
        outsideSnapshot.DirectoryPath,
        outsideManifest.CreatedAt,
        outsideManifest.Files);
    var outsideBefore = File.ReadAllBytes(validationTarget);
    AssertThrows<InvalidDataException>(
        () => new BackupUseCase(
                new BackupService(),
                new Rpcs3MutationGuard(() => false))
            .Restore(install, outsideRequest),
        "Expected a BackupPath outside its snapshot to be rejected.");
    Assert(File.ReadAllBytes(validationTarget).SequenceEqual(outsideBefore),
        "Expected outside-snapshot validation failure before target mutation.");

    var duplicateSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [firstTarget, secondTarget]);
    var duplicateEntries = duplicateSnapshot.Files.ToArray();
    duplicateEntries[1] = duplicateEntries[1] with
    {
        BackupPath = duplicateEntries[0].BackupPath
    };
    var duplicateManifest = new BackupManifest(
        duplicateSnapshot.CreatedAt,
        duplicateEntries);
    WriteBackupManifest(
        duplicateSnapshot.DirectoryPath,
        duplicateManifest);
    AssertThrows<InvalidDataException>(
        () => new BackupService().Restore(new BackupSnapshot(
            duplicateSnapshot.DirectoryPath,
            duplicateManifest.CreatedAt,
            duplicateManifest.Files)),
        "Expected a duplicate BackupPath to be rejected before restore.");

    var badHashSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [validationTarget]);
    var badHashManifest = new BackupManifest(
        badHashSnapshot.CreatedAt,
        [badHashSnapshot.Files.Single() with { Sha256 = new string('0', 64) }]);
    WriteBackupManifest(
        badHashSnapshot.DirectoryPath,
        badHashManifest);
    var badHashBefore = File.ReadAllBytes(validationTarget);
    AssertThrows<InvalidDataException>(
        () => new BackupService().Restore(new BackupSnapshot(
            badHashSnapshot.DirectoryPath,
            badHashManifest.CreatedAt,
            badHashManifest.Files)),
        "Expected a mismatched backup SHA-256 to be rejected.");
    Assert(File.ReadAllBytes(validationTarget).SequenceEqual(badHashBefore) &&
           !HasRestoreStages(configPath, patchesPath),
        "Expected hash validation to fail before stages or target writes.");

    var missingBackupSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [validationTarget]);
    File.Delete(missingBackupSnapshot.Files.Single().BackupPath);
    AssertThrows<FileNotFoundException>(
        () => new BackupService().Restore(missingBackupSnapshot),
        "Expected a missing backup payload to be rejected before restore.");

    var legacySnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [validationTarget]);
    var legacyManifest = new BackupManifest(
        legacySnapshot.CreatedAt,
        [legacySnapshot.Files.Single() with { Length = null, Sha256 = null }]);
    WriteBackupManifest(
        legacySnapshot.DirectoryPath,
        legacyManifest);
    File.WriteAllText(validationTarget, "changed-after-legacy-backup", Encoding.UTF8);
    new BackupUseCase(
        new BackupService(),
        new Rpcs3MutationGuard(() => false))
        .Restore(install, new BackupSnapshot(
            legacySnapshot.DirectoryPath,
            legacyManifest.CreatedAt,
            legacyManifest.Files));
    Assert(File.ReadAllText(validationTarget, Encoding.UTF8) == "validation-backup",
        "Expected a legacy manifest without length or SHA-256 to restore with basic validation.");

    var runningSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [validationTarget]);
    File.WriteAllText(validationTarget, "running-process-current", Encoding.UTF8);
    var runningBefore = File.ReadAllBytes(validationTarget);
    AssertMutationBlocked(
        () => new BackupUseCase(
                new BackupService(),
                new Rpcs3MutationGuard(() => true))
            .Restore(install, runningSnapshot),
        Rpcs3MutationBlockReason.Rpcs3Running,
        "Expected RPCS3 running to block restore before staging.");
    Assert(File.ReadAllBytes(validationTarget).SequenceEqual(runningBefore) &&
           !HasRestoreStages(configPath, patchesPath),
        "Expected a running-process block to leave targets and stage state unchanged.");

    var outsideTarget = Path.Combine(root, "outside-target.yml");
    File.WriteAllText(outsideTarget, "outside-backup", Encoding.UTF8);
    var outsideTargetSnapshot = CreateBackupSnapshot(
        new BackupService(),
        backupRoot,
        [outsideTarget]);
    File.WriteAllText(outsideTarget, "outside-current", Encoding.UTF8);
    AssertMutationBlocked(
        () => new BackupUseCase(
                new BackupService(),
                new Rpcs3MutationGuard(() => false))
            .Restore(install, outsideTargetSnapshot),
        Rpcs3MutationBlockReason.UnsafeTarget,
        "Expected restore targets outside RPCS3 config/patches to be rejected by the central guard.");
    Assert(File.ReadAllText(outsideTarget, Encoding.UTF8) == "outside-current",
        "Expected an unsafe restore target to remain unchanged.");

    Assert(!HasRestoreStages(configPath, patchesPath),
        "Expected every restore path to clean .r3restore and .r3rollback files.");
    Console.WriteLine("Validated transactional backup restore tests PASS.");
}

static BackupSnapshot CreateBackupSnapshot(
    BackupService service,
    string backupRoot,
    IReadOnlyList<string> targets)
{
    var manifest = service.CreateBackup(backupRoot, targets);
    var directory = Path.GetDirectoryName(
        manifest.Files.First().BackupPath)
        ?? throw new InvalidOperationException(
            "Expected a snapshot directory for the backup manifest.");

    return new BackupSnapshot(
        directory,
        manifest.CreatedAt,
        manifest.Files);
}

static void WriteBackupManifest(
    string directory,
    BackupManifest manifest)
{
    File.WriteAllText(
        Path.Combine(directory, "manifest.json"),
        JsonSerializer.Serialize(
            manifest,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }),
        Encoding.UTF8);
}

static void ApplyRestoreOperationForTest(
    string? stagePath,
    string targetPath,
    bool shouldExist)
{
    if (!shouldExist)
    {
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        return;
    }

    if (string.IsNullOrWhiteSpace(stagePath))
    {
        throw new FileNotFoundException(
            "The test restore stage is missing.",
            stagePath);
    }

    File.Copy(stagePath, targetPath, overwrite: true);
}

static bool HasRestoreStages(params string[] roots)
{
    return roots.Any(root =>
        Directory.Exists(root) &&
        Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories)
            .Any(path =>
                path.EndsWith(".r3restore", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".r3rollback", StringComparison.OrdinalIgnoreCase)));
}

static void AssertThrows<TException>(
    Action action,
    string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static string BuildTransactionPatchCatalog(
    string hash,
    string title,
    string gameTitle,
    string serial,
    string version,
    string address)
{
    return
        "Version: 1.2\r\n" +
        $"PPU-{hash}:\r\n" +
        $"  \"{title}\":\r\n" +
        "    Games:\r\n" +
        $"      \"{gameTitle}\":\r\n" +
        $"        {serial}: [ {version} ]\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        $"      - [ be32, {address}, 0x60000000 ]\r\n";
}

static void RunGameListSearchTests()
{
    var list = new GameListVm();
    var collectionChanges = 0;
    NotifyCollectionChangedAction? lastCollectionAction = null;
    list.Games.CollectionChanged += (_, args) =>
    {
        collectionChanges++;
        lastCollectionAction = args.Action;
    };
    list.Load(
    [
        MakeGame("NPEA00137", ".detuned", isDetectedInRpcs3: true),
        MakeGame("BLUS30443", "Demon's Souls", isDetectedInRpcs3: false),
        MakeGame("BCES00569", "Gran Turismo 5", isDetectedInRpcs3: true)
    ]);

    Assert(list.Games.Count == 2, "Expected detected-only filter to show detected games by default.");
    Assert(collectionChanges == 1 &&
           lastCollectionAction == NotifyCollectionChangedAction.Reset,
        "Expected game-list replacement to publish one reset instead of one UI notification per row.");
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
    var scanner = new GameScanner();
    var games = scanner.Scan(env.Install);
    Assert(games.Count == 2, "Expected one game from games.yml and one non-portable dev_hdd0 game.");
    var rogue = games.Single(game => game.Serial == "BLUS31465");
    Assert(rogue.Title == "Assassin's Creed Rogue SFO", "Expected title from PARAM.SFO.");
    Assert(rogue.Version == "01.02", "Expected app version from PARAM.SFO.");
    Assert(games.Any(game => game.Serial == "NPUB00001"), "Expected non-portable dev_hdd0 game.");

    Directory.CreateDirectory(env.Install.CustomConfigsPath);
    File.WriteAllText(
        Path.Combine(
            env.Install.CustomConfigsPath,
            "config_BLES02061.yml"),
        $"{{}}{Environment.NewLine}");
    File.WriteAllText(
        Path.Combine(
            env.Install.CustomConfigsPath,
            "config_not-a-serial.yml"),
        $"{{}}{Environment.NewLine}");
    var customConfigSerials = scanner.ScanCustomConfigSerials(env.Install);
    Assert(customConfigSerials.SequenceEqual(["BLES02061"]),
        "Expected bounded custom-config scanning to return only normalized valid serials.");
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
    Assert(portableInstall.ConfigPath == Path.Combine(portableRpcs3Root, "portable", "config"),
        "Expected config path to follow RPCS3's <RPCS3 folder>\\portable\\config contract.");
    Assert(portableInstall.CustomConfigsPath == Path.Combine(portableRpcs3Root, "portable", "config", "custom_configs"),
        "Expected custom configs to follow RPCS3's portable data root.");
    Assert(portableInstall.PatchesPath == Path.Combine(portableRpcs3Root, "portable", "patches"),
        "Expected imported_patch.yml to follow RPCS3's portable patches path.");
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

    var duplicateJsonKeyCatalogPath = Path.Combine(env.TempRoot, "duplicate-json-key-cat.json");
    File.WriteAllText(duplicateJsonKeyCatalogPath, BuildDuplicateJsonKeyCatalogJson());
    try
    {
        catalogInfoService.LoadCatalog(duplicateJsonKeyCatalogPath);
        throw new InvalidOperationException("Expected duplicate JSON object keys to fail safely.");
    }
    catch (CatalogInvalidException ex)
    {
        Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate JSON key catalog error message contract.");
    }

    var duplicateSerialCatalogPath = Path.Combine(env.TempRoot, "duplicate-serial-cat.json");
    File.WriteAllText(duplicateSerialCatalogPath, BuildDuplicateSerialCatalogJson());
    try
    {
        catalogInfoService.LoadCatalog(duplicateSerialCatalogPath);
        throw new InvalidOperationException("Expected duplicate recommendation serials to fail safely.");
    }
    catch (CatalogInvalidException ex)
    {
        Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate serial catalog error message contract.");
    }

    var duplicateSettingCatalogPath = Path.Combine(env.TempRoot, "duplicate-setting-cat.json");
    File.WriteAllText(duplicateSettingCatalogPath, BuildDuplicateSettingCatalogJson());
    try
    {
        catalogInfoService.LoadCatalog(duplicateSettingCatalogPath);
        throw new InvalidOperationException("Expected duplicate recommendation settings to fail safely.");
    }
    catch (CatalogInvalidException ex)
    {
        Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate setting catalog error message contract.");
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
    Assert(recommendation.Changes.Any(change => change.Key == "SPU XFloat Accuracy" && change.Value == "Accurate"),
        "Expected the current RPCS3 SPU XFloat Accuracy key.");
    Assert(recommendation.Changes.Any(change => change.Key == "Minimum Scalable Dimension" && change.Value == "256"),
        "Expected square resolution threshold values to be normalized to RPCS3's scalar dimension.");
    Assert(recommendation.Changes.Any(change =>
            change.Key == "Minimum Scalable Dimension" &&
            change.Notes == "Fixes bloom See game patches and RPCS3 issue #13546."),
        "Expected Wiki and HTML note markup to be cleaned.");
    Assert(recommendation.Changes.Any(change => change.Key == "Anisotropic Filter Override" && change.Value == "0"),
        "Expected anisotropic Auto to be normalized to RPCS3's numeric value.");
    Assert(recommendation.Changes.Any(change => change.Key == "Frame limit" && change.Value == "Off"),
        "Expected enum Off to remain Off instead of being converted to a boolean.");
    Assert(recommendation.Changes.Any(change => change.Key == "VSync Mode" && change.Value == "Full"),
        "Expected the current RPCS3 VSync Mode key.");
    Assert(recommendation.Changes.Any(change =>
            change.Key == "Vulkan.Asynchronous Texture Streaming" &&
            change.Value == "true"),
        "Expected the current nested Vulkan asynchronous texture streaming key.");
    Assert(recommendation.Changes.All(change => change.Key != "Preferred SPU Threads"),
        "Expected ambiguous Preferred SPU Threads ranges to be rejected instead of guessed.");
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

    return new CatState(recommendation, catalog);
}

static void RunAnalysisReadPathTests(
    SmokeEnv env,
    CatState cat)
{
    var synchronizationContext = new RecordingSynchronizationContext();
    var originalContext = SynchronizationContext.Current;
    AnalyzeResult result;

    try
    {
        SynchronizationContext.SetSynchronizationContext(
            synchronizationContext);
        result = new AppFlow()
            .AnalyzeAsync(env.Install.ExePath, cat.Catalog)
            .GetAwaiter()
            .GetResult();
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(
            originalContext);
    }

    Assert(synchronizationContext.PostCount == 0,
        "Expected analysis continuations to stay off the caller's WPF synchronization context.");
    Assert(result.Games.Single(row => row.Game.Serial == "BLUS31465").IsDetectedInRpcs3,
        "Expected games.yml discoveries to be marked as detected.");
    Assert(result.Games.Single(row => row.Game.Serial == "BLES02061").IsDetectedInRpcs3,
        "Expected an existing custom config to mark a catalog game as detected without UI filesystem probing.");
    Assert(!result.Games.Single(row => row.Game.Serial == "NPUB31547").IsDetectedInRpcs3,
        "Expected catalog-only games without local evidence to remain undetected.");
    Assert(result.Games.Single(row => row.Game.Serial == "NPUB31547")
        .RecState.Items.All(item => !item.IsActionable),
        "Expected catalog-only recommendation state to remain non-actionable.");
    Assert(result.Games.Single(row => row.Game.Serial == "NPUB00001").Rec is null,
        "Expected locally detected games without recommendations to remain visible.");

    Console.WriteLine("Analysis read-path responsiveness smoke PASS.");
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
    var flow = new AppFlow();
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

    var result = flow.ImportCanaryPatch(env.Install, canaryPatchText);

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

    var secondWrite = flow.ImportCanaryPatch(env.Install, canaryPatchText);
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
    var beforeManagedStage = File.ReadAllText(importedPath, Encoding.UTF8);
    var managedStage = importService.PrepareManagedImportedPatchFile(
        env.Install.PatchesPath,
        canaryPatchText);
    Assert(managedStage.Succeeded &&
           managedStage.PreparedFile is not null &&
           File.ReadAllText(importedPath, Encoding.UTF8) == beforeManagedStage,
        "Expected Canary preparation to produce validated staged content without touching imported_patch.yml.");

    var managedWrite = flow.ImportCanaryPatch(env.Install, canaryPatchText);
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

    const string ExactHashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string ExactHashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string ExactHashC = "cccccccccccccccccccccccccccccccccccccccc";
    const string ExactHashD = "dddddddddddddddddddddddddddddddddddddddd";

    var patchHashProbe = new PatchHashProbeService();
    var noHashProbeResult = patchHashProbe.Probe(env.Install, rogue);
    Assert(!noHashProbeResult.HashAvailable &&
           noHashProbeResult.EvidenceKind == PatchHashEvidenceKind.None,
        "Expected missing RPCS3 log metadata to keep patch hash unknown.");
    var noHashGame = patchHashProbe.Enrich(env.Install, rogue);
    Assert(noHashGame.PpuHash is null &&
           noHashGame.SpuHash is null &&
           noHashGame.HashEvidenceKind == PatchHashEvidenceKind.None,
        "Expected missing patch hash probe data not to invent hashes.");

    var gameCacheDir = Path.Combine(env.FakeRpcs3Root, "cache", rogue.Serial);
    Directory.CreateDirectory(gameCacheDir);
    Directory.CreateDirectory(Path.Combine(
        gameCacheDir,
        "ppu-0000000000000000000000000000000000000000-EBOOT.BIN"));
    var zeroCacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(zeroCacheHashGame.PpuHash is null && zeroCacheHashGame.SpuHash is null,
        "Expected all-zero placeholder PPU cache hash not to be treated as ready.");
    Directory.Delete(gameCacheDir, recursive: true);

    Directory.CreateDirectory(gameCacheDir);
    var base57CacheEntry = Path.Combine(
        gameCacheDir,
        "ppu-QoiFRPF8j00N996aQMthtWqEUG0Y-EBOOT.BIN");
    Directory.CreateDirectory(base57CacheEntry);
    var base57CacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(base57CacheHashGame.PpuHash ==
               "728fc4496a4c347a654ab54d3e3f0e44ed3c9af5" &&
           base57CacheHashGame.HashEvidenceKind == PatchHashEvidenceKind.GameCache,
        "Expected RPCS3's Base57 PPU cache directory to become canonical 40-hex compatibility evidence.");
    Directory.Delete(gameCacheDir, recursive: true);

    Directory.CreateDirectory(gameCacheDir);
    var exactCacheEntry = Path.Combine(
        gameCacheDir,
        $"ppu-{ExactHashD}-EBOOT.BIN");
    Directory.CreateDirectory(exactCacheEntry);
    var cacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(cacheHashGame.PpuHash == ExactHashD &&
           cacheHashGame.HashEvidenceKind == PatchHashEvidenceKind.GameCache &&
           cacheHashGame.HashSource == exactCacheEntry,
        "Expected PatchHashProbeService to read an exact 40-hex PPU hash from the per-game cache.");
    Directory.Delete(gameCacheDir, recursive: true);

    var metadataZeroHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
    {
        PpuHash = "0000000000000000000000000000000000000000"
    });
    Assert(!metadataZeroHashResult.HashAvailable,
        "Expected all-zero game metadata PPU hash not to be treated as ready.");

    var shortMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
    {
        PpuHash = new string('a', 39)
    });
    Assert(!shortMetadataHashResult.HashAvailable,
        "Expected a 39-character game metadata hash to be rejected.");

    var longMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
    {
        PpuHash = new string('a', 41)
    });
    Assert(!longMetadataHashResult.HashAvailable,
        "Expected a 41-character game metadata hash to be rejected.");

    var nonHexMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
    {
        PpuHash = "gggggggggggggggggggggggggggggggggggggggg"
    });
    Assert(!nonHexMetadataHashResult.HashAvailable,
        "Expected a non-hex game metadata hash to be rejected.");

    var metadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
    {
        PpuHash = ExactHashA
    });
    Assert(metadataHashResult.HashAvailable &&
           metadataHashResult.PpuHash == ExactHashA &&
           metadataHashResult.EvidenceKind == PatchHashEvidenceKind.ExistingMetadata,
        "Expected an exact existing game metadata hash to be used before log probing.");

    var rootLogPath = Path.Combine(env.FakeRpcs3Root, "RPCS3.log");

    File.WriteAllText(
        rootLogPath,
        $"License maintenance referenced {rogue.Serial}.{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");
    var maintenanceMentionGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(maintenanceMentionGame.PpuHash is null,
        "Expected a serial mention outside a SYS: Serial session not to establish hash evidence.");

    File.WriteAllText(
        rootLogPath,
        $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
        $"No executable hash was emitted for this session.{Environment.NewLine}" +
        $"SYS: Serial: BLUS99999{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{ExactHashB}{Environment.NewLine}");
    var crossedSessionGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(crossedSessionGame.PpuHash is null,
        "Expected the hash probe not to cross into another game's SYS: Serial session.");

    File.WriteAllText(
        rootLogPath,
        $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");
    var probedGame = new PatchHashProbeService().Enrich(env.Install, rogue);
    Assert(probedGame.PpuHash == ExactHashA &&
           probedGame.HashEvidenceKind == PatchHashEvidenceKind.PlainLog &&
           probedGame.HashSource == rootLogPath,
        "Expected PatchHashProbeService to read an exact PPU hash from the matching RPCS3.log session.");

    var movedLogRoot = Path.Combine(env.TempRoot, "rpcs3-log-folder");
    Directory.CreateDirectory(Path.Combine(movedLogRoot, "config"));
    Directory.CreateDirectory(Path.Combine(movedLogRoot, "log"));
    File.WriteAllText(Path.Combine(movedLogRoot, "rpcs3.exe"), string.Empty);
    var movedPlainLogPath = Path.Combine(movedLogRoot, "log", "RPCS3.log");
    File.WriteAllText(
        movedPlainLogPath,
        $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{ExactHashB}{Environment.NewLine}");
    var movedLogInstall = Rpcs3InstallService.FromExePath(
        Path.Combine(movedLogRoot, "rpcs3.exe"));
    var movedLogGame = new PatchHashProbeService().Enrich(movedLogInstall, rogue);
    Assert(movedLogGame.PpuHash == ExactHashB &&
           movedLogGame.HashEvidenceKind == PatchHashEvidenceKind.PlainLog,
        "Expected PatchHashProbeService to read PPU hash from the modern log/RPCS3.log path.");

    var largeLogRoot = Path.Combine(env.TempRoot, "large-rpcs3-log");
    Directory.CreateDirectory(Path.Combine(largeLogRoot, "config"));
    Directory.CreateDirectory(Path.Combine(largeLogRoot, "log"));
    File.WriteAllText(Path.Combine(largeLogRoot, "rpcs3.exe"), string.Empty);
    var largePlainLogPath = Path.Combine(largeLogRoot, "log", "RPCS3.log");
    var oneMiBFiller = new string('x', 1024 * 1024);
    using (var writer = new StreamWriter(
               largePlainLogPath,
               append: false,
               new UTF8Encoding(false)))
    {
        writer.WriteLine($"SYS: Serial: {rogue.Serial}");
        writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashC}");
        for (var index = 0; index < 18; index++)
        {
            writer.Write(oneMiBFiller);
        }
    }

    var largeLogInstall = Rpcs3InstallService.FromExePath(
        Path.Combine(largeLogRoot, "rpcs3.exe"));
    var largePlainLogGame = new PatchHashProbeService().Enrich(
        largeLogInstall,
        rogue);
    Assert(largePlainLogGame.PpuHash == ExactHashC &&
           largePlainLogGame.HashEvidenceKind == PatchHashEvidenceKind.PlainLog,
        "Expected a large RPCS3 log to retain boot hash evidence from its bounded head probe.");

    using (var writer = new StreamWriter(
               largePlainLogPath,
               append: false,
               new UTF8Encoding(false)))
    {
        for (var index = 0; index < 18; index++)
        {
            writer.Write(oneMiBFiller);
        }

        writer.WriteLine();
        writer.WriteLine($"SYS: Serial: {rogue.Serial}");
        writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashD}");
    }

    var largeTailLogGame = new PatchHashProbeService().Enrich(
        largeLogInstall,
        rogue);
    Assert(largeTailLogGame.PpuHash == ExactHashD &&
           largeTailLogGame.HashEvidenceKind == PatchHashEvidenceKind.PlainLog,
        "Expected the bounded tail probe to find the newest complete game session in a large RPCS3 log.");

    File.Delete(largePlainLogPath);
    var largeCompressedLogPath = Path.Combine(
        largeLogRoot,
        "log",
        "RPCS3.log.gz");
    using (var compressedFile = File.Create(largeCompressedLogPath))
    using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
    using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
    {
        writer.WriteLine($"SYS: Serial: {rogue.Serial}");
        writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashB}");
        for (var index = 0; index < 18; index++)
        {
            writer.Write(oneMiBFiller);
        }
    }

    var largeCompressedLogGame = new PatchHashProbeService().Enrich(
        largeLogInstall,
        rogue);
    Assert(largeCompressedLogGame.PpuHash == ExactHashB &&
           largeCompressedLogGame.HashEvidenceKind == PatchHashEvidenceKind.CompressedLog,
        "Expected a large compressed RPCS3 log to retain boot hash evidence from its bounded head probe.");

    var newestEvidenceRoot = Path.Combine(env.TempRoot, "newest-hash-evidence");
    Directory.CreateDirectory(Path.Combine(newestEvidenceRoot, "config"));
    Directory.CreateDirectory(Path.Combine(newestEvidenceRoot, "log"));
    File.WriteAllText(Path.Combine(newestEvidenceRoot, "rpcs3.exe"), string.Empty);

    var olderPlainLog = Path.Combine(newestEvidenceRoot, "log", "RPCS3.log");
    var newerCompressedLog = Path.Combine(newestEvidenceRoot, "log", "RPCS3.log.gz");
    File.WriteAllText(
        olderPlainLog,
        $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");

    using (var compressedFile = File.Create(newerCompressedLog))
    using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
    using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
    {
        writer.Write(
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashC}{Environment.NewLine}");
    }

    var olderTimestamp = DateTime.UtcNow.AddMinutes(-10);
    var newerTimestamp = DateTime.UtcNow.AddMinutes(-5);
    File.SetLastWriteTimeUtc(olderPlainLog, olderTimestamp);
    File.SetLastWriteTimeUtc(newerCompressedLog, newerTimestamp);
    var actualNewerTimestamp = File.GetLastWriteTimeUtc(newerCompressedLog);

    var newestEvidenceInstall = Rpcs3InstallService.FromExePath(
        Path.Combine(newestEvidenceRoot, "rpcs3.exe"));
    var newestEvidenceGame = new PatchHashProbeService().Enrich(
        newestEvidenceInstall,
        rogue);

    Assert(newestEvidenceGame.PpuHash == ExactHashC &&
           newestEvidenceGame.HashEvidenceKind == PatchHashEvidenceKind.CompressedLog &&
           newestEvidenceGame.HashSource == newerCompressedLog &&
           newestEvidenceGame.HashEvidenceTimestampUtc == actualNewerTimestamp,
        "Expected the newest valid compressed RPCS3 log evidence to win over an older plain log.");

    using (var compressedFile = File.Create(newerCompressedLog))
    using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
    using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
    {
        writer.Write(
            $"Maintenance line for {rogue.Serial}.{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{new string('d', 41)}{Environment.NewLine}");
    }

    File.SetLastWriteTimeUtc(newerCompressedLog, DateTime.UtcNow);
    var newestInvalidGame = new PatchHashProbeService().Enrich(
        newestEvidenceInstall,
        rogue);

    Assert(newestInvalidGame.PpuHash == ExactHashA &&
           newestInvalidGame.HashEvidenceKind == PatchHashEvidenceKind.PlainLog,
        "Expected newest-valid selection to ignore a newer invalid compressed log and retain the older valid evidence.");

    var matchedGame = probedGame;
    var patchMatches = new PatchMatchService().Match(patchRead.Catalog, [matchedGame]);
    Assert(patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == PatchMatchStatus.ExactMatch,
        "Expected matching serial/version/PPU hash to be ExactMatch.");
    var exactEvidence = patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
    Assert(exactEvidence.AllDetectedHashes.Contains(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            StringComparer.OrdinalIgnoreCase),
        "Expected an accepted patch to retain the detected executable hash as visible evidence.");
    Assert(!string.IsNullOrWhiteSpace(exactEvidence.HashEvidenceSource),
        "Expected an accepted patch to retain its local hash evidence source.");
    Assert(exactEvidence.HashEvidenceKind == PatchHashEvidenceKind.PlainLog,
        "Expected an accepted patch to retain its typed local hash evidence source.");
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
    Assert(wrongHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").AllDetectedHashes
            .Contains("cccccccccccccccccccccccccccccccccccccccc", StringComparer.OrdinalIgnoreCase),
        "Expected a rejected patch to show the conflicting detected hash.");

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
    Assert(PatchRiskPolicy.CanSelectInPreview(hashUnknownSafeFix) &&
           PatchActivationPolicy.IsRuntimeGuardedMatch(hashUnknownSafeFix),
        "Expected a trusted exact serial/version patch with no local Hash to be selectable under RPCS3's runtime Hash guard.");
    var explicitHashMismatch = wrongHashMatches.Single(match =>
        match.Patch.Title == "Skip Intro Videos");
    Assert(!PatchActivationPolicy.CanEnable(explicitHashMismatch),
        "Expected an explicitly different detected executable Hash to remain blocked.");
    var unknownSourceHashMatch = hashUnknownSafeFix with
    {
        Patch = hashUnknownSafeFix.Patch with
        {
            Source = PatchSourceOrigin.Unknown
        }
    };
    Assert(!PatchActivationPolicy.CanEnable(unknownSourceHashMatch),
        "Expected an untrusted patch source without local Hash evidence to remain blocked.");
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
    Assert(!PatchRiskPolicy.CanSelectInPreview(canaryPatch),
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
    Assert(PatchRiskPolicy.CanSelectInPreview(canaryExact),
        "Expected exact Canary patches to be user-selectable.");

    var patchApplyPlanner = new PatchApplyPlanner();
    var hashUnknownApplyPlan = patchApplyPlanner.CreatePlan(env.Install, [hashUnknownSafeFix], expertMode: false);
    Assert(hashUnknownApplyPlan.HasChanges &&
           hashUnknownApplyPlan.Changes.Single().PatchTitle == "Skip Intro Videos",
        "Expected a trusted serial/version match to be planned with RPCS3 enforcing its executable Hash at runtime.");

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
        "Expected all explicitly checked safe selections to be included, with duplicate exact/runtime-guarded identities deduplicated.");
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

    var unrelatedGlobalMatch = matchService.Match(
        allTargetPatch,
        [rogue with
        {
            Version = "01.02",
            PpuHash = primaryHash,
            PpuHashes = [primaryHash]
        }]);
    var unrelatedGlobalPreview = new PatchPreviewVm();
    unrelatedGlobalPreview.Load(
        [unrelatedGlobalMatch],
        "RPCS3 patch catalog",
        isEnabledResolver: _ => true);
    Assert(unrelatedGlobalPreview.Items.Count == 0,
        "Expected an enabled All/All/All patch to stay off unrelated game pages when its runtime hash does not match.");

    var exactGlobalPreview = new PatchPreviewVm();
    exactGlobalPreview.Load(
        [allTargetMatch],
        "RPCS3 patch catalog",
        isEnabledResolver: _ => true);
    Assert(exactGlobalPreview.Items.Count == 1 &&
           exactGlobalPreview.Items.Single().IsEnabled,
        "Expected an enabled All/All/All patch to remain visible on a game page with exact runtime-hash evidence.");

    const string extendedHashCatalog = """
Version: 1.2
PRX-QoiFRPF8j00N996aQMthtWqEUG0Y-0:
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

    var mergeService = new PatchCatalogMergeService();
    var versionBase = multiTargetPatch with
    {
        PatchVersion = "1.2",
        Source = PatchSourceOrigin.Official,
        SourcePath = "patch.yml"
    };

    var higherVersionMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2", [versionBase]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "1.10",
                Source = PatchSourceOrigin.Imported,
                SourcePath = "imported_patch.yml"
            }
        ]));
    Assert(higherVersionMerge.Entries.Single() is
    { PatchVersion: "1.10", Source: PatchSourceOrigin.Imported },
        "Expected a valid numerically higher RPCS3 patch version to replace the existing duplicate.");
    Assert(higherVersionMerge.MergeDiagnostics.Single().Code == "PATCH_VERSION_OVERRIDE",
        "Expected a valid higher patch version to be reported as an override.");

    var equalVersionMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2", [versionBase]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "1.2.0",
                Source = PatchSourceOrigin.Imported,
                SourcePath = "imported_patch.yml"
            }
        ]));
    Assert(equalVersionMerge.Entries.Single().Source == PatchSourceOrigin.Official,
        "Expected RPCS3-equivalent patch versions to keep the entry loaded first.");
    Assert(equalVersionMerge.MergeDiagnostics.Single().Code == "PATCH_DUPLICATE_SKIPPED",
        "Expected an equal patch version to be reported as a skipped duplicate.");

    var lowerVersionMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2", [versionBase]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "1.1",
                Source = PatchSourceOrigin.Imported,
                SourcePath = "imported_patch.yml"
            }
        ]));
    Assert(lowerVersionMerge.Entries.Single().Source == PatchSourceOrigin.Official,
        "Expected a lower patch version to keep the entry loaded first.");

    var invalidVersionMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2", [versionBase]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "9.0-beta",
                Source = PatchSourceOrigin.Imported,
                SourcePath = "imported_patch.yml"
            }
        ]));
    Assert(invalidVersionMerge.Entries.Single().Source == PatchSourceOrigin.Official,
        "Expected an incomparable patch version to keep the entry loaded first instead of using lexical order.");

    var prefixedVersionMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2", [versionBase]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "v9.0",
                Source = PatchSourceOrigin.Imported,
                SourcePath = "imported_patch.yml"
            }
        ]));
    Assert(prefixedVersionMerge.Entries.Single().Source == PatchSourceOrigin.Official,
        "Expected RPCS3-incomparable v-prefixed patch versions not to override an existing entry.");

    var residentOverPreviewMerge = mergeService.Merge(
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "99.0",
                Source = PatchSourceOrigin.BundledCanary,
                SourcePath = "can.yml"
            }
        ]),
        new Rpcs3PatchCatalog("1.2",
        [
            versionBase with
            {
                PatchVersion = "1.0",
                Source = PatchSourceOrigin.Official,
                SourcePath = "patch.yml"
            }
        ]));
    Assert(residentOverPreviewMerge.Entries.Single().Source == PatchSourceOrigin.Official,
        "Expected an RPCS3-resident source to remain authoritative over the bundled Canary preview.");

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
    preview.OfficialGroups.Single().IsExpanded = false;
    Assert(preview.HasSelectedOfficialApplyablePatches && preview.SelectedOfficialMatches().Count == 1,
        "Expected official-tab selection state to be isolated and executable.");
    preview.RefreshLanguage();
    var rebuiltEnableItem = preview.OfficialItems.Single(item => item.CanEnable);
    Assert(!ReferenceEquals(enableItem, rebuiltEnableItem),
        "Expected a live language switch to rebuild patch item containers under the new FlowDirection.");
    Assert(rebuiltEnableItem.IsSelected && preview.SelectedOfficialMatches().Count == 1,
        "Expected live language container rebuild to preserve explicit patch selection state.");
    Assert(!preview.OfficialGroups.Single().IsExpanded,
        "Expected live language container rebuild to preserve the user's group expansion state.");

    var experimentalPreview = new PatchPreviewVm();
    var experimentalPatch = multiTargetPatch with
    {
        Title = "Experimental renderer path",
        Risk = PatchRisk.Experimental,
        Source = PatchSourceOrigin.Official
    };
    experimentalPreview.Load(
        [matchService.Match(
            experimentalPatch,
            [rogue with { Version = "01.02", PpuHashes = [primaryHash, secondaryHash] }])],
        "RPCS3 patch catalog");
    Assert(experimentalPreview.CanaryItems.Count == 1 &&
           experimentalPreview.OfficialItems.Count == 0,
        "Expected experimental classification to control the experimental page even when the source catalog is official.");

    var configPath = Path.Combine(env.TempRoot, "contract-patch-config.yml");
    var change = allTargetPlan.Changes.Single();
    var writer = new PatchConfigWriter();
    var configReader = new PatchConfigReader();
    writer.Write(configPath, [change]);
    Assert(configReader.IsEnabled(configPath, change),
        "Expected RPCS3-compatible Enabled mapping to be readable after write.");
    var enabledSnapshot = configReader.ReadSnapshot(configPath);
    writer.Write(configPath, [change], enabled: false);
    Assert(enabledSnapshot.IsEnabled(change),
        "Expected a patch preview snapshot to remain stable without reparsing patch_config.yml per row.");
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
        $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
        $"{new string('x', 20000)}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{primaryHash}{Environment.NewLine}" +
        $"ppu_loader: PPU executable hash: PPU-{secondaryHash}{Environment.NewLine}" +
        $"ppu_loader: SPU executable hash: SPU-{primaryHash}{Environment.NewLine}");
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
    var officialStatus = officialOnly.SourceStatuses.Single(source =>
        source.Source == PatchSourceOrigin.Official && source.Name == "patch.yml");
    Assert(officialStatus is { Exists: true, Loaded: true, EntryCount: > 0 },
        "Expected source diagnostics to report a loaded official patch.yml with its entry count.");

    File.Delete(patchYmlPath);
    File.WriteAllText(importedPatchPath, BuildImportedPatchYml());
    var importedOnly = flow.LoadPatchCat(env.Install);
    Assert(importedOnly.Cat is not null, "Expected imported_patch.yml-only AppFlow load to return a catalog.");
    Assert(string.IsNullOrWhiteSpace(importedOnly.Error), "Expected imported_patch.yml-only AppFlow load not to report patch.yml missing.");
    Assert(importedOnly.Cat!.Entries.Any(patch => patch.Title == "Imported Canary Display Patch" && patch.Risk == PatchRisk.Canary),
        "Expected imported_patch.yml entries to load as Canary patches.");
    Assert(importedOnly.SourceStatuses.Single(source =>
            source.Source == PatchSourceOrigin.Official && source.Name == "patch.yml") is
        { Exists: false, Loaded: false, EntryCount: 0 },
        "Expected source diagnostics to distinguish a missing optional patch.yml from an invalid catalog.");
    Assert(importedOnly.SourceStatuses.Single(source =>
            source.Source == PatchSourceOrigin.Imported && source.Name == "imported_patch.yml") is
        { Exists: true, Loaded: true, EntryCount: > 0 },
        "Expected source diagnostics to report imported_patch.yml independently.");

    var importedBeforeReplay = File.ReadAllBytes(importedPatchPath);
    var importResult = flow.ImportCanaryPatch(env.Install, BuildImportedPatchYml());
    Assert(importResult.Succeeded &&
           importResult.BackupPath is null &&
           File.ReadAllBytes(importedPatchPath).SequenceEqual(importedBeforeReplay),
        "Expected an already-present Canary import to remain byte-idempotent without a backup: " + importResult.Error);

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
    var invalidOfficialStatus = invalidOfficialWithImported.SourceStatuses.Single(source =>
        source.Source == PatchSourceOrigin.Official && source.Name == "patch.yml");
    Assert(invalidOfficialStatus is { Exists: true, Loaded: false, EntryCount: 0 } &&
           !string.IsNullOrWhiteSpace(invalidOfficialStatus.Diagnostic),
        "Expected source diagnostics to retain the exact reason an existing patch.yml failed to load.");

    File.Delete(patchYmlPath);
    File.Delete(importedPatchPath);
    var none = flow.LoadPatchCat(env.Install);
    Assert(none.Cat is not null && none.Cat.Entries.Any(entry => entry.Source == PatchSourceOrigin.BundledCanary),
        "Expected bundled Canary preview data to remain available when optional RPCS3 patch files are missing.");
    Assert(string.IsNullOrWhiteSpace(none.Error),
        "Expected missing optional RPCS3 patch files not to be reported as corrupt data.");
    Assert(none.SourceStatuses.Single(source =>
            source.Source == PatchSourceOrigin.BundledCanary && source.Name == "can.yml") is
        { Exists: true, Loaded: true, EntryCount: > 0 },
        "Expected source diagnostics to prove the complete bundled Canary database was loaded even without patch.yml.");

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
        .ToDictionary(
            path => path,
            path => File.Exists(path)
                ? (Exists: true,
                   LastWriteUtc: File.GetLastWriteTimeUtc(path),
                   Length: new FileInfo(path).Length,
                   Sha256: ComputeFileSha256(path))
                : (Exists: false,
                   LastWriteUtc: DateTime.MinValue,
                   Length: 0L,
                   Sha256: string.Empty),
            StringComparer.OrdinalIgnoreCase);

    var games = new GameScanner().Scan(install);
    Assert(games.Count > 0, "Expected the supplied RPCS3 installation to expose installed games.");

    var patchReader = new PatchCatalogReader();
    var officialCount = 0;
    var officialPrxCount = 0;
    var officialOvlCount = 0;
    if (File.Exists(officialPath))
    {
        var officialRead = patchReader.Read(officialPath, PatchSourceOrigin.Official);
        Assert(officialRead.Succeeded,
            "Expected the supplied RPCS3 patch.yml to parse when present: " + officialRead.Error);
        officialCount = officialRead.Catalog.Entries.Count;
        officialPrxCount = officialRead.Catalog.Entries.Count(entry => entry.HashKind == PatchHashKind.Prx);
        officialOvlCount = officialRead.Catalog.Entries.Count(entry => entry.HashKind == PatchHashKind.Ovl);
    }
    else
    {
        Console.WriteLine(
            "Real RPCS3 installation has no patches\\patch.yml; optional official catalog absence passed.");
    }

    var importedCount = 0;
    if (File.Exists(importedPath))
    {
        var importedRead = patchReader.Read(importedPath, PatchSourceOrigin.Imported);
        Assert(importedRead.Succeeded,
            "Expected the supplied RPCS3 imported_patch.yml to parse when present: " + importedRead.Error);
        importedCount = importedRead.Catalog.Entries.Count;
    }
    else
    {
        Console.WriteLine(
            "Real RPCS3 installation has no patches\\imported_patch.yml; optional imported catalog absence passed.");
    }

    var patchConfig = new PatchConfigReader().Load(install.PatchConfigPath);
    Assert(patchConfig.Documents.Count > 0,
        "Expected patch_config.yml loading to return a valid mapping or the safe empty mapping.");
    if (!File.Exists(install.PatchConfigPath))
    {
        Console.WriteLine(
            "Real RPCS3 installation has no config\\patch_config.yml; safe empty configuration handling passed.");
    }

    var flow = new AppFlow();
    var flowResult = flow.LoadPatchCat(install);
    Assert(flowResult.Cat is not null && flowResult.Cat.Entries.Count > 0,
        "Expected AppFlow to load available RPCS3 and bundled patch sources: " + flowResult.Error);

    var hashProbe = new PatchHashProbeService();
    var enrichedGames = games
        .Select(game => hashProbe.Enrich(install, game))
        .ToArray();
    var hashSample = enrichedGames.FirstOrDefault(game =>
        game.AllPpuHashes.Count > 0 ||
        game.AllSpuHashes.Count > 0 ||
        game.AllPrxHashes.Count > 0 ||
        game.AllOvlHashes.Count > 0);
    if (hashSample is not null)
    {
        var preview = new AppFlow().BuildPatchPreview(flowResult.Cat!, hashSample);
        Assert(preview.All(match => match.MatchedSerial == hashSample.Serial),
            "Expected real-install preview results to stay scoped to the selected game.");
        Console.WriteLine(
            $"Real hash evidence sample: {hashSample.Serial}, " +
            $"PPU={hashSample.AllPpuHashes.Count}, SPU={hashSample.AllSpuHashes.Count}, " +
            $"PRX={hashSample.AllPrxHashes.Count}, OVL={hashSample.AllOvlHashes.Count}, " +
            $"kind={hashSample.HashEvidenceKind}, source={hashSample.HashSource}.");
    }
    else
    {
        Assert(enrichedGames.All(game =>
                game.AllPpuHashes.Count == 0 &&
                game.AllSpuHashes.Count == 0 &&
                game.AllPrxHashes.Count == 0 &&
                game.AllOvlHashes.Count == 0),
            "Expected a real installation without boot evidence to remain consistently HashUnknown.");
        Console.WriteLine(
            "Real RPCS3 installation has no bounded boot/cache Hash evidence; safe HashUnknown handling passed.");
    }

    var builtInCatalog = flow.LoadBuiltInCat(AppContext.BaseDirectory);
    Assert(builtInCatalog.Cat is not null,
        "Expected the bundled recommendation catalog to load during real-install analysis.");
    var analysisTimer = System.Diagnostics.Stopwatch.StartNew();
    var analysis = flow.AnalyzeAsync(exePath, builtInCatalog.Cat!).GetAwaiter().GetResult();
    analysisTimer.Stop();
    Assert(analysis.Games.Count == games.Count,
        "Expected full R3Config analysis to retain every discovered RPCS3 game.");
    var realEvaluations = analysis.Games
        .SelectMany(row => flow.BuildPatchPreview(
                analysis.PatchCat.Cat!,
                row.Game)
            .Select(match => (row.Game, Match: match)))
        .ToArray();
    Assert(realEvaluations.All(item => item.Match.Compatibility is not null),
        "Expected every real official/Canary/cheat preview to carry one canonical compatibility decision.");
    var actionableEvaluations = realEvaluations
        .Where(item => PatchActivationPolicy.CanEnable(item.Match))
        .ToArray();
    Assert(actionableEvaluations.All(item =>
            item.Match.Compatibility?.CanEnable == true &&
            Rpcs3PatchRuntimeKey.TryParse(
                item.Match.Patch.RuntimeHashKey,
                out _)),
        "Expected every real actionable patch to have a valid RPCS3 runtime key and a positive canonical decision.");
    var realExactCount = actionableEvaluations.Count(item =>
        item.Match.Compatibility?.Verdict ==
            PatchCompatibilityVerdict.Compatible);
    var realRuntimeGuardedCount = actionableEvaluations.Count(item =>
        item.Match.Compatibility?.Verdict ==
            PatchCompatibilityVerdict.RuntimeGuarded);
    var exactSamples = realEvaluations
        .Where(item => item.Match.Compatibility?.Verdict ==
            PatchCompatibilityVerdict.Compatible)
        .Select(item =>
            $"{item.Game.Serial} {item.Game.Version}: {item.Match.Patch.Title} [{item.Match.Patch.Risk}]")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(8)
        .ToArray();

    foreach (var (path, snapshot) in trackedPaths)
    {
        var exists = File.Exists(path);
        Assert(exists == snapshot.Exists,
            "Read-only integration verification changed RPCS3 file existence: " + path);

        if (!snapshot.Exists)
        {
            continue;
        }

        var currentInfo = new FileInfo(path);
        Assert(currentInfo.LastWriteTimeUtc == snapshot.LastWriteUtc,
            "Read-only integration verification changed RPCS3 timestamp: " + path);
        Assert(currentInfo.Length == snapshot.Length,
            "Read-only integration verification changed RPCS3 file length: " + path);
        Assert(string.Equals(ComputeFileSha256(path), snapshot.Sha256, StringComparison.Ordinal),
            "Read-only integration verification changed RPCS3 file content: " + path);
    }

    Console.WriteLine(
        $"Real RPCS3 read-only contract passed: {games.Count} games, " +
        $"official={officialCount} (PRX={officialPrxCount}, OVL={officialOvlCount}), " +
        $"imported={importedCount}, merged={flowResult.Cat!.Entries.Count}, " +
        $"actionable={actionableEvaluations.Length} (exact={realExactCount}, runtime-guarded={realRuntimeGuardedCount}), " +
        $"full analysis {analysisTimer.ElapsedMilliseconds} ms.");
    if (exactSamples.Length > 0)
    {
        Console.WriteLine("Real exact patch samples: " + string.Join(" | ", exactSamples));
    }
}

static string ComputeFileSha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
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
|spu xfloat accuracy = Accurate
|resolution scale threshold = 256 x 256
|resolution scale threshold notes = Fixes bloom&lt;br&gt;See [[Help:Game Patches|game patches]] and {{issue|13546}}.
|anisotropic filter = Auto
|framelimit = Off
|vsync = Full
|asynchronous texture streaming = On
|preferred spu threads = 1 - 3
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

static string BuildDuplicateJsonKeyCatalogJson()
{
    return """
{
  "DataSource": "Built-in",
  "DataVersion": "2099-01-04",
  "dataversion": "duplicate",
  "Recommendations": []
}
""";
}

static string BuildDuplicateSerialCatalogJson()
{
    return """
{
  "DataSource": "Built-in",
  "DataVersion": "2099-01-04",
  "Recommendations": [
    {
      "Serial": "TEST00002",
      "WikiTitle": "First duplicate serial",
      "Changes": []
    },
    {
      "Serial": "test00002",
      "WikiTitle": "Second duplicate serial",
      "Changes": []
    }
  ]
}
""";
}

static string BuildDuplicateSettingCatalogJson()
{
    return """
{
  "DataSource": "Built-in",
  "DataVersion": "2099-01-05",
  "Recommendations": [
    {
      "Serial": "TEST00003",
      "WikiTitle": "Duplicate setting",
      "Changes": [
        {
          "Section": "Video",
          "Key": "Write Color Buffers",
          "Value": "true",
          "WikiKey": "write color buffers",
          "Notes": "First value.",
          "Risk": "Recommended"
        },
        {
          "Section": "video",
          "Key": "write color buffers",
          "Value": "false",
          "WikiKey": "write color buffers",
          "Notes": "Conflicting duplicate.",
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

static void AssertMutationBlocked(
    Action action,
    Rpcs3MutationBlockReason reason,
    string message)
{
    try
    {
        action();
    }
    catch (Rpcs3MutationBlockedException ex)
    {
        Assert(ex.Reason == reason, message + $" Actual reason: {ex.Reason}.");
        return;
    }

    throw new InvalidOperationException(message);
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

sealed record CatState(
    WikiGameRecommendation Recommendation,
    RecommendationCatalogService Catalog);

sealed record CfgPlanState(ConfigPlan Plan, IReadOnlyList<ConfigChange> SelectedChanges);

sealed class RecordingSynchronizationContext : SynchronizationContext
{
    private int _postCount;

    public int PostCount => Volatile.Read(ref _postCount);

    public override void Post(
        SendOrPostCallback callback,
        object? state)
    {
        Interlocked.Increment(ref _postCount);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}
