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
    private void ApplyCheckedPatches(PatchSection? section)
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.T("Msg.AnalyzeRpcs3First"), UiText.T("Dialog.NoRpcs3"));
                return;
            }

            RefreshRpcs3PresentationState();
            if (IsBusy)
            {
                return;
            }

            if (IsRpcs3RunningPresentation)
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
            if (!plan.HasChg)
            {
                UiDlg.Info(UiText.T("Msg.NoSafePatchApply"), UiText.T("Dialog.NothingToApply"));
                Log(UiText.T("Msg.SkipPatchApply"));
                return;
            }

            var confirmed = _confirm(
                UiText.F("Msg.ApplyPatchConfirm", plan.Changes.Count),
                UiText.T("Dialog.ApplyPatchTitle"));

            if (!confirmed)
            {
                Log(UiText.T("Msg.UserCancelPatch"));
                return;
            }

            var result = _flow.ApplyPatchTransaction(
                plan,
                _install,
                selectedMatches);

            foreach (var message in result.Messages)
            {
                Log(message);
            }

            Log(UiText.F("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var change in result.ChgList)
            {
                Log(UiText.F("Msg.PatchApplied", change.HashKey, change.PatTitle, change.Serial, change.Version));
            }

            RefreshBackups();
            RefreshCanaryState();
            LoadPatchCatalog();
            BuildCurrentPatchPreview();
            UiDlg.Info(
                UiText.F(
                    PatchApplySuccessKey(section),
                    result.ChgList.Count),
                UiText.T("Dialog.ApplyPatchTitle"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.T("Dialog.FailPatchApply"));
            Log(UiText.F("Msg.PatchApplyFailed", ex.Message));
        }
        finally
        {
            RefreshRpcs3PresentationState();
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
            RefreshRpcs3PresentationState();
            if (IsBusy)
            {
                return;
            }

            if (IsRpcs3RunningPresentation)
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
            if (!plan.HasChg)
            {
                UiDlg.Info(UiText.T("Msg.NoPatchDisable"), UiText.T("Dialog.NothingToApply"));
                return;
            }

            var confirmed = _confirm(
                UiText.F("Msg.OffPatchConfirm", plan.Changes.Count),
                UiText.T("Dialog.OffPatchTitle"));

            if (!confirmed)
            {
                return;
            }

            var result = _flow.DisablePatch(plan, _install);

            Log(UiText.F("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var change in result.ChgList)
            {
                Log(UiText.F("Msg.PatchOff", change.HashKey, change.PatTitle, change.Serial, change.Version));
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
            RefreshRpcs3PresentationState();
        }
    }

    private void LoadPatchCatalog()
    {
        if (_install is null)
        {
            _patchCatalog = null;
            _patchCatalogError = null;
            _patchCatalogSources = [];
            HasPreparedCanaryPatches = false;
            PatchPreview.Clear();
            RefreshPatchSourceStatus();
            return;
        }

        RefreshCanaryState();

        var result = _flow.LoadPatchCat(_install);
        _patchCatalog = result.Cat;
        _patchCatalogError = result.Error;
        _patchCatalogSources = result.SrcStat;
        RefreshPatchSourceStatus();

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

        if (_patchCatalog.Diag.Count > 0)
        {
            Log(UiText.F("Msg.PatchMergeDiagnostics", _patchCatalog.Diag.Count));
        }

        Log(UiText.F("Msg.PatchCatLoaded", _patchCatalog.Entries.Count));
    }

    private void BuildCurrentPatchPreview()
    {
        _isPatchConfigReadable = false;

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
        var patchConfig = ReadPatchConfig(out var patchConfigWarning);

        PatchPreview.Load(
            matches,
            PatchCatalogSourceDisplay,
            UiText.F("Patch.LoadTitle", game.Serial),
            CombinePatchWarnings(
                patchConfigWarning,
                IsRpcs3RunningPresentation ? UiText.T("Patch.Rpcs3RunningWarning") : null,
                BuildPatchReadinessWarning(game, matches)),
            match => IsPatchEnabled(patchConfig, match));

        RefreshSelectedGameDisplay();
        Log(UiText.F("Msg.PatchPreviewLoaded", game.Serial, matches.Count));
    }

    private static string? BuildPatchReadinessWarning(
        R3Game game,
        IReadOnlyList<PatMatch> matches)
    {
        if (HasSelectedGameHashes(game) ||
            matches.Any(PatAct.Can))
        {
            return null;
        }

        return matches.Any(match =>
                match.Status == MatStat.Unknown &&
                !PatAct.Can(match))
            ? UiText.T("PatchReadiness.NeedsColdBootWarning")
            : null;
    }

    private bool IsPatchEnabled(
        CfgSnap? patchConfig,
        PatMatch match)
    {
        if (patchConfig is null)
        {
            return false;
        }

        var change = _flow.CreatePatchChange(match);
        return change is not null && patchConfig.IsOn(change);
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

    private CfgSnap? ReadPatchConfig(
        out string? warning)
    {
        warning = null;

        if (_install is null)
        {
            return null;
        }

        try
        {
            var snapshot = _flow.ReadPatchCfg(_install);
            _isPatchConfigReadable = true;
            return snapshot;
        }
        catch
        {
            warning = UiText.T("Patch.ConfigInvalidWarning");
            return null;
        }
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

        if (!CanMutateCurrentInstall || !hasSelection)
        {
            return false;
        }

        return _isPatchConfigReadable;
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

        if (install is null ||
            !CanMutateCurrentInstall ||
            !hasSelection)
        {
            return false;
        }

        return _isPatchConfigReadable;
    }

    private static string PatchApplySuccessKey(PatchSection? section)
    {
        return section switch
        {
            PatchSection.Official => "Msg.PatchApplyOfficialSuccess",
            PatchSection.Canary => "Msg.PatchApplyCanarySuccess",
            PatchSection.Cheat => "Msg.PatchApplyCheatSuccess",
            _ => "Msg.PatchApplySuccess"
        };
    }
}
