using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public enum CanaryRuntimeBindingKind
{
    None,
    Exact,
    Rebound,
    Ambiguous,
    Suppressed
}

public sealed record CanaryRuntimeBindingDecision(
    CanaryRuntimeBindingKind Kind,
    string? EffectiveRunKey = null,
    string? Detail = null,
    Rpcs3PatchEntry? Patch = null)
{
    public static CanaryRuntimeBindingDecision None { get; } =
        new(CanaryRuntimeBindingKind.None);
}

public sealed class CanaryRuntimeBindingService
{
    public CanaryRuntimeBindingDecision Resolve(
        PatCat catalog,
        Rpcs3PatchEntry patch,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(game);

        if (!IsPpuCanary(patch))
        {
            return CanaryRuntimeBindingDecision.None;
        }

        var localHashes = game.HCanPpuList;
        if (localHashes.Count == 0)
        {
            return CanaryRuntimeBindingDecision.None;
        }

        var matchingTargets = MatchingTargets(
            patch,
            game);
        if (matchingTargets.Length == 0)
        {
            return ShouldSuppressExactSibling(
                catalog,
                patch,
                game,
                localHashes)
                ? new CanaryRuntimeBindingDecision(
                    CanaryRuntimeBindingKind.Suppressed)
                : CanaryRuntimeBindingDecision.None;
        }

        var logicalTitles = matchingTargets
            .Select(target => target.Title.Trim())
            .Where(title => title.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var logicalVariants = catalog.Entries
            .Where(IsPpuCanary)
            .Where(candidate => candidate.Title.Equals(
                patch.Title,
                StringComparison.OrdinalIgnoreCase))
            .Where(candidate => candidate.GameTgt.Any(target =>
                logicalTitles.Contains(target.Title.Trim())))
            .ToArray();

        var exactVariants = logicalVariants
            .Where(candidate =>
                candidate.NormHash is { } hash &&
                localHashes.Contains(
                    hash,
                    StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (exactVariants.Length > 0)
        {
            var exactIdentities = exactVariants
                .Select(candidate => string.Join(
                    "\0",
                    candidate.RunKey,
                    DefinitionIdentity(candidate)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (exactIdentities.Length != 1)
            {
                return new CanaryRuntimeBindingDecision(
                    CanaryRuntimeBindingKind.Ambiguous,
                    Detail:
                        "Multiple different Canary definitions match the saved local PPU fingerprint for this game title.");
            }

            var selected = exactVariants
                .OrderBy(SourceRank)
                .ThenBy(candidate => candidate.Source)
                .First();
            var retargeted = Retarget(
                selected,
                matchingTargets[0],
                game);

            return new CanaryRuntimeBindingDecision(
                CanaryRuntimeBindingKind.Exact,
                selected.RunKey,
                Patch: retargeted);
        }

        var targetedVariants = logicalVariants
            .Where(candidate => TargetsGame(
                candidate,
                game))
            .ToArray();
        var distinctDefinitions = targetedVariants
            .Select(DefinitionIdentity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctDefinitions.Length != 1)
        {
            return new CanaryRuntimeBindingDecision(
                CanaryRuntimeBindingKind.Ambiguous,
                Detail:
                    "Multiple different Canary definitions target this game/version, so the local PPU fingerprint cannot be rebound automatically.");
        }

        var normalizedLocalHashes = localHashes
            .Select(hash =>
                RunKey.TryNorm(
                    HashKind.Ppu,
                    hash,
                    out var normalized)
                    ? normalized
                    : null)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedLocalHashes.Length != 1)
        {
            return new CanaryRuntimeBindingDecision(
                CanaryRuntimeBindingKind.Ambiguous,
                Detail:
                    "The selected game produced multiple local PPU executable hashes, so a single Canary runtime key cannot be chosen safely.");
        }

        return new CanaryRuntimeBindingDecision(
            CanaryRuntimeBindingKind.Rebound,
            RunKey.Build(
                HashKind.Ppu,
                normalizedLocalHashes[0],
                patch.KeySer));
    }

    private static int SourceRank(Rpcs3PatchEntry patch)
    {
        return patch.Source == PatSrc.Can ? 1 : 0;
    }

    private static Rpcs3PatchEntry Retarget(
        Rpcs3PatchEntry patch,
        R3PatTgt matchedTarget,
        Rpcs3GameDescriptor game)
    {
        var version = PatVer.Norm(game.Version);
        IReadOnlyList<string> versions = version is null
            ? matchedTarget.Versions
            : [version];

        return patch with
        {
            GameTgt =
            [
                new R3PatTgt(
                    matchedTarget.Title,
                    game.Serial,
                    versions)
            ]
        };
    }

    private static bool ShouldSuppressExactSibling(
        PatCat catalog,
        Rpcs3PatchEntry patch,
        Rpcs3GameDescriptor game,
        IReadOnlyList<string> localHashes)
    {
        if (patch.NormHash is not { } hash ||
            !localHashes.Contains(
                hash,
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var siblingTitles = patch.GameTgt
            .Select(target => target.Title.Trim())
            .Where(title => title.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return catalog.Entries.Any(candidate =>
            !ReferenceEquals(candidate, patch) &&
            IsPpuCanary(candidate) &&
            candidate.Title.Equals(
                patch.Title,
                StringComparison.OrdinalIgnoreCase) &&
            TargetsGame(candidate, game) &&
            candidate.GameTgt.Any(target =>
                siblingTitles.Contains(target.Title.Trim())));
    }

    private static R3PatTgt[] MatchingTargets(
        Rpcs3PatchEntry patch,
        Rpcs3GameDescriptor game)
    {
        return patch.GameTgt
            .Where(target =>
                (PatVer.IsAll(target.Serial) ||
                 target.Serial.Equals(
                     game.Serial,
                     StringComparison.OrdinalIgnoreCase)) &&
                VersionMatches(target, game.Version))
            .ToArray();
    }

    private static bool IsPpuCanary(
        Rpcs3PatchEntry patch)
    {
        return patch.Class == PatClass.Canary &&
               patch.HashKind == HashKind.Ppu &&
               patch.NormHash is not null;
    }

    private static bool TargetsGame(
        Rpcs3PatchEntry patch,
        Rpcs3GameDescriptor game)
    {
        return patch.GameTgt.Any(target =>
            (PatVer.IsAll(target.Serial) ||
             target.Serial.Equals(
                 game.Serial,
                 StringComparison.OrdinalIgnoreCase)) &&
            VersionMatches(target, game.Version));
    }

    private static bool VersionMatches(
        R3PatTgt target,
        string? gameVersion)
    {
        if (target.Versions.Count == 0 ||
            target.Versions.Any(PatVer.IsAll))
        {
            return true;
        }

        return target.Versions.Any(version =>
            PatVer.Equals(version, gameVersion));
    }

    private static string DefinitionIdentity(
        Rpcs3PatchEntry patch)
    {
        if (!string.IsNullOrWhiteSpace(patch.DefPrint))
        {
            return patch.DefPrint.Trim();
        }

        return string.Join(
            "\0",
            patch.RunKey,
            patch.PatVer ?? string.Empty,
            patch.Title);
    }
}

#pragma warning restore CA1822, IDE0130
