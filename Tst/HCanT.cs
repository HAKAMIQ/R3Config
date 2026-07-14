using System.Text;
using System.Text.Json;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

internal static class HCanTst
{
    public static void Run()
    {
        Hash_Ext();
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

    private static void Vld_All()
    {
        var service = new PatAuth();
        var valid = GoodDrft();
        Assert(service.Validate(valid).IsValid, "A complete PPU draft should validate.");

        HasIssue(service, valid, draft => draft.ExeHash = "PPU-abc", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.ExeHash = "PPU-gggggggggggggggggggggggggggggggggggggggg", "Hash.Invalid");
        HasIssue(service, valid, draft => draft.PatName = string.Empty, "PatchName.Required");
        HasIssue(service, valid, draft => draft.PatVer = string.Empty, "PatchVersion.Required");
        HasIssue(service, valid, draft => draft.PatVer = "١.٠", "PatchVersion.Invalid");
        HasIssue(service, valid, draft => draft.Games[0].GameId = string.Empty, "GameId.Required");
        HasIssue(service, valid, draft => draft.Games[0].GameVer = string.Empty, "GameVersion.Required");
        HasIssue(service, valid, draft => draft.Ins.Clear(), "Instructions.Required");
        HasIssue(service, valid, draft => draft.Ins[0].Address = "1234", "Address.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Value = "hello", "Value.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0].Op = "unknown", "Operation.Invalid");
        HasIssue(service, valid, draft => draft.Ins[0] = new HCanInst { Op = "load", Address = "Missing" }, "Anchor.Missing");

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
        var service = new PatAuth();
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
        Assert(!yaml.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n'), "Generated YAML must use CRLF only.");
        Assert(yaml.Contains("PPU-8645cfa235c9980ff74b5e06388b29e97fdbbed9:", StringComparison.Ordinal), "PPU key should be unchanged.");
        Assert(yaml.Contains("Group: \"FPS\"", StringComparison.Ordinal), "Group should be generated.");
        Assert(yaml.Contains("BLES00001: [ 01.01, All ]", StringComparison.Ordinal), "Multiple versions should share one Game ID mapping.");
        Assert(yaml.Contains("AnchorDesc: &AnchorDesc", StringComparison.Ordinal) && yaml.Contains("- [ load, *AnchorDesc ]", StringComparison.Ordinal), "Anchor definition and load should be generated.");
        Assert(yaml.Contains("ملاحظة عربية", StringComparison.Ordinal), "Arabic Notes must be preserved as UTF-8 text.");
        Assert(yaml.Contains("Reduce \\\"input\\\" lag", StringComparison.Ordinal), "Quoted patch names must be escaped.");

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
        var service = new PatAuth();
        var temp = NewTemp();
        try
        {
            var patches = Path.Combine(temp, "patches");
            var backup = Path.Combine(temp, "backups");
            Directory.CreateDirectory(patches);
            var path = Path.Combine(patches, "imported_patch.yml");
            var first = GoodDrft();
            first.Anchors.Add(new HCanAnc
            {
                Name = "SharedCode",
                Ins = [new HCanInst { Op = "be32", Address = "0x00000020", Value = "0x60000000" }]
            });
            first.Ins.Add(new HCanInst { Op = "load", Address = "SharedCode", Value = string.Empty });
            var saved = service.Save(path, backup, first);
            Assert(saved.PatNum == 1 && File.Exists(path), "Saving should create imported_patch.yml.");
            Assert(File.Exists(Path.Combine(patches, PatAuth.MetaName)) &&
                   !File.Exists(Path.Combine(patches, PatAuth.OldName)),
                "A new H-Canary save must write only r3hcan.json.");

            var bytes = File.ReadAllBytes(path);
            Assert(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "imported_patch.yml must be UTF-8 without BOM.");
            Assert(File.ReadAllText(path, Encoding.UTF8).Contains("\r\n", StringComparison.Ordinal), "Saved file must use CRLF.");

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
            Assert(classified.Entries.Single(item => item.Title == "Other Patch").Risk == PatRisk.Canary,
                "A custom experimental patch should reappear on the experimental patch page after catalog reload.");
            MustFail<InvalidOperationException>(() => service.Save(path, backup, second), "Duplicate Patch name and Hash should be detected.");

            service.Delete(path, backup, merged.Single(item => item.Draft.PatName == "Other Patch").Identity);
            Assert(service.Load(path).Count == 1, "Deleting one custom Patch must preserve other patches.");

            var oldDir = Path.Combine(temp, "old");
            Directory.CreateDirectory(oldDir);
            var oldPat = Path.Combine(oldDir, "imported_patch.yml");
            var oldBkp = Path.Combine(temp, "old-bkp");
            service.Save(oldPat, oldBkp, GoodDrft());
            var newMeta = Path.Combine(oldDir, PatAuth.MetaName);
            var oldMeta = Path.Combine(oldDir, PatAuth.OldName);
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
            var baseline = new PatAuth();
            baseline.Save(path, backupRoot, GoodDrft());
            var original = File.ReadAllText(path);
            var newMeta = Path.Combine(temp, PatAuth.MetaName);
            var oldMeta = Path.Combine(temp, PatAuth.OldName);
            File.Move(newMeta, oldMeta);
            var oldBytes = File.ReadAllBytes(oldMeta);
            Assert(Directory.EnumerateFiles(backupRoot, "manifest.json", SearchOption.AllDirectories).Any(), "Save should create a Backup manifest.");

            var failingTransaction = new PatTxn(commitFile: (_, _) => throw new IOException("simulated atomic replace failure"));
            var failing = new PatAuth(transaction: failingTransaction);
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
        try
        {
            var root = Path.Combine(temp, "rpcs3");
            Directory.CreateDirectory(Path.Combine(root, "patches"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            File.WriteAllText(Path.Combine(root, "rpcs3.exe"), string.Empty);
            var install = new R3Inst(root, Path.Combine(root, "rpcs3.exe"), Path.Combine(root, "config"),
                Path.Combine(root, "config", "custom_configs"), Path.Combine(root, "patches"), Path.Combine(root, "config", "patch_config.yml"));
            var game = new R3Game("NPUB30899", "TEKKEN TAG TOURNAMENT 2", "game", "01.01",
                "8645cfa235c9980ff74b5e06388b29e97fdbbed9", null,
                ["8645cfa235c9980ff74b5e06388b29e97fdbbed9"], [], Path.Combine(root, "RPCS3.log"));
            var vm = new HCanVm();
            vm.SetCtx(install, game);
            Assert(vm.Games.Count == 1 && vm.Games[0].GameId == "NPUB30899", "Selected game should initialize a new authoring draft.");
            Assert(vm.Hashes.Count == 1, "Selected game Hash evidence should populate the chooser.");
            vm.SelHash = vm.Hashes[0];
            vm.PatName = "VM Patch";
            vm.Author = "Author";
            var previewBefore = vm.Preview;
            vm.Ins[0].Address = "0x0001c310";
            vm.Ins[0].Value = "0x39200000";
            Assert(vm.Preview != previewBefore && vm.Preview.Contains("0x39200000", StringComparison.Ordinal), "Preview should update when an instruction changes.");
            Assert(vm.IsValid && vm.SaveCmd.CanRun(null), "CanExecute should allow a complete valid draft.");
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

            var draftName = vm.PatName;
            var instructionOrder = vm.Ins.Select(item => item.Address).ToArray();
            UiText.SetLang(UiText.EnLang); vm.SetLang();
            UiText.SetLang(UiText.ArabLang); vm.SetLang();
            Assert(vm.PatName == draftName && vm.Ins.Select(item => item.Address).SequenceEqual(instructionOrder), "Live language switching must preserve the draft and instruction order.");
            vm.SetCtx(install, game with { Serial = "BLUS00001", Title = "Other Game" });
            Assert(vm.PatName == draftName && vm.IsDirty, "Changing the selected game must preserve an active draft.");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static void Ui_Lang()
    {
        var root = RepoRoot();
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "en-US.json")))!;
        var ar = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "ar-SA.json")))!;
        Assert(en.Keys.Order().SequenceEqual(ar.Keys.Order()), "Arabic and English localization keys must match exactly.");
        Assert(en.Keys.Any(key => key.StartsWith("HCanary.Validation.", StringComparison.Ordinal)), "H-Canary validation messages must be localized.");
        var xaml = File.ReadAllText(Path.Combine(root, "App", "Ctrl", "HCan.xaml"));
        Assert(!xaml.Contains("CheckBox", StringComparison.OrdinalIgnoreCase), "H-Canary authoring view must not be an activation checklist.");
        Assert(xaml.Contains("HCanary.Instructions", StringComparison.Ordinal) && xaml.Contains("HCanary.Anchors", StringComparison.Ordinal) && xaml.Contains("HCanary.Preview", StringComparison.Ordinal),
            "H-Canary view must expose instruction, Anchor, and true YAML preview editing.");
        Assert(xaml.Contains("FlowDirection=\"LeftToRight\"", StringComparison.Ordinal) && xaml.Contains("TextAlignment=\"Left\"", StringComparison.Ordinal),
            "Technical YAML content must remain LTR and left aligned.");
        Assert(!xaml.Contains("Enable Code", StringComparison.OrdinalIgnoreCase) && !xaml.Contains("AnalysisItems", StringComparison.Ordinal),
            "The obsolete patch-browser and compatibility-analysis concepts must be absent.");
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

    private static void HasIssue(PatAuth service, HCanDrft valid, Action<HCanDrft> mutate, string code)
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
