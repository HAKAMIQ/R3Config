using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Tests;

// Display dates follow the Windows Region format captured before UiText
// switches CurrentCulture to the interface language; nothing else moves.
internal static class UiDateT
{
    private const char Lrm = (char)0x200E;
    private const char Sad = (char)0x0635;

    private static readonly DateTimeOffset Stamp =
        new(2026, 10, 23, 9, 5, 0, TimeSpan.Zero);

    public static void Run(string repoRoot)
    {
        // UiText first, as in the app: its startup Apply must not leak into
        // the culture UiDate reports.
        Assert(UiText.CurLang.Length > 0, "UiText did not initialize.");

        UserCultureIsWindowsRegion();
        RegionPatternWinsOverInterfaceCulture();
        OffsetsBecomeLocalTime();
        OutOfCalendarRangeFallsBack();
        InlineDateStaysLeftToRight();
        TechnicalFormattingUnchanged();
        SourcesUseHelper(repoRoot);
    }

    private static void UserCultureIsWindowsRegion()
    {
        var captured = UiDate.UserCulture;
        Assert(captured.IsReadOnly && captured.UseUserOverride,
            "The captured user culture must be read-only and keep Windows user overrides.");

        using var international = Registry.CurrentUser.OpenSubKey(@"Control Panel\International");
        var localeName = international?.GetValue("LocaleName") as string;
        var shortDate = international?.GetValue("sShortDate") as string;
        var shortTime = international?.GetValue("sShortTime") as string;
        Assert(!string.IsNullOrEmpty(localeName),
            "Windows Region LocaleName is unavailable; the captured culture cannot be proven.");
        Assert(string.Equals(captured.Name, localeName, StringComparison.OrdinalIgnoreCase),
            $"Captured culture '{captured.Name}' is not the Windows Region format '{localeName}'.");
        Assert(shortDate is null || captured.DateTimeFormat.ShortDatePattern == shortDate,
            $"Captured ShortDatePattern '{captured.DateTimeFormat.ShortDatePattern}' is not the Windows value '{shortDate}'.");
        Assert(shortTime is null || captured.DateTimeFormat.ShortTimePattern == shortTime,
            $"Captured ShortTimePattern '{captured.DateTimeFormat.ShortTimePattern}' is not the Windows value '{shortTime}'.");

        foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
        {
            WithLang(language, () =>
            {
                Assert(ReferenceEquals(UiDate.UserCulture, captured),
                    $"Switching the interface to {language} replaced the captured user culture.");
                var user = captured.DateTimeFormat;
                var expected = Stamp.ToLocalTime().DateTime.ToString(
                    user.ShortDatePattern + " " + user.ShortTimePattern,
                    captured);
                Assert(UiDate.Text(Stamp) == expected,
                    $"{language}: display date '{UiDate.Text(Stamp)}' is not the Windows Region format '{expected}'.");
                Assert(UiDate.Inline(Stamp) == UiDate.LeftToRight(expected),
                    $"{language}: inline display date must be kept left-to-right.");
                Assert(Equals(new UiDateConverter().Convert(Stamp, typeof(string), null, CultureInfo.CurrentCulture), expected),
                    $"{language}: the XAML converter must use the Windows Region format, not the binding culture.");
            });
        }
    }

    private static void RegionPatternWinsOverInterfaceCulture()
    {
        var british = CultureInfo.ReadOnly(new CultureInfo("en-GB", false));
        var custom = (CultureInfo)CultureInfo.GetCultureInfo(UiText.EnLang).Clone();
        custom.DateTimeFormat.ShortDatePattern = "yyyy.MM.dd";
        custom.DateTimeFormat.ShortTimePattern = "HH:mm";

        foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
        {
            WithLang(language, () =>
            {
                var text = UiDate.Text(Stamp, british, TimeZoneInfo.Utc);
                Assert(text == "23/10/2026 09:05",
                    $"{language}: en-GB Region date was '{text}', expected '23/10/2026 09:05'.");
                Assert(UiDate.Text(Stamp, custom, TimeZoneInfo.Utc) == "2026.10.23 09:05",
                    $"{language}: a customized Region pattern was not followed.");
            });
        }

        WithLang(UiText.ArabLang, () =>
        {
            var interfaceText = Stamp.UtcDateTime.ToString("g", CultureInfo.CurrentCulture);
            Assert(CultureInfo.CurrentCulture.DateTimeFormat.Calendar is UmAlQuraCalendar &&
                   !interfaceText.Contains("2026", StringComparison.Ordinal) &&
                   UiDate.Text(Stamp, british, TimeZoneInfo.Utc).Contains("2026", StringComparison.Ordinal),
                $"The Arabic interface culture must not leak its calendar into Region dates ('{interfaceText}').");
        });
    }

    private static void OffsetsBecomeLocalTime()
    {
        var british = CultureInfo.ReadOnly(new CultureInfo("en-GB", false));
        var plusThree = TimeZoneInfo.CreateCustomTimeZone("R3Test+3", TimeSpan.FromHours(3), "R3Test+3", "R3Test+3");
        var minusFive = new DateTimeOffset(2026, 10, 23, 23, 30, 0, TimeSpan.FromHours(-5));

        var text = UiDate.Text(minusFive, british, plusThree);
        Assert(text == "24/10/2026 07:30",
            $"A -05:00 stamp shown in a +03:00 zone was '{text}', expected '24/10/2026 07:30'.");

        var local = UiDate.Text(minusFive);
        Assert(local == UiDate.Text(minusFive.ToUniversalTime()) &&
               local == UiDate.Text(minusFive.ToOffset(TimeSpan.FromHours(9))) &&
               local == UiDate.Text(minusFive, UiDate.UserCulture, TimeZoneInfo.Local),
            "The same instant stored with different offsets must show the same local time.");
    }

    private static void OutOfCalendarRangeFallsBack()
    {
        var hijri = (CultureInfo)CultureInfo.GetCultureInfo(UiText.ArabLang).Clone();
        hijri.DateTimeFormat.Calendar = new UmAlQuraCalendar();
        var old = new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert(UiDate.Text(old, hijri, TimeZoneInfo.Utc) == "1800-01-01 00:00",
            "A date outside the Region calendar range must fall back instead of failing the view.");

        var broken = (CultureInfo)CultureInfo.GetCultureInfo(UiText.EnLang).Clone();
        broken.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy '";
        Assert(UiDate.Text(Stamp, broken, TimeZoneInfo.Utc) == "2026-10-23 09:05",
            "A malformed Region pattern must fall back instead of failing a post-write message.");
    }

    private static void InlineDateStaysLeftToRight()
    {
        // WPF ignores LRI/PDI and LRE/PDF; LRM must join date and time.
        var hijri = "23/04/1448 02:17 " + Sad;
        Assert(UiDate.LeftToRight(hijri) ==
               Lrm + "23/04/1448" + Lrm + " " + Lrm + "02:17 " + Sad + Lrm,
            "Date and time must join into one left-to-right run while the Arabic designator keeps its own side.");

        var english = "10/23/2026 2:17 PM";
        Assert(UiDate.LeftToRight(english) ==
               Lrm + "10/23/2026" + Lrm + " " + Lrm + "2:17" + Lrm + " " + Lrm + "PM" + Lrm,
            "A Latin Region date must stay one left-to-right run.");

        var inline = UiDate.Inline(Stamp);
        Assert(!inline.Any(character => character is (char)0x2066 or (char)0x2069 or (char)0x202A or (char)0x202C),
            "Inline dates must not rely on bidi isolates or embeddings that WPF does not render.");
    }

    private static void TechnicalFormattingUnchanged()
    {
        foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
        {
            WithLang(language, () =>
            {
                Assert(ReferenceEquals(CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo(language)) &&
                       ReferenceEquals(CultureInfo.CurrentUICulture, CultureInfo.GetCultureInfo(language)) &&
                       ReferenceEquals(CultureInfo.DefaultThreadCurrentCulture, CultureInfo.GetCultureInfo(language)),
                    $"{language}: the interface culture must stay the language culture; only display dates use the Region culture.");

                var message = UiText.Fmt("Msg.RestoreConfirm", UiDate.Inline(Stamp), 1234);
                Assert(message.Contains(UiDate.LeftToRight(UiDate.Text(Stamp)), StringComparison.Ordinal) &&
                       message.Contains(1234.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal),
                    $"{language}: the restore confirmation must embed the isolated Region date and keep number formatting.");

                var manifest = JsonSerializer.Serialize(new BackupManifest(Stamp, []));
                Assert(manifest.Contains("\"CreatedAt\":\"2026-10-23T09:05:00+00:00\"", StringComparison.Ordinal),
                    $"{language}: the backup manifest timestamp must stay ISO 8601 ({manifest}).");
                Assert(60.5.ToString("0.00", CultureInfo.InvariantCulture) == "60.50" &&
                       0x67ED08.ToString("X8", CultureInfo.InvariantCulture) == "0067ED08",
                    $"{language}: invariant technical values changed.");
            });
        }
    }

    private static void SourcesUseHelper(string repoRoot)
    {
        var recovery = File.ReadAllText(Path.Combine(repoRoot, "App", "View", "ARec.xaml"));
        Assert(recovery.Contains("Converter={StaticResource UiDateConverter}", StringComparison.Ordinal) &&
               !recovery.Contains("StringFormat={}{0:yyyy", StringComparison.Ordinal),
            "The backup date column must use UiDateConverter instead of a fixed StringFormat.");

        foreach (var path in Directory.EnumerateFiles(Path.Combine(repoRoot, "App", "Vm"), "*.cs"))
        {
            var source = File.ReadAllText(path);
            Assert(!source.Contains("MadeAt.LocalDateTime", StringComparison.Ordinal) &&
                   !source.Contains("\"yyyy-MM-dd HH:mm\"", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} formats a display date without UiDate.");
        }

        var backupModel = File.ReadAllText(Path.Combine(repoRoot, "Core", "Mdl", "Bkp.cs"));
        Assert(!backupModel.Contains("LocalDateTime", StringComparison.Ordinal),
            "Core backup models must not format dates for display.");
    }

    private static void WithLang(string language, Action action)
    {
        var original = UiText.CurLang;
        try
        {
            UiText.SetLang(language);
            action();
        }
        finally
        {
            UiText.SetLang(original);
        }
    }
}
