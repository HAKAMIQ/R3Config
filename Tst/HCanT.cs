using System.Text;
using System.Text.Json;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

internal static class HCanTests
{
    public static void Run()
    {
        RunHashExtractionTests();
        RunValidationTests();
        RunYamlTests();
        RunImportEditTests();
        RunTransactionTests();
        RunViewModelTests();
        RunLocalizationAndViewTests();
        Console.WriteLine("H-Canary authoring, import, validation, transaction, ViewModel, and localization tests passed.");
    }

    private static void RunHashExtractionTests()
    {
        const string ppu = "8645cfa235c9980ff74b5e06388b29e97fdbbed9";
        const string spu = "1234567890abcdef1234567890abcdef12345678";
        var log = $"noise\nPPU executable hash: PPU-{ppu}\nSPU executable hash: SPU-{spu}\n" +
                  $"PPU executable hash: PPU-{ppu}\nPPU executable hash: PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n";
        var service = new HCanaryHashExtractionService();
        var result = service.Parse(log, "RPCS3.log");
        Assert(result.Succeeded, "Hash parsing should succeed.");
        Assert(result.Hashes.Count == 3, "Hash parsing should keep multiple values and remove duplicates.");
        Assert(result.Hashes[0].Kind == PatchHashKind.Ppu && result.Hashes[0].Value == ppu, "First PPU Hash should preserve useful log order.");
        Assert(result.Hashes[1].Kind == PatchHashKind.Spu && result.Hashes[1].Value == spu, "SPU Hash should be classified separately.");
        Assert(result.Hashes[0].SourcePath == "RPCS3.log" && result.Hashes[0].FirstLine == 2, "Hash source and line should be exposed.");
        Assert(service.Parse("invalid log").Hashes.Count == 0, "A log without executable Hashes should return an empty result.");
        Assert(service.Parse("PPU executable hash: PPU-nothex").Hashes.Count == 0, "Malformed Hash text must be ignored.");

        var temp = NewTemp();
        try
        {
            var logPath = Path.Combine(temp, "RPCS3.log");
            File.WriteAllText(logPath, log);
            Assert(service.Read(logPath).Hashes.Count == 3, "RPCS3.log file reading should extract all Hashes.");
            Assert(!service.Read(Path.Combine(temp, "missing.log")).Succeeded, "Missing RPCS3.log should be reported.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void RunValidationTests()
    {
        var service = new HCanaryPatchAuthoringService();
        var valid = ValidDraft();
        Assert(service.Validate(valid).IsValid, "A complete PPU draft should validate.");

        ExpectIssue(service, valid, draft => draft.ExecutableHash = "PPU-abc", "Hash.Invalid");
        ExpectIssue(service, valid, draft => draft.ExecutableHash = "PPU-gggggggggggggggggggggggggggggggggggggggg", "Hash.Invalid");
        ExpectIssue(service, valid, draft => draft.PatchName = string.Empty, "PatchName.Required");
        ExpectIssue(service, valid, draft => draft.PatchVersion = string.Empty, "PatchVersion.Required");
        ExpectIssue(service, valid, draft => draft.PatchVersion = "١.٠", "PatchVersion.Invalid");
        ExpectIssue(service, valid, draft => draft.Games[0].GameId = string.Empty, "GameId.Required");
        ExpectIssue(service, valid, draft => draft.Games[0].GameVersion = string.Empty, "GameVersion.Required");
        ExpectIssue(service, valid, draft => draft.Instructions.Clear(), "Instructions.Required");
        ExpectIssue(service, valid, draft => draft.Instructions[0].Address = "1234", "Address.Invalid");
        ExpectIssue(service, valid, draft => draft.Instructions[0].Value = "hello", "Value.Invalid");
        ExpectIssue(service, valid, draft => draft.Instructions[0].Operation = "unknown", "Operation.Invalid");
        ExpectIssue(service, valid, draft => draft.Instructions[0] = new HCanaryInstruction { Operation = "load", Address = "Missing" }, "Anchor.Missing");

        var duplicateAnchor = valid.Clone();
        duplicateAnchor.Anchors =
        [
            new HCanaryAnchor { Name = "Shared", Instructions = [new HCanaryInstruction()] },
            new HCanaryAnchor { Name = "Shared", Instructions = [new HCanaryInstruction()] }
        ];
        Assert(service.Validate(duplicateAnchor).Issues.Any(issue => issue.Code == "Anchor.Duplicate"), "Duplicate Anchors must be rejected.");

        var spu = valid.Clone();
        spu.PatchType = HCanaryPatchType.Spu;
        Assert(service.Validate(spu).Issues.Any(issue => issue.Code == "Hash.Kind"), "SPU Patch Type must require an SPU Hash.");
    }

    private static void RunYamlTests()
    {
        var service = new HCanaryPatchAuthoringService();
        var draft = ValidDraft();
        draft.PatchName = "Reduce \"input\" lag";
        draft.Author = "WAZAAAAA";
        draft.Notes = "ملاحظة عربية\nSecond line";
        draft.Group = "FPS";
        draft.Games.Add(new HCanaryGameTarget { GameName = "Game Two", GameId = "BLES00001", GameVersion = "01.01" });
        draft.Games.Add(new HCanaryGameTarget { GameName = "Game Two", GameId = "BLES00001", GameVersion = "All" });
        draft.Instructions.Add(new HCanaryInstruction { Operation = "be16", Address = "0x0001c314", Value = "0x6000" });
        draft.Anchors.Add(new HCanaryAnchor
        {
            Name = "AnchorDesc",
            Instructions = [new HCanaryInstruction { Operation = "be32", Address = "0x00000000", Value = "0x60000000" }]
        });
        draft.Instructions.Add(new HCanaryInstruction { Operation = "load", Address = "AnchorDesc", Value = string.Empty });

        var result = service.Validate(draft);
        Assert(result.IsValid, "PPU YAML with games, Group, Arabic Notes, and Anchors should validate: " + string.Join(" | ", result.Issues.Select(i => i.Message)));
        var yaml = result.Yaml;
        Assert(yaml.StartsWith("Version: 1.2\r\n", StringComparison.Ordinal), "Version: 1.2 must be the first line.");
        Assert(!yaml.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n'), "Generated YAML must use CRLF only.");
        Assert(yaml.Contains("PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9:", StringComparison.Ordinal), "PPU key should be unchanged.");
        Assert(yaml.Contains("Group: \"FPS\"", StringComparison.Ordinal), "Group should be generated.");
        Assert(yaml.Contains("BLES00001: [ 01.01, All ]", StringComparison.Ordinal), "Multiple versions should share one Game ID mapping.");
        Assert(yaml.Contains("AnchorDesc: &AnchorDesc", StringComparison.Ordinal) && yaml.Contains("- [ load, *AnchorDesc ]", StringComparison.Ordinal), "Anchor definition and load should be generated.");
        Assert(yaml.Contains("ملاحظة عربية", StringComparison.Ordinal), "Arabic Notes must be preserved as UTF-8 text.");
        Assert(yaml.Contains("Reduce \\\"input\\\" lag", StringComparison.Ordinal), "Quoted patch names must be escaped.");

        var spu = ValidDraft();
        spu.PatchType = HCanaryPatchType.Spu;
        spu.ExecutableHash = "SPU-1234567890abcdef1234567890abcdef12345678";
        Assert(service.Validate(spu).IsValid && service.GenerateYaml(spu).Contains("SPU-", StringComparison.Ordinal), "SPU YAML should generate.");

        var experimental = ValidDraft();
        experimental.IsExperimental = true;
        experimental.PatchVersion = "0.1";
        Assert(service.Validate(experimental).IsValid,
            "Experimental classification must remain independent from the PPU/SPU executable type.");
    }

    private static void RunImportEditTests()
    {
        var service = new HCanaryPatchAuthoringService();
        var temp = NewTemp();
        try
        {
            var patches = Path.Combine(temp, "patches");
            var backup = Path.Combine(temp, "backups");
            Directory.CreateDirectory(patches);
            var path = Path.Combine(patches, "imported_patch.yml");
            var first = ValidDraft();
            first.Anchors.Add(new HCanaryAnchor
            {
                Name = "SharedCode",
                Instructions = [new HCanaryInstruction { Operation = "be32", Address = "0x00000020", Value = "0x60000000" }]
            });
            first.Instructions.Add(new HCanaryInstruction { Operation = "load", Address = "SharedCode", Value = string.Empty });
            var saved = service.Save(path, backup, first);
            Assert(saved.PatchCount == 1 && File.Exists(path), "Saving should create imported_patch.yml.");

            var bytes = File.ReadAllBytes(path);
            Assert(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "imported_patch.yml must be UTF-8 without BOM.");
            Assert(File.ReadAllText(path, Encoding.UTF8).Contains("\r\n", StringComparison.Ordinal), "Saved file must use CRLF.");

            var loaded = service.Load(path);
            Assert(loaded.Count == 1, "A saved custom Patch should open.");
            Assert(loaded[0].Draft.Status == HCanaryDraftStatus.Saved, "Saving a Draft should persist its local state as Saved.");
            var edited = loaded[0].Draft;
            Assert(edited.Games.Count == first.Games.Count && edited.Instructions.Count == first.Instructions.Count, "Open/edit should retain Games and Patch instructions.");
            Assert(edited.Anchors.Count == 1 && edited.Anchors[0].Name == "SharedCode", "Open/edit should retain referenced Anchors.");
            edited.Instructions[0].Value = "0x39200001";
            edited.Games.Add(new HCanaryGameTarget { GameName = "Alt Region", GameId = "BLES00001", GameVersion = "01.01" });
            edited.Notes = "edited";
            service.Save(path, backup, edited, loaded[0].Identity);
            var reloaded = service.Load(path).Single();
            Assert(reloaded.Draft.Instructions[0].Value == "0x39200001" && reloaded.Draft.Games.Count == 2, "Structured edits should survive regeneration.");

            var second = ValidDraft();
            second.ExecutableHash = "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            second.PatchName = "Other Patch";
            second.IsExperimental = true;
            second.Status = HCanaryDraftStatus.Testing;
            service.Save(path, backup, second);
            var merged = service.Load(path);
            Assert(merged.Count == 2 && merged.Any(item => item.Draft.PatchName == first.PatchName), "Merge must not delete other imported patches.");
            Assert(merged.Single(item => item.Draft.PatchName == "Other Patch").Draft.Status == HCanaryDraftStatus.Testing,
                "Local Draft/Testing status should persist in separate R3Config metadata.");
            Assert(merged.Single(item => item.Draft.PatchName == "Other Patch").Draft.IsExperimental &&
                   merged.Single(item => item.Draft.PatchName == "Other Patch").Draft.PatchType == HCanaryPatchType.Ppu,
                "Experimental classification must persist separately from the PPU executable type.");
            var localCatalog = new PatchCatalogReader().Read(path, PatchSourceOrigin.Imported).Catalog;
            var classified = service.ApplyLocalMetadata(localCatalog, path);
            Assert(classified.Entries.Single(item => item.Title == "Other Patch").Risk == PatchRisk.Canary,
                "A custom experimental patch should reappear on the experimental patch page after catalog reload.");
            AssertThrows<InvalidOperationException>(() => service.Save(path, backup, second), "Duplicate Patch name and Hash should be detected.");

            service.Delete(path, backup, merged.Single(item => item.Draft.PatchName == "Other Patch").Identity);
            Assert(service.Load(path).Count == 1, "Deleting one custom Patch must preserve other patches.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void RunTransactionTests()
    {
        var temp = NewTemp();
        try
        {
            var path = Path.Combine(temp, "imported_patch.yml");
            var backupRoot = Path.Combine(temp, "backups");
            var baseline = new HCanaryPatchAuthoringService();
            baseline.Save(path, backupRoot, ValidDraft());
            var original = File.ReadAllText(path);
            Assert(Directory.EnumerateFiles(backupRoot, "manifest.json", SearchOption.AllDirectories).Any(), "Save should create a Backup manifest.");

            var failingTransaction = new PatchFileTransactionService(commitFile: (_, _) => throw new IOException("simulated atomic replace failure"));
            var failing = new HCanaryPatchAuthoringService(transaction: failingTransaction);
            var changed = ValidDraft(); changed.PatchName = "Rollback Patch"; changed.ExecutableHash = "PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            AssertThrows<IOException>(() => failing.Save(path, backupRoot, changed), "A failed atomic commit should surface the original failure.");
            Assert(File.ReadAllText(path) == original, "Transaction failure must roll back imported_patch.yml exactly.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void RunViewModelTests()
    {
        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "rpcs3");
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            File.WriteAllText(Path.Combine(root, "rpcs3.exe"), string.Empty);
            var install = new Rpcs3InstallDescriptor(root, Path.Combine(root, "rpcs3.exe"), Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"), Path.Combine(root, "patches"), Path.Combine(root, "config", "patch_config.yml"));
            var game = new Rpcs3GameDescriptor("NPUB30899", "TEKKEN TAG TOURNAMENT 2", "game", "01.01",
                "8645cfa235c9980ff74b5e06388b29e97fdbbed9", null,
                ["8645cfa235c9980ff74b5e06388b29e97fdbbed9"], [], Path.Combine(root, "RPCS3.log"));
            var vm = new HCanaryVm();
            vm.SetContext(install, game);
            Assert(vm.Games.Count == 1 && vm.Games[0].GameId == "NPUB30899", "Selected game should initialize a new authoring draft.");
            Assert(vm.Hashes.Count == 1, "Selected game Hash evidence should populate the chooser.");
            vm.SelectedHash = vm.Hashes[0];
            vm.PatchName = "VM Patch";
            vm.Author = "Author";
            var previewBefore = vm.YamlPreview;
            vm.Instructions[0].Address = "0x0001c310";
            vm.Instructions[0].Value = "0x39200000";
            Assert(vm.YamlPreview != previewBefore && vm.YamlPreview.Contains("0x39200000", StringComparison.Ordinal), "Preview should update when an instruction changes.");
            Assert(vm.IsValid && vm.SaveCommand.CanExecute(null), "CanExecute should allow a complete valid draft.");
            vm.AddInstructionCommand.Execute(null);
            Assert(vm.Instructions.Count == 2, "ViewModel should add instructions.");
            vm.DeleteInstructionCommand.Execute(null);
            Assert(vm.Instructions.Count == 1, "ViewModel should delete the selected instruction.");
            vm.AddGameCommand.Execute(null);
            Assert(vm.Games.Count == 2, "ViewModel should add game targets.");
            vm.DeleteGameCommand.Execute(null);
            Assert(vm.Games.Count == 1, "ViewModel should delete the selected game target.");
            vm.AddAnchorCommand.Execute(null);
            Assert(vm.Anchors.Count == 1, "ViewModel should add Anchors.");
            vm.AddAnchorLoadCommand.Execute(null);
            Assert(vm.Instructions.Any(item => item.Operation == "load"), "ViewModel should add a load reference.");
            vm.SelectedInstruction = vm.Instructions.Last(); vm.DeleteInstructionCommand.Execute(null);

            var draftName = vm.PatchName;
            var instructionOrder = vm.Instructions.Select(item => item.Address).ToArray();
            UiText.SetLang(UiText.EnglishLang); vm.RefreshLanguage();
            UiText.SetLang(UiText.ArabicLang); vm.RefreshLanguage();
            Assert(vm.PatchName == draftName && vm.Instructions.Select(item => item.Address).SequenceEqual(instructionOrder), "Live language switching must preserve the draft and instruction order.");
            vm.SetContext(install, game with { Serial = "BLUS00001", Title = "Other Game" });
            Assert(vm.PatchName == draftName && vm.IsDirty, "Changing the selected game must preserve an active draft.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void RunLocalizationAndViewTests()
    {
        var root = FindRepoRoot();
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "en-US.json")))!;
        var ar = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "ar-SA.json")))!;
        Assert(en.Keys.Order().SequenceEqual(ar.Keys.Order()), "Arabic and English localization keys must match exactly.");
        Assert(en.Keys.Any(key => key.StartsWith("HCanary.Validation.", StringComparison.Ordinal)), "H-Canary validation messages must be localized.");
        var xaml = File.ReadAllText(Path.Combine(root, "App", "Ctrl", "HCan.xaml"));
        Assert(!xaml.Contains("CheckBox", StringComparison.OrdinalIgnoreCase), "H-Canary authoring view must not be an activation checklist.");
        Assert(xaml.Contains("HCanary.Instructions", StringComparison.Ordinal) && xaml.Contains("HCanary.Anchors", StringComparison.Ordinal) && xaml.Contains("YamlPreview", StringComparison.Ordinal),
            "H-Canary view must expose instruction, Anchor, and true YAML preview editing.");
        Assert(xaml.Contains("FlowDirection=\"LeftToRight\"", StringComparison.Ordinal) && xaml.Contains("TextAlignment=\"Left\"", StringComparison.Ordinal),
            "Technical YAML content must remain LTR and left aligned.");
        Assert(!xaml.Contains("Enable Code", StringComparison.OrdinalIgnoreCase) && !xaml.Contains("AnalysisItems", StringComparison.Ordinal),
            "The obsolete patch-browser and compatibility-analysis concepts must be absent.");
    }

    private static HCanaryPatchDraft ValidDraft() => new()
    {
        PatchType = HCanaryPatchType.Ppu,
        ExecutableHash = "PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9",
        PatchName = "Reduce input lag",
        Author = "WAZAAAAA",
        Notes = "Reduce input delay.",
        PatchVersion = "1.0",
        Games = [new HCanaryGameTarget { GameName = "TEKKEN TAG TOURNAMENT 2", GameId = "NPUB30899", GameVersion = "01.01" }],
        Instructions = [new HCanaryInstruction { Operation = "be32", Address = "0x0001c310", Value = "0x39200000" }]
    };

    private static void ExpectIssue(HCanaryPatchAuthoringService service, HCanaryPatchDraft valid, Action<HCanaryPatchDraft> mutate, string code)
    {
        var draft = valid.Clone(); mutate(draft);
        Assert(service.Validate(draft).Issues.Any(issue => issue.Code == code), "Expected validation issue " + code);
    }

    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "R3Config-HCan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("R3Config repository root was not found.");
    }

    private static void AssertThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
