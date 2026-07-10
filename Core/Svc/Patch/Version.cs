using System;
using System.Text.RegularExpressions;

namespace R3Config.Svc;

public static class Rpcs3PatchVersion
{
    public const string All = "All";

    private static readonly Regex NumericVersionPattern = new(
        @"^(?:v\.?)?(?<major>\d{1,2})\.(?<minor>\d{1,2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals(All, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        var match = NumericVersionPattern.Match(trimmed);
        if (!match.Success ||
            !int.TryParse(match.Groups["major"].Value, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, out var minor))
        {
            return trimmed;
        }

        return $"{major:00}.{minor:00}";
    }

    public static bool Equals(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft is not null && normalizedRight is not null &&
               normalizedLeft.Equals(normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAll(string? value)
    {
        return Normalize(value)?.Equals(All, StringComparison.OrdinalIgnoreCase) == true;
    }
}
