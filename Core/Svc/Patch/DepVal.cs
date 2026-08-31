using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchDependencyException : InvalidOperationException
{
    public PatchDependencyException(string? message)
        : base(message)
    {
    }
}

public static class PatDepVal
{
    public static void ValidateEnable(
        IReadOnlyList<PatMatch> requested,
        IReadOnlyList<PatMatch> active)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(active);

        var effective = active
            .Concat(requested)
            .DistinctBy(Identity, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var match in requested)
        {
            ValidateMatch(match, effective);
        }
    }

    public static void ValidateDisable(
        IReadOnlyList<PatMatch> disabling,
        IReadOnlyList<PatMatch> enabled)
    {
        ArgumentNullException.ThrowIfNull(disabling);
        ArgumentNullException.ThrowIfNull(enabled);

        var removed = disabling
            .Select(Identity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaining = enabled
            .Where(match => !removed.Contains(Identity(match)))
            .DistinctBy(Identity, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var match in remaining)
        {
            ValidateMatch(match, remaining);
        }
    }

    public static void ValidateCatalog(
        IReadOnlyList<Rpcs3PatchEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byTarget = entries
            .SelectMany(entry => entry.GameTgt.SelectMany(target =>
                target.Versions.Select(version => new CatalogNode(
                    entry,
                    target.Serial,
                    PatVer.Norm(version)))))
            .Where(node => node.Version is not null)
            .GroupBy(
                node => TargetIdentity(
                    node.Entry,
                    node.Serial,
                    node.Version!,
                    node.Entry.Title),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var node in byTarget.Values.Where(node =>
                     node.Entry.Dependencies.Count > 0))
        {
            foreach (var group in node.Entry.Dependencies)
            {
                foreach (var title in group.Titles)
                {
                    var dependencyIdentity = TargetIdentity(
                        node.Entry,
                        node.Serial,
                        node.Version!,
                        title);
                    if (!byTarget.ContainsKey(dependencyIdentity))
                    {
                        throw new InvalidDataException(
                            $"Patch '{node.Entry.Title}' requires missing exact source '{title}'.");
                    }
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in byTarget.Keys)
        {
            if (HasCycle(identity, byTarget, visiting, visited, out var cycle))
            {
                throw new InvalidDataException(
                    $"Patch dependency cycle detected: {cycle}.");
            }
        }
    }

    private static void ValidateMatch(
        PatMatch dependent,
        IReadOnlyList<PatMatch> active)
    {
        foreach (var group in dependent.Patch.Dependencies)
        {
            var satisfied = group.Mode == PatDepMode.All
                ? group.Titles.All(title => Contains(active, dependent, title))
                : group.Titles.Any(title => Contains(active, dependent, title));
            if (satisfied)
            {
                continue;
            }

            var quotedTitles = group.Titles
                .Select(title => $"'{title}'")
                .ToArray();
            var requirement = group.Mode == PatDepMode.All
                ? string.Join(" and ", quotedTitles)
                : "one of " + string.Join(", ", quotedTitles);
            throw new PatchDependencyException(
                $"Patch '{dependent.Patch.Title}' requires {requirement} for the same executable, serial, and version.");
        }
    }

    private static bool Contains(
        IReadOnlyList<PatMatch> active,
        PatMatch dependent,
        string title)
    {
        return active.Any(candidate =>
            Channel(candidate.Patch).Equals(
                Channel(dependent.Patch),
                StringComparison.OrdinalIgnoreCase) &&
            candidate.Patch.RunKey.Equals(
                dependent.Patch.RunKey,
                StringComparison.OrdinalIgnoreCase) &&
            candidate.Patch.Title.Equals(
                title,
                StringComparison.OrdinalIgnoreCase) &&
            Serial(candidate).Equals(
                Serial(dependent),
                StringComparison.OrdinalIgnoreCase) &&
            PatVer.Equals(
                Version(candidate),
                Version(dependent)));
    }

    private static bool HasCycle(
        string identity,
        IReadOnlyDictionary<string, CatalogNode> nodes,
        ISet<string> visiting,
        ISet<string> visited,
        out string cycle)
    {
        cycle = string.Empty;
        if (visited.Contains(identity))
        {
            return false;
        }

        if (!visiting.Add(identity))
        {
            cycle = nodes[identity].Entry.Title;
            return true;
        }

        var node = nodes[identity];
        foreach (var title in node.Entry.Dependencies.SelectMany(group => group.Titles))
        {
            var dependencyIdentity = TargetIdentity(
                node.Entry,
                node.Serial,
                node.Version!,
                title);
            if (nodes.ContainsKey(dependencyIdentity) &&
                HasCycle(
                    dependencyIdentity,
                    nodes,
                    visiting,
                    visited,
                    out var nested))
            {
                cycle = node.Entry.Title + " -> " + nested;
                return true;
            }
        }

        visiting.Remove(identity);
        visited.Add(identity);
        return false;
    }

    private static string Identity(PatMatch match)
    {
        return TargetIdentity(
            match.Patch,
            Serial(match),
            Version(match),
            match.Patch.Title);
    }

    private static string TargetIdentity(
        Rpcs3PatchEntry entry,
        string serial,
        string version,
        string title)
    {
        return string.Join(
            "\0",
            Channel(entry),
            entry.RunKey,
            serial,
            version,
            title);
    }

    private static string Channel(Rpcs3PatchEntry entry)
    {
        // Keep dependency identity aligned with PatchCatalogMergeService: a
        // prerequisite must come from the same catalog ownership channel as
        // its dependent. Managed Artemis is the installed form of Artemis and
        // intentionally remains in the same channel.
        if (entry.Source == PatSrc.Official)
        {
            return "Official";
        }

        if (entry.Class == PatClass.Canary)
        {
            return "Canary";
        }

        if (entry.Source is PatSrc.Artemis or PatSrc.ManagedArtemis)
        {
            return "Artemis";
        }

        return "Local";
    }

    private static string Serial(PatMatch match)
    {
        return match.TgtSer ?? match.MatSer ?? string.Empty;
    }

    private static string Version(PatMatch match)
    {
        return PatVer.Norm(match.TgtVer ?? match.MatVer) ?? string.Empty;
    }

    private sealed record CatalogNode(
        Rpcs3PatchEntry Entry,
        string Serial,
        string? Version);
}
