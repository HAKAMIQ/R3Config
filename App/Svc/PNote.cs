using System;
using System.Collections.Generic;
using R3Config.Lang;

namespace R3Config.Svc;

public enum PatchNoteTranslationQuality
{
    Original,
    Exact,
    Fragment,
    Fallback
}

public sealed record PatchNoteTranslationResult(
    string Body,
    string OriginalBody,
    bool HasOriginalBody,
    PatchNoteTranslationQuality Quality);

public static class PatchNoteTranslationService
{
    private static readonly Dictionary<string, string> ExactArabicByEnglish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prevents random freezing/crashing while playing the game"] = "يمنع التجمّد أو الانهيار العشوائي أثناء اللعب.",
        ["Fixes random crashes"] = "يصلح الانهيارات العشوائية.",
        ["Fixes graphical issues"] = "يصلح مشاكل رسومية.",
        ["Fixes flickering textures"] = "يصلح وميض الخامات.",
        ["Fixes audio crackling"] = "يصلح تقطّع الصوت.",
        ["Can improve performance"] = "قد يحسّن الأداء.",
        ["Improves performance"] = "يحسّن الأداء.",
        ["Improves stability"] = "يحسّن الاستقرار.",
        ["May cause visual bugs"] = "قد يسبب مشاكل رسومية.",
        ["Compromises visuals for extra performance"] = "يقلل جودة الرسوم مقابل أداء أعلى.",
        ["Unlocks the FPS"] = "يفتح حد الإطارات.",
        ["Unlocks FPS"] = "يفتح حد الإطارات.",
        ["Unlocks the frame rate"] = "يفتح حد الإطارات.",
        ["Requires Stretch To Display Area checked in the GPU settings"] = "يتطلب تفعيل Stretch To Display Area من إعدادات GPU.",
        ["Requires Write Color Buffers"] = "يتطلب تفعيل Write Color Buffers من إعدادات GPU.",
        ["Requires Write Depth Buffers"] = "يتطلب تفعيل Write Depth Buffers من إعدادات GPU.",
        ["Does not provide any performance benefit"] = "لا يقدم فائدة في الأداء.",
        ["HUD toggle with Select in-game"] = "إخفاء أو إظهار واجهة HUD بزر Select داخل اللعبة."
    };

    public static PatchNoteTranslationResult Translate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PatchNoteTranslationResult(
                UiText.T("Ui.EmptyValue"),
                string.Empty,
                false,
                PatchNoteTranslationQuality.Original);
        }

        var original = raw.Trim();
        if (!UiText.IsRtl || ContainsArabic(original))
        {
            return new PatchNoteTranslationResult(
                original,
                string.Empty,
                false,
                PatchNoteTranslationQuality.Original);
        }

        var normalized = Normalize(original);
        var lookupText = NormalizeForLookup(normalized);

        if (ExactArabicByEnglish.TryGetValue(lookupText, out var exact))
        {
            return new PatchNoteTranslationResult(
                exact,
                string.Empty,
                false,
                PatchNoteTranslationQuality.Exact);
        }

        var fragment = TryBuildArabicFragment(lookupText);
        if (!string.IsNullOrWhiteSpace(fragment))
        {
            return new PatchNoteTranslationResult(
                fragment,
                original,
                true,
                PatchNoteTranslationQuality.Fragment);
        }

        return new PatchNoteTranslationResult(
            UiText.T("Patch.Note.NoLocalTranslation"),
            original,
            true,
            PatchNoteTranslationQuality.Fallback);
    }

    private static string? TryBuildArabicFragment(string normalized)
    {
        var topics = new List<string>();

        if (ContainsAny(normalized, "crash", "crashes", "crashing"))
        {
            topics.Add("انهيار اللعبة");
        }

        if (ContainsAny(normalized, "freeze", "freezes", "freezing"))
        {
            topics.Add("تجمّد اللعبة أثناء اللعب");
        }

        if (ContainsAny(normalized, "performance"))
        {
            topics.Add("الأداء");
        }

        if (ContainsAny(normalized, "stability"))
        {
            topics.Add("الاستقرار");
        }

        if (ContainsAny(normalized, "visual bug", "visual bugs", "graphics", "graphical", "flickering textures", "flicker", "textures"))
        {
            topics.Add("مشاكل رسومية أو وميض في الخامات");
        }

        if (ContainsAny(normalized, "fps", "frame rate", "framerate"))
        {
            topics.Add("حد الإطارات");
        }

        if (ContainsAny(normalized, "hud", "heads-up display"))
        {
            topics.Add("إظهار أو إخفاء واجهة HUD داخل اللعبة");
        }

        if (ContainsAny(normalized, "select in-game", "select button"))
        {
            topics.Add("زر Select داخل اللعبة");
        }

        if (ContainsAny(normalized, "vblank"))
        {
            topics.Add("تردد VBlank");
        }

        if (ContainsAny(normalized, "resolution"))
        {
            topics.Add("الدقة");
        }

        if (ContainsAny(normalized, "shadows"))
        {
            topics.Add("الظلال");
        }

        if (ContainsAny(normalized, "fog"))
        {
            topics.Add("الضباب");
        }

        if (ContainsAny(normalized, "cutscene", "cutscenes"))
        {
            topics.Add("المقاطع السينمائية");
        }

        if (ContainsAny(normalized, "audio crackling", "crackling", "audio", "sound"))
        {
            topics.Add("الصوت");
        }

        if (ContainsAny(normalized, "shader", "shaders"))
        {
            topics.Add("الشيدرز");
        }

        if (ContainsAny(normalized, "workaround"))
        {
            topics.Add("حل مؤقت");
        }

        if (ContainsAny(normalized, "experimental"))
        {
            topics.Add("سلوك تجريبي");
        }

        if (topics.Count == 0)
        {
            return null;
        }

        return "تتعلق هذه الملاحظة بـ: " + string.Join("، ", topics) + ".";
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static string NormalizeForLookup(string value)
    {
        var compact = string.Join(
            " ",
            value.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        return compact.Trim().TrimEnd('.', '。');
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsArabic(string value)
    {
        foreach (var character in value)
        {
            if ((character >= '\u0600' && character <= '\u06FF') ||
                (character >= '\u0750' && character <= '\u077F') ||
                (character >= '\u08A0' && character <= '\u08FF') ||
                (character >= '\uFB50' && character <= '\uFDFF') ||
                (character >= '\uFE70' && character <= '\uFEFF'))
            {
                return true;
            }
        }

        return false;
    }
}