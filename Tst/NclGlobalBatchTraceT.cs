using System.IO;
using System.Reflection;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// READ-ONLY batch diagnostic (no production code changes). Traces a
// representative sample of exact-proven bundled NCL corpus entries through
// the real production path: Rpcs3GameDescriptor (simulated "detected" state,
// version supplied as GameScanner would produce it) -> NclImportVm ->
// AppFlow.DiscoverBundledNcl -> Cheats collection -> UI count. Prints one row
// per case plus aggregate metrics. Does not fix, rebuild, or audit anything.
internal static class NclGlobalBatchTraceT
{
    private sealed record Case(string Game, string Serial, string Version, string Tag);

    private static readonly Case[] Cases =
    [
        new("007 Legends", "BLUS30983", "01.01", "normal"),
        new("1942 Joint Strike", "NPUB30024", "01.00", "normal"),
        new("2014 FIFA World Cup Brazil", "BLUS31389", "01.01", "normal"),
        new("3D Dot Game Heroes", "BLES00875", "01.00", "normal"),
        new("A.C.E. Another Century's Episode R", "BLJS10081", "01.02", "normal"),
        new("Abyss Odyssey", "NPUB31397", "01.01", "normal"),
        new("Ace Combat Assault Horizon", "BLES01392", "01.00", "normal"),
        new("Adventure Time Explore The Dungeon", "BLES01961", "01.00", "normal"),
        new("Alien Rage", "NPUB30863", "01.00", "normal"),
        new("Alien Isolation", "BLES01697", "01.00", "normal"),

        new("3D Dot Game Heroes (multi-serial)", "BLJM60180", "01.00", "multi-serial"),
        new("After Burner Climax (multi-serial)", "NPEB00175", "01.00", "multi-serial"),
        new("Akiba's Trip 2 Undead And Undressed", "BLUS31425", "01.00", "multi-serial"),
        new("Alien Breed 2 Assault (multi-serial)", "NPEB00345", "01.00", "multi-serial"),
        new("Aliens Vs Predator", "BLUS30493", "01.00", "multi-serial"),

        new("3D Dot Game Heroes v01.00", "BLES00875", "01.00", "multi-version"),
        new("3D Dot Game Heroes v01.01", "BLES00875", "01.01", "multi-version"),
        new("Armored Core For Answer v01.02", "BLJM60066", "01.02", "multi-version"),
        new("Armored Core For Answer v01.35", "BLJM60066", "01.35", "multi-version"),
        new("Agarest Generations Of War v01.01", "BLES00594", "01.01", "multi-version"),

        new("Battlefield 4 1.00", "BLUS31162", "01.00", "sentinel"),
        new("Battlefield 4 1.01", "BLUS31162", "01.01", "sentinel"),
        new("Amy v01.02", "NPUB30613", "01.02", "spot-check"),
        new("Amy v01.00", "NPUB30613", "01.00", "spot-check"),
        new("Agarest Generations Of War v01.00", "BLES00594", "01.00", "spot-check"),
    ];

    public static void Run()
    {
        var flow = new AppFlow(
            AppComposition.Build(
                new Rpcs3MutationGuard(() => false)));

        var install = new Rpcs3InstallDescriptor(
            RootPath: AppContext.BaseDirectory,
            ExePath: Path.Combine(AppContext.BaseDirectory, "rpcs3.exe"),
            CfgPath: AppContext.BaseDirectory,
            CustPath: AppContext.BaseDirectory,
            PatPath: AppContext.BaseDirectory,
            PatCfg: Path.Combine(AppContext.BaseDirectory, "patch_config.yml"),
            DataRoot: AppContext.BaseDirectory);

        Console.WriteLine(
            "GAME | SERIAL | VERSION_AT_GAME_LIST | VERSION_AT_DIALOG_OPEN | VERSION_AT_VM | LOOKUP_RESULT | LOOKUP_COUNT | UI_COUNT");

        var total = 0;
        var versionKnownAtGameList = 0;
        var versionUnknownBeforeDialog = 0;
        var versionUnknownInVm = 0;
        var lookupReached = 0;
        var lookupZero = 0;
        var uiZeroWithLookupNonZero = 0;
        var failures = new List<string>();

        foreach (var testCase in Cases)
        {
            total++;

            // VERSION_AT_GAME_LIST: what GameVm/GameList would carry for a
            // correctly detected install (GameScanner-produced descriptor).
            var versionAtGameList = testCase.Version;
            var game = new Rpcs3GameDescriptor(
                testCase.Serial,
                testCase.Game,
                InstPath: AppContext.BaseDirectory,
                Version: versionAtGameList,
                PpuHash: null,
                SpuHash: null);

            // VERSION_AT_DIALOG_OPEN: what OpenNclImport hands to NclImportVm's
            // constructor (selected.Game.Version, unchanged from the list).
            var versionAtDialogOpen = game.Version;
            if (string.IsNullOrWhiteSpace(versionAtDialogOpen))
            {
                versionUnknownBeforeDialog++;
            }

            var viewModel = new NclImportVm(
                flow,
                install,
                game,
                game.Title,
                enabledMatches: [],
                isRpcs3Running: () => false,
                confirm: (_, _) => true,
                pickFile: () => null);

            viewModel.InitializeAsync().GetAwaiter().GetResult();

            var versionField = typeof(NclImportVm).GetField(
                "_version",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var versionAtVm = versionField?.GetValue(viewModel) as string;
            if (string.IsNullOrWhiteSpace(versionAtVm))
            {
                versionUnknownInVm++;
            }
            else
            {
                versionKnownAtGameList++;
            }

            NclCorpusLookupResult discovery;
            try
            {
                discovery = flow.DiscoverBundledNcl(testCase.Serial, testCase.Version);
            }
            catch (Exception exception)
            {
                discovery = new NclCorpusLookupResult(
                    false,
                    NclCorpusLookupCode.InvalidTarget,
                    null,
                    [],
                    [],
                    [],
                    0,
                    0,
                    exception.GetType().Name + ": " + exception.Message);
            }

            var lookupResult = discovery.Code.ToString();
            var lookupCount = discovery.Cheats.Count;
            var uiCount = viewModel.Cheats.Count;

            if (discovery.Code != NclCorpusLookupCode.InvalidTarget)
            {
                lookupReached++;
            }

            if (lookupCount == 0)
            {
                lookupZero++;
                failures.Add(
                    $"{testCase.Game} [{testCase.Serial} {testCase.Version}] -> {lookupResult}");
            }

            if (uiCount == 0 && lookupCount > 0)
            {
                uiZeroWithLookupNonZero++;
            }

            Console.WriteLine(
                $"{testCase.Game} | {testCase.Serial} | {versionAtGameList} | {versionAtDialogOpen} | {(versionAtVm ?? "<null>")} | {lookupResult} | {lookupCount} | {uiCount}");
        }

        Console.WriteLine();
        Console.WriteLine($"TOTAL_CASES = {total}");
        Console.WriteLine($"VERSION_KNOWN_AT_GAME_LIST = {versionKnownAtGameList}");
        Console.WriteLine($"VERSION_BECAME_UNKNOWN_BEFORE_DIALOG = {versionUnknownBeforeDialog}");
        Console.WriteLine($"VERSION_BECAME_UNKNOWN_IN_VM = {versionUnknownInVm}");
        Console.WriteLine($"LOOKUP_REACHED = {lookupReached}");
        Console.WriteLine($"LOOKUP_ZERO = {lookupZero}");
        Console.WriteLine($"UI_ZERO_WITH_LOOKUP_NONZERO = {uiZeroWithLookupNonZero}");
        Console.WriteLine();
        Console.WriteLine("SAMPLE_FAILURES:");
        foreach (var failure in failures.Take(5))
        {
            Console.WriteLine("  " + failure);
        }
    }
}
