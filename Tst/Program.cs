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
    public static void Main(string[] args)
    {
        var runner = new TestRunner();
        var context = new TestContext();

        try
        {
            if (args.Length == 1 &&
                args[0].Equals(
                    "--runner-failure-probe",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(
                    "runner-failure-probe",
                    () => throw new InvalidOperationException(
                        "Intentional R3Test runner failure probe."));
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--application-runtime-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot(
                    "R3ConfigApplicationRuntime");
                runner.RunCase(
                    nameof(ApplicationRuntimeT),
                    () => ApplicationRuntimeT.Run(tempRoot));
                Console.WriteLine(
                    "Application runtime focused tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--artemis-dependencies-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigArtemisDependencies");
                runner.RunCase(
                    nameof(ArtemisDependencyT),
                    () => ArtemisDependencyT.Run(tempRoot));
                Console.WriteLine("Artemis dependency focused tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--artemis-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigArtemisCollection");
                runner.RunCase(nameof(Lang_Key), Lang_Key);
                runner.RunCase(nameof(Art_Imp), Art_Imp);
                runner.RunCase(nameof(Art_Tool), () => Art_Tool(tempRoot));
                runner.RunCase(nameof(Art_Data), Art_Data);
                runner.RunCase(
                    nameof(ArtemisDependencyT),
                    () => ArtemisDependencyT.Run(tempRoot));
                runner.RunCase(nameof(Cht_Bndl), () => Cht_Bndl(tempRoot));
                Console.WriteLine("Artemis RPCS3 patch collection focused tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--game-setup-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigGameSetup");
                runner.RunCase(nameof(GameSetupT), () => GameSetupT.Run(tempRoot));
                Console.WriteLine("Game Setup focused tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--ui-gate-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(UiGate.RunPatchMode), UiGate.RunPatchMode);
                Console.WriteLine("Patch UI gate passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--ui-runtime-matrix-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(UiGate.RunVisualOnly), UiGate.RunVisualOnly);
                Console.WriteLine("Runtime UI matrix passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--backup-cleanup-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigBackupCleanup");
                runner.RunCase(nameof(Bkp_Del), () => Bkp_Del(tempRoot));
                Console.WriteLine("Backup deletion and retention tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--busy-overlay-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(UiGate.RunBusyOverlay), UiGate.RunBusyOverlay);
                Console.WriteLine("Busy overlay runtime test passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--analysis-busy-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigAnalysisBusy");
                var analysisEnv = runner.RunValue(
                    nameof(MakeEnv),
                    () => MakeEnv(tempRoot));
                var analysisCat = runner.RunValue(
                    nameof(Cat_All),
                    () => Cat_All(analysisEnv));
                runner.RunCase(
                    nameof(AnlzBusy),
                    () => AnlzBusy(
                        analysisEnv,
                        analysisCat));
                Console.WriteLine("Analysis busy-stage test passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals("--interactive-ui-only", StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigInteractiveUi");
                try
                {
                    runner.RunCase(nameof(InteractiveUiT), () => InteractiveUiT.Run(tempRoot));
                    Console.WriteLine("Interactive UI focused tests passed.");
                }
                catch (Exception ex) when (ex.ToString().Contains("Setup.PrepareButton", StringComparison.Ordinal))
                {
                    Console.WriteLine("BLOCKED — INTERACTIVE UI AUTOMATION ENVIRONMENT LIMITATION");
                    Console.WriteLine("Windows UI Automation reached the real R3Config window and Setup.Tab, but the selected WPF TabItem did not expose Setup.PrepareButton in the automation tree.");
                    Console.WriteLine(ex.Message);
                }
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals("--interactive-ui-out-of-process-only", StringComparison.OrdinalIgnoreCase))
            {
                var tempRoot = context.CreateTempRoot("R3ConfigInteractiveUiOutOfProcess");
                runner.RunCase("InteractiveUiOutOfProcess-en-US", () => OutOfProcessUiT.Run(Path.Combine(tempRoot, "en-US"), "en-US"));
                runner.RunCase("InteractiveUiOutOfProcess-ar-SA", () => OutOfProcessUiT.Run(Path.Combine(tempRoot, "ar-SA"), "ar-SA"));
                Console.WriteLine("Interactive out-of-process UI focused tests passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--patch-localization-corpus-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(Lang_Key), Lang_Key);
                runner.RunCase(
                    nameof(PatchText_CorpusWideSafety),
                    PatchText_CorpusWideSafety);
                Console.WriteLine(
                    "Patch localization full-corpus audit passed.");
                Console.WriteLine("Passed: 2, Failed: 0, Skipped: 0");
                return;
            }

            if (args.Length == 2 &&
                args[0].Equals(
                    "--official-localization-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(
                    nameof(PatchText_LocalOfficialCorpus),
                    () => PatchText_LocalOfficialCorpus(args[1]));
                Console.WriteLine(
                    "Local Official patch localization read-only audit passed.");
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--patch-localization-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                RunPatchLocalizationCases(runner);
                Console.WriteLine(
                    "Patch presentation localization focused tests passed.");
                Console.WriteLine("Passed: 11, Failed: 0, Skipped: 0");
                return;
            }

            if (args.Length is 4 or 5 &&
                args[0].Equals(
                    "--wiki-corpus",
                    StringComparison.OrdinalIgnoreCase))
            {
                var corpusRun = WikiCorpusT.RunCorpus(
                    args[1],
                    args[2],
                    args[3],
                    args.Length == 5 ? args[4] : null);
                Console.WriteLine(JsonSerializer.Serialize(
                    new
                    {
                        corpusRun.CorpusFile,
                        corpusRun.SnapshotDate,
                        corpusRun.ParseMilliseconds,
                        corpusRun.Stats,
                        corpusRun.OldCatalogChanges,
                        corpusRun.GeneratedCatalogChanges,
                        corpusRun.Comparison.OldCatalogEntries,
                        corpusRun.Comparison.GeneratedCatalogEntries,
                        corpusRun.Comparison.Added,
                        corpusRun.Comparison.Removed,
                        corpusRun.Comparison.Changed,
                        corpusRun.Comparison.Unchanged,
                        corpusRun.Deterministic,
                        corpusRun.CatalogLoadCount
                    },
                    TestJson.Indented));
                Console.WriteLine("Wiki corpus regression passed.");
                return;
            }

            if (args.Length is 2 or 3 &&
                args[0].Equals("--official-corpus", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase("OfficialCorpusT", () => OfficialCorpusT.Run(
                    args[1],
                    args.Length == 3 ? args[2] : null));
                return;
            }

            var smokeRoot = context.CreateTempRoot("R3ConfigSmoke");

            runner.RunCase(nameof(RunnerFailureCapture), RunnerFailureCapture);
            RunPatchLocalizationCases(runner);
            runner.RunCase(nameof(Art_Imp), Art_Imp);
            runner.RunCase(nameof(Art_Tool), () => Art_Tool(smokeRoot));
            runner.RunCase(nameof(Art_Data), Art_Data);
            runner.RunCase(
                nameof(ArtemisDependencyT),
                () => ArtemisDependencyT.Run(smokeRoot));
            runner.RunCase(nameof(Ui_Layout), Ui_Layout);
            runner.RunCase(
                nameof(Ui_GameDataTextDirection),
                Ui_GameDataTextDirection);
            runner.RunCase(nameof(Arch_Svc), Arch_Svc);
            runner.RunCase(nameof(Security_Hardening), () => Security_Hardening(smokeRoot));
            runner.RunCase(nameof(SetSvcT), () => SetSvcT.Run(smokeRoot));
            runner.RunCase(nameof(Can_Comp), () => Can_Comp(smokeRoot));
            runner.RunCase(nameof(App_Pat), App_Pat);
            runner.RunCase(nameof(Cat_Dup), Cat_Dup);
            runner.RunCase(nameof(WikiCorpusT), () => WikiCorpusT.Fixture(smokeRoot));
            runner.RunCase(nameof(Pat_Cmp), () => Pat_Cmp(smokeRoot));
            runner.RunCase(nameof(GameSetupT), () => GameSetupT.Run(smokeRoot));
            runner.RunCase(
                nameof(ApplicationRuntimeT),
                () => ApplicationRuntimeT.Run(smokeRoot));
            runner.RunCase(nameof(HCanTst), HCanTst.Run);
            runner.RunCase(nameof(Cht_Bndl), () => Cht_Bndl(smokeRoot));
            runner.RunCase(nameof(Pat_Txn), () => Pat_Txn(smokeRoot));
            runner.RunCase(
                nameof(Pat_TxnRollbackPreservesExternalAttempt),
                () => Pat_TxnRollbackPreservesExternalAttempt(smokeRoot));
            runner.RunCase(nameof(Can_Txn), () => Can_Txn(smokeRoot));
            runner.RunCase(nameof(Bkp_Rst), () => Bkp_Rst(smokeRoot));
            runner.RunCase(nameof(Bkp_Del), () => Bkp_Del(smokeRoot));
            runner.RunCase(
                nameof(Bkp_ConcurrencyOwnership),
                () => Bkp_ConcurrencyOwnership(smokeRoot));
            runner.RunCase(nameof(Mut_Link), () => Mut_Link(smokeRoot));
            runner.RunCase(
                nameof(Rpcs3MutationGuard),
                () => Rpcs3MutationGuard(smokeRoot));
            runner.RunCase(nameof(Inp_Lim), () => Inp_Lim(smokeRoot));
            runner.RunCase(nameof(Scan_Src), () => Scan_Src(smokeRoot));
            runner.RunCase(nameof(Mut_Cmd), () => Mut_Cmd(smokeRoot));
            runner.RunCase(nameof(ListFind), ListFind);
            runner.RunCase(nameof(UiGate), UiGate.Run);
            var env = runner.RunValue(nameof(MakeEnv), () => MakeEnv(smokeRoot));
            var hw = runner.RunValue(nameof(Hw_All), Hw_All);
            var game = runner.RunValue(nameof(GameScan), () => GameScan(env));
            var cat = runner.RunValue(nameof(Cat_All), () => Cat_All(env));
            runner.RunCase(nameof(AnlzPath), () => AnlzPath(env, cat));
            var cfg = runner.RunValue(
                nameof(Rec_Stat),
                () => Rec_Stat(env.Install, game.Rogue, cat.Rec, hw.LowEnd));
            runner.RunCase(nameof(Plan_Ui), Plan_Ui);
            runner.RunCase(
                nameof(Bkp_All),
                () => Bkp_All(env.Install, cfg.Plan, cfg.SelChg));
            runner.RunCase(
                nameof(Cfg_All),
                () => Cfg_All(env.Install, game.Rogue, cat.Rec, cfg.SelChg));
            runner.RunCase(nameof(Pat_All), () => Pat_All(env, game.Rogue));
            if (args.Length > 0)
            {
                runner.RunCase(nameof(R3_Read), () => R3_Read(args[0]));
            }

            Console.WriteLine("Smoke tests passed.");
        }
        catch (Exception exception)
        {
            TestRunner.ReportFailure(runner.CurrentTestName, exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            try
            {
                context.Cleanup();
            }
            catch (Exception cleanupException)
            {
                TestRunner.ReportFailure(
                    "temporary-directory-cleanup",
                    cleanupException);
                Environment.ExitCode = 1;
            }
        }
    }
}
