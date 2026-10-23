namespace R3Config.Svc;

// A filename index, and nothing else. It stores only what the identity parser
// already proved, keyed by target and pointing at source paths - never at
// copied cheat definitions, so different bindings in one file keep one
// semantic source and one fingerprint. It decides no semantics of its own:
// an unresolved binding is absent here because the parser refused it, not
// because this type formed an opinion about it.
internal sealed class NclCorpusTargetIndex
{
    private static readonly HashSet<string> Empty = new(StringComparer.Ordinal);

    // Serial and version both compare case-insensitively, matching every other
    // target comparison in this project. Version keys are stored normalized by
    // PatVer so a lookup that normalizes its argument cannot miss a binding the
    // source spelled differently.
    private readonly Dictionary<(string Serial, string Version), HashSet<string>> _paths =
        new(TargetKeyComparer.Instance);

    private readonly Dictionary<string, HashSet<string>> _versions =
        new(StringComparer.OrdinalIgnoreCase);

    public NclCorpusTargetIndex(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        foreach (var path in paths)
        {
            var identity = NclParser.ParseIdentity(Path.GetFileName(path));

            foreach (var binding in identity.TargetBindings)
            {
                if (binding.EffectiveVersion is not { } version ||
                    PatVer.Norm(version) is not { } normalized)
                {
                    continue;
                }

                var key = (binding.Serial, normalized);

                if (!_paths.TryGetValue(key, out var sources))
                {
                    sources = new HashSet<string>(StringComparer.Ordinal);
                    _paths.Add(key, sources);
                }

                sources.Add(path);

                if (!_versions.TryGetValue(binding.Serial, out var versions))
                {
                    versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _versions.Add(binding.Serial, versions);
                }

                versions.Add(normalized);
            }
        }
    }

    public int BindingCount => _paths.Count;

    public IReadOnlySet<string> Find(string serial, string version)
    {
        return PatVer.Norm(version) is { } normalized &&
               _paths.TryGetValue((serial, normalized), out var paths)
            ? paths
            : Empty;
    }

    public IEnumerable<string> Versions(string serial)
    {
        return _versions.TryGetValue(serial, out var versions) ? versions : Empty;
    }

    private sealed class TargetKeyComparer
        : IEqualityComparer<(string Serial, string Version)>
    {
        public static readonly TargetKeyComparer Instance = new();

        public bool Equals(
            (string Serial, string Version) x,
            (string Serial, string Version) y)
        {
            return string.Equals(x.Serial, y.Serial, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Serial, string Version) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Serial),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version));
        }
    }
}
