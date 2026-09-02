using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using R3Config;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.View;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

public sealed partial class MainVm : VmBase, IDisposable
{
    private const string SrcDsp = "RPCS3 patch catalog";

    private readonly AppFlow _flow;
    private readonly SetSvc _setSvc;
    private readonly Func<bool> _isRun;
    private readonly Func<string, string, bool> _confirm;
    private readonly Action<string, string> _info;
    private readonly Func<Rpcs3InstallDescriptor, Rpcs3GameDescriptor, GameLaunchResult> _launchGame;
    private Rpcs3InstallDescriptor? _install;
    private RecSvc? _catalog;
    private string? _loadErr;
    private PatCat? _patCat;
    private string? _catErr;
    private IReadOnlyList<PatchCatalogSourceStatus> _catSrc = [];
    private CfgPlan? _curPlan;
    private UiLaySt _layout = UiLaySvc.Current;
    private HwInfo? _hwInfo;
    private string _exePath = string.Empty;
    private string _r3Stat = UiText.GetText("Status.Rpcs3Missing");
    private string _status = UiText.Fmt("Status.DataSource", UiText.GetText("Cat.BuiltIn"));
    private string _recStat = UiText.Fmt("Status.RecCount", 0);
    private string _hwStat = UiText.GetText("Status.HwUnknown");
    private string _hwTip = UiText.GetText("Status.HwNotScanned");
    private string _bkpStat = UiText.Fmt("Status.BkpCount", 0);
    private bool _isBusy;
    private AnalyzeStage _busyStage = AnalyzeStage.ReadRpcs3;
    private bool _runUi;
    private bool _prepCan;
    private bool _isPatCf;
    public MainVm()
        : this(AppComposition.BuildRun())
    {
    }

    public MainVm(AppRuntimeServices services)
        : this(
            services,
            isRpcs3Running: null,
            confirm: null)
    {
    }

    public MainVm(
        AppRuntimeServices services,
        Func<bool>? isRpcs3Running,
        Func<string, string, bool>? confirm,
        Func<Rpcs3InstallDescriptor, Rpcs3GameDescriptor, GameLaunchResult>? launchGame = null,
        Action<string, string>? info = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _flow = services.Flow;
        _setSvc = services.Settings;
        _isRun = isRpcs3Running ??
            Rpcs3InstallService.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;
        _info = info ?? UiDlg.Info;
        _launchGame = launchGame ?? _flow.LaunchGame;
        HCanary = new HCanVm(
            _flow,
            _isRun,
            _confirm,
            _launchGame);

        R3Cmd = new RelayCmd(
            FindR3,
            () => !_disposed);
        AnlzCmd = new AsyncCmd(DoAsync, CanAnlz);
        LaunchGameCmd = new RelayCmd(
            LaunchSelectedGame,
            CanLaunchSelectedGame);
        HashRefreshCmd = new AsyncCmd(
            RefreshHashEvidenceAsync,
            CanRefreshHashEvidence);
        FingerprintCaptureCmd = new AsyncCmd(
            CaptureFingerprintAsync,
            CanCaptureFingerprint);
        PrepareGameCmd = new RelayCmd(
            PrepareGame,
            () => CanMut && _setupPreview?.CanApply == true);
        ApplyCmd = new RelayCmd(Apply, () => CanMut && Plan.HasChg && _curPlan is not null);
        DelSet = new RelayCmd(DelWiki, () => CanMut && Plan.CanDel && _curPlan is not null);
        OfclOn = new RelayCmd(
            () => ApplyPat(PatSect.Official),
            () => CanApply(PatSect.Official));
        OfclOff = new RelayCmd(
            () => OffPat(PatSect.Official),
            () => CanPatOf(PatSect.Official));
        CanOn = new RelayCmd(
            () => ApplyPat(PatSect.Canary),
            () => CanApply(PatSect.Canary));
        CanOCmd = new RelayCmd(
            () => OffPat(PatSect.Canary),
            () => CanPatOf(PatSect.Canary));
        ChtOn = new RelayCmd(
            () => ApplyPat(PatSect.Cheat),
            () => CanApply(PatSect.Cheat));
        ChtOff = new RelayCmd(
            () => OffPat(PatSect.Cheat),
            () => CanPatOf(PatSect.Cheat));
        ChtSelectAll = new RelayCmd(
            Preview.SelectAllCht,
            () => Preview.ChtItems.Any(item => item.CanSel));
        ChtClearSelection = new RelayCmd(
            Preview.ClearChtSelection,
            () => Preview.ChtItems.Any(item => item.IsSel));
        ChtRemove = new RelayCmd(
            RemoveManagedArtemis,
            () => CanMut && Preview.HasManagedChtSelection);
        RstCmd = new RelayCmd(RstBkp, () => CanMut && Advanced.SelBkp is not null);
        DelBkpCmd = new RelayCmd(
            DelBkp,
            () => CanMut && Advanced.SelBkp is not null);
        CleanBkpCmd = new RelayCmd(
            CleanBkp,
            () => CanMut &&
                  Advanced.Backups.Count > BackupUseCase.DefaultRetentionCount);
        SelCmd = new RelayCmd(Plan.Select, () => Plan.Items.Count > 0);
        ClearCmd = new RelayCmd(Plan.ClearSel, () => Plan.Items.Count > 0);
        WikiCmd = new RelayCmd(ImpWiki);
        BuildCmd = new RelayCmd(BuildCat, () => File.Exists(Advanced.WikiPath));
        InfoCmd = new RelayCmd(ShowCat);
        ExpCmd = new RelayCmd(ExpCat, () => _catalog is not null);
        CopyCmd = new RelayCmd(CopyLog, () => !string.IsNullOrWhiteSpace(Advanced.LogText));
        AdvCmd = new RelayCmd(ShowAdv);
        LangCmd = new RelayCmd(TogLang);
        CanDel = new RelayCmd(DelCan, () => CanMut && PrepCan);
        CanImp = new RelayCmd(ImpCan, () => CanMut);
        OfclMgr = new RelayCmd(
            OpenOfcl,
            () => !_disposed &&
                  _install is not null &&
                  File.Exists(_install.ExePath));
        PatDir = new RelayCmd(
            OpenDir,
            () => !_disposed &&
                  _install is not null &&
                  Directory.Exists(_install.PatPath));

        GameList.PropChg += (_, e) =>
        {
            if (e.PropertyName == nameof(ListVm.SelGame))
            {
                CancelFingerprintCapture();
                ResetHashLaunchState();
                MakePlan();
                HCanary.SetCtx(_install, GameList.SelGame?.Game);
                LoadGame();
                RefreshGameSetupPlan();
            }
        };

        Plan.SelState += (_, _) =>
        {
            RefreshGameSetupPreview();
            RaiseCmd();
        };
        Preview.SelState += (_, _) =>
        {
            RefreshGameSetupPreview();
            OnPatchPresentationChanged();
            RaiseCmd();
        };
        Advanced.PropChg += (_, e) =>
        {
            if (e.PropertyName == nameof(AdvVm.SelBkp))
            {
                RaiseCmd();
            }
        };
        HCanary.CatChg += (_, _) =>
        {
            LoadPat();
            MakePrev();
            RefreshGameSetupPlan();
            LoadBkp();
            LoadR3Ui();
        };
        HCanary.HashChg += OnHCanaryHashChanged;

        LoadCat();
        InitializeGameLibrary();
        HCanary.Load(AppContext.BaseDirectory);
        Log(UiText.GetText("Msg.Ready"));
    }

    public event EventHandler? AdvReq;

    public event EventHandler? LangChg;

    public ListVm GameList { get; } = new();

    public PlanVm Plan { get; } = new();

    public PatPrev Preview { get; } = new();

    public AdvVm Advanced { get; } = new();

    public HCanVm HCanary { get; }

    public RelayCmd R3Cmd { get; }

    public AsyncCmd AnlzCmd { get; }

    public RelayCmd LaunchGameCmd { get; }

    public AsyncCmd HashRefreshCmd { get; }

    public AsyncCmd FingerprintCaptureCmd { get; }

    public RelayCmd PrepareGameCmd { get; }

    public RelayCmd ApplyCmd { get; }

    public RelayCmd DelSet { get; }

    public RelayCmd OfclOn { get; }

    public RelayCmd OfclOff { get; }

    public RelayCmd CanOn { get; }

    public RelayCmd CanOCmd { get; }

    public RelayCmd ChtOn { get; }

    public RelayCmd ChtOff { get; }

    public RelayCmd ChtSelectAll { get; }

    public RelayCmd ChtClearSelection { get; }

    public RelayCmd ChtRemove { get; }

    public RelayCmd RstCmd { get; }

    public RelayCmd DelBkpCmd { get; }

    public RelayCmd CleanBkpCmd { get; }

    public RelayCmd SelCmd { get; }

    public RelayCmd ClearCmd { get; }

    public RelayCmd WikiCmd { get; }

    public RelayCmd BuildCmd { get; }

    public RelayCmd InfoCmd { get; }

    public RelayCmd ExpCmd { get; }

    public RelayCmd CopyCmd { get; }

    public RelayCmd AdvCmd { get; }

    public RelayCmd LangCmd { get; }

    public RelayCmd CanDel { get; }

    public RelayCmd CanImp { get; }

    public RelayCmd OfclMgr { get; }

    public RelayCmd PatDir { get; }

    public string LangText => UiText.GetText("Lang.Toggle");

    public string ToolTip => UiText.GetText("Lang.ToggleTip");


    public bool PrepCan
    {
        get => _prepCan;
        private set => Set(ref _prepCan, value);
    }

    public bool RunUi =>
        _runUi;

    public bool CanMut =>
        !_disposed &&
        _install is not null &&
        !IsBusy &&
        !RunUi &&
        !IsFingerprintCaptureRunning;

    public bool CanSelectGame =>
        !_disposed &&
        !IsBusy &&
        !RunUi &&
        !IsFingerprintCaptureRunning;

    public string SelGame => GameList.SelGame is null
        ? UiText.GetText("Shell.NoGameTitle")
        : string.IsNullOrWhiteSpace(GameList.SelGame.Game.Version)
            ? $"{GameList.SelGame.Title} | {GameList.SelGame.Serial}"
            : $"{GameList.SelGame.Title} | {GameList.SelGame.Serial} | v{PatVer.Norm(GameList.SelGame.Game.Version)}";

    public string GameSer => GameList.SelGame?.Serial ?? UiText.GetText("Shell.NoGameSerial");

    public bool HasGame => GameList.SelGame is not null;

    public bool ShowWarn => GameList.SelGame is not null && (GameList.SelGame.Pending > 0 || Preview.Items.Count > 0);

    public string PatReady
    {
        get
        {
            var selected = GameList.SelGame;
            if (selected is null)
            {
                return string.Empty;
            }

            if (HasHash(selected.Game))
            {
                return UiText.Fmt(
                    "PatchReadiness.HashReadyCount",
                    selected.Game.Hashes.Count,
                    selected.Game.SpuList.Count);
            }

            return selected.InR3
                ? UiText.GetText("PatchReadiness.NeedsColdBoot")
                : UiText.GetText("PatchReadiness.NotDetected");
        }
    }

    public string ReadyTip
    {
        get
        {
            var selected = GameList.SelGame;
            if (selected is null)
            {
                return string.Empty;
            }

            if (HasHash(selected.Game))
            {
                return UiText.Fmt(
                    "PatchReadiness.HashReadyDetail",
                    selected.Game.Hashes.Count,
                    selected.Game.SpuList.Count,
                    PatHash(selected.Game));
            }

            return selected.InR3
                ? UiText.GetText("PatchReadiness.NeedsColdBootTip")
                : UiText.GetText("PatchReadiness.NotDetectedTip");
        }
    }

    public string ReadyTon
    {
        get
        {
            var selected = GameList.SelGame;
            if (selected is null)
            {
                return "Muted";
            }

            if (HasHash(selected.Game))
            {
                return "Auto";
            }

            return selected.InR3 ? "Warning" : "Muted";
        }
    }

    public string ExePath
    {
        get => _exePath;
        set
        {
            if (Set(ref _exePath, value))
            {
                HandleExePathChanged(value);
            }
        }
    }

    public string R3Status
    {
        get => _r3Stat;
        set => Set(ref _r3Stat, value);
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public string RecStat
    {
        get => _recStat;
        set => Set(ref _recStat, value);
    }

    public string HwStatus
    {
        get => _hwStat;
        set => Set(ref _hwStat, value);
    }

    public string HwTip
    {
        get => _hwTip;
        set => Set(ref _hwTip, value);
    }

    public string BkpStat
    {
        get => _bkpStat;
        set => Set(ref _bkpStat, value);
    }

    public string BusyText => UiText.GetText(BusyKey(_busyStage));

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                AnlzCmd.RaiseCan();
                HCanary.SetAvail(
                    value,
                    RunUi);
                OnChange(nameof(CanMut));
                OnChange(nameof(CanSelectGame));
                RaiseCmd();
            }
        }
    }

    private void SetBusyStage(AnalyzeStage stage)
    {
        if (_busyStage == stage)
        {
            return;
        }

        _busyStage = stage;
        OnChange(nameof(BusyText));
    }

    private static string BusyKey(AnalyzeStage stage)
    {
        return stage switch
        {
            AnalyzeStage.ReadRpcs3 => "Busy.Analyze.ReadRpcs3",
            AnalyzeStage.ScanGames => "Busy.Analyze.ScanGames",
            AnalyzeStage.AnalyzeSettings => "Busy.Analyze.Settings",
            AnalyzeStage.LoadPatches => "Busy.Analyze.Patches",
            AnalyzeStage.PrepareResults => "Busy.Analyze.Results",
            _ => "Busy.Analyze.ReadRpcs3"
        };
    }

}
