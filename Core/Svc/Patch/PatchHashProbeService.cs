using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed partial class PatchHashProbeService
{
    private const long MaxCompressedBytes = 64L * 1024 * 1024;
    private const long MaxExpandedChars = 256L * 1024 * 1024;

    private static readonly Regex Regex = new(
        @"\bSYS:\s*Serial:\s*(?<serial>[A-Z0-9]{9})\b",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase |
        RegexOptions.NonBacktracking);

    public Rpcs3GameDescriptor Enrich(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        var result = Probe(
            install,
            normalizedGame,
            cancellationToken);
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
        IEnumerable<Rpcs3GameDescriptor> games,
        CancellationToken cancellationToken = default)
    {
        var enriched = new List<Rpcs3GameDescriptor>();
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            enriched.Add(Enrich(
                install,
                game,
                cancellationToken));
        }

        return enriched;
    }

    public string? FindNewestLog(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        return ListCand(install)
            .OrderByDescending(candidate =>
                candidate.WriteUtc)
            .ThenBy(candidate =>
                EviRank(candidate.Kind))
            .Select(candidate =>
                candidate.Path)
            .FirstOrDefault();
    }

    public HashRes Probe(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            var result = TryProbe(
                candidate,
                game.Serial,
                cancellationToken);

            if (result.HasHash)
            {
                evidence.Add(result);
            }
        }

        var cacheResults = ListCan2(
                install,
                game.Serial,
                cancellationToken)
            .Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var moduleName = Extract(path);

                return new CachePrb(
                    moduleName,
                    TryProb2(
                        path,
                        FindUtc(
                            install,
                            game,
                            moduleName,
                            cancellationToken)));
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

            var currentMainPpuHashes = cacheResults
                .Where(item =>
                    item.ModName?.Equals(
                        "EBOOT.BIN",
                        StringComparison.OrdinalIgnoreCase) == true &&
                    item.Result.PpuList.Count > 0)
                .OrderByDescending(item =>
                    item.Result.SrcTime ?? DateTime.MinValue)
                .Take(1)
                .Select(item => item.Result)
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
                Norm(currentMainPpuHashes)));
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
                install.DataRoot,
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
                Path.Combine(root, "log", "rpcs3.log.gz"),
                Path.Combine(root, "logs", "RPCS3.log"),
                Path.Combine(root, "logs", "rpcs3.log"),
                Path.Combine(root, "logs", "RPCS3.log.gz"),
                Path.Combine(root, "logs", "rpcs3.log.gz")
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
        string serial,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
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
        string? moduleName,
        CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
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

    private static HashRes TryProbe(
        MetaCand candidate,
        string serial,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var file = new FileStream(
                candidate.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite |
                FileShare.Delete);

            if (file.Length == 0)
            {
                return HashRes.Unknown(
                    "RPCS3 log is empty.");
            }

            if (candidate.Kind == HashEvi.ZipLog &&
                file.Length > MaxCompressedBytes)
            {
                return HashRes.Unknown(
                    $"Compressed RPCS3 log exceeds the {MaxCompressedBytes / (1024 * 1024)} MiB safety limit.");
            }

            using Stream input =
                candidate.Kind == HashEvi.ZipLog
                    ? new GZipStream(
                        file,
                        CompressionMode.Decompress,
                        leaveOpen: false)
                    : file;

            using var reader = new StreamReader(
                input,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 64 * 1024,
                leaveOpen: false);

            var scan = ScanLog(
                reader,
                serial,
                cancellationToken);

            if (!scan.HasHashes)
            {
                return HashRes.Unknown(
                    scan.SawSerial
                        ? "The matching RPCS3 game session did not expose a PPU, SPU, PRX, or OVL executable hash."
                        : "RPCS3 log did not contain a SYS: Serial session for the selected game.");
            }

            return BuildRes(
                scan.Ppu,
                scan.Spu,
                scan.Prx,
                scan.Ovl,
                candidate.Kind,
                candidate.Path,
                candidate.WriteUtc,
                $"Found {scan.Ppu.Count} PPU, {scan.Spu.Count} SPU, {scan.Prx.Count} PRX, and {scan.Ovl.Count} OVL runtime hash(es) " +
                "inside the newest matching SYS: Serial session.",
                scan.Ppu.Take(1).ToArray());
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return HashRes.Unknown(
                ex.Message);
        }
    }

    private static LogScan ScanLog(
        TextReader reader,
        string serial,
        CancellationToken cancellationToken)
    {
        var currentMatches = false;
        var sawSerial = false;
        long expandedChars = 0;

        var currentPpu = new List<string>();
        var currentSpu = new List<string>();
        var currentPrx = new List<string>();
        var currentOvl = new List<string>();

        var selectedPpu = Array.Empty<string>();
        var selectedSpu = Array.Empty<string>();
        var selectedPrx = Array.Empty<string>();
        var selectedOvl = Array.Empty<string>();

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            expandedChars += line.Length + 2L;
            if (expandedChars > MaxExpandedChars)
            {
                throw new InvalidDataException(
                    $"Expanded RPCS3 log exceeds the {MaxExpandedChars / (1024 * 1024)} MiB safety limit.");
            }

            var serialMatch = Regex.Match(line);
            if (serialMatch.Success)
            {
                SaveSession();

                currentMatches =
                    serialMatch.Groups["serial"].Value.Equals(
                        serial,
                        StringComparison.OrdinalIgnoreCase);

                sawSerial |= currentMatches;

                currentPpu.Clear();
                currentSpu.Clear();
                currentPrx.Clear();
                currentOvl.Clear();
            }

            if (!currentMatches)
            {
                continue;
            }

            AddMatches(
                PpuHashRegex(),
                line,
                currentPpu,
                HashKind.Ppu);

            AddMatches(
                SpuHashRegex(),
                line,
                currentSpu,
                HashKind.Spu);

            AddMatches(
                PrxHashRegex(),
                line,
                currentPrx,
                HashKind.Prx);

            AddMatches(
                OvlHashRegex(),
                line,
                currentOvl,
                HashKind.Ovl);
        }

        SaveSession();

        return new LogScan(
            sawSerial,
            selectedPpu,
            selectedSpu,
            selectedPrx,
            selectedOvl);

        void SaveSession()
        {
            if (!currentMatches ||
                currentPpu.Count == 0 &&
                currentSpu.Count == 0 &&
                currentPrx.Count == 0 &&
                currentOvl.Count == 0)
            {
                return;
            }

            selectedPpu =
            [
                .. currentPpu
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
            ];

            selectedSpu =
            [
                .. currentSpu
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
            ];

            selectedPrx =
            [
                .. currentPrx
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
            ];

            selectedOvl =
            [
                .. currentOvl
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
            ];
        }
    }

    private static void AddMatches(
        Regex regex,
        string line,
        List<string> hashes,
        HashKind kind)
    {
        foreach (Match match in regex.Matches(line))
        {
            var value =
                kind is HashKind.Ppu or
                    HashKind.Spu or
                    HashKind.Ovl
                    ? NormExe(
                        match.Groups["hash"].Value)
                    : RunKey.TryNorm2(
                        kind,
                        match.Groups["hash"].Value,
                        out var normalized)
                        ? normalized
                        : null;

            if (value is not null)
            {
                hashes.Add(value);
            }
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

    private sealed record LogScan(
        bool SawSerial,
        IReadOnlyList<string> Ppu,
        IReadOnlyList<string> Spu,
        IReadOnlyList<string> Prx,
        IReadOnlyList<string> Ovl)
    {
        public bool HasHashes =>
            Ppu.Count > 0 ||
            Spu.Count > 0 ||
            Prx.Count > 0 ||
            Ovl.Count > 0;
    }

    private sealed record CachePrb(
        string? ModName,
        HashRes Result);
}

#pragma warning restore CA1822

#pragma warning restore IDE0130
