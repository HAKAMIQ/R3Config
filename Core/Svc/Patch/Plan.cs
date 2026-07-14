using System;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class PatchApplyPlanner
{
    public PatchApplyPlan CreatePlan(
        Rpcs3InstallDescriptor install,
        IEnumerable<Rpcs3PatchMatchResult> checkedMatches,
        bool expertMode)
    {
        var changes = checkedMatches
            .Where(match => PatchRiskPolicy.CanApplyCheckedPatch(
                match,
                expertMode))
            .Select(TryCreateChange)
            .Where(change => change is not null)
            .Cast<PatchApplyChange>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatchTitle}\0{change.GameTitle}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new PatchApplyPlan(
            install.PatchConfigPath,
            changes);
    }

    public PatchApplyPlan CreateDisablePlan(
        Rpcs3InstallDescriptor install,
        IEnumerable<Rpcs3PatchMatchResult> checkedMatches)
    {
        var changes = checkedMatches
            .Where(PatchRiskPolicy.CanDisableCheckedPatch)
            .Select(CreateChangeForMatch)
            .Where(change => change is not null)
            .Cast<PatchApplyChange>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatchTitle}\0{change.GameTitle}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new PatchApplyPlan(
            install.PatchConfigPath,
            changes);
    }

    public PatchApplyChange? CreateChangeForMatch(
        Rpcs3PatchMatchResult match)
    {
        return TryCreateChange(match);
    }

    private static PatchApplyChange? TryCreateChange(
        Rpcs3PatchMatchResult match)
    {
        var target = ResolveTarget(match);

        if (target is null)
        {
            return null;
        }

        var targetVersion = match.MatchedTargetVersion
            ?? target.Versions.FirstOrDefault(version =>
                Rpcs3PatchVersion.Equals(
                    version,
                    match.MatchedVersion))
            ?? (target.Versions.Any(Rpcs3PatchVersion.IsAll) ||
                target.Versions.Count == 0
                    ? Rpcs3PatchVersion.All
                    : null);

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            return null;
        }

        return new PatchApplyChange(
            HashKey: ToHashKey(match.Patch),
            PatchTitle: match.Patch.Title,
            GameTitle: match.MatchedTargetTitle ?? target.Title,
            Serial: match.MatchedTargetSerial ?? target.Serial,
            Version: targetVersion,
            Risk: match.Patch.Risk);
    }

    private static Rpcs3PatchGameTarget? ResolveTarget(
        Rpcs3PatchMatchResult match)
    {
        if (!string.IsNullOrWhiteSpace(match.MatchedTargetSerial))
        {
            return match.Patch.GameTargets.FirstOrDefault(candidate =>
                candidate.Serial.Equals(
                    match.MatchedTargetSerial,
                    StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(match.MatchedTargetTitle) ||
                 candidate.Title.Equals(
                     match.MatchedTargetTitle,
                     StringComparison.OrdinalIgnoreCase)));
        }

        return match.Patch.GameTargets.FirstOrDefault(candidate =>
            (!string.IsNullOrWhiteSpace(match.MatchedSerial) &&
             candidate.Serial.Equals(
                 match.MatchedSerial,
                 StringComparison.OrdinalIgnoreCase)) ||
            Rpcs3PatchVersion.IsAll(candidate.Serial));
    }

    private static string ToHashKey(Rpcs3PatchEntry patch)
    {
        return patch.RuntimeHashKey;
    }
}

#pragma warning restore CA1822, IDE0130
