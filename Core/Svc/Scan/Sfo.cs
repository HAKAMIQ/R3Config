using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class ParamSfoReader
{
    public static ParamSfoInfo? TryReadForGamePath(string gamePath)
    {
        var sfoPath = FindParamSfo(gamePath);
        return sfoPath is null ? null : TryRead(sfoPath);
    }

    public static ParamSfoInfo? TryRead(string sfoPath)
    {
        try
        {
            var values = ReadValues(sfoPath);
            values.TryGetValue("TITLE", out var title);
            values.TryGetValue("TITLE_ID", out var titleId);
            values.TryGetValue("APP_VER", out var appVersion);

            return new ParamSfoInfo(
                string.IsNullOrWhiteSpace(title) ? null : title,
                string.IsNullOrWhiteSpace(titleId) ? null : titleId,
                string.IsNullOrWhiteSpace(appVersion) ? null : appVersion);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindParamSfo(string gamePath)
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

    private static Dictionary<string, string> ReadValues(string sfoPath)
    {
        var file = new FileInfo(sfoPath);
        if (file.Length > Rpcs3InputLimits.ParamSfoFileBytes)
        {
            throw new InvalidDataException(
                $"PARAM.SFO exceeds the {Rpcs3InputLimits.ParamSfoFileBytes}-byte input limit.");
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

        if (stream.Length > Rpcs3InputLimits.ParamSfoFileBytes)
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

        if (entryCount > Rpcs3InputLimits.ParamSfoEntries)
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

            if (dataLength > Rpcs3InputLimits.ParamSfoValueBytes ||
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

            var key = ReadNullTerminatedString(
                stream,
                absoluteKeyOffset,
                dataTableOffset);

            var value = ReadString(
                stream,
                absoluteDataOffset,
                checked((int)dataLength));

            values[key] = value;
        }

        return values;
    }

    private static string ReadNullTerminatedString(
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

            if (bytes.Count >= Rpcs3InputLimits.ParamSfoKeyBytes)
            {
                throw new InvalidDataException(
                    "PARAM.SFO key length exceeds the supported limit.");
            }

            bytes.Add((byte)value);
        }

        throw new InvalidDataException(
            "PARAM.SFO key is not null-terminated.");
    }

    private static string ReadString(
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
}

#pragma warning restore IDE0130
