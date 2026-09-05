using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Lang;

namespace R3Config.Svc;

public enum NoteQual
{
    Original,
    Exact,
    Fragment,
    Fallback
}

public enum NoteKind
{
    Description,
    Instruction,
    Requirement,
    Warning
}

public sealed record NoteRes(
    string Body,
    string OrigBody,
    bool HasOrig,
    NoteQual Quality,
    NoteKind Kind)
{
    public bool IsInstruction =>
        Kind is NoteKind.Instruction or NoteKind.Requirement;
}

public static partial class NoteSvc
{
    public static int ReviewedPatchArabicTranslationCount =>
        ArByEn.Count;

    public static int ReviewedWikiArabicTranslationCount =>
        WikiArByEn.Count;

    public static int ReviewedOfficialPatchArabicTranslationCount =>
        OfficialArByEn.Count;

    public static IReadOnlyList<string> ProtectedRpcS3Terms { get; } =
    [
        .. WikiSvc.CanonicalConfigSettingNames
            .Concat(
            [
                "VBlank Frequency",
                "Clock Scale",
                "Accurate RSX Reservations",
                "Resolution Scale Threshold",
                "Approximate ZCull",
                "Precise ZCull",
                "ZCull Accuracy",
                "Frame Limit",
                "Write Depth Buffers",
                "Read Depth Buffers"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    public static IReadOnlyList<string> ProtectedRpcS3Values { get; } =
    [
        .. WikiSvc.CanonicalConfigValues
    ];

    private static readonly Dictionary<string, string> ProtectedTargets =
        BuildProtectedTargets();

    private static readonly Dictionary<string, string> ActionKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["fly"] = "Patch.Text.Action.Fly",
            ["move up"] = "Patch.Text.Action.MoveUp",
            ["stop pursuit"] = "Patch.Text.Action.StopPursuit",
            ["toggle walking in air"] =
                "Patch.Text.Action.ToggleWalkingInAir",
            ["toggle or disable AI movement"] =
                "Patch.Text.Action.ToggleAiMovement",
            ["low CPU health"] = "Patch.Text.Action.LowCpuHealth",
            ["kill P2"] = "Patch.Text.Action.KillP2"
        };

    private static readonly HashSet<string> ControllerTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "L1", "L2", "L3",
            "R1", "R2", "R3",
            "X", "O", "Square", "Triangle",
            "Select", "Start"
        };



    public static NoteRes Trans(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new NoteRes(
                string.Empty,
                string.Empty,
                false,
                NoteQual.Original,
                NoteKind.Description);
        }

        var original = raw.Trim();
        var kind = Classify(original);

        if (!UiText.IsRtl || ContainsArabic(original))
        {
            return new NoteRes(
                original,
                string.Empty,
                false,
                NoteQual.Original,
                kind);
        }

        var normalized = Norm(original);
        var lookupText = NormKey(normalized);

        if (ContainsInternalDiagnostic(lookupText))
        {
            return new NoteRes(
                FallbackBody(kind),
                original,
                true,
                NoteQual.Fallback,
                kind);
        }

        if (TryExactArabic(
                lookupText,
                original,
                out var exact))
        {
            return new NoteRes(
                exact,
                string.Empty,
                false,
                NoteQual.Exact,
                kind);
        }

        if (TryStructured(
                lookupText,
                out var structured,
                out _))
        {
            return new NoteRes(
                structured,
                original,
                true,
                NoteQual.Fragment,
                kind);
        }

        return new NoteRes(
            FallbackBody(kind),
            original,
            true,
            NoteQual.Fallback,
            kind);
    }

    public static NoteKind Classify(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return NoteKind.Description;
        }

        var value = Unwrap(NormKey(Norm(raw)));
        if (ContainsArabic(value))
        {
            if (value.Equals(
                    "تحذير",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(
                    "تحذير:",
                    StringComparison.OrdinalIgnoreCase))
            {
                return NoteKind.Warning;
            }

            if (StartsWithAny(
                    value,
                    "يجب ",
                    "يتطلب ",
                    "يعمل فقط "))
            {
                return NoteKind.Requirement;
            }

            if (StartsWithAny(
                    value,
                    "فعل ",
                    "عطل ",
                    "اضغط ",
                    "بدل حالة ",
                    "استخدم "))
            {
                return NoteKind.Instruction;
            }

            return NoteKind.Description;
        }

        if (value.Equals(
                "Warning",
                StringComparison.OrdinalIgnoreCase) ||
            StartsWithAny(
                value,
                "Warning:",
                "Be careful ",
                "Caution "))
        {
            return NoteKind.Warning;
        }

        if (StartsWithAny(
                value,
                "Requires ",
                "Required ",
                "Needed ",
                "Only works with ",
                "Only works when ",
                "Must ",
                "This needs to be activated ",
                "This needs to be turned on ",
                "To be enabled ",
                "To be able to ",
                "Recommended to ",
                "You will need to "))
        {
            return NoteKind.Requirement;
        }

        if (ControllerTokens.Any(token =>
                value.StartsWith(
                    token + " =",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(
                    token + " +",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return NoteKind.Instruction;
        }

        if (StartsWithAny(
                value,
                "Enable ",
                "Disable ",
                "Toggle ",
                "Press ",
                "Hold ",
                "Just hold ",
                "Just press ",
                "Set ",
                "Use ",
                "Increase ",
                "Remove ",
                "Change ",
                "Turn on ",
                "Turn off ",
                "Activate ",
                "Deactivate ",
                "Check ",
                "Enter ",
                "Save ",
                "Take "))
        {
            return NoteKind.Instruction;
        }

        if (StartsWithAny(
                value,
                "Do not ",
                "Don't ",
                "May break ",
                "Can break ",
                "Breaks ",
                "Causes ",
                "If you encounter ",
                "If not set ",
                "Be careful ",
                "Caution ",
                "Only work ",
                "You can not ",
                "You cannot ",
                "Without this "))
        {
            return NoteKind.Warning;
        }

        if (HasAny(
                value,
                " will crash",
                " may crash",
                " will freeze",
                " can break",
                " will not progress",
                " do not ",
                " don't ",
                " need to ",
                " needs to ",
                " requires ",
                " required ",
                " must ",
                " disable this",
                " turn it off",
                " turn this off"))
        {
            return NoteKind.Warning;
        }

        return NoteKind.Description;
    }

    public static bool IsInstruction(string? raw)
    {
        return Classify(raw) is
            NoteKind.Instruction or NoteKind.Requirement;
    }

    private static string FallbackBody(NoteKind kind)
    {
        _ = kind;
        return string.Empty;
    }

    private static bool TryExactArabic(
        string normalized,
        string original,
        out string localized)
    {
        return OfficialArByEn.TryGetValue(normalized, out localized!) ||
               OfficialArByEn.TryGetValue(original, out localized!) ||
               ArByEn.TryGetValue(normalized, out localized!) ||
               ArByEn.TryGetValue(original, out localized!) ||
               WikiArByEn.TryGetValue(normalized, out localized!) ||
               WikiArByEn.TryGetValue(original, out localized!);
    }

    private static bool StartsWithAny(
        string value,
        params string[] prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryStructured(
        string normalized,
        out string localized,
        out bool isInstruction)
    {
        localized = string.Empty;
        isInstruction = false;

        var value = Unwrap(normalized);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Equals(
                "Experimental",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.GetText("Patch.Text.Experimental");
            return true;
        }

        if (value.Equals(
                "Warning",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "Warning:",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.GetText("Patch.Text.Warning");
            return true;
        }

        if (value.Equals(
                "Enable this patch if you are using \"Write Color Buffers\" to restore performance",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.Fmt(
                "Patch.Text.EnablePatchWithOption",
                "Write Color Buffers");
            isInstruction = true;
            return true;
        }

        if (value.StartsWith(
                "Requires the button enable code to be ",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.GetText(
                "Patch.Text.RequiresEnableCodeFirst");
            isInstruction = true;
            return true;
        }

        if (value.StartsWith(
                "This needs to be activated before ",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.GetText(
                "Patch.Text.EnableCodeBeforeRelated");
            isInstruction = true;
            return true;
        }

        if (value.StartsWith(
                "Just hold Select ",
                StringComparison.OrdinalIgnoreCase))
        {
            localized = UiText.GetText(
                "Patch.Text.HoldSelectToMark");
            isInstruction = true;
            return true;
        }

        if (TryControllerInstruction(
                value,
                out localized))
        {
            isInstruction = true;
            return true;
        }

        if (TryPrefix(
                value,
                "Requires disabling ",
                out var target) &&
            TryProtectedTarget(
                target,
                out var protectedTarget))
        {
            localized = UiText.Fmt(
                "Patch.Text.RequiresDisable",
                protectedTarget);
            isInstruction = true;
            return true;
        }

        if (TryPrefix(
                value,
                "Only works when ",
                out target) &&
            TryTrimSuffix(
                target,
                " is enabled",
                out target) &&
            TryProtectedTarget(
                target,
                out protectedTarget))
        {
            localized = UiText.Fmt(
                "Patch.Text.OnlyWorksEnabled",
                protectedTarget);
            isInstruction = true;
            return true;
        }

        if (TryPrefix(
                value,
                "Only works with ",
                out target) &&
            TryProtectedTarget(
                target,
                out protectedTarget))
        {
            localized = UiText.Fmt(
                "Patch.Text.OnlyWorksEnabled",
                protectedTarget);
            isInstruction = true;
            return true;
        }

        if (TryPrefix(
                value,
                "Requires ",
                out target) &&
            TryProtectedTarget(
                target,
                out protectedTarget))
        {
            localized = UiText.Fmt(
                "Patch.Text.RequiresEnable",
                protectedTarget);
            isInstruction = true;
            return true;
        }

        foreach (var (prefix, key) in new[]
                 {
                     ("Enable ", "Patch.Text.Enable"),
                     ("Disable ", "Patch.Text.Disable"),
                     ("Toggle ", "Patch.Text.Toggle")
                 })
        {
            if (!TryPrefix(
                    value,
                    prefix,
                    out target) ||
                !TryProtectedTarget(
                    target,
                    out protectedTarget))
            {
                continue;
            }

            localized = UiText.Fmt(
                key,
                protectedTarget);
            isInstruction = true;
            return true;
        }

        return false;
    }

    private static bool TryControllerInstruction(
        string value,
        out string localized)
    {
        localized = string.Empty;

        var isHold = TryPrefix(
            value,
            "Hold ",
            out var remainder);
        var isPress = !isHold && TryPrefix(
            value,
            "Press ",
            out remainder);

        if (!isHold && !isPress)
        {
            return false;
        }

        var actionIndex = remainder.IndexOf(
            " to ",
            StringComparison.OrdinalIgnoreCase);
        var chordText = actionIndex < 0
            ? remainder
            : remainder[..actionIndex];

        if (!TryControllerChord(
                chordText,
                out var chord))
        {
            return false;
        }

        if (actionIndex < 0)
        {
            localized = UiText.Fmt(
                isHold
                    ? "Patch.Text.Hold"
                    : "Patch.Text.Press",
                chord);
            return true;
        }

        var action = CleanTerminal(
            remainder[(actionIndex + 4)..]);
        if (!ActionKeys.TryGetValue(
                action,
                out var actionKey))
        {
            return false;
        }

        localized = UiText.Fmt(
            isHold
                ? "Patch.Text.HoldToAction"
                : "Patch.Text.PressToAction",
            chord,
            UiText.GetText(actionKey));
        return true;
    }

    private static bool TryControllerChord(
        string value,
        out string chord)
    {
        var tokens = value
            .Split(
                '+',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0 ||
            tokens.Any(token => !ControllerTokens.Contains(token)))
        {
            chord = string.Empty;
            return false;
        }

        chord = string.Join(" + ", tokens);
        return true;
    }

    private static bool TryProtectedTarget(
        string value,
        out string target)
    {
        var candidate = CleanTerminal(value);
        if (candidate.StartsWith(
                "the ",
                StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[4..].Trim();
        }

        if (candidate.Length >= 2 &&
            candidate[0] == '"' &&
            candidate[^1] == '"')
        {
            candidate = candidate[1..^1].Trim();
        }

        return ProtectedTargets.TryGetValue(
            candidate,
            out target!);
    }

    private static Dictionary<string, string> BuildProtectedTargets()
    {
        var targets = ProtectedRpcS3Terms.ToDictionary(
            value => value,
            value => value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var value in new[]
                 {
                     "VBlank",
                     "Enable Codes",
                     "Button Enable Code",
                     "Button Enable Codes",
                     "Joker Pad"
                 })
        {
            targets[value] = value;
        }

        return targets;
    }

    private static bool TryPrefix(
        string value,
        string prefix,
        out string remainder)
    {
        if (!value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            remainder = string.Empty;
            return false;
        }

        remainder = value[prefix.Length..].Trim();
        return remainder.Length > 0;
    }

    private static bool TryTrimSuffix(
        string value,
        string suffix,
        out string remainder)
    {
        var candidate = CleanTerminal(value);
        if (!candidate.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            remainder = string.Empty;
            return false;
        }

        remainder = candidate[..^suffix.Length].Trim();
        return remainder.Length > 0;
    }

    private static string Unwrap(string value)
    {
        var candidate = CleanTerminal(value);
        if (candidate.Length >= 2 &&
            ((candidate[0] == '(' && candidate[^1] == ')') ||
             (candidate[0] == '[' && candidate[^1] == ']')))
        {
            return candidate[1..^1].Trim();
        }

        return candidate;
    }

    private static string CleanTerminal(string value)
    {
        return value
            .Trim()
            .TrimEnd('.', '。', '،', '؛', ':')
            .Trim();
    }

    private static bool ContainsInternalDiagnostic(string value)
    {
        return HasAny(
            value,
            "HASH_UNKNOWN",
            "HASHMISMATCH",
            "HASH_MISMATCH");
    }

    private static string Norm(string value)
    {
        return value
            .Trim()
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);
    }

    private static string NormKey(string value)
    {
        var compact = string.Join(
            " ",
            value.Split(
                [' ', '\t', '\n'],
                StringSplitOptions.RemoveEmptyEntries));

        return compact
            .Trim()
            .TrimEnd('.', '。');
    }

    private static bool HasAny(
        string value,
        params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsArabic(string value)
    {
        foreach (var character in value)
        {
            if ((character >= '\u0600' &&
                 character <= '\u06FF') ||
                (character >= '\u0750' &&
                 character <= '\u077F') ||
                (character >= '\u08A0' &&
                 character <= '\u08FF') ||
                (character >= '\uFB50' &&
                 character <= '\uFDFF') ||
                (character >= '\uFE70' &&
                 character <= '\uFEFF'))
            {
                return true;
            }
        }

        return false;
    }
}
