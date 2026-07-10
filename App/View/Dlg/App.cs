using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AppSvc;
using R3Config.Lang;

namespace R3Config.View.Dlg;

public partial class AppDialog : Window
{
    private readonly bool _isConfirm;

    public AppDialog(Window? owner, string title, string message, UiDialogKind kind, bool isConfirm)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDwmSetWindowAttributeDarkMode(this);
        Owner = owner;
        _isConfirm = isConfirm;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = isConfirm ? UiText.T("Dialog.Yes") : UiText.T("Dialog.Ok");
        SecondaryButton.Content = isConfirm ? UiText.T("Dialog.No") : UiText.T("Dialog.Cancel");
        SecondaryButton.Visibility = isConfirm ? Visibility.Visible : Visibility.Collapsed;

        ApplyKind(kind);
        ApplyTextDirection();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = isConfirm ? false : true;
                Close();
            }
        };
    }

    private void ApplyTextDirection()
    {
        var layout = UiLayoutService.Current;
        ContentGrid.FlowDirection = layout.TextFlow;
        ButtonPanel.FlowDirection = layout.TextFlow;
        TitleText.FlowDirection = layout.TextFlow;
        TitleText.TextAlignment = layout.TextAlignment;
        TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;
        MessageText.FlowDirection = layout.TextFlow;
        MessageText.TextAlignment = layout.TextAlignment;
        MessageText.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void ApplyKind(UiDialogKind kind)
    {
        var accent = (Brush)FindResource("AccentBrush");
        var warning = (Brush)FindResource("WarningBrush");
        var danger = (Brush)FindResource("DangerBrush");
        var border = (Brush)FindResource("BorderBrushSoft");

        switch (kind)
        {
            case UiDialogKind.Warning:
                IconText.Text = "!";
                IconText.Foreground = warning;
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xF2, 0xC9, 0x4C));
                IconBadge.BorderBrush = warning;
                break;
            case UiDialogKind.Error:
                IconText.Text = "×";
                IconText.Foreground = danger;
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x2A, 0xF8, 0x71, 0x71));
                IconBadge.BorderBrush = danger;
                break;
            case UiDialogKind.Confirm:
                IconText.Text = "?";
                IconText.Foreground = accent;
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x2A, 0x5B, 0x8D, 0xEF));
                IconBadge.BorderBrush = accent;
                break;
            default:
                IconText.Text = "i";
                IconText.Foreground = accent;
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x2A, 0x5B, 0x8D, 0xEF));
                IconBadge.BorderBrush = border;
                break;
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
