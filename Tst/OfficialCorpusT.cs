using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

public static class OfficialCorpusT
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    public const string ExpectedSnapshotDate = "2026-08-25";
    public const long ExpectedSize = 897858;
    public const string ExpectedSha256 =
        "45F746AFC3D4EEA2450F32F92769BB555029807497CFF3512B633428EF74E8AD";

    public static void Run(
        string sourcePath,
        string? reportPath = null)
    {
        var before = VerifyIdentity(sourcePath);
        var first = Analyze(sourcePath);
        var second = Analyze(sourcePath);

        Assert(first.Version == "1.2", "Official schema version is not 1.2.");
        Assert(first.ParseOk, "Production ReadSvc rejected the official snapshot: " + first.Error);
        Assert(first.Unaccounted == 0, $"Official definitions are unaccounted: {first.Unaccounted}.");
        Assert(first.UnexplainedProductionExtras == 0,
            $"Production parser extras are unexplained: {first.UnexplainedProductionExtras}.");
        Assert(first.StructuralDefinitions == first.ParsedStructuralDefinitions +
            first.ExplicitUnsupportedDefinitions + first.ExplicitRejectedDefinitions + first.Unaccounted,
            "Independent structural accounting does not balance.");
        Assert(first.Fingerprint == second.Fingerprint,
            "Repeated production corpus fingerprints differ.");
        Assert(Analysis.VerifySourceUnchanged(sourcePath, before),
            "The official source changed during read-only regression.");

        var report = new
        {
            Source = sourcePath,
            SnapshotDate = ExpectedSnapshotDate,
            Revision = "Unavailable",
            Size = before.Length,
            Sha256 = before.Sha256,
            SourceLastWriteTimeUtc = before.LastWriteTimeUtc,
            Schema = first.Version,
            first.AnchorCount,
            first.RuntimeKeys,
            first.PpuRuntimeKeys,
            first.SpuRuntimeKeys,
            first.OtherRuntimeKeys,
            MalformedRuntimeKeys = first.MalformedRuntimeKeys,
            first.MalformedRuntimeDefinitions,
            first.StructuralDefinitions,
            first.ProductionEntries,
            first.ParsedStructuralDefinitions,
            first.ExplicitUnsupportedDefinitions,
            first.ExplicitRejectedDefinitions,
            first.UnaccountedDefinitions,
            first.ExpandedProductionEntries,
            first.UnexplainedProductionExtras,
            first.ExpandedExtraLinks,
            CorpusFingerprintRun1 = first.Fingerprint,
            CorpusFingerprintRun2 = second.Fingerprint,
            first.TotalSerialOccurrences,
            first.UniqueSerials,
            first.UniqueVersions,
            first.VersionScopedDefinitions,
            first.AllVersionDefinitions,
            first.GlobalDefinitions,
            first.EntriesWithAuthor,
            first.EntriesWithNotes,
            first.EntriesWithGroup,
            first.EntriesWithPatchVersion,
            first.EntriesWithDependencies,
            first.EntriesWithPatchBody,
            first.DuplicateIdentities,
            first.SourceUnchanged,
            Deterministic = first.Fingerprint == second.Fingerprint,
            first.Diagnostics,
            MalformedDiagnosticKeys = Regex.Matches(first.Diagnostics, @"Skipping '([^']+)'")
                .Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            first.Representatives
        };

        reportPath ??= Path.Combine(Path.GetTempPath(), "official-corpus-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, ReportOptions));
        Console.WriteLine(JsonSerializer.Serialize(report, ReportOptions));
        Console.WriteLine($"Official corpus regression PASS. Report: {reportPath}");
    }

    private static SnapshotIdentity VerifyIdentity(string path)
    {
        Assert(File.Exists(path), "Official snapshot file does not exist.");
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert(bytes.LongLength == ExpectedSize, "OFFICIAL_SNAPSHOT_IDENTITY_MISMATCH: size.");
        Assert(hash.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase),
            "OFFICIAL_SNAPSHOT_IDENTITY_MISMATCH: SHA-256.");
        return new SnapshotIdentity(bytes, hash, File.GetLastWriteTimeUtc(path));
    }

    private static Analysis Analyze(string path)
    {
        var structural = Inventory(path);
        var read = new ReadSvc().Read(path, PatSrc.Official);
        var entries = read.Catalog.Entries;
        var parsed = entries
            .Select(e => (Key: e.RunKey.ToUpperInvariant(), e.Title))
            .ToHashSet();
        var structuralSet = structural.Definitions.ToHashSet();
        var parsedStructural = structuralSet.Count(parsed.Contains);
        var extras = parsed.Where(d => !structuralSet.Contains(d)).ToArray();
        var expanded = 0;
        var expandedLinks = extras
            .Select(extra => new
            {
                Extra = extra,
                Source = structural.Definitions.FirstOrDefault(s =>
                    s.Key.Equals(extra.Key, StringComparison.OrdinalIgnoreCase) &&
                    s.Title.Equals(extra.Title, StringComparison.OrdinalIgnoreCase))
            })
            .Where(link => !string.IsNullOrEmpty(link.Source.Key))
            .Select(link => $"{link.Extra.Key}/{link.Extra.Title} <= {link.Source.Key}/{link.Source.Title}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unexplained = extras.Length - expanded;
        var diagnostics = read.Error ?? string.Empty;
        var explicitUnsupported = 0;
        var explicitRejected = 0;
        var unaccounted = 0;
        foreach (var definition in structuralSet)
        {
            if (parsed.Contains(definition)) continue;
            var diagnostic = diagnostics.Split('\n').FirstOrDefault(line =>
                line.Contains(definition.Key, StringComparison.OrdinalIgnoreCase) &&
                line.Contains(definition.Title, StringComparison.OrdinalIgnoreCase));
            var keyDiagnostic = diagnostics.Split('\n').FirstOrDefault(line =>
                line.Contains(definition.Key, StringComparison.OrdinalIgnoreCase));
            if (diagnostic is null && keyDiagnostic is null)
            {
                unaccounted++;
            }
            else if ((diagnostic ?? keyDiagnostic)!.Contains("reject", StringComparison.OrdinalIgnoreCase) ||
                     (diagnostic ?? keyDiagnostic)!.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                     (diagnostic ?? keyDiagnostic)!.Contains("not generated", StringComparison.OrdinalIgnoreCase))
            {
                explicitRejected++;
            }
            else
            {
                explicitUnsupported++;
            }
        }
        var identities = entries.Select(GameSetupPlanningService.PatchIdentity).ToArray();
        var duplicateIdentities = identities.Length - identities.Distinct(StringComparer.Ordinal).Count();

        var serials = entries.SelectMany(e => e.Serials).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        var versions = entries.SelectMany(e => e.GameVer).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        var canonical = entries
            .Select(e => string.Join("|", GameSetupPlanningService.PatchIdentity(e), e.RunKey,
                e.Title, string.Join(",", e.Serials), string.Join(",", e.GameVer), e.Author,
                e.Group, e.PatVer, e.DefPrint))
            .Order(StringComparer.Ordinal)
            .Concat(diagnostics.Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join("\n", canonical)))).ToLowerInvariant();
        var reps = new[]
        {
            Pick(entries, e => e.HashKind == HashKind.Ppu, "PPU"),
            Pick(entries, e => e.HashKind == HashKind.Spu, "SPU"),
            Pick(entries, e => e.Patches.Count > 0, "DEPENDENCY"),
            Pick(entries, e => !string.IsNullOrWhiteSpace(e.Group), "GROUP"),
            Pick(entries, e => !string.IsNullOrWhiteSpace(e.Notes), "NOTES"),
            Pick(entries, e => !string.IsNullOrWhiteSpace(e.PatVer), "PATCH_VERSION"),
            Pick(entries, e => e.Serials.Count > 1, "MULTI_SERIAL"),
            Pick(entries, e => e.GameVer.Count > 1, "MULTI_VERSION"),
            Pick(entries, e => e.GameTgt.Any(t => t.Title.Equals("All", StringComparison.OrdinalIgnoreCase) || t.Serial.Equals("All", StringComparison.OrdinalIgnoreCase)), "GLOBAL")
        }.Where(x => x is not null).Cast<string>().ToArray();

        return new Analysis(
            read.IsOk,
            read.Error,
            read.Catalog.Version,
            structural.AnchorCount,
            structural.RuntimeKeys,
            structural.PpuRuntimeKeys,
            structural.SpuRuntimeKeys,
            structural.OtherRuntimeKeys,
            structural.MalformedRuntimeKeys,
            structural.MalformedRuntimeDefinitions,
            structuralSet.Count,
            parsedStructural,
            entries.Count,
            explicitUnsupported,
            explicitRejected,
            unaccounted,
            entries.Sum(e => e.Serials.Count),
            serials.Length,
            versions.Length,
            entries.Count(e => e.GameVer.Count > 0),
            entries.Count(e => e.GameTgt.Any(t => t.Versions.Count == 0)),
            entries.Count(e => e.GameTgt.Any(t => t.Title.Equals("All", StringComparison.OrdinalIgnoreCase) || t.Serial.Equals("All", StringComparison.OrdinalIgnoreCase))),
            entries.Count(e => !string.IsNullOrWhiteSpace(e.Author)),
            entries.Count(e => !string.IsNullOrWhiteSpace(e.Notes)),
            entries.Count(e => !string.IsNullOrWhiteSpace(e.Group)),
            entries.Count(e => !string.IsNullOrWhiteSpace(e.PatVer)),
            entries.Count(e => e.Patches.Count > 0),
            entries.Count(e => !string.IsNullOrWhiteSpace(e.DefPrint)),
            duplicateIdentities,
            true,
            diagnostics,
            reps,
            true,
            expanded,
            unexplained,
            fingerprint,
            expandedLinks);
    }

    private static string? Pick(IEnumerable<Rpcs3PatchEntry> entries, Func<Rpcs3PatchEntry, bool> predicate, string label)
    {
        var e = entries.FirstOrDefault(predicate);
        return e is null ? null : $"{label}: {e.RunKey} / {e.Title}";
    }

    private static Structural Inventory(string path)
    {
        var text = R3Bound.ReadUtf8(path, R3Limits.PatCatBy, "official patch.yml");
        var defs = new List<(string Key, string Title)>();
        var runtime = 0; var ppu = 0; var spu = 0; var other = 0;
        string? currentKey = null;
        var currentValid = false;
        HashKind currentKind = HashKind.Unknown;
        string? currentHash = null;
        string? currentScope = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line.TrimStart().StartsWith('#')) continue;
            var indent = line.Length - line.TrimStart().Length;
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var trimmed = line.Trim();
            var key = indent == 2 && trimmed.StartsWith('"') && trimmed.LastIndexOf('"') > 0
                ? trimmed[1..trimmed.LastIndexOf('"')]
                : indent == 2 && trimmed.EndsWith(':')
                ? trimmed[..^1].Trim().Trim('"', '\'')
                : line[..colon].Trim().Trim('"', '\'');
            if (indent == 0)
            {
                currentKey = null;
                currentValid = false;
                if (RunKey.TryParse(key, out var rk))
                {
                    currentKey = key;
                    currentValid = true;
                    currentKind = rk.Kind;
                    currentHash = rk.Hash;
                    currentScope = rk.SerScope;
                    runtime++;
                    if (rk.Kind == HashKind.Ppu) ppu++; else if (rk.Kind == HashKind.Spu) spu++; else other++;
                }
                else if (key.StartsWith("PPU-", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("SPU-", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("OVL-", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("PRX-", StringComparison.OrdinalIgnoreCase))
                {
                    currentKey = key;
                }
            }
            else if (indent is > 0 and <= 2 && currentKey is not null &&
                     (trimmed.StartsWith('"') || trimmed.EndsWith(':')))
            {
                var structuralKey = currentValid
                    ? RunKey.Build(currentKind, currentHash!, currentScope)
                    : currentKey;
                defs.Add((structuralKey.ToUpperInvariant(), key));
            }
        }
        var anchors = Regex.Count(text, @"(?m)^\s*[^#\r\n]+:\s*&[A-Za-z0-9_.-]+\b");
        var malformed = defs.Select(d => d.Key)
            .Where(k => !RunKey.TryParse(k, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new Structural(anchors, runtime, ppu, spu, other, defs, malformed,
            defs.Where(d => malformed.Contains(d.Key, StringComparer.OrdinalIgnoreCase)).Count());
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private sealed record SnapshotIdentity(byte[] Bytes, string Sha256, DateTime LastWriteTimeUtc)
    {
        public long Length => Bytes.LongLength;
    }

    private sealed record Structural(int AnchorCount, int RuntimeKeys, int PpuRuntimeKeys, int SpuRuntimeKeys, int OtherRuntimeKeys,
        IReadOnlyList<(string Key, string Title)> Definitions, IReadOnlyList<string> MalformedRuntimeKeys,
        int MalformedRuntimeDefinitions);

    private sealed record Analysis(
        bool ParseOk, string? Error, string? Version, int AnchorCount, int RuntimeKeys, int PpuRuntimeKeys,
            int SpuRuntimeKeys, int OtherRuntimeKeys, IReadOnlyList<string> MalformedRuntimeKeys,
        int MalformedRuntimeDefinitions, int StructuralDefinitions, int ParsedStructuralDefinitions,
        int ProductionEntries, int ExplicitUnsupportedDefinitions, int ExplicitRejectedDefinitions, int UnaccountedDefinitions,
        int TotalSerialOccurrences, int UniqueSerials, int UniqueVersions, int VersionScopedDefinitions,
        int AllVersionDefinitions, int GlobalDefinitions, int EntriesWithAuthor, int EntriesWithNotes,
        int EntriesWithGroup, int EntriesWithPatchVersion, int EntriesWithDependencies, int EntriesWithPatchBody,
        int DuplicateIdentities, bool Deterministic, string Diagnostics, IReadOnlyList<string> Representatives,
        bool SourceUnchanged, int ExpandedProductionEntries, int UnexplainedProductionExtras, string Fingerprint,
        IReadOnlyList<string> ExpandedExtraLinks)
    {
        public int Unaccounted => UnaccountedDefinitions;
        public static bool VerifySourceUnchanged(string path, SnapshotIdentity before) =>
            File.ReadAllBytes(path).AsSpan().SequenceEqual(before.Bytes) &&
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(before.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
