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
    static void AnlzBusy(
        SmokeEnv env,
        CatState cat)
    {
        var synchronizationContext = new SyncCtx();
        var originalContext = SynchronizationContext.Current;
        List<AnalyzeStage> stages = [];
        var progress = new CaptureProgress<AnalyzeStage>(stages.Add);

        try
        {
            SynchronizationContext.SetSynchronizationContext(
                synchronizationContext);
            _ = TestFlow()
                .DoAsync(
                    env.Install.ExePath,
                    cat.Catalog,
                    progress)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(
                originalContext);
        }

        Assert(synchronizationContext.Posts == 0,
            "Expected analysis continuations to stay off the caller's WPF synchronization context.");
        Assert(stages.SequenceEqual(
            [
                AnalyzeStage.ReadRpcs3,
                AnalyzeStage.ScanGames,
                AnalyzeStage.AnalyzeSettings,
                AnalyzeStage.LoadPatches,
                AnalyzeStage.PrepareResults
            ]),
            "Expected analysis to report the real busy stages in deterministic order.");

        Console.WriteLine("Analysis busy-stage progress regression PASS.");
    }

    static void AnlzPath(
        SmokeEnv env,
        CatState cat)
    {
        var synchronizationContext = new SyncCtx();
        var originalContext = SynchronizationContext.Current;
        List<AnalyzeStage> stages = [];
        var progress = new CaptureProgress<AnalyzeStage>(stages.Add);
        AnalyzeResult result;

        try
        {
            SynchronizationContext.SetSynchronizationContext(
                synchronizationContext);
            result = TestFlow()
                .DoAsync(
                    env.Install.ExePath,
                    cat.Catalog,
                    progress)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(
                originalContext);
        }

        Assert(synchronizationContext.Posts == 0,
            "Expected analysis continuations to stay off the caller's WPF synchronization context.");
        Assert(stages.SequenceEqual(
            [
                AnalyzeStage.ReadRpcs3,
                AnalyzeStage.ScanGames,
                AnalyzeStage.AnalyzeSettings,
                AnalyzeStage.LoadPatches,
                AnalyzeStage.PrepareResults
            ]),
            "Expected analysis to report the real busy stages in deterministic order.");
        Assert(result.Games.Single(row => row.Game.Serial == "BLUS31465").InR3,
            "Expected games.yml discoveries to be marked as detected.");
        Assert(!result.Games.Single(row => row.Game.Serial == "BLES02061").InR3,
            "Expected a custom config without RPCS3 game-library identity evidence to remain undetected.");
        Assert(result.Games.Single(row => row.Game.Serial == "BLES02061")
            .RecState.Items.All(item => !item.IsAction),
            "Expected a custom-config-only catalog game to remain non-actionable until the serial is detected in the RPCS3 game library.");
        Assert(!result.Games.Single(row => row.Game.Serial == "NPUB31547").InR3,
            "Expected catalog-only games without local evidence to remain undetected.");
        Assert(result.Games.Single(row => row.Game.Serial == "NPUB31547")
            .RecState.Items.All(item => !item.IsAction),
            "Expected catalog-only recommendation state to remain non-actionable.");
        Assert(result.Games.Single(row => row.Game.Serial == "NPUB00001").Rec is null,
            "Expected locally detected games without recommendations to remain visible.");
        Assert(result.BuildIdentity is { State: Rpcs3BuildIdentityState.Unavailable },
            "Expected AnalysisUseCase to wire Build Identity into AnalyzeResult end to end through the real composition root, reporting Unavailable for this fixture's RPCS3.log-less install rather than leaving the field null.");

        Console.WriteLine("Analysis read-path responsiveness smoke PASS.");
    }

    static PlanStat Rec_Stat(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor rogue,
        WikiRec recommendation,
        HwInfo lowEndHardware)
    {
        var plan = new PlanService().BuildCfg(install, rogue, recommendation);
        var vblank = plan.Items.Single(item => item.Key == "Vblank Rate");
        var resolutionScale = plan.Items.Single(item => item.Key == "Resolution Scale");
        Assert(!vblank.IsSel,
            "Vblank must not be eligible for the explicit recommended-selection action.");
        Assert(!resolutionScale.IsSel,
            "Hardware-dependent settings must not be eligible for the explicit recommended-selection action.");

        var planViewModel = new PlanVm();
        planViewModel.Load(plan);
        Assert(planViewModel.Items.All(item => !item.IsSel),
            "Expected every item in a newly loaded recommendation plan to start unselected.");

        planViewModel.Select();
        Assert(planViewModel.Items
                .Where(item => item.IsSel)
                .All(item => item.AutoSel && item.IsDiff && item.IsAction),
            "Expected SelectRecommended to select only safe, different, actionable items.");
        Assert(planViewModel.Items
                .Where(item => item.Entry.State is RecState.Manual or RecState.Blocked or RecState.Unknown)
                .All(item => !item.IsSel),
            "Expected SelectRecommended not to select Manual, Blocked, or Unknown items.");

        var selectedChanges = planViewModel.Items
            .Where(item => item.IsSel)
            .Select(item => item.Change)
            .ToArray();
        Assert(selectedChanges.Length == 1,
            "Expected the explicit recommended-selection action to select one safe change.");
        Assert(selectedChanges.Single().Key == "Write Color Buffers",
            "Expected the explicit recommended-selection action to select only Write Color Buffers.");

        planViewModel.ClearSel();
        Assert(planViewModel.Items.All(item => !item.IsSel),
            "Expected ClearSelection to clear every recommendation checkbox.");

        var zcullItems = planViewModel.Items
            .Where(item => item.Change.WikiKey.Equals(
                "zcull accuracy",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert(zcullItems.Length == 2,
            "Expected the RPCS3 Wiki ZCULL recommendation to expose both backing configuration values.");
        zcullItems[0].IsSel = true;
        Assert(zcullItems.All(item => item.IsSel),
            "Expected selecting one composite Wiki recommendation value to select every actionable backing value.");
        zcullItems[1].IsSel = false;
        Assert(zcullItems.All(item => !item.IsSel),
            "Expected clearing one composite Wiki recommendation value to clear every backing value.");
        planViewModel.Select();

        var recStateService = new R3Config.Svc.StateSvc();
        var recStateRecommendation = RecBuild();
        var emptyState = recStateService.Evaluate(rogue, [recStateRecommendation], (_, _) => null);
        Assert(emptyState.Serial == "BLUS31465", "Expected RecState summary to keep the game serial.");
        Assert(emptyState.Total == 3, "Expected BLUS31465 RecState total to be 3.");
        Assert(emptyState.Applied == 0 && emptyState.Pending == 3,
            "Expected empty config to have 0 applied and 3 pending recommendations.");

        var appliedValues = new Dictionary<string, string>
        {
            ["Video/Write Color Buffers"] = "On",
            ["Video/Read Color Buffers"] = "true",
            ["Video/Resolution Scale"] = "100%"
        };
        var appliedState = recStateService.Evaluate(
            rogue,
            [recStateRecommendation],
            (section, key) => appliedValues.TryGetValue($"{section}/{key}", out var value) ? value : null);
        Assert(appliedState.Total == 3 && appliedState.Applied == 3 && appliedState.Pending == 0,
            "Expected normalized current values to count as fully applied.");

        var manualState = recStateService.Evaluate(
            rogue,
            [ManlRec()],
            (_, _) => "60");
        var manualItem = manualState.Items.Single();
        Assert(manualItem.State == RecState.Manual && !manualItem.IsSel,
            "Expected ManualTiming to be Manual and not auto-selectable.");

        var lowEndPlan = new PlanService().BuildCfg(install, rogue, LowRec(), lowEndHardware);
        var lowEndItem = lowEndPlan.Items.Single();
        var noHardwarePlan = new PlanService().BuildCfg(install, rogue, LowRec());
        Assert(lowEndItem.State == RecState.Manual && lowEndItem.IsAction && !lowEndItem.IsSel,
            "Expected a hardware-dependent documented recommendation to stay Manual (actionable, not auto-selected) on Low hardware.");
        Assert(noHardwarePlan.Items.Single().State == lowEndItem.State && noHardwarePlan.Items.Single().IsSel == lowEndItem.IsSel,
            "Expected the hardware tier not to change the state of a documented recommendation.");

        return new PlanStat(plan, selectedChanges);
    }

    static void Plan_Ui()
    {
        var change = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "write color buffers",
            null,
            R3Config.Mdl.RecRisk.Rec);

        var state = new RecItem(
            "TEST00001",
            change.Path,
            "true",
            "true",
            R3Config.Mdl.RecRisk.Rec,
            R3Config.Mdl.RecState.Applied,
            false,
            false,
            false,
            string.Empty);

        var plan = new CfgPlan(
            "TEST00001",
            "Applied Smoke Game",
            "config.yml",
            string.Empty, // Presentation-only plan; no installation or mutation.
            [new CfgEntry(change, "true", state)],
            new RecSum("TEST00001", 1, 1, 0, 0, 0, 0, [state]),
            false);

        var viewModel = new PlanVm();
        viewModel.Load(plan);

        Assert(viewModel.Items.Count == 1,
            "Expected fully applied recommendations to remain visible instead of disappearing.");
        Assert(!viewModel.IsEmpty,
            "Expected fully applied recommendation view not to enter the empty state.");
        Assert(!viewModel.Items.Single().IsSel,
            "Expected an applied recommendation to remain a visible state without becoming a current selection.");
        Assert(viewModel.Items.Single().IsApply && !viewModel.Items.Single().ShowSelection,
            "Expected an applied recommendation to render as applied instead of an empty selectable checkbox.");
        viewModel.Items.Single().IsSel = true;
        Assert(!viewModel.Items.Single().IsSel,
            "Expected a non-actionable applied recommendation not to become selected.");
        viewModel.ClearSel();
        Assert(!viewModel.Items.Single().IsSel,
            "Expected clearing selection to leave applied recommendations unselected.");

        var pendingChange = change with
        {
            Key = "Read Color Buffers",
            WikiKey = "read color buffers"
        };
        var pendingState = state with
        {
            Path = pendingChange.Path,
            Current = "false",
            State = R3Config.Mdl.RecState.Pending,
            IsDiff = true,
            IsAction = true,
            IsSel = true
        };
        var mixedPlan = plan with
        {
            Items =
            [
                plan.Items.Single(),
                new CfgEntry(
                    pendingChange,
                    "false",
                    pendingState)
            ],
            RecState = new RecSum(
                "TEST00001",
                2,
                1,
                1,
                0,
                0,
                0,
                [state, pendingState])
        };

        viewModel.Load(mixedPlan);
        Assert(viewModel.Items.All(item => !item.IsSel),
            "Expected reloading a new plan to start with no implicit selection.");
        var pendingItem = viewModel.Items.Single(item =>
            item.Key == "Read Color Buffers");
        pendingItem.IsSel = true;

        viewModel.ShowDone = false;
        Assert(viewModel.Items.Count == 1 &&
               viewModel.Items.Single().Key == "Read Color Buffers" &&
               viewModel.Items.Single().IsSel,
            "Expected filtering applied rows to preserve the user's explicit pending selection.");
        viewModel.ShowDone = true;
        Assert(viewModel.Items.Single(item =>
                   item.Key == "Read Color Buffers").IsSel &&
               !viewModel.Items.Single(item =>
                   item.Key == "Write Color Buffers").IsSel,
            "Expected rebuilding filtered rows to preserve only the explicit prior selection.");

        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(
                originalLanguage == UiText.ArabLang
                    ? UiText.EnLang
                    : UiText.ArabLang);
            viewModel.SetLang();
            viewModel.ShowDone = false;
            viewModel.ShowDone = true;
            Assert(viewModel.Items.Single(item =>
                       item.Key == "Read Color Buffers").IsSel &&
                   !viewModel.Items.Single(item =>
                       item.Key == "Write Color Buffers").IsSel,
                "Expected a language refresh to preserve only explicit user selection.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
            viewModel.SetLang();
        }
    }

}
