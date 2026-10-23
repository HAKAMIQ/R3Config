using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

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

        var fullExePath = RuntimePathPolicy.RequireLocal(
            exePath,
            "RPCS3 executable");

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
        rootPath = RuntimePathPolicy.RequireLocal(
            rootPath,
            "RPCS3 installation");

        var dataRootPath = RuntimePathPolicy.RequireLocal(
            GetData(rootPath),
            "RPCS3 data root");

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

    internal static string? Hdd0(
        Rpcs3InstallDescriptor install,
        out string? issue)
    {
        ArgumentNullException.ThrowIfNull(install);

        var dataRoot = install.DataRoot ??
                       Path.GetDirectoryName(
                           install.CfgPath) ??
                       install.RootPath;

        return ResolveHdd0(
            dataRoot,
            install.CfgPath,
            out issue);
    }

    private static string? ResolveHdd0(
        string dataRootPath,
        string configPath,
        out string? issue)
    {
        var vfsPath = Path.Combine(
            configPath,
            "vfs.yml");

        try
        {
            var emulatorDir = dataRootPath;
            var devHdd0 = "$(EmulatorDir)dev_hdd0/";

            if (File.Exists(vfsPath))
            {
                var text = R3Bound.ReadUtf8(
                    vfsPath,
                    R3Limits.CfgByte,
                    "vfs.yml");

                var yaml = R3Bound.LoadYaml(
                    text,
                    "vfs.yml");

                if (yaml.Documents[0].RootNode is not
                    YamlMappingNode root)
                {
                    throw new InvalidDataException(
                        "vfs.yml must contain one YAML mapping document.");
                }

                var configuredEmulatorDir = ReadScalar(
                    root,
                    "$(EmulatorDir)");

                var configuredDevHdd0 = ReadScalar(
                    root,
                    "/dev_hdd0/");

                if (!string.IsNullOrWhiteSpace(
                        configuredEmulatorDir))
                {
                    emulatorDir = configuredEmulatorDir.Trim();
                }

                if (configuredDevHdd0 is not null)
                {
                    devHdd0 = string.IsNullOrWhiteSpace(
                        configuredDevHdd0)
                        ? "$(EmulatorDir)dev_hdd0/"
                        : configuredDevHdd0.Trim();
                }
            }

            var resolvedHdd0 = devHdd0;
            if (devHdd0.Contains(
                    "$(EmulatorDir)",
                    StringComparison.Ordinal))
            {
                emulatorDir = RuntimePathPolicy.RequireLocal(
                    emulatorDir,
                    "RPCS3 EmulatorDir");

                var emulatorDirWithSeparator =
                    Path.EndsInDirectorySeparator(
                        emulatorDir)
                        ? emulatorDir
                        : emulatorDir + Path.DirectorySeparatorChar;

                resolvedHdd0 = devHdd0.Replace(
                    "$(EmulatorDir)",
                    emulatorDirWithSeparator,
                    StringComparison.Ordinal);
            }

            if (resolvedHdd0.Contains(
                    "$(",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "RPCS3 /dev_hdd0/ contains an unresolved VFS token.");
            }

            issue = null;
            return RuntimePathPolicy.RequireLocal(
                resolvedHdd0,
                "RPCS3 /dev_hdd0/");
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                InvalidDataException or
                IOException or
                UnauthorizedAccessException or
                SecurityException or
                DecoderFallbackException or
                YamlException)
        {
            issue = ex.Message;
            return null;
        }
    }

    private static string? ReadScalar(
        YamlMappingNode root,
        string key)
    {
        string? value = null;
        var found = false;

        foreach (var pair in root.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode ||
                !string.Equals(
                    keyNode.Value,
                    key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (found)
            {
                throw new InvalidDataException(
                    $"vfs.yml contains duplicate {key} entries.");
            }

            if (pair.Value is not YamlScalarNode valueNode)
            {
                throw new InvalidDataException(
                    $"vfs.yml entry {key} must be a scalar value.");
            }

            found = true;
            value = valueNode.Value;
        }

        return value;
    }

    private static string GetData(string rootPath)
    {
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

        // RPCS3 (Utilities/File.cpp fs::get_config_dir) turns '\' into '/'
        // and keeps the value only up to its last '/', so "C:\cfg\" is
        // C:\cfg but "C:\cfg" is C:\.
        var candidate = configuredPath.Trim().Replace('\\', '/');
        candidate = candidate[..(candidate.LastIndexOf('/') + 1)];
        if (candidate.Length == 0)
        {
            return rootPath;
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                Path.IsPathRooted(candidate)
                    ? candidate
                    : Path.Combine(rootPath, candidate)));
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
