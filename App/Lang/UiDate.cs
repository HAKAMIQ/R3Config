using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Data;

namespace R3Config.Lang;

// Display-only date/time text in the user's Windows Region format. UiText
// switches CurrentCulture to the interface language (ar-SA defaults to the
// Umm al-Qura calendar), so the user's own culture is captured before that
// switch. Technical values, logs and persisted files never use this.
public static class UiDate
{
    private const char Lrm = (char)0x200E;
    private const string FallbackPattern = "yyyy-MM-dd HH:mm";

    private static CultureInfo? _userCulture;

    public static CultureInfo UserCulture =>
        Volatile.Read(ref _userCulture) ?? CaptureUserCulture();

    // UiText calls this before it applies the interface culture. The process
    // culture at that point is the Windows Region culture with user overrides.
    internal static CultureInfo CaptureUserCulture()
    {
        var culture = CultureInfo.ReadOnly(CultureInfo.CurrentCulture);

        return Interlocked.CompareExchange(
            ref _userCulture,
            culture,
            null) ?? culture;
    }

    public static string Text(DateTimeOffset value)
    {
        return Text(
            value,
            UserCulture,
            TimeZoneInfo.Local);
    }

    public static string Text(
        DateTimeOffset value,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(value, zone).DateTime;
        var format = culture.DateTimeFormat;

        try
        {
            return local.ToString(
                format.ShortDatePattern + " " + format.ShortTimePattern,
                culture);
        }
        catch (Exception ex) when (
            ex is ArgumentOutOfRangeException or FormatException)
        {
            // Outside the range of the user's calendar (Umm al-Qura covers
            // 1900-2077 only) or a broken custom Region pattern: show the
            // stamp instead of failing the view or a post-write message.
            return local.ToString(
                FallbackPattern,
                CultureInfo.InvariantCulture);
        }
    }

    // Date only, for values that carry no real time of day (for example the
    // yyyy-MM-dd version of the embedded data), so no "12:00 AM" is shown.
    public static string DateText(DateTimeOffset value)
    {
        return DateText(
            value,
            UserCulture,
            TimeZoneInfo.Local);
    }

    public static string DateText(
        DateTimeOffset value,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(value, zone).DateTime;

        try
        {
            return local.ToString(
                culture.DateTimeFormat.ShortDatePattern,
                culture);
        }
        catch (Exception ex) when (
            ex is ArgumentOutOfRangeException or FormatException)
        {
            return local.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }
    }

    // For dates embedded in localized sentences: kept left-to-right inside
    // Arabic text.
    public static string Inline(DateTimeOffset value)
    {
        return LeftToRight(Text(value));
    }

    public static string DateInline(DateTimeOffset value)
    {
        return LeftToRight(DateText(value));
    }

    // WPF text rendering ignores the LRI/PDI and LRE/PDF controls, so date
    // and time would be ordered right-to-left in an Arabic sentence. LRM on
    // both sides of every space between non-Arabic characters joins them into
    // one left-to-right run; an Arabic AM/PM designator keeps its own side.
    internal static string LeftToRight(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length + 8);
        builder.Append(Lrm);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var joins = character == ' ' &&
                        index > 0 &&
                        index < text.Length - 1 &&
                        !IsRtl(text[index - 1]) &&
                        !IsRtl(text[index + 1]);

            if (joins)
            {
                builder.Append(Lrm).Append(character).Append(Lrm);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.Append(Lrm).ToString();
    }

    private static bool IsRtl(char character)
    {
        return character is
            >= (char)0x0590 and <= (char)0x08FF or
            >= (char)0xFB1D and <= (char)0xFDFF or
            >= (char)0xFE70 and <= (char)0xFEFF;
    }
}

public sealed class UiDateConverter : IValueConverter
{
    // The binding culture is the element language (ar-SA/en-US), not the
    // user's Region format, so it is ignored on purpose.
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is DateTimeOffset date
            ? UiDate.Text(date)
            : string.Empty;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
