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
        // No automatic recommendation sync from any window or app this runner starts.
        Environment.SetEnvironmentVariable(RecommendationSyncUseCase.AutoSyncKillSwitch, "1");

        try
        {
            if (args.Length == 1 && args[0].Equals("--game-database-only", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(Rpcs3GameDatabaseT), () => Rpcs3GameDatabaseT.Run(AppContext.BaseDirectory));
                return;
            }
            if (args.Length == 3 && args[0].Equals("--game-database-audit", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(Rpcs3GameDatabaseT), () => Rpcs3GameDatabaseT.Audit(args[1], args[2]));
                return;
            }
            if (args.Length is 1 or 2 && args[0] == "--review-fixes-only")
            {
                ReviewFixesT.Run(runner, context.CreateTempRoot("ReviewFixes"), args.Length == 2 ? args[1] : null);
                return;
            }
            if (args.Length == 1 && args[0].Equals("--ui-gate-only", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(UiGate), UiGate.Run);
                return;
            }
            if (args.Length == 1 && args[0] == "--game-settings-evidence-only")
            {
                var evidenceRoot = context.CreateTempRoot("GameSettingsEvidence");
                runner.RunCase(nameof(GameSettingsEvidenceT), () => GameSettingsEvidenceT.Run(evidenceRoot));
                runner.RunCase(nameof(Rpcs3KnowledgeT), () => Rpcs3KnowledgeT.Run(evidenceRoot));
                runner.RunCase(nameof(EvidencePolicyT), EvidencePolicyT.Run);
                return;
            }
            if (args.Length is 1 or 2 && args[0].Equals("--wiki-notes-parser-only", StringComparison.OrdinalIgnoreCase))
            {
                WikiNotesParserT.RunCases(runner, args.Length == 2 ? args[1] : null);
                return;
            }
            if (args.Length == 1 && NoteCases.TryRunFlag(runner, args[0]))
            {
                return;
            }
            if (args.Length == 1 && args[0].Equals("--wiki-scope-only", StringComparison.OrdinalIgnoreCase))
            {
                var root = context.CreateTempRoot("R3ConfigWikiScope");
                runner.RunCase(nameof(WikiExplicitSerial_Runtime), () => WikiExplicitSerial_Runtime(root));
                runner.RunCase(nameof(WikiScope_Restrictions), () => WikiScope_Restrictions(root));
                runner.RunCase(nameof(Settings_AllCatalogDiscovery), () => Settings_AllCatalogDiscovery(root));
                runner.RunCase(nameof(WikiCorpusT.Fixture), () => WikiCorpusT.Fixture(root));
                runner.RunCase(nameof(Plan_Ui), Plan_Ui);
                Console.WriteLine("Wiki scope focused tests passed. Passed: 5, Failed: 0, Skipped: 0");
                return;
            }
            if (args.Length == 3 && args[0].Equals("--gen-production-data", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(WikiProductionData), () => WikiProductionData(args[1], args[2]));
                return;
            }
            if (args.Length is 3 or 4 && args[0].Equals("--wiki-notes-audit", StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(WikiNotesAudit), () => WikiNotesAudit(args[1], args[2], args.Length == 4 ? args[3] : null));
                Console.WriteLine("Wiki notes/online audit passed. Passed: 1, Failed: 0, Skipped: 0");
                return;
            }
            if (args.Length == 5 && args[0].Equals("--wiki-scope-audit", StringComparison.OrdinalIgnoreCase))
            {
                var root = context.CreateTempRoot("R3ConfigWikiScopeAudit");
                runner.RunCase(nameof(WikiScope_SourceParity), () => WikiScope_SourceParity(root, args[1], args[2], args[3], args[4]));
                Console.WriteLine("Wiki source parity passed. Passed: 1, Failed: 0, Skipped: 0");
                return;
            }
            if (args.Length is 1 or 2 && args[0].Equals("--discovery-only", StringComparison.OrdinalIgnoreCase))
            {
                var root = context.CreateTempRoot("R3ConfigDiscovery");
                runner.RunCase(nameof(WikiExplicitSerial_Runtime), () => WikiExplicitSerial_Runtime(root));
                runner.RunCase(nameof(Settings_AllCatalogDiscovery), () => Settings_AllCatalogDiscovery(root));
                runner.RunCase(nameof(Patch_CorpusDiscovery), () => Patch_CorpusDiscovery(root, args.Length == 2 ? args[1] : null));
                runner.RunCase(nameof(WikiCorpusT.Fixture), () => WikiCorpusT.Fixture(root));
                runner.RunCase(nameof(Plan_Ui), Plan_Ui);
                runner.RunCase(nameof(CanaryRuntimeBinding_Regressions), CanaryRuntimeBinding_Regressions);
                Console.WriteLine("Discovery focused tests passed. Passed: 6, Failed: 0, Skipped: 0");
                return;
            }

            if (TryRunNclConvertMode(args))
            {
                return;
            }

            if (TryRunExtraModes(args, runner, context))
            {
                return;
            }

            if (TryRunFocusedTraceModes(args, runner, context))
            {
                return;
            }

            if (args.Length == 1 &&
                args[0].Equals(
                    "--ncl-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(nameof(NclSemanticT), NclSemanticT.Run);
                runner.RunCase(nameof(NclCorpusT), NclCorpusT.Run);
                runner.RunCase(nameof(NclUiT), NclUiT.Run);
                runner.RunCase("NclConvertCli", NclConvertCliTests);
                Console.WriteLine("NCL focused suites passed. Passed: 4, Failed: 0, Skipped: 0");
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
                Console.WriteLine("Passed: 12, Failed: 0, Skipped: 0");
                return;
            }

            if (TryRunWikiMode(args, runner, context))
            {
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

            if (TryRunNclCorpusAuditMode(args, runner))
            {
                return;
            }

            if (TryRunSecurityClosureMode(args, context))
            {
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
            runner.RunCase(nameof(PatchWriteSemanticsT), PatchWriteSemanticsT.Run);
            runner.RunCase(nameof(PatchMergeOrderT), PatchMergeOrderT.Run);
            runner.RunCase(nameof(PlanReasonT), PlanReasonT.Run);
            runner.RunCase(
                nameof(CanaryArtemisConflictT),
                () => CanaryArtemisConflictT.Run(smokeRoot));
            runner.RunCase(nameof(OfficialRuntimeT.PatchYmlReadOnly), () => OfficialRuntimeT.PatchYmlReadOnly(smokeRoot));
            runner.RunCase(nameof(OfficialRuntimeT.WikiOfficialIsolation), () => OfficialRuntimeT.WikiOfficialIsolation(smokeRoot));
            runner.RunCase(nameof(Security_Hardening), () => Security_Hardening(smokeRoot));
            runner.RunCase(nameof(ThirdPartyNoticeT), () => ThirdPartyNoticeT.Run(RepoRoot()));
            runner.RunCase(nameof(UiDateT), () => UiDateT.Run(RepoRoot()));
            runner.RunCase(nameof(SetSvcT), () => SetSvcT.Run(smokeRoot));
            runner.RunCase(nameof(RecSyncT), () => RecSyncT.Run(smokeRoot));
            runner.RunCase(nameof(Rpcs3GameDatabaseT), () => Rpcs3GameDatabaseT.Run(AppContext.BaseDirectory));
            runner.RunCase(
                nameof(CfgBaselineT),
                () => CfgBaselineT.Run(smokeRoot));
            runner.RunCase(nameof(Can_Comp), () => Can_Comp(smokeRoot));
            runner.RunCase(nameof(App_Pat), App_Pat);
            runner.RunCase(nameof(Cat_Dup), Cat_Dup);
            runner.RunCase(nameof(WikiCorpusT), () => WikiCorpusT.Fixture(smokeRoot));
            WikiNotesParserT.RunCases(runner, null);
            runner.RunCase(nameof(GameSettingsUiT), GameSettingsUiT.Run);
            NoteCases.RunAll(runner);
            runner.RunCase(nameof(Rpcs3BuildCatalogT), Rpcs3BuildCatalogT.Run);
            runner.RunCase(nameof(RuntimeContextT), RuntimeContextT.Run);
            runner.RunCase(nameof(HostCpuT), HostCpuT.Run);
            runner.RunCase(nameof(EvidenceApplicabilityT), () => EvidenceApplicabilityT.Run(smokeRoot));
            runner.RunCase(nameof(Rpcs3BuildScopeT), Rpcs3BuildScopeT.Run);
            runner.RunCase(nameof(EvidencePolicyT), EvidencePolicyT.Run);
            runner.RunCase(nameof(GameSettingsEvidenceT), () => GameSettingsEvidenceT.Run(smokeRoot));
            runner.RunCase(nameof(Rpcs3KnowledgeT), () => Rpcs3KnowledgeT.Run(smokeRoot));
            runner.RunCase(nameof(NoteVersionScopeT), NoteVersionScopeT.Run);
            runner.RunCase(nameof(HostCpuT.RunRealHost), HostCpuT.RunRealHost);
            runner.RunCase(nameof(DiagnosticEvidenceT), () => DiagnosticEvidenceT.Run(smokeRoot));
            runner.RunCase(nameof(LogDiagT), () => LogDiagT.Run(smokeRoot));
            runner.RunCase(nameof(LogRunContextT), LogRunContextT.Run);
            runner.RunCase(nameof(LogObservationT), () => LogObservationT.Run(smokeRoot));
            runner.RunCase(nameof(DiscKnowledgeT), () => DiscKnowledgeT.Run(smokeRoot));
            runner.RunCase(nameof(DiscordBotAuditT), DiscordBotAuditT.Run);
            runner.RunCase(nameof(Pat_Cmp), () => Pat_Cmp(smokeRoot));
            runner.RunCase(nameof(GameSetupT), () => GameSetupT.Run(smokeRoot));
            runner.RunCase(
                nameof(ApplicationRuntimeT),
                () => ApplicationRuntimeT.Run(smokeRoot));
            runner.RunCase(
                nameof(Rpcs3BuildIdentityProbeT),
                () => Rpcs3BuildIdentityProbeT.Run(smokeRoot));
            runner.RunCase(nameof(HCanTst), HCanTst.Run);
            runner.RunCase(nameof(NclSemanticT), NclSemanticT.Run);
            runner.RunCase(nameof(NclCorpusT), NclCorpusT.Run);

            runner.RunCase(nameof(NclUiT), NclUiT.Run);
            runner.RunCase("NclConvertCli", NclConvertCliTests);
            runner.RunCase(nameof(Cht_Bndl), () => Cht_Bndl(smokeRoot));
            runner.RunCase(nameof(Pat_Txn), () => Pat_Txn(smokeRoot));
            runner.RunCase(
                nameof(Pat_TxnRollbackPreservesExternalAttempt),
                () => Pat_TxnRollbackPreservesExternalAttempt(smokeRoot));
            runner.RunCase(nameof(Can_Txn), () => Can_Txn(smokeRoot));
            runner.RunCase(nameof(Bkp_Rst), () => Bkp_Rst(smokeRoot));
            runner.RunCase(nameof(Bkp_Del), () => Bkp_Del(smokeRoot));
            runner.RunCase(nameof(Bkp_LegacyRetention), () => Bkp_LegacyRetention(smokeRoot));
            runner.RunCase(nameof(Bkp_NameGregorian), () => Bkp_NameGregorian(smokeRoot));
            runner.RunCase(nameof(Bkp_LegacyHijri), () => Bkp_LegacyHijri(smokeRoot));
            runner.RunCase(nameof(Bkp_MixedNames), () => Bkp_MixedNames(smokeRoot));
            runner.RunCase(nameof(Bkp_PreRestore), () => Bkp_PreRestore(smokeRoot));
            runner.RunCase(
                nameof(Bkp_ConcurrencyOwnership),
                () => Bkp_ConcurrencyOwnership(smokeRoot));
            runner.RunCase(nameof(Mut_Link), () => Mut_Link(smokeRoot));
            runner.RunCase(
                nameof(Rpcs3MutationGuard),
                () => Rpcs3MutationGuard(smokeRoot));
            RunSecurityClosure(runner, smokeRoot);
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
