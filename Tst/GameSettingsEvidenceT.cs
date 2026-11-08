using System;
using System.IO;
using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;

// The production Game Settings path decides each change through ChangeScope (known grammar only) and
// EvidencePolicy: Exact + deterministic => auto-selectable, scope mismatch => Blocked, anything
// unresolved or unprovable => never auto-selected. Every fixture is synthetic.
internal static class GameSettingsEvidenceT
{
    private const string Serial = "BLUS99002";

    public static void Run(string tempRoot)
    {
        AssertGrammarIsAnchored();
        AssertStateUsesPolicy();
        AssertPlanUsesHardwareVendorFacts(tempRoot);

        Console.WriteLine("Game Settings evidence integration (anchored scope grammar, EvidencePolicy decides state, vendor facts proven) PASS.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static EvidenceScopeCondition? Cond(string note, EvidenceScopeKind kind) =>
        ChangeScope.Conditions(Serial, note).FirstOrDefault(c => c.Kind == kind);

    private static bool Is(EvidenceScopeCondition? condition, ScopeRelation relation, string? expected = null) =>
        condition is not null && condition.Relation == relation && (expected is null || condition.Expected == expected);

    private static void AssertGrammarIsAnchored()
    {
        Assert(Is(Cond("NVIDIA", EvidenceScopeKind.GpuVendor), ScopeRelation.Unresolved),
            "A bare vendor word is named but not comparable.");
        Assert(Is(Cond("Works on NVIDIA GPUs", EvidenceScopeKind.GpuVendor), ScopeRelation.Equal, "nvidia"),
            "Vendor + hardware noun is a known grammar.");
        Assert(Is(Cond("Use Vulkan", EvidenceScopeKind.Renderer), ScopeRelation.Unresolved),
            "A renderer named as a recommendation is not a scope.");
        Assert(Is(Cond("Crashes with Vulkan, fine on OpenGL", EvidenceScopeKind.Renderer), ScopeRelation.Unresolved),
            "Two renderers in one note are not one scope.");
        foreach (var note in new[] { "Only on Vulkan", "Needed only with the Vulkan renderer", "Vulkan only", "Applies when using the OpenGL renderer" })
        {
            var expected = note.Contains("Vulkan", StringComparison.Ordinal) ? "Vulkan" : "OpenGL";
            Assert(Is(Cond(note, EvidenceScopeKind.Renderer), ScopeRelation.Equal, expected),
                $"'{note}' is an anchored renderer scope.");
        }

        Assert(Is(Cond("Not only on Vulkan", EvidenceScopeKind.Renderer), ScopeRelation.Unresolved),
            "A negated renderer scope is unresolved.");
        Assert(Is(Cond("Linux users may need it", EvidenceScopeKind.Platform), ScopeRelation.Unresolved),
            "A platform named outside its anchored grammar is unresolved.");
        Assert(Is(Cond("Windows only", EvidenceScopeKind.Platform), ScopeRelation.Equal, "Windows"),
            "'Windows only' is an anchored platform scope.");
        Assert(ChangeScope.Conditions(Serial, "Set to 4 for smoother audio").Count == 1,
            "A note without a known scope adds no condition.");
    }

    private static CfgChg Change(string note, RecRisk risk = RecRisk.Required, WikiRecommendationClass cls = WikiRecommendationClass.MandatoryGameConfig) =>
        new("Video", "Write Color Buffers", "true", "wcb", note, risk, cls);

    private static RecItem Eval(CfgChg change, EvidenceRuntimeFacts? facts, string current = "false")
    {
        var game = new Rpcs3GameDescriptor(Serial, "T", string.Empty, null, null, null);
        var rec = new WikiRec(Serial, "T", [change], WikiGameIdRole.Primary);
        return new StateSvc().Evaluate(game, [rec], (_, _) => current, facts).Items.Single();
    }

    private static EvidenceRuntimeFacts Facts(string? gpu = null, string? version = null, string? platform = "Windows") =>
        new(Serial, version, GpuVendor: gpu, Platform: platform, Edition: "disc");

    private static void AssertStateUsesPolicy()
    {
        var plain = Eval(Change("Required to fix flickering."), Facts());
        Assert(plain.State == RecState.Pending && plain.IsSel && plain.Decision is { Action: SettingAction.Apply, Applicability: ApplicabilityLevel.Exact },
            "An unscoped, deterministic, current change stays Apply (auto-selectable).");

        var suggest = Eval(Change("Optional smoothing.", RecRisk.Optional, WikiRecommendationClass.GeneralRecommendation), Facts());
        Assert(suggest.State == RecState.Manual && !suggest.IsSel && suggest.Decision?.Action == SettingAction.Suggest,
            "A non-deterministic value is Suggest: actionable by the user, never auto-selected.");

        var nvidia = Change("Only for NVIDIA GPUs.");
        Assert(Eval(nvidia, Facts("nvidia")).State == RecState.Pending, "A matching GPU vendor keeps Apply.");
        var amd = Eval(nvidia, Facts("amd"));
        Assert(amd.State == RecState.Blocked && !amd.IsAction && !amd.IsSel && amd.Decision?.Action == SettingAction.Blocked,
            "A scope mismatch is Blocked.");
        var unknownGpu = Eval(nvidia, Facts(null));
        Assert(unknownGpu.State == RecState.Manual && !unknownGpu.IsSel && unknownGpu.Decision?.Applicability == ApplicabilityLevel.Partial,
            "An unprovable vendor is Partial: never auto-selected.");

        var bare = Eval(Change("Works better on NVIDIA."), Facts("nvidia"));
        Assert(bare.State == RecState.Manual && !bare.IsSel,
            "A bare vendor word never becomes a vendor scope, so it can never promote to Apply.");

        var renderer = Eval(Change("Only on Vulkan."), Facts());
        Assert(renderer.State == RecState.Manual && !renderer.IsSel,
            "A renderer-scoped change is Partial because the renderer fact is not proven.");

        Assert(Eval(Change("Digital edition only."), Facts()).State == RecState.Blocked,
            "A digital-only change is Blocked for a disc serial.");
        Assert(Eval(Change("Only on Linux."), Facts()).State == RecState.Blocked,
            "Another host platform is Blocked.");

        var versioned = Change("Only for version 1.02.");
        Assert(Eval(versioned, Facts(version: null)).State == RecState.Manual,
            "An unknown installed APP_VER never confirms a version-scoped change.");
        Assert(Eval(versioned, Facts(version: "01.05")).State == RecState.Blocked,
            "A different installed APP_VER is Blocked.");
        Assert(Eval(versioned, Facts(version: "01.02")).State == RecState.Pending,
            "The matching installed APP_VER keeps Apply.");

        Assert(Eval(nvidia, Facts("amd"), current: "true").State == RecState.Applied,
            "A value already in place is Applied whatever the scope.");

        foreach (var cls in new[] { WikiRecommendationClass.PatchSpecific, WikiRecommendationClass.VersionSpecific, WikiRecommendationClass.InformationalNote })
        {
            Assert(Eval(Change("Required.", cls: cls), Facts()).State == RecState.Blocked, $"{cls} is never a setting.");
        }
    }

    private static HwInfo Hardware(string gpu, string pci) =>
        new HwProbe(
            readCpuName: () => "Test CPU",
            readLogicalProcessors: () => 8,
            readTotalMemoryBytes: () => 16UL * 1024 * 1024 * 1024,
            readGpuName: () => gpu,
            readGpuAdapters: () => [new GpuAdapterInfo(gpu, HwProbe.VendorFromPci(pci), pci, "0001", true, true)]).Probe();

    private static void AssertPlanUsesHardwareVendorFacts(string tempRoot)
    {
        var env = GameSetupT.CreateEnvironment(Path.Combine(tempRoot, "gs-evidence"));
        var game = new Rpcs3GameDescriptor(Serial, "T", env.GamePath, null, null, null);
        var rec = new WikiRec(Serial, "T", [Change("Only for NVIDIA GPUs.")], WikiGameIdRole.Primary);
        var service = new PlanService();

        Assert(service.BuildCfg(env.Install, game, rec, Hardware("NVIDIA GeForce Test", "10DE")).Items.Single().IsSel,
            "An NVIDIA machine keeps an NVIDIA-scoped change auto-selectable.");
        var amd = service.BuildCfg(env.Install, game, rec, Hardware("AMD Radeon RX 6700", "1002")).Items.Single();
        Assert(amd.State == RecState.Blocked && !amd.IsSel, "An AMD machine blocks an NVIDIA-scoped change.");
        Assert(!service.BuildCfg(env.Install, game, rec).Items.Single().IsSel,
            "Without hardware evidence the vendor scope is unprovable and never auto-selects.");
    }
}
