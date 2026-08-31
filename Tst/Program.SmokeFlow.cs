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
    static void Flow_Cat(SmokeEnv env, string patchYmlPath)
    {
        var flow = TestFlow();
        var importedPatchPath = Path.Combine(env.FakeRoot, "patches", "imported_patch.yml");

        File.WriteAllText(patchYmlPath, PatYaml());
        if (File.Exists(importedPatchPath))
        {
            File.Delete(importedPatchPath);
        }

        var officialOnly = flow.LoadPat(env.Install);
        Assert(officialOnly.Cat is not null, "Expected patch.yml-only AppFlow load to return a catalog.");
        Assert(string.IsNullOrWhiteSpace(officialOnly.Error), "Expected patch.yml-only AppFlow load not to report an error: " + officialOnly.Error);
        Assert(officialOnly.Cat!.Entries.Any(patch => patch.Title == "Skip Intro Videos"),
            "Expected patch.yml-only AppFlow load to include official/local patches.");
        var officialStatus = officialOnly.SrcStat.Single(source =>
            source.Source == PatSrc.Official && source.Name == "patch.yml");
        Assert(officialStatus is { Exists: true, Loaded: true, EntNum: > 0 },
            "Expected source diagnostics to report a loaded official patch.yml with its entry count.");

        File.Delete(patchYmlPath);
        File.WriteAllText(importedPatchPath, ImpYaml());
        var importedOnly = flow.LoadPat(env.Install);
        Assert(importedOnly.Cat is not null, "Expected imported_patch.yml-only AppFlow load to return a catalog.");
        Assert(string.IsNullOrWhiteSpace(importedOnly.Error), "Expected imported_patch.yml-only AppFlow load not to report patch.yml missing.");
        Assert(importedOnly.Cat!.Entries.Any(patch => patch.Title == "Imported Canary Display Patch" && patch.Risk == PatRisk.Canary),
            "Expected imported_patch.yml entries to load as Canary patches.");
        Assert(importedOnly.SrcStat.Single(source =>
                source.Source == PatSrc.Official && source.Name == "patch.yml") is
        { Exists: false, Loaded: false, EntNum: 0 },
            "Expected source diagnostics to distinguish a missing optional patch.yml from an invalid catalog.");
        Assert(importedOnly.SrcStat.Single(source =>
                source.Source == PatSrc.Imported && source.Name == "imported_patch.yml") is
        { Exists: true, Loaded: true, EntNum: > 0 },
            "Expected source diagnostics to report imported_patch.yml independently.");

        var importedBeforeReplay = File.ReadAllBytes(importedPatchPath);
        var importResult = flow.ImpCan(env.Install, ImpYaml());
        Assert(importResult.IsOk &&
               importResult.BkpPath is null &&
               File.ReadAllBytes(importedPatchPath).SequenceEqual(importedBeforeReplay),
            "Expected an already-present Canary import to remain byte-idempotent without a backup: " + importResult.Error);

        File.WriteAllText(patchYmlPath, PatYaml());
        var merged = flow.LoadPat(env.Install);
        Assert(merged.Cat is not null, "Expected merged AppFlow load to return a catalog.");
        Assert(merged.Cat!.Entries.Any(patch => patch.Title == "Skip Intro Videos"),
            "Expected merged AppFlow load to include patch.yml entries.");
        Assert(merged.Cat.Entries.Any(patch => patch.Title == "Imported Canary Display Patch" && patch.Risk == PatRisk.Canary),
            "Expected merged AppFlow load to include imported_patch.yml entries as Canary.");

        File.WriteAllText(patchYmlPath, "Version: [invalid");
        var invalidOfficialWithImported = flow.LoadPat(env.Install);
        Assert(invalidOfficialWithImported.Cat is not null,
            "Expected valid imported_patch.yml to remain visible when patch.yml is invalid.");
        Assert(invalidOfficialWithImported.Cat!.Entries.Any(patch => patch.Title == "Imported Canary Display Patch"),
            "Expected imported patches to be preserved when patch.yml fails to parse.");
        Assert(invalidOfficialWithImported.Error?.Contains("patch.yml", StringComparison.OrdinalIgnoreCase) == true,
            "Expected invalid patch.yml to produce a readable warning while imported patches load.");
        var invalidOfficialStatus = invalidOfficialWithImported.SrcStat.Single(source =>
            source.Source == PatSrc.Official && source.Name == "patch.yml");
        Assert(invalidOfficialStatus is { Exists: true, Loaded: false, EntNum: 0 } &&
               !string.IsNullOrWhiteSpace(invalidOfficialStatus.Diag),
            "Expected source diagnostics to retain the exact reason an existing patch.yml failed to load.");

        File.Delete(patchYmlPath);
        File.Delete(importedPatchPath);
        var none = flow.LoadPat(env.Install);
        Assert(none.Cat is not null && none.Cat.Entries.Any(entry => entry.Source == PatSrc.Can),
            "Expected bundled Canary preview data to remain available when optional RPCS3 patch files are missing.");
        Assert(string.IsNullOrWhiteSpace(none.Error),
            "Expected missing optional RPCS3 patch files not to be reported as corrupt data.");
        Assert(none.SrcStat.Single(source =>
                source.Source == PatSrc.Can && source.Name == "can.yml") is
        { Exists: true, Loaded: true, EntNum: > 0 },
            "Expected source diagnostics to prove the complete bundled Canary database was loaded even without patch.yml.");
        File.WriteAllText(importedPatchPath, ImpDup());
        var duplicateImported = flow.LoadPat(env.Install);
        Assert(duplicateImported.Cat is not null && duplicateImported.Cat.Entries.Any(entry => entry.Source == PatSrc.Can),
            "Expected valid bundled Canary data to stay visible when imported_patch.yml is invalid.");
        Assert(duplicateImported.Error?.Contains("imported_patch.yml", StringComparison.OrdinalIgnoreCase) == true,
            "Expected duplicate imported_patch.yml error to name imported_patch.yml.");
        Assert(duplicateImported.Error?.Contains("Canary", StringComparison.OrdinalIgnoreCase) == true,
            "Expected duplicate imported_patch.yml error to suggest the Canary import tool.");

        Console.WriteLine("AppFlow patch catalog load smoke tests passed.");
    }

    static void R3_Read(string rootOrExePath)
    {
        var exePath = Directory.Exists(rootOrExePath)
            ? Path.Combine(rootOrExePath, "rpcs3.exe")
            : rootOrExePath;
        Assert(File.Exists(exePath), "RPCS3 integration path must contain rpcs3.exe: " + exePath);

        var install = Rpcs3InstallService.FromExe(exePath);
        var officialPath = Path.Combine(install.PatPath, "patch.yml");
        var importedPath = Path.Combine(install.PatPath, "imported_patch.yml");
        var trackedPaths = new[] { officialPath, importedPath, install.PatCfg }
            .ToDictionary(
                path => path,
                path => File.Exists(path)
                    ? (Exists: true,
                       LastWriteUtc: File.GetLastWriteTimeUtc(path),
                       Length: new FileInfo(path).Length,
                       Sha256: FileHash(path))
                    : (Exists: false,
                       LastWriteUtc: DateTime.MinValue,
                       Length: 0L,
                       Sha256: string.Empty),
                StringComparer.OrdinalIgnoreCase);

        var games = new GameScanner().Scan(install);
        Assert(games.Count > 0, "Expected the supplied RPCS3 installation to expose installed games.");

        var patchReader = new R3Config.Svc.ReadSvc();
        var officialCount = 0;
        var officialPrxCount = 0;
        var officialOvlCount = 0;
        if (File.Exists(officialPath))
        {
            var officialRead = patchReader.Read(officialPath, PatSrc.Official);
            Assert(officialRead.IsOk,
                "Expected the supplied RPCS3 patch.yml to parse when present: " + officialRead.Error);
            officialCount = officialRead.Catalog.Entries.Count;
            officialPrxCount = officialRead.Catalog.Entries.Count(entry => entry.HashKind == HashKind.Prx);
            officialOvlCount = officialRead.Catalog.Entries.Count(entry => entry.HashKind == HashKind.Ovl);
        }
        else
        {
            Console.WriteLine(
                "Real RPCS3 installation has no patches\\patch.yml; optional official catalog absence passed.");
        }

        var importedCount = 0;
        if (File.Exists(importedPath))
        {
            var importedRead = patchReader.Read(importedPath, PatSrc.Imported);
            Assert(importedRead.IsOk,
                "Expected the supplied RPCS3 imported_patch.yml to parse when present: " + importedRead.Error);
            importedCount = importedRead.Catalog.Entries.Count;
        }
        else
        {
            Console.WriteLine(
                "Real RPCS3 installation has no patches\\imported_patch.yml; optional imported catalog absence passed.");
        }

        var patchConfig = new CfgRead().Load(install.PatCfg);
        Assert(patchConfig.Documents.Count > 0,
            "Expected patch_config.yml loading to return a valid mapping or the safe empty mapping.");
        if (!File.Exists(install.PatCfg))
        {
            Console.WriteLine(
                "Real RPCS3 installation has no config\\patch_config.yml; safe empty configuration handling passed.");
        }

        var flow = new AppFlow();
        var flowResult = flow.LoadPat(install);
        Assert(flowResult.Cat is not null && flowResult.Cat.Entries.Count > 0,
            "Expected AppFlow to load available RPCS3 and bundled patch sources: " + flowResult.Error);

        var hashProbe = new PatchHashProbeService();
        var enrichedGames = games
            .Select(game => hashProbe.Enrich(install, game))
            .ToArray();
        var hashSample = enrichedGames.FirstOrDefault(game =>
            game.Hashes.Count > 0 ||
            game.SpuList.Count > 0 ||
            game.PrxList.Count > 0 ||
            game.OvlList.Count > 0);
        if (hashSample is not null)
        {
            var preview = new AppFlow().PatPrev(flowResult.Cat!, hashSample);
            Assert(preview.All(match => match.MatSer == hashSample.Serial),
                "Expected real-install preview results to stay scoped to the selected game.");
            Console.WriteLine(
                $"Real hash evidence sample: {hashSample.Serial}, " +
                $"PPU={hashSample.Hashes.Count}, SPU={hashSample.SpuList.Count}, " +
                $"PRX={hashSample.PrxList.Count}, OVL={hashSample.OvlList.Count}, " +
                $"kind={hashSample.HashEvi}, source={hashSample.HashSrc}.");
        }
        else
        {
            Assert(enrichedGames.All(game =>
                    game.Hashes.Count == 0 &&
                    game.SpuList.Count == 0 &&
                    game.PrxList.Count == 0 &&
                    game.OvlList.Count == 0),
                "Expected a real installation without boot evidence to remain consistently HashUnknown.");
            Console.WriteLine(
                "Real RPCS3 installation has no bounded boot/cache Hash evidence; safe HashUnknown handling passed.");
        }

        var builtInCatalog = flow.LoadCat(AppContext.BaseDirectory);
        Assert(builtInCatalog.Cat is not null,
            "Expected the bundled recommendation catalog to load during real-install analysis.");
        var analysisTimer = System.Diagnostics.Stopwatch.StartNew();
        var analysis = flow.DoAsync(exePath, builtInCatalog.Cat!).GetAwaiter().GetResult();
        analysisTimer.Stop();
        var discoveredSerials = games
            .Select(game => game.Serial)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var analyzedInstalledSerials = analysis.Games
            .Where(row => row.InR3)
            .Select(row => row.Game.Serial)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert(
            discoveredSerials.IsSubsetOf(
                analyzedInstalledSerials) &&
            analysis.Games.Count >=
            discoveredSerials.Count,
            "Expected full R3Config analysis to retain every distinct discovered RPCS3 serial while allowing recommendation-only rows and duplicate-install collapse.");
        var realEvaluations = analysis.Games
            .SelectMany(row => flow.PatPrev(
                    analysis.PatchCat.Cat!,
                    row.Game)
                .Select(match => (row.Game, Match: match)))
            .ToArray();
        Assert(realEvaluations.All(item => item.Match.Cmp is not null),
            "Expected every real official/Canary/cheat preview to carry one canonical compatibility decision.");
        var actionableEvaluations = realEvaluations
            .Where(item => PatAct.Can(item.Match))
            .ToArray();
        Assert(actionableEvaluations.All(item =>
                item.Match.Cmp?.CanOn == true &&
                RunKey.TryParse(
                    item.Match.Patch.RunKey,
                    out _)),
            "Expected every real actionable patch to have a valid RPCS3 runtime key and a positive canonical decision.");
        var realExactCount = actionableEvaluations.Count(item =>
            item.Match.Cmp?.Verdict ==
                PatchCompatibilityVerdict.Compat);
        var realRuntimeGuardedCount = actionableEvaluations.Count(item =>
            item.Match.Cmp?.Verdict ==
                PatchCompatibilityVerdict.Guarded);
        var exactSamples = realEvaluations
            .Where(item => item.Match.Cmp?.Verdict ==
                PatchCompatibilityVerdict.Compat)
            .Select(item =>
                $"{item.Game.Serial} {item.Game.Version}: {item.Match.Patch.Title} [{item.Match.Patch.Risk}]")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        foreach (var (path, snapshot) in trackedPaths)
        {
            var exists = File.Exists(path);
            Assert(exists == snapshot.Exists,
                "Read-only integration verification changed RPCS3 file existence: " + path);

            if (!snapshot.Exists)
            {
                continue;
            }

            var currentInfo = new FileInfo(path);
            Assert(currentInfo.LastWriteTimeUtc == snapshot.LastWriteUtc,
                "Read-only integration verification changed RPCS3 timestamp: " + path);
            Assert(currentInfo.Length == snapshot.Length,
                "Read-only integration verification changed RPCS3 file length: " + path);
            Assert(string.Equals(FileHash(path), snapshot.Sha256, StringComparison.Ordinal),
                "Read-only integration verification changed RPCS3 file content: " + path);
        }

        Console.WriteLine(
            $"Real RPCS3 read-only contract passed: {games.Count} games, " +
            $"official={officialCount} (PRX={officialPrxCount}, OVL={officialOvlCount}), " +
            $"imported={importedCount}, merged={flowResult.Cat!.Entries.Count}, " +
            $"actionable={actionableEvaluations.Length} (exact={realExactCount}, runtime-guarded={realRuntimeGuardedCount}), " +
            $"full analysis {analysisTimer.ElapsedMilliseconds} ms.");
        if (exactSamples.Length > 0)
        {
            Console.WriteLine("Real exact patch samples: " + string.Join(" | ", exactSamples));
        }
    }

}
