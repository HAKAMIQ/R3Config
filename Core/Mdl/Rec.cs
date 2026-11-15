using System.Text.Json.Serialization;

namespace R3Config.Mdl;

public enum RecRisk
{
    Required,
    Rec,
    Optional,
    Cond,
    PerfCost,
    PatDep,
    Dep,
    Timing,
    Only,
    Risky,
    Unknown
}

public sealed record CfgChg(
    string Section,
    string Key,
    string Value,
    string WikiKey,
    string? Notes,
    RecRisk Risk,
    WikiRecommendationClass Class =
        WikiRecommendationClass.GeneralRecommendation)
{
    [JsonIgnore]
    public string Path => $"{Section} / {Key}";

    [JsonIgnore]
    public RecOrigin Origin { get; init; } = RecOrigin.Baseline;
}

// Wiki guidance that names a range of values instead of one value, such as
// "preferred spu threads = 1 - 3". It is evidence shown as text only: it
// carries no chosen value, it never becomes a CfgChg, and nothing applies it.
// WikiCorpus keeps rejecting the value for application; this record only
// carries the range the Wiki wrote so the screen can quote it.
public sealed record CfgHint(
    string Section,
    string Key,
    string WikiKey,
    string RawValue,
    int Min,
    int Max,
    string? Notes = null)
{
    [JsonIgnore]
    public string Path => $"{Section} / {Key}";
}

public sealed record WikiRec(
    string Serial,
    [property: JsonPropertyName("WikiTitle")] string WikiName,
    IReadOnlyList<CfgChg> Changes,
    WikiGameIdRole Binding = WikiGameIdRole.Primary,
    string? VariantLabel = null,
    // Omitted from the catalog when empty, so a serial without ambiguous Wiki
    // guidance keeps the bytes it had.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CfgHint>? Hints = null)
{
    [JsonIgnore]
    public bool IsInherited =>
        Binding != WikiGameIdRole.Primary;

    [JsonIgnore]
    public IReadOnlyList<CfgHint> Guidance => Hints ?? [];
}
