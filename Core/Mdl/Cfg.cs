namespace R3Config.Mdl;

public sealed record CfgEntry(
    CfgChg Change,
    string? CurVal,
    RecItem StateIt)
{
    public string Section => Change.Section;

    public string Key => Change.Key;

    public string Path => Change.Path;

    public string Display => State == RecState.Unknown ? "(unreadable)" : CurVal ?? "(not set)";

    public string Val => Change.Value;

    public string? Notes => Change.Notes;

    public RecRisk Risk => Change.Risk;

    public RecState State => StateIt.State;

    public bool IsDiff => StateIt.IsDiff;

    public bool IsAction => StateIt.IsAction;

    public bool IsSel => StateIt.IsSel;

    public string Status => State.ToString();
}

public sealed record CfgPlan(
    string GameSer,
    string GameName,
    string CfgPath,
    IReadOnlyList<CfgEntry> Items,
    RecSum RecState,
    bool NeedBkp);

public sealed record CfgRes(
    BkpMan Backup,
    IReadOnlyList<CfgChg> ChgList);
