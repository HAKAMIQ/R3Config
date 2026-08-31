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
    static void Bkp_All(
        Rpcs3InstallDescriptor install,
        CfgPlan plan,
        IReadOnlyList<CfgChg> selectedChanges)
    {
        var backupService = new BackupService();
        var oldRoot = Rpcs3AppPaths.OldBkp(install);
        var newRoot = Rpcs3AppPaths.BkpRoot(install);
        var backup = backupService.BuildBkp(oldRoot, [plan.CfgPath]);
        var secondBackup = backupService.BuildBkp(newRoot, [plan.CfgPath]);
        var backupCase = new BackupUseCase(
            backupService,
            new Rpcs3MutationGuard(() => false));
        Assert(backupCase.List(install).Count == 2,
            "Expected backups from both R3Bkp and R3ConfigBackups to remain discoverable.");
        Assert(backup.Files[0].BkpPath != secondBackup.Files[0].BkpPath,
            "Expected backup file paths to be unique.");
        R3Yaml.Apply(plan.CfgPath, selectedChanges);

        Assert(R3Yaml.ReadVal(plan.CfgPath, "Core", "PPU Decoder") is null,
            "Expected performance-cost PPU Decoder not to be written by default.");
        Assert(R3Yaml.ReadVal(plan.CfgPath, "Video", "Write Color Buffers") == "true",
            "Expected written Write Color Buffers.");
        Assert(R3Yaml.ReadVal(plan.CfgPath, "Video", "Read Color Buffers") is null,
            "Expected conditional Read Color Buffers not to be written by default.");
        Assert(R3Yaml.ReadVal(plan.CfgPath, "Video", "Write Depth Buffer") is null,
            "Expected patch-dependent Write Depth Buffer not to be written by default.");
        Assert(R3Yaml.ReadVal(plan.CfgPath, "Video", "Strict Rendering Mode") is null,
            "Expected performance-cost Strict Rendering Mode not to be written by default.");
        Assert(R3Yaml.ReadVal(plan.CfgPath, "Video", "Vblank Rate") is null,
            "Expected unchecked Vblank not to be written.");

        var backups = backupCase.List(install);
        Assert(backups.Count == 2, "Expected legacy and current persisted backups to be discoverable together.");

        backupCase.Restore(
            install,
            backupService.ListBkp(oldRoot).Single());
        Assert(!File.Exists(plan.CfgPath), "Expected restore to remove a file that did not exist before backup.");
    }

    static void Cfg_All(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor rogue,
        WikiRec recommendation,
        IReadOnlyList<CfgChg> selectedChanges)
    {
        var plan = new PlanService().BuildCfg(install, rogue, recommendation);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.CfgPath)!);
        File.WriteAllText(plan.CfgPath, $"Video:{Environment.NewLine}  Write Color Buffers: false{Environment.NewLine}");
        var existingCustomPlan = new PlanService().BuildCfg(install, rogue, recommendation);
        Assert(existingCustomPlan.Items.Single(item => item.Key == "Write Color Buffers").CurVal == "false",
            "Expected existing custom config value to be detected.");
        var originalCustomConfigText = File.ReadAllText(plan.CfgPath);

        const string changedAfterPreview =
            "Video:\r\n  Write Color Buffers: external-before-confirm\r\n";
        File.WriteAllText(plan.CfgPath, changedAfterPreview, Encoding.UTF8);
        var staleBackupCount = new BackupService()
            .ListBkp(Rpcs3AppPaths.BkpRoot(install))
            .Count;
        MustFail<IOException>(
            () => new ConfigApplyService().Apply(
                existingCustomPlan,
                selectedChanges,
                Rpcs3AppPaths.BkpRoot(install)),
            "Expected a config value changed after preview to reject apply before backup or mutation.");
        Assert(File.ReadAllText(plan.CfgPath, Encoding.UTF8) == changedAfterPreview &&
               new BackupService().ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count == staleBackupCount,
            "Expected stale config-plan rejection to preserve the external edit and create no backup.");
        File.WriteAllText(plan.CfgPath, originalCustomConfigText, Encoding.UTF8);

        R3Yaml.Del(plan.CfgPath, []);
        Assert(File.ReadAllText(plan.CfgPath) == originalCustomConfigText,
            "Expected an empty config removal request to remain a no-op.");
        var failingConfigApply = new ConfigApplyService(applyChanges: (path, preparedText, beforeCommit) =>
        {
            R3Yaml.Commit(path, preparedText, beforeCommit);
            throw new UnauthorizedAccessException("Simulated permission denied.");
        });
        try
        {
            failingConfigApply.Apply(existingCustomPlan, selectedChanges, Rpcs3AppPaths.BkpRoot(install));
            throw new InvalidOperationException("Expected config apply failure to be surfaced.");
        }
        catch (UnauthorizedAccessException)
        {
            Assert(File.ReadAllText(plan.CfgPath) == originalCustomConfigText,
                "Expected a failure after a completed config commit to restore the previous custom config.");
        }

        File.WriteAllText(plan.CfgPath, originalCustomConfigText, Encoding.UTF8);
        const string rollbackExternal =
            "Video:\r\n  Write Color Buffers: external-after-commit-attempt\r\n";
        var externalConfigApply = new ConfigApplyService(
            applyChanges: (path, _, beforeCommit) =>
            {
                beforeCommit?.Invoke();
                File.WriteAllText(path, rollbackExternal, Encoding.UTF8);
                throw new IOException(
                    "Simulated commit failure after an external config edit.");
            });
        MustFail<InvalidOperationException>(
            () => externalConfigApply.Apply(
                existingCustomPlan,
                selectedChanges,
                Rpcs3AppPaths.BkpRoot(install)),
            "Expected config rollback to report and preserve an unknown external state.");
        Assert(File.ReadAllText(plan.CfgPath, Encoding.UTF8) == rollbackExternal,
            "Expected selective config rollback not to erase an external edit.");

        File.WriteAllText(plan.CfgPath, "Video: [broken");
        var corruptCustomConfigPlan = new PlanService().BuildCfg(install, rogue, recommendation);
        Assert(corruptCustomConfigPlan.Items.Any(item => item.CurVal is null),
            "Expected corrupt custom config to be tolerated during plan preview.");
        Assert(corruptCustomConfigPlan.Items.All(item => item.State == RecState.Unknown),
            "Expected unreadable current config to mark RecState as Unknown.");

        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var backupCountBeforeMalformed = new BackupService()
            .ListBkp(backupRoot)
            .Count;
        MustFail<InvalidDataException>(
            () => new ConfigApplyService().Apply(
                corruptCustomConfigPlan,
                selectedChanges,
                backupRoot),
            "Expected malformed current custom config to fail before backup or mutation.");
        Assert(File.ReadAllText(plan.CfgPath) == "Video: [broken" &&
               new BackupService().ListBkp(backupRoot).Count ==
               backupCountBeforeMalformed,
            "Expected invalid input to create no backup side effect and preserve the target.");

        using (var stream = File.Create(plan.CfgPath))
        {
            stream.SetLength(R3Limits.CfgByte + 1);
        }
        var backupCountBeforeOversized = new BackupService()
            .ListBkp(backupRoot)
            .Count;
        MustFail<InvalidDataException>(
            () => new ConfigApplyService().Apply(
                existingCustomPlan,
                selectedChanges,
                backupRoot),
            "Expected oversized current custom config to fail before being copied into a backup.");
        Assert(new FileInfo(plan.CfgPath).Length == R3Limits.CfgByte + 1 &&
               new BackupService().ListBkp(backupRoot).Count ==
               backupCountBeforeOversized,
            "Expected an oversized sparse target to remain unchanged with no backup side effect.");

        const string raceOriginal =
            "Video:\r\n  Write Color Buffers: false\r\n";
        const string raceExternal =
            "Video:\r\n  Write Color Buffers: external\r\n";
        File.WriteAllText(plan.CfgPath, raceOriginal, Encoding.UTF8);
        MustFail<IOException>(
            () => new ConfigApplyService().Apply(
                existingCustomPlan,
                selectedChanges,
                backupRoot,
                (phase, _, _) =>
                {
                    if (phase == PatPhase.Commit)
                    {
                        File.WriteAllText(
                            plan.CfgPath,
                            raceExternal,
                            Encoding.UTF8);
                    }
                }),
            "Expected an external custom-config edit immediately before commit to abort the apply.");
        Assert(File.ReadAllText(plan.CfgPath, Encoding.UTF8) == raceExternal,
            "Expected a pre-commit concurrency failure to preserve the external custom-config edit.");
        File.Delete(plan.CfgPath);
    }

    static void Can_Imp(SmokeEnv env, string officialPatchYmlPath)
    {
        var importService = new R3Config.Svc.CanaryPatchImportService();
        var flow = TestFlow();
        var officialBefore = File.ReadAllText(officialPatchYmlPath);
        var importedPath = Path.Combine(env.Install.PatPath, R3Config.Svc.CanaryPatchImportService.ImpName);

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

        var result = flow.ImpCan(env.Install, canaryPatchText);

        Assert(result.IsOk, "Expected Canary import to write imported_patch.yml: " + result.Error);
        Assert(result.TgtPath == importedPath, "Expected Canary import target to be imported_patch.yml in <RPCS3 folder>\\patches.");
        Assert(File.Exists(importedPath), "Expected imported_patch.yml to exist after Canary import.");
        Assert(File.ReadAllText(importedPath).StartsWith(CanaryPatchImportService.VerHead, StringComparison.Ordinal),
            "Expected imported_patch.yml to start with Version: 1.2.");
        Assert(File.ReadAllText(officialPatchYmlPath) == officialBefore,
            "Expected Canary import not to modify official patch.yml.");

        var officialRead = new R3Config.Svc.ReadSvc().Read(officialPatchYmlPath);
        var importedRead = new R3Config.Svc.ReadSvc().Read(importedPath, isCanarySource: true);
        Assert(officialRead.IsOk, "Expected official patch.yml to remain readable after Canary import.");
        Assert(importedRead.IsOk, "Expected imported_patch.yml to be readable after Canary import: " + importedRead.Error);
        Assert(importedRead.Catalog.Entries.Any(patch => patch.Title == "Canary Smoke Patch" && patch.Risk == PatRisk.Canary),
            "Expected imported Canary patch to be classified as Canary.");

        var secondWrite = flow.ImpCan(env.Install, canaryPatchText);
        Assert(secondWrite.IsOk, "Expected repeated Canary import to remain idempotent: " + secondWrite.Error);
        Assert(secondWrite.BkpPath is null, "Expected repeated identical imported_patch.yml write not to create a needless backup.");

        var manualAndManagedText =
            R3Config.Svc.CanaryPatchImportService.VerHead + "\r\n\r\n" +
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
            env.Install.PatPath,
            canaryPatchText);
        Assert(managedStage.IsOk &&
               managedStage.PreparedFile is not null &&
               File.ReadAllText(importedPath, Encoding.UTF8) == beforeManagedStage,
            "Expected Canary preparation to produce validated staged content without touching imported_patch.yml.");

        var managedWrite = flow.ImpCan(env.Install, canaryPatchText);
        Assert(managedWrite.IsOk, "Expected managed Canary write to merge into existing imported_patch.yml: " + managedWrite.Error);
        var managedText = File.ReadAllText(importedPath);
        Assert(managedText.Contains("Manual Existing Patch", StringComparison.Ordinal),
            "Expected existing manual imported_patch.yml entries to be preserved.");
        Assert(managedText.Contains("R3Config managed Canary patches v1 - begin", StringComparison.Ordinal),
            "Expected managed Canary block to be written inside imported_patch.yml.");
        var managedRead = new R3Config.Svc.ReadSvc().Read(importedPath, isCanarySource: true);
        Assert(managedRead.IsOk, "Expected managed imported_patch.yml to stay parseable: " + managedRead.Error);

        Console.WriteLine("Canary imported_patch.yml contract smoke PASS.");
    }

}
