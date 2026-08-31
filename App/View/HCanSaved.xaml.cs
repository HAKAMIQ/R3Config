using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace R3Config.View;

public partial class HCanSavedDlg : Window
{
    public HCanSavedDlg()
    {
        InitializeComponent();
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
        Close();
    }

    private void OpenSelected(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => DialogResult = true));
    }
}