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
    static void Pat_Txn(string tempRoot)
    {
        const string hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var root = Path.Combine(tempRoot, "patch-transaction-smoke");
        var patchesPath = Path.Combine(root, "patches");
        var configPath = Path.Combine(root, "config");
        var backupRoot = Path.Combine(configPath, "R3Bkp");
        Directory.CreateDirectory(patchesPath);
        Directory.CreateDirectory(configPath);

        var importedPath = Path.Combine(
            patchesPath,
            R3Config.Svc.CanaryPatchImportService.ImpName);
        var cheatPath = Path.Combine(
            patchesPath,
            BundledCheatPatchService.ManName);
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

        var reviewedPatchConfigState =
            PatchFileTransactionService.CaptureExpectedState(
                patchConfigPath,
                R3Limits.PatCfgBy,
                "patch_config.yml");
        var reviewedChange = new PatChg(
            "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "Reviewed Patch",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            PatRisk.SafeFix);
        var reviewedPlan = new PatchApplyPlan(
            patchConfigPath,
            [reviewedChange],
            reviewedPatchConfigState.Exists,
            reviewedPatchConfigState.Sha256);
        const string changedAfterPatchPreview =
            "PPU-external:\r\n  External Patch:\r\n    External Game:\r\n      BLUS99999:\r\n        01.00:\r\n          Enabled: true\r\n";
        File.WriteAllText(
            patchConfigPath,
            changedAfterPatchPreview,
            Encoding.UTF8);
        var stalePatchBackupCount = Directory.Exists(backupRoot)
            ? Directory.GetDirectories(backupRoot).Length
            : 0;
        MustFail<IOException>(
            () => new PatchApplyService().Apply(
                reviewedPlan,
                backupRoot),
            "Expected patch_config.yml changed after preview to reject apply instead of adopting a new baseline.");
        Assert(File.ReadAllText(patchConfigPath, Encoding.UTF8) == changedAfterPatchPreview &&
               (!Directory.Exists(backupRoot) ||
                Directory.GetDirectories(backupRoot).Length == stalePatchBackupCount),
            "Expected stale patch-plan rejection to preserve the external patch_config.yml edit without backup side effects.");
        File.WriteAllText(patchConfigPath, originalPatchConfig, Encoding.UTF8);

        const string malformedAfterPatchPreview = "PPU-invalid: [broken";
        File.WriteAllText(
            patchConfigPath,
            malformedAfterPatchPreview,
            Encoding.UTF8);
        var malformedPatchBackupCount = Directory.Exists(backupRoot)
            ? Directory.GetDirectories(backupRoot).Length
            : 0;

        var malformedPatchService = new PatchApplyService();
        MustFail<InvalidDataException>(
            () => malformedPatchService.Apply(
                reviewedPlan,
                backupRoot),
            "Expected malformed patch_config.yml to retain its bounded-input error during apply even when the reviewed snapshot is stale.");
        MustFail<InvalidDataException>(
            () => malformedPatchService.Disable(
                reviewedPlan,
                backupRoot),
            "Expected malformed patch_config.yml to retain its bounded-input error during disable even when the reviewed snapshot is stale.");
        MustFail<InvalidDataException>(
            () => malformedPatchService.ApplyTxn(
                reviewedPlan,
                backupRoot,
                []),
            "Expected malformed patch_config.yml to retain its bounded-input error during transactional apply even when the reviewed snapshot is stale.");
        MustFail<InvalidDataException>(
            () => malformedPatchService.RemoveTxn(
                reviewedPlan,
                backupRoot,
                []),
            "Expected malformed patch_config.yml to retain its bounded-input error during transactional remove even when the reviewed snapshot is stale.");
        Assert(File.ReadAllText(patchConfigPath, Encoding.UTF8) == malformedAfterPatchPreview &&
               (!Directory.Exists(backupRoot) ||
                Directory.GetDirectories(backupRoot).Length == malformedPatchBackupCount),
            "Expected malformed patch-config rejection to preserve the invalid input without backup side effects across all mutation paths.");
        File.WriteAllText(patchConfigPath, originalPatchConfig, Encoding.UTF8);

        var canaryText = TxnCat(
            hash,
            "Transaction Canary",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            "0x00001000");
        var cheatText = TxnCat(
            hash,
            "Transaction Cheat",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            "0x00001004");

        var canaryPrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            importedPath,
            canaryText,
            PrepKind.PatCat,
            PatSrc.Imported,
            EntNum: 1));
        var cheatPrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            cheatPath,
            cheatText,
            PrepKind.PatCat,
            PatSrc.ManagedArtemis,
            EntNum: 1));
        var change = new PatChg(
            "PPU-" + hash,
            "Transaction Canary",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            R3Config.Mdl.PatRisk.Canary);
        var plan = new R3Config.Mdl.PatchApplyPlan(
            patchConfigPath,
            [change]);

        var service = new PatchApplyService();
        var success = service.ApplyTxn(
            plan,
            backupRoot,
            [canaryPrepared, cheatPrepared]);

        Assert(success.Backup.Files.Count == 3,
            "Expected one transaction backup manifest to include Canary, Cheat, and patch_config.yml.");
        Assert(success.Backup.Files.Select(file => file.TgtPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals([importedPath, cheatPath, patchConfigPath]),
            "Expected the transaction backup to cover every affected target path.");
        Assert(File.ReadAllText(importedPath, Encoding.UTF8) == canaryText,
            "Expected staged Canary content to commit.");
        Assert(File.ReadAllText(cheatPath, Encoding.UTF8) == cheatText,
            "Expected staged Cheat content to commit.");
        Assert(new CfgRead().IsOn(patchConfigPath, change),
            "Expected patch_config.yml to commit in the same transaction.");

        var enabledImportedText = File.ReadAllText(importedPath, Encoding.UTF8);
        var enabledCheatText = File.ReadAllText(cheatPath, Encoding.UTF8);
        var disableResult = service.Disable(
            plan,
            backupRoot);
        Assert(!new CfgRead().IsOn(patchConfigPath, change),
            "Expected disabling to remove only the enabled state from patch_config.yml.");
        Assert(File.ReadAllText(importedPath, Encoding.UTF8) == enabledImportedText &&
               File.ReadAllText(cheatPath, Encoding.UTF8) == enabledCheatText,
            "Expected disabling Canary or Artemis state not to delete or rewrite their resident definitions.");
        new BackupService().Restore(disableResult.Backup);
        Assert(new CfgRead().IsOn(patchConfigPath, change),
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
        var invalidPrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            invalidStagePath,
            "Version: [broken",
            PrepKind.PatCat,
            PatSrc.Imported));

        try
        {
            service.ApplyTxn(
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

        var lateChangeTarget = Path.Combine(
            patchesPath,
            "late_external_change_patch.yml");
        var lateChangeOriginal = TxnCat(
            hash,
            "Late Original",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            "0x00001008");
        var lateChangeExternal = TxnCat(
            hash,
            "Late External",
            "Transaction Game",
            "BLUS20000",
            "01.00",
            "0x0000100C");
        File.WriteAllText(lateChangeTarget, lateChangeOriginal, Encoding.UTF8);
        var lateChangePrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    lateChangeTarget,
                    TxnCat(
                        hash,
                        "Late R3Config",
                        "Transaction Game",
                        "BLUS20000",
                        "01.00",
                        "0x00001010"),
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1));
        MustFail<IOException>(
            () => new PatchFileTransactionService().Commit(
                backupRoot,
                [lateChangePrepared],
                (phase, _, _) =>
                {
                    if (phase == PatPhase.Commit)
                    {
                        File.WriteAllText(
                            lateChangeTarget,
                            lateChangeExternal,
                            Encoding.UTF8);
                    }
                }),
            "Expected a target changed after the final path guard to abort before commit.");
        Assert(File.ReadAllText(lateChangeTarget, Encoding.UTF8) == lateChangeExternal,
            "Expected the transaction to preserve an external edit made immediately before commit.");

        var firstRaceTarget = Path.Combine(
            patchesPath,
            "first_race_patch.yml");
        var secondRaceTarget = Path.Combine(
            patchesPath,
            "second_race_patch.yml");
        var firstRaceOriginal = TxnCat(
            hash, "First Original", "Transaction Game", "BLUS20000", "01.00", "0x00001014");
        var secondRaceOriginal = TxnCat(
            hash, "Second Original", "Transaction Game", "BLUS20000", "01.00", "0x00001018");
        var secondRaceExternal = TxnCat(
            hash, "Second External", "Transaction Game", "BLUS20000", "01.00", "0x0000101C");
        File.WriteAllText(firstRaceTarget, firstRaceOriginal, Encoding.UTF8);
        File.WriteAllText(secondRaceTarget, secondRaceOriginal, Encoding.UTF8);
        var firstRacePrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    firstRaceTarget,
                    TxnCat(hash, "First R3Config", "Transaction Game", "BLUS20000", "01.00", "0x00001020"),
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1));
        var secondRacePrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    secondRaceTarget,
                    TxnCat(hash, "Second R3Config", "Transaction Game", "BLUS20000", "01.00", "0x00001024"),
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1));
        var raceCommitCount = 0;
        var racingTransaction = new PatchFileTransactionService(
            commitFile: (stagedPath, targetPath) =>
            {
                if (File.Exists(targetPath))
                {
                    File.Replace(stagedPath, targetPath, null, true);
                }
                else
                {
                    File.Move(stagedPath, targetPath);
                }

                if (++raceCommitCount == 1)
                {
                    File.WriteAllText(
                        secondRaceTarget,
                        secondRaceExternal,
                        Encoding.UTF8);
                }
            });
        MustFail<IOException>(
            () => racingTransaction.Commit(
                backupRoot,
                [firstRacePrepared, secondRacePrepared]),
            "Expected an external edit between multi-file commits to abort the remaining commit.");
        Assert(File.ReadAllText(firstRaceTarget, Encoding.UTF8) == firstRaceOriginal &&
               File.ReadAllText(secondRaceTarget, Encoding.UTF8) == secondRaceExternal,
            "Expected rollback to restore only attempted targets and preserve an external edit to an unattempted target.");

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
            failingService.ApplyTxn(
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

    static void Pat_TxnRollbackPreservesExternalAttempt(string tempRoot)
    {
        const string hash = "dddddddddddddddddddddddddddddddddddddddd";
        var root = Path.Combine(tempRoot, "patch-rollback-external-attempt");
        var patchesPath = Path.Combine(root, "patches");
        var backupRoot = Path.Combine(root, "config", "R3Bkp");
        Directory.CreateDirectory(patchesPath);

        var firstTarget = Path.Combine(patchesPath, "first_patch.yml");
        var secondTarget = Path.Combine(patchesPath, "second_patch.yml");
        var firstOriginal = TxnCat(
            hash, "First Original", "Transaction Game", "BLUS20000", "01.00", "0x00002000");
        var secondOriginal = TxnCat(
            hash, "Second Original", "Transaction Game", "BLUS20000", "01.00", "0x00002004");
        var firstPreparedText = TxnCat(
            hash, "First R3Config", "Transaction Game", "BLUS20000", "01.00", "0x00002008");
        var secondPreparedText = TxnCat(
            hash, "Second R3Config", "Transaction Game", "BLUS20000", "01.00", "0x0000200C");
        var firstExternal = TxnCat(
            hash, "First External", "Transaction Game", "BLUS20000", "01.00", "0x00002010");

        File.WriteAllText(firstTarget, firstOriginal, Encoding.UTF8);
        File.WriteAllText(secondTarget, secondOriginal, Encoding.UTF8);
        var prepared = new[]
        {
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    firstTarget,
                    firstPreparedText,
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1)),
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    secondTarget,
                    secondPreparedText,
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1))
        };

        var commitCount = 0;
        var transaction = new PatchFileTransactionService(
            commitFile: (stagedPath, targetPath) =>
            {
                commitCount++;
                if (commitCount == 2)
                {
                    throw new IOException("Simulated second commit failure after an external edit.");
                }

                File.Move(stagedPath, targetPath, overwrite: true);
                File.WriteAllText(firstTarget, firstExternal, Encoding.UTF8);
            });

        MustFail<InvalidOperationException>(
            () => transaction.Commit(backupRoot, prepared),
            "Expected rollback to report that an attempted target changed externally.");
        Assert(File.ReadAllText(firstTarget, Encoding.UTF8) == firstExternal &&
               File.ReadAllText(secondTarget, Encoding.UTF8) == secondOriginal &&
               !Directory.EnumerateFiles(
                   patchesPath,
                   "*.r3stage",
                   SearchOption.TopDirectoryOnly).Any(),
            "Expected rollback to preserve an external edit to an attempted target, preserve the uncommitted target, and remove stages.");

        Console.WriteLine("Patch rollback external-attempt regression PASS.");
    }

    static void Can_Txn(string tempRoot)
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

        var install = Rpcs3InstallService.FromExe(exePath);
        var importedPath = Rpcs3AppPaths.ImpPat(install);
        var officialPath = Path.Combine(patchesPath, "patch.yml");
        var officialBytes = Encoding.UTF8.GetBytes(
            "Version: 1.2\r\n# RPCS3-owned test catalog\r\n");
        File.WriteAllBytes(officialPath, officialBytes);

        var oldManaged = CanPat(
            oldHash,
            "Old Managed Canary",
            "0x00000100");
        var newManaged = CanPat(
            newHash,
            "New Managed Canary",
            "0x00000200");
        var manual = CanPat(
            manualHash,
            "Manual Existing Patch",
            "0x00000300");

        var normalizeOnlyRoot = Path.Combine(root, "normalize-read-only");
        var normalize = new R3Config.Svc.CanaryPatchImportService().Norm(newManaged);
        Assert(normalize.IsOk && !Directory.Exists(normalizeOnlyRoot),
            "Expected Canary normalization to remain deterministic and free of filesystem side effects.");

        var reader = new R3Config.Svc.ReadSvc();
        var backup = new BackupService();
        var transaction = new PatchFileTransactionService(
            backup,
            reader,
            new CfgRead());
        var useCase = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(reader),
            new Rpcs3MutationGuard(() => false),
            transaction,
            reader);

        var missingResult = useCase.Import(install, oldManaged);
        Assert(missingResult.IsOk && File.Exists(importedPath),
            "Expected a missing imported_patch.yml to be created transactionally: " + missingResult.Error);
        var missingText = File.ReadAllText(importedPath, Encoding.UTF8);
        Assert(missingText.StartsWith(
                   CanaryPatchImportService.VerHead,
                   StringComparison.Ordinal),
            "Expected a newly imported Canary catalog to start with Version: 1.2.");
        var missingSnapshots = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install));
        Assert(missingSnapshots.Count == 1 &&
               missingSnapshots[0].Files.Count == 1 &&
               !missingSnapshots[0].Files[0].Existed &&
               string.Equals(
                   missingResult.BkpPath,
                   missingSnapshots[0].DirPath,
                   StringComparison.OrdinalIgnoreCase),
            "Expected the first import to create a central snapshot recording Existed=false.");
        Assert(File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
            "Expected manual Canary import never to modify RPCS3-owned patch.yml.");

        File.WriteAllText(importedPath, manual, Encoding.UTF8);
        var preservingResult = useCase.Import(install, oldManaged);
        Assert(preservingResult.IsOk,
            "Expected a valid manual imported patch to be preserved: " + preservingResult.Error);
        var preservingText = File.ReadAllText(importedPath, Encoding.UTF8);
        Assert(preservingText.Contains("Manual Existing Patch", StringComparison.Ordinal) &&
               preservingText.Contains("Old Managed Canary", StringComparison.Ordinal) &&
               new ReadSvc().Read(
                   importedPath,
                   PatSrc.Imported).IsOk,
            "Expected manual and managed definitions to coexist in one readable imported_patch.yml.");

        var replacingResult = useCase.Import(install, newManaged);
        Assert(replacingResult.IsOk,
            "Expected a previous R3Config-managed block to be replaced: " + replacingResult.Error);
        var replacingText = File.ReadAllText(importedPath, Encoding.UTF8);
        Assert(replacingText.Contains("Manual Existing Patch", StringComparison.Ordinal) &&
               replacingText.Contains("New Managed Canary", StringComparison.Ordinal) &&
               !replacingText.Contains("Old Managed Canary", StringComparison.Ordinal) &&
               CountOf(
                   replacingText,
                   "R3Config managed Canary patches v1 - begin") == 1,
            "Expected replacement to keep manual content and emit exactly one managed marker.");

        var snapshotsBeforeReplay = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install)).Count;
        var bytesBeforeReplay = File.ReadAllBytes(importedPath);
        var replayResult = useCase.Import(install, newManaged);
        Assert(replayResult.IsOk &&
               replayResult.BkpPath is null &&
               File.ReadAllBytes(importedPath).SequenceEqual(bytesBeforeReplay) &&
               backup.ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count == snapshotsBeforeReplay,
            "Expected identical Canary import replay to be byte-idempotent without a new backup.");

        File.WriteAllText(importedPath, "Version: [broken", Encoding.UTF8);
        var invalidBefore = File.ReadAllBytes(importedPath);
        var snapshotsBeforeInvalid = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install)).Count;
        var invalidResult = useCase.Import(install, newManaged);
        Assert(!invalidResult.IsOk &&
               File.ReadAllBytes(importedPath).SequenceEqual(invalidBefore) &&
               backup.ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count == snapshotsBeforeInvalid &&
               File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
            "Expected malformed existing YAML to block import with zero target or backup changes.");

        var conflictingManual = CanPat(
            newHash,
            "New Managed Canary",
            "0x00009999");
        File.WriteAllText(importedPath, conflictingManual, Encoding.UTF8);
        var conflictBefore = File.ReadAllBytes(importedPath);
        var conflictResult = useCase.Import(install, newManaged);
        Assert(!conflictResult.IsOk &&
               File.ReadAllBytes(importedPath).SequenceEqual(conflictBefore),
            "Expected a manual/managed patch identity conflict to fail without silently choosing a definition.");

        File.WriteAllText(importedPath, manual, Encoding.UTF8);
        var stageBefore = File.ReadAllBytes(importedPath);
        var snapshotsBeforeStageFailure = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install)).Count;
        try
        {
            transaction.Commit(
                Rpcs3AppPaths.BkpRoot(install),
                [
                    PatchFileTransactionService.CaptureExpectedState(
                        new PatchPreparedFile(
                        importedPath,
                        "Version: [broken",
                        PrepKind.PatCat,
                        PatSrc.Imported))
                ]);
            throw new InvalidOperationException(
                "Expected invalid staged Canary YAML to fail validation.");
        }
        catch (InvalidDataException)
        {
            Assert(File.ReadAllBytes(importedPath).SequenceEqual(stageBefore) &&
                   backup.ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count == snapshotsBeforeStageFailure,
                "Expected stage validation failure before backup or target mutation.");
        }

        var commitFailureBackup = new BackupService();
        var commitFailureReader = new R3Config.Svc.ReadSvc();
        var commitFailureTransaction = new PatchFileTransactionService(
            commitFailureBackup,
            commitFailureReader,
            new CfgRead(),
            commitFile: (stagedPath, targetPath) =>
            {
                File.Copy(stagedPath, targetPath, overwrite: true);
                throw new IOException("Simulated Canary commit failure.");
            });
        var commitFailureUseCase = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(commitFailureReader),
            new Rpcs3MutationGuard(() => false),
            commitFailureTransaction,
            commitFailureReader);
        var commitBefore = File.ReadAllBytes(importedPath);
        var commitFailureResult = commitFailureUseCase.Import(
            install,
            newManaged);
        Assert(!commitFailureResult.IsOk &&
               File.ReadAllBytes(importedPath).SequenceEqual(commitBefore),
            "Expected a commit failure after backup to roll imported_patch.yml back byte-for-byte.");

        var readBackFailureBackup = new BackupService();
        var readBackFailureReader = new R3Config.Svc.ReadSvc();
        var readBackFailureTransaction = new PatchFileTransactionService(
            readBackFailureBackup,
            readBackFailureReader,
            new CfgRead(),
            commitFile: (stagedPath, targetPath) =>
            {
                File.Move(stagedPath, targetPath, overwrite: true);
                File.WriteAllText(targetPath, "Version: [broken", Encoding.UTF8);
            });
        var readBackFailureUseCase = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(readBackFailureReader),
            new Rpcs3MutationGuard(() => false),
            readBackFailureTransaction,
            readBackFailureReader);
        var readBackBefore = File.ReadAllBytes(importedPath);
        var readBackFailureResult = readBackFailureUseCase.Import(
            install,
            newManaged);
        Assert(!readBackFailureResult.IsOk &&
               File.ReadAllText(importedPath, Encoding.UTF8) == "Version: [broken" &&
               !File.ReadAllBytes(importedPath).SequenceEqual(readBackBefore),
            "Expected post-commit read-back failure to preserve content that no longer matches either the prepared commit or original backup.");

        var userMarkerText = CanPat(
                manualHash,
                "R3Config managed Canary patches v1 - begin",
                "0x00000300")
            .Replace(
                "    Patch Version: 1.0\r\n",
                "    Notes: \"# R3Config managed Canary patches v1 - end\"\r\n" +
                "    Patch Version: 1.0\r\n",
                StringComparison.Ordinal) +
            "# R3Config managed Canary patches v1 - begin\r\n";
        File.WriteAllText(importedPath, userMarkerText, Encoding.UTF8);
        var userMarkerBefore = File.ReadAllBytes(importedPath);
        var userMarkerBackups = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install)).Count;
        var userMarkerResult = useCase.Remove(install);
        Assert(userMarkerResult.IsOk &&
               userMarkerResult.BkpPath is null &&
               File.ReadAllBytes(importedPath).SequenceEqual(userMarkerBefore) &&
               backup.ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count ==
               userMarkerBackups,
            "Expected marker text in a patch title, Notes value, or ordinary comment not to grant managed ownership.");

        File.WriteAllText(importedPath, manual, Encoding.UTF8);
        Assert(useCase.Import(install, newManaged).IsOk,
            "Expected a valid managed block fixture before malformed-marker tests.");
        var managedFixture = File.ReadAllText(importedPath, Encoding.UTF8);
        const string managedEnd =
            "  # R3Config managed Canary patches v1 - end";
        var incompleteMarker = managedFixture.Replace(
            managedEnd,
            string.Empty,
            StringComparison.Ordinal);
        File.WriteAllText(importedPath, incompleteMarker, Encoding.UTF8);
        var incompleteBefore = File.ReadAllBytes(importedPath);
        var incompleteResult = useCase.Remove(install);
        Assert(!incompleteResult.IsOk &&
               File.ReadAllBytes(importedPath).SequenceEqual(incompleteBefore),
            "Expected an incomplete managed marker pair to be rejected without modifying the user file.");

        var unknownMarker = managedFixture.Replace(
            "patches v1 -",
            "patches v9 -",
            StringComparison.Ordinal);
        File.WriteAllText(importedPath, unknownMarker, Encoding.UTF8);
        var unknownBefore = File.ReadAllBytes(importedPath);
        var unknownResult = useCase.Remove(install);
        Assert(!unknownResult.IsOk &&
               File.ReadAllBytes(importedPath).SequenceEqual(unknownBefore),
            "Expected an unknown managed marker version to be rejected without mutation.");

        File.WriteAllText(importedPath, manual, Encoding.UTF8);
        Assert(useCase.Import(install, newManaged).IsOk,
            "Expected a manual plus managed fixture before preserving removal.");
        var preservingRemove = useCase.Remove(install);
        var preservedAfterRemove = File.ReadAllText(
            importedPath,
            Encoding.UTF8);
        Assert(preservingRemove.IsOk &&
               preservingRemove.BkpPath is not null &&
               File.Exists(importedPath) &&
               preservedAfterRemove.Contains(
                   "Manual Existing Patch",
                   StringComparison.Ordinal) &&
               !preservedAfterRemove.Contains(
                   "R3Config managed Canary patches v1 - begin",
                   StringComparison.Ordinal) &&
               File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
            "Expected transactional Canary removal to preserve manual definitions and patch.yml.");

        File.Delete(importedPath);
        Assert(useCase.Import(install, oldManaged).IsOk,
            "Expected a fully managed fixture before transactional deletion.");
        var deleteResult = useCase.Remove(install);
        Assert(deleteResult.IsOk &&
               deleteResult.BkpPath is not null &&
               !File.Exists(importedPath),
            "Expected a fully and structurally owned imported_patch.yml to be deleted through the central transaction.");

        Assert(useCase.Import(install, oldManaged).IsOk,
            "Expected a fully managed fixture before delete rollback.");
        var deleteRollbackBefore = File.ReadAllBytes(importedPath);
        var deleteFailureBackup = new BackupService();
        var deleteFailureReader = new R3Config.Svc.ReadSvc();
        var deleteFailureTxn = new PatchFileTransactionService(
            deleteFailureBackup,
            deleteFailureReader,
            new CfgRead(),
            deleteFile: targetPath =>
            {
                File.Delete(targetPath);
                throw new IOException(
                    "Simulated managed Canary delete commit failure.");
            });
        var deleteFailureCase = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(deleteFailureReader),
            new Rpcs3MutationGuard(() => false),
            deleteFailureTxn,
            deleteFailureReader);
        var deleteFailureResult = deleteFailureCase.Remove(install);
        Assert(!deleteFailureResult.IsOk &&
               File.Exists(importedPath) &&
               File.ReadAllBytes(importedPath).SequenceEqual(
                   deleteRollbackBefore),
            "Expected a failed Canary delete commit to restore imported_patch.yml byte-for-byte.");
        Assert(!Directory.EnumerateFiles(
                patchesPath,
                "*.r3stage",
                SearchOption.TopDirectoryOnly).Any(),
            "Expected failed Canary deletion to leave no transaction stage files.");

        File.Delete(importedPath);
        var snapshotsBeforeBlocked = backup.ListBkp(
            Rpcs3AppPaths.BkpRoot(install)).Count;
        var blockedReader = new R3Config.Svc.ReadSvc();
        var blockedBackup = new BackupService();
        var blockedUseCase = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(blockedReader),
            new Rpcs3MutationGuard(() => true),
            new PatchFileTransactionService(
                blockedBackup,
                blockedReader,
                new CfgRead()),
            blockedReader);
        MutFails(
            () => blockedUseCase.Import(install, newManaged),
            MutBlock.Running,
            "Expected a running RPCS3 process to block Canary import before normalization or writes.");
        Assert(!File.Exists(importedPath) &&
               !Directory.EnumerateFiles(
                   patchesPath,
                   "*.r3stage",
                   SearchOption.TopDirectoryOnly).Any() &&
               backup.ListBkp(Rpcs3AppPaths.BkpRoot(install)).Count == snapshotsBeforeBlocked &&
               File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
            "Expected blocked Canary import to create no target, stage, backup, or patch.yml change.");

        Console.WriteLine("Canary preserving transaction tests PASS.");
    }

    static string CanPat(
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

    static int CountOf(
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

    static string TxnCat(
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

}
