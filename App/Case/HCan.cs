using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class HCanCase(
    HCanaryPatchAuthoringService authoring,
    IRpcs3MutationGuard mutationGuard)
{
    public IReadOnlyList<HCanaryStoredPatch> Load(
        Rpcs3InstallDescriptor install)
    {
        return authoring.Load(
            R3Paths.ImpPat(install));
    }

    public HCanarySaveResult Save(
        Rpcs3InstallDescriptor install,
        HCanaryPatchDraft draft,
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
            (_, backupRoot, targets) => mutationGuard.EnsureCanMutate(
                install,
                targets,
                [backupRoot]));
    }

    public HCanarySaveResult Delete(
        Rpcs3InstallDescriptor install,
        string identity)
    {
        CheckCan(install);

        return authoring.Delete(
            R3Paths.ImpPat(install),
            R3Paths.BkpRoot(install),
            identity,
            (_, backupRoot, targets) => mutationGuard.EnsureCanMutate(
                install,
                targets,
                [backupRoot]));
    }

    private void CheckCan(
        Rpcs3InstallDescriptor install)
    {
        mutationGuard.EnsureCanMutate(
            install,
            [
                R3Paths.ImpPat(install),
                R3Paths.HCanMeta(install)
            ],
            [R3Paths.BkpRoot(install)]);
    }
}
