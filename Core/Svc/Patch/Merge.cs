using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchCatalogMergeService
{
    public Rpcs3PatchCatalog Merge(params Rpcs3PatchCatalog?[] catalogs)
    {
        var availableCatalogs = catalogs
            .Where(catalog => catalog is not null)
            .Cast<Rpcs3PatchCatalog>()
            .ToArray();
        var version = availableCatalogs
            .FirstOrDefault(catalog => !string.IsNullOrWhiteSpace(catalog.Version))?
            .Version;
        var selected = new Dictionary<string, Rpcs3PatchEntry>(StringComparer.OrdinalIgnoreCase);
        var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var diagnostics =
            new BoundedDiagnosticList<PatchCatalogDiagnostic>(
                Rpcs3InputLimits.Diagnostics);

        foreach (var entry in availableCatalogs.SelectMany(catalog => catalog.Entries))
        {
            var key = EntryKey(entry);
            if (rejected.Contains(key))
            {
                diagnostics.Add(new PatchCatalogDiagnostic(
                    Code: "PATCH_CONFLICT_REJECTED",
                    Message: BuildRejectedConflictMessage(entry),
                    KeptSource: PatchSourceOrigin.Unknown,
                    DroppedSource: entry.Source));
                continue;
            }

            if (!selected.TryGetValue(key, out var existing))
            {
                selected.Add(key, entry);
                order.Add(key);
                continue;
            }

            if (HasSameVersionDefinitionConflict(existing, entry))
            {
                selected.Remove(key);
                rejected.Add(key);
                diagnostics.Add(new PatchCatalogDiagnostic(
                    Code: "PATCH_DUPLICATE_CONFLICT",
                    Message: BuildConflictMessage(existing, entry),
                    KeptSource: PatchSourceOrigin.Unknown,
                    DroppedSource: entry.Source));
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
            order
                .Where(selected.ContainsKey)
                .Select(key => selected[key])
                .ToArray(),
            diagnostics);
    }

    private static bool HasSameVersionDefinitionConflict(
        Rpcs3PatchEntry existing,
        Rpcs3PatchEntry candidate)
    {
        return AreEquivalentPatchVersions(existing.PatchVersion, candidate.PatchVersion) &&
               !string.Equals(
                   existing.DefinitionFingerprint,
                   candidate.DefinitionFingerprint,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreEquivalentPatchVersions(string? left, string? right)
    {
        var leftValue = left?.Trim() ?? string.Empty;
        var rightValue = right?.Trim() ?? string.Empty;

        if (TryComparePatchVersions(leftValue, rightValue, out var comparison))
        {
            return comparison == 0;
        }

        return leftValue.Equals(rightValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldReplace(Rpcs3PatchEntry existing, Rpcs3PatchEntry candidate)
    {
        // Bundled catalogs are preview sources. An entry already present in an
        // RPCS3 file is authoritative because that is what RPCS3 will load.
        if (IsBundledPreview(candidate.Source) &&
            !IsBundledPreview(existing.Source))
        {
            return false;
        }

        if (IsBundledPreview(existing.Source) &&
            !IsBundledPreview(candidate.Source))
        {
            return true;
        }

        return TryComparePatchVersions(
                   candidate.PatchVersion,
                   existing.PatchVersion,
                   out var comparison) &&
               comparison > 0;
    }

    private static bool IsBundledPreview(PatchSourceOrigin source)
    {
        return source is PatchSourceOrigin.BundledCanary or PatchSourceOrigin.BundledCheats;
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
        return $"{entry.RuntimeHashKey}|{entry.Title}|{DefinitionChannel(entry)}";
    }

    private static string DefinitionChannel(Rpcs3PatchEntry entry)
    {
        // RPCS3 loads normal/Canary definitions and title-specific cheat
        // definitions from separate files. They may legitimately share the
        // same runtime key and patch title (for example, Unlock FPS) while
        // using different operations and game-title identities. Keep those
        // channels independent, but continue merging resident and bundled
        // versions inside each channel.
        return entry.Risk == PatchRisk.Cheat ||
               entry.Source is PatchSourceOrigin.BundledCheats or
                   PatchSourceOrigin.ManagedCheats
            ? "Cheat"
            : "Patch";
    }

    private static string BuildDiagnosticMessage(Rpcs3PatchEntry kept, Rpcs3PatchEntry dropped)
    {
        return $"Patch '{kept.Title}' for {kept.HashKind}-{kept.Hash} from {dropped.Source} " +
               $"was superseded by {kept.Source}.";
    }

    private static string BuildConflictMessage(
        Rpcs3PatchEntry existing,
        Rpcs3PatchEntry candidate)
    {
        var version = string.IsNullOrWhiteSpace(existing.PatchVersion)
            ? "<missing>"
            : existing.PatchVersion.Trim();
        return $"Patch '{existing.Title}' for {existing.HashKind}-{existing.Hash} has conflicting " +
               $"definitions at equivalent Patch Version '{version}' from {DescribeSource(existing)} and " +
               $"{DescribeSource(candidate)}. Every conflicting variant was rejected.";
    }

    private static string BuildRejectedConflictMessage(Rpcs3PatchEntry entry)
    {
        return $"Patch '{entry.Title}' for {entry.HashKind}-{entry.Hash} from {DescribeSource(entry)} " +
               "was rejected because that identity already has an unresolved same-version definition conflict.";
    }

    private static string DescribeSource(Rpcs3PatchEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.SourcePath)
            ? entry.Source.ToString()
            : $"{entry.Source} ({entry.SourcePath})";
    }
}
