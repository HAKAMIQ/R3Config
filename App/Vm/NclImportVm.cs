using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed record NclDialectChoice(
    string Name,
    NclDialect Value);

// The two NCL sources the dialog offers. The built-in corpus needs no file and
// no dialect question; an external file needs both. Selecting one is the second
// step of the flow, before any source-specific control is shown.
public sealed record NclSourceChoice(
    string Name,
    bool IsExternal);

// One selector per referenced Artemis option placeholder. The user picks by
// Label; Selected.Value keeps the canonical raw source value that is handed to
// conversion, so nothing is ever inferred from the displayed text.
public sealed class NclOptionSelectorVm : VmBase
{
    private readonly Action _onChanged;
    private NclManagedPatchOptionChoice? _selected;

    public NclOptionSelectorVm(
        NclManagedPatchOptionSet set,
        Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(set);
        PlaceholderId = set.PlaceholderId;
        Choices = set.Choices;
        _onChanged = onChanged;
    }

    public int PlaceholderId { get; }

    public IReadOnlyList<NclManagedPatchOptionChoice> Choices { get; }

    public string Caption =>
        UiText.Fmt(
            "Patch.Ncl.Options.Caption",
            new string('Z', PlaceholderId));

    public bool HasSelection => _selected is not null;

    public NclManagedPatchOptionChoice? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                OnChange(nameof(HasSelection));
                _onChanged();
            }
        }
    }
}

public sealed class NclImportVm : VmBase
{
    private readonly AppFlow _flow;
    private readonly Rpcs3InstallDescriptor _install;
    private readonly Rpcs3GameDescriptor _game;
    private readonly string _gameTitle;
    private readonly PatMatch[] _enabledMatches;
    private readonly Func<bool> _isRpcs3Running;
    private readonly Func<string, string, bool> _confirm;
    private readonly Func<string?> _pickFile;
    private string LogPath =>
        Rpcs3LogLocator.PreferredExistingPlainLogPath(_install);
    private readonly string? _version;

    private string _filePath = string.Empty;
    private NclDialectChoice? _selectedDialect;
    private NclManagedPatchCheatChoice? _selectedCheat;
    private string? _selectedRunKey;
    private string _inspectionStatus = string.Empty;
    private IReadOnlyList<TextFragment> _sourceNoticeFragments = [];
    private string _cheatHint = string.Empty;
    private string _runKeyHint = string.Empty;
    private string _previewStatus = string.Empty;
    private string _resMsg = string.Empty;
    private NclManagedPatchInspection? _inspection;
    private NclCorpusLookupResult? _bundledLookup;
    private NclManagedPatchPreview? _preview;
    private bool _isExternalSource;
    private bool _applied;
    private bool _isBusy;
    private bool _initialized;
    private string _details = string.Empty;
    private readonly AsyncCmd _inspectCmd;
    private readonly AsyncCmd _runKeysCmd;

    public NclImportVm(
        AppFlow flow,
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        string gameTitle,
        IReadOnlyList<PatMatch> enabledMatches,
        Func<bool>? isRpcs3Running = null,
        Func<string, string, bool>? confirm = null,
        Func<string?>? pickFile = null)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(enabledMatches);

        _flow = flow;
        _install = install;
        _game = game;
        _gameTitle = string.IsNullOrWhiteSpace(gameTitle)
            ? game.Title
            : gameTitle;
        _enabledMatches = enabledMatches.ToArray();
        _isRpcs3Running = isRpcs3Running ??
            Rpcs3InstallService.IsR3;
        _confirm = confirm ??
            UiDlg.Confirm;
        _pickFile = pickFile ??
            (() => UiDlg.PickNcl(FilePath));
        _version = PatVer.Norm(game.Version);

        Dialects =
        [
            new NclDialectChoice(
                UiText.GetText("Patch.Ncl.Dialect.Artemis"),
                NclDialect.ArtemisPs3),
            new NclDialectChoice(
                UiText.GetText("Patch.Ncl.Dialect.NetCheat"),
                NclDialect.NetCheat453)
        ];

        Sources =
        [
            new NclSourceChoice(
                UiText.GetText("Patch.Ncl.Corpus.Source"),
                false),
            new NclSourceChoice(
                UiText.GetText("Patch.Ncl.External.Source"),
                true)
        ];

        RunKeys = BuildPpuRunKeys(game);

        PickCmd = new RelayCmd(Pick, () => CanEdit);
        _inspectCmd = new AsyncCmd(() => RunOperationAsync(Inspect), () => CanEdit);
        _runKeysCmd = new AsyncCmd(
            () => RunOperationAsync(RecomputeRunKeys),
            () => CanEdit);
        UseBundledCmd = new AsyncCmd(
            () => RunOperationAsync(LoadBundled),
            () => CanEdit && IsExternalSource);
        PreviewCmd = new AsyncCmd(
            () => RunOperationAsync(PreviewConversion),
            () => CanEdit && CanPreview());
        ApplyCmd = new AsyncCmd(
            () => RunOperationAsync(Apply),
            () => CanEdit && HasPreview);
        CloseCmd = new RelayCmd(
            () => CloseReq?.Invoke(this, false),
            () => !IsBusy);

        RefreshPrerequisites();
    }

    public event EventHandler<bool>? CloseReq;

    public NclSourceChoice[] Sources { get; }

    public NclDialectChoice[] Dialects { get; }

    public ObservableCollection<NclManagedPatchCheatChoice> Cheats { get; } = [];

    public ObservableCollection<NclOptionSelectorVm> OptionSelectors { get; } = [];

    public bool HasOptionSelectors => OptionSelectors.Count > 0;

    public string OptionHint =>
        HasOptionSelectors
            ? UiText.GetText("Patch.Ncl.Options.Required")
            : string.Empty;

    // Every PPU executable the game's hash evidence knows about. This is the
    // candidate set that address-domain evidence is applied to; it is never
    // what the user chooses from.
    public IReadOnlyList<string> RunKeys { get; }

    // The subset whose proven file-backed ranges actually contain every write
    // of the selected cheat/option instance. Only these are ever presented.
    public ObservableCollection<string> EligibleRunKeys { get; } = [];

    public RelayCmd PickCmd { get; }

    public AsyncCmd UseBundledCmd { get; }

    public AsyncCmd PreviewCmd { get; }

    public AsyncCmd ApplyCmd { get; }

    public RelayCmd CloseCmd { get; }

    public bool IsBusy => _isBusy;

    public bool CanEdit => !IsBusy && !_applied;

    public string Details
    {
        get => _details;
        private set => Set(ref _details, value);
    }

    public Task CurrentOperation { get; private set; } = Task.CompletedTask;

    public Task InitializeAsync()
    {
        if (_initialized || IsBusy)
        {
            return CurrentOperation;
        }

        _initialized = true;
        return RunOperationAsync(LoadBundled);
    }

    private Task RunOperationAsync(Func<Task> operation)
    {
        if (!CanEdit)
        {
            return CurrentOperation;
        }

        CurrentOperation = RunBusyAsync(operation);
        return CurrentOperation;
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        _isBusy = true;
        OnChange(nameof(IsBusy));
        OnChange(nameof(CanEdit));
        RefreshCommands();
        try
        {
            await operation();
        }
        finally
        {
            _isBusy = false;
            OnChange(nameof(IsBusy));
            OnChange(nameof(CanEdit));
            RefreshCommands();
        }

        if (_applied)
        {
            CloseReq?.Invoke(this, true);
        }
    }

    private void SetCheat(NclManagedPatchCheatChoice? value)
    {
        if (Set(ref _selectedCheat, value, nameof(SelectedCheat)))
        {
            RebuildOptionSelectors();
            UpdateCheatHint();
            InvalidatePreview();
        }
    }

    // One selector per referenced placeholder id, never a default choice.
    private void RebuildOptionSelectors()
    {
        OptionSelectors.Clear();

        if (_selectedCheat is not null)
        {
            foreach (var set in _selectedCheat.RequiredOptions)
            {
                OptionSelectors.Add(
                    new NclOptionSelectorVm(
                        set,
                        OnOptionSelectionChanged));
            }
        }

        OnChange(nameof(HasOptionSelectors));
        OnChange(nameof(OptionHint));
    }

    // Changing any option immediately discards the current Preview, so a stale
    // Preview can never be applied against bytes it did not produce.
    private void OnOptionSelectionChanged()
    {
        InvalidatePreview();
        OnChange(nameof(RequiresPpuRunKey));

        // A different option value produces different bytes at the same
        // addresses, and an incomplete selection produces none at all, so PPU
        // eligibility is recomputed rather than carried over.
        _runKeysCmd.Execute(null);
    }

    // Null until every referenced placeholder has an explicit selection.
    private NclOptionSelection? BuildOptionSelection()
    {
        if (_selectedCheat is null ||
            OptionSelectors.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<int, string>();
        foreach (var selector in OptionSelectors)
        {
            if (selector.Selected is null)
            {
                return null;
            }

            values[selector.PlaceholderId] = selector.Selected.Value;
        }

        return new NclOptionSelection(_selectedCheat.Index, values);
    }

    public string GameTitle => _gameTitle;

    public string GameIdentity => $"{_game.Serial} | {_version ?? UiText.GetText("Ui.Unknown")}";

    // The current game's own application version, exposed on its own so a
    // version-mismatch item template can bind it without re-parsing GameIdentity.
    public string GameVersion => _version ?? UiText.GetText("Ui.Unknown");

    public string GameSummary =>
        UiText.Fmt(
            "Patch.Ncl.GameSummary",
            _gameTitle,
            _game.Serial,
            _version ?? UiText.GetText("Ui.Unknown"));

    public bool IsExternalSource
    {
        get => _isExternalSource;
        private set
        {
            if (Set(ref _isExternalSource, value))
            {
                OnChange(nameof(IsBundledSource));
                OnChange(nameof(SelectedSource));
                OnChange(nameof(RequiresPpuRunKey));
            }
        }
    }

    public bool IsBundledSource => !IsExternalSource;

    // Derived from IsExternalSource rather than duplicated into a second field,
    // so the picker can never disagree with the source the operations used.
    public NclSourceChoice? SelectedSource
    {
        get => Sources.FirstOrDefault(item =>
            item.IsExternal == IsExternalSource);
        set
        {
            if (!CanEdit ||
                value is null ||
                value.IsExternal == IsExternalSource)
            {
                return;
            }

            if (value.IsExternal)
            {
                UseExternalSource();
            }
            else
            {
                // Choosing the built-in database is itself the load: the user
                // never browses for a file and never sees a corpus path.
                UseBundledCmd.Execute(null);
            }
        }
    }

    // A cheat can only come from a source that already verified it: an
    // inspected external file, or a discovered bundled candidate.
    private bool IsSourceReady =>
        IsExternalSource
            ? _inspection?.IsOk == true &&
              SelectedDialect is not null &&
              File.Exists(FilePath)
            : SelectedCheat is
              {
                  CanConvert: true,
                  BundledCandidate: not null
              };

    // Every NCL cheat R3Config emits is written against one exact PPU
    // executable hash. Which executables can carry it is decided by the
    // concrete writes of the resolved instance, so the stage only becomes
    // meaningful once the cheat and every required option are known - and it is
    // meaningless before that. Discovering or importing NCL never depends on it.
    public bool RequiresPpuRunKey =>
        SelectedCheat is not null &&
        IsSourceReady &&
        OptionSelectors.All(item => item.HasSelection);

    public string FilePath
    {
        get => _filePath;
        private set => Set(ref _filePath, value);
    }

    public NclDialectChoice? SelectedDialect
    {
        get => _selectedDialect;
        set
        {
            if (!CanEdit || !Set(ref _selectedDialect, value))
            {
                return;
            }

            InvalidatePreview();

            if (!IsExternalSource)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(FilePath) &&
                value is not null)
            {
                _inspectCmd.Execute(null);
            }
            else
            {
                InspectionStatus =
                    UiText.GetText(
                        string.IsNullOrWhiteSpace(FilePath)
                            ? "Patch.Ncl.ChooseFileFirst"
                            : "Patch.Ncl.ChooseDialect");
            }
        }
    }

    public NclManagedPatchCheatChoice? SelectedCheat
    {
        get => _selectedCheat;
        set
        {
            if (CanEdit && Set(ref _selectedCheat, value))
            {
                RebuildOptionSelectors();
                UpdateCheatHint();
                InvalidatePreview();

                // A different cheat produces a different write set, so which
                // PPU executables can carry it has to be measured again.
                _runKeysCmd.Execute(null);
            }
        }
    }

    public string? SelectedRunKey
    {
        get => _selectedRunKey;
        set
        {
            if (!CanEdit)
            {
                return;
            }

            // A key that address-domain evidence did not prove for this exact
            // instance can never become the selection, however it was offered.
            if (value is not null &&
                !EligibleRunKeys.Contains(
                    value,
                    StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            if (Set(ref _selectedRunKey, value))
            {
                InvalidatePreview();
            }
        }
    }

    public string InspectionStatus
    {
        get => _inspectionStatus;
        private set => Set(ref _inspectionStatus, value);
    }

    public string SourceNotice
    {
        get => TextFragments.PlainText(SourceNoticeFragments);
        private set => SourceNoticeFragments = [new TextFragment(value)];
    }

    public IReadOnlyList<TextFragment> SourceNoticeFragments
    {
        get => _sourceNoticeFragments;
        private set
        {
            if (Set(ref _sourceNoticeFragments, value))
            {
                OnChange(nameof(SourceNotice));
            }
        }
    }

    public string CheatHint
    {
        get => _cheatHint;
        private set => Set(ref _cheatHint, value);
    }

    public string RunKeyHint
    {
        get => _runKeyHint;
        private set => Set(ref _runKeyHint, value);
    }

    public string PreviewStatus
    {
        get => _previewStatus;
        private set => Set(ref _previewStatus, value);
    }

    public string ResMsg
    {
        get => _resMsg;
        private set => Set(ref _resMsg, value);
    }

    public bool HasPreview =>
        _preview?.IsOk == true &&
        _preview.Code == NclManagedPatchPreviewCode.Success;

    // Switching to the external source clears everything the built-in corpus
    // produced and asks for nothing on its own: no file dialog opens until the
    // user explicitly chooses "Choose NCL file".
    private void UseExternalSource()
    {
        IsExternalSource = true;
        FilePath = string.Empty;
        _bundledLookup = null;
        _inspection = null;
        Cheats.Clear();
        SetCheat(null);
        ClearRunKeyEligibility();
        _selectedDialect = null;
        OnChange(nameof(SelectedDialect));
        SourceNotice =
            UiText.GetText("Patch.Ncl.External.SourceNotice");
        CheatHint = string.Empty;
        InspectionStatus =
            UiText.GetText("Patch.Ncl.ChooseFileFirst");
        InvalidatePreview();
        RefreshCommands();
    }

    private void Pick()
    {
        var path = _pickFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        IsExternalSource = true;
        FilePath = path;
        _bundledLookup = null;
        _inspection = null;
        Cheats.Clear();
        SetCheat(null);
        ClearRunKeyEligibility();
        SelectedDialect = null;
        SourceNotice =
            UiText.GetText("Patch.Ncl.External.SourceNotice");
        CheatHint = string.Empty;
        InspectionStatus =
            UiText.GetText("Patch.Ncl.ChooseDialect");
        InvalidatePreview();
        RefreshCommands();
    }

    // A bundled candidate already carries the placeholder ids its code
    // references and the option sets that declare them, both recorded by the
    // parser at discovery. Mapping them here lets the existing OptionSelectors
    // machinery drive a bundled cheat exactly as it drives an external one.
    // CA1859: the concrete List is the real result type of this private helper.
    private static List<NclManagedPatchOptionSet> DescribeBundledOptions(
        NclCorpusCheatCandidate candidate)
    {
        var ids = candidate.RequiredOptionIds;
        var sets = candidate.RequiredOptionSets;
        if (ids.Count == 0 ||
            ids.Count != sets.Count)
        {
            return [];
        }

        var described = new List<NclManagedPatchOptionSet>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            if (sets[index].Values.Count == 0)
            {
                return [];
            }

            described.Add(
                new NclManagedPatchOptionSet(
                    ids[index],
                    [.. sets[index].Values.Select(item =>
                        new NclManagedPatchOptionChoice(
                            item.Value,
                            item.Label))]));
        }

        return described;
    }

    private async Task LoadBundled()
    {
        IsExternalSource = false;
        FilePath = string.Empty;
        _inspection = null;
        _bundledLookup = null;
        Cheats.Clear();
        SetCheat(null);
        ClearRunKeyEligibility();
        _selectedDialect = Dialects
            .Single(item =>
                item.Value == NclDialect.ArtemisPs3);
        OnChange(nameof(SelectedDialect));
        InvalidatePreview();

        if (string.IsNullOrWhiteSpace(_version))
        {
            InspectionStatus =
                UiText.GetText("Patch.Ncl.NoVersion");
            SourceNotice = string.Empty;
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        try
        {
            _bundledLookup = await Task.Run(() => _flow.DiscoverBundledNcl(
                _game.Serial,
                _version,
                _game.Title));
            Details = _bundledLookup.Code.ToString();
        }
        catch (Exception)
        {
            Details = string.Empty;
            InspectionStatus =
                UiText.GetText("Patch.Ncl.Corpus.Unavailable");
            SourceNotice = string.Empty;
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        AddCheats(
            [.. _bundledLookup.Cheats.Select(candidate =>
                new NclManagedPatchCheatChoice(
                    candidate.PrimarySource.CheatIndex,
                    candidate.Name,
                    candidate.Author,
                    candidate.Notes,
                    candidate,
                    DescribeBundledOptions(candidate))),
             .. _bundledLookup.OtherVersionCheats.Select(candidate =>
                new NclManagedPatchCheatChoice(
                    candidate.PrimarySource.CheatIndex,
                    candidate.Name,
                    candidate.Author,
                    candidate.Notes,
                    candidate,
                    DescribeBundledOptions(candidate)))]);

        var exactCount = _bundledLookup.Cheats.Count;
        var otherVersionCount = _bundledLookup.OtherVersionCheats.Count;
        InspectionStatus = _bundledLookup.Code switch
        {
            NclCorpusLookupCode.Success =>
                UiText.Fmt(
                    "Patch.Ncl.Corpus.Ready",
                    exactCount),
            NclCorpusLookupCode.NoExactTarget when otherVersionCount > 0 =>
                UiText.GetText("Patch.Ncl.Corpus.VersionMismatchShort"),
            NclCorpusLookupCode.NoExactTarget =>
                UiText.GetText("Patch.Ncl.Corpus.None"),
            NclCorpusLookupCode.NoStaticCandidates =>
                UiText.GetText("Patch.Ncl.Corpus.NoStatic"),
            _ =>
                UiText.GetText("Patch.Ncl.Corpus.Unavailable")
        };

        var notices = new List<TextFragment>();
        if (otherVersionCount > 0)
        {
            var sourceVersions = _bundledLookup.OtherVersionCheats
                .Select(c => c.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            notices.Add(new TextFragment(
                UiText.Fmt(
                    "Patch.Ncl.Corpus.CurrentVersionLine",
                    _version ?? UiText.GetText("Ui.Unknown"))));
            notices.Add(new TextFragment(Environment.NewLine));
            notices.Add(new TextFragment(
                UiText.Fmt(
                    "Patch.Ncl.Corpus.SourceVersionLine",
                    string.Join(UiText.GetText("Ui.ListSeparator"), sourceVersions))));
            notices.Add(new TextFragment(Environment.NewLine));
            notices.Add(new TextFragment(
                UiText.Fmt(
                    "Patch.Ncl.Corpus.CompatibilityStatusLine",
                    UiText.GetText("Patch.Ncl.Cheat.VersionMismatchStatus"))));
        }

        if (_bundledLookup.OtherVersions.Count > 0)
        {
            notices.AddRange(TextFragments.TechnicalList(
                UiText.GetText("Patch.Ncl.Corpus.OtherVersions"),
                _bundledLookup.OtherVersions));
        }

        if (_bundledLookup.Conflicts.Count > 0)
        {
            if (notices.Count > 0)
            {
                notices.Add(new TextFragment(Environment.NewLine));
            }

            notices.Add(new TextFragment(
                UiText.Fmt(
                    "Patch.Ncl.Corpus.Conflicts",
                    _bundledLookup.Conflicts.Count)));
        }

        if (notices.Count == 0)
        {
            notices.Add(new TextFragment(
                UiText.GetText("Patch.Ncl.Corpus.SourceNotice")));
        }

        SourceNoticeFragments = notices.ToArray();

        ApplyCheatSelection();
        await RecomputeRunKeys();
    }

    private async Task Inspect()
    {
        Cheats.Clear();
        SetCheat(null);
        ClearRunKeyEligibility();
        _inspection = null;

        if (SelectedDialect is null)
        {
            InspectionStatus =
                UiText.GetText("Patch.Ncl.ChooseDialect");
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        if (string.IsNullOrWhiteSpace(_version))
        {
            InspectionStatus =
                UiText.GetText("Patch.Ncl.NoVersion");
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        try
        {
            var path = FilePath;
            var dialect = SelectedDialect.Value;
            _inspection = await Task.Run(() => _flow.InspectNcl(
                path,
                dialect,
                _game.Serial,
                _version));
            Details = _inspection.Code.ToString();
        }
        catch (Exception)
        {
            Details = string.Empty;
            InspectionStatus =
                UiText.GetText("Patch.Ncl.ParseFailed");
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        if (!_inspection.IsOk)
        {
            InspectionStatus =
                InspectionError(_inspection.Code);
            SourceNotice = string.Empty;
            CheatHint = string.Empty;
            RefreshCommands();
            return;
        }

        AddCheats(_inspection.StaticCheats);

        InspectionStatus = UiText.Fmt(
            "Patch.Ncl.InspectionReady",
            Cheats.Count,
            _inspection.TotalCheats);

        SourceNotice = _inspection.SourceHasExactTarget
            ? UiText.GetText("Patch.Ncl.IdentityExact")
            : UiText.GetText("Patch.Ncl.IdentityExplicitTarget");

        ApplyCheatSelection();
        await RecomputeRunKeys();
    }

    // A visible name is shown as it is written. Only a name that genuinely
    // occurs more than once in the same source is numbered, by its order of
    // appearance, so the user never sees the internal cheat index.
    private void AddCheats(
        IReadOnlyList<NclManagedPatchCheatChoice> choices)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            totals[choice.Name] =
                totals.GetValueOrDefault(choice.Name) + 1;
        }

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            var ordinal = 0;
            if (totals[choice.Name] > 1)
            {
                ordinal = seen.GetValueOrDefault(choice.Name) + 1;
                seen[choice.Name] = ordinal;
            }

            Cheats.Add(choice with { DuplicateOrdinal = ordinal });
        }
    }

    // Auto-selection is only allowed when the source left exactly one valid
    // cheat. With several, the selection stays empty and the user chooses.
    //
    // SelectedCheat is republished unconditionally: Set(...) is silent when the
    // stored value did not change, while the WPF selector keeps showing
    // whatever it last displayed over a rebuilt list. Without this the dialog
    // could open with a cheat highlighted that this view model never accepted -
    // which is exactly how "AoB Combo Multiplier" appeared pre-selected.
    private void ApplyCheatSelection()
    {
        _selectedCheat = Cheats.Count == 1
            ? Cheats[0]
            : null;

        OnChange(nameof(SelectedCheat));
        RebuildOptionSelectors();
        InvalidatePreview();

        UpdateCheatHint();
    }

    private void UpdateCheatHint()
    {
        if (SelectedCheat is
            {
                CanConvert: false,
                BundledCandidate: not null
            } unavailable)
        {
            CheatHint = UiText.GetText(
                unavailable.Status switch
                {
                    NclVerifyStat.RuntimeEvidenceRequired =>
                        "Patch.Ncl.CheatUnavailable.RuntimeEvidenceRequired",
                    NclVerifyStat.ConstantRuntimeRequired =>
                        "Patch.Ncl.CheatUnavailable.ConstantRuntimeRequired",
                    NclVerifyStat.NonExecutableMetadata =>
                        "Patch.Ncl.CheatUnavailable.NonExecutableMetadata",
                    NclVerifyStat.Unsupported =>
                        "Patch.Ncl.CheatUnavailable.Unsupported",
                    NclVerifyStat.Invalid =>
                        "Patch.Ncl.CheatUnavailable.Invalid",
                    _ =>
                        "Patch.Ncl.CheatUnavailable.ConversionRejected"
                });
            return;
        }

        CheatHint = Cheats.Count switch
        {
            0 => string.Empty,
            1 => UiText.GetText("Patch.Ncl.CheatSingle"),
            _ => UiText.Fmt(
                "Patch.Ncl.CheatMultiple",
                Cheats.Count)
        };
    }

    // PPU eligibility is recomputed from scratch whenever the source, the cheat
    // or an option changes, because each of those changes the concrete write
    // set and therefore which logged executables can carry it. Any previous
    // selection is dropped first, so an ineligible key can never survive.
    // No PPU key survives a change to what it was proven against.
    private void ClearRunKeyEligibility()
    {
        EligibleRunKeys.Clear();
        _selectedRunKey = null;
        OnChange(nameof(SelectedRunKey));
        RunKeyHint = string.Empty;
    }

    private async Task RecomputeRunKeys()
    {
        ClearRunKeyEligibility();
        InvalidatePreview();

        var cheat = SelectedCheat;
        if (!RequiresPpuRunKey ||
            cheat is null ||
            _version is null)
        {
            RunKeyHint = string.Empty;
            RefreshCommands();
            return;
        }

        if (RunKeys.Count == 0)
        {
            RunKeyHint = UiText.GetText("Patch.Ncl.NoPpu");
            RefreshCommands();
            return;
        }

        if (!File.Exists(LogPath))
        {
            RunKeyHint = UiText.GetText("Patch.Ncl.LogMissing");
            RefreshCommands();
            return;
        }

        // Target.RunKey is deliberately empty: the candidates are what is being
        // measured, so no prior selection can pre-decide the answer.
        var target = new NclPpuConversionTarget(
            string.Empty,
            cheat.Name,
            _gameTitle,
            _game.Serial,
            _version,
            cheat.Author,
            cheat.Notes);

        var optionSelection = BuildOptionSelection();
        var candidates = RunKeys;
        var bundled = !IsExternalSource
            ? cheat.BundledCandidate
            : null;
        var dialect = SelectedDialect?.Value;
        var path = FilePath;
        var sourceSha256 = _inspection?.SourceSha256;
        var cheatIndex = cheat.Index;

        NclRunKeyEligibilityResult? eligibility;
        try
        {
            eligibility = await Task.Run(() =>
            {
                if (bundled is not null)
                {
                    return _flow.DescribeBundledNclRunKeys(
                        new NclBundledManagedPatchRequest(
                            bundled,
                            LogPath,
                            target,
                            optionSelection),
                        candidates);
                }

                if (dialect is null ||
                    string.IsNullOrWhiteSpace(sourceSha256))
                {
                    return null;
                }

                return _flow.DescribeNclRunKeys(
                    new NclPpuConversionFileRequest(
                        path,
                        LogPath,
                        dialect.Value,
                        cheatIndex,
                        target,
                        sourceSha256,
                        optionSelection),
                    candidates);
            });
        }
        catch (Exception)
        {
            eligibility = null;
        }

        if (eligibility is null ||
            !eligibility.IsOk)
        {
            RunKeyHint =
                UiText.GetText("Patch.Ncl.RunKeyUndetermined");
            RefreshCommands();
            return;
        }

        foreach (var runKey in eligibility.EligibleRunKeys)
        {
            EligibleRunKeys.Add(runKey);
        }

        if (EligibleRunKeys.Count == 0)
        {
            RunKeyHint =
                UiText.GetText("Patch.Ncl.RunKeyNoneEligible");

            // The primary line stays user-facing; the exact stage that excluded
            // each executable goes to the existing Details surface so the real
            // reason is never hidden.
            Details = RunKeyDetails(eligibility);
        }
        else if (EligibleRunKeys.Count == 1)
        {
            // Evidence, not a guess: exactly one logged executable's proven
            // file-backed ranges contain every write of this instance.
            _selectedRunKey = EligibleRunKeys[0];
            OnChange(nameof(SelectedRunKey));
            RunKeyHint = UiText.Fmt(
                "Patch.Ncl.RunKeySingle",
                candidates.Count);
        }
        else
        {
            RunKeyHint = UiText.Fmt(
                "Patch.Ncl.RunKeyMultiple",
                EligibleRunKeys.Count);
        }

        RefreshCommands();
    }

    private async Task PreviewConversion()
    {
        if (SelectedCheat is null ||
            _version is null)
        {
            return;
        }

        if (!OptionSelectors.All(item => item.HasSelection))
        {
            PreviewStatus = UiText.GetText(
                "Patch.Ncl.Reason.OptionSelectionRequired");
            return;
        }

        if (SelectedRunKey is null)
        {
            PreviewStatus = UiText.GetText(
                EligibleRunKeys.Count == 0
                    ? "Patch.Ncl.RunKeyNoneEligible"
                    : "Patch.Ncl.Reason.PpuSelectionRequired");
            return;
        }

        if (!CanPreview())
        {
            return;
        }

        InvalidatePreview();

        var target = new NclPpuConversionTarget(
            SelectedRunKey,
            SelectedCheat.Name,
            _gameTitle,
            _game.Serial,
            _version,
            SelectedCheat.Author,
            SelectedCheat.Notes);

        var selectedCheat = SelectedCheat;
        var dialect = SelectedDialect?.Value;
        var path = FilePath;
        var sourceSha256 = _inspection?.SourceSha256;
        var bundled = !IsExternalSource ? selectedCheat.BundledCandidate : null;
        var optionSelection = BuildOptionSelection();

        // A cheat that declares referenced placeholders may only be converted
        // with a complete explicit selection.
        if (selectedCheat.RequiresOptionSelection &&
            optionSelection is null)
        {
            PreviewStatus = UiText.GetText(
                "Patch.Ncl.Reason.OptionSelectionRequired");
            RefreshCommands();
            return;
        }

        NclManagedPatchPreview? preview;
        try
        {
            preview = await Task.Run(() =>
            {
                if (bundled is not null)
                {
                    return _flow.PrepareBundledNclManagedPatch(
                        new NclBundledManagedPatchRequest(
                            bundled,
                            LogPath,
                            target,
                            optionSelection),
                        _install,
                        _enabledMatches);
                }

                if (dialect is null || string.IsNullOrWhiteSpace(sourceSha256))
                {
                    return null;
                }

                return _flow.PrepareNclManagedPatch(
                    new NclPpuConversionFileRequest(
                        path,
                        LogPath,
                        dialect.Value,
                        selectedCheat.Index,
                        target,
                        sourceSha256,
                        optionSelection),
                    _install,
                    _enabledMatches);
            });
        }
        catch (Exception)
        {
            Details = string.Empty;
            PreviewStatus =
                UiText.GetText("Patch.Ncl.PreviewFailed");
            RefreshCommands();
            return;
        }

        _preview = preview;
        Details = PreviewDetails(preview);

        if (preview is null || !preview.IsOk ||
            preview.Code != NclManagedPatchPreviewCode.Success ||
            preview.Conversion is null ||
            preview.ManagedPreparation is null)
        {
            PreviewStatus = PreviewReason(preview);
            RefreshCommands();
            return;
        }

        PreviewStatus = UiText.Fmt(
            "Patch.Ncl.PreviewReady",
            preview.Conversion.VerifiedByteCount,
            preview.Conversion.InstructionCount);

        OnChange(nameof(HasPreview));
        RefreshCommands();
    }

    private async Task Apply()
    {
        if (!HasPreview ||
            _preview is null ||
            SelectedCheat is null ||
            string.IsNullOrWhiteSpace(_version))
        {
            return;
        }

        try
        {
            if (_isRpcs3Running())
            {
                ResMsg =
                    UiText.GetText("Patch.Ncl.Rpcs3Running");
                return;
            }

            var verifiedBytes =
                _preview.Conversion?.VerifiedByteCount ?? 0;

            if (!_confirm(
                    UiText.Fmt(
                        "Patch.Ncl.ApplyConfirm",
                        SelectedCheat.Name,
                        _game.Serial,
                        _version,
                        verifiedBytes),
                    UiText.GetText("Patch.Ncl.Title")))
            {
                return;
            }

            var prepared = _preview;
            var result = await Task.Run(() => _flow.ApplyNclManagedPatch(
                prepared,
                _install));

            _applied = true;
            ResMsg = UiText.Fmt(
                "Patch.Ncl.ApplySuccess",
                result.ChgList.Count);

            RefreshCommands();
        }
        catch (Exception)
        {
            Details = string.Empty;
            ResMsg =
                UiText.GetText("Patch.Ncl.ApplyFailed");
        }
    }

    // Secondary diagnostic surface for PPU eligibility: one canonical line per
    // candidate executable, naming the evidence and binding stage that decided
    // it. This is what distinguishes "not logged" from "does not cover".
    private static string RunKeyDetails(
        NclRunKeyEligibilityResult eligibility)
    {
        return string.Join(
            Environment.NewLine,
            eligibility.Candidates.Select(item =>
                item.BindingCode is { } bindingCode
                    ? $"{item.RunKey}: {item.EvidenceCode}/{bindingCode}"
                    : $"{item.RunKey}: {item.EvidenceCode}"));
    }

    // Canonical internal state stays in Details, which is the existing
    // secondary diagnostic surface. What a normal user reads is a localized
    // reason derived from the stage that actually failed, so distinct causes
    // never collapse into one message and the real reason is never hidden.
    private static string PreviewReason(
        NclManagedPatchPreview? preview)
    {
        if (preview is null)
        {
            return UiText.GetText("Patch.Ncl.Reason.Unknown");
        }

        var conversion = preview.Conversion;
        if (conversion is not null &&
            conversion.Code != NclPpuConversionCode.Success)
        {
            return conversion.Code switch
            {
                NclPpuConversionCode.EvidenceRejected =>
                    UiText.GetText("Patch.Ncl.Reason.PpuEvidenceMissing"),

                NclPpuConversionCode.BindingRejected =>
                    conversion.BindingCode switch
                    {
                        NclAddressBindCode.UnmappedWrite or
                        NclAddressBindCode.CrossesDomainBoundary =>
                            UiText.GetText("Patch.Ncl.Reason.PpuAddressMismatch"),

                        NclAddressBindCode.NoDomains =>
                            UiText.GetText("Patch.Ncl.Reason.PpuEvidenceMissing"),

                        _ =>
                            UiText.GetText("Patch.Ncl.Reason.BindingRejected")
                    },

                NclPpuConversionCode.LoweringRejected =>
                    UiText.GetText("Patch.Ncl.Reason.NotStatic"),

                NclPpuConversionCode.ParseRejected =>
                    UiText.GetText("Patch.Ncl.Reason.SourceRejected"),

                NclPpuConversionCode.VerificationMismatch =>
                    UiText.GetText("Patch.Ncl.Reason.VerificationRejected"),

                NclPpuConversionCode.EmissionRejected =>
                    UiText.GetText("Patch.Ncl.Reason.EmissionRejected"),

                NclPpuConversionCode.InvalidRequest or
                NclPpuConversionCode.CheatSelectionOutOfRange =>
                    UiText.GetText("Patch.Ncl.Reason.SelectionInvalid"),

                _ =>
                    UiText.GetText("Patch.Ncl.Reason.Unknown")
            };
        }

        return preview.Code switch
        {
            NclManagedPatchPreviewCode.ConversionRejected =>
                UiText.GetText("Patch.Ncl.Reason.SourceRejected"),

            NclManagedPatchPreviewCode.GeneratedPatchRejected =>
                UiText.GetText("Patch.Ncl.Reason.PatchRejected"),

            NclManagedPatchPreviewCode.PlanRejected =>
                UiText.GetText("Patch.Ncl.Reason.PlanRejected"),

            NclManagedPatchPreviewCode.ManagedPreparationRejected =>
                UiText.GetText("Patch.Ncl.Reason.PreparationRejected"),

            _ =>
                UiText.GetText("Patch.Ncl.Reason.Unknown")
        };
    }

    private static string PreviewDetails(NclManagedPatchPreview? preview)
    {
        if (preview is null)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            $"Preview: {preview.Code}"
        };
        var conversion = preview.Conversion;
        if (conversion is not null)
        {
            lines.Add($"Conversion: {conversion.Code}");

            if (conversion.EvidenceCode is { } evidenceCode)
            {
                lines.Add($"Evidence: {evidenceCode}");
            }

            if (conversion.BindingCode is { } bindingCode)
            {
                lines.Add($"Binding: {bindingCode}");
            }

            if (conversion.EmissionCode is { } emissionCode)
            {
                lines.Add($"Emission: {emissionCode}");
            }

            if (!string.IsNullOrWhiteSpace(conversion.RunKey))
            {
                lines.Add($"Run key: {conversion.RunKey}");
            }

            lines.Add($"Verified bytes: {conversion.VerifiedByteCount}");
            lines.Add($"Instructions: {conversion.InstructionCount}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private bool CanPreview()
    {
        var runKey = SelectedRunKey;

        return !_applied &&
               IsSourceReady &&
               SelectedCheat is not null &&
               OptionSelectors.All(item => item.HasSelection) &&
               runKey is not null &&
               EligibleRunKeys.Contains(
                   runKey,
                   StringComparer.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(_version) &&
               File.Exists(LogPath);
    }

    private void InvalidatePreview()
    {
        _preview = null;
        PreviewStatus = string.Empty;
        Details = string.Empty;
        OnChange(nameof(HasPreview));
        RefreshCommands();
    }

    private void RefreshPrerequisites()
    {
        if (string.IsNullOrWhiteSpace(_version))
        {
            InspectionStatus =
                UiText.GetText("Patch.Ncl.NoVersion");
        }
        else
        {
            InspectionStatus = string.Empty;
        }

        // No PPU key is chosen before there is a resolved cheat to measure it
        // against. Which executables are eligible is address-domain evidence,
        // and RecomputeRunKeys is the only thing that decides it.
        ClearRunKeyEligibility();

        if (RunKeys.Count == 0)
        {
            RunKeyHint = UiText.GetText("Patch.Ncl.NoPpu");
        }
        else if (!File.Exists(LogPath))
        {
            RunKeyHint = UiText.GetText("Patch.Ncl.LogMissing");
        }

        RefreshCommands();
    }

    private void RefreshCommands()
    {
        // The PPU stage is derived state, so it is republished from the same
        // place the commands re-evaluate rather than from a separate flag.
        OnChange(nameof(RequiresPpuRunKey));
        PickCmd.RaiseCan();
        CloseCmd.RaiseCan();
        _inspectCmd.RaiseCan();
        _runKeysCmd.RaiseCan();
        UseBundledCmd.RaiseCan();
        PreviewCmd.RaiseCan();
        ApplyCmd.RaiseCan();
    }

    private static string InspectionError(
        NclManagedPatchInspectionCode code)
    {
        return code switch
        {
            NclManagedPatchInspectionCode.SourceSerialMismatch or
            NclManagedPatchInspectionCode.SourceVersionMismatch =>
                UiText.GetText("Patch.Ncl.IdentityMismatch"),

            NclManagedPatchInspectionCode.NoStaticCandidates =>
                UiText.GetText("Patch.Ncl.NoStatic"),

            _ =>
                UiText.GetText("Patch.Ncl.ParseFailed")
        };
    }

    private static string[] BuildPpuRunKeys(
        Rpcs3GameDescriptor game)
    {
        return
        [
            .. game.PpuList
                .Select(hash =>
                {
                    if (!RunKey.TryNorm2(
                            HashKind.Ppu,
                            hash,
                            out var normalized))
                    {
                        return null;
                    }

                    return string.Concat(
                        "PPU-",
                        normalized);
                })
                .Where(runKey =>
                    !string.IsNullOrWhiteSpace(runKey))
                .Select(runKey => runKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    runKey => runKey,
                    StringComparer.OrdinalIgnoreCase)
        ];
    }
}
