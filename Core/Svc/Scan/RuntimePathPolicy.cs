using System;
using System.IO;

namespace R3Config.Svc;

public static class RuntimePathPolicy
{
    public static string RequireLocal(
        string path,
        string pathKind,
        bool allowRemovable = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"{pathKind} path is empty.",
                nameof(path));
        }

        if (IsDevicePath(path) || IsUncPath(path))
        {
            throw new InvalidDataException(
                $"{pathKind} must use a local filesystem path.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException(
                $"{pathKind} must use a fully qualified path.");
        }

        var fullPath = Path.GetFullPath(path);
        if (IsDevicePath(fullPath) || IsUncPath(fullPath))
        {
            throw new InvalidDataException(
                $"{pathKind} must use a local filesystem path.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return fullPath;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidDataException(
                $"{pathKind} has no local drive root.");
        }

        var drive = new DriveInfo(root);
        var allowed = drive.DriveType == DriveType.Fixed ||
                      allowRemovable &&
                      drive.DriveType == DriveType.Removable;

        if (!allowed)
        {
            throw new InvalidDataException(
                $"{pathKind} must be stored on a local fixed or removable drive.");
        }

        return fullPath;
    }

    private static bool IsUncPath(string path)
    {
        return path.StartsWith(
            @"\\",
            StringComparison.Ordinal);
    }

    private static bool IsDevicePath(string path)
    {
        return path.StartsWith(
                   @"\\?\",
                   StringComparison.Ordinal) ||
               path.StartsWith(
                   @"\\.\",
                   StringComparison.Ordinal) ||
               path.StartsWith(
                   @"\??\",
                   StringComparison.Ordinal);
    }
}
