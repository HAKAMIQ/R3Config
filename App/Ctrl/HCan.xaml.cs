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

        Loaded += ViewLoaded;
        Unloaded += ViewUnloaded;
        DataContextChanged += DataChanged;

        UpdateNavigationState();
    }

    private void ViewLoaded(object sender, RoutedEventArgs e)
    {
        AttachFromDataContext();
    }

    private void DataChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Attach((e.NewValue as MainVm)?.HCanary);
    }

    private void ViewUnloaded(object sender, RoutedEventArgs e)
    {
        Attach(null);
    }

    private void AttachFromDataContext()
    {
        Attach((DataContext as MainVm)?.HCanary);
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

    private void DraftChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        UpdateNavigationState();
    }

    private void EnterWorkflow()
    {
        SavedDrawer.Visibility = Visibility.Collapsed;
        WorkflowActions.Visibility = Visibility.Collapsed;
        Steps.Visibility = Visibility.Visible;
        WorkflowBottomBar.Visibility = Visibility.Visible;
    }

    private void StartDraft(object sender, RoutedEventArgs e)
    {
        EnterWorkflow();
        SavedDrawer.Visibility = Visibility.Collapsed;

        InstructionsView.Reset();
        Steps.SelectedIndex = 0;

        UpdateNavigationState();
    }

    private void ShowSaved(object sender, RoutedEventArgs e)
    {
        SavedDrawer.Visibility =
            SavedDrawer.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void CloseSaved(object sender, RoutedEventArgs e)
    {
        SavedDrawer.Visibility = Visibility.Collapsed;
    }

    private void OpenStoredPatch(object sender, RoutedEventArgs e)
    {
        EnterWorkflow();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SavedDrawer.Visibility = Visibility.Collapsed;
            Steps.SelectedIndex = 3;

            UpdateNavigationState();
        }));
    }

    private void PreviousStep(object sender, RoutedEventArgs e)
    {
        Steps.SelectedIndex = Math.Max(
            0,
            Steps.SelectedIndex - 1);

        UpdateNavigationState();
    }

    private void NextStep(object sender, RoutedEventArgs e)
    {
        if (!CanAdvanceThrough(Steps.SelectedIndex))
        {
            return;
        }

        Steps.SelectedIndex = Math.Min(
            Steps.Items.Count - 1,
            Steps.SelectedIndex + 1);

        UpdateNavigationState();
    }

    private void StepChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateNavigationState();
    }

    private bool CanAdvanceThrough(int step)
    {
        return step switch
        {
            0 => _vm?.CanAdvanceGame == true,
            1 => _vm?.CanAdvanceTargets == true,
            2 => _vm?.CanAdvanceFingerprint == true,
            3 => _vm?.CanAdvanceDetails == true,
            4 => _vm?.CanAdvanceInstructions == true,
            _ => false
        };
    }

    private void UpdateNavigationState()
    {
        if (Steps is null ||
            WorkflowBottomBar is null ||
            TargetsStep is null ||
            FingerprintStep is null ||
            DetailsStep is null ||
            InstructionsStep is null ||
            ReviewStep is null)
        {
            return;
        }

        var current = Steps.SelectedIndex;

        var gameReady = CanAdvanceThrough(0);
        var targetsReady = CanAdvanceThrough(1);
        var fingerprintReady = CanAdvanceThrough(2);
        var detailsReady = CanAdvanceThrough(3);
        var instructionsReady = CanAdvanceThrough(4);

        TargetsStep.IsEnabled =
            current >= 1 || gameReady;

        FingerprintStep.IsEnabled =
            current >= 2 || targetsReady;

        DetailsStep.IsEnabled =
            current >= 3 || fingerprintReady;

        InstructionsStep.IsEnabled =
            current >= 4 || detailsReady;

        ReviewStep.IsEnabled =
            current >= 5 || instructionsReady;

        WorkflowBottomBar.NextEnabled =
            CanAdvanceThrough(current);
    }

}
