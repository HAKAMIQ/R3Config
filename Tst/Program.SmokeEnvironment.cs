using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static SmokeEnv MakeEnv(string tempRoot)
    {
        var fakeRpcs3Root = Path.Combine(tempRoot, "rpcs3");
        var configPath = Path.Combine(fakeRpcs3Root, "config");
        var gameFolder = Path.Combine(tempRoot, "games", "Rogue");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(Path.Combine(gameFolder, "PS3_GAME"));
        var hddGameFolder = Path.Combine(fakeRpcs3Root, "dev_hdd0", "game", "NPUB00001");
        Directory.CreateDirectory(hddGameFolder);
        var staleHddFolder = Path.Combine(
            fakeRpcs3Root,
            "dev_hdd0",
            "game",
            "FAKE12345");
        Directory.CreateDirectory(staleHddFolder);
        Directory.CreateDirectory(Path.Combine(fakeRpcs3Root, "patches"));
        File.WriteAllText(Path.Combine(fakeRpcs3Root, "rpcs3.exe"), string.Empty);
        var staleGamePath = Path.Combine(
            tempRoot,
            "placeholder",
            "TEST12345");
        Directory.CreateDirectory(Path.Combine(
            staleGamePath,
            "PS3_GAME"));
        SfoWrite(
            Path.Combine(
                staleGamePath,
                "PS3_GAME",
                "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "TEST12345",
                ["TITLE_ID"] = "TEST12345",
                ["APP_VER"] = "01.00"
            });
        File.WriteAllText(
            Path.Combine(configPath, "games.yml"),
            $"BLUS31465: \"{EscPath(gameFolder)}\"{Environment.NewLine}" +
            $"TEST12345: \"{EscPath(staleGamePath)}\"{Environment.NewLine}");
        SfoWrite(Path.Combine(gameFolder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = "Assassin's Creed Rogue SFO",
            ["TITLE_ID"] = "BLUS31465",
            ["APP_VER"] = "01.02"
        });
        SfoWrite(Path.Combine(hddGameFolder, "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = "Non Portable HDD Game",
            ["TITLE_ID"] = "NPUB00001",
            ["APP_VER"] = "01.00"
        });

        var wikiXmlPath = Path.Combine(tempRoot, "wiki.xml");
        File.WriteAllText(wikiXmlPath, WikiXml());

        var install = Rpcs3InstallService.FromExe(Path.Combine(fakeRpcs3Root, "rpcs3.exe"));
        Assert(install.CfgPath == configPath, "Expected config path detection.");
        Assert(install.PatPath == Path.Combine(fakeRpcs3Root, "patches"),
            "Expected patches folder to be anchored to the RPCS3 executable folder.");
        Console.WriteLine($"Config: {install.CfgPath}");

        return new SmokeEnv(
            TempRoot: tempRoot,
            FakeRoot: fakeRpcs3Root,
            CfgPath: configPath,
            GameDir: gameFolder,
            WikiPath: wikiXmlPath,
            Install: install);
    }

    static HwState Hw_All()
    {
        var highEndHardware = new HwProbe(
            readCpuName: () => "Intel Core i5-12600KF",
            readLogicalProcessors: () => 16,
            readTotalMemoryBytes: () => 32UL * 1024UL * 1024UL * 1024UL,
            readGpuName: () => "NVIDIA GeForce RTX 3060").Probe();
        Assert(highEndHardware.Tier == HwTier.High,
            "Expected high-end hardware profile from CPU/RAM/GPU probe data.");
        Assert(highEndHardware.CpuName.Contains("12600KF", StringComparison.OrdinalIgnoreCase),
            "Expected CPU name to be preserved.");
        Assert(highEndHardware.TotalB >= 31.9,
            "Expected total RAM to be reported in GiB.");
        Assert(highEndHardware.GpuName.Contains("RTX 3060", StringComparison.OrdinalIgnoreCase),
            "Expected GPU name to be preserved.");

        var balancedHardware = new HwProbe(
            readCpuName: () => "Ryzen 5 Test CPU",
            readLogicalProcessors: () => 6,
            readTotalMemoryBytes: () => 16UL * 1024UL * 1024UL * 1024UL,
            readGpuName: () => "Test GPU").Probe();
        Assert(balancedHardware.Tier == HwTier.Mid,
            "Expected balanced hardware profile from moderate CPU/RAM data.");

        var lowEndHardware = new HwProbe(
            readCpuName: () => "Dual Core Test CPU",
            readLogicalProcessors: () => 2,
            readTotalMemoryBytes: () => 4UL * 1024UL * 1024UL * 1024UL,
            readGpuName: () => "Basic Display Adapter").Probe();
        Assert(lowEndHardware.Tier == HwTier.Low,
            "Expected low-end hardware profile from low CPU/RAM data.");

        var failedHardware = new HwProbe(
            readCpuName: () => throw new IOException("Simulated CPU probe failure."),
            readLogicalProcessors: () => 0,
            readTotalMemoryBytes: () => null,
            readGpuName: () => null).Probe();
        Assert(failedHardware.Tier == HwTier.Unknown && failedHardware.IsFall,
            "Expected hardware probe failure to return Unknown safely.");

        return new HwState(lowEndHardware);
    }

    static ScanStat GameScan(SmokeEnv env)
    {
        var scanner = new GameScanner();
        var games = scanner.Scan(env.Install);
        Assert(games.Count == 2, "Expected stale games.yml and dev_hdd0 entries without trusted PARAM.SFO metadata to stay out of the installed-game list.");
        Assert(!games.Any(game => game.Serial is "TEST12345" or "FAKE12345"),
            "Expected placeholder serials without a real installed game title to be excluded.");
        var rogue = games.Single(game => game.Serial == "BLUS31465");
        Assert(rogue.Title == "Assassin's Creed Rogue SFO", "Expected title from PARAM.SFO.");
        Assert(rogue.Version == "01.02", "Expected app version from PARAM.SFO.");
        Assert(games.Any(game => game.Serial == "NPUB00001"), "Expected non-portable dev_hdd0 game.");

        Directory.CreateDirectory(env.Install.CustPath);
        File.WriteAllText(
            Path.Combine(
                env.Install.CustPath,
                "config_BLES02061.yml"),
            $"{{}}{Environment.NewLine}");
        File.WriteAllText(
            Path.Combine(
                env.Install.CustPath,
                "config_not-a-serial.yml"),
            $"{{}}{Environment.NewLine}");
        var customConfigSerials = scanner.Scan2(env.Install);
        Assert(customConfigSerials.SequenceEqual(["BLES02061"]),
            "Expected bounded custom-config scanning to return only normalized valid serials.");
        Console.WriteLine($"Game: {rogue.Serial} {rogue.Title} {rogue.Version}");

        var updateFolder = Path.Combine(env.FakeRoot, "dev_hdd0", "game", rogue.Serial);
        Directory.CreateDirectory(updateFolder);
        SfoWrite(Path.Combine(updateFolder, "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = "Internal update package title",
            ["TITLE_ID"] = rogue.Serial,
            ["APP_VER"] = "01.03"
        });
        var updatedRogue = new GameScanner().Scan(env.Install).Single(game => game.Serial == rogue.Serial);
        Assert(updatedRogue.Title == rogue.Title,
            "Expected an update-package PARAM.SFO title not to replace the installed base-game title.");
        Assert(updatedRogue.Version == "01.03",
            "Expected the installed dev_hdd0 update version to override the older disc PARAM.SFO version.");
        Directory.Delete(updateFolder, recursive: true);

        var mismatchedRoot = Path.Combine(
            env.TempRoot,
            "mismatched-title-id");
        var mismatchedConfig = Path.Combine(
            mismatchedRoot,
            "config");
        var mismatchedGame = Path.Combine(
            env.TempRoot,
            "games",
            "BLUS39999");
        Directory.CreateDirectory(mismatchedConfig);
        Directory.CreateDirectory(Path.Combine(
            mismatchedGame,
            "PS3_GAME"));
        File.WriteAllText(Path.Combine(
            mismatchedRoot,
            "rpcs3.exe"), string.Empty);
        File.WriteAllText(Path.Combine(
            mismatchedConfig,
            "games.yml"),
            $"BLUS39999: \"{EscPath(mismatchedGame)}\"{Environment.NewLine}");
        SfoWrite(Path.Combine(
            mismatchedGame,
            "PS3_GAME",
            "PARAM.SFO"), new Dictionary<string, string>
            {
                ["TITLE"] = "Wrong game metadata",
                ["TITLE_ID"] = "BLUS30000",
                ["APP_VER"] = "01.00"
            });
        var mismatchedInstall = Rpcs3InstallService.FromExe(
            Path.Combine(
                mismatchedRoot,
                "rpcs3.exe"));
        var mismatchedResult = new GameScanner().ScanInfo(
            mismatchedInstall);
        Assert(mismatchedResult.Games.Count == 0 &&
               mismatchedResult.Diags.Any(diagnostic =>
                   diagnostic.Code == ScanCode.GameBad &&
                   diagnostic.SrcKind == ScanSrc.ParamSfo &&
                   diagnostic.SrcPath == mismatchedGame),
            "Expected a PARAM.SFO TITLE_ID mismatch to exclude the untrusted game entry and emit a diagnostic.");

        var portableRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-portable");
        var portableGameFolder = Path.Combine(portableRpcs3Root, "portable", "dev_hdd0", "game", "NPEB00002");
        Directory.CreateDirectory(Path.Combine(portableRpcs3Root, "portable", "config"));
        Directory.CreateDirectory(portableGameFolder);
        File.WriteAllText(Path.Combine(portableRpcs3Root, "rpcs3.exe"), string.Empty);
        SfoWrite(Path.Combine(portableGameFolder, "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = "Portable HDD Game",
            ["TITLE_ID"] = "NPEB00002",
            ["APP_VER"] = "02.00"
        });

        var portableInstall = Rpcs3InstallService.FromExe(Path.Combine(portableRpcs3Root, "rpcs3.exe"));
        Assert(portableInstall.CfgPath == Path.Combine(portableRpcs3Root, "portable", "config"),
            "Expected config path to follow RPCS3's <RPCS3 folder>\\portable\\config contract.");
        Assert(portableInstall.CustPath == Path.Combine(portableRpcs3Root, "portable", "config", "custom_configs"),
            "Expected custom configs to follow RPCS3's portable data root.");
        Assert(portableInstall.PatPath == Path.Combine(portableRpcs3Root, "portable", "patches"),
            "Expected imported_patch.yml to follow RPCS3's portable patches path.");
        var portableGames = new GameScanner().Scan(portableInstall);
        Assert(portableGames.Any(game => game.Serial == "NPEB00002"), "Expected portable dev_hdd0 game.");

        var legacyRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-legacy-games-yml");
        var legacyConfigPath = Path.Combine(legacyRpcs3Root, "config");
        var legacyGameFolder = Path.Combine(env.TempRoot, "legacy-games", "BLUS00003");
        Directory.CreateDirectory(legacyConfigPath);
        Directory.CreateDirectory(Path.Combine(legacyGameFolder, "PS3_GAME"));
        File.WriteAllText(Path.Combine(legacyRpcs3Root, "rpcs3.exe"), string.Empty);
        SfoWrite(Path.Combine(legacyGameFolder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
        {
            ["TITLE"] = "Legacy Root Games YML",
            ["TITLE_ID"] = "BLUS00003",
            ["APP_VER"] = "01.00"
        });
        File.WriteAllText(Path.Combine(legacyRpcs3Root, "games.yml"), $"BLUS00003: \"{EscPath(legacyGameFolder)}\"{Environment.NewLine}");
        var legacyInstall = Rpcs3InstallService.FromExe(Path.Combine(legacyRpcs3Root, "rpcs3.exe"));
        Assert(new GameScanner().Scan(legacyInstall).Any(game => game.Serial == "BLUS00003"),
            "Expected legacy root-level games.yml to be scanned for older RPCS3 layouts.");

        var missingConfigRoot = Path.Combine(env.TempRoot, "rpcs3-missing-config");
        Directory.CreateDirectory(missingConfigRoot);
        File.WriteAllText(Path.Combine(missingConfigRoot, "rpcs3.exe"), string.Empty);
        var missingConfigInstall = Rpcs3InstallService.FromExe(Path.Combine(missingConfigRoot, "rpcs3.exe"));
        Assert(new GameScanner().Scan(missingConfigInstall).Count == 0,
            "Expected missing config folder to produce an empty game list without crashing.");

        File.WriteAllText(Path.Combine(env.CfgPath, "games.yml"), "BLUS31465: [broken");
        var corruptGamesYmlScan = new GameScanner().Scan(env.Install);
        Assert(corruptGamesYmlScan.Any(game => game.Serial == "NPUB00001"),
            "Expected corrupt games.yml not to block dev_hdd0 scanning.");
        File.WriteAllText(Path.Combine(env.CfgPath, "games.yml"), $"BLUS31465: \"{EscPath(env.GameDir)}\"{Environment.NewLine}");

        var largeRpcs3Root = Path.Combine(env.TempRoot, "rpcs3-large-list");
        var largeConfigPath = Path.Combine(largeRpcs3Root, "config");
        var largeGamesRoot = Path.Combine(env.TempRoot, "large-games");
        Directory.CreateDirectory(largeConfigPath);
        Directory.CreateDirectory(largeGamesRoot);
        File.WriteAllText(Path.Combine(largeRpcs3Root, "rpcs3.exe"), string.Empty);
        var largeGamesYml = new StringBuilder();
        const int largeGameCount = 150;
        for (var index = 0; index < largeGameCount; index++)
        {
            var serial = $"BLUS{index:00000}";
            var folder = Path.Combine(largeGamesRoot, serial);
            Directory.CreateDirectory(Path.Combine(folder, "PS3_GAME"));
            SfoWrite(Path.Combine(folder, "PS3_GAME", "PARAM.SFO"), new Dictionary<string, string>
            {
                ["TITLE"] = $"Large List Game {index}",
                ["TITLE_ID"] = serial,
                ["APP_VER"] = "01.00"
            });
            largeGamesYml.AppendLine(CultureInfo.InvariantCulture, $"{serial}: \"{EscPath(folder)}\"");
        }
        File.WriteAllText(Path.Combine(largeConfigPath, "games.yml"), largeGamesYml.ToString());
        var largeInstall = Rpcs3InstallService.FromExe(Path.Combine(largeRpcs3Root, "rpcs3.exe"));
        Assert(new GameScanner().Scan(largeInstall).Count == largeGameCount,
            "Expected large games list to scan completely.");

        var truncatedSfo = Path.Combine(env.TempRoot, "truncated.sfo");
        File.WriteAllBytes(truncatedSfo, [0x00, 0x50, 0x53, 0x46]);
        Assert(SfoRead.TryRead(truncatedSfo) is null, "Expected truncated PARAM.SFO to fail safely.");

        var invalidOffsetSfo = Path.Combine(env.TempRoot, "invalid-offset.sfo");
        BadSfo(invalidOffsetSfo);
        Assert(SfoRead.TryRead(invalidOffsetSfo) is null, "Expected invalid PARAM.SFO offsets to fail safely.");

        return new ScanStat(rogue);
    }

    static CatState Cat_All(SmokeEnv env)
    {
        var wiki = WikiSvc.Load(env.WikiPath);
        Assert(wiki.Find("BLUS31465") is not null, "Expected BLUS31465 wiki recommendation.");
        Assert(wiki.Find("BLES02061") is not null, "Expected second gameid serial to be indexed.");
        Assert(wiki.Find("NPUB31547") is not null, "Expected serial from a second gameid template to be indexed.");

        var currentRpcS3Changes = wiki.Find("BLUS31465")!.Changes;
        Assert(currentRpcS3Changes.Any(change =>
                   change.Section == "Core" &&
                   change.Key == "Disable SPU GETLLAR Spin Optimization" &&
                   change.Value == "true") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Core" &&
                   change.Key == "RSX FIFO Fetch Accuracy" &&
                   change.Value == "Ordered & Atomic") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Core" &&
                   change.Key == "Accurate PPU 128-byte Reservation Op Max Length" &&
                   change.Value == "-1") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Video" &&
                   change.Key == "Handle RSX Memory Tiling" &&
                   change.Value == "true") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Video" &&
                   change.Key == "Relaxed ZCULL Sync" &&
                   change.Value == "true") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Video" &&
                   change.Key == "Accurate ZCULL stats" &&
                   change.Value == "false") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Video" &&
                   change.Key == "Texture LOD Bias Addend" &&
                   change.Value == "-1.5") &&
               currentRpcS3Changes.Any(change =>
                   change.Section == "Miscellaneous" &&
                   change.Key == "Silence All Logs" &&
                   change.Value == "true"),
            "Expected current RPCS3-backed Wiki configuration names and composite ZCULL values to be preserved.");

        var conflictingSerialWikiPath = Path.Combine(
            env.TempRoot,
            "conflicting-serial-wiki.xml");
        File.WriteAllText(
            conflictingSerialWikiPath,
            "<mediawiki>" +
            "<page><title>First title</title><revision><text>{{gameid|BLUS30000}}\n{{config\n|write color buffers = On\n}}</text></revision></page>" +
            "<page><title>Second title</title><revision><text>{{gameid|BLUS30000}}\n{{config\n|write color buffers = Off\n}}</text></revision></page>" +
            "</mediawiki>");
        MustFail<InvalidDataException>(
            () => WikiSvc.Load(conflictingSerialWikiPath),
            "Expected conflicting Wiki pages for one serial to be rejected instead of silently using the last page.");

        var conflictingSettingWikiPath = Path.Combine(
            env.TempRoot,
            "conflicting-setting-wiki.xml");
        File.WriteAllText(
            conflictingSettingWikiPath,
            "<mediawiki><page><title>Conflicting setting</title><revision><text>" +
            "{{gameid|BLUS30001}}\n{{config\n|write color buffers = On\n|write color buffers = Off\n}}" +
            "</text></revision></page></mediawiki>");
        MustFail<InvalidDataException>(
            () => WikiSvc.Load(conflictingSettingWikiPath),
            "Expected conflicting duplicate Wiki config keys to be rejected instead of silently using the last value.");

        var catalogPath = Path.Combine(env.TempRoot, "cat.json");
        R3Config.Svc.RecSvc.Save(catalogPath, "2099-01-02", wiki.Rec);
        var catalogInfoService = new R3Config.Svc.CatSvc();
        var catalog = catalogInfoService.LoadCat(catalogPath);
        Assert(catalog.DataSrc == "Built-in", "Expected built-in catalog source.");
        Assert(catalog.DataVer == "2099-01-02", "Expected catalog data version.");
        Assert(catalog.Info.RecNum == catalog.Count, "Expected catalog info count to be computed from loaded recommendations.");
        Assert(catalog.SrcHash.Length == 64, "Expected catalog source hash to be a SHA-256 hex string.");
        Assert(catalog.GenAt is not null, "Expected catalog generated timestamp to be available.");
        Assert(catalog.Find("BLUS31465") is not null, "Expected catalog lookup to work without Wiki XML.");

        try
        {
            catalogInfoService.LoadCat(Path.Combine(env.TempRoot, "missing-cat.json"));
            throw new InvalidOperationException("Expected missing recommendation catalog to fail safely.");
        }
        catch (MissingCatalogException ex)
        {
            Assert(ex.Message == "Recommendation data is missing", "Expected missing catalog error message contract.");
        }

        var invalidCatalogPath = Path.Combine(env.TempRoot, "invalid-cat.json");
        File.WriteAllText(invalidCatalogPath, "{ invalid json");
        try
        {
            catalogInfoService.LoadCat(invalidCatalogPath);
            throw new InvalidOperationException("Expected invalid recommendation catalog to fail safely.");
        }
        catch (InvalidCatalogException ex)
        {
            Assert(ex.Message == "Recommendation data is invalid", "Expected invalid catalog error message contract.");
        }

        var oversizedCatalogPath = Path.Combine(
            env.TempRoot,
            "oversized-cat.json");
        using (var oversizedCatalog = new FileStream(
                   oversizedCatalogPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedCatalog.SetLength(R3Limits.PatCatBy + 1);
        }

        MustFail<InvalidCatalogException>(
            () => catalogInfoService.LoadCat(oversizedCatalogPath),
            "Expected an oversized recommendation catalog to fail before an unbounded whole-file read.");

        var duplicateJsonKeyCatalogPath = Path.Combine(env.TempRoot, "duplicate-json-key-cat.json");
        File.WriteAllText(duplicateJsonKeyCatalogPath, DupKeyJs());
        try
        {
            catalogInfoService.LoadCat(duplicateJsonKeyCatalogPath);
            throw new InvalidOperationException("Expected duplicate JSON object keys to fail safely.");
        }
        catch (InvalidCatalogException ex)
        {
            Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate JSON key catalog error message contract.");
        }

        var duplicateSerialCatalogPath = Path.Combine(env.TempRoot, "duplicate-serial-cat.json");
        File.WriteAllText(duplicateSerialCatalogPath, DupSerJs());
        try
        {
            catalogInfoService.LoadCat(duplicateSerialCatalogPath);
            throw new InvalidOperationException("Expected duplicate recommendation serials to fail safely.");
        }
        catch (InvalidCatalogException ex)
        {
            Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate serial catalog error message contract.");
        }

        var duplicateSettingCatalogPath = Path.Combine(env.TempRoot, "duplicate-setting-cat.json");
        File.WriteAllText(duplicateSettingCatalogPath, DupSetJs());
        try
        {
            catalogInfoService.LoadCat(duplicateSettingCatalogPath);
            throw new InvalidOperationException("Expected duplicate recommendation settings to fail safely.");
        }
        catch (InvalidCatalogException ex)
        {
            Assert(ex.Message == "Recommendation data is invalid", "Expected duplicate setting catalog error message contract.");
        }

        var wrongRiskCatalogPath = Path.Combine(env.TempRoot, "wrong-risk-cat.json");
        File.WriteAllText(wrongRiskCatalogPath, RiskJson());
        var wrongRiskCatalog = catalogInfoService.LoadCat(wrongRiskCatalogPath);
        Assert(wrongRiskCatalog.Info.RecNum == 1, "Expected stored RecommendationCount to be ignored and recomputed.");
        Assert(wrongRiskCatalog.Find("TEST00001")!.Changes.Single().Risk == RecRisk.PerfCost,
            "Expected catalog load to reapply RecommendationRiskPolicy instead of trusting stored Risk.");

        var recommendation = catalog.Find("BLUS31465")!;
        Assert(recommendation.Changes.Any(change => change.Key == "PPU Decoder" && change.Value == "Interpreter (static)"),
            "Expected PPU Decoder recommendation.");
        Assert(recommendation.Changes.Any(change => change.Key == "PPU Decoder" && change.Risk == RecRisk.PerfCost),
            "Expected interpreter decoder changes to be classified as PerformanceCost.");
        Assert(recommendation.Changes.Any(change => change.Key == "SPU XFloat Accuracy" && change.Value == "Accurate"),
            "Expected the current RPCS3 SPU XFloat Accuracy key.");
        Assert(recommendation.Changes.Any(change => change.Key == "Minimum Scalable Dimension" && change.Value == "256"),
            "Expected square resolution threshold values to be normalized to RPCS3's scalar dimension.");
        Assert(recommendation.Changes.Any(change =>
                change.Key == "Minimum Scalable Dimension" &&
                change.Notes == "Fixes bloom See game patches and RPCS3 issue #13546."),
            "Expected Wiki and HTML note markup to be cleaned.");
        Assert(recommendation.Changes.Any(change => change.Key == "Anisotropic Filter Override" && change.Value == "0"),
            "Expected anisotropic Auto to be normalized to RPCS3's numeric value.");
        Assert(recommendation.Changes.Any(change => change.Key == "Frame limit" && change.Value == "Off"),
            "Expected enum Off to remain Off instead of being converted to a boolean.");
        Assert(recommendation.Changes.Any(change => change.Key == "VSync Mode" && change.Value == "Full"),
            "Expected the current RPCS3 VSync Mode key.");
        Assert(recommendation.Changes.Any(change =>
                change.Key == "Vulkan.Asynchronous Texture Streaming" &&
                change.Value == "true"),
            "Expected the current nested Vulkan asynchronous texture streaming key.");
        Assert(recommendation.Changes.All(change => change.Key != "Preferred SPU Threads"),
            "Expected ambiguous Preferred SPU Threads ranges to be rejected instead of guessed.");
        Assert(recommendation.Changes.Any(change => change.Key == "Write Color Buffers" && change.Value == "true"),
            "Expected Write Color Buffers recommendation.");
        Assert(recommendation.Changes.Any(change => change.Key == "Write Color Buffers" && change.Risk == RecRisk.Rec),
            "Expected safe visual fix to remain Recommended.");
        Assert(recommendation.Changes.Any(change => change.Key == "Resolution Scale" && change.Value == "150"),
            "Expected percentage values to be normalized.");
        Assert(recommendation.Changes.Any(change => change.Key == "Resolution Scale" && change.Risk == RecRisk.Dep),
            "Expected resolution scale to be classified as HardwareDependent.");
        Assert(recommendation.Changes.Any(change => change.Key == "Vblank Rate" && change.Value == "120" && change.Risk == RecRisk.Timing),
            "Expected Vblank Rate to be classified as ManualTiming.");
        Assert(recommendation.Changes.Any(change => change.Key == "Read Color Buffers" && change.Risk == RecRisk.Cond),
            "Expected may-cause notes to be classified as Conditional.");
        Assert(recommendation.Changes.Any(change => change.Key == "Write Depth Buffer" && change.Risk == RecRisk.PatDep),
            "Expected patch-dependent notes to be classified as PatchDependent.");
        Assert(recommendation.Changes.Any(change => change.Key == "Strict Rendering Mode" && change.Risk == RecRisk.PerfCost),
            "Expected performance-cost notes to be classified as PerformanceCost.");

        return new CatState(recommendation, catalog);
    }

}
