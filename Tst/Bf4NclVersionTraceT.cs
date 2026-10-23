using System.IO;
using System.Reflection;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Targeted regression trace for the reported defect: selecting Battlefield 4
// (BLUS31162) from the full catalog list (detected-only filter off) let the
// NCL Import dialog open against a synthetic, undetected placeholder
// (AnalysisUseCase.BuildRow's Version: null fallback), which NclImportVm then
// displayed as "Unknown" and immediately short-circuited with
// Patch.Ncl.NoVersion before ever reaching NclCorpusReader.LookupZip. The
// fix gates NclImportCmd on GameList.SelGame is { InR3: true }, matching the
// existing Launch/Capture guards, and this test proves both sides:
// the guard now blocks the undetected placeholder, and a genuinely detected
// BLUS31162 install reaches the exact bundled lookup with real cheats.
internal static class Bf4NclVersionTraceT
{
    private const string Serial = "BLUS31162";

    public static void Run(string tempRoot)
    {
        UndetectedCatalogRowIsBlockedFromNclImport(tempRoot);
        DetectedGameReachesExactBundledLookup(tempRoot);
        Console.WriteLine(
            "Battlefield 4 (BLUS31162) NCL version trace regression PASS.");
    }

    private static void UndetectedCatalogRowIsBlockedFromNclImport(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "bf4-undetected-catalog-row");
        Directory.CreateDirectory(root);
        var portable = Path.Combine(root, "portable");
        Directory.CreateDirectory(Path.Combine(portable, "config"));
        Directory.CreateDirectory(Path.Combine(portable, "patches"));
        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);

        var settings = new SetSvc(Path.Combine(root, "settings", "settings.json"));
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

        var analyze = typeof(MainVm).GetMethod(
            "DoAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync was not found.");
        var task = (Task)(analyze.Invoke(viewModel, null) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return a Task."));
        task.GetAwaiter().GetResult();

        // BLUS31162 is present in the bundled catalog but was never detected
        // as installed here (no games.yml / dev_hdd0 entry was created), so
        // it only appears once the detected-only filter is turned off.
        viewModel.GameList.InR3 = false;
        var row = viewModel.GameList.Games.SingleOrDefault(game =>
            game.Serial.Equals(Serial, StringComparison.OrdinalIgnoreCase));

        TestAssert.Assert(
            row is { InR3: false },
            "Expected Battlefield 4 to appear as a catalog-only, undetected row when no install evidence exists.");
        TestAssert.Assert(
            string.IsNullOrWhiteSpace(row!.Game.Version),
            "Expected the undetected catalog placeholder to carry a null/blank Version (AnalysisUseCase.BuildRow fallback) — this is the FIRST LOSS POINT.");

        viewModel.GameList.SelGame = row;

        TestAssert.Assert(
            !viewModel.NclImportCmd.CanRun(null),
            "FIX: selecting an undetected catalog-only game must not allow opening NCL Import (it must require GameList.SelGame is { InR3: true }, matching Launch/Capture).");

        Console.WriteLine(
            $"[trace] undetected row: Serial={row.Serial} Version={(row.Game.Version ?? "<null>")} InR3={row.InR3} NclImportCmd.CanRun={viewModel.NclImportCmd.CanRun(null)}");
    }

    private static void DetectedGameReachesExactBundledLookup(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "bf4-detected-exact-lookup");
        Directory.CreateDirectory(root);
        var portable = Path.Combine(root, "portable");
        var configPath = Path.Combine(portable, "config");
        var patchesPath = Path.Combine(portable, "patches");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(patchesPath);
        var gamePath = Path.Combine(portable, "dev_hdd0", "game", Serial);
        Directory.CreateDirectory(gamePath);

        // A real, correctly-scanned installed-game descriptor: Version comes
        // from PARAM.SFO APP_VER via GameScanner, matching a build the
        // bundled corpus actually ships static cheats for.
        TestFixtures.SfoWrite(
            Path.Combine(gamePath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Battlefield 4",
                ["TITLE_ID"] = Serial,
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });

        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);

        var install = Rpcs3InstallService.FromExe(exePath);
        var scanned = new GameScanner().Scan(install)
            .Single(game => game.Serial.Equals(
                Serial,
                StringComparison.OrdinalIgnoreCase));

        Console.WriteLine(
            $"[trace] GameScanner descriptor: Serial={scanned.Serial} Version={(scanned.Version ?? "<null>")} Category={scanned.Category}");

        TestAssert.Assert(
            !string.IsNullOrWhiteSpace(scanned.Version) &&
            !scanned.Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase),
            "REAL_SELECTED_GAME_VERSION != Unknown: expected GameScanner to report a real APP_VER-derived version for a detected install.");

        var flow = new AppFlow(
            AppComposition.Build(
                new Rpcs3MutationGuard(() => false)));

        try
        {
            var direct = flow.DiscoverBundledNcl(Serial, "01.00");
            Console.WriteLine(
                $"[trace] direct DiscoverBundledNcl: IsOk={direct.IsOk} Code={direct.Code} Cheats={direct.Cheats.Count} Error={direct.Error}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[trace] direct DiscoverBundledNcl threw: {exception}");
        }

        var viewModel = new R3Config.Vm.NclImportVm(
            flow,
            install,
            scanned,
            scanned.Title,
            enabledMatches: [],
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            pickFile: () => null);

        viewModel.InitializeAsync().GetAwaiter().GetResult();

        Console.WriteLine(
            $"[trace] NclImportVm: GameIdentity=\"{viewModel.GameIdentity}\" InspectionStatus=\"{viewModel.InspectionStatus}\" Cheats.Count={viewModel.Cheats.Count}");

        TestAssert.Assert(
            !viewModel.GameIdentity.Contains(
                UiText.GetText("Ui.Unknown"),
                StringComparison.Ordinal),
            "REAL_SELECTED_GAME_VERSION != Unknown: NclImportVm.GameIdentity must show the real detected version, not the Unknown placeholder.");

        var nclExactLookupReached =
            !string.Equals(
                viewModel.InspectionStatus,
                UiText.GetText("Patch.Ncl.NoVersion"),
                StringComparison.Ordinal) &&
            !string.Equals(
                viewModel.InspectionStatus,
                UiText.GetText("Patch.Ncl.Corpus.Unavailable"),
                StringComparison.Ordinal);

        TestAssert.Assert(
            nclExactLookupReached,
            "NCL_EXACT_LOOKUP_REACHED = true: expected the bundled corpus lookup to actually run instead of short-circuiting on a missing version or unavailable corpus.");

        TestAssert.Assert(
            viewModel.Cheats.Count > 0,
            "UI_CHEATS_COUNT > 0: expected the exact-match bundled corpus lookup to surface at least one Battlefield 4 BLUS31162 01.00 cheat.");
    }
}
