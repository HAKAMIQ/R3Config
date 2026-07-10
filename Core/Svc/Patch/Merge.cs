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

        return TryComparePatchVersions(
                   candidate.PatchVersion,
                   existing.PatchVersion,
                   out var comparison) &&
               comparison > 0;
    }

    private static bool TryComparePatchVersions(
        string? left,
        string? right,
        out int comparison)
    {
        comparison = 0;

        var leftValue = left ?? string.Empty;
        var rightValue = right ?? string.Empty;

        if (!IsRpcS3ComparableVersion(leftValue) ||
            !IsRpcS3ComparableVersion(rightValue))
        {
            return false;
        }

        var leftParts = leftValue.Split('.');
        var rightParts = rightValue.Split('.');
        var partCount = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < partCount; index++)
        {
            var leftPart = NormalizeNumericPart(
                index < leftParts.Length ? leftParts[index] : string.Empty);
            var rightPart = NormalizeNumericPart(
                index < rightParts.Length ? rightParts[index] : string.Empty);

            if (leftPart.Length != rightPart.Length)
            {
                comparison = leftPart.Length.CompareTo(rightPart.Length);
                return true;
            }

            var partComparison = string.CompareOrdinal(leftPart, rightPart);
            if (partComparison != 0)
            {
                comparison = partComparison;
                return true;
            }
        }

        return true;
    }

    private static bool IsRpcS3ComparableVersion(string value)
    {
        return value.All(character =>
            character == '.' ||
            character is >= '0' and <= '9');
    }

    private static string NormalizeNumericPart(string value)
    {
        var firstNonZero = 0;
        while (firstNonZero < value.Length && value[firstNonZero] == '0')
        {
            firstNonZero++;
        }

        return firstNonZero == value.Length
            ? "0"
            : value[firstNonZero..];
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
