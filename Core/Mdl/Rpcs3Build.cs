using System;
using System.Collections.Generic;

namespace R3Config.Mdl;

public enum Rpcs3BuildIdentityState
{
    Unavailable,
    Partial,
    Exact,
    Conflict
}

public enum Rpcs3BuildEvidenceKind
{
    RuntimeLogBanner
}

public sealed record Rpcs3BuildBanner(
    string VersionText,
    int BuildNumber,
    string Commit,
    string Stage,
    string Branch,
    string? Architecture);

public sealed record Rpcs3BuildEvidence(
    Rpcs3BuildEvidenceKind Kind,
    string SourcePath,
    DateTimeOffset SourceLastWriteTimeUtc,
    string VersionText,
    int BuildNumber,
    string Commit,
    string Stage,
    string Branch,
    string? Architecture);

public sealed record Rpcs3BuildIdentity(
    string? VersionText,
    int? BuildNumber,
    string? Commit,
    string? Stage,
    string? Branch,
    string? Architecture,
    Rpcs3BuildIdentityState State,
    IReadOnlyList<Rpcs3BuildEvidence> Evidence,
    string? Diagnostic = null)
{
    public bool IsExact =>
        State == Rpcs3BuildIdentityState.Exact &&
        BuildNumber is > 0 &&
        !string.IsNullOrWhiteSpace(VersionText) &&
        !string.IsNullOrWhiteSpace(Commit);

    public static Rpcs3BuildIdentity Unavailable(
        string? diagnostic = null)
    {
        return new Rpcs3BuildIdentity(
            VersionText: null,
            BuildNumber: null,
            Commit: null,
            Stage: null,
            Branch: null,
            Architecture: null,
            State: Rpcs3BuildIdentityState.Unavailable,
            Evidence: [],
            Diagnostic: diagnostic);
    }
}

// The runtime RPCS3 identity as separate typed facts. Version ("0.0.35"), build
// number and commit are never merged into one string. Every fact is null when
// the source did not prove it; Confidence says how much of the identity the
// runtime sources agree on. Only an Exact identity may decide build scope.
public sealed record Rpcs3RuntimeBuild(
    string? Version,
    int? BuildNumber,
    string? Commit,
    string? Branch,
    string? Stage,
    string? Architecture,
    Rpcs3BuildIdentityState Confidence)
{
    public bool IsExact =>
        Confidence == Rpcs3BuildIdentityState.Exact &&
        BuildNumber is > 0 &&
        !string.IsNullOrWhiteSpace(Version) &&
        !string.IsNullOrWhiteSpace(Commit);

    public static Rpcs3RuntimeBuild? From(Rpcs3BuildIdentity? identity) =>
        identity is null || identity.State == Rpcs3BuildIdentityState.Unavailable
            ? null
            : new Rpcs3RuntimeBuild(
                identity.VersionText,
                identity.BuildNumber,
                identity.Commit,
                identity.Branch,
                identity.Stage,
                identity.Architecture,
                identity.State);
}
