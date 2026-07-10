using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class PatchHashProbeService
{
    private const int MaxProbeBytes = 16 * 1024 * 1024;
    private const int SerialBackwardWindow = 32 * 1024;
    private const int SerialForwardWindow = 2 * 1024 * 1024;
    private readonly Dictionary<string, CachedMetadata> _metadataCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex PpuHashRegex = new(
        @"\bPPU-(?<hash>[0-9a-zA-Z]{16,64})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SpuHashRegex = new(
        @"\bSPU-(?<hash>[0-9a-zA-Z]{16,64})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CachePpuHashRegex = new(
        @"^ppu-(?<hash>[0-9a-zA-Z]{16,64})(?:-|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CacheSpuHashRegex = new(
        @"^spu-(?<hash>[0-9a-zA-Z]{16,64})(?:-|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public Rpcs3GameDescriptor Enrich(Rpcs3InstallDescriptor install, Rpcs3GameDescriptor game)
    {
        var existingPpuHashes = NormalizeHashes(game.AllPpuHashes);
        var existingSpuHashes = NormalizeHashes(game.AllSpuHashes);
        var normalizedGame = game with
        {
            PpuHash = existingPpuHashes.FirstOrDefault(),
            SpuHash = existingSpuHashes.FirstOrDefault(),
            PpuHashes = existingPpuHashes,
            SpuHashes = existingSpuHashes
        };

        var result = Probe(install, normalizedGame);
        var ppuHashes = NormalizeHashes(existingPpuHashes.Concat(result.AllPpuHashes));
        var spuHashes = NormalizeHashes(existingSpuHashes.Concat(result.AllSpuHashes));

        return game with
        {
            PpuHash = ppuHashes.FirstOrDefault(),
            SpuHash = spuHashes.FirstOrDefault(),
            PpuHashes = ppuHashes,
            SpuHashes = spuHashes,
            HashSource = result.HashAvailable ? result.Source : game.HashSource,
            HashMessage = result.Message
        };
    }

    public IReadOnlyList<Rpcs3GameDescriptor> Enrich(Rpcs3InstallDescriptor install, IEnumerable<Rpcs3GameDescriptor> games)
    {
        return games.Select(game => Enrich(install, game)).ToArray();
    }

    public PatchHashProbeResult Probe(Rpcs3InstallDescriptor install, Rpcs3GameDescriptor game)
    {
        if (string.IsNullOrWhiteSpace(game.Serial))
        {
            return PatchHashProbeResult.Unknown("Game serial is unavailable; patch hash remains unknown.");
        }

        var existingPpuHashes = NormalizeHashes(game.AllPpuHashes);
        var existingSpuHashes = NormalizeHashes(game.AllSpuHashes);
        if (existingPpuHashes.Count > 0 || existingSpuHashes.Count > 0)
        {
            return CreateResult(
                existingPpuHashes,
                existingSpuHashes,
                game.HashSource ?? "Game metadata",
                "Patch hashes came from existing game metadata.");
        }

        foreach (var candidate in EnumerateCandidateMetadataFiles(install))
        {
            var result = TryProbeFile(candidate, game.Serial);
            if (result.HashAvailable)
            {
                return result;
            }
        }

        var cachePpuHashes = new List<string>();
        var cacheSpuHashes = new List<string>();
        string? cacheSource = null;
        foreach (var candidate in EnumerateCandidateCacheEntries(install, game.Serial))
        {
            var result = TryProbeCacheEntry(candidate);
            if (!result.HashAvailable)
            {
                continue;
            }

            cachePpuHashes.AddRange(result.AllPpuHashes);
            cacheSpuHashes.AddRange(result.AllSpuHashes);
            cacheSource ??= result.Source;
        }

        if (cachePpuHashes.Count > 0 || cacheSpuHashes.Count > 0)
        {
            return CreateResult(
                NormalizeHashes(cachePpuHashes),
                NormalizeHashes(cacheSpuHashes),
                cacheSource ?? "RPCS3 cache",
                "Patch hashes were found in the bounded RPCS3 per-game cache probe.");
        }

        return PatchHashProbeResult.Unknown(
            "No RPCS3 log or bounded per-game cache probe exposed a trustworthy patch hash. Start the game once with a cold boot, close RPCS3, then analyze again.");
    }

    private static IEnumerable<string> EnumerateCandidateMetadataFiles(Rpcs3InstallDescriptor install)
    {
        var configBase = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var candidates = new[]
        {
            Path.Combine(install.RootPath, "RPCS3.log"),
            Path.Combine(install.RootPath, "rpcs3.log"),
            Path.Combine(install.RootPath, "log", "RPCS3.log"),
            Path.Combine(install.RootPath, "log", "rpcs3.log"),
            configBase is null ? null : Path.Combine(configBase, "RPCS3.log"),
            configBase is null ? null : Path.Combine(configBase, "rpcs3.log"),
            configBase is null ? null : Path.Combine(configBase, "log", "RPCS3.log"),
            configBase is null ? null : Path.Combine(configBase, "log", "rpcs3.log"),
            Path.Combine(install.ConfigPath, "RPCS3.log"),
            Path.Combine(install.ConfigPath, "rpcs3.log"),
            Path.Combine(portablePath, "RPCS3.log"),
            Path.Combine(portablePath, "rpcs3.log"),
            Path.Combine(portablePath, "log", "RPCS3.log"),
            Path.Combine(portablePath, "log", "rpcs3.log")
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists);
    }

    private static IEnumerable<string> EnumerateCandidateCacheEntries(Rpcs3InstallDescriptor install, string serial)
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
                entries = Directory.EnumerateFileSystemEntries(cacheDir).Take(2048).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }

    private PatchHashProbeResult TryProbeFile(string path, string serial)
    {
        try
        {
            var text = ReadTailCached(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return PatchHashProbeResult.Unknown("Metadata file is empty.");
            }

            foreach (var window in ExtractSerialWindowsNewestFirst(text, serial))
            {
                var ppuHashes = ExtractHashes(PpuHashRegex, window);
                var spuHashes = ExtractHashes(SpuHashRegex, window);
                if (ppuHashes.Count > 0 || spuHashes.Count > 0)
                {
                    return CreateResult(
                        ppuHashes,
                        spuHashes,
                        path,
                        $"Found {ppuHashes.Count} PPU and {spuHashes.Count} SPU hash(es) near the latest matching RPCS3 log session.");
                }
            }

            return PatchHashProbeResult.Unknown("No hash was found near a matching game session in the bounded log probe.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return PatchHashProbeResult.Unknown(ex.Message);
        }
    }

    private static PatchHashProbeResult TryProbeCacheEntry(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return PatchHashProbeResult.Unknown("Cache entry has no name.");
        }

        var ppuHashes = ExtractHashes(CachePpuHashRegex, name);
        var spuHashes = ExtractHashes(CacheSpuHashRegex, name);
        return ppuHashes.Count == 0 && spuHashes.Count == 0
            ? PatchHashProbeResult.Unknown("Cache entry does not expose a patch hash.")
            : CreateResult(ppuHashes, spuHashes, path, "Patch hash found in the RPCS3 per-game cache.");
    }

    private static IReadOnlyList<string> ExtractSerialWindowsNewestFirst(string text, string serial)
    {
        var indexes = new List<int>();
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(serial, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                break;
            }

            indexes.Add(found);
            index = found + serial.Length;
        }

        return indexes
            .OrderByDescending(found => found)
            .Select(found =>
            {
                var start = Math.Max(0, found - SerialBackwardWindow);
                var end = Math.Min(text.Length, found + serial.Length + SerialForwardWindow);
                return text[start..end];
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractHashes(Regex regex, string text)
    {
        return regex.Matches(text)
            .Select(match => NormalizeHash(match.Groups["hash"].Value))
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeHashes(IEnumerable<string> hashes)
    {
        return hashes
            .Select(NormalizeHash)
            .Where(hash => hash is not null)
            .Select(hash => hash!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PatchHashProbeResult CreateResult(
        IReadOnlyList<string> ppuHashes,
        IReadOnlyList<string> spuHashes,
        string source,
        string message)
    {
        return new PatchHashProbeResult(
            PpuHash: ppuHashes.FirstOrDefault(),
            SpuHash: spuHashes.FirstOrDefault(),
            Source: source,
            HashAvailable: ppuHashes.Count > 0 || spuHashes.Count > 0,
            Message: message,
            PpuHashes: ppuHashes,
            SpuHashes: spuHashes);
    }

    private static string? NormalizeHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        var normalized = hash.Trim().ToLowerInvariant();
        return normalized.Length >= 16 && normalized.Any(character => character != '0')
            ? normalized
            : null;
    }

    private static string ReadTail(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytesToRead = (int)Math.Min(stream.Length, maxBytes);
        stream.Seek(-bytesToRead, SeekOrigin.End);
        var buffer = new byte[bytesToRead];
        var totalRead = 0;
        while (totalRead < bytesToRead)
        {
            var read = stream.Read(buffer, totalRead, bytesToRead - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    private string ReadTailCached(string path)
    {
        var info = new FileInfo(path);
        if (_metadataCache.TryGetValue(path, out var cached) &&
            cached.Length == info.Length &&
            cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
        {
            return cached.Text;
        }

        var text = ReadTail(path, MaxProbeBytes);
        _metadataCache[path] = new CachedMetadata(info.Length, info.LastWriteTimeUtc, text);
        return text;
    }

    private sealed record CachedMetadata(long Length, DateTime LastWriteTimeUtc, string Text);
}
