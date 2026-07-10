namespace R3Config.Mdl;

public enum RecState
{
    Applied,
    Pending,
    Manual,
    Blocked,
    Unknown
}

public sealed record RecStateItem(
    string Serial,
    string Path,
    string? Current,
    string Proposed,
    RecommendationRisk Risk,
    RecState State,
    bool IsDifferent,
    bool IsActionable,
    bool IsAutoSelectable,
    string Notes);

public sealed record RecStateSummary(
    string Serial,
    int Total,
    int Applied,
    int Pending,
    int Manual,
    int Blocked,
    int Unknown,
    IReadOnlyList<RecStateItem> Items);
