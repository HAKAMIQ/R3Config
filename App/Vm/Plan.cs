using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class PlanVm : VmBase
{
    private string _title = UiText.GetText("Plan.Title");
    private string _summary = UiText.GetText("Plan.SelectGame");
    private string _serial = string.Empty;
    private string _empty = UiText.GetText("Plan.Empty");
    private string _tgtCfg = "-";
    private bool _applied = true;
    private bool _syncingSelection;
    private bool _batchingSelection;
    private CfgPlan? _plan;
    private GameSettingsModel? _model;

    public event EventHandler? SelState;

    // Flat list: selection, apply, and remove all operate on this.
    public ObservableCollection<PlanItVm> Items { get; } = [];

    // The same rows grouped by RPCS3 Settings tab. Internal metadata only
    // (mapping, ordering, validation); the view never shows tab headers.
    public ObservableCollection<PlanSectionVm> Sections { get; } = [];

    // The one flat, actionable list the view binds to, in RPCS3 tab order.
    public ObservableCollection<PlanItVm> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

    public GameEvidenceVm Evidence { get; } = new();

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

    // G1: the selected changes would create config_SERIAL.yml for a game that
    // RPCS3's database config covers; RPCS3 then ignores that database config.
    // Lines are technical (RPCS3 section/key and values), shown verbatim.
    public bool HasDbWarn => DbLoss().Count > 0;

    public string DbWarnTitle =>
        _plan is null
            ? string.Empty
            : UiText.Fmt(
                "Plan.DbWarn",
                // WPF ignores bidi isolates, and inside RTL text it splits
                // "config_BLUS30442.yml" at the dot after the digits; an LRM
                // on both sides of every dot keeps the file name one LTR run.
                Lrm + System.IO.Path.GetFileName(_plan.CfgPath).Replace(".", $"{Lrm}.{Lrm}", StringComparison.Ordinal) + Lrm);

    public IReadOnlyList<string> DbWarnLines => [.. DbLoss().Select(Line)];

    // Round 12: (a) a recommendation above writes the same value, so selecting
    // it keeps the setting; (b+d) R3Config cannot write the value and it is lost.
    public IReadOnlyList<string> DbKeepLines => [.. DbLoss().Where(setting => Keeper(setting) is not null).Select(Line)];

    public IReadOnlyList<string> DbLostLines => [.. DbLoss().Where(setting => Keeper(setting) is null).Select(Line)];

    public bool HasDbKeep => DbKeepLines.Count > 0;

    public bool HasDbLost => DbLostLines.Count > 0;

    // Ambiguous Wiki guidance: the Wiki named a range ("preferred spu threads
    // = 1 - 3") instead of one value, so there is no row, no checkbox and no
    // value to write. The range is quoted as the Wiki's own advice and R3Config
    // picks nothing; the parser still rejects the value for application.
    public bool HasWikiRange => WikiRanges.Count > 0;

    public string WikiRangeTitle =>
        _plan is null ? string.Empty : UiText.GetText("Plan.WikiRangeTitle");

    public IReadOnlyList<PlanRangeVm> WikiRanges =>
    [
        .. (_plan?.Hints ?? []).Select(hint => new PlanRangeVm(
            hint.Path,
            UiText.Fmt(
                "Plan.WikiRange",
                hint.Min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hint.Max.ToString(System.Globalization.CultureInfo.InvariantCulture))))
    ];

    public string DbKeepTitle => _plan is null ? string.Empty : UiText.GetText("Plan.DbWarnKeep");

    public string DbLostTitle => _plan is null ? string.Empty : UiText.GetText("Plan.DbWarnLost");

    // Selects the (a) rows only, and only when the user presses the button.
    public System.Windows.Input.ICommand SelDbKeepCmd => _selDbKeep ??= new RelayCmd(SelectDbKeep, () => HasDbKeep);

    private RelayCmd? _selDbKeep;

    private const char Lrm = (char)0x200E;

    private static string Line(CfgDbSetting setting) =>
        setting.BaseVal is null
            ? $"{setting.Section}/{setting.Key}: {setting.Value}"
            : $"{setting.Section}/{setting.Key}: {setting.Value} → {setting.BaseVal}";

    private PlanItVm? Keeper(CfgDbSetting setting)
    {
        var entry = _plan is null ? null : PlanService.DatabaseKeep(_plan, setting);
        return entry is null ? null : Items.FirstOrDefault(item => ReferenceEquals(item.Entry, entry) && item.IsAction);
    }

    private void SelectDbKeep()
    {
        var keepers = DbLoss().Select(Keeper).OfType<PlanItVm>().ToArray();
        if (keepers.Length == 0)
        {
            return;
        }

        _batchingSelection = true;
        try
        {
            foreach (var item in keepers)
            {
                item.IsSel = true;
            }
        }
        finally
        {
            _batchingSelection = false;
        }

        SetSum();
        RaiseSel();
    }

    private IReadOnlyList<CfgDbSetting> DbLoss() =>
        _plan is null
            ? []
            : PlanService.DatabaseLoss(
                _plan,
                Items
                    .Where(item => item.IsSel && item.IsDiff && item.IsAction)
                    .Select(item => item.Change));

    // One quiet line: the layer RPCS3 boots the selected game with on top of
    // config.yml (PlanService.ResolveBootSource). Database is worded as R3Config's
    // copy of the RPCS3 database; the tip lists its keys verbatim (LTR).
    public bool HasBootSrc => _plan is not null;

    public string BootSrcText =>
        _plan?.BootSource switch
        {
            null => string.Empty,
            CfgBootSrc.Custom => UiText.GetText("Plan.BootSrcCustom"),
            CfgBootSrc.Database => UiText.Fmt("Plan.BootSrcDatabase", _plan.DbCfg.Count),
            CfgBootSrc.Global => UiText.GetText("Plan.BootSrcGlobal"),
            _ => UiText.GetText("Plan.BootSrcUnknown")
        };

    public bool HasBootSrcTip => _plan is { BootSource: CfgBootSrc.Database, DbCfg.Count: > 0 };

    public string BootSrcTip =>
        HasBootSrcTip
            ? string.Join(
                "\n",
                _plan!.DbCfg
                    .OrderBy(setting => setting.Section, StringComparer.Ordinal)
                    .ThenBy(setting => setting.Key, StringComparer.Ordinal)
                    .Select(setting => $"{setting.Section}/{setting.Key}: {setting.Value}"))
            : string.Empty;

    public bool CanDel => _plan?.Items.Any(item => item.HasCustomOverride) == true;

    public bool IsEmpty => Items.Count == 0;

    // Notes or online evidence are content too: the empty state must not
    // cover a game that has Wiki notes but no settings.
    public bool ShowEmpty => IsEmpty && !Evidence.HasAny;

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
        OnChange(nameof(DbWarnTitle));
        OnChange(nameof(DbKeepTitle));
        OnChange(nameof(DbLostTitle));
        OnChange(nameof(WikiRangeTitle));
        OnChange(nameof(WikiRanges));
        RaiseSrc();

        foreach (var item in Items)
        {
            item.SetLang();
        }

        foreach (var section in Sections)
        {
            section.SetLang();
        }

        Evidence.SetLang();
    }

    public void Clear()
    {
        Unsub();

        Items.Clear();
        Sections.Clear();
        Rows.Clear();
        Evidence.Clear();
        _model = null;
        Title = UiText.GetText("Plan.Title");
        Serial = string.Empty;
        Summary = UiText.GetText("Plan.SelectGame");
        EmptyTxt = UiText.GetText("Plan.Empty");
        TgtCfg = "-";
        _plan = null;
        RaiseSrc();

        OnChange(nameof(IsEmpty));
        OnChange(nameof(ShowEmpty));
        OnChange(nameof(CanDel));
        RaiseSel();
    }

    public void Load(CfgPlan plan, GameSettingsModel? model = null)
    {
        Unsub();
        Items.Clear();
        Sections.Clear();
        Rows.Clear();

        _plan = plan;
        _model = model;
        Evidence.Load(model);
        Title = plan.GameName;
        Serial = plan.GameSer;
        TgtCfg = plan.CfgPath;
        RaiseSrc();

        Reload(preserveSelection: false);
    }

    public void Select()
    {
        _batchingSelection = true;
        try
        {
            foreach (var item in Items)
            {
                item.IsSel = item.AutoSel && item.IsDiff && item.IsAction;
            }
        }
        finally
        {
            _batchingSelection = false;
        }

        SetSum();
        RaiseSel();
    }

    public void ClearSel()
    {
        _batchingSelection = true;
        try
        {
            foreach (var item in Items)
            {
                item.IsSel = false;
            }
        }
        finally
        {
            _batchingSelection = false;
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
        Sections.Clear();
        Rows.Clear();

        if (_plan is null)
        {
            OnChange(nameof(IsEmpty));
            OnChange(nameof(HasRows));
            OnChange(nameof(ShowEmpty));
            OnChange(nameof(CanDel));
            SetSum();
            RaiseSel();
            return;
        }

        var visibleItems = _plan.Items.Where(ShowItem);
        var rows = new Dictionary<CfgEntry, GameSettingRow>(
            ReferenceEqualityComparer.Instance);
        foreach (var row in _model?.Sections.SelectMany(section => section.Rows) ?? [])
        {
            rows[row.Entry] = row;
        }

        foreach (var item in visibleItems)
        {
            var isSelected = preserveSelection &&
                             selectedKeys.Contains(ItemKey(item));

            var viewModel = new PlanItVm(
                item,
                isSelected,
                rows.GetValueOrDefault(item));
            viewModel.PropChg += OnItem;
            Items.Add(viewModel);
        }

        foreach (var group in Items
                     .GroupBy(item => item.SectionKind)
                     .OrderBy(group => group.Key))
        {
            var section = new PlanSectionVm(group.Key);
            foreach (var item in group)
            {
                section.Items.Add(item);
            }

            Sections.Add(section);
            foreach (var item in section.Items)
            {
                Rows.Add(item);
            }
        }

        OnChange(nameof(HasRows));

        EmptyTxt = GetEmpty(_plan);
        OnChange(nameof(IsEmpty));
        OnChange(nameof(ShowEmpty));
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
            if (_syncingSelection)
            {
                return;
            }

            if (sender is PlanItVm changed)
            {
                _syncingSelection = true;
                try
                {
                    foreach (var item in Items.Where(item =>
                                 !ReferenceEquals(item, changed) &&
                                 string.Equals(
                                     item.Change.WikiKey,
                                     changed.Change.WikiKey,
                                     StringComparison.OrdinalIgnoreCase)))
                    {
                        item.IsSel = changed.IsSel;
                    }
                }
                finally
                {
                    _syncingSelection = false;
                }
            }

            if (!_batchingSelection)
            {
                SetSum();
                RaiseSel();
            }
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
        if (state.Pending == 0 && state.Applied != state.Total)
        {
            Summary = UiText.Fmt("Plan.SummaryReviewFormat", state.Total, state.Applied);
            return;
        }

        Summary = state.Pending == 0
            ? UiText.Fmt("Plan.SummaryCompleteFormat", state.Total)
            : state.Applied == 0
                ? UiText.Fmt(
                    "Plan.SummaryPendingOnlyFormat",
                    state.Total,
                    state.Pending)
                : UiText.Fmt(
                    "Plan.SummaryPendingFormat",
                    state.Total,
                    state.Applied,
                    state.Pending);
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

    private void RaiseSrc()
    {
        OnChange(nameof(HasBootSrc));
        OnChange(nameof(BootSrcText));
        OnChange(nameof(HasBootSrcTip));
        OnChange(nameof(BootSrcTip));
    }

    private void RaiseSel()
    {
        OnChange(nameof(HasChg));
        OnChange(nameof(CanDel));
        OnChange(nameof(HasDbWarn));
        OnChange(nameof(DbWarnTitle));
        OnChange(nameof(DbWarnLines));
        OnChange(nameof(DbKeepLines));
        OnChange(nameof(DbLostLines));
        OnChange(nameof(HasDbKeep));
        OnChange(nameof(HasDbLost));
        OnChange(nameof(DbKeepTitle));
        OnChange(nameof(DbLostTitle));
        OnChange(nameof(HasWikiRange));
        OnChange(nameof(WikiRangeTitle));
        OnChange(nameof(WikiRanges));
        _selDbKeep?.RaiseCan();
        SelState?.Invoke(this, EventArgs.Empty);
    }
}
