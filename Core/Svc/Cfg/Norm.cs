using System.Globalization;
using System.Text.RegularExpressions;

namespace R3Config.Svc;

public static class CfgValueNorm
{
    public static bool Same(string? current, string proposed)
    {
        return string.Equals(Norm(current), Norm(proposed), StringComparison.Ordinal);
    }

    public static string Norm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = Regex.Replace(value.Trim(), @"\s+", " ");
        var lower = text.ToLowerInvariant();

        return lower switch
        {
            "on" or "true" or "yes" or "enabled" => "true",
            "off" or "false" or "no" or "disabled" => "false",
            _ => NormPercent(lower)
        };
    }

    private static string NormPercent(string value)
    {
        if (value.EndsWith('%') &&
            decimal.TryParse(value[..^1], NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
        {
            return percent.ToString("0.#############################", CultureInfo.InvariantCulture);
        }

        return value;
    }
}
