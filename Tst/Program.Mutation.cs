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
    static void Rpcs3MutationGuard(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "mutation-guard", "rpcs3");
        var config = Path.Combine(root, "config");
        var patches = Path.Combine(root, "patches");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(patches);

        var install = new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            config,
            Path.Combine(config, "custom_configs"),
            patches,
            Path.Combine(config, "patch_config.yml"));
        File.WriteAllBytes(install.ExePath, []);
        var safeGuard = new Rpcs3MutationGuard(() => false);

        safeGuard.CheckCan(
            install,
            [
                Path.Combine(config, "custom_configs", "config_TEST00001.yml"),
                Path.Combine(patches, "imported_patch.yml")
            ]);

        MutFails(
            () => safeGuard.CheckCan(
                install,
                [Path.Combine(tempRoot, "outside.yml")]),
            MutBlock.Tgt,
            "Expected targets outside RPCS3 config/patches to be rejected.");
        MutFails(
            () => safeGuard.CheckCan(
                install,
                [install.PatCfg, install.PatCfg]),
            MutBlock.Tgt,
            "Expected duplicate transaction targets to be rejected before writing.");
        MutFails(
            () => safeGuard.CheckCan(
                install,
                [Path.Combine(patches, "patch.yml")]),
            MutBlock.Tgt,
            "Expected RPCS3's official patch.yml catalog to be rejected by every mutation boundary.");
        MutFails(
            () => new Rpcs3MutationGuard(() => true).CheckCan(
                install,
                [install.PatCfg]),
            MutBlock.Running,
            "Expected every mutation boundary to block while RPCS3 is running.");

        var blockedImportReader = new R3Config.Svc.ReadSvc();
        var blockedImportBackup = new BackupService();
        var blockedImport = new R3Config.Application.CanaryImportUseCase(
            new R3Config.Svc.CanaryPatchImportService(blockedImportReader),
            new Rpcs3MutationGuard(() => true),
            new PatchFileTransactionService(
                blockedImportBackup,
                blockedImportReader,
                new CfgRead()),
            blockedImportReader);
        MutFails(
            () => blockedImport.Import(
                install,
                "Version: 1.2\r\n"),
            MutBlock.Running,
            "Expected the Canary import use case to enforce the central guard.");
        Assert(
            !File.Exists(Path.Combine(patches, "imported_patch.yml")),
            "A blocked use case must not create its target file.");

        var externalDataRoot = Path.Combine(
            tempRoot,
            "mutation-guard-external-data");
        var externalConfig = Path.Combine(externalDataRoot, "config");
        var externalPatches = Path.Combine(externalDataRoot, "patches");
        Directory.CreateDirectory(externalConfig);
        Directory.CreateDirectory(externalPatches);
        var externalInstall = install with
        {
            CfgPath = externalConfig,
            CustPath = Path.Combine(
                externalConfig,
                "custom_configs"),
            PatPath = externalPatches,
            PatCfg = Path.Combine(
                externalConfig,
                "patch_config.yml"),
            DataRoot = externalDataRoot
        };
        safeGuard.CheckCan(
            externalInstall,
            [
                externalInstall.PatCfg,
                Path.Combine(externalPatches, "imported_patch.yml")
            ]);
        MutFails(
            () => safeGuard.CheckCan(
                externalInstall with
                {
                    PatPath = Path.Combine(tempRoot, "spoofed-patches")
                },
                [Path.Combine(tempRoot, "spoofed-patches", "patch.yml")]),
            MutBlock.Inst,
            "Expected a descriptor path outside its declared RPCS3 data-root layout to be rejected.");

        Console.WriteLine("Central RPCS3 mutation guard smoke PASS.");
    }

    static void Mut_Cmd(string tempRoot)
    {
        var suiteRoot = Path.Combine(
            tempRoot,
            "mutation-command-state");
        var installRoot = Path.Combine(suiteRoot, "rpcs3");
        var configPath = Path.Combine(installRoot, "config");
        var patchesPath = Path.Combine(installRoot, "patches");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(patchesPath);
        File.WriteAllBytes(Path.Combine(installRoot, "rpcs3.exe"), []);
        var install = Rpcs3InstallService.FromExe(
            Path.Combine(installRoot, "rpcs3.exe"));

        var running = false;
        var runtime = AppComposition.BuildRun(
            new Rpcs3MutationGuard(() => running));
        var main = new MainVm(
            runtime,
            () => running,
            (_, _) => true);
        var change = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "write color buffers",
            null,
            R3Config.Mdl.RecRisk.Rec);
        var state = new RecItem(
            "BLUS43000",
            change.Path,
            "false",
            "true",
            R3Config.Mdl.RecRisk.Rec,
            R3Config.Mdl.RecState.Pending,
            true,
            true,
            true,
            string.Empty);
        var plan = new CfgPlan(
            "BLUS43000",
            "Mutation Command Game",
            Path.Combine(
                install.CustPath,
                "config_BLUS43000.yml"),
            [new CfgEntry(change, "false", state)],
            new RecSum(
                "BLUS43000",
                1,
                0,
                1,
                0,
                0,
                0,
                [state]),
            true);
        main.Plan.Load(plan);
        SetField(main, "_curPlan", plan);
        SetField(main, "_prepCan", true);
        main.Advanced.LoadBkp(
        [
            new BackupSnapshot(
                Path.Combine(configPath, "R3Bkp", "snapshot"),
                DateTimeOffset.UtcNow,
                [new BackupFileEntry(plan.CfgPath, "backup.yml", true)])
        ]);

        Assert(!main.ApplyCmd.CanRun(null) &&
               !main.DelSet.CanRun(null) &&
               !main.RstCmd.CanRun(null) &&
               !main.CanDel.CanRun(null) &&
               !main.CanImp.CanRun(null),
            "Expected every Main mutation command to be disabled without an analyzed install.");

        SetField(main, "_install", install);
        main.LoadR3Ui();
        Assert(!main.ApplyCmd.CanRun(null),
            "Expected config apply to remain disabled without an explicit selection.");

        main.Plan.Items.Single().IsSel = true;
        Assert(main.ApplyCmd.CanRun(null) &&
               main.DelSet.CanRun(null) &&
               main.RstCmd.CanRun(null) &&
               main.CanDel.CanRun(null) &&
               main.CanImp.CanRun(null),
            "Expected valid Main mutation operations to be enabled while RPCS3 is closed.");

        SetField(main, "_isBusy", true);
        Assert(!main.ApplyCmd.CanRun(null) &&
               !main.DelSet.CanRun(null) &&
               !main.RstCmd.CanRun(null) &&
               !main.CanDel.CanRun(null) &&
               !main.CanImp.CanRun(null),
            "Expected every Main mutation command to be disabled while analysis is busy.");

        SetField(main, "_isBusy", false);
        running = true;
        main.LoadR3Ui();
        Assert(main.RunUi &&
               !main.ApplyCmd.CanRun(null) &&
               !main.DelSet.CanRun(null) &&
               !main.RstCmd.CanRun(null) &&
               !main.CanDel.CanRun(null) &&
               !main.CanImp.CanRun(null),
            "Expected refreshed running state to disable every Main mutation command.");

        running = false;
        main.LoadR3Ui();
        main.Plan.ClearSel();
        Assert(!main.ApplyCmd.CanRun(null) &&
               main.DelSet.CanRun(null),
            "Expected only the operation lacking selection to remain disabled.");

        var importRoot = Path.Combine(suiteRoot, "import-confirmation");
        var importConfig = Path.Combine(importRoot, "config");
        var importPatches = Path.Combine(importRoot, "patches");
        Directory.CreateDirectory(importConfig);
        Directory.CreateDirectory(importPatches);
        File.WriteAllBytes(Path.Combine(importRoot, "rpcs3.exe"), []);
        var importInstall = Rpcs3InstallService.FromExe(
            Path.Combine(importRoot, "rpcs3.exe"));
        var importFlow = runtime.Flow;
        var allowConfirmation = false;
        var confirmationCalls = 0;
        var importViewModel = new CanImpVm(
            importInstall,
            importFlow,
            () => false,
            (_, _) =>
            {
                confirmationCalls++;
                return allowConfirmation;
            })
        {
            ImpText = CanPat(
                new string('f', 40),
                "Confirmed Canary Import",
                "0x00003000")
        };
        var importedPath = Path.Combine(
            importPatches,
            R3Config.Svc.CanaryPatchImportService.ImpName);

        Assert(importViewModel.MakeCmd.CanRun(null),
            "Expected a validated import preview to enable its confirmed mutation command.");
        importViewModel.MakeCmd.Execute(null);
        Assert(confirmationCalls == 1 &&
               !File.Exists(importedPath),
            "Expected rejected Canary import confirmation to produce zero writes.");

        allowConfirmation = true;
        importViewModel.MakeCmd.Execute(null);
        Assert(confirmationCalls == 2 &&
               File.Exists(importedPath),
            "Expected accepted Canary import confirmation to reach the guarded use case.");

        var hCanaryRoot = Path.Combine(suiteRoot, "hcanary-confirmation");
        Directory.CreateDirectory(Path.Combine(hCanaryRoot, "config"));
        Directory.CreateDirectory(Path.Combine(hCanaryRoot, "patches"));
        File.WriteAllBytes(Path.Combine(hCanaryRoot, "rpcs3.exe"), []);
        var hCanaryInstall = Rpcs3InstallService.FromExe(
            Path.Combine(hCanaryRoot, "rpcs3.exe"));
        var hCanaryGame = new Rpcs3GameDescriptor(
            "BLUS43001",
            "Confirmed H-Canary Game",
            "game",
            "01.00",
            new string('a', 40),
            null,
            [new string('a', 40)],
            []);
        var hCanaryConfirm = false;
        var hCanary = new HCanVm(
            runtime.Flow,
            () => running,
            (_, _) => hCanaryConfirm,
            hashOps: EmptyHashOps());
        hCanary.SetCtx(hCanaryInstall, hCanaryGame);
        hCanary.ExeHash = "PPU-" + new string('a', 40);
        hCanary.PatName = "Confirmed H-Canary Patch";
        hCanary.Author = "R3Config Tests";
        hCanary.Ins[0].Address = "0x00004000";
        hCanary.Ins[0].Value = "0x60000000";
        Assert(hCanary.IsValid && hCanary.SaveCmd.CanRun(null),
            "Expected a valid H-Canary draft to enable Save while RPCS3 is closed.");
        hCanary.SaveCmd.Execute(null);
        var hCanaryImportedPath = Path.Combine(
            hCanaryInstall.PatPath,
            "imported_patch.yml");
        Assert(!File.Exists(hCanaryImportedPath),
            "Expected rejected H-Canary save confirmation to produce zero writes.");

        hCanaryConfirm = true;
        hCanary.SaveCmd.Execute(null);
        Assert(File.Exists(hCanaryImportedPath) &&
               hCanary.DelCmd.CanRun(null),
            "Expected accepted H-Canary save confirmation to reach the guarded use case.");
        var savedBytes = File.ReadAllBytes(hCanaryImportedPath);
        hCanaryConfirm = false;
        hCanary.DelCmd.Execute(null);
        Assert(File.ReadAllBytes(hCanaryImportedPath)
                .SequenceEqual(savedBytes),
            "Expected rejected H-Canary delete confirmation to preserve the stored definition byte-for-byte.");

        running = true;
        hCanary.LoadMut();
        Assert(!hCanary.SaveCmd.CanRun(null) &&
               !hCanary.SaveAs.CanRun(null) &&
               !hCanary.DelCmd.CanRun(null),
            "Expected refreshed RPCS3 running state to disable H-Canary mutation commands.");
        running = false;
        hCanary.SetAvail(
            isBusy: true,
            isRpcs3Running: false);
        Assert(!hCanary.SaveCmd.CanRun(null) &&
               !hCanary.SaveAs.CanRun(null) &&
               !hCanary.DelCmd.CanRun(null),
            "Expected busy state to disable H-Canary mutation commands.");

        Console.WriteLine(
            "Mutation command state and confirmation contract tests PASS.");
    }

    static void SetField<T>(
        object target,
        string fieldName,
        T value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Expected private test field was not found: {fieldName}");
        field.SetValue(target, value);
    }

    static void Mut_Link(string tempRoot)
    {
        var suiteRoot = Path.Combine(
            tempRoot,
            "mutation-guard-reparse");
        var root = Path.Combine(suiteRoot, "rpcs3");
        var config = Path.Combine(root, "config");
        var patches = Path.Combine(root, "patches");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(patches);

        var install = new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            config,
            Path.Combine(config, "custom_configs"),
            patches,
            Path.Combine(config, "patch_config.yml"),
            root);
        File.WriteAllBytes(install.ExePath, []);

        var guard = new Rpcs3MutationGuard(() => false);
        guard.CheckCan(
            install,
            [Path.Combine(config, "custom_configs", "config_TEST00001.yml")]);

        var configOnlyRoot = Path.Combine(
            suiteRoot,
            "config-only");
        Directory.CreateDirectory(Path.Combine(configOnlyRoot, "config"));
        File.WriteAllBytes(
            Path.Combine(configOnlyRoot, "rpcs3.exe"),
            []);
        var configOnly = Rpcs3InstallService.FromExe(
            Path.Combine(configOnlyRoot, "rpcs3.exe"));
        guard.CheckCan(
            configOnly,
            [Path.Combine(
                configOnly.CustPath,
                "config_TEST00002.yml")]);
        var configOnlyTarget = Path.Combine(
            configOnly.CustPath,
            "config_TEST00002.yml");
        var configOnlyChange = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "Write Color Buffers",
            null,
            R3Config.Mdl.RecRisk.Required);
        var configOnlyState = new RecItem(
            "TEST00002",
            configOnlyChange.Path,
            null,
            configOnlyChange.Value,
            configOnlyChange.Risk,
            RecState.Pending,
            IsDiff: true,
            IsAction: true,
            IsSel: true,
            Notes: string.Empty);
        var configOnlyPlan = new CfgPlan(
            "TEST00002",
            "Config Only Game",
            configOnlyTarget,
            [new CfgEntry(configOnlyChange, null, configOnlyState)],
            new RecSum(
                "TEST00002",
                1,
                0,
                1,
                0,
                0,
                0,
                [configOnlyState]),
            NeedBkp: true);
        new ConfigUseCase(
                new PlanService(),
                new ConfigApplyService(),
                guard)
            .Apply(
                configOnlyPlan,
                [configOnlyChange],
                configOnly);
        Assert(File.Exists(configOnlyTarget),
            "Expected a config mutation to succeed when patches does not exist.");
        Assert(!Directory.Exists(configOnly.PatPath),
            "Expected config mutation validation not to require or create patches.");

        var patchOnlyRoot = Path.Combine(
            suiteRoot,
            "patch-only");
        Directory.CreateDirectory(Path.Combine(patchOnlyRoot, "patches"));
        File.WriteAllBytes(
            Path.Combine(patchOnlyRoot, "rpcs3.exe"),
            []);
        var patchOnly = Rpcs3InstallService.FromExe(
            Path.Combine(patchOnlyRoot, "rpcs3.exe"));
        guard.CheckCan(
            patchOnly,
            [Path.Combine(
                patchOnly.PatPath,
                "imported_patch.yml")]);
        var patchOnlyTarget = Path.Combine(
            patchOnly.PatPath,
            "imported_patch.yml");
        var patchOnlyPrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            patchOnlyTarget,
            TxnCat(
                new string('f', 40),
                "Patch Only Test",
                "Patch Only Game",
                "BLUS30003",
                "01.00",
                "0x00002030"),
            PrepKind.PatCat,
            PatSrc.Imported,
            EntNum: 1));
        new PatchFileTransactionService().Commit(
            Rpcs3AppPaths.BkpRoot(patchOnly),
            [patchOnlyPrepared],
            (_, backupRoot, targets) =>
                guard.CheckCan(
                    patchOnly,
                    targets,
                    [backupRoot]));
        Assert(File.Exists(patchOnlyTarget),
            "Expected a patch mutation to succeed without a pre-existing config directory.");

        var externalDataRoot = Path.Combine(suiteRoot, "external-data-root");
        var externalConfig = Path.Combine(externalDataRoot, "config");
        var externalPatches = Path.Combine(externalDataRoot, "patches");
        Directory.CreateDirectory(externalConfig);
        Directory.CreateDirectory(externalPatches);
        var externalInstall = install with
        {
            CfgPath = externalConfig,
            CustPath = Path.Combine(
                externalConfig,
                "custom_configs"),
            PatPath = externalPatches,
            PatCfg = Path.Combine(
                externalConfig,
                "patch_config.yml"),
            DataRoot = externalDataRoot
        };
        guard.CheckCan(
            externalInstall,
            [Path.Combine(externalPatches, "imported_patch.yml")]);

        MutFails(
            () => guard.CheckCan(
                install with
                {
                    ExePath = Path.Combine(root, "missing", "rpcs3.exe")
                },
                [install.PatCfg]),
            MutBlock.Inst,
            "Expected a missing rpcs3.exe to invalidate the installation.");

        var directoryExeRoot = Path.Combine(suiteRoot, "directory-exe");
        var directoryExeConfig = Path.Combine(directoryExeRoot, "config");
        var directoryExePatches = Path.Combine(directoryExeRoot, "patches");
        Directory.CreateDirectory(directoryExeConfig);
        Directory.CreateDirectory(directoryExePatches);
        Directory.CreateDirectory(Path.Combine(directoryExeRoot, "rpcs3.exe"));
        var directoryExeInstall = new Rpcs3InstallDescriptor(
            directoryExeRoot,
            Path.Combine(directoryExeRoot, "rpcs3.exe"),
            directoryExeConfig,
            Path.Combine(directoryExeConfig, "custom_configs"),
            directoryExePatches,
            Path.Combine(directoryExeConfig, "patch_config.yml"),
            directoryExeRoot);
        MutFails(
            () => guard.CheckCan(
                directoryExeInstall,
                [directoryExeInstall.PatCfg]),
            MutBlock.Inst,
            "Expected a directory named rpcs3.exe to be rejected.");

        var spoofedPatches = Path.Combine(suiteRoot, "spoofed-patches");
        Directory.CreateDirectory(spoofedPatches);
        MutFails(
            () => guard.CheckCan(
                install with
                {
                    PatPath = spoofedPatches
                },
                [Path.Combine(spoofedPatches, "imported_patch.yml")]),
            MutBlock.Inst,
            "Expected a patches path that does not match DataRoot to be rejected.");
        MutFails(
            () => guard.CheckCan(
                install,
                [Path.Combine(suiteRoot, "outside.yml")]),
            MutBlock.Tgt,
            "Expected a canonical target outside config and patches to be rejected.");
        MutFails(
            () => guard.CheckCan(
                install,
                [install.PatCfg, install.PatCfg]),
            MutBlock.Tgt,
            "Expected duplicate canonical mutation targets to be rejected.");

        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine(
                "Mutation guard junction cases SKIP (Windows-only); canonical non-junction cases PASS.");
            return;
        }

        var externalEscapeRoot = Path.Combine(
            suiteRoot,
            "junction-escape-targets");
        var configEscapeTarget = Path.Combine(
            externalEscapeRoot,
            "config");
        var patchesEscapeTarget = Path.Combine(
            externalEscapeRoot,
            "patches");
        Directory.CreateDirectory(configEscapeTarget);
        Directory.CreateDirectory(patchesEscapeTarget);

        var configJunction = Path.Combine(config, "escape");
        MakeLink(configJunction, configEscapeTarget);
        MutFails(
            () => guard.CheckCan(
                install,
                [Path.Combine(configJunction, "escaped.yml")]),
            MutBlock.Tgt,
            "Expected a junction inside config that resolves outside DataRoot to be rejected.");

        var patchesJunction = Path.Combine(patches, "escape");
        MakeLink(patchesJunction, patchesEscapeTarget);
        MutFails(
            () => guard.CheckCan(
                install,
                [Path.Combine(patchesJunction, "escaped.yml")]),
            MutBlock.Tgt,
            "Expected a junction inside patches that resolves outside DataRoot to be rejected.");

        var stageEscapeTarget = Path.Combine(
            externalEscapeRoot,
            "before-stage");
        Directory.CreateDirectory(stageEscapeTarget);
        var stageParent = Path.Combine(patches, "before-stage");
        MakeLink(stageParent, stageEscapeTarget);
        var stageTarget = Path.Combine(stageParent, "stage_patch.yml");
        var stagePrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            stageTarget,
            TxnCat(
                new string('d', 40),
                "Stage Guard Patch",
                "Stage Guard Game",
                "BLUS30001",
                "01.00",
                "0x00002010"),
            PrepKind.PatCat,
            PatSrc.Imported,
            EntNum: 1));
        MutFails(
            () => new PatchFileTransactionService().Commit(
                Path.Combine(config, "R3Bkp"),
                [stagePrepared],
                (_, backupRoot, targets) =>
                    guard.CheckCan(
                        install,
                        targets,
                        [backupRoot])),
            MutBlock.Tgt,
            "Expected a junction present before staging to be rejected before any external stage file is created.");
        Assert(!Directory.EnumerateFileSystemEntries(
                stageEscapeTarget)
                .Any(),
            "Expected before-stage rejection to leave the external junction target untouched.");
        Directory.Delete(stageParent);

        var backupPhaseParent = Path.Combine(
            patches,
            "backup-phase");
        Directory.CreateDirectory(backupPhaseParent);
        var backupPhaseTarget = Path.Combine(
            backupPhaseParent,
            "backup_patch.yml");
        File.WriteAllText(
            backupPhaseTarget,
            TxnCat(
                new string('c', 40),
                "Original Backup Guard Patch",
                "Backup Guard Game",
                "BLUS30002",
                "01.00",
                "0x00002020"));
        var backupPrepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            backupPhaseTarget,
            TxnCat(
                new string('c', 40),
                "Updated Backup Guard Patch",
                "Backup Guard Game",
                "BLUS30002",
                "01.00",
                "0x00002024"),
            PrepKind.PatCat,
            PatSrc.Imported,
            EntNum: 1));
        var unsafeBackupRoot = Path.Combine(
            config,
            "UnsafeBkp");
        var externalBackupRoot = Path.Combine(
            externalEscapeRoot,
            "backup-root");
        Directory.CreateDirectory(externalBackupRoot);
        var backupBefore = File.ReadAllBytes(backupPhaseTarget);
        MutFails(
            () => new PatchFileTransactionService().Commit(
                unsafeBackupRoot,
                [backupPrepared],
                (phase, backupRoot, targets) =>
                {
                    if (phase == PatPhase.Bkp &&
                        !Directory.Exists(unsafeBackupRoot))
                    {
                        MakeLink(
                            unsafeBackupRoot,
                            externalBackupRoot);
                    }

                    guard.CheckCan(
                        install,
                        targets,
                        [backupRoot]);
                }),
            MutBlock.Tgt,
            "Expected a backup root changed to a junction before backup to be rejected before external reads or writes.");
        Assert(File.ReadAllBytes(backupPhaseTarget).SequenceEqual(
                   backupBefore) &&
               !Directory.EnumerateFileSystemEntries(
                   externalBackupRoot).Any(),
            "Expected before-backup rejection to preserve the target and leave the external backup directory untouched.");
        Directory.Delete(unsafeBackupRoot);

        var swapParent = Path.Combine(patches, "swap");
        var swapEscapeTarget = Path.Combine(
            externalEscapeRoot,
            "swap");
        Directory.CreateDirectory(swapParent);
        Directory.CreateDirectory(swapEscapeTarget);
        var swapTarget = Path.Combine(swapParent, "prepared_patch.yml");
        var prepared =
            PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
            swapTarget,
            TxnCat(
                new string('e', 40),
                "TOCTOU Patch",
                "TOCTOU Game",
                "BLUS30000",
                "01.00",
                "0x00002000"),
            PrepKind.PatCat,
            PatSrc.Imported,
            EntNum: 1));
        var transaction = new PatchFileTransactionService();

        MutFails(
            () => transaction.Commit(
                Path.Combine(config, "R3Bkp"),
                [prepared],
                (phase, backupRoot, targets) =>
                {
                    if (phase != PatPhase.Commit)
                    {
                        guard.CheckCan(
                            install,
                            targets,
                            [backupRoot]);
                        return;
                    }

                    var stagePath = Directory.EnumerateFiles(
                            swapParent,
                            "*.r3stage",
                            SearchOption.TopDirectoryOnly)
                        .Single();
                    File.Delete(stagePath);
                    Directory.Delete(swapParent);
                    MakeLink(
                        swapParent,
                        swapEscapeTarget);
                    guard.CheckCan(
                        install,
                        targets,
                        [backupRoot]);
                }),
            MutBlock.Tgt,
            "Expected the transaction's final guard to reject a parent swapped to a junction after staging.");
        Assert(!File.Exists(Path.Combine(
                   swapEscapeTarget,
                   Path.GetFileName(swapTarget))),
            "A rejected TOCTOU transaction must not write through the replacement junction.");

        Directory.Delete(configJunction);
        Directory.Delete(patchesJunction);
        Directory.Delete(swapParent);
        Assert(!Directory.EnumerateFiles(
                patches,
                "*.r3stage",
                SearchOption.AllDirectories).Any(),
            "A rejected TOCTOU transaction must clean every patch transaction stage.");

        Console.WriteLine(
            "Canonical RPCS3 mutation guard and Windows junction smoke PASS.");
    }

    static void MakeLink(
        string junctionPath,
        string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Directory junctions are only used by the Windows mutation-guard tests.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.SystemDirectory,
                "cmd.exe"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(junctionPath);
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start cmd.exe to create a test junction.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 ||
            !Directory.Exists(junctionPath))
        {
            throw new InvalidOperationException(
                "Creating a required Windows test junction failed. " +
                $"Exit code: {process.ExitCode}. Output: {standardOutput} Error: {standardError}");
        }
    }

    static void MutFails(
        Action action,
        MutBlock reason,
        string message)
    {
        try
        {
            action();
        }
        catch (Rpcs3MutationBlockedException ex)
        {
            Assert(ex.Reason == reason, message + $" Actual reason: {ex.Reason}.");
            return;
        }

        throw new InvalidOperationException(message);
    }

}
