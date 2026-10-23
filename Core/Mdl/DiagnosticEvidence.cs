using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace R3Config.Mdl;

// Who published a piece of diagnostic evidence. The role is provenance and is
// never inferred or upgraded: a community index stays a community index even
// when its game identity is resolved through the official Wiki.
public enum EvidenceSourceRole
{
    OfficialRpcnTest,
    OfficialRpcnGuidance,
    OfficialCurrentBrowser,
    CommunityPrivateServerIndex,
    GithubIssue,
    ForumThread
}

// Strength of the claim itself, strongest first. Nothing raises a level by
// inference (agreement with another source, popularity, recency).
public enum EvidenceLevel
{
    Official,
    DeveloperConfirmed,
    ReproducibleMultiReport,
    ReproducibleSingleReport,
    Anecdotal,
    Unknown
}

// What the source's own row proves about which game it is about.
public enum SubjectIdentityState
{
    // A deterministic Wiki identity exists (exact title, safe formatting
    // normalization or an exact Wiki page). WikiPage is set.
    Resolved,

    // The source row deliberately covers several games, an archive or user
    // content, so it is never forced onto one game.
    CommunityScope,

    // Extra links of the previous titled row; not a game of its own.
    ContinuationMetadata,

    // No deterministic identity: kept fail-closed and never shown for a game.
    Unresolved
}

// The kind of statement a record makes. Observations are dated snapshots of
// something time-varying; none of them is a test, a requirement or a fix.
public enum EvidenceClaimType
{
    CurrentBrowserListing,
    CurrentBrowserOnlineNow,
    CurrentBrowserPeak24h,
    CurrentBrowserPeakAllTime,
    CommunityWorkingOnline,
    CommunityRequiresCfw,
    CommunityContinuation
}

// Where a record may be used. Only GameScoped evidence is ever shown for a
// specific game; the other values keep the record from being attached to one.
public enum EvidenceApplicability
{
    GameScoped,
    ScopeOnly,
    NotApplicable,
    Unresolved
}

public sealed record EvidenceSubject(
    SubjectIdentityState Identity,
    string IdentityMethod,
    string? Title,
    string? WikiPage = null,
    string? CommId = null,
    string? ParentKey = null,
    string? ScopeType = null,
    bool HasOfficialRpcnTest = false);

// One provenance-carrying piece of evidence. Value is the source's own text,
// uninterpreted. No record can auto-apply or mutate anything: both flags are
// constant false and are not part of the shipped data.
public sealed record DiagnosticEvidenceRecord(
    EvidenceSourceRole Role,
    EvidenceLevel Level,
    string SourceId,
    string? SourceUri,
    string? SnapshotDate,
    EvidenceSubject Subject,
    EvidenceClaimType Claim,
    string? Value,
    EvidenceApplicability Applicability,
    string Provenance)
{
    [JsonIgnore]
    public bool AutoApply { get; } = false;

    [JsonIgnore]
    public bool MayMutateSettings { get; } = false;
}

// One input the artifact was generated from. InputHashes are SHA-256 of the
// exact input files (name -> hash) so a regeneration can be checked against them.
public sealed record DiagnosticEvidenceSource(
    string SourceId,
    EvidenceSourceRole Role,
    string? Uri,
    string? SnapshotDate,
    IReadOnlyDictionary<string, string> InputHashes);

// Self-description of the artifact. A record is one dated claim about one
// source entry (a browser page, a tracker row), NOT one game: the same game
// can carry several records, and Subjects counts the distinct source entries.
// There is deliberately no wall-clock timestamp: generation is deterministic,
// and LatestSnapshotDate is the newest source snapshot it was built from.
public sealed record DiagnosticEvidenceManifest(
    int SchemaVersion,
    string Generator,
    string GeneratorVersion,
    string RecordSemantics,
    string LatestSnapshotDate,
    IReadOnlyList<DiagnosticEvidenceSource> Sources,
    IReadOnlyDictionary<string, int> RecordsByRole,
    IReadOnlyDictionary<string, int> RecordsByClaim,
    IReadOnlyDictionary<string, int> RecordsByIdentity,
    IReadOnlyDictionary<string, int> SubjectsByRole);

public sealed record DiagnosticEvidenceDocument(
    int Schema,
    string Source,
    DiagnosticEvidenceManifest? Manifest,
    IReadOnlyList<DiagnosticEvidenceRecord> Records);
