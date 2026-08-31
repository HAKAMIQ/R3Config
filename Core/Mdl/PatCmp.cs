using System;
using System.Globalization;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Mdl;

public enum PatchCompatibilityVerdict
{
    Compat,
    Guarded,
    Blocked
}

public enum PatchCompatibilityReasonCode
{
    ExactId,
    R3Guard,
    Meta,
    SerBad,
    Unknown,
    VerBad,
    ExeBad,
    Key,
    KeySerBd,
    Guard,
    Hash
}

public sealed record PatchCompatibilityDecision(
    PatchCompatibilityVerdict Verdict,
    PatchCompatibilityReasonCode Code,
    bool CanOn,
    bool CanOff,
    bool UseGuard,
    string RunKey);

public sealed record KeyInf(
    string RunKey,
    HashKind Kind,
    string Hash,
    string NormHash,
    string? SerScope);

public static partial class RunKey
{
    public static bool TryParse(
        string? value,
        out KeyInf info)
    {
        info = new KeyInf(
            string.Empty,
            HashKind.Unknown,
            string.Empty,
            string.Empty,
            null);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var match = KeyRegex().Match(candidate);
        if (!match.Success)
        {
            return false;
        }

        var kind = Parse(match.Groups["kind"].Value);
        var hash = match.Groups["hash"].Value.Trim();
        var serialScope = match.Groups["serial"].Success
            ? match.Groups["serial"].Value.ToUpperInvariant()
            : null;

        if (!TryNorm(kind, hash, out var normalizedHash))
        {
            return false;
        }

        info = new KeyInf(
            candidate,
            kind,
            hash,
            normalizedHash,
            serialScope);

        return true;
    }

    public static bool TryNorm(
        HashKind kind,
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();

        if (kind is HashKind.Ppu or HashKind.Spu or HashKind.Ovl)
        {
            return ExeHash.TryNorm2(
                candidate,
                out normalized);
        }

        if (kind != HashKind.Prx)
        {
            return false;
        }

        var prx = PrxHash().Match(candidate);
        if (!prx.Success ||
            !ExeHash.TryNorm(
                prx.Groups["digest"].Value,
                out var digest) ||
            !uint.TryParse(
                prx.Groups["segment"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var segment))
        {
            return false;
        }

        normalized = string.Concat(
            digest,
            "-",
            segment.ToString(CultureInfo.InvariantCulture));

        return true;
    }

    public static bool TryNorm2(
        HashKind kind,
        string? value,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (kind is HashKind.Ppu or HashKind.Spu or HashKind.Ovl)
        {
            return ExeHash.TryNorm(
                candidate,
                out normalized);
        }

        if (kind != HashKind.Prx)
        {
            return false;
        }

        var separator = candidate.LastIndexOf('-');
        if (separator <= 0 || separator == candidate.Length - 1 ||
            !ExeHash.TryNorm(
                candidate[..separator],
                out var digest) ||
            !uint.TryParse(
                candidate[(separator + 1)..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var segment))
        {
            return false;
        }

        normalized = string.Concat(
            digest,
            "-",
            segment.ToString(CultureInfo.InvariantCulture));

        return true;
    }

    public static string Build(
        HashKind kind,
        string hash,
        string? serialScope = null)
    {
        var prefix = Kind(kind);
        if (prefix is null)
        {
            return string.Empty;
        }

        var runtimeKey = $"{prefix}-{hash.Trim()}";
        return string.IsNullOrWhiteSpace(serialScope)
            ? runtimeKey
            : $"{serialScope.Trim().ToUpperInvariant()}-{runtimeKey}";
    }

    private static HashKind Parse(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "PPU" => HashKind.Ppu,
            "SPU" => HashKind.Spu,
            "PRX" => HashKind.Prx,
            "OVL" => HashKind.Ovl,
            _ => HashKind.Unknown
        };
    }

    private static string? Kind(HashKind kind)
    {
        return kind switch
        {
            HashKind.Ppu => "PPU",
            HashKind.Spu => "SPU",
            HashKind.Prx => "PRX",
            HashKind.Ovl => "OVL",
            _ => null
        };
    }

    [GeneratedRegex(
        @"^(?:(?<serial>[A-Z0-9]{9})-)?(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex KeyRegex();

    [GeneratedRegex(
        @"^(?<digest>[0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz]{28})-(?<segment>[0-9]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PrxHash();
}

#pragma warning restore IDE0130
