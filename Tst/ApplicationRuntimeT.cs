using System.Diagnostics;
using System.IO;
using System.Reflection;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class ApplicationRuntimeT
{
    public static void Run(string tempRoot)
    {
        RunAsync(tempRoot).GetAwaiter().GetResult();
    }

    private static async Task RunAsync(string tempRoot)
    {
        RelayCommandExecutionHonorsRuntimeGuard();
        await AsyncCommandDoubleExecutionIsBlocked();
        MainVmPathReplacementInvalidatesAnalyzedContext(tempRoot);
        GameSetupContextChangeClearsPriorResult(tempRoot);
        await AnalysisCancellationPropagates(tempRoot);
        await MainVmReplacedAnalysisDropsStaleCompletion(tempRoot);
        await OldAnalysisCannotClearReplacementBusyState(tempRoot);
        await MainVmDisposeDropsLateAnalysisCompletion(tempRoot);
        await HCanaryContextReplacementDropsStaleRead(tempRoot);
        await HCanaryContextReplacementDropsStaleFailure(tempRoot);
        await HCanaryDisposeDropsLateFailure(tempRoot);
        await FingerprintContextReplacementClearsCancelledState(tempRoot);
        await PostCommitCancellationDoesNotReportFalseCancellation(tempRoot);
        await PreCancelledFingerprintDoesNotLaunchOrMutate(tempRoot);
        ProcessProbeDisposesEveryEnumeratedHandle();
    }

    private static void RelayCommandExecutionHonorsRuntimeGuard()
    {
        var allowed = false;
        var calls = 0;
        var command = new RelayCmd(
            () => calls++,
            () => allowed);

        command.Execute(null);
        TestAssert.Assert(
            calls == 0,
            "RelayCmd.Execute must not bypass a false CanExecute predicate.");

        allowed = true;
        command.Execute(null);
        TestAssert.Assert(
            calls == 1,
            "RelayCmd.Execute must execute exactly once after CanExecute becomes true.");

        Console.WriteLine(
            "ApplicationRuntimeT.RelayCommandExecutionHonorsRuntimeGuard PASS");
    }

    private static async Task AsyncCommandDoubleExecutionIsBlocked()
    {
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new AsyncCmd(async () =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult(true);
            await release.Task;
        });
        command.CanChg += (_, _) =>
        {
            if (command.CanRun(null) && Volatile.Read(ref calls) > 0)
            {
                finished.TrySetResult(true);
            }
        };

        command.Execute(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        command.Execute(null);

        TestAssert.Assert(
            Volatile.Read(ref calls) == 1 &&
            !command.CanRun(null),
            "AsyncCmd must synchronously own execution before the first await and reject a second invocation.");

        release.TrySetResult(true);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TestAssert.Assert(
            command.CanRun(null) && Volatile.Read(ref calls) == 1,
            "AsyncCmd must restore CanExecute after the owned execution completes.");

        Console.WriteLine(
            "ApplicationRuntimeT.AsyncCommandDoubleExecutionIsBlocked PASS");
    }

    private static void MainVmPathReplacementInvalidatesAnalyzedContext(
        string tempRoot)
    {
        var oldRoot = Path.Combine(
            tempRoot,
            "runtime-context-old");
        var newRoot = Path.Combine(
            tempRoot,
            "runtime-context-new");
        var oldExe = CreateRpcs3Tree(oldRoot);
        var newExe = CreateRpcs3Tree(newRoot);
        var services = new AppRuntimeServices(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-context-settings.json")));
        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        viewModel.ExePath = oldExe;
        SetPrivateField(
            viewModel,
            "_install",
            Rpcs3InstallService.FromExe(oldExe));
        viewModel.Advanced.CfgPath = Path.Combine(
            oldRoot,
            "config",
            "config.yml");
        viewModel.Advanced.PatPath = Path.Combine(
            oldRoot,
            "patches");

        TestAssert.Assert(
            viewModel.CanMut,
            "The controlled precondition must expose an analyzed mutation context before path replacement.");

        viewModel.ExePath = newExe;

        TestAssert.Assert(
            GetPrivateField<Rpcs3InstallDescriptor?>(
                viewModel,
                "_install") is null &&
            !viewModel.CanMut &&
            viewModel.GameList.Games.Count == 0 &&
            viewModel.Advanced.CfgPath == "-" &&
            viewModel.Advanced.PatPath == "-",
            "Changing the editable RPCS3 executable path must invalidate every actionable result owned by the previous install.");

        var missingExe = Path.Combine(newRoot, "missing-rpcs3.exe");
        var priorStatus = viewModel.R3Status;
        viewModel.ExePath = missingExe;
        TestAssert.Assert(
            !string.Equals(
                viewModel.R3Status,
                priorStatus,
                StringComparison.Ordinal) &&
            !viewModel.AnlzCmd.CanRun(null),
            "Editing the RPCS3 path to a missing executable must refresh connection status and keep Analyze disabled.");

        Console.WriteLine(
            "ApplicationRuntimeT.MainVmPathReplacementInvalidatesAnalyzedContext PASS");
    }

    private static void GameSetupContextChangeClearsPriorResult(
        string tempRoot)
    {
        var services = new AppRuntimeServices(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-setup-state-settings.json")));
        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        const string staleSuccess = "stale-game-setup-success";
        SetPrivateField(
            viewModel,
            "_setupResult",
            staleSuccess);

        InvokePrivate(viewModel, "RefreshGameSetupPlan");

        TestAssert.Assert(
            !viewModel.SetupStatus.Contains(
                staleSuccess,
                StringComparison.Ordinal),
            "A Game Setup result belongs to its prior context and must not survive a context-plan refresh.");

        Console.WriteLine(
            "ApplicationRuntimeT.GameSetupContextChangeClearsPriorResult PASS");
    }

    private static async Task AnalysisCancellationPropagates(
        string tempRoot)
    {
        var root = Path.Combine(tempRoot, "runtime-analysis-cancel");
        var exePath = CreateRpcs3Tree(root);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var analysis = CreateAnalysis(
            root,
            _ =>
            {
                entered.Set();
                release.Wait();
                return [];
            });
        using var cancellation = new CancellationTokenSource();

        var task = analysis.RunAsync(
            exePath,
            EmptyCatalog(),
            cancellationToken: cancellation.Token);

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "Analysis did not reach the controlled game-directory enumeration gate.");
            cancellation.Cancel();
            release.Set();
            await MustCancel(task);
        }
        finally
        {
            release.Set();
        }

        Console.WriteLine(
            "ApplicationRuntimeT.AnalysisCancellationPropagates PASS");
    }

    private static async Task MainVmReplacedAnalysisDropsStaleCompletion(
        string tempRoot)
    {
        const string cancellationMarker =
            "expected-analysis-replacement-cancellation";
        var oldRoot = Path.Combine(tempRoot, "runtime-analysis-stale-old");
        var newRoot = Path.Combine(tempRoot, "runtime-analysis-stale-new");
        var oldExe = CreateRpcs3Tree(oldRoot);
        var newExe = CreateRpcs3Tree(newRoot);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var baseCases = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var analysis = CreateAnalysis(
            oldRoot,
            _ =>
            {
                entered.Set();
                release.Wait();
                throw new OperationCanceledException(cancellationMarker);
            },
            baseCases);
        var services = new AppRuntimeServices(
            new AppFlow(baseCases with
            {
                Analysis = analysis
            }),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-analysis-settings.json")));
        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        viewModel.ExePath = oldExe;

        var analyze = typeof(MainVm).GetMethod(
            "DoAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync was not found.");
        var task = (Task)(analyze.Invoke(viewModel, null) ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return a Task."));

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "MainVm analysis did not reach the controlled scan gate.");
            viewModel.ExePath = newExe;
            release.Set();
            await task;
        }
        finally
        {
            release.Set();
        }

        var installField = typeof(MainVm).GetField(
            "_install",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "MainVm install field was not found.");

        TestAssert.Assert(
            installField.GetValue(viewModel) is null &&
            string.Equals(
                viewModel.ExePath,
                newExe,
                StringComparison.OrdinalIgnoreCase) &&
            !viewModel.IsBusy &&
            !viewModel.Advanced.LogText.Contains(
                cancellationMarker,
                StringComparison.Ordinal),
            "A cancelled analysis for the old RPCS3 path must not publish its install, remain busy, or report expected cancellation as an analysis failure.");

        Console.WriteLine(
            "ApplicationRuntimeT.MainVmReplacedAnalysisDropsStaleCompletion PASS");
    }

    private static async Task OldAnalysisCannotClearReplacementBusyState(
        string tempRoot)
    {
        var oldRoot = Path.Combine(
            tempRoot,
            "runtime-analysis-owner-old");
        var newRoot = Path.Combine(
            tempRoot,
            "runtime-analysis-owner-new");
        var oldExe = CreateRpcs3Tree(oldRoot);
        var newExe = CreateRpcs3Tree(newRoot);
        using var oldEntered = new ManualResetEventSlim();
        using var oldRelease = new ManualResetEventSlim();
        using var newEntered = new ManualResetEventSlim();
        using var newRelease = new ManualResetEventSlim();
        var baseCases = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var analysis = CreateAnalysis(
            tempRoot,
            path =>
            {
                if (path.StartsWith(
                        oldRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    oldEntered.Set();
                    oldRelease.Wait();
                    return [];
                }

                if (path.StartsWith(
                        newRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    newEntered.Set();
                    newRelease.Wait();
                    return [];
                }

                return [];
            },
            baseCases);
        var services = new AppRuntimeServices(
            new AppFlow(baseCases with
            {
                Analysis = analysis
            }),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-analysis-owner-settings.json")));
        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        SetPrivateField(viewModel, "_catalog", EmptyCatalog());
        viewModel.ExePath = oldExe;
        var oldTask = (Task)(InvokePrivate(
            viewModel,
            "DoAsync") ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return the old Task."));

        try
        {
            TestAssert.Assert(
                oldEntered.Wait(TimeSpan.FromSeconds(5)),
                "Old analysis did not reach the controlled scan gate.");

            viewModel.ExePath = newExe;
            var newTask = (Task)(InvokePrivate(
                viewModel,
                "DoAsync") ??
                throw new InvalidOperationException(
                    "MainVm.DoAsync did not return the replacement Task."));

            TestAssert.Assert(
                newEntered.Wait(TimeSpan.FromSeconds(5)),
                "Replacement analysis did not reach the controlled scan gate.");

            oldRelease.Set();
            await oldTask;
            TestAssert.Assert(
                viewModel.IsBusy &&
                string.Equals(
                    viewModel.ExePath,
                    newExe,
                    StringComparison.OrdinalIgnoreCase),
                "A stale analysis finally block must not release busy state owned by the replacement analysis.");

            newRelease.Set();
            await newTask;
            TestAssert.Assert(
                !viewModel.IsBusy,
                "The replacement analysis must release its own busy state after completion.");
        }
        finally
        {
            oldRelease.Set();
            newRelease.Set();
        }

        Console.WriteLine(
            "ApplicationRuntimeT.OldAnalysisCannotClearReplacementBusyState PASS");
    }

    private static async Task MainVmDisposeDropsLateAnalysisCompletion(
        string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "runtime-analysis-dispose");
        var exePath = CreateRpcs3Tree(root);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var baseCases = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var analysis = CreateAnalysis(
            root,
            _ =>
            {
                entered.Set();
                release.Wait();
                return [];
            },
            baseCases);
        var services = new AppRuntimeServices(
            new AppFlow(baseCases with
            {
                Analysis = analysis
            }),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-analysis-dispose-settings.json")));
        var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        viewModel.ExePath = exePath;
        var task = (Task)(InvokePrivate(
            viewModel,
            "DoAsync") ??
            throw new InvalidOperationException(
                "MainVm.DoAsync did not return a Task."));

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "MainVm analysis did not reach the controlled dispose gate.");
            TestAssert.Assert(
                viewModel.IsBusy,
                "The controlled analysis must own busy state before Dispose.");

            viewModel.Dispose();
            TestAssert.Assert(
                !viewModel.IsBusy &&
                !viewModel.AnlzCmd.CanRun(null) &&
                !viewModel.R3Cmd.CanRun(null),
                "Dispose must detach operation-owned busy state and reject new MainVm operations.");

            release.Set();
            await task;
        }
        finally
        {
            release.Set();
            viewModel.Dispose();
        }

        TestAssert.Assert(
            GetPrivateField<Rpcs3InstallDescriptor?>(
                viewModel,
                "_install") is null &&
            !viewModel.IsBusy,
            "A task that completes after MainVm.Dispose must not publish an install or reacquire busy state.");

        Console.WriteLine(
            "ApplicationRuntimeT.MainVmDisposeDropsLateAnalysisCompletion PASS");
    }

    private static async Task HCanaryContextReplacementDropsStaleRead(
        string tempRoot)
    {
        const string oldHash =
            "1111111111111111111111111111111111111111";
        var root = Path.Combine(tempRoot, "runtime-hcan-stale");
        var exePath = CreateRpcs3Tree(root);
        var install = Rpcs3InstallService.FromExe(exePath);
        var oldGame = Game(
            tempRoot,
            "BLUS00001",
            "Old game");
        var newGame = Game(
            tempRoot,
            "BLUS00002",
            "New game");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var noHashes = new HashRead(
            true,
            string.Empty,
            [],
            Code: HashReadCode.NoMatch);
        var hashOps = new HCanHashOps(
            (currentInstall, game) => Session(currentInstall, game),
            (_, _) => { },
            _ => { },
            (_, _) => HashReadCode.NoMatch,
            (_, _) =>
            {
                entered.Set();
                release.Wait();
                return new HashRead(
                    true,
                    "old.log",
                    [new HCanHash(HashKind.Ppu, oldHash, "old.log", 1)],
                    Code: HashReadCode.Found);
            },
            (_, _) => noHashes,
            (_, _) => false);
        using var viewModel = new HCanVm(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            hashOps: hashOps);
        viewModel.SetCtx(install, oldGame);

        var readLog = typeof(HCanVm).GetMethod(
            "ReadLogAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "HCanVm.ReadLogAsync was not found.");
        var task = (Task)(readLog.Invoke(viewModel, null) ??
            throw new InvalidOperationException(
                "HCanVm.ReadLogAsync did not return a Task."));

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "H-Canary read did not reach the controlled log gate.");
            viewModel.SetCtx(install, newGame);
            release.Set();
            await task;
        }
        finally
        {
            release.Set();
        }

        TestAssert.Assert(
            viewModel.Hashes.Count == 0 &&
            viewModel.GameId.Contains(
                newGame.Serial,
                StringComparison.OrdinalIgnoreCase),
            "A completed read for the old game must not project hashes into the replacement game context.");

        Console.WriteLine(
            "ApplicationRuntimeT.HCanaryContextReplacementDropsStaleRead PASS");
    }

    private static async Task HCanaryContextReplacementDropsStaleFailure(
        string tempRoot)
    {
        const string failureMarker =
            "stale-hcan-read-failure";
        var root = Path.Combine(
            tempRoot,
            "runtime-hcan-stale-failure");
        var exePath = CreateRpcs3Tree(root);
        var install = Rpcs3InstallService.FromExe(exePath);
        var oldGame = Game(
            tempRoot,
            "BLUS00011",
            "Old failing game");
        var newGame = Game(
            tempRoot,
            "BLUS00012",
            "Replacement game");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var noHashes = new HashRead(
            true,
            string.Empty,
            [],
            Code: HashReadCode.NoMatch);
        var hashOps = new HCanHashOps(
            (currentInstall, game) => Session(currentInstall, game),
            (_, _) => { },
            _ => { },
            (_, _) => HashReadCode.NoMatch,
            (_, _) =>
            {
                entered.Set();
                release.Wait();
                throw new IOException(failureMarker);
            },
            (_, _) => noHashes,
            (_, _) => false);
        using var viewModel = new HCanVm(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            hashOps: hashOps);
        viewModel.SetCtx(install, oldGame);
        var task = (Task)(InvokePrivate(
            viewModel,
            "ReadLogAsync") ??
            throw new InvalidOperationException(
                "HCanVm.ReadLogAsync did not return a Task."));

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "H-Canary failing read did not reach the controlled gate.");
            viewModel.SetCtx(install, newGame);
            var replacementStatus = viewModel.LogStat;
            release.Set();
            await task;

            TestAssert.Assert(
                viewModel.GameId.Contains(
                    newGame.Serial,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    viewModel.LogStat,
                    replacementStatus,
                    StringComparison.Ordinal) &&
                !viewModel.LogStat.Contains(
                    failureMarker,
                    StringComparison.Ordinal),
                "A stale H-Canary exception must not publish an error into the replacement game context.");
        }
        finally
        {
            release.Set();
        }

        Console.WriteLine(
            "ApplicationRuntimeT.HCanaryContextReplacementDropsStaleFailure PASS");
    }

    private static async Task HCanaryDisposeDropsLateFailure(
        string tempRoot)
    {
        const string failureMarker =
            "disposed-hcan-read-failure";
        var root = Path.Combine(
            tempRoot,
            "runtime-hcan-dispose-failure");
        var exePath = CreateRpcs3Tree(root);
        var install = Rpcs3InstallService.FromExe(exePath);
        var game = Game(
            tempRoot,
            "BLUS00013",
            "Disposed H-Canary game");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var noHashes = new HashRead(
            true,
            string.Empty,
            [],
            Code: HashReadCode.NoMatch);
        var hashOps = new HCanHashOps(
            (currentInstall, currentGame) =>
                Session(currentInstall, currentGame),
            (_, _) => { },
            _ => { },
            (_, _) => HashReadCode.NoMatch,
            (_, _) =>
            {
                entered.Set();
                release.Wait();
                throw new IOException(failureMarker);
            },
            (_, _) => noHashes,
            (_, _) => false);
        var viewModel = new HCanVm(
            new AppFlow(AppComposition.Build(
                new Rpcs3MutationGuard(() => false))),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            hashOps: hashOps);
        viewModel.SetCtx(install, game);
        var task = (Task)(InvokePrivate(
            viewModel,
            "ReadLogAsync") ??
            throw new InvalidOperationException(
                "HCanVm.ReadLogAsync did not return a Task."));

        try
        {
            TestAssert.Assert(
                entered.Wait(TimeSpan.FromSeconds(5)),
                "H-Canary read did not reach the controlled dispose gate.");
            var statusBeforeDispose = viewModel.LogStat;
            viewModel.Dispose();
            TestAssert.Assert(
                !viewModel.ReadCmd.CanRun(null) &&
                !viewModel.CanMut,
                "Disposed HCanVm must reject new read and mutation operations.");

            release.Set();
            await task;

            TestAssert.Assert(
                string.Equals(
                    viewModel.LogStat,
                    statusBeforeDispose,
                    StringComparison.Ordinal) &&
                !viewModel.LogStat.Contains(
                    failureMarker,
                    StringComparison.Ordinal),
                "A read that faults after HCanVm.Dispose must not publish a late error.");
        }
        finally
        {
            release.Set();
            viewModel.Dispose();
        }

        Console.WriteLine(
            "ApplicationRuntimeT.HCanaryDisposeDropsLateFailure PASS");
    }

    private static async Task FingerprintContextReplacementClearsCancelledState(
        string tempRoot)
    {
        const string ppu =
            "1111111111111111111111111111111111111111";
        var oldRoot = Path.Combine(
            tempRoot,
            "runtime-fingerprint-context-old");
        var newRoot = Path.Combine(
            tempRoot,
            "runtime-fingerprint-context-new");
        var oldExe = CreateRpcs3Tree(oldRoot);
        var newExe = CreateRpcs3Tree(newRoot);
        var install = Rpcs3InstallService.FromExe(oldExe);
        var game = Game(
            tempRoot,
            "BLUS00014",
            "Fingerprint context game");
        var store = new HCanDiscoveryStore(Path.Combine(
            tempRoot,
            "runtime-fingerprint-context-state.json"));
        using var startupObserved = new ManualResetEventSlim();
        var controller = new ContextCancellationProcessController(
            startupObserved);
        var baseCases = AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        var capture = new GameFingerprintCaptureUseCase(
            new HashEvidenceUseCase(
                new HCanHashDiscoveryService(),
                store,
                new NeverRunningProcessProbe()),
            new GameLaunchUseCase(
                new Rpcs3BootPathResolver(),
                new CountingLauncher()),
            controller,
            new GameFingerprintCaptureOptions(
                Timeout: TimeSpan.FromSeconds(5),
                PollInterval: TimeSpan.FromSeconds(5),
                StartupStabilityDelay: TimeSpan.Zero,
                GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                PostExitLogDelay: TimeSpan.Zero));
        var services = new AppRuntimeServices(
            new AppFlow(baseCases with
            {
                FingerprintCapture = capture
            }),
            new SetSvc(Path.Combine(
                tempRoot,
                "runtime-fingerprint-context-settings.json")));
        using var viewModel = new MainVm(
            services,
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        viewModel.ExePath = oldExe;
        SetPrivateField(viewModel, "_install", install);
        SetPrivateField(
            viewModel,
            "_patCat",
            new PatCat(
                "1",
                [new Rpcs3PatchEntry(
                    "Fingerprint context patch",
                    HashKind.Ppu,
                    ppu,
                    [new R3PatTgt(
                        game.Title,
                        game.Serial,
                        [game.Version ?? PatVer.All])],
                    "Canary",
                    PatRisk.Canary,
                    Notes: null,
                    Author: "R3Config Tests",
                    PatVer: "1.0",
                    Group: null,
                    Source: PatSrc.Can,
                    Class: PatClass.Canary)]));
        var gameVm = new GameVm(
            game,
            null,
            new RecSum(game.Serial, 0, 0, 0, 0, 0, 0, []),
            game.Serial,
            game.Title,
            isDetectedInRpcs3: true);
        viewModel.GameList.Load([gameVm]);
        viewModel.GameList.SelGame = gameVm;

        var task = (Task)(InvokePrivate(
            viewModel,
            "CaptureFingerprintAsync") ??
            throw new InvalidOperationException(
                "MainVm.CaptureFingerprintAsync did not return a Task."));

        TestAssert.Assert(
            startupObserved.Wait(TimeSpan.FromSeconds(5)),
            "Fingerprint capture did not reach the controlled startup gate.");
        TestAssert.Assert(
            viewModel.IsFingerprintCaptureRunning,
            "The controlled fingerprint capture must own running state before context replacement.");

        viewModel.ExePath = newExe;
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        TestAssert.Assert(
            !viewModel.IsFingerprintCaptureRunning &&
            !viewModel.ShowFingerprintCaptureStatus &&
            controller.StopCalls == 1 &&
            store.LoadActive() is null,
            "Replacing the fingerprint context must cancel the old capture, clear its stage/error state, and release its owned discovery session without publishing into the new context.");

        Console.WriteLine(
            "ApplicationRuntimeT.FingerprintContextReplacementClearsCancelledState PASS");
    }

    private static async Task PostCommitCancellationDoesNotReportFalseCancellation(
        string tempRoot)
    {
        const string ppu =
            "2222222222222222222222222222222222222222";
        var root = Path.Combine(
            tempRoot,
            "runtime-fingerprint-post-commit-cancel");
        var exePath = CreateRpcs3Tree(root);
        var logDir = Path.Combine(root, "log");
        var logPath = Path.Combine(logDir, "RPCS3.log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(logPath, string.Empty);
        var install = Rpcs3InstallService.FromExe(exePath);
        var game = Game(
            tempRoot,
            "BLUS00015",
            "Post-commit cancellation game");
        var store = new HCanDiscoveryStore(Path.Combine(
            tempRoot,
            "runtime-fingerprint-post-commit-state.json"));
        var controller = new CompletingProcessController(() =>
            File.AppendAllText(
                logPath,
                $"SYS: Serial: {game.Serial}{Environment.NewLine}" +
                $"SYS: App version: {game.Version}{Environment.NewLine}" +
                $"ppu_loader: PPU executable hash: PPU-{ppu}{Environment.NewLine}" +
                $"SYS: Stopping emulator...{Environment.NewLine}"));
        var capture = new GameFingerprintCaptureUseCase(
            new HashEvidenceUseCase(
                new HCanHashDiscoveryService(),
                store,
                new NeverRunningProcessProbe()),
            new GameLaunchUseCase(
                new Rpcs3BootPathResolver(),
                new CountingLauncher()),
            controller,
            new GameFingerprintCaptureOptions(
                Timeout: TimeSpan.FromSeconds(1),
                PollInterval: TimeSpan.FromMilliseconds(1),
                StartupStabilityDelay: TimeSpan.Zero,
                GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                PostExitLogDelay: TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnCompletedProgress(cancellation);

        var result = await capture.CaptureAsync(
            install,
            game,
            new CanaryHashExpectation([ppu], []),
            progress,
            cancellation.Token);

        TestAssert.Assert(
            cancellation.IsCancellationRequested &&
            result.Succeeded &&
            result.Error == GameFingerprintCaptureError.None &&
            result.Evidence is { IsSaved: true } &&
            store.LoadBinding(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath) is not null,
            "Cancellation requested only after the fingerprint commit must not rewrite a successful mutation as Cancelled or discard its saved binding.");

        Console.WriteLine(
            "ApplicationRuntimeT.PostCommitCancellationDoesNotReportFalseCancellation PASS");
    }

    private static async Task PreCancelledFingerprintDoesNotLaunchOrMutate(
        string tempRoot)
    {
        var root = Path.Combine(tempRoot, "runtime-fingerprint-cancel");
        var exePath = CreateRpcs3Tree(root);
        var install = Rpcs3InstallService.FromExe(exePath);
        var game = Game(
            tempRoot,
            "BLUS00003",
            "Cancelled game");
        var store = new HCanDiscoveryStore(Path.Combine(
            tempRoot,
            "runtime-fingerprint-state.json"));
        var launcher = new CountingLauncher();
        var controller = new CountingProcessController();
        var capture = new GameFingerprintCaptureUseCase(
            new HashEvidenceUseCase(
                new HCanHashDiscoveryService(),
                store,
                new NeverRunningProcessProbe()),
            new GameLaunchUseCase(
                new Rpcs3BootPathResolver(),
                launcher),
            controller,
            new GameFingerprintCaptureOptions(
                Timeout: TimeSpan.FromSeconds(1),
                PollInterval: TimeSpan.FromMilliseconds(1),
                StartupStabilityDelay: TimeSpan.Zero,
                GracefulCloseTimeout: TimeSpan.FromMilliseconds(1),
                PostExitLogDelay: TimeSpan.Zero));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await capture.CaptureAsync(
            install,
            game,
            cancellationToken: cancellation.Token);

        TestAssert.Assert(
            !result.Succeeded &&
            result.Error == GameFingerprintCaptureError.Cancelled &&
            launcher.StartCalls == 0 &&
            controller.AttachCalls == 0 &&
            controller.StopCalls == 0 &&
            store.LoadActive() is null &&
            store.LoadBinding(
                install.ExePath,
                game.Serial,
                game.Version,
                game.InstPath) is null,
            "A pre-cancelled fingerprint request must not launch RPCS3 or create active/saved discovery state.");

        Console.WriteLine(
            "ApplicationRuntimeT.PreCancelledFingerprintDoesNotLaunchOrMutate PASS");
    }

    private static void ProcessProbeDisposesEveryEnumeratedHandle()
    {
        var expectedPath = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "The current process path is unavailable.");
        var first = Process.GetProcessById(Environment.ProcessId);
        var second = Process.GetProcessById(Environment.ProcessId);
        try
        {
            var probe = new Rpcs3ProcessProbe(_ => [first, second]);
            var result = probe.Find(expectedPath);

            TestAssert.Assert(
                result.IsRunning &&
                IsDisposed(first) &&
                IsDisposed(second),
                "Process enumeration must dispose both the matched handle and every unvisited handle after an early return.");
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }

        Console.WriteLine(
            "ApplicationRuntimeT.ProcessProbeDisposesEveryEnumeratedHandle PASS");
    }

    private static AnalysisUseCase CreateAnalysis(
        string stateRoot,
        Func<string, IEnumerable<string>> enumerateDirectories,
        AppUseCases? baseCases = null)
    {
        baseCases ??= AppComposition.Build(
            new Rpcs3MutationGuard(() => false));
        return new AnalysisUseCase(
            new GameScanner(
                enumerateDirectories: enumerateDirectories),
            new PlanService(),
            new HwProbe(),
            new PatchHashProbeService(),
            new HCanBindingEvidenceService(
                new HCanDiscoveryStore(Path.Combine(
                    stateRoot,
                    "analysis-hcan-state.json"))),
            baseCases.PatchCatalog,
            baseCases.Backup);
    }

    private static string CreateRpcs3Tree(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "config"));
        Directory.CreateDirectory(Path.Combine(root, "patches"));
        Directory.CreateDirectory(Path.Combine(
            root,
            "dev_hdd0",
            "game"));
        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);
        return exePath;
    }

    private static RecSvc EmptyCatalog()
    {
        return RecSvc.FromRec(
            "application runtime test",
            "1",
            []);
    }

    private static Rpcs3GameDescriptor Game(
        string tempRoot,
        string serial,
        string title)
    {
        var path = Path.Combine(tempRoot, "games", serial);
        Directory.CreateDirectory(path);
        return new Rpcs3GameDescriptor(
            serial,
            title,
            path,
            "01.00",
            null,
            null);
    }

    private static HCanDiscoverySession Session(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game)
    {
        return new HCanDiscoverySession(
            install.ExePath,
            game.Serial,
            game.Version,
            game.InstPath,
            0,
            DateTime.UtcNow,
            Path.Combine(install.DataRoot ?? install.RootPath, "log", "RPCS3.log"),
            new HCanLogMark(false, 0, null, string.Empty));
    }

    private static void SetPrivateField<T>(
        object target,
        string fieldName,
        T value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(
        object target,
        string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target)!;
    }

    private static object? InvokePrivate(
        object target,
        string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Method '{methodName}' was not found on {target.GetType().Name}.");
        return method.Invoke(target, null);
    }

    private static async Task MustCancel(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(
            "The cancelled operation completed successfully instead of propagating cancellation.");
    }

    private static bool IsDisposed(Process process)
    {
        try
        {
            _ = process.Handle;
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class NeverRunningProcessProbe : IRpcs3ProcessProbe
    {
        public Rpcs3ProcessState Find(
            string executablePath,
            int? expectedProcessId = null)
        {
            return new Rpcs3ProcessState(false);
        }
    }

    private sealed class CountingLauncher : IRpcs3GameProcessLauncher
    {
        public int StartCalls { get; private set; }

        public int Start(
            string executablePath,
            string workingDirectory,
            string bootTarget)
        {
            StartCalls++;
            return 4242;
        }
    }

    private sealed class ContextCancellationProcessController(
        ManualResetEventSlim startupObserved)
        : IRpcs3OwnedProcessController
    {
        public int StopCalls { get; private set; }

        public bool TryAttach(
            string executablePath,
            int processId,
            out DateTime processStartedUtc)
        {
            processStartedUtc = DateTime.UtcNow;
            return true;
        }

        public bool IsRunning(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return true;
        }

        public bool IsGameStartupReady(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            startupObserved.Set();
            return false;
        }

        public Task<Rpcs3OwnedProcessStopResult> StopAsync(
            string executablePath,
            int processId,
            DateTime launchedUtc,
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.FromResult(
                new Rpcs3OwnedProcessStopResult(true));
        }
    }

    private sealed class CancelOnCompletedProgress(
        CancellationTokenSource cancellation)
        : IProgress<GameFingerprintCaptureProgress>
    {
        public void Report(GameFingerprintCaptureProgress value)
        {
            if (value.Stage == GameFingerprintCaptureStage.Completed)
            {
                cancellation.Cancel();
            }
        }
    }

    private sealed class CompletingProcessController(Action beforeStop)
        : IRpcs3OwnedProcessController
    {
        public bool TryAttach(
            string executablePath,
            int processId,
            out DateTime processStartedUtc)
        {
            processStartedUtc = DateTime.UtcNow;
            return true;
        }

        public bool IsRunning(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return true;
        }

        public bool IsGameStartupReady(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return true;
        }

        public Task<Rpcs3OwnedProcessStopResult> StopAsync(
            string executablePath,
            int processId,
            DateTime launchedUtc,
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken)
        {
            beforeStop();
            return Task.FromResult(
                new Rpcs3OwnedProcessStopResult(true));
        }
    }

    private sealed class CountingProcessController
        : IRpcs3OwnedProcessController
    {
        public int AttachCalls { get; private set; }

        public int StopCalls { get; private set; }

        public bool TryAttach(
            string executablePath,
            int processId,
            out DateTime processStartedUtc)
        {
            AttachCalls++;
            processStartedUtc = DateTime.UtcNow;
            return true;
        }

        public bool IsRunning(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return true;
        }

        public bool IsGameStartupReady(
            string executablePath,
            int processId,
            DateTime processStartedUtc)
        {
            return true;
        }

        public Task<Rpcs3OwnedProcessStopResult> StopAsync(
            string executablePath,
            int processId,
            DateTime launchedUtc,
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.FromResult(new Rpcs3OwnedProcessStopResult(true));
        }
    }
}
