using System.Collections.Generic;

namespace R3Config.Mdl;

// The evidence model that separates what kind of record it is (kind), who stands behind it
// (authority), what it is about (scope), how well the runtime matches that scope
// (applicability) and what R3Config may do with it (action). Classification text is
// never authority; an unproven value stays Unknown; a conflict never becomes an action.

// Where a record comes from. A kind is not a trust level: a regression record, a Wiki
// note and a hardware spreadsheet are different kinds that each carry their own authority.
public enum EvidenceKind
{
    CurrentWikiConfig,
    CurrentWikiNote,
    HistoricalWiki,
    Rpcs3Lifecycle,
    RegressionRecord,
    Canary,
    Artemis,
    HardwareData,
    RuntimeContext
}

// Authority is recorded from provenance, never inferred from wording or a user name.
// Wiki text that repeats a developer statement is Wiki authority unless the record names
// the statement's owner (Developer). Unattributed reports are Community. Official is the
// RPCS3 project's own source or documentation. There is deliberately no ordinal meaning
// in the declaration order: compare with EvidencePolicy.Rank / AtLeast only.
public enum EvidenceAuthority
{
    Community,
    Wiki,
    Developer,
    Official
}

public enum EvidenceScopeKind
{
    Game,
    Serial,
    AppVersion,
    PpuHash,
    PatchState,
    Renderer,
    Rpcs3Build,
    Rpcs3Commit,
    Architecture,
    Edition,
    Platform,
    GpuVendor,
    CpuVendor
}

// How a scope condition's expected value relates to the runtime fact. Unresolved means the
// evidence declares a scope R3Config could not turn into a comparable value: it is never a
// match, and never a mismatch either.
public enum ScopeRelation
{
    Equal,
    AtLeast,
    Above,
    AtMost,
    Below,
    Unresolved
}

public enum EvidenceScopeMatch
{
    Match,
    Mismatch,
    Unknown
}

public sealed record EvidenceScopeCondition(
    EvidenceScopeKind Kind,
    string Expected,
    ScopeRelation Relation = ScopeRelation.Equal);

// Unknown:  the identity of the evidence could not be matched to the runtime.
// Partial:  the identity matched but at least one declared scope condition is unresolved.
// Exact:    the identity matched and every declared scope condition matched.
// Conflict: the evidence contradicts itself or other evidence for the same subject.
public enum ApplicabilityLevel
{
    Unknown,
    Partial,
    Exact,
    Conflict
}

public enum SettingAction
{
    Blocked,
    Inform,
    Suggest,
    Apply
}

// Why a historical Wiki recommendation left the Wiki. ClaimedFixed is an editor's claim
// (for example "audio issue has been fixed"); only independent RPCS3 evidence may
// upgrade it to VerifiedFixedUpstream. Wiki dates are never build boundaries.
public enum RemovalReason
{
    Unknown,
    BulkCleanup,
    DefaultChanged,
    FieldRenamed,
    SettingRemoved,
    Replaced,
    ScopeChanged,
    ClaimedFixed,
    VerifiedFixedUpstream
}

public enum RuntimeHealth
{
    Healthy,
    Warning,
    Fault
}

// The runtime facts scope conditions are compared with. Null means "not proven".
public sealed record EvidenceRuntimeFacts(
    string? Serial = null,
    string? AppVersion = null,
    string? PpuHash = null,
    string? PatchState = null,
    string? Renderer = null,
    Rpcs3RuntimeBuild? Build = null,
    string? Architecture = null,
    string? Edition = null,
    string? Platform = null,
    string? GpuVendor = null,
    string? CpuVendor = null);

public sealed record EvidenceAssessment(
    ApplicabilityLevel Applicability,
    bool ScopeMismatch,
    IReadOnlyList<string> Reasons);

// The decision for one piece of evidence: what R3Config may do, why, and how well it applies.
public sealed record SettingDecision(
    SettingAction Action,
    ApplicabilityLevel Applicability,
    EvidenceKind Kind,
    EvidenceAuthority Authority,
    IReadOnlyList<string> Reasons);
