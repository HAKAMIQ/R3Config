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
public sealed class PatCmp
{
    public IReadOnlyList<Mdl.PatMatch> Evaluate(
        PatCat catalog,
        IEnumerable<R3Game> games)
    {
        var gameList = games.ToArray();
        return catalog.Entries
            .Select(entry => Evaluate(entry, gameList))
            .ToArray();
    }

    public Mdl.PatMatch Evaluate(
        PatEntry patch,
        IEnumerable<R3Game> games)
    {
        var gameList = games.ToArray();
        var runtimeKeyValid = Mdl.RunKey.TryParse(
            patch.RunKey,
            out _);

        var candidates = patch.GameTgt
            .SelectMany((target, targetIndex) => gameList
                .Where(game => Serial(target.Serial, game.Serial))
                .Select(game => Check(
                    patch,
                    target,
                    game,
                    targetIndex,
                    runtimeKeyValid)))
            .OrderBy(candidate => Status(candidate.Status))
            .ThenByDescending(candidate =>
                !PatVer.IsAll(candidate.Target.Serial))
            .ThenByDescending(candidate =>
                !PatVer.IsAll(candidate.TgtVer))
            .ThenBy(candidate => candidate.TgtIdx)
            .ToArray();

        if (candidates.Length == 0)
        {
            var status = patch.GameTgt.Count == 0
                ? MatStat.NoMeta
                : MatStat.SerBad;

            var decision = Build(
                patch,
                status,
                matchedSerial: null,
                matchedTargetSerial: null,
                matchedTargetVersion: null,
                runtimeKeyValid,
                runtimeKeySerialMismatch: false);

            return new Mdl.PatMatch(
                Patch: patch,
                Status: status,
                MatSer: null,
                MatVer: null,
                HasMeta: false,
                Reason: patch.GameTgt.Count == 0
                    ? "Patch has no game target metadata."
                    : "No installed game serial matched this patch target.",
                Cmp: decision);
        }

        var best = candidates[0];
        var compatibility = Build(
            patch,
            best.Status,
            best.Game.Serial,
            best.Target.Serial,
            best.TgtVer,
            runtimeKeyValid,
            best.KeySerBd);

        return new Mdl.PatMatch(
            Patch: patch,
            Status: best.Status,
            MatSer: best.Game.Serial,
            MatVer: PatVer.Norm(best.Game.Version),
            HasMeta: best.HasMeta,
            Reason: Build2(best, compatibility),
            TgtName: best.Target.Title,
            TgtSer: best.Target.Serial,
            TgtVer: best.TgtVer,
            DetHash: best.Game.GetHash(patch.HashKind),
            HashSrc: best.Game.HashSrc,
            HashMsg: best.Game.HashMsg,
            HashEvi: best.Game.HashEvi,
            HashTime: best.Game.HashTime,
            Cmp: compatibility);
    }

    public CmpDec Decide(
        Mdl.PatMatch match)
    {
        // Re-evaluate from immutable match facts instead of trusting a copied
        // decision. Record `with` expressions are used by merge/migration code
        // and can otherwise leave a stale verdict attached to changed source
        // or runtime-key metadata.
        return Build(
            match.Patch,
            match.Status,
            match.MatSer,
            match.TgtSer,
            match.TgtVer,
            Mdl.RunKey.TryParse(
                match.Patch.RunKey,
                out _),
            RunKey(
                match.Patch,
                match.MatSer));
    }

    private static Cand Check(
        PatEntry patch,
        R3PatTgt target,
        R3Game game,
        int targetIndex,
        bool runtimeKeyValid)
    {
        var installedVersion = PatVer.Norm(game.Version);
        var targetVersions = target.Versions
            .Select(PatVer.Norm)
            .Where(version => version is not null)
            .Select(version => version!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var runtimeKeySerialMismatch = RunKey(
            patch,
            game.Serial);

        string? matchedTargetVersion;
        MatStat status;

        if (runtimeKeySerialMismatch)
        {
            matchedTargetVersion = targetVersions.FirstOrDefault()
                ?? PatVer.All;
            status = MatStat.SerBad;
        }
        else if (targetVersions.Length == 0 ||
                 targetVersions.Any(PatVer.IsAll))
        {
            matchedTargetVersion = PatVer.All;
            status = GetHash(
                patch,
                game,
                runtimeKeyValid);
        }
        else if (installedVersion is null)
        {
            matchedTargetVersion = targetVersions[0];
            status = MatStat.VerUnk;
        }
        else if (targetVersions.FirstOrDefault(version =>
                     PatVer.Equals(
                         version,
                         installedVersion)) is { } exactVersion)
        {
            matchedTargetVersion = exactVersion;
            status = GetHash(
                patch,
                game,
                runtimeKeyValid);
        }
        else
        {
            matchedTargetVersion = targetVersions[0];
            status = MatStat.VerBad;
        }

        return new Cand(
            patch,
            target,
            game,
            status,
            matchedTargetVersion,
            game.GetHash(patch.HashKind).Count > 0,
            runtimeKeySerialMismatch,
            targetIndex);
    }

    private static MatStat GetHash(
        PatEntry patch,
        R3Game game,
        bool runtimeKeyValid)
    {
        if (!runtimeKeyValid ||
            patch.NormHash is null)
        {
            return MatStat.Unknown;
        }

        var hashes = game.GetHash(patch.HashKind);
        var comparableHashes = patch.HashKind == HashKind.Ppu
            ? game.PpuList
            : hashes;
        if (comparableHashes.Any(hash => hash.Equals(
                patch.NormHash,
                StringComparison.OrdinalIgnoreCase)))
        {
            return MatStat.Match;
        }

        if (comparableHashes.Count == 0 ||
            !IsFinal(patch.HashKind, game))
        {
            return MatStat.Unknown;
        }

        return MatStat.HashBad;
    }

    private static bool IsFinal(
        HashKind kind,
        R3Game game)
    {
        // The current main PPU executable identifies the installed build.
        // SPU, PRX, and overlay evidence is partial because those modules may
        // simply not have loaded yet.
        return kind == HashKind.Ppu &&
               game.PpuList.Count > 0;
    }

    private static CmpDec Build(
        PatEntry patch,
        MatStat status,
        string? matchedSerial,
        string? matchedTargetSerial,
        string? matchedTargetVersion,
        bool runtimeKeyValid,
        bool runtimeKeySerialMismatch)
    {
        var runtimeKey = patch.RunKey;
        // Compatibility blocks activation, never cleanup. If the catalog still
        // resolves a concrete patch_config.yml identity, an already-enabled
        // entry must remain removable after a game update changes its version
        // or main executable hash.
        var canDisable = runtimeKeyValid &&
                         !runtimeKeySerialMismatch &&
                         status is not MatStat.NoMeta and
                             not MatStat.SerBad;

        if (!runtimeKeyValid)
        {
            return Blocked(
                CmpCode.Key,
                runtimeKey,
                canDisable: false);
        }

        if (runtimeKeySerialMismatch)
        {
            return Blocked(
                CmpCode.KeySerBd,
                runtimeKey,
                canDisable: false);
        }

        if (status == MatStat.Match)
        {
            return new CmpDec(
                CmpVerd.Compat,
                CmpCode.ExactId,
                CanOn: true,
                CanOff: true,
                UseGuard: false,
                RunKey: runtimeKey);
        }

        if (status == MatStat.Unknown)
        {
            if (!IsKnown(patch.Source))
            {
                return Blocked(
                    CmpCode.Guard,
                    runtimeKey,
                    canDisable);
            }

            if (string.IsNullOrWhiteSpace(matchedSerial) ||
                string.IsNullOrWhiteSpace(matchedTargetSerial) ||
                string.IsNullOrWhiteSpace(matchedTargetVersion) ||
                PatVer.IsAll(matchedTargetSerial))
            {
                return Blocked(
                    CmpCode.Hash,
                    runtimeKey,
                    canDisable);
            }

            if (!matchedSerial.Equals(
                    matchedTargetSerial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(
                    CmpCode.SerBad,
                    runtimeKey,
                    canDisable);
            }

            return new CmpDec(
                CmpVerd.Guarded,
                CmpCode.R3Guard,
                CanOn: true,
                CanOff: true,
                UseGuard: true,
                RunKey: runtimeKey);
        }

        return Blocked(
            Reason(status),
            runtimeKey,
            canDisable);
    }

    private static CmpDec Blocked(
        CmpCode reason,
        string runtimeKey,
        bool canDisable)
    {
        return new CmpDec(
            CmpVerd.Blocked,
            reason,
            CanOn: false,
            CanOff: canDisable,
            UseGuard: false,
            RunKey: runtimeKey);
    }

    private static CmpCode Reason(
        MatStat status)
    {
        return status switch
        {
            MatStat.NoMeta =>
                CmpCode.Meta,
            MatStat.SerBad =>
                CmpCode.SerBad,
            MatStat.VerUnk =>
                CmpCode.Unknown,
            MatStat.VerBad =>
                CmpCode.VerBad,
            MatStat.HashBad =>
                CmpCode.ExeBad,
            _ => CmpCode.Meta
        };
    }

    private static bool RunKey(
        PatEntry patch,
        string? gameSerial)
    {
        return !string.IsNullOrWhiteSpace(patch.KeySer) &&
               !string.IsNullOrWhiteSpace(gameSerial) &&
               !patch.KeySer.Equals(
                   gameSerial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool Serial(
        string targetSerial,
        string gameSerial)
    {
        return PatVer.IsAll(targetSerial) ||
               targetSerial.Equals(
                   gameSerial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnown(PatSrc source)
    {
        return source is PatSrc.Official
            or PatSrc.Imported
            or PatSrc.Can
            or PatSrc.Cht
            or PatSrc.ManCht
            or PatSrc.TitlePat;
    }

    private static int Status(MatStat status)
    {
        return status switch
        {
            MatStat.Match => 0,
            MatStat.Unknown => 1,
            MatStat.VerUnk => 2,
            MatStat.HashBad => 3,
            MatStat.VerBad => 4,
            MatStat.SerBad => 5,
            _ => 6
        };
    }

    private static string Build2(
        Cand candidate,
        CmpDec compatibility)
    {
        var installedVersion =
            PatVer.Norm(candidate.Game.Version) ??
            "unknown";
        var requiredVersion =
            candidate.TgtVer ??
            PatVer.All;

        return compatibility.Code switch
        {
            CmpCode.ExactId =>
                $"Serial {candidate.Game.Serial}, installed version {installedVersion}, target version {requiredVersion}, and {candidate.Patch.HashKind} runtime key match.",
            CmpCode.R3Guard =>
                $"Serial {candidate.Game.Serial} and version {installedVersion} match; RPCS3 will require runtime key {compatibility.RunKey} at boot.",
            CmpCode.KeySerBd =>
                $"Runtime key scope does not match installed serial {candidate.Game.Serial}.",
            CmpCode.Unknown =>
                $"Serial {candidate.Game.Serial} matches, but the installed game version is unknown; target version is {requiredVersion}.",
            CmpCode.VerBad =>
                $"Serial {candidate.Game.Serial} matches, but installed version {installedVersion} does not match target version {requiredVersion}.",
            CmpCode.ExeBad =>
                $"Serial {candidate.Game.Serial} and version match, but the authoritative PPU executable hash differs.",
            CmpCode.Key =>
                "Patch key is not a runtime key generated by RPCS3.",
            _ => "Patch metadata did not match this game."
        };
    }

    private sealed record Cand(
        PatEntry Patch,
        R3PatTgt Target,
        R3Game Game,
        MatStat Status,
        string? TgtVer,
        bool HasMeta,
        bool KeySerBd,
        int TgtIdx);
}

#pragma warning restore IDE0130
