using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using R3Config.Mdl;

namespace R3Config.Lang;

public sealed class UiText : INotifyPropertyChanged
{
    public const string ArabLang = "ar-SA";
    public const string EnLang = "en-US";

    public static string CurLang { get; private set; } = ArabLang;

    private static readonly UiText CurInst = Build();

    private IReadOnlyDictionary<string, string> _text;

    private UiText(
        string currentLang,
        IReadOnlyDictionary<string, string> text)
    {
        CurLang = currentLang;
        _text = text;
    }

    public event PropertyChangedEventHandler? PropChg;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropChg += value;
        remove => PropChg -= value;
    }

    public static UiText Current => CurInst;

    public static bool IsRtl =>
        string.Equals(
            CurLang,
            ArabLang,
            StringComparison.Ordinal);

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return _text.TryGetValue(key, out var value)
            ? value
            : key;
    }

    public static string NormLang(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return ArabLang;
        }

        return lang.Trim() switch
        {
            "ar" or ArabLang => ArabLang,
            "en" or EnLang => EnLang,
            _ => ArabLang
        };
    }

    public static void SetLang(string? lang)
    {
        var normalized = NormLang(lang);
        var text = LoadText(normalized, out var effectiveLang);

        CurInst._text = text;
        CurLang = effectiveLang;

        Apply(effectiveLang);

        CurInst.PropChg?.Invoke(
            CurInst,
            new PropertyChangedEventArgs("Item[]"));
    }

    public static string GetText(string key)
    {
        return Current.Get(key);
    }

    public static string Fmt(
        string key,
        params object?[] args)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            GetText(key),
            args);
    }

    public static string RecRisk(RecRisk risk)
    {
        return GetText($"Risk.Rec.{risk}.Label");
    }

    public static string RecTip(RecRisk risk)
    {
        return GetText($"Risk.Rec.{risk}.Tip");
    }

    public static string RecState(RecState state)
    {
        return GetText($"RecState.{state}");
    }

    public static string PatRisk(PatRisk risk)
    {
        return GetText($"Risk.Patch.{risk}.Label");
    }

    public static string PatTip(
        PatRisk risk,
        MatStat matchStatus)
    {
        if (matchStatus == MatStat.Unknown)
        {
            return GetText("Msg.PatchHashUnknown");
        }

        if (matchStatus != MatStat.Match)
        {
            return MatchTip(matchStatus);
        }

        return GetText($"Risk.Patch.{risk}.Tip");
    }

    public static string PatType(string patchType)
    {
        if (string.IsNullOrWhiteSpace(patchType))
        {
            return GetText("PatchType.Unknown");
        }

        var key = $"PatchType.{patchType}";

        return Current._text.TryGetValue(key, out var value)
            ? value
            : patchType;
    }

    public static string HashStat(
        MatStat status,
        HashKind hashKind)
    {
        return status switch
        {
            MatStat.Match =>
                GetText("PatchHash.Exact"),

            MatStat.Unknown =>
                GetText("PatchHash.Unknown"),

            MatStat.HashBad =>
                GetText("PatchHash.Mismatch"),

            MatStat.NoMeta =>
                GetText("PatchHash.NoMetadata"),

            _ when hashKind == HashKind.Unknown =>
                GetText("PatchHash.Unavailable"),

            _ =>
                GetText("PatchHash.NotMatched")
        };
    }

    public static string PatMatch(MatStat status)
    {
        return GetText($"PatchMatch.{status}.Label");
    }

    public static string MatchTip(MatStat status)
    {
        return GetText($"PatchMatch.{status}.Tip");
    }

    public static string HwStatus(HwInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var tier = GetText($"HwTier.{info.Tier}");

        var cpu = info.CpuNum > 0
            ? Fmt("Hw.CpuThreads", info.CpuNum)
            : GetText("Hw.CpuUnknown");

        return Fmt(
            "Hw.Status",
            tier,
            cpu,
            info.Display);
    }

    public static string HwTip(HwInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return Fmt(
            "Hw.Tip",
            info.CpuName,
            info.Display,
            info.GpuName,
            info.Tier,
            info.ProbeMsg);
    }

    private static UiText Build()
    {
        var text = LoadText(
            ArabLang,
            out var effectiveLang);

        Apply(effectiveLang);

        return new UiText(
            effectiveLang,
            text);
    }

    private static IReadOnlyDictionary<string, string> LoadText(
        string lang,
        out string effectiveLang)
    {
        var normalized = NormLang(lang);

        effectiveLang = normalized;

        var path = LangPath(normalized);

        if (path is null)
        {
            Trace.TraceWarning(
                $"R3Config UI language file {normalized} was not found.");

            if (!string.Equals(
                    normalized,
                    ArabLang,
                    StringComparison.Ordinal))
            {
                Trace.TraceWarning(
                    "Falling back to ar-SA.");

                return LoadText(
                    ArabLang,
                    out effectiveLang);
            }

            Trace.TraceWarning(
                "R3Config Arabic UI language file was not found. " +
                "Using an empty UI text map.");

            effectiveLang = ArabLang;

            return new Dictionary<string, string>(
                StringComparer.Ordinal);
        }

        try
        {
            using var stream = File.OpenRead(path);

            var loaded =
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    stream)
                ?? new Dictionary<string, string>();

            return new Dictionary<string, string>(
                loaded,
                StringComparer.Ordinal);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            Trace.TraceWarning(
                $"R3Config UI language load failed for " +
                $"{normalized}: {ex.Message}");

            if (!string.Equals(
                    normalized,
                    ArabLang,
                    StringComparison.Ordinal))
            {
                Trace.TraceWarning(
                    "Falling back to ar-SA.");

                return LoadText(
                    ArabLang,
                    out effectiveLang);
            }

            effectiveLang = ArabLang;

            return new Dictionary<string, string>(
                StringComparer.Ordinal);
        }
    }

    private static string? LangPath(string lang)
    {
        var fileName =
            NormLang(lang) + ".json";

        var candidates = new[]
        {
            Path.Combine(
                AppContext.BaseDirectory,
                "Lang",
                fileName),

            Path.Combine(
                AppContext.BaseDirectory,
                fileName),

            Path.Combine(
                Environment.CurrentDirectory,
                "App",
                "Lang",
                fileName),

            Path.Combine(
                Environment.CurrentDirectory,
                "Lang",
                fileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void Apply(string lang)
    {
        var culture =
            CultureInfo.GetCultureInfo(
                NormLang(lang));

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}