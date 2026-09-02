using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

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
        return GetText($"Risk.Rec.{RecRiskName(risk)}.Label");
    }

    public static string RecTip(RecRisk risk)
    {
        return GetText($"Risk.Rec.{RecRiskName(risk)}.Tip");
    }

    public static string RecState(RecState state)
    {
        return GetText($"RecState.{state}");
    }

    public static string PatRisk(PatRisk risk)
    {
        return GetText($"Risk.Patch.{PatRiskName(risk)}.Label");
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

        return GetText($"Risk.Patch.{PatRiskName(risk)}.Tip");
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
        var key = status switch
        {
            MatStat.Match => "PatchMatch.ExactMatch.Label",
            MatStat.Unknown =>
                "PatchMatch.SerialVersionMatchHashUnknown.Label",
            MatStat.VerUnk => "PatchMatch.VersionUnknown.Label",
            MatStat.VerBad => "PatchMatch.VersionMismatch.Label",
            MatStat.HashBad => "PatchMatch.HashMismatch.Label",
            MatStat.SerBad => "PatchMatch.SerialMismatch.Label",
            MatStat.NoMeta => "PatchMatch.NoMetadata.Label",
            _ => "PatchMatch.NoMetadata.Label"
        };

        return GetText(key);
    }

    public static string MatchTip(MatStat status)
    {
        var key = status switch
        {
            MatStat.Match => "PatchMatch.ExactMatch.Tip",
            MatStat.Unknown =>
                "PatchMatch.SerialVersionMatchHashUnknown.Tip",
            MatStat.VerUnk => "PatchMatch.VersionUnknown.Tip",
            MatStat.VerBad => "PatchMatch.VersionMismatch.Tip",
            MatStat.HashBad => "PatchMatch.HashMismatch.Tip",
            MatStat.SerBad => "PatchMatch.SerialMismatch.Tip",
            MatStat.NoMeta => "PatchMatch.NoMetadata.Tip",
            _ => "PatchMatch.NoMetadata.Tip"
        };

        return GetText(key);
    }

    private static string RecRiskName(RecRisk risk)
    {
        return risk switch
        {
            global::R3Config.Mdl.RecRisk.Required => "Required",
            global::R3Config.Mdl.RecRisk.Rec => "Recommended",
            global::R3Config.Mdl.RecRisk.Optional => "Optional",
            global::R3Config.Mdl.RecRisk.Cond => "Conditional",
            global::R3Config.Mdl.RecRisk.PerfCost => "PerformanceCost",
            global::R3Config.Mdl.RecRisk.PatDep => "PatchDependent",
            global::R3Config.Mdl.RecRisk.Dep => "HardwareDependent",
            global::R3Config.Mdl.RecRisk.Timing => "ManualTiming",
            global::R3Config.Mdl.RecRisk.Only => "ExpertOnly",
            global::R3Config.Mdl.RecRisk.Risky => "Risky",
            _ => "Unknown"
        };
    }

    private static string PatRiskName(PatRisk risk)
    {
        return risk switch
        {
            global::R3Config.Mdl.PatRisk.SafeFix => "SafeFix",
            global::R3Config.Mdl.PatRisk.Fix => "VisualFix",
            global::R3Config.Mdl.PatRisk.Perf => "Performance",
            global::R3Config.Mdl.PatRisk.Fps60 => "Fps60",
            global::R3Config.Mdl.PatRisk.Cheat => "Cheat",
            global::R3Config.Mdl.PatRisk.Exp => "Experimental",
            global::R3Config.Mdl.PatRisk.Canary => "Canary",
            _ => "Unknown"
        };
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

        if (!R3LangManifest.TryResolve(
                normalized,
                out var fileName,
                out var expectedSha256))
        {
            Trace.TraceWarning(
                $"R3Config UI language package is not registered: {normalized}");

            return FallBack(normalized, out effectiveLang);
        }

        var path = LangPath(fileName);
        if (path is null)
        {
            Trace.TraceWarning(
                $"R3Config UI language package {fileName} was not found.");

            return FallBack(normalized, out effectiveLang);
        }

        try
        {
            return R3LangReader.Load(
                path,
                normalized,
                expectedSha256);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"R3Config UI language load failed for " +
                $"{normalized}: {ex.Message}");

            return FallBack(normalized, out effectiveLang);
        }
    }

    private static IReadOnlyDictionary<string, string> FallBack(
        string failedLanguage,
        out string effectiveLang)
    {
        if (!string.Equals(
                failedLanguage,
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
            "R3Config Arabic UI language package is unavailable. " +
            "Using an empty UI text map.");

        effectiveLang = ArabLang;

        return new Dictionary<string, string>(
            StringComparer.Ordinal);
    }

    private static string? LangPath(string fileName)
    {
        var candidate = Path.Combine(
            AppContext.BaseDirectory,
            "Lang",
            fileName);

        return File.Exists(candidate)
            ? candidate
            : null;
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
