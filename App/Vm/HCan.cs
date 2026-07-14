using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

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

public sealed partial class HCanaryVm : VmBase
{
    private readonly HCanaryPatchAuthoringService _authoring = new();
    private readonly HCanaryHashExtractionService _hashReader = new();
    private readonly AppFlow _flow;
    private readonly Func<bool> _isRpcs3Running;
    private readonly Func<string, string, bool> _confirm;
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
    private bool _isExperimental;
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
    private bool _isBusy;
    private bool _isRpcs3RunningPresentation;

    public HCanaryVm()
        : this(new AppFlow())
    {
    }

    public HCanaryVm(
        AppFlow flow,
        Func<bool>? isRpcs3Running = null,
        Func<string, string, bool>? confirm = null)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _isRpcs3Running = isRpcs3Running ??
            Rpcs3InstallService.IsRpcs3Running;
        _confirm = confirm ?? UiDlg.Confirm;

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
        SaveCommand = new RelayCommand(() => Save(false), () => IsValid && CanMutateCurrentInstall);
        SaveAsCommand = new RelayCommand(() => Save(true), () => IsValid && CanMutateCurrentInstall);
        DeleteStoredCommand = new RelayCommand(DeleteStored, () => CanMutateCurrentInstall && _currentIdentity is not null);
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

    public IReadOnlyList<string> PatchTypes { get; } = ["PPU", "SPU"];
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
    public bool IsExperimental
    {
        get => _isExperimental;
        set
        {
            if (SetProperty(ref _isExperimental, value))
            {
                OnPropertyChanged(nameof(PatchCategory));
                RefreshDraft();
            }
        }
    }
    public string PatchCategory
    {
        get => IsExperimental ? "Experimental" : "Regular";
        set => IsExperimental = value == "Experimental";
    }
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
    public bool CanMutateCurrentInstall =>
        _install is not null &&
        !_isBusy &&
        !_isRpcs3RunningPresentation;
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
            PatchType = value.Kind;
        }
    }

    public HCanaryGameTargetVm? SelectedGameTarget { get => _selectedGameTarget; set { SetProperty(ref _selectedGameTarget, value); RaiseCommandStates(); } }
    public HCanaryInstructionVm? SelectedInstruction { get => _selectedInstruction; set { SetProperty(ref _selectedInstruction, value); RaiseCommandStates(); } }
    public HCanaryAnchorVm? SelectedAnchor { get => _selectedAnchor; set { SetProperty(ref _selectedAnchor, value); SelectedAnchorInstruction = null; RaiseCommandStates(); } }
    public HCanaryInstructionVm? SelectedAnchorInstruction { get => _selectedAnchorInstruction; set { SetProperty(ref _selectedAnchorInstruction, value); RaiseCommandStates(); } }
    public HCanaryStoredPatch? SelectedStoredPatch { get => _selectedStoredPatch; set { SetProperty(ref _selectedStoredPatch, value); RaiseCommandStates(); } }
}
