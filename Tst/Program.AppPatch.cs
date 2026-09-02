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
    static void App_Pat()
    {
        var repoRoot = RepoRoot();
        var appRoot = Path.Combine(repoRoot, "App");
        var noteServicePath = Path.Combine(appRoot, "Svc", "PNote.cs");
        var importServicePath = Path.Combine(appRoot, "Svc", "CanaryPatchImportService.cs");
        var bundledCanaryServicePath = Path.Combine(appRoot, "Svc", "BundledCanaryPatchService.cs");
        var bundledCanaryDataPath = Path.Combine(appRoot, "Data", "can.yml");
        var bundledCheatServicePath = Path.Combine(appRoot, "Svc", "BundledCheatPatchService.cs");
        var bundledCheatDataPath = Path.Combine(appRoot, "Data", "art.yml");
        var legacyCheatDataPath = Path.Combine(appRoot, "Data", "cheats.yml");
        var transactionServicePath = Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Patch",
            "PatchFileTransactionService.cs");
        var appFlowPath = Path.Combine(appRoot, "Flow.cs");
        var patchMutationUseCasePath = Path.Combine(
            appRoot,
            "Application",
            "PatchMutationUseCase.cs");
        var compositionPath = Path.Combine(
            appRoot,
            "Composition",
            "AppComposition.cs");
        var mutationGuardPath = Path.Combine(
            repoRoot,
            "Core",
            "Svc",
            "Mutation",
            "Rpcs3MutationGuard.cs");
        var planVmPath = Path.Combine(appRoot, "Vm", "Plan.cs");
        var mainVmPath = Path.Combine(appRoot, "Vm", "Main.cs");
        var mainWindowPath = Path.Combine(appRoot, "Main.xaml.cs");
        var mainWindowXamlPath = Path.Combine(appRoot, "Main.xaml");
        var gamesViewPath = Path.Combine(appRoot, "Ctrl", "Games.xaml");
        var actionBarViewPath = Path.Combine(appRoot, "Ctrl", "Act.xaml");
        var hCanaryGameViewPath = Path.Combine(appRoot, "Ctrl", "HGam.xaml");
        var configViewPath = Path.Combine(appRoot, "Ctrl", "Rec.xaml");
        var configViewPaths = new[]
        {
            configViewPath,
            Path.Combine(appRoot, "Ctrl", "RSty.xaml")
        };
        var patchViewPath = Path.Combine(appRoot, "Ctrl", "Pat.xaml");
        var patchViewPaths = new[]
        {
            patchViewPath,
            Path.Combine(appRoot, "Ctrl", "PSty.xaml"),
            Path.Combine(appRoot, "Ctrl", "PTmp.xaml"),
            Path.Combine(appRoot, "Ctrl", "PSrc.xaml"),
            Path.Combine(appRoot, "Ctrl", "POff.xaml"),
            Path.Combine(appRoot, "Ctrl", "PCan.xaml"),
            Path.Combine(appRoot, "Ctrl", "PCht.xaml")
        };
        var typeThemePath = Path.Combine(appRoot, "Thm", "Type.xaml");
        var hCanaryViewPath = Path.Combine(appRoot, "Ctrl", "HCan.xaml");
        var hCanaryViewPaths = new[]
        {
            hCanaryViewPath,
            Path.Combine(appRoot, "Ctrl", "HSty.xaml"),
            Path.Combine(appRoot, "Ctrl", "HGam.xaml"),
            Path.Combine(appRoot, "Ctrl", "HTgt.xaml"),
            Path.Combine(appRoot, "Ctrl", "HHsh.xaml"),
            Path.Combine(appRoot, "Ctrl", "HDet.xaml"),
            Path.Combine(appRoot, "Ctrl", "HIns.xaml"),
            Path.Combine(appRoot, "Ctrl", "HRev.xaml"),
            Path.Combine(appRoot, "Ctrl", "HLch.xaml"),
            Path.Combine(appRoot, "Ctrl", "HBar.xaml"),
            Path.Combine(appRoot, "Ctrl", "HSav.xaml")
        };
        var advancedViewPath = Path.Combine(appRoot, "View", "Adv.xaml");
        var advancedViewPaths = new[]
        {
            advancedViewPath,
            Path.Combine(appRoot, "View", "ASty.xaml"),
            Path.Combine(appRoot, "View", "ARec.xaml"),
            Path.Combine(appRoot, "View", "ADiag.xaml")
        };
        var appDialogPath = Path.Combine(appRoot, "View", "Dlg", "App.xaml");
        Assert(File.Exists(noteServicePath), "Expected PatchNoteTranslationService to exist in App layer.");
        Assert(File.Exists(importServicePath), "Expected CanaryPatchImportService to exist in App layer.");
        Assert(File.Exists(bundledCanaryServicePath), "Expected BundledCanaryPatchService to exist in App layer.");
        Assert(File.Exists(bundledCanaryDataPath), "Expected bundled Canary patch data to exist in App/Data.");
        Assert(File.Exists(bundledCheatServicePath), "Expected the bundled Artemis preparation service to exist.");
        Assert(File.Exists(bundledCheatDataPath), "Expected the deterministic Artemis catalog to exist in App/Data.");
        Assert(!File.Exists(legacyCheatDataPath), "Expected the undocumented legacy cheats.yml catalog to be removed.");
        Assert(BundledCheatPatchService.BndlName == "art.yml",
            "Expected the runtime catalog adapter to read the five-character art.yml filename.");
        Assert(File.Exists(transactionServicePath),
            "Expected the multi-file patch transaction service to exist in Core.");
        Assert(File.Exists(appFlowPath),
            "Expected AppFlow source to exist.");
        Assert(File.Exists(patchMutationUseCasePath),
            "Expected patch mutation orchestration to live in an Application use case.");
        Assert(File.Exists(compositionPath),
            "Expected one application composition root.");
        Assert(File.Exists(mutationGuardPath),
            "Expected RPCS3 mutations to have a Core safety guard.");
        var bundledCheatText = File.ReadAllText(bundledCheatDataPath);
        Assert(!bundledCheatText.Contains(
                   "PPU-03a0b43e4a5f4bfea64f53612ee7c5d990f88129c:",
                   StringComparison.OrdinalIgnoreCase),
            "Expected the malformed 41-character God of War Ascension executable hash keys to be removed.");
        var bundledCheatStrictRead = new R3Config.Svc.ReadSvc().Read(
            bundledCheatDataPath,
            PatSrc.Artemis);
        Assert(
            bundledCheatStrictRead.IsOk &&
            string.IsNullOrWhiteSpace(bundledCheatStrictRead.Error),
            "Expected the bundled Artemis catalog to satisfy strict executable-hash validation: " +
            bundledCheatStrictRead.Error);
        Assert(
            bundledCheatStrictRead.Catalog.Entries.Count == 1023 &&
            bundledCheatStrictRead.Catalog.Entries.All(entry =>
                entry.Source == PatSrc.Artemis &&
                entry.Risk == PatRisk.Cheat),
            "Expected the retained Artemis patch collection to produce exactly 1023 source-identified cheat-risk entries.");
        PatSafetyT.Run(repoRoot);
        Assert(!bundledCheatText.Contains("Anchors:", StringComparison.Ordinal) &&
               !bundledCheatText.Contains("&WOFreedom", StringComparison.Ordinal),
            "Expected art.yml to contain expanded definitions without source anchors.");
        Assert(bundledCheatStrictRead.Catalog.Entries.Any(entry =>
                entry.GameTgt.Any(target =>
                    target.Serial == "NPEB02365") &&
                entry.GameTgt.All(target =>
                    target.Serial != "NPEB0236")),
            "Expected Artemis identity to come from internal metadata, including the corrected A.O.T. NPEB02365 serial.");
        var bundledCanaryRead = new R3Config.Svc.ReadSvc().Read(
            bundledCanaryDataPath,
            PatSrc.Can);
        Assert(
            bundledCanaryRead.IsOk,
            "Expected bundled Canary data to parse: " + bundledCanaryRead.Error);
        const string infamousCanaryHash =
            "412a9bf323b4743de21658a286f71dbe82cad807";
        var infamousCanaryEntries = bundledCanaryRead.Catalog.Entries
            .Where(entry =>
                entry.Hash.Equals(
                    infamousCanaryHash,
                    StringComparison.OrdinalIgnoreCase) &&
                entry.GameTgt.Any(target =>
                    target.Serial.Equals(
                        "BCUS98119",
                        StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Assert(
            infamousCanaryEntries.Length == 4,
            "Expected the bundled inFamous BCUS98119 Canary key to retain all four declared patches.");

        var infamousDifferentLocalFingerprint = new Rpcs3GameDescriptor(
            "BCUS98119",
            "inFamous",
            @"C:\Games\BCUS98119",
            "01.00",
            null,
            null,
            HCanPpu: [new string('f', 40)],
            HCanSpu: []);
        var infamousMismatchMatches = infamousCanaryEntries
            .Select(entry => new R3Config.Svc.PatchMatchService().Match(
                entry,
                [infamousDifferentLocalFingerprint]))
            .ToArray();
        Assert(
            infamousMismatchMatches.All(match =>
                match.Status == MatStat.HashBad &&
                !PatAct.Can(match) &&
                !PatAct.IsRun(match) &&
                match.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.ExeBad,
                    CanOn: false,
                    UseGuard: false
                }) &&
            infamousMismatchMatches
                .Select(match => new PatItVm(match))
                .All(item => !item.CanOn &&
                             item.StateDsp ==
                             UiText.GetText("Patch.State.Blocked")),
            "Expected a reliable inFamous BCUS98119 local PPU mismatch to block Canary activation.");

        var infamousCompiled = new PatchSelectionCompiler().Compile(
            File.ReadAllText(bundledCanaryDataPath),
            [infamousMismatchMatches[0]],
            [infamousMismatchMatches[0]]);
        Assert(
            !infamousCompiled.IsOk,
            "Expected the Canary compiler to reject a selected patch with a reliable local executable mismatch.");

        var gowAscensionCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
            entry.Hash.Equals(
                "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c",
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "Unlock FPS");
        Assert(gowAscensionCanary.GameTgt.Count == 7,
            "Expected the God of War Ascension Canary definition to retain all seven declared regional editions.");
        var gowAscensionGame = new Rpcs3GameDescriptor(
            "BCUS98232",
            "God of War: Ascension",
            @"C:\Games\BCUS98232",
            "01.12",
            null,
            null);
        var gowAscensionNoDiscoveryMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCanary,
            [gowAscensionGame]);
        Assert(
            gowAscensionNoDiscoveryMatch.Status == MatStat.Unknown &&
            PatAct.Can(gowAscensionNoDiscoveryMatch) &&
            PatAct.IsRun(gowAscensionNoDiscoveryMatch) &&
            gowAscensionNoDiscoveryMatch.Cmp is
            {
                Verdict: PatchCompatibilityVerdict.Guarded,
                Code: PatchCompatibilityReasonCode.R3Guard,
                CanOn: true,
                UseGuard: true
            },
            "Expected a trusted bundled Canary entry with matching serial and version to remain selectable under RPCS3's exact runtime-key guard.");
        var gowAscensionNoDiscoveryItem = new PatItVm(
            gowAscensionNoDiscoveryMatch);
        Assert(
            gowAscensionNoDiscoveryItem.CanOn &&
            gowAscensionNoDiscoveryItem.CanNote ==
                UiText.GetText("Patch.CanaryNote.Ready"),
            "Expected the compact Canary row to stay actionable while explaining that RPCS3 verifies the runtime key when the game starts.");
        var gowAscensionDiscoveredGame = gowAscensionGame with
        {
            HCanPpu = [gowAscensionCanary.Hash],
            HCanSpu = [],
            HashEvi = HashEvi.PlainLog
        };
        var gowAscensionCanaryMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCanary,
            [gowAscensionDiscoveredGame]);
        Assert(
            gowAscensionCanaryMatch.Status == MatStat.Match &&
            PatAct.Can(gowAscensionCanaryMatch),
            "Expected an exact saved PPU discovery match to upgrade the Canary decision from runtime-guarded to exact without being a prerequisite for activation.");
        foreach (var target in gowAscensionCanary.GameTgt)
        {
            foreach (var version in target.Versions)
            {
                var regionalMatch = new R3Config.Svc.PatchMatchService().Match(
                    gowAscensionCanary,
                    [new Rpcs3GameDescriptor(
                        target.Serial,
                        target.Title,
                        @"C:\Games\" + target.Serial,
                        version,
                        gowAscensionCanary.Hash,
                        null,
                        HCanPpu: [gowAscensionCanary.Hash],
                        HCanSpu: [],
                        HashEvi: HashEvi.PlainLog)]);
                Assert(
                    regionalMatch.Status == MatStat.Match &&
                    regionalMatch.TgtSer == target.Serial &&
                    regionalMatch.TgtVer == version,
                    $"Expected Canary region/version coverage to match {target.Serial} v{version} using the declared runtime hash.");
            }
        }
        var gowWrongVersionMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCanary,
            [gowAscensionDiscoveredGame with
            {
                Version = "01.11"
            }]);
        var gowWrongVersionItem = new PatItVm(
            gowWrongVersionMatch);
        Assert(
            gowWrongVersionMatch.Status == MatStat.VerBad &&
            gowWrongVersionItem.StateDsp == UiText.GetText("Patch.State.Blocked") &&
            gowWrongVersionItem.DirStat == UiText.Fmt(
                "Patch.Direct.VersionMismatch",
                "01.11",
                "01.12") &&
            gowWrongVersionItem.CanNote == UiText.Fmt(
                "Patch.CanaryNote.VersionMismatch",
                "01.11",
                "01.12"),
            "Expected an unsupported Canary game version to stay visible with the installed and required versions explained.");
        var gowAscensionCompiled = new PatchSelectionCompiler().Compile(
            File.ReadAllText(bundledCanaryDataPath),
            [gowAscensionCanaryMatch],
            [gowAscensionCanaryMatch]);
        Assert(
            gowAscensionCompiled.IsOk &&
            gowAscensionCompiled.PatText.Contains("0x003a52b4", StringComparison.OrdinalIgnoreCase) &&
            gowAscensionCompiled.PatText.Contains("0x002d024c", StringComparison.OrdinalIgnoreCase),
            "Expected the God of War Ascension Unlock FPS Canary definition to retain both official Wiki operations.");

        var bundledCanaryItem = new PatItVm(
            gowAscensionCanaryMatch);
        Assert(
            bundledCanaryItem.HasDef &&
            !bundledCanaryItem.IsDef &&
            bundledCanaryItem.DefState == UiText.GetText("Patch.Definition.NotMerged") &&
            bundledCanaryItem.StateDsp == UiText.GetText("Patch.State.Ready") &&
            bundledCanaryItem.CurrentStateDsp == UiText.Fmt(
                "Patch.State.Current",
                UiText.GetText("Patch.State.Disabled")) &&
            bundledCanaryItem.StateTon == "Muted" &&
            !bundledCanaryItem.IsSel,
            "Expected a bundled Canary option to remain explicitly not merged, report its actual disabled current state separately from readiness, and never start selected.");
        var importedCanaryMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCanary with { Source = PatSrc.Imported },
            [gowAscensionGame]);
        var importedCanaryItem = new PatItVm(
            importedCanaryMatch);
        Assert(
            importedCanaryItem.IsDef &&
            importedCanaryItem.IsCanary &&
            importedCanaryItem.PatSrcKd == PatSrcKd.Canary &&
            importedCanaryItem.DefState == UiText.GetText("Patch.Definition.Merged") &&
            importedCanaryItem.StateDsp == UiText.GetText("Patch.State.Disabled") &&
            importedCanaryItem.DirStat == UiText.GetText("Patch.Direct.MergedDisabled"),
            "Expected a resident imported Canary definition to remain in the Canary source channel after its selection checkmark clears.");
        var enabledImportedCanaryItem = new PatItVm(
            importedCanaryMatch,
            isEnabled: true);
        Assert(
            enabledImportedCanaryItem.DefState == UiText.GetText("Patch.Definition.Merged") &&
            enabledImportedCanaryItem.StateDsp == UiText.GetText("Patch.State.Enabled") &&
            enabledImportedCanaryItem.CurrentStateDsp == UiText.Fmt(
                "Patch.State.Current",
                UiText.GetText("Patch.State.Enabled")) &&
            !enabledImportedCanaryItem.IsSel,
            "Expected Canary merge state, explicitly labelled current state, and action selection to remain independent.");
        importedCanaryItem.IsSel = true;
        Assert(
            importedCanaryItem.HasPendingAction &&
            importedCanaryItem.PendingDsp == UiText.GetText("Patch.Pending.Enable") &&
            importedCanaryItem.CurrentStateDsp == UiText.Fmt(
                "Patch.State.Current",
                UiText.GetText("Patch.State.Disabled")),
            "Expected a selected disabled patch to expose a pending-enable presentation state without changing its actual enabled state.");
        enabledImportedCanaryItem.IsSel = true;
        Assert(
            enabledImportedCanaryItem.HasPendingAction &&
            enabledImportedCanaryItem.PendingDsp == UiText.GetText("Patch.Pending.Disable") &&
            enabledImportedCanaryItem.IsOn &&
            enabledImportedCanaryItem.CurrentStateDsp == UiText.Fmt(
                "Patch.State.Current",
                UiText.GetText("Patch.State.Enabled")),
            "Expected a selected enabled patch to expose pending-disable separately while preserving the current enabled state.");

        var gowAscensionCheat = bundledCheatStrictRead.Catalog.Entries.Single(entry =>
            entry.Hash.Equals(
                "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c",
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "(B) Button Enable Code");
        var gowAscensionFpsCheat = bundledCheatStrictRead.Catalog.Entries.Single(entry =>
            entry.Hash.Equals(
                gowAscensionCanary.Hash,
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "Unlock FPS");
        var crossChannelMerge = new PatchCatalogMergeService().Merge(
            new R3Config.Mdl.PatCat("1.2", [gowAscensionCanary]),
            new R3Config.Mdl.PatCat("1.2", [gowAscensionFpsCheat]));
        Assert(
            crossChannelMerge.Entries.Count == 2 &&
            crossChannelMerge.Entries.Any(entry => entry.Risk == PatRisk.Canary) &&
            crossChannelMerge.Entries.Any(entry => entry.Risk == PatRisk.Cheat),
            "Expected same-title Canary and Artemis definitions to remain independent instead of being rejected as a duplicate conflict.");
        var crossChannelPreview = new PatPrev();
        crossChannelPreview.Load(
            crossChannelMerge.Entries
                .Select(entry => new PatchMatchService().Match(entry, [gowAscensionGame]))
                .ToArray(),
            title: null);
        Assert(
            crossChannelPreview.CanItems.Any(item => item.Title == "Unlock FPS") &&
            crossChannelPreview.ChtItems.Any(item => item.Title == "Unlock FPS"),
            "Expected the God of War Ascension Unlock FPS Canary option to remain visible beside the distinct Artemis cheat.");

        var officialCheatDefinition = gowAscensionCheat with
        {
            Source = PatSrc.Official,
            SrcPath = "patch.yml",
            Class = PatClass.Main
        };
        var officialCheatMerge = new PatchCatalogMergeService().Merge(
            new R3Config.Mdl.PatCat("1.2", [officialCheatDefinition]),
            new R3Config.Mdl.PatCat("1.2", [gowAscensionCheat]));
        Assert(officialCheatMerge.Entries.Count == 2 &&
               officialCheatMerge.Entries.Any(entry => entry.Source == PatSrc.Official) &&
               officialCheatMerge.Entries.Any(entry => entry.Source == PatSrc.Artemis) &&
               officialCheatMerge.Diag.Count == 0,
            "Expected an official cheat-risk patch and an Artemis definition with the same identity to remain separate source channels.");
        var officialCheatPreview = new PatPrev();
        officialCheatPreview.Load(
            [new PatchMatchService().Match(officialCheatDefinition, [gowAscensionGame])]);
        Assert(officialCheatPreview.OfclList.Count == 1 &&
               officialCheatPreview.ChtItems.Count == 0 &&
               officialCheatPreview.OfclList.Single().IsOfcl &&
               !officialCheatPreview.OfclList.Single().IsArtemis &&
               officialCheatPreview.OfclList.Single().StateDsp == UiText.GetText("Patch.State.Disabled"),
            "Expected risk classification never to move a patch.yml entry from the official surface into Artemis, and an available disabled official patch to report its actual enable state.");
        var gowAscensionCheatMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCheat,
            [gowAscensionGame]);
        var bundledCheatItem = new PatItVm(
            gowAscensionCheatMatch);
        Assert(
            bundledCheatItem.DefState == UiText.GetText("Patch.Definition.NotPrepared") &&
            !bundledCheatItem.IsDef,
            "Expected a bundled Artemis cheat to say that its game patch definition is not prepared yet.");
        var artemisGroupPresentation = new TgtGrpVm([bundledCheatItem]);
        Assert(
            !artemisGroupPresentation.ArtemisHeader.EndsWith(" (Artemis)", StringComparison.OrdinalIgnoreCase) &&
            artemisGroupPresentation.ArtemisMeta.Contains(
                artemisGroupPresentation.Items.Count.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal),
            "Expected the Artemis tab header to remove the redundant catalog suffix while retaining compact option/state metadata.");
        var managedCheatMatch = new R3Config.Svc.PatchMatchService().Match(
            gowAscensionCheat with { Source = PatSrc.ManagedArtemis },
            [gowAscensionGame]);
        var managedCheatItem = new PatItVm(
            managedCheatMatch);
        Assert(
            managedCheatItem.DefState == UiText.GetText("Patch.Definition.Prepared") &&
            managedCheatItem.StateDsp == UiText.GetText("Patch.State.Disabled") &&
            managedCheatItem.DirStat == UiText.GetText("Patch.Direct.PreparedDisabled"),
            "Expected a resident Artemis cheat to remain visibly prepared even while disabled.");
        var infamousCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
            entry.Hash.Equals(
                "d0c3e2a107c47d20d3316325ce6f75464a8f4f1d",
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "(Experimental) Disable visual effects");
        var infamousEuropeanMatch = new R3Config.Svc.PatchMatchService().Match(
            infamousCanary,
            [new Rpcs3GameDescriptor(
                "BCES00220",
                "inFamous",
                @"C:\Games\BCES00220",
                "01.00",
                null,
                null,
                HCanPpu: ["d0c3e2a107c47d20d3316325ce6f75464a8f4f1d"])]);
        Assert(
            infamousEuropeanMatch.Status == MatStat.Match,
            "Expected the d0c3 inFamous Canary definition to retain its declared European serial scope.");
        var infamousCrossRegionMatch = new R3Config.Svc.PatchMatchService().Match(
            infamousCanary,
            [new Rpcs3GameDescriptor(
                "BCUS98119",
                "inFamous",
                @"C:\Games\BCUS98119",
                "01.00",
                null,
                null,
                HCanPpu: ["d0c3e2a107c47d20d3316325ce6f75464a8f4f1d"])]);
        Assert(
            infamousCrossRegionMatch.Status == MatStat.SerBad,
            "Expected a matching hash not to broaden a Canary definition across undeclared game regions.");
        var infamousAmericanCanary = bundledCanaryRead.Catalog.Entries.Single(entry =>
            entry.Hash.Equals(
                "412a9bf323b4743de21658a286f71dbe82cad807",
                StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "(Experimental) Disable visual effects");
        var infamousAmericanMatch = new R3Config.Svc.PatchMatchService().Match(
            infamousAmericanCanary,
            [new Rpcs3GameDescriptor(
                "BCUS98119",
                "inFamous",
                @"C:\Games\BCUS98119",
                "01.00",
                null,
                null,
                HCanPpu: ["412a9bf323b4743de21658a286f71dbe82cad807"])]);
        Assert(
            infamousAmericanMatch.Status == MatStat.Match,
            "Expected BCUS98119 to use the separate 412a Canary runtime identity declared by the source.");
        var infamousCanaryCompiled = new PatchSelectionCompiler().Compile(
            File.ReadAllText(bundledCanaryDataPath),
            [infamousEuropeanMatch],
            [infamousEuropeanMatch]);
        Assert(
            infamousCanaryCompiled.IsOk &&
            !infamousCanaryCompiled.PatText.Contains(
                "*Infam1_",
                StringComparison.Ordinal),
            "Expected selected Canary output to expand YAML anchors before imported_patch.yml merge: " +
            infamousCanaryCompiled.Error);
        Assert(File.Exists(mainVmPath), "Expected MainVm source to exist in App/Vm.");
        Assert(File.Exists(mainWindowPath), "Expected MainWindow source to exist in App.");
        Assert(File.Exists(mainWindowXamlPath), "Expected MainWindow XAML to exist in App.");
        Assert(File.Exists(gamesViewPath), "Expected game list view source to exist in App/Ctrl.");
        Assert(File.Exists(configViewPath), "Expected config recommendation view source to exist in App/Ctrl.");
        Assert(File.Exists(hCanaryViewPath), "Expected H-Canary view source to exist in App/Ctrl.");
        Assert(File.Exists(advancedViewPath), "Expected the advanced tools view to exist in App/View.");
        Assert(File.ReadAllText(planVmPath).Contains("_applied = true", StringComparison.Ordinal),
            "Expected plan view to show applied recommendations by default.");
        Assert(File.ReadAllText(appDialogPath).Contains("AllowsTransparency=\"True\"", StringComparison.Ordinal),
            "Expected custom app dialogs to avoid black rounded-corner artifacts.");
        var mainWindowXaml = File.ReadAllText(mainWindowXamlPath);
        Assert(mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Settings}\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Patches}\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Canary}\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Cheats}\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Setup}\"", StringComparison.Ordinal),
            "Expected the main navigation to expose settings, patch classes, and the final change summary.");
        var settingsTabPos = mainWindowXaml.IndexOf("Header=\"{lang:TextExt Key=Tab.Settings}\"", StringComparison.Ordinal);
        var patchesTabPos = mainWindowXaml.IndexOf("Header=\"{lang:TextExt Key=Tab.Patches}\"", StringComparison.Ordinal);
        var canaryTabPos = mainWindowXaml.IndexOf("Header=\"{lang:TextExt Key=Tab.Canary}\"", StringComparison.Ordinal);
        var cheatsTabPos = mainWindowXaml.IndexOf("Header=\"{lang:TextExt Key=Tab.Cheats}\"", StringComparison.Ordinal);
        var setupTabPos = mainWindowXaml.IndexOf("Header=\"{lang:TextExt Key=Tab.Setup}\"", StringComparison.Ordinal);
        Assert(settingsTabPos < patchesTabPos &&
               patchesTabPos < canaryTabPos &&
               canaryTabPos < cheatsTabPos &&
               cheatsTabPos < setupTabPos,
            "Expected the player workflow to start with settings and finish with the change summary.");
        Assert(!mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Official}\"", StringComparison.Ordinal) &&
               !mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.Advanced}\"", StringComparison.Ordinal) &&
               !mainWindowXaml.Contains("Header=\"{lang:TextExt Key=Tab.HCanary}\"", StringComparison.Ordinal),
            "Expected advanced authoring tools to stay out of the game patch navigation.");
        Assert(mainWindowXaml.Contains("Mode=\"Patches\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Mode=\"Canary\"", StringComparison.Ordinal) &&
               mainWindowXaml.Contains("Mode=\"Cheat\"", StringComparison.Ordinal) &&
               !mainWindowXaml.Contains("<controls:HCanView", StringComparison.Ordinal),
            "Expected each player-facing patch class to have one explicit page.");

        var gamesView = File.ReadAllText(gamesViewPath);
        Assert(!gamesView.Contains("GameNeedBadge", StringComparison.Ordinal) &&
               !gamesView.Contains("NeedDisplay", StringComparison.Ordinal) &&
               !gamesView.Contains("NeedTip", StringComparison.Ordinal),
            "Expected the game rail to omit the ambiguous recommendation count badge.");
        Assert(gamesView.Contains(
                "IsEnabled=\"{Binding CanSelectGame}\"",
                StringComparison.Ordinal),
            "Expected the game rail selection to lock while the owned fingerprint capture session is active.");
        var configView = ReadSources(configViewPaths);
        Assert(configView.Contains("Key=Plan.ApplySection", StringComparison.Ordinal) &&
               configView.Contains("Command=\"{Binding ApplyCmd}\"", StringComparison.Ordinal),
            "Expected game recommendations to expose an explicit guarded apply action.");
        Assert(!configView.Contains("Shell.SettingsSection", StringComparison.Ordinal),
            "Expected the redundant suggested-settings heading to be removed from the settings tab.");

        var patchView = ReadSources(patchViewPaths);
        var patchShellView = File.ReadAllText(patchViewPath);
        var officialView = File.ReadAllText(
            Path.Combine(appRoot, "Ctrl", "POff.xaml"));
        var canaryView = File.ReadAllText(
            Path.Combine(appRoot, "Ctrl", "PCan.xaml"));
        var cheatView = File.ReadAllText(
            Path.Combine(appRoot, "Ctrl", "PCht.xaml"));
        var patchSourceView = File.ReadAllText(
            Path.Combine(appRoot, "Ctrl", "PSrc.xaml"));
        var actionBarView = File.ReadAllText(actionBarViewPath);
        var hCanaryGameView = File.ReadAllText(hCanaryGameViewPath);
        var setupView = File.ReadAllText(
            Path.Combine(appRoot, "Ctrl", "Setup.xaml"));
        var mainSetupVm = File.ReadAllText(
            Path.Combine(appRoot, "Vm", "Main.Setup.cs"));
        var fingerprintCaptureCode = ReadSources(
            Path.Combine(
                appRoot,
                "Application",
                "GameFingerprintCaptureUseCase.cs"),
            Path.Combine(
                appRoot,
                "Svc",
                "Rpcs3FingerprintCaptureServices.cs"),
            Path.Combine(
                appRoot,
                "Vm",
                "Main.Capture.cs"),
            Path.Combine(
                appRoot,
                "Vm",
                "HCan.cs"),
            Path.Combine(
                appRoot,
                "Vm",
                "HCan.Evi.cs"),
            Path.Combine(
                appRoot,
                "Application",
                "HashEvidenceUseCase.cs"),
            Path.Combine(
                repoRoot,
                "Core",
                "Svc",
                "Patch",
                "HCan.cs"));
        Assert(!patchView.Contains("<TabControl", StringComparison.Ordinal) &&
               !patchView.Contains("OfficialSelector", StringComparison.Ordinal) &&
               !patchView.Contains("CanarySelector", StringComparison.Ordinal),
            "Expected patch category navigation to remain at the main page level without a nested selector.");
        Assert(patchView.Contains("OfclOn", StringComparison.Ordinal) &&
               patchView.Contains("CanOn", StringComparison.Ordinal) &&
               patchView.Contains("OfclOff", StringComparison.Ordinal) &&
               patchView.Contains("CanOCmd", StringComparison.Ordinal),
            "Expected each patch section to execute only its own selected items.");
        Assert(officialView.Contains("Visibility=\"{Binding Preview.HasOfclActions,", StringComparison.Ordinal) &&
               canaryView.Contains("Visibility=\"{Binding Preview.HasCanActions,", StringComparison.Ordinal) &&
               cheatView.Contains("Visibility=\"{Binding Preview.HasCht,", StringComparison.Ordinal) &&
               officialView.Contains("Visibility=\"{Binding Preview.OfclOn,", StringComparison.Ordinal) &&
               officialView.Contains("Visibility=\"{Binding Preview.OfclOff,", StringComparison.Ordinal) &&
               canaryView.Contains("Visibility=\"{Binding Preview.SelCanOn,", StringComparison.Ordinal) &&
               canaryView.Contains("Visibility=\"{Binding Preview.CanOff,", StringComparison.Ordinal) &&
               cheatView.Contains("Visibility=\"{Binding Preview.SelChtOn,", StringComparison.Ordinal) &&
               cheatView.Contains("Visibility=\"{Binding Preview.ChtOff,", StringComparison.Ordinal) &&
               officialView.Contains("x:Name=\"OfclActions\"", StringComparison.Ordinal) &&
               officialView.Contains("Grid.Row=\"2\"", StringComparison.Ordinal) &&
               canaryView.Contains("x:Name=\"CanActions\"", StringComparison.Ordinal) &&
               canaryView.Contains("Grid.Row=\"3\"", StringComparison.Ordinal) &&
               cheatView.Contains("x:Name=\"ChtActions\"", StringComparison.Ordinal) &&
               cheatView.Contains("Grid.Row=\"2\"", StringComparison.Ordinal) &&
               officialView.Contains("Text=\"{Binding Preview.OfclSum}\"", StringComparison.Ordinal) &&
               canaryView.Contains("Text=\"{Binding Preview.CanSum}\"", StringComparison.Ordinal) &&
               cheatView.Contains("Text=\"{Binding Preview.ChtSum}\"", StringComparison.Ordinal) &&
               patchView.Contains("Header=\"{lang:TextExt Key=Patch.Artemis.RemoveSelected}\"", StringComparison.Ordinal) &&
               patchView.Contains("Command=\"{Binding ChtRemove}\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Name=\"ArtemisOptionsButton\"", StringComparison.Ordinal) &&
               patchView.Contains("Content=\"{lang:TextExt Key=Patch.Artemis.Options}\"", StringComparison.Ordinal) &&
               !patchView.Contains("Content=\"⋯\"", StringComparison.Ordinal) &&
               patchView.Contains("AutomationProperties.Name=\"{lang:TextExt Key=Patch.Artemis.Options}\"", StringComparison.Ordinal) &&
               patchView.Contains("Command=\"{Binding ChtSelectAll}\"", StringComparison.Ordinal) &&
               patchView.Contains("Command=\"{Binding ChtClearSelection}\"", StringComparison.Ordinal),
            "Expected each patch tab to lead with an actual-state summary, keep mutation buttons conditional on selected transitions, and keep the Artemis bulk-selection menu available whenever Artemis entries exist.");
        Assert(patchView.Contains("Key=Patch.ApplyOfficialSection", StringComparison.Ordinal) &&
               patchView.Contains("Key=Patch.ApplyCanarySection", StringComparison.Ordinal) &&
               patchView.Contains("Key=Patch.ApplyCheatSection", StringComparison.Ordinal) &&
               !patchView.Contains("Key=Patch.ApplySection}", StringComparison.Ordinal),
            "Expected official, Canary, and cheat actions to describe their distinct target-file workflows.");
        Assert(canaryView.Contains("<Border.ToolTip>", StringComparison.Ordinal) &&
               !canaryView.Contains("<TextBlock.ToolTip>", StringComparison.Ordinal) &&
               !canaryView.Contains("<Button.ContextMenu>", StringComparison.Ordinal) &&
               !canaryView.Contains("PatCanaryInfoGlyph", StringComparison.Ordinal),
            "Expected Canary notes to stay available from the row hover surface without a floating information glyph or click-only context menu.");
        Assert(!patchSourceView.Contains("PatCanaryInfoHost", StringComparison.Ordinal) &&
               !patchSourceView.Contains("Text=\"i\"", StringComparison.Ordinal),
            "Expected the official patch toolbar to avoid a floating information-only glyph.");
        Assert(actionBarView.Contains("Command=\"{Binding AnlzCmd}\"", StringComparison.Ordinal) &&
               actionBarView.Contains("Key=Btn.Analyze", StringComparison.Ordinal) &&
               !patchSourceView.Contains("Command=\"{Binding AnlzCmd}\"", StringComparison.Ordinal) && !patchSourceView.Contains("Key=Btn.Analyze", StringComparison.Ordinal) &&
               patchSourceView.Contains("Patch.SourceStatus.UpdateOfficial", StringComparison.Ordinal) &&
               hCanaryGameView.Contains("Command=\"{Binding AnlzCmd}\"", StringComparison.Ordinal) &&
               hCanaryGameView.Contains("Key=Btn.Analyze", StringComparison.Ordinal) &&
               !hCanaryGameView.Contains("HCanary.Start.Game.Scan", StringComparison.Ordinal),
            "Expected the patch source toolbar to avoid duplicating the global RPCS3 analysis action.");
        Assert(!mainSetupVm.Contains("Setup.Warning.HashMismatchCount", StringComparison.Ordinal) &&
               mainSetupVm.Contains("token.Equals(\"HASHMISMATCH\"", StringComparison.Ordinal) &&
               mainSetupVm.Contains("LooksLikeDiagnosticToken", StringComparison.Ordinal) &&
               !setupView.Contains("HASHMISMATCH", StringComparison.Ordinal) &&
               mainSetupVm.Contains("SetupCanaryChangeCount", StringComparison.Ordinal) &&
               setupView.Contains("SetupHasCanaryChanges", StringComparison.Ordinal) &&
               !mainSetupVm.Contains("Setup.CanaryAllIncompatible", StringComparison.Ordinal), "Expected selected deltas only in the change summary.");
        Assert(patchView.Contains("x:Key=\"PatDisclosureExpander\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Key=\"PatDisclosureChevron\"", StringComparison.Ordinal) &&
               patchView.Contains("Value=\"M 1,1 L 5,5 L 9,1\"", StringComparison.Ordinal) &&
               patchView.Contains("Value=\"M 1,5 L 5,1 L 9,5\"", StringComparison.Ordinal) &&
               !patchView.Contains("<Path x:Name=\"Chevron\"", StringComparison.Ordinal) &&
               canaryView.Contains("x:Key=\"CanHead\"", StringComparison.Ordinal) &&
               canaryView.Contains("Style=\"{StaticResource PatDisclosureChevron}\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Key=\"ArtemisTgtHead\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Key=\"TgtHead\"", StringComparison.Ordinal) &&
               patchView.Contains("Grid.Column=\"0\"", StringComparison.Ordinal) &&
               patchView.Contains("Grid.Column=\"2\"", StringComparison.Ordinal) &&
               patchView.Contains("Content=\"{TemplateBinding Header}\"", StringComparison.Ordinal) &&
               patchView.Contains("ContentTemplate=\"{TemplateBinding HeaderTemplate}\"", StringComparison.Ordinal) &&
               patchView.Contains("ContentTemplateSelector=\"{TemplateBinding HeaderTemplateSelector}\"", StringComparison.Ordinal) &&
               canaryView.Contains("Style=\"{StaticResource PatDisclosureExpander}\"", StringComparison.Ordinal),
            "Expected every patch disclosure chevron to sit inside its concrete group header, with the Expander header explicitly forwarded into the full-width toggle hit target.");
        Assert(
            canaryView.Contains("x:Key=\"CanItem\"", StringComparison.Ordinal) &&
            canaryView.Contains("x:Key=\"CanHead\"", StringComparison.Ordinal) &&
            canaryView.Contains("Text=\"{Binding CanNote}\"", StringComparison.Ordinal) &&
            canaryView.Contains("Text=\"{Binding CanMeta}\"", StringComparison.Ordinal) &&
            canaryView.Contains("Text=\"{Binding CurrentStateDsp}\"", StringComparison.Ordinal) &&
            canaryView.Contains("Text=\"{Binding PendingDsp}\"", StringComparison.Ordinal) &&
            canaryView.Contains("Style=\"{StaticResource PatCompactItemRow}\"", StringComparison.Ordinal) &&
            canaryView.Contains("Style=\"{StaticResource PatCanaryNoteToolTip}\"", StringComparison.Ordinal) &&
            !canaryView.Contains("Click=\"OpenNote_Click\"", StringComparison.Ordinal) &&
            !canaryView.Contains("Text=\"{Binding DefState}\"", StringComparison.Ordinal) &&
            !canaryView.Contains("Text=\"{Binding SrcDsp}\"", StringComparison.Ordinal),
            "Expected compact Canary rows to expose actual enabled state and pending action separately while keeping technical/source details out of the primary row.");
        Assert(
            canaryView.Contains(
                "Command=\"{Binding FingerprintCaptureCmd}\"",
                StringComparison.Ordinal) &&
            canaryView.Contains(
                "Text=\"{Binding CanaryVerificationText}\"",
                StringComparison.Ordinal) &&
            canaryView.Contains(
                "ShowCanaryVerificationPanel",
                StringComparison.Ordinal) &&
            !patchShellView.Contains(
                "Command=\"{Binding FingerprintCaptureCmd}\"",
                StringComparison.Ordinal) &&
            !patchShellView.Contains(
                "FingerprintCaptureStatus",
                StringComparison.Ordinal) &&
            !officialView.Contains(
                "FingerprintCaptureCmd",
                StringComparison.Ordinal) &&
            !mainWindowXaml.Contains(
                "FingerprintCaptureCmd",
                StringComparison.Ordinal),
            "Expected Canary verification to appear only inside the Canary surface when Canary entries actually require it, without a global fingerprint control.");
        Assert(
            cheatView.Contains("Preview.ChtEmptyText", StringComparison.Ordinal) &&
            !patchView.Contains("Patch.Launch.BootRecommended", StringComparison.Ordinal),
            "Expected Artemis to use one precise dynamic empty state and Boot Game guidance to appear only at the actual apply confirmation, not persistently in the patch shell.");
        Assert(
            fingerprintCaptureCode.Contains(
                "CanaryExpectation()",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "CanaryHashExpectation",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "if (HasPpu(saved))",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "_ = expectation;",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "ValidateExpectation",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "replaceExisting: expectation.HasTargets",
                StringComparison.Ordinal),
            "Expected Canary capture to preserve one selected-session PPU binding as local authoring evidence without making catalog-key intersection a persistence gate.");
        Assert(
            fingerprintCaptureCode.Contains(
                "FingerprintAlreadyCaptured",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "LoadSaved(install, game)",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "ReloadSavedFingerprint",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "!HasSavedBinding",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "RediscoverCmd = new RelayCmd(\r\n            LaunchGame",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "RediscoverCmd = new RelayCmd(\n            LaunchGame",
                StringComparison.Ordinal),
            "Expected every saved selected-game PPU binding to remain immutable until the user explicitly deletes it without a catalog-driven replacement path.");
        Assert(
            fingerprintCaptureCode.Contains(
                "GameFingerprintCaptureStage",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "WaitingForGameStart",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "IsGameStartupReady",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "IsWindowVisible",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "GetWindowThreadProcessId",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "GetClientRect",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "var final = await CompleteAsync",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "SessionOffset(",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "_sessionProbe",
                StringComparison.Ordinal) &&
            !fingerprintCaptureCode.Contains(
                "FingerprintSignature",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "StopAsync",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "CloseMainWindow",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "Kill(entireProcessTree: true)",
                StringComparison.Ordinal) &&
            fingerprintCaptureCode.Contains(
                "Rpcs3AlreadyRunning",
                StringComparison.Ordinal),
            "Expected the workflow to detect startup without reading hashes live then close its owned process and parse only the selected post-launch log region once.");
        var typeTheme = File.ReadAllText(typeThemePath);
        var captionStyleStart = typeTheme.IndexOf(
            "<Style x:Key=\"ArabicCaptionText\"",
            StringComparison.Ordinal);
        var captionStyleEnd = typeTheme.IndexOf(
            "</Style>",
            captionStyleStart,
            StringComparison.Ordinal);
        var techStyleStart = typeTheme.IndexOf(
            "<Style x:Key=\"TechText\"",
            StringComparison.Ordinal);
        var techStyleEnd = typeTheme.IndexOf(
            "</Style>",
            techStyleStart,
            StringComparison.Ordinal);
        Assert(captionStyleStart >= 0 && captionStyleEnd > captionStyleStart &&
               techStyleStart >= 0 && techStyleEnd > techStyleStart &&
               !typeTheme[captionStyleStart..captionStyleEnd].Contains(
                   "Property=\"ToolTip\"",
                   StringComparison.Ordinal) &&
               !typeTheme[techStyleStart..techStyleEnd].Contains(
                   "Property=\"ToolTip\"",
                   StringComparison.Ordinal),
            "Expected ordinary caption and technical text to avoid implicit copy-of-text hover popups; intentional controls can still opt in explicitly.");
        Assert(patchView.Contains("ToolTip=\"{lang:TextExt Key=Patch.ApplyOfficialSectionTip}\"", StringComparison.Ordinal) &&
               canaryView.Contains("ToolTip=\"{lang:TextExt Key=Patch.ApplyCanarySectionTip}\"", StringComparison.Ordinal) &&
               patchView.Contains("ToolTip=\"{lang:TextExt Key=Patch.ApplyCheatSectionTip}\"", StringComparison.Ordinal) &&
               patchView.Contains("PatCanaryNoteToolTip", StringComparison.Ordinal) &&
               !canaryView.Contains("<Button.ContextMenu>", StringComparison.Ordinal),
            "Expected all long patch workflow explanations to live inside one timed popup instead of occupying the main action surface.");
        var patchItemViewModel = File.ReadAllText(
            Path.Combine(appRoot, "Vm", "PItem.cs"));
        Assert(patchItemViewModel.Contains("public string DefState", StringComparison.Ordinal) &&
               patchItemViewModel.Contains("public bool HasDef", StringComparison.Ordinal),
            "Expected the patch presentation model to retain merged/prepared definition state without forcing it into compact rows.");
        Assert(!patchView.Contains("Patch.SourceStatus.CanaryTitle", StringComparison.Ordinal) &&
               !patchView.Contains("Patch.SourceStatus.CanaryMatching", StringComparison.Ordinal),
            "Expected the Canary player page to omit the repeated source title and permanent technical matching paragraph.");
        Assert(!canaryView.Contains("Key=Patch.CanaryActionsTitle", StringComparison.Ordinal) &&
               !canaryView.Contains("CanStatDsp", StringComparison.Ordinal) &&
               !patchView.Contains("CanBadge", StringComparison.Ordinal) &&
               !patchView.Contains("CanState", StringComparison.Ordinal),
            "Expected the Canary tab and group header to avoid repeated titles, source badges, and group-state filler.");
        Assert(patchView.Contains("x:Key=\"PatOfficialPrimaryButton\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Key=\"PatCanaryPrimaryButton\"", StringComparison.Ordinal) &&
               patchView.Contains("x:Key=\"PatCheatPrimaryButton\"", StringComparison.Ordinal) &&
               patchView.Split(
                   "BasedOn=\"{StaticResource PatPrimaryInlineButton}\"",
                   StringSplitOptions.None).Length - 1 >= 3,
            "Expected the official, Canary, and Artemis positive patch actions to use the primary action style without counting unrelated primary controls.");
        Assert(!patchView.Contains("CanBadge", StringComparison.Ordinal) &&
               !patchView.Contains("CanState", StringComparison.Ordinal) &&
               patchView.Contains("OfclMgr", StringComparison.Ordinal) &&
               patchView.Contains("Patch.SourceStatus.UpdateOfficialTip", StringComparison.Ordinal) &&
               !patchView.Contains("SrcPath", StringComparison.Ordinal) &&
               !patchView.Contains("CanPath", StringComparison.Ordinal) &&
               !patchView.Contains("PatDir", StringComparison.Ordinal),
            "Expected player patch pages to expose only official database readiness and its RPCS3-owned update workflow without repeated Canary or file-system details.");
        Assert(!patchView.Contains("Command=\"{Binding MgrCmd}\"", StringComparison.Ordinal) &&
               !patchView.Contains("Command=\"{Binding PatCmd}\"", StringComparison.Ordinal) &&
               !patchView.Contains("Command=\"{Binding PatOff}\"", StringComparison.Ordinal),
            "Expected official, Canary, and Artemis actions to have no aggregate command binding that can mix patch channels.");
        var patchCompatibilityPath = Path.Combine(repoRoot, "Core", "Svc", "Patch", "PatchCompatibilityService.cs");
        var patchMergePath = Path.Combine(repoRoot, "Core", "Svc", "Patch", "PatchCatalogMergeService.cs");
        var patchMutationPath = Path.Combine(appRoot, "Application", "PatchMutationUseCase.cs");
        var mainPatchPath = Path.Combine(appRoot, "Vm", "Main.Pat.cs");
        var patchCompatibilityText = File.ReadAllText(patchCompatibilityPath);
        var patchMergeText = File.ReadAllText(patchMergePath);
        var patchMutationText = File.ReadAllText(patchMutationPath);
        var mainPatchText = File.ReadAllText(mainPatchPath);
        Assert(patchCompatibilityText.Contains("status == MatStat.Unknown", StringComparison.Ordinal) &&
               !patchCompatibilityText.Contains("status is MatStat.Unknown or MatStat.HashBad", StringComparison.Ordinal) &&
               patchCompatibilityText.Contains("PatchCompatibilityVerdict.Guarded", StringComparison.Ordinal) &&
               patchCompatibilityText.Contains("PatchCompatibilityReasonCode.R3Guard", StringComparison.Ordinal),
            "Expected trusted Canary-class definitions to use RPCS3's runtime-key guard only when local PPU/SPU evidence is absent, never when it contradicts the patch key.");
        Assert(patchMergeText.Contains("return \"Official\";", StringComparison.Ordinal) &&
               patchMergeText.Contains("return \"Canary\";", StringComparison.Ordinal) &&
               patchMergeText.Contains("return \"Artemis\";", StringComparison.Ordinal) &&
               patchMergeText.Contains("return \"Local\";", StringComparison.Ordinal),
            "Expected catalog merging to use source-owned Official, Canary, Artemis, and local channels instead of risk labels.");
        Assert(patchMutationText.Contains("must be applied separately", StringComparison.Ordinal) &&
               mainPatchText.Contains("ApplyPat(PatSect section)", StringComparison.Ordinal) &&
               mainPatchText.Contains("OffPat(PatSect section)", StringComparison.Ordinal) &&
               !mainPatchText.Contains("ApplyPat(PatSect?", StringComparison.Ordinal) &&
               !mainPatchText.Contains("Preview.Sel()", StringComparison.Ordinal),
            "Expected both the UI and application mutation boundary to reject mixed patch-channel activation.");
        Assert(!patchView.Contains("Command=\"{Binding PrepareCanaryPatchesCommand}\"", StringComparison.Ordinal) &&
               !patchView.Contains("Command=\"{Binding CanImp}\"", StringComparison.Ordinal) &&
               !patchView.Contains("Command=\"{Binding CanDel}\"", StringComparison.Ordinal),
            "Expected Canary preparation and authoring to stay automatic or inside advanced tools instead of the player page.");
        var hCanaryView = ReadSources(hCanaryViewPaths);
        Assert(!hCanaryView.Contains("Text=\"{lang:TextExt Key=HCanary.Title}\"", StringComparison.Ordinal) &&
               !hCanaryView.Contains("Text=\"إنشاء باتشات H-Canary\"", StringComparison.Ordinal),
            "Expected the H-Canary editor content to avoid a duplicate internal title.");
        Assert(hCanaryView.Split("<TabControl", StringSplitOptions.None).Length == 2 &&
               hCanaryView.Contains("Key=HCanary.FilesAndTools", StringComparison.Ordinal) &&
               hCanaryView.Contains("<ContextMenu", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"SavedDrawer\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"GameStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"TargetsStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"FingerprintStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"DetailsStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"InstructionsStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"ReviewStep\"", StringComparison.Ordinal) &&
               hCanaryView.Contains("x:Name=\"AnchorsPanel\"", StringComparison.Ordinal),
            "Expected H-Canary to use one six-step navigation level with saved and reference surfaces kept inline.");
        var advancedView = ReadSources(advancedViewPaths);
        Assert(advancedView.Contains("<controls:HCanView", StringComparison.Ordinal) &&
               advancedView.Contains("x:Name=\"HCanaryWorkspace\"", StringComparison.Ordinal) &&
               advancedView.Contains("x:Name=\"AdvancedNavigation\"", StringComparison.Ordinal) &&
               advancedView.Contains("x:Name=\"RecoveryWorkspace\"", StringComparison.Ordinal) &&
               advancedView.Contains("x:Name=\"DiagnosticsWorkspace\"", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.PatchDev.Template", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.OpenHCanary", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.BackToTools", StringComparison.Ordinal) &&
               advancedView.Contains("Key=Adv.Recovery", StringComparison.Ordinal) &&
               advancedView.Contains("Key=Adv.Diagnostics", StringComparison.Ordinal),
            "Expected Advanced Tools to host H-Canary directly beside recovery and diagnostics without another navigation layer.");
        Assert(advancedView.Contains("Binding=\"{Binding MadeAt,", StringComparison.Ordinal) &&
               advancedView.Contains("Binding=\"{Binding DirPath}\"", StringComparison.Ordinal) &&
               !advancedView.Contains("Binding=\"{Binding CreatedAt", StringComparison.Ordinal) &&
               !advancedView.Contains("Binding=\"{Binding DirectoryPath}", StringComparison.Ordinal),
            "Expected recovery columns to bind to BackupSnapshot.MadeAt and BackupSnapshot.DirPath.");
        Assert(!advancedView.Contains("Key=Adv.PatchDev.SourcesTitle", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.PatchDev.MatchTitle", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.PatchDev.WorkflowTitle", StringComparison.Ordinal) &&
               !advancedView.Contains("Key=Adv.PatchDev.SafetyTitle", StringComparison.Ordinal),
            "Expected advanced patch development guidance to stay concise and avoid repeated documentation cards.");

        var mainVm = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(appRoot, "Vm"),
                    "Main*.cs",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
        Assert(!mainVm.Contains("PrepareCanaryPatchesCommand", StringComparison.Ordinal) &&
               !mainVm.Contains("private void PrepareCanaryPatches()", StringComparison.Ordinal),
            "Expected the obsolete standalone Canary preparation command and handler to be removed.");
        Assert(mainVm.Contains("ApplyTxn", StringComparison.Ordinal) &&
               !mainVm.Contains("_flow.PrepareBundledCanaryPatch(_install", StringComparison.Ordinal) &&
               !mainVm.Contains("_flow.PrepareBundledCheatPatch(_install", StringComparison.Ordinal),
            "Expected patch enable to use one staged transaction instead of sequential source writes.");
        Assert(mainVm.Contains("Msg.CfgApplySuccess", StringComparison.Ordinal) &&
               mainVm.Contains("Msg.PatchApplyOfficialSuccess", StringComparison.Ordinal) &&
               mainVm.Contains("Msg.PatchApplyCanarySuccess", StringComparison.Ordinal) &&
               mainVm.Contains("Msg.PatchApplyCheatSuccess", StringComparison.Ordinal),
            "Expected successful settings and patch writes to provide a visible confirmation after disk refresh.");
        Assert(!mainVm.Contains(
                   "Combine(\r\n                _catErr",
                   StringComparison.Ordinal) &&
               !mainVm.Contains(
                   "Combine(\n                _catErr",
                   StringComparison.Ordinal),
            "Expected raw catalog parser diagnostics to stay in source status/logging instead of the global player warning banner.");
        var mainWindow = File.ReadAllText(mainWindowPath);
        Assert(mainVm.Contains("LangChg?.Invoke", StringComparison.Ordinal) &&
               mainWindow.Contains("BuildPat", StringComparison.Ordinal),
            "Expected a live language switch to recreate patch visuals after preserving their view-model state.");
        Assert(patchView.Contains("CompatibilityTip", StringComparison.Ordinal) && patchView.Contains("ArtemisTip", StringComparison.Ordinal) &&
               patchView.Contains("InsText", StringComparison.Ordinal) &&
               !patchView.Contains("EviSum", StringComparison.Ordinal) &&
               !patchView.Contains("TechTgt", StringComparison.Ordinal) &&
               !patchView.Contains("InstVer", StringComparison.Ordinal),
            "Expected player patch rows to render only an actionable state and relevant instructions while keeping technical evidence in optional details.");
        Assert(patchView.Contains("FlowDirection=\"{Binding Preview.TextFlow}\"", StringComparison.Ordinal) &&
               patchView.Contains("FlowDirection=\"{Binding NoteFlow}\"", StringComparison.Ordinal) &&
               patchView.Contains("DynamicResource App.FlowDirection", StringComparison.Ordinal) &&
               patchView.Contains("DynamicResource App.TextFlowDirection", StringComparison.Ordinal) &&
               !patchView.Contains("DynamicResource App.ShellFlowDirection", StringComparison.Ordinal) &&
               !patchView.Contains("FlowDirection=\"RightToLeft\"", StringComparison.Ordinal),
            "Expected patch geometry and localized text to follow the active Windows language direction while technical values remain explicit LTR.");

        var noteService = ReadSources(
            noteServicePath,
            Path.Combine(appRoot, "Svc", "PNote.Ar.cs"));
        Assert(noteService.Contains("Prevents random freezing/crashing while playing the game", StringComparison.Ordinal),
            "Expected exact wiki note phrase mapping source.");
        Assert(noteService.Contains("يمنع حالات التجمد أو الانهيار العشوائي أثناء اللعب", StringComparison.Ordinal),
            "Expected exact Arabic wiki note translation source.");
        var untranslatedWikiDescription = NoteSvc.Trans(
            "Keep this original wiki instruction visible");
        Assert(string.IsNullOrEmpty(untranslatedWikiDescription.Body) &&
               untranslatedWikiDescription.OrigBody ==
                   "Keep this original wiki instruction visible" &&
               untranslatedWikiDescription.HasOrig,
            "Expected an untranslated optional wiki description to be omitted from the primary Arabic surface while retaining the raw text as secondary reference data.");

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

        var importService = File.ReadAllText(importServicePath);
        Assert(importService.Contains("Version: 1.2", StringComparison.Ordinal),
            "Expected imported_patch.yml Version header normalization source.");
        Assert(importService.Contains("\"\\r\\n\"", StringComparison.Ordinal),
            "Expected imported_patch.yml normalization to force Windows CRLF line endings.");
        Assert(importService.Contains("DuplicatePpuMerged", StringComparison.Ordinal),
            "Expected duplicate PPU merge presentation result source.");
        Assert(importService.Contains("FiltDup", StringComparison.Ordinal),
            "Expected duplicate Canary patch names to be handled by a deterministic skip path.");
        Assert(!importService.Contains("WriteManagedImportedPatch", StringComparison.Ordinal),
            "Expected the obsolete direct managed Canary writer to be removed.");
        Assert(importService.Contains("CanStage PrepareManagedImportedPatchFile", StringComparison.Ordinal),
            "Expected managed Canary output to support write-free transaction staging.");
        Assert(importService.Contains("CanStage PrepDel", StringComparison.Ordinal) &&
               !importService.Contains("File.Move", StringComparison.Ordinal) &&
               !importService.Contains("File.Delete", StringComparison.Ordinal),
            "Expected managed Canary removal to prepare a transaction without direct filesystem mutation.");
        Assert(importService.Contains("ExtHash", StringComparison.Ordinal),
            "Expected managed Canary compose to merge inside existing PPU/SPU/PRX/OVL hash blocks.");
        Assert(importService.Contains("DelEquiv", StringComparison.Ordinal),
            "Expected managed Canary writes to keep equivalent definitions but reject conflicting identities explicitly.");
        Assert(importService.Contains("InDir", StringComparison.Ordinal),
            "Expected safe path write guard source.");

        var bundledService = File.ReadAllText(bundledCanaryServicePath);
        Assert(bundledService.Contains("can.yml", StringComparison.Ordinal),
            "Expected bundled Canary service to reference the bundled data file.");
        Assert(!bundledService.Contains("CanaryPatchWriteResult PrepareImportedPatch", StringComparison.Ordinal),
            "Expected the obsolete bundled Canary direct writer to be removed.");
        Assert(bundledService.Contains("public CanStage Prep(", StringComparison.Ordinal),
            "Expected bundled Canary selection to support transaction staging.");
        Assert(bundledService.Contains("private static string Build(", StringComparison.Ordinal),
            "Expected bundled Canary service to write only selected Canary patches.");

        var bundledCheatService = File.ReadAllText(bundledCheatServicePath);
        Assert(bundledCheatService.Contains("public LibStRes Prep(", StringComparison.Ordinal),
            "Expected bundled cheat output to support write-free transaction staging.");

        var transactionService = File.ReadAllText(transactionServicePath);
        Assert(transactionService.Contains("StgAnd", StringComparison.Ordinal) &&
               transactionService.Contains("_BkpSvc.BuildBkp", StringComparison.Ordinal) &&
               transactionService.Contains("_BkpSvc.Restore", StringComparison.Ordinal),
            "Expected staged validation, one backup manifest, and full rollback in the transaction service.");

        var appFlow = File.ReadAllText(appFlowPath);
        var patchMutationUseCase = File.ReadAllText(patchMutationUseCasePath);
        var composition = File.ReadAllText(compositionPath);
        var mutationGuard = File.ReadAllText(mutationGuardPath);
        Assert(appFlow.Contains("_cases.PatchMutation.ApplyTxn", StringComparison.Ordinal) &&
               !appFlow.Contains("PrepareImportedPatchFile", StringComparison.Ordinal) &&
               !appFlow.Contains("PrepareManagedPatchFile", StringComparison.Ordinal) &&
               !appFlow.Contains("PrepareBundledCanaryPatch", StringComparison.Ordinal),
            "Expected AppFlow to remain a thin compatibility facade.");
        Assert(patchMutationUseCase.Contains("bundledCanary.Prep(", StringComparison.Ordinal) &&
               patchMutationUseCase.Contains("bundledCheats.Prep(", StringComparison.Ordinal) &&
               patchMutationUseCase.Contains("applyService.ApplyTxn", StringComparison.Ordinal),
            "Expected the patch mutation use case to stage Canary/Cheat outputs before one transaction.");
        Assert(composition.Contains("new PatchFileTransactionService", StringComparison.Ordinal) &&
               composition.Contains("new Rpcs3MutationGuard", StringComparison.Ordinal),
            "Expected the composition root to share transaction and mutation-guard dependencies.");
        Assert(mutationGuard.Contains("IsR3", StringComparison.Ordinal) &&
               mutationGuard.Contains("IsChild", StringComparison.Ordinal),
            "Expected the central mutation guard to enforce process and path boundaries.");

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
        Assert(bundledCheatRead.Catalog.Entries.Count == 1023,
            "Expected the retained Artemis patch collection to expose its complete 1023-entry accepted inventory.");
        Assert(bundledCheatRead.Catalog.Entries.All(entry => entry.Risk == PatRisk.Cheat),
            "Expected every bundled expansion entry to be handled as a user-selected cheat.");

        Console.WriteLine("App patch presentation source checks passed.");
    }

}
