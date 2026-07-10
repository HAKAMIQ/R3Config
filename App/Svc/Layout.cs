using System.Windows;
using System.Windows.Markup;
using R3Config.Lang;

namespace AppSvc;

public sealed class UiLayoutState
{
    private UiLayoutState(
        string language,
        bool isRtl,
        XmlLanguage xmlLanguage,
        GridLength gamesPanelWidth,
        GridLength detailsPanelWidth)
    {
        Language = language;
        IsRtl = isRtl;
        XmlLanguage = xmlLanguage;
        GamesPanelWidth = gamesPanelWidth;
        DetailsPanelWidth = detailsPanelWidth;
    }

    public string Language { get; }

    public bool IsRtl { get; }

    public XmlLanguage XmlLanguage { get; }

    public FlowDirection WindowFlow => TextFlow;

    public FlowDirection ShellFlow => TextFlow;

    public FlowDirection TextFlow => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public FlowDirection TechnicalFlow => FlowDirection.LeftToRight;

    public TextAlignment TextAlignment => IsRtl ? TextAlignment.Right : TextAlignment.Left;

    public TextAlignment SettingsTextAlignment => TextAlignment.Left;

    public TextAlignment TechnicalTextAlignment => TextAlignment.Left;

    public HorizontalAlignment HorizontalAlignment => IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public GridLength GamesPanelWidth { get; }

    public GridLength DetailsPanelWidth { get; }

    public int GamesPanelColumn => 0;

    public int DetailsPanelColumn => 1;

    public GridLength LeftShellColumnWidth => GamesPanelWidth;

    public GridLength RightShellColumnWidth => DetailsPanelWidth;

    public FlowDirection SelectedGameTitleFlow(bool hasSelectedGame)
    {
        return hasSelectedGame ? TechnicalFlow : TextFlow;
    }

    public TextAlignment SelectedGameTitleAlignment(bool hasSelectedGame)
    {
        return hasSelectedGame ? TechnicalTextAlignment : TextAlignment;
    }

    public static UiLayoutState FromLanguage(string? language)
    {
        var normalized = UiText.NormalizeLang(language);
        return new UiLayoutState(
            normalized,
            normalized == UiText.ArabicLang,
            XmlLanguage.GetLanguage(normalized),
            new GridLength(304, GridUnitType.Pixel),
            new GridLength(1, GridUnitType.Star));
    }
}

public static class UiLayoutService
{
    public static UiLayoutState Current => UiLayoutState.FromLanguage(UiText.CurrentLang);

    public static void ApplyApplicationResources()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var layout = Current;
        app.Resources["App.FlowDirection"] = layout.TextFlow;
        app.Resources["App.CaptionButtonsFlowDirection"] = layout.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        app.Resources["App.DefaultTextAlignment"] = layout.TextAlignment;
        app.Resources["Options.SettingsTextAlignment"] = layout.SettingsTextAlignment;
        app.Resources["App.HorizontalAlignment"] = layout.HorizontalAlignment;
        app.Resources["App.XmlLanguage"] = layout.XmlLanguage;

        app.Resources["TechnicalText.FlowDirection"] = layout.TechnicalFlow;
        app.Resources["TechnicalText.TextAlignment"] = layout.TechnicalTextAlignment;
        app.Resources["TechnicalText.HorizontalAlignment"] = HorizontalAlignment.Left;
        app.Resources["TechnicalText.Language"] = XmlLanguage.GetLanguage(UiText.EnglishLang);

        app.Resources["App.ShellFlowDirection"] = layout.ShellFlow;
        app.Resources["App.TextFlowDirection"] = layout.TextFlow;
        app.Resources["App.TechnicalFlowDirection"] = layout.TechnicalFlow;
        app.Resources["App.TextAlignment"] = layout.TextAlignment;
        app.Resources["App.TechnicalTextAlignment"] = layout.TechnicalTextAlignment;
    }
}
