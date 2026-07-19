using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Application;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class HashVm(HCanHash hash) : VmBase
{
    public HCanHash Hash { get; } = hash;
    public string Key => Hash.Key;
    public string Kind => Hash.Kind == HashKind.Ppu ? "PPU" : "SPU";
    public string Source => $"{Path.GetFileName(Hash.SrcPath)}:{Hash.Line}";
}

public sealed class TgtVm : VmBase
{
    private string _gmName = string.Empty;
    private string _gameId = string.Empty;
    private string _gameVer = string.Empty;
    private string _error = string.Empty;

    public string GameName { get => _gmName; set => Set(ref _gmName, value); }
    public string GameId { get => _gameId; set => Set(ref _gameId, value); }
    public string GameVer { get => _gameVer; set => Set(ref _gameVer, value); }
    public string Error { get => _error; set => Set(ref _error, value); }

    public HCanTgt ToModel() => new()
    {
        GameName = GameName, GameId = GameId, GameVer = GameVer
    };

    public static TgtVm From(HCanTgt value) => new()
    {
        GameName = value.GameName, GameId = value.GameId, GameVer = value.GameVer
    };
}

public sealed class InsVm : VmBase
{
    private string _op = "be32";
    private string _address = "0x00000000";
    private string _value = "0x60000000";
    private string _error = string.Empty;

    public string Op { get => _op; set => Set(ref _op, value); }
    public string Address { get => _address; set => Set(ref _address, value); }
    public string Value { get => _value; set => Set(ref _value, value); }
    public string Error { get => _error; set => Set(ref _error, value); }

    public HCanInst ToModel() => new()
    {
        Op = Op, Address = Address, Value = Value
    };

    public static InsVm From(HCanInst value) => new()
    {
        Op = value.Op, Address = value.Address, Value = value.Value
    };

    public InsVm Clone() => From(ToModel());
}

public sealed class AncVm : VmBase
{
    private string _name = string.Empty;
    private string _error = string.Empty;
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Error { get => _error; set => Set(ref _error, value); }
    public ObservableCollection<InsVm> Ins { get; } = [];

    public HCanAnc ToModel() => new()
    {
        Name = Name, Ins = Ins.Select(item => item.ToModel()).ToList()
    };

    public static AncVm From(HCanAnc value)
    {
        var vm = new AncVm { Name = value.Name };
        foreach (var item in value.Ins) vm.Ins.Add(InsVm.From(item));
        return vm;
    }
}

public sealed class DraftStatusVm(DraftSt value) : VmBase
{
    public DraftSt Value { get; } = value;
    public string Label => UiText.GetText($"HCanary.DraftStatus.{Value}");
    public void SetLang() => OnChange(nameof(Label));
}

public sealed partial class HCanVm : VmBase
{
    private readonly HCanaryPatchAuthoringService _auth = new();
    private readonly HashExt _reader = new();
    private readonly AppFlow _flow;
    private readonly Func<bool> _isRun;
    private readonly Func<string, string, bool> _confirm;
    private readonly Func<Rpcs3InstallDescriptor, Rpcs3GameDescriptor, GameLaunchResult> _launchGame;
    private Rpcs3InstallDescriptor? _install;
    private Rpcs3GameDescriptor? _game;
    private HashVm? _selHash;
    private TgtVm? _gameTgt;
    private InsVm? _selIns;
    private AncVm? _selAnc;
    private InsVm? _ancIns;
    private HCanStor? _selPat;
    private string? _curId;
    private bool _dirty;
    private bool _noRfsh;
    private string _patType = "PPU";
    private bool _isExp;
    private string _exeHash = string.Empty;
    private string _patName = string.Empty;
    private string _author = string.Empty;
    private string _notes = string.Empty;
    private string _group = string.Empty;
    private string _patVer = "1.0";
    private DraftSt _status = DraftSt.Draft;
    private string _preview = string.Empty;
    private string _vldSum = string.Empty;
    private string? _msgKey;
    private object?[] _msgArgs = [];
    private string _msgRaw = string.Empty;
    private string? _logStatKey;
    private object?[] _logStatArgs = [];
    private VldRes? _lastValidation;
    private string _hashErr = string.Empty;
    private string _nameErr = string.Empty;
    private string _verErr = string.Empty;
    private bool _isValid;
    private bool _isBusy;
    private bool _runUi;

    public HCanVm()
        : this(new AppFlow())
    {
    }

    public HCanVm(
        AppFlow flow,
        Func<bool>? isRpcs3Running = null,
        Func<string, string, bool>? confirm = null,
        Func<Rpcs3InstallDescriptor, Rpcs3GameDescriptor, GameLaunchResult>? launchGame = null)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _isRun = isRpcs3Running ??
            Rpcs3InstallService.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;
        _launchGame = launchGame ?? _flow.LaunchGame;

        NewCmd = new RelayCmd(NewDraft);
        ReadCmd = new RelayCmd(ReadLog, () => _install is not null);
        LaunchCmd = new RelayCmd(
            LaunchGame,
            () => _install is not null && _game is not null);
        UseCmd = new RelayCmd(AddCur, () => _game is not null);
        AddGmCmd = new RelayCmd(() => AddGame(new TgtVm()));
        DelGmCmd = new RelayCmd(DelGame, () => GameTgt is not null && Games.Count > 1);
        AddICmd = new RelayCmd(() => AddIns(new InsVm()));
        DelICmd = new RelayCmd(DelIns, () => SelIns is not null);
        DupICmd = new RelayCmd(DupIns, () => SelIns is not null);
        InsUp = new RelayCmd(() => MoveIns(-1), () => CanMove(-1));
        InsDown = new RelayCmd(() => MoveIns(1), () => CanMove(1));
        AddACmd = new RelayCmd(AddAnc);
        DelACmd = new RelayCmd(DelAnc, () => SelAnc is not null);
        AncAdd = new RelayCmd(AddAncI, () => SelAnc is not null);
        AncDel = new RelayCmd(DelAncI, () => AncIns is not null && SelAnc is not null);
        AncLoad = new RelayCmd(LoadAnc, () => SelAnc is not null);
        OpenCmd = new RelayCmd(OpenSave, () => SelPat is not null);
        SaveCmd = new RelayCmd(() => Save(false), () => IsValid && CanMut);
        SaveAs = new RelayCmd(() => Save(true), () => IsValid && CanMut);
        DelCmd = new RelayCmd(DelSave, () => CanMut && _curId is not null);
        PatDir = new RelayCmd(OpenDir, () => _install is not null);
        ImpFile = new RelayCmd(OpenFile, () => _install is not null && File.Exists(PatPath));
        R3Cmd = new RelayCmd(OpenR3, () => _install is not null && File.Exists(_install.ExePath));
        CopyCmd = new RelayCmd(() => UiDlg.CopyText(Preview), () => !string.IsNullOrWhiteSpace(Preview));
        Games.CollectionChanged += (_, _) => OnDraft();
        Ins.CollectionChanged += (_, _) => OnDraft();
        Anchors.CollectionChanged += (_, _) => OnDraft();
        NewDraft();
    }

    public event EventHandler? CatChg;

    public IReadOnlyList<string> PatTypes { get; } = ["PPU", "SPU"];
    public IReadOnlyList<string> Op => HCanaryPatchAuthoringService.Op;
    public IReadOnlyList<DraftStatusVm> Statuses { get; } =
        Enum.GetValues<DraftSt>().Select(value => new DraftStatusVm(value)).ToArray();
    public ObservableCollection<HashVm> Hashes { get; } = [];
    public ObservableCollection<TgtVm> Games { get; } = [];
    public ObservableCollection<InsVm> Ins { get; } = [];
    public ObservableCollection<AncVm> Anchors { get; } = [];
    public ObservableCollection<HCanStor> Patches { get; } = [];

    public bool CanAdvanceTarget =>
        !string.IsNullOrWhiteSpace(ExeHash) &&
        string.IsNullOrWhiteSpace(HashErr) &&
        Games.Count > 0 &&
        Games.All(item => string.IsNullOrWhiteSpace(item.Error));

    public bool CanAdvanceDetails =>
        CanAdvanceTarget &&
        string.IsNullOrWhiteSpace(NameErr) &&
        string.IsNullOrWhiteSpace(VerErr);

    public bool CanAdvanceInstructions =>
        CanAdvanceDetails &&
        Ins.Count > 0 &&
        Ins.All(item => string.IsNullOrWhiteSpace(item.Error)) &&
        Anchors.All(item => string.IsNullOrWhiteSpace(item.Error));

    public RelayCmd NewCmd { get; }
    public RelayCmd ReadCmd { get; }
    public RelayCmd LaunchCmd { get; }
    public RelayCmd UseCmd { get; }
    public RelayCmd AddGmCmd { get; }
    public RelayCmd DelGmCmd { get; }
    public RelayCmd AddICmd { get; }
    public RelayCmd DelICmd { get; }
    public RelayCmd DupICmd { get; }
    public RelayCmd InsUp { get; }
    public RelayCmd InsDown { get; }
    public RelayCmd AddACmd { get; }
    public RelayCmd DelACmd { get; }
    public RelayCmd AncAdd { get; }
    public RelayCmd AncDel { get; }
    public RelayCmd AncLoad { get; }
    public RelayCmd OpenCmd { get; }
    public RelayCmd SaveCmd { get; }
    public RelayCmd SaveAs { get; }
    public RelayCmd DelCmd { get; }
    public RelayCmd PatDir { get; }
    public RelayCmd ImpFile { get; }
    public RelayCmd R3Cmd { get; }
    public RelayCmd CopyCmd { get; }

    public string PatType { get => _patType; set { if (Set(ref _patType, value)) LoadDrft(); } }
    public bool IsExp
    {
        get => _isExp;
        set
        {
            if (Set(ref _isExp, value))
            {
                OnChange(nameof(Category));
                LoadDrft();
            }
        }
    }
    public string Category
    {
        get => IsExp ? "Experimental" : "Regular";
        set => IsExp = value == "Experimental";
    }
    public string ExeHash
    {
        get => _exeHash;
        set
        {
            if (Set(ref _exeHash, value))
            {
                OnChange(nameof(HasHashInput));
                LoadDrft();
            }
        }
    }
    public string PatName { get => _patName; set { if (Set(ref _patName, value)) LoadDrft(); } }
    public string Author { get => _author; set { if (Set(ref _author, value)) LoadDrft(); } }
    public string Notes { get => _notes; set { if (Set(ref _notes, value)) LoadDrft(); } }
    public string Group { get => _group; set { if (Set(ref _group, value)) LoadDrft(); } }
    public string PatVer { get => _patVer; set { if (Set(ref _patVer, value)) LoadDrft(); } }
    public DraftSt Status
    {
        get => _status;
        set
        {
            if (!Set(ref _status, value) || _noRfsh) return;
            _dirty = true;
            OnChange(nameof(IsDirty));
            LoadDrft(markDirty: false);
        }
    }
    public string Preview { get => _preview; private set => Set(ref _preview, value); }
    public string VldSum { get => _vldSum; private set => Set(ref _vldSum, value); }
    public string Msg => ResolveLocalizedText(_msgKey, _msgArgs, _msgRaw);
    public string LogStat => ResolveLocalizedText(_logStatKey, _logStatArgs, string.Empty);
    public string HashErr { get => _hashErr; private set => Set(ref _hashErr, value); }
    public string NameErr { get => _nameErr; private set => Set(ref _nameErr, value); }
    public string VerErr { get => _verErr; private set => Set(ref _verErr, value); }
    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (Set(ref _isValid, value))
            {
                OnChange(nameof(SaveHint));
                RaiseCmd();
            }
        }
    }
    public bool IsDirty => _dirty;
    public bool CanMut =>
        _install is not null &&
        !_isBusy &&
        !_runUi;
    public bool HasHash => Hashes.Count > 0;
    public bool HasHashInput => !string.IsNullOrWhiteSpace(ExeHash);
    public bool HasGame => _game is not null;
    public bool IsStoredEmpty => Patches.Count == 0;
    public bool HasStoredPatch => _curId is not null;
    public string SaveHint => _install is null
        ? UiText.GetText("HCanary.SaveHint.MissingRpcs3")
        : _runUi
            ? UiText.GetText("HCanary.SaveHint.Rpcs3Running")
            : _isBusy
                ? UiText.GetText("HCanary.SaveHint.Busy")
                : IsValid
                    ? UiText.GetText("HCanary.SaveHint.Ready")
                    : UiText.GetText("HCanary.SaveHint.Invalid");
    public string R3Path => _install?.RootPath ?? UiText.GetText("HCanary.Error.Rpcs3Missing");
    public string LogPath => _install is null ? string.Empty : Path.Combine(_install.RootPath, "RPCS3.log");
    public string PatPath => _install is null ? string.Empty : Path.Combine(_install.PatPath, "imported_patch.yml");
    public string GameName => _game?.Title ?? UiText.GetText("HCanary.Error.GameMissing");
    public string GameId => _game?.Serial ?? "—";
    public string GameVer => _game?.Version ?? "—";
    public string DraftTxt => UiText.GetText($"HCanary.DraftStatus.{Status}");

    public HashVm? SelHash
    {
        get => _selHash;
        set
        {
            if (!Set(ref _selHash, value) || value is null) return;
            ExeHash = value.Key;
            PatType = value.Kind;
        }
    }

    public TgtVm? GameTgt { get => _gameTgt; set { Set(ref _gameTgt, value); RaiseCmd(); } }
    public InsVm? SelIns { get => _selIns; set { Set(ref _selIns, value); RaiseCmd(); } }
    public AncVm? SelAnc { get => _selAnc; set { Set(ref _selAnc, value); AncIns = null; RaiseCmd(); } }
    public InsVm? AncIns { get => _ancIns; set { Set(ref _ancIns, value); RaiseCmd(); } }
    public HCanStor? SelPat { get => _selPat; set { Set(ref _selPat, value); RaiseCmd(); } }
}
