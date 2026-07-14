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
    private void ApplyPat(PatSect? section)
    {
        try
        {
            if (_install is null)
            {
                UiDlg.Info(UiText.GetText("Msg.AnalyzeRpcs3First"), UiText.GetText("Dialog.NoRpcs3"));
                return;
            }

            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRpcs3"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedApplyRunning"));
                return;
            }

            var selectedMatches = section switch
            {
                PatSect.Official => Preview.SelOfcl(),
                PatSect.Canary => Preview.SelCan(),
                PatSect.Cheat => Preview.SelCht(),
                _ => Preview.Sel()
            };
            if (selectedMatches.Count == 0)
            {
                UiDlg.Info(UiText.GetText("Msg.NoSafePatch"), UiText.GetText("Dialog.NothingToApply"));
                return;
            }

            var plan = _flow.PatPlan(_install, selectedMatches, Preview.ShowExp);
            if (!plan.HasChg)
            {
                UiDlg.Info(UiText.GetText("Msg.NoSafePatchApply"), UiText.GetText("Dialog.NothingToApply"));
                Log(UiText.GetText("Msg.SkipPatchApply"));
                return;
            }

            var confirmed = _confirm(
                UiText.Fmt("Msg.ApplyPatchConfirm", plan.Changes.Count),
                UiText.GetText("Dialog.ApplyPatchTitle"));

            if (!confirmed)
            {
                Log(UiText.GetText("Msg.UserCancelPatch"));
                return;
            }

            var result = _flow.ApplyTxn(
                plan,
                _install,
                selectedMatches);

            foreach (var message in result.Messages)
            {
                Log(message);
            }

            Log(UiText.Fmt("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var change in result.ChgList)
            {
                Log(UiText.Fmt("Msg.PatchApplied", change.HashKey, change.PatTitle, change.Serial, change.Version));
            }

            LoadBkp();
            LoadCan();
            LoadPat();
            MakePrev();
            UiDlg.Info(
                UiText.Fmt(
                    OkKey(section),
                    result.ChgList.Count),
                UiText.GetText("Dialog.ApplyPatchTitle"));
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailPatchApply"));
            Log(UiText.Fmt("Msg.PatchApplyFailed", ex.Message));
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void OffPat(PatSect? section)
    {
        if (_install is null)
        {
            return;
        }

        try
        {
            LoadR3Ui();
            if (IsBusy)
            {
                return;
            }

            if (RunUi)
            {
                UiDlg.Warn(UiText.GetText("Msg.BlockedRpcs3"), UiText.GetText("Dialog.Rpcs3Running"));
                Log(UiText.GetText("Msg.BlockedApplyRunning"));
                return;
            }

            var selectedMatches = section switch
            {
                PatSect.Official => Preview.SelOfOff(),
                PatSect.Canary => Preview.SelCanOf(),
                PatSect.Cheat => Preview.SelChtOf(),
                _ => Preview.SelOff()
            };
            if (selectedMatches.Count == 0)
            {
                UiDlg.Info(UiText.GetText("Msg.NoPatchDisable"), UiText.GetText("Dialog.NothingToApply"));
                return;
            }

            var plan = _flow.OffPlan(_install, selectedMatches);
            if (!plan.HasChg)
            {
                UiDlg.Info(UiText.GetText("Msg.NoPatchDisable"), UiText.GetText("Dialog.NothingToApply"));
                return;
            }

            var confirmed = _confirm(
                UiText.Fmt("Msg.OffPatchConfirm", plan.Changes.Count),
                UiText.GetText("Dialog.OffPatchTitle"));

            if (!confirmed)
            {
                return;
            }

            var result = _flow.OffPat(plan, _install);

            Log(UiText.Fmt("Msg.BkpSaved", result.Backup.MadeAt.LocalDateTime));

            foreach (var change in result.ChgList)
            {
                Log(UiText.Fmt("Msg.PatchOff", change.HashKey, change.PatTitle, change.Serial, change.Version));
            }

            LoadBkp();
            MakePrev();
        }
        catch (Exception ex)
        {
            UiDlg.Error(ex.Message, UiText.GetText("Dialog.FailPatchApply"));
            Log(UiText.Fmt("Msg.PatchApplyFailed", ex.Message));
        }
        finally
        {
            LoadR3Ui();
        }
    }

    private void LoadPat()
    {
        if (_install is null)
        {
            _patCat = null;
            _catErr = null;
            _catSrc = [];
            PrepCan = false;
            Preview.Clear();
            LoadSrc();
            return;
        }

        LoadCan();

        var result = _flow.LoadPat(_install);
        _patCat = result.Cat;
        _catErr = result.Error;
        _catSrc = result.SrcStat;
        LoadSrc();

        if (_patCat is null)
        {
            Preview.LoadErr(_catErr ?? UiText.GetText("Patch.FileInvalid"));
            Log(UiText.Fmt("Msg.PatchPreviewUnavailable", _catErr));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_catErr))
        {
            Log(UiText.Fmt("Msg.PatchCatWarning", _catErr));
        }

        if (_patCat.Diag.Count > 0)
        {
            Log(UiText.Fmt("Msg.PatchMergeDiagnostics", _patCat.Diag.Count));
        }

        Log(UiText.Fmt("Msg.PatchCatLoaded", _patCat.Entries.Count));
    }

    private void MakePrev()
    {
        _isPatCf = false;

        if (_patCat is null)
        {
            if (!string.IsNullOrWhiteSpace(_catErr))
            {
                Preview.LoadErr(_catErr);
            }
            else
            {
                Preview.Clear();
            }

            return;
        }

        if (GameList.SelGame is null)
        {
            Preview.Clear();
            Preview.Summary = UiText.GetText("Patch.SelectGame");
            return;
        }

        var game = GameList.SelGame.Game;
        var matches = _flow.PatPrev(_patCat, game);
        var patchConfig = ReadCfg(out var patchConfigWarning);

        Preview.Load(
            matches,
            SrcDsp,
            UiText.Fmt("Patch.LoadTitle", game.Serial),
            Combine(
                patchConfigWarning,
                RunUi ? UiText.GetText("Patch.Rpcs3RunningWarning") : null,
                PatWarn(game, matches)),
            match => IsPatOn(patchConfig, match));

        LoadGame();
        Log(UiText.Fmt("Msg.PatchPreviewLoaded", game.Serial, matches.Count));
    }

    private static string? PatWarn(
        R3Game game,
        IReadOnlyList<PatMatch> matches)
    {
        if (HasHash(game) ||
            matches.Any(PatAct.Can))
        {
            return null;
        }

        return matches.Any(match =>
                match.Status == MatStat.Unknown &&
                !PatAct.Can(match))
            ? UiText.GetText("PatchReadiness.NeedsColdBootWarning")
            : null;
    }

    private bool IsPatOn(
        CfgSnap? patchConfig,
        PatMatch match)
    {
        if (patchConfig is null)
        {
            return false;
        }

        var change = _flow.PatChg(match);
        return change is not null && patchConfig.IsOn(change);
    }

    private static string? Combine(params string?[] warnings)
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

    private CfgSnap? ReadCfg(
        out string? warning)
    {
        warning = null;

        if (_install is null)
        {
            return null;
        }

        try
        {
            var snapshot = _flow.ReadCfg(_install);
            _isPatCf = true;
            return snapshot;
        }
        catch
        {
            warning = UiText.GetText("Patch.ConfigInvalidWarning");
            return null;
        }
    }

    private bool CanApply(PatSect? section)
    {
        var hasSelection = section switch
        {
            PatSect.Official => Preview.OfclOn,
            PatSect.Canary => Preview.SelCanOn,
            PatSect.Cheat => Preview.SelChtOn,
            _ => Preview.HasSelOn
        };

        if (!CanMut || !hasSelection)
        {
            return false;
        }

        return _isPatCf;
    }

    private bool CanPatOf(PatSect? section)
    {
        var install = _install;
        var hasSelection = section switch
        {
            PatSect.Official => Preview.OfclOff,
            PatSect.Canary => Preview.CanOff,
            PatSect.Cheat => Preview.ChtOff,
            _ => Preview.HasOff
        };

        if (install is null ||
            !CanMut ||
            !hasSelection)
        {
            return false;
        }

        return _isPatCf;
    }

    private static string OkKey(PatSect? section)
    {
        return section switch
        {
            PatSect.Official => "Msg.PatchApplyOfficialSuccess",
            PatSect.Canary => "Msg.PatchApplyCanarySuccess",
            PatSect.Cheat => "Msg.PatchApplyCheatSuccess",
            _ => "Msg.PatchApplySuccess"
        };
    }
}
