using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using AppSvc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class HCanaryHashVm(HCanaryHash hash) : VmBase
{
    public HCanaryHash Hash { get; } = hash;
    public string Key => Hash.Key;
    public string Kind => Hash.Kind == PatchHashKind.Ppu ? "PPU" : "SPU";
    public string Source => $"{Path.GetFileName(Hash.SourcePath)}:{Hash.FirstLine}";
}

public sealed class HCanaryGameTargetVm : VmBase
{
    private string _gameName = string.Empty;
    private string _gameId = string.Empty;
    private string _gameVersion = string.Empty;
    private string _error = string.Empty;

    public string GameName { get => _gameName; set => SetProperty(ref _gameName, value); }
    public string GameId { get => _gameId; set => SetProperty(ref _gameId, value); }
    public string GameVersion { get => _gameVersion; set => SetProperty(ref _gameVersion, value); }
    public string Error { get => _error; set => SetProperty(ref _error, value); }

    public HCanaryGameTarget ToModel() => new()
    {
        GameName = GameName, GameId = GameId, GameVersion = GameVersion
    };

    public static HCanaryGameTargetVm From(HCanaryGameTarget value) => new()
    {
        GameName = value.GameName, GameId = value.GameId, GameVersion = value.GameVersion
    };
}

public sealed class HCanaryInstructionVm : VmBase
{
    private string _operation = "be32";
    private string _address = "0x00000000";
    private string _value = "0x60000000";
    private string _error = string.Empty;

    public string Operation { get => _operation; set => SetProperty(ref _operation, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string Value { get => _value; set => SetProperty(ref _value, value); }
    public string Error { get => _error; set => SetProperty(ref _error, value); }

    public HCanaryInstruction ToModel() => new()
    {
        Operation = Operation, Address = Address, Value = Value
    };

    public static HCanaryInstructionVm From(HCanaryInstruction value) => new()
    {
        Operation = value.Operation, Address = value.Address, Value = value.Value
    };

    public HCanaryInstructionVm Clone() => From(ToModel());
}

public sealed class HCanaryAnchorVm : VmBase
{
    private string _name = string.Empty;
    private string _error = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Error { get => _error; set => SetProperty(ref _error, value); }
    public ObservableCollection<HCanaryInstructionVm> Instructions { get; } = [];

    public HCanaryAnchor ToModel() => new()
    {
        Name = Name, Instructions = Instructions.Select(item => item.ToModel()).ToList()
    };

    public static HCanaryAnchorVm From(HCanaryAnchor value)
    {
        var vm = new HCanaryAnchorVm { Name = value.Name };
        foreach (var item in value.Instructions) vm.Instructions.Add(HCanaryInstructionVm.From(item));
        return vm;
    }
}

public sealed class HCanaryVm : VmBase
{
    private readonly HCanaryPatchAuthoringService _authoring = new();
    private readonly HCanaryHashExtractionService _hashReader = new();
    private readonly AppFlow _flow = new();
    private Rpcs3InstallDescriptor? _install;
    private Rpcs3GameDescriptor? _contextGame;
    private HCanaryHashVm? _selectedHash;
    private HCanaryGameTargetVm? _selectedGameTarget;
    private HCanaryInstructionVm? _selectedInstruction;
    private HCanaryAnchorVm? _selectedAnchor;
    private HCanaryInstructionVm? _selectedAnchorInstruction;
    private HCanaryStoredPatch? _selectedStoredPatch;
    private string? _currentIdentity;
    private bool _dirty;
    private bool _suppressRefresh;
    private string _patchType = "PPU";
    private string _executableHash = string.Empty;
    private string _patchName = string.Empty;
    private string _author = string.Empty;
    private string _notes = string.Empty;
    private string _group = string.Empty;
    private string _patchVersion = "1.0";
    private HCanaryDraftStatus _status = HCanaryDraftStatus.Draft;
    private string _yamlPreview = string.Empty;
    private string _validationSummary = string.Empty;
    private string _statusMessage = string.Empty;
    private string _logStatus = string.Empty;
    private string _hashError = string.Empty;
    private string _patchNameError = string.Empty;
    private string _patchVersionError = string.Empty;
    private bool _isValid;

    public HCanaryVm()
    {
        NewCommand = new RelayCommand(NewDraft);
        ReadLogCommand = new RelayCommand(ReadLog, () => _install is not null);
        UseSelectedGameCommand = new RelayCommand(AddCurrentGame, () => _contextGame is not null);
        AddGameCommand = new RelayCommand(() => AddGame(new HCanaryGameTargetVm()));
        DeleteGameCommand = new RelayCommand(DeleteGame, () => SelectedGameTarget is not null && Games.Count > 1);
        AddInstructionCommand = new RelayCommand(() => AddInstruction(new HCanaryInstructionVm()));
        DeleteInstructionCommand = new RelayCommand(DeleteInstruction, () => SelectedInstruction is not null);
        DuplicateInstructionCommand = new RelayCommand(DuplicateInstruction, () => SelectedInstruction is not null);
        MoveInstructionUpCommand = new RelayCommand(() => MoveInstruction(-1), () => CanMoveInstruction(-1));
        MoveInstructionDownCommand = new RelayCommand(() => MoveInstruction(1), () => CanMoveInstruction(1));
        AddAnchorCommand = new RelayCommand(AddAnchor);
        DeleteAnchorCommand = new RelayCommand(DeleteAnchor, () => SelectedAnchor is not null);
        AddAnchorInstructionCommand = new RelayCommand(AddAnchorInstruction, () => SelectedAnchor is not null);
        DeleteAnchorInstructionCommand = new RelayCommand(DeleteAnchorInstruction, () => SelectedAnchorInstruction is not null && SelectedAnchor is not null);
        AddAnchorLoadCommand = new RelayCommand(AddAnchorLoad, () => SelectedAnchor is not null);
        OpenStoredCommand = new RelayCommand(OpenStored, () => SelectedStoredPatch is not null);
        SaveCommand = new RelayCommand(() => Save(false), () => IsValid && _install is not null);
        SaveAsCommand = new RelayCommand(() => Save(true), () => IsValid && _install is not null);
        DeleteStoredCommand = new RelayCommand(DeleteStored, () => _install is not null && _currentIdentity is not null);
        OpenPatchesFolderCommand = new RelayCommand(OpenPatchesFolder, () => _install is not null);
        OpenImportedFileCommand = new RelayCommand(OpenImportedFile, () => _install is not null && File.Exists(ImportedPatchPath));
        OpenRpcs3Command = new RelayCommand(OpenRpcs3, () => _install is not null && File.Exists(_install.ExePath));
        CopyYamlCommand = new RelayCommand(() => UiDlg.CopyText(YamlPreview), () => !string.IsNullOrWhiteSpace(YamlPreview));
        Games.CollectionChanged += (_, _) => DraftCollectionChanged();
        Instructions.CollectionChanged += (_, _) => DraftCollectionChanged();
        Anchors.CollectionChanged += (_, _) => DraftCollectionChanged();
        NewDraft();
    }

    public event EventHandler? CatalogChanged;

    public IReadOnlyList<string> PatchTypes { get; } = ["PPU", "SPU", "Canary"];
    public IReadOnlyList<string> Operations => HCanaryPatchAuthoringService.Operations;
    public IReadOnlyList<HCanaryDraftStatus> Statuses { get; } = Enum.GetValues<HCanaryDraftStatus>();
    public ObservableCollection<HCanaryHashVm> Hashes { get; } = [];
    public ObservableCollection<HCanaryGameTargetVm> Games { get; } = [];
    public ObservableCollection<HCanaryInstructionVm> Instructions { get; } = [];
    public ObservableCollection<HCanaryAnchorVm> Anchors { get; } = [];
    public ObservableCollection<HCanaryStoredPatch> StoredPatches { get; } = [];

    public RelayCommand NewCommand { get; }
    public RelayCommand ReadLogCommand { get; }
    public RelayCommand UseSelectedGameCommand { get; }
    public RelayCommand AddGameCommand { get; }
    public RelayCommand DeleteGameCommand { get; }
    public RelayCommand AddInstructionCommand { get; }
    public RelayCommand DeleteInstructionCommand { get; }
    public RelayCommand DuplicateInstructionCommand { get; }
    public RelayCommand MoveInstructionUpCommand { get; }
    public RelayCommand MoveInstructionDownCommand { get; }
    public RelayCommand AddAnchorCommand { get; }
    public RelayCommand DeleteAnchorCommand { get; }
    public RelayCommand AddAnchorInstructionCommand { get; }
    public RelayCommand DeleteAnchorInstructionCommand { get; }
    public RelayCommand AddAnchorLoadCommand { get; }
    public RelayCommand OpenStoredCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand DeleteStoredCommand { get; }
    public RelayCommand OpenPatchesFolderCommand { get; }
    public RelayCommand OpenImportedFileCommand { get; }
    public RelayCommand OpenRpcs3Command { get; }
    public RelayCommand CopyYamlCommand { get; }

    public string PatchType { get => _patchType; set { if (SetProperty(ref _patchType, value)) RefreshDraft(); } }
    public string ExecutableHash { get => _executableHash; set { if (SetProperty(ref _executableHash, value)) RefreshDraft(); } }
    public string PatchName { get => _patchName; set { if (SetProperty(ref _patchName, value)) RefreshDraft(); } }
    public string Author { get => _author; set { if (SetProperty(ref _author, value)) RefreshDraft(); } }
    public string Notes { get => _notes; set { if (SetProperty(ref _notes, value)) RefreshDraft(); } }
    public string Group { get => _group; set { if (SetProperty(ref _group, value)) RefreshDraft(); } }
    public string PatchVersion { get => _patchVersion; set { if (SetProperty(ref _patchVersion, value)) RefreshDraft(); } }
    public HCanaryDraftStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public string YamlPreview { get => _yamlPreview; private set => SetProperty(ref _yamlPreview, value); }
    public string ValidationSummary { get => _validationSummary; private set => SetProperty(ref _validationSummary, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string LogStatus { get => _logStatus; private set => SetProperty(ref _logStatus, value); }
    public string HashError { get => _hashError; private set => SetProperty(ref _hashError, value); }
    public string PatchNameError { get => _patchNameError; private set => SetProperty(ref _patchNameError, value); }
    public string PatchVersionError { get => _patchVersionError; private set => SetProperty(ref _patchVersionError, value); }
    public bool IsValid { get => _isValid; private set { if (SetProperty(ref _isValid, value)) RaiseCommandStates(); } }
    public bool IsDirty => _dirty;
    public bool HasHashes => Hashes.Count > 0;
    public string Rpcs3Path => _install?.RootPath ?? UiText.T("HCanary.Error.Rpcs3Missing");
    public string LogPath => _install is null ? string.Empty : Path.Combine(_install.RootPath, "RPCS3.log");
    public string ImportedPatchPath => _install is null ? string.Empty : Path.Combine(_install.PatchesPath, "imported_patch.yml");
    public string CurrentGameName => _contextGame?.Title ?? UiText.T("HCanary.Error.GameMissing");
    public string CurrentGameId => _contextGame?.Serial ?? "—";
    public string CurrentGameVersion => _contextGame?.Version ?? "—";
    public string DraftStateText => UiText.T($"HCanary.DraftStatus.{Status}");

    public HCanaryHashVm? SelectedHash
    {
        get => _selectedHash;
        set
        {
            if (!SetProperty(ref _selectedHash, value) || value is null) return;
            ExecutableHash = value.Key;
            if (PatchType != "Canary") PatchType = value.Kind;
        }
    }

    public HCanaryGameTargetVm? SelectedGameTarget { get => _selectedGameTarget; set { SetProperty(ref _selectedGameTarget, value); RaiseCommandStates(); } }
    public HCanaryInstructionVm? SelectedInstruction { get => _selectedInstruction; set { SetProperty(ref _selectedInstruction, value); RaiseCommandStates(); } }
    public HCanaryAnchorVm? SelectedAnchor { get => _selectedAnchor; set { SetProperty(ref _selectedAnchor, value); SelectedAnchorInstruction = null; RaiseCommandStates(); } }
    public HCanaryInstructionVm? SelectedAnchorInstruction { get => _selectedAnchorInstruction; set { SetProperty(ref _selectedAnchorInstruction, value); RaiseCommandStates(); } }
    public HCanaryStoredPatch? SelectedStoredPatch { get => _selectedStoredPatch; set { SetProperty(ref _selectedStoredPatch, value); RaiseCommandStates(); } }

    public void Load(string baseDirectory) => RefreshStoredPatches();

    public void SetContext(Rpcs3InstallDescriptor? install, Rpcs3GameDescriptor? game)
    {
        var gameChanged = _contextGame?.Serial != game?.Serial;
        _install = install;
        _contextGame = game;
        LoadContextHashes();
        RefreshStoredPatches();
        OnPropertyChanged(nameof(Rpcs3Path));
        OnPropertyChanged(nameof(LogPath));
        OnPropertyChanged(nameof(ImportedPatchPath));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentGameId));
        OnPropertyChanged(nameof(CurrentGameVersion));
        if (gameChanged && _dirty)
        {
            StatusMessage = UiText.T("HCanary.Message.DraftPreserved");
        }
        else if (game is not null && !_dirty && _currentIdentity is null)
        {
            NewDraft();
        }
        RaiseCommandStates();
    }

    public void Analyze(Rpcs3GameDescriptor? game) => SetContext(_install, game);

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Rpcs3Path));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentGameId));
        OnPropertyChanged(nameof(CurrentGameVersion));
        OnPropertyChanged(nameof(DraftStateText));
        RefreshDraft(markDirty: false);
    }

    private void NewDraft()
    {
        _suppressRefresh = true;
        PatchType = "PPU";
        ExecutableHash = string.Empty;
        PatchName = string.Empty;
        Author = string.Empty;
        Notes = string.Empty;
        Group = string.Empty;
        PatchVersion = "1.0";
        Status = HCanaryDraftStatus.Draft;
        Games.Clear();
        Instructions.Clear();
        Anchors.Clear();
        if (_contextGame is not null)
        {
            AddGame(HCanaryGameTargetVm.From(new HCanaryGameTarget
            {
                GameName = _contextGame.Title,
                GameId = _contextGame.Serial,
                GameVersion = Rpcs3PatchVersion.Normalize(_contextGame.Version) ?? string.Empty
            }));
        }
        else AddGame(new HCanaryGameTargetVm());
        AddInstruction(new HCanaryInstructionVm());
        _currentIdentity = null;
        SelectedStoredPatch = null;
        _dirty = false;
        _suppressRefresh = false;
        StatusMessage = UiText.T("HCanary.Message.NewDraft");
        RefreshDraft(markDirty: false);
    }

    private void AddCurrentGame()
    {
        if (_contextGame is null) return;
        var version = Rpcs3PatchVersion.Normalize(_contextGame.Version) ?? string.Empty;
        if (Games.Any(item => item.GameId.Equals(_contextGame.Serial, StringComparison.OrdinalIgnoreCase) && item.GameVersion == version)) return;
        AddGame(HCanaryGameTargetVm.From(new HCanaryGameTarget
        {
            GameName = _contextGame.Title, GameId = _contextGame.Serial, GameVersion = version
        }));
    }

    private void AddGame(HCanaryGameTargetVm item) { Subscribe(item); Games.Add(item); SelectedGameTarget = item; }
    private void AddInstruction(HCanaryInstructionVm item) { Subscribe(item); Instructions.Add(item); SelectedInstruction = item; }

    private void DeleteGame()
    {
        if (SelectedGameTarget is null || Games.Count <= 1) return;
        Games.Remove(SelectedGameTarget); SelectedGameTarget = Games.FirstOrDefault();
    }

    private void DeleteInstruction()
    {
        if (SelectedInstruction is null) return;
        var index = Instructions.IndexOf(SelectedInstruction);
        Instructions.Remove(SelectedInstruction);
        SelectedInstruction = Instructions.ElementAtOrDefault(Math.Min(index, Instructions.Count - 1));
    }

    private void DuplicateInstruction()
    {
        if (SelectedInstruction is null) return;
        var index = Instructions.IndexOf(SelectedInstruction) + 1;
        var copy = SelectedInstruction.Clone(); Subscribe(copy); Instructions.Insert(index, copy); SelectedInstruction = copy;
    }

    private bool CanMoveInstruction(int offset) => SelectedInstruction is not null &&
        Instructions.IndexOf(SelectedInstruction) + offset >= 0 && Instructions.IndexOf(SelectedInstruction) + offset < Instructions.Count;

    private void MoveInstruction(int offset)
    {
        if (!CanMoveInstruction(offset) || SelectedInstruction is null) return;
        Instructions.Move(Instructions.IndexOf(SelectedInstruction), Instructions.IndexOf(SelectedInstruction) + offset);
    }

    private void AddAnchor()
    {
        var index = Anchors.Count + 1;
        var anchor = new HCanaryAnchorVm { Name = $"Anchor{index}" };
        var instruction = new HCanaryInstructionVm(); Subscribe(instruction); anchor.Instructions.Add(instruction);
        Subscribe(anchor); anchor.Instructions.CollectionChanged += (_, _) => RefreshDraft();
        Anchors.Add(anchor); SelectedAnchor = anchor;
    }

    private void DeleteAnchor() { if (SelectedAnchor is not null) Anchors.Remove(SelectedAnchor); SelectedAnchor = Anchors.FirstOrDefault(); }

    private void AddAnchorInstruction()
    {
        if (SelectedAnchor is null) return;
        var item = new HCanaryInstructionVm(); Subscribe(item); SelectedAnchor.Instructions.Add(item); SelectedAnchorInstruction = item; RefreshDraft();
    }

    private void DeleteAnchorInstruction()
    {
        if (SelectedAnchor is null || SelectedAnchorInstruction is null) return;
        SelectedAnchor.Instructions.Remove(SelectedAnchorInstruction); SelectedAnchorInstruction = SelectedAnchor.Instructions.FirstOrDefault(); RefreshDraft();
    }

    private void AddAnchorLoad()
    {
        if (SelectedAnchor is null) return;
        AddInstruction(new HCanaryInstructionVm { Operation = "load", Address = SelectedAnchor.Name, Value = string.Empty });
    }

    private void Subscribe(VmBase item) => item.PropertyChanged += ItemChanged;
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != "Error") RefreshDraft(); }
    private void DraftCollectionChanged() { if (!_suppressRefresh) RefreshDraft(); }

    private HCanaryPatchDraft BuildDraft() => new()
    {
        PatchType = PatchType switch { "SPU" => HCanaryPatchType.Spu, "Canary" => HCanaryPatchType.Canary, _ => HCanaryPatchType.Ppu },
        ExecutableHash = ExecutableHash,
        PatchName = PatchName,
        Author = Author,
        Notes = Notes,
        Group = Group,
        PatchVersion = PatchVersion,
        Status = Status,
        Games = Games.Select(item => item.ToModel()).ToList(),
        Instructions = Instructions.Select(item => item.ToModel()).ToList(),
        Anchors = Anchors.Select(item => item.ToModel()).ToList()
    };

    private void RefreshDraft(bool markDirty = true)
    {
        if (_suppressRefresh) return;
        if (markDirty) { _dirty = true; OnPropertyChanged(nameof(IsDirty)); if (Status == HCanaryDraftStatus.Saved) Status = HCanaryDraftStatus.Draft; }
        var result = _authoring.Validate(BuildDraft());
        YamlPreview = string.IsNullOrEmpty(result.Yaml) ? SafePreview() : result.Yaml;
        IsValid = result.IsValid;
        HashError = FieldError(result, "ExecutableHash");
        PatchNameError = FieldError(result, "PatchName");
        PatchVersionError = FieldError(result, "PatchVersion");
        foreach (var item in Games) item.Error = FieldPrefixError(result, $"Games[{Games.IndexOf(item)}]");
        foreach (var item in Instructions) item.Error = FieldPrefixError(result, $"Instructions[{Instructions.IndexOf(item)}]");
        foreach (var item in Anchors) item.Error = FieldPrefixError(result, $"Anchors[{Anchors.IndexOf(item)}]");
        ValidationSummary = result.IsValid
            ? UiText.T("HCanary.Validation.Valid")
            : UiText.F("HCanary.Validation.Summary", result.Issues.Count) + Environment.NewLine +
              string.Join(Environment.NewLine, result.Issues.Select(issue => "• " + LocalizeIssue(issue)));
        CopyYamlCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(DraftStateText));
    }

    private string SafePreview()
    {
        try { return _authoring.GenerateYaml(BuildDraft()); }
        catch { return "Version: 1.2\r\n"; }
    }

    private static string FieldError(HCanaryValidationResult result, string field) =>
        result.Issues.FirstOrDefault(issue => issue.Field == field) is { } issue ? LocalizeIssue(issue) : string.Empty;

    private static string FieldPrefixError(HCanaryValidationResult result, string prefix) =>
        result.Issues.FirstOrDefault(issue => issue.Field.StartsWith(prefix, StringComparison.Ordinal)) is { } issue ? LocalizeIssue(issue) : string.Empty;

    private static string LocalizeIssue(HCanaryValidationIssue issue)
    {
        var key = "HCanary.Validation." + issue.Code;
        var text = UiText.T(key);
        return text == key ? issue.Message : text;
    }

    private void ReadLog()
    {
        if (_install is null) return;
        var result = _hashReader.Read(LogPath);
        Hashes.Clear();
        foreach (var hash in result.Hashes) Hashes.Add(new HCanaryHashVm(hash));
        OnPropertyChanged(nameof(HasHashes));
        LogStatus = !result.Succeeded
            ? UiText.F("HCanary.Log.Error", result.Error)
            : result.Hashes.Count == 0
                ? UiText.T("HCanary.Log.None")
                : UiText.F("HCanary.Log.Found", result.Hashes.Count, result.SourcePath);
    }

    private void LoadContextHashes()
    {
        Hashes.Clear();
        if (_contextGame is not null)
        {
            var source = _contextGame.HashSource ?? LogPath;
            foreach (var hash in _contextGame.AllPpuHashes) Hashes.Add(new HCanaryHashVm(new HCanaryHash(PatchHashKind.Ppu, hash, source, 0)));
            foreach (var hash in _contextGame.AllSpuHashes) Hashes.Add(new HCanaryHashVm(new HCanaryHash(PatchHashKind.Spu, hash, source, 0)));
        }
        OnPropertyChanged(nameof(HasHashes));
        LogStatus = Hashes.Count == 0 ? UiText.T("HCanary.Log.None") : UiText.F("HCanary.Log.Found", Hashes.Count, _contextGame?.HashSource ?? LogPath);
    }

    private void RefreshStoredPatches()
    {
        StoredPatches.Clear();
        if (_install is null || !File.Exists(ImportedPatchPath)) return;
        try { foreach (var patch in _flow.LoadHCanaryPatches(_install)) StoredPatches.Add(patch); }
        catch (Exception ex) { StatusMessage = UiText.F("HCanary.Error.ImportedInvalid", ex.Message); }
    }

    private void OpenStored()
    {
        if (SelectedStoredPatch is null) return;
        LoadDraft(SelectedStoredPatch.Draft);
        _currentIdentity = SelectedStoredPatch.Identity;
        _dirty = false;
        StatusMessage = UiText.T("HCanary.Message.Opened");
    }

    private void LoadDraft(HCanaryPatchDraft draft)
    {
        _suppressRefresh = true;
        PatchType = draft.PatchType switch { HCanaryPatchType.Spu => "SPU", HCanaryPatchType.Canary => "Canary", _ => "PPU" };
        ExecutableHash = draft.ExecutableHash; PatchName = draft.PatchName; Author = draft.Author; Notes = draft.Notes;
        Group = draft.Group; PatchVersion = draft.PatchVersion; Status = draft.Status;
        Games.Clear(); Instructions.Clear(); Anchors.Clear();
        foreach (var item in draft.Games) AddGame(HCanaryGameTargetVm.From(item));
        foreach (var item in draft.Instructions) AddInstruction(HCanaryInstructionVm.From(item));
        foreach (var item in draft.Anchors)
        {
            var anchor = HCanaryAnchorVm.From(item); Subscribe(anchor);
            foreach (var instruction in anchor.Instructions) Subscribe(instruction);
            anchor.Instructions.CollectionChanged += (_, _) => RefreshDraft(); Anchors.Add(anchor);
        }
        _suppressRefresh = false; RefreshDraft(markDirty: false);
    }

    private void Save(bool saveAsNew)
    {
        if (_install is null) return;
        if (Rpcs3InstallService.IsRpcs3Running())
        {
            UiDlg.Warn(UiText.T("HCanary.Error.Rpcs3Running"), UiText.T("HCanary.Title")); return;
        }
        try
        {
            var result = _flow.SaveHCanaryPatch(_install, BuildDraft(), _currentIdentity, saveAsNew);
            Status = HCanaryDraftStatus.Saved; _dirty = false;
            _currentIdentity = ExecutableHash.Trim() + "\0" + PatchName.Trim();
            StatusMessage = UiText.F("HCanary.Message.Saved", result.Path, result.PatchCount);
            RefreshStoredPatches(); CatalogChanged?.Invoke(this, EventArgs.Empty); RefreshDraft(markDirty: false);
        }
        catch (Exception ex) { UiDlg.Error(UiText.F("HCanary.Error.Save", ex.Message), UiText.T("HCanary.Title")); StatusMessage = ex.Message; }
    }

    private void DeleteStored()
    {
        if (_install is null || _currentIdentity is null || !UiDlg.Confirm(UiText.T("HCanary.Confirm.Delete"), UiText.T("HCanary.Title"))) return;
        try
        {
            _flow.DeleteHCanaryPatch(_install, _currentIdentity);
            CatalogChanged?.Invoke(this, EventArgs.Empty); RefreshStoredPatches(); NewDraft(); StatusMessage = UiText.T("HCanary.Message.Deleted");
        }
        catch (Exception ex) { UiDlg.Error(UiText.F("HCanary.Error.Delete", ex.Message), UiText.T("HCanary.Title")); }
    }

    private void OpenPatchesFolder() { if (_install is not null) OpenPath(_install.PatchesPath); }
    private void OpenImportedFile() { if (File.Exists(ImportedPatchPath)) OpenPath(ImportedPatchPath); }
    private void OpenRpcs3() { if (_install is not null) Process.Start(new ProcessStartInfo(_install.ExePath) { UseShellExecute = true }); }
    private static void OpenPath(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { ReadLogCommand, UseSelectedGameCommand, DeleteGameCommand, DeleteInstructionCommand,
                     DuplicateInstructionCommand, MoveInstructionUpCommand, MoveInstructionDownCommand, DeleteAnchorCommand,
                     AddAnchorInstructionCommand, DeleteAnchorInstructionCommand, AddAnchorLoadCommand, OpenStoredCommand,
                     SaveCommand, SaveAsCommand, DeleteStoredCommand, OpenPatchesFolderCommand, OpenImportedFileCommand,
                     OpenRpcs3Command, CopyYamlCommand }) command.RaiseCanExecuteChanged();
    }
}
