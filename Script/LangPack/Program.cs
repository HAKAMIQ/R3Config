using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace R3Config.LangPack;

internal static partial class Program
{
    private const int FormatVersion = 1;
    private const int MaxLanguageFiles = 32;
    private const int MaxEntries = 4096;
    private const int MaxKeyBytes = 1024;
    private const int MaxValueBytes = 128 * 1024;
    private const int MaxPayloadBytes = 2 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("R3LANG01");
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine(
                    "Usage: R3LangPack <source-dir> <output-dir> <generated-manifest.cs>");
                return 2;
            }

            CompileAll(
                Path.GetFullPath(args[0]),
                Path.GetFullPath(args[1]),
                Path.GetFullPath(args[2]));

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"R3LangPack failed: {ex.Message}");
            return 1;
        }
    }

    private static void CompileAll(
        string sourceDirectory,
        string outputDirectory,
        string generatedManifestPath)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Language source directory was not found: {sourceDirectory}");
        }

        var sourceFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceFiles.Length == 0 || sourceFiles.Length > MaxLanguageFiles)
        {
            throw new InvalidDataException(
                $"Expected 1-{MaxLanguageFiles} language JSON files but found {sourceFiles.Length}.");
        }

        Directory.CreateDirectory(outputDirectory);
        var manifestDirectory = Path.GetDirectoryName(generatedManifestPath);
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new InvalidDataException(
                "Generated manifest path must include a directory.");
        }
        Directory.CreateDirectory(manifestDirectory);

        var compiled = new List<CompiledLanguage>(sourceFiles.Length);
        HashSet<string>? canonicalKeys = null;

        foreach (var sourcePath in sourceFiles)
        {
            var language = Path.GetFileNameWithoutExtension(sourcePath);
            if (!LanguageCodeRegex().IsMatch(language))
            {
                throw new InvalidDataException(
                    $"Invalid language code in source file name: {Path.GetFileName(sourcePath)}");
            }

            var entries = ReadSource(sourcePath);
            var keys = new HashSet<string>(entries.Keys, StringComparer.Ordinal);

            canonicalKeys ??= keys;
            if (!canonicalKeys.SetEquals(keys))
            {
                throw new InvalidDataException(
                    $"Language key parity failed for {Path.GetFileName(sourcePath)}.");
            }

            var outputPath = Path.Combine(outputDirectory, language + ".r3lang");
            WritePack(outputPath, language, entries);

            var bytes = File.ReadAllBytes(outputPath);
            compiled.Add(new CompiledLanguage(
                language,
                Path.GetFileName(outputPath),
                Convert.ToHexString(SHA256.HashData(bytes)),
                entries.Count));
        }

        WriteManifest(generatedManifestPath, compiled);

        Console.WriteLine(
            $"R3LangPack compiled {compiled.Count} language(s), " +
            $"{compiled[0].EntryCount} keys each.");
    }

    private static SortedDictionary<string, string> ReadSource(string path)
    {
        var raw = File.ReadAllBytes(path);
        var json = StrictUtf8.GetString(raw);

        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Language source root must be an object: {Path.GetFileName(path)}");
        }

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Language value must be text: {Path.GetFileName(path)}:{property.Name}");
            }

            var value = property.Value.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(property.Name) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    $"Language key/value must not be empty: {Path.GetFileName(path)}:{property.Name}");
            }

            if (!values.TryAdd(property.Name, value))
            {
                throw new InvalidDataException(
                    $"Duplicate language key: {Path.GetFileName(path)}:{property.Name}");
            }
        }

        if (values.Count == 0 || values.Count > MaxEntries)
        {
            throw new InvalidDataException(
                $"Language source entry count is outside 1-{MaxEntries}: {Path.GetFileName(path)}");
        }

        return values;
    }

    private static void WritePack(
        string outputPath,
        string language,
        SortedDictionary<string, string> entries)
    {
        var payload = BuildPayload(entries);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                $"Language payload exceeds {MaxPayloadBytes} bytes: {language}");
        }

        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var deflate = new DeflateStream(
                       compressedStream,
                       CompressionLevel.SmallestSize,
                       leaveOpen: true))
            {
                deflate.Write(payload);
            }
            compressed = compressedStream.ToArray();
        }

        var languageBytes = StrictUtf8.GetBytes(language);
        if (languageBytes.Length is 0 or > 64)
        {
            throw new InvalidDataException(
                $"Language code is too long: {language}");
        }

        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, StrictUtf8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write((byte)languageBytes.Length);
            writer.Write(languageBytes);
            writer.Write(entries.Count);
            writer.Write(payload.Length);
            writer.Write(compressed.Length);
            writer.Write(compressed);
        }

        WriteIfChanged(outputPath, output.ToArray());
    }

    private static byte[] BuildPayload(
        SortedDictionary<string, string> entries)
    {
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, StrictUtf8, leaveOpen: true))
        {
            writer.Write(entries.Count);
            foreach (var (key, value) in entries)
            {
                var keyBytes = StrictUtf8.GetBytes(key);
                var valueBytes = StrictUtf8.GetBytes(value);

                if (keyBytes.Length is 0 or > MaxKeyBytes)
                {
                    throw new InvalidDataException(
                        $"Language key exceeds {MaxKeyBytes} UTF-8 bytes: {key}");
                }
                if (valueBytes.Length is 0 or > MaxValueBytes)
                {
                    throw new InvalidDataException(
                        $"Language value exceeds {MaxValueBytes} UTF-8 bytes: {key}");
                }

                writer.Write((ushort)keyBytes.Length);
                writer.Write(keyBytes);
                writer.Write(valueBytes.Length);
                writer.Write(valueBytes);
            }
        }

        return payload.ToArray();
    }

    private static void WriteManifest(
        string path,
        IReadOnlyList<CompiledLanguage> languages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("namespace R3Config.Lang;");
        builder.AppendLine();
        builder.AppendLine("internal static class R3LangManifest");
        builder.AppendLine("{");
        builder.AppendLine("    public static bool TryResolve(");
        builder.AppendLine("        string language,");
        builder.AppendLine("        out string fileName,");
        builder.AppendLine("        out string sha256)");
        builder.AppendLine("    {");
        builder.AppendLine("        switch (language)");
        builder.AppendLine("        {");

        foreach (var item in languages.OrderBy(item => item.Language, StringComparer.Ordinal))
        {
            builder.Append("            case \"");
            builder.Append(EscapeCSharp(item.Language));
            builder.AppendLine("\":");
            builder.Append("                fileName = \"");
            builder.Append(EscapeCSharp(item.FileName));
            builder.AppendLine("\";");
            builder.Append("                sha256 = \"");
            builder.Append(EscapeCSharp(item.Sha256));
            builder.AppendLine("\";");
            builder.AppendLine("                return true;");
        }

        builder.AppendLine("            default:");
        builder.AppendLine("                fileName = string.Empty;");
        builder.AppendLine("                sha256 = string.Empty;");
        builder.AppendLine("                return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        WriteIfChanged(path, StrictUtf8.GetBytes(builder.ToString()));
    }

    private static void WriteIfChanged(string path, byte[] bytes)
    {
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }

    private static string EscapeCSharp(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    [GeneratedRegex(
        "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LanguageCodeRegex();

    private sealed record CompiledLanguage(
        string Language,
        string FileName,
        string Sha256,
        int EntryCount);
}
