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
    private const string PatchCatalogSourceDisplay = "RPCS3 patch catalog";

    private readonly AppFlow _flow;
    private readonly AppSettingsService _settingsService;
    private readonly Func<bool> _isRpcs3Running;
    private readonly Func<string, string, bool> _confirm;
    private R3Inst? _install;
    private RecSvc? _catalog;
    private string? _catalogLoadError;
    private PatCat? _patchCatalog;
    private string? _patchCatalogError;
    private IReadOnlyList<PatSrcSt> _patchCatalogSources = [];
    private CfgPlan? _currentPlan;
    private UiLayoutState _layout = UiLayoutService.Current;
    private HwInfo? _hwInfo;
    private string _rpcs3ExePath = string.Empty;
    private string _rpcs3Status = UiText.T("Status.Rpcs3Missing");
    private string _dataStatus = UiText.F("Status.DataSource", UiText.T("Cat.BuiltIn"));
    private string _recommendationsStatus = UiText.F("Status.RecCount", 0);
    private string _hardwareStatus = UiText.T("Status.HwUnknown");
    private string _hardwareTooltip = UiText.T("Status.HwNotScanned");
    private string _backupsStatus = UiText.F("Status.BkpCount", 0);
    private bool _isBusy;
    private bool _isRpcs3RunningPresentation;
    private bool _hasPreparedCanaryPatches;
    private bool _isPatchConfigReadable;

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
        _settingsService = services.Settings;
        _isRpcs3Running = isRpcs3Running ??
            InstSvc.IsR3;
        _confirm = confirm ?? UiDlg.Confirm;
        HCanary = new HCanaryVm(
            _flow,
            _isRpcs3Running,
            _confirm);

        BrowseRpcs3Command = new RelayCommand(BrowseRpcs3);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze);
        ApplyCheckedCommand = new RelayCommand(ApplyChecked, () => CanMutateCurrentInstall && Plan.HasSelectedChanges && _currentPlan is not null);
        RemoveWikiSettingsCommand = new RelayCommand(RemoveWikiSettings, () => CanMutateCurrentInstall && Plan.HasRemovableSettings && _currentPlan is not null);
        ApplyCheckedPatchesCommand = new RelayCommand(() => ApplyCheckedPatches(null), () => CanApplyCheckedPatches(null));
        OffPatchesCommand = new RelayCommand(() => OffPatches(null), () => CanDisableCheckedPatches(null));
        ApplyOfficialPatchesCommand = new RelayCommand(
            () => ApplyCheckedPatches(PatchSection.Official),
            () => CanApplyCheckedPatches(PatchSection.Official));
        DisableOfficialPatchesCommand = new RelayCommand(
            () => OffPatches(PatchSection.Official),
            () => CanDisableCheckedPatches(PatchSection.Official));
        ApplyCanaryPatchesCommand = new RelayCommand(
            () => ApplyCheckedPatches(PatchSection.Canary),
            () => CanApplyCheckedPatches(PatchSection.Canary));
        DisableCanaryPatchesCommand = new RelayCommand(
            () => OffPatches(PatchSection.Canary),
            () => CanDisableCheckedPatches(PatchSection.Canary));
        ApplyCheatPatchesCommand = new RelayCommand(
            () => ApplyCheckedPatches(PatchSection.Cheat),
            () => CanApplyCheckedPatches(PatchSection.Cheat));
        DisableCheatPatchesCommand = new RelayCommand(
            () => OffPatches(PatchSection.Cheat),
            () => CanDisableCheckedPatches(PatchSection.Cheat));
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => CanMutateCurrentInstall && Advanced.SelectedBackup is not null);
        SelectRecommendedCommand = new RelayCommand(Plan.SelectRecommended, () => Plan.Items.Count > 0);
        ClearSelectionCommand = new RelayCommand(Plan.ClearSelection, () => Plan.Items.Count > 0);
        ImportWikiXmlCommand = new RelayCommand(ImportWikiXml);
        RebuildCatalogCommand = new RelayCommand(RebuildCatalog, () => File.Exists(Advanced.DeveloperWikiPath));
        ShowCatalogInfoCommand = new RelayCommand(ShowCatalogInfo);
        ExportCatalogCommand = new RelayCommand(ExportCatalog, () => _catalog is not null);
        CopyLogCommand = new RelayCommand(CopyLog, () => !string.IsNullOrWhiteSpace(Advanced.LogText));
        ShowAdvancedCommand = new RelayCommand(ShowAdvanced);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);
        RemoveCanaryPatchesCommand = new RelayCommand(RemoveCanaryPatches, () => CanMutateCurrentInstall && HasPreparedCanaryPatches);
        ImportCanaryPatchCommand = new RelayCommand(ImportCanaryPatch, () => CanMutateCurrentInstall);
        OpenRpcs3PatchManagerCommand = new RelayCommand(
            OpenRpcs3PatchManager,
            () => _install is not null && File.Exists(_install.ExePath));
        OpenPatchesFolderCommand = new RelayCommand(
            OpenPatchesFolder,
            () => _install is not null && Directory.Exists(_install.PatPath));

        GameList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameListVm.SelectedGame))
            {
                BuildCurrentPlan();
                HCanary.SetContext(_install, GameList.SelectedGame?.Game);
                RefreshSelectedGameDisplay();
            }
        };

        Plan.SelectionStateChanged += (_, _) => RaiseCommandStates();
        PatchPreview.SelectionStateChanged += (_, _) => RaiseCommandStates();
        Advanced.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AdvancedVm.SelectedBackup))
            {
                RaiseCommandStates();
            }
        };
        HCanary.CatalogChanged += (_, _) =>
        {
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
            RefreshBackups();
            RefreshRpcs3PresentationState();
        };

        var settings = _settingsService.Load();
        if (!string.IsNullOrWhiteSpace(settings.LastRpcs3ExePath) && File.Exists(settings.LastRpcs3ExePath))
        {
            Rpcs3ExePath = settings.LastRpcs3ExePath;
            Rpcs3Status = UiText.T("Status.Rpcs3Ready");
        }

        LoadBuiltInCatalog();
        HCanary.Load(AppContext.BaseDirectory);
        Log(UiText.T("Msg.Ready"));
    }

    public event EventHandler? RequestAdvancedDialog;

    public event EventHandler? LanguageChanged;

    public GameListVm GameList { get; } = new();

    public PlanVm Plan { get; } = new();

    public PatchPreviewVm PatchPreview { get; } = new();

    public AdvancedVm Advanced { get; } = new();

    public HCanaryVm HCanary { get; }

    public RelayCommand BrowseRpcs3Command { get; }

    public AsyncRelayCommand AnalyzeCommand { get; }

    public RelayCommand ApplyCheckedCommand { get; }

    public RelayCommand RemoveWikiSettingsCommand { get; }

    public RelayCommand ApplyCheckedPatchesCommand { get; }

    public RelayCommand OffPatchesCommand { get; }

    public RelayCommand ApplyOfficialPatchesCommand { get; }

    public RelayCommand DisableOfficialPatchesCommand { get; }

    public RelayCommand ApplyCanaryPatchesCommand { get; }

    public RelayCommand DisableCanaryPatchesCommand { get; }

    public RelayCommand ApplyCheatPatchesCommand { get; }

    public RelayCommand DisableCheatPatchesCommand { get; }

    public RelayCommand RestoreBackupCommand { get; }

    public RelayCommand SelectRecommendedCommand { get; }

    public RelayCommand ClearSelectionCommand { get; }

    public RelayCommand ImportWikiXmlCommand { get; }

    public RelayCommand RebuildCatalogCommand { get; }

    public RelayCommand ShowCatalogInfoCommand { get; }

    public RelayCommand ExportCatalogCommand { get; }

    public RelayCommand CopyLogCommand { get; }

    public RelayCommand ShowAdvancedCommand { get; }

    public RelayCommand ToggleLanguageCommand { get; }

    public RelayCommand RemoveCanaryPatchesCommand { get; }

    public RelayCommand ImportCanaryPatchCommand { get; }

    public RelayCommand OpenRpcs3PatchManagerCommand { get; }

    public RelayCommand OpenPatchesFolderCommand { get; }

    public string ToggleLanguageText => UiText.T("Lang.Toggle");

    public string CurrentLanguageText => UiText.T("Lang.Current");

    public string ToggleLanguageToolTip => UiText.T("Lang.ToggleTip");

    public bool HasPreparedCanaryPatches
    {
        get => _hasPreparedCanaryPatches;
        private set => SetProperty(ref _hasPreparedCanaryPatches, value);
    }

    public bool IsRpcs3RunningPresentation =>
        _isRpcs3RunningPresentation;

    public bool CanMutateCurrentInstall =>
        _install is not null &&
        !IsBusy &&
        !IsRpcs3RunningPresentation;

    public FlowDirection WindowFlow => _layout.WindowFlow;

    public FlowDirection ShellFlow => _layout.ShellFlow;

    public FlowDirection TechnicalFlow => _layout.TechnicalFlow;

    public XmlLanguage XmlLanguage => _layout.XmlLanguage;

    public int GamesPanelColumn => _layout.GamesPanelColumn;

    public int DetailsPanelColumn => _layout.DetailsPanelColumn;

    public GridLength GamesPanelWidth => _layout.GamesPanelWidth;

    public GridLength DetailsPanelWidth => _layout.DetailsPanelWidth;

    public GridLength LeftShellColumnWidth => _layout.LeftShellColumnWidth;

    public GridLength RightShellColumnWidth => _layout.RightShellColumnWidth;

    public FlowDirection TextFlow => _layout.TextFlow;

    public bool IsRtlLanguage => _layout.IsRtl;

    public TextAlignment UiTextAlignment => _layout.TextAlignment;

    public HorizontalAlignment UiHorizontalAlignment => _layout.HorizontalAlignment;

    public string SelectedGameTitle => GameList.SelectedGame is null
        ? UiText.T("Shell.NoGameTitle")
        : string.IsNullOrWhiteSpace(GameList.SelectedGame.Game.Version)
            ? $"{GameList.SelectedGame.Title} | {GameList.SelectedGame.Serial}"
            : $"{GameList.SelectedGame.Title} | {GameList.SelectedGame.Serial} | v{PatVer.Norm(GameList.SelectedGame.Game.Version)}";

    public FlowDirection SelectedGameTitleFlow => _layout.SelectedGameTitleFlow(GameList.SelectedGame is not null);

    public TextAlignment SelectedGameTitleAlignment => _layout.SelectedGameTitleAlignment(GameList.SelectedGame is not null);

    public string SelectedGameSerial => GameList.SelectedGame?.Serial ?? UiText.T("Shell.NoGameSerial");

    public bool HasSelectedGame => GameList.SelectedGame is not null;

    public bool ShowGameAlert => GameList.SelectedGame is not null && (GameList.SelectedGame.Pending > 0 || PatchPreview.Items.Count > 0);

    public string SelectedGamePatchReadinessText
    {
        get
        {
            var selected = GameList.SelectedGame;
            if (selected is null)
            {
                return string.Empty;
            }

            if (HasSelectedGameHashes(selected.Game))
            {
                return UiText.F(
                    "PatchReadiness.HashReadyCount",
                    selected.Game.Hashes.Count,
                    selected.Game.SpuList.Count);
            }

            return selected.IsDetectedInRpcs3
                ? UiText.T("PatchReadiness.NeedsColdBoot")
                : UiText.T("PatchReadiness.NotDetected");
        }
    }

    public string SelectedGamePatchReadinessTooltip
    {
        get
        {
            var selected = GameList.SelectedGame;
            if (selected is null)
            {
                return string.Empty;
            }

            if (HasSelectedGameHashes(selected.Game))
            {
                return UiText.F(
                    "PatchReadiness.HashReadyDetail",
                    selected.Game.Hashes.Count,
                    selected.Game.SpuList.Count,
                    PatchHashSourceDisplay(selected.Game));
            }

            return selected.IsDetectedInRpcs3
                ? UiText.T("PatchReadiness.NeedsColdBootTip")
                : UiText.T("PatchReadiness.NotDetectedTip");
        }
    }

    public string SelectedGamePatchReadinessTone
    {
        get
        {
            var selected = GameList.SelectedGame;
            if (selected is null)
            {
                return "Muted";
            }

            if (HasSelectedGameHashes(selected.Game))
            {
                return "Auto";
            }

            return selected.IsDetectedInRpcs3 ? "Warning" : "Muted";
        }
    }

    public string Rpcs3ExePath
    {
        get => _rpcs3ExePath;
        set
        {
            if (SetProperty(ref _rpcs3ExePath, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Rpcs3Status
    {
        get => _rpcs3Status;
        set => SetProperty(ref _rpcs3Status, value);
    }

    public string DataStatus
    {
        get => _dataStatus;
        set => SetProperty(ref _dataStatus, value);
    }

    public string RecommendationsStatus
    {
        get => _recommendationsStatus;
        set => SetProperty(ref _recommendationsStatus, value);
    }

    public string HardwareStatus
    {
        get => _hardwareStatus;
        set => SetProperty(ref _hardwareStatus, value);
    }

    public string HardwareTooltip
    {
        get => _hardwareTooltip;
        set => SetProperty(ref _hardwareTooltip, value);
    }

    public string BackupsStatus
    {
        get => _backupsStatus;
        set => SetProperty(ref _backupsStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                HCanary.UpdateMutationAvailability(
                    value,
                    IsRpcs3RunningPresentation);
                OnPropertyChanged(nameof(CanMutateCurrentInstall));
                RaiseCommandStates();
            }
        }
    }
}
