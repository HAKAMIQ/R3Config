using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace R3Config.Svc;

public static partial class PatVer
{
    public const string All = "All";

    public static string? Norm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Equals(
                All,
                StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(
                "Any",
                StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        var match = Numeric().Match(trimmed);

        if (!match.Success ||
            !int.TryParse(
                match.Groups["major"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var major) ||
            !int.TryParse(
                match.Groups["minor"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minor))
        {
            return trimmed;
        }

        return string.Concat(
            major.ToString("00", CultureInfo.InvariantCulture),
            ".",
            minor.ToString("00", CultureInfo.InvariantCulture));
    }

    public static bool Equals(string? left, string? right)
    {
        var normalizedLeft = Norm(left);
        var normalizedRight = Norm(right);

        return normalizedLeft is not null &&
               normalizedRight is not null &&
               normalizedLeft.Equals(
                   normalizedRight,
                   StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAll(string? value)
    {
        return Norm(value)?.Equals(
            All,
            StringComparison.OrdinalIgnoreCase) == true;
    }

    [GeneratedRegex(
        @"^(?:v\.?)?(?<major>[0-9]{1,2})\.(?<minor>[0-9]{1,2})$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex Numeric();
}