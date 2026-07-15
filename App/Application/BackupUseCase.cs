using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class BackupUseCase(
    BackupService backupService,
    IRpcs3MutationGuard mutationGuard)
{
    public IReadOnlyList<BackupSnapshot> List(
        Rpcs3InstallDescriptor install)
    {
        return backupService.ListBkp(
                Rpcs3AppPaths.BkpRoot(install))
            .Concat(backupService.ListBkp(
                Rpcs3AppPaths.OldBkp(install)))
            .OrderByDescending(item => item.MadeAt)
            .ThenBy(item => item.DirPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Restore(
        Rpcs3InstallDescriptor install,
        BackupSnapshot snapshot)
    {
        var backupRoot = Path.GetFullPath(
            Rpcs3AppPaths.BkpRoot(install));
        var snapshotPath = Path.GetFullPath(
            snapshot.DirPath);
        var oldRoot = Path.GetFullPath(
            Rpcs3AppPaths.OldBkp(install));
        if (!IsChild(backupRoot, snapshotPath) &&
            !IsChild(oldRoot, snapshotPath))
        {
            throw new InvalidDataException(
                "The selected backup snapshot is outside this RPCS3 installation's managed backup directories.");
        }

        mutationGuard.CheckCan(
            install,
            snapshot.Files
                .Select(file => file.TgtPath)
                .ToArray());

        backupService.Restore(snapshot);
    }

    private static bool IsChild(
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
