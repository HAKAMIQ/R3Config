using System.Collections.Generic;

namespace R3Config.Mdl;

// Free-text Wiki evidence outside the {{config}} template — "Known Issues",
// "Special Notes", platform-specific subsections, and similar prose sections.
// This is deliberately separate from WikiCorpusNote (Core/Mdl/Wiki.cs), which
// only covers notes attached to a specific settings-template key. A
// WikiFreeTextNote never becomes a setting; see WikiNotesParser.
public enum WikiNoteClass
{
    GeneralInstruction,
    Warning,
    CompatibilityNote,
    VersionSpecificNote,
    HardwareConditionalNote,
    PlatformConditionalNote,
    PatchSpecificNote,
    NetworkNote,
    RpcnNote,
    Workaround,
    InformationalNote
}

public sealed record WikiFreeTextNote(
    string Page,
    string SourceSection,
    WikiNoteClass Class,
    string Text,
    string? VersionCondition,
    string? PlatformCondition,
    string? EditionCondition = null,
    string? HardwareCondition = null,
    string? Rpcs3BuildCondition = null,
    string? SerialCondition = null,
    bool PatchDependent = false);

// Structured per-game online/netplay evidence recovered from the Wiki's
// {{netplay}} template. A "Game Version" field inside this table is always
// the TESTED version, never an inferred requirement — see
// RequiredGameVersion, which stays null unless a requirement is stated
// somewhere else on the page in explicit "required/minimum/exact version"
// language (WikiNotesParser does not fabricate this from the netplay table).
public enum WikiOnlineState
{
    Supported,
    NotSupported,
    TestedPartial,
    Unknown
}

public sealed record WikiOnlineEvidence(
    string Page,
    WikiOnlineState State,
    string? TestedGameVersion,
    string? RequiredGameVersion,
    string? Rpcs3VersionTested,
    string? Connects,
    string? CreateJoinLobbies,
    string? MatchWithOthers,
    string? OnlineFeaturesWorking,
    string? RequiresCustomServers,
    string? Notes);

public sealed record WikiPageEvidence(
    string Page,
    IReadOnlyList<string> Serials,
    IReadOnlyList<WikiFreeTextNote> Notes,
    IReadOnlyList<WikiOnlineEvidence> Online);

// Sidecar artifact that carries Wiki notes/online evidence to the app. Loaded
// by WikiEvidenceCatalog; it is independent of the settings catalog (cat.json)
// so the notes consumer path never changes which settings ship.
public sealed record WikiEvidenceDocument(
    int Schema,
    string DataSource,
    string DataVersion,
    string SourceHash,
    IReadOnlyList<WikiPageEvidence> Pages,
    string? SourceFile = null);
