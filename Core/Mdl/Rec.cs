using System.Text.Json.Serialization;

namespace R3Config.Mdl;

public enum RecommendationRisk
{
    Required,
    Recommended,
    Optional,
    Conditional,
    PerformanceCost,
    PatchDependent,
    HardwareDependent,
    ManualTiming,
    ExpertOnly,
    Risky,
    Unknown
}

public sealed record ConfigChange(
    string Section,
    string Key,
    string Value,
    string WikiKey,
    string? Notes,
    RecommendationRisk Risk)
{
    [JsonIgnore]
    public string DisplayPath => $"{Section} / {Key}";
}

public sealed record WikiGameRecommendation(
    string Serial,
    string WikiTitle,
    IReadOnlyList<ConfigChange> Changes);
