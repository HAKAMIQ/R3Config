using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public sealed class HCanaryUseCase(
    HCanaryPatchAuthoringService authoring,
    IRpcs3MutationGuard mutationGuard)
{
    public IReadOnlyList<HCanaryStoredPatch> Load(
        Rpcs3InstallDescriptor install)
    {
        return authoring.Load(
            Rpcs3AppPaths.ImportedPatch(install));
    }

    public HCanarySaveResult Save(
        Rpcs3InstallDescriptor install,
        HCanaryPatchDraft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        EnsureCanWrite(install);

        return authoring.Save(
            Rpcs3AppPaths.ImportedPatch(install),
            Rpcs3AppPaths.BackupRoot(install),
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
        EnsureCanWrite(install);

        return authoring.Delete(
            Rpcs3AppPaths.ImportedPatch(install),
            Rpcs3AppPaths.BackupRoot(install),
            identity,
            (_, backupRoot, targets) => mutationGuard.EnsureCanMutate(
                install,
                targets,
                [backupRoot]));
    }

    private void EnsureCanWrite(
        Rpcs3InstallDescriptor install)
    {
        mutationGuard.EnsureCanMutate(
            install,
            [
                Rpcs3AppPaths.ImportedPatch(install),
                Rpcs3AppPaths.HCanaryMetadata(install)
            ],
            [Rpcs3AppPaths.BackupRoot(install)]);
    }
}
