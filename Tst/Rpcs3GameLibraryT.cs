using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
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
        await CustomConfigOnlySerialDoesNotAppearInstalled(tempRoot);
        GameDataWithoutBaseDoesNotAppearInstalled(tempRoot);
        CustomVfsHdd0IsUsedForInstalledLibrary(tempRoot);
        await SameTitleDifferentInstalledSerialsRemainDistinct(tempRoot);
        await AnalysisRefreshPreservesSelectedGame(tempRoot);
        await AnalysisRefreshPreservesSelectedGameWhenTitleChanges(tempRoot);
        await PathReplacementDropsStaleLibraryScan(tempRoot);
        await GameIconLookupUsesDescriptorPath(tempRoot);
        await GameIconRejectsMalformedAndBoundedInputs(tempRoot);
        await GameIconLoadReleasesFileAndFreezesPixels(tempRoot);
        await GameIconCachePreservesIdentityAndEvicts(tempRoot);
        await GameIconRejectsNonLocalPathsAndPreservesCancellation(tempRoot);

        Console.WriteLine(
            "Automatic startup RPCS3 game-library scan and stale-result ownership PASS.");
        Console.WriteLine("Game icon discovery, bounded decode, frozen cache identity, eviction, and cancellation PASS.");
    }

    private static async Task GameIconLookupUsesDescriptorPath(string tempRoot)
    {
        var game = IconGame(tempRoot, "icon-lookup");
        TestAssert.Assert(Rpcs3GameIconLocator.Find(game) is null && await GameIcon.LoadAsync(game) is null,
            "A missing optional icon must return null without creating a game directory.");
        TestAssert.Assert(!Directory.Exists(game.InstPath), "Icon lookup must remain read-only.");

        var discIcon = Path.Combine(game.InstPath, "PS3_GAME", "ICON0.PNG");
        Directory.CreateDirectory(Path.GetDirectoryName(discIcon)!);
        File.WriteAllBytes(discIcon, IconPng(2, 1, 70));
        TestAssert.Assert(Rpcs3GameIconLocator.Find(game) == discIcon &&
                          Rpcs3GameIconLocator.Find(game with { InstPath = Path.GetDirectoryName(discIcon)! }) == discIcon,
            "Disc root and an existing PS3_GAME descriptor must resolve the same ICON0.PNG.");

        var rootIcon = Path.Combine(game.InstPath, "ICON0.PNG");
        File.WriteAllBytes(rootIcon, IconPng(3, 1, 90));
        TestAssert.Assert(Rpcs3GameIconLocator.Find(game) == rootIcon &&
                          Rpcs3GameIconLocator.Find(game with { Title = "Different display title" }) == rootIcon,
            "Root ICON0.PNG must take precedence and discovery must not depend on display title.");
        var canonical = game with { InstPath = Path.Combine(game.InstPath, ".") };
        TestAssert.Assert(Rpcs3GameIconLocator.Find(canonical) == rootIcon,
            "Equivalent descriptor paths must resolve a canonical local icon path.");
    }

    private static async Task GameIconRejectsMalformedAndBoundedInputs(string tempRoot)
    {
        var valid = IconPng(2, 1, 80);
        var zeroWidth = valid.ToArray();
        Array.Clear(zeroWidth, 16, 4);
        var hostileHeight = valid.ToArray();
        Array.Fill(hostileHeight, byte.MaxValue, 20, 4);
        var oversizedFile = new byte[GameIcon.MaxFileBytes + 1];
        valid.CopyTo(oversizedFile, 0);
        var malformed = new (string Name, byte[] Bytes)[]
        {
            ("empty", []),
            ("short-header", valid[..32]),
            ("truncated-pixels", valid[..45]),
            ("not-png", new byte[64]),
            ("zero-width", zeroWidth),
            ("hostile-height", hostileHeight),
            ("too-wide", IconPng(GameIcon.MaxDimension + 1, 1, 30)),
            ("too-tall", IconPng(1, GameIcon.MaxDimension + 1, 30)),
            ("oversize-file", oversizedFile)
        };
        foreach (var item in malformed)
        {
            var game = WriteIcon(tempRoot, "icon-invalid-" + item.Name, item.Bytes);
            TestAssert.Assert(await GameIcon.LoadAsync(game) is null,
                $"Untrusted {item.Name} ICON0.PNG must be rejected without a decoding exception.");
        }

        var atFileLimit = new byte[GameIcon.MaxFileBytes];
        valid.CopyTo(atFileLimit, 0);
        var fileBoundary = WriteIcon(tempRoot, "icon-file-limit", atFileLimit);
        TestAssert.Assert(await GameIcon.LoadAsync(fileBoundary) is { PixelWidth: 2, PixelHeight: 1 },
            "A valid PNG exactly at the file-size limit remains supported.");
        foreach (var size in new[] { (GameIcon.MaxDimension, 2), (2, GameIcon.MaxDimension) })
        {
            var game = WriteIcon(tempRoot, $"icon-dimension-limit-{size.Item1}", IconPng(size.Item1, size.Item2, 40));
            var image = await GameIcon.LoadAsync(game);
            TestAssert.Assert(image is not null && image.IsFrozen &&
                              Math.Max(image.PixelWidth, image.PixelHeight) == GameIcon.PreviewDimension &&
                              Math.Min(image.PixelWidth, image.PixelHeight) >= 1,
                "Valid maximum PNG dimensions must decode into a bounded, aspect-preserving frozen preview.");
        }
    }

    private static async Task GameIconLoadReleasesFileAndFreezesPixels(string tempRoot)
    {
        var game = WriteIcon(tempRoot, "icon-unlocked", IconPng(2, 1, 110));
        var image = await GameIcon.LoadAsync(game);
        TestAssert.Assert(image is { IsFrozen: true, PixelWidth: 2, PixelHeight: 1 },
            "Small icons must retain their original pixel dimensions and be frozen before leaving the worker.");
        var path = Path.Combine(game.InstPath, "ICON0.PNG");
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            TestAssert.Assert(exclusive.Length > 0, "Loading an icon must release its source file handle.");
        }

        File.Delete(path);
        var pixels = new byte[8];
        await Task.Run(() => image!.CopyPixels(pixels, 8, 0));
        TestAssert.Assert(pixels[0] == 110 && pixels[4] == 111,
            "Frozen icon pixels must remain usable on another thread after the source file is deleted.");
    }

    private static async Task GameIconCachePreservesIdentityAndEvicts(string tempRoot)
    {
        var game = WriteIcon(tempRoot, "icon-cache-first", IconPng(2, 1, 120));
        var first = await GameIcon.LoadAsync(game);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GameIcon.LoadAsync(game)));
        TestAssert.Assert(first is not null && concurrent.All(image => ReferenceEquals(image, first)),
            "Concurrent requests for the same descriptor must reuse one decoded image.");
        TestAssert.Assert(ReferenceEquals(first, await GameIcon.LoadAsync(game with
            { InstPath = Path.Combine(game.InstPath, "."), Title = "Renamed title" })),
            "Canonical path identity and title refresh must reuse the same icon.");

        var other = WriteIcon(tempRoot, "icon-cache-other", IconPng(3, 1, 140)) with { Title = game.Title };
        var second = await GameIcon.LoadAsync(other);
        TestAssert.Assert(second is { PixelWidth: 3 } && !ReferenceEquals(first, second),
            "Different game paths sharing a display title and serial must not share an icon.");
        var alternateSerial = await GameIcon.LoadAsync(game with { Serial = "BLES90010" });
        TestAssert.Assert(alternateSerial is not null && !ReferenceEquals(first, alternateSerial),
            "The cache identity must retain the descriptor serial as well as its canonical path.");

        // Ensure first.Created is older even on coarse-resolution system clocks.
        await Task.Delay(30);
        for (var index = 0; index < GameIcon.CacheCapacity; index++)
        {
            var next = WriteIcon(tempRoot, $"icon-cache-fill-{index}", IconPng(2, 1, (byte)index));
            TestAssert.Assert(await GameIcon.LoadAsync(next) is not null, "Cache fill icon must decode.");
        }

        var reloaded = await GameIcon.LoadAsync(game);
        TestAssert.Assert(reloaded is { PixelWidth: 2 } && !ReferenceEquals(first, reloaded),
            "More than CacheCapacity distinct games must evict the oldest bitmap instead of retaining it indefinitely.");
    }

    private static async Task GameIconRejectsNonLocalPathsAndPreservesCancellation(string tempRoot)
    {
        foreach (var path in new[] { "", "relative-game", @"\\invalid-host\share\game", @"\\?\C:\game", "https://example.invalid/game", "\0" })
        {
            var game = IconGame(tempRoot, "unused-icon-path") with { InstPath = path };
            TestAssert.Assert(Rpcs3GameIconLocator.Find(game) is null && await GameIcon.LoadAsync(game) is null,
                "Non-local, relative, device, and malformed icon paths must fail closed.");
        }

        var cancelledGame = IconGame(tempRoot, "icon-cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;
        try
        {
            await GameIcon.LoadAsync(cancelledGame, cancellation.Token);
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellation.Token)
        {
            cancelled = true;
        }

        TestAssert.Assert(cancelled && !Directory.Exists(cancelledGame.InstPath),
            "Pre-cancelled icon work must preserve cancellation without filesystem side effects.");
        var subsequent = WriteIcon(tempRoot, "icon-after-cancellation", IconPng(2, 1, 160));
        TestAssert.Assert(await GameIcon.LoadAsync(subsequent) is not null,
            "Cancelling one icon request must not consume the worker semaphore or block later games.");
    }

    private static Rpcs3GameDescriptor IconGame(string tempRoot, string name) =>
        new("BLUS90010", "Same display title", Path.Combine(tempRoot, name), "01.00", null, null);

    private static Rpcs3GameDescriptor WriteIcon(string tempRoot, string name, byte[] bytes)
    {
        var game = IconGame(tempRoot, name);
        Directory.CreateDirectory(game.InstPath);
        File.WriteAllBytes(Path.Combine(game.InstPath, "ICON0.PNG"), bytes);
        return game;
    }

    private static byte[] IconPng(int width, int height, byte blue)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = (byte)(blue + index / 4 % 2);
            pixels[index + 3] = byte.MaxValue;
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        source.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        return memory.ToArray();
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

    private static async Task CustomConfigOnlySerialDoesNotAppearInstalled(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "custom-config-is-not-library-membership");
        var exePath = CreateRpcs3WithGame(
            root,
            "BLUS90008",
            "Detected Library Game");
        var customConfigPath = Path.Combine(
            root,
            "portable",
            "config",
            "custom_configs");
        Directory.CreateDirectory(customConfigPath);
        File.WriteAllText(
            Path.Combine(
                customConfigPath,
                "config_BLUS31465.yml"),
            "Core:\r\n  PPU Decoder: Recompiler (LLVM)\r\n");

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

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                  GameLibraryScanState.Completed,
            "RPCS3 game-library scan with a stale custom config did not complete.");

        TestAssert.Assert(
            viewModel.GameList.Games.Count == 1 &&
            viewModel.GameList.Games[0].Serial == "BLUS90008" &&
            viewModel.GameList.Games.All(game =>
                !game.Serial.Equals(
                    "BLUS31465",
                    StringComparison.OrdinalIgnoreCase)),
            "A custom config must not create an installed-game row when RPCS3 has no matching serial in its game library.");
    }

    private static void GameDataWithoutBaseDoesNotAppearInstalled(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "orphan-game-data-is-not-library-membership");
        var dataRoot = Path.Combine(
            root,
            "portable");
        var configPath = Path.Combine(
            dataRoot,
            "config");
        var gameDataPath = Path.Combine(
            dataRoot,
            "dev_hdd0",
            "game",
            "BLUS90010");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(gameDataPath);
        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);
        TestFixtures.SfoWrite(
            Path.Combine(gameDataPath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Orphan Game Data",
                ["TITLE_ID"] = "BLUS90010",
                ["APP_VER"] = "01.19",
                ["CATEGORY"] = "GD"
            });

        var result = new GameScanner().Scan(
            Rpcs3InstallService.FromExe(exePath));

        TestAssert.Assert(
            result.All(game => game.Serial != "BLUS90010"),
            "CATEGORY=GD without a trusted DG/HG base must not create an installed-game row.");
    }

    private static void CustomVfsHdd0IsUsedForInstalledLibrary(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "custom-vfs-installed-library");
        var configPath = Path.Combine(
            root,
            "config");
        var resolvedHdd0 = Path.Combine(
            tempRoot,
            "custom-vfs-installed-library-hdd0");
        var realGamePath = Path.Combine(
            resolvedHdd0,
            "game",
            "NPEB90011");
        var decoyGamePath = Path.Combine(
            root,
            "dev_hdd0",
            "game",
            "NPEB90012");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(realGamePath);
        Directory.CreateDirectory(decoyGamePath);
        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);
        File.WriteAllText(
            Path.Combine(configPath, "vfs.yml"),
            $"\"/dev_hdd0/\": \"{TestFixtures.EscPath(resolvedHdd0)}\"{Environment.NewLine}");
        TestFixtures.SfoWrite(
            Path.Combine(realGamePath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Resolved VFS Game",
                ["TITLE_ID"] = "NPEB90011",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });
        TestFixtures.SfoWrite(
            Path.Combine(decoyGamePath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Default Path Decoy",
                ["TITLE_ID"] = "NPEB90012",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });

        var install = Rpcs3InstallService.FromExe(exePath);
        var result = new GameScanner().Scan(install);

        TestAssert.Assert(
            result.Any(game => game.Serial == "NPEB90011") &&
            result.All(game => game.Serial != "NPEB90012"),
            "The installed-game library must follow RPCS3's resolved /dev_hdd0/ VFS path and ignore a DataRoot\\dev_hdd0 decoy.");
    }

    private static async Task SameTitleDifferentInstalledSerialsRemainDistinct(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "same-title-different-installed-serials");
        var exePath = CreateRpcs3WithGame(
            root,
            "BLUS90009",
            "Regional Game");
        AddGame(
            root,
            "BLES90009",
            "Regional Game");
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

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                      GameLibraryScanState.Completed &&
                  viewModel.GameList.Games.Count == 2,
            "RPCS3 game-library scan did not expose both installed regional serials.");

        TestAssert.Assert(
            viewModel.GameList.Games
                .Select(game => game.Serial)
                .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(
                    ["BLES90009", "BLUS90009"],
                    StringComparer.OrdinalIgnoreCase),
            "Installed games with the same display title must remain distinct when their RPCS3 serials differ.");
    }

    private static async Task AnalysisRefreshPreservesSelectedGame(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "analysis-preserves-game-selection");
        var exePath = CreateRpcs3WithGame(
            root,
            "BLUS90004",
            "Alpha Library Game");
        AddGame(
            root,
            "BLUS90005",
            "Zulu Library Game");
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

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                      GameLibraryScanState.Completed &&
                  viewModel.GameList.Games.Count == 2,
            "Startup RPCS3 game-library scan did not expose both controlled games.");

        viewModel.GameList.SelGame = viewModel.GameList.Games.Single(game =>
            game.Serial.Equals(
                "BLUS90005",
                StringComparison.OrdinalIgnoreCase));

        var analyze = typeof(MainVm).GetMethod(
            "DoAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync was not found.");
        var task = (Task)(analyze.Invoke(viewModel, null) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return a Task."));
        await task;

        TestAssert.Assert(
            viewModel.GameList.SelGame?.Serial.Equals(
                "BLUS90005",
                StringComparison.OrdinalIgnoreCase) == true,
            "Refreshing full RPCS3 analysis must preserve the user's selected game when that game still exists in the refreshed library.");
    }

    private static async Task AnalysisRefreshPreservesSelectedGameWhenTitleChanges(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "analysis-preserves-game-selection-after-title-refresh");
        var exePath = CreateRpcs3WithGame(
            root,
            "BLUS90006",
            "Alpha Library Game");
        AddGame(
            root,
            "BLUS90007",
            "Original Zulu Title");
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

        await WaitUntilAsync(
            () => viewModel.GameList.LibraryScanState ==
                      GameLibraryScanState.Completed &&
                  viewModel.GameList.Games.Count == 2,
            "Startup RPCS3 game-library scan did not expose both controlled games before the title refresh.");

        viewModel.GameList.SelGame = viewModel.GameList.Games.Single(game =>
            game.Serial.Equals(
                "BLUS90007",
                StringComparison.OrdinalIgnoreCase));

        AddGame(
            root,
            "BLUS90007",
            "Updated Zulu Title");

        var analyze = typeof(MainVm).GetMethod(
            "DoAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync was not found.");
        var task = (Task)(analyze.Invoke(viewModel, null) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return a Task."));
        await task;

        TestAssert.Assert(
            viewModel.GameList.SelGame is
            {
                Serial: "BLUS90007",
                Title: "Updated Zulu Title"
            },
            "Refreshing analysis must preserve the selected game by its normalized serial even when refreshed metadata changes the display title.");
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
        AddGame(
            root,
            serial,
            title);

        var exePath = Path.Combine(
            root,
            "rpcs3.exe");
        File.WriteAllBytes(
            exePath,
            []);
        return exePath;
    }

    private static void AddGame(
        string root,
        string serial,
        string title)
    {
        var gamePath = Path.Combine(
            root,
            "portable",
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
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });
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
