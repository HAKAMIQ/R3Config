using System.Text;
using System.Collections.ObjectModel;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

internal sealed class BoundedDiagnosticList<T>(int maxCount) :
    Collection<T>
{
    protected override void InsertItem(
        int index,
        T item)
    {
        if (Count < maxCount)
        {
            base.InsertItem(index, item);
        }
    }
}

public static class Rpcs3BoundedInput
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

    public static YamlStream LoadYaml(
        string text,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateYaml(text, sourceName);

        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        if (yaml.Documents.Count == 0 ||
            yaml.Documents.Count > Rpcs3InputLimits.YamlDocuments)
        {
            throw new InvalidDataException(
                $"{sourceName} contains an invalid YAML document count.");
        }

        return yaml;
    }

    public static void ValidateYaml(
        string text,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parser = new Parser(new StringReader(text));
        var depth = 0;
        var nodes = 0;
        var aliases = 0;
        var mappings = 0;
        var sequences = 0;
        var documents = 0;

        while (parser.MoveNext())
        {
            switch (parser.Current)
            {
                case DocumentStart:
                    documents++;
                    EnsureYamlLimit(
                        documents,
                        Rpcs3InputLimits.YamlDocuments,
                        sourceName,
                        "documents");
                    break;

                case MappingStart:
                    nodes++;
                    mappings++;
                    depth++;
                    EnsureYamlLimit(
                        mappings,
                        Rpcs3InputLimits.YamlMappings,
                        sourceName,
                        "mappings");
                    EnsureYamlDepth(depth, sourceName);
                    break;

                case SequenceStart:
                    nodes++;
                    sequences++;
                    depth++;
                    EnsureYamlLimit(
                        sequences,
                        Rpcs3InputLimits.YamlSequences,
                        sourceName,
                        "sequences");
                    EnsureYamlDepth(depth, sourceName);
                    break;

                case MappingEnd or SequenceEnd:
                    depth--;
                    break;

                case AnchorAlias:
                    nodes++;
                    aliases++;
                    EnsureYamlLimit(
                        aliases,
                        Rpcs3InputLimits.YamlAliases,
                        sourceName,
                        "aliases");
                    break;

                case Scalar:
                    nodes++;
                    break;
            }

            EnsureYamlLimit(
                nodes,
                Rpcs3InputLimits.YamlNodes,
                sourceName,
                "nodes");
        }
    }

    private static void EnsureYamlDepth(
        int depth,
        string sourceName)
    {
        if (depth > Rpcs3InputLimits.YamlDepth)
        {
            throw new InvalidDataException(
                $"{sourceName} exceeds the YAML depth limit of {Rpcs3InputLimits.YamlDepth}.");
        }
    }

    private static void EnsureYamlLimit(
        int count,
        int limit,
        string sourceName,
        string kind)
    {
        if (count > limit)
        {
            throw new InvalidDataException(
                $"{sourceName} exceeds the YAML {kind} limit of {limit}.");
        }
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
