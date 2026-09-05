using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private CanaryHashExpectation CanaryExpectation()
    {
        var game = GameList.SelGame?.Game;
        if (game is null || _patCat is null)
        {
            return CanaryHashExpectation.Empty;
        }

        var entries = _patCat.Entries
            .Where(entry =>
                entry.Class == PatClass.Canary &&
                entry.HashKind is HashKind.Ppu or HashKind.Spu &&
                entry.NormHash is not null &&
                entry.GameTgt.Any(target =>
                    CanaryTargetMatches(target, game)))
            .ToArray();

        return new CanaryHashExpectation(
            ExpectedHashes(entries, HashKind.Ppu),
            ExpectedHashes(entries, HashKind.Spu));
    }

    private static string[] ExpectedHashes(
        IEnumerable<Rpcs3PatchEntry> entries,
        HashKind kind)
    {
        return entries
            .Where(entry => entry.HashKind == kind)
            .Select(entry => entry.NormHash)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CanaryTargetMatches(
        R3PatTgt target,
        Rpcs3GameDescriptor game)
    {
        var serialMatches =
            PatVer.IsAll(target.Serial) ||
            target.Serial.Equals(
                game.Serial,
                StringComparison.OrdinalIgnoreCase);
        if (!serialMatches)
        {
            return false;
        }

        if (target.Versions.Count == 0 ||
            target.Versions.Any(PatVer.IsAll))
        {
            return true;
        }

        return target.Versions.Any(version =>
            PatVer.Equals(version, game.Version));
    }

    private static bool MatchesExpectation(
        Rpcs3GameDescriptor game,
        CanaryHashExpectation expectation)
    {
        if (!expectation.HasTargets)
        {
            return false;
        }

        var expectedPpu = expectation.PpuHashes
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (expectedPpu.Count > 0)
        {
            return game.HCanPpuList.Any(expectedPpu.Contains);
        }

        var expectedSpu = expectation.SpuHashes
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return game.HCanSpuList.Any(expectedSpu.Contains);
    }
}
