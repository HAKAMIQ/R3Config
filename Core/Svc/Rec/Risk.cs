using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class RecommendationRiskPolicy
{
    private static readonly Regex RequiresPatchPattern = new(
        @"requires\b.{0,80}\bpatch\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly IReadOnlySet<string> AutoSelectableFixKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "write color buffers",
            "read color buffers",
            "write depth buffers",
            "read depth buffers",
            "strict rendering mode",
            "accurate spu dma",
            "accurate rsx reservation access",
            "spu xfloat accuracy"
        };

    private static readonly IReadOnlySet<string> ManualTimingKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vblank rate",
            "clocks scale",
            "framelimit",
            "vblank ntsc fixup"
        };

    private static readonly IReadOnlySet<string> HardwareDependentKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "renderer",
            "preferred spu threads",
            "thread scheduler",
            "resolution scale",
            "resolution scale threshold",
            "default resolution",
            "anisotropic filter",
            "anti-aliasing",
            "shader quality",
            "asynchronous texture streaming",
            "multithreaded rsx",
            "driver wake-up delay"
        };

    public static RecommendationRisk Classify(string wikiKey, string value, string? notes)
    {
        var haystack = $"{wikiKey} {value} {notes}".ToLowerInvariant();

        if (ManualTimingKeys.Contains(wikiKey) ||
            ContainsAny(haystack, "60 fps", "60fps", "timing", "physics"))
        {
            return RecommendationRisk.ManualTiming;
        }

        if (IsExpertOnlyKey(wikiKey) || ContainsAny(haystack, "experimental", "debug"))
        {
            return RecommendationRisk.ExpertOnly;
        }

        if (IsPatchDependent(haystack))
        {
            return RecommendationRisk.PatchDependent;
        }

        if (IsPerformanceCost(wikiKey, value, haystack))
        {
            return RecommendationRisk.PerformanceCost;
        }

        if (HardwareDependentKeys.Contains(wikiKey) || ContainsAny(haystack, "gpu dependent", "cpu dependent", "hardware dependent"))
        {
            return RecommendationRisk.HardwareDependent;
        }

        if (ContainsAny(
            haystack,
            "optional",
            "unless using",
            "if not using",
            "only required if",
            "required only if",
            "required unless",
            "only needed if",
            "needed only if",
            "if using",
            "when using",
            "may cause"))
        {
            return RecommendationRisk.Conditional;
        }

        if (!AutoSelectableFixKeys.Contains(wikiKey))
        {
            return RecommendationRisk.Unknown;
        }

        if (ContainsAny(haystack, "required", "mandatory"))
        {
            return RecommendationRisk.Required;
        }

        if (ContainsAny(haystack, "fix", "recommended", "needed"))
        {
            return RecommendationRisk.Recommended;
        }

        return RecommendationRisk.Unknown;
    }

    public static bool RequiresExtraApplyWarning(RecommendationRisk risk)
    {
        return risk is RecommendationRisk.Conditional
            or RecommendationRisk.PerformanceCost
            or RecommendationRisk.PatchDependent
            or RecommendationRisk.HardwareDependent
            or RecommendationRisk.ManualTiming
            or RecommendationRisk.ExpertOnly
            or RecommendationRisk.Risky;
    }

    private static bool IsPatchDependent(string haystack)
    {
        return RequiresPatchPattern.IsMatch(haystack) ||
               ContainsAny(
                   haystack,
                   "requires patch",
                   "required if not using patch",
                   "only required if not using patch",
                   "unless using patch",
                   "unless using fps patch",
                   "requires mlaa patch",
                   "patch dependent");
    }

    private static bool IsPerformanceCost(string wikiKey, string value, string haystack)
    {
        if ((wikiKey.Equals("ppu decoder", StringComparison.OrdinalIgnoreCase) ||
             wikiKey.Equals("spu decoder", StringComparison.OrdinalIgnoreCase)) &&
            value.Contains("interpreter", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContainsAny(
            haystack,
            "reduces performance",
            "reduce performance",
            "performance cost",
            "performance hit",
            "decreases performance",
            "decrease performance",
            "lower performance",
            "slowdown",
            "slowdowns",
            "very demanding");
    }

    private static bool IsExpertOnlyKey(string wikiKey)
    {
        return wikiKey.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("force cpu blit emulation", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("force gpu texture scaling", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("disable zcull occlusion queries", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("hook static functions", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
