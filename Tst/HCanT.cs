using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

using AppFlow = global::R3Config.AppFlow;

internal static class HCanTst
{
    private static readonly string[] Rpcs3AuthoringOperations =
    [
        "alloc",
        "calloc",
        "jump",
        "jumpl",
        "jumpf",
        "load",
        "byte",
        "le16",
        "le32",
        "le64",
        "bef32",
        "bef64",
        "be16",
        "be32",
        "bd32",
        "be64",
        "bd64",
        "lef32",
        "lef64",
        "bpex",
        "utf8",
        "cutf8",
        "move_file",
        "hide_file"
    ];

    public static void Run()
    {
        Hash_Ext();
        Launch_All();
        Vld_All();
        Yaml_All();
        Imp_Edit();
        Txn_All();
        Vm_All();
        Ui_Lang();
        Console.WriteLine("H-Canary authoring, import, validation, transaction, ViewModel, and localization tests passed.");
    }

    private static void Hash_Ext()
    {
        const string ppu = "8645cfa235c9980ff74b5e06388b29e97fdbbed9";
        const string spu = "1234567890abcdef1234567890abcdef12345678";
        var log = $"noise\nPPU executable hash: PPU-{ppu}\nSPU executable hash: SPU-{spu}\n" +
                  $"PPU executable hash: PPU-{ppu}\nPPU executable hash: PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n";
        var service = new HashExt();
        var result = service.Parse(log, "RPCS3.log");
        Assert(result.IsOk, "Hash parsing should succeed.");
        Assert(result.Hashes.Count == 3, "Hash parsing should keep multiple values and remove duplicates.");
        Assert(result.Hashes[0].Kind == HashKind.Ppu && result.Hashes[0].Value == ppu, "First PPU Hash should preserve useful log order.");
        Assert(result.Hashes[1].Kind == HashKind.Spu && result.Hashes[1].Value == spu, "SPU Hash should be classified separately.");
        Assert(result.Hashes[0].SrcPath == "RPCS3.log" && result.Hashes[0].Line == 2, "Hash source and line should be exposed.");
        Assert(service.Parse("invalid log").Hashes.Count == 0, "A log without executable Hashes should return an empty result.");
        Assert(service.Parse("PPU executable hash: PPU-nothex").Hashes.Count == 0, "Malformed Hash text must be ignored.");

        var temp = NewTemp();
        try
        {
            var logPath = Path.Combine(temp, "RPCS3.log");
            File.WriteAllText(logPath, log);
            Assert(service.Read(logPath).Hashes.Count == 3, "RPCS3.log file reading should extract all Hashes.");
            Assert(!service.Read(Path.Combine(temp, "missing.log")).IsOk, "Missing RPCS3.log should be reported.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void Launch_All()
    {
        var temp = NewTemp();
        try
        {
            var root = Path.Combine(temp, "RPCS3 Root");
            var exePath = Path.Combine(root, "rpcs3.exe");
            Directory.CreateDirectory(Path.Combine(root, "config"));
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            File.WriteAllBytes(exePath, []);

            var install = new Rpcs3InstallDescriptor(
                root,
                exePath,
                Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"),
                Path.Combine(root, "patches"),
                Path.Combine(root, "config", "patch_config.yml"));
            var game = new Rpcs3GameDescriptor(
                "BLES01465",
                "Resident Evil 6 Europe",
                Path.Combine(temp, "Unavailable Library", "Resident Evil 6 (Europe).chd"),
                "01.01",
                null,
                null);
            var launcher = new FakeGameProcessLauncher();
            var useCase = new GameLaunchUseCase(
                new Rpcs3BootPathResolver(),
                launcher);

            var result = useCase.Launch(install, game);
            Assert(result.Started && result.Error == GameLaunchError.None,
                "A selected game with a valid Game ID must produce an RPCS3 launch request.");
            Assert(launcher.ExecutablePath == Path.GetFullPath(exePath) &&
                   launcher.WorkingDirectory == Path.GetFullPath(root) &&
                   launcher.BootTarget == "%RPCS3_GAMEID%:BLES01465",
                "RPCS3 launch must pass its executable, data root, and official GAMEID token without probing the library path.");
            Assert(result.BootTarget == "%RPCS3_GAMEID%:BLES01465" &&
                   !result.BootTarget.Contains(temp, StringComparison.OrdinalIgnoreCase),
                "The launch request must let RPCS3 resolve games.yml instead of exposing a host game path.");

            var missing = useCase.Launch(
                install,
                game with { Serial = "bad/id" });
            Assert(!missing.Started && missing.Error == GameLaunchError.GameIdentityMissing,
                "An invalid Game ID must produce a precise non-launch result without probing a path.");

            var launchCalls = 0;
            var vm = new HCanVm(
                new AppFlow(),
                isRpcs3Running: () => false,
                confirm: (_, _) => false,
                launchGame: (_, _) =>
                {
                    launchCalls++;
                    return new GameLaunchResult(
                        true,
                        GameLaunchError.None,
                        "%RPCS3_GAMEID%:BLES01465");
                });
            vm.SetCtx(install, game);
            Assert(vm.LaunchCmd.CanRun(null),
                "The selected game must enable the explicit launch command.");
            vm.LaunchCmd.Execute(null);
            Assert(launchCalls == 1 &&
                   vm.LogStat == UiText.Fmt("HCanary.Launch.Started", game.Title),
                "Launching from H-Canary must report the selected game and preserve the next log-scan step.");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void Vld_All()
    {
        var service = new HCanaryPatchAuthoringService();
        var valid = GoodDrft();
        var validResult = service.Validate(valid);
        Assert(validResult.IsValid,
            "A complete PPU draft should validate: " + string.Join(" | ", validResult.Issues.Select(issue => issue.Message)));
        AssertOperationContract(HCanaryPatchAuthoringService.Op);

        HasIssue(service, valid, draft => draft.ExeHash = "PPU-abc", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.ExeHash = "PPU-gggggggggggggggggggggggggggggggggggggggg", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.PatName = string.Empty, "PatchName.Required");
        HasIssue(service, valid, draft => draft.PatVer = string.Empty, "PatchVersion.Required");
        HasIssue(service, valid, draft => draft.PatVer = "١.٠", "PatchVersion.Invalid");
        HasIssue(service, valid, draft => draft.Games[0].GameId = string.Empty, "GameId.Required");
        HasIssue(service, valid, draft => draft.Games[0].GameVer = string.Empty, "GameVersion.Required");
        HasIssue(service, valid, draft => draft.Ins.Clear(), "Instructions.Required");
        HasIssue(service, valid, draft => draft.Ins[0].Address = "not-an-address", "Address.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Value = "hello", "Value.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Op = "unknown", "Operation.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Op = "be8", "Operation.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0] = new HCanInst { Op = "load", Address = "Missing" }, "Anchor.Missing");

        HasIssue(service, valid, draft => draft.Ins[0].Address = "1234", "Address.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Address = "0x100000000", "Address.Invalid");

        var decimalInteger = valid.Clone();
        decimalInteger.Ins[0] = new HCanInst { Op = "be16", Address = "0x0001c310", Value = "720" };
        Assert(service.Validate(decimalInteger).IsValid, "RPCS3 decimal integer values should validate.");

        var floatingPoint = valid.Clone();
        floatingPoint.Ins[0] = new HCanInst { Op = "bef32", Address = "0x0001c310", Value = "0.65" };
        Assert(service.Validate(floatingPoint).IsValid, "RPCS3 floating-point values should validate.");

        var hideFile = valid.Clone();
        hideFile.Ins[0] = new HCanInst { Op = "hide_file", Address = "/dev_hdd0/game/NPUB30899/USRDIR/file.bin", Value = string.Empty };
        Assert(service.Validate(hideFile).IsValid, "hide_file should accept a source path and an empty value.");

        var moveFile = valid.Clone();
        moveFile.Ins[0] = new HCanInst { Op = "move_file", Address = "/dev_hdd0/game/NPUB30899/USRDIR/source.bin", Value = "/dev_hdd0/game/NPUB30899/USRDIR/target.bin" };
        Assert(service.Validate(moveFile).IsValid, "move_file should accept source and destination paths.");

        var allTarget = valid.Clone();
        allTarget.Games = [new HCanTgt { GameName = "All", GameId = "All", GameVer = "All" }];
        Assert(service.Validate(allTarget).IsValid, "The official Games: All / All / All target should validate.");
        HasIssue(service, valid, draft =>
        {
            draft.Games[0].GameName = "All";
            draft.Games[0].GameId = "NPUB30899";
        }, "GameTarget.AllPair");

        var duplicateAnchor = valid.Clone();
        duplicateAnchor.Anchors =
        [
            new HCanAnc { Name = "Shared", Ins = [new HCanInst()] },
            new HCanAnc { Name = "Shared", Ins = [new HCanInst()] }
        ];
        Assert(service.Validate(duplicateAnchor).Issues.Any(issue => issue.Code == "Anchor.Duplicate"), "Duplicate Anchors must be rejected.");

        var spu = valid.Clone();
        spu.PatType = HCanType.Spu;
        Assert(service.Validate(spu).Issues.Any(issue => issue.Code == "Hash.Kind"), "SPU Patch Type must require an SPU Hash.");
    }

    private static void Yaml_All()
    {
        var service = new HCanaryPatchAuthoringService();
        var draft = GoodDrft();
        draft.PatName = "Reduce \"input\" lag";
        draft.Author = "WAZAAAAA";
        draft.Notes = "ملاحظة عربية\nSecond line";
        draft.Group = "FPS";
        draft.Games.Add(new HCanTgt { GameName = "Game Two", GameId = "BLES00001", GameVer = "01.01" });
        draft.Games.Add(new HCanTgt { GameName = "Game Two", GameId = "BLES00001", GameVer = "All" });
        draft.Ins.Add(new HCanInst { Op = "be16", Address = "0x0001c314", Value = "0x6000" });
        draft.Anchors.Add(new HCanAnc
        {
            Name = "AnchorDesc",
            Ins = [new HCanInst { Op = "be32", Address = "0x00000000", Value = "0x60000000" }]
        });
        draft.Ins.Add(new HCanInst { Op = "load", Address = "AnchorDesc", Value = string.Empty });

        var result = service.Validate(draft);
        Assert(result.IsValid, "PPU YAML with games, Group, Arabic Notes, and Anchors should validate: " + string.Join(" | ", result.Issues.Select(i => i.Message)));
        var yaml = result.Yaml;
        Assert(yaml.StartsWith("Version: 1.2\r\n", StringComparison.Ordinal), "Version: 1.2 must be the first line.");
        AssertCrLfOnly(yaml, "Generated YAML must use CRLF only.");
        Assert(yaml.Contains("PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9:", StringComparison.Ordinal), "PPU key should be unchanged.");
        Assert(yaml.Contains("Group: \"FPS\"", StringComparison.Ordinal), "Group should be generated.");
        Assert(yaml.Contains("BLES00001: [ 01.01, All ]", StringComparison.Ordinal), "Multiple versions should share one Game ID mapping.");
        Assert(yaml.Contains("AnchorDesc: &AnchorDesc", StringComparison.Ordinal) && yaml.Contains("- [ load, *AnchorDesc ]", StringComparison.Ordinal), "Anchor definition and load should be generated.");
        Assert(yaml.Contains("ملاحظة عربية", StringComparison.Ordinal), "Arabic Notes must be preserved as UTF-8 text.");
        Assert(yaml.Contains("Reduce \\\"input\\\" lag", StringComparison.Ordinal), "Quoted patch names must be escaped.");

        var fileOperations = GoodDrft();
        fileOperations.Ins =
        [
            new HCanInst { Op = "hide_file", Address = "/dev_hdd0/game/NPUB30899/USRDIR/file.bin", Value = string.Empty },
            new HCanInst { Op = "move_file", Address = "/dev_hdd0/game/NPUB30899/USRDIR/source.bin", Value = "/dev_hdd0/game/NPUB30899/USRDIR/target.bin" }
        ];
        var fileYaml = service.Validate(fileOperations);
        Assert(fileYaml.IsValid &&
               fileYaml.Yaml.Contains("[ hide_file, \"/dev_hdd0/game/NPUB30899/USRDIR/file.bin\", \"\" ]", StringComparison.Ordinal) &&
               fileYaml.Yaml.Contains("[ move_file, \"/dev_hdd0/game/NPUB30899/USRDIR/source.bin\", \"/dev_hdd0/game/NPUB30899/USRDIR/target.bin\" ]", StringComparison.Ordinal),
            "File operations must generate quoted YAML paths and preserve hide_file's empty value.");

        var allTarget = GoodDrft();
        allTarget.Games = [new HCanTgt { GameName = "all", GameId = "all", GameVer = "all" }];
        var allYaml = service.Validate(allTarget);
        Assert(allYaml.IsValid && allYaml.Yaml.Contains("All: [ All ]", StringComparison.Ordinal),
            "The All target must be emitted with RPCS3's canonical spelling.");

        var spu = GoodDrft();
        spu.PatType = HCanType.Spu;
        spu.ExeHash = "SPU-1234567890abcdef1234567890abcdef12345678";
        Assert(service.Validate(spu).IsValid && service.Generate(spu).Contains("SPU-", StringComparison.Ordinal), "SPU YAML should generate.");

        var experimental = GoodDrft();
        experimental.IsExp = true;
        experimental.PatVer = "0.1";
        Assert(service.Validate(experimental).IsValid,
            "Experimental classification must remain independent from the PPU/SPU executable type.");
    }

    private static void Imp_Edit()
    {
        var service = new HCanaryPatchAuthoringService();
        var temp = NewTemp();
        try
        {
            var patches = Path.Combine(temp, "patches");
            var backup = Path.Combine(temp, "backups");
            Directory.CreateDirectory(patches);
            var path = Path.Combine(patches, "imported_patch.yml");
            var unmanagedPath = Path.Combine(temp, "unmanaged", "imported_patch.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(unmanagedPath)!);
            File.WriteAllText(unmanagedPath, service.Generate(GoodDrft()));
            Assert(service.Load(unmanagedPath).Count == 0,
                "The editor must not offer unmanaged imported patches that it cannot round-trip without losing unsupported fields.");
            var first = GoodDrft();
            first.Anchors.Add(new HCanAnc
            {
                Name = "SharedCode",
                Ins = [new HCanInst { Op = "be32", Address = "0x00000020", Value = "0x60000000" }]
            });
            first.Ins.Add(new HCanInst { Op = "load", Address = "SharedCode", Value = string.Empty });
            var saved = service.Save(path, backup, first);
            Assert(saved.PatNum == 1 && File.Exists(path), "Saving should create imported_patch.yml.");
            Assert(File.Exists(Path.Combine(patches, HCanaryPatchAuthoringService.MetaName)) &&
                   !File.Exists(Path.Combine(patches, HCanaryPatchAuthoringService.OldName)),
                "A new H-Canary save must write only r3hcan.json.");

            var bytes = File.ReadAllBytes(path);
            Assert(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "imported_patch.yml must be UTF-8 without BOM.");
            AssertCrLfOnly(File.ReadAllText(path, Encoding.UTF8), "Saved file must use CRLF only.");

            var loaded = service.Load(path);
            Assert(loaded.Count == 1, "A saved custom Patch should open.");
            Assert(loaded[0].Draft.Status == DraftSt.Saved, "Saving a Draft should persist its local state as Saved.");
            var edited = loaded[0].Draft;
            Assert(edited.Games.Count == first.Games.Count && edited.Ins.Count == first.Ins.Count, "Open/edit should retain Games and Patch instructions.");
            Assert(edited.Anchors.Count == 1 && edited.Anchors[0].Name == "SharedCode", "Open/edit should retain referenced Anchors.");
            edited.Ins[0].Value = "0x39200001";
            edited.Games.Add(new HCanTgt { GameName = "Alt Region", GameId = "BLES00001", GameVer = "01.01" });
            edited.Notes = "edited";
            service.Save(path, backup, edited, loaded[0].Identity);
            var reloaded = service.Load(path).Single();
            Assert(reloaded.Draft.Ins[0].Value == "0x39200001" && reloaded.Draft.Games.Count == 2, "Structured edits should survive regeneration.");

            var second = GoodDrft();
            second.ExeHash = "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            second.PatName = "Other Patch";
            second.IsExp = true;
            second.Status = DraftSt.Testing;
            service.Save(path, backup, second);
            var merged = service.Load(path);
            Assert(merged.Count == 2 && merged.Any(item => item.Draft.PatName == first.PatName), "Merge must not delete other imported patches.");
            Assert(merged.Single(item => item.Draft.PatName == "Other Patch").Draft.Status == DraftSt.Testing,
                "Local Draft/Testing status should persist in separate R3Config metadata.");
            Assert(merged.Single(item => item.Draft.PatName == "Other Patch").Draft.IsExp &&
                   merged.Single(item => item.Draft.PatName == "Other Patch").Draft.PatType == HCanType.Ppu,
                "Experimental classification must persist separately from the PPU executable type.");
            var localCatalog = new R3Config.Svc.ReadSvc().Read(path, PatSrc.Imported).Catalog;
            var classified = service.Apply(localCatalog, path);
            var classifiedOther = classified.Entries.Single(item => item.Title == "Other Patch");
            Assert(classifiedOther.Risk == PatRisk.Canary &&
                   classifiedOther.Class == PatClass.Canary &&
                   classifiedOther.HashKind == HashKind.Ppu &&
                   !classifiedOther.PatType.Equals("Canary", StringComparison.OrdinalIgnoreCase),
                "H-Canary metadata must classify stability without replacing the PPU executable kind or functional category.");
            MustFail<InvalidOperationException>(() => service.Save(path, backup, second), "Duplicate Patch name and Hash should be detected.");

            service.Delete(path, backup, merged.Single(item => item.Draft.PatName == "Other Patch").Identity);
            Assert(service.Load(path).Count == 1, "Deleting one custom Patch must preserve other patches.");

            var oldDir = Path.Combine(temp, "old");
            Directory.CreateDirectory(oldDir);
            var oldPat = Path.Combine(oldDir, "imported_patch.yml");
            var oldBkp = Path.Combine(temp, "old-bkp");
            service.Save(oldPat, oldBkp, GoodDrft());
            var newMeta = Path.Combine(oldDir, HCanaryPatchAuthoringService.MetaName);
            var oldMeta = Path.Combine(oldDir, HCanaryPatchAuthoringService.OldName);
            File.Move(newMeta, oldMeta);
            var oldLoad = service.Load(oldPat).Single();
            Assert(oldLoad.Draft.PatName == GoodDrft().PatName,
                "Legacy r3config_hcanary.json metadata must remain readable.");
            oldLoad.Draft.Notes = "migrated";
            service.Save(oldPat, oldBkp, oldLoad.Draft, oldLoad.Identity);
            Assert(File.Exists(newMeta) && File.Exists(oldMeta),
                "Saving legacy H-Canary data must transactionally write r3hcan.json without deleting the old file.");
            Assert(service.Load(oldPat).Single().Draft.Notes == "migrated",
                "Once written, r3hcan.json must be authoritative over the retained legacy metadata.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void Txn_All()
    {
        var temp = NewTemp();
        try
        {
            var path = Path.Combine(temp, "imported_patch.yml");
            var backupRoot = Path.Combine(temp, "backups");
            var baseline = new HCanaryPatchAuthoringService();
            baseline.Save(path, backupRoot, GoodDrft());
            var original = File.ReadAllText(path);
            var newMeta = Path.Combine(temp, HCanaryPatchAuthoringService.MetaName);
            var oldMeta = Path.Combine(temp, HCanaryPatchAuthoringService.OldName);
            File.Move(newMeta, oldMeta);
            var oldBytes = File.ReadAllBytes(oldMeta);
            Assert(Directory.EnumerateFiles(backupRoot, "manifest.json", SearchOption.AllDirectories).Any(), "Save should create a Backup manifest.");

            var failingTransaction = new PatchFileTransactionService(commitFile: (_, _) => throw new IOException("simulated atomic replace failure"));
            var failing = new HCanaryPatchAuthoringService(transaction: failingTransaction);
            var changed = GoodDrft(); changed.PatName = "Rollback Patch"; changed.ExeHash = "PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            MustFail<IOException>(() => failing.Save(path, backupRoot, changed), "A failed atomic commit should surface the original failure.");
            Assert(File.ReadAllText(path) == original, "Transaction failure must roll back imported_patch.yml exactly.");
            Assert(!File.Exists(newMeta) &&
                   File.Exists(oldMeta) &&
                   File.ReadAllBytes(oldMeta).SequenceEqual(oldBytes),
                "A failed H-Canary migration must leave the legacy metadata byte-for-byte unchanged.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void Vm_All()
    {
        var temp = NewTemp();
        var originalLang = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.ArabLang);
            var root = Path.Combine(temp, "rpcs3");
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            File.WriteAllText(Path.Combine(root, "rpcs3.exe"), string.Empty);
            var install = new Rpcs3InstallDescriptor(root, Path.Combine(root, "rpcs3.exe"), Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"), Path.Combine(root, "patches"), Path.Combine(root, "config", "patch_config.yml"));
            var game = new Rpcs3GameDescriptor("NPUB30899", "TEKKEN TAG TOURNAMENT 2", "game", "01.01",
                "8645cfa235c9980ff74b5e06388b29e97fdbbed9", null,
                ["8645cfa235c9980ff74b5e06388b29e97fdbbed9"], [], Path.Combine(root, "RPCS3.log"));
            var vm = new HCanVm(
                new AppFlow(),
                isRpcs3Running: () => false,
                confirm: (_, _) => false);
            vm.SetCtx(install, game);
            Assert(vm.Games.Count == 1 && vm.Games[0].GameId == "NPUB30899", "Selected game should initialize a new authoring draft.");
            Assert(vm.Hashes.Count == 1, "Selected game Hash evidence should populate the chooser.");
            Assert(!vm.HasHashInput, "A new draft should not show a Hash validation error before the user selects or enters a Hash.");
            Assert(!vm.CanAdvanceTarget && !vm.CanAdvanceDetails && !vm.CanAdvanceInstructions,
                "A new draft must not advance before the required target evidence is valid.");
            vm.SelHash = vm.Hashes[0];
            Assert(vm.HasHashInput, "Selecting discovered Hash evidence should reveal field validation state.");
            vm.PatName = "VM Patch";
            vm.Author = "Author";
            var previewBefore = vm.Preview;
            vm.Ins[0].Address = "0x0001c310";
            vm.Ins[0].Value = "0x39200000";
            Assert(vm.Preview != previewBefore && vm.Preview.Contains("0x39200000", StringComparison.Ordinal), "Preview should update when an instruction changes.");
            Assert(vm.CanAdvanceTarget && vm.CanAdvanceDetails && vm.CanAdvanceInstructions,
                "A complete valid draft must unlock every authoring step.");
            Assert(vm.IsValid && vm.SaveCmd.CanRun(null),
                $"CanExecute should allow a complete valid draft (IsValid={vm.IsValid}, CanMut={vm.CanMut}).");
            Assert(vm.SaveHint == UiText.GetText("HCanary.SaveHint.Ready") && vm.IsStoredEmpty,
                "A valid new draft should explain that saving is ready and that no stored patches exist.");
            vm.SetAvail(isBusy: false, isRpcs3Running: true);
            Assert(!vm.SaveCmd.CanRun(null) && vm.SaveHint == UiText.GetText("HCanary.SaveHint.Rpcs3Running"),
                "The save hint and CanExecute must both report the RPCS3 running guard.");
            vm.SetAvail(isBusy: false, isRpcs3Running: false);
            vm.AddICmd.Execute(null);
            Assert(vm.Ins.Count == 2, "ViewModel should add instructions.");
            vm.DelICmd.Execute(null);
            Assert(vm.Ins.Count == 1, "ViewModel should delete the selected instruction.");
            vm.AddGmCmd.Execute(null);
            Assert(vm.Games.Count == 2, "ViewModel should add game targets.");
            vm.DelGmCmd.Execute(null);
            Assert(vm.Games.Count == 1, "ViewModel should delete the selected game target.");
            vm.AddACmd.Execute(null);
            Assert(vm.Anchors.Count == 1, "ViewModel should add Anchors.");
            vm.AncLoad.Execute(null);
            Assert(vm.Ins.Any(item => item.Op == "load"), "ViewModel should add a load reference.");
            vm.SelIns = vm.Ins.Last(); vm.DelICmd.Execute(null);

            vm.PatVer = "bad";
            vm.Games[0].GameVer = "bad";
            vm.GameTgt = vm.Games[0];
            vm.SelIns = vm.Ins[0];
            var selectedAnchorItem = vm.Anchors[0];
            vm.SelAnc = selectedAnchorItem;
            vm.AncIns = selectedAnchorItem.Ins[0];
            vm.SelPat = new HCanStor("stored", "Stored patch", GoodDrft());

            var draftName = vm.PatName;
            var preview = vm.Preview;
            var games = JsonSerializer.Serialize(vm.Games.Select(item => item.ToModel()));
            var instructions = JsonSerializer.Serialize(vm.Ins.Select(item => item.ToModel()));
            var anchors = JsonSerializer.Serialize(vm.Anchors.Select(item => item.ToModel()));
            var selectedGame = vm.GameTgt;
            var selectedInstruction = vm.SelIns;
            var selectedAnchor = vm.SelAnc;
            var selectedAnchorInstruction = vm.AncIns;
            var selectedStoredPatch = vm.SelPat;
            var dirty = vm.IsDirty;
            var arabicMessage = vm.Msg;
            var arabicLogStatus = vm.LogStat;
            var arabicValidation = vm.VldSum;
            var arabicVersionError = vm.VerErr;
            var arabicGameError = vm.Games[0].Error;
            var filesBefore = FileState(root);

            UiText.SetLang(UiText.EnLang);
            vm.SetLang();

            Assert(vm.Msg == UiText.GetText("HCanary.Message.NewDraft") && vm.Msg != arabicMessage,
                "H-Canary message must refresh from its localization key.");
            Assert(vm.LogStat == UiText.Fmt("HCanary.Log.Found", vm.Hashes.Count, game.HashSrc!) && vm.LogStat != arabicLogStatus,
                "H-Canary log status must refresh from its localization key and arguments.");
            Assert(vm.VldSum != arabicValidation && vm.VerErr != arabicVersionError && vm.Games[0].Error != arabicGameError,
                "Validation summary and field errors must refresh without re-validating the draft.");
            Assert(vm.PatName == draftName && vm.Preview == preview && vm.IsDirty == dirty,
                "Language refresh must preserve draft values, preview, and dirty state.");
            Assert(JsonSerializer.Serialize(vm.Games.Select(item => item.ToModel())) == games &&
                   JsonSerializer.Serialize(vm.Ins.Select(item => item.ToModel())) == instructions &&
                   JsonSerializer.Serialize(vm.Anchors.Select(item => item.ToModel())) == anchors,
                "Language refresh must preserve games, instructions, and anchors.");
            Assert(ReferenceEquals(vm.GameTgt, selectedGame) &&
                   ReferenceEquals(vm.SelIns, selectedInstruction) &&
                   ReferenceEquals(vm.SelAnc, selectedAnchor) &&
                   ReferenceEquals(vm.AncIns, selectedAnchorInstruction) &&
                   ReferenceEquals(vm.SelPat, selectedStoredPatch),
                "Language refresh must preserve every H-Canary selection.");
            Assert(FileState(root).SequenceEqual(filesBefore),
                "Language refresh must not mutate the RPCS3 tree.");

            UiText.SetLang(UiText.ArabLang);
            vm.SetLang();
            Assert(vm.Msg == arabicMessage && vm.LogStat == arabicLogStatus &&
                   vm.VldSum == arabicValidation && vm.VerErr == arabicVersionError &&
                   vm.Games[0].Error == arabicGameError,
                "Returning to Arabic must restore all localized H-Canary presentation text.");

            vm.SetCtx(install, game with { Serial = "BLUS00001", Title = "Other Game" });
            Assert(vm.PatName == draftName && vm.IsDirty, "Changing the selected game must preserve an active draft.");
        }
        finally
        {
            UiText.SetLang(originalLang);
            Directory.Delete(temp, true);
        }
    }

    private static void Ui_Lang()
    {
        var root = RepoRoot();
        var appRoot = Path.Combine(root, "App");
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(appRoot, "Lang", "en-US.json")))!;
        var ar = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(appRoot, "Lang", "ar-SA.json")))!;
        Assert(en.Keys.Order().SequenceEqual(ar.Keys.Order()), "Arabic and English localization keys must match exactly.");
        Assert(en.Keys.Any(key => key.StartsWith("HCanary.Validation.", StringComparison.Ordinal)), "H-Canary validation messages must be localized.");

        var xamlKeys = Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"\{lang:TextExt\s+Key=([^},\s]+)",
                RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value));
        var codeKeys = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"""((?:Adv|HCanary)\.[A-Za-z0-9_.]+)""",
                RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value))
            .Where(key => !key.EndsWith(".", StringComparison.Ordinal));
        var referencedKeys = xamlKeys
            .Concat(codeKeys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingArabic = referencedKeys.Where(key => !ar.ContainsKey(key)).ToArray();
        var missingEnglish = referencedKeys.Where(key => !en.ContainsKey(key)).ToArray();
        Assert(missingArabic.Length == 0, "Arabic localization is missing UI keys: " + FormatItems(missingArabic));
        Assert(missingEnglish.Length == 0, "English localization is missing UI keys: " + FormatItems(missingEnglish));

        var project = File.ReadAllText(Path.Combine(appRoot, "R3App.csproj"));
        Assert(project.Contains("Lang\\ar-SA.json\" CopyToOutputDirectory=\"Always\"", StringComparison.Ordinal) &&
               project.Contains("Lang\\en-US.json\" CopyToOutputDirectory=\"Always\"", StringComparison.Ordinal),
            "Language resources must always refresh the build output so stale JSON cannot expose raw keys.");

        var xaml = File.ReadAllText(Path.Combine(appRoot, "Ctrl", "HCan.xaml"));
        var hCanCode = File.ReadAllText(Path.Combine(appRoot, "Ctrl", "HCan.xaml.cs"));
        var advancedXaml = File.ReadAllText(Path.Combine(appRoot, "View", "Adv.xaml"));
        var advancedCode = File.ReadAllText(Path.Combine(appRoot, "View", "Adv.xaml.cs"));
        Assert(!xaml.Contains("CheckBox", StringComparison.OrdinalIgnoreCase), "H-Canary authoring view must not be an activation checklist.");
        Assert(xaml.Contains("HCanary.Step.Target", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Step.Details", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Step.Instructions", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Step.Review", StringComparison.Ordinal),
            "H-Canary must expose the four-step authoring workflow.");
        Assert(xaml.Contains("HCanary.Workflow.Title", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Start.Game.Title", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Start.Hash.Title", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Start.Game.Missing", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Start.Game.Return", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Launch.Action", StringComparison.Ordinal) &&
               xaml.Contains("Command=\"{Binding HCanary.LaunchCmd}\"", StringComparison.Ordinal) &&
               xaml.Contains("Binding HCanary.HasGame", StringComparison.Ordinal) &&
               xaml.Contains("Binding HCanary.HasHash", StringComparison.Ordinal) &&
               hCanCode.Contains("ReturnToGames", StringComparison.Ordinal),
            "H-Canary must explain where to start and progressively reveal game and Hash evidence.");
        Assert(advancedXaml.Contains("<controls:HCanView", StringComparison.Ordinal) &&
               advancedXaml.Contains("x:Name=\"HCanaryWorkspace\"", StringComparison.Ordinal) &&
               advancedXaml.Contains("x:Name=\"AdvancedNavigation\"", StringComparison.Ordinal) &&
               advancedXaml.Contains("x:Name=\"RecoveryWorkspace\"", StringComparison.Ordinal) &&
               advancedXaml.Contains("x:Name=\"DiagnosticsWorkspace\"", StringComparison.Ordinal) &&
               !advancedXaml.Contains("Key=Adv.OpenHCanary", StringComparison.Ordinal) &&
               !advancedCode.Contains("OpenHCanary", StringComparison.Ordinal) &&
               !advancedCode.Contains("BackToTools", StringComparison.Ordinal) &&
               !advancedCode.Contains("new HCanDlg", StringComparison.Ordinal) &&
               !advancedCode.Contains("new HCanVm", StringComparison.Ordinal) &&
               !hCanCode.Contains("new HCanVm", StringComparison.Ordinal),
            "Advanced Tools must open directly on the existing HCanVm workspace and expose recovery and diagnostics through one navigation rail.");
        Assert(xaml.Contains("x:Name=\"SavedDrawer\"", StringComparison.Ordinal) &&
               xaml.Contains("x:Name=\"TargetsPanel\"", StringComparison.Ordinal) &&
               xaml.Contains("x:Name=\"AnchorsPanel\"", StringComparison.Ordinal) &&
               !hCanCode.Contains("HCanSavedDlg", StringComparison.Ordinal) &&
               !hCanCode.Contains("HCanTargetsDlg", StringComparison.Ordinal) &&
               !hCanCode.Contains("HCanAnchorsDlg", StringComparison.Ordinal),
            "Saved patches, target versions, and advanced references must remain inside the H-Canary workspace.");
        Assert(xaml.Contains("x:Name=\"NextButton\"", StringComparison.Ordinal) &&
               xaml.Contains("Command=\"{Binding HCanary.OpenCmd}\"", StringComparison.Ordinal) &&
               hCanCode.Contains("CanAdvanceThrough", StringComparison.Ordinal),
            "H-Canary navigation and saved-patch actions must visibly enforce their CanExecute state.");
        Assert(xaml.Contains("HCanary.Instructions", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Anchors", StringComparison.Ordinal) &&
               xaml.Contains("HCanary.Preview", StringComparison.Ordinal),
            "H-Canary view must expose instructions, advanced references, and true YAML preview editing.");
        Assert(xaml.Contains("FlowDirection=\"LeftToRight\"", StringComparison.Ordinal) && xaml.Contains("TextAlignment=\"Left\"", StringComparison.Ordinal),
            "Technical YAML content must remain LTR and left aligned.");
        Assert(!xaml.Contains("Enable Code", StringComparison.OrdinalIgnoreCase) && !xaml.Contains("AnalysisItems", StringComparison.Ordinal),
            "The obsolete patch-browser and compatibility-analysis concepts must be absent.");
    }


    private static string[] FileState(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var bytes = File.ReadAllBytes(path);
                return $"{relative}|{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))}";
            })
            .ToArray();
    }

    private sealed class FakeGameProcessLauncher : IRpcs3GameProcessLauncher
    {
        public string? ExecutablePath { get; private set; }
        public string? WorkingDirectory { get; private set; }
        public string? BootTarget { get; private set; }

        public void Start(
            string executablePath,
            string workingDirectory,
            string bootTarget)
        {
            ExecutablePath = executablePath;
            WorkingDirectory = workingDirectory;
            BootTarget = bootTarget;
        }
    }

    private static HCanDrft GoodDrft() => new()
    {
        PatType = HCanType.Ppu,
        ExeHash = "PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9",
        PatName = "Reduce input lag",
        Author = "WAZAAAAA",
        Notes = "Reduce input delay.",
        PatVer = "1.0",
        Games = [new HCanTgt { GameName = "TEKKEN TAG TOURNAMENT 2", GameId = "NPUB30899", GameVer = "01.01" }],
        Ins = [new HCanInst { Op = "be32", Address = "0x0001c310", Value = "0x39200000" }]
    };

    private static void AssertOperationContract(IEnumerable<string> actualOperations)
    {
        var actual = actualOperations.ToArray();
        if (actual.SequenceEqual(Rpcs3AuthoringOperations, StringComparer.Ordinal))
        {
            return;
        }

        var missing = Rpcs3AuthoringOperations.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(Rpcs3AuthoringOperations, StringComparer.Ordinal).ToArray();
        var duplicates = actual
            .GroupBy(operation => operation, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        var firstMismatch = -1;
        var comparedLength = Math.Min(actual.Length, Rpcs3AuthoringOperations.Length);
        for (var index = 0; index < comparedLength; index++)
        {
            if (!string.Equals(actual[index], Rpcs3AuthoringOperations[index], StringComparison.Ordinal))
            {
                firstMismatch = index;
                break;
            }
        }

        var mismatch = firstMismatch >= 0
            ? $"First order mismatch at index {firstMismatch}: expected '{Rpcs3AuthoringOperations[firstMismatch]}', actual '{actual[firstMismatch]}'."
            : actual.Length == Rpcs3AuthoringOperations.Length
                ? "The operation order differs."
                : $"Operation count differs: expected {Rpcs3AuthoringOperations.Length}, actual {actual.Length}.";

        throw new InvalidOperationException(
            "The H-Canary authoring operation contract does not match RPCS3. " +
            mismatch +
            $" Missing: {FormatItems(missing)}." +
            $" Unexpected: {FormatItems(unexpected)}." +
            $" Duplicates: {FormatItems(duplicates)}." +
            $" Expected: [{string.Join(", ", Rpcs3AuthoringOperations)}]." +
            $" Actual: [{string.Join(", ", actual)}].");
    }

    private static void AssertCrLfOnly(string value, string message)
    {
        var remainder = value.Replace("\r\n", string.Empty, StringComparison.Ordinal);
        Assert(!remainder.Contains('\r') && !remainder.Contains('\n'), message);
    }

    private static string FormatItems(IEnumerable<string> items)
    {
        var values = items.ToArray();
        return values.Length == 0 ? "<none>" : string.Join(", ", values);
    }

    private static void HasIssue(HCanaryPatchAuthoringService service, HCanDrft valid, Action<HCanDrft> mutate, string code)
    {
        var draft = valid.Clone(); mutate(draft);
        Assert(service.Validate(draft).Issues.Any(issue => issue.Code == code), "Expected validation issue " + code);
    }

    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "R3Config-HCan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("R3Config repository root was not found.");
    }

    private static void MustFail<T>(Action action, string message) where T : Exception
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
