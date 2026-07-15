using System;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class PatPlnr
{
    public Mdl.PatchApplyPlan Build(
        Rpcs3InstallDescriptor install,
        IEnumerable<Mdl.PatMatch> checkedMatches,
        bool expertMode)
    {
        var changes = checkedMatches
            .Where(match => PatPol.CanApply(
                match,
                expertMode))
            .Select(Try)
            .Where(change => change is not null)
            .Cast<PatChg>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatTitle}\0{change.GameName}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new Mdl.PatchApplyPlan(
            install.PatCfg,
            changes);
    }

    public Mdl.PatchApplyPlan Build2(
        Rpcs3InstallDescriptor install,
        IEnumerable<Mdl.PatMatch> checkedMatches)
    {
        var changes = checkedMatches
            .Where(PatPol.CanPat)
            .Select(Build3)
            .Where(change => change is not null)
            .Cast<PatChg>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatTitle}\0{change.GameName}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new Mdl.PatchApplyPlan(
            install.PatCfg,
            changes);
    }

    public PatChg? Build3(
        Mdl.PatMatch match)
    {
        return Try(match);
    }

    private static PatChg? Try(
        Mdl.PatMatch match)
    {
        var target = GetTgt(match);

        if (target is null)
        {
            return null;
        }

        var targetVersion = match.TgtVer
            ?? target.Versions.FirstOrDefault(version =>
                PatVer.Equals(
                    version,
                    match.MatVer))
            ?? (target.Versions.Any(PatVer.IsAll) ||
                target.Versions.Count == 0
                    ? PatVer.All
                    : null);

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            return null;
        }

        return new PatChg(
            HashKey: ToHash(match.Patch),
            PatTitle: match.Patch.Title,
            GameName: match.TgtName ?? target.Title,
            Serial: match.TgtSer ?? target.Serial,
            Version: targetVersion,
            Risk: match.Patch.Risk);
    }

    private static R3PatTgt? GetTgt(
        Mdl.PatMatch match)
    {
        if (!string.IsNullOrWhiteSpace(match.TgtSer))
        {
            return match.Patch.GameTgt.FirstOrDefault(candidate =>
                candidate.Serial.Equals(
                    match.TgtSer,
                    StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(match.TgtName) ||
                 candidate.Title.Equals(
                     match.TgtName,
                     StringComparison.OrdinalIgnoreCase)));
        }

        return match.Patch.GameTgt.FirstOrDefault(candidate =>
            (!string.IsNullOrWhiteSpace(match.MatSer) &&
             candidate.Serial.Equals(
                 match.MatSer,
                 StringComparison.OrdinalIgnoreCase)) ||
            PatVer.IsAll(candidate.Serial));
    }

    private static string ToHash(Rpcs3PatchEntry patch)
    {
        return patch.RunKey;
    }
}

#pragma warning restore CA1822, IDE0130
