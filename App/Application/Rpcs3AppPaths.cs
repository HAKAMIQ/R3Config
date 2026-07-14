using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public static class Rpcs3AppPaths
{
    public static string BackupRoot(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.ConfigPath,
            "R3ConfigBackups");
    }

    public static string ImportedPatch(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatchesPath,
            CanaryPatchImportService.ImportedPatchFileName);
    }

    public static string HCanaryMetadata(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatchesPath,
            HCanaryPatchAuthoringService.MetadataFileName);
    }
}
