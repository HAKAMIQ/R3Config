using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using R3Config.Vm;

namespace R3Config.Ctrl;

public partial class HCanView : UserControl
{
    private HCanVm? _vm;

    public HCanView()
    {
        InitializeComponent();
        DataContextChanged += DataChanged;
        Unloaded += ViewUnloaded;
        UpdateNavigationState();
    }

    private void DataChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Attach((e.NewValue as MainVm)?.HCanary);
    }

    private void ViewUnloaded(object sender, RoutedEventArgs e)
    {
        Attach(null);
    }

    private void Attach(HCanVm? viewModel)
    {
        if (ReferenceEquals(_vm, viewModel))
        {
            return;
        }

        if (_vm is not null)
        {
            _vm.PropChg -= DraftChanged;
        }

        _vm = viewModel;
        if (_vm is not null)
        {
            _vm.PropChg += DraftChanged;
        }

        UpdateNavigationState();
    }

    private void DraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateNavigationState();
    }

    private void StartDraft(object sender, RoutedEventArgs e)
    {
        SavedDrawer.Visibility = Visibility.Collapsed;
        TargetsPanel.IsExpanded = false;
        AnchorsPanel.IsExpanded = false;
        Steps.SelectedIndex = 0;
        UpdateNavigationState();
    }

    private void ShowSaved(object sender, RoutedEventArgs e)
    {
        SavedDrawer.Visibility = SavedDrawer.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseSaved(object sender, RoutedEventArgs e)
    {
        SavedDrawer.Visibility = Visibility.Collapsed;
    }

    private void ReturnToGames(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private void OpenStoredPatch(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SavedDrawer.Visibility = Visibility.Collapsed;
            Steps.SelectedIndex = 1;
            UpdateNavigationState();
        }));
    }

    private void PreviousStep(object sender, RoutedEventArgs e)
    {
        Steps.SelectedIndex = Math.Max(0, Steps.SelectedIndex - 1);
    }

    private void NextStep(object sender, RoutedEventArgs e)
    {
        if (!CanAdvanceThrough(Steps.SelectedIndex))
        {
            return;
        }

        Steps.SelectedIndex = Math.Min(Steps.Items.Count - 1, Steps.SelectedIndex + 1);
    }

    private void StepChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateNavigationState();
    }

    private bool CanAdvanceThrough(int step)
    {
        return step switch
        {
            0 => _vm?.CanAdvanceTarget == true,
            1 => _vm?.CanAdvanceDetails == true,
            2 => _vm?.CanAdvanceInstructions == true,
            _ => false
        };
    }

    private void UpdateNavigationState()
    {
        if (NextButton is null || DetailsStep is null || InstructionsStep is null || ReviewStep is null)
        {
            return;
        }

        var current = Steps.SelectedIndex;
        var targetReady = CanAdvanceThrough(0);
        var detailsReady = CanAdvanceThrough(1);
        var instructionsReady = CanAdvanceThrough(2);

        DetailsStep.IsEnabled = current >= 1 || targetReady;
        InstructionsStep.IsEnabled = current >= 2 || detailsReady;
        ReviewStep.IsEnabled = current >= 3 || instructionsReady;
        NextButton.IsEnabled = current switch
        {
            0 => targetReady,
            1 => detailsReady,
            2 => instructionsReady,
            _ => false
        };
    }

    private void OpenMore(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
