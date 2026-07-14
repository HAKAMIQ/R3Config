using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using R3Config.Svc;
using R3Config.Lang;

namespace R3Config.View.Dlg;

public partial class AppDialog : Window
{
    public AppDialog(
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

        TitleText.Text = title;
        MessageText.Text = message;

        PrimaryButton.Content = isConfirm
            ? UiText.T("Dialog.Yes")
            : UiText.T("Dialog.Ok");

        SecondaryButton.Content = isConfirm
            ? UiText.T("Dialog.No")
            : UiText.T("Dialog.Cancel");

        SecondaryButton.Visibility = isConfirm
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyKind(kind);
        ApplyTextDirection();

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

    private void ApplyTextDirection()
    {
        var layout = UiLaySvc.Current;

        ContentGrid.FlowDirection = layout.TextFlow;
        ButtonPanel.FlowDirection = layout.TextFlow;

        TitleText.FlowDirection = layout.TextFlow;
        TitleText.TextAlignment = layout.TxtAlign;
        TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;

        MessageText.FlowDirection = layout.TextFlow;
        MessageText.TextAlignment = layout.TxtAlign;
        MessageText.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void ApplyKind(DlgKind kind)
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
                IconBadge.Background = warningSurface;
                IconBadge.BorderBrush = warning;
                break;

            case DlgKind.Error:
                IconText.Text = "×";
                IconText.Foreground = danger;
                IconBadge.Background = dangerSurface;
                IconBadge.BorderBrush = danger;
                break;

            case DlgKind.Confirm:
                IconText.Text = "?";
                IconText.Foreground = accent;
                IconBadge.Background = infoSurface;
                IconBadge.BorderBrush = accent;
                break;

            default:
                IconText.Text = "i";
                IconText.Foreground = accent;
                IconBadge.Background = infoSurface;
                IconBadge.BorderBrush = border;
                break;
        }
    }

    private void PrimaryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
