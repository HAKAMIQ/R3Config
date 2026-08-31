using System;
using System.Collections.Generic;

namespace R3Config.Mdl;

public enum WikiRecommendationClass
{
    MandatoryGameConfig,
    GeneralRecommendation,
    PatchSpecific,
    InformationalNote,
    VersionSpecific,
    Unsupported,
    Ambiguous
}

public enum WikiParserStatus
{
    Mapped,
    Unsupported,
    Ambiguous,
    Rejected,
    UnknownWikiKey
}

public enum WikiFindingCode
{
    EmptyGameId,
    MalformedGameId,
    InvalidConfigTemplate,
    UnknownWikiKey,
    UnsupportedWikiKey,
    InvalidWikiValue,
    AmbiguousWikiValue,
    DuplicateSettingConflict,
    DuplicateSerialConflict,
    RegionAmbiguous,
    OrphanNote
}

public sealed record WikiMappedSetting(
    string Section,
    string Key,
    string Value);

public sealed record WikiCorpusEntry(
    string Page,
    IReadOnlyList<string> PageSerials,
    IReadOnlyList<string> ApplicableSerials,
    string WikiKey,
    string RawWikiValue,
    string? Notes,
    IReadOnlyList<WikiMappedSetting> MappedSettings,
    WikiRecommendationClass Classification,
    WikiParserStatus ParserStatus,
    bool IncludedInCatalog,
    string? Detail);

public sealed record WikiCorpusNote(
    string Page,
    string WikiKey,
    string RawText,
    string Text,
    WikiRecommendationClass Classification,
    bool AssociatedWithSetting);

public sealed record WikiCorpusPage(
    string Title,
    int Namespace,
    bool IsRedirect,
    bool IsGameContent,
    bool Parsed,
    string? RevisionId,
    DateTimeOffset? RevisionTimestamp,
    IReadOnlyList<string> Serials,
    int GameIdTemplateCount,
    int ConfigTemplateCount,
    IReadOnlyList<string> PatchReferences,
    IReadOnlyList<WikiCorpusEntry> Recommendations,
    IReadOnlyList<WikiCorpusNote> Notes);

public sealed record WikiCorpusFinding(
    WikiFindingCode Code,
    string Page,
    string? Serial,
    string? WikiKey,
    string Detail);

public sealed record WikiCorpusStats(
    int TotalPages,
    int ParsedPages,
    int ParseFailures,
    int MainNamespacePages,
    int GameContentPages,
    int RedirectPages,
    int NonGamePages,
    int PagesWithGameIdTemplate,
    int PagesWithSerials,
    int PagesWithConfigTemplate,
    int PagesWithRecommendations,
    int PagesWithoutRecommendations,
    int UniqueGameTitles,
    int SerialOccurrences,
    int UniqueSerials,
    int RegionVariants,
    int TotalConfigEntries,
    int TotalNotes,
    int MappedRecommendations,
    int UnsupportedRecommendations,
    int AmbiguousRecommendations,
    int RejectedRecommendations,
    int VersionSpecificRecommendations,
    int PatchSpecificRecommendations,
    int UnknownRecommendations,
    int DuplicateSerials,
    int ConflictingSerials,
    int ConflictingSettings);

public sealed record WikiCorpusAnalysis(
    string SourceFile,
    string SourceSha256,
    string SnapshotDate,
    TimeSpan ParseDuration,
    WikiCorpusStats Stats,
    IReadOnlyList<WikiCorpusPage> Pages,
    IReadOnlyList<WikiCorpusEntry> Recommendations,
    IReadOnlyList<WikiCorpusNote> Notes,
    IReadOnlyList<WikiCorpusFinding> Findings,
    IReadOnlyList<WikiRec> CatalogRecommendations);

public enum WikiCatalogDiffKind
{
    Exact,
    MissingInCatalog,
    ExtraInCatalog,
    ValueMismatch,
    KeyMismatch,
    SerialMismatch,
    RegionMismatch,
    NotesMismatch,
    VersionAmbiguous,
    PatchSpecificMismatch
}

public sealed record WikiCatalogDiff(
    string Serial,
    string? WikiTitle,
    WikiCatalogDiffKind Kind,
    string Detail);

public sealed record WikiCatalogComparison(
    int OldCatalogEntries,
    int GeneratedCatalogEntries,
    int Added,
    int Removed,
    int Changed,
    int Unchanged,
    int ExactRecommendationMatches,
    int MissingRecommendations,
    int ExtraRecommendations,
    int DifferentRecommendations,
    IReadOnlyList<WikiCatalogDiff> Differences);
