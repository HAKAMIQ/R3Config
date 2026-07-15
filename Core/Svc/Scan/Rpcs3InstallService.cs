using System;
using System.Diagnostics;
using System.IO;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class Rpcs3InstallService
{
    public static Rpcs3InstallDescriptor FromExe(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException(
                "Select rpcs3.exe first.",
                nameof(exePath));
        }

        var fullExePath = Path.GetFullPath(exePath);

        if (!File.Exists(fullExePath))
        {
            throw new FileNotFoundException(
                "rpcs3.exe was not found.",
                fullExePath);
        }

        if (!string.Equals(
                Path.GetFileName(fullExePath),
                "rpcs3.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected file must be rpcs3.exe.");
        }

        var rootPath = Path.GetDirectoryName(fullExePath)
            ?? throw new InvalidOperationException(
                "Could not detect RPCS3 root path.");

        var dataRootPath = GetData(rootPath);

        var configPath = Path.Combine(
            dataRootPath,
            "config");

        var patchesPath = Path.Combine(
            dataRootPath,
            "patches");

        return new Rpcs3InstallDescriptor(
            RootPath: rootPath,
            ExePath: fullExePath,
            CfgPath: configPath,
            CustPath: Path.Combine(
                configPath,
                "custom_configs"),
            PatPath: patchesPath,
            PatCfg: Path.Combine(
                configPath,
                "patch_config.yml"),
            DataRoot: dataRootPath);
    }

    private static string GetData(string rootPath)
    {
        // RPCS3 checks <exe>/portable before RPCS3_CONFIG_DIR.
        var portablePath = Path.Combine(rootPath, "portable");
        if (Directory.Exists(portablePath))
        {
            return Path.GetFullPath(portablePath);
        }

        var configuredPath = Environment.GetEnvironmentVariable(
            "RPCS3_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return rootPath;
        }

        var candidate = configuredPath.Trim();
        return Path.GetFullPath(
            Path.IsPathRooted(candidate)
                ? candidate
                : Path.Combine(rootPath, candidate));
    }

    public static bool IsR3()
    {
        var processes = Process.GetProcessesByName("rpcs3");

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}

#pragma warning restore IDE0130
