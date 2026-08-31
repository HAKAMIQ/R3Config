using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record NoteText(
    string Title,
    string Type,
    string Body,
    string OrigBody,
    bool HasOrig,
    NoteKind Kind)
{
    public bool IsInstruction =>
        Kind is NoteKind.Instruction or NoteKind.Requirement;
}

public static class PatText
{
    private static readonly Dictionary<string, string> BodyKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["patch.yml file is missing"] =
                "Patch.Localized.FileMissing",
            ["ملف patch.yml غير موجود"] =
                "Patch.Localized.FileMissing",
            ["Run analysis first to show patches"] =
                "Patch.Localized.RunAnalysisFirst",
            ["نفذ التحليل أولا لعرض الباتشات"] =
                "Patch.Localized.RunAnalysisFirst",
            ["No patches are available for this game"] =
                "Patch.Localized.NoPatchesForGame",
            ["No patches found for this game"] =
                "Patch.Localized.NoPatchesForGame",
            ["لا توجد باتشات متاحة لهذه اللعبة"] =
                "Patch.Localized.NoPatchesForGame",
            ["لا توجد باتشات لهذه اللعبة"] =
                "Patch.Localized.NoPatchesForGame"
        };

    private static readonly Dictionary<string, string> TypeKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Compatibility warning"] =
                "NoteType.CompatibilityWarning",
            ["Hardware based"] =
                "NoteType.HardwareBased",
            ["Manual timing"] =
                "NoteType.ManualTiming",
            ["Optional"] =
                "NoteType.Optional",
            ["Conditional"] =
                "NoteType.Conditional",
            ["Not auto-selected"] =
                "NoteType.NotAutoSelected",
            ["Patch required"] =
                "NoteType.PatchRequired"
        };

    public static NoteText RecNote(
        string? raw,
        RecRisk risk)
    {
        return Localize(
            raw,
            UiText.GetText("Plan.NoteTooltipTitle"),
            TypeKey(risk));
    }

    public static NoteText PatMsg(
        string? raw)
    {
        return Localize(
            raw,
            UiText.GetText("Patch.NoteTooltipTitle"),
            RawKey(raw));
    }

    public static NoteText SurfMsg(
        string? raw)
    {
        return Localize(
            raw,
            UiText.GetText("Patch.Title"),
            RawKey(raw));
    }

    public static string DspTitle(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : raw.Trim();
    }

    private static NoteText Localize(
        string? raw,
        string title,
        string typeKey)
    {
        var type = UiText.GetText(typeKey);
        var kind = NoteSvc.Classify(raw);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Ui.EmptyValue"),
                string.Empty,
                false,
                kind);
        }

        var trimmedRaw = raw.Trim();
        var normalized = Norm(trimmedRaw);
        var lookupText = NormKey(normalized);

        if (BodyKeys.TryGetValue(
                lookupText,
                out var bodyKey))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText(bodyKey),
                string.Empty,
                false,
                kind);
        }

        if (TypeKeys.TryGetValue(
                lookupText,
                out var exactTypeKey))
        {
            var exactType = UiText.GetText(exactTypeKey);

            return new NoteText(
                title,
                exactType,
                exactType,
                string.Empty,
                false,
                kind);
        }

        if (IsImpDup(normalized))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Patch.ImportedDuplicateKeyReadable") +
                Environment.NewLine +
                UiText.GetText("Patch.ImportedDuplicateKeySuggestion"),
                string.Empty,
                false,
                kind);
        }

        if (IsImpBad(normalized))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Patch.ImportedFileInvalid"),
                trimmedRaw,
                true,
                kind);
        }

        if (IsDupKey(normalized))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Patch.DuplicateKeyReadable") +
                Environment.NewLine +
                UiText.GetText("Patch.DuplicateKeySuggestion"),
                trimmedRaw,
                true,
                kind);
        }

        if (IsParse(normalized))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Patch.Localized.ParseError"),
                trimmedRaw,
                true,
                kind);
        }

        if (HasAny(
                normalized,
                "unsupported",
                "not supported",
                "unknown patch"))
        {
            return new NoteText(
                title,
                type,
                UiText.GetText("Patch.Localized.UnsupportedPatchNote"),
                trimmedRaw,
                true,
                kind);
        }

        var translated =
            NoteSvc.Trans(trimmedRaw);

        return new NoteText(
            title,
            type,
            translated.Body,
            translated.OrigBody,
            translated.HasOrig,
            translated.Kind);
    }

    private static string TypeKey(
        RecRisk risk)
    {
        return risk switch
        {
            RecRisk.Optional =>
                "NoteType.Optional",
            RecRisk.Cond =>
                "NoteType.Conditional",
            RecRisk.Dep =>
                "NoteType.HardwareBased",
            RecRisk.Timing =>
                "NoteType.ManualTiming",
            RecRisk.PatDep =>
                "NoteType.PatchRequired",
            RecRisk.Only or
            RecRisk.Risky or
            RecRisk.Unknown =>
                "NoteType.NotAutoSelected",
            _ =>
                "NoteType.CompatibilityWarning"
        };
    }

    private static string RawKey(
        string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "NoteType.NotAutoSelected";
        }

        var normalized = Norm(raw);
        var lookupText = NormKey(normalized);

        if (TypeKeys.TryGetValue(
                lookupText,
                out var key))
        {
            return key;
        }

        if (IsDupKey(normalized) ||
            IsParse(normalized) ||
            HasAny(
                normalized,
                "unsupported",
                "not supported",
                "unknown patch"))
        {
            return "NoteType.CompatibilityWarning";
        }

        if (HasAny(
                normalized,
                "patch required",
                "requires patch",
                "depends on patch"))
        {
            return "NoteType.PatchRequired";
        }

        if (HasAny(
                normalized,
                "fps",
                "frame rate",
                "framerate"))
        {
            return "NoteType.ManualTiming";
        }

        if (HasAny(
                normalized,
                "optional",
                "performance"))
        {
            return "NoteType.Optional";
        }

        return "NoteType.CompatibilityWarning";
    }

    private static bool IsImpDup(
        string normalized)
    {
        return HasAny(
                   normalized,
                   "imported_patch.yml") &&
               IsDupKey(normalized);
    }

    private static bool IsImpBad(
        string normalized)
    {
        return HasAny(
                   normalized,
                   "imported_patch.yml") &&
               HasAny(
                   normalized,
                   "parse",
                   "parsing",
                   "parser",
                   "yaml",
                   "invalid",
                   "could not be read");
    }

    private static bool IsDupKey(
        string normalized)
    {
        return HasAny(
            normalized,
            "duplicate key",
            "duplicated key",
            "already contains an entry");
    }

    private static bool IsParse(
        string normalized)
    {
        return HasAny(
            normalized,
            "parse",
            "parsing",
            "parser",
            "yaml",
            "invalid");
    }

    private static string Norm(string value)
    {
        return StripArabicDiacritics(value)
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

    private static string StripArabicDiacritics(string value)
    {
        return new string(value.Where(ch => !IsArabicDiacritic(ch)).ToArray());
    }

    private static bool IsArabicDiacritic(char ch)
    {
        return (ch >= '\u0610' && ch <= '\u061A') ||
               (ch >= '\u064B' && ch <= '\u065F') ||
               ch == '\u0670' ||
               (ch >= '\u06D6' && ch <= '\u06ED');
    }

    private static string NormKey(
        string value)
    {
        var compact = string.Join(
            " ",
            value.Split(
                [' ', '\t', '\n'],
                StringSplitOptions.RemoveEmptyEntries));

        return compact
            .Trim()
            .TrimEnd('.', '。', '،', '؛');
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
}
