using System;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class PatchRiskPolicy
{
    public static PatchRisk Classify(
        string title,
        string? group,
        string? notes,
        string? author,
        bool isCanarySource)
    {
        var haystack = $"{title} {group} {notes} {author}";

        if (isCanarySource || ContainsAny(haystack, "canary"))
        {
            return PatchRisk.Canary;
        }

        if (ContainsAny(
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
            return PatchRisk.Cheat;
        }

        if (ContainsAny(
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
            return PatchRisk.Fps60;
        }

        if (ContainsAny(
            haystack,
            "experimental",
            "unstable",
            "testing only",
            "test patch"))
        {
            return PatchRisk.Experimental;
        }

        if (ContainsAny(
            haystack,
            "performance",
            "faster",
            "speed",
            "slowdown",
            "disable shadows",
            "disable effects"))
        {
            return PatchRisk.Performance;
        }

        if (ContainsAny(
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
            return PatchRisk.VisualFix;
        }

        if (ContainsAny(
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
            return PatchRisk.SafeFix;
        }

        return PatchRisk.Unknown;
    }

    public static string InferPatchType(
        string title,
        string? group,
        PatchRisk risk)
    {
        if (!string.IsNullOrWhiteSpace(group))
        {
            return group.Trim();
        }

        return risk switch
        {
            PatchRisk.SafeFix => "Fix",
            PatchRisk.VisualFix => "Visual",
            PatchRisk.Performance => "Performance",
            PatchRisk.Fps60 => "FPS",
            PatchRisk.Cheat => "Cheat",
            PatchRisk.Experimental => "Experimental",
            PatchRisk.Canary => "Canary",
            _ => InferUnknownType(title)
        };
    }

    public static bool IsVisibleInPreview(
        PatchRisk risk,
        bool expertMode)
    {
        _ = risk;
        _ = expertMode;
        return true;
    }

    public static bool CanSelectInPreview(
        PatchRisk risk,
        PatchMatchStatus matchStatus)
    {
        return IsUserSelectablePatch(risk) &&
               IsSafeMatchForPatchConfig(matchStatus);
    }

    public static bool CanSelectInPreview(
        Rpcs3PatchMatchResult match)
    {
        return IsUserSelectablePatch(match.Patch.Risk) &&
               PatchActivationPolicy.CanEnable(match);
    }

    public static bool ShouldSelectByDefaultInPreview(
        PatchRisk risk,
        PatchMatchStatus matchStatus)
    {
        _ = risk;
        _ = matchStatus;
        return false;
    }

    public static bool CanApplyCheckedPatch(
        PatchRisk risk,
        PatchMatchStatus matchStatus,
        bool expertMode)
    {
        _ = expertMode;

        return IsUserSelectablePatch(risk) &&
               IsSafeMatchForPatchConfig(matchStatus);
    }

    public static bool CanApplyCheckedPatch(
        Rpcs3PatchMatchResult match,
        bool expertMode)
    {
        _ = expertMode;

        return IsUserSelectablePatch(match.Patch.Risk) &&
               PatchActivationPolicy.CanEnable(match);
    }

    public static bool CanDisableCheckedPatch(
        PatchRisk risk,
        PatchMatchStatus matchStatus)
    {
        return IsUserSelectablePatch(risk) &&
               IsKnownPatchConfigMatch(matchStatus);
    }

    public static bool CanDisableCheckedPatch(
        Rpcs3PatchMatchResult match)
    {
        return IsUserSelectablePatch(match.Patch.Risk) &&
               PatchActivationPolicy.CanDisable(match);
    }

    private static bool IsUserSelectablePatch(PatchRisk risk)
    {
        return risk is PatchRisk.SafeFix
            or PatchRisk.VisualFix
            or PatchRisk.Performance
            or PatchRisk.Fps60
            or PatchRisk.Cheat
            or PatchRisk.Experimental
            or PatchRisk.Canary
            or PatchRisk.Unknown;
    }

    private static bool IsSafeMatchForPatchConfig(
        PatchMatchStatus matchStatus)
    {
        return matchStatus == PatchMatchStatus.ExactMatch;
    }

    private static bool IsKnownPatchConfigMatch(
        PatchMatchStatus matchStatus)
    {
        return matchStatus is PatchMatchStatus.ExactMatch
            or PatchMatchStatus.SerialVersionMatchHashUnknown;
    }

    private static string InferUnknownType(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Unknown";
        }

        return ContainsAny(title, "patch")
            ? "Patch"
            : "Unknown";
    }

    private static bool ContainsAny(
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
