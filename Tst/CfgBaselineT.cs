using System;
using System.IO;
using System.Linq;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

internal static class CfgBaselineT
{
    public static void Run(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "CfgBaseline");
        var configPath = Path.Combine(root, "config");
        var customPath = Path.Combine(configPath, "custom_configs");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(customPath);
        Directory.CreateDirectory(patchPath);

        var install = new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            configPath,
            customPath,
            patchPath,
            Path.Combine(configPath, "patch_config.yml"));

        var game = new Rpcs3GameDescriptor(
            "BCES00509",
            "Uncharted 2: Among Thieves",
            Path.Combine(root, "game"),
            "01.09",
            null,
            null);

        var recommendation = new WikiRec(
            "BCES00509",
            "Uncharted 2: Among Thieves",
            [
                new CfgChg(
                    "Video",
                    "Write Color Buffers",
                    "true",
                    "write color buffers",
                    null,
                    RecRisk.Rec),
                new CfgChg(
                    "Video",
                    "Read Depth Buffer",
                    "true",
                    "read depth buffers",
                    null,
                    RecRisk.Rec)
            ]);

        var service = new PlanService();

        var unknownPlan = service.BuildCfg(install, game, recommendation);
        Assert(unknownPlan.Items.All(item => item.State != RecState.Applied),
            "Recommendations that differ from RPCS3 built-in defaults must remain deviations when config files are absent.");

        var builtInRecommendation = new WikiRec(
            game.Serial,
            game.Title,
            [
                new CfgChg(
                    "Video",
                    "Write Color Buffers",
                    "false",
                    "write color buffers",
                    null,
                    RecRisk.Rec)
            ]);
        var builtInPlan = service.BuildCfg(
            install,
            game,
            builtInRecommendation);
        var builtInSatisfied = builtInPlan.Items.Single();
        Assert(builtInSatisfied.State == RecState.Applied &&
               builtInSatisfied.CurVal == "false" &&
               !builtInSatisfied.IsDiff &&
               !builtInSatisfied.IsAction,
            "A recommendation equal to the RPCS3 built-in default must not create a game custom override.");

        File.WriteAllText(
            Path.Combine(configPath, "config.yml"),
            "Video:\n  Write Color Buffers: true\n  Read Depth Buffer: false\n");

        var globalPlan = service.BuildCfg(install, game, recommendation);
        var satisfied = globalPlan.Items.Single(item => item.Key == "Write Color Buffers");
        var deviating = globalPlan.Items.Single(item => item.Key == "Read Depth Buffer");

        var globalOverridePlan = service.BuildCfg(
            install,
            game,
            builtInRecommendation);
        var globalOverride = globalOverridePlan.Items.Single();
        Assert(globalOverride.CurVal == "true" &&
               globalOverride.IsDiff &&
               globalOverride.IsAction,
            "A global RPCS3 override must remain the effective value even when the recommendation equals the built-in default.");

        Assert(satisfied.State == RecState.Applied &&
               !satisfied.IsDiff &&
               !satisfied.IsAction,
            "A recommendation already satisfied by the global RPCS3 configuration must not become a custom-config write.");
        Assert(deviating.IsDiff && deviating.IsAction,
            "A recommendation that deviates from the effective RPCS3 value must stay actionable.");

        File.WriteAllText(
            Path.Combine(customPath, "config_BCES00509.yml"),
            "Video:\n  Write Color Buffers: false\n");

        var customPlan = service.BuildCfg(install, game, recommendation);
        var overridden = customPlan.Items.Single(item => item.Key == "Write Color Buffers");

        Assert(overridden.IsAction &&
               overridden.CurVal == "false",
            "The per-game custom configuration must win over the global RPCS3 baseline.");

        File.Delete(Path.Combine(customPath, "config_BCES00509.yml"));
        File.WriteAllText(
            Path.Combine(configPath, "config.yml"),
            "Video: [invalid");
        var unreadableGlobal = service.BuildCfg(install, game, recommendation);
        Assert(unreadableGlobal.Items.All(item =>
                   item.State == RecState.Unknown &&
                   !item.IsAction),
            "An unreadable global RPCS3 config must fail closed instead of falling through to guessed defaults.");

        ApplyGuard(tempRoot);

        Console.WriteLine("Config baseline deviation gate PASS.");
    }

    // Field regression: Grand Theft Auto IV (BLES00229) has no game custom
    // config, so every recommended key is inherited from the global RPCS3
    // config. Apply used to re-read the custom config alone, find nothing, and
    // reject the plan as "config changed after preview" even though no file had
    // changed. The guard must compare the effective value it showed the user.
    private static void ApplyGuard(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "CfgApplyGuard");
        var configPath = Path.Combine(root, "config");
        var customPath = Path.Combine(configPath, "custom_configs");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(customPath);
        Directory.CreateDirectory(patchPath);

        var install = new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            configPath,
            customPath,
            patchPath,
            Path.Combine(configPath, "patch_config.yml"));

        var globalConfigPath = Path.Combine(configPath, "config.yml");
        const string globalConfigText =
            "Core:\n  Sleep Timers Accuracy: Usleep Only\nVideo:\n  Write Color Buffers: Disabled\n";
        File.WriteAllText(globalConfigPath, globalConfigText);

        var game = new Rpcs3GameDescriptor(
            "BLES00229",
            "Grand Theft Auto IV",
            Path.Combine(root, "game"),
            "01.00",
            null,
            null);

        var sleepTimers = new CfgChg(
            "Core",
            "Sleep Timers Accuracy",
            "As Host",
            "sleep timers accuracy",
            null,
            RecRisk.Unknown);

        var recommendation = new WikiRec(
            game.Serial,
            game.Title,
            [
                sleepTimers,
                new CfgChg(
                    "Video",
                    "Write Color Buffers",
                    "false",
                    "write color buffers",
                    null,
                    RecRisk.Rec)
            ]);

        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        var planner = new PlanService();
        var backups = new BackupService();

        var absentPlan = planner.BuildCfg(install, game, recommendation);
        Assert(absentPlan.GlobalCfgPath == globalConfigPath,
            "A config plan must record the global RPCS3 baseline it resolved inherited values against.");
        Assert(!File.Exists(absentPlan.CfgPath),
            "Building a config plan must not create a game custom config.");

        var inherited = absentPlan.Items.Single(
            item => item.Key == "Sleep Timers Accuracy");
        Assert(inherited.CurVal == "Usleep Only" &&
               inherited.IsDiff &&
               inherited.IsAction,
            "A key inherited from the global RPCS3 config must stay actionable with the inherited value as its reviewed current state.");

        // Semantically equal serialized values must resolve as already applied
        // at the canonical-model layer instead of becoming a pending write.
        var equivalent = absentPlan.Items.Single(
            item => item.Key == "Write Color Buffers");
        Assert(equivalent.State == RecState.Applied &&
               !equivalent.IsDiff &&
               !equivalent.IsAction,
            "A global value that is semantically equal to the recommendation must not become a config change.");

        var applied = new ConfigApplyService().Apply(
            absentPlan,
            [inherited.Change],
            backupRoot);
        Assert(applied.ChgList.Count == 1,
            "Applying a key the game custom config inherits must not be rejected as an external change.");
        Assert(R3Yaml.CanRead(absentPlan.CfgPath) &&
               R3Yaml.ReadVal(absentPlan.CfgPath, "Core", "Sleep Timers Accuracy") == "As Host",
            "Apply must write the canonical RPCS3 value and the written custom config must read back.");
        Assert(backups.ListBkp(backupRoot).Count == 1,
            "Applying a config change must record one backup.");
        Assert(File.ReadAllText(globalConfigPath) == globalConfigText,
            "Applying a game config change must never write the global RPCS3 config.");

        // A custom config that exists but does not override the key is the same
        // inherited case, and must behave identically.
        const string userOwnedText =
            "Video:\r\n  Anisotropic Filter Override: 16\r\n";
        File.WriteAllText(absentPlan.CfgPath, userOwnedText);

        var inheritedPlan = planner.BuildCfg(install, game, recommendation);
        var stillInherited = inheritedPlan.Items.Single(
            item => item.Key == "Sleep Timers Accuracy");
        Assert(stillInherited.CurVal == "Usleep Only" && stillInherited.IsAction,
            "An existing custom config that does not override the key must still report the inherited value.");

        _ = new ConfigApplyService().Apply(
            inheritedPlan,
            [stillInherited.Change],
            backupRoot);
        Assert(R3Yaml.ReadVal(inheritedPlan.CfgPath, "Core", "Sleep Timers Accuracy") == "As Host",
            "Apply must merge the reviewed key into an existing custom config.");
        Assert(R3Yaml.ReadVal(inheritedPlan.CfgPath, "Video", "Anisotropic Filter Override") == "16",
            "Apply must preserve unrelated user-owned settings in the game custom config.");

        // A real external edit of the custom config must still abort.
        File.WriteAllText(
            inheritedPlan.CfgPath,
            "Core:\r\n  Sleep Timers Accuracy: Usleep Only\r\n");
        var externalPlan = planner.BuildCfg(install, game, recommendation);
        var externalItem = externalPlan.Items.Single(
            item => item.Key == "Sleep Timers Accuracy");
        const string externalEdit =
            "Core:\r\n  Sleep Timers Accuracy: All Timers\r\n";
        File.WriteAllText(externalPlan.CfgPath, externalEdit);
        var backupsBeforeAbort = backups.ListBkp(backupRoot).Count;
        RejectsStalePlan(
            () => new ConfigApplyService().Apply(
                externalPlan,
                [externalItem.Change],
                backupRoot),
            "An external edit of the game custom config must still abort Apply.");
        Assert(File.ReadAllText(externalPlan.CfgPath) == externalEdit &&
               backups.ListBkp(backupRoot).Count == backupsBeforeAbort,
            "A stale config plan must preserve the external edit and create no backup.");

        // An external edit of the global baseline changes the effective value
        // the user reviewed for an inherited key, so it must abort as well.
        File.WriteAllText(externalPlan.CfgPath, userOwnedText);
        var baselinePlan = planner.BuildCfg(install, game, recommendation);
        var baselineItem = baselinePlan.Items.Single(
            item => item.Key == "Sleep Timers Accuracy");
        File.WriteAllText(
            globalConfigPath,
            "Core:\n  Sleep Timers Accuracy: All Timers\nVideo:\n  Write Color Buffers: Disabled\n");
        RejectsStalePlan(
            () => new ConfigApplyService().Apply(
                baselinePlan,
                [baselineItem.Change],
                backupRoot),
            "An external edit of the global RPCS3 baseline must abort Apply for an inherited key.");
        File.WriteAllText(globalConfigPath, globalConfigText);

        // Target ownership: a plan may only be applied to the installation and
        // serial it was built for, baseline included.
        var otherGame = game with
        {
            Serial = "BLES01128",
            Title = "Another Game"
        };
        var otherPlan = planner.BuildCfg(install, otherGame, recommendation);
        Assert(otherPlan.CfgPath != baselinePlan.CfgPath,
            "Each serial must resolve its own game custom config target.");
        Assert(!File.Exists(otherPlan.CfgPath),
            "Applying one serial's plan must not create another serial's custom config.");

        var useCase = new ConfigUseCase(
            planner,
            new ConfigApplyService(),
            new Rpcs3MutationGuard(() => false));

        MustFail<Rpcs3MutationBlockedException>(
            () => useCase.Apply(
                baselinePlan with { CfgPath = otherPlan.CfgPath },
                [baselineItem.Change],
                install),
            "A config plan whose target belongs to another serial must be blocked.");

        MustFail<Rpcs3MutationBlockedException>(
            () => useCase.Apply(
                baselinePlan with
                {
                    GlobalCfgPath = Path.Combine(root, "config.yml")
                },
                [baselineItem.Change],
                install),
            "A config plan whose global baseline is outside this installation must be blocked.");
    }

    private static void RejectsStalePlan(
        Action apply,
        string message)
    {
        try
        {
            apply();
        }
        catch (IOException exception) when (
            exception.Message.Contains(
                "changed after preview",
                StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
