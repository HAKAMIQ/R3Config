using System;
using System.Linq;
using System.Windows;
using AppSvc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class PlanItemVm : VmBase
{
    private bool _isSelected;

    public PlanItemVm(ConfigPlanEntry entry, bool isSelected)
    {
        Entry = entry;
        _isSelected = entry.State == RecState.Applied || (isSelected && entry.IsActionable);
    }

    public ConfigPlanEntry Entry { get; }

    public ConfigChange Change => Entry.Change;

    public string Section => Entry.Section;

    public string Key => Entry.Key;

    public string DisplayPath => Entry.DisplayPath;

    public string? CurrentValue => Entry.CurrentValue;

    public string CurrentDisplay => Entry.CurrentDisplay;

    public string ProposedValue => Entry.ProposedValue;

    public string ValueTransitionDisplay => $"{CurrentDisplay} -> {ProposedValue}";

    public string? Notes => Entry.Notes;

    public string NoteTag => string.IsNullOrWhiteSpace(Entry.Notes)
        ? UiText.T("Ui.EmptyValue")
        : UiText.T(NoteTagKey());

    public string NoteTitle => LocalizedNoteTitle;

    public string NoteTypeLine => UiText.F("Plan.NoteTooltipType", LocalizedNoteType);

    public string NoteOriginalLabel => UiText.T("Note.OriginalTextLabel");

    public string NoteBody => LocalizedNoteBody;

    public string LocalizedNoteTitle => PatchTextLocalizer.LocalizeRecommendationNote(Entry.Notes, Entry.Risk).Title;

    public string LocalizedNoteType => PatchTextLocalizer.LocalizeRecommendationNote(Entry.Notes, Entry.Risk).Type;

    public string LocalizedNoteBody => PatchTextLocalizer.LocalizeRecommendationNote(Entry.Notes, Entry.Risk).Body;

    public string LocalizedOriginalBody => PatchTextLocalizer.LocalizeRecommendationNote(Entry.Notes, Entry.Risk).OriginalBody;

    public bool HasOriginalBody => PatchTextLocalizer.LocalizeRecommendationNote(Entry.Notes, Entry.Risk).HasOriginalBody;

    public FlowDirection NoteFlow => UiLayoutService.Current.TextFlow;

    public TextAlignment NoteTextAlignment => UiLayoutService.Current.TextAlignment;

    public RecommendationRisk Risk => Entry.Risk;

    public string RiskDisplay => UiText.RecRiskLabel(Entry.Risk);

    public string RiskTooltip => UiText.RecRiskTip(Entry.Risk);

    public string HandlingDisplay => UiText.T(HandlingKey(Entry.Risk));

    public string HandlingTooltip => UiText.RecRiskTip(Entry.Risk);

    public string HandlingTone => HandlingToneFor(Entry.Risk);

    public bool IsDifferent => Entry.IsDifferent;

    public bool IsActionable => Entry.IsActionable;

    public bool IsAutoSelectable => Entry.IsAutoSelectable;

    public string Status => Entry.Status;

    public string StatusDisplay => UiText.RecStateLabel(Entry.State);

    public bool IsApplied => Entry.State == RecState.Applied;

    public bool HasNote => !string.IsNullOrWhiteSpace(Entry.Notes);

    public string PrimaryBadgeDisplay => IsApplied
        ? StatusDisplay
        : HandlingDisplay;

    public string PrimaryBadgeTooltip => IsApplied
        ? StatusDisplay
        : HandlingTooltip;

    public string PrimaryBadgeTone => IsApplied
        ? "Auto"
        : HandlingTone;

    public string BeginnerSummary => IsApplied
        ? UiText.F("Plan.Item.AppliedSummary", ProposedValue)
        : UiText.F("Plan.Item.PendingSummary", ProposedValue);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Entry.State == RecState.Applied)
            {
                value = true;
            }
            else if (!IsActionable)
            {
                value = false;
            }

            SetProperty(ref _isSelected, value);
        }
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(RiskDisplay));
        OnPropertyChanged(nameof(RiskTooltip));
        OnPropertyChanged(nameof(HandlingDisplay));
        OnPropertyChanged(nameof(HandlingTooltip));
        OnPropertyChanged(nameof(HandlingTone));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(IsApplied));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(PrimaryBadgeDisplay));
        OnPropertyChanged(nameof(PrimaryBadgeTooltip));
        OnPropertyChanged(nameof(PrimaryBadgeTone));
        OnPropertyChanged(nameof(BeginnerSummary));
        OnPropertyChanged(nameof(ValueTransitionDisplay));
        OnPropertyChanged(nameof(NoteTag));
        OnPropertyChanged(nameof(NoteTitle));
        OnPropertyChanged(nameof(NoteTypeLine));
        OnPropertyChanged(nameof(NoteOriginalLabel));
        OnPropertyChanged(nameof(NoteBody));
        OnPropertyChanged(nameof(LocalizedNoteTitle));
        OnPropertyChanged(nameof(LocalizedNoteType));
        OnPropertyChanged(nameof(LocalizedNoteBody));
        OnPropertyChanged(nameof(LocalizedOriginalBody));
        OnPropertyChanged(nameof(HasOriginalBody));
        OnPropertyChanged(nameof(NoteFlow));
        OnPropertyChanged(nameof(NoteTextAlignment));
    }

    private static string HandlingKey(RecommendationRisk risk)
    {
        return risk switch
        {
            RecommendationRisk.Required or RecommendationRisk.Recommended => "Handling.Auto",
            RecommendationRisk.Optional => "Handling.Optional",
            RecommendationRisk.Conditional => "Handling.Conditional",
            RecommendationRisk.HardwareDependent => "Handling.HardwareDependent",
            RecommendationRisk.ManualTiming => "Handling.ManualTiming",
            RecommendationRisk.PatchDependent => "Handling.PatchRequired",
            _ => "Handling.NotAutoSelected"
        };
    }

    private static string HandlingToneFor(RecommendationRisk risk)
    {
        return risk switch
        {
            RecommendationRisk.Required or RecommendationRisk.Recommended => "Auto",
            RecommendationRisk.Optional => "Optional",
            RecommendationRisk.Conditional or RecommendationRisk.ManualTiming => "Warning",
            RecommendationRisk.HardwareDependent or RecommendationRisk.PatchDependent => "Info",
            _ => "Muted"
        };
    }

    private string NoteTagKey()
    {
        if (Entry.Risk == RecommendationRisk.ManualTiming)
        {
            return "NoteTag.FpsTiming";
        }

        if (Entry.Risk == RecommendationRisk.HardwareDependent)
        {
            return "NoteTag.HardwareBased";
        }

        if (Entry.Risk == RecommendationRisk.PerformanceCost)
        {
            return "NoteTag.Performance";
        }

        if (Entry.Risk == RecommendationRisk.Conditional)
        {
            return "NoteTag.CompatibilityWarning";
        }

        if (Entry.Risk == RecommendationRisk.PatchDependent)
        {
            return "NoteTag.PatchRequired";
        }

        var notes = Entry.Notes ?? string.Empty;

        if (Entry.Risk == RecommendationRisk.Recommended && ContainsAny(notes, "fix", "fixes", "resolves"))
        {
            return "NoteTag.Fix";
        }

        if (ContainsAny(notes, "color", "flicker", "rendering", "graphics", "visual"))
        {
            return "NoteTag.VisualFix";
        }

        if (Entry.Risk == RecommendationRisk.Optional || ContainsAny(notes, "optional"))
        {
            return "NoteTag.Optional";
        }

        return "NoteTag.WikiNote";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
