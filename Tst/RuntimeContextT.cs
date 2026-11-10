using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// Domain 5 (Runtime Environment & Compatibility): the evidence snapshot, the
// hardware evidence it hands to Game Settings, the presentation view model and
// the "one scanner, no F: dependency, no game hardcode" guarantees. Every
// fixture is synthetic and generic; nothing here depends on one title.
internal static class RuntimeContextT
{
    private const string Serial = "TEST00002";

    private const string PpuHash = "38656a143a665d48c38ae914e7c826d059efd291";

    private const string Url19000 =
        "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-89abcdef0123456789abcdef0123456789abcdef/rpcs3-v0.0.41-19000-89abcdef_win64_msvc.7z";

    public static void Run()
    {
        AssertHardwareProbe();
        AssertHardwareConditions();
        AssertHardwareFlowsIntoGameSettings();
        AssertSnapshotStates();
        AssertCompatibilityAndBuildResolution();
        AssertViewModelAndLocalization();
        AssertSingleScannerAndNoDevelopmentDependency();
        RuntimePpuHashT.Run();

        Console.WriteLine("Runtime Context (hardware evidence, states, applicability flow, build resolution, presentation, single scanner) PASS.");
    }

    private static GpuAdapterInfo Adapter(string name, string? pci, bool primary = true) =>
        new(name, pci is null ? HwProbe.VendorFromName(name) : HwProbe.VendorFromPci(pci), pci, pci is null ? null : "0001", primary, true);

    private static HwInfo Hardware(params GpuAdapterInfo[] adapters) =>
        new HwProbe(
            readCpuName: () => "Test CPU 9000",
            readLogicalProcessors: () => 16,
            readTotalMemoryBytes: () => 32UL * 1024 * 1024 * 1024,
            readGpuName: () => adapters.FirstOrDefault()?.Name,
            readGpuAdapters: () => adapters,
            readCpuVendor: () => (HwVendor.Amd, "AuthenticAMD"),
            readOs: () => ("Windows 10.0.26200", "26200", "X64", "X64")).Probe();

    private static void AssertHardwareProbe()
    {
        Assert(HwProbe.VendorFromPci("10de") == HwVendor.Nvidia &&
               HwProbe.VendorFromPci("1002") == HwVendor.Amd &&
               HwProbe.VendorFromPci("1022") == HwVendor.Amd &&
               HwProbe.VendorFromPci("8086") == HwVendor.Intel &&
               HwProbe.VendorFromPci("1414") == HwVendor.Other &&
               HwProbe.VendorFromPci(null) == HwVendor.Unknown &&
               HwProbe.VendorFromPci("") == HwVendor.Unknown,
            "The PCI vendor ID decides the adapter vendor; an unknown ID is Other and no ID is Unknown, never a guess.");
        Assert(HwProbe.VendorFromName("Microsoft Basic Render Driver") == HwVendor.Unknown,
            "A name with no vendor token has an Unknown vendor.");

        var hardware = Hardware(
            Adapter("NVIDIA GeForce Test", "10DE"),
            Adapter("Intel(R) UHD Test", "8086", primary: false));
        Assert(hardware.GpuAdapters.Count == 2 &&
               hardware.GpuAdapters[0].Vendor == HwVendor.Nvidia && hardware.GpuAdapters[0].VendorFromPci &&
               hardware.GpuAdapters[1].Vendor == HwVendor.Intel &&
               hardware.GpuName == "NVIDIA GeForce Test" &&
               hardware.CpuVendor == HwVendor.Amd && hardware.CpuVendorId == "AuthenticAMD" &&
               hardware.OsDescription == "Windows 10.0.26200" && hardware.OsBuild == "26200" &&
               hardware.OsArchitecture == "X64" && hardware.ProcessArchitecture == "X64",
            "Every adapter is preserved with its own vendor evidence, alongside CPU vendor and host OS/architecture.");

        var nameOnly = new HwProbe(readGpuName: () => "AMD Radeon Test").Probe();
        Assert(nameOnly.GpuAdapters.Count == 1 && nameOnly.GpuAdapters[0].Vendor == HwVendor.Amd &&
               !nameOnly.GpuAdapters[0].VendorFromPci,
            "A GPU known only by name keeps a name-derived vendor that is marked as not PCI-proven.");

        var failed = new HwProbe(
            readCpuName: () => throw new IOException("Simulated failure."),
            readLogicalProcessors: () => 0,
            readTotalMemoryBytes: () => null,
            readGpuName: () => null).Probe();
        Assert(failed.GpuAdapters.Count == 0 && failed.CpuVendor == HwVendor.Unknown && failed.OsDescription is null,
            "A failed probe carries no adapter, vendor or OS evidence.");
    }

    private static void AssertHardwareConditions()
    {
        var nvidia = Hardware(Adapter("NVIDIA GeForce Test", "10DE"));
        var amd = Hardware(Adapter("AMD Radeon Test", "1002"));
        var mixed = Hardware(Adapter("Intel UHD Test", "8086", primary: true), Adapter("NVIDIA GeForce Test", "10DE", primary: false));

        Assert(NoteScope.HardwareApplies("NVIDIA", nvidia) == true &&
               NoteScope.HardwareApplies("AMD", nvidia) == false &&
               NoteScope.HardwareApplies("AMD", amd) == true &&
               NoteScope.HardwareApplies("NVIDIA", amd) == false,
            "A documented vendor condition matches or mismatches the detected adapter vendor.");
        Assert(NoteScope.HardwareApplies("NVIDIA", (HwInfo?)null) is null &&
               NoteScope.HardwareApplies("NVIDIA", Hardware()) is null &&
               NoteScope.HardwareApplies("NVIDIA", new HwInfo("c", 1, null, "g", HwTier.Unknown, true, "m",
                   Adapters: [new GpuAdapterInfo("Unnamed vendor", HwVendor.Unknown, null, null, true, true)])) is null,
            "Unknown hardware fails closed: no adapter evidence, or an adapter of unknown vendor, is neither a match nor a mismatch.");
        Assert(NoteScope.HardwareApplies("NVIDIA", mixed) is null &&
               NoteScope.HardwareApplies("AMD", mixed) == false,
            "With several vendors present, RPCS3's own adapter is not assumed: the present vendor is unresolved, an absent vendor is a mismatch.");
        Assert(NoteScope.HardwareApplies("GPU", (HwInfo?)null) == true &&
               NoteScope.HardwareApplies("Vulkan", nvidia) == true,
            "A generic GPU/CPU/API word is not a vendor condition and applies everywhere.");
    }

    // Domain 5's hardware evidence reaches Game Settings through the one
    // context object: no consumer scans the machine itself.
    private static void AssertHardwareFlowsIntoGameSettings()
    {
        var note = new WikiFreeTextNote("P", "Known Issues", WikiNoteClass.HardwareConditionalNote,
            "Only on NVIDIA GPUs the game flickers.", null, null, HardwareCondition: "NVIDIA");
        var page = new WikiPageEvidence("P", [Serial], [note], []);
        WikiNoteApplicability Applicability(HwInfo? hardware) =>
            new WikiEvidenceUseCase().Project(
                null,
                new Rpcs3GameDescriptor(Serial, "Synthetic", string.Empty, "1.00", null, null),
                null,
                WikiEvidenceCatalog.FromPages("t", "t", "t", [page]),
                null,
                hardware).Notes.Single().Applicability;

        Assert(Applicability(Hardware(Adapter("NVIDIA GeForce Test", "10DE"))) == WikiNoteApplicability.General &&
               Applicability(Hardware(Adapter("AMD Radeon Test", "1002"))) == WikiNoteApplicability.OtherHardware &&
               Applicability(null) == WikiNoteApplicability.Unresolved,
            "An NVIDIA-only note applies on NVIDIA, is not applicable on AMD and stays unresolved when the hardware is unknown.");
    }

    private static RuntimeFact Fact(RuntimeContextSnapshot snapshot, string key) =>
        snapshot.Find(key) ?? throw new InvalidOperationException($"Missing runtime fact {key}.");

    private static Rpcs3InstallDescriptor Install(string? dataRoot) =>
        new(@"C:\Emu\RPCS3", @"C:\Emu\RPCS3\rpcs3.exe", @"C:\Emu\RPCS3\config", @"C:\Emu\RPCS3\config\custom",
            @"C:\Emu\RPCS3\patches", @"C:\Emu\RPCS3\config\patch_config.yml", dataRoot);

    private static void AssertSnapshotStates()
    {
        var empty = RuntimeContextBuilder.Build(null, null, null, null, null, null);
        Assert(empty.BuildIdentityState == EvidenceState.Unavailable &&
               Fact(empty, "Cpu").State == EvidenceState.Unavailable &&
               Fact(empty, "Rpcs3Executable").State == EvidenceState.Unavailable &&
               Fact(empty, "CurrentBuild").State == EvidenceState.Unavailable && Fact(empty, "CurrentBuild").Value is null &&
               Fact(empty, "Game").State == EvidenceState.NotApplicable &&
               empty.Facts.All(fact => fact.State is EvidenceState.Unavailable or EvidenceState.NotApplicable),
            "With no evidence every fact is Unavailable or Not applicable; nothing becomes Known or Exact.");

        var hardware = Hardware(Adapter("NVIDIA GeForce Test", "10DE"), Adapter("Intel UHD Test", "8086", primary: false));
        var exact = new Rpcs3BuildIdentity("0.0.42", 19843, "abcdef1", "master", "master", "x64", Rpcs3BuildIdentityState.Exact, []);
        var game = new Rpcs3GameDescriptor(Serial, "Synthetic Game", string.Empty, "1.05", PpuHash, null, Category: "DG");
        var full = RuntimeContextBuilder.Build(hardware, Install(@"C:\Emu\RPCS3"), exact, game, null, null);

        Assert(Fact(full, "Cpu").State == EvidenceState.Known && Fact(full, "CpuVendor").Value == "AMD" &&
               full.Facts.Count(fact => fact.Key == "Gpu") == 2 &&
               full.Facts.Where(fact => fact.Key == "Gpu").All(fact => fact.State == EvidenceState.Known) &&
               Fact(full, "GpuSelection").State == EvidenceState.Unknown &&
               Fact(full, "Ram").Value == "32 GB" &&
               Fact(full, "Os").Value == "Windows 10.0.26200" && Fact(full, "OsArchitecture").Value == "X64",
            "Hardware facts come from the probe, keep every adapter, and never assume which GPU RPCS3 uses.");
        Assert(Fact(full, "DataRoot").State == EvidenceState.Known &&
               Fact(full, "DataBesideExecutable").Code == "Yes" &&
               Fact(RuntimeContextBuilder.Build(null, Install(@"D:\RPCS3Data"), null, null, null, null), "DataBesideExecutable").Code == "No",
            "Data-beside-executable is stated only from the proven DataRoot.");
        var noDataRoot = RuntimeContextBuilder.Build(null, Install(null), null, null, null, null);
        Assert(Fact(noDataRoot, "DataRoot").State == EvidenceState.Unknown &&
               Fact(noDataRoot, "DataBesideExecutable").State == EvidenceState.Unknown &&
               Fact(noDataRoot, "Rpcs3Executable").State == EvidenceState.Known,
            "An unproven DataRoot is Unknown; the executable directory is never assumed to be the data root.");

        Assert(full.BuildIdentityState == EvidenceState.Exact &&
               Fact(full, "CurrentBuild").Value == "0.0.42-19843" && Fact(full, "CurrentBuild").State == EvidenceState.Exact &&
               Fact(full, "BuildCommit").Value == "abcdef1",
            "An exact identity is Exact and shows version-build with its commit as separate facts.");
        var partial = RuntimeContextBuilder.Build(null, null,
            new Rpcs3BuildIdentity("0.0.42", 19843, null, null, null, null, Rpcs3BuildIdentityState.Partial, []), null, null, null);
        Assert(partial.BuildIdentityState == EvidenceState.Partial && Fact(partial, "CurrentBuild").Value is null &&
               Fact(partial, "BuildNumber").Value == "19843" && Fact(partial, "BuildCommit").State == EvidenceState.Unknown,
            "A partial identity stays Partial: no combined build is manufactured and the missing commit is Unknown.");
        var conflict = RuntimeContextBuilder.Build(null, null,
            new Rpcs3BuildIdentity("0.0.42", 19843, "abc", null, null, null, Rpcs3BuildIdentityState.Conflict, []), null, null, null);
        Assert(conflict.BuildIdentityState == EvidenceState.Conflict && Fact(conflict, "CurrentBuild").State == EvidenceState.Conflict &&
               Fact(conflict, "CurrentBuild").Value is null,
            "Conflicting build evidence stays a Conflict with no chosen winner.");

        Assert(full.GameIdentityState == EvidenceState.Exact && Fact(full, "GameEdition").State == EvidenceState.Unknown &&
               Fact(full, "GameVersion").Value == "1.05" && Fact(full, "GamePpuHash").Value!.Contains("38656a143a665d48", StringComparison.OrdinalIgnoreCase),
            "A game with serial, installed version and PPU hash has an Exact identity.");
        var noVersion = RuntimeContextBuilder.Build(null, null, null,
            new Rpcs3GameDescriptor("NPUB00000", "T", string.Empty, null, null, null), null, null);
        Assert(noVersion.GameIdentityState == EvidenceState.Partial &&
               Fact(noVersion, "GameVersion").State == EvidenceState.Unknown && Fact(noVersion, "GameVersion").Value is null &&
               Fact(noVersion, "GamePpuHash").State == EvidenceState.Unknown &&
               Fact(noVersion, "GameEdition").Value == "digital" &&
               Fact(noVersion, "GameIdentity").State == EvidenceState.Partial,
            "Missing APP_VER and PPU hash stay Unknown and keep the identity Partial; the edition is only the serial-prefix rule.");
        Assert(Fact(RuntimeContextBuilder.Build(null, null, null, new Rpcs3GameDescriptor("ZZZZ00000", "T", string.Empty, "1.00", null, null), null, null),
                   "GameEdition").State == EvidenceState.Unknown,
            "An unrecognized serial prefix gives no edition (no inference across editions).");
    }

    private static WikiPageEvidence Page(string? tested, string? testedGameVersion = "2.10") =>
        new("Synthetic", [Serial], [],
            [new WikiOnlineEvidence("Synthetic", WikiOnlineState.Supported, testedGameVersion, null, tested,
                "Yes", "Yes", "Yes", "Yes", "Not required", null)]);

    private static RuntimeContextSnapshot Compat(
        WikiPageEvidence page, string? gameVersion, Rpcs3BuildCatalog? builds, string? current, string host = "Windows")
    {
        var game = new Rpcs3GameDescriptor(Serial, "Synthetic", string.Empty, gameVersion, PpuHash, null);
        var identity = current is null
            ? null
            : new Rpcs3BuildIdentity(current[..current.LastIndexOf('-')], int.Parse(current[(current.LastIndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture), "abc", "master", "master", "x64", Rpcs3BuildIdentityState.Exact, []);
        var model = GameSettingsProjector.Project(null, page, new GameSettingsContext(Serial, gameVersion, host), builds);
        return RuntimeContextBuilder.Build(null, null, identity, game, model, builds, host);
    }

    // Tested is documentation, never a requirement or a downgrade suggestion.
    private static void AssertCompatibilityAndBuildResolution()
    {
        var github = new Rpcs3BuildCatalog([new Rpcs3BuildEntry("0.0.41", 19000, "Windows", Url19000)]);
        var archive = new Rpcs3BuildCatalog([new Rpcs3BuildEntry("0.0.41", 19000, "Windows",
            "https://archive.org/download/x/y.7z", SourceClass: Rpcs3BuildCatalog.ArchivePointerClass, Verification: Rpcs3BuildCatalog.ArchiveMetadataVerified)]);

        var official = Compat(Page("0.0.41-19000"), "2.10", github, "0.0.42-19843");
        Assert(Fact(official, "TestedBuild").Value == "0.0.41-19000" && Fact(official, "TestedBuild").Code == "OfficialGitHub" &&
               Fact(official, "Rpcn").Code == "Applicable",
            "A tested build with an exact official GitHub row is reported as available; it is not called required.");
        Assert(Fact(Compat(Page("0.0.41-19000"), "2.10", archive, "0.0.42-19843"), "TestedBuild").Code == "HistoricalArchiveOnly" &&
               Fact(Compat(Page("0.0.41-19000"), "2.10", archive, "0.0.42-19843"), "TestedBuild").State == EvidenceState.Partial,
            "Archive-only evidence is reported as historical evidence with no download, never as an official binary.");
        Assert(Fact(Compat(Page("0.0.41-19000"), "2.10", Rpcs3BuildCatalog.Empty, "0.0.42-19843"), "TestedBuild").Code == "BuildUnresolved" &&
               Fact(Compat(Page("0.0.41-19000"), "2.10", github, "0.0.42-19843", host: "macOS"), "TestedBuild").Code == "BuildUnresolved",
            "A missing build, or a non-Windows host, is Unresolved: no Windows binary is offered.");
        Assert(Fact(Compat(Page("0.0.41-19000"), "2.10", github, "0.0.41-19000"), "TestedBuild").Code == "SameAsCurrent",
            "When the tested build is the current build the fact says so instead of offering another build.");
        Assert(Fact(Compat(Page("0.0.41-19000"), "2.11", github, "0.0.42-19843"), "Rpcn").Code == "VersionMismatch" &&
               Compat(Page("0.0.41-19000"), "2.11", github, "0.0.42-19843").Find("TestedBuild") is null &&
               Fact(Compat(Page("0.0.41-19000"), null, github, "0.0.42-19843"), "Rpcn").Code == "InstalledVersionUnknown" &&
               Compat(Page("0.0.41-19000"), null, github, "0.0.42-19843").Find("TestedBuild") is null,
            "A known version mismatch or an unknown installed version applies no version-scoped RPCN evidence and no tested build.");
        Assert(Fact(Compat(Page(null), "2.10", github, "0.0.42-19843"), "Rpcn").Code == "Applicable" &&
               Compat(Page(null), "2.10", github, "0.0.42-19843").Find("TestedBuild") is null &&
               Compat(Page("N/A"), "2.10", github, "0.0.42-19843").Find("TestedBuild") is null,
            "Applicable evidence without a tested build (missing or N/A) lists no tested build.");
        Assert(Fact(Compat(Page("garbage"), "2.10", github, "0.0.42-19843"), "TestedBuild").Code == "BuildUnresolved",
            "A documented tested-build text that is not a build stays visible but Unresolved.");
    }

    private static void AssertViewModelAndLocalization()
    {
        var hardware = Hardware(Adapter("NVIDIA GeForce Test", "10DE"));
        var partial = new Rpcs3BuildIdentity("0.0.42", 19843, null, null, null, null, Rpcs3BuildIdentityState.Partial, []);
        var snapshot = RuntimeContextBuilder.Build(hardware, Install(null), partial,
            new Rpcs3GameDescriptor(Serial, "Synthetic", string.Empty, null, null, null), null, null);

        var vm = new RuntimeVm();
        try
        {
            UiText.SetLang(UiText.EnLang);
            vm.Load(snapshot);
            var groups = vm.Groups.ToDictionary(group => group.Group);
            Assert(vm.HasGroups && groups.Count == 5,
                "Runtime shows the hardware, installation, build, game and compatibility groups.");
            var cpu = groups[RuntimeFactGroup.Hardware].SummaryRows.Single(row => row.Id == "Cpu");
            Assert(cpu.Label == "CPU" && cpu.IsTechnical && cpu.Value == "Test CPU 9000" && !cpu.HasState,
                "A fully known fact shows its technical value with no state badge.");
            var current = groups[RuntimeFactGroup.Build].SummaryRows.Single(row => row.Id == "CurrentBuild");
            Assert(current.HasState && current.StateText == "Partial" && !current.IsTechnical && current.Value == "The build identity is incomplete",
                "A partial build identity is labeled Partial with a localized explanation and no invented value.");
            var dataRoot = groups[RuntimeFactGroup.Installation].SummaryRows.Single(row => row.Id == "DataRoot");
            Assert(dataRoot.StateText == "Unknown" && dataRoot.Value.Contains("not assumed", StringComparison.Ordinal),
                "An unproven DataRoot is shown Unknown with the not-assumed explanation.");
            Assert(groups[RuntimeFactGroup.Build].DetailRows.Any(row => row.Id == "BuildCommit") &&
                   groups[RuntimeFactGroup.Build].HasDetails,
                "Raw build parts stay behind the Details expander.");

            UiText.SetLang(UiText.ArabLang);
            vm.SetLang();
            var arabic = vm.Groups.ToDictionary(group => group.Group);
            var arabicCurrent = arabic[RuntimeFactGroup.Build].SummaryRows.Single(row => row.Id == "CurrentBuild");
            var arabicCpu = arabic[RuntimeFactGroup.Hardware].SummaryRows.Single(row => row.Id == "Cpu");
            Assert(arabicCurrent.Label == "نسخة RPCS3 الحالية" && arabicCurrent.StateText == "جزئي" &&
                   arabicCpu.Value == "Test CPU 9000" && arabicCpu.Label == "المعالج" &&
                   UiText.GetText("Tab.Runtime") == "بيئة التشغيل والتوافق",
                "Arabic shows natural labels and states while technical values stay untouched.");

            // No mojibake or missing localization in any group/state/fact/code text.
            var texts = new List<string>();
            foreach (var group in arabic.Values)
            {
                texts.Add(group.Title);
                texts.AddRange(group.SummaryRows.Concat(group.DetailRows).SelectMany(row => new[] { row.Label, row.Value, row.Note, row.StateText }));
            }

            Assert(texts.All(text => !text.Contains('\uFFFD') && !text.Contains('Ø', StringComparison.Ordinal) &&
                                     !text.StartsWith("Runtime.", StringComparison.Ordinal)),
                "Arabic Runtime text must have no mojibake and no missing resource keys.");
        }
        finally
        {
            UiText.SetLang(UiText.EnLang);
        }

        // Every Runtime resource key exists in both languages.
        var root = FindRepoRoot();
        var english = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "en-US.json")))!;
        var arabicMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "App", "Lang", "ar-SA.json")))!;
        var keys = english.Keys.Where(key => key.StartsWith("Runtime.", StringComparison.Ordinal) || key == "Tab.Runtime").ToArray();
        Assert(keys.Length > 60 && keys.All(arabicMap.ContainsKey) &&
               arabicMap.Keys.Where(key => key.StartsWith("Runtime.", StringComparison.Ordinal)).All(english.ContainsKey),
            "Runtime resources exist in Arabic and English with the same key set.");

        // Every fact key and code the builder can emit has a localized string.
        var emitted = RuntimeContextBuilder.Build(Hardware(Adapter("NVIDIA Test", null), Adapter("AMD Test", "1002", primary: false)),
            Install(null), partial, new Rpcs3GameDescriptor("NPUB00000", "T", string.Empty, null, null, null), null, null);
        foreach (var fact in emitted.Facts.Concat(empty()))
        {
            Assert(english.ContainsKey($"Runtime.Fact.{fact.Key}") &&
                   (fact.Code is null || english.ContainsKey($"Runtime.Code.{fact.Code}")) &&
                   english.ContainsKey($"Runtime.State.{fact.State}"),
                $"Runtime fact {fact.Key}/{fact.Code} must have localized resources.");
        }

        static IEnumerable<RuntimeFact> empty() => RuntimeContextBuilder.Build(null, null, null, null, null, null).Facts;
    }

    private static readonly string[] SourceFolders = ["App", "Core"];

    private static readonly string[] HardwareScanMarkers =
        ["EnumDisplayDevices", "GlobalMemoryStatusEx", "CentralProcessor", "Win32_VideoController", "Win32_Processor", "VendorIdentifier"];

    private static void AssertSingleScannerAndNoDevelopmentDependency()
    {
        var root = FindRepoRoot();
        var sources = SourceFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllText);

        foreach (var marker in HardwareScanMarkers)
        {
            var owners = sources.Where(pair => pair.Value.Contains(marker, StringComparison.Ordinal)).Select(pair => Path.GetFileName(pair.Key)).Distinct().ToArray();
            Assert(owners.All(owner => owner == "HwProbe.cs"),
                $"'{marker}' hardware scanning must live only in HwProbe (found in: {string.Join(", ", owners)}).");
        }

        Assert(sources.All(pair => !pair.Value.Contains("Downloaded-Websites", StringComparison.OrdinalIgnoreCase) &&
                                   !System.Text.RegularExpressions.Regex.IsMatch(pair.Value, "\"[Ff]:[\\\\/]")),
            "Runtime code must not depend on the F: development workspace.");

        var data = Directory.EnumerateFiles(Path.Combine(root, "App", "Data"), "*.json")
            .Where(path => Path.GetFileName(path) != "ncl.zip")
            .ToArray();
        Assert(data.All(path => !File.ReadAllText(path).Contains("Downloaded-Websites", StringComparison.OrdinalIgnoreCase)),
            "Shipped data must not reference the development workspace.");

        // Domain 5 owns no policy: it must not reach the mutation/patch layers.
        var builder = sources.Single(pair => pair.Key.EndsWith("RuntimeContextBuilder.cs", StringComparison.Ordinal)).Value;
        Assert(!builder.Contains("File.Write", StringComparison.Ordinal) && !builder.Contains("Process.", StringComparison.Ordinal) &&
               !builder.Contains("PatchApply", StringComparison.Ordinal) && !builder.Contains("ConfigApply", StringComparison.Ordinal),
            "The Runtime Context builder is read-only evidence composition.");

        // Production decision code is generic: no game, serial or build literal.
        var generic = new[] { "RuntimeContextBuilder.cs", "Rpcs3BuildCatalog.cs", "NoteScope.cs", "RuntimeVm.cs", "RuntimeContextUseCase.cs" };
        foreach (var pair in sources.Where(pair => generic.Contains(Path.GetFileName(pair.Key))))
        {
            Assert(!pair.Value.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) &&
                   !pair.Value.Contains("16004", StringComparison.Ordinal) &&
                   !pair.Value.Contains("15565", StringComparison.Ordinal) &&
                   !System.Text.RegularExpressions.Regex.IsMatch(pair.Value, "\"(BLES|BLUS|BCES|BCUS|NPUB|NPEB|NPUA)\\d"),
                $"{Path.GetFileName(pair.Key)} must stay generic (no game, serial or build hardcode).");
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "R3Cfg.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
