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
    RecRisk Risk)
{
    [JsonIgnore]
    public string Path => $"{Section} / {Key}";
}

public sealed record WikiRec(
    string Serial,
    [property: JsonPropertyName("WikiTitle")] string WikiName,
    IReadOnlyList<CfgChg> Changes);
