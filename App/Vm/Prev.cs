using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

public sealed class PatPrev : VmBase
{
    private readonly List<PatMatch> _matches = [];
    private string _title = UiText.GetText("Patch.Title");
    private string _summary = UiText.GetText("Patch.Empty");
    private string _empty = UiText.GetText("Patch.Empty");
    private string _warnTxt = string.Empty;
    private string? _surfRaw;
    private string? _warnRaw;
    private PatSurf _state = PatSurf.NoAnlz;
    private bool _showExp = true;
    private bool _resolvingExclusiveSelection;
    private string? _exclusiveNoticeKey;
    private object?[] _exclusiveNoticeArgs = [];
    private Func<PatMatch, bool>? _isOnFn;

    public event EventHandler? SelState;

    public ObservableCollection<PatItVm> Items { get; } = [];

    public ObservableCollection<PatItVm> OfclList { get; } = [];

    public ObservableCollection<PatItVm> CanItems { get; } = [];

    public ObservableCollection<PatItVm> ChtItems { get; } = [];

    public ObservableCollection<TgtGrpVm> OfclGrp { get; } = [];

    public ObservableCollection<TgtGrpVm> CanGrps { get; } = [];

    public ObservableCollection<TgtGrpVm> ChtGrps { get; } = [];

    public bool OfclOn => OfclList.Any(item => item.IsSel && item.CanOn);

    public bool OfclOff => OfclList.Any(item => item.IsSel && item.CanOff);

    public bool SelCanOn => CanItems.Any(item => item.IsSel && item.CanOn);

    public bool CanOff => CanItems.Any(item => item.IsSel && item.CanOff);

    public bool SelChtOn => ChtItems.Any(item => item.IsSel && item.CanOn);

    public bool ChtOff => ChtItems.Any(item => item.IsSel && item.CanOff);

    public bool IsEmpty => Items.Count == 0;

    public bool HasItems => Items.Count > 0;

    public bool HasOfcl => OfclList.Count > 0;

    public bool HasCan => CanItems.Count > 0;

    public bool HasCht => ChtItems.Count > 0;

    public bool HasLocal => OfclList.Any(item => item.IsSel && item.CanSel);

    public bool HasOfclActions => OfclOn || OfclOff;

    public bool HasCanActions => SelCanOn || CanOff;

    public bool HasChtActions => SelChtOn || ChtOff;

    public string OfclSum => SubSum(
        OfclList,
        "Patch.OfficialSummary",
        "Patch.OfficialSummaryPending",
        "Patch.OfficialSummaryNone",
        "Patch.OfficialSummaryPendingOnly");

    public string CanSum => CanarySum();

    public string ChtSum => ArtemisSum();

    public string OfclHint => ActHint(OfclList);

    public string CanHint => ActHint(CanItems);

    public string ChtHint => ActHint(ChtItems);

    public string ChtSelectionNotice =>
        _exclusiveNoticeKey is null
            ? string.Empty
            : UiText.Fmt(
                _exclusiveNoticeKey,
                _exclusiveNoticeArgs);

    public bool HasChtSelectionNotice =>
        !string.IsNullOrWhiteSpace(ChtSelectionNotice);

    public bool IsOfcl => OfclList.Count == 0;

    public bool CanEmpty => CanItems.Count == 0;

    public bool ChtEmpty => ChtItems.Count == 0;

    public bool HasWarn => !string.IsNullOrWhiteSpace(WarnText);

    public string WarnText
    {
        get => _warnTxt;
        private set
        {
            if (Set(ref _warnTxt, value))
            {
                OnChange(nameof(HasWarn));
            }
        }
    }

    public string EmptyTxt
    {
        get => _empty;
        private set => Set(ref _empty, value);
    }

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

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment TxtAlign => UiLaySvc.Current.TxtAlign;

    public bool ShowExp
    {
        get => _showExp;
        set
        {
            if (Set(ref _showExp, value))
            {
                LoadView();
            }
        }
    }

    public void SetLang()
    {
        Title = UiText.GetText("Patch.Title");
        OnChange(nameof(TextFlow));
        OnChange(nameof(TxtAlign));
        OnChange(nameof(ChtSelectionNotice));
        OnChange(nameof(HasChtSelectionNotice));

        if (_state == PatSurf.Loaded)
        {
            // WPF can retain the old FlowDirection inside already-materialized
            // DataTemplate containers after a live Arabic/English switch. Rebuild
            // the item containers under the new application resources while
            // preserving every explicit checkbox state.
            var selection = Items.ToDictionary(
                SelId,
                item => item.IsSel,
                StringComparer.OrdinalIgnoreCase);
            var expansion = OfclGrp
                .Concat(CanGrps)
                .Concat(ChtGrps)
                .ToDictionary(
                    GroupId,
                    group => group.IsOpen,
                    StringComparer.OrdinalIgnoreCase);

            LoadView();
            foreach (var item in Items)
            {
                if (selection.TryGetValue(SelId(item), out var isSelected))
                {
                    item.IsSel = isSelected;
                }
            }
            foreach (var group in OfclGrp.Concat(CanGrps).Concat(ChtGrps))
            {
                if (expansion.TryGetValue(GroupId(group), out var isExpanded))
                {
                    group.IsOpen = isExpanded;
                }
            }

            return;
        }

        switch (_state)
        {
            case PatSurf.NoAnlz:
                Summary = UiText.GetText("Patch.Empty");
                EmptyTxt = UiText.GetText("Patch.Empty");
                break;

            case PatSurf.Error:
                var message = FmtPat(_surfRaw);
                Summary = message;
                EmptyTxt = message;
                break;

            case PatSurf.NoPat:
                Summary = WithWarn(UiText.GetText("Patch.NoneForGame"));
                EmptyTxt = UiText.GetText("Patch.NoneForGame");
                break;

            case PatSurf.Loaded:
                EmptyTxt = IsEmpty ? UiText.GetText("Patch.EmptyVisible") : string.Empty;
                Summary = AllSum();
                break;
        }

        foreach (var item in Items)
        {
            item.SetLang();
        }

        foreach (var group in OfclGrp.Concat(CanGrps).Concat(ChtGrps))
        {
            group.SetLang();
        }
    }

    private static string SelId(PatItVm item)
    {
        return string.Join(
            "\0",
            item.Match.Patch.HashKind,
            item.Match.Patch.Hash,
            item.Match.Patch.Title,
            TgtGrpVm.BuildKey(item));
    }

    private static string GroupId(TgtGrpVm group)
    {
        var first = group.Items[0];
        var surface = first.IsOfcl
            ? "Official"
            : first.IsCanary
                ? "Canary"
                : first.IsArtemis
                    ? "Artemis"
                    : "Local";
        return string.Join("\0", surface, TgtGrpVm.BuildKey(first));
    }

    public void Clear()
    {
        _matches.Clear();
        Items.Clear();
        OfclList.Clear();
        CanItems.Clear();
        ChtItems.Clear();
        OfclGrp.Clear();
        CanGrps.Clear();
        ChtGrps.Clear();
        _surfRaw = null;
        _warnRaw = null;
        WarnText = string.Empty;
        _state = PatSurf.NoAnlz;
        _isOnFn = null;
        ClearExclusiveNotice();

        Title = UiText.GetText("Patch.Title");
        Summary = UiText.GetText("Patch.Empty");
        EmptyTxt = UiText.GetText("Patch.Empty");

        OnChange(nameof(IsEmpty));
        RaiseSel();
    }

    public void Load(
        IReadOnlyList<PatMatch> matches,
        string? title = null,
        string? warningMessage = null,
        Func<PatMatch, bool>? isEnabledResolver = null)
    {
        _matches.Clear();
        _matches.AddRange(matches);

        _surfRaw = null;
        _warnRaw = warningMessage;
        WarnText = warningMessage?.Trim() ?? string.Empty;
        _isOnFn = isEnabledResolver;
        _state = matches.Count == 0
            ? PatSurf.NoPat
            : PatSurf.Loaded;
        ClearExclusiveNotice();

        Title = title ?? UiText.GetText("Patch.Title");
        LoadView();

        if (_state == PatSurf.NoPat)
        {
            EmptyTxt = UiText.GetText("Patch.NoneForGame");
            Summary = AllSum();
        }
        else
        {
            EmptyTxt = IsEmpty ? UiText.GetText("Patch.EmptyVisible") : string.Empty;
            Summary = AllSum();
        }

        OnChange(nameof(IsEmpty));
    }

    public void LoadErr(string message)
    {
        _matches.Clear();
        Items.Clear();
        OfclList.Clear();
        CanItems.Clear();
        ChtItems.Clear();
        OfclGrp.Clear();
        CanGrps.Clear();
        ChtGrps.Clear();
        _surfRaw = message;
        _warnRaw = null;
        WarnText = string.Empty;
        _state = PatSurf.Error;
        _isOnFn = null;
        ClearExclusiveNotice();

        Title = UiText.GetText("Patch.Title");

        var localizedMessage = FmtPat(message);
        Summary = localizedMessage;
        EmptyTxt = localizedMessage;

        OnChange(nameof(IsEmpty));
        RaiseSel();
    }

    public IReadOnlyList<PatMatch> Enabled()
    {
        return Items
            .Where(item => item.IsOn)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelCan()
    {
        return CanItems
            .Where(item => item.IsSel && item.CanOn)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelOfcl()
    {
        return OfclList
            .Where(item => item.IsSel && item.CanOn)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelOfOff()
    {
        return OfclList
            .Where(item => item.IsSel && item.CanOff)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelCanOf()
    {
        return CanItems
            .Where(item => item.IsSel && item.CanOff)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelCht()
    {
        return ChtItems
            .Where(item => item.IsSel && item.CanOn)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelChtOf()
    {
        return ChtItems
            .Where(item => item.IsSel && item.CanOff)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelManagedCht()
    {
        return ChtItems
            .Where(item => item.IsSel && item.Match.Patch.Source == PatSrc.ManagedArtemis)
            .Select(item => item.Match)
            .ToArray();
    }

    public bool HasManagedChtSelection => SelManagedCht().Count > 0;

    public void SelectAllCht()
    {
        ClearExclusiveNotice();
        foreach (var item in ChtItems.Where(item => item.CanSel))
        {
            item.IsSel = true;
        }

        RaiseSel();
    }

    public void ClearChtSelection()
    {
        ClearExclusiveNotice();
        foreach (var item in ChtItems.Where(item => item.IsSel))
        {
            item.IsSel = false;
        }

        RaiseSel();
    }

    private void LoadView()
    {
        Items.Clear();
        OfclList.Clear();
        CanItems.Clear();
        ChtItems.Clear();
        OfclGrp.Clear();
        CanGrps.Clear();
        ChtGrps.Clear();

        foreach (var match in _matches.Where(match => PatPol.Is(match.Patch.Risk, ShowExp)))
        {
            var isEnabled = _isOnFn?.Invoke(match) == true;
            if (PatVer.IsAll(match.TgtSer) &&
                match.Status != MatStat.Match)
            {
                // RPCS3 uses All/All/All for hash-scoped modules shared by many
                // games. Enabled state in patch_config.yml is global too, so it
                // cannot prove that the patch belongs to the selected game.
                // Only exact local runtime evidence may place it on a game page.
                continue;
            }

            var item = new PatItVm(match, isEnabled);

            if (item.IsOfcl)
            {
                OfclList.Add(item);
            }
            else if (item.IsCanary)
            {
                CanItems.Add(item);
            }
            else if (item.IsArtemis)
            {
                ChtItems.Add(item);
            }
            else
            {
                // Local non-Canary definitions are not official RPCS3
                // patch.yml entries. Keep them out of the official surface
                // until a dedicated local-patch surface exists.
                continue;
            }

            item.PropChg += PatItem;
            Items.Add(item);
        }

        BuildTgt(OfclList, OfclGrp);
        BuildTgt(CanItems, CanGrps);
        BuildTgt(ChtItems, ChtGrps);

        if (_matches.Count > 0)
        {
            EmptyTxt = IsEmpty ? UiText.GetText("Patch.EmptyVisible") : string.Empty;
            OnChange(nameof(IsEmpty));
            Summary = AllSum();
        }

        RaiseSel();
    }

    private static void BuildTgt(
        IReadOnlyCollection<PatItVm> source,
        ObservableCollection<TgtGrpVm> destination)
    {
        foreach (var group in source
                     .GroupBy(TgtGrpVm.BuildKey, StringComparer.OrdinalIgnoreCase)
                     .Select(items => new TgtGrpVm(items.ToArray()))
                     .OrderBy(group => group.Rank)
                     .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.Serial, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.SortKey, StringComparer.OrdinalIgnoreCase))
        {
            destination.Add(group);
        }
    }

    private void PatItem(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PatItVm.IsSel))
        {
            return;
        }

        if (!_resolvingExclusiveSelection &&
            sender is PatItVm item &&
            item.IsSel &&
            item.CanOn)
        {
            ResolveExclusiveSelection(item);
        }

        Summary = AllSum();
        RaiseSel();
    }

    private void ResolveExclusiveSelection(PatItVm selected)
    {
        if (!selected.IsArtemis)
        {
            ClearExclusiveNotice();
            return;
        }

        var group = selected.Match.Patch.Group?.Trim();
        if (string.IsNullOrWhiteSpace(group))
        {
            ClearExclusiveNotice();
            return;
        }

        var enabledPeer = Items.FirstOrDefault(peer =>
            !ReferenceEquals(peer, selected) &&
            peer.IsOn &&
            SameExclusiveGroup(selected, peer));

        if (enabledPeer is not null)
        {
            _resolvingExclusiveSelection = true;
            try
            {
                selected.IsSel = false;
            }
            finally
            {
                _resolvingExclusiveSelection = false;
            }

            SetExclusiveNotice(
                "Patch.Exclusive.EnabledBlock",
                selected.DspTitle,
                enabledPeer.DspTitle,
                group);
            return;
        }

        var selectedPeers = Items
            .Where(peer =>
                !ReferenceEquals(peer, selected) &&
                peer.IsSel &&
                peer.CanOn &&
                SameExclusiveGroup(selected, peer))
            .ToArray();

        if (selectedPeers.Length == 0)
        {
            ClearExclusiveNotice();
            return;
        }

        _resolvingExclusiveSelection = true;
        try
        {
            foreach (var peer in selectedPeers)
            {
                peer.IsSel = false;
            }
        }
        finally
        {
            _resolvingExclusiveSelection = false;
        }

        SetExclusiveNotice(
            "Patch.Exclusive.AutoResolved",
            selected.DspTitle,
            string.Join(
                ", ",
                selectedPeers
                    .Select(peer => peer.DspTitle)
                    .Order(StringComparer.OrdinalIgnoreCase)),
            group);
    }

    private static bool SameExclusiveGroup(
        PatItVm left,
        PatItVm right)
    {
        return left.IsArtemis &&
               right.IsArtemis &&
               left.Match.Patch.RunKey.Equals(
                   right.Match.Patch.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(left.Match.Patch.Group) &&
               !string.IsNullOrWhiteSpace(right.Match.Patch.Group) &&
               left.Match.Patch.Group.Trim().Equals(
                   right.Match.Patch.Group.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private void SetExclusiveNotice(
        string key,
        params object?[] args)
    {
        _exclusiveNoticeKey = key;
        _exclusiveNoticeArgs = args;
        OnChange(nameof(ChtSelectionNotice));
        OnChange(nameof(HasChtSelectionNotice));
    }

    private void ClearExclusiveNotice()
    {
        if (_exclusiveNoticeKey is null &&
            _exclusiveNoticeArgs.Length == 0)
        {
            return;
        }

        _exclusiveNoticeKey = null;
        _exclusiveNoticeArgs = [];
        OnChange(nameof(ChtSelectionNotice));
        OnChange(nameof(HasChtSelectionNotice));
    }

    private void RaiseSel()
    {
        OnChange(nameof(IsEmpty));
        OnChange(nameof(HasItems));
        OnChange(nameof(HasOfcl));
        OnChange(nameof(HasCan));
        OnChange(nameof(HasCht));
        OnChange(nameof(OfclGrp));
        OnChange(nameof(CanGrps));
        OnChange(nameof(ChtGrps));
        OnChange(nameof(OfclSum));
        OnChange(nameof(CanSum));
        OnChange(nameof(ChtSum));
        OnChange(nameof(OfclHint));
        OnChange(nameof(CanHint));
        OnChange(nameof(ChtHint));
        OnChange(nameof(IsOfcl));
        OnChange(nameof(CanEmpty));
        OnChange(nameof(ChtEmpty));
        OnChange(nameof(OfclOn));
        OnChange(nameof(OfclOff));
        OnChange(nameof(HasOfclActions));
        OnChange(nameof(SelCanOn));
        OnChange(nameof(CanOff));
        OnChange(nameof(HasCanActions));
        OnChange(nameof(SelChtOn));
        OnChange(nameof(ChtOff));
        OnChange(nameof(HasChtActions));
        OnChange(nameof(HasLocal));
        SelState?.Invoke(this, EventArgs.Empty);
    }

    private string CanarySum()
    {
        if (CanItems.Count == 0)
        {
            return string.Empty;
        }

        var enabledCount = CanItems.Count(item => item.IsOn);
        var selectedCount = CanItems.Count(item => item.IsSel);

        if (enabledCount == 0 &&
            selectedCount == 0 &&
            CanItems.All(item => !item.CanOn && !item.IsOn))
        {
            return UiText.GetText("Patch.CanarySummaryAllIncompatible");
        }

        if (enabledCount == 0)
        {
            return selectedCount == 0
                ? UiText.Fmt("Patch.CanarySummaryNone", CanItems.Count)
                : UiText.Fmt(
                    "Patch.CanarySummaryPendingOnly",
                    CanItems.Count,
                    selectedCount);
        }

        return selectedCount == 0
            ? UiText.Fmt(
                "Patch.CanarySummary",
                CanItems.Count,
                enabledCount)
            : UiText.Fmt(
                "Patch.CanarySummaryPending",
                CanItems.Count,
                enabledCount,
                selectedCount);
    }

    private string ArtemisSum()
    {
        if (ChtItems.Count == 0)
        {
            return string.Empty;
        }

        var enabledCount = ChtItems.Count(item => item.IsOn);
        var selectedCount = ChtItems.Count(item => item.IsSel);

        if (enabledCount == 0)
        {
            return selectedCount == 0
                ? UiText.Fmt("Patch.Artemis.SummaryNone", ChtItems.Count)
                : UiText.Fmt(
                    "Patch.Artemis.SummaryPendingOnly",
                    ChtItems.Count,
                    selectedCount);
        }

        return selectedCount == 0
            ? UiText.Fmt(
                "Patch.Artemis.Summary",
                ChtItems.Count,
                enabledCount)
            : UiText.Fmt(
                "Patch.Artemis.SummaryPending",
                ChtItems.Count,
                enabledCount,
                selectedCount);
    }

    private static string SubSum(
        ObservableCollection<PatItVm> items,
        string key,
        string pendingKey,
        string noneKey,
        string pendingOnlyKey)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var enabledCount = items.Count(item => item.IsOn);
        var selectedCount = items.Count(item => item.IsSel);

        if (enabledCount == 0)
        {
            return selectedCount == 0
                ? UiText.Fmt(noneKey, items.Count)
                : UiText.Fmt(pendingOnlyKey, items.Count, selectedCount);
        }

        return selectedCount == 0
            ? UiText.Fmt(key, items.Count, enabledCount)
            : UiText.Fmt(pendingKey, items.Count, enabledCount, selectedCount);
    }

    private static string ActHint(IReadOnlyCollection<PatItVm> items)
    {
        var enableCount = items.Count(item => item.IsSel && item.CanOn);
        var disableCount = items.Count(item => item.IsSel && item.CanOff);
        if (enableCount > 0 || disableCount > 0)
        {
            return UiText.Fmt("Patch.ActionHint.Selected", enableCount, disableCount);
        }

        return items.Any(item => item.CanSel)
            ? UiText.GetText("Patch.ActionHint.Select")
            : UiText.GetText("Patch.ActionHint.None");
    }

    private string AllSum()
    {
        if (Items.Count == 0)
        {
            return string.Empty;
        }

        var selectedCount = Items.Count(item => item.IsSel);
        var readyCount = Items.Count(item => item.CanOn || item.CanOff);
        var canaryCount = Items.Count(item => item.IsExpPat);

        if (canaryCount > 0)
        {
            return WithWarn(UiText.Fmt("Patch.SummaryWithCanary", Items.Count, canaryCount, readyCount, selectedCount));
        }

        return WithWarn(UiText.Fmt("Patch.SummaryClean", Items.Count, readyCount, selectedCount));
    }

    private string WithWarn(string summary)
    {
        return summary;
    }

    private static string FmtPat(string? raw)
    {
        var localized = PatText.SurfMsg(raw);
        return localized.HasOrig
            ? $"{localized.Body}{Environment.NewLine}{UiText.GetText("Note.OriginalTextLabel")}{Environment.NewLine}{localized.OrigBody}"
            : localized.Body;
    }

    private enum PatSurf
    {
        NoAnlz,
        Error,
        NoPat,
        Loaded
    }
}

public sealed class TgtGrpVm : VmBase
{
    private readonly MatStat _status;
    private readonly string _title;
    private readonly string? _instVer;
    private bool _isOpen;

    public TgtGrpVm(IReadOnlyList<PatItVm> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Patch target group requires at least one item.", nameof(items));
        }

        var first = items[0];
        var target = GetTgt(first.Match);

        Serial = first.Match.TgtSer
            ?? target?.Serial
            ?? first.Match.MatSer
            ?? (first.Match.Patch.Serials.Count == 0 ? null : first.Match.Patch.Serials[0])
            ?? "-";

        Versions = !string.IsNullOrWhiteSpace(first.Match.TgtVer)
            ? [first.Match.TgtVer!]
            : target?.Versions
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];

        _instVer = PatVer.Norm(first.Match.MatVer);
        _title = first.Match.TgtName
            ?? target?.Title
            ?? (first.Match.Patch.GameTgt.Count == 0 ? null : first.Match.Patch.GameTgt[0].Title)
            ?? "-";
        _status = items
            .Select(item => item.MatStat)
            .OrderBy(MatchFor)
            .First();
        _isOpen = _status is
            MatStat.Match or
            MatStat.Unknown;

        foreach (var duplicateSet in items.GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            var duplicates = duplicateSet
                .OrderBy(item => item.Match.Patch.HashKind)
                .ThenBy(item => item.Match.Patch.Hash, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            for (var index = 0; index < duplicates.Length; index++)
            {
                duplicates[index].SetDup(index + 1, duplicates.Length);
            }
        }

        foreach (var item in items
                     .OrderBy(item => item.GroupDsp, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.DspTitle, StringComparer.OrdinalIgnoreCase))
        {
            Items.Add(item);
        }
    }

    public ObservableCollection<PatItVm> Items { get; } = [];

    public string Serial { get; }

    public string Title => _title;

    public IReadOnlyList<string> Versions { get; }

    public string VerDsp => Versions.Count == 0
        ? UiText.GetText("Patch.Version.Any")
        : string.Join(", ", Versions);

    public string InstVer => _instVer ?? UiText.GetText("Ui.Unknown");

    public string SortKey => Versions.Count == 0
        ? "~~~~"
        : string.Join("|", Versions);

    public string Header => _title;

    public string ArtemisHeader => CleanArtemisTitle(_title);

    public int EnabledCount => Items.Count(item => item.IsOn);

    public string Details => UiText.Fmt(
        "Patch.GroupMeta",
        FmtTech(InstVer),
        Items.Count,
        EnabledCount);

    public string CanMeta => UiText.Fmt(
        "Patch.CanaryGroupMeta",
        FmtTech(InstVer),
        Items.Count,
        EnabledCount);

    public string ArtemisMeta => UiText.Fmt(
        "Patch.Artemis.GroupMeta",
        FmtTech(InstVer),
        Items.Count,
        EnabledCount);

    public string CanNote => UiText.Fmt(
        "Patch.CanaryGroupNote",
        FmtTech(InstVer),
        Items.Count);

    public string Tooltip => string.Join(
        Environment.NewLine,
        Header,
        Details,
        UiText.PatMatch(_status));

    public string StatDsp => Items.Any(item => item.IsOn)
        ? UiText.GetText("Patch.State.Enabled")
        : Items.Any(item => item.CanOn && item.IsDef)
            ? UiText.GetText("Patch.State.Disabled")
        : Items.Any(item => item.CanOn)
            ? UiText.GetText("Patch.State.Ready")
            : UiText.PatMatch(_status);

    public string Tone => Items.Any(item => item.IsOn)
        ? "Game"
        : Items.Any(item => item.CanOn && item.IsDef)
            ? "Muted"
            : _status switch
            {
                MatStat.Match => "Auto",
                MatStat.Unknown when Items.Any(item => item.CanOn) => "Auto",
                MatStat.Unknown => "Warning",
                _ => "Muted"
            };

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment TxtAlign => UiLaySvc.Current.TxtAlign;

    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    public int Rank => MatchFor(_status);

    public void SetLang()
    {
        OnChange(nameof(TextFlow));
        OnChange(nameof(TxtAlign));
        OnChange(nameof(VerDsp));
        OnChange(nameof(InstVer));
        OnChange(nameof(Header));
        OnChange(nameof(ArtemisHeader));
        OnChange(nameof(EnabledCount));
        OnChange(nameof(Details));
        OnChange(nameof(CanMeta));
        OnChange(nameof(ArtemisMeta));
        OnChange(nameof(CanNote));
        OnChange(nameof(Tooltip));
        OnChange(nameof(StatDsp));
        OnChange(nameof(Tone));
    }

    private static string CleanArtemisTitle(string title)
    {
        const string suffix = " (Artemis)";
        return title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? title[..^suffix.Length]
            : title;
    }

    public static string BuildKey(PatItVm item)
    {
        var target = GetTgt(item.Match);
        var title = item.Match.TgtName
            ?? target?.Title
            ?? (item.Match.Patch.GameTgt.Count == 0 ? null : item.Match.Patch.GameTgt[0].Title)
            ?? "-";
        var serial = item.Match.TgtSer
            ?? target?.Serial
            ?? item.Match.MatSer
            ?? (item.Match.Patch.Serials.Count == 0 ? null : item.Match.Patch.Serials[0])
            ?? "-";
        var versions = item.Match.TgtVer
            ?? (target is not null && target.Versions.Count > 0
                ? string.Join(
                    "|",
                    target.Versions
                        .Where(version => !string.IsNullOrWhiteSpace(version))
                        .OrderBy(version => version, StringComparer.OrdinalIgnoreCase))
                : PatVer.All);

        return string.Join(
            "\0",
            title,
            serial,
            versions);
    }

    private static R3PatTgt? GetTgt(PatMatch match)
    {
        var serial = match.TgtSer
            ?? match.MatSer
            ?? (match.Patch.Serials.Count == 0 ? null : match.Patch.Serials[0]);

        if (string.IsNullOrWhiteSpace(serial))
        {
            return match.Patch.GameTgt.Count == 0 ? null : match.Patch.GameTgt[0];
        }

        return match.Patch.GameTgt.FirstOrDefault(target =>
                   target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrWhiteSpace(match.TgtName) ||
                    target.Title.Equals(match.TgtName, StringComparison.OrdinalIgnoreCase)))
               ?? (match.Patch.GameTgt.Count == 0 ? null : match.Patch.GameTgt[0]);
    }

    private static int MatchFor(MatStat status)
    {
        return status switch
        {
            MatStat.Match => 0,
            MatStat.Unknown => 1,
            MatStat.VerUnk => 2,
            MatStat.VerBad => 3,
            MatStat.HashBad => 4,
            MatStat.NoMeta => 5,
            _ => 6
        };
    }

    private static string FmtTech(string value)
    {
        return UiText.IsRtl && !string.IsNullOrWhiteSpace(value)
            ? "\u200E" + value + "\u200E"
            : value;
    }
}
