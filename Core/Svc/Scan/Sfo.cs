using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class SfoRead
{
    public static SfoInfo? TryGame(string gamePath)
    {
        return ReadFor(gamePath).Info;
    }

    public static SfoInfo? TryRead(string sfoPath)
    {
        return Read(sfoPath).Info;
    }

    internal static SfoProbe ReadFor(
        string gamePath)
    {
        var sfoPath = FindSfo(gamePath);
        if (sfoPath is null)
        {
            return SfoProbe.Failure(
                ScanCode.SfoMiss,
                gamePath ?? string.Empty,
                "No PARAM.SFO was found at the bounded game-path locations.");
        }

        return Read(sfoPath);
    }

    internal static SfoProbe Read(string sfoPath)
    {
        if (string.IsNullOrWhiteSpace(sfoPath) ||
            !File.Exists(sfoPath))
        {
            return SfoProbe.Failure(
                ScanCode.SfoMiss,
                sfoPath ?? string.Empty,
                "PARAM.SFO was not found.");
        }

        try
        {
            if (new FileInfo(sfoPath).Length >
                R3Limits.Bytes)
            {
                return SfoProbe.Failure(
                    ScanCode.SfoBig,
                    sfoPath,
                    $"PARAM.SFO exceeds the {R3Limits.Bytes}-byte input limit.");
            }

            var values = ReadVal(sfoPath);
            values.TryGetValue("TITLE", out var title);
            values.TryGetValue("TITLE_ID", out var titleId);
            values.TryGetValue("APP_VER", out var appVersion);
            values.TryGetValue("VERSION", out var discVersion);

            return SfoProbe.Success(
                new SfoInfo(
                    string.IsNullOrWhiteSpace(title) ? null : title,
                    string.IsNullOrWhiteSpace(titleId) ? null : titleId,
                    string.IsNullOrWhiteSpace(appVersion) ? null : appVersion,
                    string.IsNullOrWhiteSpace(discVersion) ? null : discVersion));
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                InvalidDataException or
                IOException or
                UnauthorizedAccessException or
                OverflowException or
                DecoderFallbackException)
        {
            return SfoProbe.Failure(
                IsToo(sfoPath)
                    ? ScanCode.SfoBig
                    : ScanCode.SfoBad,
                sfoPath,
                ex.Message);
        }
    }

    private static string? FindSfo(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) ||
            !Directory.Exists(gamePath))
        {
            return null;
        }

        var direct = Path.Combine(gamePath, "PARAM.SFO");

        if (File.Exists(direct))
        {
            return direct;
        }

        var ps3Game = Path.Combine(
            gamePath,
            "PS3_GAME",
            "PARAM.SFO");

        return File.Exists(ps3Game)
            ? ps3Game
            : null;
    }

    private static Dictionary<string, string> ReadVal(string sfoPath)
    {
        var file = new FileInfo(sfoPath);
        if (file.Length > R3Limits.Bytes)
        {
            throw new InvalidDataException(
                $"PARAM.SFO exceeds the {R3Limits.Bytes}-byte input limit.");
        }

        using var stream = File.OpenRead(sfoPath);
        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: false);

        if (stream.Length < 20)
        {
            throw new InvalidDataException(
                "PARAM.SFO header is truncated.");
        }

        if (stream.Length > R3Limits.Bytes)
        {
            throw new InvalidDataException(
                "PARAM.SFO grew beyond the supported input limit while it was being read.");
        }

        var magic = reader.ReadUInt32();

        if (magic != 0x46535000)
        {
            throw new InvalidDataException(
                "Invalid PARAM.SFO magic.");
        }

        _ = reader.ReadUInt32();

        var keyTableOffset = reader.ReadUInt32();
        var dataTableOffset = reader.ReadUInt32();
        var entryCount = reader.ReadUInt32();

        if (entryCount > R3Limits.Entries)
        {
            throw new InvalidDataException(
                "PARAM.SFO entry count is unreasonable.");
        }

        var indexTableEnd = checked(
            20L + checked((long)entryCount * 16L));

        if (keyTableOffset < indexTableEnd ||
            dataTableOffset < keyTableOffset ||
            keyTableOffset > stream.Length ||
            dataTableOffset > stream.Length ||
            indexTableEnd > stream.Length)
        {
            throw new InvalidDataException(
                "PARAM.SFO table offsets are out of range.");
        }

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entryCount; index++)
        {
            stream.Position = 20L + index * 16L;

            var keyOffset = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            var dataLength = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            if (dataLength > R3Limits.SfoValBy ||
                dataLength > int.MaxValue)
            {
                throw new InvalidDataException(
                    "PARAM.SFO value length exceeds the supported limit.");
            }

            var absoluteKeyOffset = checked(
                (long)keyTableOffset + keyOffset);

            var absoluteDataOffset = checked(
                (long)dataTableOffset + dataOffset);

            var absoluteDataEnd = checked(
                absoluteDataOffset + dataLength);

            if (absoluteKeyOffset < keyTableOffset ||
                absoluteKeyOffset >= dataTableOffset ||
                absoluteDataOffset < dataTableOffset ||
                absoluteDataEnd < absoluteDataOffset ||
                absoluteDataEnd > stream.Length)
            {
                throw new InvalidDataException(
                    "PARAM.SFO entry offsets are out of range.");
            }

            var key = ReadNull(
                stream,
                absoluteKeyOffset,
                dataTableOffset);

            var value = Read2(
                stream,
                absoluteDataOffset,
                checked((int)dataLength));

            values[key] = value;
        }

        return values;
    }

    private static string ReadNull(
        Stream stream,
        long offset,
        long limit)
    {
        stream.Position = offset;
        List<byte> bytes = [];

        while (stream.Position < limit)
        {
            var value = stream.ReadByte();

            if (value < 0)
            {
                throw new EndOfStreamException(
                    "PARAM.SFO key data is truncated.");
            }

            if (value == 0)
            {
                byte[] result = [.. bytes];
                return Encoding.UTF8.GetString(result);
            }

            if (bytes.Count >= R3Limits.SfoKeyBy)
            {
                throw new InvalidDataException(
                    "PARAM.SFO key length exceeds the supported limit.");
            }

            bytes.Add((byte)value);
        }

        throw new InvalidDataException(
            "PARAM.SFO key is not null-terminated.");
    }

    private static string Read2(
        Stream stream,
        long offset,
        int length)
    {
        stream.Position = offset;

        var bytes = new byte[length];
        stream.ReadExactly(bytes);

        var end = Array.IndexOf(bytes, (byte)0);

        if (end < 0)
        {
            end = bytes.Length;
        }

        return Encoding.UTF8
            .GetString(bytes, 0, end)
            .Trim();
    }

    private static bool IsToo(string path)
    {
        try
        {
            return File.Exists(path) &&
                   new FileInfo(path).Length >
                   R3Limits.Bytes;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record SfoProbe(
    SfoInfo? Info,
    GameScanDiagnostic? Diag)
{
    public static SfoProbe Success(SfoInfo info)
    {
        return new SfoProbe(info, null);
    }

    public static SfoProbe Failure(
        ScanCode code,
        string sourcePath,
        string technicalMessage)
    {
        return new SfoProbe(
            null,
            new GameScanDiagnostic(
                code,
                ScanSev.Warning,
                ScanSrc.ParamSfo,
                sourcePath,
                technicalMessage));
    }
}

#pragma warning restore IDE0130
