using System;
using System.Collections.Generic;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record LocalizedNoteText(
    string Title,
    string Type,
    string Body,
    string OriginalBody,
    bool HasOriginalBody);

public static class PatchTextLocalizer
{
    private static readonly Dictionary<string, string> ExactBodyKeyByRaw =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["patch.yml file is missing"] =
                "Patch.Localized.FileMissing",
            ["ملف patch.yml غير موجود"] =
                "Patch.Localized.FileMissing",
            ["Run analysis first to show patches"] =
                "Patch.Localized.RunAnalysisFirst",
            ["نفّذ التحليل أولًا لعرض الباتشات"] =
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

    private static readonly Dictionary<string, string> ExactTypeKeyByRaw =
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

    public static LocalizedNoteText LocalizeRecommendationNote(
        string? raw,
        RecommendationRisk risk)
    {
        return Localize(
            raw,
            UiText.T("Plan.NoteTooltipTitle"),
            TypeKeyFor(risk));
    }

    public static LocalizedNoteText LocalizePatchMessage(
        string? raw)
    {
        return Localize(
            raw,
            UiText.T("Patch.NoteTooltipTitle"),
            TypeKeyForPatchRaw(raw));
    }

    public static LocalizedNoteText LocalizePatchSurfaceMessage(
        string? raw)
    {
        return Localize(
            raw,
            UiText.T("Patch.Title"),
            TypeKeyForPatchRaw(raw));
    }

    private static LocalizedNoteText Localize(
        string? raw,
        string title,
        string typeKey)
    {
        var type = UiText.T(typeKey);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Ui.EmptyValue"),
                string.Empty,
                false);
        }

        var trimmedRaw = raw.Trim();
        var normalized = Normalize(trimmedRaw);
        var lookupText = NormalizeForLookup(normalized);

        if (ExactBodyKeyByRaw.TryGetValue(
                lookupText,
                out var bodyKey))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T(bodyKey),
                string.Empty,
                false);
        }

        if (ExactTypeKeyByRaw.TryGetValue(
                lookupText,
                out var exactTypeKey))
        {
            var exactType = UiText.T(exactTypeKey);

            return new LocalizedNoteText(
                title,
                exactType,
                exactType,
                string.Empty,
                false);
        }

        if (IsImportedPatchDuplicateMessage(normalized))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Patch.ImportedDuplicateKeyReadable") +
                Environment.NewLine +
                UiText.T("Patch.ImportedDuplicateKeySuggestion"),
                string.Empty,
                false);
        }

        if (IsImportedPatchParseMessage(normalized))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Patch.ImportedFileInvalid"),
                trimmedRaw,
                true);
        }

        if (IsDuplicateKeyMessage(normalized))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Patch.DuplicateKeyReadable") +
                Environment.NewLine +
                UiText.T("Patch.DuplicateKeySuggestion"),
                trimmedRaw,
                true);
        }

        if (IsParseMessage(normalized))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Patch.Localized.ParseError"),
                trimmedRaw,
                true);
        }

        if (ContainsAny(
                normalized,
                "unsupported",
                "not supported",
                "unknown patch"))
        {
            return new LocalizedNoteText(
                title,
                type,
                UiText.T("Patch.Localized.UnsupportedPatchNote"),
                trimmedRaw,
                true);
        }

        var translated =
            PatchNoteTranslationService.Translate(trimmedRaw);

        return new LocalizedNoteText(
            title,
            type,
            translated.Body,
            translated.OriginalBody,
            translated.HasOriginalBody);
    }

    private static string TypeKeyFor(
        RecommendationRisk risk)
    {
        return risk switch
        {
            RecommendationRisk.Optional =>
                "NoteType.Optional",
            RecommendationRisk.Conditional =>
                "NoteType.Conditional",
            RecommendationRisk.HardwareDependent =>
                "NoteType.HardwareBased",
            RecommendationRisk.ManualTiming =>
                "NoteType.ManualTiming",
            RecommendationRisk.PatchDependent =>
                "NoteType.PatchRequired",
            RecommendationRisk.ExpertOnly or
            RecommendationRisk.Risky or
            RecommendationRisk.Unknown =>
                "NoteType.NotAutoSelected",
            _ =>
                "NoteType.CompatibilityWarning"
        };
    }

    private static string TypeKeyForPatchRaw(
        string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "NoteType.NotAutoSelected";
        }

        var normalized = Normalize(raw);
        var lookupText = NormalizeForLookup(normalized);

        if (ExactTypeKeyByRaw.TryGetValue(
                lookupText,
                out var key))
        {
            return key;
        }

        if (IsDuplicateKeyMessage(normalized) ||
            IsParseMessage(normalized) ||
            ContainsAny(
                normalized,
                "unsupported",
                "not supported",
                "unknown patch"))
        {
            return "NoteType.CompatibilityWarning";
        }

        if (ContainsAny(
                normalized,
                "patch required",
                "requires patch",
                "depends on patch"))
        {
            return "NoteType.PatchRequired";
        }

        if (ContainsAny(
                normalized,
                "fps",
                "frame rate",
                "framerate"))
        {
            return "NoteType.ManualTiming";
        }

        if (ContainsAny(
                normalized,
                "optional",
                "performance"))
        {
            return "NoteType.Optional";
        }

        return "NoteType.CompatibilityWarning";
    }

    private static bool IsImportedPatchDuplicateMessage(
        string normalized)
    {
        return ContainsAny(
                   normalized,
                   "imported_patch.yml") &&
               IsDuplicateKeyMessage(normalized);
    }

    private static bool IsImportedPatchParseMessage(
        string normalized)
    {
        return ContainsAny(
                   normalized,
                   "imported_patch.yml") &&
               ContainsAny(
                   normalized,
                   "parse",
                   "parsing",
                   "parser",
                   "yaml",
                   "invalid",
                   "could not be read");
    }

    private static bool IsDuplicateKeyMessage(
        string normalized)
    {
        return ContainsAny(
            normalized,
            "duplicate key",
            "duplicated key",
            "already contains an entry");
    }

    private static bool IsParseMessage(
        string normalized)
    {
        return ContainsAny(
            normalized,
            "parse",
            "parsing",
            "parser",
            "yaml",
            "invalid");
    }

    private static string Normalize(string value)
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

    private static string NormalizeForLookup(
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

    private static bool ContainsAny(
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