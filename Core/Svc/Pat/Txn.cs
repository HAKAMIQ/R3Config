using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed class PatTxn(
    BkpSvc? backupService = null,
    ReadSvc? catalogReader = null,
    CfgRead? configReader = null,
    Action<string, string>? commitFile = null,
    Action<string>? deleteFile = null)
{
    private static readonly Encoding Utf8Bom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly BkpSvc _BkpSvc =
        backupService ?? new BkpSvc();

    private readonly ReadSvc _Reader =
        catalogReader ?? new ReadSvc();

    private readonly CfgRead _Reader2 =
        configReader ?? new CfgRead();

    private readonly Action<string, string> _File =
        commitFile ?? Commit;

    private readonly Action<string> _DelFile =
        deleteFile ?? Commit2;

    public BkpMan Commit(
        string backupRootPath,
        IReadOnlyList<PrepFile> preparedFiles,
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

            foreach (var file in files)
            {
                staged.Add(StgAnd(file));
            }

            validatePaths?.Invoke(
                PatPhase.Bkp,
                backupRootPath,
                targets);

            var backup = _BkpSvc.BuildBkp(
                backupRootPath,
                targets);

            validatePaths?.Invoke(
                PatPhase.Commit,
                backupRootPath,
                targets);

            try
            {
                foreach (var file in staged)
                {
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
                    _BkpSvc.Restore(backup);
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

    private static List<PrepFile> NormAnd(
        IReadOnlyList<PrepFile> preparedFiles)
    {
        var output = new List<PrepFile>(
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

    private StgFile StgAnd(
        PrepFile prepared)
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
            File.WriteAllText(
                stagedPath,
                prepared.Content,
                Utf8Bom);

            var readBack = File.ReadAllText(
                stagedPath,
                Encoding.UTF8);

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

    private void VldStg(
        string stagedPath,
        PrepFile prepared)
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
        PrepFile prepared,
        string? stagedPath)
    {
        public PrepFile Prepared { get; } = prepared;

        public string? StgPath { get; } = stagedPath;
    }
}

#pragma warning restore IDE0130
