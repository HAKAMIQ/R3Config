using System.IO;
using System.Text;
using R3Config.Svc;
using R3Config.Mdl;
using R3Config.Application;
using R3Config.Tests;

internal static partial class Program
{
    static void Bkp_LegacyRetention(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "backup-legacy-retention");
        var customConfigsPath = Path.Combine(root, "config", "custom_configs");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var oldRoot = Rpcs3AppPaths.OldBkp(install);
        var target = Path.Combine(customConfigsPath, "config_BLUS10001.yml");
        File.WriteAllText(
            target,
            "Video:\r\n  Write Color Buffers: false\r\n",
            Encoding.UTF8);
        var backupService = new BackupService();
        var useCase = new BackupUseCase(
            backupService,
            new Rpcs3MutationGuard(() => false));

        BackupSnapshot Persist(string rootPath, int minute)
        {
            var built = BackupSnapshot(backupService, rootPath, [target]);
            var manifest = new BackupManifest(
                new DateTimeOffset(2026, 8, 1, 0, minute, 0, TimeSpan.Zero),
                built.Files);
            BackupManifest(built.DirPath, manifest);
            return new BackupSnapshot(built.DirPath, manifest.MadeAt, manifest.Files);
        }

        var legacy = Enumerable.Range(0, 4)
            .Select(minute => Persist(oldRoot, minute))
            .ToArray();
        var legacyBytes = legacy
            .Select(snapshot => File.ReadAllBytes(snapshot.Files.Single().BkpPath))
            .ToArray();
        var foreign = Persist(backupRoot, 4);
        var foreignFile = Path.Combine(foreign.DirPath, "user-notes.txt");
        File.WriteAllText(foreignFile, "user data", Encoding.UTF8);
        var nested = Persist(backupRoot, 5);
        var nestedFile = Path.Combine(nested.DirPath, "sub", "keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedFile)!);
        File.WriteAllText(nestedFile, "nested user data", Encoding.UTF8);
        var managed = Enumerable.Range(10, 12)
            .Select(minute => Persist(backupRoot, minute))
            .ToArray();
        var userFolder = Path.Combine(backupRoot, "user-folder");
        Directory.CreateDirectory(userFolder);
        File.WriteAllText(Path.Combine(userFolder, "keep.txt"), "keep", Encoding.UTF8);

        Assert(useCase.List(install).Count == 18,
            "Expected legacy and managed backups to remain listed together.");

        MustFail<InvalidDataException>(
            () => useCase.Delete(install, legacy[0]),
            "Expected explicit deletion of a legacy R3ConfigBackups snapshot to be rejected.");
        Assert(Directory.Exists(legacy[0].DirPath),
            "Expected a rejected legacy deletion to keep the snapshot directory.");

        MustFail<InvalidDataException>(
            () => useCase.Delete(install, foreign),
            "Expected deletion of a snapshot holding unmanaged files to be rejected.");
        Assert(File.Exists(foreignFile),
            "Expected a rejected deletion to keep unmanaged files inside the snapshot.");

        var deleted = useCase.DeleteOld(install);
        var expectedKept = managed
            .OrderByDescending(snapshot => snapshot.MadeAt)
            .Take(BackupUseCase.DefaultRetentionCount)
            .ToArray();
        var expectedDeleted = managed.Except(expectedKept).ToArray();

        Assert(deleted == expectedDeleted.Length &&
               expectedKept.All(snapshot => Directory.Exists(snapshot.DirPath)) &&
               expectedDeleted.All(snapshot => !Directory.Exists(snapshot.DirPath)),
            "Expected cleanup to keep the newest managed R3Bkp snapshots and delete only older managed ones.");
        Assert(legacy.Select((snapshot, index) =>
                       Directory.Exists(snapshot.DirPath) &&
                       File.ReadAllBytes(snapshot.Files.Single().BkpPath)
                           .SequenceEqual(legacyBytes[index]))
                   .All(kept => kept),
            "Expected cleanup to leave every legacy R3ConfigBackups snapshot byte-for-byte intact.");
        Assert(File.Exists(foreignFile) &&
               File.Exists(nestedFile) &&
               File.Exists(Path.Combine(userFolder, "keep.txt")),
            "Expected cleanup to leave unmanaged files and folders under R3Bkp untouched.");
        Assert(useCase.DeleteOld(install) == 0,
            "Expected cleanup to be idempotent once only protected snapshots exceed retention.");

        Console.WriteLine("Legacy backup protection and managed-only retention PASS.");
    }

    static void Bkp_PreRestore(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "backup-pre-restore");
        var customConfigsPath = Path.Combine(root, "config", "custom_configs");
        var patchesPath = Path.Combine(root, "patches");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(customConfigsPath);
        Directory.CreateDirectory(patchesPath);
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var config = Path.Combine(customConfigsPath, "config_BLUS10001.yml");
        var removed = Path.Combine(customConfigsPath, "config_BLUS10002.yml");
        var imported = Path.Combine(patchesPath, "imported_patch.yml");
        string[] targets = [config, removed, imported];
        const string configBackup = "Video:\r\n  Write Color Buffers: false\r\n";
        const string importedBackup = "Version: 1.2\r\n";
        File.WriteAllText(config, configBackup, Encoding.UTF8);
        File.WriteAllText(removed, configBackup, Encoding.UTF8);
        File.WriteAllText(imported, importedBackup, Encoding.UTF8);
        var snapshot = BackupSnapshot(new BackupService(), backupRoot, targets);

        void SetCurrent(string tag)
        {
            File.WriteAllText(config, $"Video:\r\n  Write Color Buffers: {tag}\r\n", Encoding.UTF8);
            File.WriteAllText(imported, $"Version: 1.2\r\n# current {tag}\r\n", Encoding.UTF8);
            if (File.Exists(removed))
            {
                File.Delete(removed);
            }
        }

        byte[]?[] Read() =>
            [.. targets.Select(path => File.Exists(path) ? File.ReadAllBytes(path) : null)];

        bool Same(byte[]?[] left, byte[]?[] right) =>
            left.Zip(right).All(pair =>
                pair.First is null
                    ? pair.Second is null
                    : pair.Second is not null && pair.First.SequenceEqual(pair.Second));

        HashSet<string> Dirs() =>
            new BackupService().ListBkp(backupRoot)
                .Select(item => Path.GetFullPath(item.DirPath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        BackupSnapshot NewSnapshot(HashSet<string> before) =>
            new BackupService().ListBkp(backupRoot)
                .Single(item => !before.Contains(Path.GetFullPath(item.DirPath)));

        var useCase = new BackupUseCase(
            new BackupService(),
            new Rpcs3MutationGuard(() => false));

        SetCurrent("success");
        var current = Read();
        var before = Dirs();
        useCase.Restore(install, snapshot);
        Assert(File.ReadAllText(config, Encoding.UTF8) == configBackup &&
               File.ReadAllText(removed, Encoding.UTF8) == configBackup &&
               File.ReadAllText(imported, Encoding.UTF8) == importedBackup,
            "Expected the selected snapshot to be restored.");
        var preRestore = NewSnapshot(before);
        Assert(preRestore.Files.Count == targets.Length &&
               preRestore.Files.Single(entry => string.Equals(
                   Path.GetFullPath(entry.TgtPath),
                   Path.GetFullPath(removed),
                   StringComparison.OrdinalIgnoreCase)) is { Existed: false },
            "Expected restore to save one snapshot of the current state of every target first.");

        useCase.Restore(install, preRestore);
        Assert(Same(Read(), current),
            "Expected the pre-restore snapshot to undo the restore byte-for-byte, including an absent target.");

        SetCurrent("commit-failure");
        var commitFailureBefore = Read();
        before = Dirs();
        var commitIndex = 0;
        var rollbackFailure = new BackupService(
            commitRestoreOperation: (stagePath, targetPath, shouldExist) =>
            {
                if (++commitIndex == 2)
                {
                    throw new IOException("Simulated restore commit failure.");
                }

                RstTest(stagePath, targetPath, shouldExist);
            },
            rollbackRestoreOperation: (_, _, _) =>
                throw new UnauthorizedAccessException("Simulated restore rollback failure."));
        MustFail<AggregateException>(
            () => new BackupUseCase(
                    rollbackFailure,
                    new Rpcs3MutationGuard(() => false))
                .Restore(install, snapshot),
            "Expected a commit failure followed by a rollback failure to be surfaced.");
        Assert(!HasRst(Path.Combine(root, "config"), patchesPath),
            "Expected a failed restore to clean every stage.");
        useCase.Restore(install, NewSnapshot(before));
        Assert(Same(Read(), commitFailureBefore),
            "Expected the pre-restore snapshot to recover user data even when restore rollback fails.");

        var blockedRoot = Path.Combine(tempRoot, "backup-pre-restore-blocked");
        var blockedCustom = Path.Combine(blockedRoot, "config", "custom_configs");
        var blockedExe = Path.Combine(blockedRoot, "rpcs3.exe");
        Directory.CreateDirectory(blockedCustom);
        File.WriteAllBytes(blockedExe, []);
        var blockedInstall = Rpcs3InstallService.FromExe(blockedExe);
        var blockedTarget = Path.Combine(blockedCustom, "config_BLUS10003.yml");
        File.WriteAllText(blockedTarget, configBackup, Encoding.UTF8);
        var legacySnapshot = BackupSnapshot(
            new BackupService(),
            Rpcs3AppPaths.OldBkp(blockedInstall),
            [blockedTarget]);
        File.WriteAllText(blockedTarget, "current-blocked", Encoding.UTF8);
        var blockedBefore = File.ReadAllBytes(blockedTarget);
        var blocker = Rpcs3AppPaths.BkpRoot(blockedInstall);
        File.WriteAllText(blocker, "not a directory", Encoding.UTF8);
        MustFail<Exception>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => false))
                .Restore(blockedInstall, legacySnapshot),
            "Expected restore to stop when the backup root is not a usable directory.");
        Assert(File.ReadAllBytes(blockedTarget).SequenceEqual(blockedBefore) &&
               File.ReadAllText(blocker, Encoding.UTF8) == "not a directory" &&
               !HasRst(Path.Combine(blockedRoot, "config")),
            "Expected a rejected backup root to leave every target unchanged with no stages.");

        File.Delete(blocker);
        var denied = Directory.CreateDirectory(blocker);
        var deny = new System.Security.AccessControl.FileSystemAccessRule(
            System.Security.Principal.WindowsIdentity.GetCurrent().User!,
            System.Security.AccessControl.FileSystemRights.CreateDirectories |
            System.Security.AccessControl.FileSystemRights.CreateFiles,
            System.Security.AccessControl.AccessControlType.Deny);
        var acl = denied.GetAccessControl();
        acl.AddAccessRule(deny);
        denied.SetAccessControl(acl);
        try
        {
            MustFail<UnauthorizedAccessException>(
                () => new BackupUseCase(
                        new BackupService(),
                        new Rpcs3MutationGuard(() => false))
                    .Restore(blockedInstall, legacySnapshot),
                "Expected restore to stop when the current state cannot be backed up first.");
        }
        finally
        {
            acl = denied.GetAccessControl();
            acl.RemoveAccessRule(deny);
            denied.SetAccessControl(acl);
        }

        Assert(File.ReadAllBytes(blockedTarget).SequenceEqual(blockedBefore) &&
               !Directory.EnumerateFileSystemEntries(blocker).Any() &&
               !HasRst(Path.Combine(blockedRoot, "config")),
            "Expected a failed pre-restore backup to leave every target unchanged with no stages.");

        SetCurrent("running");
        before = Dirs();
        MutFails(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() => true))
                .Restore(install, snapshot),
            MutBlock.Running,
            "Expected RPCS3 running to block restore.");
        var externalChecks = 0;
        MustFail<IOException>(
            () => new BackupUseCase(
                    new BackupService(),
                    new Rpcs3MutationGuard(() =>
                    {
                        if (++externalChecks == 2)
                        {
                            File.WriteAllText(config, "external-during-stage", Encoding.UTF8);
                        }

                        return false;
                    }))
                .Restore(install, snapshot),
            "Expected restore to reject a target changed during staging.");
        Assert(Dirs().SetEquals(before),
            "Expected blocked or rejected restores not to create pre-restore snapshots.");

        Console.WriteLine("Pre-restore snapshot and restore undo regressions PASS.");
    }
}
