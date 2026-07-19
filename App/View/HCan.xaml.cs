using System;
using System.Windows;
using System.Windows.Input;

namespace R3Config.View;

public partial class HCanDlg : Window
{
    public HCanDlg()
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
            // DragMove can throw if the mouse button is released first.
        }
    }

    private void CloseWin(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
