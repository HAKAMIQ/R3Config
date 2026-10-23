using System;
using System.Collections.Generic;
using System.Linq;

namespace R3Config.Mdl;

// How well one runtime fact is proven. Unknown/Unavailable/Unsupported/
// Conflict are never silently promoted to a positive answer.
public enum EvidenceState
{
    // Every required identity component is present and agrees.
    Exact,

    // Read from real evidence on this machine.
    Known,

    // Some of the evidence exists, some is missing or only name-derived.
    Partial,

    // Evidence was looked for and could not be established.
    Unknown,

    // The source of the evidence is not there (no install, no scan yet).
    Unavailable,

    // Sources disagree; nothing is chosen.
    Conflict,

    // Evidence exists but this build of R3Config cannot use it.
    Unsupported,

    // The question does not apply in this context.
    NotApplicable
}

public enum RuntimeFactGroup
{
    Hardware,
    Installation,
    Build,
    Game,
    Compatibility
}

// One fact of the runtime environment. Value is raw technical text (a name, a
// path, a build, a hash) shown left-to-right and never translated; Code is a
// stable identifier for descriptive text the presentation layer localizes.
public sealed record RuntimeFact(
    RuntimeFactGroup Group,
    string Key,
    string? Value,
    EvidenceState State,
    string? Code = null);

// Evidence snapshot of the actual machine, RPCS3 installation and selected
// game. It carries facts only: it owns no mutation, patch, Canary or Artemis
// policy, and it recommends nothing.
public sealed record RuntimeContextSnapshot(
    IReadOnlyList<RuntimeFact> Facts,
    EvidenceState BuildIdentityState,
    EvidenceState GameIdentityState)
{
    public static RuntimeContextSnapshot Empty { get; } = new([], EvidenceState.Unavailable, EvidenceState.NotApplicable);

    public IReadOnlyList<RuntimeFact> InGroup(RuntimeFactGroup group) =>
        [.. Facts.Where(fact => fact.Group == group)];

    public RuntimeFact? Find(string key) =>
        Facts.FirstOrDefault(fact => string.Equals(fact.Key, key, StringComparison.Ordinal));
}
