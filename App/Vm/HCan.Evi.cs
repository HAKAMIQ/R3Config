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
    private void ReadLog()
    {
        if (_install is null) return;
        var result = _reader.Read(LogPath);
        Hashes.Clear();
        foreach (var hash in result.Hashes) Hashes.Add(new HashVm(hash));
        OnChange(nameof(HasHash));
        if (!result.IsOk)
        {
            SetLogStat("HCanary.Log.Error", result.Error);
        }
        else if (result.Hashes.Count == 0)
        {
            SetLogStat("HCanary.Log.None");
        }
        else
        {
            SetLogStat("HCanary.Log.Found", result.Hashes.Count, result.SrcPath);
        }
    }

    private void LoadHash()
    {
        Hashes.Clear();
        if (_game is not null)
        {
            var source = _game.HashSrc ?? LogPath;
            foreach (var hash in _game.Hashes) Hashes.Add(new HashVm(new HCanHash(HashKind.Ppu, hash, source, 0)));
            foreach (var hash in _game.SpuList) Hashes.Add(new HashVm(new HCanHash(HashKind.Spu, hash, source, 0)));
        }
        OnChange(nameof(HasHash));
        if (Hashes.Count == 0)
        {
            SetLogStat("HCanary.Log.None");
        }
        else
        {
            SetLogStat("HCanary.Log.Found", Hashes.Count, _game?.HashSrc ?? LogPath);
        }
    }

    private void LoadStor()
    {
        Patches.Clear();
        if (_install is null || !File.Exists(PatPath)) return;
        try { foreach (var patch in _flow.LoadHCan(_install)) Patches.Add(patch); }
        catch (Exception ex) { SetMsg("HCanary.Error.ImportedInvalid", ex.Message); }
    }

    private void OpenSave()
    {
        if (SelPat is null) return;
        GetDrft(SelPat.Draft);
        _curId = SelPat.Identity;
        _dirty = false;
        SetMsg("HCanary.Message.Opened");
    }

    private void GetDrft(HCanDrft draft)
    {
        _noRfsh = true;
        PatType = draft.PatType == HCanType.Spu ? "SPU" : "PPU";
        IsExp = draft.IsExp;
        ExeHash = draft.ExeHash; PatName = draft.PatName; Author = draft.Author; Notes = draft.Notes;
        Group = draft.Group; PatVer = draft.PatVer; Status = draft.Status;
        Games.Clear(); Ins.Clear(); Anchors.Clear();
        foreach (var item in draft.Games) AddGame(TgtVm.From(item));
        foreach (var item in draft.Ins) AddIns(InsVm.From(item));
        foreach (var item in draft.Anchors)
        {
            var anchor = AncVm.From(item); Sub(anchor);
            foreach (var instruction in anchor.Ins) Sub(instruction);
            anchor.Ins.CollectionChanged += (_, _) => LoadDrft(); Anchors.Add(anchor);
        }
        _noRfsh = false; LoadDrft(markDirty: false);
    }
}
