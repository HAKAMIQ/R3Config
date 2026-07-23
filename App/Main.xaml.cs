using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using R3Config.Vm;
using R3Config.View;

namespace R3Config;

public partial class MainWin : Window
{
    private readonly MainVm _vm = new();

    public MainWin()
    {
        InitializeComponent();

        DataContext = _vm;

        _vm.AdvReq += (_, _) => ShowAdv();
        _vm.LangChg += (_, _) => BuildPat();
        Activated += (_, _) =>
            _vm.LoadR3Ui();
        StateChanged += (_, _) => SetCap();
        SetCap();
    }

    private void ShowAdv()
    {
        var dialog = new AdvDlg
        {
            Owner = this,
            DataContext = _vm
        };

        dialog.ShowDialog();
    }

    private void BuildPat()
    {
        PatTab.Content = MakePat(Ctrl.PatMode.Patches);
        CanTab.Content = MakePat(Ctrl.PatMode.Canary);
        ChtTab.Content = MakePat(Ctrl.PatMode.Cheat);
    }

    private static Ctrl.PatView MakePat(Ctrl.PatMode mode)
    {
        return new Ctrl.PatView
        {
            Mode = mode,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
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
            TogState();
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

    private void MinWin(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxWin(object sender, RoutedEventArgs e)
    {
        TogState();
    }

    private void CloseWin(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TogState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        SetCap();
    }

    private void SetCap()
    {
        if (MaxBtn is null)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;

        MaxBtn.Content = isMaximized
            ? RstIcon()
            : MaxIcon();

        MaxBtn.ToolTip = isMaximized
            ? Lang.UiText.GetText("Window.RestoreTip")
            : Lang.UiText.GetText("Window.MaximizeTip");
    }

    private FrameworkElement MaxIcon()
    {
        var root = new Grid
        {
            Width = 14,
            Height = 14,
            FlowDirection = FlowDirection.LeftToRight
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

    private FrameworkElement RstIcon()
    {
        var root = new Grid
        {
            Width = 14,
            Height = 14,
            FlowDirection = FlowDirection.LeftToRight
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
