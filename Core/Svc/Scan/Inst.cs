using System;
using System.Diagnostics;
using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class Rpcs3InstallService
{
    public static Rpcs3InstallDescriptor FromExePath(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("Select rpcs3.exe first.", nameof(exePath));
        }

        var fullExePath = Path.GetFullPath(exePath);

        if (!File.Exists(fullExePath))
        {
            throw new FileNotFoundException("rpcs3.exe was not found.", fullExePath);
        }

        if (!string.Equals(Path.GetFileName(fullExePath), "rpcs3.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected file must be rpcs3.exe.");
        }

        var rootPath = Path.GetDirectoryName(fullExePath)
            ?? throw new InvalidOperationException("Could not detect RPCS3 root path.");

        // R3Config writes per-game wiki settings to the emulator-root config tree.
        // The final-user contract is always:
        // <RPCS3 folder>\config\custom_configs\config_<SERIAL>.yml
        // regardless of the folder name chosen by the user.
        var configPath = Path.Combine(rootPath, "config");

        // RPCS3 documents manually added patches on Windows as:
        // <RPCS3 folder>\patches\imported_patch.yml
        // Keep patch files anchored to the emulator folder even when RPCS3 uses
        // a portable config folder for config and patch_config.yml.
        var patchesPath = Path.Combine(rootPath, "patches");

        return new Rpcs3InstallDescriptor(
            RootPath: rootPath,
            ExePath: fullExePath,
            ConfigPath: configPath,
            CustomConfigsPath: Path.Combine(configPath, "custom_configs"),
            PatchesPath: patchesPath,
            PatchConfigPath: Path.Combine(configPath, "patch_config.yml"));
    }

    public static bool IsRpcs3Running()
    {
        return Process.GetProcessesByName("rpcs3").Length > 0;
    }
}
