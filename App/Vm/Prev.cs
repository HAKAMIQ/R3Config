using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class PatchPreviewVm : VmBase
{
    private const string DefaultSourceLabel = "patch.yml / imported_patch.yml / bundled Canary";

    private readonly List<Rpcs3PatchMatchResult> _matches = [];
    private string _title = UiText.T("Patch.Title");
    private string _summary = UiText.T("Patch.Empty");
    private string _emptyStateText = UiText.T("Patch.Empty");
    private string _warningText = string.Empty;
    private string _sourceLabel = DefaultSourceLabel;
    private string? _surfaceRawMessage;
    private string? _warningRawMessage;
    private PatchSurfaceState _surfaceState = PatchSurfaceState.NotAnalyzed;
    private bool _showExpertPatches = true;
    private Func<Rpcs3PatchMatchResult, bool>? _isEnabledResolver;

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

    public bool HasSelectedCanaryPatches => Items.Any(item => item.IsSelected && item.CanEnable && item.IsCanary);

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
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        string sourceLabel,
        string? title = null,
        string? warningMessage = null,
        Func<Rpcs3PatchMatchResult, bool>? isEnabledResolver = null)
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

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedMatches()
    {
        return Items
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedDisableMatches()
    {
        return Items
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedCanaryMatches()
    {
        return Items
            .Where(item => item.IsSelected && item.CanEnable && item.IsCanary)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedOfficialMatches()
    {
        return OfficialItems
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedOfficialDisableMatches()
    {
        return OfficialItems
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedCanaryDisableMatches()
    {
        return CanaryItems
            .Where(item => item.IsSelected && item.CanDisable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedCheatMatches()
    {
        return CheatItems
            .Where(item => item.IsSelected && item.CanEnable)
            .Select(item => item.Match)
            .ToArray();
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> SelectedCheatDisableMatches()
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

        foreach (var match in _matches.Where(match => PatchRiskPolicy.IsVisibleInPreview(match.Patch.Risk, ShowExpertPatches)))
        {
            var isEnabled = _isEnabledResolver?.Invoke(match) == true;
            if (Rpcs3PatchVersion.IsAll(match.MatchedTargetSerial) &&
                match.Status != PatchMatchStatus.ExactMatch &&
                !isEnabled)
            {
                // RPCS3 uses All/All/All for hash-scoped modules shared by many
                // games. A non-matching global hash is not evidence that the
                // patch belongs to the selected game, so do not clutter its page.
                continue;
            }

            var item = new PatchItemVm(match, _sourceLabel, isEnabled);
            item.PropertyChanged += PatchItem_PropertyChanged;
            Items.Add(item);

            if (item.IsCanary)
            {
                CanaryItems.Add(item);
            }
            else if (item.Risk == PatchRisk.Cheat)
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
        var exactCount = Items.Count(item => item.MatchStatus == PatchMatchStatus.ExactMatch);
        var canaryCount = Items.Count(item => item.IsCanary);

        if (canaryCount > 0)
        {
            return WithWarning(UiText.F("Patch.SummaryWithCanary", Items.Count, canaryCount, exactCount, selectedCount));
        }

        return WithWarning(UiText.F("Patch.SummaryClean", Items.Count, exactCount, selectedCount));
    }

    private string WithWarning(string summary)
    {
        return summary;
    }

    private static string FormatPatchSurfaceMessage(string? raw)
    {
        var localized = PatchTextLocalizer.LocalizePatchSurfaceMessage(raw);
        return localized.HasOriginalBody
            ? $"{localized.Body}{Environment.NewLine}{UiText.T("Note.OriginalTextLabel")}{Environment.NewLine}{localized.OriginalBody}"
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
    private readonly PatchMatchStatus _bestStatus;
    private readonly string _gameTitle;
    private readonly string? _installedVersion;

    public PatchTargetGroupVm(IReadOnlyList<PatchItemVm> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Patch target group requires at least one item.", nameof(items));
        }

        var first = items[0];
        var target = ResolveTarget(first.Match);

        Serial = first.Match.MatchedTargetSerial
            ?? target?.Serial
            ?? first.Match.MatchedSerial
            ?? first.Match.Patch.GameSerials.FirstOrDefault()
            ?? "-";

        Versions = !string.IsNullOrWhiteSpace(first.Match.MatchedTargetVersion)
            ? [first.Match.MatchedTargetVersion!]
            : target?.Versions
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];

        _installedVersion = Rpcs3PatchVersion.Normalize(first.Match.MatchedVersion);
        _gameTitle = first.Match.MatchedTargetTitle
            ?? target?.Title
            ?? first.Match.Patch.GameTargets.FirstOrDefault()?.Title
            ?? "-";
        _bestStatus = items
            .Select(item => item.MatchStatus)
            .OrderBy(MatchRankFor)
            .First();

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
        "Patch.GroupDetails",
        FormatTechInline(Serial),
        FormatTechInline(InstalledVersionDisplay),
        FormatTechInline(VersionDisplay));

    public string Tooltip => string.Join(
        Environment.NewLine,
        Header,
        Details,
        UiText.PatchMatchLabel(_bestStatus));

    public string StatusDisplay => UiText.PatchMatchLabel(_bestStatus);

    public string StatusTone => _bestStatus switch
    {
        PatchMatchStatus.ExactMatch => "Auto",
        PatchMatchStatus.SerialVersionMatchHashUnknown => "Warning",
        _ => "Muted"
    };

    public bool IsExpanded => _bestStatus is
        PatchMatchStatus.ExactMatch or
        PatchMatchStatus.SerialVersionMatchHashUnknown;

    public int MatchRank => MatchRankFor(_bestStatus);

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(InstalledVersionDisplay));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(StatusDisplay));
    }

    public static string BuildKey(PatchItemVm item)
    {
        var target = ResolveTarget(item.Match);
        var title = item.Match.MatchedTargetTitle
            ?? target?.Title
            ?? item.Match.Patch.GameTargets.FirstOrDefault()?.Title
            ?? "-";
        var serial = item.Match.MatchedTargetSerial
            ?? target?.Serial
            ?? item.Match.MatchedSerial
            ?? item.Match.Patch.GameSerials.FirstOrDefault()
            ?? "-";
        var versions = item.Match.MatchedTargetVersion
            ?? (target is not null && target.Versions.Count > 0
                ? string.Join(
                    "|",
                    target.Versions
                        .Where(version => !string.IsNullOrWhiteSpace(version))
                        .OrderBy(version => version, StringComparer.OrdinalIgnoreCase))
                : Rpcs3PatchVersion.All);

        return string.Join(
            "\0",
            title,
            serial,
            versions);
    }

    private static Rpcs3PatchGameTarget? ResolveTarget(Rpcs3PatchMatchResult match)
    {
        var serial = match.MatchedTargetSerial
            ?? match.MatchedSerial
            ?? match.Patch.GameSerials.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(serial))
        {
            return match.Patch.GameTargets.FirstOrDefault();
        }

        return match.Patch.GameTargets.FirstOrDefault(target =>
                   target.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrWhiteSpace(match.MatchedTargetTitle) ||
                    target.Title.Equals(match.MatchedTargetTitle, StringComparison.OrdinalIgnoreCase)))
               ?? match.Patch.GameTargets.FirstOrDefault();
    }

    private static int MatchRankFor(PatchMatchStatus status)
    {
        return status switch
        {
            PatchMatchStatus.ExactMatch => 0,
            PatchMatchStatus.SerialVersionMatchHashUnknown => 1,
            PatchMatchStatus.VersionUnknown => 2,
            PatchMatchStatus.VersionMismatch => 3,
            PatchMatchStatus.HashMismatch => 4,
            PatchMatchStatus.NoMetadata => 5,
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
