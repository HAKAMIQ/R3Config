using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchCatalogMergeService
{
    public Rpcs3PatchCatalog Merge(params Rpcs3PatchCatalog?[] catalogs)
    {
        var availableCatalogs = catalogs.Where(catalog => catalog is not null).Cast<Rpcs3PatchCatalog>().ToArray();
        var version = availableCatalogs.FirstOrDefault(catalog => !string.IsNullOrWhiteSpace(catalog.Version))?.Version;
        var selected = new Dictionary<string, Rpcs3PatchEntry>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var diagnostics = new List<PatchCatalogDiagnostic>();

        foreach (var entry in availableCatalogs.SelectMany(catalog => catalog.Entries))
        {
            var key = EntryKey(entry);
            if (!selected.TryGetValue(key, out var existing))
            {
                selected.Add(key, entry);
                order.Add(key);
                continue;
            }

            var keepCandidate = ShouldReplace(existing, entry);
            var kept = keepCandidate ? entry : existing;
            var dropped = keepCandidate ? existing : entry;
            selected[key] = kept;

            diagnostics.Add(new PatchCatalogDiagnostic(
                Code: keepCandidate ? "PATCH_VERSION_OVERRIDE" : "PATCH_DUPLICATE_SKIPPED",
                Message: BuildDiagnosticMessage(kept, dropped),
                KeptSource: kept.Source,
                DroppedSource: dropped.Source));
        }

        return new Rpcs3PatchCatalog(
            version,
            order.Select(key => selected[key]).ToArray(),
            diagnostics);
    }

    private static bool ShouldReplace(Rpcs3PatchEntry existing, Rpcs3PatchEntry candidate)
    {
        // Bundled Canary is a preview source. An entry already present in an RPCS3
        // file is authoritative because that is the entry RPCS3 will actually load.
        if (candidate.Source == PatchSourceOrigin.BundledCanary &&
            existing.Source != PatchSourceOrigin.BundledCanary)
        {
            return false;
        }

        if (existing.Source == PatchSourceOrigin.BundledCanary &&
            candidate.Source != PatchSourceOrigin.BundledCanary)
        {
            return true;
        }

        return ComparePatchVersions(candidate.PatchVersion, existing.PatchVersion) > 0;
    }

    private static int ComparePatchVersions(string? left, string? right)
    {
        var normalizedLeft = NormalizePatchVersion(left);
        var normalizedRight = NormalizePatchVersion(right);

        if (Version.TryParse(normalizedLeft, out var leftVersion) &&
            Version.TryParse(normalizedRight, out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(normalizedLeft, normalizedRight);
    }

    private static string NormalizePatchVersion(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? normalized[1..].TrimStart('.')
            : normalized;
    }

    private static string EntryKey(Rpcs3PatchEntry entry)
    {
        return $"{entry.HashKind}|{entry.Hash}|{entry.Title}";
    }

    private static string BuildDiagnosticMessage(Rpcs3PatchEntry kept, Rpcs3PatchEntry dropped)
    {
        return $"Patch '{kept.Title}' for {kept.HashKind}-{kept.Hash} from {dropped.Source} " +
               $"was superseded by {kept.Source}.";
    }
}
