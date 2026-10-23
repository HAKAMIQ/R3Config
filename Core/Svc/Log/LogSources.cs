using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public enum LogSourceType
{
    PlainText,
    Gzip,
    ZipEntry
}

// Why a log source could not be opened. Fail-closed: an ambiguous archive is
// never guessed at; Candidates lists what the caller may choose from.
public sealed class LogSourceException(string reason, IReadOnlyList<string>? candidates = null) : Exception(reason)
{
    public IReadOnlyList<string> Candidates { get; } = candidates ?? [];
}

// A readable log, whatever holds it. Local files only: R3Config never downloads
// a log. OpenRead yields the log's own decompressed bytes.
public interface ILogSource
{
    string Name { get; }

    LogSourceType Type { get; }

    IReadOnlyList<string> Warnings { get; }

    Stream OpenRead(CancellationToken cancellationToken = default);
}

// A plain RPCS3.log or a rotated RPCS3.log.gz. Opened for shared read (RPCS3
// may still be writing) and never modified.
public sealed class LocalFileLogSource(LogSourceInfo info) : ILogSource
{
    public string Name => System.IO.Path.GetFileName(info.Path);

    public LogSourceType Type => info.Kind == LogSourceKind.RotatedGzip ? LogSourceType.Gzip : LogSourceType.PlainText;

    public IReadOnlyList<string> Warnings { get; } = [];

    public Stream OpenRead(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = new FileStream(
            info.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        return info.Kind == LogSourceKind.RotatedGzip ? new GZipStream(file, CompressionMode.Decompress) : file;
    }
}

// One log inside a local .zip. The archive is only READ: nothing is extracted
// to disk, so there is no path to traverse. Entry names that are rooted,
// contain "..", a drive colon or control characters are never candidates;
// nested archives are never opened; the number of entries and the declared
// uncompressed size are bounded. More than one candidate log fails closed
// unless the caller names the entry.
public sealed class LocalArchiveLogSource : ILogSource
{
    public const int MaxEntries = 10_000;
    public const long MaxEntryBytes = 256L * 1024 * 1024;

    private static readonly string[] NestedArchiveExtensions = [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"];
    private readonly string _path;
    private readonly string _entry;

    private LocalArchiveLogSource(string path, string entry, IReadOnlyList<string> warnings)
    {
        _path = path;
        _entry = entry;
        Warnings = warnings;
    }

    public string Name => System.IO.Path.GetFileName(_path) + "!" + _entry;

    public LogSourceType Type => LogSourceType.ZipEntry;

    public IReadOnlyList<string> Warnings { get; }

    public static LocalArchiveLogSource Open(string path, string? entryName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var archive = OpenArchive(path);
        if (archive.Entries.Count > MaxEntries)
        {
            throw new LogSourceException($"The archive lists more than {MaxEntries} entries.");
        }

        var warnings = new List<string>();
        var candidates = new List<ZipArchiveEntry>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.Length == 0)
            {
                continue;
            }

            if (!SafeName(entry.FullName))
            {
                warnings.Add("UnsafeEntryIgnored:" + Printable(entry.FullName));
                continue;
            }

            var extension = System.IO.Path.GetExtension(entry.FullName).ToLowerInvariant();
            if (NestedArchiveExtensions.Contains(extension))
            {
                warnings.Add("NestedArchiveIgnored:" + Printable(entry.FullName));
                continue;
            }

            if (extension is ".log" or ".txt")
            {
                candidates.Add(entry);
            }
        }

        var names = candidates.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        ZipArchiveEntry? chosen;
        if (entryName is not null)
        {
            chosen = candidates.FirstOrDefault(entry => string.Equals(entry.FullName, entryName, StringComparison.Ordinal));
            if (chosen is null)
            {
                throw new LogSourceException("The named entry is not a log candidate of this archive.", names);
            }
        }
        else if (candidates.Count == 0)
        {
            throw new LogSourceException("The archive holds no .log or .txt entry.", names);
        }
        else if (candidates.Count > 1)
        {
            throw new LogSourceException("The archive holds more than one log candidate; name the entry.", names);
        }
        else
        {
            chosen = candidates[0];
        }

        if (chosen.Length > MaxEntryBytes)
        {
            throw new LogSourceException($"The entry declares more than {MaxEntryBytes} uncompressed bytes.");
        }

        return new LocalArchiveLogSource(path, chosen.FullName, warnings);
    }

    public Stream OpenRead(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archive = OpenArchive(_path);
        try
        {
            var entry = archive.GetEntry(_entry) ?? throw new LogSourceException("The entry disappeared from the archive.");
            return new ArchiveEntryStream(archive, entry.Open(), MaxEntryBytes);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static ZipArchive OpenArchive(string path)
    {
        try
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                return new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        catch (InvalidDataException ex)
        {
            throw new LogSourceException("The file is not a readable zip archive: " + ex.Message);
        }
    }

    private static bool SafeName(string name)
    {
        if (name.Length == 0 || name.Length > 260 || name[0] is '/' or '\\' || name.Contains(':', StringComparison.Ordinal) ||
            name.Any(char.IsControl))
        {
            return false;
        }

        return !name.Split('/', '\\').Any(part => part == "..");
    }

    private static string Printable(string name) =>
        new(name.Take(80).Select(ch => char.IsControl(ch) ? '?' : ch).ToArray());

    // Enforces the byte budget on what the entry actually yields (the declared
    // length can lie) and disposes the archive with the stream.
    private sealed class ArchiveEntryStream(ZipArchive archive, Stream inner, long budget) : Stream
    {
        private long _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            _read += read;
            if (_read > budget)
            {
                throw new LogSourceException($"The entry yielded more than {budget} bytes.");
            }

            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                archive.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

public static class LogSourceResolver
{
    // .zip => archive source; .gz => rotated gzip; anything else => plain text.
    public static ILogSource FromPath(string path, string? archiveEntry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var info = new FileInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LogSourceException("The log file does not exist or is a reparse point.");
        }

        var extension = info.Extension.ToLowerInvariant();
        return extension switch
        {
            ".zip" => LocalArchiveLogSource.Open(info.FullName, archiveEntry),
            ".gz" => new LocalFileLogSource(new LogSourceInfo(info.FullName, LogSourceKind.RotatedGzip, info.Length, info.LastWriteTimeUtc)),
            _ => new LocalFileLogSource(new LogSourceInfo(info.FullName, LogSourceKind.Current, info.Length, info.LastWriteTimeUtc))
        };
    }
}

#pragma warning restore IDE0130
