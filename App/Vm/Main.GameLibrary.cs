using System.IO;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private static readonly TimeSpan GameLibraryScanDelay =
        TimeSpan.FromMilliseconds(200);

    private void InitializeGameLibrary()
    {
        var settings = _setSvc.Load();
        if (string.IsNullOrWhiteSpace(settings.LastExe) ||
            !File.Exists(settings.LastExe))
        {
            return;
        }

        ExePath = settings.LastExe;
        R3Status = UiText.GetText("Status.Rpcs3Ready");
    }

    private void ScheduleGameLibraryScan(
        string exePath)
    {
        if (_disposed || !File.Exists(exePath))
        {
            return;
        }

        var operation = BeginGameLibraryScan();
        GameList.BeginLibraryScan();
        _ = ScanGameLibraryAsync(
            exePath,
            operation);
    }

    private async Task ScanGameLibraryAsync(
        string exePath,
        GameLibraryScanOperation operation)
    {
        try
        {
            await Task.Delay(
                GameLibraryScanDelay,
                operation.Token);

            var scan = await _flow.ScanGameLibraryAsync(
                exePath,
                operation.Token);

            if (!IsCurrentGameLibraryScan(
                    operation,
                    exePath))
            {
                return;
            }

            var gameItems = BuildGameLibraryRows(
                scan);

            GameList.SelGame = null;
            GameList.Load(gameItems);
            GameList.SelGame =
                GameList.Games.FirstOrDefault();

            Log(UiText.Fmt(
                "Msg.GamesFound",
                GameList.Games.Count));
            LogGame(scan.Diags);
        }
        catch (OperationCanceledException)
            when (operation.Token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsCurrentGameLibraryScan(
                    operation,
                    exePath))
            {
                return;
            }

            GameList.FailLibraryScan();
            Log(UiText.Fmt(
                "Msg.GameLibraryScanFailed",
                ex.Message));
        }
        finally
        {
            EndGameLibraryScan(operation);
        }
    }

    private GameVm[] BuildGameLibraryRows(
        GameScanResult scan)
    {
        var bySerial = scan.Games
            .GroupBy(
                game => game.Serial,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        return
        [
            .. bySerial.Values
                .Select(game =>
                {
                    var recommendation =
                        _catalog?.Find(game.Serial);

                    return new GameVm(
                        game,
                        recommendation,
                        EmptyLibrarySummary(game.Serial),
                        game.Serial,
                        GameVm.ResolveTitle(
                            game,
                            recommendation),
                        isDetectedInRpcs3: true);
                })
        ];
    }

    private static RecSum EmptyLibrarySummary(
        string serial)
    {
        return new RecSum(
            serial,
            0,
            0,
            0,
            0,
            0,
            0,
            []);
    }

    private GameLibraryScanOperation BeginGameLibraryScan()
    {
        lock (_operationSync)
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);
            _gameLibraryScanCancellation?.Cancel();
            var cancellation =
                new CancellationTokenSource();
            _gameLibraryScanCancellation = cancellation;

            return new GameLibraryScanOperation(
                ++_gameLibraryScanVersion,
                cancellation,
                cancellation.Token);
        }
    }

    private void CancelGameLibraryScan()
    {
        lock (_operationSync)
        {
            _gameLibraryScanVersion++;
            _gameLibraryScanCancellation?.Cancel();
            _gameLibraryScanCancellation = null;
        }
    }

    private bool IsCurrentGameLibraryScan(
        GameLibraryScanOperation operation,
        string exePath)
    {
        lock (_operationSync)
        {
            return !_disposed &&
                   !operation.Token.IsCancellationRequested &&
                   operation.Version == _gameLibraryScanVersion &&
                   ReferenceEquals(
                       _gameLibraryScanCancellation,
                       operation.Cancellation) &&
                   string.Equals(
                       ExePath,
                       exePath,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    private void EndGameLibraryScan(
        GameLibraryScanOperation operation)
    {
        lock (_operationSync)
        {
            if (operation.Version ==
                    _gameLibraryScanVersion &&
                ReferenceEquals(
                    _gameLibraryScanCancellation,
                    operation.Cancellation))
            {
                _gameLibraryScanCancellation = null;
            }
        }

        operation.Cancellation.Dispose();
    }

    private sealed record GameLibraryScanOperation(
        long Version,
        CancellationTokenSource Cancellation,
        CancellationToken Token);
}
