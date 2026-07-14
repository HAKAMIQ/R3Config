using System.Text;

namespace R3Config.Svc;

internal static class Rpcs3BoundedInput
{
    private const int CopyBufferBytes = 80 * 1024;

    public static string ReadUtf8TextFile(
        string path,
        long maxBytes,
        string sourceName)
    {
        var file = new FileInfo(path);
        EnsureLength(file.Length, maxBytes, sourceName);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return ReadUtf8Text(
            stream,
            maxBytes,
            sourceName);
    }

    public static string ReadUtf8Text(
        Stream stream,
        long maxBytes,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ValidateLimit(maxBytes);

        if (stream.CanSeek)
        {
            var remaining = checked(stream.Length - stream.Position);
            EnsureLength(remaining, maxBytes, sourceName);
        }

        using var boundedBytes = new MemoryStream();
        var buffer = new byte[CopyBufferBytes];
        long total = 0;

        while (true)
        {
            var remainingAllowance = maxBytes - total;
            var requested = (int)Math.Min(
                buffer.Length,
                remainingAllowance + 1);
            var read = stream.Read(buffer, 0, requested);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            EnsureLength(total, maxBytes, sourceName);
            boundedBytes.Write(buffer, 0, read);
        }

        boundedBytes.Position = 0;
        using var reader = new StreamReader(
            boundedBytes,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4_096,
            leaveOpen: false);
        return reader.ReadToEnd();
    }

    public static void EnsureTextLength(
        string text,
        long maxBytes,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateLimit(maxBytes);
        EnsureLength(
            Encoding.UTF8.GetByteCount(text),
            maxBytes,
            sourceName);
    }

    private static void EnsureLength(
        long length,
        long maxBytes,
        string sourceName)
    {
        ValidateLimit(maxBytes);

        if (length < 0 || length > maxBytes)
        {
            throw new InvalidDataException(
                $"{sourceName} exceeds the {maxBytes}-byte input limit.");
        }
    }

    private static void ValidateLimit(long maxBytes)
    {
        if (maxBytes <= 0 || maxBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes));
        }
    }
}
