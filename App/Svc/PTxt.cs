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

    public static string ArtemisTitle(
        string? raw,
        bool hasDependencies = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = raw.Trim();
        foreach (var prefix in new[] { "(B)", "(b)", "(M)" })
        {
            if (title.StartsWith(prefix + " ", StringComparison.Ordinal))
            {
                title = title[(prefix.Length + 1)..].TrimStart();
                break;
            }
        }

        title = StripArtemisTitleMetadata(
            title,
            hasDependencies);

        if (title.Equals(
                "Button Enable Code",
                StringComparison.OrdinalIgnoreCase) ||
            title.Equals(
                "Button Enable Codes",
                StringComparison.OrdinalIgnoreCase))
        {
            return UiText.GetText("Patch.Artemis.ButtonSupportCode");
        }

        return title
            .Replace(" + \\/", " + ↓", StringComparison.Ordinal)
            .Replace(" + >", " + →", StringComparison.Ordinal)
            .Replace(" + <", " + ←", StringComparison.Ordinal)
            .Replace(" + ^", " + ↑", StringComparison.Ordinal);
    }

    public static bool IsEmptyDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var value = raw.Trim();
        return value is "-" or "–" or "—";
    }

    public static bool SameMeaning(
        string? first,
        string? second)
    {
        var left = MeaningKey(first);
        var right = MeaningKey(second);
        return left.Length > 0 &&
               left.Equals(right, StringComparison.Ordinal);
    }

    public static bool RepeatsDependency(
        string? note,
        IEnumerable<string> dependencyTitles)
    {
        var noteKey = MeaningKey(note);
        if (noteKey.Length == 0)
        {
            return false;
        }

        return dependencyTitles.Any(title =>
        {
            var dependencyKey = MeaningKey(title);
            return dependencyKey.Length > 0 &&
                   noteKey.Contains(
                       dependencyKey,
                       StringComparison.Ordinal);
        });
    }

    public static bool IsTechnicalArtemisNote(string? raw)
    {
        if (IsEmptyDescription(raw))
        {
            return false;
        }

        var text = raw!.Trim();
        var assignmentCount = text.Count(character => character == '=');
        if (assignmentCount == 0)
        {
            return false;
        }

        var hasValueMapping = text
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment =>
            {
                var separator = segment.IndexOf('=');
                if (separator <= 0)
                {
                    return false;
                }

                var left = segment[..separator].Trim();
                var tokenStart = left.LastIndexOfAny([' ', ',', '(']);
                var token = left[(tokenStart + 1)..].Trim();
                return token.Length > 0 &&
                       token.All(character =>
                           char.IsAsciiHexDigit(character));
            });

        return hasValueMapping &&
               (assignmentCount > 1 ||
                text.Contains(';') ||
                text.Contains("0x", StringComparison.OrdinalIgnoreCase) ||
                text.Split('=', 2)[0].Trim().Length >= 4);
    }

    public static string SafetyLabel(PatSafetyAssessment safety)
    {
        return safety.Severity switch
        {
            PatSafetySeverity.High =>
                UiText.GetText("Patch.Safety.High.Label"),
            PatSafetySeverity.Caution =>
                UiText.GetText("Patch.Safety.Caution.Label"),
            _ => string.Empty
        };
    }

    public static string SafetyText(PatSafetyAssessment safety)
    {
        if (!safety.HasRisk)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (safety.CrashOrFreezeRisk)
        {
            parts.Add(UiText.GetText("Patch.Safety.CrashFreeze"));
        }

        if (safety.ProgressionRisk)
        {
            parts.Add(UiText.GetText("Patch.Safety.Progression"));
        }

        if (safety.DisableAndRetry)
        {
            parts.Add(UiText.GetText("Patch.Safety.DisableRetry"));
        }

        if (parts.Count == 0)
        {
            parts.Add(UiText.GetText("Patch.Safety.GenericCaution"));
        }

        return UiText.Fmt(
            "Patch.Safety.Message",
            SafetyLabel(safety),
            string.Join(" ", parts));
    }

    private static string StripArtemisTitleMetadata(
        string title,
        bool hasDependencies)
    {
        title = StripTrailingMarker(
            title,
            '[',
            ']',
            hasDependencies);
        return StripTrailingMarker(
            title,
            '(',
            ')',
            hasDependencies);
    }

    private static string StripTrailingMarker(
        string title,
        char open,
        char close,
        bool hasDependencies)
    {
        if (!title.EndsWith(close))
        {
            return title;
        }

        var markerStart = title.LastIndexOf(open);
        if (markerStart <= 0)
        {
            return title;
        }

        var marker = title[(markerStart + 1)..^1].Trim();
        var isPresentationMetadata =
            marker.Equals(
                "Must Be On",
                StringComparison.OrdinalIgnoreCase) ||
            hasDependencies &&
            (marker.StartsWith(
                 "Requires ",
                 StringComparison.OrdinalIgnoreCase) ||
             marker.StartsWith(
                 "Turn On ",
                 StringComparison.OrdinalIgnoreCase));
        return isPresentationMetadata
            ? title[..markerStart].TrimEnd()
            : title;
    }

    private static string MeaningKey(string? value)
    {
        if (IsEmptyDescription(value))
        {
            return string.Empty;
        }

        return new string(value!
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
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
                string.Empty,
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
