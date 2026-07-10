using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using R3Config.Mdl;

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
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return null;
        }

        var direct = Path.Combine(gamePath, "PARAM.SFO");
        if (File.Exists(direct))
        {
            return direct;
        }

        var ps3Game = Path.Combine(gamePath, "PS3_GAME", "PARAM.SFO");
        if (File.Exists(ps3Game))
        {
            return ps3Game;
        }

        return null;
    }

    private static Dictionary<string, string> ReadValues(string sfoPath)
    {
        using var stream = File.OpenRead(sfoPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (stream.Length < 20)
        {
            throw new InvalidDataException("PARAM.SFO header is truncated.");
        }

        var magic = reader.ReadUInt32();
        if (magic != 0x46535000)
        {
            throw new InvalidDataException("Invalid PARAM.SFO magic.");
        }

        _ = reader.ReadUInt32();
        var keyTableOffset = reader.ReadUInt32();
        var dataTableOffset = reader.ReadUInt32();
        var entryCount = reader.ReadUInt32();

        if (entryCount > 4096)
        {
            throw new InvalidDataException("PARAM.SFO entry count is unreasonable.");
        }

        var indexTableEnd = 20L + (entryCount * 16L);
        if (keyTableOffset < indexTableEnd ||
            dataTableOffset < keyTableOffset ||
            keyTableOffset > stream.Length ||
            dataTableOffset > stream.Length ||
            indexTableEnd > stream.Length)
        {
            throw new InvalidDataException("PARAM.SFO table offsets are out of range.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < entryCount; i++)
        {
            stream.Position = 20 + (i * 16);
            var keyOffset = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            var dataLength = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            var absoluteKeyOffset = (long)keyTableOffset + keyOffset;
            var absoluteDataOffset = (long)dataTableOffset + dataOffset;
            var absoluteDataEnd = absoluteDataOffset + dataLength;

            if (absoluteKeyOffset < keyTableOffset ||
                absoluteKeyOffset >= dataTableOffset ||
                absoluteDataOffset < dataTableOffset ||
                absoluteDataEnd > stream.Length ||
                dataLength > int.MaxValue)
            {
                throw new InvalidDataException("PARAM.SFO entry offsets are out of range.");
            }

            var key = ReadNullTerminatedString(stream, absoluteKeyOffset, dataTableOffset);
            var value = ReadString(stream, absoluteDataOffset, dataLength);
            values[key] = value;
        }

        return values;
    }

    private static string ReadNullTerminatedString(Stream stream, long offset, long limit)
    {
        stream.Position = offset;
        var bytes = new List<byte>();
        while (stream.Position < limit)
        {
            var value = stream.ReadByte();
            if (value <= 0)
            {
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            bytes.Add((byte)value);
        }

        throw new InvalidDataException("PARAM.SFO key is not null-terminated.");
    }

    private static string ReadString(Stream stream, long offset, uint length)
    {
        stream.Position = offset;
        var bytes = new byte[length];
        var read = stream.Read(bytes, 0, bytes.Length);
        var end = Array.IndexOf(bytes, (byte)0, 0, read);
        if (end < 0)
        {
            end = read;
        }

        return Encoding.UTF8.GetString(bytes, 0, end).Trim();
    }
}
