using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using R3Config;
using AppSvc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.View;

namespace R3Config.Vm;

public sealed class MainVm : VmBase
{
    private const string PatchCatalogSourceDisplay = "RPCS3 patch catalog";

    private readonly AppFlow _flow = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly CanaryPatchImportService _canaryPatchImportService = new();
    private Rpcs3InstallDescriptor? _install;
    private RecommendationCatalogService? _catalog;
    private string? _catalogLoadError;
    private Rpcs3PatchCatalog? _patchCatalog;
    private string? _patchCatalogError;
    private ConfigPlan? _currentPlan;
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
    private bool _hasPreparedCanaryPatches;

    public MainVm()
    {
        BrowseRpcs3Command = new RelayCommand(BrowseRpcs3);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze);
        ApplyCheckedCommand = new RelayCommand(ApplyChecked, () => Plan.HasSelectedChanges && _currentPlan is not null && _install is not null);
        RemoveWikiSettingsCommand = new RelayCommand(RemoveWikiSettings, () => Plan.HasRemovableSettings && _currentPlan is not null && _install is not null);
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
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => Advanced.SelectedBackup is not null && _install is not null);
        SelectRecommendedCommand = new RelayCommand(Plan.SelectRecommended, () => Plan.Items.Count > 0);
        ClearSelectionCommand = new RelayCommand(Plan.ClearSelection, () => Plan.Items.Count > 0);
        ImportWikiXmlCommand = new RelayCommand(ImportWikiXml);
        RebuildCatalogCommand = new RelayCommand(RebuildCatalog, () => File.Exists(Advanced.DeveloperWikiPath));
        ShowCatalogInfoCommand = new RelayCommand(ShowCatalogInfo);
        ExportCatalogCommand = new RelayCommand(ExportCatalog, () => _catalog is not null);
        CopyLogCommand = new RelayCommand(CopyLog, () => !string.IsNullOrWhiteSpace(Advanced.LogText));
        ShowAdvancedCommand = new RelayCommand(ShowAdvanced);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);
        RemoveCanaryPatchesCommand = new RelayCommand(RemoveCanaryPatches, () => _install is not null && HasPreparedCanaryPatches);
        ImportCanaryPatchCommand = new RelayCommand(ImportCanaryPatch, () => _install is not null);

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
        HCanary.CatalogChanged += (_, _) =>
        {
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
            RefreshBackups();
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

    public GameListVm GameList { get; } = new();

    public PlanVm Plan { get; } = new();

    public PatchPreviewVm PatchPreview { get; } = new();

    public AdvancedVm Advanced { get; } = new();

    public HCanaryVm HCanary { get; } = new();

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

    public string ToggleLanguageText => UiText.T("Lang.Toggle");

    public string CurrentLanguageText => UiText.T("Lang.Current");

    public string ToggleLanguageToolTip => UiText.T("Lang.ToggleTip");

    public bool HasPreparedCanaryPatches
    {
        get => _hasPreparedCanaryPatches;
        private set => SetProperty(ref _hasPreparedCanaryPatches, value);
    }

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
            : $"{GameList.SelectedGame.Title} | {GameList.SelectedGame.Serial} | v{Rpcs3PatchVersion.Normalize(GameList.SelectedGame.Game.Version)}";

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
                    selected.Game.AllPpuHashes.Count,
                    selected.Game.AllSpuHashes.Count);
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
                    selected.Game.AllPpuHashes.Count,
                    selected.Game.AllSpuHashes.Count,
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
            }
        }
    }

    public void ShowAdvanced()
    {
        RequestAdvancedDialog?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleLanguage()
    {
        var nextLang = UiText.IsRtl ? UiText.EnglishLang : UiText.ArabicLang;
        UiText.SetLang(nextLang);
        UiLayoutService.ApplyApplicationResources();
        UiPrefs.SaveLang(UiText.CurrentLang);
        RefreshLanguageText();
    }

    private void RemoveCanaryPatches()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Warn(UiText.T("Patch.ImportCanaryNoRpcs3Path"), UiText.T("Patch.RemoveCanary"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var result = _flow.RemovePreparedCanaryPatch(_install);
            if (!result.Succeeded)
            {
                UiDlg.Error(result.Error ?? result.Message, UiText.T("Patch.RemoveCanary"));
                Log(result.Error ?? result.Message);
                return;
            }

            Log(result.Message);
            UiDlg.Info(result.Message, UiText.T("Patch.RemoveCanary"));
            RefreshCanaryState();
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Patch.RemoveCanary"));
            Log(ex.Message);
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void ImportCanaryPatch()
    {
        if (_install is null)
        {
            UiDlg.Warn(UiText.T("Patch.ImportCanaryNoRpcs3Path"), UiText.T("Patch.ImportCanaryTitle"));
            return;
        }

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            ?? Application.Current?.MainWindow;

        var viewModel = new CanaryPatchImportVm(_install.PatchesPath);
        var dialog = new CanaryPatchImportDialog(owner, viewModel);

        if (dialog.ShowDialog() == true)
        {
            Log(viewModel.ResultMessage);
            UiDlg.Info(viewModel.ResultMessage, UiText.T("Patch.ImportCanaryTitle"));
            PatchPreview.ShowExpertPatches = true;
            RefreshCanaryState();
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
        }
    }

    private void BrowseRpcs3()
    {
        var fileName = UiDlg.PickRpcs3Exe(Rpcs3ExePath);
        if (fileName is null)
        {
            return;
        }

        Rpcs3ExePath = fileName;
        Rpcs3Status = UiText.T("Status.Rpcs3Ready");
        SaveSettings();
        Log(UiText.F("Msg.SelectRpcs3", fileName));
    }

    private async Task AnalyzeAsync()
    {
        try
        {
            IsBusy = true;
            Log(UiText.T("Msg.StartAnalyze"));

            if (_catalog is null)
            {
                throw new InvalidOperationException(_catalogLoadError ?? CatalogMissingException.UserMessage);
            }

            var result = await _flow.AnalyzeAsync(Rpcs3ExePath, _catalog);
            _install = result.Install;
            RefreshCanaryState();
            _hwInfo = result.Hw;
            _patchCatalog = result.PatchCat.Cat;
            _patchCatalogError = result.PatchCat.Error;
            Rpcs3Status = UiText.T("Status.Rpcs3Ready");
            SaveSettings();

            Advanced.ConfigPath = _install.ConfigPath;
            Advanced.CustomConfigsPath = _install.CustomConfigsPath;
            Advanced.PatchesPath = _install.PatchesPath;

            HardwareStatus = FormatHardwareStatus();
            HardwareTooltip = FormatHardwareTooltip();
            Log(UiText.F("Msg.HwScanned", _hwInfo.Tier));

            var detectedRows = result.Games
                .GroupBy(row => row.Game.Serial, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var gameItems = new List<GameItemVm>();
            foreach (var recommendation in _catalog.Recommendations.OrderBy(rec => rec.WikiTitle).ThenBy(rec => rec.Serial))
            {
                if (detectedRows.TryGetValue(recommendation.Serial, out var row))
                {
                    gameItems.Add(new GameItemVm(
                        game: row.Game,
                        recommendation: row.Rec ?? recommendation,
                        recState: row.Plan.RecState,
                        serial: row.Game.Serial,
                        title: row.Rec?.WikiTitle ?? recommendation.WikiTitle,
                        isDetectedInRpcs3: true));

                    continue;
                }

                var catalogOnlyGame = new Rpcs3GameDescriptor(
                    Serial: recommendation.Serial,
                    Title: recommendation.WikiTitle,
                    InstallPath: string.Empty,
                    Version: null,
                    PpuHash: null,
                    SpuHash: null);

                var isDetectedByConfig = HasDetectedLocalMarker(result.Install, recommendation.Serial);
                var recState = isDetectedByConfig
                    ? _flow.BuildCfgPlan(result.Install, catalogOnlyGame, recommendation, _hwInfo).RecState
                    : BuildCatalogOnlySummary(recommendation);

                gameItems.Add(new GameItemVm(
                    game: catalogOnlyGame,
                    recommendation: recommendation,
                    recState: recState,
                    serial: recommendation.Serial,
                    title: recommendation.WikiTitle,
                    isDetectedInRpcs3: isDetectedByConfig));
            }

            foreach (var row in result.Games.Where(row => !gameItems.Any(item => string.Equals(item.Serial, row.Game.Serial, StringComparison.OrdinalIgnoreCase))))
            {
                gameItems.Add(new GameItemVm(
                    game: row.Game,
                    recommendation: row.Rec,
                    recState: row.Plan.RecState,
                    serial: row.Game.Serial,
                    title: row.Rec?.WikiTitle ?? row.Game.Title,
                    isDetectedInRpcs3: true));
            }

            GameList.SelectedGame = null;
            GameList.Load(gameItems);
            GameList.SelectedGame = GameList.Games.FirstOrDefault();

            BuildCurrentPatchPreview();

            Advanced.RefreshBackups(result.Backups);
            BackupsStatus = UiText.F("Status.BkpCount", Advanced.Backups.Count);

            Log(UiText.F("Msg.CatLoaded", _catalog.DataSource, _catalog.DataVersion, _catalog.Count));
            Log(UiText.F("Msg.GamesFound", GameList.Games.Count));
        }
        catch (Exception ex)
        {
            Rpcs3Status = File.Exists(Rpcs3ExePath)
                ? UiText.T("Status.Rpcs3Ready")
                : UiText.T("Status.Rpcs3Missing");

            UiDlg.Error(ex.Message, UiText.T("Dialog.FailAnalyze"));
            Log(UiText.F("Msg.AnalyzeFailed", ex.Message));
            PatchPreview.LoadError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private void ApplyChecked()
    {
        try
        {
            if (_install is null || _currentPlan is null)
            {
                UiDlg.Info(UiText.T("Msg.SelectGameFirst"), UiText.T("Dialog.NoGame"));
                return;
            }

            var selectedItems = Plan.Items
                .Where(item => item.IsSelected && item.IsDifferent && item.IsActionable)
                .ToArray();

            if (selectedItems.Length == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoCfgSelected"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var manualCount = selectedItems.Count(item => RecommendationRiskPolicy.RequiresExtraApplyWarning(item.Risk));
            var warning = manualCount > 0
                ? UiText.F("Msg.ManualWarning", manualCount)
                : string.Empty;

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.ApplyCfgConfirm", selectedItems.Length, _currentPlan.GameSerial, warning),
                UiText.T("Dialog.ApplyCfgTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelApply"));
                return;
            }

            var selectedChanges = selectedItems.Select(item => item.Change).ToArray();
            var result = _flow.ApplyCfg(_currentPlan, selectedChanges, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.CreatedAt.LocalDateTime));

            foreach (var item in selectedItems)
            {
                Log(UiText.F("Msg.CfgApplied", _currentPlan.GameSerial, item.DisplayPath, item.CurrentDisplay, item.ProposedValue));
            }

            Log(UiText.F("Msg.CfgWritten", _currentPlan.TargetConfigPath));

            RefreshBackups();
            BuildCurrentPlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailApply"));
            Log(UiText.F("Msg.ApplyFailed", ex.Message));
        }
    }

    private void RemoveWikiSettings()
    {
        try
        {
            if (_install is null || _currentPlan is null)
            {
                UiDlg.Info(UiText.T("Msg.SelectGameFirst"), UiText.T("Dialog.NoGame"));
                return;
            }

            var removableChanges = _currentPlan.Items
                .Where(item => item.CurrentValue is not null)
                .Select(item => item.Change)
                .ToArray();

            if (removableChanges.Length == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoWikiSettingsToRemove"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.RemoveWikiSettingsConfirm", removableChanges.Length, _currentPlan.GameSerial),
                UiText.T("Dialog.RemoveWikiSettingsTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelRemoveWikiSettings"));
                return;
            }

            var result = _flow.RemoveCfg(_currentPlan, removableChanges, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.CreatedAt.LocalDateTime));

            foreach (var change in removableChanges)
            {
                Log(UiText.F("Msg.WikiSettingRemoved", _currentPlan.GameSerial, change.DisplayPath));
            }

            RefreshBackups();
            BuildCurrentPlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRemoveWikiSettings"));
            Log(UiText.F("Msg.RemoveWikiSettingsFailed", ex.Message));
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void ApplyCheckedPatches(PatchSection? section)
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.T("Msg.AnalyzeRpcs3First"), UiText.T("Dialog.NoRpcs3"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var selectedMatches = section switch
            {
                PatchSection.Official => PatchPreview.SelectedOfficialMatches(),
                PatchSection.Canary => PatchPreview.SelectedCanaryMatches(),
                PatchSection.Cheat => PatchPreview.SelectedCheatMatches(),
                _ => PatchPreview.SelectedMatches()
            };
            if (selectedMatches.Count == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoSafePatch"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            var plan = _flow.BuildPatchPlan(_install, selectedMatches, PatchPreview.ShowExpertPatches);
            if (!plan.HasChanges)
            {
                UiDlg.Info(UiText.T("Msg.NoSafePatchApply"), UiText.T("Dialog.NothingToApply"));
                Log(UiText.T("Msg.SkipPatchApply"));
                return;
            }

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.ApplyPatchConfirm", plan.Changes.Count),
                UiText.T("Dialog.ApplyPatchTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelPatch"));
                return;
            }

            var bundledCanaryMatches = selectedMatches
                .Where(match => match.Patch.Source == PatchSourceOrigin.BundledCanary)
                .ToArray();
            if (bundledCanaryMatches.Length > 0)
            {
                var canaryResult = _flow.PrepareBundledCanaryPatch(_install, bundledCanaryMatches);
                if (!canaryResult.Succeeded)
                {
                    UiDlg.Error(canaryResult.Error ?? canaryResult.Message, UiText.T("Patch.PrepareCanary"));
                    Log(canaryResult.Error ?? canaryResult.Message);
                    return;
                }

                Log(canaryResult.Message);
            }

            var result = _flow.ApplyPatch(plan, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.CreatedAt.LocalDateTime));

            foreach (var change in result.AppliedChanges)
            {
                Log(UiText.F("Msg.PatchApplied", change.HashKey, change.PatchTitle, change.Serial, change.Version));
            }

            RefreshBackups();
            RefreshCanaryState();
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailPatchApply"));
            Log(UiText.F("Msg.PatchApplyFailed", ex.Message));
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void OffPatches(PatchSection? section)
    {
        if (_install is null)
        {
            return;
        }

        try
        {
            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRpcs3"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedApplyRunning"));
                return;
            }

            var selectedMatches = section switch
            {
                PatchSection.Official => PatchPreview.SelectedOfficialDisableMatches(),
                PatchSection.Canary => PatchPreview.SelectedCanaryDisableMatches(),
                PatchSection.Cheat => PatchPreview.SelectedCheatDisableMatches(),
                _ => PatchPreview.SelectedDisableMatches()
            };
            if (selectedMatches.Count == 0)
            {
                UiDlg.Info(UiText.T("Msg.NoPatchDisable"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            var plan = _flow.BuildPatchDisablePlan(_install, selectedMatches);
            if (!plan.HasChanges)
            {
                UiDlg.Info(UiText.T("Msg.NoPatchDisable"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.OffPatchConfirm", plan.Changes.Count),
                UiText.T("Dialog.OffPatchTitle"));

            if (!confirmed)
            {
                return;
            }

            var result = _flow.DisablePatch(plan, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.CreatedAt.LocalDateTime));

            foreach (var change in result.AppliedChanges)
            {
                Log(UiText.F("Msg.PatchOff", change.HashKey, change.PatchTitle, change.Serial, change.Version));
            }

            RefreshBackups();
            BuildCurrentPatchPreview();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailPatchApply"));
            Log(UiText.F("Msg.PatchApplyFailed", ex.Message));
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void RestoreBackup()
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.T("Msg.AnalyzeRpcs3First"), UiText.T("Dialog.NoRpcs3"));
                return;
            }

            var backup = Advanced.SelectedBackup;
            if (backup is null)
            {
                UiDlg.Info(UiText.T("Msg.NoBkp"), UiText.T("Dialog.NoBkp"));
                return;
            }

            if (Rpcs3InstallService.IsRpcs3Running())
            {
                UiDlg.Warn(UiText.T("Msg.BlockedRestoreRunning"), UiText.T("Dialog.Rpcs3Running"));
                Log(UiText.T("Msg.BlockedRestoreLog"));
                return;
            }

            var confirmed = UiDlg.Confirm(
                UiText.F("Msg.RestoreConfirm", backup.CreatedAt.LocalDateTime),
                UiText.T("Dialog.RestoreTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelRestore"));
                return;
            }

            _flow.Restore(backup);
            Log(UiText.F("Msg.Restored", backup.DirectoryPath));

            BuildCurrentPlan();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRestore"));
            Log(UiText.F("Msg.RestoreFailed", ex.Message));
        }
    }

    private void ImportWikiXml()
    {
        var fileName = UiDlg.PickWikiXml();
        if (fileName is null)
        {
            return;
        }

        Advanced.DeveloperWikiPath = fileName;
        RebuildCatalog();
    }

    private void RebuildCatalog()
    {
        try
        {
            if (!File.Exists(Advanced.DeveloperWikiPath))
            {
                UiDlg.Info(UiText.T("Msg.ImportWikiFirst"), UiText.T("Dialog.NoWiki"));
                return;
            }

            _catalog = _flow.RebuildCat(Advanced.DeveloperWikiPath);
            _catalogLoadError = null;

            UpdateCatalogStatus();
            Log(UiText.F("Msg.CatRebuilt", _catalog.Count));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailRebuildCat"));
            Log(UiText.F("Msg.CatRebuildFailed", ex.Message));
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private void ShowCatalogInfo()
    {
        if (_catalog is null)
        {
            UiDlg.Info(_catalogLoadError ?? CatalogMissingException.UserMessage, UiText.T("Dialog.CatInfo"));
            return;
        }

        UiDlg.Info(_flow.CatInfoText(_catalog.Info), UiText.T("Dialog.CatInfo"));
    }

    private void ExportCatalog()
    {
        try
        {
            if (_catalog is null)
            {
                UiDlg.Info(_catalogLoadError ?? CatalogMissingException.UserMessage, UiText.T("Dialog.NoCat"));
                return;
            }

            var fileName = UiDlg.PickCatExportPath();
            if (fileName is null)
            {
                return;
            }

            _flow.ExportCat(_catalog, fileName);
            Log(UiText.F("Msg.CatExported", fileName));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailExportCat"));
            Log(UiText.F("Msg.CatExportFailed", ex.Message));
        }
    }

    private void CopyLog()
    {
        if (!string.IsNullOrWhiteSpace(Advanced.LogText))
        {
            UiDlg.CopyText(Advanced.LogText);
        }
    }

    private void BuildCurrentPlan()
    {
        if (_install is null || GameList.SelectedGame is null)
        {
            _currentPlan = null;
            Plan.Clear();
            PatchPreview.Clear();
            Advanced.TargetConfigPath = UiText.T("Ui.EmptyValue");
            RefreshSelectedGameDisplay();
            RaiseCommandStates();
            return;
        }

        _currentPlan = _flow.BuildCfgPlan(
            _install,
            GameList.SelectedGame.Game,
            GameList.SelectedGame.Recommendation,
            _hwInfo);

        GameList.SelectedGame.RecState = _currentPlan.RecState;
        Plan.Load(_currentPlan);
        Advanced.TargetConfigPath = _currentPlan.TargetConfigPath;

        BuildCurrentPatchPreview();
        RefreshSelectedGameDisplay();
        RaiseCommandStates();
    }

    private void RefreshBackups()
    {
        if (_install is null)
        {
            Advanced.RefreshBackups([]);
            BackupsStatus = UiText.F("Status.BkpCount", 0);
            RaiseCommandStates();
            return;
        }

        var backups = _flow.ListBackups(_install);

        Advanced.RefreshBackups(backups);
        BackupsStatus = UiText.F("Status.BkpCount", Advanced.Backups.Count);

        RaiseCommandStates();
    }

    private void LoadPatchCatalog()
    {
        if (_install is null)
        {
            _patchCatalog = null;
            _patchCatalogError = null;
            HasPreparedCanaryPatches = false;
            PatchPreview.Clear();
            return;
        }

        RefreshCanaryState();

        var result = _flow.LoadPatchCat(_install);
        _patchCatalog = result.Cat;
        _patchCatalogError = result.Error;

        if (_patchCatalog is null)
        {
            PatchPreview.LoadError(_patchCatalogError ?? UiText.T("Patch.FileInvalid"));
            Log(UiText.F("Msg.PatchPreviewUnavailable", _patchCatalogError));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_patchCatalogError))
        {
            Log(UiText.F("Msg.PatchCatWarning", _patchCatalogError));
        }

        if (_patchCatalog.MergeDiagnostics.Count > 0)
        {
            Log(UiText.F("Msg.PatchMergeDiagnostics", _patchCatalog.MergeDiagnostics.Count));
        }

        Log(UiText.F("Msg.PatchCatLoaded", _patchCatalog.Entries.Count));
    }

    private void BuildCurrentPatchPreview()
    {
        if (_patchCatalog is null)
        {
            if (!string.IsNullOrWhiteSpace(_patchCatalogError))
            {
                PatchPreview.LoadError(_patchCatalogError);
            }
            else
            {
                PatchPreview.Clear();
            }

            return;
        }

        if (GameList.SelectedGame is null)
        {
            PatchPreview.Clear();
            PatchPreview.Summary = UiText.T("Patch.SelectGame");
            return;
        }

        var game = GameList.SelectedGame.Game;
        var matches = _flow.BuildPatchPreview(_patchCatalog, game);

        PatchPreview.Load(
            matches,
            PatchCatalogSourceDisplay,
            UiText.F("Patch.LoadTitle", game.Serial),
            CombinePatchWarnings(
                _patchCatalogError,
                BuildPatchConfigWarning(),
                Rpcs3InstallService.IsRpcs3Running() ? UiText.T("Patch.Rpcs3RunningWarning") : null,
                BuildPatchReadinessWarning(game, matches)),
            IsPatchEnabled);

        RefreshSelectedGameDisplay();
        Log(UiText.F("Msg.PatchPreviewLoaded", game.Serial, matches.Count));
    }

    private static string? BuildPatchReadinessWarning(
        Rpcs3GameDescriptor game,
        IReadOnlyList<Rpcs3PatchMatchResult> matches)
    {
        if (HasSelectedGameHashes(game))
        {
            return null;
        }

        return matches.Any(match => match.Status == PatchMatchStatus.SerialVersionMatchHashUnknown)
            ? UiText.T("PatchReadiness.NeedsColdBootWarning")
            : null;
    }

    private bool IsPatchEnabled(Rpcs3PatchMatchResult match)
    {
        if (_install is null)
        {
            return false;
        }

        try
        {
            var change = _flow.CreatePatchChange(match);
            return change is not null && new PatchConfigReader().IsEnabled(_install.PatchConfigPath, change);
        }
        catch
        {
            return false;
        }
    }

    private static string? CombinePatchWarnings(params string?[] warnings)
    {
        var clean = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return clean.Length == 0
            ? null
            : string.Join(Environment.NewLine, clean);
    }

    private string? BuildPatchConfigWarning()
    {
        return _install is not null && !_flow.CanReadPatchCfg(_install)
            ? UiText.T("Patch.ConfigInvalidWarning")
            : null;
    }

    private void LoadBuiltInCatalog()
    {
        try
        {
            var result = _flow.LoadBuiltInCat(AppContext.BaseDirectory);
            _catalog = result.Cat;
            _catalogLoadError = result.Error;

            if (_catalog is null)
            {
                UpdateCatalogStatus();
                Log(UiText.F("Msg.CatLoadFailed", _catalogLoadError));
                return;
            }

            UpdateCatalogStatus();
        }
        catch (CatalogMissingException ex)
        {
            _catalog = null;
            _catalogLoadError = ex.Message;
            UpdateCatalogStatus();
            Log(UiText.F("Msg.CatLoadFailed", ex.Message));
        }
        catch (CatalogInvalidException ex)
        {
            _catalog = null;
            _catalogLoadError = ex.Message;
            UpdateCatalogStatus();
            Log(UiText.F("Msg.CatLoadFailed", ex.Message));
        }
        catch (Exception ex)
        {
            _catalog = null;
            _catalogLoadError = CatalogInvalidException.UserMessage;
            UpdateCatalogStatus();
            Log(UiText.F("Msg.CatLoadFailed", ex.Message));
        }
    }

    private void UpdateCatalogStatus()
    {
        if (_catalog is null)
        {
            var message = _catalogLoadError ?? CatalogMissingException.UserMessage;
            DataStatus = UiText.F("Status.DataError", message);
            RecommendationsStatus = UiText.F("Status.RecCount", 0);
            Advanced.SetCatalogInfo(null, message);
            return;
        }

        var dataSource = string.Equals(_catalog.Info.DataSource, "Built" + "-in", StringComparison.Ordinal)
            ? UiText.T("Cat.BuiltIn")
            : _catalog.Info.DataSource;

        DataStatus = UiText.F("Status.DataSource", dataSource);
        RecommendationsStatus = UiText.F("Status.RecCount", _catalog.Info.RecommendationCount);
        Advanced.SetCatalogInfo(_catalog.Info, string.Empty);
    }

    private static bool HasDetectedLocalMarker(Rpcs3InstallDescriptor install, string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return false;
        }

        var customConfigPath = Path.Combine(install.CustomConfigsPath, $"config_{serial}.yml");
        if (File.Exists(customConfigPath))
        {
            return true;
        }

        var configBasePath = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var candidateGameFolders = new[]
        {
            configBasePath is null ? null : Path.Combine(configBasePath, "dev_hdd0", "game", serial),
            Path.Combine(install.RootPath, "dev_hdd0", "game", serial),
            Path.Combine(portablePath, "dev_hdd0", "game", serial)
        };

        return candidateGameFolders
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Any(path => Directory.Exists(path!));
    }

    private static RecStateSummary BuildCatalogOnlySummary(WikiGameRecommendation recommendation)
    {
        var items = recommendation.Changes
            .Select(change => new RecStateItem(
                Serial: recommendation.Serial,
                Path: change.DisplayPath,
                Current: null,
                Proposed: change.Value,
                Risk: change.Risk,
                State: RecState.Unknown,
                IsDifferent: true,
                IsActionable: false,
                IsAutoSelectable: false,
                Notes: change.Notes ?? string.Empty))
            .ToArray();

        return new RecStateSummary(
            Serial: recommendation.Serial,
            Total: items.Length,
            Applied: 0,
            Pending: 0,
            Manual: 0,
            Blocked: 0,
            Unknown: items.Length,
            Items: items);
    }

    private void RefreshSelectedGameDisplay()
    {
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGameTitleFlow));
        OnPropertyChanged(nameof(SelectedGameTitleAlignment));
        OnPropertyChanged(nameof(SelectedGameSerial));
        OnPropertyChanged(nameof(SelectedGamePatchReadinessText));
        OnPropertyChanged(nameof(SelectedGamePatchReadinessTooltip));
        OnPropertyChanged(nameof(SelectedGamePatchReadinessTone));
        OnPropertyChanged(nameof(HasSelectedGame));
        OnPropertyChanged(nameof(ShowGameAlert));
    }

    private void RefreshLanguageText()
    {
        _layout = UiLayoutService.Current;

        UiLayoutService.ApplyApplicationResources();

        OnPropertyChanged(nameof(WindowFlow));
        OnPropertyChanged(nameof(ShellFlow));
        OnPropertyChanged(nameof(TechnicalFlow));
        OnPropertyChanged(nameof(XmlLanguage));
        OnPropertyChanged(nameof(GamesPanelColumn));
        OnPropertyChanged(nameof(DetailsPanelColumn));
        OnPropertyChanged(nameof(GamesPanelWidth));
        OnPropertyChanged(nameof(DetailsPanelWidth));
        OnPropertyChanged(nameof(LeftShellColumnWidth));
        OnPropertyChanged(nameof(RightShellColumnWidth));
        OnPropertyChanged(nameof(TextFlow));
        OnPropertyChanged(nameof(IsRtlLanguage));
        OnPropertyChanged(nameof(UiTextAlignment));
        OnPropertyChanged(nameof(UiHorizontalAlignment));
        OnPropertyChanged(nameof(ToggleLanguageText));
        OnPropertyChanged(nameof(CurrentLanguageText));
        OnPropertyChanged(nameof(ToggleLanguageToolTip));

        RefreshSelectedGameDisplay();

        Rpcs3Status = File.Exists(Rpcs3ExePath)
            ? UiText.T("Status.Rpcs3Ready")
            : UiText.T("Status.Rpcs3Missing");

        HardwareStatus = FormatHardwareStatus();
        HardwareTooltip = FormatHardwareTooltip();
        BackupsStatus = UiText.F("Status.BkpCount", Advanced.Backups.Count);

        UpdateCatalogStatus();
        GameList.RefreshLanguage();
        Plan.RefreshLanguage();
        PatchPreview.RefreshLanguage();
        Advanced.RefreshLanguage();
        HCanary.RefreshLanguage();
    }

    private string FormatHardwareStatus()
    {
        if (_hwInfo is null)
        {
            return UiText.T("Status.HwUnknown");
        }

        return UiText.F("Status.HwLevel", UiText.T($"HwTier.{_hwInfo.Tier}"));
    }

    private string FormatHardwareTooltip()
    {
        if (_hwInfo is null)
        {
            return UiText.T("Status.HwNotScanned");
        }

        return UiText.F("Status.HwTip", _hwInfo.LogicalProcessorCount, _hwInfo.MemoryDisplay);
    }

    private static bool HasSelectedGameHashes(Rpcs3GameDescriptor game)
    {
        return game.AllPpuHashes.Count > 0 || game.AllSpuHashes.Count > 0;
    }

    private static string PatchHashSourceDisplay(Rpcs3GameDescriptor game)
    {
        var source = game.HashSource ?? string.Empty;
        if (source.Contains("RPCS3.log", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("rpcs3.log", StringComparison.OrdinalIgnoreCase))
        {
            return UiText.T("PatchReadiness.Source.Log");
        }

        if (source.Contains("cache", StringComparison.OrdinalIgnoreCase))
        {
            return UiText.T("PatchReadiness.Source.Cache");
        }

        return UiText.T("PatchReadiness.Source.Metadata");
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings(string.IsNullOrWhiteSpace(Rpcs3ExePath) ? null : Rpcs3ExePath));
    }

    private void RefreshCanaryState()
    {
        HasPreparedCanaryPatches = _install is not null &&
                                   _canaryPatchImportService.HasManagedImportedPatch(_install.PatchesPath);

        OnPropertyChanged(nameof(HasPreparedCanaryPatches));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        ApplyCheckedCommand.RaiseCanExecuteChanged();
        RemoveWikiSettingsCommand.RaiseCanExecuteChanged();
        ApplyCheckedPatchesCommand.RaiseCanExecuteChanged();
        OffPatchesCommand.RaiseCanExecuteChanged();
        ApplyOfficialPatchesCommand.RaiseCanExecuteChanged();
        DisableOfficialPatchesCommand.RaiseCanExecuteChanged();
        ApplyCanaryPatchesCommand.RaiseCanExecuteChanged();
        DisableCanaryPatchesCommand.RaiseCanExecuteChanged();
        ApplyCheatPatchesCommand.RaiseCanExecuteChanged();
        DisableCheatPatchesCommand.RaiseCanExecuteChanged();
        RemoveCanaryPatchesCommand.RaiseCanExecuteChanged();
        ImportCanaryPatchCommand.RaiseCanExecuteChanged();
        RestoreBackupCommand.RaiseCanExecuteChanged();
        SelectRecommendedCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
        CopyLogCommand.RaiseCanExecuteChanged();
        RebuildCatalogCommand.RaiseCanExecuteChanged();
        ShowCatalogInfoCommand.RaiseCanExecuteChanged();
        ExportCatalogCommand.RaiseCanExecuteChanged();
    }

    private void Log(string message)
    {
        Advanced.AppendLog(message);
        CopyLogCommand.RaiseCanExecuteChanged();
    }

    private bool CanApplyCheckedPatches(PatchSection? section)
    {
        var hasSelection = section switch
        {
            PatchSection.Official => PatchPreview.HasSelectedOfficialApplyablePatches,
            PatchSection.Canary => PatchPreview.HasSelectedCanaryApplyablePatches,
            PatchSection.Cheat => PatchPreview.HasSelectedCheatApplyablePatches,
            _ => PatchPreview.HasSelectedApplyablePatches
        };

        if (_install is null || !hasSelection || Rpcs3InstallService.IsRpcs3Running())
        {
            return false;
        }

        try
        {
            return _flow.CanReadPatchCfg(_install);
        }
        catch
        {
            return false;
        }
    }

    private bool CanDisableCheckedPatches(PatchSection? section)
    {
        var install = _install;
        var hasSelection = section switch
        {
            PatchSection.Official => PatchPreview.HasSelectedOfficialDisableablePatches,
            PatchSection.Canary => PatchPreview.HasSelectedCanaryDisableablePatches,
            PatchSection.Cheat => PatchPreview.HasSelectedCheatDisableablePatches,
            _ => PatchPreview.HasSelectedDisableablePatches
        };

        if (install is null || !hasSelection || Rpcs3InstallService.IsRpcs3Running())
        {
            return false;
        }

        try
        {
            return _flow.CanReadPatchCfg(install);
        }
        catch
        {
            return false;
        }
    }

    private bool CanAnalyze()
    {
        return !IsBusy && File.Exists(Rpcs3ExePath);
    }

    private enum PatchSection
    {
        Official,
        Canary,
        Cheat
    }
}
