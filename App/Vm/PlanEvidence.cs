using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

// One RPCS3 Settings tab of the selected game and the settings under it. The
// tab is internal metadata (mapping, ordering, validation); it is never shown
// as a header in the Game Settings list.
public sealed class PlanSectionVm : VmBase
{
    public PlanSectionVm(Rpcs3SettingsSection section)
    {
        Section = section;
    }

    public Rpcs3SettingsSection Section { get; }

    public string Title => UiText.GetText($"Section.{Section}");

    public ObservableCollection<PlanItVm> Items { get; } = [];

    public void SetLang()
    {
        OnChange(nameof(Title));
    }
}

// A Wiki note shown beside the settings. Only notes confirmed for the detected
// game context reach the view; the rest stay in the model as evidence.
// One ambiguous Wiki range as the Settings screen shows it: the RPCS3 setting
// it is about (technical, LTR) and the Wiki's own advice as a sentence. There
// is no value, no checkbox and no apply: R3Config never picks a number from a
// range.
public sealed record PlanRangeVm(string Path, string Text);

public sealed class GameNoteVm
{
    // The Wiki's default text for a page with no known issues carries no
    // instruction, so it is not worth screen space.
    private const string NoIssuesBoilerplate = "There are no reported issues with this title.";

    public GameNoteVm(GameNote note)
    {
        Model = note;
    }

    public GameNote Model { get; }

    public WikiFreeTextNote Note => Model.Note;

    // The original Wiki wording, unchanged.
    public string Text => Note.Text;

    // Show a reviewed Arabic translation when one ships. Otherwise say that a
    // reviewed translation is unavailable and keep the Wiki wording in the
    // per-note details. Nothing is machine-translated here.
    public string DisplayText
    {
        get
        {
            var result = NoteSvc.Trans(Note.Text);
            if (!UiText.IsRtl || NoteSvc.ContainsArabic(result.Body))
            {
                return string.IsNullOrWhiteSpace(result.Body) ? Note.Text : result.Body;
            }

            return UiText.GetText("Wiki.ArabicUnavailable");
        }
    }

    public bool IsOriginalText =>
        UiText.IsRtl && DisplayText == Note.Text && !NoteSvc.ContainsArabic(Note.Text);

    public string OriginalText => Note.Text;

    public string SourceUrl => "https://wiki.rpcs3.net/index.php?title=" + Uri.EscapeDataString(Note.Page.Replace(' ', '_'));

    public string DetailsLabel => UiText.GetText("Wiki.ShowOriginalDetails");

    public bool IsNoise =>
        Note.Text.Trim().Equals(NoIssuesBoilerplate, StringComparison.OrdinalIgnoreCase);

    public string Title => UiText.GetText($"Note.Class.{Note.Class}");

    public string Applicability =>
        UiText.GetText($"Note.Applicability.{Model.Applicability}");

    public string Header => Title;

    public bool IsGeneral => Model.Applicability == WikiNoteApplicability.General;

    public bool IsOtherScope => !IsGeneral;

    public IReadOnlyList<string> Scope => BuildScope();

    public bool HasScope => Scope.Count > 0;

    public string ScopeLine => string.Join("  |  ", Scope);

    private List<string> BuildScope()
    {
        var scope = new List<string>();

        Add(scope, "Serial", Note.SerialCondition);
        Add(scope, "Version", Note.VersionCondition);
        Add(scope, "Edition", Note.EditionCondition);
        Add(scope, "Platform", Note.PlatformCondition);
        Add(scope, "Hardware", Note.HardwareCondition);
        Add(scope, "Rpcs3Build", Note.Rpcs3BuildCondition);

        if (Note.PatchDependent)
        {
            scope.Add(UiText.GetText("Note.Scope.Patch"));
        }

        return scope;
    }

    private static void Add(List<string> scope, string kind, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            scope.Add($"{UiText.GetText($"Note.Scope.{kind}")}: {value}");
        }
    }
}

// Url is set only when the visible value itself has an exact official target.
// ActionUrl/ActionLabel is a separate action on the row (download of the exact
// RPCS3 build named by the row); it exists only when the shipped build catalog
// resolves that build.
public sealed record OnlineRowVm(
    string Id,
    string Label,
    string Value,
    string? Url = null,
    string? ActionLabel = null,
    string? ActionUrl = null)
{
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionUrl);
}

// Online/RPCN evidence for one Wiki {{netplay}} table, in a compact form: a
// short summary first (support, tested game version, tested RPCS3 build with
// its exact download when known, custom servers), the yes/no matrix and
// workaround text behind a details toggle. A field the Wiki did not state is
// left out of the summary, and the tested game version is never presented as a
// required one.
public sealed class GameOnlineVm
{
    private static readonly string[] SummaryIds =
        ["State", "TestedGameVersion", "RequiredGameVersion", "CurrentRpcs3Build", "Rpcs3VersionTested", "RequiresCustomServers"];

    public GameOnlineVm(
        WikiOnlineEvidence evidence,
        string? exactBuildUrl = null,
        string? currentBuild = null)
    {
        Evidence = evidence;
        ExactBuildUrl = WikiLinks.IsSafeUrl(exactBuildUrl) ? exactBuildUrl : null;
        CurrentBuild = string.IsNullOrWhiteSpace(currentBuild) ? null : currentBuild.Trim();
    }

    public WikiOnlineEvidence Evidence { get; }

    // The exact official archive of the tested RPCS3 build, or null.
    public string? ExactBuildUrl { get; }

    // The installed RPCS3 build from the Runtime Context, or null.
    public string? CurrentBuild { get; }

    // The tested build is the installed one: nothing to compare or download.
    public bool TestedIsCurrent =>
        SameBuild(Evidence.Rpcs3VersionTested, CurrentBuild);

    // Installed and tested builds both known and different: both are shown, as
    // information only. The tested build is never called required.
    public bool ShowCurrentBuild =>
        CurrentBuild is not null &&
        !string.IsNullOrWhiteSpace(Evidence.Rpcs3VersionTested) &&
        !TestedIsCurrent;

    private static bool SameBuild(string? tested, string? current)
    {
        return Rpcs3BuildCatalog.TryParse(tested, out var testedVersion, out var testedBuild) &&
               Rpcs3BuildCatalog.TryParse(current, out var currentVersion, out var currentBuild) &&
               testedVersion == currentVersion &&
               testedBuild == currentBuild;
    }
    public string State =>
        UiText.GetText($"Online.State.{Evidence.State}");

    public IReadOnlyList<OnlineRowVm> Rows => BuildRows();

    public IReadOnlyList<OnlineRowVm> SummaryRows =>
        [.. Rows.Where(row =>
            SummaryIds.Contains(row.Id, StringComparer.Ordinal) &&
            (row.Id == "State" || row.Value != UiText.GetText("Online.Value.Unknown")))];

    public IReadOnlyList<OnlineRowVm> DetailRows =>
        [.. Rows.Where(row => !SummaryIds.Contains(row.Id, StringComparer.Ordinal))];

    public bool HasDetails =>
        DetailRows.Any(row => row.Value != UiText.GetText("Online.Value.Unknown")) || HasNotes;

    public string DetailsLabel => UiText.GetText("Online.Details");

    public bool ShowTestedHint =>
        !string.IsNullOrWhiteSpace(Evidence.TestedGameVersion) &&
        string.IsNullOrWhiteSpace(Evidence.RequiredGameVersion);

    public string TestedHint => UiText.GetText("Online.TestedHint");

    public bool HasNotes => !string.IsNullOrWhiteSpace(Evidence.Notes);

    // The Wiki wording; the view copies IP/Host switches from it, never from a translation.
    public string RawNotes => Evidence.Notes ?? string.Empty;

    // Same reviewed-translation path as free-text Wiki notes. Only an exact
    // reviewed Arabic text replaces the Wiki wording; otherwise the original is
    // kept (these notes are often technical, e.g. "RPCN 1.1 - 2024/02/09").
    public string Notes
    {
        get
        {
            var raw = Evidence.Notes;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var result = NoteSvc.Trans(raw);
            return result.Quality == NoteQual.Exact && NoteSvc.ContainsArabic(result.Body)
                ? result.Body
                : raw;
        }
    }

    private List<OnlineRowVm> BuildRows()
    {
        var rows = new List<OnlineRowVm>
        {
            new("State", UiText.GetText("Online.Support"), State),
            Row("TestedGameVersion", "Online.TestedVersion", Evidence.TestedGameVersion, raw: true)
        };

        // Only an explicit Wiki requirement earns a Required Version row.
        if (!string.IsNullOrWhiteSpace(Evidence.RequiredGameVersion))
        {
            rows.Add(Row("RequiredGameVersion", "Online.RequiredVersion", Evidence.RequiredGameVersion, raw: true));
        }

        if (ShowCurrentBuild)
        {
            rows.Add(Row("CurrentRpcs3Build", "Online.Rpcs3Current", CurrentBuild, raw: true));
        }

        rows.Add(Row(
            "Rpcs3VersionTested",
            "Online.Rpcs3Build",
            Evidence.Rpcs3VersionTested,
            raw: true,
            actionUrl: TestedIsCurrent ? null : ExactBuildUrl));
        rows.Add(Row("Connects", "Online.Connects", Evidence.Connects));
        rows.Add(Row("CreateJoinLobbies", "Online.Lobbies", Evidence.CreateJoinLobbies));
        rows.Add(Row("MatchWithOthers", "Online.Match", Evidence.MatchWithOthers));
        rows.Add(Row("OnlineFeaturesWorking", "Online.Features", Evidence.OnlineFeaturesWorking));
        rows.Add(Row("RequiresCustomServers", "Online.CustomServers", Evidence.RequiresCustomServers));
        return rows;
    }

    private static OnlineRowVm Row(
        string id,
        string labelKey,
        string? value,
        bool raw = false,
        string? url = null,
        string? actionUrl = null)
    {
        var missing = string.IsNullOrWhiteSpace(value);
        var display = missing
            ? UiText.GetText("Online.Value.Unknown")
            : raw
                ? value!.Trim()
                : Localize(value!.Trim());

        return new OnlineRowVm(
            id,
            UiText.GetText(labelKey),
            display,
            missing ? null : url,
            missing || actionUrl is null ? null : UiText.GetText("Online.DownloadBuild"),
            missing ? null : actionUrl);
    }

    private static string Localize(string value)
    {
        var key = value.ToLowerInvariant() switch
        {
            "yes" => "Yes",
            "no" => "No",
            "partial" => "Partial",
            "untested" => "Untested",
            "n/a" => "NotApplicable",
            "not required" => "NotRequired",
            "available" => "Available",
            "not available" => "NotAvailable",
            _ => null
        };

        return key is null
            ? value
            : UiText.GetText($"Online.Value.{key}");
    }
}

// Notes and online evidence of the selected game. Absent or non-applicable
// evidence stays out of the view: there is no "not supported" placeholder and
// no "other version/platform" card.
public sealed class GameEvidenceVm : VmBase
{
    private IReadOnlyList<GameNoteVm> _notes = [];
    private IReadOnlyList<GameNoteVm> _otherNotes = [];
    private IReadOnlyList<GameOnlineVm> _online = [];
    private bool _onlineOtherVersionOnly;
    private bool _onlineVersionUnknown;
    private IReadOnlyList<string> _knowledgeLines = [];
    private GameSettingsModel? _model;

    // Applicable to this game: shown.
    public IReadOnlyList<GameNoteVm> Notes
    {
        get => _notes;
        private set => Set(ref _notes, value);
    }

    // Scoped to another version, edition, platform, serial, or hardware, or not
    // confirmable from the detected context: kept as evidence, never bound.
    public IReadOnlyList<GameNoteVm> OtherNotes
    {
        get => _otherNotes;
        private set => Set(ref _otherNotes, value);
    }

    // Online tables whose tested game version matches the installed version.
    public IReadOnlyList<GameOnlineVm> Online
    {
        get => _online;
        private set => Set(ref _online, value);
    }

    public bool HasNotes => _notes.Count > 0;

    public bool HasOtherNotes => _otherNotes.Count > 0;

    public bool HasOnline => _online.Count > 0;

    // RPCN evidence exists, but the installed version is known and differs.
    public bool HasOnlineMismatch => _onlineOtherVersionOnly && !_onlineVersionUnknown;

    // RPCN evidence exists, but the installed version could not be determined:
    // nothing version-scoped may be shown as applicable.
    public bool HasOnlineUnknownVersion => _onlineOtherVersionOnly && _onlineVersionUnknown;

    public string OnlineMismatchText => UiText.GetText("Online.NoMatchingVersion");

    public string OnlineUnknownVersionText => UiText.GetText("Online.UnknownVersion");

    // History and community hardware evidence for the game's exact Wiki page: informational lines,
    // never settings and never applied.
    public IReadOnlyList<string> KnowledgeLines
    {
        get => _knowledgeLines;
        private set => Set(ref _knowledgeLines, value);
    }

    public bool HasKnowledge => _knowledgeLines.Count > 0;

    public string KnowledgeTitle => UiText.GetText("Knowledge.Title");

    public bool HasAny => HasNotes || HasOnline || HasOnlineMismatch || HasOnlineUnknownVersion || HasKnowledge;

    public bool ShowOriginalCaption => _notes.Any(note => note.IsOriginalText);

    public void Load(GameSettingsModel? model)
    {
        _model = model;
        var notes = model?.Notes
            .Select(note => new GameNoteVm(note))
            .Where(note => !note.IsNoise)
            .ToArray() ?? [];
        Notes = notes.Where(note => note.IsGeneral).ToArray();
        OtherNotes = notes.Where(note => note.IsOtherScope).ToArray();
        Online = model?.OnlineForInstalled
            .Select(online => new GameOnlineVm(online, model.ExactBuildUrl(online.Rpcs3VersionTested), model.CurrentRpcs3Build))
            .ToArray() ?? [];
        _onlineOtherVersionOnly = model?.HasOnlineForOtherVersionOnly == true;
        _onlineVersionUnknown = model?.OnlineVersionUnknown == true;
        KnowledgeLines = BuildKnowledgeLines(model?.GameKnowledge);
        Raise();
    }

    private const int MaxKnowledgeLines = 8;

    // Technical values (setting names, core/thread configs, grades) stay left-to-right inside an
    // Arabic sentence; digits are Western.
    private static string Ltr(string value) => "⁦" + value + "⁩";

    private static List<string> BuildKnowledgeLines(GameKnowledge? knowledge)
    {
        var lines = new List<string>();
        if (knowledge is null || !knowledge.HasAny)
        {
            return lines;
        }

        foreach (var item in knowledge.Removals.OrderBy(item => item.Fact.WikiKey, StringComparer.Ordinal))
        {
            lines.Add(UiText.Fmt($"Knowledge.Removal.{item.Fact.Reason}", Ltr(item.Fact.WikiKey)));
        }

        foreach (var item in knowledge.Lifecycles)
        {
            var commit = item.Fact.LastChangeCommit!;
            lines.Add(UiText.Fmt(
                "Knowledge.Lifecycle.Removed",
                Ltr(item.Fact.DisplayName),
                item.Fact.LastChangeBuild!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Ltr(commit[..8])));
        }

        foreach (var item in knowledge.ThreadScaling)
        {
            var parts = new List<string> { item.Fact.CoreThreadConfig };
            if (item.Fact.FpsTarget is not null)
            {
                parts.Add($"{item.Fact.FpsTarget} FPS");
            }

            parts.Add(item.Fact.Grade);
            lines.Add(UiText.Fmt("Knowledge.Scaling", Ltr(string.Join("  |  ", parts))));
        }

        if (lines.Count > MaxKnowledgeLines)
        {
            var extra = lines.Count - (MaxKnowledgeLines - 1);
            lines.RemoveRange(MaxKnowledgeLines - 1, extra);
            lines.Add(UiText.Fmt("Knowledge.More", extra.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return lines;
    }

    public void Clear()
    {
        Load(null);
    }

    // Rebuild so every localized label follows the language toggle.
    public void SetLang()
    {
        Load(_model);
    }

    private void Raise()
    {
        OnChange(nameof(HasNotes));
        OnChange(nameof(HasOtherNotes));
        OnChange(nameof(HasOnline));
        OnChange(nameof(HasOnlineMismatch));
        OnChange(nameof(HasOnlineUnknownVersion));
        OnChange(nameof(OnlineMismatchText));
        OnChange(nameof(OnlineUnknownVersionText));
        OnChange(nameof(ShowOriginalCaption));
        OnChange(nameof(HasKnowledge));
        OnChange(nameof(KnowledgeTitle));
        OnChange(nameof(HasAny));
    }
}
