using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed partial class PatchHashProbeService
{
    private const int MaxBytes = 16 * 1024 * 1024;
    private const long MaxByte2 = 64L * 1024 * 1024;
    private const long MaxByte3 = 256L * 1024 * 1024;
    private const int Bytes = 64 * 1024;

    private readonly Dictionary<string, MetaCach> _Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex Regex = new(
        @"^[^\r\n]{0,128}\bSYS:\s*Serial:\s*(?<serial>[A-Z0-9]{9})\b",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase |
        RegexOptions.NonBacktracking |
        RegexOptions.Multiline);

    public Rpcs3GameDescriptor Enrich(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        var existingPpuHashes = Norm(game.Hashes);
        var existingSpuHashes = Norm(game.SpuList);
        var existingPrxHashes = Norm2(
            HashKind.Prx,
            game.PrxList);
        var existingOvlHashes = Norm2(
            HashKind.Ovl,
            game.OvlList);
        var existingAuthoritativePpuHashes = Norm(
            game.PpuList);

        var normalizedGame = game with
        {
            PpuHash = existingPpuHashes.Length == 0
                ? null
                : existingPpuHashes[0],
            SpuHash = existingSpuHashes.Length == 0
                ? null
                : existingSpuHashes[0],
            PpuRaw = existingPpuHashes,
            SpuRaw = existingSpuHashes,
            PrxRaw = existingPrxHashes,
            OvlRaw = existingOvlHashes,
            AuthPpu = existingAuthoritativePpuHashes
        };

        var result = Probe(install, normalizedGame);
        var ppuHashes = Norm(
            existingPpuHashes.Concat(result.Hashes));
        var spuHashes = Norm(
            existingSpuHashes.Concat(result.SpuList));
        var prxHashes = Norm2(
            HashKind.Prx,
            existingPrxHashes.Concat(result.PrxList));
        var ovlHashes = Norm2(
            HashKind.Ovl,
            existingOvlHashes.Concat(result.OvlList));
        var authoritativePpuHashes = Norm(
            existingAuthoritativePpuHashes.Concat(
                result.PpuList));

        return game with
        {
            PpuHash = ppuHashes.Length == 0
                ? null
                : ppuHashes[0],
            SpuHash = spuHashes.Length == 0
                ? null
                : spuHashes[0],
            PpuRaw = ppuHashes,
            SpuRaw = spuHashes,
            PrxRaw = prxHashes,
            OvlRaw = ovlHashes,
            AuthPpu = authoritativePpuHashes,
            HashSrc = result.HasHash
                ? result.SrcPath
                : game.HashSrc,
            HashMsg = result.Message,
            HashEvi = result.HasHash
                ? result.EviKind
                : game.HashEvi,
            HashTime = result.HasHash
                ? result.SrcTime
                : game.HashTime
        };
    }

    public IReadOnlyList<Rpcs3GameDescriptor> Enrich(
        Rpcs3InstallDescriptor install,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return
        [
            .. games.Select(game => Enrich(install, game))
        ];
    }

    public HashRes Probe(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        if (string.IsNullOrWhiteSpace(game.Serial))
        {
            return HashRes.Unknown(
                "Game serial is unavailable; patch hash remains unknown.");
        }

        var existingPpuHashes = Norm(game.Hashes);
        var existingSpuHashes = Norm(game.SpuList);
        var existingPrxHashes = Norm2(
            HashKind.Prx,
            game.PrxList);
        var existingOvlHashes = Norm2(
            HashKind.Ovl,
            game.OvlList);
        var existingAuthoritativePpuHashes = Norm(
            game.PpuList);

        if (existingPpuHashes.Length > 0 ||
            existingSpuHashes.Length > 0 ||
            existingPrxHashes.Length > 0 ||
            existingOvlHashes.Length > 0)
        {
            var existingKind = game.HashEvi == HashEvi.None
                ? HashEvi.Meta
                : game.HashEvi;

            return BuildRes(
                existingPpuHashes,
                existingSpuHashes,
                existingPrxHashes,
                existingOvlHashes,
                existingKind,
                game.HashSrc,
                game.HashTime,
                "Patch hashes came from existing game metadata.",
                existingAuthoritativePpuHashes);
        }

        var evidence = new List<HashRes>();

        foreach (var candidate in ListCand(install))
        {
            var result = TryProbe(
                candidate,
                game.Serial);

            if (result.HasHash)
            {
                evidence.Add(result);
            }
        }

        var cacheResults = ListCan2(
                install,
                game.Serial)
            .Select(path =>
            {
                var moduleName = Extract(path);

                return new CachePrb(
                    moduleName,
                    TryProb2(
                        path,
                        FindUtc(
                            install,
                            game,
                            moduleName)));
            })
            .Where(item => item.Result.HasHash)
            .ToArray();

        if (cacheResults.Length > 0)
        {
            var newestCacheResult = cacheResults
                .Select(item => item.Result)
                .OrderByDescending(result =>
                    result.SrcTime ?? DateTime.MinValue)
                .First();

            var currentModuleHashes = cacheResults
                .Where(item =>
                    item.ModName is not null &&
                    item.Result.PpuList.Count > 0)
                .GroupBy(
                    item => item.ModName!,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(item =>
                        item.Result.SrcTime ?? DateTime.MinValue)
                    .First()
                    .Result)
                .SelectMany(result => result.PpuList);

            evidence.Add(BuildRes(
                Norm(cacheResults.SelectMany(
                    item => item.Result.Hashes)),
                Norm(cacheResults.SelectMany(
                    item => item.Result.SpuList)),
                Norm2(
                    HashKind.Prx,
                    cacheResults.SelectMany(
                        item => item.Result.PrxList)),
                Norm2(
                    HashKind.Ovl,
                    cacheResults.SelectMany(
                        item => item.Result.OvlList)),
                HashEvi.Cache,
                newestCacheResult.SrcPath,
                newestCacheResult.SrcTime,
                $"Found {cacheResults.SelectMany(item => item.Result.Hashes).Distinct(StringComparer.OrdinalIgnoreCase).Count()} PPU and " +
                $"{cacheResults.SelectMany(item => item.Result.SpuList).Distinct(StringComparer.OrdinalIgnoreCase).Count()} SPU hash(es) " +
                "in the bounded RPCS3 per-game cache probe.",
                Norm(currentModuleHashes)));
        }

        var newestLog = evidence
            .Where(result => result.EviKind is
                HashEvi.PlainLog or
                HashEvi.ZipLog)
            .OrderByDescending(result =>
                result.SrcTime ?? DateTime.MinValue)
            .ThenBy(result =>
                EviRank(result.EviKind))
            .FirstOrDefault();

        var cache = evidence.FirstOrDefault(result =>
            result.EviKind == HashEvi.Cache);

        var selectedEvidence = new[]
            {
                newestLog,
                cache
            }
            .Where(result => result is not null)
            .Select(result => result!)
            .ToArray();

        if (selectedEvidence.Length > 0)
        {
            var newest = selectedEvidence
                .OrderByDescending(result =>
                    result.SrcTime ?? DateTime.MinValue)
                .ThenBy(result =>
                    EviRank(result.EviKind))
                .First();

            return BuildRes(
                Norm(selectedEvidence.SelectMany(
                    result => result.Hashes)),
                Norm(selectedEvidence.SelectMany(
                    result => result.SpuList)),
                Norm2(
                    HashKind.Prx,
                    selectedEvidence.SelectMany(
                        result => result.PrxList)),
                Norm2(
                    HashKind.Ovl,
                    selectedEvidence.SelectMany(
                        result => result.OvlList)),
                newest.EviKind,
                newest.SrcPath,
                newest.SrcTime,
                $"Collected RPCS3 runtime identity evidence from {selectedEvidence.Length} bounded source(s).",
                Norm(selectedEvidence.SelectMany(
                    result => result.PpuList)));
        }

        return HashRes.Unknown(
            "No RPCS3 log session or bounded per-game cache probe exposed a trustworthy 40-hex executable hash. " +
            "Start the game once with a cold boot, close RPCS3, then analyze again.");
    }

    private static IEnumerable<MetaCand> ListCand(
        Rpcs3InstallDescriptor install)
    {
        var configBase = Path.GetDirectoryName(
            install.CfgPath);

        var portablePath = Path.Combine(
            install.RootPath,
            "portable");

        var roots = new[]
            {
                install.RootPath,
                configBase,
                install.CfgPath,
                portablePath
            }
            .Where(path =>
                !string.IsNullOrWhiteSpace(path))
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

            yield return new MetaCand(
                path,
                path.EndsWith(
                    ".gz",
                    StringComparison.OrdinalIgnoreCase)
                    ? HashEvi.ZipLog
                    : HashEvi.PlainLog,
                info.LastWriteTimeUtc);
        }
    }

    private static IEnumerable<string> ListCan2(
        Rpcs3InstallDescriptor install,
        string serial)
    {
        var configBase = Path.GetDirectoryName(
            install.CfgPath);

        var portablePath = Path.Combine(
            install.RootPath,
            "portable");

        var candidateDirs = new[]
        {
            Path.Combine(
                install.RootPath,
                "cache",
                serial),
            configBase is null
                ? null
                : Path.Combine(
                    configBase,
                    "cache",
                    serial),
            Path.Combine(
                portablePath,
                "cache",
                serial)
        };

        foreach (var cacheDir in candidateDirs
                     .Where(path =>
                         !string.IsNullOrWhiteSpace(path))
                     .Cast<string>()
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(cacheDir))
            {
                continue;
            }

            string[] entries;

            try
            {
                entries =
                [
                    .. Directory
                        .EnumerateFileSystemEntries(cacheDir)
                        .Take(2048)
                ];
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

    private static string? Extract(string path)
    {
        var name = Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var match = PpuCacheRegex().Match(name);

        if (!match.Success ||
            !match.Groups["module"].Success)
        {
            return null;
        }

        var moduleName =
            match.Groups["module"].Value;

        return moduleName.Equals(
                   Path.GetFileName(moduleName),
                   StringComparison.Ordinal)
            ? moduleName
            : null;
    }

    private static DateTime? FindUtc(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return null;
        }

        var dataRoot =
            install.DataRoot ??
            Path.GetDirectoryName(install.CfgPath) ??
            install.RootPath;

        var candidates = new[]
        {
            Path.Combine(
                game.InstPath,
                "PS3_GAME",
                "USRDIR",
                moduleName),
            Path.Combine(
                game.InstPath,
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

                var timestamp =
                    File.GetLastWriteTimeUtc(path);

                if (latest is null ||
                    timestamp > latest)
                {
                    latest = timestamp;
                }
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException)
            {
            }
        }

        return latest;
    }

    private HashRes TryProbe(
        MetaCand candidate,
        string serial)
    {
        try
        {
            var segments = ReadMeta(candidate);

            if (segments.All(
                    string.IsNullOrWhiteSpace))
            {
                return HashRes.Unknown(
                    "Metadata file is empty.");
            }

            foreach (var segment in segments)
            {
                foreach (var session in Extract2(
                             segment,
                             serial))
                {
                    var ppuHashes = Extract3(
                        PpuHashRegex(),
                        session);

                    var spuHashes = Extract3(
                        SpuHashRegex(),
                        session);

                    var prxHashes = Extract4(
                        PrxHashRegex(),
                        session,
                        HashKind.Prx);

                    var ovlHashes = Extract4(
                        OvlHashRegex(),
                        session,
                        HashKind.Ovl);

                    if (ppuHashes.Length == 0 &&
                        spuHashes.Length == 0 &&
                        prxHashes.Length == 0 &&
                        ovlHashes.Length == 0)
                    {
                        continue;
                    }

                    return BuildRes(
                        ppuHashes,
                        spuHashes,
                        prxHashes,
                        ovlHashes,
                        candidate.Kind,
                        candidate.Path,
                        candidate.WriteUtc,
                        $"Found {ppuHashes.Length} PPU, {spuHashes.Length} SPU, {prxHashes.Length} PRX, and {ovlHashes.Length} OVL runtime hash(es) " +
                        "inside the newest matching SYS: Serial session.",
                        ppuHashes);
                }
            }

            return HashRes.Unknown(
                "No exact executable hash was found inside a matching SYS: Serial session.");
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return HashRes.Unknown(ex.Message);
        }
    }

    private static HashRes TryProb2(
        string path,
        DateTime? latestExecutableWriteUtc)
    {
        try
        {
            var name = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(name))
            {
                return HashRes.Unknown(
                    "Cache entry has no name.");
            }

            var ppuHashes = Extract3(
                PpuCacheRegex(),
                name);

            var spuHashes = Extract3(
                SpuCacheRegex(),
                name);

            if (ppuHashes.Length == 0 &&
                spuHashes.Length == 0)
            {
                return HashRes.Unknown(
                    "Cache entry does not expose an exact 40-hex patch hash.");
            }

            var cacheTimestamp =
                File.GetLastWriteTimeUtc(path);

            var isCurrentInstalledModule =
                ppuHashes.Length > 0 &&
                latestExecutableWriteUtc is
                {
                } executableTimestamp &&
                cacheTimestamp >= executableTimestamp;

            return BuildRes(
                ppuHashes,
                spuHashes,
                [],
                [],
                HashEvi.Cache,
                path,
                cacheTimestamp,
                "Patch hash found in the RPCS3 per-game cache.",
                isCurrentInstalledModule
                    ? ppuHashes
                    : []);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return HashRes.Unknown(ex.Message);
        }
    }

    private static List<string> Extract2(
        string text,
        string serial)
    {
        var markers = Regex.Matches(text)
            .Cast<Match>()
            .ToArray();

        var sessions = new List<string>();

        for (var index = markers.Length - 1;
             index >= 0;
             index--)
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

    private static string[] Extract3(
        Regex regex,
        string text)
    {
        return
        [
            .. regex.Matches(text)
                .Select(match => NormExe(
                    match.Groups["hash"].Value))
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string[] Extract4(
        Regex regex,
        string text,
        HashKind kind)
    {
        return
        [
            .. regex.Matches(text)
                .Select(match =>
                    RunKey.TryNorm2(
                        kind,
                        match.Groups["hash"].Value,
                        out var normalized)
                        ? normalized
                        : null)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string[] Norm(
        IEnumerable<string> hashes)
    {
        return
        [
            .. hashes
                .Select(NormExe)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static HashRes BuildRes(
        IReadOnlyList<string> ppuHashes,
        IReadOnlyList<string> spuHashes,
        IReadOnlyList<string> prxHashes,
        IReadOnlyList<string> ovlHashes,
        HashEvi evidenceKind,
        string? sourcePath,
        DateTime? sourceTimestampUtc,
        string message,
        IReadOnlyList<string>? authoritativePpuHashes = null)
    {
        var normalizedPpuHashes = Norm(ppuHashes);
        var normalizedSpuHashes = Norm(spuHashes);

        var normalizedPrxHashes = Norm2(
            HashKind.Prx,
            prxHashes);

        var normalizedOvlHashes = Norm2(
            HashKind.Ovl,
            ovlHashes);

        if (normalizedPpuHashes.Length == 0 &&
            normalizedSpuHashes.Length == 0 &&
            normalizedPrxHashes.Length == 0 &&
            normalizedOvlHashes.Length == 0)
        {
            return HashRes.Unknown(message);
        }

        return new HashRes(
            PpuHash: normalizedPpuHashes.Length == 0
                ? null
                : normalizedPpuHashes[0],
            SpuHash: normalizedSpuHashes.Length == 0
                ? null
                : normalizedSpuHashes[0],
            EviKind: evidenceKind,
            SrcPath: sourcePath,
            SrcTime: sourceTimestampUtc,
            HasHash: true,
            Message: message,
            PpuRaw: normalizedPpuHashes,
            SpuRaw: normalizedSpuHashes,
            PrxRaw: normalizedPrxHashes,
            OvlRaw: normalizedOvlHashes,
            AuthPpu: Norm(
                authoritativePpuHashes ?? []));
    }

    private static string[] Norm2(
        HashKind kind,
        IEnumerable<string> hashes)
    {
        return
        [
            .. hashes
                .Select(hash =>
                    RunKey.TryNorm2(
                        kind,
                        hash,
                        out var normalized)
                        ? normalized
                        : null)
                .Where(hash => hash is not null)
                .Select(hash => hash!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string? NormExe(
        string? hash)
    {
        return ExeHash.TryNorm(
            hash,
            out var normalized)
            ? normalized
            : null;
    }

    private IReadOnlyList<string> ReadMeta(
        MetaCand candidate)
    {
        var info = new FileInfo(candidate.Path);

        if (_Cache.TryGetValue(
                candidate.Path,
                out var cached) &&
            cached.Length == info.Length &&
            cached.WriteUtc == info.LastWriteTimeUtc &&
            cached.Kind == candidate.Kind)
        {
            return cached.Segments;
        }

        var segments =
            candidate.Kind == HashEvi.ZipLog
                ? ReadGzip(
                    candidate.Path,
                    MaxBytes,
                    MaxByte2,
                    MaxByte3)
                : ReadHead(
                    candidate.Path,
                    MaxBytes);

        _Cache[candidate.Path] = new MetaCach(
            info.Length,
            info.LastWriteTimeUtc,
            candidate.Kind,
            segments);

        return segments;
    }

    private static IReadOnlyList<string> ReadHead(
        string path,
        int maxBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite |
            FileShare.Delete);

        if (stream.Length == 0)
        {
            return [];
        }

        if (stream.Length <= maxBytes)
        {
            return
            [
                Read(
                    stream,
                    0,
                    (int)stream.Length)
            ];
        }

        var segmentBytes =
            Math.Max(
                1,
                maxBytes / 2);

        var head = Read(
            stream,
            0,
            segmentBytes);

        var tail = Read(
            stream,
            stream.Length - segmentBytes,
            segmentBytes);

        return [tail, head];
    }

    private static string Read(
        Stream stream,
        long offset,
        int bytesToRead)
    {
        stream.Seek(
            offset,
            SeekOrigin.Begin);

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

        return Encoding.UTF8.GetString(
            buffer,
            0,
            totalRead);
    }

    private static IReadOnlyList<string> ReadGzip(
        string path,
        int maxBytes,
        long maxCompressedBytes,
        long maxExpandedBytes)
    {
        using var fileStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite |
            FileShare.Delete);

        if (fileStream.Length > maxCompressedBytes)
        {
            throw new InvalidDataException(
                $"Compressed RPCS3 log exceeds the {maxCompressedBytes / (1024 * 1024)} MiB safety limit.");
        }

        using var gzipStream = new GZipStream(
            fileStream,
            CompressionMode.Decompress,
            leaveOpen: false);

        var headCapacity =
            Math.Max(
                1,
                maxBytes / 2);

        var head = new byte[headCapacity];
        var headCount = 0;
        var ring = new byte[maxBytes];
        var buffer = new byte[Bytes];
        var writeOffset = 0;
        var retainedCount = 0;
        long expandedBytes = 0;

        while (true)
        {
            var read = gzipStream.Read(
                buffer,
                0,
                buffer.Length);

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
                var headRead = Math.Min(
                    read,
                    head.Length - headCount);

                Buffer.BlockCopy(
                    buffer,
                    0,
                    head,
                    headCount,
                    headRead);

                headCount += headRead;
            }

            AppendTo(
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

        var ordered =
            new byte[retainedCount];

        if (retainedCount < ring.Length)
        {
            Buffer.BlockCopy(
                ring,
                0,
                ordered,
                0,
                retainedCount);
        }
        else
        {
            var firstLength =
                ring.Length - writeOffset;

            Buffer.BlockCopy(
                ring,
                writeOffset,
                ordered,
                0,
                firstLength);

            Buffer.BlockCopy(
                ring,
                0,
                ordered,
                firstLength,
                writeOffset);
        }

        if (expandedBytes <= maxBytes)
        {
            return
            [
                Encoding.UTF8.GetString(ordered)
            ];
        }

        var tailCount = Math.Min(
            headCapacity,
            ordered.Length);

        var tail = Encoding.UTF8.GetString(
            ordered,
            ordered.Length - tailCount,
            tailCount);

        var headText = Encoding.UTF8.GetString(
            head,
            0,
            headCount);

        return [tail, headText];
    }

    private static void AppendTo(
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

        var firstLength = Math.Min(
            sourceCount,
            ring.Length - writeOffset);

        Buffer.BlockCopy(
            source,
            0,
            ring,
            writeOffset,
            firstLength);

        var remaining =
            sourceCount - firstLength;

        if (remaining > 0)
        {
            Buffer.BlockCopy(
                source,
                firstLength,
                ring,
                0,
                remaining);
        }

        writeOffset =
            (writeOffset + sourceCount) %
            ring.Length;

        retainedCount = Math.Min(
            ring.Length,
            retainedCount + sourceCount);
    }

    private static int EviRank(
        HashEvi kind)
    {
        return kind switch
        {
            HashEvi.PlainLog => 0,
            HashEvi.ZipLog => 1,
            HashEvi.Cache => 2,
            HashEvi.Meta => 3,
            _ => 4
        };
    }

    [GeneratedRegex(
        @"\bPPU executable hash:\s*PPU-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex PpuHashRegex();

    [GeneratedRegex(
        @"\b(?:SPU executable hash:|Loaded SPU image:)\s*SPU-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex SpuHashRegex();

    [GeneratedRegex(
        @"\bPRX hash of [^\r\n]*?:\s*PRX-(?<hash>[0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz]{28}-[0-9]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PrxHashRegex();

    [GeneratedRegex(
        @"\bOVL hash of [^\r\n]*?:\s*OVL-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex OvlHashRegex();

    [GeneratedRegex(
        @"^ppu-(?<hash>[0-9A-Za-z]{28}|[0-9a-f]{40})(?:-(?<module>[^\\/:*?\x22<>|]+))?$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex PpuCacheRegex();

    [GeneratedRegex(
        @"^spu-(?<hash>[0-9a-f]{40})(?:-|$)",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex SpuCacheRegex();

    private sealed record MetaCand(
        string Path,
        HashEvi Kind,
        DateTime WriteUtc);

    private sealed record MetaCach(
        long Length,
        DateTime WriteUtc,
        HashEvi Kind,
        IReadOnlyList<string> Segments);

    private sealed record CachePrb(
        string? ModName,
        HashRes Result);
}

#pragma warning restore IDE0130