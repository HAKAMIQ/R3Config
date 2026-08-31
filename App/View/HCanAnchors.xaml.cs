using System;
using System.Windows;
using System.Windows.Input;

namespace R3Config.View;

public partial class HCanAnchorsDlg : Window
{
    public HCanAnchorsDlg()
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

    private void DataGrid_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
    }
}