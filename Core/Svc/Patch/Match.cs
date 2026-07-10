using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchMatchService
{
    public IReadOnlyList<Rpcs3PatchMatchResult> Match(
        Rpcs3PatchCatalog catalog,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        var gameList = games.ToArray();
        return catalog.Entries.Select(entry => Match(entry, gameList)).ToArray();
    }

    public Rpcs3PatchMatchResult Match(
        Rpcs3PatchEntry patch,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        var gameList = games.ToArray();
        var candidates = patch.GameTargets
            .SelectMany((target, targetIndex) => gameList
                .Where(game => SerialMatches(target.Serial, game.Serial))
                .Select(game => EvaluateCandidate(patch, target, game, targetIndex)))
            .OrderBy(candidate => StatusRank(candidate.Status))
            .ThenByDescending(candidate => !Rpcs3PatchVersion.IsAll(candidate.Target.Serial))
            .ThenByDescending(candidate => !Rpcs3PatchVersion.IsAll(candidate.TargetVersion))
            .ThenBy(candidate => candidate.TargetIndex)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new Rpcs3PatchMatchResult(
                Patch: patch,
                Status: patch.GameTargets.Count == 0 ? PatchMatchStatus.NoMetadata : PatchMatchStatus.SerialMismatch,
                MatchedSerial: null,
                MatchedVersion: null,
                HasHashMetadata: false,
                MatchReason: patch.GameTargets.Count == 0
                    ? "Patch has no game target metadata."
                    : "No installed game serial matched this patch target.");
        }

        var best = candidates[0];
        return new Rpcs3PatchMatchResult(
            Patch: patch,
            Status: best.Status,
            MatchedSerial: best.Game.Serial,
            MatchedVersion: Rpcs3PatchVersion.Normalize(best.Game.Version),
            HasHashMetadata: best.HasHashMetadata,
            MatchReason: BuildReason(best),
            MatchedTargetTitle: best.Target.Title,
            MatchedTargetSerial: best.Target.Serial,
            MatchedTargetVersion: best.TargetVersion);
    }

    private static Candidate EvaluateCandidate(
        Rpcs3PatchEntry patch,
        Rpcs3PatchGameTarget target,
        Rpcs3GameDescriptor game,
        int targetIndex)
    {
        var installedVersion = Rpcs3PatchVersion.Normalize(game.Version);
        var targetVersions = target.Versions
            .Select(Rpcs3PatchVersion.Normalize)
            .Where(version => version is not null)
            .Select(version => version!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? matchedTargetVersion;
        PatchMatchStatus status;

        if (targetVersions.Length == 0 || targetVersions.Any(Rpcs3PatchVersion.IsAll))
        {
            matchedTargetVersion = Rpcs3PatchVersion.All;
            status = DetermineHashStatus(patch, game);
        }
        else if (installedVersion is null)
        {
            matchedTargetVersion = targetVersions[0];
            status = PatchMatchStatus.VersionUnknown;
        }
        else if (targetVersions.FirstOrDefault(version => Rpcs3PatchVersion.Equals(version, installedVersion)) is { } exactVersion)
        {
            matchedTargetVersion = exactVersion;
            status = DetermineHashStatus(patch, game);
        }
        else
        {
            matchedTargetVersion = targetVersions[0];
            status = PatchMatchStatus.VersionMismatch;
        }

        return new Candidate(
            patch,
            target,
            game,
            status,
            matchedTargetVersion,
            HasRequiredHashMetadata(patch, game),
            targetIndex);
    }

    private static PatchMatchStatus DetermineHashStatus(Rpcs3PatchEntry patch, Rpcs3GameDescriptor game)
    {
        if (!HasRequiredHashMetadata(patch, game))
        {
            return PatchMatchStatus.SerialVersionMatchHashUnknown;
        }

        return HashMatches(patch, game)
            ? PatchMatchStatus.ExactMatch
            : PatchMatchStatus.HashMismatch;
    }

    private static bool HasRequiredHashMetadata(Rpcs3PatchEntry patch, Rpcs3GameDescriptor game)
    {
        return patch.HashKind switch
        {
            PatchHashKind.Ppu => game.AllPpuHashes.Count > 0,
            PatchHashKind.Spu => game.AllSpuHashes.Count > 0,
            PatchHashKind.Prx or PatchHashKind.Ovl => false,
            _ => true
        };
    }

    private static bool HashMatches(Rpcs3PatchEntry patch, Rpcs3GameDescriptor game)
    {
        return patch.HashKind switch
        {
            PatchHashKind.Ppu => game.AllPpuHashes.Any(hash => hash.Equals(patch.Hash, StringComparison.OrdinalIgnoreCase)),
            PatchHashKind.Spu => game.AllSpuHashes.Any(hash => hash.Equals(patch.Hash, StringComparison.OrdinalIgnoreCase)),
            PatchHashKind.Prx or PatchHashKind.Ovl => false,
            _ => true
        };
    }

    private static bool SerialMatches(string targetSerial, string gameSerial)
    {
        return Rpcs3PatchVersion.IsAll(targetSerial) ||
               targetSerial.Equals(gameSerial, StringComparison.OrdinalIgnoreCase);
    }

    private static int StatusRank(PatchMatchStatus status)
    {
        return status switch
        {
            PatchMatchStatus.ExactMatch => 0,
            PatchMatchStatus.SerialVersionMatchHashUnknown => 1,
            PatchMatchStatus.VersionUnknown => 2,
            PatchMatchStatus.HashMismatch => 3,
            PatchMatchStatus.VersionMismatch => 4,
            PatchMatchStatus.SerialMismatch => 5,
            _ => 6
        };
    }

    private static string BuildReason(Candidate candidate)
    {
        var installedVersion = Rpcs3PatchVersion.Normalize(candidate.Game.Version) ?? "unknown";
        var requiredVersion = candidate.TargetVersion ?? Rpcs3PatchVersion.All;

        return candidate.Status switch
        {
            PatchMatchStatus.ExactMatch =>
                $"Serial {candidate.Game.Serial}, installed version {installedVersion}, target version {requiredVersion}, and {candidate.Patch.HashKind} hash match.",
            PatchMatchStatus.SerialVersionMatchHashUnknown =>
                $"Serial {candidate.Game.Serial} and version match, but the required {candidate.Patch.HashKind} hash is unavailable.",
            PatchMatchStatus.VersionUnknown =>
                $"Serial {candidate.Game.Serial} matches, but the installed game version is unknown; target version is {requiredVersion}.",
            PatchMatchStatus.VersionMismatch =>
                $"Serial {candidate.Game.Serial} matches, but installed version {installedVersion} does not match target version {requiredVersion}.",
            PatchMatchStatus.HashMismatch =>
                $"Serial {candidate.Game.Serial} and version match, but the required {candidate.Patch.HashKind} hash does not match.",
            _ => "Patch metadata did not match this game."
        };
    }

    private sealed record Candidate(
        Rpcs3PatchEntry Patch,
        Rpcs3PatchGameTarget Target,
        Rpcs3GameDescriptor Game,
        PatchMatchStatus Status,
        string? TargetVersion,
        bool HasHashMetadata,
        int TargetIndex);
}
