using System;
using System.Collections.Generic;
using R3Config.Lang;

namespace R3Config.Svc;

public enum NoteQual
{
    Original,
    Exact,
    Fragment,
    Fallback
}

public sealed record NoteRes(
    string Body,
    string OrigBody,
    bool HasOrig,
    NoteQual Quality);

public static class NoteSvc
{
    private static readonly Dictionary<string, string> ArByEn =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Prevents random freezing/crashing while playing the game"] =
                "يمنع التجمّد أو الانهيار العشوائي أثناء اللعب.",
            ["Fixes random crashes"] =
                "يصلح الانهيارات العشوائية.",
            ["Fixes graphical issues"] =
                "يصلح مشاكل رسومية.",
            ["Fixes flickering textures"] =
                "يصلح وميض الخامات.",
            ["Fixes audio crackling"] =
                "يصلح تقطّع الصوت.",
            ["Can improve performance"] =
                "قد يحسّن الأداء.",
            ["Improves performance"] =
                "يحسّن الأداء.",
            ["Improves stability"] =
                "يحسّن الاستقرار.",
            ["May cause visual bugs"] =
                "قد يسبب مشاكل رسومية.",
            ["Compromises visuals for extra performance"] =
                "يقلل جودة الرسوم مقابل أداء أعلى.",
            ["Unlocks the FPS"] =
                "يفتح حد الإطارات.",
            ["Unlocks FPS"] =
                "يفتح حد الإطارات.",
            ["Unlocks the frame rate"] =
                "يفتح حد الإطارات.",
            ["Requires Stretch To Display Area checked in the GPU settings"] =
                "يتطلب تفعيل Stretch To Display Area من إعدادات GPU.",
            ["Requires Write Color Buffers"] =
                "يتطلب تفعيل Write Color Buffers من إعدادات GPU.",
            ["Requires Write Depth Buffers"] =
                "يتطلب تفعيل Write Depth Buffers من إعدادات GPU.",
            ["Does not provide any performance benefit"] =
                "لا يقدم فائدة في الأداء.",
            ["HUD toggle with Select in-game"] =
                "إخفاء أو إظهار واجهة HUD بزر Select داخل اللعبة."
        };

    public static NoteRes Trans(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new NoteRes(
                UiText.T("Ui.EmptyValue"),
                string.Empty,
                false,
                NoteQual.Original);
        }

        var original = raw.Trim();

        if (!UiText.IsRtl || HasArab(original))
        {
            return new NoteRes(
                original,
                string.Empty,
                false,
                NoteQual.Original);
        }

        var normalized = Norm(original);
        var lookupText = NormKey(normalized);

        if (ArByEn.TryGetValue(
                lookupText,
                out var exact))
        {
            return new NoteRes(
                exact,
                string.Empty,
                false,
                NoteQual.Exact);
        }

        var fragment = TryArab(lookupText);
        if (!string.IsNullOrWhiteSpace(fragment))
        {
            return new NoteRes(
                fragment,
                original,
                true,
                NoteQual.Fragment);
        }

        return new NoteRes(
            UiText.T("Patch.Note.NoLocalTranslation"),
            original,
            true,
            NoteQual.Fallback);
    }

    private static string? TryArab(
        string normalized)
    {
        List<string> topics = [];

        if (HasAny(
                normalized,
                "crash",
                "crashes",
                "crashing"))
        {
            topics.Add("انهيار اللعبة");
        }

        if (HasAny(
                normalized,
                "freeze",
                "freezes",
                "freezing"))
        {
            topics.Add("تجمّد اللعبة أثناء اللعب");
        }

        if (HasAny(
                normalized,
                "performance"))
        {
            topics.Add("الأداء");
        }

        if (HasAny(
                normalized,
                "stability"))
        {
            topics.Add("الاستقرار");
        }

        if (HasAny(
                normalized,
                "visual bug",
                "visual bugs",
                "graphics",
                "graphical",
                "flickering textures",
                "flicker",
                "textures"))
        {
            topics.Add("مشاكل رسومية أو وميض في الخامات");
        }

        if (HasAny(
                normalized,
                "fps",
                "frame rate",
                "framerate"))
        {
            topics.Add("حد الإطارات");
        }

        if (HasAny(
                normalized,
                "hud",
                "heads-up display"))
        {
            topics.Add("إظهار أو إخفاء واجهة HUD داخل اللعبة");
        }

        if (HasAny(
                normalized,
                "select in-game",
                "select button"))
        {
            topics.Add("زر Select داخل اللعبة");
        }

        if (HasAny(
                normalized,
                "vblank"))
        {
            topics.Add("تردد VBlank");
        }

        if (HasAny(
                normalized,
                "resolution"))
        {
            topics.Add("الدقة");
        }

        if (HasAny(
                normalized,
                "shadows"))
        {
            topics.Add("الظلال");
        }

        if (HasAny(
                normalized,
                "fog"))
        {
            topics.Add("الضباب");
        }

        if (HasAny(
                normalized,
                "cutscene",
                "cutscenes"))
        {
            topics.Add("المقاطع السينمائية");
        }

        if (HasAny(
                normalized,
                "audio crackling",
                "crackling",
                "audio",
                "sound"))
        {
            topics.Add("الصوت");
        }

        if (HasAny(
                normalized,
                "shader",
                "shaders"))
        {
            topics.Add("الشيدرز");
        }

        if (HasAny(
                normalized,
                "workaround"))
        {
            topics.Add("حل مؤقت");
        }

        if (HasAny(
                normalized,
                "experimental"))
        {
            topics.Add("سلوك تجريبي");
        }

        if (topics.Count == 0)
        {
            return null;
        }

        return "تتعلق هذه الملاحظة بـ: " +
               string.Join("، ", topics) +
               ".";
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

    private static bool HasArab(string value)
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