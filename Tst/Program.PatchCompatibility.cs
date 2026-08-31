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
    static void Pat_Cmp(string tempRoot)
    {
        const string serial = "BLUS00001";
        const string version = "01.00";
        const string exactHash = "728fc4496a4c347a654ab54d3e3f0e44ed3c9af6";
        const string otherHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string prxBase57 = "QoiFRPF8j00N996aQMthtWqEUG0Y";
        const string olderCacheHash = "83681f6110d33442329073b72b8dc88a2f677172";
        const string olderCacheBase57 = "U8pStuqfrkhA3fGiHfGZz99SofxM";

        Assert(RunKey.TryParse(
                   $"PPU-{exactHash}",
                   out var ppuKey) &&
               ppuKey.Kind == HashKind.Ppu &&
               ppuKey.NormHash == exactHash,
            "Expected a 40-hex PPU key to match RPCS3's runtime naming contract.");
        Assert(!RunKey.TryParse(
                   $"PPU-{prxBase57}",
                   out _),
            "Expected a Base57 PPU patch key to be rejected because RPCS3 emits hexadecimal PPU runtime keys.");
        Assert(RunKey.TryParse(
                   $"PRX-{prxBase57}-0",
                   out var prxKey) &&
               prxKey.Kind == HashKind.Prx &&
               prxKey.NormHash == exactHash + "-0",
            "Expected RPCS3's Base57-plus-segment PRX key to normalize for comparison.");
        Assert(RunKey.TryParse(
                   $"{serial}-PPU-{exactHash}",
                   out var scopedKey) &&
               scopedKey.SerScope == serial,
            "Expected RPCS3 TITLE_ID-prefixed alternative runtime keys to be recognized.");
        Assert(RunKey.TryParse(
                   $"OVL-{exactHash}",
                   out var ovlKey) &&
               ovlKey.Kind == HashKind.Ovl,
            "Expected hexadecimal OVL runtime keys to be recognized.");

        var compatibility = new PatchCompatibilityService();
        var target = new R3PatTgt(
            "Compatibility Game",
            serial,
            [version]);
        var exactGame = new Rpcs3GameDescriptor(
            Serial: serial,
            Title: "Compatibility Game",
            InstPath: tempRoot,
            Version: version,
            PpuHash: exactHash,
            SpuHash: null,
            HashEvi: HashEvi.Meta);

        foreach (var source in new[]
                 {
                     PatSrc.Official,
                     PatSrc.Can,
                     PatSrc.Imported,
                     PatSrc.Artemis,
                     PatSrc.ManagedArtemis,
                     PatSrc.TitlePat
                 })
        {
            var patch = NewPatch(
                "Unified source " + source,
                HashKind.Ppu,
                exactHash,
                target,
                source);
            var exactInput = source == PatSrc.Can
                ? exactGame with
                {
                    HCanPpu = [exactHash],
                    HCanSpu = [],
                    HashEvi = HashEvi.PlainLog
                }
                : exactGame;
            var exact = compatibility.Evaluate(patch, [exactInput]);
            Assert(exact.Cmp is
            {
                Verdict: PatchCompatibilityVerdict.Compat,
                Code: PatchCompatibilityReasonCode.ExactId,
                CanOn: true
            },
                $"Expected {source} to accept an exact executable compatibility decision from its allowed evidence source.");

            var noLocalHash = compatibility.Evaluate(
                patch,
                [exactGame with
                {
                    PpuHash = null,
                    PpuRaw = [],
                    HCanPpu = null,
                    HCanSpu = null,
                    HashEvi = HashEvi.None
                }]);
            Assert(noLocalHash.Cmp is
            {
                Verdict: PatchCompatibilityVerdict.Guarded,
                Code: PatchCompatibilityReasonCode.R3Guard,
                CanOn: true,
                UseGuard: true
            },
                $"Expected {source} to use RPCS3's runtime hash guard when local PPU evidence is absent.");
        }

        var residentCanary = NewPatch(
            "Resident Canary compatibility",
            HashKind.Ppu,
            exactHash,
            target,
            PatSrc.Imported) with
        {
            Class = PatClass.Canary,
            Risk = PatRisk.Canary
        };
        var residentCanaryUnknown = compatibility.Evaluate(
            residentCanary,
            [exactGame with
            {
                PpuHash = exactHash,
                HCanPpu = null,
                HCanSpu = null
            }]);
        Assert(residentCanaryUnknown.Cmp is
        {
            Verdict: PatchCompatibilityVerdict.Guarded,
            Code: PatchCompatibilityReasonCode.R3Guard,
            CanOn: true,
            UseGuard: true
        },
            "Expected a trusted Canary definition already resident in imported_patch.yml to remain selectable under RPCS3's runtime-key guard.");
        var residentCanaryExact = compatibility.Evaluate(
            residentCanary,
            [exactGame with
            {
                HCanPpu = [exactHash],
                HCanSpu = []
            }]);
        Assert(residentCanaryExact.Cmp is
        {
            Verdict: PatchCompatibilityVerdict.Compat,
            CanOn: true
        },
            "Expected an exact saved PPU/SPU discovery match to produce an exact compatibility decision for a resident imported Canary definition.");
        var residentCanaryMismatch = compatibility.Evaluate(
            residentCanary,
            [exactGame with
            {
                HCanPpu = [otherHash],
                HCanSpu = []
            }]);
        Assert(residentCanaryMismatch.Status == MatStat.HashBad &&
               residentCanaryMismatch.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Blocked,
                   Code: PatchCompatibilityReasonCode.ExeBad,
                   CanOn: false,
                   UseGuard: false
               },
            "Expected a different saved H-Canary PPU set to block activation as contradictory executable evidence.");

        var separationRoot = Path.Combine(tempRoot, "patch-source-separation");
        var separationConfig = Path.Combine(separationRoot, "config");
        var separationCustom = Path.Combine(separationConfig, "custom_configs");
        var separationPatches = Path.Combine(separationRoot, "patches");
        var separationPatchConfig = Path.Combine(separationConfig, "patch_config.yml");
        var separationInstall = new Rpcs3InstallDescriptor(
            separationRoot,
            Path.Combine(separationRoot, "rpcs3.exe"),
            separationConfig,
            separationCustom,
            separationPatches,
            separationPatchConfig,
            separationRoot);
        var separationOfficial = compatibility.Evaluate(
            NewPatch(
                "Official separation",
                HashKind.Ppu,
                exactHash,
                target,
                PatSrc.Official),
            [exactGame]);
        var separationPlan = new PatchApplyPlan(
            separationPatchConfig,
            [new PatChg(
                separationOfficial.Patch.RunKey,
                separationOfficial.Patch.Title,
                target.Title,
                serial,
                version,
                PatRisk.SafeFix)]);
        var separationMutation = new PatchMutationUseCase(
            new PatPlnr(),
            new PatchApplyService(),
            new BundledCanaryPatchService(),
            new BundledCheatPatchService(),
            new Rpcs3MutationGuard(() => false));
        Directory.CreateDirectory(separationConfig);
        Directory.CreateDirectory(separationPatches);
        var liveGroupA = compatibility.Evaluate(
            NewPatch(
                "Live group A",
                HashKind.Ppu,
                exactHash,
                target,
                PatSrc.Official) with { Group = "FPS" },
            [exactGame]);
        var liveGroupB = compatibility.Evaluate(
            NewPatch(
                "Live group B",
                HashKind.Ppu,
                exactHash,
                target,
                PatSrc.Official) with { Group = "FPS" },
            [exactGame]);
        var liveGroupBChange = separationMutation.BuildChg(liveGroupB)
            ?? throw new InvalidOperationException("Expected a live group peer change.");
        File.WriteAllText(
            separationPatchConfig,
            new CfgWrite().Build(
                separationPatchConfig,
                [liveGroupBChange]));
        try
        {
            _ = separationMutation.BuildOn(
                separationInstall,
                [liveGroupA],
                enabledMatches: [],
                expertMode: false,
                allMatches: [liveGroupA, liveGroupB]);
            throw new InvalidOperationException(
                "Expected live patch_config.yml state to override stale preview enabled state for group validation.");
        }
        catch (PatchGroupConflictException ex)
        {
            Assert(ex.HasEnabledPeer &&
                   ex.SelectedTitle == liveGroupA.Patch.Title &&
                   ex.EnabledTitle == liveGroupB.Patch.Title,
                "Expected plan construction to detect the live enabled peer rather than stale Preview.Enabled state.");
        }
        File.Delete(separationPatchConfig);

        var collisionOfficialPath = Path.Combine(separationPatches, "patch.yml");
        var collisionManagedPath = Path.Combine(separationPatches, serial + "_patch.yml");
        var collisionOfficialText =
            "Version: 1.2\n" +
            $"PPU-{exactHash}:\n" +
            "  \"Runtime Collision\":\n" +
            "    Games:\n" +
            "      \"Compatibility Game\":\n" +
            $"        {serial}: [ {version} ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00001000, 0x11111111 ]\n";
        var collisionManagedText =
            BundledCheatPatchService.ManMark + "\r\n" +
            collisionOfficialText.Replace(
                "0x11111111",
                "0x22222222",
                StringComparison.Ordinal);
        File.WriteAllText(collisionOfficialPath, collisionOfficialText);
        File.WriteAllText(collisionManagedPath, collisionManagedText);
        var collisionRead = new ReadSvc().Read(
            collisionOfficialPath,
            PatSrc.Official);
        Assert(collisionRead.IsOk && collisionRead.Catalog.Entries.Count == 1,
            "Expected the official runtime-collision fixture to parse.");
        var collisionSelected = compatibility.Evaluate(
            collisionRead.Catalog.Entries.Single(),
            [exactGame]);
        var collisionPlan = separationMutation.BuildOn(
            separationInstall,
            [collisionSelected],
            enabledMatches: [],
            expertMode: false,
            allMatches: [collisionSelected]);
        try
        {
            separationMutation.ApplyTxn(
                collisionPlan,
                separationInstall,
                [collisionSelected]);
            throw new InvalidOperationException(
                "Expected a different-body Artemis/Official runtime identity collision to fail closed.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains(
                "runtime identity collision",
                StringComparison.OrdinalIgnoreCase))
        {
        }
        File.Delete(collisionManagedPath);
        File.WriteAllText(
            collisionOfficialPath,
            collisionOfficialText.Replace(
                "0x11111111",
                "0x33333333",
                StringComparison.Ordinal));
        try
        {
            separationMutation.ApplyTxn(
                collisionPlan,
                separationInstall,
                [collisionSelected]);
            throw new InvalidOperationException(
                "Expected an official patch body changed after preview to fail before mutation.");
        }
        catch (IOException ex) when (
            ex.Message.Contains(
                "changed after preview",
                StringComparison.OrdinalIgnoreCase))
        {
        }
        File.Delete(collisionOfficialPath);

        try
        {
            separationMutation.ApplyTxn(
                separationPlan,
                separationInstall,
                [separationOfficial, residentCanaryExact]);
            throw new InvalidOperationException(
                "Expected mixed official and Canary activation to be rejected before any file mutation.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains(
                "must be applied separately",
                StringComparison.Ordinal))
        {
        }

        var officialPatch = NewPatch(
            "Official compatibility",
            HashKind.Ppu,
            exactHash,
            target,
            PatSrc.Official);

        var wrongVersion = compatibility.Evaluate(
            officialPatch,
            [exactGame with { Version = "02.00" }]);
        Assert(wrongVersion.Cmp is
        {
            Verdict: PatchCompatibilityVerdict.Blocked,
            Code: PatchCompatibilityReasonCode.VerBad,
            CanOn: false,
            CanOff: true
        },
            "Expected an installed-version mismatch to block activation while preserving safe removal of an old enabled entry.");

        var authoritativeHashMismatch = compatibility.Evaluate(
            officialPatch,
            [exactGame with { PpuHash = otherHash }]);
        Assert(authoritativeHashMismatch.Status == MatStat.HashBad &&
               authoritativeHashMismatch.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Blocked,
                   Code: PatchCompatibilityReasonCode.ExeBad,
                   CanOn: false,
                   CanOff: true,
                   UseGuard: false
               },
            "Expected a trusted metadata PPU mismatch to block official patch activation while preserving safe removal.");

        var staleCacheOnly = compatibility.Evaluate(
            officialPatch,
            [exactGame with
            {
                PpuHash = otherHash,
                HashEvi = HashEvi.Cache
            }]);
        Assert(staleCacheOnly.Status ==
                   MatStat.Unknown &&
               staleCacheOnly.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Guarded,
                   CanOn: true
               },
            "Expected a non-matching stale cache entry not to become false incompatibility; RPCS3 remains the runtime guard.");

        var spuPatch = NewPatch(
            "Partial SPU evidence",
            HashKind.Spu,
            exactHash,
            target,
            PatSrc.Can);
        var partialSpu = compatibility.Evaluate(
            spuPatch,
            [exactGame with
            {
                PpuHash = null,
                SpuHash = otherHash,
                HCanPpu = null,
                HCanSpu = null
            }]);
        Assert(partialSpu.Status == MatStat.Unknown &&
               partialSpu.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Guarded,
                   Code: PatchCompatibilityReasonCode.R3Guard,
                   CanOn: true,
                   UseGuard: true
               },
            "Expected bundled Canary SPU activation to use RPCS3's exact runtime-key guard when a local discovery set is absent.");
        var exactDiscoveredSpu = compatibility.Evaluate(
            spuPatch,
            [exactGame with
            {
                PpuHash = null,
                SpuHash = null,
                HCanPpu = [],
                HCanSpu = [exactHash],
                HashEvi = HashEvi.PlainLog
            }]);
        Assert(exactDiscoveredSpu.Status == MatStat.Match &&
               exactDiscoveredSpu.Cmp?.CanOn == true,
            "Expected an exact saved SPU discovery fingerprint to enable the matching bundled Canary patch.");

        var prxPatch = NewPatch(
            "PRX exact",
            HashKind.Prx,
            prxBase57 + "-0",
            target,
            PatSrc.Official);
        var prxExact = compatibility.Evaluate(
            prxPatch,
            [exactGame with
            {
                PpuHash = null,
                PrxRaw = [exactHash + "-0"]
            }]);
        Assert(prxExact.Cmp?.Verdict ==
                   PatchCompatibilityVerdict.Compat,
            "Expected PRX runtime evidence to produce an exact compatibility decision.");

        var ovlPatch = NewPatch(
            "OVL exact",
            HashKind.Ovl,
            exactHash,
            target,
            PatSrc.Official);
        var ovlExact = compatibility.Evaluate(
            ovlPatch,
            [exactGame with
            {
                PpuHash = null,
                OvlRaw = [exactHash]
            }]);
        Assert(ovlExact.Cmp?.Verdict ==
                   PatchCompatibilityVerdict.Compat,
            "Expected OVL runtime evidence to produce an exact compatibility decision.");

        var scopedToAnotherGame = officialPatch with
        {
            KeySer = "BLUS99999"
        };
        var scopeMismatch = compatibility.Evaluate(
            scopedToAnotherGame,
            [exactGame]);
        Assert(scopeMismatch.Cmp is
        {
            Code: PatchCompatibilityReasonCode.KeySerBd,
            CanOn: false
        },
            "Expected a TITLE_ID-prefixed runtime key for another game to be blocked.");

        var unknownSource = compatibility.Evaluate(
            officialPatch with { Source = PatSrc.Unknown },
            [exactGame with { PpuHash = null }]);
        Assert(unknownSource.Cmp is
        {
            Code: PatchCompatibilityReasonCode.Guard,
            CanOn: false
        },
            "Expected an unknown source to require exact local hash evidence.");

        var globalPatch = officialPatch with
        {
            GameTgt =
            [
                new R3PatTgt(
                    "All",
                    PatVer.All,
                    [PatVer.All])
            ]
        };
        var globalWithoutHash = compatibility.Evaluate(
            globalPatch,
            [exactGame with { PpuHash = null }]);
        Assert(globalWithoutHash.Cmp is
        {
            Code: PatchCompatibilityReasonCode.Hash,
            CanOn: false
        },
            "Expected All/All definitions to require an exact local hash before showing for a selected game.");

        var layoutRoot = Path.Combine(tempRoot, "compat-layout");
        Directory.CreateDirectory(layoutRoot);
        var portableExe = Path.Combine(layoutRoot, "rpcs3.exe");
        File.WriteAllText(portableExe, string.Empty);
        var portableData = Path.Combine(layoutRoot, "portable");
        Directory.CreateDirectory(portableData);
        var portableInstall = Rpcs3InstallService.FromExe(portableExe);
        Assert(portableInstall.DataRoot == portableData &&
               portableInstall.CfgPath == Path.Combine(portableData, "config") &&
               portableInstall.PatPath == Path.Combine(portableData, "patches"),
            "Expected R3Config to mirror RPCS3's portable data-root precedence.");

        var envLayoutRoot = Path.Combine(tempRoot, "compat-env-layout");
        Directory.CreateDirectory(envLayoutRoot);
        var envExe = Path.Combine(envLayoutRoot, "rpcs3.exe");
        File.WriteAllText(envExe, string.Empty);
        var configuredData = Path.Combine(tempRoot, "compat-configured-data");
        var previousConfigDir = Environment.GetEnvironmentVariable(
            "RPCS3_CONFIG_DIR");
        try
        {
            Environment.SetEnvironmentVariable(
                "RPCS3_CONFIG_DIR",
                configuredData);
            var configuredInstall = Rpcs3InstallService.FromExe(envExe);
            Assert(configuredInstall.DataRoot == configuredData &&
                   configuredInstall.PatCfg == Path.Combine(
                       configuredData,
                       "config",
                       "patch_config.yml"),
                "Expected R3Config to mirror RPCS3_CONFIG_DIR when portable mode is absent.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "RPCS3_CONFIG_DIR",
                previousConfigDir);
        }

        var cacheProofRoot = Path.Combine(
            tempRoot,
            "compat-cache-proof");
        var cacheProofGamePath = Path.Combine(
            cacheProofRoot,
            "games",
            "Compatibility Game");
        var cacheProofEboot = Path.Combine(
            cacheProofGamePath,
            "PS3_GAME",
            "USRDIR",
            "EBOOT.BIN");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheProofEboot)!);
        File.WriteAllText(cacheProofEboot, "test executable identity");
        File.SetLastWriteTimeUtc(
            cacheProofEboot,
            DateTime.UtcNow.AddMinutes(-10));
        var cacheProofExe = Path.Combine(
            cacheProofRoot,
            "rpcs3.exe");
        File.WriteAllText(cacheProofExe, string.Empty);
        var cacheProofEntry = Path.Combine(
            cacheProofRoot,
            "cache",
            serial,
            $"ppu-{prxBase57}-EBOOT.BIN");
        Directory.CreateDirectory(cacheProofEntry);
        Directory.SetLastWriteTimeUtc(
            cacheProofEntry,
            DateTime.UtcNow.AddMinutes(-1));
        var olderCacheProofEntry = Path.Combine(
            cacheProofRoot,
            "cache",
            serial,
            $"ppu-{olderCacheBase57}-EBOOT.BIN");
        Directory.CreateDirectory(olderCacheProofEntry);
        Directory.SetLastWriteTimeUtc(
            olderCacheProofEntry,
            DateTime.UtcNow.AddMinutes(-5));
        var cacheProofInstall = Rpcs3InstallService.FromExe(
            cacheProofExe);
        var cacheProofIdentity = new PatchHashProbeService().Enrich(
            cacheProofInstall,
            exactGame with
            {
                InstPath = cacheProofGamePath,
                PpuHash = null,
                HashEvi = HashEvi.None
            });
        Assert(cacheProofIdentity.PpuList.Count == 1 &&
               cacheProofIdentity.PpuList.Contains(
                   exactHash) &&
               cacheProofIdentity.Hashes.Contains(olderCacheHash),
            "Expected only the newest RPCS3 EBOOT cache entry to become authoritative while older valid entries remain historical evidence.");
        var currentCacheMismatch = compatibility.Evaluate(
            NewPatch(
                "Different current PPU",
                HashKind.Ppu,
                otherHash,
                target,
                PatSrc.Official),
            [cacheProofIdentity]);
        Assert(currentCacheMismatch.Status == MatStat.HashBad &&
               currentCacheMismatch.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Blocked,
                   Code: PatchCompatibilityReasonCode.ExeBad,
                   CanOn: false,
                   UseGuard: false
               },
            "Expected a verified current EBOOT cache mismatch to block official patch activation.");

        var moduleExecutable = Path.Combine(
            cacheProofGamePath,
            "PS3_GAME",
            "USRDIR",
            "GOWA.SELF");
        File.WriteAllText(moduleExecutable, "secondary executable identity");
        File.SetLastWriteTimeUtc(
            moduleExecutable,
            DateTime.UtcNow.AddMinutes(-10));
        var moduleCacheProofEntry = Path.Combine(
            cacheProofRoot,
            "cache",
            serial,
            $"ppu-{otherHash}-GOWA.SELF");
        Directory.CreateDirectory(moduleCacheProofEntry);
        Directory.SetLastWriteTimeUtc(
            moduleCacheProofEntry,
            DateTime.UtcNow.AddMinutes(-1));
        var multiModuleIdentity = new PatchHashProbeService().Enrich(
            cacheProofInstall,
            exactGame with
            {
                InstPath = cacheProofGamePath,
                PpuHash = null,
                HashEvi = HashEvi.None
            });
        Assert(multiModuleIdentity.PpuList.SequenceEqual([exactHash]) &&
               multiModuleIdentity.Hashes.Contains(otherHash),
            "Expected only current EBOOT cache evidence to be authoritative while a matching secondary PPU module remains positive evidence.");
        var secondaryModuleExact = compatibility.Evaluate(
            NewPatch(
                "Current secondary PPU",
                HashKind.Ppu,
                otherHash,
                target,
                PatSrc.Official),
            [multiModuleIdentity]);
        Assert(secondaryModuleExact.Cmp?.Verdict ==
                   PatchCompatibilityVerdict.Compat,
            "Expected a secondary PPU cache hash to prove an exact positive match without becoming main-executable contradiction evidence.");
        Directory.Delete(moduleCacheProofEntry, recursive: true);
        File.Delete(moduleExecutable);

        Directory.SetLastWriteTimeUtc(
            cacheProofEntry,
            DateTime.UtcNow.AddMinutes(-20));
        File.SetLastWriteTimeUtc(
            cacheProofEboot,
            DateTime.UtcNow.AddMinutes(-1));
        var staleCacheIdentity = new PatchHashProbeService().Enrich(
            cacheProofInstall,
            exactGame with
            {
                InstPath = cacheProofGamePath,
                PpuHash = null,
                HashEvi = HashEvi.None
            });
        Assert(staleCacheIdentity.PpuList.Count == 0,
            "Expected a cache older than EBOOT.BIN to remain positive-only historical evidence.");
        var historicalCacheExact = compatibility.Evaluate(
            NewPatch(
                "Historical positive PPU",
                HashKind.Ppu,
                exactHash,
                target,
                PatSrc.Official),
            [staleCacheIdentity]);
        Assert(historicalCacheExact.Cmp?.Verdict ==
                   PatchCompatibilityVerdict.Compat,
            "Expected historical PPU cache evidence to prove an exact positive match without proving a mismatch.");

        var probeRoot = Path.Combine(tempRoot, "compat-probe");
        Directory.CreateDirectory(Path.Combine(probeRoot, "log"));
        var probeExe = Path.Combine(probeRoot, "rpcs3.exe");
        File.WriteAllText(probeExe, string.Empty);
        File.WriteAllText(
            Path.Combine(probeRoot, "log", "RPCS3.log"),
            $"SYS: Serial: {serial}{Environment.NewLine}" +
            $"ppu_loader: PPU executable hash: PPU-{exactHash}{Environment.NewLine}" +
            $"sys_spu: Loaded SPU image: SPU-{otherHash} (<- 0){Environment.NewLine}" +
            $"ppu_loader: PRX hash of module[0]: PRX-{prxBase57}-0{Environment.NewLine}" +
            $"ppu_loader: OVL hash of overlay: OVL-{otherHash}{Environment.NewLine}");
        var probeInstall = Rpcs3InstallService.FromExe(probeExe);
        var probedIdentity = new PatchHashProbeService().Enrich(
            probeInstall,
            exactGame with
            {
                PpuHash = null,
                SpuHash = null,
                HashEvi = HashEvi.None,
                PrxRaw = null,
                OvlRaw = null
            });
        Assert(probedIdentity.Hashes.Contains(exactHash) &&
               probedIdentity.SpuList.Contains(otherHash) &&
               probedIdentity.PrxList.Contains(exactHash + "-0") &&
               probedIdentity.OvlList.Contains(otherHash),
            "Expected the RPCS3 log probe to collect PPU, SPU, PRX, and OVL runtime identity evidence from one serial-scoped boot session.");

        Console.WriteLine("Unified patch compatibility architecture PASS.");

        static Rpcs3PatchEntry NewPatch(
            string title,
            HashKind kind,
            string hash,
            R3PatTgt target,
            PatSrc source)
        {
            return new Rpcs3PatchEntry(
                Title: title,
                HashKind: kind,
                Hash: hash,
                GameTgt: [target],
                PatType: "Compatibility",
                Risk: R3Config.Mdl.PatRisk.Unknown,
                Notes: null,
                Author: "Test",
                PatVer: "1.0",
                Group: null,
                Source: source,
                Class: source == PatSrc.Can
                    ? PatClass.Canary
                    : PatClass.Main);
        }
    }

}
