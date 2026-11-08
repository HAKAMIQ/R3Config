namespace R3Config.Mdl;

public sealed record CfgEntry(
    CfgChg Change,
    string? CurVal,
    RecItem StateIt)
{
    // CurVal is the effective value; removal needs evidence of a physical key.
    public bool HasCustomOverride { get; init; }

    // The value RPCS3 boots with now. It differs from CurVal only when the game
    // has no custom config and RPCS3's database config sets this key: CurVal is
    // the value that remains once any write creates the custom config.
    public string? RunVal { get; init; }

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
    bool NeedBkp)
{
    // RPCS3's database config settings that apply to this game now, because it
    // has no custom config. Empty once a custom config exists.
    public IReadOnlyList<CfgDbSetting> DbCfg { get; init; } = [];
}

// One RPCS3 database config setting. BaseVal is the value RPCS3 falls back to
// (config.yml, then the verified built-in default) once a custom config exists;
// null when that value cannot be proven.
public sealed record CfgDbSetting(
    string Section,
    string Key,
    string Value,
    string? BaseVal);

public sealed record CfgRes(
    BackupManifest Backup,
    IReadOnlyList<CfgChg> ChgList);
