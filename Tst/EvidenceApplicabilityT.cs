using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;

// The applicability of documented evidence never depends on a generic hardware
// tier. Documented changes keep the state their own class/risk/value give them;
// a hardware condition may only come from the evidence itself (a GPU vendor on
// a note), and Unknown stays Unknown. Every fixture is synthetic.
internal static class EvidenceApplicabilityT
{
    private const string Serial = "BLUS99001";

    private static readonly string[] SourceFolders = ["Core", "App"];

    public static void Run(string tempRoot)
    {
        AssertDocumentedChangesIgnoreHardwareTier(tempRoot);
        AssertClassScopeStillFailsClosed();
        AssertBindingAndUnknownStayScoped();
        AssertEvidenceDefinedHardwareConditionOnly();
        AssertVersionScope();
        AssertTierIsIsolatedFromOtherDomains();

        Console.WriteLine("Evidence applicability (no generic HwTier authority, class/scope fail-closed, evidence-defined hardware only, tier isolation) PASS.");
    }

    private static HwInfo Hardware(string cpu, int threads, ulong gib, string gpu, string? pci = null) =>
        new HwProbe(
            readCpuName: () => cpu,
            readLogicalProcessors: () => threads,
            readTotalMemoryBytes: () => gib * 1024UL * 1024UL * 1024UL,
            readGpuName: () => gpu,
            readGpuAdapters: () =>
            [
                new GpuAdapterInfo(gpu, pci is null ? HwProbe.VendorFromName(gpu) : HwProbe.VendorFromPci(pci), pci, pci is null ? null : "0001", true, true)
            ]).Probe();

    private static HwInfo LowTier() => Hardware("Dual Core Test CPU", 2, 4, "Basic Display Adapter");

    private static HwInfo HighTier(string gpu = "NVIDIA GeForce RTX 3060", string? pci = "10DE") =>
        Hardware("Intel Core i5-12600KF", 16, 32, gpu, pci);

    private static CfgChg Change(
        string section,
        string key,
        string value,
        RecRisk risk,
        WikiRecommendationClass evidenceClass,
        string? notes = null) =>
        new(section, key, value, key.ToLowerInvariant(), notes, risk, evidenceClass);

    private static WikiRec Rec(WikiGameIdRole binding, string serial, params CfgChg[] changes) =>
        new(serial, "Synthetic Game", changes, binding);

    private static void AssertDocumentedChangesIgnoreHardwareTier(string tempRoot)
    {
        var low = LowTier();
        var high = HighTier();
        Assert(low.Tier == HwTier.Low && high.Tier == HwTier.High, "Fixtures cover both tiers.");

        var mandatoryPerf = Change("Core", "PPU Decoder", "Interpreter (static)", RecRisk.PerfCost,
            WikiRecommendationClass.MandatoryGameConfig, "Required to fix crashing.");
        var mandatoryDep = Change("Video", "Minimum Scalable Dimension", "320", RecRisk.Dep,
            WikiRecommendationClass.MandatoryGameConfig, "Required when using resolution scaling.");
        var visualRec = Change("Video", "Resolution Scale", "150", RecRisk.Rec, WikiRecommendationClass.GeneralRecommendation);
        var required = Change("Video", "Write Color Buffers", "true", RecRisk.Required, WikiRecommendationClass.MandatoryGameConfig);
        var rec = Rec(WikiGameIdRole.Primary, Serial, mandatoryPerf, mandatoryDep, visualRec, required);

        var env = GameSetupT.CreateEnvironment(Path.Combine(tempRoot, "evidence-applicability"));
        var game = new Rpcs3GameDescriptor(Serial, "Synthetic Game", env.GamePath, null, null, null);
        var service = new PlanService();
        var atLow = service.BuildCfg(env.Install, game, rec, low);
        var atHigh = service.BuildCfg(env.Install, game, rec, high);
        var atNone = service.BuildCfg(env.Install, game, rec);

        string Shape(CfgPlan plan) => string.Join("|", plan.Items.Select(item => $"{item.Key}:{item.State}:{item.IsAction}:{item.IsSel}"));
        Assert(Shape(atLow) == Shape(atHigh) && Shape(atLow) == Shape(atNone),
            "The state of documented changes is identical on Low, High and unknown hardware.");
        Assert(atLow.RecState.Blocked == 0 && atLow.Items.All(item => item.State != RecState.Blocked),
            "A Low tier alone never blocks a Wiki-documented change.");

        var byKey = atLow.Items.ToDictionary(item => item.Key);
        Assert(byKey["PPU Decoder"].State == RecState.Manual && byKey["PPU Decoder"].IsAction && !byKey["PPU Decoder"].IsSel &&
               byKey["Minimum Scalable Dimension"].State == RecState.Manual && byKey["Minimum Scalable Dimension"].IsAction,
            "A mandatory game-specific change whose risk is PerfCost or Dep stays actionable (Manual, never auto-selected) on Low hardware.");
        Assert(byKey["Resolution Scale"].State == RecState.Pending && byKey["Resolution Scale"].IsSel &&
               byKey["Write Color Buffers"].State == RecState.Pending && byKey["Write Color Buffers"].IsSel,
            "Auto-selectable documented changes stay auto-selectable on Low hardware, including visual keys the old heuristic listed.");

        var state = new StateSvc();
        Assert(state.Evaluate(new Rpcs3GameDescriptor(Serial, "T", string.Empty, null, null, null), [rec], (_, _) => "150").Items
                   .Single(item => item.Path == "Video / Resolution Scale").State == RecState.Applied &&
               state.Evaluate(new Rpcs3GameDescriptor(Serial, "T", string.Empty, null, null, null), [rec], (_, _) => throw new IOException("unreadable")).Items
                   .All(item => item.State == RecState.Unknown && !item.IsAction && !item.IsSel),
            "A value already applied is Applied; an unreadable current value is Unknown, not Applicable or Blocked.");
    }

    private static void AssertClassScopeStillFailsClosed()
    {
        var game = new Rpcs3GameDescriptor(Serial, "T", string.Empty, null, null, null);
        var state = new StateSvc();
        foreach (var evidenceClass in new[]
                 {
                     WikiRecommendationClass.PatchSpecific, WikiRecommendationClass.InformationalNote,
                     WikiRecommendationClass.VersionSpecific, WikiRecommendationClass.Unsupported, WikiRecommendationClass.Ambiguous
                 })
        {
            var item = state.Evaluate(game, [Rec(WikiGameIdRole.Primary, Serial,
                Change("Core", "SPU Block Size", "Giga", RecRisk.Required, evidenceClass))], (_, _) => "Safe").Items.Single();
            Assert(item.State == RecState.Blocked && !item.IsAction && !item.IsSel,
                $"{evidenceClass} is never an actionable or default setting, by class and not by hardware.");
        }

        var general = state.Evaluate(game, [Rec(WikiGameIdRole.Primary, Serial,
            Change("Core", "SPU Block Size", "Giga", RecRisk.Unknown, WikiRecommendationClass.GeneralRecommendation))], (_, _) => "Safe").Items.Single();
        Assert(general.State == RecState.Manual && !general.IsSel,
            "A GENERAL recommendation is never Mandatory or auto-selected without a Required/Recommended evidence risk.");
    }

    private static void AssertBindingAndUnknownStayScoped()
    {
        var game = new Rpcs3GameDescriptor(Serial, "T", string.Empty, null, null, null);
        var change = Change("Core", "SPU Block Size", "Giga", RecRisk.Required, WikiRecommendationClass.MandatoryGameConfig);
        var state = new StateSvc();
        foreach (var binding in new[] { WikiGameIdRole.Demo, WikiGameIdRole.RelatedEdition, WikiGameIdRole.Unknown })
        {
            Assert(state.Evaluate(game, [Rec(binding, Serial, change)], (_, _) => "Safe").Total == 0,
                $"A {binding} binding inherits no setting and selects nothing.");
        }

        Assert(state.Evaluate(game, [Rec(WikiGameIdRole.Primary, "BLUS11111", change)], (_, _) => "Safe").Total == 0,
            "Evidence for another serial is never evaluated for this game.");
    }

    private static void AssertEvidenceDefinedHardwareConditionOnly()
    {
        var lowNvidia = Hardware("Dual Core Test CPU", 2, 4, "NVIDIA GeForce Test", "10DE");
        var highAmd = HighTier("AMD Radeon Test", "1002");
        Assert(NoteScope.HardwareApplies("NVIDIA", lowNvidia) == true && NoteScope.HardwareApplies("NVIDIA", highAmd) == false &&
               NoteScope.HardwareApplies("AMD", lowNvidia) == false && NoteScope.HardwareApplies("AMD", highAmd) == true,
            "A documented vendor condition follows the proven adapter vendor, not the tier.");

        var lowUnknown = Hardware("Dual Core Test CPU", 2, 4, "Unnamed Adapter", "1414");
        var highUnknown = new HwInfo("CPU", 16, 32UL << 30, "g", HwTier.High, false, "m",
            Adapters: [new GpuAdapterInfo("Unnamed", HwVendor.Unknown, null, null, true, true)]);
        Assert(NoteScope.HardwareApplies("NVIDIA", highUnknown) is null &&
               NoteScope.HardwareApplies("NVIDIA", lowUnknown) == false &&
               NoteScope.HardwareApplies("NVIDIA", (HwInfo?)null) is null,
            "An unproven vendor stays Unknown on every tier; a proven different vendor is a mismatch.");

        foreach (var text in new[] { "weak CPU", "high-end CPU", "slow processor", "CPU", "GPU" })
        {
            Assert(NoteScope.HardwareApplies(text, LowTier()) == NoteScope.HardwareApplies(text, HighTier()) &&
                   NoteScope.HardwareApplies(text, LowTier()) == true,
                $"'{text}' is free wording: it is not mapped to a tier or threshold and restricts nothing.");
        }
    }

    private static void AssertVersionScope()
    {
        Assert(NoteScope.VersionApplies("Only 1.00 version is affected.", "1.00", "1.05") == false &&
               NoteScope.VersionApplies("Only 1.00 version is affected.", "1.00", "1.00") == true &&
               NoteScope.VersionApplies("Only 1.00 version is affected.", "1.00", null) is null,
            "A VERSION-scoped note applies to that version only, and an unknown installed version is Unknown.");
    }

    // Tier is a legacy status/display value. Nothing that decides applicability,
    // patches, diagnostics, build identity or Runtime facts can read it.
    private static void AssertTierIsIsolatedFromOtherDomains()
    {
        var evaluators = new[] { typeof(StateSvc), typeof(DiagnosticEngine), typeof(PatchCompatibilityService) }
            .SelectMany(type => type.GetMethods().Where(method => method.Name == "Evaluate"))
            .ToArray();
        Assert(evaluators.Length >= 3, "The state, diagnostic and patch-compatibility evaluators were found.");
        foreach (var method in evaluators)
        {
            Assert(method.GetParameters().All(parameter => parameter.ParameterType != typeof(HwInfo)),
                $"{method.DeclaringType!.Name}.{method.Name} takes no hardware input.");
        }

        var build = new Rpcs3BuildIdentity("0.0.41", 19000, "89abcdef0123456789abcdef0123456789abcdef", "Alpha", "master", "x64",
            Rpcs3BuildIdentityState.Exact, []);
        var low = LowTier() with { CpuVendor = HwVendor.Intel, OsArchitecture = "X64", ProcessArchitecture = "X64" };
        var high = low with { Tier = HwTier.High };
        var lowFacts = RuntimeContextBuilder.Build(low, null, build, null, null, null).Facts;
        var highFacts = RuntimeContextBuilder.Build(high, null, build, null, null, null).Facts;
        Assert(lowFacts.SequenceEqual(highFacts),
            "Runtime Context facts, including build identity, are identical whatever the tier.");

        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("Core", "Mdl", "Hw.cs"), Path.Combine("Core", "Svc", "Hw", "HwProbe.cs"),
            Path.Combine("App", "Vm", "Main.Sh.cs"), Path.Combine("App", "Vm", "Main.Ui.cs")
        };
        var readers = SourceFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"\bHwTier\b|\.Tier\b"))
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path => !allowed.Contains(path))
            .ToArray();
        Assert(readers.Length == 0,
            $"Only the probe and the status display may read HwTier (found in: {string.Join(", ", readers)}).");
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
