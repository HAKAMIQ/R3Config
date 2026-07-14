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
    private void NewDraft()
    {
        _suppressRefresh = true;
        PatchType = "PPU";
        IsExperimental = false;
        ExecutableHash = string.Empty;
        PatchName = string.Empty;
        Author = string.Empty;
        Notes = string.Empty;
        Group = string.Empty;
        PatchVersion = "1.0";
        Status = DraftSt.Draft;
        Games.Clear();
        Instructions.Clear();
        Anchors.Clear();
        if (_contextGame is not null)
        {
            AddGame(HCanaryGameTargetVm.From(new HCanTgt
            {
                GameName = _contextGame.Title,
                GameId = _contextGame.Serial,
                GameVer = PatVer.Norm(_contextGame.Version) ?? string.Empty
            }));
        }
        else AddGame(new HCanaryGameTargetVm());
        AddInstruction(new HCanaryInstructionVm());
        _currentIdentity = null;
        SelectedStoredPatch = null;
        _dirty = false;
        _suppressRefresh = false;
        StatusMessage = UiText.T("HCanary.Message.NewDraft");
        RefreshDraft(markDirty: false);
    }

    private void AddCurrentGame()
    {
        if (_contextGame is null) return;
        var version = PatVer.Norm(_contextGame.Version) ?? string.Empty;
        if (Games.Any(item => item.GameId.Equals(_contextGame.Serial, StringComparison.OrdinalIgnoreCase) && item.GameVersion == version)) return;
        AddGame(HCanaryGameTargetVm.From(new HCanTgt
        {
            GameName = _contextGame.Title, GameId = _contextGame.Serial, GameVer = version
        }));
    }

    private void AddGame(HCanaryGameTargetVm item) { Subscribe(item); Games.Add(item); SelectedGameTarget = item; }
    private void AddInstruction(HCanaryInstructionVm item) { Subscribe(item); Instructions.Add(item); SelectedInstruction = item; }

    private void DeleteGame()
    {
        if (SelectedGameTarget is null || Games.Count <= 1) return;
        Games.Remove(SelectedGameTarget); SelectedGameTarget = Games.FirstOrDefault();
    }

    private void DeleteInstruction()
    {
        if (SelectedInstruction is null) return;
        var index = Instructions.IndexOf(SelectedInstruction);
        Instructions.Remove(SelectedInstruction);
        SelectedInstruction = Instructions.ElementAtOrDefault(Math.Min(index, Instructions.Count - 1));
    }

    private void DuplicateInstruction()
    {
        if (SelectedInstruction is null) return;
        var index = Instructions.IndexOf(SelectedInstruction) + 1;
        var copy = SelectedInstruction.Clone(); Subscribe(copy); Instructions.Insert(index, copy); SelectedInstruction = copy;
    }

    private bool CanMoveInstruction(int offset) => SelectedInstruction is not null &&
        Instructions.IndexOf(SelectedInstruction) + offset >= 0 && Instructions.IndexOf(SelectedInstruction) + offset < Instructions.Count;

    private void MoveInstruction(int offset)
    {
        if (!CanMoveInstruction(offset) || SelectedInstruction is null) return;
        Instructions.Move(Instructions.IndexOf(SelectedInstruction), Instructions.IndexOf(SelectedInstruction) + offset);
    }

    private void AddAnchor()
    {
        var index = Anchors.Count + 1;
        var anchor = new HCanaryAnchorVm { Name = $"Anchor{index}" };
        var instruction = new HCanaryInstructionVm(); Subscribe(instruction); anchor.Instructions.Add(instruction);
        Subscribe(anchor); anchor.Instructions.CollectionChanged += (_, _) => RefreshDraft();
        Anchors.Add(anchor); SelectedAnchor = anchor;
    }

    private void DeleteAnchor() { if (SelectedAnchor is not null) Anchors.Remove(SelectedAnchor); SelectedAnchor = Anchors.FirstOrDefault(); }

    private void AddAnchorInstruction()
    {
        if (SelectedAnchor is null) return;
        var item = new HCanaryInstructionVm(); Subscribe(item); SelectedAnchor.Instructions.Add(item); SelectedAnchorInstruction = item; RefreshDraft();
    }

    private void DeleteAnchorInstruction()
    {
        if (SelectedAnchor is null || SelectedAnchorInstruction is null) return;
        SelectedAnchor.Instructions.Remove(SelectedAnchorInstruction); SelectedAnchorInstruction = SelectedAnchor.Instructions.FirstOrDefault(); RefreshDraft();
    }

    private void AddAnchorLoad()
    {
        if (SelectedAnchor is null) return;
        AddInstruction(new HCanaryInstructionVm { Operation = "load", Address = SelectedAnchor.Name, Value = string.Empty });
    }

    private void Subscribe(VmBase item) => item.PropertyChanged += ItemChanged;
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != "Error") RefreshDraft(); }
    private void DraftCollectionChanged() { if (!_suppressRefresh) RefreshDraft(); }

    private HCanDrft BuildDraft() => new()
    {
        PatType = PatchType == "SPU" ? HCanType.Spu : HCanType.Ppu,
        IsExp = IsExperimental,
        ExeHash = ExecutableHash,
        PatName = PatchName,
        Author = Author,
        Notes = Notes,
        Group = Group,
        PatVer = PatchVersion,
        Status = Status,
        Games = Games.Select(item => item.ToModel()).ToList(),
        Ins = Instructions.Select(item => item.ToModel()).ToList(),
        Anchors = Anchors.Select(item => item.ToModel()).ToList()
    };

    private void RefreshDraft(bool markDirty = true)
    {
        if (_suppressRefresh) return;
        if (markDirty) { _dirty = true; OnPropertyChanged(nameof(IsDirty)); if (Status == DraftSt.Saved) Status = DraftSt.Draft; }
        var result = _authoring.Validate(BuildDraft());
        YamlPreview = string.IsNullOrEmpty(result.Yaml) ? SafePreview() : result.Yaml;
        IsValid = result.IsValid;
        HashError = FieldError(result, "ExecutableHash");
        PatchNameError = FieldError(result, "PatchName");
        PatchVersionError = FieldError(result, "PatchVersion");
        foreach (var item in Games) item.Error = FieldPrefixError(result, $"Games[{Games.IndexOf(item)}]");
        foreach (var item in Instructions) item.Error = FieldPrefixError(result, $"Instructions[{Instructions.IndexOf(item)}]");
        foreach (var item in Anchors) item.Error = FieldPrefixError(result, $"Anchors[{Anchors.IndexOf(item)}]");
        ValidationSummary = result.IsValid
            ? UiText.T("HCanary.Validation.Valid")
            : UiText.F("HCanary.Validation.Summary", result.Issues.Count) + Environment.NewLine +
              string.Join(Environment.NewLine, result.Issues.Select(issue => "• " + LocalizeIssue(issue)));
        CopyYamlCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(DraftStateText));
    }

    private string SafePreview()
    {
        try { return _authoring.Generate(BuildDraft()); }
        catch { return "Version: 1.2\r\n"; }
    }

    private static string FieldError(VldRes result, string field) =>
        result.Issues.FirstOrDefault(issue => issue.Field == field) is { } issue ? LocalizeIssue(issue) : string.Empty;

    private static string FieldPrefixError(VldRes result, string prefix) =>
        result.Issues.FirstOrDefault(issue => issue.Field.StartsWith(prefix, StringComparison.Ordinal)) is { } issue ? LocalizeIssue(issue) : string.Empty;

    private static string LocalizeIssue(VldIssue issue)
    {
        var key = "HCanary.Validation." + issue.Code;
        var text = UiText.T(key);
        return text == key ? issue.Message : text;
    }
}
