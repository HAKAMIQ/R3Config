using System;
using System.Collections.Generic;
using System.Linq;

namespace R3Config.Mdl;

// What one structured log event is evidence of. A category is assigned only by
// an explicit rule that matches the event's own text; a category with no rule
// is never emitted, and an unmatched error is UnknownError, never a guess.
public enum LogCategory
{
    GameBoot,
    GameExit,
    Crash,
    FatalError,
    Assertion,
    BlackScreenIndicator,
    FreezeOrStallIndicator,
    Timeout,
    PerformanceWarning,
    FrameTimeSpike,
    CpuBoundIndicator,
    GpuBoundIndicator,
    PpuError,
    SpuError,
    RsxError,
    VulkanError,
    OpenglError,
    RenderingError,
    ShaderCompilationEvent,
    MemoryError,
    OutOfMemory,
    FilesystemError,
    VfsError,
    GameDataError,
    AudioError,
    ModuleLoadError,
    PrxError,
    NetworkError,
    RpcnError,
    UnknownError
}

public enum LogSourceKind
{
    Current,
    RotatedGzip
}

// A log file found through the current RPCS3 installation layout.
public sealed record LogSourceInfo(
    string Path,
    LogSourceKind Kind,
    long Length,
    DateTime LastWriteUtc);

// One deterministic diagnostic signature: subsystem (log channel), level, the
// normalized message pattern and an optional error code. Only equality of
// these parts is ever a signature match; free-text similarity is not.
public sealed record LogSignature(
    string Id,
    string Subsystem,
    char Level,
    string Pattern,
    string? ErrorCode);

// One (possibly repeated) event. Line is the first occurrence; Count is how
// often the same signature occurred in the session.
public sealed record LogEvent(
    int Line,
    int LastLine,
    double Seconds,
    char Level,
    string? Thread,
    string Channel,
    string Message,
    IReadOnlyList<LogCategory> Categories,
    LogSignature? Signature,
    int Count = 1)
{
    public LogCategory Primary => Categories.Count > 0 ? Categories[0] : LogCategory.UnknownError;

    public bool IsProblem => Categories.Any(category => category is not
        (LogCategory.GameBoot or LogCategory.GameExit or LogCategory.ShaderCompilationEvent));

    // An HLE call that returned an error code ('sys_x' failed with 0x...). RPCS3
    // logs these constantly, often harmlessly, so they are kept as evidence but
    // ranked below every other problem.
    public bool IsMinor => Level == 'E' && HleCallFailure.IsMatch(Message);

    private static readonly System.Text.RegularExpressions.Regex HleCallFailure = new(
        @"^(?:Stat: )?'\w+' failed with 0x[0-9a-fA-F]+ : ",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);
}

public sealed record LogTimelineStep(
    string Stage,
    int Line,
    double Seconds,
    string? Detail = null);

// The settings RPCS3 itself printed in its "Used configuration" block. These
// are exactly the lines RPCS3 wrote (key and value), never reconstructed.
public sealed record LogSetting(string Path, string Value);

// What one explicit RPCS3 line states. A kind is emitted only by a rule anchored
// to the exact level, channel and message RPCS3 prints (LogObservationRules); it
// records the statement and never a cause or a fix.
public enum LogObservationKind
{
    FirmwareNotInstalled,
    FirmwareCertificateMissing,
    PatchFileLoadFailed,
    IsoCorrupt,
    InvalidFileFormat,
    NpDrmVerifyFailed,
    EdatDecryptFailed,
    EdatBlockInvalidHash,
    RapNotFound,
    GameLicenseNotFound,
    GpuFeatureUnsupported,
    DriverFeatureLacking,
    UnsupportedDevice,
    DescriptorPoolFragmentation,
    PadBindFailed,
    AudioBackendInitFailed,
    UnimplementedSyscall,
    TrophyInstallFailed,
    SaveDataRemoveFailed,
    ShaderCompileFailed,
    ShaderLinkFailed,
    TitleRenamedOnBoot
}

// One distinct (kind, value) statement of a boot session with how often the same
// statement was printed and where. Value is the statement's own subject (a
// feature, a file name, a syscall name); null when the line has none.
public sealed record LogObservation(
    LogObservationKind Kind,
    string? Value,
    char Level,
    int Count,
    int FirstLine,
    int LastLine);

// An OS error the log printed ("(error=<text> (0x<code>))"). Symbol comes from
// the Windows SDK winerror.h table (Win32ErrorSymbols) and is null when the code
// is not a plain Win32 error. It is what the OS reported, not the root cause.
public sealed record Win32ErrorObservation(
    int Code,
    string? Symbol,
    string? Description,
    string RawText,
    int Line);

// Whether the log covers a whole run. Partial: cut short or a fragment with no
// header; Complete: header, a booted game and its exit are all present;
// Unknown: header present but no exit (still running, crashed hard, or cut).
public enum LogCompleteness
{
    Unknown,
    Partial,
    Complete
}

// CPU/RAM samples RPCS3's performance sensor printed. Observations only: every
// sample is kept (bounded), and the summary uses the median and the 5th/95th
// percentiles so a spike is not an average.
public sealed record LogPerformanceSummary(
    int Samples,
    double CpuAverage,
    double CpuMedian,
    double CpuP05,
    double CpuP95,
    double CpuMin,
    double CpuMax,
    int RamSamples,
    int? RamMedianMb,
    int? RamPeakMb);

// Everything needed to reproduce and qualify one analysis. Deterministic: no
// wall-clock time (the duration lives on LogSession.AnalysisDuration).
public sealed record LogAnalysisManifest(
    string ParserVersion,
    string RuleSetVersion,
    string? LogSha256,
    string SourceType,
    int TotalLines,
    int BootSessionCount,
    bool IsPartial,
    LogCompleteness Completeness,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> EvidenceSnapshots);

public enum LogBootMedia
{
    NotStated,
    IsoArchive
}

public enum LogIsoKeyState
{
    NotStated,
    Missing,
    Found
}

// A game patch RPCS3 itself reported applying (PAT: Applied patch ...).
public sealed record LogAppliedPatch(string Hash, string Description);

// Run facts the log states in its own words. Each is null / NotStated / empty
// when the log does not say it; none is inferred, and none is a diagnosis (an
// ISO key that RPCS3 could not find, for instance, is only a problem when the
// log's own encryption type says the ISO is encrypted).
public sealed record LogRunContext(
    string? FirmwareVersion,
    string? GpuDriver,
    string? PackageVersion,
    string? ConfigMode,
    LogBootMedia BootMedia,
    LogIsoKeyState IsoKey,
    string? IsoEncryption,
    IReadOnlyList<LogAppliedPatch> AppliedPatches,
    int ConfigBlocks = 0,
    bool CustomConfigFile = false,
    IReadOnlyList<LogObservation>? Observations = null,
    IReadOnlyList<Win32ErrorObservation>? Win32Errors = null,
    LogPerformanceSummary? Performance = null,
    LogCompleteness Completeness = LogCompleteness.Unknown,
    IReadOnlyList<string>? FailedDiscPaths = null,
    string? DataPath = null,
    IReadOnlyList<string>? DataPathTraits = null)
{
    public IReadOnlyList<string> DataPathTraitList => DataPathTraits ?? [];

    // Distinct /dev_bdvd paths RPCS3 failed to open/stat/list (sys_fs error lines),
    // relative to the disc root. An observed anomaly only: whether the dump is
    // wrong is decided by an IRD, never by this list alone.
    public IReadOnlyList<string> FailedDiscPathList => FailedDiscPaths ?? [];

    public IReadOnlyList<LogObservation> ObservationList => Observations ?? [];

    public IReadOnlyList<Win32ErrorObservation> Win32ErrorList => Win32Errors ?? [];

    public static LogRunContext Empty { get; } = new(
        null, null, null, null, LogBootMedia.NotStated, LogIsoKeyState.NotStated, null, []);
}

// One RPCS3 run as far as the log proves it, correlated with the game that
// booted in it. Every field is null/empty when the log does not state it.
public sealed record LogSession(
    LogSourceInfo? Source,
    Rpcs3BuildBanner? Build,
    string? CpuLine,
    string? OsLine,
    string? GpuName,
    string? Renderer,
    string? GameSerial,
    string? GameTitle,
    string? AppVersion,
    string? GameCategory,
    IReadOnlyList<string> PpuHashes,
    IReadOnlyList<LogSetting> Settings,
    IReadOnlyList<LogEvent> Events,
    IReadOnlyList<LogTimelineStep> Timeline,
    IReadOnlyList<string> RawEventLines,
    int TotalLines,
    int BootCount,
    bool Truncated,
    LogRunContext? Run = null,
    LogAnalysisManifest? Analysis = null,
    TimeSpan? AnalysisDuration = null)
{
    public LogRunContext RunContext => Run ?? LogRunContext.Empty;

    public IReadOnlyList<LogEvent> Problems => [.. Events.Where(item => item.IsProblem)];

    public static LogSession Empty { get; } = new(
        null, null, null, null, null, null, null, null, null, null,
        [], [], [], [], [], 0, 0, false);
}

// ---------------------------------------------------------------- knowledge

public enum DiagnosticEvidenceClass
{
    // A regression with corroborated working and broken evidence and a
    // maintainer-confirmed fix.
    DeveloperConfirmedFix,

    // A regression with corroborated working and broken evidence.
    CorroboratedRegression,

    // A report with a build boundary but no corroborating logs or fix.
    ReportedOnly
}

public enum DiagnosticEntryKind
{
    Regression,
    KnownIssue,
    Workaround
}

public enum DiagnosticSolutionKind
{
    FixedBuild,
    KnownGoodBuild,
    Setting,
    Renderer,
    GameUpdate,
    Patch,
    PlatformWorkaround,
    HardwareWorkaround,
    KnownUnresolved
}

public sealed record DiagnosticSource(
    string Class,
    string Url,
    string Title,
    int? Number = null,
    string? Date = null);

// A documented solution, always with where it came from and what it applies to.
public sealed record DiagnosticSolution(
    DiagnosticSolutionKind Kind,
    string? Text = null,
    int? Build = null,
    string? Setting = null,
    string? Applies = null);

public sealed record DiagnosticSignatureRef(
    string Id,
    string Subsystem,
    char Level,
    string Pattern,
    string? ErrorCode = null);

// The environment an entry applies to. Empty means the source did not restrict
// that dimension; it never means "all".
public sealed record DiagnosticScope(
    IReadOnlyList<string> TitleIds,
    string? Platform = null,
    string? Architecture = null,
    string? Renderer = null,
    string? GpuVendor = null);

public sealed record DiagnosticEntry(
    string Id,
    DiagnosticEntryKind Kind,
    DiagnosticEvidenceClass EvidenceClass,
    DiagnosticSource Source,
    string Scope,
    DiagnosticScope Applies,
    int? LastGoodBuild,
    int? FirstBrokenBuild,
    int? FixedBuild,
    int? CausingPr,
    int? FixPr,
    IReadOnlyList<LogCategory> Categories,
    IReadOnlyList<DiagnosticSignatureRef> Signatures,
    IReadOnlyList<DiagnosticSolution> Solutions,
    string? Notes = null);

public sealed record DiagnosticKnowledgeDocument(
    int Schema,
    string Source,
    IReadOnlyList<DiagnosticEntry> Entries);

// ---------------------------------------------------------------- diagnosis

public enum DiagnosisKind
{
    ObservedEvent,
    KnownIssueMatch,
    KnownRegressionMatch,
    KnownFixMatch,
    DocumentedWorkaround,
    SimilarCommunityReport,
    UnknownCause
}

// How the log event was linked to the documented entry, strongest first.
public enum MatchQuality
{
    None,
    ScopeCompatible,
    SignatureExact
}

public sealed record DiagnosticFinding(
    DiagnosisKind Kind,
    LogEvent? Event,
    DiagnosticEntry? Entry,
    MatchQuality Quality,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<DiagnosticSolution> Solutions);

public sealed record DiagnosticReport(
    LogSession Session,
    IReadOnlyList<DiagnosticFinding> Findings,
    IReadOnlyList<string> Notes)
{
    public static DiagnosticReport Empty { get; } = new(LogSession.Empty, [], []);

    // The strongest documented diagnosis; UnknownCause outranks a bare event.
    public DiagnosticFinding? Primary => Findings.OrderBy(Rank).FirstOrDefault();

    private static int Rank(DiagnosticFinding finding) => finding.Kind switch
    {
        DiagnosisKind.KnownRegressionMatch => 0,
        DiagnosisKind.KnownFixMatch => 1,
        DiagnosisKind.KnownIssueMatch => 2,
        DiagnosisKind.DocumentedWorkaround => 3,
        DiagnosisKind.SimilarCommunityReport => 4,
        DiagnosisKind.UnknownCause => 5,
        _ => 6
    };
}
