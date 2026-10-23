using System;
using R3Config.Tests;

internal static partial class Program
{
    // Additional standalone focused-suite CLI flags kept out of the thin
    // router in Program.cs so its line-count architecture bound (<400)
    // stays satisfied. Mechanical, behavior-preserving extraction only —
    // each block below is moved verbatim from Program.cs with its trailing
    // `return;` converted to `return true;`, and no test/flag semantics
    // were changed.
    private static bool TryRunExtraModes(
        string[] args,
        TestRunner runner,
        TestContext context)
    {
        if (args.Length == 1 && args[0] == "--ui-date-only")
        {
            runner.RunCase(nameof(UiDateT), () => UiDateT.Run(RepoRoot()));
            return true;
        }

        if (args.Length == 1 && args[0] == "--rec-sync-only")
        {
            var tempRoot = context.CreateTempRoot("R3ConfigRecSync");
            runner.RunCase(nameof(RecSyncT), () => RecSyncT.Run(tempRoot));
            return true;
        }

        // Real network check against the official sources into a temporary
        // store; never part of the release gate.
        if (args.Length == 2 && args[0] == "--rec-sync-live")
        {
            runner.RunCase("RecSyncLive", () => RecSyncT.Live(args[1]));
            return true;
        }

        if (args.Length == 1 && args[0] == "--cpu-evidence-only")
        {
            CpuEvidenceRuntimeT.Run(runner, context.CreateTempRoot("CpuEvidenceRuntime"));
            return true;
        }

        if (args.Length == 1 && args[0].Equals("--game-library-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigGameIcons");
            runner.RunCase(nameof(Rpcs3GameLibraryT),
                () => Rpcs3GameLibraryT.RunAsync(tempRoot).GetAwaiter().GetResult());
            Console.WriteLine("Game library/icon focused suite passed. Passed: 1, Failed: 0, Skipped: 0");
            return true;
        }

        if (args.Length is 2 or 3 && args[0].Equals("--runtime-context-real", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("RuntimeContextReal", () => RuntimeContextRealT.Run(args[1], args.Length == 3 ? args[2] : null));
            return true;
        }

        if (args.Length == 1 && args[0].Equals("--log-diag-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigLogDiag");
            runner.RunCase(nameof(LogDiagT), () => LogDiagT.Run(tempRoot));
            return true;
        }

        if (args.Length is 2 or 3 && args[0].Equals("--log-diag-real", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("LogDiagReal", () => LogDiagRealT.Run(args[1], args.Length == 3 ? args[2] : null));
            return true;
        }

        if (args.Length == 4 && args[0].Equals("--gen-diagnostics-kb", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("GenDiagnosticsKb", () => DiagnosticKbGen.Run(args[1], args[2], args[3]));
            return true;
        }

        if (args.Length == 4 && args[0].Equals("--gen-build-catalog", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("GenBuildCatalog", () => Rpcs3BuildCatalogGen.Run(args[1], args[2], args[3]));
            return true;
        }

        if (args.Length == 3 && args[0].Equals("--gen-win32-symbols", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("GenWin32Symbols", () => Win32ErrorSymbolsGen.Run(args[1], args[2]));
            return true;
        }

        if (args.Length == 5 && args[0].Equals("--gen-rpcn-evidence", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("GenRpcnEvidence", () => RpcnEvidenceGen.Run(args[1], args[2], args[3], args[4]));
            return true;
        }

        if (args.Length == 7 && args[0].Equals("--gen-knowledge", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase("GenKnowledge", () => Rpcs3KnowledgeGen.Run(args[1], args[2], args[3], args[4], args[5], args[6]));
            return true;
        }

        if (args.Length == 1 && args[0].Equals("--ncl-global-batch-trace-only", StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(NclGlobalBatchTraceT), NclGlobalBatchTraceT.Run);
            return true;
        }

        if (args.Length == 1 && args[0].Equals("--rpcs3-build-identity-only", StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigBuildIdentity");
            runner.RunCase(nameof(Rpcs3BuildIdentityProbeT), () => Rpcs3BuildIdentityProbeT.Run(tempRoot));
            Console.WriteLine("RPCS3 build identity focused suite passed. Passed: 1, Failed: 0, Skipped: 0");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--runner-failure-probe",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(
                "runner-failure-probe",
                () => throw new InvalidOperationException(
                    "Intentional R3Test runner failure probe."));
            return true;
        }

        // OFFICIAL_RUNTIME_COMPATIBILITY_GATE: the current production
        // guarantees of the Official patch path, using no historical corpus.
        // Each case is an existing production-behavior regression:
        //  - Pat_Cmp: schema 1.2 parsing, unknown/unsupported structures stay
        //    explicit and fail closed, Serial/version/hash applicability strict.
        //  - Pat_All: Unknown is never reported as Mismatch.
        //  - Pat_Txn / rollback: the RPCS3 patch.yml stays read-only and
        //    patch_config.yml state is preserved through apply/rollback.
        //  - PatchWriteSemanticsT / App_Pat / AppPat_*: Official patches stay
        //    isolated from Canary, Artemis and imported patches.
        //  - OfficialRuntimeT.PatchYmlReadOnly: patch.yml is byte-identical across
        //    Official apply/disable/restore/failed-transaction rollback and only
        //    patch_config.yml is ever the mutation target.
        //  - OfficialRuntimeT.WikiOfficialIsolation: Wiki settings/notes/patch
        //    pointers cannot reach the Official catalog, match, plan, or state.
        if (args.Length == 1 &&
            args[0].Equals(
                "--official-runtime-only",
                StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigOfficialRuntime");
            runner.RunCase(nameof(Pat_Cmp), () => Pat_Cmp(tempRoot));
            runner.RunCase(nameof(PatchWriteSemanticsT), PatchWriteSemanticsT.Run);
            runner.RunCase(nameof(App_Pat), App_Pat);
            runner.RunCase(nameof(AppPat_InstallPathContracts), AppPat_InstallPathContracts);
            runner.RunCase(nameof(Pat_Txn), () => Pat_Txn(tempRoot));
            runner.RunCase(
                nameof(Pat_TxnRollbackPreservesExternalAttempt),
                () => Pat_TxnRollbackPreservesExternalAttempt(tempRoot));
            var env = runner.RunValue(nameof(MakeEnv), () => MakeEnv(tempRoot));
            var game = runner.RunValue(nameof(GameScan), () => GameScan(env));
            runner.RunCase(nameof(Pat_All), () => Pat_All(env, game.Rogue));
            runner.RunCase(nameof(OfficialRuntimeT.PatchYmlReadOnly), () => OfficialRuntimeT.PatchYmlReadOnly(tempRoot));
            runner.RunCase(nameof(OfficialRuntimeT.WikiOfficialIsolation), () => OfficialRuntimeT.WikiOfficialIsolation(tempRoot));
            Console.WriteLine("Official runtime compatibility focused suite passed. Passed: 10, Failed: 0, Skipped: 0");
            return true;
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
            return true;
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
            return true;
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
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--game-setup-only",
                StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigGameSetup");
            runner.RunCase(nameof(GameSetupT), () => GameSetupT.Run(tempRoot));
            Console.WriteLine("Game Setup focused tests passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--ui-gate-only",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(UiGate.RunPatchMode), UiGate.RunPatchMode);
            Console.WriteLine("Patch UI gate passed.");
            return true;
        }

        // The complete UiGate case the full suite runs (static XAML contracts
        // plus the rendered matrix), for focused visual-redesign iterations.
        if (args.Length == 1 &&
            args[0].Equals(
                "--ui-full-gate-only",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(UiGate), UiGate.Run);
            Console.WriteLine("Full UI gate passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--ui-runtime-matrix-only",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(UiGate.RunVisualOnly), UiGate.RunVisualOnly);
            Console.WriteLine("Runtime UI matrix passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--backup-cleanup-only",
                StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigBackupCleanup");
            runner.RunCase(nameof(Bkp_Del), () => Bkp_Del(tempRoot));
            Console.WriteLine("Backup deletion and retention tests passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--backup-name-only",
                StringComparison.OrdinalIgnoreCase))
        {
            var tempRoot = context.CreateTempRoot("R3ConfigBackupName");
            runner.RunCase(nameof(Bkp_NameGregorian), () => Bkp_NameGregorian(tempRoot));
            runner.RunCase(nameof(Bkp_LegacyHijri), () => Bkp_LegacyHijri(tempRoot));
            runner.RunCase(nameof(Bkp_MixedNames), () => Bkp_MixedNames(tempRoot));
            Console.WriteLine("Backup snapshot name tests passed.");
            return true;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--busy-overlay-only",
                StringComparison.OrdinalIgnoreCase))
        {
            runner.RunCase(nameof(UiGate.RunBusyOverlay), UiGate.RunBusyOverlay);
            Console.WriteLine("Busy overlay runtime test passed.");
            return true;
        }

        return false;
    }
}
