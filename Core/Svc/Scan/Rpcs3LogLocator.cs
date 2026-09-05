using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class Rpcs3LogLocator
{
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
}
