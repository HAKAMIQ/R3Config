using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public static class R3Paths
{
    public static string BkpRoot(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.ConfigPath,
            "R3ConfigBackups");
    }

    public static string ImpPat(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatchesPath,
            CanaryPatchImportService.ImportedPatchFileName);
    }

    public static string HCanMeta(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatchesPath,
            HCanaryPatchAuthoringService.MetadataFileName);
    }
}
