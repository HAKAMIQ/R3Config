using System.Collections.Generic;

namespace R3Config.Mdl;

// The RPCS3 Settings dialog tab a Wiki-derived setting belongs to. Only the
// tabs that actually receive evidence for a game are ever projected.
public enum Rpcs3SettingsSection
{
    Cpu,
    Gpu,
    Audio,
    Io,
    System,
    Network,
    Advanced,
    Emulator,
    Gui,
    Debug
}

// How the Wiki frames a setting for the selected game. This is presentation
// semantics only; it never changes what Apply writes.
public enum SettingRole
{
    Required,
    Recommended,
    Optional,
    Conditional,
    Other
}

public sealed record GameSettingRow(
    CfgEntry Entry,
    Rpcs3SettingsSection Section,
    SettingRole Role,
    bool UsedSectionFallback);

public sealed record GameSettingsSection(
    Rpcs3SettingsSection Section,
    IReadOnlyList<GameSettingRow> Rows);

// Whether a free-text Wiki note applies to the selected game. Only General
// notes are shown; every other value keeps the evidence internally but hides it
// from the normal Game Settings view. Unresolved means the note carries a scope
// (version, edition, hardware, build) that the detected context cannot confirm.
public enum WikiNoteApplicability
{
    General,
    OtherPlatform,
    OtherSerial,
    OtherVersion,
    OtherEdition,
    OtherHardware,
    OtherBuild,
    Unresolved
}

public sealed record GameNote(
    WikiFreeTextNote Note,
    WikiNoteApplicability Applicability);

// Detected runtime context the projection compares note and online scope
// against: title ID, installed APP_VER, host platform, RPCS3 build, hardware.
// RuntimeBuild carries version, build number, commit, branch, stage and
// architecture as separate typed facts (never one merged string); only an Exact
// identity decides build scope. CurrentRpcs3Build ("X.Y.Z-BUILD") is a display
// string for the same build, null when the Runtime Context could not prove it.
public sealed record GameSettingsContext(
    string Serial,
    string? GameVersion = null,
    string HostPlatform = "Windows",
    Rpcs3RuntimeBuild? RuntimeBuild = null,
    string? GpuName = null,
    string? CpuName = null,
    string? CurrentRpcs3Build = null,
    HwInfo? Hardware = null);

public sealed record GameSettingsModel(
    string Serial,
    string? WikiPage,
    IReadOnlyList<GameSettingsSection> Sections,
    IReadOnlyList<GameNote> Notes,
    IReadOnlyList<WikiOnlineEvidence> Online,
    IReadOnlyList<WikiOnlineEvidence>? ApplicableOnline = null,
    bool OnlineVersionUnknown = false,
    IReadOnlyDictionary<string, string>? ExactBuildUrls = null,
    string? CurrentRpcs3Build = null,
    GameKnowledge? Knowledge = null)
{
    // History and hardware evidence for the game's Wiki page; Inform-level only, never a setting.
    public GameKnowledge GameKnowledge => Knowledge ?? Mdl.GameKnowledge.Empty;

    // Wiki guidance that named a range of values instead of one value. Shown
    // as text beside the settings: it has no row, no checkbox and no value,
    // and no path converts it into a setting.
    public IReadOnlyList<CfgHint> Guidance { get; init; } = [];

    // Exact official download URL per tested RPCS3 build text ("X.Y.Z-BUILD"),
    // only for builds the shipped catalog resolves for the host platform. A
    // missing key means no exact download is offered.
    public string? ExactBuildUrl(string? testedBuildText) =>
        testedBuildText is not null &&
        ExactBuildUrls is not null &&
        ExactBuildUrls.TryGetValue(testedBuildText, out var url)
            ? url
            : null;

    // Online evidence whose tested game version matches the installed one (or
    // is not version-scoped). The full list stays available in Online.
    public IReadOnlyList<WikiOnlineEvidence> OnlineForInstalled => ApplicableOnline ?? Online;

    // Evidence exists, but none of it is confirmed for the installed version:
    // either the installed version differs from every tested one, or it could
    // not be determined (OnlineVersionUnknown). Unknown is never applicable.
    public bool HasOnlineForOtherVersionOnly =>
        Online.Count > 0 && OnlineForInstalled.Count == 0;

    // No {{netplay}} evidence is Unknown, never NotSupported.
    public WikiOnlineState OnlineState => Online.Count == 0
        ? WikiOnlineState.Unknown
        : Online[0].State;

    public static GameSettingsModel Empty(string serial) =>
        new(serial, null, [], [], []);
}
