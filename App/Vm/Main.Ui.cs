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

        SetRunUi(isRunning);
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
        catch (MissingCatalogException ex)
        {
            _catalog = null;
            _loadErr = ex.Message;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
        catch (InvalidCatalogException ex)
        {
            _catalog = null;
            _loadErr = ex.Message;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
        catch (Exception ex)
        {
            _catalog = null;
            _loadErr = InvalidCatalogException.UserMsg;
            SetCat();
            Log(UiText.Fmt("Msg.CatLoadFailed", ex.Message));
        }
    }

    private void SetCat()
    {
        if (_catalog is null)
        {
            var message = _loadErr ?? MissingCatalogException.UserMsg;
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
        OnChange(nameof(GameSer));
        OnChange(nameof(PatReady));
        OnChange(nameof(ReadyTip));
        OnChange(nameof(ReadyTon));
        OnChange(nameof(HasGame));
        OnChange(nameof(ShowWarn));
        OnHashLaunchStateChanged();
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
        OnChange(nameof(ToolTip));
        OnChange(nameof(BusyText));

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
        RefreshGameSetupPresentation();
        LoadSrc();
        Advanced.SetLang();
        HCanary.SetLang();
        LoadR3Ui();
        LangChg?.Invoke(this, EventArgs.Empty);
    }

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
    }

    private void HandleExePathChanged(string value)
    {
        CancelAnalysis();
        CancelGameLibraryScan();
        CancelFingerprintCapture();
        InvalidateAnalyzedContext();
        R3Status = File.Exists(value)
            ? UiText.GetText("Status.Rpcs3Ready")
            : UiText.GetText("Status.Rpcs3Missing");

        if (File.Exists(value))
        {
            ScheduleGameLibraryScan(value);
        }

        AnlzCmd.RaiseCan();
    }

    private void InvalidateAnalyzedContext()
    {
        _install = null;
        _hwInfo = null;
        _patCat = null;
        _catErr = null;
        _catSrc = [];
        _curPlan = null;
        _isPatCf = false;
        PrepCan = false;

        GameList.Clear();
        MakePlan();
        LoadPat();
        LoadBkp();
        HCanary.SetCtx(
            install: null,
            game: null);

        Advanced.CfgPath = "-";
        Advanced.CustCfg = "-";
        Advanced.PatPath = "-";
        Advanced.TgtCfg = UiText.GetText("Ui.EmptyValue");
        HwStatus = FmtHw();
        HwTip = FmtHwTip();
        SetRunUi(isRunning: false);
        LoadSrc();
        LoadGame();
        RaiseCmd();
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
        OfclOn.RaiseCan();
        OfclOff.RaiseCan();
        CanOn.RaiseCan();
        CanOCmd.RaiseCan();
        ChtOn.RaiseCan();
        ChtOff.RaiseCan();
        ChtSelectAll.RaiseCan();
        ChtClearSelection.RaiseCan();
        ChtRemove.RaiseCan();
        CanDel.RaiseCan();
        CanImp.RaiseCan();
        OfclMgr.RaiseCan();
        PatDir.RaiseCan();
        RstCmd.RaiseCan();
        DelBkpCmd.RaiseCan();
        CleanBkpCmd.RaiseCan();
        SelCmd.RaiseCan();
        ClearCmd.RaiseCan();
        CopyCmd.RaiseCan();
        BuildCmd.RaiseCan();
        InfoCmd.RaiseCan();
        ExpCmd.RaiseCan();
        LaunchGameCmd.RaiseCan();
        HashRefreshCmd.RaiseCan();
        FingerprintCaptureCmd.RaiseCan();
        PrepareGameCmd.RaiseCan();
    }

    private void Log(string message)
    {
        Advanced.Append(message);
        CopyCmd.RaiseCan();
    }

    private bool CanAnlz()
    {
        return !_disposed &&
               !IsBusy &&
               File.Exists(ExePath);
    }

    private enum PatSect
    {
        Official,
        Canary,
        Cheat
    }
}
