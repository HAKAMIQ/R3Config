using System;
using System.Windows;
using System.Windows.Input;

namespace R3Config.View;

public partial class AdvDlg : Window
{
    public AdvDlg()
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

    private void AdvancedNavigationChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (HCanaryWorkspace is null ||
            RecoveryWorkspace is null ||
            DiagnosticsWorkspace is null ||
            RecSyncWorkspace is null)
        {
            return;
        }

        var selectedIndex = AdvancedNavigation.SelectedIndex;
        HCanaryWorkspace.Visibility = selectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecoveryWorkspace.Visibility = selectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsWorkspace.Visibility = selectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        RecSyncWorkspace.Visibility = selectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HCanaryWorkspace_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
