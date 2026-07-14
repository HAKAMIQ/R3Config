using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class HCanCase(
    PatAuth authoring,
    IMutGd mutationGuard)
{
    public IReadOnlyList<HCanStor> Load(
        R3Inst install)
    {
        return authoring.Load(
            R3Paths.ImpPat(install));
    }

    public SaveRes Save(
        R3Inst install,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        CheckCan(install);

        return authoring.Save(
            R3Paths.ImpPat(install),
            R3Paths.BkpRoot(install),
            draft,
            originalIdentity,
            saveAsNew,
            (_, backupRoot, targets) => mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]));
    }

    public SaveRes Delete(
        R3Inst install,
        string identity)
    {
        CheckCan(install);

        return authoring.Delete(
            R3Paths.ImpPat(install),
            R3Paths.BkpRoot(install),
            identity,
            (_, backupRoot, targets) => mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]));
    }

    private void CheckCan(
        R3Inst install)
    {
        mutationGuard.CheckCan(
            install,
            [
                R3Paths.ImpPat(install),
                R3Paths.HCanMeta(install),
                R3Paths.HCanOld(install)
            ],
            [R3Paths.BkpRoot(install)]);
    }
}
