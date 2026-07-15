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
    private string _title = UiText.GetText("Plan.Title");
    private string _summary = UiText.GetText("Plan.SelectGame");
    private string _serial = string.Empty;
    private string _empty = UiText.GetText("Plan.Empty");
    private string _tgtCfg = "-";
    private bool _applied = true;
    private CfgPlan? _plan;

    public event EventHandler? SelState;

    public ObservableCollection<PlanItVm> Items { get; } = [];

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        set => Set(ref _summary, value);
    }

    public string Serial
    {
        get => _serial;
        private set => Set(ref _serial, value);
    }

    public string TgtCfg
    {
        get => _tgtCfg;
        set => Set(ref _tgtCfg, value);
    }

    public bool HasChg => Items.Any(item => item.IsSel && item.IsDiff && item.IsAction);

    public bool CanDel => _plan?.Items.Any(item => item.CurVal is not null) == true;

    public bool IsEmpty => Items.Count == 0;

    public string EmptyTxt
    {
        get => _empty;
        private set => Set(ref _empty, value);
    }

    public bool ShowDone
    {
        get => _applied;
        set
        {
            if (Set(ref _applied, value))
            {
                Reload(preserveSelection: true);
            }
        }
    }

    public void SetLang()
    {
        if (_plan is null)
        {
            Title = UiText.GetText("Plan.Title");
            Serial = string.Empty;
        }

        EmptyTxt = _plan is null
            ? UiText.GetText("Plan.Empty")
            : GetEmpty(_plan);

        SetSum();

        foreach (var item in Items)
        {
            item.SetLang();
        }
    }

    public void Clear()
    {
        Unsub();

        Items.Clear();
        Title = UiText.GetText("Plan.Title");
        Serial = string.Empty;
        Summary = UiText.GetText("Plan.SelectGame");
        EmptyTxt = UiText.GetText("Plan.Empty");
        TgtCfg = "-";
        _plan = null;

        OnChange(nameof(IsEmpty));
        OnChange(nameof(CanDel));
        RaiseSel();
    }

    public void Load(CfgPlan plan)
    {
        Clear();

        _plan = plan;
        Title = plan.GameName;
        Serial = plan.GameSer;
        TgtCfg = plan.CfgPath;

        Reload(preserveSelection: false);
    }

    public void Select()
    {
        foreach (var item in Items)
        {
            item.IsSel = item.AutoSel && item.IsDiff && item.IsAction;
        }

        SetSum();
        RaiseSel();
    }

    public void ClearSel()
    {
        foreach (var item in Items)
        {
            item.IsSel = false;
        }

        SetSum();
        RaiseSel();
    }

    private void Reload(bool preserveSelection)
    {
        var selectedKeys = preserveSelection
            ? SelItem()
            : [];

        Unsub();
        Items.Clear();

        if (_plan is null)
        {
            OnChange(nameof(IsEmpty));
            OnChange(nameof(CanDel));
            SetSum();
            RaiseSel();
            return;
        }

        var visibleItems = _plan.Items.Where(ShowItem);

        foreach (var item in visibleItems)
        {
            var isSelected = preserveSelection &&
                             selectedKeys.Contains(ItemKey(item));

            var viewModel = new PlanItVm(item, isSelected);
            viewModel.PropChg += OnItem;
            Items.Add(viewModel);
        }

        EmptyTxt = GetEmpty(_plan);
        OnChange(nameof(IsEmpty));
        OnChange(nameof(CanDel));
        SetSum();
        RaiseSel();
    }

    private bool ShowItem(CfgEntry item)
    {
        if (ShowDone)
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

    private HashSet<string> SelItem()
    {
        return Items
            .Where(item => item.IsSel)
            .Select(item => ItemKey(item.Entry))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void Unsub()
    {
        foreach (var item in Items)
        {
            item.PropChg -= OnItem;
        }
    }

    private void OnItem(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlanItVm.IsSel))
        {
            SetSum();
            RaiseSel();
        }
    }

    private void SetSum()
    {
        if (_plan is null)
        {
            Summary = UiText.GetText("Plan.SelectGame");
            return;
        }

        var state = _plan.RecState;
        Summary = UiText.Fmt("Plan.SummaryFormat", state.Total, state.Applied, state.Pending);
    }

    private static string ItemKey(CfgEntry item)
    {
        return string.Join("|", item.Section, item.Key, item.Path);
    }

    private static string GetEmpty(CfgPlan plan)
    {
        if (plan.RecState.Total == 0)
        {
            return UiText.GetText("Plan.NoRec");
        }

        if (plan.RecState.Applied == plan.RecState.Total)
        {
            return UiText.GetText("Plan.AllApplied");
        }

        return string.Empty;
    }

    private void RaiseSel()
    {
        OnChange(nameof(HasChg));
        OnChange(nameof(CanDel));
        SelState?.Invoke(this, EventArgs.Empty);
    }
}
