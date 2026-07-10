using System;
using R3Config.Mdl;

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

        if (isCanarySource)
        {
            return PatchRisk.Canary;
        }

        if (ContainsAny(haystack, "canary"))
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

        if (ContainsAny(haystack, "experimental", "unstable", "testing only", "test patch"))
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

    public static string InferPatchType(string title, string? group, PatchRisk risk)
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

    public static bool IsVisibleInPreview(PatchRisk risk, bool expertMode)
    {
        // RPCS3 exposes Canary patches as a distinct documented patch type.
        // The preview must show them for the selected game instead of hiding them
        // behind an expert toggle, while still requiring an explicit user check.
        return true;
    }

    public static bool CanSelectInPreview(PatchRisk risk, PatchMatchStatus matchStatus)
    {
        return IsUserSelectablePatch(risk) && IsSafeMatchForPatchConfig(matchStatus);
    }

    public static bool ShouldSelectByDefaultInPreview(PatchRisk risk, PatchMatchStatus matchStatus)
    {
        // Every patch write must reflect an explicit user choice. Even a safe,
        // exact match starts unchecked.
        return false;
    }

    public static bool CanApplyCheckedPatch(PatchRisk risk, PatchMatchStatus matchStatus, bool expertMode)
    {
        return IsUserSelectablePatch(risk) && IsSafeMatchForPatchConfig(matchStatus);
    }

    public static bool CanDisableCheckedPatch(PatchRisk risk, PatchMatchStatus matchStatus)
    {
        return IsUserSelectablePatch(risk) && IsKnownPatchConfigMatch(matchStatus);
    }

    private static bool IsUserSelectablePatch(PatchRisk risk)
    {
        // The preview is an execution surface, not an automatic policy engine.
        // Exact and serial/version-matched patches may be selected manually,
        // while only SafeFix remains auto-selected by default.
        return risk is PatchRisk.SafeFix
            or PatchRisk.VisualFix
            or PatchRisk.Performance
            or PatchRisk.Fps60
            or PatchRisk.Cheat
            or PatchRisk.Experimental
            or PatchRisk.Canary
            or PatchRisk.Unknown;
    }

    private static bool IsSafeMatchForPatchConfig(PatchMatchStatus matchStatus)
    {
        return matchStatus == PatchMatchStatus.ExactMatch;
    }

    private static bool IsKnownPatchConfigMatch(PatchMatchStatus matchStatus)
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

        return ContainsAny(title, "patch") ? "Patch" : "Unknown";
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
