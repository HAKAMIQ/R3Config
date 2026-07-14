using System;
using System.Globalization;
using System.Text.RegularExpressions;

#pragma warning disable IDE0130

namespace R3Config.Mdl;

/// <summary>
/// The final compatibility outcome consumed by every patch source.  Risk and
/// compatibility intentionally remain separate: a cheat can be compatible,
/// but it still requires an explicit user choice.
/// </summary>
public enum PatchCompatibilityVerdict
{
    Compatible,
    RuntimeGuarded,
    Blocked
}

public enum PatchCompatibilityReasonCode
{
    ExactRuntimeIdentity,
    Rpcs3RuntimeHashGuard,
    MissingTargetMetadata,
    SerialMismatch,
    VersionUnknown,
    VersionMismatch,
    ExecutableHashMismatch,
    InvalidRuntimeKey,
    RuntimeKeySerialMismatch,
    UntrustedRuntimeGuard,
    GlobalTargetNeedsExactHash
}

public sealed record PatchCompatibilityDecision(
    PatchCompatibilityVerdict Verdict,
    PatchCompatibilityReasonCode ReasonCode,
    bool CanEnable,
    bool CanDisable,
    bool UsesRpcs3RuntimeHashGuard,
    string RuntimeKey);

public sealed record Rpcs3PatchRuntimeKeyInfo(
    string RuntimeKey,
    PatchHashKind Kind,
    string Hash,
    string NormalizedHash,
    string? SerialScope);

/// <summary>
/// Mirrors the names passed by RPCS3 to patch_engine::apply:
/// PPU/SPU/OVL use a 40-hex SHA-1, while PRX uses RPCS3 Base57 plus a segment
/// ordinal.  RPCS3 also accepts a TITLE_ID-prefixed alternative key.
/// </summary>
public static partial class Rpcs3PatchRuntimeKey
{
    public static bool TryParse(
        string? value,
        out Rpcs3PatchRuntimeKeyInfo info)
    {
        info = new Rpcs3PatchRuntimeKeyInfo(
            string.Empty,
            PatchHashKind.Unknown,
            string.Empty,
            string.Empty,
            null);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var match = RuntimeKeyPattern().Match(candidate);
        if (!match.Success)
        {
            return false;
        }

        var kind = ParseKind(match.Groups["kind"].Value);
        var hash = match.Groups["hash"].Value.Trim();
        var serialScope = match.Groups["serial"].Success
            ? match.Groups["serial"].Value.ToUpperInvariant()
            : null;

        if (!TryNormalizeHash(kind, hash, out var normalizedHash))
        {
            return false;
        }

        info = new Rpcs3PatchRuntimeKeyInfo(
            candidate,
            kind,
            hash,
            normalizedHash,
            serialScope);

        return true;
    }

    public static bool TryNormalizeHash(
        PatchHashKind kind,
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();

        if (kind is PatchHashKind.Ppu or PatchHashKind.Spu or PatchHashKind.Ovl)
        {
            return Rpcs3ExecutableHash.TryNormalizeHex(
                candidate,
                out normalized);
        }

        if (kind != PatchHashKind.Prx)
        {
            return false;
        }

        var prx = PrxHashPattern().Match(candidate);
        if (!prx.Success ||
            !Rpcs3ExecutableHash.TryNormalize(
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

    public static bool TryNormalizeEvidence(
        PatchHashKind kind,
        string? value,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (kind is PatchHashKind.Ppu or PatchHashKind.Spu or PatchHashKind.Ovl)
        {
            return Rpcs3ExecutableHash.TryNormalize(
                candidate,
                out normalized);
        }

        if (kind != PatchHashKind.Prx)
        {
            return false;
        }

        var separator = candidate.LastIndexOf('-');
        if (separator <= 0 || separator == candidate.Length - 1 ||
            !Rpcs3ExecutableHash.TryNormalize(
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
        PatchHashKind kind,
        string hash,
        string? serialScope = null)
    {
        var prefix = KindPrefix(kind);
        if (prefix is null)
        {
            return string.Empty;
        }

        var runtimeKey = $"{prefix}-{hash.Trim()}";
        return string.IsNullOrWhiteSpace(serialScope)
            ? runtimeKey
            : $"{serialScope.Trim().ToUpperInvariant()}-{runtimeKey}";
    }

    private static PatchHashKind ParseKind(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "PPU" => PatchHashKind.Ppu,
            "SPU" => PatchHashKind.Spu,
            "PRX" => PatchHashKind.Prx,
            "OVL" => PatchHashKind.Ovl,
            _ => PatchHashKind.Unknown
        };
    }

    private static string? KindPrefix(PatchHashKind kind)
    {
        return kind switch
        {
            PatchHashKind.Ppu => "PPU",
            PatchHashKind.Spu => "SPU",
            PatchHashKind.Prx => "PRX",
            PatchHashKind.Ovl => "OVL",
            _ => null
        };
    }

    [GeneratedRegex(
        @"^(?:(?<serial>[A-Z0-9]{9})-)?(?<kind>PPU|SPU|PRX|OVL)-(?<hash>.+)$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex RuntimeKeyPattern();

    [GeneratedRegex(
        @"^(?<digest>[0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz]{28})-(?<segment>[0-9]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PrxHashPattern();
}

#pragma warning restore IDE0130
