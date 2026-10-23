using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Svc;

namespace R3Config.View.Dlg;

public partial class AppDlg : Window
{
    private readonly bool _isConfirm;

    public AppDlg(
        Window? owner,
        string title,
        string message,
        DlgKind kind,
        bool isConfirm)
    {
        InitializeComponent();

        _isConfirm = isConfirm;

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

        ConfigureAutomation();
        SetKind(kind);
        SetFlow();

        SourceInitialized += OnSourceInitialized;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void ConfigureAutomation()
    {
        AutomationProperties.SetAutomationId(
            this,
            _isConfirm
                ? "Dialog.Confirm"
                : "Dialog.Info");

        AutomationProperties.SetName(
            this,
            Title);

        AutomationProperties.SetAutomationId(
            MainBtn,
            _isConfirm
                ? "Dialog.ConfirmYes"
                : "Dialog.InfoOk");

        AutomationProperties.SetName(
            MainBtn,
            MainBtn.Content?.ToString() ?? string.Empty);

        if (_isConfirm)
        {
            AutomationProperties.SetAutomationId(
                AltBtn,
                "Dialog.ConfirmNo");

            AutomationProperties.SetName(
                AltBtn,
                AltBtn.Content?.ToString() ?? string.Empty);
        }
    }

    private void SetFlow()
    {
        var layout = UiLaySvc.Current;

        CntGrid.FlowDirection = layout.ShFlow;

        Btns.FlowDirection = FlowDirection.LeftToRight;

        TitlTxt.FlowDirection = layout.TextFlow;
        TitlTxt.TextAlignment = layout.TxtAlign;
        TitlTxt.HorizontalAlignment = HorizontalAlignment.Stretch;

        MsgText.FlowDirection = layout.TextFlow;
        MsgText.TextAlignment = layout.TxtAlign;
        MsgText.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void SetKind(DlgKind kind)
    {
        switch (kind)
        {
            case DlgKind.Warning:
                SetIcon(
                    "!",
                    "Status.Warning",
                    "Status.WarningSurface",
                    "Status.Warning");
                break;

            case DlgKind.Error:
                SetIcon(
                    "×",
                    "Status.Danger",
                    "Status.DangerSurface",
                    "Status.Danger");
                break;

            case DlgKind.Confirm:
                SetIcon(
                    "?",
                    "Accent.Default",
                    "Status.InfoSurface",
                    "Accent.Default");
                break;

            default:
                SetIcon(
                    "i",
                    "Accent.Default",
                    "Status.InfoSurface",
                    "Stroke.Default");
                break;
        }
    }

    private void SetIcon(
        string text,
        string foregroundResource,
        string backgroundResource,
        string borderResource)
    {
        IconText.Text = text;

        IconText.Foreground =
            (Brush)FindResource(foregroundResource);

        IconBdg.Background =
            (Brush)FindResource(backgroundResource);

        IconBdg.BorderBrush =
            (Brush)FindResource(borderResource);
    }

    private void OnSourceInitialized(
        object? sender,
        EventArgs e)
    {
        WinTheme.Apply(this);
    }

    private void OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;

        DialogResult = !_isConfirm;
        Close();
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