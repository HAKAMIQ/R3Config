namespace R3Config.Mdl;

public enum PatSafetySeverity
{
    None,
    Caution,
    High
}

public sealed record PatSafetyAssessment(
    PatSafetySeverity Severity,
    bool CrashOrFreezeRisk,
    bool ProgressionRisk,
    bool DisableAndRetry)
{
    public bool HasRisk => Severity != PatSafetySeverity.None;
}
