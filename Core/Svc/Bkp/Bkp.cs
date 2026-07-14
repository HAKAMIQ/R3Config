using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed class BackupService(
    Action<string?, string, bool>? commitRestoreOperation = null,
    Action<string?, string, bool>? rollbackRestoreOperation = null)
{
    private const string ManifestFileName = "manifest.json";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _restoreSync = new();

    private readonly Action<string?, string, bool> _commitRestoreOperation =
        commitRestoreOperation ?? CommitRestoreOperation;

    private readonly Action<string?, string, bool> _rollbackRestoreOperation =
        rollbackRestoreOperation ?? RollbackRestoreOperation;

    public IReadOnlyList<BackupSnapshot> ListBackups(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateDirectories(backupRootPath)
                .Select(TryLoadSnapshot)
                .Where(snapshot => snapshot is not null)
                .Cast<BackupSnapshot>()
                .OrderByDescending(snapshot => snapshot.CreatedAt)
        ];
    }

    public BackupManifest CreateBackup(
        string backupRootPath,
        IReadOnlyList<string> targetPaths)
    {
        ArgumentNullException.ThrowIfNull(targetPaths);

        if (targetPaths.Count == 0)
        {
            throw new ArgumentException(
                "A backup must contain at least one target file.",
                nameof(targetPaths));
        }

        var normalizedTargets = NormalizeUniqueTargets(targetPaths);
        Directory.CreateDirectory(backupRootPath);

        var backupPath = Path.Combine(
            backupRootPath,
            $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");

        Directory.CreateDirectory(backupPath);

        try
        {
            List<BackupFileEntry> entries = [];

            foreach (var normalizedTargetPath in normalizedTargets)
            {
                var existed = File.Exists(normalizedTargetPath);
                var backupFilePath = Path.Combine(
                    backupPath,
                    MakeBackupFileName(normalizedTargetPath));
                long? length = null;
                string? sha256 = null;

                if (existed)
                {
                    File.Copy(
                        normalizedTargetPath,
                        backupFilePath,
                        overwrite: false);

                    length = new FileInfo(backupFilePath).Length;
                    sha256 = ComputeSha256(backupFilePath);
                }

                entries.Add(new BackupFileEntry(
                    normalizedTargetPath,
                    backupFilePath,
                    existed,
                    length,
                    sha256));
            }

            var manifest = new BackupManifest(
                DateTimeOffset.Now,
                entries);

            var manifestPath = Path.Combine(
                backupPath,
                ManifestFileName);

            var manifestText = JsonSerializer.Serialize(
                manifest,
                _jsonOptions);
            Rpcs3BoundedInput.EnsureTextLength(
                manifestText,
                Rpcs3InputLimits.BackupManifestBytes,
                "R3Config backup manifest");

            File.WriteAllText(
                manifestPath,
                manifestText);

            var persisted = ReadManifest(manifestPath);
            if (!ManifestsEquivalent(manifest, persisted))
            {
                throw new InvalidDataException(
                    "The backup manifest failed read-back validation.");
            }

            return manifest;
        }
        catch
        {
            try
            {
                Directory.Delete(backupPath, recursive: true);
            }
            catch
            {
            }

            throw;
        }
    }

    public void Restore(BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var snapshotPath = GetManifestSnapshotPath(manifest);
        Restore(new BackupSnapshot(
            snapshotPath,
            manifest.CreatedAt,
            manifest.Files));
    }

    public void Restore(BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_restoreSync)
        {
            var validated = ValidateSnapshot(snapshot);
            List<RestoreOperation> operations = [];

            try
            {
                foreach (var entry in validated)
                {
                    var operation = new RestoreOperation(entry);
                    operations.Add(operation);

                    if (!entry.Entry.Existed)
                    {
                        continue;
                    }

                    var targetDirectory = Path.GetDirectoryName(entry.TargetPath)
                        ?? throw new InvalidDataException(
                            $"A restore target has no parent directory: {entry.TargetPath}");
                    Directory.CreateDirectory(targetDirectory);

                    operation.RestoreStagePath = MakeStagePath(
                        entry.TargetPath,
                        "r3restore");
                    File.Copy(
                        entry.BackupPath,
                        operation.RestoreStagePath,
                        overwrite: false);
                    EnsureFilesEqual(
                        entry.BackupPath,
                        operation.RestoreStagePath,
                        "Restore stage validation failed.");
                }

                foreach (var operation in operations)
                {
                    operation.TargetOriginallyExisted = File.Exists(
                        operation.Validated.TargetPath);
                    if (!operation.TargetOriginallyExisted)
                    {
                        continue;
                    }

                    operation.RollbackStagePath = MakeStagePath(
                        operation.Validated.TargetPath,
                        "r3rollback");
                    File.Copy(
                        operation.Validated.TargetPath,
                        operation.RollbackStagePath,
                        overwrite: false);
                    EnsureFilesEqual(
                        operation.Validated.TargetPath,
                        operation.RollbackStagePath,
                        "Rollback stage validation failed.");
                }

                try
                {
                    foreach (var operation in operations)
                    {
                        _commitRestoreOperation(
                            operation.RestoreStagePath,
                            operation.Validated.TargetPath,
                            operation.Validated.Entry.Existed);

                        ValidateCommittedOperation(operation);
                    }
                }
                catch (Exception commitException)
                {
                    var rollbackErrors = new List<Exception>();

                    foreach (var operation in operations.AsEnumerable().Reverse())
                    {
                        try
                        {
                            _rollbackRestoreOperation(
                                operation.RollbackStagePath,
                                operation.Validated.TargetPath,
                                operation.TargetOriginallyExisted);
                        }
                        catch (Exception rollbackException)
                        {
                            rollbackErrors.Add(rollbackException);
                        }
                    }

                    if (rollbackErrors.Count > 0)
                    {
                        throw new AggregateException(
                            "Backup restore failed and rollback also failed.",
                            [commitException, .. rollbackErrors]);
                    }

                    ExceptionDispatchInfo
                        .Capture(commitException)
                        .Throw();
                    throw;
                }
            }
            finally
            {
                foreach (var operation in operations)
                {
                    TryDelete(operation.RestoreStagePath);
                    TryDelete(operation.RollbackStagePath);
                }
            }
        }
    }

    private IReadOnlyList<ValidatedBackupEntry> ValidateSnapshot(
        BackupSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.DirectoryPath))
        {
            throw new InvalidDataException(
                "The backup snapshot directory is empty.");
        }

        var snapshotPath = Path.GetFullPath(snapshot.DirectoryPath);
        if (!Directory.Exists(snapshotPath))
        {
            throw new DirectoryNotFoundException(
                $"Backup snapshot directory not found: {snapshotPath}");
        }

        var canonicalSnapshotPath = Rpcs3CanonicalPath.ResolveCanonicalExistingPath(
            snapshotPath);
        if (!PathsEqual(
                snapshotPath,
                canonicalSnapshotPath))
        {
            throw new InvalidDataException(
                "The backup snapshot directory resolves through a reparse point.");
        }

        var manifestPath = Path.Combine(
            snapshotPath,
            ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "The backup snapshot manifest is missing.",
                manifestPath);
        }

        var canonicalManifestPath = Rpcs3CanonicalPath.ResolveCanonicalExistingPath(
            manifestPath);
        if (!IsChildPath(
                canonicalSnapshotPath,
                canonicalManifestPath))
        {
            throw new InvalidDataException(
                "The backup manifest resolves outside its snapshot directory.");
        }

        var persisted = ReadManifest(manifestPath);
        var requested = new BackupManifest(
            snapshot.CreatedAt,
            snapshot.Files);
        if (!ManifestsEquivalent(requested, persisted))
        {
            throw new InvalidDataException(
                "The selected backup snapshot does not match its manifest.json file.");
        }

        if (persisted.Files is null || persisted.Files.Count == 0)
        {
            throw new InvalidDataException(
                "The backup snapshot contains no files.");
        }

        var seenTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var seenBackups = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var validated = new List<ValidatedBackupEntry>(
            persisted.Files.Count);

        foreach (var entry in persisted.Files)
        {
            if (string.IsNullOrWhiteSpace(entry.TargetPath) ||
                string.IsNullOrWhiteSpace(entry.BackupPath) ||
                !Path.IsPathFullyQualified(entry.TargetPath) ||
                !Path.IsPathFullyQualified(entry.BackupPath))
            {
                throw new InvalidDataException(
                    "A backup manifest entry contains an invalid path.");
            }

            if (ContainsParentTraversal(entry.BackupPath))
            {
                throw new InvalidDataException(
                    "A backup manifest path contains parent traversal.");
            }

            var targetPath = Path.GetFullPath(entry.TargetPath);
            var backupPath = Path.GetFullPath(entry.BackupPath);
            if (!seenTargets.Add(targetPath))
            {
                throw new InvalidDataException(
                    $"A backup manifest target is duplicated: {targetPath}");
            }

            if (!seenBackups.Add(backupPath))
            {
                throw new InvalidDataException(
                    $"A backup manifest file is duplicated: {backupPath}");
            }

            var expectedBackupPath = Path.Combine(
                snapshotPath,
                MakeBackupFileName(targetPath));
            if (!PathsEqual(backupPath, expectedBackupPath))
            {
                throw new InvalidDataException(
                    "A backup manifest file does not match the expected snapshot file for its target.");
            }

            if (Directory.Exists(targetPath))
            {
                throw new InvalidDataException(
                    $"A backup restore target is a directory: {targetPath}");
            }

            if (entry.Existed)
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException(
                        "A required backup file is missing.",
                        backupPath);
                }

                var canonicalBackupPath = Rpcs3CanonicalPath.ResolveCanonicalExistingPath(
                    backupPath);
                if (!IsChildPath(
                        canonicalSnapshotPath,
                        canonicalBackupPath))
                {
                    throw new InvalidDataException(
                        "A backup file resolves outside its snapshot directory.");
                }

                ValidateStoredFileMetadata(entry, backupPath);
            }

            validated.Add(new ValidatedBackupEntry(
                entry,
                targetPath,
                backupPath));
        }

        return validated;
    }

    private static List<string> NormalizeUniqueTargets(
        IReadOnlyList<string> targetPaths)
    {
        var output = new List<string>(targetPaths.Count);
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException(
                    "A backup target path is empty.",
                    nameof(targetPaths));
            }

            var normalized = Path.GetFullPath(targetPath);
            if (!seen.Add(normalized))
            {
                throw new InvalidOperationException(
                    $"A backup target is duplicated: {normalized}");
            }

            if (Directory.Exists(normalized))
            {
                throw new InvalidOperationException(
                    $"A backup target is a directory: {normalized}");
            }

            output.Add(normalized);
        }

        return output;
    }

    private static void ValidateStoredFileMetadata(
        BackupFileEntry entry,
        string backupPath)
    {
        if (entry.Length is long expectedLength)
        {
            if (expectedLength < 0 ||
                new FileInfo(backupPath).Length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Backup file length mismatch: {backupPath}");
            }
        }

        if (entry.Sha256 is not null)
        {
            if (!IsSha256(entry.Sha256) ||
                !string.Equals(
                    ComputeSha256(backupPath),
                    entry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Backup file SHA-256 mismatch: {backupPath}");
            }
        }
    }

    private static void ValidateCommittedOperation(
        RestoreOperation operation)
    {
        if (!operation.Validated.Entry.Existed)
        {
            if (File.Exists(operation.Validated.TargetPath))
            {
                throw new IOException(
                    $"Restore failed to remove target: {operation.Validated.TargetPath}");
            }

            return;
        }

        if (!File.Exists(operation.Validated.TargetPath))
        {
            throw new IOException(
                $"Restore failed to create target: {operation.Validated.TargetPath}");
        }

        EnsureFilesEqual(
            operation.Validated.BackupPath,
            operation.Validated.TargetPath,
            "Committed restore validation failed.");
    }

    private BackupSnapshot? TryLoadSnapshot(string backupDirectory)
    {
        var manifestPath = Path.Combine(
            backupDirectory,
            ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = ReadManifest(manifestPath);
            return manifest.Files is null
                ? null
                : new BackupSnapshot(
                    backupDirectory,
                    manifest.CreatedAt,
                    manifest.Files);
        }
        catch
        {
            return null;
        }
    }

    private BackupManifest ReadManifest(string manifestPath)
    {
        var text = Rpcs3BoundedInput.ReadUtf8TextFile(
            manifestPath,
            Rpcs3InputLimits.BackupManifestBytes,
            "R3Config backup manifest");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            text,
            _jsonOptions);

        return manifest ?? throw new InvalidDataException(
            "The backup manifest is empty or invalid.");
    }

    private static string GetManifestSnapshotPath(
        BackupManifest manifest)
    {
        if (manifest.Files is null || manifest.Files.Count == 0)
        {
            throw new InvalidDataException(
                "The backup manifest contains no files.");
        }

        string? snapshotPath = null;
        foreach (var entry in manifest.Files)
        {
            var candidate = Path.GetDirectoryName(
                Path.GetFullPath(entry.BackupPath))
                ?? throw new InvalidDataException(
                    "A backup manifest path has no snapshot directory.");

            if (snapshotPath is null)
            {
                snapshotPath = candidate;
                continue;
            }

            if (!PathsEqual(snapshotPath, candidate))
            {
                throw new InvalidDataException(
                    "A backup manifest references more than one snapshot directory.");
            }
        }

        return snapshotPath!;
    }

    private static bool ManifestsEquivalent(
        BackupManifest left,
        BackupManifest right)
    {
        if (left.Files is null ||
            right.Files is null ||
            left.Files.Count != right.Files.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Files.Count; index++)
        {
            var leftEntry = left.Files[index];
            var rightEntry = right.Files[index];
            if (!PathsEqual(leftEntry.TargetPath, rightEntry.TargetPath) ||
                !PathsEqual(leftEntry.BackupPath, rightEntry.BackupPath) ||
                leftEntry.Existed != rightEntry.Existed ||
                leftEntry.Length != rightEntry.Length ||
                !string.Equals(
                    leftEntry.Sha256,
                    rightEntry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string MakeBackupFileName(string path)
    {
        var normalizedPath = Path
            .GetFullPath(path)
            .ToUpperInvariant();

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(normalizedPath));

        return Convert.ToHexString(hash);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 &&
               value.All(Uri.IsHexDigit);
    }

    private static void EnsureFilesEqual(
        string expectedPath,
        string actualPath,
        string error)
    {
        var expectedInfo = new FileInfo(expectedPath);
        var actualInfo = new FileInfo(actualPath);
        if (expectedInfo.Length != actualInfo.Length ||
            !string.Equals(
                ComputeSha256(expectedPath),
                ComputeSha256(actualPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{error} Expected '{expectedPath}', actual '{actualPath}'.");
        }
    }

    private static string MakeStagePath(
        string targetPath,
        string suffix)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidDataException(
                $"A restore target has no parent directory: {targetPath}");

        return Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.{suffix}");
    }

    private static void CommitRestoreOperation(
        string? restoreStagePath,
        string targetPath,
        bool shouldExist)
    {
        if (!shouldExist)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(restoreStagePath) ||
            !File.Exists(restoreStagePath))
        {
            throw new FileNotFoundException(
                "The prepared restore stage is missing.",
                restoreStagePath);
        }

        if (File.Exists(targetPath))
        {
            File.Replace(
                restoreStagePath,
                targetPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(restoreStagePath, targetPath);
        }
    }

    private static void RollbackRestoreOperation(
        string? rollbackStagePath,
        string targetPath,
        bool shouldExist)
    {
        if (!shouldExist)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(rollbackStagePath) ||
            !File.Exists(rollbackStagePath))
        {
            throw new FileNotFoundException(
                "The prepared rollback stage is missing.",
                rollbackStagePath);
        }

        if (File.Exists(targetPath))
        {
            File.Replace(
                rollbackStagePath,
                targetPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(rollbackStagePath, targetPath);
        }
    }

    private static bool ContainsParentTraversal(string path)
    {
        return path.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(
                "..",
                StringComparison.Ordinal));
    }

    private static bool IsChildPath(
        string parentPath,
        string childPath)
    {
        var relative = Path.GetRelativePath(parentPath, childPath);

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

    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed record ValidatedBackupEntry(
        BackupFileEntry Entry,
        string TargetPath,
        string BackupPath);

    private sealed class RestoreOperation(
        ValidatedBackupEntry validated)
    {
        public ValidatedBackupEntry Validated { get; } = validated;

        public string? RestoreStagePath { get; set; }

        public string? RollbackStagePath { get; set; }

        public bool TargetOriginallyExisted { get; set; }
    }
}

#pragma warning restore IDE0130
