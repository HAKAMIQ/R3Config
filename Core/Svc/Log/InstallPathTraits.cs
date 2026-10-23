using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Facts about WHERE a run's data folder is, from the path RPCS3 printed. Only
// the location shape is stated (a named folder, a drive root); what such a
// location may cause is not concluded here.
public static class InstallPathTraits
{
    public const string ProgramFiles = "ProgramFilesFolder";
    public const string CloudSync = "CloudSyncFolder";
    public const string ArchiveTemp = "ArchiveTempFolder";
    public const string DriveRoot = "DriveRoot";

    private static readonly string[] SyncFolders = ["OneDrive", "Dropbox", "Google Drive", "iCloudDrive", "iCloud Drive", "MEGA", "pCloud Drive", "Box"];

    public static IReadOnlyList<string> Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var traits = new List<string>();
        if (segments.Any(part => part.Equals("Program Files", StringComparison.OrdinalIgnoreCase) ||
                                 part.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase)))
        {
            traits.Add(ProgramFiles);
        }

        if (segments.Any(part => SyncFolders.Contains(part, StringComparer.OrdinalIgnoreCase) ||
                                 part.StartsWith("OneDrive - ", StringComparison.OrdinalIgnoreCase)))
        {
            traits.Add(CloudSync);
        }

        if (segments.Any(part => part.StartsWith("Rar$", StringComparison.Ordinal)))
        {
            traits.Add(ArchiveTemp);
        }

        if (segments.Length <= 1 && (segments.Length == 0 || segments[0].EndsWith(':')))
        {
            traits.Add(DriveRoot);
        }

        return traits;
    }
}

#pragma warning restore IDE0130
