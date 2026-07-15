using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class RecPol
{
    private static readonly Regex Pattern = new(
        @"requires\b.{0,80}\bpatch\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly IReadOnlySet<string> AutoKeys =
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

    private static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vblank rate",
            "clocks scale",
            "framelimit",
            "vblank ntsc fixup"
        };

    private static readonly IReadOnlySet<string> Keys2 =
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

    public static Mdl.RecRisk Classify(string wikiKey, string value, string? notes)
    {
        var haystack = $"{wikiKey} {value} {notes}".ToLowerInvariant();

        if (Keys.Contains(wikiKey) ||
            Contains(haystack, "60 fps", "60fps", "timing", "physics"))
        {
            return Mdl.RecRisk.Timing;
        }

        if (IsExpert(wikiKey) || Contains(haystack, "experimental", "debug"))
        {
            return Mdl.RecRisk.Only;
        }

        if (IsPatDep(haystack))
        {
            return Mdl.RecRisk.PatDep;
        }

        if (IsPerf(wikiKey, value, haystack))
        {
            return Mdl.RecRisk.PerfCost;
        }

        if (Keys2.Contains(wikiKey) || Contains(haystack, "gpu dependent", "cpu dependent", "hardware dependent"))
        {
            return Mdl.RecRisk.Dep;
        }

        if (Contains(
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
            return Mdl.RecRisk.Cond;
        }

        if (!AutoKeys.Contains(wikiKey))
        {
            return Mdl.RecRisk.Unknown;
        }

        if (Contains(haystack, "required", "mandatory"))
        {
            return Mdl.RecRisk.Required;
        }

        if (Contains(haystack, "fix", "recommended", "needed"))
        {
            return Mdl.RecRisk.Rec;
        }

        return Mdl.RecRisk.Unknown;
    }

    public static bool Requires(Mdl.RecRisk risk)
    {
        return risk is Mdl.RecRisk.Cond
            or Mdl.RecRisk.PerfCost
            or Mdl.RecRisk.PatDep
            or Mdl.RecRisk.Dep
            or Mdl.RecRisk.Timing
            or Mdl.RecRisk.Only
            or Mdl.RecRisk.Risky;
    }

    private static bool IsPatDep(string haystack)
    {
        return Pattern.IsMatch(haystack) ||
               Contains(
                   haystack,
                   "requires patch",
                   "required if not using patch",
                   "only required if not using patch",
                   "unless using patch",
                   "unless using fps patch",
                   "requires mlaa patch",
                   "patch dependent");
    }

    private static bool IsPerf(string wikiKey, string value, string haystack)
    {
        if ((wikiKey.Equals("ppu decoder", StringComparison.OrdinalIgnoreCase) ||
             wikiKey.Equals("spu decoder", StringComparison.OrdinalIgnoreCase)) &&
            value.Contains("interpreter", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Contains(
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

    private static bool IsExpert(string wikiKey)
    {
        return wikiKey.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("force cpu blit emulation", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("force gpu texture scaling", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("disable zcull occlusion queries", StringComparison.OrdinalIgnoreCase) ||
               wikiKey.Equals("hook static functions", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, params string[] fragments)
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
