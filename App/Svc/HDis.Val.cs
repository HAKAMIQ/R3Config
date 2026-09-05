using System.IO;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class HCanDiscoveryStore
{
    private static StoreState NormalizeState(StoreState? state)
    {
        if (state is not { Schema: SchemaVersion })
        {
            return StoreState.Empty;
        }

        var active = NormalizeSession(state.Active);
        var unique = new Dictionary<string, HCanHashBinding>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in state.Bindings ?? [])
        {
            var binding = NormalizeBinding(candidate);
            if (binding is null)
            {
                continue;
            }

            var key = BindingKey(binding);
            if (!unique.TryGetValue(key, out var existing) ||
                binding.DetectedUtc > existing.DetectedUtc)
            {
                unique[key] = binding;
            }
        }

        return new StoreState(
            SchemaVersion,
            active,
            unique.Values
                .OrderByDescending(item => item.DetectedUtc)
                .Take(MaxBindings)
                .ToArray());
    }

    private static HCanDiscoverySession? NormalizeSession(
        HCanDiscoverySession? session)
    {
        if (session is null ||
            !TryFullPath(session.Rpcs3ExecutablePath, out var executablePath) ||
            !TrySerial(session.Serial, out var serial) ||
            !TryVersion(session.GameVersion, out var gameVersion) ||
            !TryFullPath(session.GameInstallPath, out var gameInstallPath) ||
            session.ProcessId < 0 ||
            !TryUtc(session.StartedUtc, out var startedUtc) ||
            !TryFullPath(session.LogPath, out var logPath) ||
            !TryNormalizeMark(session.Baseline, out var baseline) ||
            !TryNormalizeLogSources(
                session.LogSources,
                logPath,
                baseline,
                out var logSources))
        {
            return null;
        }

        var primaryLog = logSources[0];
        return new HCanDiscoverySession(
            executablePath,
            serial,
            gameVersion,
            gameInstallPath,
            session.ProcessId,
            startedUtc,
            primaryLog.LogPath,
            primaryLog.Baseline,
            logSources);
    }

    private static HCanHashBinding? NormalizeBinding(
        HCanHashBinding? binding)
    {
        if (binding is null ||
            !TryFullPath(binding.Rpcs3ExecutablePath, out var executablePath) ||
            !TrySerial(binding.Serial, out var serial) ||
            !TryVersion(binding.GameVersion, out var gameVersion) ||
            !TryFullPath(binding.GameInstallPath, out var gameInstallPath) ||
            !TryHashes(binding.DetectedPpuHashes, out var ppuHashes) ||
            !TryHashes(binding.DetectedSpuHashes, out var spuHashes) ||
            ppuHashes.Length + spuHashes.Length == 0 ||
            !TryUtc(binding.DetectedUtc, out var detectedUtc) ||
            !TryUtc(binding.DiscoverySessionStartUtc, out var sessionStartUtc) ||
            !TryUtc(binding.LogLastWriteTimeUtc, out var logWriteUtc) ||
            !TryFingerprint(binding.LogFingerprint, out var fingerprint) ||
            !TryFullPath(binding.LogPath, out var logPath))
        {
            return null;
        }

        return new HCanHashBinding(
            executablePath,
            serial,
            gameVersion,
            gameInstallPath,
            ppuHashes,
            spuHashes,
            detectedUtc,
            sessionStartUtc,
            logWriteUtc,
            fingerprint,
            logPath);
    }

    private static bool TryNormalizeKey(
        string rpcs3ExecutablePath,
        string serial,
        string? gameVersion,
        string gameInstallPath,
        out BindingLookupKey key)
    {
        key = default;

        if (!TryFullPath(rpcs3ExecutablePath, out var executablePath) ||
            !TrySerial(serial, out var normalizedSerial) ||
            !TryVersion(gameVersion, out var normalizedVersion) ||
            !TryFullPath(gameInstallPath, out var installPath))
        {
            return false;
        }

        key = new BindingLookupKey(
            executablePath,
            normalizedSerial,
            normalizedVersion,
            installPath);
        return true;
    }

    private static bool TryFullPath(
        string? path,
        out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > MaxPathChars)
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            return fullPath.Length <= MaxPathChars;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }

    private static bool TrySerial(
        string? serial,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(serial))
        {
            return false;
        }

        var candidate = serial.Trim();
        if (candidate.Length != 9 ||
            candidate.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return false;
        }

        normalized = candidate.ToUpperInvariant();
        return true;
    }

    private static bool TryVersion(
        string? version,
        out string? normalized)
    {
        normalized = null;

        if (version is null)
        {
            return true;
        }

        if (version.Length > MaxVersionChars)
        {
            return false;
        }

        normalized = PatVer.Norm(version);
        return normalized is null ||
               normalized.Length <= MaxVersionChars;
    }

    private static bool TryHashes(
        IReadOnlyList<string>? values,
        out string[] normalized)
    {
        normalized = [];

        if (values is null ||
            values.Count > MaxHashesPerKind)
        {
            return false;
        }

        var result = new List<string>(values.Count);
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (!ExeHash.TryNorm(value, out var hash))
            {
                return false;
            }

            if (seen.Add(hash))
            {
                result.Add(hash);
            }
        }

        normalized = result.ToArray();
        return true;
    }

    private static bool TryFingerprint(
        string? fingerprint,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return false;
        }

        var candidate = fingerprint.Trim();
        if (candidate.Length != 64 ||
            candidate.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        normalized = candidate.ToLowerInvariant();
        return true;
    }

    private static bool TryNormalizeLogSources(
        IReadOnlyList<HCanLogSource>? sources,
        string fallbackPath,
        HCanLogMark fallbackBaseline,
        out IReadOnlyList<HCanLogSource> normalized)
    {
        normalized = [];

        if (sources is null ||
            sources.Count == 0)
        {
            normalized =
            [
                new HCanLogSource(
                    fallbackPath,
                    fallbackBaseline)
            ];
            return true;
        }

        if (sources.Count > 4)
        {
            return false;
        }

        var result = new List<HCanLogSource>(
            sources.Count);
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (source is null ||
                !TryFullPath(
                    source.LogPath,
                    out var sourcePath) ||
                !TryNormalizeMark(
                    source.Baseline,
                    out var sourceBaseline))
            {
                return false;
            }

            if (seen.Add(sourcePath))
            {
                result.Add(new HCanLogSource(
                    sourcePath,
                    sourceBaseline));
            }
        }

        if (result.Count == 0)
        {
            return false;
        }

        normalized = result;
        return true;
    }

    private static bool TryNormalizeMark(
        HCanLogMark? mark,
        out HCanLogMark normalized)
    {
        normalized = new HCanLogMark(
            false,
            0,
            null,
            string.Empty);

        if (mark is null ||
            mark.Length < 0)
        {
            return false;
        }

        if (!mark.Exists)
        {
            return true;
        }

        if (mark.LastWriteTimeUtc is not { } writeTime ||
            !TryUtc(writeTime, out var writeUtc))
        {
            return false;
        }

        if (string.IsNullOrEmpty(mark.Fingerprint))
        {
            normalized = new HCanLogMark(
                true,
                mark.Length,
                writeUtc,
                string.Empty);
            return true;
        }

        if (!TryFingerprint(mark.Fingerprint, out var fingerprint))
        {
            return false;
        }

        normalized = new HCanLogMark(
            true,
            mark.Length,
            writeUtc,
            fingerprint);
        return true;
    }

    private static bool TryUtc(
        DateTime value,
        out DateTime utc)
    {
        utc = default;

        if (value == default ||
            value.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        utc = value;
        return true;
    }

    private static bool SameKey(
        HCanHashBinding binding,
        HCanHashBinding candidate)
    {
        return SameKey(
            binding,
            new BindingLookupKey(
                candidate.Rpcs3ExecutablePath,
                candidate.Serial,
                candidate.GameVersion,
                candidate.GameInstallPath));
    }

    private static bool SameKey(
        HCanHashBinding binding,
        BindingLookupKey key)
    {
        return binding.Rpcs3ExecutablePath.Equals(
                   key.Rpcs3ExecutablePath,
                   StringComparison.OrdinalIgnoreCase) &&
               binding.Serial.Equals(
                   key.Serial,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   binding.GameVersion,
                   key.GameVersion,
                   StringComparison.OrdinalIgnoreCase) &&
               binding.GameInstallPath.Equals(
                   key.GameInstallPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string BindingKey(HCanHashBinding binding)
    {
        return string.Join(
            '\0',
            binding.Rpcs3ExecutablePath,
            binding.Serial,
            binding.GameVersion ?? string.Empty,
            binding.GameInstallPath);
    }

    private static string BuildMutexName(string path)
    {
        var canonical = path.ToUpperInvariant();
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical));

        return MutexPrefix + Convert.ToHexString(digest);
    }

    private readonly record struct BindingLookupKey(
        string Rpcs3ExecutablePath,
        string Serial,
        string? GameVersion,
        string GameInstallPath);

    private sealed record StoreState(
        int Schema,
        HCanDiscoverySession? Active,
        HCanHashBinding[] Bindings)
    {
        public static StoreState Empty { get; } = new(
            SchemaVersion,
            null,
            []);
    }
}
