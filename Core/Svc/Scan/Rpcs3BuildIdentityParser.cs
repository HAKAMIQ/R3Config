using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public static partial class Rpcs3BuildIdentityParser
{
    public static bool TryParse(
        IReadOnlyList<string> lines,
        out Rpcs3BuildBanner banner)
    {
        ArgumentNullException.ThrowIfNull(lines);

        banner = new Rpcs3BuildBanner(
            VersionText: string.Empty,
            BuildNumber: 0,
            Commit: string.Empty,
            Stage: string.Empty,
            Branch: string.Empty,
            Architecture: null);

        if (lines.Count == 0)
        {
            return false;
        }

        var firstLine = lines[0]
            .TrimStart('\uFEFF')
            .TrimEnd();

        var match = BannerRegex().Match(firstLine);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(
                match.Groups["build"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var buildNumber) ||
            buildNumber <= 0)
        {
            return false;
        }

        var architecture = ParseArchitecture(lines);

        banner = new Rpcs3BuildBanner(
            VersionText: match.Groups["version"].Value,
            BuildNumber: buildNumber,
            Commit: match.Groups["commit"].Value.ToLowerInvariant(),
            Stage: match.Groups["stage"].Value,
            Branch: match.Groups["branch"].Value,
            Architecture: architecture);

        return true;
    }

    private static string? ParseArchitecture(
        IReadOnlyList<string> lines)
    {
        var limit = Math.Min(lines.Count, 4);

        for (var index = 1; index < limit; index++)
        {
            var match = ArchitectureRegex().Match(
                lines[index].Trim());

            if (match.Success)
            {
                return match.Groups["architecture"].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(
        @"^RPCS3 v(?<version>[0-9]+\.[0-9]+\.[0-9]+)-(?<build>[0-9]+)-(?<commit>[0-9a-fA-F]{8,40}) (?<stage>[A-Za-z0-9._-]+) \| (?<branch>\S+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex BannerRegex();

    [GeneratedRegex(
        @"^Architecture:\s*(?<architecture>[A-Za-z0-9._-]+)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ArchitectureRegex();
}
