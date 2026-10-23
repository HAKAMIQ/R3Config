namespace R3Config.Mdl;

public sealed record CfgEntry(
    CfgChg Change,
    string? CurVal,
    RecItem StateIt)
{
    // CurVal is the effective value; removal needs evidence of a physical key.
    public bool HasCustomOverride { get; init; }

    public string Section => Change.Section;

    public string Key => Change.Key;

    public string Path => Change.Path;

    public string Display => State == RecState.Unknown ? "(unreadable)" : CurVal ?? "(not set)";

    public string Val => Change.Value;

    public string? Notes => Change.Notes;

    public RecRisk Risk => Change.Risk;

    public WikiRecommendationClass Class => Change.Class;

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
    string GlobalCfgPath,
    IReadOnlyList<CfgEntry> Items,
    RecSum RecState,
    bool NeedBkp);

public sealed record CfgRes(
    BackupManifest Backup,
    IReadOnlyList<CfgChg> ChgList);
