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
    public const string ArabicLang = "ar-SA";
    public const string EnglishLang = "en-US";

    private static readonly UiText CurrentInstance = CreateDefault();
    private IReadOnlyDictionary<string, string> _text;

    private UiText(string currentLang, IReadOnlyDictionary<string, string> text)
    {
        CurrentLang = currentLang;
        _text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static UiText Current => CurrentInstance;

    public static string CurrentLang { get; private set; } = ArabicLang;

    public static bool IsRtl => string.Equals(CurrentLang, ArabicLang, StringComparison.Ordinal);

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        return _text.TryGetValue(key, out var value) ? value : key;
    }

    private static UiText CreateDefault()
    {
        var text = LoadText(ArabicLang, out var effectiveLang);
        return new UiText(effectiveLang, text);
    }

    public static string NormalizeLang(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return ArabicLang;
        }

        var normalized = lang.Trim();
        return normalized switch
        {
            "ar" or ArabicLang => ArabicLang,
            "en" or EnglishLang => EnglishLang,
            _ => ArabicLang
        };
    }

    public static void SetLang(string? lang)
    {
        var normalized = NormalizeLang(lang);
        CurrentInstance._text = LoadText(normalized, out var effectiveLang);
        CurrentLang = effectiveLang;
        ApplyCulture(effectiveLang);
        CurrentInstance.PropertyChanged?.Invoke(CurrentInstance, new PropertyChangedEventArgs("Item[]"));
    }

    public static string T(string key)
    {
        return Current.Get(key);
    }

    public static string F(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }

    public static string RecRiskLabel(RecRisk risk)
    {
        return T($"Risk.Rec.{risk}.Label");
    }

    public static string RecRiskTip(RecRisk risk)
    {
        return T($"Risk.Rec.{risk}.Tip");
    }

    public static string RecStateLabel(RecState state)
    {
        return T($"RecState.{state}");
    }

    public static string PatchRiskLabel(PatRisk risk)
    {
        return T($"Risk.Patch.{risk}.Label");
    }

    public static string PatchRiskTip(PatRisk risk, MatStat matchStatus)
    {
        if (matchStatus == MatStat.Unknown)
        {
            return T("Msg.PatchHashUnknown");
        }

        if (matchStatus != MatStat.Match)
        {
            return PatchMatchTip(matchStatus);
        }

        return T($"Risk.Patch.{risk}.Tip");
    }

    public static string PatchTypeLabel(string patchType)
    {
        if (string.IsNullOrWhiteSpace(patchType))
        {
            return T("PatchType.Unknown");
        }

        var key = $"PatchType.{patchType}";
        return Current._text.TryGetValue(key, out var value) ? value : patchType;
    }

    public static string HashStatusLabel(MatStat status, HashKind hashKind)
    {
        return status switch
        {
            MatStat.Match => T("PatchHash.Exact"),
            MatStat.Unknown => T("PatchHash.Unknown"),
            MatStat.HashBad => T("PatchHash.Mismatch"),
            MatStat.NoMeta => T("PatchHash.NoMetadata"),
            _ => hashKind == HashKind.Unknown ? T("PatchHash.Unavailable") : T("PatchHash.NotMatched")
        };
    }

    public static string PatchMatchLabel(MatStat status)
    {
        return T($"PatchMatch.{status}.Label");
    }

    public static string PatchMatchTip(MatStat status)
    {
        return T($"PatchMatch.{status}.Tip");
    }

    public static string HwStatus(HwInfo info)
    {
        var tier = T($"HwTier.{info.Tier}");
        var cpu = info.CpuNum > 0
            ? F("Hw.CpuThreads", info.CpuNum)
            : T("Hw.CpuUnknown");

        return F("Hw.Status", tier, cpu, info.Display);
    }

    public static string HwTip(HwInfo info)
    {
        return F("Hw.Tip", info.CpuName, info.Display, info.GpuName, info.Tier, info.ProbeMsg);
    }

    private static IReadOnlyDictionary<string, string> LoadText(string lang, out string effectiveLang)
    {
        var normalized = NormalizeLang(lang);
        effectiveLang = normalized;
        var path = ResolveLangPath(normalized);
        if (path is null)
        {
            Trace.TraceWarning($"R3Config UI language file {normalized} not found. Falling back to ar-SA.");
            if (!string.Equals(normalized, ArabicLang, StringComparison.Ordinal))
            {
                return LoadText(ArabicLang, out effectiveLang);
            }

            Trace.TraceWarning("R3Config Arabic UI language file not found. Falling back to empty UI text map.");
            effectiveLang = ArabicLang;
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                         ?? new Dictionary<string, string>(StringComparer.Ordinal);
            return new Dictionary<string, string>(loaded, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning($"R3Config UI language load failed for {normalized}: {ex.Message}");
            if (!string.Equals(normalized, ArabicLang, StringComparison.Ordinal))
            {
                return LoadText(ArabicLang, out effectiveLang);
            }

            effectiveLang = ArabicLang;
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string? ResolveLangPath(string lang)
    {
        var fileName = NormalizeLang(lang) + ".json";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Lang", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Environment.CurrentDirectory, "App", "Lang", fileName),
            Path.Combine(Environment.CurrentDirectory, "Lang", fileName)
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

    private static void ApplyCulture(string lang)
    {
        var culture = CultureInfo.GetCultureInfo(NormalizeLang(lang));
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
