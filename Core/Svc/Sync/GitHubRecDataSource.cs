using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record GitHubRecDataFetch(
    RecSyncSourceStatus Status,
    RecSyncGitHubData? Data,
    string Detail,
    string? ManifestSha256 = null);

// Reads the data bundle published by the HAKAMIQ/rpcs3 master branch. The
// manifest is the only source of linked-file hashes and lengths; file paths
// are restricted to the two files emitted by RecDataExport.
public sealed class GitHubRecDataSource(RecSyncHttp http)
{
    public const string ManifestUrl = "https://raw.githubusercontent.com/HAKAMIQ/rpcs3/master/r3config-data/manifest.json";
    public const long MaxManifestBytes = 64 * 1024;
    public const long MaxRecommendationsBytes = 8 * 1024 * 1024;
    public const long MaxNotesBytes = 1024 * 1024;
    private const int MaxNotes = 20_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 32, PropertyNameCaseInsensitive = true };

    public async Task<GitHubRecDataFetch> FetchAsync(
        string? acceptedManifestSha256,
        string? acceptedVersion,
        CancellationToken cancellationToken)
    {
        var manifestResult = await http.GetAsync(new Uri(ManifestUrl), MaxManifestBytes, "application/json", null, null, cancellationToken).ConfigureAwait(false);
        if (manifestResult.Status != RecSyncHttpStatus.Ok)
        {
            if (manifestResult.HttpStatus == 404)
            {
                return new GitHubRecDataFetch(RecSyncSourceStatus.NotPublished, null, "GitHub recommendation data has not been published yet.");
            }

            return Failure(manifestResult, "GitHub manifest request failed.");
        }

        try
        {
            var manifest = ParseManifest(manifestResult.Body);
            var manifestHash = Convert.ToHexString(SHA256.HashData(manifestResult.Body));
            if (string.Equals(manifestHash, acceptedManifestSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new GitHubRecDataFetch(RecSyncSourceStatus.Unchanged, null, "No changes.", manifestHash);
            }

            if (acceptedVersion is not null && string.CompareOrdinal(manifest.Version, acceptedVersion) <= 0)
            {
                return new GitHubRecDataFetch(RecSyncSourceStatus.Invalid, null, "The GitHub data version is not newer than the accepted version.");
            }

            var recommendations = await DownloadAsync(manifest.Files["recommendations.json"], MaxRecommendationsBytes, cancellationToken).ConfigureAwait(false);
            if (recommendations.Status is not null)
            {
                return recommendations.Status == RecSyncSourceStatus.NotPublished
                    ? new GitHubRecDataFetch(RecSyncSourceStatus.Invalid, null, "A manifest file is missing.")
                    : new GitHubRecDataFetch(recommendations.Status.Value, null, recommendations.Detail);
            }

            var notes = await DownloadAsync(manifest.Files["notes-ar.json"], MaxNotesBytes, cancellationToken).ConfigureAwait(false);
            if (notes.Status is not null)
            {
                return notes.Status == RecSyncSourceStatus.NotPublished
                    ? new GitHubRecDataFetch(RecSyncSourceStatus.Invalid, null, "A manifest file is missing.")
                    : new GitHubRecDataFetch(notes.Status.Value, null, notes.Detail);
            }

            var generated = DateTimeOffset.Parse(manifest.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var recs = ParseRecommendations(recommendations.Body!, manifest.Version);
            var arabicNotes = ParseNotes(notes.Body!, manifest.Version);
            var data = new RecSyncGitHubData(manifest.Version, generated, manifestHash, recs, arabicNotes);
            return new GitHubRecDataFetch(RecSyncSourceStatus.Updated, data, "Updated.", manifestHash);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return new GitHubRecDataFetch(RecSyncSourceStatus.Invalid, null, $"GitHub data was rejected: {ex.Message}");
        }
    }

    private async Task<DownloadedFile> DownloadAsync(ManifestFile file, long maxBytes, CancellationToken cancellationToken)
    {
        if (file.SizeBytes <= 0 || file.SizeBytes > maxBytes)
        {
            return new DownloadedFile(RecSyncSourceStatus.Invalid, null, "A manifest file size is outside the allowed range.");
        }

        var uri = new Uri(ManifestUrl[..(ManifestUrl.LastIndexOf('/') + 1)] + Uri.EscapeDataString(file.Path));
        var result = await http.GetAsync(uri, maxBytes, "application/json", null, null, cancellationToken).ConfigureAwait(false);
        if (result.Status != RecSyncHttpStatus.Ok)
        {
            var status = result.HttpStatus == 404 ? RecSyncSourceStatus.NotPublished : Map(result.Status);
            return new DownloadedFile(status, null, result.Detail);
        }

        if (result.Body.LongLength != file.SizeBytes ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(result.Body)), file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new DownloadedFile(RecSyncSourceStatus.Invalid, null, "A GitHub data file failed its size or SHA-256 check.");
        }

        return new DownloadedFile(null, result.Body, string.Empty);
    }

    private static Manifest ParseManifest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
        var root = document.RootElement;
        CheckUnique(root, "manifest");
        if (root.ValueKind != JsonValueKind.Object ||
            !Number(root, "schema", out var schema) || schema != 1 ||
            !String(root, "repository", out var repository) || repository != "HAKAMIQ/rpcs3" ||
            !String(root, "version", out var version) || !Regex.IsMatch(version, "^[0-9]{14}$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(GetString(root, "generatedAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _) ||
            !root.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array || filesElement.GetArrayLength() != 2)
        {
            throw new InvalidDataException("The manifest schema, repository, version, generation time, or file list is invalid.");
        }

        if (!DateTimeOffset.TryParseExact(version, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
        {
            throw new InvalidDataException("The data version is not a valid UTC timestamp.");
        }

        var entries = new Dictionary<string, ManifestFile>(StringComparer.Ordinal);
        foreach (var item in filesElement.EnumerateArray())
        {
            CheckUnique(item, "manifest file");
            if (item.ValueKind != JsonValueKind.Object ||
                !String(item, "path", out var path) ||
                !Number(item, "sizeBytes", out var size) ||
                !String(item, "sha256", out var hash) ||
                !Regex.IsMatch(hash, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant) ||
                path is not ("recommendations.json" or "notes-ar.json") ||
                !entries.TryAdd(path, new ManifestFile(path, size, hash)))
            {
                throw new InvalidDataException("The manifest contains an invalid or duplicate file entry.");
            }
        }

        if (entries.Count != 2)
        {
            throw new InvalidDataException("The manifest must contain both exported data files.");
        }

        return new Manifest(version, GetString(root, "generatedAt")!, entries);
    }

    private static IReadOnlyList<WikiRec> ParseRecommendations(byte[] bytes, string version)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        CheckUnique(document.RootElement, "recommendations");
        var root = JsonNode.Parse(bytes)?.AsObject() ?? throw new InvalidDataException("Recommendations data is not an object.");
        if (!root.TryGetPropertyValue("schema", out var schema) || schema?.GetValue<int>() != 1 ||
            !root.TryGetPropertyValue("dataVersion", out var dataVersion) || dataVersion?.GetValue<string>() != version ||
            !root.TryGetPropertyValue("recommendations", out var recommendations) || recommendations is not JsonArray)
        {
            throw new InvalidDataException("Recommendations schema or version does not match the manifest.");
        }

        var catalog = new JsonObject
        {
            ["DataSource"] = "HAKAMIQ/rpcs3",
            ["DataVersion"] = version,
            ["GeneratedAt"] = root["generatedAt"]?.DeepClone(),
            ["SourceHash"] = Convert.ToHexString(SHA256.HashData(bytes)),
            ["Recommendations"] = recommendations.DeepClone()
        };
        using var catalogStream = new MemoryStream(Encoding.UTF8.GetBytes(catalog.ToJsonString()));
        var loaded = RecSvc.Load(catalogStream);
        if (loaded.Count == 0 || loaded.Count > 20_000)
        {
            throw new InvalidDataException("Recommendations data has an invalid number of games.");
        }

        return [.. loaded.Rec.OrderBy(item => item.Serial, StringComparer.Ordinal)];
    }

    private static List<RecSyncGitHubNote> ParseNotes(byte[] bytes, string version)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        CheckUnique(document.RootElement, "Arabic notes");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !Number(root, "schema", out var schema) || schema != 1 ||
            !String(root, "dataVersion", out var actualVersion) || actualVersion != version ||
            !root.TryGetProperty("notes", out var notes) || notes.ValueKind != JsonValueKind.Array || notes.GetArrayLength() > MaxNotes)
        {
            throw new InvalidDataException("Arabic notes schema or version does not match the manifest.");
        }

        var output = new List<RecSyncGitHubNote>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var note in notes.EnumerateArray())
        {
            CheckUnique(note, "Arabic note");
            if (note.ValueKind != JsonValueKind.Object || !String(note, "sourceEnglish", out var source) ||
                !String(note, "noteArabic", out var arabic) || source.Length is 0 or > 4096 || arabic.Length is 0 or > 4096 ||
                source.Any(char.IsControl) || arabic.Any(char.IsControl) || !arabic.Any(ch => ch is >= '\u0600' and <= '\u06ff') || !keys.Add(source))
            {
                throw new InvalidDataException("Arabic notes contain an invalid, duplicate, or untranslated entry.");
            }

            output.Add(new RecSyncGitHubNote(source, arabic));
        }

        return output;
    }

    private static GitHubRecDataFetch Failure(RecSyncHttpResult result, string prefix) =>
        new(Map(result.Status), null, $"{prefix} {result.Detail}");

    private static RecSyncSourceStatus Map(RecSyncHttpStatus status) => status switch
    {
        RecSyncHttpStatus.Blocked => RecSyncSourceStatus.Blocked,
        RecSyncHttpStatus.RateLimited => RecSyncSourceStatus.RateLimited,
        RecSyncHttpStatus.Rejected or RecSyncHttpStatus.TooLarge => RecSyncSourceStatus.Invalid,
        _ => RecSyncSourceStatus.Unavailable
    };

    private static void CheckUnique(JsonElement element, string label)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"{label} has duplicate fields.");
                CheckUnique(property.Value, label);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) CheckUnique(item, label);
        }
    }

    private static bool String(JsonElement element, string name, out string value)
    {
        value = GetString(element, name) ?? string.Empty;
        return value.Length > 0;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static bool Number(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetInt64(out value);
    }

    private sealed record Manifest(string Version, string GeneratedAt, IReadOnlyDictionary<string, ManifestFile> Files);
    private sealed record ManifestFile(string Path, long SizeBytes, string Sha256);
    private sealed record DownloadedFile(RecSyncSourceStatus? Status, byte[]? Body, string Detail);
}
