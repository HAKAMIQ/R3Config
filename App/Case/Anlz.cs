using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class AnlzCase(
    GmScan scanner,
    PlanSvc planService,
    HwProbe hardwareProbe,
    HashPrb patchHashProbe,
    PatCat patchCatalog,
    BkpCase backups)
{
    public async Task<AnlzRes> RunAsync(
        string rpcs3ExePath,
        Svc.RecSvc catalog)
    {
        var install = InstSvc.FromExe(rpcs3ExePath);

        var hardwareTask = Task.Run(hardwareProbe.Probe);
        var scanTask = Task.Run(() =>
            scanner.ScanInfo(install));

        await Task.WhenAll(
                hardwareTask,
                scanTask)
            .ConfigureAwait(false);

        var hardware = await hardwareTask.ConfigureAwait(false);
        var scanResult = await scanTask.ConfigureAwait(false);
        var games = await Task.Run(() =>
                patchHashProbe.Enrich(
                    install,
                    scanResult.Games))
            .ConfigureAwait(false);

        var rowsTask = Task.Run(() => BuildRow(
            install,
            catalog,
            hardware,
            games,
            scanResult.CfgSer));
        var patchCatalogTask = Task.Run(() =>
            patchCatalog.Load(install));
        var backupsTask = Task.Run(() =>
            backups.List(install));

        await Task.WhenAll(rowsTask, patchCatalogTask, backupsTask)
            .ConfigureAwait(false);

        return new AnlzRes(
            install,
            hardware,
            await rowsTask.ConfigureAwait(false),
            await patchCatalogTask.ConfigureAwait(false),
            await backupsTask.ConfigureAwait(false),
            scanResult.Diags);
    }

    private IReadOnlyList<GameRow> BuildRow(
        R3Inst install,
        Svc.RecSvc catalog,
        HwInfo hardware,
        IReadOnlyList<R3Game> games,
        IReadOnlyList<string> customConfigSerials)
    {
        var gamesBySerial = games
            .GroupBy(
                game => game.Serial,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        var detectedSerials = gamesBySerial.Keys
            .Concat(customConfigSerials)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<GameRow>(
            catalog.Count + gamesBySerial.Count);

        foreach (var recommendation in catalog.Rec
                     .OrderBy(rec => rec.WikiName)
                     .ThenBy(rec => rec.Serial))
        {
            var isDetected = detectedSerials.Contains(
                recommendation.Serial);
            var game = gamesBySerial.TryGetValue(
                recommendation.Serial,
                out var scannedGame)
                ? scannedGame
                : new R3Game(
                    Serial: recommendation.Serial,
                    Title: recommendation.WikiName,
                    InstPath: string.Empty,
                    Version: null,
                    PpuHash: null,
                    SpuHash: null);
            var recState = isDetected
                ? planService.BuildCfg(
                    install,
                    game,
                    recommendation,
                    hardware).RecState
                : BuildSum(recommendation);

            rows.Add(new GameRow(
                game,
                recommendation,
                recState,
                isDetected));
        }

        foreach (var game in gamesBySerial.Values
                     .Where(game => catalog.Find(game.Serial) is null)
                     .OrderBy(game => game.Title)
                     .ThenBy(game => game.Serial))
        {
            rows.Add(new GameRow(
                game,
                Rec: null,
                RecState: planService.BuildCfg(
                    install,
                    game,
                    recommendation: null,
                    hardware).RecState,
                InR3: true));
        }

        return rows;
    }

    private static RecSum BuildSum(
        WikiRec recommendation)
    {
        var items = recommendation.Changes
            .Select(change => new RecItem(
                Serial: recommendation.Serial,
                Path: change.Path,
                Current: null,
                Proposed: change.Value,
                Risk: change.Risk,
                State: RecState.Unknown,
                IsDiff: true,
                IsAction: false,
                IsSel: false,
                Notes: change.Notes ?? string.Empty))
            .ToArray();

        return new RecSum(
            Serial: recommendation.Serial,
            Total: items.Length,
            Applied: 0,
            Pending: 0,
            Manual: 0,
            Blocked: 0,
            Unknown: items.Length,
            Items: items);
    }
}
