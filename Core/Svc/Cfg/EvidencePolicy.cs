using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Pure decision functions: scope conditions x runtime facts -> applicability, then
// applicability x authority x currentness -> action. No I/O, no heuristics: a condition is
// either comparable (and then matched or mismatched) or Unknown, never guessed.
public static class EvidencePolicy
{
    private static readonly Regex BuildText = new(
        @"^v?(?<version>\d+\.\d+\.\d+)(?:-(?<build>\d+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Authority is compared through this ranking only; the enum declaration order carries no meaning.
    public static int Rank(EvidenceAuthority authority) => authority switch
    {
        EvidenceAuthority.Community => 0,
        EvidenceAuthority.Wiki => 1,
        EvidenceAuthority.Developer => 2,
        EvidenceAuthority.Official => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown authority.")
    };

    public static bool AtLeast(EvidenceAuthority authority, EvidenceAuthority minimum) =>
        Rank(authority) >= Rank(minimum);

    private static bool Holds(int order, ScopeRelation relation) => relation switch
    {
        ScopeRelation.Equal => order == 0,
        ScopeRelation.AtLeast => order >= 0,
        ScopeRelation.Above => order > 0,
        ScopeRelation.AtMost => order <= 0,
        ScopeRelation.Below => order < 0,
        _ => false
    };

    // `order` is Compare(runtime, expected).
    private static EvidenceScopeMatch FromOrder(int order, ScopeRelation relation) =>
        Holds(order, relation) ? EvidenceScopeMatch.Match : EvidenceScopeMatch.Mismatch;

    private static EvidenceScopeMatch CompareBuild(EvidenceScopeCondition condition, Rpcs3RuntimeBuild? runtime)
    {
        if (runtime is not { IsExact: true })
        {
            return EvidenceScopeMatch.Unknown;
        }

        var expected = condition.Expected.Trim();
        if (int.TryParse(expected, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return runtime.BuildNumber is { } actualNumber
                ? FromOrder(actualNumber.CompareTo(number), condition.Relation)
                : EvidenceScopeMatch.Unknown;
        }

        var match = BuildText.Match(expected);
        var actualVersion = NoteScope.ParseVersion(runtime.Version);
        if (!match.Success || actualVersion is null)
        {
            return EvidenceScopeMatch.Unknown;
        }

        var order = NoteScope.Compare(actualVersion, NoteScope.ParseVersion(match.Groups["version"].Value)!);
        if (order == 0 && match.Groups["build"].Success)
        {
            if (runtime.BuildNumber is not { } actualBuild ||
                !int.TryParse(match.Groups["build"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var build))
            {
                return EvidenceScopeMatch.Unknown;
            }

            order = actualBuild.CompareTo(build);
        }

        return FromOrder(order, condition.Relation);
    }

    private static EvidenceScopeMatch Compare(EvidenceScopeCondition condition, EvidenceRuntimeFacts facts)
    {
        if (condition.Relation == ScopeRelation.Unresolved)
        {
            return EvidenceScopeMatch.Unknown;
        }

        if (condition.Kind == EvidenceScopeKind.Rpcs3Build)
        {
            return CompareBuild(condition, facts.Build);
        }

        string? actual = condition.Kind switch
        {
            EvidenceScopeKind.Serial or EvidenceScopeKind.Game => facts.Serial,
            EvidenceScopeKind.AppVersion => facts.AppVersion,
            EvidenceScopeKind.PpuHash => facts.PpuHash,
            EvidenceScopeKind.PatchState => facts.PatchState,
            EvidenceScopeKind.Renderer => facts.Renderer,
            EvidenceScopeKind.Architecture => facts.Architecture,
            EvidenceScopeKind.Edition => facts.Edition,
            EvidenceScopeKind.Platform => facts.Platform,
            EvidenceScopeKind.GpuVendor => facts.GpuVendor,
            EvidenceScopeKind.CpuVendor => facts.CpuVendor,
            EvidenceScopeKind.Rpcs3Commit => facts.Build is { IsExact: true } c ? c.Commit : null,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(condition.Expected))
        {
            return EvidenceScopeMatch.Unknown;
        }

        if (condition.Kind == EvidenceScopeKind.AppVersion)
        {
            var expectedVersion = NoteScope.ParseVersion(condition.Expected);
            var actualVersion = NoteScope.ParseVersion(actual);
            return expectedVersion is null || actualVersion is null
                ? EvidenceScopeMatch.Unknown
                : FromOrder(NoteScope.Compare(actualVersion, expectedVersion), condition.Relation);
        }

        if (condition.Relation != ScopeRelation.Equal)
        {
            return EvidenceScopeMatch.Unknown;
        }

        var equal = condition.Kind == EvidenceScopeKind.Rpcs3Commit
            ? CommitEquals(condition.Expected, actual)
            : string.Equals(condition.Expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
        return equal ? EvidenceScopeMatch.Match : EvidenceScopeMatch.Mismatch;
    }

    // A short commit is compared as a prefix only when both sides are at least 8 hex characters.
    private static bool CommitEquals(string expected, string actual)
    {
        var a = expected.Trim().ToLowerInvariant();
        var b = actual.Trim().ToLowerInvariant();
        if (a.Length < 8 || b.Length < 8)
        {
            return false;
        }

        return a.Length <= b.Length ? b.StartsWith(a, StringComparison.Ordinal) : a.StartsWith(b, StringComparison.Ordinal);
    }

    // Exact:    the identity (serial/game) matched and EVERY declared condition matched.
    // Partial:  the identity matched but a declared condition is unresolved (unknown runtime fact or
    //           a scope the evidence states but R3Config cannot compare). Never an action.
    // Unknown:  no identity condition, or the identity could not be matched.
    // A mismatch is reported separately (ScopeMismatch) and never lifts a level.
    public static EvidenceAssessment Assess(IReadOnlyList<EvidenceScopeCondition> conditions, EvidenceRuntimeFacts facts, bool conflict = false)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(facts);

        var reasons = new List<string>();
        if (conflict)
        {
            reasons.Add("EvidenceConflict");
            return new EvidenceAssessment(ApplicabilityLevel.Conflict, false, reasons);
        }

        var results = conditions.Select(c => (Condition: c, Result: Compare(c, facts))).ToList();
        foreach (var (condition, result) in results)
        {
            reasons.Add($"{condition.Kind}:{result}");
        }

        if (results.Any(r => r.Result == EvidenceScopeMatch.Mismatch))
        {
            return new EvidenceAssessment(ApplicabilityLevel.Unknown, true, reasons);
        }

        static bool IsIdentity(EvidenceScopeKind kind) => kind is EvidenceScopeKind.Serial or EvidenceScopeKind.Game;
        var identityMatched = results.Any(r => IsIdentity(r.Condition.Kind) && r.Result == EvidenceScopeMatch.Match);
        if (!identityMatched)
        {
            reasons.Add("IdentityNotMatched");
            return new EvidenceAssessment(ApplicabilityLevel.Unknown, false, reasons);
        }

        return results.Any(r => r.Result == EvidenceScopeMatch.Unknown)
            ? new EvidenceAssessment(ApplicabilityLevel.Partial, false, reasons)
            : new EvidenceAssessment(ApplicabilityLevel.Exact, false, reasons);
    }

    // Apply only for deterministic, Exact, current evidence of at least Wiki authority. Suggest for
    // Exact evidence whose value the user must choose. Partial and Unknown are Inform: an unresolved
    // scope is shown, never acted on. Historical evidence without a proven build applicability is
    // Inform at most. Conflict and a scope mismatch are Blocked.
    public static SettingAction Decide(
        EvidenceAssessment assessment,
        EvidenceAuthority authority,
        bool isHistorical,
        bool buildApplicabilityProven,
        bool deterministicValue)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (assessment.Applicability == ApplicabilityLevel.Conflict || assessment.ScopeMismatch)
        {
            return SettingAction.Blocked;
        }

        if (assessment.Applicability != ApplicabilityLevel.Exact)
        {
            return SettingAction.Inform;
        }

        if (isHistorical && !buildApplicabilityProven)
        {
            return SettingAction.Inform;
        }

        if (!AtLeast(authority, EvidenceAuthority.Wiki))
        {
            return SettingAction.Inform;
        }

        return deterministicValue ? SettingAction.Apply : SettingAction.Suggest;
    }
}

#pragma warning restore IDE0130
