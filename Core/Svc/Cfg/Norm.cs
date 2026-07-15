using System.Globalization;
using System.Text.RegularExpressions;

namespace R3Config.Svc;

public static partial class CfgNorm
{
    public static bool Same(string? current, string proposed)
    {
        return string.Equals(
            Norm(current),
            Norm(proposed),
            StringComparison.Ordinal);
    }

    public static string Norm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = Space()
            .Replace(value.Trim(), " ");

        var lower = text.ToLowerInvariant();

        return lower switch
        {
            "on" or "true" or "yes" or "enabled" => "true",
            "off" or "false" or "no" or "disabled" => "false",
            _ => NormPct(lower)
        };
    }

    private static string NormPct(string value)
    {
        if (value.EndsWith('%') &&
            decimal.TryParse(
                value[..^1],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var percent))
        {
            return percent.ToString(
                "0.#############################",
                CultureInfo.InvariantCulture);
        }

        return value;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Space();
}
