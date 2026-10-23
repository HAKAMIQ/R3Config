using System;
using System.Collections.Generic;
using R3Config.Mdl;
using R3Config.Svc;

// Evidence model behavior: scope conditions x runtime facts -> applicability, then
// applicability x authority x currentness -> action. Unknown never applies, a conflict or a
// scope mismatch is Blocked, historical evidence without a proven build is Inform, and
// authority is data, never wording. Every fixture is synthetic.
internal static class EvidencePolicyT
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    private static EvidenceScopeCondition C(EvidenceScopeKind kind, string value) => new(kind, value);

    private static EvidenceRuntimeFacts Facts(string? hash = Hash, Rpcs3RuntimeBuild? build = null) =>
        new("BLUS30001", "01.02", hash, null, "Vulkan", build, "x64");

    private static Rpcs3RuntimeBuild Build(int number = 17000, Rpcs3BuildIdentityState state = Rpcs3BuildIdentityState.Exact) =>
        new("0.0.35", number, "abcdef1234567890abcdef1234567890abcdef12", "master", "Alpha", "x64", state);

    public static void Run()
    {
        AssertApplicabilityLevels();
        AssertFailClosedRules();
        AssertActionPolicy();
        AssertAuthorityIsData();

        Console.WriteLine("Evidence model policy (Exact/Partial/Unknown/Conflict, scope mismatch, Unknown never Apply, historical needs build, authority is data) PASS.");
    }

    private static void AssertApplicabilityLevels()
    {
        var full = new[] { C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.AppVersion, "1.02"), C(EvidenceScopeKind.PpuHash, Hash) };
        Assert(EvidencePolicy.Assess(full, Facts()).Applicability == ApplicabilityLevel.Exact,
            "Serial + APP_VERSION (numeric compare: 1.02 == 01.02) + PPU hash match is Exact.");
        Assert(EvidencePolicy.Assess([C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.AppVersion, "01.02")], Facts(hash: null)).Applicability == ApplicabilityLevel.Exact,
            "Matched Serial + APP_VERSION is Exact when these are all declared conditions; an undeclared runtime hash is irrelevant.");
        Assert(EvidencePolicy.Assess(full, Facts(hash: null)).Applicability == ApplicabilityLevel.Partial,
            "A declared PPU hash the runtime cannot prove leaves the evidence Partial.");
        Assert(EvidencePolicy.Assess([C(EvidenceScopeKind.Serial, "BLUS30001")], Facts()).Applicability == ApplicabilityLevel.Exact,
            "A matching Serial alone is Exact when no further scope conditions are declared.");
        Assert(EvidencePolicy.Assess([C(EvidenceScopeKind.Renderer, "OpenGL")], new EvidenceRuntimeFacts()).Applicability == ApplicabilityLevel.Unknown,
            "Nothing provable is Unknown.");
        Assert(EvidencePolicy.Assess(full, Facts(), conflict: true).Applicability == ApplicabilityLevel.Conflict,
            "Conflicting evidence is Conflict regardless of a full scope match.");

        var withBuild = full.Append2(C(EvidenceScopeKind.Rpcs3Build, "17000"));
        Assert(EvidencePolicy.Assess(withBuild, Facts(build: Build())).Applicability == ApplicabilityLevel.Exact,
            "A build condition matched by an Exact runtime identity keeps Exact.");
        Assert(EvidencePolicy.Assess(withBuild, Facts(build: Build(state: Rpcs3BuildIdentityState.Partial))).Applicability == ApplicabilityLevel.Partial &&
               EvidencePolicy.Assess(withBuild, Facts(build: Build(state: Rpcs3BuildIdentityState.Conflict))).Applicability == ApplicabilityLevel.Partial &&
               EvidencePolicy.Assess(withBuild, Facts()).Applicability == ApplicabilityLevel.Partial,
            "Partial, Conflict or missing runtime build never proves a build condition.");
        var commitOnly = EvidencePolicy.Assess([C(EvidenceScopeKind.Rpcs3Commit, "abcdef12")], Facts(build: Build()));
        Assert(commitOnly.Applicability == ApplicabilityLevel.Unknown && !commitOnly.ScopeMismatch,
            "A matching commit without a declared game identity is Unknown.");
        var commitWithIdentity = EvidencePolicy.Assess(
            [C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.Rpcs3Commit, "abcdef12")], Facts(build: Build()));
        Assert(commitWithIdentity.Applicability == ApplicabilityLevel.Exact && !commitWithIdentity.ScopeMismatch,
            "A matched identity and a matching commit prefix (>= 8 hex) satisfy all declared conditions.");
        var shortCommit = EvidencePolicy.Assess(
            [C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.Rpcs3Commit, "abc")], Facts(build: Build()));
        Assert(shortCommit.Applicability == ApplicabilityLevel.Unknown && shortCommit.ScopeMismatch &&
               EvidencePolicy.Decide(shortCommit, EvidenceAuthority.Official, false, true, true) == SettingAction.Blocked,
            "A commit shorter than 8 characters never matches and its scope mismatch blocks action.");
    }

    private static void AssertFailClosedRules()
    {
        var mismatch = EvidencePolicy.Assess([C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.AppVersion, "1.00")], Facts());
        Assert(mismatch.ScopeMismatch && mismatch.Applicability != ApplicabilityLevel.Exact,
            "A mismatching scope condition is reported and can never lift the level (APP_VERSION 1.00 vs 01.02).");
        Assert(EvidencePolicy.Assess([C(EvidenceScopeKind.Rpcs3Build, "17000")], Facts(build: Build(number: 17001))).ScopeMismatch,
            "A different runtime build is a scope mismatch.");
        Assert(EvidencePolicy.Assess([C(EvidenceScopeKind.Architecture, "ARM64")], Facts()).ScopeMismatch,
            "A different architecture is a scope mismatch.");
    }

    private static void AssertActionPolicy()
    {
        var exact = new EvidenceAssessment(ApplicabilityLevel.Exact, false, []);
        var matchedScope = EvidencePolicy.Assess(
            [C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.AppVersion, "01.02")], Facts(hash: null));
        var partial = EvidencePolicy.Assess(
            [C(EvidenceScopeKind.Serial, "BLUS30001"), C(EvidenceScopeKind.AppVersion, "01.02"), C(EvidenceScopeKind.PpuHash, Hash)], Facts(hash: null));
        var unknown = new EvidenceAssessment(ApplicabilityLevel.Unknown, false, []);
        var conflict = new EvidenceAssessment(ApplicabilityLevel.Conflict, false, []);
        var mismatch = new EvidenceAssessment(ApplicabilityLevel.Unknown, true, []);

        Assert(EvidencePolicy.Decide(exact, EvidenceAuthority.Wiki, false, false, true) == SettingAction.Apply,
            "Exact, current, deterministic evidence of Wiki authority is Apply.");
        Assert(EvidencePolicy.Decide(exact, EvidenceAuthority.Wiki, false, false, false) == SettingAction.Suggest,
            "Exact but not deterministic is Suggest, never Apply.");
        Assert(matchedScope.Applicability == ApplicabilityLevel.Exact &&
               EvidencePolicy.Decide(matchedScope, EvidenceAuthority.Wiki, false, false, true) == SettingAction.Apply &&
               EvidencePolicy.Decide(matchedScope, EvidenceAuthority.Wiki, false, false, false) == SettingAction.Suggest,
            "Fully matched declared scope is Exact: a deterministic value may Apply; a value requiring user choice is Suggest.");
        Assert(partial.Applicability == ApplicabilityLevel.Partial &&
               EvidencePolicy.Decide(partial, EvidenceAuthority.Wiki, false, false, true) == SettingAction.Inform,
            "An unknown declared hash keeps scope Partial and action Inform, even for a deterministic value.");
        Assert(EvidencePolicy.Decide(unknown, EvidenceAuthority.Official, false, true, true) == SettingAction.Inform,
            "Unknown never becomes Apply or Suggest, even with Official authority.");
        Assert(EvidencePolicy.Decide(conflict, EvidenceAuthority.Official, false, true, true) == SettingAction.Blocked &&
               EvidencePolicy.Decide(mismatch, EvidenceAuthority.Official, false, true, true) == SettingAction.Blocked,
            "Conflict and scope mismatch are Blocked.");
        Assert(EvidencePolicy.Decide(exact, EvidenceAuthority.Wiki, true, false, true) == SettingAction.Inform,
            "Historical evidence with an unproven build applicability is Inform, even when everything else is Exact.");
        Assert(EvidencePolicy.Decide(exact, EvidenceAuthority.Wiki, true, true, true) == SettingAction.Apply,
            "Historical evidence with a proven build applicability follows the normal rules.");
    }

    private static void AssertAuthorityIsData()
    {
        var exact = new EvidenceAssessment(ApplicabilityLevel.Exact, false, []);
        Assert(EvidencePolicy.Decide(exact, EvidenceAuthority.Community, false, true, true) == SettingAction.Inform,
            "Community authority never reaches Suggest or Apply on its own.");
        Assert(EvidencePolicy.Rank(EvidenceAuthority.Official) > EvidencePolicy.Rank(EvidenceAuthority.Developer) &&
               EvidencePolicy.Rank(EvidenceAuthority.Developer) > EvidencePolicy.Rank(EvidenceAuthority.Wiki) &&
               EvidencePolicy.Rank(EvidenceAuthority.Wiki) > EvidencePolicy.Rank(EvidenceAuthority.Community),
            "Authority is ranked by provenance through policy, not by enum declaration order or evidence kind.");
        foreach (var authority in new[] { EvidenceAuthority.Developer, EvidenceAuthority.Community })
        {
            var regression = new SettingDecision(
                EvidencePolicy.Decide(exact, authority, false, true, true),
                exact.Applicability, EvidenceKind.RegressionRecord, authority, []);
            Assert(regression.Kind == EvidenceKind.RegressionRecord && regression.Authority == authority &&
                   regression.Action == (authority == EvidenceAuthority.Developer ? SettingAction.Apply : SettingAction.Inform),
                "A regression record keeps its actual source authority; its kind alone cannot authorize Apply.");
        }
        Assert(Enum.GetNames<RemovalReason>().Length == 9 && Enum.IsDefined(RemovalReason.ClaimedFixed) && Enum.IsDefined(RemovalReason.VerifiedFixedUpstream),
            "ClaimedFixed and VerifiedFixedUpstream are distinct removal reasons.");
    }

    private static EvidenceScopeCondition[] Append2(this EvidenceScopeCondition[] source, EvidenceScopeCondition extra)
    {
        var list = new List<EvidenceScopeCondition>(source) { extra };
        return [.. list];
    }
}
