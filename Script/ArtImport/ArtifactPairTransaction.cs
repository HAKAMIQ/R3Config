using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace R3Config.ArtImport;

internal static class ArtifactPairTransaction
{
    private const long MaxOutputBytes = 32L * 1024 * 1024;
    private const int MaxJournalBytes = 64 * 1024;
    private const string PreparedPhase = "prepared";
    private const string CommittedPhase = "committed";

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static IReadOnlyList<string> ReservedPaths(
        string firstPath,
        string secondPath)
    {
        return
        [
            JournalPath(firstPath),
            BackupPath(firstPath),
            BackupPath(secondPath),
            LockPath(firstPath)
        ];
    }

    public static void Write(
        string firstPath,
        string firstText,
        string secondPath,
        string secondText)
    {
        Write(
            firstPath,
            firstText,
            secondPath,
            secondText,
            afterCommit: null);
    }

    internal static void Write(
        string firstPath,
        string firstText,
        string secondPath,
        string secondText,
        Action<int>? afterCommit)
    {
        var firstDirectory = Path.GetDirectoryName(firstPath);
        var secondDirectory = Path.GetDirectoryName(secondPath);
        if (string.IsNullOrWhiteSpace(firstDirectory) ||
            string.IsNullOrWhiteSpace(secondDirectory) ||
            !Directory.Exists(firstDirectory) ||
            !Directory.Exists(secondDirectory))
        {
            throw new IOException(
                "The catalog and notice output directories must already exist.");
        }

        if (PathEquals(firstPath, secondPath))
        {
            throw new IOException(
                "The catalog and notice output paths must be distinct.");
        }

        var firstBytes = Utf8.GetBytes(firstText);
        var secondBytes = Utf8.GetBytes(secondText);
        if (firstBytes.LongLength > MaxOutputBytes ||
            secondBytes.LongLength > MaxOutputBytes)
        {
            throw new IOException(
                "Generated Artemis output exceeds the transaction size limit.");
        }

        var journalPath = JournalPath(firstPath);
        var firstBackupPath = BackupPath(firstPath);
        var secondBackupPath = BackupPath(secondPath);
        var lockPath = LockPath(firstPath);
        EnsureInternalPathsAreDistinct(
            firstPath,
            secondPath,
            journalPath,
            firstBackupPath,
            secondBackupPath,
            lockPath);

        FileStream? lockStream = null;
        try
        {
            lockStream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);

            if (File.Exists(journalPath))
            {
                RecoverInterrupted(
                    journalPath,
                    firstPath,
                    secondPath,
                    firstBackupPath,
                    secondBackupPath);
            }
            else
            {
                TryDelete(firstBackupPath);
                TryDelete(secondBackupPath);
            }

            var firstOriginal = Capture(firstPath);
            var secondOriginal = Capture(secondPath);
            var firstCommitted = Snapshot.FromBytes(firstBytes);
            var secondCommitted = Snapshot.FromBytes(secondBytes);

            var firstStage = StagePath(firstPath);
            var secondStage = StagePath(secondPath);
            var journalCreated = false;
            var firstDidCommit = false;
            var secondDidCommit = false;
            var keepRecoveryArtifacts = false;

            try
            {
                WriteNew(
                    firstStage,
                    firstBytes);
                WriteNew(
                    secondStage,
                    secondBytes);
                ValidateBytes(
                    firstStage,
                    firstBytes,
                    "catalog stage");
                ValidateBytes(
                    secondStage,
                    secondBytes,
                    "notice stage");

                if (firstOriginal.Exists)
                {
                    WriteNew(
                        firstBackupPath,
                        firstOriginal.Content!);
                }

                if (secondOriginal.Exists)
                {
                    WriteNew(
                        secondBackupPath,
                        secondOriginal.Content!);
                }

                var journal = new TransactionJournal(
                    PreparedPhase,
                    firstPath,
                    secondPath,
                    firstOriginal.Exists,
                    firstOriginal.Hash,
                    secondOriginal.Exists,
                    secondOriginal.Hash,
                    firstCommitted.Hash!,
                    secondCommitted.Hash!,
                    firstBackupPath,
                    secondBackupPath);
                WriteJournalNew(
                    journalPath,
                    journal);
                journalCreated = true;

                ValidateExpected(
                    firstPath,
                    firstOriginal,
                    "catalog");
                ValidateExpected(
                    secondPath,
                    secondOriginal,
                    "notice");

                Commit(
                    firstStage,
                    firstPath);
                firstDidCommit = true;
                ValidateExpected(
                    firstPath,
                    firstCommitted,
                    "committed catalog");
                afterCommit?.Invoke(1);

                ValidateExpected(
                    secondPath,
                    secondOriginal,
                    "notice before commit");
                ValidateExpected(
                    firstPath,
                    firstCommitted,
                    "catalog before notice commit");

                Commit(
                    secondStage,
                    secondPath);
                secondDidCommit = true;
                ValidateExpected(
                    secondPath,
                    secondCommitted,
                    "committed notice");
                ValidateExpected(
                    firstPath,
                    firstCommitted,
                    "committed catalog");
                afterCommit?.Invoke(2);

                WriteJournalReplace(
                    journalPath,
                    journal with { Phase = CommittedPhase });

                TryDelete(firstBackupPath);
                TryDelete(secondBackupPath);
                TryDelete(journalPath);
                journalCreated = false;
            }
            catch (Exception commitException)
            {
                var rollbackErrors = new List<Exception>();

                if (secondDidCommit)
                {
                    TryRollback(
                        secondPath,
                        secondOriginal,
                        secondCommitted,
                        secondBackupPath,
                        rollbackErrors);
                }

                if (firstDidCommit)
                {
                    TryRollback(
                        firstPath,
                        firstOriginal,
                        firstCommitted,
                        firstBackupPath,
                        rollbackErrors);
                }

                if (rollbackErrors.Count == 0)
                {
                    TryDelete(firstBackupPath);
                    TryDelete(secondBackupPath);
                    if (journalCreated)
                    {
                        TryDelete(journalPath);
                        journalCreated = false;
                    }

                    throw;
                }

                keepRecoveryArtifacts = true;
                throw new IOException(
                    "Artemis artifact transaction failed and rollback could not safely restore every committed output.",
                    new AggregateException(
                        new[] { commitException }
                            .Concat(rollbackErrors)));
            }
            finally
            {
                TryDelete(firstStage);
                TryDelete(secondStage);

                if (!keepRecoveryArtifacts &&
                    journalCreated)
                {
                    TryDelete(journalPath);
                }
            }
        }
        finally
        {
            lockStream?.Dispose();
            TryDelete(lockPath);
        }
    }

    internal static void RecoverInterrupted(
        string journalPath,
        string firstPath,
        string secondPath,
        string firstBackupPath,
        string secondBackupPath)
    {
        var journal = ReadJournal(journalPath);
        if (!PathEquals(journal.FirstPath, firstPath) ||
            !PathEquals(journal.SecondPath, secondPath) ||
            !PathEquals(journal.FirstBackupPath, firstBackupPath) ||
            !PathEquals(journal.SecondBackupPath, secondBackupPath))
        {
            throw new IOException(
                "The Artemis transaction journal does not match the requested output paths.");
        }

        if (journal.Phase.Equals(
                CommittedPhase,
                StringComparison.Ordinal))
        {
            TryDelete(firstBackupPath);
            TryDelete(secondBackupPath);
            TryDelete(journalPath);
            return;
        }

        if (!journal.Phase.Equals(
                PreparedPhase,
                StringComparison.Ordinal))
        {
            throw new IOException(
                "The Artemis transaction journal has an unknown phase.");
        }

        var errors = new List<Exception>();
        TryRecover(
            secondPath,
            journal.SecondOriginalExisted,
            journal.SecondOriginalHash,
            journal.SecondCommittedHash,
            secondBackupPath,
            errors);
        TryRecover(
            firstPath,
            journal.FirstOriginalExisted,
            journal.FirstOriginalHash,
            journal.FirstCommittedHash,
            firstBackupPath,
            errors);

        if (errors.Count > 0)
        {
            throw new IOException(
                "The interrupted Artemis artifact transaction could not be recovered safely.",
                new AggregateException(errors));
        }

        TryDelete(firstBackupPath);
        TryDelete(secondBackupPath);
        TryDelete(journalPath);
    }

    private static void TryRollback(
        string path,
        Snapshot original,
        Snapshot committed,
        string backupPath,
        List<Exception> errors)
    {
        try
        {
            var current = Capture(path);
            if (current.Matches(original))
            {
                return;
            }

            if (!current.Matches(committed))
            {
                throw new IOException(
                    $"Rollback preserved an Artemis output that changed externally after commit: {path}");
            }

            Restore(
                path,
                original,
                backupPath);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }
    }

    private static void TryRecover(
        string path,
        bool originalExisted,
        string? originalHash,
        string committedHash,
        string backupPath,
        List<Exception> errors)
    {
        try
        {
            var current = Capture(path);
            if (originalExisted)
            {
                if (current.Exists &&
                    string.Equals(
                        current.Hash,
                        originalHash,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            else if (!current.Exists)
            {
                return;
            }

            if (!current.Exists ||
                !string.Equals(
                    current.Hash,
                    committedHash,
                    StringComparison.Ordinal))
            {
                throw new IOException(
                    $"Recovery preserved an Artemis output that changed externally after the interrupted commit: {path}");
            }

            if (originalExisted)
            {
                if (!File.Exists(backupPath))
                {
                    throw new IOException(
                        $"Recovery backup is missing for {path}.");
                }

                var backup = Capture(backupPath);
                if (!backup.Exists ||
                    !string.Equals(
                        backup.Hash,
                        originalHash,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        $"Recovery backup hash mismatch for {path}.");
                }

                RestoreExisting(
                    path,
                    backup.Content!);
            }
            else
            {
                File.Delete(path);
            }

            var restored = Capture(path);
            if (originalExisted)
            {
                if (!restored.Exists ||
                    !string.Equals(
                        restored.Hash,
                        originalHash,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        $"Recovery verification failed for {path}.");
                }
            }
            else if (restored.Exists)
            {
                throw new IOException(
                    $"Recovery failed to remove newly-created output {path}.");
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }
    }

    private static void Restore(
        string path,
        Snapshot original,
        string backupPath)
    {
        if (original.Exists)
        {
            if (!File.Exists(backupPath))
            {
                throw new IOException(
                    $"Rollback backup is missing for {path}.");
            }

            var backup = Capture(backupPath);
            if (!backup.Exists ||
                !backup.Matches(original))
            {
                throw new IOException(
                    $"Rollback backup does not match the original output for {path}.");
            }

            RestoreExisting(
                path,
                backup.Content!);
        }
        else
        {
            File.Delete(path);
        }

        ValidateExpected(
            path,
            original,
            "rollback output");
    }

    private static void RestoreExisting(
        string path,
        byte[] content)
    {
        var stage = StagePath(path);
        try
        {
            WriteNew(
                stage,
                content);
            Commit(
                stage,
                path);
        }
        finally
        {
            TryDelete(stage);
        }
    }

    private static void ValidateBytes(
        string path,
        byte[] expected,
        string description)
    {
        var actual = Capture(path);
        if (!actual.Exists ||
            actual.Content is null ||
            !actual.Content.AsSpan().SequenceEqual(expected))
        {
            throw new IOException(
                $"Generated Artemis {description} failed staged read-back verification.");
        }
    }

    private static void ValidateExpected(
        string path,
        Snapshot expected,
        string description)
    {
        var current = Capture(path);
        if (!current.Matches(expected))
        {
            throw new IOException(
                $"Artemis {description} changed outside the transaction: {path}");
        }
    }

    private static Snapshot Capture(string path)
    {
        if (!File.Exists(path))
        {
            return Snapshot.Missing;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length > MaxOutputBytes)
        {
            throw new IOException(
                $"Artemis transaction target exceeds the {MaxOutputBytes}-byte snapshot limit: {path}");
        }

        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = stream.Read(
                buffer,
                0,
                buffer.Length);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > MaxOutputBytes)
            {
                throw new IOException(
                    $"Artemis transaction target exceeds the {MaxOutputBytes}-byte snapshot limit: {path}");
            }

            output.Write(
                buffer,
                0,
                read);
        }

        return Snapshot.FromBytes(
            output.ToArray());
    }

    private static void WriteNew(
        string path,
        byte[] content)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.WriteThrough);
        stream.Write(
            content,
            0,
            content.Length);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteJournalNew(
        string journalPath,
        TransactionJournal journal)
    {
        WriteNew(
            journalPath,
            JsonSerializer.SerializeToUtf8Bytes(journal));
    }

    private static void WriteJournalReplace(
        string journalPath,
        TransactionJournal journal)
    {
        var stage = StagePath(journalPath);
        try
        {
            WriteNew(
                stage,
                JsonSerializer.SerializeToUtf8Bytes(journal));
            Commit(
                stage,
                journalPath);
        }
        finally
        {
            TryDelete(stage);
        }
    }

    private static TransactionJournal ReadJournal(string journalPath)
    {
        var snapshot = CaptureJournal(journalPath);
        try
        {
            return JsonSerializer.Deserialize<TransactionJournal>(snapshot)
                ?? throw new IOException(
                    "The Artemis transaction journal is empty.");
        }
        catch (JsonException ex)
        {
            throw new IOException(
                "The Artemis transaction journal is invalid.",
                ex);
        }
    }

    private static byte[] CaptureJournal(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length > MaxJournalBytes)
        {
            throw new IOException(
                "The Artemis transaction journal exceeds its size limit.");
        }

        using var output = new MemoryStream();
        stream.CopyTo(output);
        if (output.Length > MaxJournalBytes)
        {
            throw new IOException(
                "The Artemis transaction journal exceeds its size limit.");
        }

        return output.ToArray();
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
            File.Move(
                stagedPath,
                targetPath);
        }
    }

    private static void EnsureInternalPathsAreDistinct(
        params string[] paths)
    {
        for (var left = 0; left < paths.Length; left++)
        {
            for (var right = left + 1; right < paths.Length; right++)
            {
                if (PathEquals(paths[left], paths[right]))
                {
                    throw new IOException(
                        $"An Artemis transaction output collides with an internal transaction artifact: {paths[left]}");
                }
            }
        }
    }

    private static bool PathEquals(
        string first,
        string second)
    {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static string JournalPath(string firstPath)
    {
        var directory = Path.GetDirectoryName(firstPath)
            ?? throw new IOException(
                "The Artemis catalog path has no parent directory.");
        return Path.Combine(
            directory,
            "." +
            Path.GetFileName(firstPath) +
            ".r3artimport.txn.json");
    }

    private static string BackupPath(string path)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new IOException(
                "An Artemis output path has no parent directory.");
        return Path.Combine(
            directory,
            "." +
            Path.GetFileName(path) +
            ".r3artimport.backup");
    }

    private static string LockPath(string firstPath)
    {
        var directory = Path.GetDirectoryName(firstPath)
            ?? throw new IOException(
                "The Artemis catalog path has no parent directory.");
        return Path.Combine(
            directory,
            "." +
            Path.GetFileName(firstPath) +
            ".r3artimport.lock");
    }

    private static string StagePath(string path)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new IOException(
                "An Artemis transaction path has no parent directory.");
        return Path.Combine(
            directory,
            "." +
            Path.GetFileName(path) +
            "." +
            Guid.NewGuid().ToString("N") +
            ".r3stage");
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
        }
    }

    private sealed record Snapshot(
        bool Exists,
        byte[]? Content,
        string? Hash)
    {
        public static Snapshot Missing { get; } = new(
            false,
            null,
            null);

        public static Snapshot FromBytes(byte[] content)
        {
            return new Snapshot(
                true,
                content,
                Convert
                    .ToHexString(
                        SHA256.HashData(content))
                    .ToLowerInvariant());
        }

        public bool Matches(Snapshot other)
        {
            if (Exists != other.Exists)
            {
                return false;
            }

            if (!Exists)
            {
                return true;
            }

            return string.Equals(
                Hash,
                other.Hash,
                StringComparison.Ordinal);
        }
    }

    private sealed record TransactionJournal(
        string Phase,
        string FirstPath,
        string SecondPath,
        bool FirstOriginalExisted,
        string? FirstOriginalHash,
        bool SecondOriginalExisted,
        string? SecondOriginalHash,
        string FirstCommittedHash,
        string SecondCommittedHash,
        string FirstBackupPath,
        string SecondBackupPath);
}
