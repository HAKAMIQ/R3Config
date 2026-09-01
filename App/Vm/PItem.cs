using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

public enum PatSrcKd
{
    Official,
    Canary,
    Artemis,
    Local,
    Custom,
    Missing,
    Unknown
}

public sealed class PatDepVm(PatDepGroup dependency)
{
    public string Label => UiText.GetText(
        dependency.Mode == PatDepMode.All
            ? "Patch.Dependency.All"
            : "Patch.Dependency.Any");

    public string Titles => string.Join(", ", dependency.Titles);

    public FlowDirection LabelFlow => UiLaySvc.Current.TextFlow;

    public TextAlignment LabelAlignment => UiLaySvc.Current.TxtAlign;

    public FlowDirection TechnicalFlow => UiLaySvc.Current.TechFlow;
}

public sealed class PatItVm : VmBase
{
    private readonly bool _isOn;
    private bool _isSel;
    private int _duplicateOrdinal = 1;
    private int _duplicateCount = 1;

    public PatItVm(PatMatch match, bool isEnabled = false)
    {
        Match = match;
        _isOn = isEnabled;
        CanOn = !isEnabled && Svc.PatPol.Can(match);
        CanOff = isEnabled && Svc.PatPol.CanPat(match);
        CanSel = CanOn || CanOff;
        _isSel = !isEnabled && Svc.PatPol.Should(match.Patch.Risk, match.Status);
    }

    public PatMatch Match { get; }

    public string Title => Match.Patch.Title;

    public string DspTitle
    {
        get
        {
            var localized = PatText.DspTitle(Title);
            return _duplicateCount > 1
                ? $"{localized} ({_duplicateOrdinal:00})"
                : localized;
        }
    }

    public Mdl.PatRisk Risk => Match.Patch.Risk;

    public string RiskDsp => UiText.PatRisk(Match.Patch.Risk);

    public string RiskTip => UiText.PatTip(Match.Patch.Risk, Match.Status);

    public string HandDsp => UiText.GetText(Handling(Match.Patch.Risk));

    public string HandTip => UiText.PatTip(Match.Patch.Risk, Match.Status);

    public string Tone => HandTone(Match.Patch.Risk);

    public string PatType => Match.Patch.PatType;

    public string TypeDsp => UiText.PatType(Match.Patch.PatType);

    public string PatTitle => Title;

    public string PatDesc => LocMsg;

    public string Desc => string.IsNullOrWhiteSpace(LocMsg)
        ? UiText.GetText("Patch.NoShortDescription")
        : LocMsg;

    public bool HasDesc => !string.IsNullOrWhiteSpace(LocMsg);

    public IReadOnlyList<PatDepVm> Dependencies =>
    [
        .. Match.Patch.Dependencies.Select(
            dependency => new PatDepVm(dependency))
    ];

    public bool HasDependencies => Dependencies.Count > 0;

    public PatSafetyAssessment Safety =>
        PatSafe.Eval(Match.Patch.Notes);

    public bool HasSafety => Safety.HasRisk;

    public string SafetyLabel => PatText.SafetyLabel(Safety);

    public string SafetyText => PatText.SafetyText(Safety);

    public PatSrcKd PatSrcKd => InferSrc(Match.Patch);

    public string SrcDsp => UiText.GetText(SrcKey(PatSrcKd));

    public string SrcTip => IsCanary
        ? UiText.GetText("Patch.CanaryNotice")
        : UiText.Fmt("Patch.Source.Tooltip", SrcDsp);

    public string SrcTone => PatSrcKd switch
    {
        PatSrcKd.Official => "Game",
        PatSrcKd.Canary => "Canary",
        PatSrcKd.Artemis => "Custom",
        PatSrcKd.Custom => "Custom",
        PatSrcKd.Local => "Local",
        PatSrcKd.Missing => "Warning",
        _ => "Muted"
    };


    public string ScopeDsp => UiText.Fmt("Patch.CanaryScope", SerDsp, CanVer);

    public string ScopeTip => UiText.Fmt("Patch.CanaryScopeTip", SerDsp, CanVer, HashStat);

    public string CanVer => !string.IsNullOrWhiteSpace(Match.TgtVer)
        ? Match.TgtVer!
        : IsVer
            ? string.Join(", ", Match.Patch.GameVer)
            : UiText.GetText("Patch.Version.Any");

    public bool IsVer => Match.Patch.GameVer.Any(version => !string.IsNullOrWhiteSpace(version));

    public bool IsOfcl => PatSrcKd == PatSrcKd.Official;

    public bool IsCanary => Match.Patch.Class == Mdl.PatClass.Canary;

    public bool IsArtemis => Match.Patch.Source is
        PatSrc.Artemis or
        PatSrc.ManagedArtemis;

    public bool IsExpPat => IsCanary;

    public bool IsLocal => PatSrcKd == PatSrcKd.Local;

    public bool IsCustom => PatSrcKd == PatSrcKd.Custom;

    public bool IsAvail => PatSrcKd is not PatSrcKd.Missing and not PatSrcKd.Unknown;

    public bool IsDef => Match.Patch.Source is
        PatSrc.Official or
        PatSrc.Imported or
        PatSrc.ManagedArtemis or
        PatSrc.TitlePat;

    public bool HasDef => Match.Patch.Source switch
    {
        PatSrc.Can => true,
        PatSrc.Imported => true,
        PatSrc.Artemis or PatSrc.ManagedArtemis => true,
        PatSrc.TitlePat => Risk == Mdl.PatRisk.Cheat,
        _ => false
    };

    public string DefState => Match.Patch.Source switch
    {
        PatSrc.Can => UiText.GetText("Patch.Definition.NotMerged"),
        PatSrc.Imported => UiText.GetText("Patch.Definition.Merged"),
        PatSrc.Artemis => UiText.GetText("Patch.Definition.NotPrepared"),
        PatSrc.ManagedArtemis => UiText.GetText("Patch.Definition.Prepared"),
        PatSrc.TitlePat when Risk == Mdl.PatRisk.Cheat => UiText.GetText("Patch.Definition.Existing"),
        _ => string.Empty
    };

    public string DefTone => IsDef ? "Game" : "Muted";

    public string BadgeDsp => SrcDsp;

    public string MainTone => SrcTone;

    public string BadgeTip => SrcTip;

    public bool HasScope => IsCanary;

    public string GameDsp => UiText.Fmt("Patch.GameScope", SerDsp);

    public string GameTip => UiText.Fmt("Patch.GameScopeTip", SerDsp, CanVer);

    public string Benefit => string.IsNullOrWhiteSpace(LocMsg)
        ? UiText.GetText("Patch.PlayerBenefitUnknown")
        : UiText.Fmt("Patch.PlayerBenefit", Desc);

    public string SrcText => IsCanary
        ? UiText.GetText("Patch.PlayerSourceCanary")
        : UiText.Fmt("Patch.PlayerSource", SrcDsp);

    public string ScopeTxt => UiText.Fmt("Patch.PlayerScope", FmtTech(SerDsp), FmtTech(CanVer));

    public string ActText => IsOn && CanOff
        ? UiText.GetText("Patch.PlayerActionEnabled")
        : CanOn
            ? UiText.GetText(IsCanary ? "Patch.PlayerActionCanarySelectable" : "Patch.PlayerActionSelectable")
            : MatchTip;

    public bool IsGuard =>
        PatAct.IsRun(Match);

    public PatchCompatibilityDecision Decision =>
        PatAct.Decide(Match);

    public bool HasIns =>
        PatText.PatMsg(Notes).IsInstruction &&
        !string.IsNullOrWhiteSpace(LocMsg);

    public string InsText => HasIns
        ? LocMsg
        : string.Empty;

    public string InstVer => PatVer.Norm(Match.MatVer)
        ?? UiText.GetText("Ui.Unknown");

    public string ReqVer => PatVer.Norm(Match.TgtVer)
        ?? (Match.Patch.GameVer.Count > 0
            ? string.Join(", ", Match.Patch.GameVer)
            : UiText.GetText("Patch.Version.Any"));

    public string TgtDsp => Match.TgtSer
        ?? Match.MatSer
        ?? JoinOr(Match.Patch.Serials);

    public string KindDsp => Match.Patch.HashKind switch
    {
        HashKind.Ppu => "PPU",
        HashKind.Spu => "SPU",
        HashKind.Prx => "PRX",
        HashKind.Ovl => "OVL",
        _ => "HASH"
    };

    private string CmpHash =>
        Match.Patch.NormHash ?? Match.Patch.Hash;

    public string Hash => string.IsNullOrWhiteSpace(CmpHash)
        ? "-"
        : CmpHash[..Math.Min(12, CmpHash.Length)];

    public string TechTgt => $"{KindDsp}-{Hash}";

    public bool HasEvi => Match.Patch.HashKind != HashKind.Unknown;

    public string HashDsp => $"{KindDsp}-{CmpHash}";

    public string DetHash => Match.Hashes.Count == 0
        ? UiText.GetText("Ui.Unknown")
        : string.Join(", ", Match.Hashes.Select(hash => $"{KindDsp}-{hash}"));

    public string HashSrc
    {
        get
        {
            return Match.HashEvi switch
            {
                HashEvi.PlainLog or
                HashEvi.ZipLog =>
                    UiText.GetText("PatchReadiness.Source.Log"),

                HashEvi.Cache =>
                    UiText.GetText("PatchReadiness.Source.Cache"),

                HashEvi.Meta =>
                    UiText.GetText("PatchReadiness.Source.Metadata"),

                _ => string.IsNullOrWhiteSpace(Match.HashSrc)
                    ? UiText.GetText("Ui.Unknown")
                    : UiText.GetText("PatchReadiness.Source.Metadata")
            };
        }
    }

    public string EviSum
    {
        get
        {
            if (IsGuard)
            {
                return UiText.GetText("Patch.HashEvidence.RuntimeGuarded");
            }

            if (Match.Hashes.Any(hash =>
                    hash.Equals(
                        Match.Patch.NormHash,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return UiText.Fmt(
                    "Patch.HashEvidence.Proven",
                    FmtTech(HashDsp),
                    HashSrc);
            }

            return Match.Hashes.Count > 0
                ? UiText.Fmt(
                    "Patch.HashEvidence.Rejected",
                    FmtTech(HashDsp),
                    FmtTech(DetHash))
                : UiText.Fmt(
                    "Patch.HashEvidence.Missing",
                    FmtTech(HashDsp));
        }
    }

    public string EviDet => UiText.Fmt(
        "Patch.HashEvidence.Details",
        FmtTech(HashDsp),
        FmtTech(DetHash),
        HashSrc);

    public string GroupDsp => string.IsNullOrWhiteSpace(Match.Patch.Group)
        ? TypeDsp
        : Match.Patch.Group!;

    public string TgtLine => UiText.Fmt(
        "Patch.TargetLine",
        FmtTech(TgtDsp),
        FmtTech(InstVer),
        FmtTech(ReqVer));

    public string DirStat
    {
        get
        {
            if (IsOn)
            {
                return UiText.GetText("Patch.Direct.Enabled");
            }

            var compatibilityKey = Decision.Code switch
            {
                PatchCompatibilityReasonCode.Key =>
                    "Patch.Direct.InvalidRuntimeKey",
                PatchCompatibilityReasonCode.KeySerBd =>
                    "Patch.Direct.RuntimeKeySerialMismatch",
                PatchCompatibilityReasonCode.Guard =>
                    "Patch.Direct.UntrustedRuntimeGuard",
                PatchCompatibilityReasonCode.Hash
                    when IsCanary =>
                    "Patch.Direct.CanaryHashRequired",
                PatchCompatibilityReasonCode.Hash =>
                    "Patch.Direct.GlobalNeedsExactHash",
                _ => null
            };

            if (compatibilityKey is not null)
            {
                return UiText.GetText(compatibilityKey);
            }

            if (CanOn && IsDef)
            {
                return Match.Patch.Source switch
                {
                    PatSrc.Imported when IsExpPat =>
                        UiText.GetText("Patch.Direct.MergedDisabled"),
                    PatSrc.ManagedArtemis =>
                        UiText.GetText("Patch.Direct.PreparedDisabled"),
                    PatSrc.TitlePat when Risk == Mdl.PatRisk.Cheat =>
                        UiText.GetText("Patch.Direct.PreparedDisabled"),
                    _ => UiText.GetText("Patch.Direct.Disabled")
                };
            }

            return MatStat switch
            {
                MatStat.Match => UiText.GetText("Patch.Direct.Ready"),
                MatStat.Unknown or MatStat.HashBad when IsGuard =>
                    UiText.GetText("Patch.Direct.RuntimeGuarded"),
                MatStat.Unknown => UiText.Fmt("Patch.Direct.HashUnknown", KindDsp),
                MatStat.VerUnk => UiText.Fmt("Patch.Direct.VersionUnknown", ReqVer),
                MatStat.VerBad => UiText.Fmt(
                    "Patch.Direct.VersionMismatch",
                    InstVer,
                    ReqVer),
                MatStat.HashBad when IsCanary =>
                    UiText.GetText("Patch.Direct.CanaryHashMismatch"),
                MatStat.HashBad => UiText.GetText(
                    "Patch.Direct.HashMismatch"),
                _ => UiText.GetText("Patch.Direct.Unavailable")
            };
        }
    }

    public string StateDsp => IsOn
        ? UiText.GetText("Patch.State.Enabled")
        : CanOn
            ? UiText.GetText(IsOfcl || IsDef
                ? "Patch.State.Disabled"
                : "Patch.State.Ready")
            : UiText.GetText("Patch.State.Blocked");

    public string CurrentStateDsp => UiText.Fmt(
        "Patch.State.Current",
        StateDsp);

    public string StateTon => IsOn
        ? "Game"
        : CanOn
            ? IsOfcl || IsDef ? "Muted" : "Auto"
            : "Warning";

    public bool HasPendingAction => IsSel && CanSel;

    public string PendingDsp => !HasPendingAction
        ? string.Empty
        : UiText.GetText(CanOff
            ? "Patch.Pending.Disable"
            : "Patch.Pending.Enable");

    public string PendingTone => CanOff ? "Warning" : "Auto";

    public string CanNote
    {
        get
        {
            if (IsOn)
            {
                return UiText.GetText(
                    "Patch.CanaryNote.Enabled");
            }

            if (CanOn && IsDef)
            {
                return UiText.GetText(
                    "Patch.CanaryNote.Merged");
            }

            if (CanOn)
            {
                return UiText.GetText(
                    "Patch.CanaryNote.Ready");
            }

            return MatStat switch
            {
                MatStat.HashBad =>
                    UiText.GetText(
                        "Patch.CanaryNote.HashMismatch"),
                MatStat.VerBad =>
                    UiText.Fmt(
                        "Patch.CanaryNote.VersionMismatch",
                        InstVer,
                        ReqVer),
                MatStat.VerUnk =>
                    UiText.Fmt(
                        "Patch.CanaryNote.VersionUnknown",
                        ReqVer),
                MatStat.Unknown when Decision.Code ==
                    PatchCompatibilityReasonCode.Hash =>
                    UiText.GetText(
                        "Patch.CanaryNote.NeedsHash"),
                _ => UiText.GetText(
                    "Patch.CanaryNote.Unavailable")
            };
        }
    }

    public string DetLine => UiText.Fmt(
        "Patch.DetailsLine",
        SrcDsp,
        MatchDsp,
        TypeDsp,
        HashStat);

    public bool IsOn => _isOn;

    public string Status => MatchDsp;

    public string SerDsp => Match.MatSer
        ?? JoinOr(Match.Patch.Serials);

    public string VerDsp => Match.MatVer
        ?? JoinOr(Match.Patch.GameVer);

    public string HashStat => UiText.HashStat(Match.Status, Match.Patch.HashKind);

    public MatStat MatStat => Match.Status;

    public string MatchDsp => UiText.PatMatch(Match.Status);

    public string MatchTip => UiText.MatchTip(Match.Status);

    public string? Notes => Match.Patch.Notes;

    public string NoteTag => string.IsNullOrWhiteSpace(Match.Patch.Notes)
        ? UiText.GetText("Ui.EmptyValue")
        : UiText.GetText(TagKey());

    public string NoteName => LocNote;

    public string TypeLine => UiText.Fmt("Plan.NoteTooltipType", MsgType);

    public string NoteOrig => UiText.GetText("Note.OriginalTextLabel");

    public string NoteBody => LocMsg;

    public string LocNote => PatText.PatMsg(Match.Patch.Notes).Title;

    public string MsgType => PatText.PatMsg(Match.Patch.Notes).Type;

    public string LocMsg => PatText.PatMsg(Match.Patch.Notes).Body;

    public string OrigMsg => PatText.PatMsg(Match.Patch.Notes).OrigBody;

    public bool HasOrig => PatText.PatMsg(Match.Patch.Notes).HasOrig;

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "WPF binds this presentation property on each item instance.")]
    public FlowDirection TitleFlow =>
        UiLaySvc.Current.TechFlow;

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

    public bool CanSel { get; }

    public bool CanOn { get; }

    public bool CanOff { get; }

    public bool IsSel
    {
        get => _isSel;
        set
        {
            if (!CanSel)
            {
                value = false;
            }

            if (Set(ref _isSel, value))
            {
                OnChange(nameof(HasPendingAction));
                OnChange(nameof(PendingDsp));
                OnChange(nameof(PendingTone));
            }
        }
    }

    public void SetLang()
    {
        OnChange(nameof(RiskDsp));
        OnChange(nameof(RiskTip));
        OnChange(nameof(HandDsp));
        OnChange(nameof(HandTip));
        OnChange(nameof(Tone));
        OnChange(nameof(TypeDsp));
        OnChange(nameof(PatTitle));
        OnChange(nameof(DspTitle));
        OnChange(nameof(PatDesc));
        OnChange(nameof(Desc));
        OnChange(nameof(HasDesc));
        OnChange(nameof(Dependencies));
        OnChange(nameof(HasDependencies));
        OnChange(nameof(Safety));
        OnChange(nameof(HasSafety));
        OnChange(nameof(SafetyLabel));
        OnChange(nameof(SafetyText));
        OnChange(nameof(PatSrcKd));
        OnChange(nameof(SrcDsp));
        OnChange(nameof(SrcTip));
        OnChange(nameof(SrcTone));
        OnChange(nameof(ScopeDsp));
        OnChange(nameof(ScopeTip));
        OnChange(nameof(CanVer));
        OnChange(nameof(IsVer));
        OnChange(nameof(IsOfcl));
        OnChange(nameof(IsCanary));
        OnChange(nameof(IsArtemis));
        OnChange(nameof(IsExpPat));
        OnChange(nameof(IsLocal));
        OnChange(nameof(IsCustom));
        OnChange(nameof(IsAvail));
        OnChange(nameof(IsDef));
        OnChange(nameof(HasDef));
        OnChange(nameof(DefState));
        OnChange(nameof(DefTone));
        OnChange(nameof(BadgeDsp));
        OnChange(nameof(MainTone));
        OnChange(nameof(BadgeTip));
        OnChange(nameof(HasScope));
        OnChange(nameof(GameDsp));
        OnChange(nameof(GameTip));
        OnChange(nameof(Benefit));
        OnChange(nameof(SrcText));
        OnChange(nameof(ScopeTxt));
        OnChange(nameof(ActText));
        OnChange(nameof(IsGuard));
        OnChange(nameof(Decision));
        OnChange(nameof(HasIns));
        OnChange(nameof(InsText));
        OnChange(nameof(InstVer));
        OnChange(nameof(ReqVer));
        OnChange(nameof(TgtDsp));
        OnChange(nameof(KindDsp));
        OnChange(nameof(Hash));
        OnChange(nameof(TechTgt));
        OnChange(nameof(HasEvi));
        OnChange(nameof(HashDsp));
        OnChange(nameof(DetHash));
        OnChange(nameof(HashSrc));
        OnChange(nameof(EviSum));
        OnChange(nameof(EviDet));
        OnChange(nameof(GroupDsp));
        OnChange(nameof(TgtLine));
        OnChange(nameof(DirStat));
        OnChange(nameof(StateDsp));
        OnChange(nameof(CurrentStateDsp));
        OnChange(nameof(StateTon));
        OnChange(nameof(HasPendingAction));
        OnChange(nameof(PendingDsp));
        OnChange(nameof(PendingTone));
        OnChange(nameof(CanNote));
        OnChange(nameof(DetLine));
        OnChange(nameof(IsOn));
        OnChange(nameof(Status));
        OnChange(nameof(HashStat));
        OnChange(nameof(MatchDsp));
        OnChange(nameof(MatchTip));
        OnChange(nameof(TagKey));
        OnChange(nameof(Title));
        OnChange(nameof(TypeLine));
        OnChange(nameof(NoteOrig));
        OnChange(nameof(NoteBody));
        OnChange(nameof(LocNote));
        OnChange(nameof(MsgType));
        OnChange(nameof(LocMsg));
        OnChange(nameof(OrigMsg));
        OnChange(nameof(HasOrig));
        OnChange(nameof(TitleFlow));
        OnChange(nameof(NoteFlow));
        OnChange(nameof(NoteText));
    }

    internal void SetDup(int ordinal, int count)
    {
        if (_duplicateOrdinal != ordinal ||
            _duplicateCount != count)
        {
            _duplicateOrdinal = ordinal;
            _duplicateCount = count;
            OnChange(nameof(DspTitle));
        }
    }

    private static PatSrcKd InferSrc(Rpcs3PatchEntry patch)
    {
        if (patch.Class == Mdl.PatClass.Canary)
        {
            return PatSrcKd.Canary;
        }

        return patch.Source switch
        {
            PatSrc.Official => PatSrcKd.Official,
            PatSrc.Can => PatSrcKd.Canary,
            PatSrc.Artemis or
                PatSrc.ManagedArtemis => PatSrcKd.Artemis,
            PatSrc.Imported => PatSrcKd.Local,
            PatSrc.TitlePat => PatSrcKd.Custom,
            _ => PatSrcKd.Unknown
        };
    }

    private static string SrcKey(PatSrcKd sourceKind)
    {
        return sourceKind switch
        {
            PatSrcKd.Official => "Patch.Source.Official",
            PatSrcKd.Canary => "Patch.Source.Canary",
            PatSrcKd.Artemis => "Patch.Source.CheatLibrary",
            PatSrcKd.Local => "Patch.Source.Local",
            PatSrcKd.Custom => "Patch.Source.Custom",
            PatSrcKd.Missing => "Patch.Source.Missing",
            _ => "Patch.Source.Unknown"
        };
    }

    private static string Handling(Mdl.PatRisk risk)
    {
        return risk switch
        {
            Mdl.PatRisk.SafeFix or Mdl.PatRisk.Fix => "Handling.Auto",
            Mdl.PatRisk.Perf => "Handling.Optional",
            Mdl.PatRisk.Fps60 => "Handling.ManualTiming",
            Mdl.PatRisk.Canary => "Handling.UserSelected",
            Mdl.PatRisk.Cheat or Mdl.PatRisk.Exp => "Handling.NotAutoSelected",
            _ => "Handling.NotAutoSelected"
        };
    }

    private static string HandTone(Mdl.PatRisk risk)
    {
        return risk switch
        {
            Mdl.PatRisk.SafeFix or Mdl.PatRisk.Fix => "Auto",
            Mdl.PatRisk.Perf => "Optional",
            Mdl.PatRisk.Fps60 => "Warning",
            Mdl.PatRisk.Canary => "Canary",
            Mdl.PatRisk.Cheat or Mdl.PatRisk.Exp => "Muted",
            _ => "Muted"
        };
    }

    private string TagKey()
    {
        var notes = Match.Patch.Notes ?? string.Empty;

        if (Match.Patch.Risk == Mdl.PatRisk.Fps60 || Contains(notes, "fps", "framerate", "frame rate"))
        {
            return "NoteTag.FpsTiming";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.Perf)
        {
            return "NoteTag.Performance";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.Fix || Contains(notes, "color", "flicker", "rendering", "graphics", "visual"))
        {
            return "NoteTag.VisualFix";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.SafeFix || Contains(notes, "fix", "fixes", "resolves"))
        {
            return "NoteTag.Fix";
        }

        if (Match.Patch.Risk is Mdl.PatRisk.Cheat or Mdl.PatRisk.Exp or Mdl.PatRisk.Canary)
        {
            return "NoteTag.CompatibilityWarning";
        }

        if (Contains(notes, "optional"))
        {
            return "NoteTag.Optional";
        }

        return "NoteTag.WikiNote";
    }

    private static bool Contains(string value, params string[] needles)
    {
        var normalizedValue = StripArabicDiacritics(value);
        return needles.Any(needle => normalizedValue.Contains(
            StripArabicDiacritics(needle),
            StringComparison.OrdinalIgnoreCase));
    }

    private static string StripArabicDiacritics(string value)
    {
        return new string(value.Where(ch => !IsArabicDiacritic(ch)).ToArray());
    }

    private static bool IsArabicDiacritic(char ch)
    {
        return (ch >= '\u0610' && ch <= '\u061A') ||
               (ch >= '\u064B' && ch <= '\u065F') ||
               ch == '\u0670' ||
               (ch >= '\u06D6' && ch <= '\u06ED');
    }

    private static string FmtTech(string value)
    {
        if (!UiText.IsRtl || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return "\u200E" + value + "\u200E";
    }

    private static string JoinOr(IEnumerable<string> values)
    {
        var array = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return array.Length == 0 ? "-" : string.Join(", ", array);
    }
}
