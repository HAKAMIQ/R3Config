namespace R3Config.Mdl;

public sealed record CatalogInfo(
    string DataSource,
    string DataVersion,
    DateTimeOffset? GeneratedAt,
    string SourceHash,
    int RecommendationCount)
{
    public string DataDisplay => string.IsNullOrWhiteSpace(DataVersion)
        ? $"Data: {DataSource}"
        : $"Data: {DataSource} {DataVersion}";

    public string RecommendationDisplay => $"Recommendations: {RecommendationCount}";
}
