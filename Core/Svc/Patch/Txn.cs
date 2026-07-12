using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchFileTransactionService
{
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly BackupService _backupService;
    private readonly PatchCatalogReader _catalogReader;
    private readonly PatchConfigReader _configReader;
    private readonly Action<string, string> _commitFile;

    public PatchFileTransactionService(
        BackupService? backupService = null,
        PatchCatalogReader? catalogReader = null,
        PatchConfigReader? configReader = null,
        Action<string, string>? commitFile = null)
    {
        _backupService = backupService ?? new BackupService();
        _catalogReader = catalogReader ?? new PatchCatalogReader();
        _configReader = configReader ?? new PatchConfigReader();
        _commitFile = commitFile ?? CommitStagedFile;
    }

    public BackupManifest Commit(
        string backupRootPath,
        IReadOnlyList<PatchPreparedFile> preparedFiles)
    {
        if (preparedFiles.Count == 0)
        {
            throw new InvalidOperationException("Patch file transaction is empty.");
        }

        var files = NormalizeAndValidateTargets(preparedFiles);
        var staged = new List<StagedPatchFile>();

        try
        {
            foreach (var file in files)
            {
                staged.Add(StageAndValidate(file));
            }

            var backup = _backupService.CreateBackup(
                backupRootPath,
                files.Select(file => file.TargetPath).ToArray());

            try
            {
                foreach (var file in staged)
                {
                    _commitFile(file.StagedPath, file.Prepared.TargetPath);
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
                        new AggregateException(commitException, restoreException));
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

    private static IReadOnlyList<PatchPreparedFile> NormalizeAndValidateTargets(
        IReadOnlyList<PatchPreparedFile> preparedFiles)
    {
        var output = new List<PatchPreparedFile>(preparedFiles.Count);
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in preparedFiles)
        {
            if (string.IsNullOrWhiteSpace(file.TargetPath))
            {
                throw new ArgumentException("A transaction target path is empty.");
            }

            if (string.IsNullOrWhiteSpace(file.Content))
            {
                throw new InvalidDataException(
                    $"Prepared patch file is empty: {file.TargetPath}");
            }

            var targetPath = Path.GetFullPath(file.TargetPath);
            if (!seenTargets.Add(targetPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate transaction target: {targetPath}");
            }

            output.Add(file with { TargetPath = targetPath });
        }

        return output;
    }

    private StagedPatchFile StageAndValidate(PatchPreparedFile prepared)
    {
        var directory = Path.GetDirectoryName(prepared.TargetPath)
            ?? throw new InvalidOperationException(
                $"Transaction target has no parent directory: {prepared.TargetPath}");

        Directory.CreateDirectory(directory);

        var stagedPath = Path.Combine(
            directory,
            $".{Path.GetFileName(prepared.TargetPath)}.{Guid.NewGuid():N}.r3stage");

        try
        {
            File.WriteAllText(stagedPath, prepared.Content, Utf8NoBom);

            var readBack = File.ReadAllText(stagedPath, Encoding.UTF8);
            if (!string.Equals(readBack, prepared.Content, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged file read-back mismatch: {prepared.TargetPath}");
            }

            ValidateStagedFile(stagedPath, prepared);
            return new StagedPatchFile(prepared, stagedPath);
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
                using (var document = JsonDocument.Parse(File.ReadAllText(stagedPath, Encoding.UTF8)))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException("R3Config Patch metadata root must be a JSON object.");
                    }
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported prepared patch file kind: {prepared.Kind}");
        }
    }

    private static void CommitStagedFile(string stagedPath, string targetPath)
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort staging cleanup. A transaction result never depends on it.
        }
    }

    private sealed class StagedPatchFile
    {
        public StagedPatchFile(
            PatchPreparedFile prepared,
            string stagedPath)
        {
            Prepared = prepared;
            StagedPath = stagedPath;
        }

        public PatchPreparedFile Prepared { get; }

        public string StagedPath { get; }
    }
}
