using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using R3Config.Svc;

namespace R3Config.Lang;

internal static class R3LangReader
{
    private const int FormatVersion = 1;
    private const long MaxPackBytes = 1024L * 1024;
    private const int MaxPayloadBytes = 2 * 1024 * 1024;
    private const int MaxEntries = 4096;
    private const int MaxKeyBytes = 1024;
    private const int MaxValueBytes = 128 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("R3LANG01");
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static IReadOnlyDictionary<string, string> Load(
        string path,
        string expectedLanguage,
        string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        var pack = R3Bound.ReadBytes(
            path,
            MaxPackBytes,
            $"R3Config UI language {expectedLanguage}");

        var actualSha256 = Convert.ToHexString(SHA256.HashData(pack));
        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"R3Config UI language integrity check failed: {expectedLanguage}");
        }

        try
        {
            return Parse(pack, expectedLanguage);
        }
        catch (Exception ex) when (
            ex is EndOfStreamException or
            DecoderFallbackException or
            IOException or
            InvalidDataException)
        {
            throw new InvalidDataException(
                $"R3Config UI language package is invalid: {expectedLanguage}",
                ex);
        }
    }

    private static Dictionary<string, string> Parse(
        byte[] pack,
        string expectedLanguage)
    {
        using var stream = new MemoryStream(pack, writable: false);
        using var reader = new BinaryReader(stream, StrictUtf8, leaveOpen: true);

        var magic = ReadExact(reader, Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException(
                "R3Config UI language package magic is invalid.");
        }

        if (reader.ReadInt32() != FormatVersion)
        {
            throw new InvalidDataException(
                "R3Config UI language package version is unsupported.");
        }

        var languageLength = reader.ReadByte();
        if (languageLength is 0 or > 64)
        {
            throw new InvalidDataException(
                "R3Config UI language code length is invalid.");
        }

        var language = StrictUtf8.GetString(
            ReadExact(reader, languageLength));
        if (!string.Equals(
                language,
                expectedLanguage,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"R3Config UI language package identity mismatch: {language}");
        }

        var entryCount = reader.ReadInt32();
        if (entryCount is <= 0 or > MaxEntries)
        {
            throw new InvalidDataException(
                "R3Config UI language entry count is invalid.");
        }

        var payloadLength = reader.ReadInt32();
        if (payloadLength is <= 0 or > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                "R3Config UI language payload length is invalid.");
        }

        var compressedLength = reader.ReadInt32();
        if (compressedLength <= 0 ||
            compressedLength != stream.Length - stream.Position)
        {
            throw new InvalidDataException(
                "R3Config UI language compressed length is invalid.");
        }

        var payload = InflateBounded(
            stream,
            payloadLength,
            MaxPayloadBytes);

        using var payloadStream = new MemoryStream(
            payload,
            writable: false);
        using var payloadReader = new BinaryReader(
            payloadStream,
            StrictUtf8,
            leaveOpen: true);

        if (payloadReader.ReadInt32() != entryCount)
        {
            throw new InvalidDataException(
                "R3Config UI language payload entry count does not match its header.");
        }

        var loaded = new Dictionary<string, string>(
            entryCount,
            StringComparer.Ordinal);

        for (var index = 0; index < entryCount; index++)
        {
            var keyLength = payloadReader.ReadUInt16();
            if (keyLength is 0 or > MaxKeyBytes)
            {
                throw new InvalidDataException(
                    "R3Config UI language key length is invalid.");
            }

            var key = StrictUtf8.GetString(
                ReadExact(payloadReader, keyLength));

            var valueLength = payloadReader.ReadInt32();
            if (valueLength is <= 0 or > MaxValueBytes)
            {
                throw new InvalidDataException(
                    $"R3Config UI language value length is invalid: {key}");
            }

            var value = StrictUtf8.GetString(
                ReadExact(payloadReader, valueLength));

            if (!loaded.TryAdd(key, value))
            {
                throw new InvalidDataException(
                    $"R3Config UI language contains a duplicate key: {key}");
            }
        }

        if (payloadStream.Position != payloadStream.Length)
        {
            throw new InvalidDataException(
                "R3Config UI language payload contains trailing data.");
        }

        return loaded;
    }

    private static byte[] InflateBounded(
        Stream compressed,
        int expectedLength,
        int maxLength)
    {
        using var deflate = new DeflateStream(
            compressed,
            CompressionMode.Decompress,
            leaveOpen: true);
        using var output = new MemoryStream(expectedLength);

        var buffer = new byte[16 * 1024];
        var total = 0;
        while (true)
        {
            var read = deflate.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > expectedLength || total > maxLength)
            {
                throw new InvalidDataException(
                    "R3Config UI language payload exceeds its declared bound.");
            }

            output.Write(buffer, 0, read);
        }

        if (total != expectedLength)
        {
            throw new InvalidDataException(
                "R3Config UI language payload length does not match its header.");
        }

        return output.ToArray();
    }

    private static byte[] ReadExact(
        BinaryReader reader,
        int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException();
        }

        return bytes;
    }
}
