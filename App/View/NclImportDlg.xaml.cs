using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using R3Config.Vm;

namespace R3Config.View;

public partial class NclImportDlg : Window
{
    private NclImportVm? _viewModel;

    public NclImportDlg()
    {
        InitializeComponent();
    }

    public NclImportDlg(
        Window? owner,
        NclImportVm viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;

        if (owner is not null)
        {
            Owner = owner;
        }

        viewModel.CloseReq += OnCloseRequested;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel?.IsBusy == true)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel is not null)
        {
            _viewModel.CloseReq -= OnCloseRequested;
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    private void DragWin(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void CloseWin(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.IsBusy == true)
        {
            return;
        }

        DialogResult = false;
        Close();
    }

    private void OnCloseRequested(
        object? sender,
        bool applied)
    {
        DialogResult = applied;
    }
}
