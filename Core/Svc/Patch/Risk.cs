using System;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class PatPol
{
    public static Mdl.PatRisk Classify(
        string title,
        string? group,
        string? notes,
        string? author,
        bool isCanarySource)
    {
        var haystack = $"{title} {group} {notes} {author}";

        if (isCanarySource || Contains(haystack, "canary"))
        {
            return Mdl.PatRisk.Canary;
        }

        if (Contains(
            haystack,
            "cheat",
            "infinite health",
            "infinite ammo",
            "infinite money",
            "max money",
            "god mode",
            "unlock all",
            "debug menu",
            "freecam"))
        {
            return Mdl.PatRisk.Cheat;
        }

        if (Contains(
            haystack,
            "60 fps",
            "60fps",
            "120 fps",
            "120fps",
            "unlock fps",
            "fps unlock",
            "framerate",
            "frame rate",
            "vblank"))
        {
            return Mdl.PatRisk.Fps60;
        }

        if (Contains(
            haystack,
            "experimental",
            "unstable",
            "testing only",
            "test patch"))
        {
            return Mdl.PatRisk.Exp;
        }

        if (Contains(
            haystack,
            "performance",
            "faster",
            "speed",
            "slowdown",
            "disable shadows",
            "disable effects"))
        {
            return Mdl.PatRisk.Perf;
        }

        if (Contains(
            haystack,
            "aspect ratio",
            "widescreen",
            "ultrawide",
            "resolution",
            "motion blur",
            "depth-of-field",
            "depth of field",
            "dof",
            "hud",
            "menus",
            "visual",
            "graphics"))
        {
            return Mdl.PatRisk.Fix;
        }

        if (Contains(
            haystack,
            "fix",
            "skip intro",
            "skip videos",
            "crash",
            "freeze",
            "hang",
            "stutter",
            "black screen"))
        {
            return Mdl.PatRisk.SafeFix;
        }

        return Mdl.PatRisk.Unknown;
    }

    public static string InferPat(
        string title,
        string? group,
        Mdl.PatRisk risk)
    {
        if (!string.IsNullOrWhiteSpace(group))
        {
            return group.Trim();
        }

        return risk switch
        {
            Mdl.PatRisk.SafeFix => "Fix",
            Mdl.PatRisk.Fix => "Visual",
            Mdl.PatRisk.Perf => "Performance",
            Mdl.PatRisk.Fps60 => "FPS",
            Mdl.PatRisk.Cheat => "Cheat",
            Mdl.PatRisk.Exp => "Experimental",
            Mdl.PatRisk.Canary => Infer(title),
            _ => Infer(title)
        };
    }

    public static bool Is(
        Mdl.PatRisk risk,
        bool expertMode)
    {
        _ = risk;
        _ = expertMode;
        return true;
    }

    public static bool Can(
        Mdl.PatRisk risk,
        MatStat matchStatus)
    {
        return IsUser(risk) &&
               IsSafe(matchStatus);
    }

    public static bool Can(
        Mdl.PatMatch match)
    {
        return IsUser(match.Patch.Risk) &&
               PatAct.Can(match);
    }

    public static bool Should(
        Mdl.PatRisk risk,
        MatStat matchStatus)
    {
        _ = risk;
        _ = matchStatus;
        return false;
    }

    public static bool CanApply(
        Mdl.PatRisk risk,
        MatStat matchStatus,
        bool expertMode)
    {
        _ = expertMode;

        return IsUser(risk) &&
               IsSafe(matchStatus);
    }

    public static bool CanApply(
        Mdl.PatMatch match,
        bool expertMode)
    {
        _ = expertMode;

        return IsUser(match.Patch.Risk) &&
               PatAct.Can(match);
    }

    public static bool CanPat(
        Mdl.PatRisk risk,
        MatStat matchStatus)
    {
        return IsUser(risk) &&
               IsKnown(matchStatus);
    }

    public static bool CanPat(
        Mdl.PatMatch match)
    {
        return IsUser(match.Patch.Risk) &&
               PatAct.Can2(match);
    }

    private static bool IsUser(Mdl.PatRisk risk)
    {
        return risk is Mdl.PatRisk.SafeFix
            or Mdl.PatRisk.Fix
            or Mdl.PatRisk.Perf
            or Mdl.PatRisk.Fps60
            or Mdl.PatRisk.Cheat
            or Mdl.PatRisk.Exp
            or Mdl.PatRisk.Canary
            or Mdl.PatRisk.Unknown;
    }

    private static bool IsSafe(
        MatStat matchStatus)
    {
        return matchStatus == MatStat.Match;
    }

    private static bool IsKnown(
        MatStat matchStatus)
    {
        return matchStatus is MatStat.Match
            or MatStat.Unknown;
    }

    private static string Infer(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Unknown";
        }

        return Contains(title, "patch")
            ? "Patch"
            : "Unknown";
    }

    private static bool Contains(
        string value,
        params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

#pragma warning restore IDE0130
