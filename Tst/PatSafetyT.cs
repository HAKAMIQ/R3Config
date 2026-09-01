using System.IO;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class PatSafetyT
{
    public static void Run(string repoRoot)
    {
        var appRoot = Path.Combine(repoRoot, "App");
        var artPath = Path.Combine(appRoot, "Data", "art.yml");
        var read = new ReadSvc().Read(artPath, PatSrc.Artemis);
        TestAssert.Assert(read.IsOk, "Expected Artemis safety regression catalog to parse.");

        var breakPatch = read.Catalog.Entries.Single(entry =>
            string.Equals(
                entry.Title,
                "(B) Press R3 + > To Toggle Enemies Die From Any Damage Or Die On Spawn",
                StringComparison.Ordinal) &&
            entry.GameTgt.Any(target =>
                string.Equals(
                    target.Serial,
                    "BCUS98232",
                    StringComparison.OrdinalIgnoreCase)));
        var breakSafety = PatSafe.Eval(breakPatch.Notes);
        TestAssert.Assert(
            breakSafety is
            {
                Severity: PatSafetySeverity.High,
                ProgressionRisk: true
            },
            "Expected explicit break-game notes to surface as high progression risk.");

        var retryPatch = read.Catalog.Entries.Single(entry =>
            string.Equals(
                entry.Title,
                "(B) Toggle Invincibility From Melee Attacks And Grabs With R3 + ^",
                StringComparison.Ordinal) &&
            entry.GameTgt.Any(target =>
                string.Equals(
                    target.Serial,
                    "BCUS98232",
                    StringComparison.OrdinalIgnoreCase)));
        var retrySafety = PatSafe.Eval(retryPatch.Notes);
        TestAssert.Assert(
            retrySafety is
            {
                Severity: PatSafetySeverity.High,
                ProgressionRisk: true,
                DisableAndRetry: true
            },
            "Expected source guidance to retain the disable-and-retry recovery action.");

        var crashSafety = PatSafe.Eval(
            "Requires the Button Enable Code to be activated once before this or the game will instantly freeze.");
        TestAssert.Assert(
            crashSafety is
            {
                Severity: PatSafetySeverity.High,
                CrashOrFreezeRisk: true
            },
            "Expected explicit crash/freeze prerequisites to surface as high risk.");

        TestAssert.Assert(
            !PatSafe.Eval("Fixes game crashes/freezes.").HasRisk,
            "Expected positive crash/freeze fix notes not to become false danger warnings.");
        TestAssert.Assert(
            !PatSafe.Eval(
                "Without this, the game will only spawn a total of 40 treasure chests and then you will be stuck.").HasRisk,
            "Expected a warning about omitting a patch not to be inverted into a risk caused by enabling it.");

        var previousLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.EnLang);
            var match = new PatchMatchService().Match(
                breakPatch,
                [new Rpcs3GameDescriptor(
                    "BCUS98232",
                    "God of War: Ascension",
                    @"C:\Games\BCUS98232",
                    "01.12",
                    breakPatch.NormHash,
                    null)]);
            var item = new PatItVm(match);
            TestAssert.Assert(
                item.HasSafety &&
                item.SafetyLabel == UiText.GetText("Patch.Safety.High.Label") &&
                item.SafetyText.Contains(
                    UiText.GetText("Patch.Safety.Progression"),
                    StringComparison.Ordinal),
                "Expected player-facing patch items to project structured safety guidance.");
        }
        finally
        {
            UiText.SetLang(previousLanguage);
        }

        var patchView = File.ReadAllText(Path.Combine(appRoot, "Ctrl", "Pat.xaml"));
        var patchTemplate = File.ReadAllText(Path.Combine(appRoot, "Ctrl", "PTmp.xaml"));
        var setupVm = File.ReadAllText(Path.Combine(appRoot, "Vm", "Main.Setup.cs"));
        var setupPlanner = File.ReadAllText(Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Setup",
            "GameSetupPlanningService.cs"));
        TestAssert.Assert(
            patchView.Contains("Patch.Launch.BootRecommended", StringComparison.Ordinal) &&
            patchTemplate.Contains("SafetyText", StringComparison.Ordinal) &&
            patchTemplate.Contains("HasSafety", StringComparison.Ordinal),
            "Expected patch UI to expose boot/save-state guidance and per-patch safety warnings.");
        TestAssert.Assert(
            setupPlanner.Contains("PATCH_BOOT_RECOMMENDED", StringComparison.Ordinal) &&
            setupPlanner.Contains("PATCH_SAFETY_HIGH", StringComparison.Ordinal) &&
            setupVm.Contains("Setup.Warning.PatchBoot", StringComparison.Ordinal) &&
            setupVm.Contains("Setup.Warning.PatchSafetyHigh", StringComparison.Ordinal),
            "Expected Game Setup to retain boot guidance and selected-patch safety diagnostics through presentation.");

        Console.WriteLine("Patch safety, recovery guidance, and boot-path regressions PASS.");
    }
}
