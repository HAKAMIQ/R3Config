using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

// An optional presentation asset from the already discovered game, never a scan.
public static class Rpcs3GameIconLocator
{
    public static string? Find(Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(game);
        try
        {
            var root = RuntimePathPolicy.RequireLocal(game.InstPath, "Game icon");
            foreach (var candidate in new[]
                     {
                         Path.Combine(root, "ICON0.PNG"),
                         Path.Combine(root, "PS3_GAME", "ICON0.PNG")
                     })
            {
                // Reject junctions/symlinks all the way to the local drive so
                // a seemingly local asset cannot redirect the read to a share.
                if (File.Exists(candidate) && IsRegularLocalFile(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (Exception exception) when (exception is
                   IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
                   NotSupportedException or System.Security.SecurityException)
        {
            // Optional icons must not make an otherwise valid game unavailable.
        }

        return null;
    }

    private static bool IsRegularLocalFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            return false;
        }

        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
        {
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
