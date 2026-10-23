using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class CanaryArtemisPolicy
{
    public static IReadOnlyList<PatMatch> Apply(
        IReadOnlyList<PatMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var canary = matches
            .Where(match => match.Patch.Class == PatClass.Canary)
            .ToArray();

        return matches
            .Select(match => HasApplicableEquivalent(match, canary)
                ? BlockEnable(match)
                : match)
            .ToArray();
    }

    public static bool HasApplicableEquivalent(
        PatMatch artemis,
        IEnumerable<PatMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(artemis);
        ArgumentNullException.ThrowIfNull(matches);

        if (!IsArtemis(artemis.Patch.Source))
        {
            return false;
        }

        var decision = PatAct.Decide(artemis);
        if (decision.Code == PatchCompatibilityReasonCode.CanaryEquivalent)
        {
            artemis = artemis with { Cmp = null };
            decision = PatAct.Decide(artemis);
        }

        if (!decision.CanOn ||
            artemis.Patch.Dependencies.Count != 0)
        {
            return false;
        }

        return matches.Any(canary =>
            canary.Patch.Class == PatClass.Canary &&
            PatAct.Decide(canary).CanOn &&
            canary.Status == MatStat.Match &&
            canary.Patch.Dependencies.Count == 0 &&
            SameRuntimeScope(artemis, canary) &&
            PatchWriteSemantics.Classify(
                artemis.Patch.WritePrint,
                canary.Patch.WritePrint) ==
                PatchWriteRelation.SemanticallyEquivalent);
    }

    private static PatMatch BlockEnable(PatMatch match)
    {
        var current = PatAct.Decide(match);
        return match with
        {
            Reason = "An applicable Canary patch has the same proven effective writes; use the Canary version.",
            Cmp = new PatchCompatibilityDecision(
                PatchCompatibilityVerdict.Blocked,
                PatchCompatibilityReasonCode.CanaryEquivalent,
                CanOn: false,
                CanOff: current.CanOff,
                UseGuard: false,
                RunKey: current.RunKey)
        };
    }

    private static bool SameRuntimeScope(
        PatMatch artemis,
        PatMatch canary)
    {
        if (!ExactSerial(artemis, out var artemisSerial) ||
            !ExactSerial(canary, out var canarySerial) ||
            !artemisSerial.Equals(
                canarySerial,
                StringComparison.OrdinalIgnoreCase) ||
            !ExactVersion(artemis, out var artemisVersion) ||
             !ExactVersion(canary, out var canaryVersion) ||
             !artemisVersion.Equals(
                 canaryVersion,
                 StringComparison.OrdinalIgnoreCase) ||
             !RunKey.TryParse(artemis.Patch.RunKey, out var artemisDeclaredKey) ||
             !RunKey.TryParse(canary.Patch.RunKey, out var canaryDeclaredKey) ||
             !RunKey.TryParse(artemis.EffectiveRunKey, out var artemisKey) ||
             !RunKey.TryParse(canary.EffectiveRunKey, out var canaryKey))
        {
            return false;
        }

        return SameExecutableScope(artemisDeclaredKey, canaryDeclaredKey) &&
               SameExecutableScope(artemisKey, canaryKey);
    }

    private static bool SameExecutableScope(
        KeyInf left,
        KeyInf right)
    {
        return left.Kind == right.Kind &&
               left.NormHash.Equals(
                   right.NormHash,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.SerScope,
                   right.SerScope,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExactSerial(
        PatMatch match,
        out string serial)
    {
        serial = match.MatSer?.Trim() ?? string.Empty;
        var target = match.TgtSer?.Trim();
        return serial.Length != 0 &&
               !string.IsNullOrWhiteSpace(target) &&
               !PatVer.IsAll(target) &&
               serial.Equals(target, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExactVersion(
        PatMatch match,
        out string version)
    {
        version = PatVer.Norm(match.MatVer) ?? string.Empty;
        var target = PatVer.Norm(match.TgtVer);
        return version.Length != 0 &&
               target is not null &&
               (PatVer.IsAll(target) ||
                PatVer.Equals(version, target));
    }

    // ManagedArtemis is the resident R3Config-owned <SERIAL>_patch.yml copy of
    // the Artemis channel; the catalog merge shows it instead of the bundled
    // row once a definition has been prepared, so it must meet the same rule.
    private static bool IsArtemis(PatSrc source)
    {
        return source is PatSrc.Artemis or PatSrc.ManagedArtemis;
    }
}

#pragma warning restore IDE0130
