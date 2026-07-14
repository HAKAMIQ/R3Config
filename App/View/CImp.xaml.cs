using System;
using System.Windows;
using System.Windows.Input;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.View;

public partial class CanaryPatchImportDialog : Window
{
    public CanaryPatchImportDialog(Window? owner, CanaryPatchImportVm viewModel)
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
            WindowThemeService.ApplyDwmSetWindowAttributeDarkMode(this);

        Owner = owner;
        DataContext = viewModel;
        Activated += (_, _) => viewModel.RefreshMutationState();

        viewModel.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
