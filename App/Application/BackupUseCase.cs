using System.IO;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class BackupUseCase(
    BackupService backupService,
    IRpcs3MutationGuard mutationGuard)
{
    public const int DefaultRetentionCount = 10;

    public IReadOnlyList<BackupSnapshot> List(
        Rpcs3InstallDescriptor install,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return backupService.ListBkp(
                Rpcs3AppPaths.BkpRoot(install),
                cancellationToken)
            .Concat(backupService.ListBkp(
                Rpcs3AppPaths.OldBkp(install),
                cancellationToken))
            .OrderByDescending(item => item.MadeAt)
            .ThenBy(item => item.DirPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool Delete(
        Rpcs3InstallDescriptor install,
        BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(snapshot);

        return backupService.DeleteSnapshot(
            ManagedRoot(install, snapshot),
            snapshot);
    }

    public int DeleteOld(
        Rpcs3InstallDescriptor install,
        int keepCount = DefaultRetentionCount)
    {
        ArgumentNullException.ThrowIfNull(install);
        if (keepCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keepCount),
                "At least one backup snapshot must be retained.");
        }

        var snapshots = List(install);
        if (snapshots.Count <= keepCount)
        {
            return 0;
        }

        var deleted = 0;
        foreach (var snapshot in snapshots.Skip(keepCount))
        {
            if (Delete(install, snapshot))
            {
                deleted++;
            }
        }

        return deleted;
    }

    public void Restore(
        Rpcs3InstallDescriptor install,
        BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Files is null ||
            snapshot.Files.Count == 0 ||
            snapshot.Files.Count > R3Limits.Entries)
        {
            throw new InvalidDataException(
                "The selected backup snapshot has an invalid file count.");
        }

        var backupRoot = Path.GetFullPath(
            Rpcs3AppPaths.BkpRoot(install));
        var snapshotPath = Path.GetFullPath(
            snapshot.DirPath);
        var oldRoot = Path.GetFullPath(
            Rpcs3AppPaths.OldBkp(install));
        if (!IsChild(backupRoot, snapshotPath) &&
            !IsChild(oldRoot, snapshotPath))
        {
            throw new InvalidDataException(
                "The selected backup snapshot is outside this RPCS3 installation's managed backup directories.");
        }

        var candidates = snapshot.Files
            .Select(entry => CreateCandidate(
                install,
                entry))
            .ToArray();
        var targetPaths = candidates
            .Select(candidate => candidate.TargetPath)
            .ToArray();

        mutationGuard.CheckCan(
            install,
            targetPaths);

        var contracts = candidates
            .Select(candidate => CaptureContract(candidate))
            .ToArray();

        backupService.Restore(
            snapshot,
            () =>
            {
                mutationGuard.CheckCan(
                    install,
                    targetPaths);
                foreach (var contract in contracts)
                {
                    ValidateManagedOwnership(contract.Candidate);
                    PatchFileTransactionService.ValidateExpectedState(
                        contract.Candidate.TargetPath,
                        contract.ExpectedState);
                }
            },
            () =>
            {
                foreach (var candidate in candidates)
                {
                    ValidateBackupPayload(candidate);
                }
            });
    }

    private static string ManagedRoot(
        Rpcs3InstallDescriptor install,
        BackupSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.DirPath))
        {
            throw new InvalidDataException(
                "The backup snapshot directory is empty.");
        }

        var snapshotPath = Path.GetFullPath(snapshot.DirPath);
        var backupRoot = Path.GetFullPath(
            Rpcs3AppPaths.BkpRoot(install));
        if (IsChild(backupRoot, snapshotPath))
        {
            return backupRoot;
        }

        var oldRoot = Path.GetFullPath(
            Rpcs3AppPaths.OldBkp(install));
        if (IsChild(oldRoot, snapshotPath))
        {
            return oldRoot;
        }

        throw new InvalidDataException(
            "The selected backup snapshot is outside this RPCS3 installation's managed backup directories.");
    }

    private static RestoreContract CaptureContract(
        RestoreCandidate candidate)
    {
        ValidateCurrentOwnership(candidate);
        return new RestoreContract(
            candidate,
            PatchFileTransactionService.CaptureExpectedState(
                candidate.TargetPath,
                candidate.MaxBytes,
                candidate.Description));
    }

    private static RestoreCandidate CreateCandidate(
        Rpcs3InstallDescriptor install,
        BackupFileEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.TgtPath))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                "A backup restore target path is empty.");
        }

        var targetPath = Path.GetFullPath(entry.TgtPath);
        if (Paths(targetPath, install.PatCfg))
        {
            return new RestoreCandidate(
                entry,
                targetPath,
                R3Limits.PatCfgBy,
                "patch_config.yml",
                RestoreKind.PatchConfig);
        }

        if (IsCustomConfig(install, targetPath))
        {
            return new RestoreCandidate(
                entry,
                targetPath,
                R3Limits.CfgByte,
                "custom config YAML",
                RestoreKind.CustomConfig);
        }

        if (Paths(targetPath, Rpcs3AppPaths.ImpPat(install)))
        {
            return new RestoreCandidate(
                entry,
                targetPath,
                R3Limits.PatCatBy,
                "imported_patch.yml",
                RestoreKind.PatchCatalog);
        }

        if (Paths(targetPath, Rpcs3AppPaths.HCanMeta(install)))
        {
            return new RestoreCandidate(
                entry,
                targetPath,
                R3Limits.HBytes,
                "R3Config H-Canary metadata",
                RestoreKind.JsonMetadata);
        }

        if (IsManagedCheatPath(install, targetPath))
        {
            return new RestoreCandidate(
                entry,
                targetPath,
                R3Limits.PatCatBy,
                "managed RPCS3 cheat patch",
                RestoreKind.ManagedPatchCatalog);
        }

        throw new Rpcs3MutationBlockedException(
            MutBlock.Tgt,
            $"R3Config does not own the backup restore target: {targetPath}");
    }

    private static void ValidateCurrentOwnership(
        RestoreCandidate candidate)
    {
        if (candidate.Kind == RestoreKind.ManagedPatchCatalog &&
            File.Exists(candidate.TargetPath) &&
            !BundledCheatPatchService.IsMan(candidate.TargetPath))
        {
            throw new Rpcs3MutationBlockedException(
                MutBlock.Tgt,
                $"R3Config will not replace an unmarked title-specific patch file: {candidate.TargetPath}");
        }
    }

    private static void ValidateManagedOwnership(
        RestoreCandidate candidate)
    {
        if (candidate.Kind == RestoreKind.ManagedPatchCatalog &&
            candidate.Entry.Existed &&
            !BundledCheatPatchService.IsMan(candidate.Entry.BkpPath))
        {
            throw new InvalidDataException(
                "A managed cheat backup payload is missing the R3Config ownership marker.");
        }
    }

    private static void ValidateBackupPayload(
        RestoreCandidate candidate)
    {
        if (!candidate.Entry.Existed)
        {
            return;
        }

        if (!File.Exists(candidate.Entry.BkpPath))
        {
            throw new FileNotFoundException(
                "A required backup file is missing.",
                candidate.Entry.BkpPath);
        }

        var text = R3Bound.ReadUtf8(
            candidate.Entry.BkpPath,
            candidate.MaxBytes,
            candidate.Description);
        switch (candidate.Kind)
        {
            case RestoreKind.PatchConfig:
                CfgWrite.VldText(text);
                break;
            case RestoreKind.CustomConfig:
                if (!R3Yaml.CanRead(candidate.Entry.BkpPath))
                {
                    throw new InvalidDataException(
                        "The custom config backup payload is invalid.");
                }

                break;
            case RestoreKind.PatchCatalog:
            case RestoreKind.ManagedPatchCatalog:
                var source = candidate.Kind == RestoreKind.PatchCatalog
                    ? PatSrc.Imported
                    : PatSrc.ManagedArtemis;
                var read = new ReadSvc().ReadText(
                    text,
                    source,
                    candidate.Entry.BkpPath);
                if (!read.IsOk)
                {
                    throw new InvalidDataException(
                        read.Error ?? "The patch backup payload is invalid.");
                }

                break;
            case RestoreKind.JsonMetadata:
                using (var document = JsonDocument.Parse(
                           text,
                           new JsonDocumentOptions
                           {
                               MaxDepth = R3Limits.Depth
                           }))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException(
                            "The H-Canary metadata backup root must be a JSON object.");
                    }
                }

                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported restore payload kind: {candidate.Kind}");
        }
    }

    private static bool IsCustomConfig(
        Rpcs3InstallDescriptor install,
        string targetPath)
    {
        if (!Paths(
                Path.GetDirectoryName(targetPath) ?? string.Empty,
                install.CustPath))
        {
            return false;
        }

        var name = Path.GetFileName(targetPath);
        const string prefix = "config_";
        const string suffix = ".yml";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var serial = name[prefix.Length..^suffix.Length];
        return IsSerial(serial);
    }

    private static bool IsManagedCheatPath(
        Rpcs3InstallDescriptor install,
        string targetPath)
    {
        if (Paths(
                targetPath,
                BundledCheatPatchService.GetPath(install.PatPath)))
        {
            return true;
        }

        if (!Paths(
                Path.GetDirectoryName(targetPath) ?? string.Empty,
                install.PatPath))
        {
            return false;
        }

        var name = Path.GetFileName(targetPath);
        const string suffix = "_patch.yml";
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
               IsSerial(name[..^suffix.Length]);
    }

    private static bool IsSerial(string value)
    {
        return value.Length == 9 &&
               value.All(character =>
                   character is >= 'A' and <= 'Z' or
                       >= 'a' and <= 'z' or
                       >= '0' and <= '9');
    }

    private static bool Paths(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChild(
        string parentPath,
        string childPath)
    {
        var relative = Path.GetRelativePath(
            parentPath,
            childPath);

        return !Path.IsPathRooted(relative) &&
               !relative.Equals(".", StringComparison.Ordinal) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.AltDirectorySeparatorChar}",
                   StringComparison.Ordinal);
    }

    private sealed record RestoreCandidate(
        BackupFileEntry Entry,
        string TargetPath,
        long MaxBytes,
        string Description,
        RestoreKind Kind);

    private sealed record RestoreContract(
        RestoreCandidate Candidate,
        PatchTargetState ExpectedState);

    private enum RestoreKind
    {
        PatchConfig,
        CustomConfig,
        PatchCatalog,
        ManagedPatchCatalog,
        JsonMetadata
    }
}
