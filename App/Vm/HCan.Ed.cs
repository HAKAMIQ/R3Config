using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class HCanVm : VmBase
{
    private void NewDraft()
    {
        _noRfsh = true;
        PatType = "PPU";
        IsExp = false;
        ExeHash = string.Empty;
        PatName = string.Empty;
        Author = string.Empty;
        Notes = string.Empty;
        Group = string.Empty;
        PatVer = "1.0";
        Status = DraftSt.Draft;
        Games.Clear();
        Ins.Clear();
        Anchors.Clear();
        if (_game is not null)
        {
            AddGame(TgtVm.From(new HCanTgt
            {
                GameName = _game.Title,
                GameId = _game.Serial,
                GameVer = Svc.PatVer.Norm(_game.Version) ?? string.Empty
            }));
        }
        else AddGame(new TgtVm());
        AddIns(new InsVm());
        _curId = null;
        OnChange(nameof(HasStoredPatch));
        OnChange(nameof(SaveHint));
        SelPat = null;
        _dirty = false;
        _noRfsh = false;
        SetMsg("HCanary.Message.NewDraft");
        LoadDrft(markDirty: false);
    }

    private void AddCur()
    {
        if (_game is null) return;
        var version = Svc.PatVer.Norm(_game.Version) ?? string.Empty;
        if (Games.Any(item => item.GameId.Equals(_game.Serial, StringComparison.OrdinalIgnoreCase) && item.GameVer == version)) return;
        AddGame(TgtVm.From(new HCanTgt
        {
            GameName = _game.Title, GameId = _game.Serial, GameVer = version
        }));
    }

    private void AddGame(TgtVm item) { Sub(item); Games.Add(item); GameTgt = item; }
    private void AddIns(InsVm item) { Sub(item); Ins.Add(item); SelIns = item; }

    private void DelGame()
    {
        if (GameTgt is null || Games.Count <= 1) return;
        Games.Remove(GameTgt); GameTgt = Games.FirstOrDefault();
    }

    private void DelIns()
    {
        if (SelIns is null) return;
        var index = Ins.IndexOf(SelIns);
        Ins.Remove(SelIns);
        SelIns = Ins.ElementAtOrDefault(Math.Min(index, Ins.Count - 1));
    }

    private void DupIns()
    {
        if (SelIns is null) return;
        var index = Ins.IndexOf(SelIns) + 1;
        var copy = SelIns.Clone(); Sub(copy); Ins.Insert(index, copy); SelIns = copy;
    }

    private bool CanMove(int offset) => SelIns is not null &&
        Ins.IndexOf(SelIns) + offset >= 0 && Ins.IndexOf(SelIns) + offset < Ins.Count;

    private void MoveIns(int offset)
    {
        if (!CanMove(offset) || SelIns is null) return;
        Ins.Move(Ins.IndexOf(SelIns), Ins.IndexOf(SelIns) + offset);
    }

    private void AddAnc()
    {
        var index = Anchors.Count + 1;
        var anchor = new AncVm { Name = $"Anchor{index}" };
        var instruction = new InsVm(); Sub(instruction); anchor.Ins.Add(instruction);
        Sub(anchor); anchor.Ins.CollectionChanged += (_, _) => LoadDrft();
        Anchors.Add(anchor); SelAnc = anchor;
    }

    private void DelAnc() { if (SelAnc is not null) Anchors.Remove(SelAnc); SelAnc = Anchors.FirstOrDefault(); }

    private void AddAncI()
    {
        if (SelAnc is null) return;
        var item = new InsVm(); Sub(item); SelAnc.Ins.Add(item); AncIns = item; LoadDrft();
    }

    private void DelAncI()
    {
        if (SelAnc is null || AncIns is null) return;
        SelAnc.Ins.Remove(AncIns); AncIns = SelAnc.Ins.FirstOrDefault(); LoadDrft();
    }

    private void LoadAnc()
    {
        if (SelAnc is null) return;
        AddIns(new InsVm { Op = "load", Address = SelAnc.Name, Value = string.Empty });
    }

    private void Sub(VmBase item) => item.PropChg += OnItem;
    private void OnItem(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != "Error") LoadDrft(); }
    private void OnDraft() { if (!_noRfsh) LoadDrft(); }

    private HCanDrft Build() => new()
    {
        PatType = PatType == "SPU" ? HCanType.Spu : HCanType.Ppu,
        IsExp = IsExp,
        ExeHash = ExeHash,
        PatName = PatName,
        Author = Author,
        Notes = Notes,
        Group = Group,
        PatVer = PatVer,
        Status = Status,
        Games = Games.Select(item => item.ToModel()).ToList(),
        Ins = Ins.Select(item => item.ToModel()).ToList(),
        Anchors = Anchors.Select(item => item.ToModel()).ToList()
    };

    private void LoadDrft(bool markDirty = true)
    {
        if (_noRfsh) return;
        if (markDirty) { _dirty = true; OnChange(nameof(IsDirty)); if (Status == DraftSt.Saved) Status = DraftSt.Draft; }
        var result = _auth.Validate(Build());
        _lastValidation = result;
        Preview = string.IsNullOrEmpty(result.Yaml) ? TryPrev() : result.Yaml;
        IsValid = result.IsValid;
        RefreshValidationText();
        CopyCmd.RaiseCan();
        OnChange(nameof(DraftTxt));
    }

    private void RefreshValidationText()
    {
        if (_lastValidation is not { } result)
        {
            return;
        }

        HashErr = FieldErr(result, "ExecutableHash");
        NameErr = FieldErr(result, "PatchName");
        VerErr = FieldErr(result, "PatchVersion");
        foreach (var item in Games) item.Error = PreErr(result, $"Games[{Games.IndexOf(item)}]");
        foreach (var item in Ins) item.Error = PreErr(result, $"Instructions[{Ins.IndexOf(item)}]");
        foreach (var item in Anchors) item.Error = PreErr(result, $"Anchors[{Anchors.IndexOf(item)}]");
        VldSum = result.IsValid
            ? UiText.GetText("HCanary.Validation.Valid")
            : UiText.Fmt("HCanary.Validation.Summary", result.Issues.Count) + Environment.NewLine +
              string.Join(Environment.NewLine, result.Issues.Select(issue => "• " + FmtIssue(issue)));
        OnChange(nameof(CanAdvanceTarget));
        OnChange(nameof(CanAdvanceDetails));
        OnChange(nameof(CanAdvanceInstructions));
    }

    private string TryPrev()
    {
        try { return _auth.Generate(Build()); }
        catch { return "Version: 1.2\r\n"; }
    }

    private static string FieldErr(VldRes result, string field) =>
        result.Issues.FirstOrDefault(issue => issue.Field == field) is { } issue ? FmtIssue(issue) : string.Empty;

    private static string PreErr(VldRes result, string prefix) =>
        result.Issues.FirstOrDefault(issue => issue.Field.StartsWith(prefix, StringComparison.Ordinal)) is { } issue ? FmtIssue(issue) : string.Empty;

    private static string FmtIssue(VldIssue issue)
    {
        var key = "HCanary.Validation." + issue.Code;
        var text = UiText.GetText(key);
        return text == key ? issue.Message : text;
    }
}
