using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public static class R3Paths
{
    public static string BkpRoot(
        R3Inst install)
    {
        return Path.Combine(
            install.CfgPath,
            "R3ConfigBackups");
    }

    public static string ImpPat(
        R3Inst install)
    {
        return Path.Combine(
            install.PatPath,
            CanaryPatchImportService.ImportedPatchFileName);
    }

    public static string HCanMeta(
        R3Inst install)
    {
        return Path.Combine(
            install.PatPath,
            PatAuth.MetaName);
    }
}
