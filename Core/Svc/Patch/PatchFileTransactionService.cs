using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public enum PatPhase
{
    Stg,
    Bkp,
    Commit
}

public delegate void PathVld(
    PatPhase phase,
    string backupRootPath,
    IReadOnlyList<string> targetPaths);

public readonly record struct PatchTargetState(
    bool Exists,
    string? Sha256,
    long MaxBytes,
    string Description);

public sealed class PatchFileTransactionService(
    BackupService? backupService = null,
    ReadSvc? catalogReader = null,
    CfgRead? configReader = null,
    Action<string, string>? commitFile = null,
    Action<string>? deleteFile = null)
{
    private static readonly Encoding Utf8Bom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly BackupService _BkpSvc =
        backupService ?? new BackupService();

    private readonly ReadSvc _Reader =
        catalogReader ?? new ReadSvc();

    private readonly CfgRead _Reader2 =
        configReader ?? new CfgRead();

    private readonly Action<string, string> _File =
        commitFile ?? Commit;

    private readonly Action<string> _DelFile =
        deleteFile ?? Commit2;

    public static PatchTargetState CaptureExpectedState(
        string targetPath,
        long maxBytes,
        string description)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var fullPath = Path.GetFullPath(targetPath);
        if (!File.Exists(fullPath))
        {
            return new PatchTargetState(
                false,
                null,
                maxBytes,
                description);
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length > maxBytes)
        {
            throw new InvalidDataException(
                $"The {description} exceeds the {maxBytes}-byte input limit.");
        }

        return new PatchTargetState(
            true,
            Convert.ToHexString(SHA256.HashData(stream)),
            maxBytes,
            description);
    }

    public static PatchPreparedFile CaptureExpectedState(
        PatchPreparedFile preparedFile)
    {
        ArgumentNullException.ThrowIfNull(preparedFile);
        var policy = ExpectedStatePolicy(
            preparedFile.Kind);
        var state = CaptureExpectedState(
            preparedFile.TgtPath,
            policy.MaxBytes,
            policy.Description);
        return preparedFile with
        {
            ExpectedExists = state.Exists,
            ExpectedSha256 = state.Sha256
        };
    }

    public static void ValidateExpectedState(
        string targetPath,
        PatchTargetState expectedState)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            expectedState.MaxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            expectedState.Description);

        var fullPath = Path.GetFullPath(targetPath);
        var exists = File.Exists(fullPath);
        if (exists != expectedState.Exists)
        {
            throw new IOException(
                $"Transaction target changed externally before commit: {fullPath}");
        }

        if (!exists)
        {
            return;
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length > expectedState.MaxBytes)
        {
            throw new InvalidDataException(
                $"The {expectedState.Description} exceeds the {expectedState.MaxBytes}-byte input limit.");
        }

        var digest = Convert.ToHexString(
            SHA256.HashData(stream));
        if (!string.Equals(
                digest,
                expectedState.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                $"Transaction target changed externally before commit: {fullPath}");
        }
    }

    public BackupManifest Commit(
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        PathVld? validatePaths = null)
    {
        if (preparedFiles.Count == 0)
        {
            throw new InvalidOperationException(
                "Patch file transaction is empty.");
        }

        var files = NormAnd(preparedFiles);
        var targets = files
            .Select(file => file.TgtPath)
            .ToArray();
        List<StgFile> staged = [];

        try
        {
            validatePaths?.Invoke(
                PatPhase.Stg,
                backupRootPath,
                targets);

            ValidateExpectedState(files);

            foreach (var file in files)
            {
                staged.Add(StgAnd(file));
            }

            ValidateStages(staged);
            ValidateExpectedState(files);

            validatePaths?.Invoke(
                PatPhase.Bkp,
                backupRootPath,
                targets);

            var backup = _BkpSvc.BuildBkp(
                backupRootPath,
                targets);

            ValidateStages(staged);
            ValidateExpectedState(files);

            validatePaths?.Invoke(
                PatPhase.Commit,
                backupRootPath,
                targets);
            ValidateStages(staged);
            ValidateExpectedState(files);

            var attemptedFiles = new List<PatchPreparedFile>(
                staged.Count);
            try
            {
                foreach (var file in staged)
                {
                    ValidateStage(file);
                    ValidateExpectedState([file.Prepared]);
                    attemptedFiles.Add(file.Prepared);
                    if (file.Prepared.DelTgt)
                    {
                        _DelFile(file.Prepared.TgtPath);
                    }
                    else
                    {
                        _File(
                            file.StgPath ?? throw new InvalidOperationException(
                                "A staged patch file is missing."),
                            file.Prepared.TgtPath);
                    }
                }

                return backup;
            }
            catch (Exception commitException)
            {
                try
                {
                    if (attemptedFiles.Count > 0)
                    {
                        RollbackCommitted(
                            backup,
                            attemptedFiles);
                    }
                }
                catch (Exception restoreException)
                {
                    throw new InvalidOperationException(
                        "Patch file transaction failed and rollback also failed.",
                        new AggregateException(
                            commitException,
                            restoreException));
                }

                throw;
            }
        }
        finally
        {
            foreach (var file in staged)
            {
                TryDel(file.StgPath);
            }
        }
    }

    public void RollbackCommitted(
        BackupManifest backup,
        IReadOnlyCollection<PatchPreparedFile> attemptedFiles)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(attemptedFiles);
        if (attemptedFiles.Count == 0)
        {
            return;
        }

        var files = NormAnd([.. attemptedFiles]);
        var safeToRestore = new List<PatchPreparedFile>(files.Count);
        var rollbackErrors = new List<Exception>();

        foreach (var file in files)
        {
            try
            {
                var current = CaptureExpectedState(
                    file.TgtPath,
                    ExpectedStatePolicy(file.Kind));
                if (Matches(current, OriginalState(file)))
                {
                    continue;
                }

                if (Matches(current, CommittedState(file)))
                {
                    safeToRestore.Add(file);
                    continue;
                }

                rollbackErrors.Add(new IOException(
                    $"Rollback preserved a transaction target that changed externally after commit: {file.TgtPath}"));
            }
            catch (Exception exception)
            {
                rollbackErrors.Add(exception);
            }
        }

        if (safeToRestore.Count > 0)
        {
            try
            {
                _BkpSvc.RestoreTargets(
                    backup,
                    safeToRestore.Select(file => file.TgtPath).ToArray(),
                    () => ValidateCommittedState(safeToRestore));
            }
            catch (Exception exception)
            {
                rollbackErrors.Add(exception);
            }
        }

        if (rollbackErrors.Count > 0)
        {
            throw new AggregateException(
                "Patch rollback could not safely restore every attempted target.",
                rollbackErrors);
        }
    }

    private static List<PatchPreparedFile> NormAnd(
        IReadOnlyList<PatchPreparedFile> preparedFiles)
    {
        var output = new List<PatchPreparedFile>(
            preparedFiles.Count);

        var seenTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in preparedFiles)
        {
            if (string.IsNullOrWhiteSpace(file.TgtPath))
            {
                throw new ArgumentException(
                    "A transaction target path is empty.");
            }

            if (file.DelTgt &&
                !string.IsNullOrEmpty(file.Content))
            {
                throw new InvalidDataException(
                    $"A prepared delete operation contains file content: {file.TgtPath}");
            }

            if (!file.DelTgt &&
                string.IsNullOrWhiteSpace(file.Content))
            {
                throw new InvalidDataException(
                    $"Prepared patch file is empty: {file.TgtPath}");
            }

            if (!file.DelTgt &&
                file.Kind == PrepKind.JsonMeta)
            {
                R3Bound.Check(
                    file.Content,
                    R3Limits.HBytes,
                    "R3Config H-Canary metadata");
            }

            var targetPath = Path.GetFullPath(file.TgtPath);

            if (file.ExpectedExists is null)
            {
                throw new InvalidDataException(
                    $"Prepared patch file is missing its original existence state: {targetPath}");
            }

            if (file.ExpectedExists == true &&
                string.IsNullOrWhiteSpace(file.ExpectedSha256))
            {
                throw new InvalidDataException(
                    $"Prepared patch file is missing its original content fingerprint: {targetPath}");
            }

            if (file.ExpectedExists == false &&
                file.ExpectedSha256 is not null)
            {
                throw new InvalidDataException(
                    $"Prepared patch file has a fingerprint for a target that did not exist: {targetPath}");
            }

            if (!seenTargets.Add(targetPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate transaction target: {targetPath}");
            }

            output.Add(file with
            {
                TgtPath = targetPath
            });
        }

        return output;
    }

    private static void ValidateExpectedState(
        IReadOnlyList<PatchPreparedFile> files)
    {
        foreach (var file in files)
        {
            if (file.ExpectedExists is not bool expectedExists)
            {
                continue;
            }

            var policy = ExpectedStatePolicy(
                file.Kind);
            ValidateExpectedState(
                file.TgtPath,
                new PatchTargetState(
                    expectedExists,
                    file.ExpectedSha256,
                    policy.MaxBytes,
                    policy.Description));
        }
    }

    private static (long MaxBytes, string Description)
        ExpectedStatePolicy(PrepKind kind)
    {
        return kind switch
        {
            PrepKind.PatCat => (
                R3Limits.PatCatBy,
                "patch catalog"),
            PrepKind.PatCfg => (
                R3Limits.PatCfgBy,
                "patch_config.yml"),
            PrepKind.JsonMeta => (
                R3Limits.HBytes,
                "R3Config H-Canary metadata"),
            PrepKind.CustomCfg => (
                R3Limits.CfgByte,
                "RPCS3 custom config YAML"),
            _ => throw new InvalidOperationException(
                $"Unsupported prepared patch file kind: {kind}")
        };
    }

    private static PatchTargetState CaptureExpectedState(
        string targetPath,
        (long MaxBytes, string Description) policy)
    {
        return CaptureExpectedState(
            targetPath,
            policy.MaxBytes,
            policy.Description);
    }

    private static PatchTargetState OriginalState(
        PatchPreparedFile file)
    {
        var policy = ExpectedStatePolicy(file.Kind);
        return new PatchTargetState(
            file.ExpectedExists!.Value,
            file.ExpectedSha256,
            policy.MaxBytes,
            policy.Description);
    }

    private static PatchTargetState CommittedState(
        PatchPreparedFile file)
    {
        var policy = ExpectedStatePolicy(file.Kind);
        return file.DelTgt
            ? new PatchTargetState(
                false,
                null,
                policy.MaxBytes,
                policy.Description)
            : new PatchTargetState(
                true,
                Convert.ToHexString(SHA256.HashData(
                    Utf8Bom.GetBytes(file.Content))),
                policy.MaxBytes,
                policy.Description);
    }

    private static bool Matches(
        PatchTargetState left,
        PatchTargetState right)
    {
        return left.Exists == right.Exists &&
               (!left.Exists || string.Equals(
                   left.Sha256,
                   right.Sha256,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateCommittedState(
        IReadOnlyList<PatchPreparedFile> files)
    {
        foreach (var file in files)
        {
            ValidateExpectedState(
                file.TgtPath,
                CommittedState(file));
        }
    }

    private StgFile StgAnd(
        PatchPreparedFile prepared)
    {
        if (prepared.DelTgt)
        {
            return new StgFile(
                prepared,
                stagedPath: null);
        }

        var directory = Path.GetDirectoryName(prepared.TgtPath)
            ?? throw new InvalidOperationException(
                $"Transaction target has no parent directory: {prepared.TgtPath}");

        Directory.CreateDirectory(directory);

        var stagedPath = Path.Combine(
            directory,
            $".{Path.GetFileName(prepared.TgtPath)}.{Guid.NewGuid():N}.r3stage");

        try
        {
            WriteNew(
                stagedPath,
                prepared.Content);

            var policy = ExpectedStatePolicy(prepared.Kind);
            var readBack = R3Bound.ReadUtf8(
                stagedPath,
                policy.MaxBytes,
                policy.Description);

            if (!string.Equals(
                    readBack,
                    prepared.Content,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged file read-back mismatch: {prepared.TgtPath}");
            }

            VldStg(stagedPath, prepared);

            return new StgFile(
                prepared,
                stagedPath);
        }
        catch
        {
            TryDel(stagedPath);
            throw;
        }
    }


    private static void WriteNew(
        string path,
        string content)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(
            stream,
            Utf8Bom,
            bufferSize: 16 * 1024,
            leaveOpen: true);

        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void ValidateStages(
        IReadOnlyList<StgFile> staged)
    {
        foreach (var file in staged)
        {
            ValidateStage(file);
        }
    }

    private static void ValidateStage(StgFile file)
    {
        if (file.StgPath is null)
        {
            return;
        }

        StagedFileGuard.Validate(
            file.StgPath,
            file.Prepared.TgtPath,
            "patch transaction");
    }

    private void VldStg(
        string stagedPath,
        PatchPreparedFile prepared)
    {
        switch (prepared.Kind)
        {
            case PrepKind.PatCat:
                {
                    var read = _Reader.Read(
                        stagedPath,
                        prepared.CatSrc);

                    if (!read.IsOk)
                    {
                        throw new InvalidDataException(
                            read.Error ??
                            $"Staged patch catalog is invalid: {prepared.TgtPath}");
                    }

                    if (prepared.EntNum is int expected &&
                        read.Catalog.Entries.Count != expected)
                    {
                        throw new InvalidDataException(
                            $"Staged patch catalog entry count mismatch for {prepared.TgtPath}. " +
                            $"Expected {expected}, actual {read.Catalog.Entries.Count}.");
                    }

                    break;
                }

            case PrepKind.PatCfg:
                _Reader2.Load(stagedPath);
                break;

            case PrepKind.JsonMeta:
                using (var document = JsonDocument.Parse(
                           R3Bound.ReadUtf8(
                               stagedPath,
                               R3Limits.HBytes,
                               "R3Config H-Canary metadata"),
                           new JsonDocumentOptions
                           {
                               MaxDepth = R3Limits.Depth
                           }))
                {
                    if (document.RootElement.ValueKind !=
                        JsonValueKind.Object)
                    {
                        throw new InvalidDataException(
                            "R3Config Patch metadata root must be a JSON object.");
                    }
                }

                break;

            case PrepKind.CustomCfg:
                R3Yaml.VldText(
                    R3Bound.ReadUtf8(
                        stagedPath,
                        R3Limits.CfgByte,
                        "staged RPCS3 custom config YAML"));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported prepared patch file kind: {prepared.Kind}");
        }
    }

    private static void Commit(
        string stagedPath,
        string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(
                stagedPath,
                targetPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(stagedPath, targetPath);
        }
    }

    private static void Commit2(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
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

    private sealed class StgFile(
        PatchPreparedFile prepared,
        string? stagedPath)
    {
        public PatchPreparedFile Prepared { get; } = prepared;

        public string? StgPath { get; } = stagedPath;
    }
}

#pragma warning restore IDE0130
