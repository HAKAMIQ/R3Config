using System;
using System.Windows;
using System.Windows.Input;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.View;

public partial class CanDlg : Window
{
    public CanDlg(Window? owner, CanImpVm viewModel)
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
            WinTheme.Apply(this);

        Owner = owner;
        DataContext = viewModel;
        Activated += (_, _) => viewModel.LoadMut();

        viewModel.CloseReq += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
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
            // DragMove can throw if the mouse button is released before WPF starts the drag.
        }
    }

    private void CloseWin(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
