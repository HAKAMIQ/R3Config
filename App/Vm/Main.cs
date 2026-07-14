using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using R3Config;
using R3Config.Comp;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.View;

namespace R3Config.Vm;

public sealed partial class MainVm : VmBase
{
    private const string SrcDsp = "RPCS3 patch catalog";

    private readonly AppFlow _flow;
    private readonly SetSvc _setSvc;
    private readonly Func<bool> _isRun;
    private readonly Func<string, string, bool> _confirm;
    private R3Inst? _install;
    private RecSvc? _catalog;
    private string? _loadErr;
    private PatCat? _patCat;
    private string? _catErr;
    private IReadOnlyList<PatSrcSt> _catSrc = [];
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
    private bool _runUi;
    private bool _prepCan;
    private bool _isPatCf;

    public MainVm()
        : this(AppComp.BuildRun())
    {
    }

    public MainVm(AppRun services)
        : this(
            services,
            isRpcs3Running: null,
            confirm: null)
    {
    }

    public MainVm(
        AppRun services,
        Func<bool>? isRpcs3Running,
        Func<string, string, bool>? confirm)
    {
        ArgumentNullException.ThrowIfNull(services);
        _flow = services.Flow;
        _setSvc = services.Settings;
        _isRun = isRpcs3Running ??
            InstSvc.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;
        HCanary = new HCanVm(
            _flow,
            _isRun,
            _confirm);

        R3Cmd = new RelayCmd(FindR3);
        AnlzCmd = new AsyncCmd(DoAsync, CanAnlz);
        ApplyCmd = new RelayCmd(Apply, () => CanMut && Plan.HasChg && _curPlan is not null);
        DelSet = new RelayCmd(DelWiki, () => CanMut && Plan.CanDel && _curPlan is not null);
        PatCmd = new RelayCmd(() => ApplyPat(null), () => CanApply(null));
        PatOff = new RelayCmd(() => OffPat(null), () => CanPatOf(null));
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
        RstCmd = new RelayCmd(RstBkp, () => CanMut && Advanced.SelBkp is not null);
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
        MgrCmd = new RelayCmd(
            OpenMgr,
            () => _install is not null && File.Exists(_install.ExePath));
        PatDir = new RelayCmd(
            OpenDir,
            () => _install is not null && Directory.Exists(_install.PatPath));

        GameList.PropChg += (_, e) =>
        {
            if (e.PropertyName == nameof(ListVm.SelGame))
            {
                MakePlan();
                HCanary.SetCtx(_install, GameList.SelGame?.Game);
                LoadGame();
            }
        };

        Plan.SelState += (_, _) => RaiseCmd();
        Preview.SelState += (_, _) => RaiseCmd();
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
            LoadBkp();
            LoadR3Ui();
        };

        var settings = _setSvc.Load();
        if (!string.IsNullOrWhiteSpace(settings.LastExe) && File.Exists(settings.LastExe))
        {
            ExePath = settings.LastExe;
            R3Status = UiText.GetText("Status.Rpcs3Ready");
        }

        LoadCat();
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

    public RelayCmd ApplyCmd { get; }

    public RelayCmd DelSet { get; }

    public RelayCmd PatCmd { get; }

    public RelayCmd PatOff { get; }

    public RelayCmd OfclOn { get; }

    public RelayCmd OfclOff { get; }

    public RelayCmd CanOn { get; }

    public RelayCmd CanOCmd { get; }

    public RelayCmd ChtOn { get; }

    public RelayCmd ChtOff { get; }

    public RelayCmd RstCmd { get; }

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

    public RelayCmd MgrCmd { get; }

    public RelayCmd PatDir { get; }

    public string LangText => UiText.GetText("Lang.Toggle");

    public string CurLang => UiText.GetText("Lang.Current");

    public string ToolTip => UiText.GetText("Lang.ToggleTip");

    public bool PrepCan
    {
        get => _prepCan;
        private set => Set(ref _prepCan, value);
    }

    public bool RunUi =>
        _runUi;

    public bool CanMut =>
        _install is not null &&
        !IsBusy &&
        !RunUi;

    public FlowDirection WinFlow => _layout.WinFlow;

    public FlowDirection ShFlow => _layout.ShFlow;

    public FlowDirection TechFlow => _layout.TechFlow;

    public XmlLanguage XmlLang => _layout.XmlLang;

    public int GameCol => _layout.GameCol;

    public int DetCol => _layout.DetCol;

    public GridLength GameWide => _layout.GameWide;

    public GridLength DetWide => _layout.DetWide;

    public GridLength LeftWide => _layout.LeftWide;

    public GridLength RghtWide => _layout.RghtWide;

    public FlowDirection TextFlow => _layout.TextFlow;

    public bool RtlLang => _layout.IsRtl;

    public TextAlignment UiAlign => _layout.TxtAlign;

    public HorizontalAlignment UiHoriz => _layout.HorAlign;

    public string SelGame => GameList.SelGame is null
        ? UiText.GetText("Shell.NoGameTitle")
        : string.IsNullOrWhiteSpace(GameList.SelGame.Game.Version)
            ? $"{GameList.SelGame.Title} | {GameList.SelGame.Serial}"
            : $"{GameList.SelGame.Title} | {GameList.SelGame.Serial} | v{PatVer.Norm(GameList.SelGame.Game.Version)}";

    public FlowDirection GameFlow => _layout.GameFlow(GameList.SelGame is not null);

    public TextAlignment GameAlgn => _layout.GameAlgn(GameList.SelGame is not null);

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
                AnlzCmd.RaiseCan();
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
                RaiseCmd();
            }
        }
    }
}
