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
    static void AppPat_InstallPathContracts()
    {
        var installRoot = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigInstallSmoke",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(installRoot);

        try
        {
            var exePath = Path.Combine(installRoot, "rpcs3.exe");
            File.WriteAllBytes(exePath, []);

            var install = Rpcs3InstallService.FromExe(exePath);
            var expectedConfigPath = Path.Combine(installRoot, "config");
            var expectedCustomConfigsPath = Path.Combine(expectedConfigPath, "custom_configs");

            Assert(string.Equals(
                    install.CfgPath,
                    expectedConfigPath,
                    StringComparison.OrdinalIgnoreCase),
                "Expected Rpcs3InstallService to anchor config to root config.");
            Assert(string.Equals(
                    install.CustPath,
                    expectedCustomConfigsPath,
                    StringComparison.OrdinalIgnoreCase),
                "Expected Rpcs3InstallService to anchor custom_configs below root config.");
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    static void AppPat_BundledCatalogContracts()
    {
        var repoRoot = RepoRoot();
        var appRoot = Path.Combine(repoRoot, "App");
        var bundledCanaryDataPath = Path.Combine(appRoot, "Data", "can.yml");
        var bundledCheatDataPath = Path.Combine(appRoot, "Data", "art.yml");

        var bundledCanaryText = File.ReadAllText(bundledCanaryDataPath);
        Assert(bundledCanaryText.StartsWith("# R3Config bundled RPCS3 Canary patches", StringComparison.Ordinal),
            "Expected bundled Canary data source marker.");
        Assert(bundledCanaryText.Contains("Version: 1.2", StringComparison.Ordinal),
            "Expected bundled Canary data to use RPCS3 patch version 1.2.");
        Assert(bundledCanaryText.Contains("PPU-", StringComparison.Ordinal),
            "Expected bundled Canary data to include PPU patch entries.");

        var normalizedCanary = new R3Config.Svc.CanaryPatchImportService().Norm(bundledCanaryText);
        Assert(normalizedCanary.IsOk, "Expected bundled Canary data to normalize without duplicate-name failures: " + normalizedCanary.Error);

        var canaryRead = new R3Config.Svc.ReadSvc().ReadText(normalizedCanary.NormText, isCanarySource: true);
        Assert(canaryRead.IsOk, "Expected bundled Canary data to be parseable after normalization: " + canaryRead.Error);
        Assert(canaryRead.Catalog.Entries.Count > 0, "Expected bundled Canary data to include patch catalog entries.");
        Assert(canaryRead.Catalog.Entries
                .Select(entry => $"{entry.HashKind}|{entry.Hash}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 86,
            "Expected the bundled Canary database to retain all 86 executable Hash keys from the supplied RPCS3 Wiki snapshot.");
        Assert(canaryRead.Catalog.Entries.All(entry => entry.Risk == PatRisk.Canary),
            "Expected bundled Canary entries to be classified as Canary.");
        Assert(canaryRead.Catalog.Entries.All(entry => entry.Class == PatClass.Canary),
            "Expected every bundled Canary entry to remain classified for separate Canary handling and explicit user activation.");

        var compatibilityService = new R3Config.Svc.PatchMatchService();
        foreach (var entry in canaryRead.Catalog.Entries.Where(entry =>
                     entry.HashKind is HashKind.Ppu or HashKind.Spu &&
                     entry.NormHash is not null))
        {
            foreach (var target in entry.GameTgt)
            {
                var serial = PatVer.IsAll(target.Serial)
                    ? "BLUS00001"
                    : target.Serial;
                var version = target.Versions
                    .Select(PatVer.Norm)
                    .FirstOrDefault(value =>
                        value is not null &&
                        !PatVer.IsAll(value)) ?? "01.00";
                var expectedHash = entry.NormHash!;
                var game = new Rpcs3GameDescriptor(
                    serial,
                    target.Title,
                    Path.Combine(@"C:\Games", serial),
                    version,
                    null,
                    null,
                    HCanPpu: entry.HashKind == HashKind.Ppu
                        ? [expectedHash]
                        : [],
                    HCanSpu: entry.HashKind == HashKind.Spu
                        ? [expectedHash]
                        : []);
                var match = compatibilityService.Match(
                    entry,
                    [game]);

                Assert(
                    match.Status == MatStat.Match,
                    $"Expected Canary target {serial} {version} to match its declared {entry.HashKind} key {entry.Hash}.");
            }
        }

        var normalizedSpuCanary = new R3Config.Svc.CanaryPatchImportService().Norm(
            "Version: 1.2\n" +
            "SPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:\n" +
            "  \"SPU Canary\":\n" +
            "    Games:\n" +
            "      \"SPU Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n");
        Assert(normalizedSpuCanary.IsOk,
            "Expected the custom import path to support SPU executable hashes.");
        var spuCanaryRead = new R3Config.Svc.ReadSvc().ReadText(
            normalizedSpuCanary.NormText,
            isCanarySource: true);
        Assert(spuCanaryRead.IsOk &&
               spuCanaryRead.Catalog.Entries.Single().HashKind == HashKind.Spu,
            "Expected imported SPU Canary data to remain SPU-scoped.");

        var bundledCheatRead = new R3Config.Svc.ReadSvc().Read(
            bundledCheatDataPath,
            PatSrc.Artemis);
        Assert(bundledCheatRead.IsOk,
            "Expected the bundled converted cheat library to be readable: " + bundledCheatRead.Error);
        Assert(bundledCheatRead.Catalog.Entries.Count == 1471,
            "Expected the retained Artemis patch collection to expose its complete 1471-entry accepted inventory.");
        Assert(bundledCheatRead.Catalog.Entries.All(entry => entry.Risk == PatRisk.Cheat),
            "Expected every bundled expansion entry to be handled as a user-selected cheat.");
    }

    // God of War: Ascension Unlock FPS: the Canary option stays actionable,
    // the exact Artemis equivalent stays visible but enable-blocked, and a
    // legacy enabled Artemis copy can still be disabled.
    static void AppPat_CrossChannelUnlockFps(
        R3Config.Mdl.PatCat crossChannelMerge,
        Rpcs3GameDescriptor gowAscensionDiscoveredGame)
    {
        var crossChannelPreview = new PatPrev();
        var crossChannelMatches = new PatchMatchService().Match(
            crossChannelMerge,
            gowAscensionDiscoveredGame);
        crossChannelPreview.Load(crossChannelMatches, title: null);
        var canaryUnlockFps = crossChannelPreview.CanItems.Single(item =>
            item.Title == "Unlock FPS");
        var artemisUnlockFps = crossChannelPreview.ChtItems.Single(item =>
            item.Title == "Unlock FPS");
        var enabledArtemisUnlockFps = new PatItVm(
            artemisUnlockFps.Match,
            isEnabled: true);
        Assert(
            canaryUnlockFps.Match.Patch.Source == PatSrc.Can &&
            canaryUnlockFps.CanOn &&
            artemisUnlockFps.Match.Patch.Source == PatSrc.Artemis &&
            artemisUnlockFps.Decision.Code == PatchCompatibilityReasonCode.CanaryEquivalent &&
            !artemisUnlockFps.CanOn &&
            !artemisUnlockFps.CanSel &&
            artemisUnlockFps.CompatibilityTip ==
                UiText.GetText("Patch.Direct.CanaryEquivalent") &&
            enabledArtemisUnlockFps.CanOff &&
            enabledArtemisUnlockFps.CanSel &&
            !enabledArtemisUnlockFps.CanOn,
            "Expected God of War Ascension Unlock FPS to remain actionable through Canary, keep the exact Artemis equivalent visible but blocked from enable, and preserve legacy Artemis disable recovery.");
    }
}
