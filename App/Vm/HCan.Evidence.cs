using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed partial class HCanaryVm : VmBase
{
    private void ReadLog()
    {
        if (_install is null) return;
        var result = _hashReader.Read(LogPath);
        Hashes.Clear();
        foreach (var hash in result.Hashes) Hashes.Add(new HCanaryHashVm(hash));
        OnPropertyChanged(nameof(HasHashes));
        LogStatus = !result.Succeeded
            ? UiText.F("HCanary.Log.Error", result.Error)
            : result.Hashes.Count == 0
                ? UiText.T("HCanary.Log.None")
                : UiText.F("HCanary.Log.Found", result.Hashes.Count, result.SourcePath);
    }

    private void LoadContextHashes()
    {
        Hashes.Clear();
        if (_contextGame is not null)
        {
            var source = _contextGame.HashSource ?? LogPath;
            foreach (var hash in _contextGame.AllPpuHashes) Hashes.Add(new HCanaryHashVm(new HCanaryHash(PatchHashKind.Ppu, hash, source, 0)));
            foreach (var hash in _contextGame.AllSpuHashes) Hashes.Add(new HCanaryHashVm(new HCanaryHash(PatchHashKind.Spu, hash, source, 0)));
        }
        OnPropertyChanged(nameof(HasHashes));
        LogStatus = Hashes.Count == 0 ? UiText.T("HCanary.Log.None") : UiText.F("HCanary.Log.Found", Hashes.Count, _contextGame?.HashSource ?? LogPath);
    }

    private void RefreshStoredPatches()
    {
        StoredPatches.Clear();
        if (_install is null || !File.Exists(ImportedPatchPath)) return;
        try { foreach (var patch in _flow.LoadHCanaryPatches(_install)) StoredPatches.Add(patch); }
        catch (Exception ex) { StatusMessage = UiText.F("HCanary.Error.ImportedInvalid", ex.Message); }
    }

    private void OpenStored()
    {
        if (SelectedStoredPatch is null) return;
        LoadDraft(SelectedStoredPatch.Draft);
        _currentIdentity = SelectedStoredPatch.Identity;
        _dirty = false;
        StatusMessage = UiText.T("HCanary.Message.Opened");
    }

    private void LoadDraft(HCanaryPatchDraft draft)
    {
        _suppressRefresh = true;
        PatchType = draft.PatchType == HCanaryPatchType.Spu ? "SPU" : "PPU";
        IsExperimental = draft.IsExperimental;
        ExecutableHash = draft.ExecutableHash; PatchName = draft.PatchName; Author = draft.Author; Notes = draft.Notes;
        Group = draft.Group; PatchVersion = draft.PatchVersion; Status = draft.Status;
        Games.Clear(); Instructions.Clear(); Anchors.Clear();
        foreach (var item in draft.Games) AddGame(HCanaryGameTargetVm.From(item));
        foreach (var item in draft.Instructions) AddInstruction(HCanaryInstructionVm.From(item));
        foreach (var item in draft.Anchors)
        {
            var anchor = HCanaryAnchorVm.From(item); Subscribe(anchor);
            foreach (var instruction in anchor.Instructions) Subscribe(instruction);
            anchor.Instructions.CollectionChanged += (_, _) => RefreshDraft(); Anchors.Add(anchor);
        }
        _suppressRefresh = false; RefreshDraft(markDirty: false);
    }
}
