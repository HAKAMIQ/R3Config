using System;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// The "effective configuration source" line of the game Settings tab. RPCS3
// boots with built-in defaults, then config.yml, then either a custom config
// that loads (rpcs3/Emu/System.cpp:1808-1832, "Found custom config. Ignoring
// database config") or, only when none loads, its database config entry
// (System.cpp:1838-1850). A custom config RPCS3 fails to parse does not stop
// the database layer, so a malformed custom file is Unknown, never Custom.
internal static class CfgPlanSourceT
{
    public static void Run(string tempRoot)
    {
        var env = DbConfigLayerT.Env(tempRoot, "Source");
        var database = DbConfigLayerT.Database();
        CfgPlan Build(RecSyncApiGame? config, bool loaded = true, Rpcs3GameDescriptor? game = null) =>
            Rows(new PlanService().BuildCfg(env.Install, game ?? env.Game, DbConfigLayerT.Rec(), databaseConfig: config, databaseLoaded: loaded));

        var fromDatabase = Build(database);
        Assert(fromDatabase.BootSource == CfgBootSrc.Database && fromDatabase.DbCfg.Count == 6,
            "No custom config and a database entry in R3Config's copy must report Database with its six settings.");

        var global = Build(null);
        Assert(global.BootSource == CfgBootSrc.Global && global.DbCfg.Count == 0,
            "No custom config and no database entry in a loaded database copy must report Global.");

        Assert(Build(null, loaded: false).BootSource == CfgBootSrc.Unknown &&
               Rows(new PlanService().BuildCfg(env.Install, env.Game, DbConfigLayerT.Rec())).BootSource == CfgBootSrc.Unknown,
            "Without a loaded database copy the absence of an entry is not evidence: the source must be Unknown.");

        Assert(Build(new RecSyncApiGame(DbConfigLayerT.Database().Serial, [])).BootSource == CfgBootSrc.Unknown,
            "A database entry without any parsed setting cannot be shown as a layer: the source must be Unknown.");

        File.WriteAllText(env.Target, "Audio:\n  Master Volume: 90\n");
        var custom = Build(database);
        Assert(custom.BootSource == CfgBootSrc.Custom && custom.DbCfg.Count == 0 && Build(null).BootSource == CfgBootSrc.Custom,
            "A readable custom config, even with one unrelated key, must report Custom whether or not a database entry exists.");

        File.WriteAllText(env.Target, "Video: [unclosed\n  - : :\n");
        Assert(Build(database).BootSource == CfgBootSrc.Unknown && Build(null).BootSource == CfgBootSrc.Unknown,
            "A malformed custom config must report Unknown: RPCS3 fails to apply it and may still apply the database config.");
        File.Delete(env.Target);

        Directory.CreateDirectory(env.Target);
        Assert(Build(database).BootSource == CfgBootSrc.Unknown && Build(null).BootSource == CfgBootSrc.Unknown,
            "A directory in place of the custom config file must report Unknown.");
        Directory.Delete(env.Target);

        // RPCS3 first tries "<boot path>.yml" (System.cpp:1813); R3Config does
        // not resolve the boot path, so any such file next to the game is Unknown.
        var gameDir = Path.Combine(env.Root, "game");
        Directory.CreateDirectory(Path.Combine(gameDir, "PS3_GAME", "USRDIR"));
        foreach (var bootConfig in new[] { Path.Combine(gameDir, "PS3_GAME", "USRDIR", "EBOOT.BIN.yml"), gameDir + ".yml" })
        {
            File.WriteAllText(bootConfig, "Audio:\n  Master Volume: 90\n");
            Assert(Build(database).BootSource == CfgBootSrc.Unknown && Build(null).BootSource == CfgBootSrc.Unknown,
                $"A boot-path config ({Path.GetFileName(bootConfig)}) must report Unknown.");
            File.Delete(bootConfig);
        }

        // Game paths follow RuntimePathPolicy.RequireLocal like other game-path
        // consumers: a path that is not local and fully qualified is Unknown.
        foreach (var path in new[] { "game", @"\\?\" + gameDir, string.Empty })
        {
            Assert(Build(database, game: env.Game with { InstPath = path }).BootSource == CfgBootSrc.Unknown,
                $"A non-local game path ('{path}') must report Unknown.");
        }

        DriveRoot(env, database, Build);

        File.WriteAllText(env.Global, "Video: [unclosed\n");
        Assert(Build(database).BootSource == CfgBootSrc.Unknown && Build(null).BootSource == CfgBootSrc.Unknown,
            "An unreadable config.yml leaves the global layer unknown, so the source must be Unknown.");
        File.WriteAllText(env.Global, "Video:\n  Write Color Buffers: false\n  Multithreaded RSX: true\n");

        var noSerial = Rows(new PlanService().BuildCfg(env.Install, env.Game with { Serial = string.Empty }, null, databaseLoaded: true));
        Assert(noSerial.BootSource == CfgBootSrc.Unknown, "A game without a Serial must report Unknown.");

        Lines(fromDatabase, global, custom, Build(null, loaded: false));
    }

    // Row sources come from the same facts as the line: Unknown makes every
    // row Unknown, and only a Database boot source has database rows.
    private static CfgPlan Rows(CfgPlan plan)
    {
        Assert(plan.BootSource != CfgBootSrc.Unknown || plan.Items.All(item => item.CurSrc == CfgValSrc.Unknown),
            "An Unknown settings source must leave every row source Unknown. Rows: " +
            string.Join(", ", plan.Items.Select(item => $"{item.Key}={item.CurSrc}")));
        Assert(plan.BootSource == CfgBootSrc.Database ||
               plan.Items.All(item => item.CurSrc != CfgValSrc.Database && item.RunVal == item.CurVal),
            $"A {plan.BootSource} settings source must not show a database row source or boot value.");
        return plan;
    }

    // A game at a drive root: "<root>.yml" would be the drive-relative "E:.yml"
    // (the current directory of that drive), never a boot-path config.
    private static void DriveRoot(
        (Rpcs3InstallDescriptor Install, Rpcs3GameDescriptor Game, string Target, string Global, string Root) env,
        RecSyncApiGame database,
        Func<RecSyncApiGame?, bool, Rpcs3GameDescriptor?, CfgPlan> build)
    {
        var drive = Path.GetPathRoot(Path.GetFullPath(env.Root))!;
        var probes = new[] { Path.Combine(drive, ".yml"), Path.Combine(drive, "PS3_GAME", "USRDIR", "EBOOT.BIN.yml"), Path.Combine(drive, "USRDIR", "EBOOT.BIN.yml") };
        Assert(probes.All(probe => !File.Exists(probe)), "The drive-root fixture needs a drive without boot-path configs at its root.");

        var current = Path.Combine(env.Root, "drive-relative");
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(current, ".yml"), "Audio:\n  Master Volume: 90\n");
        var original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = current;
            Assert(build(database, true, env.Game with { InstPath = drive }).BootSource == CfgBootSrc.Database,
                $"A game at the drive root ({drive}) must not read the drive-relative '{drive.TrimEnd('\\')}.yml' as a boot-path config.");
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }
    }

    private static void Lines(CfgPlan fromDatabase, CfgPlan global, CfgPlan custom, CfgPlan unknown)
    {
        var originalLanguage = UiText.CurLang;
        try
        {
            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                var arabic = language == UiText.ArabLang;
                var view = new PlanVm();
                Assert(!view.HasBootSrc && view.BootSrcText.Length == 0, $"{language}: no line before a game is selected.");

                view.Load(fromDatabase);
                Assert(view.HasBootSrc &&
                       view.BootSrcText == UiText.Fmt("Plan.BootSrcDatabase", 6) &&
                       NoteSvc.ContainsArabic(view.BootSrcText) == arabic &&
                       view.BootSrcText.Contains("Serial", StringComparison.Ordinal) &&
                       view.HasBootSrcTip &&
                       view.BootSrcTip.Split('\n').SequenceEqual(
                           [
                               "Core/Sleep Timers Accuracy: As Host",
                               "Video/Accurate ZCULL stats: false",
                               "Video/Multithreaded RSX: true",
                               "Video/Relaxed ZCULL Sync: true",
                               "Video/Vulkan.Asynchronous Texture Streaming: true",
                               "Video/Write Color Buffers: true"
                           ],
                           StringComparer.Ordinal),
                    $"{language}: Database must name the setting count and the per-Serial scope, and the tip must list every database key verbatim.");
                Assert(arabic || view.BootSrcText.Contains("R3Config's copy of the RPCS3 database", StringComparison.Ordinal),
                    "en-US: Database must be worded as R3Config's copy of the RPCS3 database.");

                view.Load(custom);
                Assert(view.BootSrcText == UiText.GetText("Plan.BootSrcCustom") && !view.HasBootSrcTip && view.BootSrcTip.Length == 0,
                    $"{language}: Custom must say RPCS3 ignores its database config, with no key tip.");
                Assert(arabic || view.BootSrcText.Contains("ignores its database config", StringComparison.Ordinal),
                    "en-US: Custom must say RPCS3 ignores its database config for this game.");

                view.Load(global);
                Assert(view.BootSrcText == UiText.GetText("Plan.BootSrcGlobal") && !view.HasBootSrcTip,
                    $"{language}: Global must render its own line.");

                view.Load(unknown);
                Assert(view.BootSrcText == UiText.GetText("Plan.BootSrcUnknown") && !view.HasBootSrcTip,
                    $"{language}: Unknown must render its own line.");

                view.Load(fromDatabase);
                UiText.SetLang(arabic ? UiText.EnLang : UiText.ArabLang);
                view.SetLang();
                Assert(NoteSvc.ContainsArabic(view.BootSrcText) != arabic,
                    $"{language}: the line must follow a language switch.");

                view.Clear();
                Assert(!view.HasBootSrc && view.BootSrcText.Length == 0, $"{language}: clearing the plan must hide the line.");
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }
}
