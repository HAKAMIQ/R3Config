using System.IO;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class CanImp(
    CanaryPatchImportService importService,
    IRpcs3MutationGuard mutationGuard,
    PatchFileTransactionService transaction,
    BackupService backupService,
    PatchCatalogReader catalogReader)
{
    public CanaryPatchNormalizeResult Norm(
        string? sourceText)
    {
        return importService.Normalize(sourceText);
    }

    public bool HasPat(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        return importService.HasManagedImportedPatch(
            install.PatchesPath);
    }

    public CanaryPatchWriteResult Import(
        Rpcs3InstallDescriptor install,
        string sourceText)
    {
        var targetPath = R3Paths.ImpPat(install);
        mutationGuard.EnsureCanMutate(
            install,
            [targetPath]);

        var normalized = importService.Normalize(sourceText);
        if (!normalized.Succeeded)
        {
            return Failure(
                targetPath,
                normalized.Error ?? normalized.Message);
        }

        var prepared = importService.PrepareManagedImportedPatchFile(
            install.PatchesPath,
            normalized.NormalizedText);
        if (!prepared.Succeeded)
        {
            return Failure(
                targetPath,
                prepared.Error ?? prepared.Message);
        }

        if (prepared.PreparedFile is null)
        {
            return new CanaryPatchWriteResult(
                true,
                targetPath,
                null,
                prepared.Message,
                null);
        }

        BackupManifest? committedBackup = null;
        try
        {
            committedBackup = transaction.Commit(
                R3Paths.BkpRoot(install),
                [prepared.PreparedFile],
                (_, backupRoot, targets) => mutationGuard.EnsureCanMutate(
                    install,
                    targets,
                    [backupRoot]));

            var readBack = importService.ReadImportedPatchText(
                targetPath);
            if (!string.Equals(
                    readBack,
                    prepared.PreparedFile.Content,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The imported patch file changed during commit read-back.");
            }

            var catalog = catalogReader.ReadText(
                readBack,
                PatchSourceOrigin.Imported,
                targetPath);
            if (!catalog.Succeeded)
            {
                throw new InvalidDataException(
                    catalog.Error ??
                    "The committed imported patch file is invalid.");
            }

            if (prepared.PreparedFile.ExpectedEntryCount is int expected &&
                catalog.Catalog.Entries.Count != expected)
            {
                throw new InvalidDataException(
                    "The committed imported patch entry count changed during read-back. " +
                    $"Expected {expected}, actual {catalog.Catalog.Entries.Count}.");
            }

            return new CanaryPatchWriteResult(
                true,
                targetPath,
                SnapPath(committedBackup),
                prepared.Message,
                null);
        }
        catch (Exception commitOrReadBackException)
        {
            Exception failure = commitOrReadBackException;

            if (committedBackup is not null)
            {
                try
                {
                    backupService.Restore(committedBackup);
                }
                catch (Exception rollbackException)
                {
                    failure = new InvalidOperationException(
                        "Canary import validation failed and rollback also failed.",
                        new AggregateException(
                            commitOrReadBackException,
                            rollbackException));
                }
            }

            return Failure(
                targetPath,
                failure.Message,
                committedBackup is null
                    ? null
                    : SnapPath(committedBackup));
        }
    }

    public CanaryPatchRemoveResult Remove(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var targetPath = R3Paths.ImpPat(install);
        mutationGuard.EnsureCanMutate(
            install,
            [targetPath]);

        var prepared = importService
            .PrepareManagedImportedPatchRemoval(
                install.PatchesPath);
        if (!prepared.Succeeded)
        {
            return DelFail(
                targetPath,
                prepared.Error ?? prepared.Message);
        }

        if (prepared.PreparedFile is null)
        {
            return new CanaryPatchRemoveResult(
                true,
                targetPath,
                null,
                prepared.Message,
                null);
        }

        BackupManifest? committedBackup = null;
        try
        {
            committedBackup = transaction.Commit(
                R3Paths.BkpRoot(install),
                [prepared.PreparedFile],
                (_, backupRoot, targets) => mutationGuard.EnsureCanMutate(
                    install,
                    targets,
                    [backupRoot]));

            if (prepared.PreparedFile.DeleteTarget)
            {
                if (File.Exists(targetPath))
                {
                    throw new IOException(
                        "The managed Canary target still exists after transactional deletion.");
                }
            }
            else
            {
                var readBack = importService.ReadImportedPatchText(
                    targetPath);
                if (!string.Equals(
                        readBack,
                        prepared.PreparedFile.Content,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "The imported patch file changed during removal read-back.");
                }

                var catalog = catalogReader.ReadText(
                    readBack,
                    PatchSourceOrigin.Imported,
                    targetPath);
                if (!catalog.Succeeded ||
                    prepared.PreparedFile.ExpectedEntryCount is int expected &&
                    catalog.Catalog.Entries.Count != expected)
                {
                    throw new InvalidDataException(
                        catalog.Error ??
                        "The imported patch file is invalid after managed Canary removal.");
                }
            }

            return new CanaryPatchRemoveResult(
                true,
                targetPath,
                SnapPath(committedBackup),
                prepared.Message,
                null);
        }
        catch (Exception commitOrReadBackException)
        {
            Exception failure = commitOrReadBackException;

            if (committedBackup is not null)
            {
                try
                {
                    backupService.Restore(committedBackup);
                }
                catch (Exception rollbackException)
                {
                    failure = new InvalidOperationException(
                        "Canary removal validation failed and rollback also failed.",
                        new AggregateException(
                            commitOrReadBackException,
                            rollbackException));
                }
            }

            return DelFail(
                targetPath,
                failure.Message,
                committedBackup is null
                    ? null
                    : SnapPath(committedBackup));
        }
    }

    private static CanaryPatchWriteResult Failure(
        string targetPath,
        string error,
        string? backupPath = null)
    {
        return new CanaryPatchWriteResult(
            false,
            targetPath,
            backupPath,
            error,
            error);
    }

    private static CanaryPatchRemoveResult DelFail(
        string targetPath,
        string error,
        string? backupPath = null)
    {
        return new CanaryPatchRemoveResult(
            false,
            targetPath,
            backupPath,
            error,
            error);
    }

    private static string? SnapPath(
        BackupManifest manifest)
    {
        return manifest.Files
            .Select(file => Path.GetDirectoryName(file.BackupPath))
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }
}
