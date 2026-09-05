using System;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class PatchGroupConflictException : InvalidOperationException
{
    public PatchGroupConflictException()
        : this(string.Empty, [])
    {
    }

    public PatchGroupConflictException(string? message)
        : base(message)
    {
        Group = string.Empty;
        Titles = [];
    }

    public PatchGroupConflictException(
        string? message,
        Exception? innerException)
        : base(message, innerException)
    {
        Group = string.Empty;
        Titles = [];
    }

    public PatchGroupConflictException(
        string group,
        IReadOnlyList<string> titles,
        string? selectedTitle = null,
        string? enabledTitle = null)
        : base(BuildMessage(group, titles))
    {
        Group = group;
        Titles = titles;
        SelectedTitle = selectedTitle;
        EnabledTitle = enabledTitle;
    }

    public string Group { get; }

    public IReadOnlyList<string> Titles { get; }

    public string? SelectedTitle { get; }

    public string? EnabledTitle { get; }

    public bool HasEnabledPeer =>
        !string.IsNullOrWhiteSpace(SelectedTitle) &&
        !string.IsNullOrWhiteSpace(EnabledTitle);

    private static string BuildMessage(
        string group,
        IEnumerable<string> titles)
    {
        var options = titles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

        return
            $"Patch group '{group}' is mutually exclusive for this executable hash. " +
            $"Select only one of: {string.Join(", ", options)}.";
    }
}

public sealed class PatPlnr
{
    public Mdl.PatchApplyPlan Build(
        Rpcs3InstallDescriptor install,
        IEnumerable<Mdl.PatMatch> checkedMatches,
        bool expertMode,
        IEnumerable<Mdl.PatMatch>? enabledMatches = null)
    {
        var applicable = checkedMatches
            .Where(match => PatPol.CanApply(
                match,
                expertMode))
            .ToArray();

        PatDepVal.ValidateEnable(
            applicable,
            enabledMatches?.ToArray() ?? []);

        EnsureNoGroupConflict(
            applicable,
            enabledMatches?.ToArray() ?? []);

        var changes = applicable
            .Select(Try)
            .Where(change => change is not null)
            .Cast<PatChg>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatTitle}\0{change.GameName}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var state = PatchFileTransactionService.CaptureExpectedState(
            install.PatCfg,
            R3Limits.PatCfgBy,
            "patch_config.yml");

        return new Mdl.PatchApplyPlan(
            install.PatCfg,
            changes,
            state.Exists,
            state.Sha256);
    }

    public Mdl.PatchApplyPlan Build2(
        Rpcs3InstallDescriptor install,
        IEnumerable<Mdl.PatMatch> checkedMatches,
        IEnumerable<Mdl.PatMatch>? enabledMatches = null)
    {
        var disabling = checkedMatches
            .Where(PatPol.CanPat)
            .ToArray();

        if (enabledMatches is not null)
        {
            PatDepVal.ValidateDisable(
                disabling,
                enabledMatches.ToArray());
        }

        var changes = disabling
            .Select(Build3)
            .Where(change => change is not null)
            .Cast<PatChg>()
            .GroupBy(
                change => $"{change.HashKey}\0{change.PatTitle}\0{change.GameName}\0{change.Serial}\0{change.Version}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var state = PatchFileTransactionService.CaptureExpectedState(
            install.PatCfg,
            R3Limits.PatCfgBy,
            "patch_config.yml");

        return new Mdl.PatchApplyPlan(
            install.PatCfg,
            changes,
            state.Exists,
            state.Sha256);
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
            HashKey: match.EffectiveRunKey,
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

    private static void EnsureNoGroupConflict(
        IReadOnlyList<Mdl.PatMatch> matches,
        IReadOnlyList<Mdl.PatMatch> enabledMatches)
    {
        var conflict = matches
            .Where(match =>
                !string.IsNullOrWhiteSpace(match.Patch.Group))
            .GroupBy(
                match =>
                    $"{match.EffectiveRunKey}\0{match.Patch.Group!.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Group = group.First().Patch.Group!.Trim(),
                Titles = group
                    .Select(match => match.Patch.Title)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .FirstOrDefault(group => group.Titles.Length > 1);

        if (conflict is null)
        {
            foreach (var selected in matches.Where(match =>
                         !string.IsNullOrWhiteSpace(match.Patch.Group)))
            {
                var enabledPeer = enabledMatches.FirstOrDefault(enabled =>
                    SameGroup(selected, enabled) &&
                    !enabled.Patch.Title.Equals(
                        selected.Patch.Title,
                        StringComparison.OrdinalIgnoreCase));

                if (enabledPeer is null)
                {
                    continue;
                }

                throw new PatchGroupConflictException(
                    selected.Patch.Group!.Trim(),
                    [selected.Patch.Title, enabledPeer.Patch.Title],
                    selected.Patch.Title,
                    enabledPeer.Patch.Title);
            }

            return;
        }

        throw new PatchGroupConflictException(
            conflict.Group,
            conflict.Titles);
    }

    private static bool SameGroup(
        Mdl.PatMatch left,
        Mdl.PatMatch right)
    {
        return left.EffectiveRunKey.Equals(
                   right.EffectiveRunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(left.Patch.Group) &&
               !string.IsNullOrWhiteSpace(right.Patch.Group) &&
               left.Patch.Group.Trim().Equals(
                   right.Patch.Group.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

}

#pragma warning restore CA1822, IDE0130
