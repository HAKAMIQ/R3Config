using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed record LogReadResult(
    IReadOnlyList<LogSession> Sessions,
    IReadOnlyList<string> Notes);

// Locates the RPCS3 logs of the current installation from its own layout and
// reads them strictly read-only (shared open, no write access, no delete, no
// truncation). A log under a reparse point is not followed.
public sealed class Rpcs3LogService
{
    // Bounds for untrusted, possibly huge input.
    public const long MaxDecompressedBytes = 256L * 1024 * 1024;
    public const int MaxLineChars = 64 * 1024;
    public const int MaxSources = 4;

    // The plain logs of both roots the install scan knows (executable folder
    // and, when proven, the data root), and the rotated ".gz" next to each.
    // Nothing else is assumed to be a log.
    public static IReadOnlyList<LogSourceInfo> Locate(Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var roots = new List<string> { install.RootPath };
        if (!string.IsNullOrWhiteSpace(install.DataRoot))
        {
            roots.Add(install.DataRoot);
        }

        var found = new List<LogSourceInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var plain in Rpcs3LogLocator.PlainLogPaths(root))
            {
                Add(plain, LogSourceKind.Current);
                Add(plain + ".gz", LogSourceKind.RotatedGzip);
            }
        }

        return
        [
            .. found
                .OrderBy(item => item.Kind)
                .ThenByDescending(item => item.LastWriteUtc)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxSources)
        ];

        void Add(string path, LogSourceKind kind)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists ||
                    (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    !seen.Add(info.FullName))
                {
                    return;
                }

                found.Add(new LogSourceInfo(info.FullName, kind, info.Length, info.LastWriteTimeUtc));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // An unreadable candidate is simply not a source.
            }
        }
    }

    public static LogReadResult Read(Rpcs3InstallDescriptor install, string? preferSerial = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sessions = new List<LogSession>();
        var notes = new List<string>();
        var sources = Locate(install);
        if (sources.Count == 0)
        {
            notes.Add("NoLogFound");
        }

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                sessions.Add(ReadOne(source, preferSerial, cancellationToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                notes.Add("Unreadable:" + Path.GetFileName(source.Path));
            }
        }

        return new LogReadResult(sessions, notes);
    }

    public static LogSession ReadOne(LogSourceInfo source, string? preferSerial = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Read(new LocalFileLogSource(source), source, preferSerial, cancellationToken);
    }

    // Reads any local log source (plain, .gz, or one entry of a .zip) read-only,
    // with a byte budget, and attaches the reproducibility manifest. The hash is
    // of the decompressed text the parser actually consumed, so the same log
    // hashes the same whether it was plain, gzip or zipped.
    public static LogSession Read(
        ILogSource logSource,
        LogSourceInfo? info = null,
        string? preferSerial = null,
        CancellationToken cancellationToken = default) =>
        Read(logSource, MaxDecompressedBytes, info, preferSerial, cancellationToken);

    public static LogSession Read(
        ILogSource logSource,
        long maxDecompressedBytes,
        LogSourceInfo? info = null,
        string? preferSerial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDecompressedBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxDecompressedBytes, MaxDecompressedBytes);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var raw = logSource.OpenRead(cancellationToken);
        using var hashing = new HashingStream(raw, hash, maxDecompressedBytes, cancellationToken);
        using var reader = new StreamReader(hashing, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);

        var lines = new BoundedLines(reader, hashing, cancellationToken);
        var session = Rpcs3LogParser.Parse(lines, info, preferSerial, cancellationToken: cancellationToken);
        var truncated = lines.Truncated || hashing.LimitReached;
        if (truncated)
        {
            session = session with { Truncated = true };
        }

        var warnings = new List<string>(logSource.Warnings);
        if (truncated)
        {
            warnings.Add("HashCoversReadPrefixOnly");
        }

        var completeness = session.RunContext.Completeness;
        var manifest = new LogAnalysisManifest(
            Rpcs3LogParser.ParserVersion,
            LogRules.RuleSetVersion,
            Convert.ToHexString(hash.GetHashAndReset()),
            logSource.Type.ToString(),
            session.TotalLines,
            session.BootCount,
            truncated || completeness == LogCompleteness.Partial,
            completeness,
            warnings,
            new Dictionary<string, string>(StringComparer.Ordinal));
        timer.Stop();
        return session with { Analysis = manifest, AnalysisDuration = timer.Elapsed };
    }

    // Hashes exactly the bytes handed to the reader.
    private sealed class HashingStream(Stream inner, IncrementalHash hash, long budget, CancellationToken cancellationToken) : Stream
    {
        private long _remaining = budget;
        public bool LimitReached { get; private set; }
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
            cancellationToken.ThrowIfCancellationRequested();
            if (count == 0) return 0;
            if (_remaining == 0)
            {
                // Do not read beyond the prefix even to probe for EOF.
                LimitReached = true;
                return 0;
            }

            var read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            cancellationToken.ThrowIfCancellationRequested();
            _remaining -= read;
            if (read > 0)
            {
                hash.AppendData(buffer, offset, read);
            }

            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Never materialize an unbounded line. A partial line at either limit is
    // discarded, so a clipped identity or event cannot become parser evidence.
    private sealed class BoundedLines(StreamReader reader, HashingStream stream, CancellationToken cancellationToken = default) : IEnumerable<string>
    {
        public bool Truncated { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            var line = new StringBuilder();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var next = reader.Read();
                if (next < 0)
                {
                    if (!stream.LimitReached && line.Length > 0) yield return line.ToString();
                    yield break;
                }

                if (next is '\r' or '\n')
                {
                    if (next == '\r' && reader.Peek() == '\n') reader.Read();
                    yield return line.ToString();
                    line.Clear();
                    continue;
                }

                if (line.Length == MaxLineChars)
                {
                    Truncated = true;
                    yield break;
                }

                line.Append((char)next);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

#pragma warning restore IDE0130
