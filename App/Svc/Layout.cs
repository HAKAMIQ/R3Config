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

    // Arabic follows the native Windows logical layout (RTL); English stays LTR.
    // Caption buttons and technical values are handled by dedicated resources.
    public FlowDirection WinFlow => TextFlow;

    public FlowDirection ShFlow => TextFlow;

    public FlowDirection TextFlow => IsRtl
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;

    public FlowDirection TechFlow { get; } =
        FlowDirection.LeftToRight;

    public TextAlignment TxtAlign => IsRtl
        ? TextAlignment.Right
        : TextAlignment.Left;

    public TextAlignment SetAlign => TxtAlign;

    public TextAlignment TechAlgn { get; } =
        TextAlignment.Left;

    public HorizontalAlignment HorAlign => IsRtl
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;

    public HorizontalAlignment TrailAlign => IsRtl
        ? HorizontalAlignment.Left
        : HorizontalAlignment.Right;

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
            new GridLength(360, GridUnitType.Pixel),
            new GridLength(1, GridUnitType.Star));
    }
}

public static class UiLaySvc
{
    public static UiLaySt Current =>
        UiLaySt.FromLang(UiText.CurLang);

    public static void Apply()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var layout = Current;

        app.Resources["App.FlowDirection"] =
            layout.ShFlow;

        app.Resources["App.CaptionButtonsFlowDirection"] =
            layout.ShFlow;

        app.Resources["App.DefaultTextAlignment"] =
            layout.TxtAlign;

        app.Resources["Options.SettingsTextAlignment"] =
            layout.SetAlign;

        app.Resources["App.HorizontalAlignment"] =
            layout.HorAlign;

        app.Resources["App.TrailingHorizontalAlignment"] =
            layout.TrailAlign;

        app.Resources["App.After6Margin"] =
            layout.IsRtl
                ? new Thickness(6, 0, 0, 0)
                : new Thickness(0, 0, 6, 0);

        app.Resources["App.After8Margin"] =
            layout.IsRtl
                ? new Thickness(8, 0, 0, 0)
                : new Thickness(0, 0, 8, 0);

        app.Resources["App.After10Margin"] =
            layout.IsRtl
                ? new Thickness(10, 0, 0, 0)
                : new Thickness(0, 0, 10, 0);

        app.Resources["App.After12Margin"] =
            layout.IsRtl
                ? new Thickness(12, 0, 0, 0)
                : new Thickness(0, 0, 12, 0);

        app.Resources["App.Before8Margin"] =
            layout.IsRtl
                ? new Thickness(0, 0, 8, 0)
                : new Thickness(8, 0, 0, 0);

        app.Resources["App.Before10Margin"] =
            layout.IsRtl
                ? new Thickness(0, 0, 10, 0)
                : new Thickness(10, 0, 0, 0);

        app.Resources["App.Indent24Margin"] =
            layout.IsRtl
                ? new Thickness(0, 6, 24, 0)
                : new Thickness(24, 6, 0, 0);

        app.Resources["App.ScrollPadding4"] =
            layout.IsRtl
                ? new Thickness(4, 0, 0, 0)
                : new Thickness(0, 0, 4, 0);

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
