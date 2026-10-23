using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

public enum LogDiagState
{
    NotRun,
    Running,
    NoLog,
    Ready
}

// One labeled line. A technical value (name, build, path, hash) is raw and
// left-to-right; otherwise Value is localized text.
public sealed record DiagLineVm(string Label, string Value, bool IsTechnical = true, string? Note = null)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool IsLocalized => !IsTechnical;
}

public sealed class DiagEventVm(LogEvent item)
{
    public LogEvent Model { get; } = item;

    public string Categories => string.Join(" / ", Model.Categories.Select(SymptomText.Text));

    public string Where => $"{Model.Level} {Model.Channel}";

    public string Count => Model.Count > 1
        ? string.Create(CultureInfo.InvariantCulture, $"x{Model.Count}")
        : string.Empty;

    public string Line => string.Create(CultureInfo.InvariantCulture, $"#{Model.Line}");

    public string Time => TimeSpan.FromSeconds(Model.Seconds).ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);

    public string Message => Model.Message;

    public string Signature => Model.Signature?.Id ?? string.Empty;

    public bool IsMinor => Model.IsMinor;
}

public sealed record DiagTimelineVm(string Stage, string Time, string Detail, int Line);

// One documented solution with where it came from; a fixed or known-good build
// carries its exact official download only when the shipped catalog resolves it.
public sealed record DiagSolutionVm(string Label, string Detail, string? DownloadUrl, string DownloadLabel)
{
    public bool HasDownload => !string.IsNullOrWhiteSpace(DownloadUrl);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}

public sealed class DiagFindingVm
{
    public DiagFindingVm(DiagnosticFinding finding, Rpcs3BuildCatalog? builds, string host)
    {
        Model = finding;
        var entry = finding.Entry;
        Title = UiText.GetText($"Diag.Kind.{finding.Kind}");
        Symptom = finding.Event is null ? string.Empty : string.Join(" / ", finding.Event.Categories.Select(SymptomText.Text));
        Quality = finding.Quality == MatchQuality.None ? string.Empty : UiText.GetText($"Diag.Quality.{finding.Quality}");

        if (entry is null)
        {
            SourceText = UiText.GetText("Diag.Cause.Unknown");
            Rows = [];
            Solutions = [];
            NoSolution = string.Empty;
            return;
        }

        SourceText = entry.Source.Number is { } number
            ? string.Create(CultureInfo.InvariantCulture, $"{UiText.GetText("Diag.Source.GitHubIssue")} #{number} - {entry.Source.Title}")
            : entry.Source.Title;
        SourceUrl = WikiLinks.IsSafeUrl(entry.Source.Url) ? entry.Source.Url : null;

        var rows = new List<DiagLineVm>
        {
            new(UiText.GetText("Diag.Label.EvidenceClass"), UiText.GetText($"Diag.Evidence.{entry.EvidenceClass}"), false),
            new(UiText.GetText("Diag.Label.Quality"), Quality, false)
        };
        if (entry.Source.Date is { } date)
        {
            rows.Add(new DiagLineVm(UiText.GetText("Diag.Label.Date"), date));
        }

        var applies = new List<string>();
        applies.AddRange(entry.Applies.TitleIds);
        applies.AddRange(new[] { entry.Applies.Platform, entry.Applies.Architecture, entry.Applies.Renderer, entry.Applies.GpuVendor }
            .Where(text => !string.IsNullOrWhiteSpace(text))!);
        rows.Add(new DiagLineVm(UiText.GetText("Diag.Label.Applies"), applies.Count == 0 ? UiText.GetText("Diag.Value.NotRestricted") : string.Join(" | ", applies), applies.Count > 0));
        if (entry.FirstBrokenBuild is { } first)
        {
            var range = entry.FixedBuild is { } fixedBuild
                ? string.Create(CultureInfo.InvariantCulture, $"{first} - {fixedBuild - 1}")
                : string.Create(CultureInfo.InvariantCulture, $"{first} +");
            rows.Add(new DiagLineVm(UiText.GetText("Diag.Label.Affected"), range));
        }

        if (entry.FixedBuild is { } fixedIn)
        {
            rows.Add(new DiagLineVm(UiText.GetText("Diag.Label.FixedIn"), fixedIn.ToString(CultureInfo.InvariantCulture)));
        }

        rows.AddRange(finding.Reasons.Select(reason => new DiagLineVm(UiText.GetText("Diag.Label.Basis"), reason)));
        Rows = rows;

        Solutions =
        [
            .. finding.Solutions.Select(solution => Solve(solution, builds, host))
        ];
        NoSolution = Solutions.Count == 0 ? UiText.GetText("Diag.Solution.None") : string.Empty;
    }

    public DiagnosticFinding Model { get; }

    public string Title { get; }

    public string Symptom { get; }

    public string Quality { get; }

    public string SourceText { get; }

    public string? SourceUrl { get; }

    public bool HasSourceUrl => SourceUrl is not null;

    public IReadOnlyList<DiagLineVm> Rows { get; }

    public IReadOnlyList<DiagSolutionVm> Solutions { get; }

    public string NoSolution { get; }

    public bool HasNoSolution => NoSolution.Length > 0;

    public string ViewEvidenceLabel => UiText.GetText("Diag.ViewEvidence");

    internal static DiagSolutionVm Solve(DiagnosticSolution solution, Rpcs3BuildCatalog? builds, string host)
    {
        var label = UiText.GetText($"Diag.Solution.{solution.Kind}");
        var detail = solution.Build is { } build
            ? build.ToString(CultureInfo.InvariantCulture)
            : solution.Setting ?? solution.Text ?? string.Empty;
        string? url = null;
        if (solution.Kind is DiagnosticSolutionKind.FixedBuild or DiagnosticSolutionKind.KnownGoodBuild && solution.Build is { } number)
        {
            var resolved = (builds ?? Rpcs3BuildCatalog.Empty).ResolveBuildNumber(number, host);
            url = resolved.State == Rpcs3BuildResolutionState.ResolvedOfficialGitHub && WikiLinks.IsSafeUrl(resolved.DownloadUrl)
                ? resolved.DownloadUrl
                : null;
        }

        return new DiagSolutionVm(label, detail, url, UiText.GetText("Diag.DownloadBuild"));
    }
}

// Localized names of what an event is evidence of.
internal static class SymptomText
{
    public static string Text(LogCategory category) => UiText.GetText($"Diag.Symptom.{category}");
}

// Domain 5 Log Diagnostics. It presents one DiagnosticReport (read-only facts):
// a concise overview, the session and timeline, the observed events (raw lines
// under Details) and the documented evidence and solutions. Nothing here scans,
// writes or decides a cause.
public sealed class LogDiagVm : VmBase
{
    private LogDiagState _state = LogDiagState.NotRun;
    private DiagnosticReport _report = DiagnosticReport.Empty;
    private IReadOnlyList<string> _notes = [];
    private RuntimeContextSnapshot? _context;
    private Rpcs3BuildCatalog? _builds;
    private string _host = Rpcs3BuildCatalog.WindowsPlatform;
    private string? _evidenceError;
    private int _selectedTab;

    public LogDiagVm(ICommand? analyze = null)
    {
        AnalyzeCmd = analyze ?? new RelayCmd(() => { }, () => false);
        ShowEventsCmd = new RelayCmd(() => SelectedTab = TabLogs);
        ShowEvidenceCmd = new RelayCmd(() => SelectedTab = TabEvidence);
    }

    public const int TabOverview = 0;
    public const int TabHardware = 1;
    public const int TabLogs = 2;
    public const int TabEvidence = 3;

    public ICommand AnalyzeCmd { get; }

    public ICommand ShowEventsCmd { get; }

    public ICommand ShowEvidenceCmd { get; }

    public LogDiagState State => _state;

    public DiagnosticReport Report => _report;

    public int SelectedTab
    {
        get => _selectedTab;
        set => Set(ref _selectedTab, value);
    }

    public bool IsRunning => _state == LogDiagState.Running;

    public bool IsReady => _state == LogDiagState.Ready;

    public bool HasProblems => IsReady && _report.Session.Problems.Count > 0;

    public string StateText => _state switch
    {
        LogDiagState.Running => UiText.GetText("Diag.State.Running"),
        LogDiagState.NoLog => UiText.GetText("Diag.State.NoLog"),
        LogDiagState.Ready when !HasProblems => UiText.GetText(_notes.Contains("LogHasNoGameBoot") ? "Diag.State.NoBoot" : "Diag.State.NoProblem"),
        LogDiagState.Ready => string.Empty,
        _ => UiText.GetText("Diag.State.NotRun")
    };

    public bool HasStateText => StateText.Length > 0;

    public string AnalyzeLabel => UiText.GetText(IsReady || _state == LogDiagState.NoLog ? "Diag.Reanalyze" : "Diag.Analyze");

    public string NotesText => string.Join("  |  ", _notes.Select(note =>
        note.StartsWith("Unreadable:", StringComparison.Ordinal)
            ? UiText.Fmt("Diag.Note.Unreadable", note["Unreadable:".Length..])
            : UiText.GetText($"Diag.Note.{note}")));

    public bool HasNotes => _notes.Count > 0;

    public string EvidenceErrorText => _evidenceError is null ? string.Empty : UiText.GetText("Diag.Note.KnowledgeInvalid");

    // ------------------------------------------------------------ overview

    public IReadOnlyList<DiagLineVm> OverviewRows { get; private set; } = [];

    public string ActionText { get; private set; } = string.Empty;

    public bool HasAction => ActionText.Length > 0;

    public string? EvidenceUrl { get; private set; }

    public bool HasEvidenceUrl => EvidenceUrl is not null;

    public string? DownloadUrl { get; private set; }

    public bool HasDownloadUrl => DownloadUrl is not null;

    public string ViewEvidenceLabel => UiText.GetText("Diag.ViewEvidence");

    public string ViewEventsLabel => UiText.GetText("Diag.ViewEvents");

    public string DownloadLabel => UiText.GetText("Diag.DownloadBuild");

    // ---------------------------------------------------------- log details

    public string SourceText { get; private set; } = string.Empty;

    public IReadOnlyList<DiagLineVm> SessionRows { get; private set; } = [];

    public IReadOnlyList<DiagLineVm> SettingRows { get; private set; } = [];

    public IReadOnlyList<DiagTimelineVm> Timeline { get; private set; } = [];

    public string TimelineText { get; private set; } = string.Empty;

    public IReadOnlyList<DiagEventVm> Events { get; private set; } = [];

    public IReadOnlyList<DiagEventVm> MinorEvents { get; private set; } = [];

    public IReadOnlyList<string> RawLines { get; private set; } = [];

    public bool HasTimeline => Timeline.Count > 0 && IsReady;

    public bool HasEvents => Events.Count > 0;

    public bool HasMinorEvents => MinorEvents.Count > 0;

    public bool HasRawLines => RawLines.Count > 0;

    public bool HasSettings => SettingRows.Count > 0;

    public string DetailsLabel => UiText.GetText("Ui.Details");

    // ------------------------------------------------------------ evidence

    public IReadOnlyList<DiagFindingVm> Findings { get; private set; } = [];

    public bool HasFindings => Findings.Count > 0;

    public void SetContext(RuntimeContextSnapshot? context, Rpcs3BuildCatalog? builds, string host)
    {
        _context = context;
        _builds = builds;
        _host = host;
    }

    public void MarkRunning()
    {
        _state = LogDiagState.Running;
        Raise();
    }

    // The previous game's report must not stay on screen for another game.
    public void Clear()
    {
        _report = DiagnosticReport.Empty;
        _notes = [];
        _evidenceError = null;
        _state = LogDiagState.NotRun;
        Rebuild();
    }

    public void Load(LogDiagnosticsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _report = result.Report;
        _notes = result.Notes;
        _evidenceError = result.EvidenceError;
        _state = result.Sessions.Count == 0 ? LogDiagState.NoLog : LogDiagState.Ready;
        Rebuild();
    }

    // Rebuild so every localized label follows the language toggle.
    public void SetLang() => Rebuild();

    private void Rebuild()
    {
        var session = _report.Session;
        var primary = _report.Primary;

        SourceText = session.Source is null
            ? string.Empty
            : $"{UiText.GetText($"Diag.SourceKind.{session.Source.Kind}")}: {System.IO.Path.GetFileName(session.Source.Path)}";
        SessionRows = _state == LogDiagState.Ready ? BuildSession(session) : [];
        SettingRows = _state == LogDiagState.Ready ? BuildSettings(session) : [];
        Timeline = [.. session.Timeline.Select(step => new DiagTimelineVm(
            StageText(step.Stage),
            TimeSpan.FromSeconds(step.Seconds).ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture),
            step.Detail ?? string.Empty,
            step.Line))];
        TimelineText = string.Join("  >  ", Timeline.Select(step => step.Stage));
        Events = [.. session.Problems.Where(item => !item.IsMinor).OrderBy(item => item.Line).Select(item => new DiagEventVm(item))];
        MinorEvents = [.. session.Problems.Where(item => item.IsMinor).OrderBy(item => item.Line).Select(item => new DiagEventVm(item))];
        RawLines = session.RawEventLines;
        Findings = [.. _report.Findings.Where(item => item.Kind != DiagnosisKind.ObservedEvent).Select(item => new DiagFindingVm(item, _builds, _host))];

        BuildOverview(session, primary);
        Raise();
    }

    private void BuildOverview(LogSession session, DiagnosticFinding? primary)
    {
        var rows = new List<DiagLineVm>();
        ActionText = string.Empty;
        EvidenceUrl = null;
        DownloadUrl = null;

        if (_state == LogDiagState.Ready && primary?.Event is { } observed)
        {
            var symptom = SymptomText.Text(observed.Primary);
            var before = session.Timeline.LastOrDefault(step => step.Line < observed.Line && !step.Stage.StartsWith("Problem:", StringComparison.Ordinal));
            rows.Add(new DiagLineVm(
                UiText.GetText("Diag.Overview.Detected"),
                before is null ? symptom : UiText.Fmt("Diag.Overview.After", symptom, StageText(before.Stage)),
                false));
        }

        rows.AddRange(BuildEnvironment(session));

        if (_state == LogDiagState.Ready && primary is not null)
        {
            if (primary.Entry is { } entry)
            {
                var line = $"{UiText.GetText($"Diag.Kind.{primary.Kind}")} - {UiText.GetText("Diag.Source.GitHubIssue")} #{entry.Source.Number?.ToString(CultureInfo.InvariantCulture)}";
                rows.Add(new DiagLineVm(UiText.GetText("Diag.Overview.Evidence"), line, false));
                if (entry.FirstBrokenBuild is { } first)
                {
                    rows.Add(new DiagLineVm(
                        UiText.GetText("Diag.Label.Affected"),
                        entry.FixedBuild is { } fixedBuild
                            ? string.Create(CultureInfo.InvariantCulture, $"{first} - {fixedBuild - 1}")
                            : string.Create(CultureInfo.InvariantCulture, $"{first} +")));
                }

                if (entry.FixedBuild is { } fixedIn)
                {
                    rows.Add(new DiagLineVm(UiText.GetText("Diag.Label.FixedIn"), fixedIn.ToString(CultureInfo.InvariantCulture)));
                }

                rows.Add(new DiagLineVm(
                    UiText.GetText("Diag.Overview.Confidence"),
                    $"{UiText.GetText($"Diag.Evidence.{entry.EvidenceClass}")} ({UiText.GetText($"Diag.Quality.{primary.Quality}")})",
                    false));
                EvidenceUrl = WikiLinks.IsSafeUrl(entry.Source.Url) ? entry.Source.Url : null;

                var fixSolution = primary.Solutions.FirstOrDefault(item => item.Kind == DiagnosticSolutionKind.FixedBuild);
                if (fixSolution is not null)
                {
                    var solved = DiagFindingVm.Solve(fixSolution, _builds, _host);
                    ActionText = UiText.Fmt("Diag.Action.TestFixed", solved.Detail);
                    DownloadUrl = solved.DownloadUrl;
                }
                else if (primary.Solutions.Any(item => item.Kind == DiagnosticSolutionKind.KnownUnresolved))
                {
                    ActionText = UiText.GetText("Diag.Solution.KnownUnresolved");
                }
                else if (primary.Solutions.Count == 0)
                {
                    ActionText = primary.Kind == DiagnosisKind.KnownFixMatch
                        ? UiText.GetText("Diag.Action.FixIncluded")
                        : UiText.GetText("Diag.Solution.None");
                }
            }
            else if (primary.Kind == DiagnosisKind.UnknownCause)
            {
                rows.Add(new DiagLineVm(UiText.GetText("Diag.Overview.Cause"), UiText.GetText("Diag.Cause.Unknown"), false));
                ActionText = UiText.GetText("Diag.Solution.None");
            }
        }

        OverviewRows = rows;
    }

    private IEnumerable<DiagLineVm> BuildEnvironment(LogSession session)
    {
        string? Fact(string key) => _context?.Find(key) is { Value: { } value } ? value : null;
        var build = Fact("CurrentBuild") ?? (session.Build is null ? null : $"{session.Build.VersionText}-{session.Build.BuildNumber}");
        if (build is not null)
        {
            yield return new DiagLineVm(UiText.GetText("Diag.Label.Build"), build);
        }

        var gpu = Fact("Gpu") ?? session.GpuName;
        if (gpu is not null)
        {
            yield return new DiagLineVm(UiText.GetText("Diag.Label.Gpu"), gpu);
        }

        var windows = Fact("Os");
        if (windows is not null)
        {
            yield return new DiagLineVm(UiText.GetText("Diag.Label.Windows"), windows);
        }

        var serial = Fact("GameSerial") ?? session.GameSerial;
        var version = Fact("GameVersion") ?? session.AppVersion;
        if (serial is not null)
        {
            yield return new DiagLineVm(UiText.GetText("Diag.Label.Game"), version is null ? serial : $"{serial}  {version}");
        }
    }

    private List<DiagLineVm> BuildSession(LogSession session)
    {
        var rows = new List<DiagLineVm>();
        void Add(string key, string? value, string? note = null)
        {
            rows.Add(new DiagLineVm(UiText.GetText(key), value ?? UiText.GetText("Diag.Value.NotInLog"), value is not null, note));
        }

        var runtimeSerial = _context?.Find("GameSerial")?.Value;
        Add("Diag.Label.Serial", session.GameSerial,
            session.GameSerial is null || runtimeSerial is null ? null : UiText.GetText(Same(session.GameSerial, runtimeSerial) ? "Diag.Corr.Match" : "Diag.Corr.Mismatch"));
        var runtimeVersion = _context?.Find("GameVersion")?.Value;
        Add("Diag.Label.AppVer", session.AppVersion,
            session.AppVersion is null || runtimeVersion is null ? null : UiText.GetText(Same(session.AppVersion, runtimeVersion) ? "Diag.Corr.Match" : "Diag.Corr.Mismatch"));
        Add("Diag.Label.PpuHash", session.PpuHashes.Count == 0 ? null : string.Join(", ", session.PpuHashes));
        var runtimeBuild = _context?.Find("CurrentBuild")?.Value;
        var logBuild = session.Build is null ? null : $"{session.Build.VersionText}-{session.Build.BuildNumber}";
        Add("Diag.Label.Build", logBuild,
            logBuild is null || runtimeBuild is null ? null : UiText.GetText(logBuild == runtimeBuild ? "Diag.Corr.Match" : "Diag.Corr.Mismatch"));
        Add("Diag.Label.Commit", session.Build?.Commit);
        Add("Diag.Label.Cpu", session.CpuLine);
        Add("Diag.Label.Gpu", session.GpuName);
        Add("Diag.Label.Renderer", session.Renderer);
        Add("Diag.Label.Windows", session.OsLine);



        // Run facts the log states in its own words; a row exists only when the

        // log said it, so an absent line is never shown as a fact.

        var run = session.RunContext;

        void AddStated(string key, string? value, string? note = null)

        {

            if (value is not null)

            {

                rows.Add(new DiagLineVm(UiText.GetText(key), value, true, note));

            }

        }



        AddStated("Diag.Label.Firmware", run.FirmwareVersion);

        AddStated("Diag.Label.GpuDriver", run.GpuDriver);

        AddStated("Diag.Label.PackageVersion", run.PackageVersion);

        AddStated("Diag.Label.ConfigMode", run.ConfigMode,

            run.CustomConfigFile ? UiText.GetText("Diag.Note.CustomConfigFile") : null);

        AddStated("Diag.Label.BootMedia", run.BootMedia == LogBootMedia.IsoArchive ? UiText.GetText("Diag.Value.IsoArchive") : null);

        AddStated("Diag.Label.IsoEncryption", run.IsoEncryption);

        AddStated("Diag.Label.IsoKey", run.IsoKey switch

        {

            LogIsoKeyState.Missing => UiText.GetText("Diag.Value.KeyMissing"),

            LogIsoKeyState.Found => UiText.GetText("Diag.Value.KeyFound"),

            _ => null

        });

        AddStated("Diag.Label.LogCompleteness", run.Completeness switch
        {
            LogCompleteness.Complete => UiText.GetText("Diag.Value.Complete"),
            LogCompleteness.Partial => UiText.GetText("Diag.Value.Partial"),
            _ => UiText.GetText("Diag.Value.CompletenessUnknown")
        });
        AddStated("Diag.Label.LogHash", session.Analysis?.LogSha256 is { Length: >= 16 } hash ? hash[..16].ToLowerInvariant() + "…" : null);
        foreach (var observation in run.ObservationList)
        {
            AddStated(
                "Diag.Obs." + observation.Kind,
                observation.Value is null
                    ? (observation.Count > 1 ? $"×{observation.Count}" : "·")
                    : observation.Count > 1 ? $"{observation.Value}  ×{observation.Count}" : observation.Value);
        }

        AddStated("Diag.Label.DataPath", run.DataPath);
        if (run.DataPathTraitList.Count > 0)
        {
            AddStated("Diag.Label.DataPathTraits", string.Join(", ", run.DataPathTraitList.Select(trait => UiText.GetText("Diag.Trait." + trait))));
        }

        if (run.FailedDiscPathList.Count > 0)
        {
            // Observed anomaly only; an IRD is what would verify a mismatch.
            AddStated("Diag.Label.DiscFileFailures", run.FailedDiscPathList.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UiText.GetText("Diag.Note.IrdNotChecked"));
        }

        foreach (var error in run.Win32ErrorList)
        {
            AddStated("Diag.Label.OsError", error.Symbol is null ? $"0x{error.Code:x}" : $"0x{error.Code:x} {error.Symbol}", error.Description);
        }

        if (run.Performance is { } perf)
        {
            AddStated(
                "Diag.Label.CpuUsage",
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{perf.CpuMedian:0.#}% (5–95%: {perf.CpuP05:0.#}–{perf.CpuP95:0.#}), n={perf.Samples}"));
            if (perf.RamSamples > 0)
            {
                AddStated(
                    "Diag.Label.RamUsage",
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{perf.RamMedianMb} MB (peak {perf.RamPeakMb} MB), n={perf.RamSamples}"));
            }
        }

        AddStated("Diag.Label.AppliedPatches", run.AppliedPatches.Count == 0

            ? null

            : string.Join(", ", run.AppliedPatches.Select(patch => patch.Description).Distinct(StringComparer.Ordinal)));

        return rows;
    }

    // The settings that matter for troubleshooting, exactly as RPCS3 printed them.
    private static readonly string[] RelevantSettings =
    [
        "Video/Renderer", "Core/PPU Decoder", "Core/SPU Decoder", "Video/Resolution Scale", "Video/Shader Mode",
        "Video/Vulkan/Adapter", "Video/Frame limit", "Video/VSync", "Video/Write Color Buffers", "Core/Preferred SPU Threads",
        "Audio/Renderer"
    ];

    private static IReadOnlyList<DiagLineVm> BuildSettings(LogSession session) =>
    [
        .. session.Settings
            .Where(setting => RelevantSettings.Contains(setting.Path, StringComparer.Ordinal))
            .Select(setting => new DiagLineVm(setting.Path, setting.Value))
    ];

    private static string StageText(string stage) =>
        stage.StartsWith("Problem:", StringComparison.Ordinal) && Enum.TryParse<LogCategory>(stage["Problem:".Length..], out var category)
            ? SymptomText.Text(category)
            : UiText.GetText($"Diag.Stage.{stage}");

    private static bool Same(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private void Raise()
    {
        foreach (var name in new[]
                 {
                     nameof(State), nameof(Report), nameof(IsRunning), nameof(IsReady), nameof(HasProblems), nameof(StateText), nameof(HasStateText),
                     nameof(AnalyzeLabel), nameof(NotesText), nameof(HasNotes), nameof(EvidenceErrorText), nameof(OverviewRows), nameof(ActionText),
                     nameof(HasAction), nameof(EvidenceUrl), nameof(HasEvidenceUrl), nameof(DownloadUrl), nameof(HasDownloadUrl), nameof(ViewEvidenceLabel),
                     nameof(ViewEventsLabel), nameof(DownloadLabel), nameof(SourceText), nameof(SessionRows), nameof(SettingRows), nameof(Timeline),
                     nameof(TimelineText), nameof(HasTimeline), nameof(Events), nameof(MinorEvents), nameof(RawLines), nameof(HasEvents), nameof(HasMinorEvents),
                     nameof(HasRawLines), nameof(HasSettings), nameof(DetailsLabel), nameof(Findings), nameof(HasFindings)
                 })
        {
            OnChange(name);
        }
    }
}
