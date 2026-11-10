using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Composes the Runtime Context snapshot from evidence the existing scanners
// already produced (hardware probe, install scan, RPCS3 build banner, game
// descriptor) and the already projected Game Settings model. It never scans
// anything itself, never writes, and never decides a policy: every fact is
// either proven, or Unknown/Unavailable/Partial/Conflict/NotApplicable.
public static class RuntimeContextBuilder
{
    private static readonly string[] CpuKeys =
    [
        "CpuArchitecture", "CpuPhysicalCores", "CpuSmt", "CpuHybrid", "CpuNativeThreads", "CpuFeatures", "Rpcs3SchedulerLayout"
    ];

    private static readonly string[] BuildCpuKeys =
        ["BuildArchitectureMatch", "Rpcs3SpuDecoders", "Rpcs3SpuDecodersLlvm", "Rpcs3AccurateDfmaUi"];

    private static readonly string[] UnscannedHardwareKeys =
        ["Cpu", "CpuThreads", "CpuVendor", "Gpu", "Ram", "Os", "OsArchitecture", .. CpuKeys];

    private static readonly string[] UnidentifiedBuildKeys =
        ["CurrentBuild", "BuildVersion", "BuildNumber", "BuildCommit", "BuildBranch", "BuildStage", "BuildArchitecture", .. BuildCpuKeys];

    public static RuntimeContextSnapshot Build(
        HwInfo? hardware,
        Rpcs3InstallDescriptor? install,
        Rpcs3BuildIdentity? build,
        Rpcs3GameDescriptor? game,
        GameSettingsModel? settings,
        Rpcs3BuildCatalog? builds,
        string hostPlatform = Rpcs3BuildCatalog.WindowsPlatform,
        DiagnosticEvidenceCatalog? evidence = null)
    {
        var facts = new List<RuntimeFact>();
        AddHardware(facts, hardware);
        AddInstallation(facts, install);
        var buildState = AddBuild(facts, build, hardware?.Cpu);
        var trusted = TrustedPpu(game);
        var gameState = AddGame(facts, game, trusted);
        AddCompatibility(facts, game, settings, builds, build, hostPlatform, evidence);
        return new RuntimeContextSnapshot(facts, buildState, gameState, trusted);
    }

    private static void AddHardware(List<RuntimeFact> facts, HwInfo? hardware)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Hardware;
        if (hardware is null)
        {
            foreach (var key in UnscannedHardwareKeys)
            {
                facts.Add(new RuntimeFact(group, key, null, EvidenceState.Unavailable, "NotScanned"));
            }

            return;
        }

        var cpuKnown = !IsPlaceholder(hardware.CpuName, "Unknown CPU");

        // One logical-processor count: the topology the OS reported when it was
        // read (all processor groups), else the coarse runtime count.
        int? logicalProcessors = hardware.Cpu?.LogicalProcessorsTotal ?? (hardware.CpuNum > 0 ? (int?)hardware.CpuNum : null);
        facts.Add(new RuntimeFact(
            group,
            "Cpu",
            cpuKnown ? hardware.CpuName : null,
            cpuKnown ? EvidenceState.Known : EvidenceState.Unknown));
        facts.Add(new RuntimeFact(
            group,
            "CpuThreads",
            logicalProcessors?.ToString(CultureInfo.InvariantCulture),
            logicalProcessors is null ? EvidenceState.Unknown : EvidenceState.Known));
        facts.Add(new RuntimeFact(
            group,
            "CpuVendor",
            hardware.CpuVendor == HwVendor.Unknown ? null : VendorText(hardware.CpuVendor),
            hardware.CpuVendor == HwVendor.Unknown ? EvidenceState.Unknown : EvidenceState.Known));

        if (hardware.GpuAdapters.Count == 0)
        {
            var named = !IsPlaceholder(hardware.GpuName, "Unknown GPU");
            facts.Add(new RuntimeFact(
                group,
                "Gpu",
                named ? hardware.GpuName : null,
                named ? EvidenceState.Partial : EvidenceState.Unknown,
                named ? "VendorFromNameOnly" : null));
        }
        else
        {
            foreach (var adapter in hardware.GpuAdapters)
            {
                var vendorText = adapter.Vendor == HwVendor.Unknown
                    ? null
                    : VendorText(adapter.Vendor);
                facts.Add(new RuntimeFact(
                    group,
                    "Gpu",
                    vendorText is null ? adapter.Name : $"{adapter.Name} ({vendorText})",
                    adapter.VendorFromPci ? EvidenceState.Known : EvidenceState.Partial,
                    adapter.VendorFromPci ? null : "VendorFromNameOnly"));
            }

            if (hardware.GpuAdapters.Count > 1)
            {
                // Which adapter RPCS3 renders with is only known from RPCS3's
                // own configuration, never from this list.
                facts.Add(new RuntimeFact(group, "GpuSelection", null, EvidenceState.Unknown, "MultipleAdaptersNotAssumed"));
            }
        }

        facts.Add(new RuntimeFact(
            group,
            "Ram",
            hardware.TotalB is null ? null : $"{hardware.TotalB.Value.ToString("0.#", CultureInfo.InvariantCulture)} GB",
            hardware.TotalB is null ? EvidenceState.Unknown : EvidenceState.Known));
        facts.Add(new RuntimeFact(
            group,
            "Os",
            hardware.OsDescription,
            hardware.OsDescription is null ? EvidenceState.Unknown : EvidenceState.Known));
        facts.Add(new RuntimeFact(
            group,
            "OsArchitecture",
            hardware.OsArchitecture is null ? null : hardware.OsArchitecture.ToUpperInvariant(),
            hardware.OsArchitecture is null ? EvidenceState.Unknown : EvidenceState.Known,
            hardware.ProcessArchitecture is not null &&
            !string.Equals(hardware.OsArchitecture, hardware.ProcessArchitecture, StringComparison.OrdinalIgnoreCase)
                ? "ProcessArchitectureDiffers"
                : null));
        AddCpu(facts, hardware);
    }

    // Host CPU facts as RPCS3's own probes would read them. Each is proven,
    // or Unknown/Unsupported with the reason; nothing is inferred from the
    // CPU name and nothing is advised.
    private static void AddCpu(List<RuntimeFact> facts, HwInfo hardware)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Hardware;
        var cpu = hardware.Cpu;
        if (cpu is null)
        {
            foreach (var key in CpuKeys)
            {
                facts.Add(new RuntimeFact(group, key, null, EvidenceState.Unknown, "CpuNotProbed"));
            }

            return;
        }

        var emulated = HostCpuAnalyzer.IsEmulated(hardware.OsArchitecture, hardware.ProcessArchitecture);
        facts.Add(cpu.Architecture switch
        {
            CpuArchitecture.X64 => new RuntimeFact(group, "CpuArchitecture", "x64", EvidenceState.Known, emulated ? "CpuEmulatedProcess" : null),
            CpuArchitecture.Arm64 => new RuntimeFact(group, "CpuArchitecture", "ARM64", EvidenceState.Known, emulated ? "CpuEmulatedProcess" : null),
            CpuArchitecture.Unsupported => new RuntimeFact(group, "CpuArchitecture", hardware.OsArchitecture, EvidenceState.Unsupported, "ArchitectureNotSupportedByRpcs3"),
            _ => new RuntimeFact(group, "CpuArchitecture", null, EvidenceState.Unknown, "CpuNotProbed")
        });

        facts.Add(new RuntimeFact(
            group,
            "CpuPhysicalCores",
            cpu.PhysicalCores?.ToString(CultureInfo.InvariantCulture),
            cpu.PhysicalCores is null ? EvidenceState.Unknown : EvidenceState.Known,
            cpu.PhysicalCores is null ? "TopologyNotRead" : null));

        facts.Add(cpu.Smt switch
        {
            SmtState.Active => new RuntimeFact(group, "CpuSmt", null, EvidenceState.Known, "SmtActive"),
            SmtState.Inactive => new RuntimeFact(group, "CpuSmt", null, EvidenceState.Known, "SmtInactive"),
            _ => new RuntimeFact(group, "CpuSmt", null, EvidenceState.Unknown, "TopologyNotRead")
        });

        facts.Add(cpu.Hybrid switch
        {
            HybridTopology.Uniform => new RuntimeFact(group, "CpuHybrid", null, EvidenceState.Known, "HybridUniform"),
            HybridTopology.Heterogeneous => new RuntimeFact(
                group,
                "CpuHybrid",
                string.Join(
                    " + ",
                    cpu.CoreClasses.Select(coreClass => string.Create(
                        CultureInfo.InvariantCulture,
                        $"{coreClass.PhysicalCores} cores (class {coreClass.EfficiencyClass})"))),
                EvidenceState.Known,
                "HybridNoRpcs3Handling"),
            _ => new RuntimeFact(group, "CpuHybrid", null, EvidenceState.Unknown, "TopologyNotRead")
        });

        facts.Add(new RuntimeFact(
            group,
            "CpuNativeThreads",
            cpu.NativeThreadCount?.ToString(CultureInfo.InvariantCulture),
            cpu.NativeThreadCount is null
                ? EvidenceState.Unknown
                : cpu.ThreadCountIsPerGroup ? EvidenceState.Partial : EvidenceState.Known,
            cpu.NativeThreadCount is null ? "CpuNotProbed" : cpu.ThreadCountIsPerGroup ? "ThreadCountIsPerGroup" : null));

        facts.Add(FeatureFact(group, cpu));

        var layoutText = cpu.SchedulerLayout switch
        {
            Rpcs3SchedulerLayout.AmdCcx => "amd_ccx",
            Rpcs3SchedulerLayout.IntelHt => "intel_ht",
            Rpcs3SchedulerLayout.Generic => "generic",
            _ => null
        };
        facts.Add(layoutText is null
            ? new RuntimeFact(group, "Rpcs3SchedulerLayout", null, EvidenceState.Unknown, "SchedulerLayoutUnknown")
            : new RuntimeFact(
                group,
                "Rpcs3SchedulerLayout",
                layoutText,
                cpu.SchedulerEffect == SchedulerAffinityEffect.Unknown ? EvidenceState.Partial : EvidenceState.Known,
                cpu.SchedulerEffect switch
                {
                    SchedulerAffinityEffect.NoChange => "SchedulerNoAffinityChange",
                    SchedulerAffinityEffect.LayoutSpecific => "SchedulerLayoutSpecific",
                    _ => "SchedulerEffectUnknown"
                }));
    }

    private static RuntimeFact FeatureFact(RuntimeFactGroup group, HostCpuInfo cpu)
    {
        const string key = "CpuFeatures";
        if (cpu.Features.Count == 0 || cpu.Features.All(fact => fact.Presence == CpuFeaturePresence.Unknown))
        {
            var emulated = cpu.Features.Any(fact => fact.Source == "EmulatedProcess");
            return new RuntimeFact(group, key, null, EvidenceState.Unknown, emulated ? "CpuEmulatedProcess" : "CpuFeaturesUnavailable");
        }

        var present = cpu.Features
            .Where(fact => fact.Presence == CpuFeaturePresence.Present)
            .Select(fact => Rpcs3CpuSemantics.DisplayName(fact.Feature))
            .ToArray();
        var partial = cpu.Features.Any(fact => fact.Presence == CpuFeaturePresence.Unknown);
        return new RuntimeFact(
            group,
            key,
            present.Length == 0 ? null : string.Join(", ", present),
            partial ? EvidenceState.Partial : EvidenceState.Known,
            partial ? "CpuFeaturesPartial" : present.Length == 0 ? "NoneDetected" : null);
    }

    // RPCS3-side facts that depend on the architecture RPCS3 itself logged,
    // never on the CPU name or on the R3Config process.
    private static void AddBuildCpu(List<RuntimeFact> facts, Rpcs3BuildIdentity build, HostCpuInfo? cpu)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Build;
        var rpcs3 = HostCpuAnalyzer.MapArchitecture(build.Architecture);
        var logged = string.IsNullOrWhiteSpace(build.Architecture) ? null : build.Architecture.Trim();

        if (rpcs3 == CpuArchitecture.Unknown)
        {
            foreach (var key in BuildCpuKeys)
            {
                facts.Add(new RuntimeFact(group, key, null, EvidenceState.Unknown, "BuildArchitectureNotLogged"));
            }

            return;
        }

        var host = cpu?.Architecture ?? CpuArchitecture.Unknown;
        facts.Add(host == CpuArchitecture.Unknown
            ? new RuntimeFact(group, "BuildArchitectureMatch", logged, EvidenceState.Unknown, "CpuNotProbed")
            : rpcs3 == host
                ? new RuntimeFact(group, "BuildArchitectureMatch", logged, EvidenceState.Known, "ArchitectureMatches")
                : new RuntimeFact(group, "BuildArchitectureMatch", logged, EvidenceState.Partial, "ArchitectureDiffers"));

        // Source-defined decoders only. No LLVM_AVAILABLE evidence exists, so the
        // decoders that need an LLVM build are listed separately as unproven.
        var decoders = Rpcs3CpuSemantics.SpuDecoders(rpcs3);
        if (decoders.Count == 0)
        {
            facts.Add(new RuntimeFact(group, "Rpcs3SpuDecoders", logged, EvidenceState.Unsupported, "ArchitectureNotSupportedByRpcs3"));
            facts.Add(new RuntimeFact(group, "Rpcs3SpuDecodersLlvm", null, EvidenceState.Unsupported, "ArchitectureNotSupportedByRpcs3"));
        }
        else
        {
            facts.Add(new RuntimeFact(
                group,
                "Rpcs3SpuDecoders",
                string.Join(", ", decoders.Where(decoder => decoder.Availability == DecoderAvailability.Available).Select(decoder => decoder.Decoder)),
                EvidenceState.Known,
                decoders.Any(decoder => decoder.Availability == DecoderAvailability.ReplacedAtStart) ? "AsmjitReplacedOnArm64" : null));
            var needLlvm = decoders.Where(decoder => decoder.Availability == DecoderAvailability.BuildCapabilityUnknown).ToArray();
            facts.Add(new RuntimeFact(
                group,
                "Rpcs3SpuDecodersLlvm",
                needLlvm.Length == 0 ? null : string.Join(", ", needLlvm.Select(decoder => decoder.Decoder)),
                needLlvm.Length == 0 ? EvidenceState.NotApplicable : EvidenceState.Unknown,
                needLlvm.Length == 0 ? null : "LlvmBuildNotProven"));
        }

        facts.Add(Rpcs3CpuSemantics.AccurateDfmaUiLocked(rpcs3, cpu) switch
        {
            true => new RuntimeFact(group, "Rpcs3AccurateDfmaUi", null, EvidenceState.Known, "DfmaUiLocked"),
            false => new RuntimeFact(group, "Rpcs3AccurateDfmaUi", null, EvidenceState.Known, "DfmaUiEditable"),
            null => new RuntimeFact(group, "Rpcs3AccurateDfmaUi", null, EvidenceState.Unknown, "Rpcs3CpuViewNotProven")
        });
    }

    private static void AddInstallation(List<RuntimeFact> facts, Rpcs3InstallDescriptor? install)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Installation;
        if (install is null)
        {
            foreach (var key in new[] { "Rpcs3Executable", "DataRoot", "ConfigRoot", "DataBesideExecutable", "PatchesFolder", "PatchConfig" })
            {
                facts.Add(new RuntimeFact(group, key, null, EvidenceState.Unavailable, "Rpcs3NotFound"));
            }

            return;
        }

        facts.Add(new RuntimeFact(group, "Rpcs3Executable", install.ExePath, EvidenceState.Known));

        // DataRoot is only what the install scan proved; the executable
        // directory is never assumed to be the data root.
        var hasDataRoot = !string.IsNullOrWhiteSpace(install.DataRoot);
        facts.Add(new RuntimeFact(
            group,
            "DataRoot",
            hasDataRoot ? install.DataRoot : null,
            hasDataRoot ? EvidenceState.Known : EvidenceState.Unknown,
            hasDataRoot ? null : "DataRootNotProven"));
        facts.Add(new RuntimeFact(group, "ConfigRoot", install.CfgPath, EvidenceState.Known));

        facts.Add(new RuntimeFact(
            group,
            "DataBesideExecutable",
            null,
            hasDataRoot ? EvidenceState.Known : EvidenceState.Unknown,
            hasDataRoot
                ? SamePath(install.DataRoot!, install.RootPath) ? "Yes" : "No"
                : "DataRootNotProven"));
        facts.Add(new RuntimeFact(group, "PatchesFolder", install.PatPath, EvidenceState.Known));
        facts.Add(new RuntimeFact(group, "PatchConfig", install.PatCfg, EvidenceState.Known));
    }

    private static EvidenceState AddBuild(List<RuntimeFact> facts, Rpcs3BuildIdentity? build, HostCpuInfo? cpu)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Build;
        if (build is null || build.State == Rpcs3BuildIdentityState.Unavailable)
        {
            foreach (var key in UnidentifiedBuildKeys)
            {
                facts.Add(new RuntimeFact(group, key, null, EvidenceState.Unavailable, "BuildNotIdentified"));
            }

            return EvidenceState.Unavailable;
        }

        var overall = build.State switch
        {
            Rpcs3BuildIdentityState.Exact when build.IsExact => EvidenceState.Exact,
            Rpcs3BuildIdentityState.Conflict => EvidenceState.Conflict,
            _ => EvidenceState.Partial
        };

        // The combined "version-build" is shown only when every component is
        // proven; a partial identity keeps its parts, never a manufactured one.
        facts.Add(new RuntimeFact(
            group,
            "CurrentBuild",
            overall == EvidenceState.Exact ? $"{build.VersionText}-{build.BuildNumber}" : null,
            overall,
            overall == EvidenceState.Exact ? null : overall == EvidenceState.Conflict ? "BuildConflict" : "BuildPartial"));

        Part(facts, "BuildVersion", build.VersionText, build.State);
        Part(facts, "BuildNumber", build.BuildNumber?.ToString(CultureInfo.InvariantCulture), build.State);
        Part(facts, "BuildCommit", build.Commit, build.State);
        Part(facts, "BuildBranch", build.Branch, build.State);
        Part(facts, "BuildStage", build.Stage, build.State);
        Part(facts, "BuildArchitecture", build.Architecture, build.State);
        AddBuildCpu(facts, build, cpu);
        return overall;
    }

    private static void Part(List<RuntimeFact> facts, string key, string? value, Rpcs3BuildIdentityState state)
    {
        facts.Add(new RuntimeFact(
            RuntimeFactGroup.Build,
            key,
            string.IsNullOrWhiteSpace(value) ? null : value,
            string.IsNullOrWhiteSpace(value)
                ? EvidenceState.Unknown
                : state == Rpcs3BuildIdentityState.Conflict ? EvidenceState.Conflict : EvidenceState.Known));
    }

    // The selected game's trusted PPU hashes: PpuList (the probe's
    // authoritative set, empty for cache-only evidence) plus a saved H-Canary
    // binding. Each carries only the source and time the descriptor proves.
    private static IReadOnlyList<RuntimePpuHash> TrustedPpu(Rpcs3GameDescriptor? game)
    {
        if (game is null)
        {
            return [];
        }

        var binding = new HashSet<string>(game.HCanPpuList, StringComparer.OrdinalIgnoreCase);
        var bound = game.HCanPpu is not null;
        // HashEvi.None records no evidence kind, so no source is claimed for it.
        var probeSource = game.HashEvi switch
        {
            HashEvi.PlainLog or HashEvi.ZipLog or HashEvi.Cache => RuntimePpuHashSource.Rpcs3Runtime,
            HashEvi.Meta => RuntimePpuHashSource.Metadata,
            _ => RuntimePpuHashSource.Unknown
        };

        return
        [
            .. game.PpuList
                .Concat(game.HCanPpuList)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(hash => binding.Contains(hash)
                    ? new RuntimePpuHash(hash, RuntimePpuHashSource.HCanBinding, game.HashTime)
                    : bound
                        ? new RuntimePpuHash(hash, RuntimePpuHashSource.Analysis, null)
                        : new RuntimePpuHash(hash, probeSource, game.HashTime))
        ];
    }

    private static EvidenceState AddGame(List<RuntimeFact> facts, Rpcs3GameDescriptor? game, IReadOnlyList<RuntimePpuHash> trusted)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Game;
        if (game is null)
        {
            facts.Add(new RuntimeFact(group, "Game", null, EvidenceState.NotApplicable, "NoGameSelected"));
            return EvidenceState.NotApplicable;
        }

        var serial = string.IsNullOrWhiteSpace(game.Serial) ? null : game.Serial.Trim();
        var version = string.IsNullOrWhiteSpace(game.Version) ? null : game.Version.Trim();
        var category = string.IsNullOrWhiteSpace(game.Category) ? null : game.Category.Trim();
        var edition = serial is null ? null : NoteScope.EditionOf(serial);

        // Only trusted hashes are the game's hash; an untrusted per-game cache
        // hash (game.Hashes minus PpuList) is never presented as a fact.
        var ppu = trusted.Count > 0 ? string.Join(", ", trusted.Select(item => item.Hash)) : null;

        facts.Add(Fact(group, "GameSerial", serial));
        facts.Add(Fact(group, "GameTitle", string.IsNullOrWhiteSpace(game.Title) ? null : game.Title.Trim()));
        facts.Add(Fact(group, "GameVersion", version));
        facts.Add(Fact(group, "GameCategory", category));
        facts.Add(new RuntimeFact(
            group,
            "GameEdition",
            edition,
            edition is null ? EvidenceState.Unknown : EvidenceState.Known,
            edition is null ? null : "FromSerialPrefix"));
        facts.Add(Fact(group, "GamePpuHash", ppu));

        // Exact needs the serial, the installed version and a runtime hash.
        var proven = new[] { serial, version, ppu }.Count(value => value is not null);
        var overall = serial is null
            ? EvidenceState.Unknown
            : proven == 3 ? EvidenceState.Exact : EvidenceState.Partial;
        facts.Add(new RuntimeFact(
            group,
            "GameIdentity",
            null,
            overall,
            overall == EvidenceState.Exact ? null : "GameIdentityIncomplete"));
        return overall;
    }

    private static RuntimeFact Fact(RuntimeFactGroup group, string key, string? value) =>
        new(group, key, value, value is null ? EvidenceState.Unknown : EvidenceState.Known);

    private static void AddCompatibility(
        List<RuntimeFact> facts,
        Rpcs3GameDescriptor? game,
        GameSettingsModel? settings,
        Rpcs3BuildCatalog? builds,
        Rpcs3BuildIdentity? build,
        string hostPlatform,
        DiagnosticEvidenceCatalog? evidence)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Compatibility;
        if (game is null)
        {
            facts.Add(new RuntimeFact(group, "Compatibility", null, EvidenceState.NotApplicable, "NoGameSelected"));
            return;
        }

        if (settings is null)
        {
            facts.Add(new RuntimeFact(group, "Compatibility", null, EvidenceState.Unknown, "EvidenceNotLoaded"));
            return;
        }

        var rows = settings.Sections.Sum(section => section.Rows.Count);
        facts.Add(new RuntimeFact(
            group,
            "SettingsApplicable",
            rows.ToString(CultureInfo.InvariantCulture),
            rows == 0 ? EvidenceState.Unknown : EvidenceState.Known,
            rows == 0 ? "NoDocumentedSettings" : null));

        var applicableNotes = settings.Notes.Count(note => note.Applicability == WikiNoteApplicability.General);
        facts.Add(new RuntimeFact(
            group,
            "NotesApplicable",
            applicableNotes.ToString(CultureInfo.InvariantCulture),
            settings.Notes.Count == 0 ? EvidenceState.Unknown : EvidenceState.Known,
            settings.Notes.Count == 0 ? "NoDocumentedNotes" : null));

        var hardware = settings.Notes.Where(note => !string.IsNullOrWhiteSpace(note.Note.HardwareCondition)).ToArray();
        facts.Add(new RuntimeFact(
            group,
            "HardwareConditionalNotes",
            hardware.Length == 0
                ? null
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{hardware.Count(note => note.Applicability == WikiNoteApplicability.General)} / {hardware.Count(note => note.Applicability == WikiNoteApplicability.OtherHardware)} / {hardware.Count(note => note.Applicability == WikiNoteApplicability.Unresolved)}"),
            hardware.Length == 0 ? EvidenceState.NotApplicable : EvidenceState.Known,
            hardware.Length == 0 ? "NoHardwareConditions" : "ApplicableOtherUnresolved"));

        AddRpcn(facts, settings, builds, build, hostPlatform);
        AddRpcnSources(facts, settings.WikiPage, evidence);
    }

    // Source-separated RPCN evidence for the game's exact Wiki page. The
    // official browser listing is a dated observation, the community index is a
    // community claim; neither is an RPCN test, and neither is ever applied.
    // Only GameScoped records with a resolved Wiki identity reach this point.
    private static void AddRpcnSources(
        List<RuntimeFact> facts,
        string? wikiPage,
        DiagnosticEvidenceCatalog? evidence)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Compatibility;
        var records = (evidence ?? DiagnosticEvidenceCatalog.Empty).ForWikiPage(wikiPage);

        foreach (var listing in records.Where(record =>
                     record.Role == EvidenceSourceRole.OfficialCurrentBrowser &&
                     record.Claim == EvidenceClaimType.CurrentBrowserListing &&
                     !string.IsNullOrWhiteSpace(record.Value)))
        {
            facts.Add(new RuntimeFact(
                group,
                "RpcnCurrentBrowser",
                $"{listing.Subject.CommId} ({listing.Value})",
                EvidenceState.Known,
                "CurrentBrowserObservation"));
        }

        foreach (var claim in records.Where(record =>
                     record.Role == EvidenceSourceRole.CommunityPrivateServerIndex &&
                     record.Claim is EvidenceClaimType.CommunityWorkingOnline or EvidenceClaimType.CommunityRequiresCfw &&
                     !string.IsNullOrWhiteSpace(record.Value)))
        {
            facts.Add(new RuntimeFact(
                group,
                claim.Claim == EvidenceClaimType.CommunityWorkingOnline ? "RpcnCommunity" : "RpcnCommunityCfw",
                claim.Value,
                EvidenceState.Partial,
                "CommunityIndexOnly"));
        }
    }

    private static void AddRpcn(
        List<RuntimeFact> facts,
        GameSettingsModel settings,
        Rpcs3BuildCatalog? builds,
        Rpcs3BuildIdentity? build,
        string hostPlatform)
    {
        const RuntimeFactGroup group = RuntimeFactGroup.Compatibility;
        if (settings.Online.Count == 0)
        {
            facts.Add(new RuntimeFact(group, "Rpcn", null, EvidenceState.Unknown, "NoRpcnEvidence"));
            return;
        }

        if (settings.OnlineForInstalled.Count == 0)
        {
            facts.Add(new RuntimeFact(
                group,
                "Rpcn",
                null,
                settings.OnlineVersionUnknown ? EvidenceState.Unknown : EvidenceState.Known,
                settings.OnlineVersionUnknown ? "InstalledVersionUnknown" : "VersionMismatch"));
            return;
        }

        facts.Add(new RuntimeFact(group, "Rpcn", null, EvidenceState.Known, "Applicable"));

        var current = build is { IsExact: true, BuildNumber: { } number }
            ? $"{build.VersionText}-{number}"
            : null;

        // A tested build is documented evidence, never a requirement, and
        // never a suggestion to change the installed build.
        foreach (var tested in settings.OnlineForInstalled
                     .Select(evidence => evidence.Rpcs3VersionTested?.Trim())
                     .Where(text => !string.IsNullOrWhiteSpace(text) &&
                                    !string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.Ordinal))
        {
            var resolution = (builds ?? Rpcs3BuildCatalog.Empty).Resolve(tested, hostPlatform);
            var same = current is not null &&
                       Rpcs3BuildCatalog.TryParse(tested, out var testedVersion, out var testedBuild) &&
                       Rpcs3BuildCatalog.TryParse(current, out var currentVersion, out var currentBuild) &&
                       testedVersion == currentVersion &&
                       testedBuild == currentBuild;

            var (state, code) = same
                ? (EvidenceState.Known, "SameAsCurrent")
                : resolution.State switch
                {
                    Rpcs3BuildResolutionState.ResolvedOfficialGitHub => (EvidenceState.Known, "OfficialGitHub"),
                    Rpcs3BuildResolutionState.HistoricalArchiveEvidenceOnly => (EvidenceState.Partial, "HistoricalArchiveOnly"),
                    _ => (EvidenceState.Unknown, "BuildUnresolved")
                };
            facts.Add(new RuntimeFact(group, "TestedBuild", tested, state, code));
        }
    }

    private static string VendorText(HwVendor vendor) => vendor switch
    {
        HwVendor.Amd => "AMD",
        HwVendor.Nvidia => "NVIDIA",
        HwVendor.Intel => "Intel",
        _ => "Other"
    };

    private static bool IsPlaceholder(string? value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, placeholder, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

#pragma warning restore IDE0130
