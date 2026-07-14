using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public enum PatchSourceKind
{
    Official,
    Canary,
    CheatLibrary,
    Local,
    Custom,
    Missing,
    Unknown
}

public sealed class PatchItemVm : VmBase
{
    private readonly string _sourceLabel;
    private readonly bool _isEnabled;
    private bool _isSelected;
    private string _displayTitle;

    public PatchItemVm(PatMatch match, string sourceLabel, bool isEnabled = false)
    {
        Match = match;
        _displayTitle = match.Patch.Title;
        _sourceLabel = sourceLabel;
        _isEnabled = isEnabled;
        CanEnable = !isEnabled && Svc.PatPol.Can(match);
        CanDisable = isEnabled && Svc.PatPol.CanPat(match);
        IsSelectable = CanEnable || CanDisable;
        _isSelected = !isEnabled && Svc.PatPol.Should(match.Patch.Risk, match.Status);
    }

    public PatMatch Match { get; }

    public string Title => Match.Patch.Title;

    public string DisplayTitle => _displayTitle;

    public Mdl.PatRisk Risk => Match.Patch.Risk;

    public string RiskDisplay => UiText.PatchRiskLabel(Match.Patch.Risk);

    public string RiskTooltip => UiText.PatchRiskTip(Match.Patch.Risk, Match.Status);

    public string HandlingDisplay => UiText.T(HandlingKey(Match.Patch.Risk));

    public string HandlingTooltip => UiText.PatchRiskTip(Match.Patch.Risk, Match.Status);

    public string HandlingTone => HandlingToneFor(Match.Patch.Risk);

    public string PatchType => Match.Patch.PatType;

    public string PatchTypeDisplay => UiText.PatchTypeLabel(Match.Patch.PatType);

    public string PatchTitle => Title;

    public string PatchDescription => LocalizedMessage;

    public string ShortDescription => string.IsNullOrWhiteSpace(LocalizedMessage)
        ? UiText.T("Patch.NoShortDescription")
        : LocalizedMessage;

    public bool HasDescription => !string.IsNullOrWhiteSpace(LocalizedMessage);

    public PatchSourceKind PatchSourceKind => InferSourceKind(_sourceLabel, Match.Patch);

    public string PatchSourceDisplay => UiText.T(SourceKey(PatchSourceKind));

    public string PatchSourceTooltip => PatchSourceKind == PatchSourceKind.Canary
        ? UiText.T("Patch.CanaryNotice")
        : UiText.F("Patch.Source.Tooltip", PatchSourceDisplay);

    public string PatchSourceTone => PatchSourceKind switch
    {
        PatchSourceKind.Official => "Game",
        PatchSourceKind.Canary => "Canary",
        PatchSourceKind.CheatLibrary => "Custom",
        PatchSourceKind.Custom => "Custom",
        PatchSourceKind.Local => "Local",
        PatchSourceKind.Missing => "Warning",
        _ => "Muted"
    };

    public string CanaryScopeDisplay => UiText.F("Patch.CanaryScope", SerialDisplay, CanaryVersionDisplay);

    public string CanaryScopeTooltip => UiText.F("Patch.CanaryScopeTip", SerialDisplay, CanaryVersionDisplay, HashStatusDisplay);

    public string CanaryVersionDisplay => !string.IsNullOrWhiteSpace(Match.TgtVer)
        ? Match.TgtVer!
        : IsVersionSpecific
            ? string.Join(", ", Match.Patch.GameVer)
            : UiText.T("Patch.Version.Any");

    public bool IsVersionSpecific => Match.Patch.GameVer.Any(version => !string.IsNullOrWhiteSpace(version));

    public bool IsOfficial => PatchSourceKind == PatchSourceKind.Official;

    public bool IsCanary => PatchSourceKind == PatchSourceKind.Canary;

    public bool IsExperimentalPatch =>
        IsCanary ||
        Risk is Mdl.PatRisk.Exp or Mdl.PatRisk.Canary;

    public bool IsLocal => PatchSourceKind == PatchSourceKind.Local;

    public bool IsCustom => PatchSourceKind == PatchSourceKind.Custom;

    public bool IsAvailable => PatchSourceKind is not PatchSourceKind.Missing and not PatchSourceKind.Unknown;

    public bool IsDefinitionResident => Match.Patch.Source is
        PatSrc.Official or
        PatSrc.Imported or
        PatSrc.ManCht or
        PatSrc.TitlePat;

    public bool HasDefinitionState => Match.Patch.Source switch
    {
        PatSrc.Can => true,
        PatSrc.Imported => IsExperimentalPatch,
        PatSrc.Cht or PatSrc.ManCht => true,
        PatSrc.TitlePat => Risk == Mdl.PatRisk.Cheat,
        _ => false
    };

    public string DefinitionStateDisplay => Match.Patch.Source switch
    {
        PatSrc.Can => UiText.T("Patch.Definition.NotMerged"),
        PatSrc.Imported when IsExperimentalPatch => UiText.T("Patch.Definition.Merged"),
        PatSrc.Cht => UiText.T("Patch.Definition.NotPrepared"),
        PatSrc.ManCht => UiText.T("Patch.Definition.Prepared"),
        PatSrc.TitlePat when Risk == Mdl.PatRisk.Cheat => UiText.T("Patch.Definition.Existing"),
        _ => string.Empty
    };

    public string DefinitionStateTone => IsDefinitionResident ? "Game" : "Muted";

    public string PrimaryBadgeDisplay => PatchSourceDisplay;

    public string PrimaryBadgeTone => PatchSourceTone;

    public string PrimaryBadgeTooltip => PatchSourceTooltip;

    public bool HasScopeBadge => IsCanary;

    public string GameScopeDisplay => UiText.F("Patch.GameScope", SerialDisplay);

    public string GameScopeTooltip => UiText.F("Patch.GameScopeTip", SerialDisplay, CanaryVersionDisplay);

    public string PlayerBenefitText => string.IsNullOrWhiteSpace(LocalizedMessage)
        ? UiText.T("Patch.PlayerBenefitUnknown")
        : UiText.F("Patch.PlayerBenefit", ShortDescription);

    public string PlayerSourceText => IsCanary
        ? UiText.T("Patch.PlayerSourceCanary")
        : UiText.F("Patch.PlayerSource", PatchSourceDisplay);

    public string PlayerScopeText => UiText.F("Patch.PlayerScope", FormatTechInline(SerialDisplay), FormatTechInline(CanaryVersionDisplay));

    public string PlayerActionText => IsEnabled && CanDisable
        ? UiText.T("Patch.PlayerActionEnabled")
        : CanEnable
            ? UiText.T(IsCanary ? "Patch.PlayerActionCanarySelectable" : "Patch.PlayerActionSelectable")
            : MatchTooltip;

    public bool IsRuntimeGuarded =>
        PatAct.IsRun(Match);

    public CmpDec CompatibilityDecision =>
        PatAct.Decide(Match);

    public bool HasPlayerInstruction =>
        !string.IsNullOrWhiteSpace(Notes) &&
        ContainsAny(
            Notes,
            "requires",
            "required",
            "must ",
            "enable ",
            "disable ",
            "press ",
            "hold ",
            "toggle ",
            "using ",
            "set ",
            "check ",
            "only works",
            "يجب",
            "يتطلب",
            "فعّل",
            "عطّل",
            "اضغط");

    public string PlayerInstructionText => HasPlayerInstruction
        ? UiText.F("Patch.User.Instruction", LocalizedMessage)
        : string.Empty;

    public string InstalledVersionDisplay => PatVer.Norm(Match.MatVer)
        ?? UiText.T("Ui.Unknown");

    public string RequiredVersionDisplay => PatVer.Norm(Match.TgtVer)
        ?? (Match.Patch.GameVer.Count > 0
            ? string.Join(", ", Match.Patch.GameVer)
            : UiText.T("Patch.Version.Any"));

    public string TargetSerialDisplay => Match.TgtSer
        ?? Match.MatSer
        ?? JoinOrDash(Match.Patch.Serials);

    public string HashKindDisplay => Match.Patch.HashKind switch
    {
        HashKind.Ppu => "PPU",
        HashKind.Spu => "SPU",
        HashKind.Prx => "PRX",
        HashKind.Ovl => "OVL",
        _ => "HASH"
    };

    private string ComparableHash =>
        Match.Patch.NormHash ?? Match.Patch.Hash;

    public string ShortHash => string.IsNullOrWhiteSpace(ComparableHash)
        ? "-"
        : ComparableHash[..Math.Min(12, ComparableHash.Length)];

    public string TechnicalTargetDisplay => $"{HashKindDisplay}-{ShortHash}";

    public bool HasHashEvidence => Match.Patch.HashKind != HashKind.Unknown;

    public string RequiredHashDisplay => $"{HashKindDisplay}-{ComparableHash}";

    public string DetectedHashDisplay => Match.Hashes.Count == 0
        ? UiText.T("Ui.Unknown")
        : string.Join(", ", Match.Hashes.Select(hash => $"{HashKindDisplay}-{hash}"));

    public string HashEvidenceSourceDisplay
    {
        get
        {
            return Match.HashEvi switch
            {
                HashEvi.PlainLog or
                HashEvi.ZipLog =>
                    UiText.T("PatchReadiness.Source.Log"),

                HashEvi.Cache =>
                    UiText.T("PatchReadiness.Source.Cache"),

                HashEvi.Meta =>
                    UiText.T("PatchReadiness.Source.Metadata"),

                _ => string.IsNullOrWhiteSpace(Match.HashSrc)
                    ? UiText.T("Ui.Unknown")
                    : UiText.T("PatchReadiness.Source.Metadata")
            };
        }
    }

    public string HashEvidenceSummary
    {
        get
        {
            if (IsRuntimeGuarded)
            {
                return UiText.T("Patch.HashEvidence.RuntimeGuarded");
            }

            if (Match.Hashes.Any(hash =>
                    hash.Equals(
                        Match.Patch.NormHash,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return UiText.F(
                    "Patch.HashEvidence.Proven",
                    FormatTechInline(RequiredHashDisplay),
                    HashEvidenceSourceDisplay);
            }

            return Match.Hashes.Count > 0
                ? UiText.F(
                    "Patch.HashEvidence.Rejected",
                    FormatTechInline(RequiredHashDisplay),
                    FormatTechInline(DetectedHashDisplay))
                : UiText.F(
                    "Patch.HashEvidence.Missing",
                    FormatTechInline(RequiredHashDisplay));
        }
    }

    public string HashEvidenceDetails => UiText.F(
        "Patch.HashEvidence.Details",
        FormatTechInline(RequiredHashDisplay),
        FormatTechInline(DetectedHashDisplay),
        HashEvidenceSourceDisplay);

    public string GroupDisplay => string.IsNullOrWhiteSpace(Match.Patch.Group)
        ? PatchTypeDisplay
        : Match.Patch.Group!;

    public string TargetLine => UiText.F(
        "Patch.TargetLine",
        FormatTechInline(TargetSerialDisplay),
        FormatTechInline(InstalledVersionDisplay),
        FormatTechInline(RequiredVersionDisplay));

    public string DirectStatusText
    {
        get
        {
            if (IsEnabled)
            {
                return UiText.T("Patch.Direct.Enabled");
            }

            var compatibilityKey = CompatibilityDecision.Code switch
            {
                CmpCode.Key =>
                    "Patch.Direct.InvalidRuntimeKey",
                CmpCode.KeySerBd =>
                    "Patch.Direct.RuntimeKeySerialMismatch",
                CmpCode.Guard =>
                    "Patch.Direct.UntrustedRuntimeGuard",
                CmpCode.Hash =>
                    "Patch.Direct.GlobalNeedsExactHash",
                _ => null
            };

            if (compatibilityKey is not null)
            {
                return UiText.T(compatibilityKey);
            }

            if (CanEnable && IsDefinitionResident)
            {
                return Match.Patch.Source switch
                {
                    PatSrc.Imported when IsExperimentalPatch =>
                        UiText.T("Patch.Direct.MergedDisabled"),
                    PatSrc.ManCht =>
                        UiText.T("Patch.Direct.PreparedDisabled"),
                    PatSrc.TitlePat when Risk == Mdl.PatRisk.Cheat =>
                        UiText.T("Patch.Direct.PreparedDisabled"),
                    _ => UiText.T("Patch.Direct.Disabled")
                };
            }

            return MatchStatus switch
            {
                MatStat.Match => UiText.T("Patch.Direct.Ready"),
                MatStat.Unknown when IsRuntimeGuarded =>
                    UiText.T("Patch.Direct.RuntimeGuarded"),
                MatStat.Unknown => UiText.F("Patch.Direct.HashUnknown", HashKindDisplay),
                MatStat.VerUnk => UiText.F("Patch.Direct.VersionUnknown", RequiredVersionDisplay),
                MatStat.VerBad => UiText.F(
                    "Patch.Direct.VersionMismatch",
                    InstalledVersionDisplay,
                    RequiredVersionDisplay),
                MatStat.HashBad => UiText.T(
                    "Patch.Direct.HashMismatch"),
                _ => UiText.T("Patch.Direct.Unavailable")
            };
        }
    }

    public string StateDisplay => IsEnabled
        ? UiText.T("Patch.State.Enabled")
        : CanEnable
            ? UiText.T(IsDefinitionResident ? "Patch.State.Disabled" : "Patch.State.Ready")
            : UiText.T("Patch.State.Blocked");

    public string StateTone => IsEnabled
        ? "Game"
        : CanEnable
            ? IsDefinitionResident ? "Muted" : "Auto"
            : "Warning";

    public string DetailsLine => UiText.F(
        "Patch.DetailsLine",
        PatchSourceDisplay,
        MatchStatusDisplay,
        PatchTypeDisplay,
        HashStatusDisplay);

    public bool IsEnabled => _isEnabled;

    public string LocalizedStatus => MatchStatusDisplay;

    public string SerialDisplay => Match.MatSer
        ?? JoinOrDash(Match.Patch.Serials);

    public string VersionDisplay => Match.MatVer
        ?? JoinOrDash(Match.Patch.GameVer);

    public string HashStatusDisplay => UiText.HashStatusLabel(Match.Status, Match.Patch.HashKind);

    public MatStat MatchStatus => Match.Status;

    public string MatchStatusDisplay => UiText.PatchMatchLabel(Match.Status);

    public string MatchTooltip => UiText.PatchMatchTip(Match.Status);

    public string? Notes => Match.Patch.Notes;

    public string NoteTag => string.IsNullOrWhiteSpace(Match.Patch.Notes)
        ? UiText.T("Ui.EmptyValue")
        : UiText.T(NoteTagKey());

    public string NoteTitle => LocalizedNoteTitle;

    public string NoteTypeLine => UiText.F("Plan.NoteTooltipType", LocalizedMessageType);

    public string NoteOriginalLabel => UiText.T("Note.OriginalTextLabel");

    public string NoteBody => LocalizedMessage;

    public string LocalizedNoteTitle => PatchTextLocalizer.LocalizePatchMessage(Match.Patch.Notes).Title;

    public string LocalizedMessageType => PatchTextLocalizer.LocalizePatchMessage(Match.Patch.Notes).Type;

    public string LocalizedMessage => PatchTextLocalizer.LocalizePatchMessage(Match.Patch.Notes).Body;

    public string LocalizedOriginalMessage => PatchTextLocalizer.LocalizePatchMessage(Match.Patch.Notes).OriginalBody;

    public bool HasOriginalMessage => PatchTextLocalizer.LocalizePatchMessage(Match.Patch.Notes).HasOriginalBody;

    public FlowDirection NoteFlow => UiLayoutService.Current.TextFlow;

    public TextAlignment NoteTextAlignment => UiLayoutService.Current.TextAlignment;

    public bool IsSelectable { get; }

    public bool CanEnable { get; }

    public bool CanDisable { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!IsSelectable)
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
        OnPropertyChanged(nameof(PatchTypeDisplay));
        OnPropertyChanged(nameof(PatchTitle));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(PatchDescription));
        OnPropertyChanged(nameof(ShortDescription));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(PatchSourceKind));
        OnPropertyChanged(nameof(PatchSourceDisplay));
        OnPropertyChanged(nameof(PatchSourceTooltip));
        OnPropertyChanged(nameof(PatchSourceTone));
        OnPropertyChanged(nameof(CanaryScopeDisplay));
        OnPropertyChanged(nameof(CanaryScopeTooltip));
        OnPropertyChanged(nameof(CanaryVersionDisplay));
        OnPropertyChanged(nameof(IsVersionSpecific));
        OnPropertyChanged(nameof(IsOfficial));
        OnPropertyChanged(nameof(IsCanary));
        OnPropertyChanged(nameof(IsExperimentalPatch));
        OnPropertyChanged(nameof(IsLocal));
        OnPropertyChanged(nameof(IsCustom));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsDefinitionResident));
        OnPropertyChanged(nameof(HasDefinitionState));
        OnPropertyChanged(nameof(DefinitionStateDisplay));
        OnPropertyChanged(nameof(DefinitionStateTone));
        OnPropertyChanged(nameof(PrimaryBadgeDisplay));
        OnPropertyChanged(nameof(PrimaryBadgeTone));
        OnPropertyChanged(nameof(PrimaryBadgeTooltip));
        OnPropertyChanged(nameof(HasScopeBadge));
        OnPropertyChanged(nameof(GameScopeDisplay));
        OnPropertyChanged(nameof(GameScopeTooltip));
        OnPropertyChanged(nameof(PlayerBenefitText));
        OnPropertyChanged(nameof(PlayerSourceText));
        OnPropertyChanged(nameof(PlayerScopeText));
        OnPropertyChanged(nameof(PlayerActionText));
        OnPropertyChanged(nameof(IsRuntimeGuarded));
        OnPropertyChanged(nameof(CompatibilityDecision));
        OnPropertyChanged(nameof(HasPlayerInstruction));
        OnPropertyChanged(nameof(PlayerInstructionText));
        OnPropertyChanged(nameof(InstalledVersionDisplay));
        OnPropertyChanged(nameof(RequiredVersionDisplay));
        OnPropertyChanged(nameof(TargetSerialDisplay));
        OnPropertyChanged(nameof(HashKindDisplay));
        OnPropertyChanged(nameof(ShortHash));
        OnPropertyChanged(nameof(TechnicalTargetDisplay));
        OnPropertyChanged(nameof(HasHashEvidence));
        OnPropertyChanged(nameof(RequiredHashDisplay));
        OnPropertyChanged(nameof(DetectedHashDisplay));
        OnPropertyChanged(nameof(HashEvidenceSourceDisplay));
        OnPropertyChanged(nameof(HashEvidenceSummary));
        OnPropertyChanged(nameof(HashEvidenceDetails));
        OnPropertyChanged(nameof(GroupDisplay));
        OnPropertyChanged(nameof(TargetLine));
        OnPropertyChanged(nameof(DirectStatusText));
        OnPropertyChanged(nameof(StateDisplay));
        OnPropertyChanged(nameof(StateTone));
        OnPropertyChanged(nameof(DetailsLine));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(LocalizedStatus));
        OnPropertyChanged(nameof(HashStatusDisplay));
        OnPropertyChanged(nameof(MatchStatusDisplay));
        OnPropertyChanged(nameof(MatchTooltip));
        OnPropertyChanged(nameof(NoteTag));
        OnPropertyChanged(nameof(NoteTitle));
        OnPropertyChanged(nameof(NoteTypeLine));
        OnPropertyChanged(nameof(NoteOriginalLabel));
        OnPropertyChanged(nameof(NoteBody));
        OnPropertyChanged(nameof(LocalizedNoteTitle));
        OnPropertyChanged(nameof(LocalizedMessageType));
        OnPropertyChanged(nameof(LocalizedMessage));
        OnPropertyChanged(nameof(LocalizedOriginalMessage));
        OnPropertyChanged(nameof(HasOriginalMessage));
        OnPropertyChanged(nameof(NoteFlow));
        OnPropertyChanged(nameof(NoteTextAlignment));
    }

    internal void SetDuplicateOrdinal(int ordinal, int count)
    {
        var displayTitle = count > 1 ? $"{Title} ({ordinal:00})" : Title;
        if (!string.Equals(_displayTitle, displayTitle, StringComparison.Ordinal))
        {
            _displayTitle = displayTitle;
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    private static PatchSourceKind InferSourceKind(string sourceLabel, PatEntry patch)
    {
        var explicitSource = patch.Source switch
        {
            PatSrc.Official => PatchSourceKind.Official,
            PatSrc.Can => PatchSourceKind.Canary,
            PatSrc.Cht => PatchSourceKind.CheatLibrary,
            PatSrc.ManCht => PatchSourceKind.Local,
            PatSrc.Imported when patch.Risk == Mdl.PatRisk.Canary => PatchSourceKind.Canary,
            PatSrc.Imported => PatchSourceKind.Local,
            PatSrc.TitlePat => PatchSourceKind.Custom,
            _ => PatchSourceKind.Unknown
        };

        if (explicitSource != PatchSourceKind.Unknown)
        {
            return explicitSource;
        }

        var patchText = string.Join(
            " ",
            patch.Title,
            patch.PatType,
            patch.Group ?? string.Empty,
            patch.Notes ?? string.Empty,
            patch.Author ?? string.Empty);

        if (patch.Risk == Mdl.PatRisk.Canary ||
            string.Equals(patch.PatType, "Canary", StringComparison.OrdinalIgnoreCase))
        {
            return PatchSourceKind.Canary;
        }

        if (string.IsNullOrWhiteSpace(sourceLabel) || ContainsAny(sourceLabel, "missing", "not found"))
        {
            return PatchSourceKind.Missing;
        }

        if (ContainsAny(sourceLabel, "custom", "user patch", "user-patch") ||
            ContainsAny(patchText, "custom", "user patch", "user-patch"))
        {
            return PatchSourceKind.Custom;
        }

        if (ContainsAny(sourceLabel, "canary") &&
            !ContainsAny(sourceLabel, "patch.yml / imported_patch.yml"))
        {
            return PatchSourceKind.Canary;
        }

        if (ContainsAny(sourceLabel, "local") &&
            !ContainsAny(sourceLabel, "patch.yml"))
        {
            return PatchSourceKind.Local;
        }

        if (ContainsAny(sourceLabel, "game patches", "wiki", "rpcs3 patches", "patch.yml"))
        {
            return PatchSourceKind.Official;
        }

        if (ContainsAny(sourceLabel, ".yml", ".yaml"))
        {
            return PatchSourceKind.Local;
        }

        return PatchSourceKind.Unknown;
    }

    private static string SourceKey(PatchSourceKind sourceKind)
    {
        return sourceKind switch
        {
            PatchSourceKind.Official => "Patch.Source.Official",
            PatchSourceKind.Canary => "Patch.Source.Canary",
            PatchSourceKind.CheatLibrary => "Patch.Source.CheatLibrary",
            PatchSourceKind.Local => "Patch.Source.Local",
            PatchSourceKind.Custom => "Patch.Source.Custom",
            PatchSourceKind.Missing => "Patch.Source.Missing",
            _ => "Patch.Source.Unknown"
        };
    }

    private static string HandlingKey(Mdl.PatRisk risk)
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

    private static string HandlingToneFor(Mdl.PatRisk risk)
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

    private string NoteTagKey()
    {
        var notes = Match.Patch.Notes ?? string.Empty;

        if (Match.Patch.Risk == Mdl.PatRisk.Fps60 || ContainsAny(notes, "fps", "framerate", "frame rate"))
        {
            return "NoteTag.FpsTiming";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.Perf)
        {
            return "NoteTag.Performance";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.Fix || ContainsAny(notes, "color", "flicker", "rendering", "graphics", "visual"))
        {
            return "NoteTag.VisualFix";
        }

        if (Match.Patch.Risk == Mdl.PatRisk.SafeFix || ContainsAny(notes, "fix", "fixes", "resolves"))
        {
            return "NoteTag.Fix";
        }

        if (Match.Patch.Risk is Mdl.PatRisk.Cheat or Mdl.PatRisk.Exp or Mdl.PatRisk.Canary)
        {
            return "NoteTag.CompatibilityWarning";
        }

        if (ContainsAny(notes, "optional"))
        {
            return "NoteTag.Optional";
        }

        return "NoteTag.WikiNote";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatTechInline(string value)
    {
        if (!UiText.IsRtl || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return "\u200E" + value + "\u200E";
    }

    private static string JoinOrDash(IEnumerable<string> values)
    {
        var array = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return array.Length == 0 ? "-" : string.Join(", ", array);
    }
}
