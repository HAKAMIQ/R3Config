using System.IO;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class Rpcs3GameLibraryT
{
    public static async Task RunAsync(
        string tempRoot)
    {
        FirstRunWithoutPathRequiresRpcs3Path(tempRoot);
        await SavedPathScansLibraryAtStartup(tempRoot);
        await PathReplacementDropsStaleLibraryScan(tempRoot);

        Console.WriteLine(
            "Automatic startup RPCS3 game-library scan and stale-result ownership PASS.");
    }

    private static void FirstRunWithoutPathRequiresRpcs3Path(
        string tempRoot)
    {
        var settings = new SetSvc(Path.Combine(
            tempRoot,
            "first-run-game-library-settings.json"));
        var services = new AppRuntimeServices(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            settings);

        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });

        TestAssert.Assert(
            string.IsNullOrWhiteSpace(viewModel.ExePath) &&
            viewModel.GameList.LibraryScanState ==
                GameLibraryScanState.NotStarted &&
            viewModel.GameList.EmptyTxt ==
                UiText.GetText("Games.LibraryPathRequired") &&
            !viewModel.CanMut,
            "First run without an RPCS3 path must ask for the emulator path and must not report an empty scanned library.");
    }

    private static async Task SavedPathScansLibraryAtStartup(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "startup-game-library-scan");
        var exePath = CreateRpcs3WithGame(
            root,
            "BLUS90001",
            "Startup Library Game");
        var settings = new SetSvc(Path.Combine(
            root,
            "settings",
            "settings.json"));
        settings.Save(new AppSet(exePath));

        var services = new AppRuntimeServices(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            settings);

        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });

        TestAssert.Assert(
            viewModel.GameList.LibraryScanState ==
                GameLibraryScanState.Scanning,
            "A valid saved RPCS3 path must enter automatic RPCS3 game-library scan during MainVm startup.");

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                  GameLibraryScanState.Completed,
            "Startup RPCS3 game-library scan did not complete.");

        TestAssert.Assert(
            viewModel.GameList.Games.Count == 1 &&
            viewModel.GameList.Games[0].Serial == "BLUS90001" &&
            viewModel.GameList.Games[0].Title ==
                "Startup Library Game" &&
            !viewModel.CanMut,
            "Startup game-library scan must populate detected games without creating an analyzed or mutable RPCS3 context.");
    }

    private static async Task PathReplacementDropsStaleLibraryScan(
        string tempRoot)
    {
        var oldRoot = Path.Combine(
            tempRoot,
            "game-library-old");
        var newRoot = Path.Combine(
            tempRoot,
            "game-library-new");
        var oldExe = CreateRpcs3WithGame(
            oldRoot,
            "BLUS90002",
            "Old Library Game");
        var newExe = CreateRpcs3WithGame(
            newRoot,
            "BLUS90003",
            "New Library Game");
        using var oldEntered = new ManualResetEventSlim();
        using var oldRelease = new ManualResetEventSlim();

        IEnumerable<string> EnumerateDirectories(
            string path)
        {
            if (path.StartsWith(
                    oldRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                oldEntered.Set();
                oldRelease.Wait();
            }

            return Directory.EnumerateDirectories(path);
        }

        var baseCases = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var gameLibrary = new Rpcs3GameLibraryUseCase(
            new GameScanner(
                enumerateDirectories: EnumerateDirectories));
        var services = new AppRuntimeServices(
            new AppFlow(baseCases with
            {
                GameLibrary = gameLibrary
            }),
            new SetSvc(Path.Combine(
                tempRoot,
                "game-library-settings.json")));

        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });

        viewModel.ExePath = oldExe;
        TestAssert.Assert(
            oldEntered.Wait(TimeSpan.FromSeconds(5)),
            "Old RPCS3 game-library scan did not reach the controlled scan gate.");

        viewModel.ExePath = newExe;
        oldRelease.Set();

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                      GameLibraryScanState.Completed &&
                  viewModel.GameList.Games.Count == 1,
            "Replacement RPCS3 game-library scan did not complete.");

        TestAssert.Assert(
            viewModel.GameList.Games[0].Serial == "BLUS90003" &&
            viewModel.GameList.Games.All(game =>
                !game.Serial.Equals(
                    "BLUS90002",
                    StringComparison.OrdinalIgnoreCase)),
            "A stale game-library scan completion must not publish games from the replaced RPCS3 path.");
    }

    private static string CreateRpcs3WithGame(
        string root,
        string serial,
        string title)
    {
        Directory.CreateDirectory(root);
        var dataRoot = Path.Combine(
            root,
            "portable");
        Directory.CreateDirectory(Path.Combine(
            dataRoot,
            "config"));
        Directory.CreateDirectory(Path.Combine(
            dataRoot,
            "patches"));
        var gamePath = Path.Combine(
            dataRoot,
            "dev_hdd0",
            "game",
            serial);
        Directory.CreateDirectory(gamePath);

        TestFixtures.SfoWrite(
            Path.Combine(
                gamePath,
                "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = title,
                ["TITLE_ID"] = serial,
                ["APP_VER"] = "01.00"
            });

        var exePath = Path.Combine(
            root,
            "rpcs3.exe");
        File.WriteAllBytes(
            exePath,
            []);
        return exePath;
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        string failureMessage)
    {
        var deadline = DateTime.UtcNow +
                       TimeSpan.FromSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    failureMessage);
            }

            await Task.Delay(25);
        }
    }
}
