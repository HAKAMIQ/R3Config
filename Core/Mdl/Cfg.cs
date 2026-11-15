namespace R3Config.Mdl;

// The layer a row's current value (CfgEntry.CurVal) comes from, derived by
// PlanService from the same facts as CfgPlan.BootSource. Database only when
// R3Config's copy of the RPCS3 database boots that same value. Unknown when
// the boot source is Unknown, a config cannot be read or decoded, or the key
// has no verified built-in default.
public enum CfgValSrc
{
    Unknown,
    Custom,
    Database,
    Global,
    BuiltIn
}

public sealed record CfgEntry(
    CfgChg Change,
    string? CurVal,
    RecItem StateIt)
{
    // CurVal is the effective value; removal needs evidence of a physical key.
    public bool HasCustomOverride { get; init; }

    // The value RPCS3 boots with now. It differs from CurVal only when the boot
    // source is Database and R3Config's copy of the RPCS3 database sets this
    // key to another value: CurVal is the value that remains once any write
    // creates the custom config.
    public string? RunVal { get; init; }

    // The layer CurVal comes from (display only).
    public CfgValSrc CurSrc { get; init; }

    // RPCS3's default for this key from R3Config's verified built-in table;
    // null when the table has no entry (never a guess).
    public string? DefVal { get; init; }

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

    // Which layer RPCS3 boots this game with on top of config.yml.
    public CfgBootSrc BootSource { get; init; } = CfgBootSrc.Unknown;

    // Wiki guidance that named a range instead of one value. It is evidence
    // text only: it has no CfgEntry, no selection, no current value and no
    // write, so no apply path can ever reach it.
    public IReadOnlyList<CfgHint> Hints { get; init; } = [];
}

// The layer RPCS3 applies on top of config.yml and its built-in defaults.
// Custom: a readable config_<SERIAL>.yml exists, so RPCS3 ignores its database
// config. Database: no custom config and R3Config's copy of the RPCS3 database
// has an entry for the Serial. Global: neither. Unknown: anything not proven.
public enum CfgBootSrc
{
    Unknown,
    Custom,
    Database,
    Global
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
