using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed class PlanService
{
    private static readonly Dictionary<string, string> BuiltInDefaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Core/PPU Decoder"] = "Recompiler (LLVM)",
            ["Core/PPU Threads"] = "2",
            ["Core/SPU Decoder"] = "Recompiler (LLVM)",
            ["Core/Preferred SPU Threads"] = "0",
            ["Core/Thread Scheduler Mode"] = "Operating System",
            ["Core/SPU loop detection"] = "false",
            ["Core/SPU XFloat Accuracy"] = "Approximate",
            ["Core/SPU Block Size"] = "Safe",
            ["Core/Accurate SPU DMA"] = "false",
            ["Core/Accurate RSX reservation access"] = "false",
            ["Core/Disable SPU GETLLAR Spin Optimization"] = "false",
            ["Core/RSX FIFO Fetch Accuracy"] = "Atomic",
            ["Core/Accurate PPU 128-byte Reservation Op Max Length"] = "0",
            ["Core/Max SPURS Threads"] = "6",
            ["Core/Sleep Timers Accuracy"] = "Usleep Only",
            ["Core/Clocks scale"] = "100",
            ["Core/Use Accurate DFMA"] = "true",
            ["Core/PPU Accurate Non-Java Mode"] = "false",
            ["Core/PPU Accurate Vector NaN Values"] = "false",
            ["Core/Debug Console Mode"] = "false",
            ["Core/Hook static functions"] = "false",
            ["Core/Libraries Control"] = "",

            ["Video/Renderer"] = "Vulkan",
            ["Video/Aspect ratio"] = "16:9",
            ["Video/Frame limit"] = "Auto",
            ["Video/Anisotropic Filter Override"] = "0",
            ["Video/MSAA"] = "Auto",
            ["Video/Shader Precision"] = "High",
            ["Video/3D Display Mode"] = "Disabled",
            ["Video/Resolution"] = "1280x720",
            ["Video/Resolution Scale"] = "100",
            ["Video/Minimum Scalable Dimension"] = "16",
            ["Video/Output Scaling Mode"] = "Bilinear",
            ["Video/Shader Mode"] = "Async Recompiler with Shader Interpreter",
            ["Video/Write Color Buffers"] = "false",
            ["Video/Read Color Buffers"] = "false",
            ["Video/Write Depth Buffer"] = "false",
            ["Video/Read Depth Buffer"] = "false",
            ["Video/Handle RSX Memory Tiling"] = "false",
            ["Video/Strict Rendering Mode"] = "false",
            ["Video/VSync Mode"] = "Disabled",
            ["Video/Stretch To Display Area"] = "false",
            ["Video/Multithreaded RSX"] = "false",
            ["Video/Vulkan.Asynchronous Texture Streaming"] = "false",
            ["Video/Disable On-Disk Shader Cache"] = "false",
            ["Video/Disable Vertex Cache"] = "false",
            ["Video/Driver Wake-Up Delay"] = "0",
            ["Video/Vblank Rate"] = "60",
            ["Video/Vblank NTSC Fixup"] = "true",
            ["Video/Force CPU Blit"] = "false",
            ["Video/Use GPU texture scaling"] = "false",
            ["Video/Disable ZCull Occlusion Queries"] = "false",
            ["Video/Texture LOD Bias Addend"] = "0",
            ["Video/Allow Host GPU Labels"] = "false",
            ["Video/Force Hardware MSAA Resolve"] = "false",
            ["Video/Emulate Special Depth Comparison"] = "false",
            ["Video/Disable Asynchronous Memory Manager"] = "false",
            ["Video/Relaxed ZCULL Sync"] = "false",
            ["Video/Accurate ZCULL stats"] = "true",

            ["Audio/Renderer"] = "Cubeb",
            ["Audio/Audio Format"] = "Stereo",
            ["Audio/Dump to file"] = "false",
            ["Audio/Convert to 16 bit"] = "false",
            ["Audio/Master Volume"] = "100",
            ["Audio/Enable Buffering"] = "true",
            ["Audio/Desired Audio Buffer Duration"] = "34",
            ["Audio/Enable Time Stretching"] = "false",
            ["Audio/Time Stretching Threshold"] = "75",
            ["Audio/Microphone Type"] = "Null",

            ["Input/Output/Keyboard"] = "Null",
            ["Input/Output/Mouse"] = "Basic",
            ["Input/Output/Camera type"] = "Unknown",
            ["Input/Output/Camera"] = "Null",
            ["Input/Output/Move"] = "Null",

            ["Net/Internet enabled"] = "Disconnected",
            ["Net/PSN status"] = "Disconnected",
            ["Miscellaneous/Silence All Logs"] = "false"
        };

    private readonly StateSvc _recSvc = new();

    public CfgPlan BuildCfg(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        HwInfo? hw = null,
        Rpcs3RuntimeBuild? build = null,
        string hostPlatform = "Windows",
        RecSyncApiGame? databaseConfig = null)
    {
        // hw is never used as a tier: the state of a documented change never depends on
        // Low/Mid/High. Only proven vendor/host/build facts feed the change's own scope.
        var facts = RuntimeFacts.Create(game, hostPlatform, build, hw);
        if (databaseConfig is not null &&
            !databaseConfig.Serial.Equals(game.Serial, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The RPCS3 database config entry belongs to another serial.",
                nameof(databaseConfig));
        }

        var database = databaseConfig?.Settings;
        var targetPath = Path.Combine(
            install.CustPath,
            $"config_{game.Serial}.yml");

        var globalPath = Path.Combine(
            install.CfgPath,
            "config.yml");

        var changes = (recommendation?.Changes ?? [])
            .OrderBy(change => change.Section)
            .ThenBy(change => change.Key)
            .ToArray();

        WikiRec[] recs = recommendation is null
            ? []
            : [recommendation with { Changes = changes }];

        // State and CurVal use the chain without the database layer on purpose:
        // the first write creates the custom config, after which RPCS3 ignores
        // the database config, and ConfigApplyService re-resolves CurVal through
        // this same chain before writing. RunVal carries what RPCS3 runs now.
        var state = _recSvc.Evaluate(
            game,
            recs,
            (section, key) => ResolveEffective(
                targetPath,
                globalPath,
                section,
                key),
            facts);

        var items = changes
            .Zip(
                state.Items,
                (change, stateItem) =>
                    new CfgEntry(
                        change,
                        stateItem.Current,
                        stateItem)
                    {
                        HasCustomOverride = R3Yaml.HasKey(targetPath, change.Section, change.Key),
                        RunVal = RunValue(targetPath, globalPath, database, change, stateItem.Current)
                    })
            .ToArray();

        return new CfgPlan(
            GameSer: game.Serial,
            GameName: recommendation?.WikiName ?? game.Title,
            CfgPath: targetPath,
            GlobalCfgPath: globalPath,
            Items: items,
            RecState: state,
            NeedBkp: items.Any(
                item => item.IsAction && item.IsDiff))
        {
            DbCfg = ActiveDatabase(targetPath, globalPath, database)
        };
    }

    // The chain without RPCS3's database config: the game custom config when it
    // overrides the key, otherwise the global RPCS3 config, otherwise the
    // verified RPCS3 built-in default. This is the value that holds once a
    // custom config exists, and the one Apply re-resolves before writing.
    // Reading only the custom config reports every key the custom config
    // inherits rather than overrides as an external change, because an
    // inherited key is physically absent there.
    internal static string? ResolveEffective(
        string targetPath,
        string globalPath,
        string section,
        string key) =>
        ResolveEffective(targetPath, globalPath, null, section, key);

    // The value a booting RPCS3 uses for this key, in the order of
    // rpcs3/Emu/System.cpp:1810-1858: built-in default, then config.yml, then
    // either the game custom config (when the file exists and loads) or, only
    // when there is none, the database config entry for the game. A custom
    // config with a single unrelated key still disables the whole database
    // layer ("Found custom config. Ignoring database config").
    internal static string? ResolveEffective(
        string targetPath,
        string globalPath,
        IReadOnlyList<RecSyncSetting>? database,
        string section,
        string key)
    {
        if (!R3Yaml.CanRead(targetPath))
        {
            throw new InvalidDataException(
                "Config file is not readable.");
        }

        var current = R3Yaml.ReadVal(
            targetPath,
            section,
            key);

        if (current is not null)
        {
            return current;
        }

        if (database is not null &&
            !File.Exists(targetPath) &&
            FindSetting(database, section, key) is { } fromDatabase)
        {
            return fromDatabase.Value;
        }

        if (!R3Yaml.CanRead(globalPath))
        {
            throw new InvalidDataException(
                "Global RPCS3 config file is not readable.");
        }

        var global = R3Yaml.ReadVal(
            globalPath,
            section,
            key);

        return global ?? ReadBuiltIn(section, key);
    }

    // The database config settings that stop applying when the selected changes
    // create the game custom config: every active database setting the write
    // does not set itself and whose fallback value differs (or is unknown).
    // Nothing is lost when nothing is selected or a custom config exists.
    public static IReadOnlyList<CfgDbSetting> DatabaseLoss(
        CfgPlan plan,
        IEnumerable<CfgChg> selected)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selected);

        var written = selected
            .Select(change => (change.Section, change.Key))
            .ToHashSet();
        if (written.Count == 0)
        {
            return [];
        }

        return
        [
            .. plan.DbCfg
                .Where(setting =>
                    !written.Contains((setting.Section, setting.Key)) &&
                    !string.Equals(setting.BaseVal, setting.Value, StringComparison.Ordinal))
                .OrderBy(setting => setting.Section, StringComparer.Ordinal)
                .ThenBy(setting => setting.Key, StringComparer.Ordinal)
        ];
    }

    // Round 12 (read only): the recommendation row that keeps a lost database
    // setting when the user selects it, i.e. an actionable, not-yet-applied
    // change of the same key whose value is exactly the database value. Null
    // means R3Config cannot write that value for the game (another value, no
    // recommendation, or a key outside the map). Selection stays the user's.
    public static CfgEntry? DatabaseKeep(CfgPlan plan, CfgDbSetting setting)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(setting);

        return plan.Items.FirstOrDefault(item =>
            item.IsAction &&
            item.IsDiff &&
            item.Section.Equals(setting.Section, StringComparison.Ordinal) &&
            item.Key.Equals(setting.Key, StringComparison.Ordinal) &&
            string.Equals(item.Val, setting.Value, StringComparison.Ordinal));
    }

    private static string? RunValue(
        string targetPath,
        string globalPath,
        IReadOnlyList<RecSyncSetting>? database,
        CfgChg change,
        string? current)
    {
        if (database is null)
        {
            return current;
        }

        try
        {
            return ResolveEffective(targetPath, globalPath, database, change.Section, change.Key);
        }
        catch (InvalidDataException)
        {
            // Unreadable configs already leave the state Unknown.
            return current;
        }
    }

    private static IReadOnlyList<CfgDbSetting> ActiveDatabase(
        string targetPath,
        string globalPath,
        IReadOnlyList<RecSyncSetting>? database)
    {
        if (database is null || database.Count == 0 || File.Exists(targetPath))
        {
            return [];
        }

        var globalReadable = R3Yaml.CanRead(globalPath);
        return
        [
            .. database.Select(setting => new CfgDbSetting(
                setting.Section,
                setting.Key,
                setting.Value,
                globalReadable
                    ? R3Yaml.ReadVal(globalPath, setting.Section, setting.Key)
                      ?? BuiltInDefaults.GetValueOrDefault($"{setting.Section}/{setting.Key}")
                    : null))
        ];
    }

    private static RecSyncSetting? FindSetting(
        IReadOnlyList<RecSyncSetting> database,
        string section,
        string key) =>
        database.FirstOrDefault(setting =>
            setting.Section.Equals(section, StringComparison.Ordinal) &&
            setting.Key.Equals(key, StringComparison.Ordinal));

    internal static string? ReadBuiltIn(
        string section,
        string key)
    {
        if (!BuiltInDefaults.TryGetValue(
                $"{section}/{key}",
                out var value))
        {
            throw new InvalidDataException(
                $"No verified RPCS3 built-in default exists for {section}/{key}.");
        }

        return value;
    }
}

#pragma warning restore IDE0130
