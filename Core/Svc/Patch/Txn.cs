using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public enum PatchTransactionPhase
{
    BeforeStage,
    BeforeBackup,
    BeforeCommit
}

public delegate void PatchPathValidator(
    PatchTransactionPhase phase,
    string backupRootPath,
    IReadOnlyList<string> targetPaths);

public sealed class PatchFileTransactionService(
    BackupService? backupService = null,
    PatchCatalogReader? catalogReader = null,
    PatchConfigReader? configReader = null,
    Action<string, string>? commitFile = null,
    Action<string>? deleteFile = null)
{
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly BackupService _backupService =
        backupService ?? new BackupService();

    private readonly PatchCatalogReader _catalogReader =
        catalogReader ?? new PatchCatalogReader();

    private readonly PatchConfigReader _configReader =
        configReader ?? new PatchConfigReader();

    private readonly Action<string, string> _commitFile =
        commitFile ?? CommitStagedFile;

    private readonly Action<string> _deleteFile =
        deleteFile ?? CommitDelete;

    public BackupManifest Commit(
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles,
        PatchPathValidator? validatePaths = null)
    {
        if (preparedFiles.Count == 0)
        {
            throw new InvalidOperationException(
                "Patch file transaction is empty.");
        }

        var files = NormalizeAndValidateTargets(preparedFiles);
        var targets = files
            .Select(file => file.TargetPath)
            .ToArray();
        List<StagedPatchFile> staged = [];

        try
        {
            validatePaths?.Invoke(
                PatchTransactionPhase.BeforeStage,
                backupRootPath,
                targets);

            foreach (var file in files)
            {
                staged.Add(StageAndValidate(file));
            }

            validatePaths?.Invoke(
                PatchTransactionPhase.BeforeBackup,
                backupRootPath,
                targets);

            var backup = _backupService.CreateBackup(
                backupRootPath,
                targets);

            validatePaths?.Invoke(
                PatchTransactionPhase.BeforeCommit,
                backupRootPath,
                targets);

            try
            {
                foreach (var file in staged)
                {
                    if (file.Prepared.DeleteTarget)
                    {
                        _deleteFile(file.Prepared.TargetPath);
                    }
                    else
                    {
                        _commitFile(
                            file.StagedPath ?? throw new InvalidOperationException(
                                "A staged patch file is missing."),
                            file.Prepared.TargetPath);
                    }
                }

                return backup;
            }
            catch (Exception commitException)
            {
                try
                {
                    _backupService.Restore(backup);
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
                TryDelete(file.StagedPath);
            }
        }
    }

    private static List<PatchPreparedFile> NormalizeAndValidateTargets(
        IReadOnlyList<PatchPreparedFile> preparedFiles)
    {
        var output = new List<PatchPreparedFile>(
            preparedFiles.Count);

        var seenTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in preparedFiles)
        {
            if (string.IsNullOrWhiteSpace(file.TargetPath))
            {
                throw new ArgumentException(
                    "A transaction target path is empty.");
            }

            if (file.DeleteTarget &&
                !string.IsNullOrEmpty(file.Content))
            {
                throw new InvalidDataException(
                    $"A prepared delete operation contains file content: {file.TargetPath}");
            }

            if (!file.DeleteTarget &&
                string.IsNullOrWhiteSpace(file.Content))
            {
                throw new InvalidDataException(
                    $"Prepared patch file is empty: {file.TargetPath}");
            }

            if (!file.DeleteTarget &&
                file.Kind == PatchPreparedFileKind.JsonMetadata)
            {
                Rpcs3BoundedInput.EnsureTextLength(
                    file.Content,
                    Rpcs3InputLimits.HCanaryMetadataBytes,
                    "R3Config H-Canary metadata");
            }

            var targetPath = Path.GetFullPath(file.TargetPath);

            if (!seenTargets.Add(targetPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate transaction target: {targetPath}");
            }

            output.Add(file with
            {
                TargetPath = targetPath
            });
        }

        return output;
    }

    private StagedPatchFile StageAndValidate(
        PatchPreparedFile prepared)
    {
        if (prepared.DeleteTarget)
        {
            return new StagedPatchFile(
                prepared,
                stagedPath: null);
        }

        var directory = Path.GetDirectoryName(prepared.TargetPath)
            ?? throw new InvalidOperationException(
                $"Transaction target has no parent directory: {prepared.TargetPath}");

        Directory.CreateDirectory(directory);

        var stagedPath = Path.Combine(
            directory,
            $".{Path.GetFileName(prepared.TargetPath)}.{Guid.NewGuid():N}.r3stage");

        try
        {
            File.WriteAllText(
                stagedPath,
                prepared.Content,
                Utf8NoBom);

            var readBack = File.ReadAllText(
                stagedPath,
                Encoding.UTF8);

            if (!string.Equals(
                    readBack,
                    prepared.Content,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged file read-back mismatch: {prepared.TargetPath}");
            }

            ValidateStagedFile(stagedPath, prepared);

            return new StagedPatchFile(
                prepared,
                stagedPath);
        }
        catch
        {
            TryDelete(stagedPath);
            throw;
        }
    }

    private void ValidateStagedFile(
        string stagedPath,
        PatchPreparedFile prepared)
    {
        switch (prepared.Kind)
        {
            case PatchPreparedFileKind.PatchCatalog:
                {
                    var read = _catalogReader.Read(
                        stagedPath,
                        prepared.CatalogSource);

                    if (!read.Succeeded)
                    {
                        throw new InvalidDataException(
                            read.Error ??
                            $"Staged patch catalog is invalid: {prepared.TargetPath}");
                    }

                    if (prepared.ExpectedEntryCount is int expected &&
                        read.Catalog.Entries.Count != expected)
                    {
                        throw new InvalidDataException(
                            $"Staged patch catalog entry count mismatch for {prepared.TargetPath}. " +
                            $"Expected {expected}, actual {read.Catalog.Entries.Count}.");
                    }

                    break;
                }

            case PatchPreparedFileKind.PatchConfig:
                _configReader.Load(stagedPath);
                break;

            case PatchPreparedFileKind.JsonMetadata:
                using (var document = JsonDocument.Parse(
                           Rpcs3BoundedInput.ReadUtf8TextFile(
                               stagedPath,
                               Rpcs3InputLimits.HCanaryMetadataBytes,
                               "R3Config H-Canary metadata"),
                           new JsonDocumentOptions
                           {
                               MaxDepth = Rpcs3InputLimits.YamlDepth
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

            default:
                throw new InvalidOperationException(
                    $"Unsupported prepared patch file kind: {prepared.Kind}");
        }
    }

    private static void CommitStagedFile(
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

    private static void CommitDelete(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
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

    private sealed class StagedPatchFile(
        PatchPreparedFile prepared,
        string? stagedPath)
    {
        public PatchPreparedFile Prepared { get; } = prepared;

        public string? StagedPath { get; } = stagedPath;
    }
}

#pragma warning restore IDE0130
