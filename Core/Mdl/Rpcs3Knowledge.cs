using System.Collections.Generic;

namespace R3Config.Mdl;

// Verified, normalized knowledge distilled from the development-time evidence pipelines
// (frozen RPCS3 source history, the historical Wiki analyzer, the CPU workbook). Only what
// the runtime can act on or show is carried; raw corpora are never shipped. Every fact
// keeps its source, and an unproven value stays null. Authority and kind are not stored per
// fact: they are fixed by the fact type (see Rpcs3KnowledgeCatalog).

public sealed record KnowledgeSource(
    string Id,
    string Kind,
    string Name,
    string Sha256,
    string? Date,
    string? Note);

// A setting's name history in RPCS3, from the frozen source history. Build numbers are git
// commit counts (verified for every official build row). It records lifecycle only: it never
// says a game needed the setting. LastChangeBuild is null when no history was found.
public sealed record SettingLifecycleFact(
    string WikiKey,
    string DisplayName,
    bool DeclaredAtFrozenHead,
    int? LastChangeBuild,
    string? LastChangeCommit,
    string Evidence);

// A recommendation a Wiki editor removed while claiming the underlying problem was fixed, was
// replaced, or the setting no longer exists. The claim is not verified: it never becomes a
// build boundary, never changes the current recommendation and is shown as history only.
// Bulk cleanups and unclassified removals are not shipped.
public sealed record HistoricalRemovalFact(
    string Page,
    string WikiKey,
    RemovalReason Reason,
    string Basis,
    string ObservedUntil,
    string? EditComment);

// One thread-scaling observation from the community CPU workbook: a game at an FPS target on a
// core/thread configuration, graded from linked evidence. Informational hardware evidence only.
// WikiPage is set only when the workbook's game name equals a Wiki page title exactly; a game
// that does not resolve keeps null and is never attached to a game by similarity.
public sealed record ThreadScalingFact(
    string Game,
    string? WikiPage,
    string? FpsTarget,
    string CoreThreadConfig,
    int? Cores,
    int? Threads,
    string Grade,
    string? SourceLink,
    string? Note);

// A community CPU tier list entry. Informational hardware evidence only: it never selects,
// applies or gates a game setting, and a host CPU is never matched to it by similarity.
public sealed record CpuTierFact(
    string List,
    string Block,
    string Tier,
    string Cpu);

public sealed record Rpcs3KnowledgeDocument(
    int Schema,
    string DataVersion,
    IReadOnlyList<KnowledgeSource> Sources,
    IReadOnlyList<SettingLifecycleFact> Settings,
    IReadOnlyList<HistoricalRemovalFact> HistoricalRemovals,
    IReadOnlyList<ThreadScalingFact> ThreadScaling,
    IReadOnlyList<CpuTierFact> CpuTiers);

// One knowledge fact together with what R3Config may do with it. Knowledge facts are history or
// hardware evidence: their action is Inform at most (never Apply), because a historical claim is
// not a current build boundary and a community observation is not authority.
public sealed record KnowledgeItem<T>(T Fact, SettingDecision Decision);

public sealed record GameKnowledge(
    IReadOnlyList<KnowledgeItem<HistoricalRemovalFact>> Removals,
    IReadOnlyList<KnowledgeItem<SettingLifecycleFact>> Lifecycles,
    IReadOnlyList<KnowledgeItem<ThreadScalingFact>> ThreadScaling)
{
    public static GameKnowledge Empty { get; } = new([], [], []);

    public bool HasAny => Removals.Count > 0 || Lifecycles.Count > 0 || ThreadScaling.Count > 0;
}
