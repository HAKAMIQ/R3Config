using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public static class Rpcs3AppPaths
{
    public static string BkpRoot(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.CfgPath,
            "R3Bkp");
    }

    public static string OldBkp(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.CfgPath,
            "R3ConfigBackups");
    }

    public static string ImpPat(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatPath,
            Svc.CanaryPatchImportService.ImpName);
    }

    public static string HCanMeta(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatPath,
            HCanaryPatchAuthoringService.MetaName);
    }

    public static string HCanOld(
        Rpcs3InstallDescriptor install)
    {
        return Path.Combine(
            install.PatPath,
            HCanaryPatchAuthoringService.OldName);
    }
}
