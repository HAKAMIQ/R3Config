using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private GameSetupPlan? _setupPlan;
    private GameSetupPreview? _setupPreview;
    private string _setupError = string.Empty;
    private string _setupResult = string.Empty;

    public string SetupSource => _setupPlan is null
        ? UiText.GetText("Ui.EmptyValue")
        : string.IsNullOrWhiteSpace(_setupPlan.RecommendationProvenance.DataVer)
            ? _setupPlan.RecommendationProvenance.DataSrc
            : $"{_setupPlan.RecommendationProvenance.DataSrc} {_setupPlan.RecommendationProvenance.DataVer}";

    public string SetupBaseCount => StatusCount(
        "Setup.AppliedCount",
        "Setup.AppliedCountPending",
        "Setup.AppliedNone",
        "Setup.AppliedPendingOnly",
        _setupPlan?.BaseConfiguration.Items.Count ?? 0,
        _setupPlan?.BaseConfiguration.Items.Count(item =>
            item.State == RecState.Applied) ?? 0,
        _setupPreview?.SelectedBaseSettings.Count ?? 0);

    public string SetupOfficialCount => StatusCount(
        "Setup.ActivationCount",
        "Setup.ActivationCountPending",
        "Setup.ActivationNone",
        "Setup.ActivationPendingOnly",
        _setupPlan?.OfficialPatches.Count ?? 0,
        _setupPlan?.OfficialPatches.Count(candidate =>
            candidate.IsEnabled) ?? 0,
        _setupPreview?.SelectedPatches.Count(candidate =>
            (candidate.Provenance.Kind is SetupPatchSourceKind.OfficialMain or
                SetupPatchSourceKind.OfficialSpu) &&
            !candidate.IsEnabled) ?? 0);

    public string SetupCanaryCount
    {
        get
        {
            var available = _setupPlan?.CanaryPatches.Count ?? 0;
            var current = _setupPlan?.CanaryPatches.Count(candidate =>
                candidate.IsEnabled) ?? 0;
            var pending = _setupPreview?.SelectedPatches.Count(candidate =>
                candidate.Provenance.Kind == SetupPatchSourceKind.Canary &&
                !candidate.IsEnabled) ?? 0;

            if (_setupPlan is not null &&
                available > 0 &&
                current == 0 &&
                pending == 0 &&
                CountSetupWarnings("HASHMISMATCH") == available)
            {
                return UiText.Fmt(
                    "Setup.CanaryAllIncompatible",
                    available);
            }

            return StatusCount(
                "Setup.ActivationCount",
                "Setup.ActivationCountPending",
                "Setup.ActivationNone",
                "Setup.ActivationPendingOnly",
                available,
                current,
                pending);
        }
    }

    public string SetupArtemisCount => StatusCount(
        "Setup.ActivationCount",
        "Setup.ActivationCountPending",
        "Setup.ActivationNone",
        "Setup.ActivationPendingOnly",
        _setupPlan?.ArtemisPatches.Count ?? 0,
        _setupPlan?.ArtemisPatches.Count(candidate =>
            candidate.IsEnabled) ?? 0,
        _setupPreview?.SelectedPatches.Count(candidate =>
            candidate.Provenance.Kind == SetupPatchSourceKind.Artemis &&
            !candidate.IsEnabled) ?? 0);

    public string SetupRequirementCount => _setupPlan is null
        ? UiText.GetText("Ui.EmptyValue")
        : UiText.Fmt(
            "Setup.ItemCount",
            _setupPreview?.PatchRequirements.Count ?? 0);

    public string SetupFiles => _setupPreview?.Files.Count > 0
        ? string.Join(Environment.NewLine, _setupPreview.Files.Select(file => file.Path))
        : string.Empty;

    public string SetupWarnings => FormatSetupWarnings(_setupPreview?.Warnings);

    public string SetupConflicts => _setupPreview?.Conflicts.Count > 0
        ? string.Join(Environment.NewLine, _setupPreview.Conflicts)
        : string.Empty;

    public string SetupStatus => !string.IsNullOrWhiteSpace(_setupError)
        ? _setupError
        : !string.IsNullOrWhiteSpace(_setupResult)
            ? _setupResult
            : _setupPreview?.CanApply == true
                ? UiText.GetText("Setup.ReadyToPrepare")
                : UiText.GetText("Setup.SelectHint");

    public bool SetupHasRequirements => _setupPreview?.PatchRequirements.Count > 0;

    public bool SetupHasFiles => _setupPreview?.Files.Count > 0;

    public bool SetupHasWarnings => _setupPreview?.Warnings.Count > 0;

    public bool SetupHasConflicts => _setupPreview?.Conflicts.Count > 0;

    public bool SetupHasIssues => SetupHasWarnings || SetupHasConflicts;

    public bool SetupHasReview => SetupHasFiles || SetupHasIssues;

    public bool SetupCanApply => CanMut && _setupPreview?.CanApply == true;


    private string StatusCount(
        string currentKey,
        string pendingKey,
        string noneCurrentKey,
        string pendingOnlyKey,
        int available,
        int current,
        int pending)
    {
        if (_setupPlan is null)
        {
            return UiText.GetText("Ui.EmptyValue");
        }

        if (available == 0)
        {
            return UiText.GetText("Setup.None");
        }

        if (current == 0)
        {
            return pending > 0
                ? UiText.Fmt(pendingOnlyKey, available, pending)
                : UiText.Fmt(noneCurrentKey, available);
        }

        return pending > 0
            ? UiText.Fmt(
                pendingKey,
                current,
                available,
                pending)
            : UiText.Fmt(
                currentKey,
                current,
                available);
    }

    private int CountSetupWarnings(string token)
    {
        if (_setupPreview?.Warnings is null)
        {
            return 0;
        }

        return _setupPreview.Warnings.Count(warning =>
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return false;
            }

            var trimmed = warning.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            return trimmed[..separator].Trim().Equals(
                token,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string FormatSetupWarnings(IEnumerable<string>? warnings)
    {
        if (warnings is null)
        {
            return string.Empty;
        }

        var visible = new List<string>();
        var hashMismatchCount = 0;

        foreach (var warning in warnings)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                continue;
            }

            var trimmed = warning.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                visible.Add(PatText.SurfMsg(trimmed).Body);
                continue;
            }

            var token = trimmed[..separator].Trim();
            var detail = trimmed[(separator + 1)..].Trim();

            if (token.Equals("HASHMISMATCH", StringComparison.OrdinalIgnoreCase))
            {
                hashMismatchCount++;
                continue;
            }

            if (token.Equals("HASH_UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                visible.Add(UiText.GetText("Setup.Warning.HashUnknown"));
                continue;
            }

            if (token.Equals(
                    "PATCH_BOOT_RECOMMENDED",
                    StringComparison.OrdinalIgnoreCase))
            {
                visible.Add(UiText.GetText("Setup.Warning.PatchBoot"));
                continue;
            }

            if (token.Equals(
                    "PATCH_SAFETY_HIGH",
                    StringComparison.OrdinalIgnoreCase))
            {
                visible.Add(UiText.Fmt(
                    "Setup.Warning.PatchSafetyHigh",
                    detail));
                continue;
            }

            if (token.Equals(
                    "PATCH_SAFETY_CAUTION",
                    StringComparison.OrdinalIgnoreCase))
            {
                visible.Add(UiText.Fmt(
                    "Setup.Warning.PatchSafetyCaution",
                    detail));
                continue;
            }

            if (LooksLikeDiagnosticToken(token))
            {
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    visible.Add(detail);
                }
                continue;
            }

            visible.Add(PatText.SurfMsg(trimmed).Body);
        }

        if (hashMismatchCount > 0)
        {
            visible.Insert(
                0,
                UiText.Fmt(
                    "Setup.Warning.HashMismatchCount",
                    hashMismatchCount));
        }

        return string.Join(Environment.NewLine, visible.Distinct(StringComparer.Ordinal));
    }

    private static bool LooksLikeDiagnosticToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 48)
        {
            return false;
        }

        return value.All(character =>
            char.IsUpper(character) ||
            char.IsDigit(character) ||
            character == '_');
    }

    private void RefreshGameSetupPlan()
    {
        _setupPlan = null;
        _setupPreview = null;
        _setupError = string.Empty;
        _setupResult = string.Empty;

        if (_install is null ||
            GameList.SelGame is null ||
            _catalog is null ||
            _patCat is null)
        {
            RefreshGameSetupPresentation();
            return;
        }

        try
        {
            _setupPlan = _flow.BuildGameSetup(
                _install,
                GameList.SelGame.Game,
                _catalog,
                _patCat,
                _hwInfo);
            RefreshGameSetupPreview();
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException)
        {
            _setupError = ex.Message;
            RefreshGameSetupPresentation();
        }
    }

    private void RefreshGameSetupPreview()
    {
        _setupResult = string.Empty;
        if (_setupPlan is null)
        {
            RefreshGameSetupPresentation();
            return;
        }

        var settings = Plan.Items
            .Where(item => item.IsSel && item.IsDiff && item.IsAction)
            .Select(item => item.Change)
            .ToArray();
        var patchIdentities = Preview.SelOfcl()
            .Concat(Preview.SelCan())
            .Concat(Preview.SelCht())
            .Select(match => GameSetupPlanningService.PatchIdentity(match.Patch))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _setupPreview = _flow.PreviewGameSetup(
            _setupPlan,
            new GameSetupSelection(settings, patchIdentities));
        _setupError = string.Empty;
        RefreshGameSetupPresentation();
    }

    private void PrepareGame()
    {
        try
        {
            LoadR3Ui();
            RefreshGameSetupPlan();
            if (_install is null || _setupPreview?.CanApply != true)
            {
                UiDlg.Info(
                    SetupHasConflicts ? SetupConflicts : UiText.GetText("Setup.SelectHint"),
                    UiText.GetText("Setup.Title"));
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(
                    UiText.GetText("Msg.BlockedRpcs3"),
                    UiText.GetText("Dialog.Rpcs3Running"));
                return;
            }

            var confirmed = _confirm(
                UiText.Fmt(
                    "Setup.ApplyConfirm",
                    _setupPreview.EffectiveSettings.Count,
                    _setupPreview.SelectedPatches.Count,
                    _setupPreview.Files.Count),
                UiText.GetText("Setup.Title"));
            if (!confirmed)
            {
                return;
            }

            var result = _flow.ApplyGameSetup(_install, _setupPreview);
            var resultMessage = result.AlreadyPrepared
                ? UiText.GetText("Setup.AlreadyPrepared")
                : UiText.Fmt(
                    "Setup.GameReadySummary",
                    result.AppliedGameSettings,
                    result.EnabledOfficialPatches,
                    result.InstalledCanaryPatches,
                    result.InstalledArtemisPatches,
                    result.Warnings.Count);

            if (result.Backup is not null)
            {
                Log(UiText.Fmt(
                    "Msg.BkpSaved",
                    result.Backup.MadeAt.LocalDateTime));
            }

            Log(resultMessage);
            LoadBkp();
            LoadCan();
            LoadPat();
            MakePlan();
            _setupError = string.Empty;
            _setupResult = resultMessage;
            RefreshGameSetupPresentation();
            _info(resultMessage, UiText.GetText("Setup.GameReady"));
        }
        catch (Exception ex)
        {
            _setupError = ex.Message;
            UiDlg.Error(ex.Message, UiText.GetText("Setup.ApplyFailed"));
            Log(UiText.Fmt("Msg.ApplyFailed", ex.Message));
            RefreshGameSetupPresentation();
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void ClearGameSetup()
    {
        _setupPlan = null;
        _setupPreview = null;
        _setupError = string.Empty;
        _setupResult = string.Empty;
        RefreshGameSetupPresentation();
    }

    private void RefreshGameSetupPresentation()
    {
        OnChange(nameof(SetupSource));
        OnChange(nameof(SetupBaseCount));
        OnChange(nameof(SetupOfficialCount));
        OnChange(nameof(SetupCanaryCount));
        OnChange(nameof(SetupArtemisCount));
        OnChange(nameof(SetupRequirementCount));
        OnChange(nameof(SetupFiles));
        OnChange(nameof(SetupWarnings));
        OnChange(nameof(SetupConflicts));
        OnChange(nameof(SetupStatus));
        OnChange(nameof(SetupHasRequirements));
        OnChange(nameof(SetupHasFiles));
        OnChange(nameof(SetupHasWarnings));
        OnChange(nameof(SetupHasConflicts));
        OnChange(nameof(SetupHasIssues));
        OnChange(nameof(SetupHasReview));
        OnChange(nameof(SetupCanApply));
        PrepareGameCmd.RaiseCan();
    }
}
