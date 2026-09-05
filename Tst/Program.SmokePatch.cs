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
    static void Pat_All(SmokeEnv env, Rpcs3GameDescriptor rogue)
    {
        var patchYmlPath = Path.Combine(env.FakeRoot, "patches", "patch.yml");
        File.WriteAllText(patchYmlPath, PatYaml());
        var patchConfigPreviewPath = Path.Combine(env.CfgPath, "patch_config.yml");
        var patchLastWrite = File.GetLastWriteTimeUtc(patchYmlPath);

        Can_Imp(env, patchYmlPath);

        var patchReader = new R3Config.Svc.ReadSvc();
        var patchRead = patchReader.Read(patchYmlPath);
        Assert(patchRead.IsOk, $"Expected valid patch.yml to read successfully: {patchRead.Error}");
        Assert(patchRead.Catalog.Version == "1.2", "Expected patch catalog version.");
        Assert(patchRead.Catalog.Entries.Count == 8, "Expected multiple patch entries to be parsed.");
        Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "60 FPS" && patch.Risk == PatRisk.Fps60),
            "Expected 60 FPS patch to be classified as Fps60.");
        Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Infinite Health Cheat" && patch.Risk == PatRisk.Cheat),
            "Expected cheat patch to be classified as Cheat.");
        Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "SPU Timing Patch" && patch.Risk != PatRisk.Canary),
            "Expected SPU game patch from patch.yml not to be classified as Canary by title text.");
        Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Experimental Test Patch" && patch.Risk == PatRisk.Exp),
            "Expected experimental patch to be classified as Experimental.");
        Assert(patchRead.Catalog.Entries.Any(patch => patch.Title == "Unknown Memory Patch" && patch.Risk == PatRisk.Unknown),
            "Expected unknown patch to remain Unknown.");
        Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").Serials.Contains("BLUS31465"),
            "Expected patch game serials to be parsed.");
        Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").GameVer.Contains("01.02"),
            "Expected patch game versions to be parsed.");
        Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "Skip Intro Videos").HashKind == HashKind.Ppu,
            "Expected PPU hash kind.");
        Assert(patchRead.Catalog.Entries.Single(patch => patch.Title == "SPU Timing Patch").HashKind == HashKind.Spu,
            "Expected SPU hash kind.");

        const string ExactHashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string ExactHashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string ExactHashC = "cccccccccccccccccccccccccccccccccccccccc";
        const string ExactHashD = "dddddddddddddddddddddddddddddddddddddddd";

        var patchHashProbe = new PatchHashProbeService();
        var noHashProbeResult = patchHashProbe.Probe(env.Install, rogue);
        Assert(!noHashProbeResult.HasHash &&
               noHashProbeResult.EviKind == HashEvi.None,
            "Expected missing RPCS3 log metadata to keep patch hash unknown.");
        var noHashGame = patchHashProbe.Enrich(env.Install, rogue);
        Assert(noHashGame.PpuHash is null &&
               noHashGame.SpuHash is null &&
               noHashGame.HashEvi == HashEvi.None,
            "Expected missing patch hash probe data not to invent hashes.");

        var gameCacheDir = Path.Combine(env.FakeRoot, "cache", rogue.Serial);
        Directory.CreateDirectory(gameCacheDir);
        Directory.CreateDirectory(Path.Combine(
            gameCacheDir,
            "ppu-0000000000000000000000000000000000000000-EBOOT.BIN"));
        var zeroCacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(zeroCacheHashGame.PpuHash is null && zeroCacheHashGame.SpuHash is null,
            "Expected all-zero placeholder PPU cache hash not to be treated as ready.");
        Directory.Delete(gameCacheDir, recursive: true);

        Directory.CreateDirectory(gameCacheDir);
        var base57CacheEntry = Path.Combine(
            gameCacheDir,
            "ppu-QoiFRPF8j00N996aQMthtWqEUG0Y-EBOOT.BIN");
        Directory.CreateDirectory(base57CacheEntry);
        var base57CacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(base57CacheHashGame.PpuHash ==
                   "728fc4496a4c347a654ab54d3e3f0e44ed3c9af6" &&
               base57CacheHashGame.HashEvi == HashEvi.Cache,
            "Expected RPCS3's Base57 PPU cache directory to become canonical 40-hex compatibility evidence.");
        Directory.Delete(gameCacheDir, recursive: true);

        Directory.CreateDirectory(gameCacheDir);
        var exactCacheEntry = Path.Combine(
            gameCacheDir,
            $"ppu-{ExactHashD}-EBOOT.BIN");
        Directory.CreateDirectory(exactCacheEntry);
        var cacheHashGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(cacheHashGame.PpuHash == ExactHashD &&
               cacheHashGame.HashEvi == HashEvi.Cache &&
               cacheHashGame.HashSrc == exactCacheEntry,
            "Expected PatchHashProbeService to read an exact 40-hex PPU hash from the per-game cache.");
        Directory.Delete(gameCacheDir, recursive: true);

        var metadataZeroHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
        {
            PpuHash = "0000000000000000000000000000000000000000"
        });
        Assert(!metadataZeroHashResult.HasHash,
            "Expected all-zero game metadata PPU hash not to be treated as ready.");

        var shortMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
        {
            PpuHash = new string('a', 39)
        });
        Assert(!shortMetadataHashResult.HasHash,
            "Expected a 39-character game metadata hash to be rejected.");

        var longMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
        {
            PpuHash = new string('a', 41)
        });
        Assert(!longMetadataHashResult.HasHash,
            "Expected a 41-character game metadata hash to be rejected.");

        var nonHexMetadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
        {
            PpuHash = "gggggggggggggggggggggggggggggggggggggggg"
        });
        Assert(!nonHexMetadataHashResult.HasHash,
            "Expected a non-hex game metadata hash to be rejected.");

        var metadataHashResult = new PatchHashProbeService().Probe(env.Install, rogue with
        {
            PpuHash = ExactHashA
        });
        Assert(metadataHashResult.HasHash &&
               metadataHashResult.PpuHash == ExactHashA &&
               metadataHashResult.EviKind == HashEvi.Meta,
            "Expected an exact existing game metadata hash to be used before log probing.");

        var rootLogPath = Path.Combine(env.FakeRoot, "RPCS3.log");

        File.WriteAllText(
            rootLogPath,
            $"License maintenance referenced {rogue.Serial}.{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");
        var maintenanceMentionGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(maintenanceMentionGame.PpuHash is null,
            "Expected a serial mention outside a SYS: Serial session not to establish hash evidence.");

        File.WriteAllText(
            rootLogPath,
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"No executable hash was emitted for this session.{Environment.NewLine}" +
            $"SYS: Serial: BLUS99999{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashB}{Environment.NewLine}");
        var crossedSessionGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(crossedSessionGame.PpuHash is null,
            "Expected the hash probe not to cross into another game's SYS: Serial session.");

        File.WriteAllText(
            rootLogPath,
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");
        var probedGame = new PatchHashProbeService().Enrich(env.Install, rogue);
        Assert(probedGame.PpuHash == ExactHashA &&
               probedGame.HashEvi == HashEvi.PlainLog &&
               probedGame.HashSrc == rootLogPath,
            "Expected PatchHashProbeService to read an exact PPU hash from the matching RPCS3.log session.");

        var movedLogRoot = Path.Combine(env.TempRoot, "rpcs3-log-folder");
        Directory.CreateDirectory(Path.Combine(movedLogRoot, "config"));
        Directory.CreateDirectory(Path.Combine(movedLogRoot, "log"));
        File.WriteAllText(Path.Combine(movedLogRoot, "rpcs3.exe"), string.Empty);
        var movedPlainLogPath = Path.Combine(movedLogRoot, "log", "RPCS3.log");
        File.WriteAllText(
            movedPlainLogPath,
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashB}{Environment.NewLine}");
        var movedLogInstall = Rpcs3InstallService.FromExe(
            Path.Combine(movedLogRoot, "rpcs3.exe"));
        var movedLogGame = new PatchHashProbeService().Enrich(movedLogInstall, rogue);
        Assert(movedLogGame.PpuHash == ExactHashB &&
               movedLogGame.HashEvi == HashEvi.PlainLog,
            "Expected PatchHashProbeService to read PPU hash from the modern log/RPCS3.log path.");

        var largeLogRoot = Path.Combine(env.TempRoot, "large-rpcs3-log");
        Directory.CreateDirectory(Path.Combine(largeLogRoot, "config"));
        Directory.CreateDirectory(Path.Combine(largeLogRoot, "log"));
        File.WriteAllText(Path.Combine(largeLogRoot, "rpcs3.exe"), string.Empty);
        var largePlainLogPath = Path.Combine(largeLogRoot, "log", "RPCS3.log");
        var oneMiBFiller = new string('x', 1024 * 1024);
        using (var writer = new StreamWriter(
                   largePlainLogPath,
                   append: false,
                   new UTF8Encoding(false)))
        {
            writer.WriteLine($"SYS: Serial: {rogue.Serial}");
            writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashC}");
            for (var index = 0; index < 18; index++)
            {
                writer.Write(oneMiBFiller);
            }
        }

        var largeLogInstall = Rpcs3InstallService.FromExe(
            Path.Combine(largeLogRoot, "rpcs3.exe"));
        var largePlainLogGame = new PatchHashProbeService().Enrich(
            largeLogInstall,
            rogue);
        Assert(largePlainLogGame.PpuHash == ExactHashC &&
               largePlainLogGame.HashEvi == HashEvi.PlainLog,
            "Expected a large RPCS3 log to retain boot hash evidence from its bounded streaming probe.");

        using (var writer = new StreamWriter(
                   largePlainLogPath,
                   append: false,
                   new UTF8Encoding(false)))
        {
            for (var index = 0; index < 18; index++)
            {
                writer.Write(oneMiBFiller);
            }

            writer.WriteLine();
            writer.WriteLine($"SYS: Serial: {rogue.Serial}");
            writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashD}");
        }

        var largeTailLogGame = new PatchHashProbeService().Enrich(
            largeLogInstall,
            rogue);
        Assert(largeTailLogGame.PpuHash == ExactHashD &&
               largeTailLogGame.HashEvi == HashEvi.PlainLog,
            "Expected the bounded streaming probe to find the newest complete game session near the end of a large RPCS3 log.");

        using (var writer = new StreamWriter(
                   largePlainLogPath,
                   append: false,
                   new UTF8Encoding(false)))
        {
            for (var index = 0; index < 9; index++)
            {
                writer.Write(oneMiBFiller);
            }

            writer.WriteLine();
            writer.WriteLine($"SYS: Serial: {rogue.Serial}");
            writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashA}");

            for (var index = 0; index < 9; index++)
            {
                writer.Write(oneMiBFiller);
            }
        }

        var largeMiddleLogGame = new PatchHashProbeService().Enrich(
            largeLogInstall,
            rogue);
        Assert(largeMiddleLogGame.PpuHash == ExactHashA &&
               largeMiddleLogGame.HashEvi == HashEvi.PlainLog,
            "Expected the bounded streaming probe to find a complete game session in the middle of a large RPCS3 log instead of reading only the head and tail.");

        File.Delete(largePlainLogPath);
        var largeCompressedLogPath = Path.Combine(
            largeLogRoot,
            "log",
            "RPCS3.log.gz");
        using (var compressedFile = File.Create(largeCompressedLogPath))
        using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine($"SYS: Serial: {rogue.Serial}");
            writer.WriteLine($"ppu_loader: PPU executable hash: PPU-{ExactHashB}");
            for (var index = 0; index < 18; index++)
            {
                writer.Write(oneMiBFiller);
            }
        }

        var largeCompressedLogGame = new PatchHashProbeService().Enrich(
            largeLogInstall,
            rogue);
        Assert(largeCompressedLogGame.PpuHash == ExactHashB &&
               largeCompressedLogGame.HashEvi == HashEvi.ZipLog,
            "Expected a large compressed RPCS3 log to retain boot hash evidence from its bounded streaming probe.");

        var newestEvidenceRoot = Path.Combine(env.TempRoot, "newest-hash-evidence");
        Directory.CreateDirectory(Path.Combine(newestEvidenceRoot, "config"));
        Directory.CreateDirectory(Path.Combine(newestEvidenceRoot, "log"));
        File.WriteAllText(Path.Combine(newestEvidenceRoot, "rpcs3.exe"), string.Empty);

        var olderPlainLog = Path.Combine(newestEvidenceRoot, "log", "RPCS3.log");
        var newerCompressedLog = Path.Combine(newestEvidenceRoot, "log", "RPCS3.log.gz");
        File.WriteAllText(
            olderPlainLog,
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{ExactHashA}{Environment.NewLine}");

        using (var compressedFile = File.Create(newerCompressedLog))
        using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write(
                $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{ExactHashC}{Environment.NewLine}");
        }

        var olderTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var newerTimestamp = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(olderPlainLog, olderTimestamp);
        File.SetLastWriteTimeUtc(newerCompressedLog, newerTimestamp);
        var actualNewerTimestamp = File.GetLastWriteTimeUtc(newerCompressedLog);

        var newestEvidenceInstall = Rpcs3InstallService.FromExe(
            Path.Combine(newestEvidenceRoot, "rpcs3.exe"));
        var newestEvidenceGame = new PatchHashProbeService().Enrich(
            newestEvidenceInstall,
            rogue);

        Assert(newestEvidenceGame.PpuHash == ExactHashC &&
               newestEvidenceGame.HashEvi == HashEvi.ZipLog &&
               newestEvidenceGame.HashSrc == newerCompressedLog &&
               newestEvidenceGame.HashTime == actualNewerTimestamp,
            "Expected the newest valid compressed RPCS3 log evidence to win over an older plain log.");

        using (var compressedFile = File.Create(newerCompressedLog))
        using (var gzip = new GZipStream(compressedFile, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write(
                $"Maintenance line for {rogue.Serial}.{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{new string('d', 41)}{Environment.NewLine}");
        }

        File.SetLastWriteTimeUtc(newerCompressedLog, DateTime.UtcNow);
        var newestInvalidGame = new PatchHashProbeService().Enrich(
            newestEvidenceInstall,
            rogue);

        Assert(newestInvalidGame.PpuHash == ExactHashA &&
               newestInvalidGame.HashEvi == HashEvi.PlainLog,
            "Expected newest-valid selection to ignore a newer invalid compressed log and retain the older valid evidence.");

        var matchedGame = probedGame;
        var patchMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [matchedGame]);
        Assert(patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.Match,
            "Expected matching serial/version/PPU hash to be ExactMatch.");
        var exactEvidence = patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
        Assert(exactEvidence.Hashes.Contains(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                StringComparer.OrdinalIgnoreCase),
            "Expected an accepted patch to retain the detected executable hash as visible evidence.");
        Assert(!string.IsNullOrWhiteSpace(exactEvidence.HashSrc),
            "Expected an accepted patch to retain its local hash evidence source.");
        Assert(exactEvidence.HashEvi == HashEvi.PlainLog,
            "Expected an accepted patch to retain its typed local hash evidence source.");
        Assert(patchMatches.Single(match => match.Patch.Title == "SPU Timing Patch").Status == MatStat.SerBad,
            "Expected unmatched serial to stay SerialMismatch.");

        var missingHashMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [rogue]);
        Assert(missingHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.Unknown,
            "Expected missing hash to be SerialVersionMatchHashUnknown.");

        var wrongHashMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [rogue with
        {
            PpuHash = "cccccccccccccccccccccccccccccccccccccccc"
        }]);
        Assert(wrongHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.HashBad &&
               wrongHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Blocked,
                   Code: PatchCompatibilityReasonCode.ExeBad,
                   CanOn: false,
                   UseGuard: false
               },
            "Expected a trusted local PPU mismatch to block an official patch.yml entry even when serial/version match.");
        Assert(wrongHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Hashes
                .Contains("cccccccccccccccccccccccccccccccccccccccc", StringComparer.OrdinalIgnoreCase),
            "Expected the blocked official patch to retain the different local hash as diagnostic evidence.");

        var wrongVersionMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [rogue with
        {
            Version = "99.99",
            PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        }]);
        Assert(wrongVersionMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.VerBad,
            "Expected mismatched version to be VersionMismatch.");

        var unknownVersionMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [rogue with
        {
            Version = null,
            PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        }]);
        Assert(unknownVersionMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.VerUnk,
            "Expected missing version to be VersionUnknown.");

        var serialMismatchMatches = new R3Config.Svc.PatchMatchService().Match(patchRead.Catalog, [rogue with
        {
            Serial = "BLUS99999",
            PpuHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        }]);
        Assert(serialMismatchMatches.Single(match => match.Patch.Title == "Skip Intro Videos").Status == MatStat.SerBad,
            "Expected missing serial to be SerialMismatch.");
        var noMetadataMatch = new R3Config.Svc.PatchMatchService().Match(
            new Rpcs3PatchEntry(
                Title: "No Metadata Patch",
                HashKind: HashKind.Ppu,
                Hash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                GameTgt: [],
                PatType: "Unknown",
                Risk: R3Config.Mdl.PatRisk.Unknown,
                Notes: null,
                Author: null,
                PatVer: null,
                Group: null),
            [rogue]);
        Assert(noMetadataMatch.Status == MatStat.NoMeta,
            "Expected patch without game metadata to be NoMetadata.");

        var exactSafeFix = patchMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
        var hashUnknownSafeFix = missingHashMatches.Single(match => match.Patch.Title == "Skip Intro Videos");
        var fpsPatch = patchMatches.Single(match => match.Patch.Title == "60 FPS");
        var cheatPatch = patchMatches.Single(match => match.Patch.Title == "Infinite Health Cheat");
        var experimentalPatch = patchMatches.Single(match => match.Patch.Title == "Experimental Test Patch");
        var unknownPatch = patchMatches.Single(match => match.Patch.Title == "Unknown Memory Patch");
        var canaryPatch = new R3Config.Svc.PatchMatchService().Match(
            new Rpcs3PatchEntry(
                Title: "Canary Timing Patch",
                HashKind: HashKind.Spu,
                Hash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                GameTgt: [new R3PatTgt("Canary Game", "BLES00001", ["01.00"])],
                PatType: "Canary",
                Risk: R3Config.Mdl.PatRisk.Canary,
                Notes: "Canary-only timing experiment.",
                Author: "Example",
                PatVer: "0.1",
                Group: null),
            [matchedGame]);

        Assert(!PatPol.Should(exactSafeFix.Patch.Risk, exactSafeFix.Status),
            "Expected exact SafeFix patch to require an explicit user selection.");
        Assert(!PatPol.Should(hashUnknownSafeFix.Patch.Risk, hashUnknownSafeFix.Status),
            "Expected HashUnknown patch not to be selected by default.");
        Assert(PatPol.Can(hashUnknownSafeFix) &&
               PatAct.IsRun(hashUnknownSafeFix),
            "Expected a trusted exact serial/version patch with no local Hash to be selectable under RPCS3's runtime Hash guard.");
        var explicitHashMismatch = wrongHashMatches.Single(match =>
            match.Patch.Title == "Skip Intro Videos");
        Assert(!PatAct.Can(explicitHashMismatch) &&
               !PatAct.IsRun(explicitHashMismatch),
            "Expected a trusted local Hash mismatch to block patch activation.");
        var unknownSourceHashMatch = new R3Config.Svc.PatchMatchService().Match(
            hashUnknownSafeFix.Patch with
            {
                Source = PatSrc.Unknown
            },
            [rogue]);
        Assert(!PatAct.Can(unknownSourceHashMatch),
            "Expected an untrusted patch source without local Hash evidence to remain blocked.");
        Assert(!PatPol.Should(fpsPatch.Patch.Risk, MatStat.Match),
            "Expected 60 FPS patch not to be selected by default.");
        Assert(!PatPol.Should(cheatPatch.Patch.Risk, MatStat.Match),
            "Expected Cheat patch not to be selected by default.");
        Assert(!PatPol.Should(experimentalPatch.Patch.Risk, MatStat.Match),
            "Expected Experimental patch not to be selected by default.");
        Assert(!PatPol.Should(unknownPatch.Patch.Risk, MatStat.Match),
            "Expected Unknown patch not to be selected by default.");
        Assert(PatPol.Is(canaryPatch.Patch.Risk, expertMode: false),
            "Expected Canary patch to be visible for the selected game without an expert-only gate.");
        Assert(!PatPol.Can(canaryPatch),
            "Expected Canary patches for a different serial not to be user-selectable.");
        Assert(!PatPol.Should(canaryPatch.Patch.Risk, canaryPatch.Status),
            "Expected Canary patches not to be auto-selected.");

        var selectedGamePreviewMatches = patchRead.Catalog.Entries
            .Select(entry => new PatchMatchService().Match(entry, [matchedGame]))
            .Where(match => match.Status is not MatStat.SerBad and not MatStat.NoMeta)
            .ToArray();
        Assert(selectedGamePreviewMatches.Length == 7,
            "Expected selected-game preview to include only patches for the selected game's serial.");
        Assert(selectedGamePreviewMatches.All(match => match.MatSer == "BLUS31465"),
            "Expected selected-game preview to exclude patches for other serials.");

        Assert(File.GetLastWriteTimeUtc(patchYmlPath) == patchLastWrite,
            "Expected patch preview read not to modify patch.yml.");
        Assert(!File.Exists(patchConfigPreviewPath), "Expected patch preview read not to create patch_config.yml.");

        var missingPatchConfigApplyPlan = new R3Config.Svc.PatPlnr().Build(
            env.Install,
            [exactSafeFix],
            expertMode: false);
        var backupService = new BackupService();
        var missingPatchConfigApplyResult = new PatchApplyService().Apply(
            missingPatchConfigApplyPlan,
            Rpcs3AppPaths.BkpRoot(env.Install));
        Assert(!missingPatchConfigApplyResult.Backup.Files.Single().Existed,
            "Expected missing patch_config.yml backup entry to record Existed=false.");
        Assert(new CfgRead().IsOn(patchConfigPreviewPath, missingPatchConfigApplyPlan.Changes.Single()),
            "Expected missing patch_config.yml to be created only during checked patch apply.");
        backupService.Restore(missingPatchConfigApplyResult.Backup);
        Assert(!File.Exists(patchConfigPreviewPath),
            "Expected restore to remove patch_config.yml that did not exist before apply.");

        File.WriteAllText(patchConfigPreviewPath, CfgYaml());
        var canaryExact = new R3Config.Svc.PatchMatchService().Match(canaryPatch.Patch, [new Rpcs3GameDescriptor(
            Serial: "BLES00001",
            Title: "Canary Game",
            InstPath: env.TempRoot,
            Version: "01.00",
            PpuHash: null,
            SpuHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]);
        Assert(canaryExact.Status == MatStat.Match,
            "Expected canary test patch to be exact for apply filtering test.");
        Assert(PatPol.Can(canaryExact),
            "Expected exact Canary patches to be user-selectable.");

        var patchApplyPlanner = new R3Config.Svc.PatPlnr();
        var hashUnknownApplyPlan = patchApplyPlanner.Build(env.Install, [hashUnknownSafeFix], expertMode: false);
        Assert(hashUnknownApplyPlan.HasChg &&
               hashUnknownApplyPlan.Changes.Single().PatTitle == "Skip Intro Videos",
            "Expected a trusted serial/version match to be planned with RPCS3 enforcing its executable Hash at runtime.");

        var hashUnknownDisablePlan = patchApplyPlanner.Build2(env.Install, [hashUnknownSafeFix]);
        Assert(hashUnknownDisablePlan.HasChg,
            "Expected HashUnknown patch to be planned for disable when serial and version match.");
        Assert(hashUnknownDisablePlan.Changes.Any(change => change.PatTitle == "Skip Intro Videos"),
            "Expected HashUnknown patch to create a disable patch_config change.");

        var patchApplyPlan = patchApplyPlanner.Build(
            env.Install,
            [
                exactSafeFix,
                fpsPatch,
                cheatPatch,
                canaryExact,
                hashUnknownSafeFix,
                experimentalPatch,
                unknownPatch
            ],
            expertMode: false);
        Assert(patchApplyPlan.Changes.Count == 6,
            "Expected all explicitly checked safe selections to be included, with duplicate exact/runtime-guarded identities deduplicated.");
        Assert(patchApplyPlan.Changes.Any(change => change.PatTitle == "Skip Intro Videos"),
            "Expected Skip Intro Videos to be planned for apply.");
        Assert(patchApplyPlan.Changes.Any(change => change.PatTitle == "Canary Timing Patch"),
            "Expected exact Canary patches to be planned when explicitly selected by the user.");

        var patchApplyService = new PatchApplyService();
        var patchBackupRoot = Rpcs3AppPaths.BkpRoot(env.Install);
        var patchApplyResult = patchApplyService.Apply(patchApplyPlan, patchBackupRoot);
        Assert(patchApplyResult.Backup.Files.Single().TgtPath == patchConfigPreviewPath,
            "Expected patch_config.yml to be backed up before write.");
        Assert(patchApplyResult.Backup.Files.Single().Existed,
            "Expected backup to record existing patch_config.yml.");

        var patchConfigReader = new CfgRead();
        Assert(patchConfigReader.IsOn(patchConfigPreviewPath, patchApplyPlan.Changes.Single(change => change.PatTitle == "Skip Intro Videos")),
            "Expected selected SafeFix patch to be enabled in patch_config.yml.");
        var patchConfigText = File.ReadAllText(patchConfigPreviewPath);
        Assert(File.ReadAllText(patchConfigPreviewPath, Encoding.UTF8).Contains("\r\n", StringComparison.Ordinal),
            "Expected generated patch_config.yml to use Windows CRLF line endings.");
        Assert(patchConfigText.Contains("Existing Patch", StringComparison.Ordinal),
            "Expected unrelated patch_config entries to be preserved.");
        Assert(patchConfigText.Contains("60 FPS", StringComparison.Ordinal),
            "Expected explicitly selected 60FPS patch to be written.");
        Assert(patchConfigText.Contains("Infinite Health Cheat", StringComparison.Ordinal),
            "Expected explicitly selected Cheat patch to be written.");
        Assert(patchConfigText.Contains("Canary Timing Patch", StringComparison.Ordinal),
            "Expected explicitly selected Canary patch to be written.");
        Assert(patchConfigText.Contains("Experimental Test Patch", StringComparison.Ordinal),
            "Expected explicitly selected Experimental patch to be written.");
        Assert(patchConfigText.Contains("Unknown Memory Patch", StringComparison.Ordinal),
            "Expected explicitly selected Unknown patch to be written.");

        backupService.Restore(patchApplyResult.Backup);
        var restoredPatchConfigText = File.ReadAllText(patchConfigPreviewPath);
        Assert(restoredPatchConfigText.Contains("Existing Patch", StringComparison.Ordinal),
            "Expected restore to keep original patch_config entry.");
        Assert(!restoredPatchConfigText.Contains("Skip Intro Videos", StringComparison.Ordinal),
            "Expected restore to remove applied patch entry.");

        var failingPatchBackup = new BackupService();
        var failingPatchTransaction = new PatchFileTransactionService(
            failingPatchBackup,
            commitFile: (stagePath, targetPath) =>
            {
                File.Copy(stagePath, targetPath, overwrite: true);
                throw new UnauthorizedAccessException(
                    "Simulated permission denied.");
            });
        var failingPatchApplyService = new PatchApplyService(
            failingPatchBackup,
            transaction: failingPatchTransaction);
        try
        {
            failingPatchApplyService.Apply(patchApplyPlan, patchBackupRoot);
            throw new InvalidOperationException("Expected patch apply failure to be surfaced.");
        }
        catch (UnauthorizedAccessException)
        {
            Assert(File.ReadAllText(patchConfigPreviewPath) == restoredPatchConfigText,
                "Expected failed patch write to restore the previous patch_config.yml.");
        }

        File.WriteAllText(patchConfigPreviewPath, "PPU-invalid: [broken");
        try
        {
            patchApplyService.Apply(patchApplyPlan, patchBackupRoot);
            throw new InvalidOperationException("Expected invalid patch_config.yml to fail safely.");
        }
        catch (InvalidDataException)
        {
            Assert(File.ReadAllText(patchConfigPreviewPath) == "PPU-invalid: [broken",
                "Expected invalid patch_config.yml to remain unchanged after failed apply.");
        }

        var invalidPatchYmlPath = Path.Combine(env.FakeRoot, "patches", "invalid_patch.yml");
        File.WriteAllText(invalidPatchYmlPath, "Version: [invalid");
        var invalidPatchRead = patchReader.Read(invalidPatchYmlPath);
        Assert(!invalidPatchRead.IsOk, "Expected invalid patch YAML to be rejected safely.");
        Assert(invalidPatchRead.Catalog.Entries.Count == 0, "Expected invalid patch YAML to return an empty catalog.");
        File.Delete(invalidPatchYmlPath);

        var missingPatchRead = patchReader.Read(Path.Combine(env.TempRoot, "missing-patches", "patch.yml"));
        Assert(!missingPatchRead.IsOk, "Expected missing patches folder/patch.yml to fail safely.");
        Assert(missingPatchRead.Catalog.Entries.Count == 0,
            "Expected missing patch.yml to return an empty patch catalog.");

        R3_Pat(env, rogue);
        Flow_Cat(env, patchYmlPath);
    }

    static void R3_Pat(SmokeEnv env, Rpcs3GameDescriptor rogue)
    {
        const string primaryHash = "1111111111111111111111111111111111111111";
        const string secondaryHash = "2222222222222222222222222222222222222222";
        var matchService = new R3Config.Svc.PatchMatchService();

        var multiTargetPatch = new Rpcs3PatchEntry(
            Title: "Best Target Patch",
            HashKind: HashKind.Ppu,
            Hash: secondaryHash,
            GameTgt:
            [
                new R3PatTgt("Contract Game", rogue.Serial, ["01.00"]),
                new R3PatTgt("Contract Game", rogue.Serial, ["01.02"])
            ],
            PatType: "Fix",
            Risk: R3Config.Mdl.PatRisk.SafeFix,
            Notes: "Contract test.",
            Author: "R3Config",
            PatVer: "1.0",
            Group: "Fixes",
            Source: PatSrc.Official,
            SrcPath: "patch.yml");

        var normalizedVersionMatch = matchService.Match(multiTargetPatch, [rogue with
        {
            Version = "v1.2 ",
            PpuHash = primaryHash,
            PpuRaw = [primaryHash, secondaryHash]
        }]);
        Assert(normalizedVersionMatch.Status == MatStat.Match,
            "Expected matcher to evaluate every same-serial target and choose the exact version/hash match.");
        Assert(normalizedVersionMatch.TgtVer == "01.02",
            "Expected RPCS3 version normalization to map v1.2 to target version 01.02.");

        var allTargetPatch = multiTargetPatch with
        {
            Title = "All Target Patch",
            GameTgt = [new R3PatTgt("All", "All", ["All"])]
        };
        var allTargetMatch = matchService.Match(allTargetPatch, [rogue with
        {
            Version = null,
            PpuHash = secondaryHash
        }]);
        Assert(allTargetMatch.Status == MatStat.Match,
            "Expected RPCS3 All/All/All targets to match an installed game even when its version is unknown.");
        var allTargetPlan = new R3Config.Svc.PatPlnr().Build(env.Install, [allTargetMatch], expertMode: false);
        Assert(allTargetPlan.Changes.Single() is { GameName: "All", Serial: "All", Version: "All" },
            "Expected patch_config.yml keys to preserve RPCS3's literal All target path.");

        var unrelatedGlobalMatch = matchService.Match(
            allTargetPatch,
            [rogue with
            {
                Version = "01.02",
                PpuHash = primaryHash,
                PpuRaw = [primaryHash]
            }]);
        var unrelatedGlobalPreview = new PatPrev();
        unrelatedGlobalPreview.Load(
            [unrelatedGlobalMatch],
            isEnabledResolver: _ => true);
        Assert(unrelatedGlobalPreview.Items.Count == 0,
            "Expected an enabled All/All/All patch to stay off unrelated game pages when its runtime hash does not match.");

        var exactGlobalPreview = new PatPrev();
        exactGlobalPreview.Load(
            [allTargetMatch],
            isEnabledResolver: _ => true);
        Assert(exactGlobalPreview.Items.Count == 1 &&
               exactGlobalPreview.Items.Single().IsOn,
            "Expected an enabled All/All/All patch to remain visible on a game page with exact runtime-hash evidence.");

        const string extendedHashCatalog = """
    Version: 1.2
    PRX-QoiFRPF8j00N996aQMthtWqEUG0Y-0:
      "PRX Contract Patch":
        Games:
          All:
            All: [ All ]
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    OVL-4444444444444444444444444444444444444444:
      "OVL Contract Patch":
        Games:
          All:
            All: [ All ]
        Patch Version: 1.0
        Patch:
          - [ be32, 0x00000020, 0x60000000 ]
    """;
        var reader = new R3Config.Svc.ReadSvc();

        var missingVersionRead = reader.ReadText(
            """
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Missing Version":
        Games:
          Contract Game:
            BLUS31465: [ 01.02 ]
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(!missingVersionRead.IsOk &&
               missingVersionRead.Error?.Contains("Version", StringComparison.Ordinal) == true,
            "Expected RPCS3 patch files without Version: 1.2 to be rejected.");

        var unsupportedVersionRead = reader.ReadText(
            """
    Version: 1.1
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Unsupported Version":
        Games:
          Contract Game:
            BLUS31465: [ 01.02 ]
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(!unsupportedVersionRead.IsOk &&
               unsupportedVersionRead.Error?.Contains("1.2", StringComparison.Ordinal) == true,
            "Expected patch engine versions other than 1.2 to be rejected.");

        var scalarVersionRead = reader.ReadText(
            """
    Version: 1.2
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Scalar Version":
        Games:
          Contract Game:
            BLUS31465: 01.02
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(scalarVersionRead.IsOk &&
               scalarVersionRead.Catalog.Entries.Count == 0 &&
               scalarVersionRead.Error?.Contains("sequence", StringComparison.OrdinalIgnoreCase) == true,
            "Expected scalar app versions to be excluded and surfaced as RPCS3 contract warnings.");

        var invalidAllRead = reader.ReadText(
            """
    Version: 1.2
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Invalid All Pair":
        Games:
          Contract Game:
            All: [ All ]
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(invalidAllRead.IsOk &&
               invalidAllRead.Catalog.Entries.Count == 0 &&
               invalidAllRead.Error?.Contains("serial All", StringComparison.Ordinal) == true,
            "Expected serial All to be rejected outside the All title.");

        var alphanumericSerialRead = reader.ReadText(
            """
    Version: 1.2
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Alphanumeric Serial":
        Games:
          Contract Game:
            AB12C34D5: [ 01.02 ]
        Patch:
          - [ be32, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(alphanumericSerialRead.IsOk &&
               alphanumericSerialRead.Catalog.Entries.Single().GameTgt.Single().Serial == "AB12C34D5" &&
               string.IsNullOrWhiteSpace(alphanumericSerialRead.Error),
            "Expected RPCS3's nine-character alphanumeric serial contract to be accepted.");

        var invalidInstructionRead = reader.ReadText(
            """
    Version: 1.2
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Invalid Instruction":
        Games:
          Contract Game:
            BLUS31465: [ 01.02 ]
        Patch:
          - [ not_a_patch_type, 0x00000010, 0x60000000 ]
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(invalidInstructionRead.IsOk &&
               invalidInstructionRead.Catalog.Entries.Count == 0 &&
               invalidInstructionRead.Error?.Contains("not_a_patch_type", StringComparison.Ordinal) == true,
            "Expected patches containing unsupported RPCS3 instruction types to be excluded with a warning.");

        var invalidPatchShapeRead = reader.ReadText(
            """
    Version: 1.2
    PPU-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:
      "Invalid Patch Shape":
        Games:
          Contract Game:
            BLUS31465: [ 01.02 ]
        Patch: be32
    """,
            PatSrc.Official,
            "patch.yml");
        Assert(invalidPatchShapeRead.IsOk &&
               invalidPatchShapeRead.Catalog.Entries.Count == 0 &&
               invalidPatchShapeRead.Error?.Contains("Patch must be a YAML sequence", StringComparison.Ordinal) == true,
            "Expected non-sequence Patch nodes to be excluded with a warning.");

        var extendedRead = reader.ReadText(extendedHashCatalog, PatSrc.Official, "patch.yml");
        Assert(extendedRead.IsOk, "Expected PRX/OVL RPCS3 patch keys to parse.");
        Assert(extendedRead.Catalog.Entries.Any(entry => entry.HashKind == HashKind.Prx) &&
               extendedRead.Catalog.Entries.Any(entry => entry.HashKind == HashKind.Ovl),
            "Expected PRX and OVL entries not to be silently dropped.");
        Assert(extendedRead.Catalog.Entries.All(entry => entry.Source == PatSrc.Official),
            "Expected explicit patch provenance to survive parsing.");
        Assert(extendedRead.Catalog.Entries.All(entry => entry.GameTgt.Single().Serial == "All"),
            "Expected global RPCS3 patch targets to survive parsing.");

        var imported = multiTargetPatch with
        {
            PatVer = "1.1",
            Source = PatSrc.Imported,
            Class = PatClass.Canary,
            Risk = PatRisk.Canary
        };
        var bundled = multiTargetPatch with
        {
            PatVer = "2.0",
            Source = PatSrc.Can,
            Class = PatClass.Canary,
            Risk = PatRisk.Canary
        };
        var merged = new PatchCatalogMergeService().Merge(
            new R3Config.Mdl.PatCat("1.2", [multiTargetPatch]),
            new R3Config.Mdl.PatCat("1.2", [imported]),
            new R3Config.Mdl.PatCat("1.2", [bundled]));
        Assert(merged.Entries.Count == 2 &&
               merged.Entries.Any(entry =>
                   entry.Source == PatSrc.Official &&
                   entry.Class == PatClass.Main) &&
               merged.Entries.Any(entry =>
                   entry.Source == PatSrc.Imported &&
                   entry.Class == PatClass.Canary),
            "Expected official patch.yml to remain independent while the resident imported Canary definition supersedes only its bundled Canary preview duplicate.");
        Assert(merged.Diag.Count == 1 &&
               merged.Diag.Single().DropSrc == PatSrc.Can,
            "Expected only the bundled Canary preview duplicate to be reported as dropped.");

        var mergeService = new PatchCatalogMergeService();
        var versionBase = multiTargetPatch with
        {
            PatVer = "1.2",
            Source = PatSrc.Imported,
            SrcPath = "imported_patch.yml"
        };

        var higherVersionMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2", [versionBase]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "1.10",
                    Source = PatSrc.Imported,
                    SrcPath = "imported_patch.yml"
                }
            ]));
        Assert(higherVersionMerge.Entries.Single() is
        { PatVer: "1.10", Source: PatSrc.Imported },
            "Expected a valid numerically higher RPCS3 patch version to replace the existing duplicate.");
        Assert(higherVersionMerge.Diag.Single().Code == "PATCH_VERSION_OVERRIDE",
            "Expected a valid higher patch version to be reported as an override.");

        var equalVersionMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2", [versionBase]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "1.2.0",
                    Source = PatSrc.Imported,
                    SrcPath = "imported_patch.yml"
                }
            ]));
        Assert(equalVersionMerge.Entries.Single().Source == PatSrc.Imported,
            "Expected RPCS3-equivalent patch versions to keep the entry loaded first.");
        Assert(equalVersionMerge.Diag.Single().Code == "PATCH_DUPLICATE_SKIPPED",
            "Expected an equal patch version to be reported as a skipped duplicate.");

        var lowerVersionMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2", [versionBase]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "1.1",
                    Source = PatSrc.Imported,
                    SrcPath = "imported_patch.yml"
                }
            ]));
        Assert(lowerVersionMerge.Entries.Single().Source == PatSrc.Imported,
            "Expected a lower patch version to keep the entry loaded first.");

        var invalidVersionMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2", [versionBase]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "9.0-beta",
                    Source = PatSrc.Imported,
                    SrcPath = "imported_patch.yml"
                }
            ]));
        Assert(invalidVersionMerge.Entries.Single().Source == PatSrc.Imported,
            "Expected an incomparable patch version to keep the entry loaded first instead of using lexical order.");

        var prefixedVersionMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2", [versionBase]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "v9.0",
                    Source = PatSrc.Imported,
                    SrcPath = "imported_patch.yml"
                }
            ]));
        Assert(prefixedVersionMerge.Entries.Single().Source == PatSrc.Imported,
            "Expected RPCS3-incomparable v-prefixed patch versions not to override an existing entry.");

        var residentOverPreviewMerge = mergeService.Merge(
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "99.0",
                    Source = PatSrc.Can,
                    SrcPath = "can.yml",
                    Class = PatClass.Canary,
                    Risk = PatRisk.Canary
                }
            ]),
            new R3Config.Mdl.PatCat("1.2",
            [
                versionBase with
                {
                    PatVer = "1.0",
                    Source = PatSrc.Official,
                    SrcPath = "patch.yml",
                    Class = PatClass.Main
                }
            ]));
        Assert(residentOverPreviewMerge.Entries.Count == 2 &&
               residentOverPreviewMerge.Entries.Any(entry =>
                   entry.Source == PatSrc.Official &&
                   entry.Class == PatClass.Main) &&
               residentOverPreviewMerge.Entries.Any(entry =>
                   entry.Source == PatSrc.Can &&
                   entry.Class == PatClass.Canary) &&
               residentOverPreviewMerge.Diag.Count == 0,
            "Expected official patch.yml and Canary definitions with the same runtime identity to remain independent channels.");

        var duplicateHashPatch = multiTargetPatch with { Hash = primaryHash };
        var previewMatches = new[]
        {
            matchService.Match(multiTargetPatch, [rogue with { Version = "01.02", PpuRaw = [primaryHash, secondaryHash] }]),
            matchService.Match(duplicateHashPatch, [rogue with { Version = "01.02", PpuRaw = [primaryHash, secondaryHash] }])
        };
        var preview = new PatPrev();
        preview.Load(
            previewMatches,
            warningMessage: "Contract warning",
            isEnabledResolver: match => match.Patch.Hash == primaryHash);
        Assert(preview.HasWarn && preview.WarnText == "Contract warning",
            "Expected real patch catalog warnings to be visible in the preview surface.");
        Assert(preview.OfclGrp.Count == 1 && preview.OfclGrp.Single().Items.Count == 2,
            "Expected RPCS3 hierarchy to group different hashes under the same game/serial/version node.");
        Assert(preview.OfclList.All(item => !item.IsSel),
            "Expected no patch to be preselected.");
        Assert(preview.OfclList.Count(item => item.CanOn) == 1 &&
               preview.OfclList.Count(item => item.CanOff) == 1,
            "Expected enabled-state resolution to separate enable and disable actions.");
        Assert(preview.OfclList.Select(item => item.DspTitle).Distinct().Count() == 2,
            "Expected duplicate patch titles for different hashes to receive distinct RPCS3-style display ordinals.");
        var enableItem = preview.OfclList.Single(item => item.CanOn);
        enableItem.IsSel = true;
        preview.OfclGrp.Single().IsOpen = false;
        Assert(preview.OfclOn && preview.SelOfcl().Count == 1,
            "Expected official-tab selection state to be isolated and executable.");
        preview.SetLang();
        var rebuiltEnableItem = preview.OfclList.Single(item => item.CanOn);
        Assert(!ReferenceEquals(enableItem, rebuiltEnableItem),
            "Expected a live language switch to rebuild patch item containers under the new FlowDirection.");
        Assert(rebuiltEnableItem.IsSel && preview.SelOfcl().Count == 1,
            "Expected live language container rebuild to preserve explicit patch selection state.");
        Assert(!preview.OfclGrp.Single().IsOpen,
            "Expected live language container rebuild to preserve the user's group expansion state.");

        var experimentalPreview = new PatPrev();
        var experimentalPatch = multiTargetPatch with
        {
            Title = "Experimental renderer path",
            Risk = R3Config.Mdl.PatRisk.Exp,
            Source = PatSrc.Official
        };
        experimentalPreview.Load(
            [matchService.Match(
                experimentalPatch,
                [rogue with { Version = "01.02", PpuRaw = [primaryHash, secondaryHash] }])],
            title: null);
        Assert(experimentalPreview.CanItems.Count == 0 &&
               experimentalPreview.OfclList.Count == 1 &&
               experimentalPatch.Class == PatClass.Main,
            "Expected an Experimental risk label to remain independent from the Main/Canary distribution class.");

        var configPath = Path.Combine(env.TempRoot, "contract-patch-config.yml");
        var change = allTargetPlan.Changes.Single();
        var writer = new CfgWrite();
        var configReader = new CfgRead();
        writer.Write(configPath, [change]);
        Assert(configReader.IsOn(configPath, change),
            "Expected RPCS3-compatible Enabled mapping to be readable after write.");
        var enabledSnapshot = configReader.ReadSnap(configPath);
        writer.Write(configPath, [change], enabled: false);
        Assert(enabledSnapshot.IsOn(change),
            "Expected a patch preview snapshot to remain stable without reparsing patch_config.yml per row.");
        Assert(!configReader.IsOn(configPath, change),
            "Expected disabling a patch to remove its enabled state.");
        Assert(!File.ReadAllText(configPath).Contains("Enabled: false", StringComparison.Ordinal),
            "Expected disable to mirror RPCS3 and avoid stale Enabled: false leaves.");

        File.WriteAllText(configPath, $"""
    {change.HashKey}:
      {change.PatTitle}:
        All:
          All:
            All:
              Enabled: true
              Configurable Values:
                Scale: 2
    """);
        writer.Write(configPath, [change], enabled: false);
        var configurableText = File.ReadAllText(configPath);
        Assert(!configurableText.Contains("Enabled:", StringComparison.Ordinal) &&
               configurableText.Contains("Configurable Values", StringComparison.Ordinal),
            "Expected disable to preserve RPCS3 configurable patch values while removing Enabled.");

        File.WriteAllText(configPath, $"""
    {change.HashKey}:
      {change.PatTitle}:
        All:
          All:
            All: true
    """);
        Assert(configReader.IsOn(configPath, change),
            "Expected legacy RPCS3 scalar patch_config.yml entries to remain readable.");
        writer.Write(configPath, [change], enabled: false);
        Assert(!File.ReadAllText(configPath).Contains(change.PatTitle, StringComparison.Ordinal),
            "Expected legacy scalar disable to prune the empty patch path.");

        var hashProbeRoot = Path.Combine(env.TempRoot, "multi-hash-log");
        Directory.CreateDirectory(Path.Combine(hashProbeRoot, "config"));
        Directory.CreateDirectory(Path.Combine(hashProbeRoot, "log"));
        File.WriteAllText(Path.Combine(hashProbeRoot, "rpcs3.exe"), string.Empty);
        File.WriteAllText(
            Path.Combine(hashProbeRoot, "log", "RPCS3.log"),
            $"SYS: Serial: {rogue.Serial}{Environment.NewLine}" +
            $"{new string('x', 20000)}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{primaryHash}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{secondaryHash}{Environment.NewLine}" +
            $"ppu_loader: SPU executable hash: SPU-{primaryHash}{Environment.NewLine}");
        var probeInstall = Rpcs3InstallService.FromExe(Path.Combine(hashProbeRoot, "rpcs3.exe"));
        var multiHashGame = new PatchHashProbeService().Enrich(probeInstall, rogue);
        Assert(multiHashGame.Hashes.Count == 2 && multiHashGame.SpuList.Count == 1,
            "Expected bounded RPCS3 log probing to retain every relevant PPU/SPU hash in the game session.");
        Assert(multiHashGame.PpuList.SequenceEqual([primaryHash]),
            "Expected only the first main PPU log identity to become authoritative while later PPU modules remain positive evidence.");
        Assert(matchService.Match(multiTargetPatch, [multiHashGame]).Status == MatStat.Match,
            "Expected patch matching to use the complete discovered hash set, not only the first hash.");

        Console.WriteLine("RPCS3 patch engine contract tests passed.");
    }

}
