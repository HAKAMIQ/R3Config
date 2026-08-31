namespace R3Config.Mdl;

public enum RecState
{
    Applied,
    Pending,
    Manual,
    Blocked,
    Unknown
}

public sealed record RecItem(
    string Serial,
    string Path,
    string? Current,
    string Proposed,
    RecRisk Risk,
    RecState State,
    bool IsDiff,
    bool IsAction,
    bool IsSel,
    string Notes);

public sealed record RecSum(
    string Serial,
    int Total,
    int Applied,
    int Pending,
    int Manual,
    int Blocked,
    int Unknown,
    IReadOnlyList<RecItem> Items);
