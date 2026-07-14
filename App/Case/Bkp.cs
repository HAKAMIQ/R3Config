using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class BkpCase(
    BkpSvc backupService,
    IMutGd mutationGuard)
{
    public IReadOnlyList<BkpSnap> List(
        R3Inst install)
    {
        return backupService.ListBkp(
                R3Paths.BkpRoot(install))
            .Concat(backupService.ListBkp(
                R3Paths.OldBkp(install)))
            .OrderByDescending(item => item.MadeAt)
            .ThenBy(item => item.DirPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Restore(
        R3Inst install,
        BkpSnap snapshot)
    {
        var backupRoot = Path.GetFullPath(
            R3Paths.BkpRoot(install));
        var snapshotPath = Path.GetFullPath(
            snapshot.DirPath);
        var oldRoot = Path.GetFullPath(
            R3Paths.OldBkp(install));
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
