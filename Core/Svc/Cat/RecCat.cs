using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class RecommendationCatalogService
{
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
        Info = info with { RecommendationCount = recommendations.Count };
        _recommendations = recommendations;
    }

    public CatalogInfo Info { get; }

    public string DataSource => Info.DataSource;

    public string DataVersion => Info.DataVersion;

    public DateTimeOffset? GeneratedAt => Info.GeneratedAt;

    public string SourceHash => Info.SourceHash;

    public int Count => _recommendations.Count;

    public IReadOnlyCollection<WikiGameRecommendation> Recommendations => _recommendations.Values.ToArray();

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
        RecommendationCatalogDto catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<RecommendationCatalogDto>(catalogStream, JsonOptions)
                ?? throw new InvalidDataException("Recommendation catalog is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Recommendation catalog JSON is invalid.", ex);
        }

        if (string.IsNullOrWhiteSpace(catalog.DataVersion))
        {
            throw new InvalidDataException("Recommendation catalog is missing DataVersion.");
        }

        var recommendations = catalog.Recommendations
            .Where(item => !string.IsNullOrWhiteSpace(item.Serial))
            .Select(NormalizeRecommendation)
            .ToDictionary(item => item.Serial, StringComparer.OrdinalIgnoreCase);

        var info = new CatalogInfo(
            string.IsNullOrWhiteSpace(catalog.DataSource) ? "Built-in" : catalog.DataSource.Trim(),
            catalog.DataVersion.Trim(),
            catalog.GeneratedAt,
            string.IsNullOrWhiteSpace(catalog.SourceHash) ? string.Empty : catalog.SourceHash.Trim(),
            recommendations.Count);

        return new RecommendationCatalogService(info, recommendations);
    }

    public static RecommendationCatalogService FromRecommendations(
        string dataSource,
        string dataVersion,
        IEnumerable<WikiGameRecommendation> recommendations,
        DateTimeOffset? generatedAt = null,
        string? sourceHash = null)
    {
        var map = recommendations
            .Where(item => !string.IsNullOrWhiteSpace(item.Serial))
            .Select(NormalizeRecommendation)
            .ToDictionary(item => item.Serial, StringComparer.OrdinalIgnoreCase);

        var info = new CatalogInfo(
            string.IsNullOrWhiteSpace(dataSource) ? "Built-in" : dataSource.Trim(),
            dataVersion.Trim(),
            generatedAt ?? DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(sourceHash) ? string.Empty : sourceHash.Trim(),
            map.Count);

        return new RecommendationCatalogService(info, map);
    }

    public static void Save(string catalogPath, string dataVersion, IEnumerable<WikiGameRecommendation> recommendations)
    {
        FromRecommendations("Built-in", dataVersion, recommendations).Save(catalogPath);
    }

    public static void Save(string catalogPath, CatalogInfo info, IEnumerable<WikiGameRecommendation> recommendations)
    {
        FromRecommendations(info.DataSource, info.DataVersion, recommendations, info.GeneratedAt, info.SourceHash)
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
            Recommendations = _recommendations.Values
                .Select(NormalizeRecommendation)
                .OrderBy(item => item.Serial, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var directory = Path.GetDirectoryName(catalogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, JsonOptions));
    }

    private static WikiGameRecommendation NormalizeRecommendation(WikiGameRecommendation recommendation)
    {
        var changes = recommendation.Changes
            .Select(change => change with
            {
                Risk = RecommendationRiskPolicy.Classify(change.WikiKey, change.Value, change.Notes)
            })
            .ToArray();

        return recommendation with { Changes = changes };
    }

    private sealed class RecommendationCatalogDto
    {
        public string DataSource { get; set; } = "Built-in";

        public string DataVersion { get; set; } = string.Empty;

        public DateTimeOffset? GeneratedAt { get; set; }

        public string SourceHash { get; set; } = string.Empty;

        public int RecommendationCount { get; set; }

        public List<WikiGameRecommendation> Recommendations { get; set; } = [];
    }
}
