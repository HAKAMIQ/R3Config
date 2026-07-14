using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchHashProbeService
{
    private const int MaxProbeBytes = 16 * 1024 * 1024;
    private const long MaxCompressedProbeBytes = 64L * 1024 * 1024;
    private const long MaxExpandedProbeBytes = 256L * 1024 * 1024;
    private const int StreamBufferBytes = 64 * 1024;

    private readonly Dictionary<string, CachedMetadata> _metadataCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SerialSessionRegex = new(
        @"^[^\r\n]{0,128}\bSYS:\s*Serial:\s*(?<serial>[A-Z0-9]{9})\b",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase |
        RegexOptions.NonBacktracking |
        RegexOptions.Multiline);

    private static readonly Regex PpuHashRegex = new(
        @"\bPPU executable hash:\s*PPU-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex SpuHashRegex = new(
        @"\b(?:SPU executable hash:|Loaded SPU image:)\s*SPU-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex PrxHashRegex = new(
        @"\bPRX hash of [^\r\n]*?:\s*PRX-(?<hash>[0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz]{28}-[0-9]+)",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

    private static readonly Regex OvlHashRegex = new(
        @"\bOVL hash of [^\r\n]*?:\s*OVL-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex CachePpuHashRegex = new(
        @"^ppu-(?<hash>[0-9A-Za-z]{28}|[0-9a-f]{40})(?:-(?<module>[^\\/:*?\x22<>|]+))?$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex CacheSpuHashRegex = new(
        @"^spu-(?<hash>[0-9a-f]{40})(?:-|$)",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    public Rpcs3GameDescriptor Enrich(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        var existingPpuHashes = NormalizeHashes(game.AllPpuHashes);
        var existingSpuHashes = NormalizeHashes(game.AllSpuHashes);
        var existingPrxHashes = NormalizeModuleHashes(
            PatchHashKind.Prx,
            game.AllPrxHashes);
        var existingOvlHashes = NormalizeModuleHashes(
            PatchHashKind.Ovl,
            game.AllOvlHashes);
        var existingAuthoritativePpuHashes = NormalizeHashes(
            game.AllAuthoritativePpuHashes);
        var normalizedGame = game with
        {
            PpuHash = existingPpuHashes.FirstOrDefault(),
            SpuHash = existingSpuHashes.FirstOrDefault(),
            PpuHashes = existingPpuHashes,
            SpuHashes = existingSpuHashes,
            PrxHashes = existingPrxHashes,
            OvlHashes = existingOvlHashes,
            AuthoritativePpuHashes = existingAuthoritativePpuHashes
        };

        var result = Probe(install, normalizedGame);
        var ppuHashes = NormalizeHashes(existingPpuHashes.Concat(result.AllPpuHashes));
        var spuHashes = NormalizeHashes(existingSpuHashes.Concat(result.AllSpuHashes));
        var prxHashes = NormalizeModuleHashes(
            PatchHashKind.Prx,
            existingPrxHashes.Concat(result.AllPrxHashes));
        var ovlHashes = NormalizeModuleHashes(
            PatchHashKind.Ovl,
            existingOvlHashes.Concat(result.AllOvlHashes));
        var authoritativePpuHashes = NormalizeHashes(
            existingAuthoritativePpuHashes.Concat(
                result.AllAuthoritativePpuHashes));

        return game with
        {
            PpuHash = ppuHashes.FirstOrDefault(),
            SpuHash = spuHashes.FirstOrDefault(),
            PpuHashes = ppuHashes,
            SpuHashes = spuHashes,
            PrxHashes = prxHashes,
            OvlHashes = ovlHashes,
            AuthoritativePpuHashes = authoritativePpuHashes,
            HashSource = result.HashAvailable
                ? result.SourcePath
                : game.HashSource,
            HashMessage = result.Message,
            HashEvidenceKind = result.HashAvailable
                ? result.EvidenceKind
                : game.HashEvidenceKind,
            HashEvidenceTimestampUtc = result.HashAvailable
                ? result.SourceTimestampUtc
                : game.HashEvidenceTimestampUtc
        };
    }

    public IReadOnlyList<Rpcs3GameDescriptor> Enrich(
        Rpcs3InstallDescriptor install,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return games.Select(game => Enrich(install, game)).ToArray();
    }

    public PatchHashProbeResult Probe(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        if (string.IsNullOrWhiteSpace(game.Serial))
        {
            return PatchHashProbeResult.Unknown(
                "Game serial is unavailable; patch hash remains unknown.");
        }

        var existingPpuHashes = NormalizeHashes(game.AllPpuHashes);
        var existingSpuHashes = NormalizeHashes(game.AllSpuHashes);
        var existingPrxHashes = NormalizeModuleHashes(
            PatchHashKind.Prx,
            game.AllPrxHashes);
        var existingOvlHashes = NormalizeModuleHashes(
            PatchHashKind.Ovl,
            game.AllOvlHashes);
        var existingAuthoritativePpuHashes = NormalizeHashes(
            game.AllAuthoritativePpuHashes);
        if (existingPpuHashes.Count > 0 ||
            existingSpuHashes.Count > 0 ||
            existingPrxHashes.Count > 0 ||
            existingOvlHashes.Count > 0)
        {
            var existingKind = game.HashEvidenceKind == PatchHashEvidenceKind.None
                ? PatchHashEvidenceKind.ExistingMetadata
                : game.HashEvidenceKind;

            return CreateResult(
                existingPpuHashes,
                existingSpuHashes,
                existingPrxHashes,
                existingOvlHashes,
                existingKind,
                game.HashSource,
                game.HashEvidenceTimestampUtc,
                "Patch hashes came from existing game metadata.",
                existingAuthoritativePpuHashes);
        }

        var evidence = new List<PatchHashProbeResult>();

        foreach (var candidate in EnumerateCandidateMetadataFiles(install))
        {
            var result = TryProbeFile(candidate, game.Serial);
            if (result.HashAvailable)
            {
                evidence.Add(result);
            }
        }

        var cacheResults = EnumerateCandidateCacheEntries(install, game.Serial)
            .Select(path =>
            {
                var moduleName = ExtractPpuCacheModuleName(path);
                return new CacheProbe(
                    moduleName,
                    TryProbeCacheEntry(
                        path,
                        FindLatestExecutableWriteUtc(
                            install,
                            game,
                            moduleName)));
            })
            .Where(item => item.Result.HashAvailable)
            .ToArray();

        if (cacheResults.Length > 0)
        {
            var newestCacheResult = cacheResults
                .Select(item => item.Result)
                .OrderByDescending(result =>
                    result.SourceTimestampUtc ?? DateTime.MinValue)
                .First();
            var currentModuleHashes = cacheResults
                .Where(item =>
                    item.ModuleName is not null &&
                    item.Result.AllAuthoritativePpuHashes.Count > 0)
                .GroupBy(
                    item => item.ModuleName!,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(item =>
                        item.Result.SourceTimestampUtc ?? DateTime.MinValue)
                    .First()
                    .Result)
                .SelectMany(result => result.AllAuthoritativePpuHashes);
            evidence.Add(CreateResult(
                NormalizeHashes(cacheResults.SelectMany(item => item.Result.AllPpuHashes)),
                NormalizeHashes(cacheResults.SelectMany(item => item.Result.AllSpuHashes)),
                NormalizeModuleHashes(
                    PatchHashKind.Prx,
                    cacheResults.SelectMany(item => item.Result.AllPrxHashes)),
                NormalizeModuleHashes(
                    PatchHashKind.Ovl,
                    cacheResults.SelectMany(item => item.Result.AllOvlHashes)),
                PatchHashEvidenceKind.GameCache,
                newestCacheResult.SourcePath,
                newestCacheResult.SourceTimestampUtc,
                $"Found {cacheResults.SelectMany(item => item.Result.AllPpuHashes).Distinct(StringComparer.OrdinalIgnoreCase).Count()} PPU and " +
                $"{cacheResults.SelectMany(item => item.Result.AllSpuHashes).Distinct(StringComparer.OrdinalIgnoreCase).Count()} SPU hash(es) " +
                 "in the bounded RPCS3 per-game cache probe.",
                NormalizeHashes(currentModuleHashes)));
        }

        var newestLog = evidence
            .Where(result => result.EvidenceKind is
                PatchHashEvidenceKind.PlainLog or
                PatchHashEvidenceKind.CompressedLog)
            .OrderByDescending(result =>
                result.SourceTimestampUtc ?? DateTime.MinValue)
            .ThenBy(result => EvidenceRank(result.EvidenceKind))
            .FirstOrDefault();
        var cache = evidence.FirstOrDefault(result =>
            result.EvidenceKind == PatchHashEvidenceKind.GameCache);
        var selectedEvidence = new[] { newestLog, cache }
            .Where(result => result is not null)
            .Select(result => result!)
            .ToArray();

        if (selectedEvidence.Length > 0)
        {
            var newest = selectedEvidence
                .OrderByDescending(result =>
                    result.SourceTimestampUtc ?? DateTime.MinValue)
                .ThenBy(result => EvidenceRank(result.EvidenceKind))
                .First();

            return CreateResult(
                NormalizeHashes(selectedEvidence.SelectMany(result => result.AllPpuHashes)),
                NormalizeHashes(selectedEvidence.SelectMany(result => result.AllSpuHashes)),
                NormalizeModuleHashes(
                    PatchHashKind.Prx,
                    selectedEvidence.SelectMany(result => result.AllPrxHashes)),
                NormalizeModuleHashes(
                    PatchHashKind.Ovl,
                    selectedEvidence.SelectMany(result => result.AllOvlHashes)),
                newest.EvidenceKind,
                newest.SourcePath,
                newest.SourceTimestampUtc,
                $"Collected RPCS3 runtime identity evidence from {selectedEvidence.Length} bounded source(s).",
                NormalizeHashes(selectedEvidence.SelectMany(result =>
                    result.AllAuthoritativePpuHashes)));
        }

        return PatchHashProbeResult.Unknown(
            "No RPCS3 log session or bounded per-game cache probe exposed a trustworthy 40-hex executable hash. " +
            "Start the game once with a cold boot, close RPCS3, then analyze again.");
    }

    private static IEnumerable<MetadataCandidate> EnumerateCandidateMetadataFiles(
        Rpcs3InstallDescriptor install)
    {
        var configBase = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var roots = new[]
        {
            install.RootPath,
            configBase,
            install.ConfigPath,
            portablePath
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase);

        var paths = roots
            .SelectMany(root => new[]
            {
                Path.Combine(root, "RPCS3.log"),
                Path.Combine(root, "rpcs3.log"),
                Path.Combine(root, "RPCS3.log.gz"),
                Path.Combine(root, "rpcs3.log.gz"),
                Path.Combine(root, "log", "RPCS3.log"),
                Path.Combine(root, "log", "rpcs3.log"),
                Path.Combine(root, "log", "RPCS3.log.gz"),
                Path.Combine(root, "log", "rpcs3.log.gz")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException)
            {
                continue;
            }

            yield return new MetadataCandidate(
                path,
                path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? PatchHashEvidenceKind.CompressedLog
                    : PatchHashEvidenceKind.PlainLog,
                info.LastWriteTimeUtc);
        }
    }

    private static IEnumerable<string> EnumerateCandidateCacheEntries(
        Rpcs3InstallDescriptor install,
        string serial)
    {
        var configBase = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var candidateDirs = new[]
        {
            Path.Combine(install.RootPath, "cache", serial),
            configBase is null ? null : Path.Combine(configBase, "cache", serial),
            Path.Combine(portablePath, "cache", serial)
        };

        foreach (var cacheDir in candidateDirs
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(cacheDir))
            {
                continue;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory
                    .EnumerateFileSystemEntries(cacheDir)
                    .Take(2048)
                    .ToArray();
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }

    private static string? ExtractPpuCacheModuleName(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var match = CachePpuHashRegex.Match(name);
        if (!match.Success || !match.Groups["module"].Success)
        {
            return null;
        }

        var moduleName = match.Groups["module"].Value;
        return moduleName.Equals(
                   Path.GetFileName(moduleName),
                   StringComparison.Ordinal)
            ? moduleName
            : null;
    }

    private static DateTime? FindLatestExecutableWriteUtc(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return null;
        }

        var dataRoot = install.DataRootPath ??
                       Path.GetDirectoryName(install.ConfigPath) ??
                       install.RootPath;
        var candidates = new[]
        {
            Path.Combine(
                game.InstallPath,
                "PS3_GAME",
                "USRDIR",
                moduleName),
            Path.Combine(
                game.InstallPath,
                "USRDIR",
                moduleName),
            Path.Combine(
                dataRoot,
                "dev_hdd0",
                "game",
                game.Serial,
                "USRDIR",
                moduleName)
        };

        DateTime? latest = null;
        foreach (var path in candidates.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var timestamp = File.GetLastWriteTimeUtc(path);
                if (latest is null || timestamp > latest)
                {
                    latest = timestamp;
                }
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException)
            {
                // Independent evidence sources remain usable.
            }
        }

        return latest;
    }

    private PatchHashProbeResult TryProbeFile(
        MetadataCandidate candidate,
        string serial)
    {
        try
        {
            var segments = ReadMetadataSegmentsCached(candidate);
            if (segments.All(string.IsNullOrWhiteSpace))
            {
                return PatchHashProbeResult.Unknown("Metadata file is empty.");
            }

            // Segments are ordered newest-first. Keeping the head and tail as
            // separate inputs avoids attributing hashes across a skipped
            // middle region while still finding boot evidence near the start
            // of large RPCS3 logs.
            foreach (var segment in segments)
            {
                foreach (var session in ExtractMatchingSessionsNewestFirst(
                             segment,
                             serial))
                {
                    var ppuHashes = ExtractExecutableHashes(
                        PpuHashRegex,
                        session);
                    var spuHashes = ExtractExecutableHashes(
                        SpuHashRegex,
                        session);
                    var prxHashes = ExtractModuleHashes(
                        PrxHashRegex,
                        session,
                        PatchHashKind.Prx);
                    var ovlHashes = ExtractModuleHashes(
                        OvlHashRegex,
                        session,
                        PatchHashKind.Ovl);
                    if (ppuHashes.Count == 0 &&
                        spuHashes.Count == 0 &&
                        prxHashes.Count == 0 &&
                        ovlHashes.Count == 0)
                    {
                        continue;
                    }

                    return CreateResult(
                        ppuHashes,
                        spuHashes,
                        prxHashes,
                        ovlHashes,
                        candidate.Kind,
                        candidate.Path,
                        candidate.LastWriteTimeUtc,
                        $"Found {ppuHashes.Count} PPU, {spuHashes.Count} SPU, {prxHashes.Count} PRX, and {ovlHashes.Count} OVL runtime hash(es) " +
                        "inside the newest matching SYS: Serial session.",
                        ppuHashes);
                }
            }

            return PatchHashProbeResult.Unknown(
                "No exact executable hash was found inside a matching SYS: Serial session.");
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return PatchHashProbeResult.Unknown(ex.Message);
        }
    }

    private static PatchHashProbeResult TryProbeCacheEntry(
        string path,
        DateTime? latestExecutableWriteUtc)
    {
        try
        {
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(name))
            {
                return PatchHashProbeResult.Unknown("Cache entry has no name.");
            }

            var ppuHashes = ExtractExecutableHashes(
                CachePpuHashRegex,
                name);
            var spuHashes = ExtractExecutableHashes(
                CacheSpuHashRegex,
                name);
            if (ppuHashes.Count == 0 && spuHashes.Count == 0)
            {
                return PatchHashProbeResult.Unknown(
                    "Cache entry does not expose an exact 40-hex patch hash.");
            }

            var cacheTimestamp = File.GetLastWriteTimeUtc(path);
            var isCurrentInstalledModule =
                ppuHashes.Count > 0 &&
                latestExecutableWriteUtc is { } executableTimestamp &&
                cacheTimestamp >= executableTimestamp;

            return CreateResult(
                ppuHashes,
                spuHashes,
                [],
                [],
                PatchHashEvidenceKind.GameCache,
                path,
                cacheTimestamp,
                "Patch hash found in the RPCS3 per-game cache.",
                isCurrentInstalledModule ? ppuHashes : []);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return PatchHashProbeResult.Unknown(ex.Message);
        }
    }

    private static IReadOnlyList<string> ExtractMatchingSessionsNewestFirst(
        string text,
        string serial)
    {
        var markers = SerialSessionRegex.Matches(text)
            .Cast<Match>()
            .ToArray();

        var sessions = new List<string>();
        for (var index = markers.Length - 1; index >= 0; index--)
        {
            var marker = markers[index];
            if (!marker.Groups["serial"].Value.Equals(
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = marker.Index;
            var end = index + 1 < markers.Length
                ? markers[index + 1].Index
                : text.Length;

            if (end > start)
            {
                sessions.Add(text[start..end]);
            }
        }

        return sessions;
    }

    private static IReadOnlyList<string> ExtractExecutableHashes(
        Regex regex,
        string text)
    {
        return regex.Matches(text)
            .Select(match => NormalizeExecutableHash(
                match.Groups["hash"].Value))
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractModuleHashes(
        Regex regex,
        string text,
        PatchHashKind kind)
    {
        return regex.Matches(text)
            .Select(match =>
                Rpcs3PatchRuntimeKey.TryNormalizeEvidence(
                    kind,
                    match.Groups["hash"].Value,
                    out var normalized)
                    ? normalized
                    : null)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeHashes(
        IEnumerable<string> hashes)
    {
        return hashes
            .Select(NormalizeExecutableHash)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PatchHashProbeResult CreateResult(
        IReadOnlyList<string> ppuHashes,
        IReadOnlyList<string> spuHashes,
        IReadOnlyList<string> prxHashes,
        IReadOnlyList<string> ovlHashes,
        PatchHashEvidenceKind evidenceKind,
        string? sourcePath,
        DateTime? sourceTimestampUtc,
        string message,
        IReadOnlyList<string>? authoritativePpuHashes = null)
    {
        var normalizedPpuHashes = NormalizeHashes(ppuHashes);
        var normalizedSpuHashes = NormalizeHashes(spuHashes);
        var normalizedPrxHashes = NormalizeModuleHashes(
            PatchHashKind.Prx,
            prxHashes);
        var normalizedOvlHashes = NormalizeModuleHashes(
            PatchHashKind.Ovl,
            ovlHashes);

        if (normalizedPpuHashes.Count == 0 &&
            normalizedSpuHashes.Count == 0 &&
            normalizedPrxHashes.Count == 0 &&
            normalizedOvlHashes.Count == 0)
        {
            return PatchHashProbeResult.Unknown(message);
        }

        return new PatchHashProbeResult(
            PpuHash: normalizedPpuHashes.FirstOrDefault(),
            SpuHash: normalizedSpuHashes.FirstOrDefault(),
            EvidenceKind: evidenceKind,
            SourcePath: sourcePath,
            SourceTimestampUtc: sourceTimestampUtc,
            HashAvailable: true,
            Message: message,
            PpuHashes: normalizedPpuHashes,
            SpuHashes: normalizedSpuHashes,
            PrxHashes: normalizedPrxHashes,
            OvlHashes: normalizedOvlHashes,
            AuthoritativePpuHashes: NormalizeHashes(
                authoritativePpuHashes ?? []));
    }

    private static IReadOnlyList<string> NormalizeModuleHashes(
        PatchHashKind kind,
        IEnumerable<string> hashes)
    {
        return hashes
            .Select(hash =>
                Rpcs3PatchRuntimeKey.TryNormalizeEvidence(
                    kind,
                    hash,
                    out var normalized)
                    ? normalized
                    : null)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeExecutableHash(string? hash)
    {
        return Rpcs3ExecutableHash.TryNormalize(
            hash,
            out var normalized)
            ? normalized
            : null;
    }

    private IReadOnlyList<string> ReadMetadataSegmentsCached(
        MetadataCandidate candidate)
    {
        var info = new FileInfo(candidate.Path);
        if (_metadataCache.TryGetValue(candidate.Path, out var cached) &&
            cached.Length == info.Length &&
            cached.LastWriteTimeUtc == info.LastWriteTimeUtc &&
            cached.Kind == candidate.Kind)
        {
            return cached.Segments;
        }

        var segments = candidate.Kind == PatchHashEvidenceKind.CompressedLog
            ? ReadGzipHeadAndTail(
                candidate.Path,
                MaxProbeBytes,
                MaxCompressedProbeBytes,
                MaxExpandedProbeBytes)
            : ReadHeadAndTail(candidate.Path, MaxProbeBytes);

        _metadataCache[candidate.Path] = new CachedMetadata(
            info.Length,
            info.LastWriteTimeUtc,
            candidate.Kind,
            segments);

        return segments;
    }

    private static IReadOnlyList<string> ReadHeadAndTail(
        string path,
        int maxBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length == 0)
        {
            return [];
        }

        if (stream.Length <= maxBytes)
        {
            return
            [
                ReadRange(stream, 0, (int)stream.Length)
            ];
        }

        var segmentBytes = Math.Max(1, maxBytes / 2);
        var head = ReadRange(stream, 0, segmentBytes);
        var tail = ReadRange(
            stream,
            stream.Length - segmentBytes,
            segmentBytes);

        // Search the tail first because it contains the newest boot session.
        return [tail, head];
    }

    private static string ReadRange(
        Stream stream,
        long offset,
        int bytesToRead)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[bytesToRead];
        var totalRead = 0;
        while (totalRead < bytesToRead)
        {
            var read = stream.Read(
                buffer,
                totalRead,
                bytesToRead - totalRead);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    private static IReadOnlyList<string> ReadGzipHeadAndTail(
        string path,
        int maxBytes,
        long maxCompressedBytes,
        long maxExpandedBytes)
    {
        using var fileStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (fileStream.Length > maxCompressedBytes)
        {
            throw new InvalidDataException(
                $"Compressed RPCS3 log exceeds the {maxCompressedBytes / (1024 * 1024)} MiB safety limit.");
        }

        using var gzipStream = new GZipStream(
            fileStream,
            CompressionMode.Decompress,
            leaveOpen: false);

        var headCapacity = Math.Max(1, maxBytes / 2);
        var head = new byte[headCapacity];
        var headCount = 0;
        // Keep a full maxBytes tail so a log that fits the original bound is
        // still parsed as one continuous document.
        var ring = new byte[maxBytes];
        var buffer = new byte[StreamBufferBytes];
        var writeOffset = 0;
        var retainedCount = 0;
        long expandedBytes = 0;

        while (true)
        {
            var read = gzipStream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            expandedBytes += read;
            if (expandedBytes > maxExpandedBytes)
            {
                throw new InvalidDataException(
                    $"Expanded RPCS3 log exceeds the {maxExpandedBytes / (1024 * 1024)} MiB safety limit.");
            }

            if (headCount < head.Length)
            {
                var headRead = Math.Min(read, head.Length - headCount);
                Buffer.BlockCopy(buffer, 0, head, headCount, headRead);
                headCount += headRead;
            }

            AppendToRing(
                ring,
                buffer,
                read,
                ref writeOffset,
                ref retainedCount);
        }

        if (retainedCount == 0)
        {
            return [];
        }

        var ordered = new byte[retainedCount];
        if (retainedCount < ring.Length)
        {
            Buffer.BlockCopy(ring, 0, ordered, 0, retainedCount);
        }
        else
        {
            var firstLength = ring.Length - writeOffset;
            Buffer.BlockCopy(ring, writeOffset, ordered, 0, firstLength);
            Buffer.BlockCopy(ring, 0, ordered, firstLength, writeOffset);
        }

        if (expandedBytes <= maxBytes)
        {
            return
            [
                Encoding.UTF8.GetString(ordered)
            ];
        }

        var tailCount = Math.Min(headCapacity, ordered.Length);
        var tail = Encoding.UTF8.GetString(
            ordered,
            ordered.Length - tailCount,
            tailCount);
        var headText = Encoding.UTF8.GetString(head, 0, headCount);

        return [tail, headText];
    }

    private static void AppendToRing(
        byte[] ring,
        byte[] source,
        int sourceCount,
        ref int writeOffset,
        ref int retainedCount)
    {
        if (sourceCount >= ring.Length)
        {
            Buffer.BlockCopy(
                source,
                sourceCount - ring.Length,
                ring,
                0,
                ring.Length);

            writeOffset = 0;
            retainedCount = ring.Length;
            return;
        }

        var firstLength = Math.Min(sourceCount, ring.Length - writeOffset);
        Buffer.BlockCopy(source, 0, ring, writeOffset, firstLength);

        var remaining = sourceCount - firstLength;
        if (remaining > 0)
        {
            Buffer.BlockCopy(source, firstLength, ring, 0, remaining);
        }

        writeOffset = (writeOffset + sourceCount) % ring.Length;
        retainedCount = Math.Min(ring.Length, retainedCount + sourceCount);
    }

    private static int EvidenceRank(PatchHashEvidenceKind kind)
    {
        return kind switch
        {
            PatchHashEvidenceKind.PlainLog => 0,
            PatchHashEvidenceKind.CompressedLog => 1,
            PatchHashEvidenceKind.GameCache => 2,
            PatchHashEvidenceKind.ExistingMetadata => 3,
            _ => 4
        };
    }

    private sealed record MetadataCandidate(
        string Path,
        PatchHashEvidenceKind Kind,
        DateTime LastWriteTimeUtc);

    private sealed record CachedMetadata(
        long Length,
        DateTime LastWriteTimeUtc,
        PatchHashEvidenceKind Kind,
        IReadOnlyList<string> Segments);

    private sealed record CacheProbe(
        string? ModuleName,
        PatchHashProbeResult Result);
}
