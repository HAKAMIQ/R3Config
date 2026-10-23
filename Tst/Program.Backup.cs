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
    static void Bkp_Del(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "backup-delete-retention");
        var configPath = Path.Combine(root, "config");
        var customConfigsPath = Path.Combine(configPath, "custom_configs");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var oldRoot = Rpcs3AppPaths.OldBkp(install);
        var target = Path.Combine(
            customConfigsPath,
            "config_BLUS10001.yml");
        File.WriteAllText(
            target,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var targetBefore = File.ReadAllBytes(target);
        var backupService = new BackupService();
        var useCase = new BackupUseCase(
            backupService,
            new Rpcs3MutationGuard(() => false));

        for (var index = 0; index < 14; index++)
        {
            var rootPath = index < 9
                ? backupRoot
                : oldRoot;
            var snapshot = BackupSnapshot(
                backupService,
                rootPath,
                [target]);
            var madeAt = new DateTimeOffset(
                2026,
                8,
                1,
                0,
                index,
                0,
                TimeSpan.Zero);
            BackupManifest(
                snapshot.DirPath,
                new BackupManifest(
                    madeAt,
                    snapshot.Files));
        }

        var listed = useCase.List(install);
        Assert(listed.Count == 14,
            "Expected backup listing to combine current and legacy managed backup roots.");

        var selected = listed[5];
        Assert(useCase.Delete(install, selected) &&
               !Directory.Exists(selected.DirPath) &&
               useCase.List(install).Count == 13,
            "Expected explicit deletion to remove only the selected managed backup snapshot.");
        Assert(File.ReadAllBytes(target).SequenceEqual(targetBefore),
            "Expected backup deletion to leave RPCS3 target files unchanged.");

        var outside = Path.Combine(root, "outside-backup");
        Directory.CreateDirectory(outside);
        var forged = new BackupSnapshot(
            outside,
            DateTimeOffset.UtcNow,
            selected.Files);
        MustFail<InvalidDataException>(
            () => useCase.Delete(install, forged),
            "Expected backup deletion to reject a snapshot outside managed backup roots.");
        Assert(Directory.Exists(outside),
            "Expected rejected backup deletion to leave the outside directory untouched.");

        for (var index = 0; index < 5; index++)
        {
            var extra = BackupSnapshot(
                backupService,
                backupRoot,
                [target]);
            BackupManifest(
                extra.DirPath,
                new BackupManifest(
                    new DateTimeOffset(2026, 8, 1, 1, index, 0, TimeSpan.Zero),
                    extra.Files));
        }

        var beforeCleanup = useCase.List(install);
        var managedBefore = beforeCleanup
            .Where(snapshot => !snapshot.IsLegacy)
            .ToArray();
        var legacyBefore = beforeCleanup
            .Where(snapshot => snapshot.IsLegacy)
            .Select(snapshot => Path.GetFullPath(snapshot.DirPath))
            .ToArray();
        var expectedRemaining = managedBefore
            .Take(BackupUseCase.DefaultRetentionCount)
            .Select(snapshot => Path.GetFullPath(snapshot.DirPath))
            .Concat(legacyBefore)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedDeleteCount = managedBefore.Length -
                                  BackupUseCase.DefaultRetentionCount;
        var deleted = useCase.DeleteOld(install);
        var remaining = useCase.List(install);

        Assert(legacyBefore.Length == 5 &&
               expectedDeleteCount > 0 &&
               deleted == expectedDeleteCount &&
               remaining.Count == expectedRemaining.Count &&
               remaining.All(snapshot => expectedRemaining.Contains(
                   Path.GetFullPath(snapshot.DirPath))),
            "Expected backup cleanup to delete only managed snapshots older than the newest retention set and keep legacy snapshots.");
        Assert(File.ReadAllBytes(target).SequenceEqual(targetBefore),
            "Expected backup cleanup to leave RPCS3 target files unchanged.");

        Assert(useCase.DeleteOld(install) == 0 &&
               useCase.List(install).Count == expectedRemaining.Count,
            "Expected backup cleanup to be idempotent once the retention count is reached.");

        Console.WriteLine("Backup deletion and retention regressions PASS.");
    }

    static void Bkp_Rst(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "backup-restore-transaction");
        var configPath = Path.Combine(root, "config");
        var customConfigsPath = Path.Combine(configPath, "custom_configs");
        var patchesPath = Path.Combine(root, "patches");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        Directory.CreateDirectory(patchesPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var firstTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS10001.yml");
        var secondTarget = Path.Combine(
            patchesPath,
            "imported_patch.yml");
        var missingTarget = Path.Combine(
            configPath,
            "patch_config.yml");

        const string firstBackup =
            "Video:\r\n  Write Color Buffers: false\r\n";
        const string secondBackup = "Version: 1.2\r\n";
        File.WriteAllBytes(firstTarget, Encoding.UTF8.GetBytes(firstBackup));
        File.WriteAllBytes(secondTarget, Encoding.UTF8.GetBytes(secondBackup));
        var backupService = new BackupService();
        var snapshot = BackupSnapshot(
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

        Assert(File.ReadAllText(firstTarget, Encoding.UTF8) == firstBackup &&
               File.ReadAllText(secondTarget, Encoding.UTF8) == secondBackup &&
               !File.Exists(missingTarget),
            "Expected one transactional restore to recover multiple files and remove a target that was originally absent.");

        var replayFirst = File.ReadAllBytes(firstTarget);
        var replaySecond = File.ReadAllBytes(secondTarget);
        restoreUseCase.Restore(install, snapshot);
        Assert(File.ReadAllBytes(firstTarget).SequenceEqual(replayFirst) &&
               File.ReadAllBytes(secondTarget).SequenceEqual(replaySecond) &&
               !File.Exists(missingTarget),
            "Expected restoring the same snapshot again to be idempotent.");

        var payloadRaceTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS10009.yml");
        File.WriteAllText(
            payloadRaceTarget,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var payloadRaceSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [payloadRaceTarget]);
        File.WriteAllText(payloadRaceTarget, "current-payload-target", Encoding.UTF8);
        var payloadRaceBefore = File.ReadAllBytes(payloadRaceTarget);
        var payloadRaceService = new BackupService(
            beforeRestoreStageCopy: (backupPath, _) =>
                File.WriteAllText(
                    backupPath,
                    "Video:\r\n  Write Color Buffers: true\r\n",
                    Encoding.UTF8));
        MustFail<IOException>(
            () => new BackupUseCase(
                    payloadRaceService,
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, payloadRaceSnapshot),
            "Expected a backup payload changed after manifest validation to be rejected before staging.");
        Assert(File.ReadAllBytes(payloadRaceTarget).SequenceEqual(payloadRaceBefore) &&
               !HasRst(configPath, patchesPath),
            "Expected late backup payload tampering to preserve the target and leave no restore stages.");

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

                RstTest(
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

        File.WriteAllText(firstTarget, "before-late-second-target-edit", Encoding.UTF8);
        File.WriteAllText(secondTarget, "before-late-second-target-edit-2", Encoding.UTF8);
        var lateSecondFirstBefore = File.ReadAllBytes(firstTarget);
        const string lateSecondExternal = "external-before-second-target-commit";
        var lateSecondCommitIndex = 0;
        var lateSecondService = new BackupService(
            commitRestoreOperation: (stagePath, targetPath, shouldExist) =>
            {
                lateSecondCommitIndex++;
                RstTest(stagePath, targetPath, shouldExist);
                if (lateSecondCommitIndex == 1)
                {
                    File.WriteAllText(
                        secondTarget,
                        lateSecondExternal,
                        Encoding.UTF8);
                }
            });
        MustFail<AggregateException>(
            () => new BackupUseCase(
                    lateSecondService,
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, snapshot),
            "Expected a target changed after another restore commit to be rejected before its own commit.");
        Assert(File.ReadAllBytes(firstTarget).SequenceEqual(lateSecondFirstBefore) &&
               File.ReadAllText(secondTarget, Encoding.UTF8) == lateSecondExternal &&
               !HasRst(configPath, patchesPath),
            "Expected per-target restore concurrency validation to roll back owned output, preserve the external edit, and clean stages.");

        File.WriteAllText(firstTarget, "before-external-rollback-race", Encoding.UTF8);
        File.WriteAllText(secondTarget, "before-external-rollback-race-2", Encoding.UTF8);
        var rollbackRaceSecondBefore = File.ReadAllBytes(secondTarget);
        var rollbackRaceCommitIndex = 0;
        var rollbackRaceService = new BackupService(
            commitRestoreOperation: (stagePath, targetPath, shouldExist) =>
            {
                rollbackRaceCommitIndex++;
                if (rollbackRaceCommitIndex == 2)
                {
                    throw new IOException(
                        "Simulated second restore commit failure after an external edit.");
                }

                RstTest(stagePath, targetPath, shouldExist);
                File.WriteAllText(
                    firstTarget,
                    "external-after-first-restore-commit",
                    Encoding.UTF8);
            });
        MustFail<AggregateException>(
            () => new BackupUseCase(
                    rollbackRaceService,
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, snapshot),
            "Expected restore rollback to report an externally changed committed target.");
        Assert(File.ReadAllText(firstTarget, Encoding.UTF8) ==
               "external-after-first-restore-commit" &&
               File.ReadAllBytes(secondTarget).SequenceEqual(
                   rollbackRaceSecondBefore) &&
               !HasRst(configPath, patchesPath),
            "Expected restore rollback to preserve the external edit, preserve the uncommitted target, and clean stages.");

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

                RstTest(
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
        const string validationBackup =
            "Video:\r\n  Write Color Buffers: false\r\n";
        File.WriteAllText(validationTarget, validationBackup, Encoding.UTF8);

        var outsideSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        var outsideBackupFile = Path.Combine(root, "outside-backup.bin");
        File.Copy(
            outsideSnapshot.Files.Single().BkpPath,
            outsideBackupFile,
            overwrite: true);
        var outsideManifest = new BackupManifest(
            outsideSnapshot.MadeAt,
            [outsideSnapshot.Files.Single() with { BkpPath = outsideBackupFile }]);
        BackupManifest(outsideSnapshot.DirPath, outsideManifest);
        var outsideRequest = new BackupSnapshot(
            outsideSnapshot.DirPath,
            outsideManifest.MadeAt,
            outsideManifest.Files);
        var outsideBefore = File.ReadAllBytes(validationTarget);
        MustFail<InvalidDataException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, outsideRequest),
            "Expected a BackupPath outside its snapshot to be rejected.");
        Assert(File.ReadAllBytes(validationTarget).SequenceEqual(outsideBefore),
            "Expected outside-snapshot validation failure before target mutation.");

        var duplicateSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [firstTarget, secondTarget]);
        var duplicateEntries = duplicateSnapshot.Files.ToArray();
        duplicateEntries[1] = duplicateEntries[1] with
        {
            BkpPath = duplicateEntries[0].BkpPath
        };
        var duplicateManifest = new BackupManifest(
            duplicateSnapshot.MadeAt,
            duplicateEntries);
        BackupManifest(
            duplicateSnapshot.DirPath,
            duplicateManifest);
        MustFail<InvalidDataException>(
            () => new BackupService().Restore(new BackupSnapshot(
                duplicateSnapshot.DirPath,
                duplicateManifest.MadeAt,
                duplicateManifest.Files)),
            "Expected a duplicate BackupPath to be rejected before restore.");

        var badHashSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        var badHashManifest = new BackupManifest(
            badHashSnapshot.MadeAt,
            [badHashSnapshot.Files.Single() with { Sha256 = new string('0', 64) }]);
        BackupManifest(
            badHashSnapshot.DirPath,
            badHashManifest);
        var badHashBefore = File.ReadAllBytes(validationTarget);
        MustFail<InvalidDataException>(
            () => new BackupService().Restore(new BackupSnapshot(
                badHashSnapshot.DirPath,
                badHashManifest.MadeAt,
                badHashManifest.Files)),
            "Expected a mismatched backup SHA-256 to be rejected.");
        Assert(File.ReadAllBytes(validationTarget).SequenceEqual(badHashBefore) &&
               !HasRst(configPath, patchesPath),
            "Expected hash validation to fail before stages or target writes.");

        var missingBackupSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        File.Delete(missingBackupSnapshot.Files.Single().BkpPath);
        MustFail<FileNotFoundException>(
            () => new BackupService().Restore(missingBackupSnapshot),
            "Expected a missing backup payload to be rejected before restore.");

        var legacySnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        var legacyManifest = new BackupManifest(
            legacySnapshot.MadeAt,
            [legacySnapshot.Files.Single() with { Length = null, Sha256 = null }]);
        BackupManifest(
            legacySnapshot.DirPath,
            legacyManifest);
        File.WriteAllText(validationTarget, "changed-after-legacy-backup", Encoding.UTF8);
        new BackupUseCase(
            new BackupService(),
            new Rpcs3MutationGuard(() => false))
            .Restore(install, new BackupSnapshot(
                legacySnapshot.DirPath,
                legacyManifest.MadeAt,
                legacyManifest.Files));
        Assert(File.ReadAllText(validationTarget, Encoding.UTF8) == validationBackup,
            "Expected a legacy manifest without length or SHA-256 to restore with basic validation.");

        var oversizedLegacySnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        using (var oversizedPayload = new FileStream(
                   oversizedLegacySnapshot.Files.Single().BkpPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedPayload.SetLength(R3Limits.PatCatBy + 1);
        }

        var oversizedLegacyManifest = new BackupManifest(
            oversizedLegacySnapshot.MadeAt,
            [oversizedLegacySnapshot.Files.Single() with
            {
                Length = null,
                Sha256 = null
            }]);
        BackupManifest(
            oversizedLegacySnapshot.DirPath,
            oversizedLegacyManifest);
        MustFail<InvalidDataException>(
            () => new BackupService().Restore(new BackupSnapshot(
                oversizedLegacySnapshot.DirPath,
                oversizedLegacyManifest.MadeAt,
                oversizedLegacyManifest.Files)),
            "Expected an oversized legacy backup payload to fail before copy or hashing.");
        Assert(!HasRst(configPath, patchesPath),
            "Expected oversized legacy payload rejection before restore staging.");

        var entryLimitSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        var repeatedEntries = Enumerable.Repeat(
                entryLimitSnapshot.Files.Single(),
                checked((int)R3Limits.Entries + 1))
            .ToArray();
        var entryLimitManifest = new BackupManifest(
            entryLimitSnapshot.MadeAt,
            repeatedEntries);
        BackupManifest(
            entryLimitSnapshot.DirPath,
            entryLimitManifest);
        MustFail<InvalidDataException>(
            () => new BackupService().Restore(new BackupSnapshot(
                entryLimitSnapshot.DirPath,
                entryLimitManifest.MadeAt,
                entryLimitManifest.Files)),
            "Expected a backup manifest above the entry limit to fail before per-entry work.");

        var runningSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        File.WriteAllText(validationTarget, "running-process-current", Encoding.UTF8);
        var runningBefore = File.ReadAllBytes(validationTarget);
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => true))
                .Restore(install, runningSnapshot),
            MutBlock.Running,
            "Expected RPCS3 running to block restore before staging.");
        Assert(File.ReadAllBytes(validationTarget).SequenceEqual(runningBefore) &&
               !HasRst(configPath, patchesPath),
            "Expected a running-process block to leave targets and stage state unchanged.");

        File.WriteAllText(validationTarget, validationBackup, Encoding.UTF8);
        var lateGuardSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [validationTarget]);
        File.WriteAllText(validationTarget, "late-guard-current", Encoding.UTF8);
        var lateGuardBefore = File.ReadAllBytes(validationTarget);
        var guardChecks = 0;
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => ++guardChecks == 2))
                .Restore(install, lateGuardSnapshot),
            MutBlock.Running,
            "Expected RPCS3 starting during restore staging to block the commit.");
        Assert(guardChecks == 2 &&
               File.ReadAllBytes(validationTarget).SequenceEqual(lateGuardBefore) &&
               !HasRst(configPath, patchesPath),
            "Expected the final running-process guard to preserve targets and clean every restore stage.");

        var officialPatch = Path.Combine(patchesPath, "patch.yml");
        File.WriteAllText(officialPatch, "official-backup", Encoding.UTF8);
        var officialSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [officialPatch]);
        File.WriteAllText(officialPatch, "official-current", Encoding.UTF8);
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, officialSnapshot),
            MutBlock.Tgt,
            "Expected a valid or tampered backup manifest to remain unable to restore RPCS3's official patch.yml.");
        Assert(File.ReadAllText(officialPatch, Encoding.UTF8) == "official-current" &&
               !HasRst(configPath, patchesPath),
            "Expected the official patch.yml target to remain byte-for-byte unchanged.");

        var outsideTarget = Path.Combine(root, "outside-target.yml");
        File.WriteAllText(outsideTarget, "outside-backup", Encoding.UTF8);
        var outsideTargetSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [outsideTarget]);
        File.WriteAllText(outsideTarget, "outside-current", Encoding.UTF8);
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, outsideTargetSnapshot),
            MutBlock.Tgt,
            "Expected restore targets outside RPCS3 config/patches to be rejected by the central guard.");
        Assert(File.ReadAllText(outsideTarget, Encoding.UTF8) == "outside-current",
            "Expected an unsafe restore target to remain unchanged.");

        Assert(!HasRst(configPath, patchesPath),
            "Expected every restore path to clean .r3restore and .r3rollback files.");
        Console.WriteLine("Validated transactional backup restore tests PASS.");
    }

    static void Bkp_ConcurrencyOwnership(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "backup-restore-concurrency-ownership");
        var configPath = Path.Combine(root, "config");
        var customConfigsPath = Path.Combine(configPath, "custom_configs");
        var patchesPath = Path.Combine(root, "patches");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        Directory.CreateDirectory(patchesPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var vfsPath = Path.Combine(configPath, "vfs.yml");
        File.WriteAllText(vfsPath, "backup-vfs", Encoding.UTF8);
        var vfsSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [vfsPath]);
        File.WriteAllText(vfsPath, "current-vfs", Encoding.UTF8);
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, vfsSnapshot),
            MutBlock.Tgt,
            "Expected restore ownership to reject an RPCS3 config file that R3Config never writes.");
        Assert(File.ReadAllText(vfsPath, Encoding.UTF8) == "current-vfs",
            "Expected a structurally valid but unowned restore target to remain unchanged.");

        var invalidPayloadTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS30009.yml");
        File.WriteAllText(
            invalidPayloadTarget,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var invalidPayloadSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [invalidPayloadTarget]);
        var invalidPayloadEntry = invalidPayloadSnapshot.Files.Single();
        var invalidPayloadBytes = Encoding.UTF8.GetBytes("Video: [broken");
        File.WriteAllBytes(
            invalidPayloadEntry.BkpPath,
            invalidPayloadBytes);
        var invalidPayloadManifest = new BackupManifest(
            invalidPayloadSnapshot.MadeAt,
            [invalidPayloadEntry with
            {
                Length = invalidPayloadBytes.LongLength,
                Sha256 = Convert.ToHexString(
                    SHA256.HashData(invalidPayloadBytes))
            }]);
        BackupManifest(
            invalidPayloadSnapshot.DirPath,
            invalidPayloadManifest);
        const string invalidPayloadCurrent =
            "Video:\r\n  Write Color Buffers: current\r\n";
        File.WriteAllText(
            invalidPayloadTarget,
            invalidPayloadCurrent,
            Encoding.UTF8);
        MustFail<InvalidDataException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(
                    install,
                    new BackupSnapshot(
                        invalidPayloadSnapshot.DirPath,
                        invalidPayloadManifest.MadeAt,
                        invalidPayloadManifest.Files)),
            "Expected a self-consistent manifest with malformed custom-config content to fail before mutation.");
        Assert(File.ReadAllText(invalidPayloadTarget, Encoding.UTF8) ==
               invalidPayloadCurrent &&
               !HasRst(configPath, patchesPath),
            "Expected malformed backup payload validation to preserve the current target and leave no stages.");

        var changedTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS30001.yml");
        File.WriteAllText(
            changedTarget,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var changedSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [changedTarget]);
        File.WriteAllText(changedTarget, "current-before-stage", Encoding.UTF8);
        var guardChecks = 0;
        MustFail<IOException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() =>
                    {
                        if (++guardChecks == 2)
                        {
                            File.WriteAllText(
                                changedTarget,
                                "external-during-stage",
                                Encoding.UTF8);
                        }

                        return false;
                    }))
                .Restore(install, changedSnapshot),
            "Expected restore to reject a target changed externally during staging.");
        Assert(File.ReadAllText(changedTarget, Encoding.UTF8) == "external-during-stage" &&
               !HasRst(configPath, patchesPath),
            "Expected restore concurrency rejection to preserve the external edit and remove stages.");

        var disappearedTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS30002.yml");
        File.WriteAllText(
            disappearedTarget,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var disappearedSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [disappearedTarget]);
        File.WriteAllText(disappearedTarget, "current-disappeared", Encoding.UTF8);
        var disappearChecks = 0;
        MustFail<IOException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() =>
                    {
                        if (++disappearChecks == 2)
                        {
                            File.Delete(disappearedTarget);
                        }

                        return false;
                    }))
                .Restore(install, disappearedSnapshot),
            "Expected restore to reject a target that disappears during staging.");
        Assert(!File.Exists(disappearedTarget) &&
               !HasRst(configPath, patchesPath),
            "Expected restore to preserve external target removal and remove stages.");

        var appearedTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS30003.yml");
        var appearedSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [appearedTarget]);
        var appearChecks = 0;
        MustFail<IOException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() =>
                    {
                        if (++appearChecks == 2)
                        {
                            File.WriteAllText(
                                appearedTarget,
                                "external-created-during-stage",
                                Encoding.UTF8);
                        }

                        return false;
                    }))
                .Restore(install, appearedSnapshot),
            "Expected restore to reject a missing target created externally during staging.");
        Assert(File.ReadAllText(appearedTarget, Encoding.UTF8) ==
               "external-created-during-stage" &&
               !HasRst(configPath, patchesPath),
            "Expected restore to preserve an externally created target and remove stages.");

        var lockedTarget = Path.Combine(
            customConfigsPath,
            "config_BLUS30004.yml");
        File.WriteAllText(
            lockedTarget,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var lockedSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [lockedTarget]);
        File.WriteAllText(lockedTarget, "current-locked", Encoding.UTF8);
        FileStream? targetLock = null;
        var lockChecks = 0;
        try
        {
            MustFail<IOException>(
                () => new BackupUseCase(
                        new BackupService(),
                        new Rpcs3MutationGuard(() =>
                        {
                            if (++lockChecks == 2)
                            {
                                targetLock = new FileStream(
                                    lockedTarget,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.None);
                            }

                            return false;
                        }))
                    .Restore(install, lockedSnapshot),
                "Expected restore to fail safely when a target becomes locked during staging.");
        }
        finally
        {
            targetLock?.Dispose();
        }

        Assert(File.ReadAllText(lockedTarget, Encoding.UTF8) == "current-locked" &&
               !HasRst(configPath, patchesPath),
            "Expected a late target lock to preserve content and remove restore stages.");

        var titlePatchPath = BundledCheatPatchService.GetPath(
            patchesPath,
            "BLUS30005");
        File.WriteAllText(
            titlePatchPath,
            BundledCheatPatchService.ManMark + "\r\nVersion: 1.2\r\n",
            Encoding.UTF8);
        var titleSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [titlePatchPath]);
        File.WriteAllText(
            titlePatchPath,
            "Version: 1.2\r\n# user-owned title patch\r\n",
            Encoding.UTF8);
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, titleSnapshot),
            MutBlock.Tgt,
            "Expected restore to reject an unmarked current title-specific patch file.");
        Assert(File.ReadAllText(titlePatchPath, Encoding.UTF8).Contains(
                   "user-owned",
                   StringComparison.Ordinal),
            "Expected title-specific restore ownership rejection to preserve the user file.");

        File.WriteAllText(
            titlePatchPath,
            BundledCheatPatchService.ManMark + "\r\nVersion: 1.2\r\n",
            Encoding.UTF8);
        var tamperedTitleSnapshot = BackupSnapshot(
            new BackupService(),
            backupRoot,
            [titlePatchPath]);
        var tamperedEntry = tamperedTitleSnapshot.Files.Single();
        var tamperedPayload = Encoding.UTF8.GetBytes(
            "Version: 1.2\r\n# marker removed from backup\r\n");
        File.WriteAllBytes(tamperedEntry.BkpPath, tamperedPayload);
        var tamperedTitleManifest = new BackupManifest(
            tamperedTitleSnapshot.MadeAt,
            [tamperedEntry with
            {
                Length = tamperedPayload.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(tamperedPayload))
            }]);
        BackupManifest(
            tamperedTitleSnapshot.DirPath,
            tamperedTitleManifest);
        MustFail<InvalidDataException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, new BackupSnapshot(
                    tamperedTitleSnapshot.DirPath,
                    tamperedTitleManifest.MadeAt,
                    tamperedTitleManifest.Files)),
            "Expected a self-consistent but unmarked managed title backup payload to be rejected.");
        Assert(BundledCheatPatchService.IsMan(titlePatchPath) &&
               !HasRst(configPath, patchesPath),
            "Expected tampered managed-title backup rejection before target mutation with clean stages.");

        Console.WriteLine("Backup restore concurrency and ownership regressions PASS.");
    }

    static BackupSnapshot BackupSnapshot(
        BackupService service,
        string backupRoot,
        IReadOnlyList<string> targets)
    {
        var manifest = service.BuildBkp(backupRoot, targets);
        var directory = Path.GetDirectoryName(
            manifest.Files[0].BkpPath)
            ?? throw new InvalidOperationException(
                "Expected a snapshot directory for the backup manifest.");

        return new BackupSnapshot(
            directory,
            manifest.MadeAt,
            manifest.Files);
    }

    static void BackupManifest(
        string directory,
        BackupManifest manifest)
    {
        File.WriteAllText(
            Path.Combine(directory, "manifest.json"),
            JsonSerializer.Serialize(
                manifest,
                TestJson.IndentedIgnoreNull),
            Encoding.UTF8);
    }

    static void RstTest(
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

    static bool HasRst(params string[] roots)
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

}
