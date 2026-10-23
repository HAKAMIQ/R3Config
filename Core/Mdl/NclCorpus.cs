namespace R3Config.Mdl;

public enum NclCorpusSourceKind
{
    Directory,
    Zip
}

// One cheat exactly as the corpus reader saw it. SemanticSha256 is the value
// the runtime lookup groups by - a hash of the lowered write sequence, so it
// changes when the operation shape changes. WritePrint is what those writes
// actually do to memory, from PatchWriteSemantics. The two disagree whenever
// one source expresses the same effect with a different operation shape, and
// that disagreement is what decides whether two corpora can be merged without
// losing a cheat.
public sealed record NclCorpusCheatReport(
    int Index,
    string Name,
    string? Author,
    NclVerifyStat Status,
    int OptionSetCount,
    NclStaticLowerCode LowerCode,
    string? SemanticSha256,
    string? WritePrint,
    long WrittenByteCount,
    // A content identity for every cheat, lowered or not: the name and the
    // raw text of its operations. It is what keeps an unprovable definition
    // distinguishable without ever implying it is equivalent to another.
    string RawSha256 = "");

public sealed record NclCorpusFileReport(
    string RelativePath,
    long Bytes,
    bool UsedWindows1252Fallback,
    NclDocFormat Format,
    NclDialect Dialect,
    NclSourceIdentity Identity,
    int CheatCount,
    int StaticCandidates,
    int RuntimeEvidenceRequired,
    int ConstantRuntimeRequired,
    int OptionSelectionRequired,
    int NonExecutableMetadata,
    int Unsupported,
    int Invalid,
    int CheatsWithOptions,
    int OptionSetCount,
    IReadOnlyList<string> ErrorCodes,
    int StaticLowered = 0,
    int StaticLowerRejected = 0,
    int StaticLowerUnverifiedValueIncrement = 0,
    int StaticLowerExpansionLimit = 0,
    int StaticLowerAddressOverflow = 0,
    int StaticLowerOtherRejected = 0,
    long LoweredWriteCount = 0,
    long LoweredByteCount = 0,
    long LoweredWritesOutsideUInt32 = 0,
    int LoweredCheatsOutsideUInt32 = 0,
    IReadOnlyList<NclCorpusCheatReport>? CheatReports = null)
{
    public bool HasExactIdentity => Identity.HasExactTarget;

    public bool HasParserErrors => ErrorCodes.Count > 0;

    // Per-cheat evidence. Optional so every existing construction and every
    // existing assertion in this project keeps its exact meaning.
    public IReadOnlyList<NclCorpusCheatReport> Cheats => CheatReports ?? [];
}

public sealed record NclCorpusReport(
    NclCorpusSourceKind SourceKind,
    string SourcePath,
    string? SourceSha256,
    NclDialect Dialect,
    int FileCount,
    int ModernFiles,
    int LegacyFiles,
    int CheatCount,
    int StaticCandidates,
    int RuntimeEvidenceRequired,
    int ConstantRuntimeRequired,
    int OptionSelectionRequired,
    int NonExecutableMetadata,
    int Unsupported,
    int Invalid,
    int ExactIdentityFiles,
    int MissingIdentityFiles,
    int FilesWithParserErrors,
    int FilesWithWindows1252Fallback,
    int CheatsWithOptions,
    int OptionSetCount,
    long TotalNclBytes,
    IReadOnlyList<NclCorpusFileReport> Files,
    int StaticLowered = 0,
    int StaticLowerRejected = 0,
    int StaticLowerUnverifiedValueIncrement = 0,
    int StaticLowerExpansionLimit = 0,
    int StaticLowerAddressOverflow = 0,
    int StaticLowerOtherRejected = 0,
    long LoweredWriteCount = 0,
    long LoweredByteCount = 0,
    long LoweredWritesOutsideUInt32 = 0,
    int LoweredCheatsOutsideUInt32 = 0);

public enum NclCorpusLookupCode
{
    Success,
    InvalidTarget,
    SourceMissing,
    SourceRejected,
    NoExactTarget,
    NoStaticCandidates
}

public enum NclCorpusCandidateReadCode
{
    Success,
    InvalidCandidate,
    SourceMissing,
    SourceRejected,
    EntryMissing,
    EntryChanged,
    TargetMismatch,
    SemanticMismatch
}

public sealed record NclCorpusSourceRef(
    string RelativePath,
    string EntrySha256,
    int CheatIndex);

public sealed record NclCorpusCheatCandidate(
    string CorpusSha256,
    string Serial,
    string Version,
    string Name,
    string? Author,
    string? Notes,
    NclDialect Dialect,
    string SemanticSha256,
    IReadOnlyList<NclCorpusSourceRef> Sources,
    IReadOnlyList<int>? RequiredOptionPlaceholderIds = null,
    IReadOnlyList<NclOptionSet>? DeclaredOptionSets = null,
    // The name exactly as the source file spells it. Set only when Name
    // carries a disambiguating suffix because one source name covers more
    // than one proven variant inside a single target. Read-back validation
    // always compares the source against this, never against the shown name.
    string? OriginalName = null,
    // Discovery and convertibility are deliberately separate. Every cheat in
    // an exact Serial+Version source stays visible with its verifier status,
    // while CanConvert remains false for runtime-only/unsupported/invalid
    // definitions and for any static/option definition that cannot complete
    // the verified offline conversion contract.
    NclVerifyStat Status = NclVerifyStat.OfflineStaticCandidate,
    bool CanConvert = false,
    bool IdentityProven = true,
    // True only for an exact Serial + exact normalized Version match. A
    // same-serial candidate surfaced from a different source version keeps
    // this false; Apply stays blocked until independent binary evidence
    // (PPU hash / expected bytes) proves the target bytes for the running
    // game, exactly like an unproven TitleOnly identity.
    bool VersionProven = true)
{
    public NclCorpusSourceRef PrimarySource => Sources[0];

    // What the source file calls this cheat.
    public string SourceName => OriginalName ?? Name;

    // True when Name had to be made unique for this target.
    public bool IsDisambiguated => OriginalName is not null;

    // Placeholder ids the bundled cheat's executable code references. Empty for
    // an ordinary static candidate; non-empty only while the cheat is waiting
    // for an explicit option selection.
    public IReadOnlyList<int> RequiredOptionIds =>
        RequiredOptionPlaceholderIds ?? [];

    // The option sets that own those ids, in the same order.
    public IReadOnlyList<NclOptionSet> RequiredOptionSets =>
        DeclaredOptionSets ?? [];

    public bool RequiresOptionSelection => RequiredOptionIds.Count > 0;
}

public sealed record NclCorpusConflict(
    string Name,
    IReadOnlyList<string> SemanticSha256s,
    IReadOnlyList<NclCorpusSourceRef> Sources);

public sealed record NclCorpusLookupResult(
    bool IsOk,
    NclCorpusLookupCode Code,
    string? CorpusSha256,
    IReadOnlyList<NclCorpusCheatCandidate> Cheats,
    IReadOnlyList<string> OtherVersions,
    IReadOnlyList<NclCorpusConflict> Conflicts,
    int ExactSourceFileCount,
    int RejectedSourceFileCount,
    string? Error,
    // Discovery-only: cheats bound to the exact same Serial from a source
    // that declares a different application Version. Never mixed into
    // Cheats, never counted toward ExactSourceFileCount, and every entry
    // here always has VersionProven=false and CanConvert=false.
    IReadOnlyList<NclCorpusCheatCandidate>? SameSerialOtherVersionCheats = null)
{
    public IReadOnlyList<NclCorpusCheatCandidate> OtherVersionCheats =>
        SameSerialOtherVersionCheats ?? [];
}

public sealed record NclCorpusCandidateReadResult(
    bool IsOk,
    NclCorpusCandidateReadCode Code,
    string Text,
    NclDoc? Document,
    NclCheat? Cheat,
    string? Error);
