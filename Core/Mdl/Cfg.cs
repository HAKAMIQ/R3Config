namespace R3Config.Mdl;

public sealed record ConfigPlanEntry(
    ConfigChange Change,
    string? CurrentValue,
    RecStateItem StateItem)
{
    public string Section => Change.Section;

    public string Key => Change.Key;

    public string DisplayPath => Change.DisplayPath;

    public string CurrentDisplay => State == RecState.Unknown ? "(unreadable)" : CurrentValue ?? "(not set)";

    public string ProposedValue => Change.Value;

    public string? Notes => Change.Notes;

    public RecommendationRisk Risk => Change.Risk;

    public RecState State => StateItem.State;

    public bool IsDifferent => StateItem.IsDifferent;

    public bool IsActionable => StateItem.IsActionable;

    public bool IsAutoSelectable => StateItem.IsAutoSelectable;

    public string Status => State.ToString();
}

public sealed record ConfigPlan(
    string GameSerial,
    string GameTitle,
    string TargetConfigPath,
    IReadOnlyList<ConfigPlanEntry> Items,
    RecStateSummary RecState,
    bool RequiresBackup);

public sealed record ConfigApplyResult(
    BackupManifest Backup,
    IReadOnlyList<ConfigChange> AppliedChanges);
