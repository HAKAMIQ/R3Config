using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using R3Config;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.View;

namespace R3Config.Vm;

public sealed partial class MainVm : VmBase
{
    public void RefreshRpcs3PresentationState()
    {
        var isRunning = false;
        if (_install is not null)
        {
            try
            {
                isRunning = _isRpcs3Running();
            }
            catch
            {
                isRunning = true;
            }
        }

        if (SetProperty(
                ref _isRpcs3RunningPresentation,
                isRunning,
                nameof(IsRpcs3RunningPresentation)))
        {
            OnPropertyChanged(nameof(CanMutateCurrentInstall));
        }

        HCanary.UpdateMutationAvailability(
            IsBusy,
            isRunning);
        RaiseCommandStates();
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
        RefreshPatchSourceStatus();
        Advanced.RefreshLanguage();
        HCanary.RefreshLanguage();
        RefreshRpcs3PresentationState();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
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
        return game.HashEvidenceKind switch
        {
            PatchHashEvidenceKind.PlainLog or
            PatchHashEvidenceKind.CompressedLog =>
                UiText.T("PatchReadiness.Source.Log"),

            PatchHashEvidenceKind.GameCache =>
                UiText.T("PatchReadiness.Source.Cache"),

            PatchHashEvidenceKind.ExistingMetadata =>
                UiText.T("PatchReadiness.Source.Metadata"),

            _ => string.IsNullOrWhiteSpace(game.HashSource)
                ? UiText.T("Ui.Unknown")
                : UiText.T("PatchReadiness.Source.Metadata")
        };
    }

    public string OfficialPatchSourceStatusText
    {
        get
        {
            var source = FindPatchSource(PatchSourceOrigin.Official, "patch.yml");
            if (source?.Loaded == true)
            {
                return UiText.F(
                    HasSourceWarning(source)
                        ? "Patch.SourceStatus.OfficialLoadedWithWarnings"
                        : "Patch.SourceStatus.OfficialLoaded",
                    source.EntryCount);
            }

            return source?.Exists == true
                ? UiText.F(
                    "Patch.SourceStatus.OfficialInvalid",
                    source.Diagnostic ?? UiText.T("Patch.FileInvalid"))
                : UiText.T("Patch.SourceStatus.OfficialMissing");
        }
    }

    public string OfficialPatchSourcePath =>
        FindPatchSource(PatchSourceOrigin.Official, "patch.yml")?.Path
        ?? (_install is null
            ? string.Empty
            : Path.Combine(_install.PatchesPath, "patch.yml"));

    public string OfficialPatchSourceTone =>
        SourceTone(FindPatchSource(PatchSourceOrigin.Official, "patch.yml"));

    public string OfficialPatchSourceBadgeText =>
        SourceBadge(FindPatchSource(PatchSourceOrigin.Official, "patch.yml"));

    public string CanaryPatchSourceStatusText
    {
        get
        {
            var source = FindPatchSource(PatchSourceOrigin.BundledCanary, "can.yml");
            if (source?.Loaded == true)
            {
                return UiText.F(
                    HasSourceWarning(source)
                        ? "Patch.SourceStatus.CanaryLoadedWithWarnings"
                        : "Patch.SourceStatus.CanaryLoaded",
                    source.EntryCount);
            }

            return source?.Exists == true
                ? UiText.F(
                    "Patch.SourceStatus.CanaryInvalid",
                    source.Diagnostic ?? UiText.T("Patch.FileInvalid"))
                : UiText.T("Patch.SourceStatus.CanaryMissing");
        }
    }

    public string CanaryPatchSourcePath =>
        FindPatchSource(PatchSourceOrigin.BundledCanary, "can.yml")?.Path
        ?? BundledCanaryPatchService.GetBundledPath(AppContext.BaseDirectory);

    public string CanaryPatchSourceTone =>
        SourceTone(FindPatchSource(PatchSourceOrigin.BundledCanary, "can.yml"));

    public string CanaryPatchSourceBadgeText =>
        SourceBadge(FindPatchSource(PatchSourceOrigin.BundledCanary, "can.yml"));

    private PatSrcSt? FindPatchSource(
        PatchSourceOrigin source,
        string name)
    {
        return _patchCatalogSources.FirstOrDefault(item =>
            item.Source == source &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string SourceTone(PatSrcSt? source)
    {
        return source?.Loaded == true
            ? HasSourceWarning(source) ? "Warning" : "Auto"
            : source?.Exists == true
                ? "Danger"
                : "Warning";
    }

    private static string SourceBadge(PatSrcSt? source)
    {
        return source?.Loaded == true
            ? UiText.T(HasSourceWarning(source)
                ? "Patch.SourceStatus.PartialBadge"
                : "Patch.SourceStatus.LoadedBadge")
            : source?.Exists == true
                ? UiText.T("Patch.SourceStatus.InvalidBadge")
                : UiText.T("Patch.SourceStatus.MissingBadge");
    }

    private static bool HasSourceWarning(PatSrcSt? source)
    {
        return source?.Loaded == true &&
               !string.IsNullOrWhiteSpace(source.Diagnostic);
    }

    private void RefreshPatchSourceStatus()
    {
        OnPropertyChanged(nameof(OfficialPatchSourceStatusText));
        OnPropertyChanged(nameof(OfficialPatchSourcePath));
        OnPropertyChanged(nameof(OfficialPatchSourceTone));
        OnPropertyChanged(nameof(OfficialPatchSourceBadgeText));
        OnPropertyChanged(nameof(CanaryPatchSourceStatusText));
        OnPropertyChanged(nameof(CanaryPatchSourcePath));
        OnPropertyChanged(nameof(CanaryPatchSourceTone));
        OnPropertyChanged(nameof(CanaryPatchSourceBadgeText));
    }

    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings(string.IsNullOrWhiteSpace(Rpcs3ExePath) ? null : Rpcs3ExePath));
    }

    private void RefreshCanaryState()
    {
        HasPreparedCanaryPatches = _install is not null &&
                                   _flow.HasManagedImportedPatch(
                                       _install);

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
        OpenRpcs3PatchManagerCommand.RaiseCanExecuteChanged();
        OpenPatchesFolderCommand.RaiseCanExecuteChanged();
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
