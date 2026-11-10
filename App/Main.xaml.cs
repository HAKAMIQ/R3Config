using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using R3Config.Svc;
using R3Config.Vm;
using R3Config.View;

namespace R3Config;

public partial class MainWin : Window
{
    private readonly MainVm _vm;

    public MainWin(MainVm? viewModel = null)
    {
        _vm = viewModel ?? new MainVm();
        InitializeComponent();

        DataContext = _vm;

        ThemeToggle.IsChecked = IsDarkTheme();

        _vm.AdvReq += (_, _) => ShowAdv();
        _vm.LangChg += (_, _) => BuildPat();
        _vm.DiagReq += (_, _) => Tabs.SelectedItem = RtmTab;
        Activated += (_, _) =>
            _vm.LoadR3Ui();
        Loaded += (_, _) => _vm.StartRecSync();
        StateChanged += (_, _) => SetCap();
        SetCap();
    }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new MainAutomationPeer(this);

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        // Checked/Unchecked rather than Click, so a UI Automation Toggle
        // (Narrator, Voice Access) switches the theme as well as the switch.
        var isDark = ThemeToggle.IsChecked == true;
        if (isDark == IsDarkTheme())
        {
            return;
        }

        WinTheme.ApplyResources(isDark);
        WinTheme.Apply(this);
    }

    private static bool IsDarkTheme()
    {
        return System.Windows.Application.Current?
                   .TryFindResource("Fluent.Theme.Mode") is string mode &&
               string.Equals(
                   mode,
                   "Dark",
                   StringComparison.OrdinalIgnoreCase);
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
        SetCap();
    }

    private static Ctrl.PatchView MakePat(Ctrl.PatMode mode)
    {
        return new Ctrl.PatchView
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

    protected override void OnClosed(EventArgs e)
    {
        _vm.Dispose();
        base.OnClosed(e);
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

        var actionName = isMaximized
            ? Lang.UiText.GetText("Window.RestoreTip")
            : Lang.UiText.GetText("Window.MaximizeTip");

        MaxBtn.ToolTip = actionName;

        AutomationProperties.SetName(
            MaxBtn,
            actionName);
    }

    private static Grid MaxIcon()
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

        square.SetResourceReference(
            Border.BorderBrushProperty,
            "Text.Primary");

        root.Children.Add(square);

        return root;
    }

    private static Grid RstIcon()
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

        back.SetResourceReference(
            Border.BorderBrushProperty,
            "Text.Primary");

        front.SetResourceReference(
            Border.BorderBrushProperty,
            "Text.Primary");

        root.Children.Add(back);
        root.Children.Add(front);

        return root;
    }
}
