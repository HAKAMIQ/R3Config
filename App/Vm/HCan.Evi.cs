using System.IO;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed partial class HCanVm : VmBase
{
    private async Task ReadLogAsync()
    {
        var install = _install;
        var game = _game;
        if (install is null ||
            game is null)
        {
            return;
        }

        var operation = BeginRead();
        HashRead result;
        var applyResult = false;
        try
        {
            result = await Task.Run(() =>
                    _hashOps.ReadCancellable?.Invoke(
                        install,
                        game,
                        operation.Token) ??
                    _hashOps.Read(
                        install,
                        game),
                    operation.Token)
                .ConfigureAwait(true);
            applyResult =
                !operation.Token.IsCancellationRequested &&
                IsCurrentRead(
                    operation,
                    install,
                    game);
        }
        catch (OperationCanceledException)
            when (operation.Token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException or
            InvalidOperationException or
            NotSupportedException or
            System.Text.Json.JsonException)
        {
            if (IsCurrentRead(
                    operation,
                    install,
                    game))
            {
                SetLogStat(
                    "HCanary.Log.Error",
                    ex.Message);
            }
            return;
        }
        finally
        {
            EndRead(operation);
        }

        if (!applyResult)
        {
            return;
        }

        switch (result.Code)
        {
            case HashReadCode.Found
                when result.Hashes.Count > 0:
                ApplyHashRead(result, isOld: false);
                if (result.IsSaved)
                {
                    ClearLogStat();
                }
                else
                {
                    SetLogStat(
                        "HCanary.Log.Found",
                        PpuHashCount,
                        SpuHashCount);
                }
                if (result.IsSaved)
                {
                    HashChg?.Invoke(this, EventArgs.Empty);
                }
                break;

            case HashReadCode.SelectedGameRunning:
                SetLogStat(
                    "HCanary.Log.SelectedGameRunning");
                break;

            case HashReadCode.Rpcs3Running:
                SetLogStat(
                    "HCanary.Log.Rpcs3Running");
                break;

            case HashReadCode.LogNotChanged:
                SetLogStat(
                    "HCanary.Log.LogNotChanged");
                break;

            case HashReadCode.EmptyLog:
                SetLogStat(
                    "HCanary.Log.EmptyLog");
                break;

            case HashReadCode.WrongGame:
                SetLogStat(
                    "HCanary.Log.WrongGame",
                    result.Serial ?? "—");
                break;

            case HashReadCode.WrongVersion:
                SetLogStat(
                    "HCanary.Log.WrongVersion",
                    result.Version ?? "—",
                    game.Version ?? "—");
                break;

            case HashReadCode.SelectedGameNotFound:
                SetLogStat(
                    "HCanary.Log.SelectedGameNotFound");
                break;

            case HashReadCode.NoExecutableHashes:
                SetLogStat(
                    "HCanary.Log.NoExecutableHashes");
                break;

            case HashReadCode.CanaryHashMismatch:
                SetLogStat(
                    "PatchReadiness.Capture.CatalogMismatch");
                break;

            case HashReadCode.NoLog:
                SetLogStat(
                    "HCanary.Log.NoFile");
                break;

            case HashReadCode.NoMatch:
                SetLogStat(
                    "HCanary.Log.None");
                break;

            default:
                SetLogStat(
                    "HCanary.Log.Error",
                    result.Error ?? string.Empty);
                break;
        }
    }

    private ReadOperation BeginRead()
    {
        ReadOperation operation;
        lock (_readSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _readCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _readCancellation = cancellation;
            operation = new ReadOperation(
                ++_readVersion,
                cancellation,
                cancellation.Token);
        }

        return operation;
    }

    private void CancelRead()
    {
        lock (_readSync)
        {
            _readVersion++;
            _readCancellation?.Cancel();
            _readCancellation = null;
        }
    }

    private void EndRead(ReadOperation operation)
    {
        lock (_readSync)
        {
            if (operation.Version == _readVersion &&
                ReferenceEquals(
                    _readCancellation,
                    operation.Cancellation))
            {
                _readCancellation = null;
            }
        }

        operation.Cancellation.Dispose();
    }

    private bool IsCurrentRead(
        ReadOperation operation,
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        lock (_readSync)
        {
            return !_disposed &&
                   operation.Version == _readVersion &&
                   ReferenceEquals(
                       _readCancellation,
                       operation.Cancellation) &&
                   ReferenceEquals(_install, install) &&
                   ReferenceEquals(_game, game);
        }
    }

    private sealed record ReadOperation(
        long Version,
        CancellationTokenSource Cancellation,
        CancellationToken Token);

    private void ReloadSavedFingerprint()
    {
        if (_install is null ||
            _game is null ||
            !HasSavedBinding)
        {
            return;
        }

        LoadHash();
        if (HasSavedBinding)
        {
            SetLogStat("HCanary.Hash.Reused");
        }
    }

    private void LoadHash()
    {
        Hashes.Clear();
        SelHash = null;
        HasSavedBinding = false;
        _hashOld = false;
        OnChange(nameof(HashHead));

        if (_install is not null &&
            _game is not null)
        {
            var result = _hashOps.LoadSaved(
                _install,
                _game);
            if (result.Code == HashReadCode.Found &&
                result.Hashes.Count > 0)
            {
                ApplyHashRead(result, isOld: true);
                ClearLogStat();
                return;
            }
        }

        NotifyHashSummary();
        SetLogStat("HCanary.Log.None");
    }

    private void ApplyHashRead(HashRead result, bool isOld)
    {
        Hashes.Clear();
        SelHash = null;

        foreach (var hash in result.Hashes)
        {
            Hashes.Add(new HashVm(hash));
        }

        HasSavedBinding = result.IsSaved;
        _hashOld = isOld;
        OnChange(nameof(HashHead));

        if (Hashes.Count == 1 &&
            !_dirty &&
            _curId is null &&
            string.IsNullOrWhiteSpace(ExeHash))
        {
            SelHash = Hashes[0];
        }

        NotifyHashSummary();
    }

    private void NotifyHashSummary()
    {
        OnChange(nameof(HasHash));
        OnChange(nameof(PpuHashCount));
        OnChange(nameof(SpuHashCount));
    }

    private void LoadStor()
    {
        Patches.Clear();
        if (_install is not null && File.Exists(PatPath))
        {
            try
            {
                foreach (var patch in _flow.LoadHCan(_install))
                {
                    Patches.Add(patch);
                }
            }
            catch (Exception ex)
            {
                SetMsg(
                    "HCanary.Error.ImportedInvalid",
                    ex.Message);
            }
        }

        OnChange(nameof(IsStoredEmpty));
    }

    private void OpenSave()
    {
        if (SelPat is null)
        {
            return;
        }

        GetDrft(SelPat.Draft);
        _curId = SelPat.Identity;
        OnChange(nameof(HasStoredPatch));
        OnChange(nameof(SaveHint));
        _dirty = false;
        SetMsg("HCanary.Message.Opened");
    }

    private void GetDrft(HCanDrft draft)
    {
        _noRfsh = true;
        PatType = draft.PatType == HCanType.Spu
            ? "SPU"
            : "PPU";
        IsExp = draft.IsExp;
        ExeHash = draft.ExeHash;
        PatName = draft.PatName;
        Author = draft.Author;
        Notes = draft.Notes;
        Group = draft.Group;
        PatVer = draft.PatVer;
        Status = draft.Status;
        Games.Clear();
        Ins.Clear();
        Anchors.Clear();
        foreach (var item in draft.Games)
        {
            AddGame(TgtVm.From(item));
        }

        foreach (var item in draft.Ins)
        {
            AddIns(InsVm.From(item));
        }

        foreach (var item in draft.Anchors)
        {
            var anchor = AncVm.From(item);
            Sub(anchor);
            foreach (var instruction in anchor.Ins)
            {
                Sub(instruction);
            }

            anchor.Ins.CollectionChanged += (_, _) => LoadDrft();
            Anchors.Add(anchor);
        }

        _noRfsh = false;
        LoadDrft(markDirty: false);
    }
}
