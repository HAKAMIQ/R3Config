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
    public void LoadR3Ui()
    {
        var isRunning = false;
        if (_install is not null)
        {
            try
            {
                isRunning = _isRun();
            }
            catch
            {
                isRunning = true;
            }
        }

        if (Set(
                ref _runUi,
                isRunning,
                nameof(RunUi)))
        {
            OnChange(nameof(CanMut));
        }

        HCanary.SetAvail(
            IsBusy,
            isRunning);
        RaiseCmd();
    }

    private void LoadCat()
    {
        try
        {
            var result = _flow.LoadCat(AppContext.BaseDirectory);
            _catalog = result.Cat;
            _loadErr = result.Error;

            if (_catalog is null)
            {
                SetCat();
                Log(UiText.Fmt("Msg.CatLoadFailed", _loadErr));
                return;
            }

            SetCat();
        }
        catch (MissEx ex)
        {
            _catalog = null;
            _loadErr = ex.Message;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
        catch (CatBadEx ex)
        {
            _catalog = null;
            _loadErr = ex.Message;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
        catch (Exception ex)
        {
            _catalog = null;
            _loadErr = CatBadEx.UserMsg;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
    }

    private void SetCat()
    {
        if (_catalog is null)
        {
            var message = _loadErr ?? MissEx.UserMsg;
            Status = UiText.Fmt("Status.DataError", message);
            RecStat = UiText.Fmt("Status.RecCount", 0);
            Advanced.SetCat(null, message);
            return;
        }

        var dataSource = string.Equals(_catalog.Info.DataSrc, "Built" + "-in", StringComparison.Ordinal)
            ? UiText.GetText("Cat.BuiltIn")
            : _catalog.Info.DataSrc;

        Status = UiText.Fmt("Status.DataSource", dataSource);
        RecStat = UiText.Fmt("Status.RecCount", _catalog.Info.RecNum);
        Advanced.SetCat(_catalog.Info, string.Empty);
    }

    private void LoadGame()
    {
        OnChange(nameof(SelGame));
        OnChange(nameof(GameFlow));
        OnChange(nameof(GameAlgn));
        OnChange(nameof(GameSer));
        OnChange(nameof(PatReady));
        OnChange(nameof(ReadyTip));
        OnChange(nameof(ReadyTon));
        OnChange(nameof(HasGame));
        OnChange(nameof(ShowWarn));
    }

    private void LoadLang()
    {
        _layout = UiLaySvc.Current;

        UiLaySvc.Apply();

        OnChange(nameof(WinFlow));
        OnChange(nameof(ShFlow));
        OnChange(nameof(TechFlow));
        OnChange(nameof(XmlLang));
        OnChange(nameof(GameCol));
        OnChange(nameof(DetCol));
        OnChange(nameof(GameWide));
        OnChange(nameof(DetWide));
        OnChange(nameof(LeftWide));
        OnChange(nameof(RghtWide));
        OnChange(nameof(TextFlow));
        OnChange(nameof(RtlLang));
        OnChange(nameof(UiAlign));
        OnChange(nameof(UiHoriz));
        OnChange(nameof(LangText));
        OnChange(nameof(CurLang));
        OnChange(nameof(ToolTip));

        LoadGame();

        R3Status = File.Exists(ExePath)
            ? UiText.GetText("Status.Rpcs3Ready")
            : UiText.GetText("Status.Rpcs3Missing");

        HwStatus = FmtHw();
        HwTip = FmtHwTip();
        BkpStat = UiText.Fmt("Status.BkpCount", Advanced.Backups.Count);

        SetCat();
        GameList.SetLang();
        Plan.SetLang();
        Preview.SetLang();
        LoadSrc();
        Advanced.SetLang();
        HCanary.SetLang();
        LoadR3Ui();
        LangChg?.Invoke(this, EventArgs.Empty);
    }

    private string FmtHw()
    {
        if (_hwInfo is null)
        {
            return UiText.GetText("Status.HwUnknown");
        }

        return UiText.Fmt("Status.HwLevel", UiText.GetText($"HwTier.{_hwInfo.Tier}"));
    }

    private string FmtHwTip()
    {
        if (_hwInfo is null)
        {
            return UiText.GetText("Status.HwNotScanned");
        }

        return UiText.Fmt("Status.HwTip", _hwInfo.CpuNum, _hwInfo.Display);
    }

    private static bool HasHash(Rpcs3GameDescriptor game)
    {
        return game.Hashes.Count > 0 || game.SpuList.Count > 0;
    }

    private static string PatHash(Rpcs3GameDescriptor game)
    {
        return game.HashEvi switch
        {
            HashEvi.PlainLog or
            HashEvi.ZipLog =>
                UiText.GetText("PatchReadiness.Source.Log"),

            HashEvi.Cache =>
                UiText.GetText("PatchReadiness.Source.Cache"),

            HashEvi.Meta =>
                UiText.GetText("PatchReadiness.Source.Metadata"),

            _ => string.IsNullOrWhiteSpace(game.HashSrc)
                ? UiText.GetText("Ui.Unknown")
                : UiText.GetText("PatchReadiness.Source.Metadata")
        };
    }

    public string PatSrc
    {
        get
        {
            var source = FindPat(Mdl.PatSrc.Official, "patch.yml");
            if (source?.Loaded == true)
            {
                return UiText.Fmt(
                    HasSrc(source)
                        ? "Patch.SourceStatus.OfficialLoadedWithWarnings"
                        : "Patch.SourceStatus.OfficialLoaded",
                    source.EntNum);
            }

            return source?.Exists == true
                ? UiText.Fmt(
                    "Patch.SourceStatus.OfficialInvalid",
                    source.Diag ?? UiText.GetText("Patch.FileInvalid"))
                : UiText.GetText("Patch.SourceStatus.OfficialMissing");
        }
    }

    public string SrcPath =>
        FindPat(Mdl.PatSrc.Official, "patch.yml")?.Path
        ?? (_install is null
            ? string.Empty
            : Path.Combine(_install.PatPath, "patch.yml"));

    public string SrcTone =>
        GetTone(FindPat(Mdl.PatSrc.Official, "patch.yml"));

    public string SrcBadge =>
        GetBadge(FindPat(Mdl.PatSrc.Official, "patch.yml"));

    public string CanState
    {
        get
        {
            var source = FindPat(Mdl.PatSrc.Can, "can.yml");
            if (source?.Loaded == true)
            {
                return UiText.Fmt(
                    HasSrc(source)
                        ? "Patch.SourceStatus.CanaryLoadedWithWarnings"
                        : "Patch.SourceStatus.CanaryLoaded",
                    source.EntNum);
            }

            return source?.Exists == true
                ? UiText.Fmt(
                    "Patch.SourceStatus.CanaryInvalid",
                    source.Diag ?? UiText.GetText("Patch.FileInvalid"))
                : UiText.GetText("Patch.SourceStatus.CanaryMissing");
        }
    }

    public string CanPath =>
        FindPat(Mdl.PatSrc.Can, "can.yml")?.Path
        ?? BundledCanaryPatchService.GetPath(AppContext.BaseDirectory);

    public string CanTone =>
        GetTone(FindPat(Mdl.PatSrc.Can, "can.yml"));

    public string CanBadge =>
        GetBadge(FindPat(Mdl.PatSrc.Can, "can.yml"));

    private PatchCatalogSourceStatus? FindPat(
        PatSrc source,
        string name)
    {
        return _catSrc.FirstOrDefault(item =>
            item.Source == source &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTone(PatchCatalogSourceStatus? source)
    {
        return source?.Loaded == true
            ? HasSrc(source) ? "Warning" : "Auto"
            : source?.Exists == true
                ? "Danger"
                : "Warning";
    }

    private static string GetBadge(PatchCatalogSourceStatus? source)
    {
        return source?.Loaded == true
            ? UiText.GetText(HasSrc(source)
                ? "Patch.SourceStatus.PartialBadge"
                : "Patch.SourceStatus.LoadedBadge")
            : source?.Exists == true
                ? UiText.GetText("Patch.SourceStatus.InvalidBadge")
                : UiText.GetText("Patch.SourceStatus.MissingBadge");
    }

    private static bool HasSrc(PatchCatalogSourceStatus? source)
    {
        return source?.Loaded == true &&
               !string.IsNullOrWhiteSpace(source.Diag);
    }

    private void LoadSrc()
    {
        OnChange(nameof(PatSrc));
        OnChange(nameof(SrcPath));
        OnChange(nameof(SrcTone));
        OnChange(nameof(SrcBadge));
        OnChange(nameof(CanState));
        OnChange(nameof(CanPath));
        OnChange(nameof(CanTone));
        OnChange(nameof(CanBadge));
    }

    private void SaveSet()
    {
        _setSvc.Save(new AppSet(string.IsNullOrWhiteSpace(ExePath) ? null : ExePath));
    }

    private void LoadCan()
    {
        PrepCan = _install is not null &&
                                   _flow.HasImp(
                                       _install);

        OnChange(nameof(PrepCan));
        RaiseCmd();
    }

    private void RaiseCmd()
    {
        ApplyCmd.RaiseCan();
        DelSet.RaiseCan();
        PatCmd.RaiseCan();
        PatOff.RaiseCan();
        OfclOn.RaiseCan();
        OfclOff.RaiseCan();
        CanOn.RaiseCan();
        CanOCmd.RaiseCan();
        ChtOn.RaiseCan();
        ChtOff.RaiseCan();
        CanDel.RaiseCan();
        CanImp.RaiseCan();
        MgrCmd.RaiseCan();
        PatDir.RaiseCan();
        RstCmd.RaiseCan();
        SelCmd.RaiseCan();
        ClearCmd.RaiseCan();
        CopyCmd.RaiseCan();
        BuildCmd.RaiseCan();
        InfoCmd.RaiseCan();
        ExpCmd.RaiseCan();
    }

    private void Log(string message)
    {
        Advanced.Append(message);
        CopyCmd.RaiseCan();
    }

    private bool CanAnlz()
    {
        return !IsBusy && File.Exists(ExePath);
    }

    private enum PatSect
    {
        Official,
        Canary,
        Cheat
    }
}
