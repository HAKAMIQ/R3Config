using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class Rpcs3LogLocator
{
    internal static IEnumerable<string> CandidateRoots(Rpcs3InstallDescriptor install) =>
        new[]
        {
            install.RootPath,
            install.DataRoot,
            Path.GetDirectoryName(install.CfgPath),
            install.CfgPath,
            Path.Combine(install.RootPath, "portable")
        }.Where(path => !string.IsNullOrWhiteSpace(path))
         .Cast<string>()
         .Distinct(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> PlainLogCandidates(Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return CandidateRoots(install).SelectMany(PlainLogPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> PlainLogPaths(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        return PlainLogPaths(install.RootPath);
    }

    public static IReadOnlyList<string> PlainLogPaths(
        string rpcs3RootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rpcs3RootPath);

        var rootPath = Path.GetFullPath(
            rpcs3RootPath);

        return
        [
            Path.Combine(
                rootPath,
                "log",
                "RPCS3.log"),
            Path.Combine(
                rootPath,
                "RPCS3.log")
        ];
    }

    public static string PreferredPlainLogPath(
        Rpcs3InstallDescriptor install)
    {
        return PlainLogPaths(install)[0];
    }

    public static string PreferredExistingPlainLogPath(
        Rpcs3InstallDescriptor install)
    {
        string? selected = null;
        var selectedWriteTimeUtc = DateTime.MinValue;

        foreach (var candidate in PlainLogCandidates(install))
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                using var stream = new FileStream(
                    candidate,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var writeTimeUtc = File.GetLastWriteTimeUtc(candidate);
                if (selected is null ||
                    writeTimeUtc > selectedWriteTimeUtc)
                {
                    selected = candidate;
                    selectedWriteTimeUtc = writeTimeUtc;
                }
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                // An unreadable candidate cannot safely displace a readable one.
            }
        }

        return selected ?? PreferredPlainLogPath(install);
    }
}
