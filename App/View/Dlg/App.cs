using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using R3Config.Svc;
using R3Config.Lang;

namespace R3Config.View.Dlg;

public partial class AppDlg : Window
{
    public AppDlg(
        Window? owner,
        string title,
        string message,
        DlgKind kind,
        bool isConfirm)
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
            WinTheme.Apply(this);

        Owner = owner;
        Title = title;

        TitlTxt.Text = title;
        MsgText.Text = message;

        MainBtn.Content = isConfirm
            ? UiText.GetText("Dialog.Yes")
            : UiText.GetText("Dialog.Ok");

        AltBtn.Content = isConfirm
            ? UiText.GetText("Dialog.No")
            : UiText.GetText("Dialog.Cancel");

        AltBtn.Visibility = isConfirm
            ? Visibility.Visible
            : Visibility.Collapsed;

        SetKind(kind);
        SetFlow();

        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            DialogResult = !isConfirm;
            Close();
        };
    }

    private void SetFlow()
    {
        var layout = UiLaySvc.Current;

        CntGrid.FlowDirection = layout.TextFlow;
        Btns.FlowDirection = layout.TextFlow;

        TitlTxt.FlowDirection = layout.TextFlow;
        TitlTxt.TextAlignment = layout.TxtAlign;
        TitlTxt.HorizontalAlignment = HorizontalAlignment.Stretch;

        MsgText.FlowDirection = layout.TextFlow;
        MsgText.TextAlignment = layout.TxtAlign;
        MsgText.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void SetKind(DlgKind kind)
    {
        var accent = (Brush)FindResource("AccentBrush");
        var warning = (Brush)FindResource("WarningBrush");
        var danger = (Brush)FindResource("DangerBrush");
        var border = (Brush)FindResource("BorderBrushSoft");
        var warningSurface = (Brush)FindResource("ChipWarningBackgroundBrush");
        var dangerSurface = (Brush)FindResource("DangerSurfaceBrush");
        var infoSurface = (Brush)FindResource("ChipInfoBackgroundBrush");

        switch (kind)
        {
            case DlgKind.Warning:
                IconText.Text = "!";
                IconText.Foreground = warning;
                IconBdg.Background = warningSurface;
                IconBdg.BorderBrush = warning;
                break;

            case DlgKind.Error:
                IconText.Text = "×";
                IconText.Foreground = danger;
                IconBdg.Background = dangerSurface;
                IconBdg.BorderBrush = danger;
                break;

            case DlgKind.Confirm:
                IconText.Text = "?";
                IconText.Foreground = accent;
                IconBdg.Background = infoSurface;
                IconBdg.BorderBrush = accent;
                break;

            default:
                IconText.Text = "i";
                IconText.Foreground = accent;
                IconBdg.Background = infoSurface;
                IconBdg.BorderBrush = border;
                break;
        }
    }

    private void MainClk(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void AltClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
