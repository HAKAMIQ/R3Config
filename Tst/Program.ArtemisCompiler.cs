using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static void Cht_Bndl(string tempRoot)
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

        var reader = new R3Config.Svc.ReadSvc();
        var read = reader.ReadText(sourceText, PatSrc.Artemis, "art.yml");
        Assert(read.IsOk, "Expected duplicate community cheat titles to be readable: " + read.Error);
        var merged = new PatchCatalogMergeService().Merge(read.Catalog);
        Assert(merged.Entries.Count == 2, "Expected duplicate cheat versions to merge deterministically.");
        Assert(merged.Entries.All(entry => entry.Risk == PatRisk.Cheat),
            "Expected bundled community entries to be forced into explicit Cheat handling.");
        Assert(merged.Entries.Single(entry => entry.Title == "Infinite Health").PatVer == "1.1",
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
        Assert(matches.All(match => match.Status == MatStat.Match),
            "Expected compiler smoke selections to require exact serial/version/hash evidence.");

        var compiler = new PatchSelectionCompiler();
        var compiled = compiler.Compile(
            sourceText,
            [matches.Single(match => match.Patch.Title == "Infinite Health")]);
        Assert(compiled.IsOk, "Expected an anchored selected cheat to compile: " + compiled.Error);
        Assert(compiled.PatText.StartsWith("Version: 1.2", StringComparison.Ordinal),
            "Expected compiled cheat text to start with the RPCS3 version header.");
        Assert(!compiled.PatText.Contains("*NewCode", StringComparison.Ordinal) &&
               !compiled.PatText.Contains("Anchors:", StringComparison.Ordinal),
            "Expected compiler output to expand aliases and have no external anchor dependency.");
        Assert(compiled.PatText.Contains("0x60000000", StringComparison.OrdinalIgnoreCase),
            "Expected the compiler to select the newer duplicate patch body.");

        var runtimeGuardedGame = game with
        {
            PpuHash = null,
            PpuRaw = []
        };
        var runtimeGuardedMatch = new R3Config.Svc.PatchMatchService().Match(
            merged.Entries.Single(entry => entry.Title == "Infinite Health"),
            [runtimeGuardedGame]);
        Assert(runtimeGuardedMatch.Status == MatStat.Unknown &&
               PatAct.IsRun(runtimeGuardedMatch),
            "Expected a trusted cheat with exact serial/version and no local Hash to use RPCS3's runtime Hash guard.");
        var runtimeGuardedCompile = compiler.Compile(
            sourceText,
            [runtimeGuardedMatch]);
        Assert(runtimeGuardedCompile.IsOk &&
               runtimeGuardedCompile.SelNum == 1,
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
            PatSrc.Artemis,
            "art.yml");
        Assert(conflictRead.IsOk,
            "Expected same-version conflict source to parse before merge validation: " + conflictRead.Error);
        var conflictMerge = new PatchCatalogMergeService().Merge(conflictRead.Catalog);
        Assert(conflictMerge.Entries.Count == 0,
            "Expected every same-version conflicting duplicate definition to be rejected.");
        Assert(conflictMerge.Diag.Any(diagnostic =>
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
            PatSrc.Artemis,
            "art.yml");
        Assert(integrityRead.IsOk,
            "Expected dependency/group integrity source to parse: " + integrityRead.Error);
        var integrityMatches = integrityRead.Catalog.Entries
            .Select(entry => new PatchMatchService().Match(entry, [game]))
            .ToArray();
        var dependencyOnly = compiler.Compile(
            integritySourceText,
            [integrityMatches.Single(match => match.Patch.Title == "Dependent Health")]);
        Assert(!dependencyOnly.IsOk &&
               dependencyOnly.Error?.Contains("Enable Codes", StringComparison.Ordinal) == true,
            "Expected a dependent cheat to be rejected when its required patch is not selected.");
        var dependencyPair = compiler.Compile(
            integritySourceText,
            [
                integrityMatches.Single(match => match.Patch.Title == "Enable Codes"),
                integrityMatches.Single(match => match.Patch.Title == "Dependent Health")
            ]);
        Assert(dependencyPair.IsOk && dependencyPair.SelNum == 2,
            "Expected an explicit dependency pair to compile together: " + dependencyPair.Error);
        var groupConflict = compiler.Compile(
            integritySourceText,
            [
                integrityMatches.Single(match => match.Patch.Title == "Choice A"),
                integrityMatches.Single(match => match.Patch.Title == "Choice B")
            ]);
        Assert(!groupConflict.IsOk &&
               groupConflict.Error?.Contains("mutually exclusive", StringComparison.OrdinalIgnoreCase) == true,
            "Expected same-hash selections in one Group to be rejected as mutually exclusive.");

        var exclusivePreview = new PatPrev();
        exclusivePreview.Load(
            [
                integrityMatches.Single(match => match.Patch.Title == "Choice A"),
                integrityMatches.Single(match => match.Patch.Title == "Choice B")
            ]);
        foreach (var item in exclusivePreview.ChtItems)
        {
            item.IsSel = false;
        }
        var choiceAItem = exclusivePreview.ChtItems.Single(item => item.Title == "Choice A");
        var choiceBItem = exclusivePreview.ChtItems.Single(item => item.Title == "Choice B");
        choiceAItem.IsSel = true;
        choiceBItem.IsSel = true;
        Assert(
            !choiceAItem.IsSel &&
            choiceBItem.IsSel &&
            exclusivePreview.SelCht().Single().Patch.Title == "Choice B" &&
            exclusivePreview.HasChtSelectionNotice,
            "Expected the Artemis UI to keep only the latest choice from one executable-hash conflict group.");

        var enabledExclusivePreview = new PatPrev();
        enabledExclusivePreview.Load(
            [
                integrityMatches.Single(match => match.Patch.Title == "Choice A"),
                integrityMatches.Single(match => match.Patch.Title == "Choice B")
            ],
            isEnabledResolver: match => match.Patch.Title == "Choice A");
        var blockedChoice = enabledExclusivePreview.ChtItems.Single(item => item.Title == "Choice B");
        blockedChoice.IsSel = true;
        Assert(
            !blockedChoice.IsSel &&
            enabledExclusivePreview.HasChtSelectionNotice &&
            enabledExclusivePreview.ChtSelectionNotice.Contains("Choice A", StringComparison.Ordinal),
            "Expected a new Artemis alternative to remain unselected until the enabled peer is disabled first.");

        try
        {
            _ = new PatPlnr().Build(
                new Rpcs3InstallDescriptor(
                    RootPath: tempRoot,
                    ExePath: Path.Combine(tempRoot, "rpcs3.exe"),
                    CfgPath: Path.Combine(tempRoot, "config.yml"),
                    CustPath: Path.Combine(tempRoot, "custom_configs"),
                    PatPath: tempRoot,
                    PatCfg: Path.Combine(tempRoot, "patch_config.yml")),
                [integrityMatches.Single(match => match.Patch.Title == "Choice B")],
                expertMode: true,
                enabledMatches:
                [
                    integrityMatches.Single(match => match.Patch.Title == "Choice A")
                ]);
            throw new InvalidOperationException(
                "Expected the planner to reject an alternative while its peer is enabled.");
        }
        catch (PatchGroupConflictException ex)
        {
            Assert(
                ex.HasEnabledPeer &&
                ex.SelectedTitle == "Choice B" &&
                ex.EnabledTitle == "Choice A" &&
                ex.Group == "Movement",
                "Expected typed conflict details for localized actionable UI guidance.");
        }

        var dependencyCycle = compiler.Compile(
            integritySourceText,
            [
                integrityMatches.Single(match => match.Patch.Title == "Cycle A"),
                integrityMatches.Single(match => match.Patch.Title == "Cycle B")
            ]);
        Assert(!dependencyCycle.IsOk &&
               dependencyCycle.Error?.Contains("cycle", StringComparison.OrdinalIgnoreCase) == true,
            "Expected cyclic cheat dependencies to be rejected.");

        var libraryRoot = Path.Combine(tempRoot, "cheat-library-smoke");
        var dataPath = Path.Combine(libraryRoot, "Data");
        var patchesPath = Path.Combine(libraryRoot, "rpcs3", "patches");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(patchesPath);
        File.WriteAllText(Path.Combine(dataPath, BundledCheatPatchService.BndlName), sourceText);
        var oldLib = Path.Combine(patchesPath, BundledCheatPatchService.OldName);
        var oldText = BundledCheatPatchService.ManMark + "\r\n" + compiled.PatText;
        File.WriteAllText(oldLib, oldText);

        var service = new BundledCheatPatchService();
        var managedPath = Path.Combine(
            patchesPath,
            "BLUS00001_patch.yml");
        Assert(
            managedPath.Equals(
                BundledCheatPatchService.GetPath(
                    patchesPath,
                    "BLUS00001"),
                StringComparison.OrdinalIgnoreCase),
            "Expected managed cheats to use RPCS3's runtime-loaded <TITLE_ID>_patch.yml filename.");
        File.WriteAllText(
            managedPath,
            BundledCheatPatchService.ManMark + "\r\n",
            Encoding.UTF8);
        using (var stream = new FileStream(
                   managedPath,
                   FileMode.Open,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.SetLength(R3Limits.PatCatBy + 1);
        }
        Assert(!BundledCheatPatchService.IsMan(managedPath),
            "Expected an oversized title-specific patch not to be trusted as R3Config-managed from its first line alone.");
        var oversizedManaged = service.Prep(
            patchesPath,
            libraryRoot,
            [matches.Single(match => match.Patch.Title == "Infinite Health")]);
        Assert(!oversizedManaged.IsOk &&
               oversizedManaged.PreparedFile is null &&
               oversizedManaged.Error?.Contains(
                   "input limit",
                   StringComparison.OrdinalIgnoreCase) == true &&
               new FileInfo(managedPath).Length == R3Limits.PatCatBy + 1,
            "Expected an oversized user-owned title-specific patch to be rejected without reading or changing it.");
        File.Delete(managedPath);
        var stagedFirst = service.Prep(
            patchesPath,
            libraryRoot,
            [matches.Single(match => match.Patch.Title == "Infinite Health")]);
        Assert(stagedFirst.IsOk &&
               stagedFirst.PreparedFile is not null &&
               stagedFirst.Extra.Count == 1 &&
               stagedFirst.Extra[0].TgtPath.Equals(
                   BundledCheatPatchService.GetPath(patchesPath),
                   StringComparison.OrdinalIgnoreCase) &&
               !File.Exists(managedPath),
            $"Expected Cheat preparation to stage both the runtime target and legacy-library migration without touching disk. Error={stagedFirst.Error}; Extra={stagedFirst.Extra.Count}");

        var cheatTxn = new PatchFileTransactionService();
        var cheatBackupRoot = Path.Combine(
            libraryRoot,
            "rpcs3",
            "config",
            "R3Bkp");
        var firstBackup = cheatTxn.Commit(
            cheatBackupRoot,
            [stagedFirst.PreparedFile!, .. stagedFirst.Extra]);
        Assert(File.Exists(managedPath) &&
               firstBackup.Files.Single(file => file.TgtPath.Equals(
                   managedPath,
                   StringComparison.OrdinalIgnoreCase)).Existed == false,
            "Expected first exact cheat preparation to commit through the transaction with Existed=false backup state.");
        var newLib = BundledCheatPatchService.GetPath(patchesPath);
        Assert(File.Exists(newLib) &&
               File.Exists(oldLib) &&
               File.ReadAllBytes(newLib).SequenceEqual(File.ReadAllBytes(oldLib)),
            "Expected r3cht.yml to migrate byte-for-byte inside the transaction while retaining the old library.");

        var stagedSecond = service.Prep(
            patchesPath,
            libraryRoot,
            [matches.Single(match => match.Patch.Title == "Max Orbs")]);
        Assert(stagedSecond.IsOk &&
               stagedSecond.PreparedFile is not null &&
               stagedSecond.Extra.Count == 0,
            "Expected second exact cheat preparation to produce transaction content: " + stagedSecond.Error);
        var secondBackup = cheatTxn.Commit(
            cheatBackupRoot,
            [stagedSecond.PreparedFile!]);
        Assert(secondBackup.Files.Single() is
        { Existed: true, BkpPath: not null } &&
               File.Exists(secondBackup.Files.Single().BkpPath),
            "Expected the central transaction to back up an existing managed cheat file before extension.");

        var managedRead = reader.Read(managedPath, PatSrc.ManagedArtemis);
        Assert(managedRead.IsOk, "Expected managed cheat file to read back: " + managedRead.Error);
        Assert(managedRead.Catalog.Entries.Count == 2,
            "Expected adding a second cheat to preserve the first managed definition.");

        const string residentOnlyHash =
            "cccccccccccccccccccccccccccccccccccccccc";
        var residentOnlyText =
            "Version: 1.2\n" +
            $"PPU-{residentOnlyHash}:\n" +
            "  \"Legacy Resident Cheat\":\n" +
            "    Games:\n" +
            "      \"Compiler Game\":\n" +
            "        BLUS00001: [ 01.01 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00100030, 0x60000000 ]\n";
        var fallbackRoot = Path.Combine(
            tempRoot,
            "cheat-managed-fallback-smoke");
        var fallbackDataPath = Path.Combine(
            fallbackRoot,
            "Data");
        var fallbackPatchesPath = Path.Combine(
            fallbackRoot,
            "rpcs3",
            "patches");
        Directory.CreateDirectory(fallbackDataPath);
        Directory.CreateDirectory(fallbackPatchesPath);
        File.WriteAllText(
            Path.Combine(
                fallbackDataPath,
                BundledCheatPatchService.BndlName),
            sourceText);
        File.WriteAllText(
            BundledCheatPatchService.GetPath(
                fallbackPatchesPath,
                "BLUS00001"),
            BundledCheatPatchService.ManMark +
            "\r\n" +
            residentOnlyText);

        var fallbackPrepared = service.Prep(
            fallbackPatchesPath,
            fallbackRoot,
            [matches.Single(match => match.Patch.Title == "Max Orbs")]);
        Assert(
            fallbackPrepared.IsOk &&
            fallbackPrepared.PreparedFile is not null &&
            fallbackPrepared.PreparedFile.Content.Contains(
                "Legacy Resident Cheat",
                StringComparison.Ordinal) &&
            fallbackPrepared.PreparedFile.Content.Contains(
                "Max Orbs",
                StringComparison.Ordinal),
            "Expected a resident managed definition that is absent from the current bundle to be preserved while compiling a new selection: " +
            fallbackPrepared.Error);

        Console.WriteLine("Bundled cheat selection compiler smoke PASS.");
    }

}
