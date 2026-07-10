using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using R3Config.Vm;
using R3Config.View;

namespace R3Config;

public partial class MainWindow : Window
{
    private readonly MainVm _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _viewModel;

        _viewModel.RequestAdvancedDialog += (_, _) => ShowAdvancedDialog();
        StateChanged += (_, _) => UpdateCaptionButtons();
        UpdateCaptionButtons();
    }

    private void ShowAdvancedDialog()
    {
        var dialog = new AdvancedDialog
        {
            Owner = this,
            DataContext = _viewModel
        };

        dialog.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        UpdateCaptionButtons();
    }

    private void UpdateCaptionButtons()
    {
        if (MaximizeRestoreButton is null)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;

        MaximizeRestoreButton.Content = isMaximized
            ? CreateRestoreIcon()
            : CreateMaximizeIcon();

        MaximizeRestoreButton.ToolTip = isMaximized
            ? Lang.UiText.T("Window.RestoreTip")
            : Lang.UiText.T("Window.MaximizeTip");
    }

    private FrameworkElement CreateMaximizeIcon()
    {
        var root = new Grid
        {
            Width = 14,
            Height = 14
        };

        var square = new Border
        {
            Width = 10,
            Height = 10,
            BorderThickness = new Thickness(1.4),
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        BindingOperations.SetBinding(
            square,
            Border.BorderBrushProperty,
            new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
            });

        root.Children.Add(square);

        return root;
    }

    private FrameworkElement CreateRestoreIcon()
    {
        var root = new Grid
        {
            Width = 14,
            Height = 14
        };

        var back = new Border
        {
            Width = 8,
            Height = 8,
            BorderThickness = new Thickness(1.3),
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(2, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        var front = new Border
        {
            Width = 8,
            Height = 8,
            BorderThickness = new Thickness(1.3),
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 0, 2, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        BindingOperations.SetBinding(
            back,
            Border.BorderBrushProperty,
            new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
            });

        BindingOperations.SetBinding(
            front,
            Border.BorderBrushProperty,
            new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
            });

        root.Children.Add(back);
        root.Children.Add(front);

        return root;
    }
}
