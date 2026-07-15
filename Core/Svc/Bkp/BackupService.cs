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
    private const string ManName = "manifest.json";

    private readonly JsonSerializerOptions _Options = new()
    {
        WriteIndented = true
    };

    private readonly object _RstSync = new();

    private readonly Action<string?, string, bool> _commitO =
        commitRestoreOperation ?? CommitOp;

    private readonly Action<string?, string, bool> _Op =
        rollbackRestoreOperation ?? Rollback;

    public IReadOnlyList<BackupSnapshot> ListBkp(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateDirectories(backupRootPath)
                .Select(TryLoad)
                .Where(snapshot => snapshot is not null)
                .Cast<BackupSnapshot>()
                .OrderByDescending(snapshot => snapshot.MadeAt)
        ];
    }

    public BackupManifest BuildBkp(
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

        var normalizedTargets = NormTgt(targetPaths);
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
                    MakeBkp(normalizedTargetPath));
                long? length = null;
                string? sha256 = null;

                if (existed)
                {
                    File.Copy(
                        normalizedTargetPath,
                        backupFilePath,
                        overwrite: false);

                    length = new FileInfo(backupFilePath).Length;
                    sha256 = Calc(backupFilePath);
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
                ManName);

            var manifestText = JsonSerializer.Serialize(
                manifest,
                _Options);
            R3Bound.Check(
                manifestText,
                R3Limits.BkpByte,
                "R3Config backup manifest");

            File.WriteAllText(
                manifestPath,
                manifestText);

            var persisted = ReadMan(manifestPath);
            if (!ManEqual(manifest, persisted))
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

        var snapshotPath = GetMan(manifest);
        Restore(new BackupSnapshot(
            snapshotPath,
            manifest.MadeAt,
            manifest.Files));
    }

    public void Restore(BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_RstSync)
        {
            var validated = VldSnap(snapshot);
            List<RstOp> operations = [];

            try
            {
                foreach (var entry in validated)
                {
                    var operation = new RstOp(entry);
                    operations.Add(operation);

                    if (!entry.Entry.Existed)
                    {
                        continue;
                    }

                    var targetDirectory = Path.GetDirectoryName(entry.TgtPath)
                        ?? throw new InvalidDataException(
                            $"A restore target has no parent directory: {entry.TgtPath}");
                    Directory.CreateDirectory(targetDirectory);

                    operation.RstPath = MakeStg(
                        entry.TgtPath,
                        "r3restore");
                    File.Copy(
                        entry.BkpPath,
                        operation.RstPath,
                        overwrite: false);
                    Check(
                        entry.BkpPath,
                        operation.RstPath,
                        "Restore stage validation failed.");
                }

                foreach (var operation in operations)
                {
                    operation.Existed = File.Exists(
                        operation.Vld.TgtPath);
                    if (!operation.Existed)
                    {
                        continue;
                    }

                    operation.Path = MakeStg(
                        operation.Vld.TgtPath,
                        "r3rollback");
                    File.Copy(
                        operation.Vld.TgtPath,
                        operation.Path,
                        overwrite: false);
                    Check(
                        operation.Vld.TgtPath,
                        operation.Path,
                        "Rollback stage validation failed.");
                }

                try
                {
                    foreach (var operation in operations)
                    {
                        _commitO(
                            operation.RstPath,
                            operation.Vld.TgtPath,
                            operation.Vld.Entry.Existed);

                        VldOp(operation);
                    }
                }
                catch (Exception commitException)
                {
                    var rollbackErrors = new List<Exception>();

                    foreach (var operation in operations.AsEnumerable().Reverse())
                    {
                        try
                        {
                            _Op(
                                operation.Path,
                                operation.Vld.TgtPath,
                                operation.Existed);
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
                    TryDel(operation.RstPath);
                    TryDel(operation.Path);
                }
            }
        }
    }

    private IReadOnlyList<VldBkp> VldSnap(
        BackupSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.DirPath))
        {
            throw new InvalidDataException(
                "The backup snapshot directory is empty.");
        }

        var snapshotPath = Path.GetFullPath(snapshot.DirPath);
        if (!Directory.Exists(snapshotPath))
        {
            throw new DirectoryNotFoundException(
                $"Backup snapshot directory not found: {snapshotPath}");
        }

        var canonicalSnapshotPath = CanPath.GetPath(
            snapshotPath);
        if (!Paths(
                snapshotPath,
                canonicalSnapshotPath))
        {
            throw new InvalidDataException(
                "The backup snapshot directory resolves through a reparse point.");
        }

        var manifestPath = Path.Combine(
            snapshotPath,
            ManName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "The backup snapshot manifest is missing.",
                manifestPath);
        }

        var canonicalManifestPath = CanPath.GetPath(
            manifestPath);
        if (!IsChild(
                canonicalSnapshotPath,
                canonicalManifestPath))
        {
            throw new InvalidDataException(
                "The backup manifest resolves outside its snapshot directory.");
        }

        var persisted = ReadMan(manifestPath);
        var requested = new BackupManifest(
            snapshot.MadeAt,
            snapshot.Files);
        if (!ManEqual(requested, persisted))
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
        var validated = new List<VldBkp>(
            persisted.Files.Count);

        foreach (var entry in persisted.Files)
        {
            if (string.IsNullOrWhiteSpace(entry.TgtPath) ||
                string.IsNullOrWhiteSpace(entry.BkpPath) ||
                !Path.IsPathFullyQualified(entry.TgtPath) ||
                !Path.IsPathFullyQualified(entry.BkpPath))
            {
                throw new InvalidDataException(
                    "A backup manifest entry contains an invalid path.");
            }

            if (Contains(entry.BkpPath))
            {
                throw new InvalidDataException(
                    "A backup manifest path contains parent traversal.");
            }

            var targetPath = Path.GetFullPath(entry.TgtPath);
            var backupPath = Path.GetFullPath(entry.BkpPath);
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
                MakeBkp(targetPath));
            if (!Paths(backupPath, expectedBackupPath))
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

                var canonicalBackupPath = CanPath.GetPath(
                    backupPath);
                if (!IsChild(
                        canonicalSnapshotPath,
                        canonicalBackupPath))
                {
                    throw new InvalidDataException(
                        "A backup file resolves outside its snapshot directory.");
                }

                VldMeta(entry, backupPath);
            }

            validated.Add(new VldBkp(
                entry,
                targetPath,
                backupPath));
        }

        return validated;
    }

    private static List<string> NormTgt(
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

    private static void VldMeta(
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
                    Calc(backupPath),
                    entry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Backup file SHA-256 mismatch: {backupPath}");
            }
        }
    }

    private static void VldOp(
        RstOp operation)
    {
        if (!operation.Vld.Entry.Existed)
        {
            if (File.Exists(operation.Vld.TgtPath))
            {
                throw new IOException(
                    $"Restore failed to remove target: {operation.Vld.TgtPath}");
            }

            return;
        }

        if (!File.Exists(operation.Vld.TgtPath))
        {
            throw new IOException(
                $"Restore failed to create target: {operation.Vld.TgtPath}");
        }

        Check(
            operation.Vld.BkpPath,
            operation.Vld.TgtPath,
            "Committed restore validation failed.");
    }

    private BackupSnapshot? TryLoad(string backupDirectory)
    {
        var manifestPath = Path.Combine(
            backupDirectory,
            ManName);

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = ReadMan(manifestPath);
            return manifest.Files is null
                ? null
                : new BackupSnapshot(
                    backupDirectory,
                    manifest.MadeAt,
                    manifest.Files);
        }
        catch
        {
            return null;
        }
    }

    private BackupManifest ReadMan(string manifestPath)
    {
        var text = R3Bound.ReadUtf8(
            manifestPath,
            R3Limits.BkpByte,
            "R3Config backup manifest");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            text,
            _Options);

        return manifest ?? throw new InvalidDataException(
            "The backup manifest is empty or invalid.");
    }

    private static string GetMan(
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
                Path.GetFullPath(entry.BkpPath))
                ?? throw new InvalidDataException(
                    "A backup manifest path has no snapshot directory.");

            if (snapshotPath is null)
            {
                snapshotPath = candidate;
                continue;
            }

            if (!Paths(snapshotPath, candidate))
            {
                throw new InvalidDataException(
                    "A backup manifest references more than one snapshot directory.");
            }
        }

        return snapshotPath!;
    }

    private static bool ManEqual(
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
            if (!Paths(leftEntry.TgtPath, rightEntry.TgtPath) ||
                !Paths(leftEntry.BkpPath, rightEntry.BkpPath) ||
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

    private static string MakeBkp(string path)
    {
        var normalizedPath = Path
            .GetFullPath(path)
            .ToUpperInvariant();

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(normalizedPath));

        return Convert.ToHexString(hash);
    }

    private static string Calc(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 &&
               value.All(Uri.IsHexDigit);
    }

    private static void Check(
        string expectedPath,
        string actualPath,
        string error)
    {
        var expectedInfo = new FileInfo(expectedPath);
        var actualInfo = new FileInfo(actualPath);
        if (expectedInfo.Length != actualInfo.Length ||
            !string.Equals(
                Calc(expectedPath),
                Calc(actualPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{error} Expected '{expectedPath}', actual '{actualPath}'.");
        }
    }

    private static string MakeStg(
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

    private static void CommitOp(
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

    private static void Rollback(
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

    private static bool Contains(string path)
    {
        return path.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(
                "..",
                StringComparison.Ordinal));
    }

    private static bool IsChild(
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

    private static bool Paths(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDel(string? path)
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

    private sealed record VldBkp(
        BackupFileEntry Entry,
        string TgtPath,
        string BkpPath);

    private sealed class RstOp(
        VldBkp validated)
    {
        public VldBkp Vld { get; } = validated;

        public string? RstPath { get; set; }

        public string? Path { get; set; }

        public bool Existed { get; set; }
    }
}

#pragma warning restore IDE0130
