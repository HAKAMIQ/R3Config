using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class PlanVm : VmBase
{
    private string _title = UiText.T("Plan.Title");
    private string _summary = UiText.T("Plan.SelectGame");
    private string _gameSerial = string.Empty;
    private string _emptyStateText = UiText.T("Plan.Empty");
    private string _targetConfigPath = "-";
    private bool _showApplied = true;
    private ConfigPlan? _plan;

    public event EventHandler? SelectionStateChanged;

    public ObservableCollection<PlanItemVm> Items { get; } = [];

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string GameSerial
    {
        get => _gameSerial;
        private set => SetProperty(ref _gameSerial, value);
    }

    public string TargetConfigPath
    {
        get => _targetConfigPath;
        set => SetProperty(ref _targetConfigPath, value);
    }

    public bool HasSelectedChanges => Items.Any(item => item.IsSelected && item.IsDifferent && item.IsActionable);

    public bool HasRemovableSettings => _plan?.Items.Any(item => item.CurrentValue is not null) == true;

    public bool IsEmpty => Items.Count == 0;

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    public bool ShowApplied
    {
        get => _showApplied;
        set
        {
            if (SetProperty(ref _showApplied, value))
            {
                ReloadItems(preserveSelection: true);
            }
        }
    }

    public void RefreshLanguage()
    {
        if (_plan is null)
        {
            Title = UiText.T("Plan.Title");
            GameSerial = string.Empty;
        }

        EmptyStateText = _plan is null
            ? UiText.T("Plan.Empty")
            : GetEmptyStateText(_plan);

        UpdateSummary();

        foreach (var item in Items)
        {
            item.RefreshLanguage();
        }
    }

    public void Clear()
    {
        UnsubscribeItems();

        Items.Clear();
        Title = UiText.T("Plan.Title");
        GameSerial = string.Empty;
        Summary = UiText.T("Plan.SelectGame");
        EmptyStateText = UiText.T("Plan.Empty");
        TargetConfigPath = "-";
        _plan = null;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRemovableSettings));
        RaiseSelectionChanged();
    }

    public void Load(ConfigPlan plan)
    {
        Clear();

        _plan = plan;
        Title = plan.GameTitle;
        GameSerial = plan.GameSerial;
        TargetConfigPath = plan.TargetConfigPath;

        ReloadItems(preserveSelection: false);
    }

    public void SelectRecommended()
    {
        foreach (var item in Items)
        {
            item.IsSelected = item.IsAutoSelectable && item.IsDifferent && item.IsActionable;
        }

        UpdateSummary();
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }

        UpdateSummary();
        RaiseSelectionChanged();
    }

    private void ReloadItems(bool preserveSelection)
    {
        var selectedKeys = preserveSelection
            ? SelectedItemKeys()
            : [];

        UnsubscribeItems();
        Items.Clear();

        if (_plan is null)
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasRemovableSettings));
            UpdateSummary();
            RaiseSelectionChanged();
            return;
        }

        var visibleItems = _plan.Items.Where(ShouldShowItem);

        foreach (var item in visibleItems)
        {
            var isSelected = preserveSelection &&
                             selectedKeys.Contains(ItemKey(item));

            var viewModel = new PlanItemVm(item, isSelected);
            viewModel.PropertyChanged += Item_PropertyChanged;
            Items.Add(viewModel);
        }

        EmptyStateText = GetEmptyStateText(_plan);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRemovableSettings));
        UpdateSummary();
        RaiseSelectionChanged();
    }

    private bool ShouldShowItem(ConfigPlanEntry item)
    {
        if (ShowApplied)
        {
            return true;
        }

        if (item.State != RecState.Applied)
        {
            return true;
        }

        if (_plan is null)
        {
            return false;
        }

        // When every recommendation is already configured, keep the rows visible.
        // The final user needs proof of what was applied instead of an empty panel.
        var state = _plan.RecState;
        return state.Pending == 0
            && state.Manual == 0
            && state.Blocked == 0
            && state.Unknown == 0;
    }

    private HashSet<string> SelectedItemKeys()
    {
        return Items
            .Where(item => item.IsSelected)
            .Select(item => ItemKey(item.Entry))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void UnsubscribeItems()
    {
        foreach (var item in Items)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlanItemVm.IsSelected))
        {
            UpdateSummary();
            RaiseSelectionChanged();
        }
    }

    private void UpdateSummary()
    {
        if (_plan is null)
        {
            Summary = UiText.T("Plan.SelectGame");
            return;
        }

        var state = _plan.RecState;
        Summary = UiText.F("Plan.SummaryFormat", state.Total, state.Applied, state.Pending);
    }

    private static string ItemKey(ConfigPlanEntry item)
    {
        return string.Join("|", item.Section, item.Key, item.DisplayPath);
    }

    private static string GetEmptyStateText(ConfigPlan plan)
    {
        if (plan.RecState.Total == 0)
        {
            return UiText.T("Plan.NoRec");
        }

        if (plan.RecState.Applied == plan.RecState.Total)
        {
            return UiText.T("Plan.AllApplied");
        }

        return string.Empty;
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedChanges));
        OnPropertyChanged(nameof(HasRemovableSettings));
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
