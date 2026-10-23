using System.IO.Compression;
using System.Text.Json;
using R3Config.Mdl;

namespace R3Config.Svc;

// Optional row provenance for raw source archives. Serial/version authority
// remains the original filename parsed by NclParser, never a manifest override.
internal sealed class NclCorpusManifest
{
    private const int MaxManifestBytes = 4 * 1024 * 1024;
    private readonly Dictionary<string, Source> _sources = new(StringComparer.Ordinal);

    internal sealed record Source(
        int RowOrder, string Title, string[] Serials, string Version,
        string MatchMode, string OriginalFilename, string SourceSha256,
        string EntryPath);

    private sealed record Manifest(int SchemaVersion, Source[] Sources);

    public static NclCorpusManifest? Read(ZipArchive archive, ZipArchiveEntry[] entries)
    {
        var metadata = archive.Entries.Where(entry =>
            entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (metadata.Length == 0)
        {
            return null;
        }
        if (metadata.Length != 1 || metadata[0].Length > MaxManifestBytes)
        {
            throw new InvalidDataException("NCL manifest is duplicated or exceeds its size limit.");
        }

        Manifest manifest;
        try
        {
            using var stream = metadata[0].Open();
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (memory.Length + read > MaxManifestBytes)
                {
                    throw new InvalidDataException("NCL manifest exceeds its expanded size limit.");
                }
                memory.Write(buffer, 0, read);
            }
            manifest = JsonSerializer.Deserialize<Manifest>(memory.ToArray())
                ?? throw new InvalidDataException("NCL manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("NCL manifest is malformed.", ex);
        }

        if (manifest.SchemaVersion != 1 || manifest.Sources is null ||
            manifest.Sources.Length != entries.Length)
        {
            throw new InvalidDataException("NCL manifest schema or source count is invalid.");
        }
        var paths = entries.Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        var rows = new HashSet<int>();
        var result = new NclCorpusManifest();
        foreach (var source in manifest.Sources)
        {
            if (source is null || source.RowOrder <= 0 || !rows.Add(source.RowOrder) ||
                string.IsNullOrWhiteSpace(source.EntryPath) || !paths.Contains(source.EntryPath) ||
                string.IsNullOrWhiteSpace(source.Title) || source.Serials is null ||
                source.Version is null || string.IsNullOrWhiteSpace(source.OriginalFilename) ||
                !string.Equals(Path.GetFileName(source.EntryPath), source.OriginalFilename, StringComparison.Ordinal) ||
                source.SourceSha256 is not { Length: 64 } || !source.SourceSha256.All(Uri.IsHexDigit) ||
                !result._sources.TryAdd(source.EntryPath, source))
            {
                throw new InvalidDataException("NCL manifest source identity is invalid or duplicated.");
            }
            var identity = NclParser.ParseIdentity(source.OriginalFilename);
            if (!identity.Serials.ToHashSet(StringComparer.Ordinal).SetEquals(source.Serials) ||
                source.Serials.Length != identity.Serials.Count ||
                source.MatchMode != (identity.Serials.Count == 0 ? "TitleOnly"
                    : identity.HasExactTarget ? "ExactSerialVersion" : "Unresolved"))
            {
                throw new InvalidDataException("NCL manifest contradicts its original filename identity.");
            }
        }
        return result;
    }

    public Source this[string path] => _sources[path];

    public IEnumerable<string> FindTitle(string? title, string version)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }
        return _sources.Values.Where(source => source.MatchMode == "TitleOnly" &&
                source.Title.Trim().Equals(title.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (source.Version.Length == 0 || PatVer.Equals(source.Version, version)))
            .Select(source => source.EntryPath);
    }

    public void VerifyHash(string path, string sha256)
    {
        if (!_sources[path].SourceSha256.Equals(sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("NCL source bytes differ from the row manifest fingerprint.");
        }
    }
}
