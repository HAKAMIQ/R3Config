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

public sealed class PatchPreviewVm : VmBase
{
    private const string DefaultSourceLabel = "patch.yml / imported_patch.yml / bundled Canary";

    private readonly List<PatMatch> _matches = [];
    private string _title = UiText.T("Patch.Title");
    private string _summary = UiText.T("Patch.Empty");
    private string _emptyStateText = UiText.T("Patch.Empty");
    private string _warningText = string.Empty;
    private string _sourceLabel = DefaultSourceLabel;
    private string? _surfaceRawMessage;
    private string? _warningRawMessage;
    private PatchSurfaceState _surfaceState = PatchSurfaceState.NotAnalyzed;
    private bool _showExpertPatches = true;
    private Func<PatMatch, bool>? _isEnabledResolver;

    public event EventHandler? SelectionStateChanged;

    public ObservableCollection<PatchItemVm> Items { get; } = [];

    public ObservableCollection<PatchItemVm> OfficialItems { get; } = [];

    public ObservableCollection<PatchItemVm> CanaryItems { get; } = [];

    public ObservableCollection<PatchItemVm> CheatItems { get; } = [];

    public ObservableCollection<PatchTargetGroupVm> OfficialGroups { get; } = [];

    public ObservableCollection<PatchTargetGroupVm> CanaryGroups { get; } = [];

    public ObservableCollection<PatchTargetGroupVm> CheatGroups { get; } = [];

    public bool HasSelectedApplyablePatches => Items.Any(item => item.IsSelected && item.CanEnable);

    public bool HasSelectedDisableablePatches => Items.Any(item => item.IsSelected && item.CanDisable);

    public bool HasSelectedCanaryPatches => CanaryItems.Any(item => item.IsSelected && item.CanEnable);

    public bool HasSelectedOfficialApplyablePatches => OfficialItems.Any(item => item.IsSelected && item.CanEnable);

    public bool HasSelectedOfficialDisableablePatches => OfficialItems.Any(item => item.IsSelected && item.CanDisable);

    public bool HasSelectedCanaryApplyablePatches => CanaryItems.Any(item => item.IsSelected && item.CanEnable);

    public bool HasSelectedCanaryDisableablePatches => CanaryItems.Any(item => item.IsSelected && item.CanDisable);

    public bool HasSelectedCheatApplyablePatches => CheatItems.Any(item => item.IsSelected && item.CanEnable);

    public bool HasSelectedCheatDisableablePatches => CheatItems.Any(item => item.IsSelected && item.CanDisable);

    public bool IsEmpty => Items.Count == 0;

    public bool HasItems => Items.Count > 0;

    public bool HasOfficialItems => OfficialItems.Count > 0;

    public bool HasCanaryItems => CanaryItems.Count > 0;

    public bool HasCheatItems => CheatItems.Count > 0;

    public bool HasSelectedOfficialOrLocalPatches => OfficialItems.Any(item => item.IsSelected && item.IsSelectable);

    public string OfficialSummary => BuildSubsetSummary(OfficialItems, "Patch.OfficialSummary");

    public string CanarySummary => BuildSubsetSummary(CanaryItems, "Patch.CanarySummary");

    public string CheatSummary => BuildSubsetSummary(CheatItems, "Patch.CheatSummary");

    public string OfficialActionHint => BuildActionHint(OfficialItems);

    public string CanaryActionHint => BuildActionHint(CanaryItems);

    public string CheatActionHint => BuildActionHint(CheatItems);

    public bool IsOfficialEmpty => OfficialItems.Count == 0;

    public bool IsCanaryEmpty => CanaryItems.Count == 0;

    public bool IsCheatEmpty => CheatItems.Count == 0;

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);

    public string WarningText
    {
        get => _warningText;
        private set
        {
            if (SetProperty(ref _warningText, value))
            {
                OnPropertyChanged(nameof(HasWarning));
            }
        }
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

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

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment TextAlignment => UiLaySvc.Current.TxtAlign;

    public bool ShowExpertPatches
    {
        get => _showExpertPatches;
        set
        {
            if (SetProperty(ref _showExpertPatches, value))
            {
                RefreshVisibleItems();
            }
        }
    }

    public void RefreshLanguage()
    {
        Title = UiText.T("Patch.Title");
        OnPropertyChanged(nameof(TextFlow));
        OnPropertyChanged(nameof(TextAlignment));

        if (_surfaceState == PatchSurfaceState.Loaded)
        {
            // WPF can retain the old FlowDirection inside already-materialized
            // DataTemplate containers after a live Arabic/English switch. Rebuild
            // the item containers under the new application resources while
            // preserving every explicit checkbox state.
            var selection = Items.ToDictionary(
                SelectionIdentity,
                item => item.IsSelected,
                StringComparer.OrdinalIgnoreCase);
            var expansion = OfficialGroups
                .Concat(CanaryGroups)
                .Concat(CheatGroups)
                .ToDictionary(
                    GroupIdentity,
                    group => group.IsExpanded,
                    StringComparer.OrdinalIgnoreCase);

            RefreshVisibleItems();
            foreach (var item in Items)
            {
                if (selection.TryGetValue(SelectionIdentity(item), out var isSelected))
                {
                    item.IsSelected = isSelected;
                }
            }
            foreach (var group in OfficialGroups.Concat(CanaryGroups).Concat(CheatGroups))
            {
                if (expansion.TryGetValue(GroupIdentity(group), out var isExpanded))
                {
                    group.IsExpanded = isExpanded;
                }
            }

            return;
        }

        switch (_surfaceState)
        {
            case PatchSurfaceState.NotAnalyzed:
                Summary = UiText.T("Patch.Empty");
                EmptyStateText = UiText.T("Patch.Empty");
                break;

            case PatchSurfaceState.Error:
                var message = FormatPatchSurfaceMessage(_surfaceRawMessage);
                Summary = message;
                EmptyStateText = message;
                break;

            case PatchSurfaceState.NoPatchesForGame:
                Summary = WithWarning(UiText.T("Patch.NoneForGame"));
                EmptyStateText = UiText.T("Patch.NoneForGame");
                break;

            case PatchSurfaceState.Loaded:
                EmptyStateText = Items.Count == 0 ? UiText.T("Patch.EmptyVisible") : string.Empty;
                Summary = BuildSummary();
                break;
        }

        foreach (var item in Items)
        {
            item.RefreshLanguage();
        }

        foreach (var group in OfficialGroups.Concat(CanaryGroups).Concat(CheatGroups))
        {
            group.RefreshLanguage();
        }
    }

    private static string SelectionIdentity(PatchItemVm item)
    {
        return string.Join(
            "\0",
            item.Match.Patch.HashKind,
            item.Match.Patch.Hash,
            item.Match.Patch.Title,
            PatchTargetGroupVm.BuildKey(item));
    }

    private static string GroupIdentity(PatchTargetGroupVm group)
    {
        var first = group.Items[0];
        var surface = first.IsExperimentalPatch
            ? "Canary"
            : first.Risk == Mdl.PatRisk.Cheat
                ? "Cheat"
                : "Official";
        return string.Join("\0", surface, PatchTargetGroupVm.BuildKey(first));
    }

    public void Clear()
    {
        _matches.Clear();
        Items.Clear();
        OfficialItems.Clear();
        CanaryItems.Clear();
        CheatItems.Clear();
        OfficialGroups.Clear();
        CanaryGroups.Clear();
        CheatGroups.Clear();
        _sourceLabel = DefaultSourceLabel;
        _surfaceRawMessage = null;
        _warningRawMessage = null;
        WarningText = string.Empty;
        _surfaceState = PatchSurfaceState.NotAnalyzed;
        _isEnabledResolver = null;

        Title = UiText.T("Patch.Title");
        Summary = UiText.T("Patch.Empty");
        EmptyStateText = UiText.T("Patch.Empty");

        OnPropertyChanged(nameof(IsEmpty));
        RaiseSelectionChanged();
    }

    public void Load(
        IReadOnlyList<PatMatch> matches,
        string sourceLabel,
        string? title = null,
        string? warningMessage = null,
        Func<PatMatch, bool>? isEnabledResolver = null)
    {
        _matches.Clear();
        _matches.AddRange(matches);

        _sourceLabel = string.IsNullOrWhiteSpace(sourceLabel)
            ? DefaultSourceLabel
            : sourceLabel;

        _surfaceRawMessage = null;
        _warningRawMessage = warningMessage;
        WarningText = warningMessage?.Trim() ?? string.Empty;
        _isEnabledResolver = isEnabledResolver;
        _surfaceState = matches.Count == 0
            ? PatchSurfaceState.NoPatchesForGame
            : PatchSurfaceState.Loaded;

        Title = title ?? UiText.T("Patch.Title");
        RefreshVisibleItems();

        if (_surfaceState == PatchSurfaceState.NoPatchesForGame)
        {
            EmptyStateText = UiText.T("Patch.NoneForGame");
            Summary = BuildSummary();
        }
        else
        {
            EmptyStateText = Items.Count == 0 ? UiText.T("Patch.EmptyVisible") : string.Empty;
            Summary = BuildSummary();
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public void LoadError(string message)
    {
        _matches.Clear();
        Items.Clear();
        OfficialItems.Clear();
        CanaryItems.Clear();
        CheatItems.Clear();
        OfficialGroups.Clear();
        CanaryGroups.Clear();
        CheatGroups.Clear();
        _sourceLabel = DefaultSourceLabel;
        _surfaceRawMessage = message;
        _warningRawMessage = null;
        WarningText = string.Empty;
        _surfaceState = PatchSurfaceState.Error;
        _isEnabledResolver = null;

        Title = UiText.T("Patch.Title");

        var localizedMessage = FormatPatchSurfaceMessage(message);
        Summary = localizedMessage;
        EmptyStateText = localizedMessage;

        OnPropertyChanged(nameof(IsEmpty));
        RaiseSelectionChanged();
    }

    public IReadOnlyList<PatMatch> SelectedMatches()
    {
        return Items
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedDisableMatches()
    {
        return Items
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedCanaryMatches()
    {
        return CanaryItems
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedOfficialMatches()
    {
        return OfficialItems
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedOfficialDisableMatches()
    {
        return OfficialItems
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedCanaryDisableMatches()
    {
        return CanaryItems
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedCheatMatches()
    {
        return CheatItems
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<PatMatch> SelectedCheatDisableMatches()
    {
        return CheatItems
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    private void RefreshVisibleItems()
    {
        Items.Clear();
        OfficialItems.Clear();
        CanaryItems.Clear();
        CheatItems.Clear();
        OfficialGroups.Clear();
        CanaryGroups.Clear();
        CheatGroups.Clear();

        foreach (var match in _matches.Where(match => PatPol.Is(match.Patch.Risk, ShowExpertPatches)))
        {
            var isEnabled = _isEnabledResolver?.Invoke(match) == true;
            if (PatVer.IsAll(match.TgtSer) &&
                match.Status != MatStat.Match)
            {
                // RPCS3 uses All/All/All for hash-scoped modules shared by many
                // games. Enabled state in patch_config.yml is global too, so it
                // cannot prove that the patch belongs to the selected game.
                // Only exact local runtime evidence may place it on a game page.
                continue;
            }

            var item = new PatchItemVm(match, _sourceLabel, isEnabled);
            item.PropertyChanged += PatchItem_PropertyChanged;
            Items.Add(item);

            if (item.IsExperimentalPatch)
            {
                CanaryItems.Add(item);
            }
            else if (item.Risk == PatRisk.Cheat)
            {
                CheatItems.Add(item);
            }
            else
            {
                OfficialItems.Add(item);
            }
        }

        BuildTargetGroups(OfficialItems, OfficialGroups);
        BuildTargetGroups(CanaryItems, CanaryGroups);
        BuildTargetGroups(CheatItems, CheatGroups);

        if (_matches.Count > 0)
        {
            EmptyStateText = Items.Count == 0 ? UiText.T("Patch.EmptyVisible") : string.Empty;
            OnPropertyChanged(nameof(IsEmpty));
            Summary = BuildSummary();
        }

        RaiseSelectionChanged();
    }

    private static void BuildTargetGroups(
        IReadOnlyCollection<PatchItemVm> source,
        ObservableCollection<PatchTargetGroupVm> destination)
    {
        foreach (var group in source
                     .GroupBy(PatchTargetGroupVm.BuildKey, StringComparer.OrdinalIgnoreCase)
                     .Select(items => new PatchTargetGroupVm(items.ToArray()))
                     .OrderBy(group => group.MatchRank)
                     .ThenBy(group => group.GameTitle, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.Serial, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.VersionSortKey, StringComparer.OrdinalIgnoreCase))
        {
            destination.Add(group);
        }
    }

    private void PatchItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatchItemVm.IsSelected))
        {
            Summary = BuildSummary();
            RaiseSelectionChanged();
        }
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasOfficialItems));
        OnPropertyChanged(nameof(HasCanaryItems));
        OnPropertyChanged(nameof(HasCheatItems));
        OnPropertyChanged(nameof(OfficialGroups));
        OnPropertyChanged(nameof(CanaryGroups));
        OnPropertyChanged(nameof(CheatGroups));
        OnPropertyChanged(nameof(OfficialSummary));
        OnPropertyChanged(nameof(CanarySummary));
        OnPropertyChanged(nameof(CheatSummary));
        OnPropertyChanged(nameof(OfficialActionHint));
        OnPropertyChanged(nameof(CanaryActionHint));
        OnPropertyChanged(nameof(CheatActionHint));
        OnPropertyChanged(nameof(IsOfficialEmpty));
        OnPropertyChanged(nameof(IsCanaryEmpty));
        OnPropertyChanged(nameof(IsCheatEmpty));
        OnPropertyChanged(nameof(HasSelectedApplyablePatches));
        OnPropertyChanged(nameof(HasSelectedDisableablePatches));
        OnPropertyChanged(nameof(HasSelectedCanaryPatches));
        OnPropertyChanged(nameof(HasSelectedOfficialApplyablePatches));
        OnPropertyChanged(nameof(HasSelectedOfficialDisableablePatches));
        OnPropertyChanged(nameof(HasSelectedCanaryApplyablePatches));
        OnPropertyChanged(nameof(HasSelectedCanaryDisableablePatches));
        OnPropertyChanged(nameof(HasSelectedCheatApplyablePatches));
        OnPropertyChanged(nameof(HasSelectedCheatDisableablePatches));
        OnPropertyChanged(nameof(HasSelectedOfficialOrLocalPatches));
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildSubsetSummary(IReadOnlyCollection<PatchItemVm> items, string key)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var selectedCount = items.Count(item => item.IsSelected);
        var actionableCount = items.Count(item => item.IsSelectable);
        return UiText.F(key, items.Count, actionableCount, selectedCount);
    }

    private static string BuildActionHint(IReadOnlyCollection<PatchItemVm> items)
    {
        var enableCount = items.Count(item => item.IsSelected && item.CanEnable);
        var disableCount = items.Count(item => item.IsSelected && item.CanDisable);
        if (enableCount > 0 || disableCount > 0)
        {
            return UiText.F("Patch.ActionHint.Selected", enableCount, disableCount);
        }

        return items.Any(item => item.IsSelectable)
            ? UiText.T("Patch.ActionHint.Select")
            : UiText.T("Patch.ActionHint.None");
    }

    private string BuildSummary()
    {
        if (_matches.Count == 0 || Items.Count == 0)
        {
            return string.Empty;
        }

        var selectedCount = Items.Count(item => item.IsSelected);
        var readyCount = Items.Count(item => item.CanEnable || item.CanDisable);
        var canaryCount = Items.Count(item => item.IsExperimentalPatch);

        if (canaryCount > 0)
        {
            return WithWarning(UiText.F("Patch.SummaryWithCanary", Items.Count, canaryCount, readyCount, selectedCount));
        }

        return WithWarning(UiText.F("Patch.SummaryClean", Items.Count, readyCount, selectedCount));
    }

    private string WithWarning(string summary)
    {
        return summary;
    }

    private static string FormatPatchSurfaceMessage(string? raw)
    {
        var localized = PatText.SurfMsg(raw);
        return localized.HasOrig
            ? $"{localized.Body}{Environment.NewLine}{UiText.T("Note.OriginalTextLabel")}{Environment.NewLine}{localized.OrigBody}"
            : localized.Body;
    }

    private enum PatchSurfaceState
    {
        NotAnalyzed,
        Error,
        NoPatchesForGame,
        Loaded
    }
}

public sealed class PatchTargetGroupVm : VmBase
{
    private readonly MatStat _bestStatus;
    private readonly string _gameTitle;
    private readonly string? _installedVersion;
    private bool _isExpanded;

    public PatchTargetGroupVm(IReadOnlyList<PatchItemVm> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Patch target group requires at least one item.", nameof(items));
        }

        var first = items[0];
        var target = ResolveTarget(first.Match);

        Serial = first.Match.TgtSer
            ?? target?.Serial
            ?? first.Match.MatSer
            ?? first.Match.Patch.Serials.FirstOrDefault()
            ?? "-";

        Versions = !string.IsNullOrWhiteSpace(first.Match.TgtVer)
            ? [first.Match.TgtVer!]
            : target?.Versions
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];

        _installedVersion = PatVer.Norm(first.Match.MatVer);
        _gameTitle = first.Match.TgtName
            ?? target?.Title
            ?? first.Match.Patch.GameTgt.FirstOrDefault()?.Title
            ?? "-";
        _bestStatus = items
            .Select(item => item.MatchStatus)
            .OrderBy(MatchRankFor)
            .First();
        _isExpanded = _bestStatus is
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
                duplicates[index].SetDuplicateOrdinal(index + 1, duplicates.Length);
            }
        }

        foreach (var item in items
                     .OrderBy(item => item.GroupDisplay, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.DisplayTitle, StringComparer.OrdinalIgnoreCase))
        {
            Items.Add(item);
        }
    }

    public ObservableCollection<PatchItemVm> Items { get; } = [];

    public string Serial { get; }

    public string GameTitle => _gameTitle;

    public IReadOnlyList<string> Versions { get; }

    public string VersionDisplay => Versions.Count == 0
        ? UiText.T("Patch.Version.Any")
        : string.Join(", ", Versions);

    public string InstalledVersionDisplay => _installedVersion ?? UiText.T("Ui.Unknown");

    public string VersionSortKey => Versions.Count == 0
        ? "~~~~"
        : string.Join("|", Versions);

    public string Header => _gameTitle;

    public string Details => UiText.F(
        "Patch.GroupUserDetails",
        FormatTechInline(InstalledVersionDisplay),
        Items.Count);

    public string Tooltip => string.Join(
        Environment.NewLine,
        Header,
        Details,
        UiText.PatchMatchLabel(_bestStatus));

    public string StatusDisplay => Items.Any(item => item.IsEnabled)
        ? UiText.T("Patch.State.Enabled")
        : Items.Any(item => item.CanEnable && item.IsDefinitionResident)
            ? UiText.T("Patch.State.Disabled")
        : Items.Any(item => item.CanEnable)
            ? UiText.T("Patch.State.Ready")
            : UiText.PatchMatchLabel(_bestStatus);

    public string StatusTone => Items.Any(item => item.IsEnabled)
        ? "Game"
        : Items.Any(item => item.CanEnable && item.IsDefinitionResident)
            ? "Muted"
            : _bestStatus switch
            {
                MatStat.Match => "Auto",
                MatStat.Unknown when Items.Any(item => item.CanEnable) => "Auto",
                MatStat.Unknown => "Warning",
                _ => "Muted"
            };

    public FlowDirection TextFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment TextAlignment => UiLaySvc.Current.TxtAlign;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int MatchRank => MatchRankFor(_bestStatus);

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(TextFlow));
        OnPropertyChanged(nameof(TextAlignment));
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(InstalledVersionDisplay));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusTone));
    }

    public static string BuildKey(PatchItemVm item)
    {
        var target = ResolveTarget(item.Match);
        var title = item.Match.TgtName
            ?? target?.Title
            ?? item.Match.Patch.GameTgt.FirstOrDefault()?.Title
            ?? "-";
        var serial = item.Match.TgtSer
            ?? target?.Serial
            ?? item.Match.MatSer
            ?? item.Match.Patch.Serials.FirstOrDefault()
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

    private static R3PatTgt? ResolveTarget(PatMatch match)
    {
        var serial = match.TgtSer
            ?? match.MatSer
            ?? match.Patch.Serials.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(serial))
        {
            return match.Patch.GameTgt.FirstOrDefault();
        }

        return match.Patch.GameTgt.FirstOrDefault(target =>
                   target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrWhiteSpace(match.TgtName) ||
                    target.Title.Equals(match.TgtName, StringComparison.OrdinalIgnoreCase)))
               ?? match.Patch.GameTgt.FirstOrDefault();
    }

    private static int MatchRankFor(MatStat status)
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

    private static string FormatTechInline(string value)
    {
        return UiText.IsRtl && !string.IsNullOrWhiteSpace(value)
            ? "\u200E" + value + "\u200E"
            : value;
    }
}
