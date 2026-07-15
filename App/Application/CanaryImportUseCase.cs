using System.IO;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class CanaryImportUseCase(
    Svc.CanaryPatchImportService importService,
    IRpcs3MutationGuard mutationGuard,
    PatchFileTransactionService transaction,
    BackupService backupService,
    Svc.ReadSvc catalogReader)
{
    public CanNorm Norm(
        string? sourceText)
    {
        return importService.Norm(sourceText);
    }

    public bool HasPat(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        return importService.HasPat(
            install.PatPath);
    }

    public CanWrite Import(
        Rpcs3InstallDescriptor install,
        string sourceText)
    {
        var targetPath = Rpcs3AppPaths.ImpPat(install);
        mutationGuard.CheckCan(
            install,
            [targetPath]);

        var normalized = importService.Norm(sourceText);
        if (!normalized.IsOk)
        {
            return Failure(
                targetPath,
                normalized.Error ?? normalized.Message);
        }

        var prepared = importService.PrepareManagedImportedPatchFile(
            install.PatPath,
            normalized.NormText);
        if (!prepared.IsOk)
        {
            return Failure(
                targetPath,
                prepared.Error ?? prepared.Message);
        }

        if (prepared.PreparedFile is null)
        {
            return new CanWrite(
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
                Rpcs3AppPaths.BkpRoot(install),
                [prepared.PreparedFile],
                (_, backupRoot, targets) => mutationGuard.CheckCan(
                    install,
                    targets,
                    [backupRoot]));

            var readBack = importService.ReadText(
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
                PatSrc.Imported,
                targetPath);
            if (!catalog.IsOk)
            {
                throw new InvalidDataException(
                    catalog.Error ??
                    "The committed imported patch file is invalid.");
            }

            if (prepared.PreparedFile.EntNum is int expected &&
                catalog.Catalog.Entries.Count != expected)
            {
                throw new InvalidDataException(
                    "The committed imported patch entry count changed during read-back. " +
                    $"Expected {expected}, actual {catalog.Catalog.Entries.Count}.");
            }

            return new CanWrite(
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

    public CanDel Remove(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var targetPath = Rpcs3AppPaths.ImpPat(install);
        mutationGuard.CheckCan(
            install,
            [targetPath]);

        var prepared = importService
            .PrepDel(
                install.PatPath);
        if (!prepared.IsOk)
        {
            return DelFail(
                targetPath,
                prepared.Error ?? prepared.Message);
        }

        if (prepared.PreparedFile is null)
        {
            return new CanDel(
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
                Rpcs3AppPaths.BkpRoot(install),
                [prepared.PreparedFile],
                (_, backupRoot, targets) => mutationGuard.CheckCan(
                    install,
                    targets,
                    [backupRoot]));

            if (prepared.PreparedFile.DelTgt)
            {
                if (File.Exists(targetPath))
                {
                    throw new IOException(
                        "The managed Canary target still exists after transactional deletion.");
                }
            }
            else
            {
                var readBack = importService.ReadText(
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
                    PatSrc.Imported,
                    targetPath);
                if (!catalog.IsOk ||
                    prepared.PreparedFile.EntNum is int expected &&
                    catalog.Catalog.Entries.Count != expected)
                {
                    throw new InvalidDataException(
                        catalog.Error ??
                        "The imported patch file is invalid after managed Canary removal.");
                }
            }

            return new CanDel(
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

    private static CanWrite Failure(
        string targetPath,
        string error,
        string? backupPath = null)
    {
        return new CanWrite(
            false,
            targetPath,
            backupPath,
            error,
            error);
    }

    private static CanDel DelFail(
        string targetPath,
        string error,
        string? backupPath = null)
    {
        return new CanDel(
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
            .Select(file => Path.GetDirectoryName(file.BkpPath))
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }
}
