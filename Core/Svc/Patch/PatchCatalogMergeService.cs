using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed class PatchCatalogMergeService
{
    public PatCat Merge(params PatCat?[] catalogs)
    {
        var availableCatalogs = catalogs
            .Where(catalog => catalog is not null)
            .Cast<PatCat>()
            .ToArray();
        var version = availableCatalogs
            .FirstOrDefault(catalog => !string.IsNullOrWhiteSpace(catalog.Version))?
            .Version;
        var selected = new Dictionary<string, Rpcs3PatchEntry>(StringComparer.OrdinalIgnoreCase);
        var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var diagnostics =
            new DiagList<PatDiag>(
                R3Limits.Diag);

        foreach (var entry in availableCatalogs.SelectMany(catalog => catalog.Entries))
        {
            var key = EntryKey(entry);
            if (rejected.Contains(key))
            {
                diagnostics.Add(new PatDiag(
                    Code: "PATCH_CONFLICT_REJECTED",
                    Message: BuildMs3(entry),
                    KeepSrc: PatSrc.Unknown,
                    DropSrc: entry.Source));
                continue;
            }

            if (!selected.TryGetValue(key, out var existing))
            {
                selected.Add(key, entry);
                order.Add(key);
                continue;
            }

            if (HasSame(existing, entry))
            {
                selected.Remove(key);
                rejected.Add(key);
                diagnostics.Add(new PatDiag(
                    Code: "PATCH_DUPLICATE_CONFLICT",
                    Message: BuildMs2(existing, entry),
                    KeepSrc: PatSrc.Unknown,
                    DropSrc: entry.Source));
                continue;
            }

            var keepCandidate = Should(existing, entry);
            var kept = keepCandidate ? entry : existing;
            var dropped = keepCandidate ? existing : entry;
            selected[key] = kept;

            diagnostics.Add(new PatDiag(
                Code: keepCandidate ? "PATCH_VERSION_OVERRIDE" : "PATCH_DUPLICATE_SKIPPED",
                Message: BuildMsg(kept, dropped),
                KeepSrc: kept.Source,
                DropSrc: dropped.Source));
        }

        return new PatCat(
            version,
            order
                .Where(selected.ContainsKey)
                .Select(key => selected[key])
                .ToArray(),
            diagnostics);
    }

    private static bool HasSame(
        Rpcs3PatchEntry existing,
        Rpcs3PatchEntry candidate)
    {
        return AreEqual(existing.PatVer, candidate.PatVer) &&
               !string.Equals(
                   existing.DefPrint,
                   candidate.DefPrint,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreEqual(string? left, string? right)
    {
        var leftValue = left?.Trim() ?? string.Empty;
        var rightValue = right?.Trim() ?? string.Empty;

        if (TryVer(leftValue, rightValue, out var comparison))
        {
            return comparison == 0;
        }

        return leftValue.Equals(rightValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Should(Rpcs3PatchEntry existing, Rpcs3PatchEntry candidate)
    {
        // Bundled catalogs are preview sources. An entry already present in an
        // RPCS3 file is authoritative because that is what RPCS3 will load.
        if (Is(candidate.Source) &&
            !Is(existing.Source))
        {
            return false;
        }

        if (Is(existing.Source) &&
            !Is(candidate.Source))
        {
            return true;
        }

        return TryVer(
                   candidate.PatVer,
                   existing.PatVer,
                   out var comparison) &&
               comparison > 0;
    }

    private static bool Is(PatSrc source)
    {
        return source is PatSrc.Can or PatSrc.Artemis;
    }

    private static bool TryVer(
        string? left,
        string? right,
        out int comparison)
    {
        comparison = 0;

        var leftValue = left ?? string.Empty;
        var rightValue = right ?? string.Empty;

        if (!IsRpc(leftValue) ||
            !IsRpc(rightValue))
        {
            return false;
        }

        var leftParts = leftValue.Split('.');
        var rightParts = rightValue.Split('.');
        var partCount = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < partCount; index++)
        {
            var leftPart = NormPart(
                index < leftParts.Length ? leftParts[index] : string.Empty);
            var rightPart = NormPart(
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

    private static bool IsRpc(string value)
    {
        return value.All(character =>
            character == '.' ||
            character is >= '0' and <= '9');
    }

    private static string NormPart(string value)
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
        return $"{entry.RunKey}|{entry.Title}|{Def(entry)}";
    }

    private static string Def(Rpcs3PatchEntry entry)
    {
        // Source ownership defines the catalog channel. A patch's risk or
        // title must never move an official Patch Manager entry into Artemis,
        // nor merge an official entry with a Canary/local definition that
        // happens to share the same runtime key and title. Resident and bundled
        // copies continue to merge only inside their own source channel.
        if (entry.Source == PatSrc.Official)
        {
            return "Official";
        }

        if (entry.Class == PatClass.Canary)
        {
            return "Canary";
        }

        if (entry.Source is PatSrc.Artemis or
            PatSrc.ManagedArtemis)
        {
            return "Artemis";
        }

        return "Local";
    }

    private static string BuildMsg(Rpcs3PatchEntry kept, Rpcs3PatchEntry dropped)
    {
        return $"Patch '{kept.Title}' for {kept.HashKind}-{kept.Hash} from {dropped.Source} " +
               $"was superseded by {kept.Source}.";
    }

    private static string BuildMs2(
        Rpcs3PatchEntry existing,
        Rpcs3PatchEntry candidate)
    {
        var version = string.IsNullOrWhiteSpace(existing.PatVer)
            ? "<missing>"
            : existing.PatVer.Trim();
        return $"Patch '{existing.Title}' for {existing.HashKind}-{existing.Hash} has conflicting " +
               $"definitions at equivalent Patch Version '{version}' from {Describe(existing)} and " +
               $"{Describe(candidate)}. Every conflicting variant was rejected.";
    }

    private static string BuildMs3(Rpcs3PatchEntry entry)
    {
        return $"Patch '{entry.Title}' for {entry.HashKind}-{entry.Hash} from {Describe(entry)} " +
               "was rejected because that identity already has an unresolved same-version definition conflict.";
    }

    private static string Describe(Rpcs3PatchEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.SrcPath)
            ? entry.Source.ToString()
            : $"{entry.Source} ({entry.SrcPath})";
    }
}

#pragma warning restore CA1822
