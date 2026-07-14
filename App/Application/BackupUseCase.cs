using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public sealed class BackupUseCase(
    BackupService backupService,
    IRpcs3MutationGuard mutationGuard)
{
    public IReadOnlyList<BackupSnapshot> List(
        Rpcs3InstallDescriptor install)
    {
        return backupService.ListBackups(
            Rpcs3AppPaths.BackupRoot(install));
    }

    public void Restore(
        Rpcs3InstallDescriptor install,
        BackupSnapshot snapshot)
    {
        var backupRoot = Path.GetFullPath(
            Rpcs3AppPaths.BackupRoot(install));
        var snapshotPath = Path.GetFullPath(
            snapshot.DirectoryPath);
        if (!IsChildPath(backupRoot, snapshotPath))
        {
            throw new InvalidDataException(
                "The selected backup snapshot is outside this RPCS3 installation's R3ConfigBackups directory.");
        }

        mutationGuard.EnsureCanMutate(
            install,
            snapshot.Files
                .Select(file => file.TargetPath)
                .ToArray());

        backupService.Restore(snapshot);
    }

    private static bool IsChildPath(
        string parentPath,
        string childPath)
    {
        var relative = Path.GetRelativePath(
            parentPath,
            childPath);

        return !Path.IsPathRooted(relative) &&
               !relative.Equals(".", StringComparison.Ordinal) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.AltDirectorySeparatorChar}",
                   StringComparison.Ordinal);
    }
}
