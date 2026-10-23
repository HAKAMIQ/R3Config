using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Scope comparison between what a Wiki note/table states and the detected
// game context. Every method answers only from explicit text and identifiers;
// null means "cannot be confirmed", never a guess.
public static partial class NoteScope
{
    private static readonly Regex Newer = new(
        @"\b(?:past|above|after|newer|later|higher|beyond|over)\s*(?:version\s*|v)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AtLeast = new(
        @"\b(?:since|from)\s*(?:version\s*|v)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Older = new(
        @"\b(?:below|before|prior to|older|lower|under|until|earlier than)\s*(?:version\s*|v)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TrailingDown = new(
        @"^\s*(?:and|or)\s+(?:below|lower|older|earlier|before|less)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TrailingUp = new(
        @"^\s*(?:and|or)\s+(?:above|higher|newer|later|after|more|up)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GameVersionText = new(
        @"^\d{1,2}\.\d{1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int[]? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().TrimStart('v', 'V');
        var parts = text.Split('.');
        var numbers = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }

        return numbers;
    }

    public static int Compare(int[] left, int[] right)
    {
        var length = Math.Max(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            var a = index < left.Length ? left[index] : 0;
            var b = index < right.Length ? right[index] : 0;
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        return 0;
    }

    public static bool SameVersion(string? left, string? right)
    {
        var a = ParseVersion(left);
        var b = ParseVersion(right);
        return a is not null && b is not null && Compare(a, b) == 0;
    }

    // "past 1.00" means newer than 1.00, "below 1.05" older, "since 1.02" that
    // version or newer, and a bare "1.00 version" exactly that version.
    public static bool? VersionApplies(string text, string condition, string? installed)
    {
        var target = ParseVersion(condition);
        var actual = ParseVersion(installed);
        if (target is null || actual is null)
        {
            return null;
        }

        var index = text.IndexOf(condition, StringComparison.Ordinal);
        var prefix = index <= 0
            ? string.Empty
            : text[Math.Max(0, index - 24)..index];
        var order = Compare(actual, target);
        var suffix = index < 0 ? string.Empty : text[(index + condition.Length)..Math.Min(text.Length, index + condition.Length + 24)];

        if (Newer.IsMatch(prefix))
        {
            return order > 0;
        }

        if (AtLeast.IsMatch(prefix))
        {
            return order >= 0;
        }

        if (Older.IsMatch(prefix))
        {
            return order < 0;
        }

        // "1.05 and below", "1.02 or newer": the operator follows the number.
        if (TrailingDown.IsMatch(suffix))
        {
            return order <= 0;
        }

        if (TrailingUp.IsMatch(suffix))
        {
            return order >= 0;
        }

        return order == 0;
    }

    // A game (APP_VER) version is major.minor ("1.05", "01.11"). Anything else that a note
    // parser picked up (an IP address such as 95.217.209, an RPCS3 version) is not a game
    // version scope and must never restrict a note to a game version.
    public static bool IsGameVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && GameVersionText.IsMatch(value.Trim());

    // Direction words in front of an RPCS3 version/build mention. The mention may be
    // introduced by "RPCS3", "RPCS", "version", "build" or a "v" prefix.
    private const string Rpcs3Lead = @"\s*(?:rpcs3?\s+)?(?:versions?\s*|builds?\s*|v)?$";

    private static readonly Regex Rpcs3Newer = new(
        @"\b(?:past|above|after|newer|later|higher|beyond|over)" + Rpcs3Lead,
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Rpcs3AtLeast = new(
        @"\b(?:since|from)" + Rpcs3Lead,
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Rpcs3Older = new(
        @"\b(?:below|before|prior to|older|lower|under|until|earlier than)" + Rpcs3Lead,
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Rpcs3Text = new(
        @"^v?(?<version>\d+\.\d+\.\d+)(?:-(?<build>\d+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A build scope written in a note ("v0.0.26-14569", "0.0.21") against the
    // typed runtime RPCS3 identity. Versions and build numbers are compared as
    // numbers, never as text. "after/since/below/before" set the direction; a
    // mention with no direction applies only to that exact build. null means it
    // cannot be confirmed (unknown/partial/conflicting runtime identity, or an
    // ambiguous mention): never a guess.
    public static bool? Rpcs3BuildApplies(string text, string condition, Rpcs3RuntimeBuild? runtime)
    {
        if (runtime is null || !runtime.IsExact)
        {
            return null;
        }

        var match = Rpcs3Text.Match(condition.Trim());
        var actualVersion = ParseVersion(runtime.Version);
        if (!match.Success || actualVersion is null)
        {
            return null;
        }

        var targetVersion = ParseVersion(match.Groups["version"].Value)!;
        var order = Compare(actualVersion, targetVersion);
        if (order == 0)
        {
            if (!match.Groups["build"].Success)
            {
                order = 0;
            }
            else if (runtime.BuildNumber is { } actualBuild &&
                     int.TryParse(match.Groups["build"].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var targetBuild))
            {
                order = actualBuild.CompareTo(targetBuild);
            }
            else
            {
                return null;
            }
        }

        var index = text.IndexOf(condition, StringComparison.Ordinal);
        var prefix = index <= 0 ? string.Empty : text[Math.Max(0, index - 24)..index];
        if (Rpcs3Newer.IsMatch(prefix)) return order > 0;
        if (Rpcs3AtLeast.IsMatch(prefix)) return order >= 0;
        if (Rpcs3Older.IsMatch(prefix)) return order < 0;
        return order == 0 && match.Groups["build"].Success ? true : null;
    }

    // Title IDs are the only deterministic edition signal: NP* is a PSN
    // (digital) release, BL*/BC* a disc release.
    public static string? EditionOf(string serial)
    {
        var id = serial.Trim().ToUpperInvariant();
        return id.StartsWith("NP", StringComparison.Ordinal)
            ? "digital"
            : id.StartsWith("BL", StringComparison.Ordinal) || id.StartsWith("BC", StringComparison.Ordinal)
                ? "disc"
                : null;
    }

    public static bool? EditionApplies(string condition, string serial)
    {
        var actual = EditionOf(serial);
        if (actual is null)
        {
            return null;
        }

        var wanted = condition.Equals("digital", StringComparison.OrdinalIgnoreCase)
            ? "digital"
            : "disc";
        return wanted == actual;
    }

    // Only a graphics vendor (AMD/NVIDIA/Intel) restricts a note; GPU/CPU/
    // Vulkan/OpenGL on their own are generic and apply to every machine. The
    // vendor is compared with the display adapters' vendor evidence (PCI vendor
    // ID when the OS reports it). No adapter evidence, an adapter of unknown
    // vendor, or several vendors with the condition among them (RPCS3's own
    // adapter is not proven) all answer null: never a guess.
    public static bool? HardwareApplies(string condition, HwInfo? hardware) =>
        HardwareApplies(condition, hardware?.GpuAdapters);

    // Name-only context (a single GPU name): its vendor is name-derived.
    public static bool? HardwareApplies(string condition, string? gpuName) =>
        HardwareApplies(
            condition,
            string.IsNullOrWhiteSpace(gpuName)
                ? null
                : [new GpuAdapterInfo(gpuName.Trim(), HwProbe.VendorFromName(gpuName), null, null, IsPrimary: true, IsActive: true)]);

    private static bool? HardwareApplies(string condition, IReadOnlyList<GpuAdapterInfo>? adapters)
    {
        var wanted = condition.ToLowerInvariant() switch
        {
            "amd" => HwVendor.Amd,
            "nvidia" => HwVendor.Nvidia,
            "intel" => HwVendor.Intel,
            _ => HwVendor.Unknown
        };

        if (wanted == HwVendor.Unknown)
        {
            return true;
        }

        if (adapters is null || adapters.Count == 0 ||
            adapters.Any(adapter => adapter.Vendor == HwVendor.Unknown))
        {
            return null;
        }

        var vendors = adapters.Select(adapter => adapter.Vendor).Distinct().ToArray();
        if (!vendors.Contains(wanted))
        {
            return false;
        }

        return vendors.Length == 1 ? true : null;
    }
}

#pragma warning restore IDE0130
