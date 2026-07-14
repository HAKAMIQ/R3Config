using System;
using System.Windows;
using System.Windows.Markup;
using R3Config.Lang;

namespace R3Config.Svc;

public sealed class UiLaySt
{
    private UiLaySt(
        string language,
        bool isRtl,
        XmlLanguage xmlLanguage,
        GridLength gamesPanelWidth,
        GridLength detailsPanelWidth)
    {
        Language = language;
        IsRtl = isRtl;
        XmlLang = xmlLanguage;
        GameWide = gamesPanelWidth;
        DetWide = detailsPanelWidth;
    }

    public string Language { get; }

    public bool IsRtl { get; }

    public XmlLanguage XmlLang { get; }

    public FlowDirection WinFlow => TextFlow;

    public FlowDirection ShFlow => TextFlow;

    public FlowDirection TextFlow =>
        IsRtl
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public FlowDirection TechFlow { get; } =
        FlowDirection.LeftToRight;

    public TextAlignment TxtAlign =>
        IsRtl
            ? TextAlignment.Right
            : TextAlignment.Left;

    public TextAlignment SetAlign { get; } =
        TextAlignment.Left;

    public TextAlignment TechAlgn { get; } =
        TextAlignment.Left;

    public HorizontalAlignment HorAlign =>
        IsRtl
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

    public GridLength GameWide { get; }

    public GridLength DetWide { get; }

    public int GameCol { get; } = 0;

    public int DetCol { get; } = 1;

    public GridLength LeftWide => GameWide;

    public GridLength RghtWide => DetWide;

    public FlowDirection GameFlow(
        bool hasSelectedGame)
    {
        return hasSelectedGame
            ? TechFlow
            : TextFlow;
    }

    public TextAlignment GameAlgn(
        bool hasSelectedGame)
    {
        return hasSelectedGame
            ? TechAlgn
            : TxtAlign;
    }

    public static UiLaySt FromLang(string? language)
    {
        var normalized = UiText.NormLang(language);

        return new UiLaySt(
            normalized,
            string.Equals(
                normalized,
                UiText.ArabLang,
                StringComparison.Ordinal),
            XmlLanguage.GetLanguage(normalized),
            new GridLength(304, GridUnitType.Pixel),
            new GridLength(1, GridUnitType.Star));
    }
}

public static class UiLaySvc
{
    public static UiLaySt Current =>
        UiLaySt.FromLang(UiText.CurLang);

    public static void Apply()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var layout = Current;

        app.Resources["App.FlowDirection"] =
            layout.TextFlow;

        app.Resources["App.CaptionButtonsFlowDirection"] =
            layout.IsRtl
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

        app.Resources["App.DefaultTextAlignment"] =
            layout.TxtAlign;

        app.Resources["Options.SettingsTextAlignment"] =
            layout.SetAlign;

        app.Resources["App.HorizontalAlignment"] =
            layout.HorAlign;

        app.Resources["App.XmlLanguage"] =
            layout.XmlLang;

        app.Resources["TechnicalText.FlowDirection"] =
            layout.TechFlow;

        app.Resources["TechnicalText.TextAlignment"] =
            layout.TechAlgn;

        app.Resources["TechnicalText.HorizontalAlignment"] =
            HorizontalAlignment.Left;

        app.Resources["TechnicalText.Language"] =
            XmlLanguage.GetLanguage(UiText.EnLang);

        app.Resources["App.ShellFlowDirection"] =
            layout.ShFlow;

        app.Resources["App.TextFlowDirection"] =
            layout.TextFlow;

        app.Resources["App.TechnicalFlowDirection"] =
            layout.TechFlow;

        app.Resources["App.TextAlignment"] =
            layout.TxtAlign;

        app.Resources["App.TechnicalTextAlignment"] =
            layout.TechAlgn;
    }
}