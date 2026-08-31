using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

public sealed class PlanItVm : VmBase
{
    private bool _isSel;

    public PlanItVm(CfgEntry entry, bool isSelected)
    {
        Entry = entry;
        _isSel = isSelected && entry.IsAction;
    }

    public CfgEntry Entry { get; }

    public CfgChg Change => Entry.Change;

    public string Section => Entry.Section;

    public string Key => Entry.Key;

    public string DspPath => Entry.Path;

    public string? CurVal => Entry.CurVal;

    public string CurDsp => Entry.Display;

    public string Val => Entry.Val;

    public string TransDsp => $"{CurDsp} -> {Val}";

    public string? Notes => Entry.Notes;

    public string NoteTag => string.IsNullOrWhiteSpace(Entry.Notes)
        ? UiText.GetText("Ui.EmptyValue")
        : UiText.GetText(TagKey());

    public string NoteName => LocNote;

    public string TypeLine => UiText.Fmt("Plan.NoteTooltipType", NoteType);

    public string NoteOrig => UiText.GetText("Note.OriginalTextLabel");

    public string NoteBody => LocBody;

    public string LocNote => PatText.RecNote(Entry.Notes, Entry.Risk).Title;

    public string NoteType => PatText.RecNote(Entry.Notes, Entry.Risk).Type;

    public string LocBody => PatText.RecNote(Entry.Notes, Entry.Risk).Body;

    public string OrigBody => PatText.RecNote(Entry.Notes, Entry.Risk).OrigBody;

    public bool HasOrig => PatText.RecNote(Entry.Notes, Entry.Risk).HasOrig;

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "WPF binds this presentation property on each item instance.")]
    public FlowDirection NoteFlow =>
        UiLaySvc.Current.TextFlow;

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "WPF binds this presentation property on each item instance.")]
    public TextAlignment NoteText =>
        UiLaySvc.Current.TxtAlign;

    public RecRisk Risk => Entry.Risk;

    public string RiskDsp => UiText.RecRisk(Entry.Risk);

    public string RiskTip => UiText.RecTip(Entry.Risk);

    public string HandDsp => UiText.GetText(Handling(Entry.Risk));

    public string HandTip => UiText.RecTip(Entry.Risk);

    public string Tone => HandTone(Entry.Risk);

    public bool IsDiff => Entry.IsDiff;

    public bool IsAction => Entry.IsAction;

    public bool AutoSel => Entry.IsSel;

    public string Status => Entry.Status;

    public string StatDsp => UiText.RecState(Entry.State);

    public bool IsApply => Entry.State == Mdl.RecState.Applied;

    public bool ShowSelection => !IsApply && IsAction;

    public bool ShowNonActionState => !IsApply && !IsAction;

    public bool HasNote => !string.IsNullOrWhiteSpace(Entry.Notes);

    public string BadgeDsp => IsApply
        ? StatDsp
        : HandDsp;

    public string BadgeTip => IsApply
        ? StatDsp
        : HandTip;

    public string MainTone => IsApply
        ? "Auto"
        : Tone;

    public bool IsSel
    {
        get => _isSel;
        set
        {
            if (!IsAction)
            {
                value = false;
            }

            Set(ref _isSel, value);
        }
    }

    public void SetLang()
    {
        OnChange(nameof(RiskDsp));
        OnChange(nameof(RiskTip));
        OnChange(nameof(HandDsp));
        OnChange(nameof(HandTip));
        OnChange(nameof(Tone));
        OnChange(nameof(StatDsp));
        OnChange(nameof(IsApply));
        OnChange(nameof(ShowSelection));
        OnChange(nameof(ShowNonActionState));
        OnChange(nameof(HasNote));
        OnChange(nameof(BadgeDsp));
        OnChange(nameof(BadgeTip));
        OnChange(nameof(MainTone));
        OnChange(nameof(TransDsp));
        OnChange(nameof(TagKey));
        OnChange(nameof(NoteName));
        OnChange(nameof(TypeLine));
        OnChange(nameof(NoteOrig));
        OnChange(nameof(NoteBody));
        OnChange(nameof(LocNote));
        OnChange(nameof(NoteType));
        OnChange(nameof(NoteBody));
        OnChange(nameof(OrigBody));
        OnChange(nameof(HasOrig));
        OnChange(nameof(NoteFlow));
        OnChange(nameof(NoteText));
    }

    private static string Handling(RecRisk risk)
    {
        return risk switch
        {
            RecRisk.Required or RecRisk.Rec => "Handling.Auto",
            RecRisk.Optional => "Handling.Optional",
            RecRisk.Cond => "Handling.Conditional",
            RecRisk.Dep => "Handling.HardwareDependent",
            RecRisk.Timing => "Handling.ManualTiming",
            RecRisk.PatDep => "Handling.PatchRequired",
            _ => "Handling.NotAutoSelected"
        };
    }

    private static string HandTone(RecRisk risk)
    {
        return risk switch
        {
            RecRisk.Required or RecRisk.Rec => "Auto",
            RecRisk.Optional => "Optional",
            RecRisk.Cond or RecRisk.Timing => "Warning",
            RecRisk.Dep or RecRisk.PatDep => "Info",
            _ => "Muted"
        };
    }

    private string TagKey()
    {
        if (Entry.Risk == RecRisk.Timing)
        {
            return "NoteTag.FpsTiming";
        }

        if (Entry.Risk == RecRisk.Dep)
        {
            return "NoteTag.HardwareBased";
        }

        if (Entry.Risk == RecRisk.PerfCost)
        {
            return "NoteTag.Performance";
        }

        if (Entry.Risk == RecRisk.Cond)
        {
            return "NoteTag.CompatibilityWarning";
        }

        if (Entry.Risk == RecRisk.PatDep)
        {
            return "NoteTag.PatchRequired";
        }

        var notes = Entry.Notes ?? string.Empty;

        if (Entry.Risk == RecRisk.Rec && Contains(notes, "fix", "fixes", "resolves"))
        {
            return "NoteTag.Fix";
        }

        if (Contains(notes, "color", "flicker", "rendering", "graphics", "visual"))
        {
            return "NoteTag.VisualFix";
        }

        if (Entry.Risk == RecRisk.Optional || Contains(notes, "optional"))
        {
            return "NoteTag.Optional";
        }

        return "NoteTag.WikiNote";
    }

    private static bool Contains(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
