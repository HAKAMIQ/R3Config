using System.Text;
using System.Collections.ObjectModel;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

internal sealed class DiagList<T>(int maxCount) :
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

public static class R3Bound
{
    private const int Bytes = 80 * 1024;

    public static string ReadUtf8(
        string path,
        long maxBytes,
        string sourceName)
    {
        var file = new FileInfo(path);
        Check4(file.Length, maxBytes, sourceName);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return ReadUtf2(
            stream,
            maxBytes,
            sourceName);
    }

    public static string ReadUtf2(
        Stream stream,
        long maxBytes,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        VldLimit(maxBytes);

        if (stream.CanSeek)
        {
            var remaining = checked(stream.Length - stream.Position);
            Check4(remaining, maxBytes, sourceName);
        }

        using var boundedBytes = new MemoryStream();
        var buffer = new byte[Bytes];
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
            Check4(total, maxBytes, sourceName);
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

    public static void Check(
        string text,
        long maxBytes,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        VldLimit(maxBytes);
        Check4(
            Encoding.UTF8.GetByteCount(text),
            maxBytes,
            sourceName);
    }

    public static YamlStream LoadYaml(
        string text,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        VldYaml(text, sourceName);

        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        if (yaml.Documents.Count == 0 ||
            yaml.Documents.Count > R3Limits.YamlDoc)
        {
            throw new InvalidDataException(
                $"{sourceName} contains an invalid YAML document count.");
        }

        return yaml;
    }

    public static void VldYaml(
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
                    Check3(
                        documents,
                        R3Limits.YamlDoc,
                        sourceName,
                        "documents");
                    break;

                case MappingStart:
                    nodes++;
                    mappings++;
                    depth++;
                    Check3(
                        mappings,
                        R3Limits.YamlMap,
                        sourceName,
                        "mappings");
                    Check2(depth, sourceName);
                    break;

                case SequenceStart:
                    nodes++;
                    sequences++;
                    depth++;
                    Check3(
                        sequences,
                        R3Limits.YamlSeq,
                        sourceName,
                        "sequences");
                    Check2(depth, sourceName);
                    break;

                case MappingEnd or SequenceEnd:
                    depth--;
                    break;

                case AnchorAlias:
                    nodes++;
                    aliases++;
                    Check3(
                        aliases,
                        R3Limits.Aliases,
                        sourceName,
                        "aliases");
                    break;

                case Scalar:
                    nodes++;
                    break;
            }

            Check3(
                nodes,
                R3Limits.Nodes,
                sourceName,
                "nodes");
        }
    }

    private static void Check2(
        int depth,
        string sourceName)
    {
        if (depth > R3Limits.Depth)
        {
            throw new InvalidDataException(
                $"{sourceName} exceeds the YAML depth limit of {R3Limits.Depth}.");
        }
    }

    private static void Check3(
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

    private static void Check4(
        long length,
        long maxBytes,
        string sourceName)
    {
        VldLimit(maxBytes);

        if (length < 0 || length > maxBytes)
        {
            throw new InvalidDataException(
                $"{sourceName} exceeds the {maxBytes}-byte input limit.");
        }
    }

    private static void VldLimit(long maxBytes)
    {
        if (maxBytes <= 0 || maxBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes));
        }
    }
}
