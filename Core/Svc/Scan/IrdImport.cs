using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// ---------------------------------------------------------------------------
// Local IRD (ISO Rebuild Data) import: identity and file-list metadata of a
// known-good PS3 disc, read from a file the user supplies. Offline only, no
// lookup, no cache. Format: gzip'd (or raw) "3IRD" binary, versions 6-9.
// IRD availability never changes what the filesystem shows: with no IRD the
// answer is NotChecked, never "corrupt".
// ---------------------------------------------------------------------------

public sealed class IrdFormatException(string message) : Exception(message);

public sealed record IrdFileEntry(long Offset, string Md5);

public sealed record IrdDiscFile(string Path, long Lba, long Size);

public sealed record IrdInfo(
    byte Version,
    string ProductCode,
    string Title,
    string UpdateVersion,
    string GameVersion,
    string AppVersion,
    int RegionHashCount,
    IReadOnlyList<IrdFileEntry> FileHashes,
    IReadOnlyList<IrdDiscFile> DiscFiles,
    bool DiscListingComplete,
    string SourceSha256);

public static class IrdParser
{
    public const int MaxFileBytes = 16 * 1024 * 1024;
    public const int MaxHeaderBytes = 64 * 1024 * 1024;
    private const int MaxFiles = 200_000;
    private static readonly byte[] Magic = "3IRD"u8.ToArray();

    public static IrdInfo Parse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length < 200 || content.Length > MaxFileBytes)
        {
            throw new IrdFormatException("The data is too small or too large to be an IRD file.");
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(content));
        var raw = content.AsSpan(0, 4).SequenceEqual(Magic) ? content : Gunzip(content, MaxFileBytes);
        if (raw.Length < 200 || !raw.AsSpan(0, 4).SequenceEqual(Magic))
        {
            throw new IrdFormatException("Not an IRD file (missing 3IRD signature).");
        }

        try
        {
            using var stream = new MemoryStream(raw, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            reader.ReadInt32();
            var version = reader.ReadByte();
            if (version is < 6 or > 9)
            {
                throw new IrdFormatException($"Unsupported IRD version {version}.");
            }

            var productCode = Encoding.ASCII.GetString(reader.ReadBytes(9));
            var title = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
            var update = Encoding.ASCII.GetString(reader.ReadBytes(4)).Trim();
            var game = Encoding.ASCII.GetString(reader.ReadBytes(5)).Trim();
            var app = Encoding.ASCII.GetString(reader.ReadBytes(5)).Trim();
            if (version == 7)
            {
                reader.ReadInt32();
            }

            var header = ReadBlock(reader, MaxHeaderBytes);
            var footer = ReadBlock(reader, MaxHeaderBytes);
            _ = footer;
            var regionCount = reader.ReadByte();
            for (var index = 0; index < regionCount; index++)
            {
                reader.ReadBytes(16);
            }

            var fileCount = reader.ReadInt32();
            if (fileCount is < 0 or > MaxFiles)
            {
                throw new IrdFormatException("The IRD file count is out of range.");
            }

            var files = new List<IrdFileEntry>(fileCount);
            for (var index = 0; index < fileCount; index++)
            {
                files.Add(new IrdFileEntry(reader.ReadInt64(), Convert.ToHexString(reader.ReadBytes(16))));
            }

            reader.ReadInt32();
            if (version == 9)
            {
                reader.ReadBytes(115);
            }

            reader.ReadBytes(32);
            if (version < 9)
            {
                reader.ReadBytes(115);
            }

            reader.ReadInt32();
            var dataLength = (int)stream.Position;
            var expected = reader.ReadUInt32();
            var actual = Crc32(raw.AsSpan(0, dataLength));
            if (expected != actual)
            {
                throw new IrdFormatException($"IRD checksum mismatch (expected {expected:x8}, computed {actual:x8}).");
            }

            var listing = IsoHeaderLister.List(Gunzip(header, MaxHeaderBytes));
            return new IrdInfo(version, productCode, title, update, game, app, regionCount, files, listing.Files, listing.Complete, sourceHash);
        }
        catch (EndOfStreamException ex)
        {
            throw new IrdFormatException("The IRD data ends before its declared structure does: " + ex.Message);
        }
    }

    public static IrdInfo Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxFileBytes)
        {
            throw new IrdFormatException("The IRD file is missing or larger than the limit.");
        }

        return Parse(File.ReadAllBytes(path));
    }

    private static byte[] ReadBlock(BinaryReader reader, int limit)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > limit)
        {
            throw new IrdFormatException("An IRD block length is out of range.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new IrdFormatException("An IRD block is truncated.");
        }

        return bytes;
    }

    private static byte[] Gunzip(byte[] data, int limit)
    {
        try
        {
            using var input = new MemoryStream(data, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > limit)
                {
                    throw new IrdFormatException("Decompressed IRD data exceeds the limit.");
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new IrdFormatException("Invalid compressed IRD data: " + ex.Message);
        }
    }

    // IEEE CRC-32 (the IRD trailer checksum).
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}

// Minimal ISO 9660 file lister over the IRD header (the first sectors of the
// disc image: system area, volume descriptors and directory records).
public static class IsoHeaderLister
{
    private const int Sector = 2048;
    private const int MaxFiles = 200_000;
    private const int MaxDepth = 32;

    public sealed record Listing(IReadOnlyList<IrdDiscFile> Files, bool Complete);

    public static Listing List(byte[] header)
    {
        var files = new List<IrdDiscFile>();
        var complete = true;
        var pvd = 16 * Sector;
        if (header.Length < pvd + Sector || header[pvd] != 1 || !header.AsSpan(pvd + 1, 5).SequenceEqual("CD001"u8))
        {
            return new Listing([], false);
        }

        var root = pvd + 156;
        Walk(header, LittleEndian32(header, root + 2), LittleEndian32(header, root + 10), string.Empty, 0, files, ref complete);
        return new Listing([.. files.OrderBy(file => file.Path, StringComparer.Ordinal)], complete);
    }

    private static void Walk(byte[] header, long lba, long length, string prefix, int depth, List<IrdDiscFile> files, ref bool complete)
    {
        var start = lba * Sector;
        if (depth > MaxDepth || start < 0 || start + length > header.Length || length > int.MaxValue)
        {
            complete = false;
            return;
        }

        var position = (int)start;
        var end = (int)(start + length);
        while (position < end)
        {
            var size = header[position];
            if (size == 0)
            {
                // Directory records never straddle a sector: skip to the next one.
                position = (position / Sector + 1) * Sector;
                continue;
            }

            if (position + size > end || size < 34)
            {
                complete = false;
                return;
            }

            var flags = header[position + 25];
            var nameLength = header[position + 32];
            var nameBytes = header.AsSpan(position + 33, nameLength);
            var recordLba = LittleEndian32(header, position + 2);
            var recordSize = LittleEndian32(header, position + 10);
            position += size;

            if (nameLength == 1 && nameBytes[0] is 0 or 1)
            {
                continue;
            }

            var name = Encoding.ASCII.GetString(nameBytes);
            var semicolon = name.IndexOf(';', StringComparison.Ordinal);
            if (semicolon >= 0)
            {
                name = name[..semicolon];
            }

            name = name.TrimEnd('.');
            var path = prefix.Length == 0 ? name : prefix + "/" + name;
            if ((flags & 2) != 0)
            {
                Walk(header, recordLba, recordSize, path, depth + 1, files, ref complete);
            }
            else if (files.Count < MaxFiles)
            {
                files.Add(new IrdDiscFile(path, recordLba, recordSize));
            }
            else
            {
                complete = false;
                return;
            }
        }
    }

    private static long LittleEndian32(byte[] data, int offset) =>
        BitConverter.ToUInt32(data, offset);
}

public enum IrdCheckState
{
    // No IRD was supplied: nothing was compared. Never a finding.
    NotChecked,

    // The IRD lists this file and the log shows RPCS3 failing to open it.
    ListedAndFailed,

    // The IRD does not list the path (an optional or generated probe): no anomaly.
    NotListed
}

public sealed record IrdPathCheck(string Path, IrdCheckState State);

// Observed filesystem anomaly vs IRD-verified mismatch. The log alone yields
// an observed failure; only a supplied IRD that lists the same file turns it
// into a verified mismatch. Without an IRD everything is NotChecked.
public static class IrdComparer
{
    public static IReadOnlyList<IrdPathCheck> Compare(IrdInfo? ird, IEnumerable<string> observedFailedDiscPaths)
    {
        ArgumentNullException.ThrowIfNull(observedFailedDiscPaths);
        var listed = ird?.DiscFiles.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. observedFailedDiscPaths
                .Select(Normalize)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(path => new IrdPathCheck(
                    path,
                    listed is null ? IrdCheckState.NotChecked : listed.Contains(path) ? IrdCheckState.ListedAndFailed : IrdCheckState.NotListed))
        ];
    }

    // "/dev_bdvd/PS3_GAME/USRDIR/x" -> "PS3_GAME/USRDIR/x" (the IRD's own layout).
    public static string Normalize(string path)
    {
        var text = path.Replace('\\', '/').TrimStart('/');
        return text.StartsWith("dev_bdvd/", StringComparison.Ordinal) ? text["dev_bdvd/".Length..] : text;
    }
}

#pragma warning restore IDE0130
