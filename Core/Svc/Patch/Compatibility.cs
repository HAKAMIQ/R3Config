using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

/// <summary>
/// Canonical compatibility boundary for official, Canary, imported and cheat
/// definitions.  It mirrors RPCS3's order: runtime key, title id, app version,
/// enabled state.  Callers must consume the attached decision rather than
/// inventing source-specific compatibility rules.
/// </summary>
public sealed class PatchCompatibilityService
{
    public IReadOnlyList<Rpcs3PatchMatchResult> Evaluate(
        Rpcs3PatchCatalog catalog,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        var gameList = games.ToArray();
        return catalog.Entries
            .Select(entry => Evaluate(entry, gameList))
            .ToArray();
    }

    public Rpcs3PatchMatchResult Evaluate(
        Rpcs3PatchEntry patch,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        var gameList = games.ToArray();
        var runtimeKeyValid = Rpcs3PatchRuntimeKey.TryParse(
            patch.RuntimeHashKey,
            out _);

        var candidates = patch.GameTargets
            .SelectMany((target, targetIndex) => gameList
                .Where(game => SerialMatches(target.Serial, game.Serial))
                .Select(game => EvaluateCandidate(
                    patch,
                    target,
                    game,
                    targetIndex,
                    runtimeKeyValid)))
            .OrderBy(candidate => StatusRank(candidate.Status))
            .ThenByDescending(candidate =>
                !Rpcs3PatchVersion.IsAll(candidate.Target.Serial))
            .ThenByDescending(candidate =>
                !Rpcs3PatchVersion.IsAll(candidate.TargetVersion))
            .ThenBy(candidate => candidate.TargetIndex)
            .ToArray();

        if (candidates.Length == 0)
        {
            var status = patch.GameTargets.Count == 0
                ? PatchMatchStatus.NoMetadata
                : PatchMatchStatus.SerialMismatch;

            var decision = CreateDecision(
                patch,
                status,
                matchedSerial: null,
                matchedTargetSerial: null,
                matchedTargetVersion: null,
                runtimeKeyValid,
                runtimeKeySerialMismatch: false);

            return new Rpcs3PatchMatchResult(
                Patch: patch,
                Status: status,
                MatchedSerial: null,
                MatchedVersion: null,
                HasHashMetadata: false,
                MatchReason: patch.GameTargets.Count == 0
                    ? "Patch has no game target metadata."
                    : "No installed game serial matched this patch target.",
                Compatibility: decision);
        }

        var best = candidates[0];
        var compatibility = CreateDecision(
            patch,
            best.Status,
            best.Game.Serial,
            best.Target.Serial,
            best.TargetVersion,
            runtimeKeyValid,
            best.RuntimeKeySerialMismatch);

        return new Rpcs3PatchMatchResult(
            Patch: patch,
            Status: best.Status,
            MatchedSerial: best.Game.Serial,
            MatchedVersion: Rpcs3PatchVersion.Normalize(best.Game.Version),
            HasHashMetadata: best.HasHashMetadata,
            MatchReason: BuildReason(best, compatibility),
            MatchedTargetTitle: best.Target.Title,
            MatchedTargetSerial: best.Target.Serial,
            MatchedTargetVersion: best.TargetVersion,
            DetectedHashes: best.Game.HashesFor(patch.HashKind),
            HashEvidenceSource: best.Game.HashSource,
            HashEvidenceMessage: best.Game.HashMessage,
            HashEvidenceKind: best.Game.HashEvidenceKind,
            HashEvidenceTimestampUtc: best.Game.HashEvidenceTimestampUtc,
            Compatibility: compatibility);
    }

    public PatchCompatibilityDecision Decide(
        Rpcs3PatchMatchResult match)
    {
        // Re-evaluate from immutable match facts instead of trusting a copied
        // decision. Record `with` expressions are used by merge/migration code
        // and can otherwise leave a stale verdict attached to changed source
        // or runtime-key metadata.
        return CreateDecision(
            match.Patch,
            match.Status,
            match.MatchedSerial,
            match.MatchedTargetSerial,
            match.MatchedTargetVersion,
            Rpcs3PatchRuntimeKey.TryParse(
                match.Patch.RuntimeHashKey,
                out _),
            RuntimeKeySerialMismatch(
                match.Patch,
                match.MatchedSerial));
    }

    private static Candidate EvaluateCandidate(
        Rpcs3PatchEntry patch,
        Rpcs3PatchGameTarget target,
        Rpcs3GameDescriptor game,
        int targetIndex,
        bool runtimeKeyValid)
    {
        var installedVersion = Rpcs3PatchVersion.Normalize(game.Version);
        var targetVersions = target.Versions
            .Select(Rpcs3PatchVersion.Normalize)
            .Where(version => version is not null)
            .Select(version => version!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var runtimeKeySerialMismatch = RuntimeKeySerialMismatch(
            patch,
            game.Serial);

        string? matchedTargetVersion;
        PatchMatchStatus status;

        if (runtimeKeySerialMismatch)
        {
            matchedTargetVersion = targetVersions.FirstOrDefault()
                ?? Rpcs3PatchVersion.All;
            status = PatchMatchStatus.SerialMismatch;
        }
        else if (targetVersions.Length == 0 ||
                 targetVersions.Any(Rpcs3PatchVersion.IsAll))
        {
            matchedTargetVersion = Rpcs3PatchVersion.All;
            status = DetermineHashStatus(
                patch,
                game,
                runtimeKeyValid);
        }
        else if (installedVersion is null)
        {
            matchedTargetVersion = targetVersions[0];
            status = PatchMatchStatus.VersionUnknown;
        }
        else if (targetVersions.FirstOrDefault(version =>
                     Rpcs3PatchVersion.Equals(
                         version,
                         installedVersion)) is { } exactVersion)
        {
            matchedTargetVersion = exactVersion;
            status = DetermineHashStatus(
                patch,
                game,
                runtimeKeyValid);
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
            game.HashesFor(patch.HashKind).Count > 0,
            runtimeKeySerialMismatch,
            targetIndex);
    }

    private static PatchMatchStatus DetermineHashStatus(
        Rpcs3PatchEntry patch,
        Rpcs3GameDescriptor game,
        bool runtimeKeyValid)
    {
        if (!runtimeKeyValid ||
            patch.NormalizedExecutableHash is null)
        {
            return PatchMatchStatus.SerialVersionMatchHashUnknown;
        }

        var hashes = game.HashesFor(patch.HashKind);
        var comparableHashes = patch.HashKind == PatchHashKind.Ppu
            ? game.AllAuthoritativePpuHashes
            : hashes;
        if (comparableHashes.Any(hash => hash.Equals(
                patch.NormalizedExecutableHash,
                StringComparison.OrdinalIgnoreCase)))
        {
            return PatchMatchStatus.ExactMatch;
        }

        if (comparableHashes.Count == 0 ||
            !IsConclusiveNegativeEvidence(patch.HashKind, game))
        {
            return PatchMatchStatus.SerialVersionMatchHashUnknown;
        }

        return PatchMatchStatus.HashMismatch;
    }

    private static bool IsConclusiveNegativeEvidence(
        PatchHashKind kind,
        Rpcs3GameDescriptor game)
    {
        // The current main PPU executable identifies the installed build.
        // SPU, PRX, and overlay evidence is partial because those modules may
        // simply not have loaded yet.
        return kind == PatchHashKind.Ppu &&
               game.AllAuthoritativePpuHashes.Count > 0;
    }

    private static PatchCompatibilityDecision CreateDecision(
        Rpcs3PatchEntry patch,
        PatchMatchStatus status,
        string? matchedSerial,
        string? matchedTargetSerial,
        string? matchedTargetVersion,
        bool runtimeKeyValid,
        bool runtimeKeySerialMismatch)
    {
        var runtimeKey = patch.RuntimeHashKey;
        // Compatibility blocks activation, never cleanup. If the catalog still
        // resolves a concrete patch_config.yml identity, an already-enabled
        // entry must remain removable after a game update changes its version
        // or main executable hash.
        var canDisable = runtimeKeyValid &&
                         !runtimeKeySerialMismatch &&
                         status is not PatchMatchStatus.NoMetadata and
                             not PatchMatchStatus.SerialMismatch;

        if (!runtimeKeyValid)
        {
            return Blocked(
                PatchCompatibilityReasonCode.InvalidRuntimeKey,
                runtimeKey,
                canDisable: false);
        }

        if (runtimeKeySerialMismatch)
        {
            return Blocked(
                PatchCompatibilityReasonCode.RuntimeKeySerialMismatch,
                runtimeKey,
                canDisable: false);
        }

        if (status == PatchMatchStatus.ExactMatch)
        {
            return new PatchCompatibilityDecision(
                PatchCompatibilityVerdict.Compatible,
                PatchCompatibilityReasonCode.ExactRuntimeIdentity,
                CanEnable: true,
                CanDisable: true,
                UsesRpcs3RuntimeHashGuard: false,
                RuntimeKey: runtimeKey);
        }

        if (status == PatchMatchStatus.SerialVersionMatchHashUnknown)
        {
            if (!IsKnownSource(patch.Source))
            {
                return Blocked(
                    PatchCompatibilityReasonCode.UntrustedRuntimeGuard,
                    runtimeKey,
                    canDisable);
            }

            if (string.IsNullOrWhiteSpace(matchedSerial) ||
                string.IsNullOrWhiteSpace(matchedTargetSerial) ||
                string.IsNullOrWhiteSpace(matchedTargetVersion) ||
                Rpcs3PatchVersion.IsAll(matchedTargetSerial))
            {
                return Blocked(
                    PatchCompatibilityReasonCode.GlobalTargetNeedsExactHash,
                    runtimeKey,
                    canDisable);
            }

            if (!matchedSerial.Equals(
                    matchedTargetSerial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(
                    PatchCompatibilityReasonCode.SerialMismatch,
                    runtimeKey,
                    canDisable);
            }

            return new PatchCompatibilityDecision(
                PatchCompatibilityVerdict.RuntimeGuarded,
                PatchCompatibilityReasonCode.Rpcs3RuntimeHashGuard,
                CanEnable: true,
                CanDisable: true,
                UsesRpcs3RuntimeHashGuard: true,
                RuntimeKey: runtimeKey);
        }

        return Blocked(
            ReasonFor(status),
            runtimeKey,
            canDisable);
    }

    private static PatchCompatibilityDecision Blocked(
        PatchCompatibilityReasonCode reason,
        string runtimeKey,
        bool canDisable)
    {
        return new PatchCompatibilityDecision(
            PatchCompatibilityVerdict.Blocked,
            reason,
            CanEnable: false,
            CanDisable: canDisable,
            UsesRpcs3RuntimeHashGuard: false,
            RuntimeKey: runtimeKey);
    }

    private static PatchCompatibilityReasonCode ReasonFor(
        PatchMatchStatus status)
    {
        return status switch
        {
            PatchMatchStatus.NoMetadata =>
                PatchCompatibilityReasonCode.MissingTargetMetadata,
            PatchMatchStatus.SerialMismatch =>
                PatchCompatibilityReasonCode.SerialMismatch,
            PatchMatchStatus.VersionUnknown =>
                PatchCompatibilityReasonCode.VersionUnknown,
            PatchMatchStatus.VersionMismatch =>
                PatchCompatibilityReasonCode.VersionMismatch,
            PatchMatchStatus.HashMismatch =>
                PatchCompatibilityReasonCode.ExecutableHashMismatch,
            _ => PatchCompatibilityReasonCode.MissingTargetMetadata
        };
    }

    private static bool RuntimeKeySerialMismatch(
        Rpcs3PatchEntry patch,
        string? gameSerial)
    {
        return !string.IsNullOrWhiteSpace(patch.RuntimeKeySerial) &&
               !string.IsNullOrWhiteSpace(gameSerial) &&
               !patch.RuntimeKeySerial.Equals(
                   gameSerial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool SerialMatches(
        string targetSerial,
        string gameSerial)
    {
        return Rpcs3PatchVersion.IsAll(targetSerial) ||
               targetSerial.Equals(
                   gameSerial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownSource(PatchSourceOrigin source)
    {
        return source is PatchSourceOrigin.Official
            or PatchSourceOrigin.Imported
            or PatchSourceOrigin.BundledCanary
            or PatchSourceOrigin.BundledCheats
            or PatchSourceOrigin.ManagedCheats
            or PatchSourceOrigin.TitleSpecific;
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

    private static string BuildReason(
        Candidate candidate,
        PatchCompatibilityDecision compatibility)
    {
        var installedVersion =
            Rpcs3PatchVersion.Normalize(candidate.Game.Version) ??
            "unknown";
        var requiredVersion =
            candidate.TargetVersion ??
            Rpcs3PatchVersion.All;

        return compatibility.ReasonCode switch
        {
            PatchCompatibilityReasonCode.ExactRuntimeIdentity =>
                $"Serial {candidate.Game.Serial}, installed version {installedVersion}, target version {requiredVersion}, and {candidate.Patch.HashKind} runtime key match.",
            PatchCompatibilityReasonCode.Rpcs3RuntimeHashGuard =>
                $"Serial {candidate.Game.Serial} and version {installedVersion} match; RPCS3 will require runtime key {compatibility.RuntimeKey} at boot.",
            PatchCompatibilityReasonCode.RuntimeKeySerialMismatch =>
                $"Runtime key scope does not match installed serial {candidate.Game.Serial}.",
            PatchCompatibilityReasonCode.VersionUnknown =>
                $"Serial {candidate.Game.Serial} matches, but the installed game version is unknown; target version is {requiredVersion}.",
            PatchCompatibilityReasonCode.VersionMismatch =>
                $"Serial {candidate.Game.Serial} matches, but installed version {installedVersion} does not match target version {requiredVersion}.",
            PatchCompatibilityReasonCode.ExecutableHashMismatch =>
                $"Serial {candidate.Game.Serial} and version match, but the authoritative PPU executable hash differs.",
            PatchCompatibilityReasonCode.InvalidRuntimeKey =>
                "Patch key is not a runtime key generated by RPCS3.",
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
        bool RuntimeKeySerialMismatch,
        int TargetIndex);
}

#pragma warning restore IDE0130
