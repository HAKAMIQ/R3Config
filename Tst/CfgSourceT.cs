using System;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// The Settings row ToolTip shows the row's current value (CfgEntry.CurVal)
// with the layer it comes from, the value RPCS3 boots with from R3Config's
// copy of the RPCS3 database when that differs, and the RPCS3 default from
// R3Config's verified built-in table. Row sources come from the same facts as
// CfgPlan.BootSource: an Unknown boot source makes every row source Unknown.
// Nothing here is guessed: a key outside the table has no default (null) and
// an unreadable config leaves the source Unknown.
internal static class CfgSourceT
{
    private const string Serial = "BLUS30442";

    public static void Run(string tempRoot)
    {
        var env = Env(Path.Combine(tempRoot, "CfgSource"));
        var database = new RecSyncApiGame(
            Serial,
            [new RecSyncSetting("Video", "Multithreaded RSX", "true"), new RecSyncSetting("Video", "Read Depth Buffer", "false")]);
        File.WriteAllText(env.Global, "Video:\n  Write Color Buffers: true\n");

        CfgPlan Plan(bool withDatabase = true, bool loaded = true) =>
            new PlanService().BuildCfg(env.Install, env.Game, Rec(), databaseConfig: withDatabase ? database : null, databaseLoaded: loaded);
        CfgEntry Row(string key, bool withDatabase = true, bool loaded = true) =>
            Plan(withDatabase, loaded).Items.Single(item => item.Key == key);

        Assert(Plan(withDatabase: false).BootSource == CfgBootSrc.Global, "The fixture without a database entry must boot from Global.");
        var global = Row("Write Color Buffers", withDatabase: false);
        Assert(global.CurSrc == CfgValSrc.Global && global.DefVal == "false" && global.RunVal == global.CurVal,
            "A key set only in config.yml must read Global, with the built-in default from R3Config's table.");
        var builtIn = Row("Read Depth Buffer", withDatabase: false);
        Assert(builtIn.CurSrc == CfgValSrc.BuiltIn && builtIn.DefVal == "false" && builtIn.CurVal == "false",
            "A key set in no config file must read BuiltIn and carry the table default.");

        Assert(Plan().BootSource == CfgBootSrc.Database, "The fixture with a database entry must boot from Database.");
        var differs = Row("Multithreaded RSX");
        Assert(differs.CurVal == "false" && differs.CurSrc == CfgValSrc.BuiltIn && differs.RunVal == "true",
            "When the database copy boots another value, the row's current value keeps its own source and RunVal carries the boot value.");
        var same = Row("Read Depth Buffer");
        Assert(same.CurVal == "false" && same.RunVal == "false" && same.CurSrc == CfgValSrc.Database,
            "When the database copy boots the row's current value itself, that value reads Database.");
        Assert(Row("Write Color Buffers").CurSrc == CfgValSrc.Global,
            "A key outside the database entry keeps its config.yml source while the database layer applies.");

        // databaseLoaded false: the absence of an entry is no evidence, so the
        // boot source is Unknown and so is every row (never Global/BuiltIn).
        var notLoaded = Plan(withDatabase: false, loaded: false);
        Assert(notLoaded.BootSource == CfgBootSrc.Unknown &&
               notLoaded.Items.All(item => item.CurSrc == CfgValSrc.Unknown && item.RunVal == item.CurVal),
            "Without a loaded database copy every row source must be Unknown, like the Settings source line.");

        var unknownKey = Row("Unmapped Source Test Key");
        Assert(unknownKey.DefVal is null && unknownKey.CurSrc == CfgValSrc.Unknown && unknownKey.State == RecState.Unknown,
            "A key outside the built-in table must have no default and an Unknown source, never a guess.");
        Assert(!PlanService.TryReadBuiltIn("Video", "Unmapped Source Test Key", out var missing) && missing is null &&
               PlanService.TryReadBuiltIn("Video", "Shader Mode", out var shader) &&
               shader == "Async Recompiler with Shader Interpreter",
            "TryReadBuiltIn must return the table value and must not throw for a missing key.");

        Tooltip(differs, same);

        File.WriteAllText(env.Target, "Video:\n  Multithreaded RSX: false\n");
        var custom = Row("Multithreaded RSX");
        Assert(custom.CurSrc == CfgValSrc.Custom && custom.RunVal == "false",
            "A key the custom config sets must read Custom, and the database layer no longer applies.");
        Assert(Row("Write Color Buffers").CurSrc == CfgValSrc.Global && Row("Read Depth Buffer").CurSrc == CfgValSrc.BuiltIn,
            "A key the custom config inherits must keep its config.yml or built-in source.");

        File.WriteAllText(env.Target, "Video: [\n  broken");
        var malformed = Plan();
        Assert(malformed.BootSource == CfgBootSrc.Unknown &&
               malformed.Items.All(item => item.CurSrc == CfgValSrc.Unknown && item.RunVal == item.CurVal) &&
               malformed.Items.All(item => item.Key == "Unmapped Source Test Key" || item.DefVal == "false"),
            "A malformed custom config must leave every source Unknown; the table default stays known.");
        File.Delete(env.Target);

        // Invalid UTF-8 in config.yml: the display-only reads must not throw.
        File.WriteAllBytes(env.Global, [.. "Video:\n  Write Color Buffers: "u8.ToArray(), 0xC3, 0x28, (byte)'\n']);
        foreach (var (withDatabase, loaded) in new[] { (true, true), (false, true), (false, false) })
        {
            var invalid = Plan(withDatabase, loaded);
            Assert(invalid.BootSource == CfgBootSrc.Unknown && invalid.Items.All(item => item.CurSrc == CfgValSrc.Unknown),
                $"An invalid UTF-8 config.yml must give a plan with Unknown sources instead of throwing (database {withDatabase}, loaded {loaded}).");
        }

        File.WriteAllText(env.Global, "Video:\n  Write Color Buffers: true\n");
    }

    // The row ToolTip keeps "Current" equal to the row's current value and its
    // own source line; the boot value from R3Config's database copy is a
    // separate line, only when it differs.
    private static void Tooltip(CfgEntry differs, CfgEntry same)
    {
        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.EnLang);
            var row = new PlanItVm(differs, isSelected: false);
            Assert(row.CurDsp == "false" && row.TransDsp == "false -> true" &&
                   row.SrcDsp == UiText.GetText("Plan.CurSrc.BuiltIn") &&
                   row.HasDbBoot && row.DbBootDsp == "true",
                "The ToolTip Current must equal the row's current value with its own source, plus the database boot value.");
            var equal = new PlanItVm(same, isSelected: false);
            Assert(!equal.HasDbBoot && equal.SrcDsp == UiText.GetText("Plan.CurSrc.Database"),
                "No extra boot line when the database copy boots the current value itself.");
            foreach (var key in new[] { "Plan.CurSrc.Database", "Plan.DbBoot" })
            {
                Assert(UiText.GetText(key).Contains("R3Config's copy of the RPCS3 database", StringComparison.Ordinal),
                    $"en-US {key} must carry the same \"R3Config's copy\" qualifier as the Settings source line.");
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }

    private static WikiRec Rec() =>
        new(
            Serial,
            "Midnight Club: Los Angeles",
            [
                new CfgChg("Video", "Write Color Buffers", "true", "write color buffers", null, RecRisk.Rec),
                new CfgChg("Video", "Read Depth Buffer", "true", "read depth buffer", null, RecRisk.Rec),
                new CfgChg("Video", "Multithreaded RSX", "true", "multithreaded rsx", null, RecRisk.Rec),
                new CfgChg("Video", "Unmapped Source Test Key", "true", "unmapped", null, RecRisk.Rec)
            ]);

    private static (Rpcs3InstallDescriptor Install, Rpcs3GameDescriptor Game, string Target, string Global) Env(string root)
    {
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
        var game = new Rpcs3GameDescriptor(Serial, "Midnight Club: Los Angeles", Path.Combine(root, "game"), "01.06", null, null);

        return (install, game, Path.Combine(customPath, $"config_{Serial}.yml"), Path.Combine(configPath, "config.yml"));
    }
}
