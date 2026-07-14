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
            R3Paths.BkpRoot(install));
    }

    public void Restore(
        R3Inst install,
        BkpSnap snapshot)
    {
        var backupRoot = Path.GetFullPath(
            R3Paths.BkpRoot(install));
        var snapshotPath = Path.GetFullPath(
            snapshot.DirPath);
        if (!IsChild(backupRoot, snapshotPath))
        {
            throw new InvalidDataException(
                "The selected backup snapshot is outside this RPCS3 installation's R3ConfigBackups directory.");
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
