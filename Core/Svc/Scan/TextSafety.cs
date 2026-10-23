using System;
using System.Globalization;
using System.Linq;
using System.Text;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// The exact-vs-display split for text that came from outside (log lines, file
// names, titles). Raw text is never modified; Display is a sanitized copy for
// showing; SearchKey is an optional loose key for search boxes ONLY. Nothing
// here can make an inexact identifier exact: identity checks use the raw text.
public sealed record SafeText(string Raw, string Display, bool Altered);

public static class TextSafety
{
    // Zero-width, bidi-control, BOM and other invisible format characters.
    private static bool IsInvisible(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned or UnicodeCategory.PrivateUse
                   or UnicodeCategory.Surrogate
               && rune.Value is not ('\t' or '\n' or '\r');
    }

    public static SafeText ForDisplay(string? raw, int maxLength = 300)
    {
        raw ??= string.Empty;
        var builder = new StringBuilder(Math.Min(raw.Length, maxLength));
        var altered = false;
        foreach (var rune in raw.EnumerateRunes())
        {
            if (rune == Rune.ReplacementChar || IsInvisible(rune))
            {
                altered = true;
                continue;
            }

            if (builder.Length + rune.Utf16SequenceLength > maxLength)
            {
                altered = true;
                break;
            }

            builder.Append(rune.ToString());
        }

        // A run of combining marks is capped so a stacked-mark string cannot
        // overflow its row; the base characters and the first marks stay.
        var display = CapCombining(builder.ToString(), 2, ref altered);
        return new SafeText(raw, display.Normalize(NormalizationForm.FormC), altered || !string.Equals(display, display.Normalize(NormalizationForm.FormC), StringComparison.Ordinal));
    }

    private static string CapCombining(string text, int max, ref bool altered)
    {
        var builder = new StringBuilder(text.Length);
        var run = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var mark = Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark;
            run = mark ? run + 1 : 0;
            if (run > max)
            {
                altered = true;
                continue;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    // For search boxes only: compatibility-folded, marks removed, case-folded.
    public static string SearchKey(string? text) =>
        new string((text ?? string.Empty)
            .Normalize(NormalizationForm.FormKD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.Format or UnicodeCategory.Control))
            .ToArray()).ToLowerInvariant();

    // Identity is exact or absent: the raw text must already be the identifier.
    // Hidden characters, full-width digits, look-alike letters or wrong case make
    // it NOT an identifier; they are never repaired into one.
    public static string? ExactProductCode(string? raw) =>
        raw is not null && GameUpdateSnapshot.ProductCodePattern().IsMatch(raw) ? raw : null;
}

#pragma warning restore IDE0130
