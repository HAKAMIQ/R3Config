using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed class PatchMatchService(
    PatchCompatibilityService? compatibility = null,
    CanaryRuntimeBindingService? canaryBinding = null)
{
    private readonly PatchCompatibilityService _Cmp =
        compatibility ?? new PatchCompatibilityService();
    private readonly CanaryRuntimeBindingService _CanBind =
        canaryBinding ?? new CanaryRuntimeBindingService();

    public IReadOnlyList<Mdl.PatMatch> Match(
        PatCat catalog,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _Cmp.Evaluate(catalog, games);
    }

    public IReadOnlyList<Mdl.PatMatch> Match(
        PatCat catalog,
        Rpcs3GameDescriptor game)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(game);

        var matches = catalog.Entries
            .Select(entry => new
            {
                Entry = entry,
                Binding = _CanBind.Resolve(
                    catalog,
                    entry,
                    game)
            })
            .Where(item => item.Binding.Kind !=
                CanaryRuntimeBindingKind.Suppressed)
            .Select(item => _Cmp.Evaluate(
                item.Binding.Patch ?? item.Entry,
                [game],
                item.Binding))
            .ToArray();

        return PreferResidentCanary(matches);
    }

    public Mdl.PatMatch Match(
        Rpcs3PatchEntry patch,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _Cmp.Evaluate(patch, games);
    }

    private static List<PatMatch> PreferResidentCanary(
        PatMatch[] matches)
    {
        var output = new List<PatMatch>(matches.Length);
        var groupedCanary = matches
            .Where(match => match.Patch.Class == PatClass.Canary)
            .GroupBy(
                CanaryIdentity,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(match => match.Patch.Source == PatSrc.Can ? 1 : 0)
                    .ThenBy(match => match.Patch.Source)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var emitted = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var match in matches)
        {
            if (match.Patch.Class != PatClass.Canary)
            {
                output.Add(match);
                continue;
            }

            var identity = CanaryIdentity(match);
            if (!emitted.Add(identity))
            {
                continue;
            }

            output.Add(groupedCanary[identity]);
        }

        return output;
    }

    private static string CanaryIdentity(PatMatch match)
    {
        return string.Join(
            "\0",
            match.EffectiveRunKey,
            match.Patch.Title,
            match.Patch.DefPrint ?? string.Empty,
            match.TgtSer ?? match.MatSer ?? string.Empty,
            PatVer.Norm(match.TgtVer ?? match.MatVer) ?? string.Empty);
    }
}

#pragma warning restore IDE0130
