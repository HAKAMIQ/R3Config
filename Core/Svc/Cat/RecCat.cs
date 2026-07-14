using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class RecommendationCatalogService
{
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IReadOnlyDictionary<string, WikiGameRecommendation> _recommendations;

    private RecommendationCatalogService(
        CatalogInfo info,
        IReadOnlyDictionary<string, WikiGameRecommendation> recommendations)
    {
        Info = info with
        {
            RecommendationCount = recommendations.Count
        };

        _recommendations = recommendations;
    }

    public CatalogInfo Info { get; }

    public string DataSource => Info.DataSource;

    public string DataVersion => Info.DataVersion;

    public DateTimeOffset? GeneratedAt => Info.GeneratedAt;

    public string SourceHash => Info.SourceHash;

    public int Count => _recommendations.Count;

    public IReadOnlyCollection<WikiGameRecommendation> Recommendations =>
    [
        .. _recommendations.Values
    ];

    public WikiGameRecommendation? Find(string serial)
    {
        return _recommendations.TryGetValue(serial, out var recommendation)
            ? recommendation
            : null;
    }

    public RecommendationCatalogService WithInfo(CatalogInfo info)
    {
        return new RecommendationCatalogService(info, _recommendations);
    }

    public static RecommendationCatalogService Load(string catalogPath)
    {
        using var stream = File.OpenRead(catalogPath);
        return Load(stream);
    }

    public static RecommendationCatalogService Load(Stream catalogStream)
    {
        ArgumentNullException.ThrowIfNull(catalogStream);

        byte[] bytes;

        using (var buffer = new MemoryStream())
        {
            catalogStream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        if (bytes.Length == 0)
        {
            throw new InvalidDataException(
                "Recommendation catalog is empty or invalid.");
        }

        RecommendationCatalogDto catalog;

        try
        {
            using var document = JsonDocument.Parse(bytes);
            ValidateUniqueJsonKeys(document.RootElement, "$");

            catalog = JsonSerializer.Deserialize<RecommendationCatalogDto>(
                bytes,
                JsonOptions)
                ?? throw new InvalidDataException(
                    "Recommendation catalog is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Recommendation catalog JSON is invalid.",
                ex);
        }

        var dataVersion = RequireText(
            catalog.DataVersion,
            "DataVersion");

        var recommendations = BuildMap(catalog.Recommendations);

        var info = new CatalogInfo(
            string.IsNullOrWhiteSpace(catalog.DataSource)
                ? "Built-in"
                : catalog.DataSource.Trim(),
            dataVersion,
            catalog.GeneratedAt,
            string.IsNullOrWhiteSpace(catalog.SourceHash)
                ? string.Empty
                : catalog.SourceHash.Trim(),
            recommendations.Count);

        return new RecommendationCatalogService(
            info,
            recommendations);
    }

    public static RecommendationCatalogService FromRecommendations(
        string dataSource,
        string dataVersion,
        IEnumerable<WikiGameRecommendation> recommendations,
        DateTimeOffset? generatedAt = null,
        string? sourceHash = null)
    {
        ArgumentNullException.ThrowIfNull(recommendations);

        var map = BuildMap(recommendations);

        var info = new CatalogInfo(
            string.IsNullOrWhiteSpace(dataSource)
                ? "Built-in"
                : dataSource.Trim(),
            RequireText(dataVersion, "DataVersion"),
            generatedAt ?? DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(sourceHash)
                ? string.Empty
                : sourceHash.Trim(),
            map.Count);

        return new RecommendationCatalogService(info, map);
    }

    public static void Save(
        string catalogPath,
        string dataVersion,
        IEnumerable<WikiGameRecommendation> recommendations)
    {
        FromRecommendations(
            "Built-in",
            dataVersion,
            recommendations)
            .Save(catalogPath);
    }

    public static void Save(
        string catalogPath,
        CatalogInfo info,
        IEnumerable<WikiGameRecommendation> recommendations)
    {
        FromRecommendations(
            info.DataSource,
            info.DataVersion,
            recommendations,
            info.GeneratedAt,
            info.SourceHash)
            .Save(catalogPath);
    }

    public void Save(string catalogPath)
    {
        var catalog = new RecommendationCatalogDto
        {
            DataSource = Info.DataSource,
            DataVersion = Info.DataVersion,
            GeneratedAt = Info.GeneratedAt,
            SourceHash = Info.SourceHash,
            RecommendationCount = Count,
            Recommendations =
            [
                .. _recommendations.Values
                    .Select(NormalizeRecommendation)
                    .OrderBy(
                        item => item.Serial,
                        StringComparer.OrdinalIgnoreCase)
            ]
        };

        var directory = Path.GetDirectoryName(catalogPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer
            .Serialize(catalog, JsonOptions)
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);

        File.WriteAllText(
            catalogPath,
            json,
            Utf8NoBom);
    }

    private static Dictionary<string, WikiGameRecommendation> BuildMap(
        IEnumerable<WikiGameRecommendation>? source)
    {
        if (source is null)
        {
            throw new InvalidDataException(
                "Recommendation catalog is missing Recommendations.");
        }

        var map =
            new Dictionary<string, WikiGameRecommendation>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in source)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "Recommendation catalog contains a null recommendation.");
            }

            var normalized = NormalizeRecommendation(item);

            if (!map.TryAdd(normalized.Serial, normalized))
            {
                throw new InvalidDataException(
                    $"Recommendation catalog contains duplicate serial '{normalized.Serial}'.");
            }
        }

        return map;
    }

    private static WikiGameRecommendation NormalizeRecommendation(
        WikiGameRecommendation recommendation)
    {
        var serial = RequireText(
            recommendation.Serial,
            "Serial")
            .ToUpperInvariant();

        if (!SerialPattern().IsMatch(serial))
        {
            throw new InvalidDataException(
                $"Recommendation serial '{serial}' is invalid.");
        }

        var title = RequireText(
            recommendation.WikiTitle,
            $"WikiTitle for {serial}");

        var sourceChanges = recommendation.Changes
            ?? throw new InvalidDataException(
                $"Recommendation '{serial}' is missing Changes.");

        List<ConfigChange> changes = [];
        var paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var change in sourceChanges)
        {
            if (change is null)
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' contains a null change.");
            }

            var section = RequireText(
                change.Section,
                $"Section for {serial}");

            var key = RequireText(
                change.Key,
                $"Key for {serial}");

            var value = RequireText(
                change.Value,
                $"Value for {serial} / {section} / {key}");

            var wikiKey = RequireText(
                change.WikiKey,
                $"WikiKey for {serial} / {section} / {key}");

            var path = $"{section}\0{key}";

            if (!paths.Add(path))
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' contains duplicate setting '{section} / {key}'.");
            }

            var notes = string.IsNullOrWhiteSpace(change.Notes)
                ? null
                : change.Notes.Trim();

            changes.Add(change with
            {
                Section = section,
                Key = key,
                Value = value,
                WikiKey = wikiKey,
                Notes = notes,
                Risk = RecommendationRiskPolicy.Classify(
                    wikiKey,
                    value,
                    notes)
            });
        }

        return recommendation with
        {
            Serial = serial,
            WikiTitle = title,
            Changes =
            [
                .. changes
            ]
        };
    }

    private static string RequireText(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Recommendation catalog field '{fieldName}' is missing or empty.");
        }

        return value.Trim();
    }

    private static void ValidateUniqueJsonKeys(
        JsonElement element,
        string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var keys = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var property in element.EnumerateObject())
                    {
                        if (!keys.Add(property.Name))
                        {
                            throw new InvalidDataException(
                                $"Recommendation catalog contains duplicate JSON key '{property.Name}' at '{path}'.");
                        }

                        ValidateUniqueJsonKeys(
                            property.Value,
                            $"{path}.{property.Name}");
                    }

                    break;
                }

            case JsonValueKind.Array:
                {
                    var index = 0;

                    foreach (var item in element.EnumerateArray())
                    {
                        ValidateUniqueJsonKeys(
                            item,
                            $"{path}[{index}]");

                        index++;
                    }

                    break;
                }
        }
    }

    [GeneratedRegex(
        @"^[A-Z0-9]{9}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SerialPattern();

    private sealed class RecommendationCatalogDto
    {
        public string? DataSource { get; set; } = "Built-in";

        public string? DataVersion { get; set; }

        public DateTimeOffset? GeneratedAt { get; set; }

        public string? SourceHash { get; set; }

        public int RecommendationCount { get; set; }

        public List<WikiGameRecommendation>? Recommendations { get; set; }
    }
}