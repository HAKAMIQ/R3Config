using System;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// G1 (Round 10): RPCS3 applies its database config for a game only when the
// game has no custom config (rpcs3/Emu/System.cpp:1810-1858, commit 13a3cd42).
// Proven in a real RPCS3 v0.0.43-20568-b94078be log for BLUS30442: "Applying
// database config" with no custom config, then "Found custom config. Ignoring
// database config" once config_BLUS30442.yml existed.
internal static class DbConfigLayerT
{
    private const string Serial = "BLUS30442";
    private const char Lrm = (char)0x200E;

    // BLUS30442 in the shipped Data/game-configs.json snapshot:
    //   Video: Vulkan.Asynchronous Texture Streaming: true, Multithreaded RSX: true,
    //          Write Color Buffers: true, Accurate ZCULL stats: false,
    //          Relaxed ZCULL Sync: true
    //   Core:  Sleep Timers Accuracy: As Host
    private static RecSyncApiGame Database() =>
        Rpcs3GameDatabase.Load(Path.Combine(AppContext.BaseDirectory, "Data")).FindConfig(Serial)
        ?? throw new InvalidDataException($"The shipped game-configs.json has no entry for {Serial}.");

    // Routed here so Tst/Program.cs stays a thin CLI router (Arch_Svc bound).
    public static bool TryRunFlag(R3Config.Tests.TestRunner runner, R3Config.Tests.TestContext context, string flag)
    {
        if (!flag.Equals("--db-config-layer-only", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        RunAll(runner, context.CreateTempRoot("DbConfigLayer"));
        return true;
    }

    public static void RunAll(R3Config.Tests.TestRunner runner, string tempRoot)
    {
        runner.RunCase(nameof(Resolve), () => Resolve(tempRoot));
        runner.RunCase(nameof(Warning), () => Warning(tempRoot));
        runner.RunCase(nameof(WarningGroups), () => WarningGroups(tempRoot));
    }

    // Round 12: the lost list splits into (a) settings a recommendation above
    // writes with the same value (select them to keep them) and (b+d) settings
    // R3Config cannot write. "Select database settings" selects (a) only, and
    // only when the user asks; nothing is selected automatically.
    public static void WarningGroups(string tempRoot)
    {
        var plan = FixturePlan(Path.Combine(tempRoot, "Groups"), withKeeper: true);
        var keeper = plan.Items.Single(item => item.Key == "Relaxed ZCULL Sync");
        Assert(keeper.IsAction && keeper.IsDiff && !keeper.IsSel,
            "The fixture keeper must be an actionable recommendation that is not auto-selected.");

        var relaxed = plan.DbCfg.Single(item => item.Key == "Relaxed ZCULL Sync");
        var zcull = plan.DbCfg.Single(item => item.Key == "Accurate ZCULL stats");
        Assert(ReferenceEquals(PlanService.DatabaseKeep(plan, relaxed), keeper) &&
               PlanService.DatabaseKeep(plan, zcull) is null &&
               PlanService.DatabaseKeep(plan, plan.DbCfg.Single(item => item.Key == "Sleep Timers Accuracy")) is null,
            "Only an actionable recommendation with the database value keeps a database setting; another value or no recommendation cannot.");

        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.EnLang);
            var view = new PlanVm();
            view.Load(plan);
            view.Select();
            var row = view.Items.Single(item => item.Key == "Relaxed ZCULL Sync");
            Assert(!row.IsSel, "Automatic selection must never select a database-keeping row.");

            view.ClearSel();
            view.Items.Single(item => item.Key == "Write Color Buffers").IsSel = true;
            Assert(view.HasDbKeep && view.HasDbLost &&
                   view.DbKeepLines.SequenceEqual(["Video/Relaxed ZCULL Sync: true → false"], StringComparer.Ordinal) &&
                   view.DbLostLines.SequenceEqual(
                       [
                           "Core/Sleep Timers Accuracy: As Host → Usleep Only",
                           "Video/Accurate ZCULL stats: false → true",
                           "Video/Vulkan.Asynchronous Texture Streaming: true → false"
                       ],
                       StringComparer.Ordinal) &&
                   view.DbKeepTitle == UiText.GetText("Plan.DbWarnKeep") &&
                   view.DbLostTitle == UiText.GetText("Plan.DbWarnLost") &&
                   !row.IsSel,
                "The warning must split into (a) keepable by selecting the recommendation above and (b+d) lost, without selecting anything.");

            Assert(view.SelDbKeepCmd.CanExecute(null), "Select database settings must be available while (a) has rows.");
            view.SelDbKeepCmd.Execute(null);
            Assert(row.IsSel &&
                   view.Items.Where(item => item.IsSel).Select(item => item.Key).Order(StringComparer.Ordinal)
                       .SequenceEqual(["Relaxed ZCULL Sync", "Write Color Buffers"], StringComparer.Ordinal) &&
                   !view.HasDbKeep && view.DbKeepLines.Count == 0 && view.DbLostLines.Count == 3 &&
                   !view.SelDbKeepCmd.CanExecute(null),
                "Select database settings must select exactly the (a) rows; the (b+d) list stays.");

            UiText.SetLang(UiText.ArabLang);
            view.SetLang();
            Assert(NoteSvc.ContainsArabic(view.DbLostTitle) && view.DbLostLines.All(line => !NoteSvc.ContainsArabic(line)),
                "Group titles follow the language; the lines stay technical LTR text.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }

    public static void Resolve(string tempRoot)
    {
        var env = Env(tempRoot, "Resolve");
        var database = Database();
        Assert(database.Settings.Count == 6,
            "The BLUS30442 fixture must come from the shipped game-configs.json entry with six settings.");

        string? Effective(string section, string key, bool withDatabase = true) =>
            PlanService.ResolveEffective(env.Target, env.Global, withDatabase ? database.Settings : null, section, key);

        Assert(Effective("Video", "Write Color Buffers") == "true",
            "With no custom config, RPCS3's database config must override config.yml (System.cpp:1845-1858).");
        Assert(Effective("Core", "Sleep Timers Accuracy") == "As Host" &&
               Effective("Video", "Vulkan.Asynchronous Texture Streaming") == "true",
            "Database config settings outside config.yml, including nested Vulkan keys, must resolve from the database layer.");
        Assert(Effective("Video", "Read Depth Buffer") == "false",
            "A key absent from the database config must keep resolving through config.yml and the built-in default.");
        Assert(Effective("Video", "Write Color Buffers", withDatabase: false) == "false" &&
               PlanService.ResolveEffective(env.Target, env.Global, "Video", "Write Color Buffers") == "false",
            "Without database data the chain must stay custom -> config.yml -> built-in.");

        File.WriteAllText(env.Target, "Audio:\n  Master Volume: 90\n");
        Assert(Effective("Video", "Write Color Buffers") == "false" &&
               Effective("Core", "Sleep Timers Accuracy") == "Usleep Only",
            "Any readable custom config, even one with an unrelated key, must make RPCS3 ignore the whole database config (System.cpp:1831-1836).");

        var customPlan = new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: database);
        Assert(customPlan.DbCfg.Count == 0 &&
               customPlan.Items.All(item => item.RunVal == item.CurVal),
            "A plan for a game with a custom config must carry no database layer.");
        File.Delete(env.Target);

        var plan = new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: database);
        var wcb = plan.Items.Single(item => item.Key == "Write Color Buffers");
        Assert(wcb.RunVal == "true" && wcb.CurVal == "false",
            "The plan must expose the database value RPCS3 runs with now (RunVal) and keep CurVal as the value that remains after a write.");
        Assert(plan.Items.All(item => item.CurVal == PlanService.ResolveEffective(plan.CfgPath, plan.GlobalCfgPath, item.Section, item.Key)),
            "CurVal must stay equal to what ConfigApplyService re-resolves before writing, or every Apply would be rejected as stale.");
        Assert(wcb.IsDiff && wcb.IsAction,
            "A recommendation equal to the database value stays actionable: the first write drops the database layer, so only writing the key keeps it.");

        Assert(plan.DbCfg.Count == 6 &&
               Base(plan, "Video", "Write Color Buffers") == "false" &&
               Base(plan, "Video", "Multithreaded RSX") == "true" &&
               Base(plan, "Core", "Sleep Timers Accuracy") == "Usleep Only" &&
               Base(plan, "Video", "Accurate ZCULL stats") == "true",
            "The plan must list every active database setting with the config.yml/built-in value that replaces it.");

        var plain = new PlanService().BuildCfg(env.Install, env.Game, Rec());
        Assert(plain.DbCfg.Count == 0 && plain.Items.All(item => item.RunVal == item.CurVal),
            "A plan built without database data must behave exactly as before.");

        // Apply through the unchanged write path: a plan built with the database
        // layer must write exactly what a plan without it writes, and never a
        // database-only key.
        string Applied(CfgPlan built, string backupName)
        {
            var selected = built.Items.Where(item => item.IsSel && item.IsDiff && item.IsAction).Select(item => item.Change).ToArray();
            new ConfigApplyService().Apply(built, selected, Path.Combine(env.Root, backupName));
            var text = File.ReadAllText(env.Target);
            File.Delete(env.Target);
            return text;
        }

        var withDatabase = Applied(new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: database), "bkp-db");
        var withoutDatabase = Applied(new PlanService().BuildCfg(env.Install, env.Game, Rec()), "bkp-plain");
        Assert(withDatabase == withoutDatabase &&
               withDatabase.Contains("Write Color Buffers", StringComparison.Ordinal) &&
               !withDatabase.Contains("Sleep Timers Accuracy", StringComparison.Ordinal) &&
               !withDatabase.Contains("Relaxed ZCULL Sync", StringComparison.Ordinal),
            "Apply must write the same custom config with or without database data and must never copy database-only keys.");

        MustFail<ArgumentException>(
            () => new PlanService().BuildCfg(env.Install, env.Game with { Serial = "BLES00932" }, null, databaseConfig: database),
            "A database entry for another serial must never be applied to this game.");
    }

    public static void Warning(string tempRoot)
    {
        var env = Env(tempRoot, "Warning");
        var database = Database();
        var plan = new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: database);
        var wcb = plan.Items.Single(item => item.Key == "Write Color Buffers").Change;

        var lost = PlanService.DatabaseLoss(plan, [wcb]);
        Assert(lost.Select(item => $"{item.Section}/{item.Key}").Order(StringComparer.Ordinal).SequenceEqual(
                   [
                       "Core/Sleep Timers Accuracy",
                       "Video/Accurate ZCULL stats",
                       "Video/Relaxed ZCULL Sync",
                       "Video/Vulkan.Asynchronous Texture Streaming"
                   ],
                   StringComparer.Ordinal),
            "Creating the custom config must report every database setting whose value changes, excluding keys being written and keys config.yml already sets to the same value.");
        Assert(PlanService.DatabaseLoss(plan, []).Count == 0,
            "With nothing selected nothing is written, so nothing is lost.");

        File.WriteAllText(env.Target, "Audio:\n  Master Volume: 90\n");
        var existing = new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: database);
        Assert(PlanService.DatabaseLoss(existing, [wcb]).Count == 0,
            "When the custom config already exists RPCS3 already ignores the database config: no new loss to warn about.");
        File.Delete(env.Target);

        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.EnLang);
            var view = new PlanVm();
            view.Load(plan);
            view.ClearSel();
            Assert(!view.HasDbWarn,
                "No warning while nothing is selected for writing.");

            view.Items.Single(item => item.Key == "Write Color Buffers").IsSel = true;
            Assert(view.HasDbWarn &&
                   view.DbWarnTitle.Replace(Lrm.ToString(), string.Empty, StringComparison.Ordinal).Contains($"config_{Serial}.yml", StringComparison.Ordinal) &&
                   view.DbWarnTitle.Contains("database config", StringComparison.Ordinal) &&
                   view.DbWarnLines.SequenceEqual(
                       [
                           "Core/Sleep Timers Accuracy: As Host → Usleep Only",
                           "Video/Accurate ZCULL stats: false → true",
                           "Video/Relaxed ZCULL Sync: true → false",
                           "Video/Vulkan.Asynchronous Texture Streaming: true → false"
                       ],
                       StringComparer.Ordinal),
                "Selecting a write for a game with database config must warn in en-US and list each lost key with its current and resulting value.");

            UiText.SetLang(UiText.ArabLang);
            view.SetLang();
            Assert(view.HasDbWarn &&
                   NoteSvc.ContainsArabic(view.DbWarnTitle) &&
                   view.DbWarnTitle.Contains($"{Lrm}config_{Serial}{Lrm}.{Lrm}yml{Lrm}", StringComparison.Ordinal) &&
                   view.DbWarnLines.Count == 4 &&
                   view.DbWarnLines.All(line => !NoteSvc.ContainsArabic(line)),
                "The ar-SA warning must be Arabic, keep the file name LTR with LRM marks, and keep the technical lines untranslated.");

            view.ClearSel();
            Assert(!view.HasDbWarn, "Clearing the selection must remove the warning.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }

    // A BLUS30442 plan with no custom config, for runtime UI renders.
    // withKeeper adds a manual recommendation equal to the database value
    // (Relaxed ZCULL Sync: true) and one with another value (Accurate ZCULL
    // stats: true, database false).
    internal static CfgPlan FixturePlan(string root, bool withKeeper = false)
    {
        var env = Env(root, "Ui");
        var rec = Rec();
        if (withKeeper)
        {
            rec = rec with
            {
                Changes =
                [
                    .. rec.Changes,
                    new CfgChg("Video", "Relaxed ZCULL Sync", "true", "relaxed zcull sync", null, RecRisk.Unknown),
                    new CfgChg("Video", "Accurate ZCULL stats", "true", "accurate zcull stats", null, RecRisk.Unknown)
                ]
            };
        }

        return new PlanService().BuildCfg(env.Install, env.Game, rec, databaseConfig: Database());
    }

    private static string? Base(CfgPlan plan, string section, string key) =>
        plan.DbCfg.Single(item => item.Section == section && item.Key == key).BaseVal;

    private static WikiRec Rec() =>
        new(
            Serial,
            "Midnight Club: Los Angeles",
            [
                new CfgChg("Video", "Write Color Buffers", "true", "write color buffers", null, RecRisk.Rec),
                new CfgChg("Video", "Read Depth Buffer", "true", "read depth buffer", null, RecRisk.Rec)
            ]);

    private static (Rpcs3InstallDescriptor Install, Rpcs3GameDescriptor Game, string Target, string Global, string Root) Env(string tempRoot, string name)
    {
        var root = Path.Combine(tempRoot, "DbConfigLayer", name);
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
        var global = Path.Combine(configPath, "config.yml");
        File.WriteAllText(global, "Video:\n  Write Color Buffers: false\n  Multithreaded RSX: true\n");

        var game = new Rpcs3GameDescriptor(
            Serial,
            "Midnight Club: Los Angeles",
            Path.Combine(root, "game"),
            "01.06",
            null,
            null);

        return (install, game, Path.Combine(customPath, $"config_{Serial}.yml"), global, root);
    }
}
