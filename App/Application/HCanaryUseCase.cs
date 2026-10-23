using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class HCanaryUseCase(
    HCanaryPatchAuthoringService authoring,
    IRpcs3MutationGuard mutationGuard)
{
    public IReadOnlyList<HCanStor> Load(
        Rpcs3InstallDescriptor install)
    {
        return authoring.Load(
            Rpcs3AppPaths.ImpPat(install));
    }

    public SaveRes Save(
        Rpcs3InstallDescriptor install,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return SaveGuarded(install, draft, originalIdentity, saveAsNew);
    }

    public SaveRes Delete(
        Rpcs3InstallDescriptor install,
        string identity)
    {
        CheckCan(install);

        return authoring.Delete(
            Rpcs3AppPaths.ImpPat(install),
            Rpcs3AppPaths.BkpRoot(install),
            identity,
            (_, backupRoot, targets) => mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]));
    }

    private SaveRes SaveGuarded(
        Rpcs3InstallDescriptor install,
        HCanDrft draft,
        string? originalIdentity,
        bool saveAsNew)
    {
        CheckCan(install);

        return authoring.Save(
            Rpcs3AppPaths.ImpPat(install),
            Rpcs3AppPaths.BkpRoot(install),
            draft,
            originalIdentity,
            saveAsNew,
            (_, backupRoot, targets) => mutationGuard.CheckCan(
                install,
                targets,
                [backupRoot]));
    }

    private void CheckCan(
        Rpcs3InstallDescriptor install)
    {
        mutationGuard.CheckCan(
            install,
            [
                Rpcs3AppPaths.ImpPat(install),
                Rpcs3AppPaths.HCanMeta(install),
                Rpcs3AppPaths.HCanOld(install)
            ],
            [Rpcs3AppPaths.BkpRoot(install)]);
    }
}
