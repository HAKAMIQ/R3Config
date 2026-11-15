using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
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
        RecSyncApiGame? databaseConfig = null,
        bool databaseLoaded = false)
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

        // The Settings line and every row source come from this one boot source.
        var bootSource = ResolveBootSource(game, targetPath, globalPath, databaseConfig, databaseLoaded);
        var items = changes
            .Zip(
                state.Items,
                (change, stateItem) =>
                {
                    var (runVal, source) = RowSource(bootSource, targetPath, globalPath, database, change, stateItem);
                    return new CfgEntry(
                        change,
                        stateItem.Current,
                        stateItem)
                    {
                        HasCustomOverride = R3Yaml.HasKey(targetPath, change.Section, change.Key),
                        RunVal = runVal,
                        CurSrc = source,
                        DefVal = TryReadBuiltIn(change.Section, change.Key, out var builtIn) ? builtIn : null
                    };
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
            DbCfg = DisplayRead(() => ActiveDatabase(targetPath, globalPath, database), []),
            BootSource = bootSource,
            Hints = recommendation?.Guidance ?? []
        };
    }

    // The layer RPCS3 boots this game with on top of config.yml, in the order of
    // rpcs3/Emu/System.cpp:1808-1850: "<boot path>.yml", then config_<SERIAL>.yml;
    // the first that loads disables the database config, which otherwise applies
    // when its entry is not empty. A custom config RPCS3 fails to parse does not
    // disable it, so an unreadable one is Unknown. The database side is only what
    // R3Config's shipped copy says: databaseLoaded tells whether that copy loaded,
    // so a missing entry is evidence only then. The user's own
    // GuiConfigs/config_database.dat is not read and may differ. Display only:
    // a config that cannot be read or decoded is Unknown, never an exception.
    internal static CfgBootSrc ResolveBootSource(
        Rpcs3GameDescriptor game,
        string targetPath,
        string globalPath,
        RecSyncApiGame? databaseConfig,
        bool databaseLoaded)
    {
        ArgumentNullException.ThrowIfNull(game);

        return DisplayRead(
            () =>
            {
                if (string.IsNullOrWhiteSpace(game.Serial) ||
                    Directory.Exists(targetPath) ||
                    BootConfigExists(game.InstPath) != false)
                {
                    return CfgBootSrc.Unknown;
                }

                if (File.Exists(targetPath))
                {
                    return R3Yaml.CanRead(targetPath) ? CfgBootSrc.Custom : CfgBootSrc.Unknown;
                }

                if (Directory.Exists(globalPath) || !R3Yaml.CanRead(globalPath))
                {
                    return CfgBootSrc.Unknown;
                }

                if (databaseConfig is not null)
                {
                    // An entry with no parsed setting cannot be shown as a layer.
                    return databaseConfig.Settings.Count > 0 ? CfgBootSrc.Database : CfgBootSrc.Unknown;
                }

                return databaseLoaded ? CfgBootSrc.Global : CfgBootSrc.Unknown;
            },
            CfgBootSrc.Unknown);
    }

    // R3Config does not resolve RPCS3's boot path (the game folder or its
    // EBOOT.BIN), so any config named after a likely boot path makes the source
    // unprovable rather than ignored. The game path follows the local-path
    // policy of the other game-path consumers: null (unknown) when it is not a
    // local, fully qualified path.
    private static bool? BootConfigExists(string? gamePath)
    {
        string root;
        try
        {
            root = RuntimePathPolicy.RequireLocal(gamePath ?? string.Empty, "Game");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or
                                       UnauthorizedAccessException or NotSupportedException or
                                       System.Security.SecurityException)
        {
            return null;
        }

        var folder = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidates = new List<string>
        {
            Path.Combine(root, ".yml"),
            Path.Combine(root, "PS3_GAME", "USRDIR", "EBOOT.BIN.yml"),
            Path.Combine(root, "USRDIR", "EBOOT.BIN.yml")
        };

        // At a drive root "E:" + ".yml" is the drive-relative "E:.yml" (the
        // current directory of that drive), not a file next to the game.
        var driveRoot = Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (folder.Length > 0 && !string.Equals(folder, driveRoot, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(folder + ".yml");
        }

        return candidates.Any(File.Exists);
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
        string key) =>
        Resolve(targetPath, globalPath, database, section, key).Value;

    // ResolveEffective plus the layer the value came from, recorded at the
    // point each layer answers. Throws exactly where ResolveEffective throws.
    private static (string? Value, CfgValSrc Source) Resolve(
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
            return (current, CfgValSrc.Custom);
        }

        if (database is not null &&
            !File.Exists(targetPath) &&
            FindSetting(database, section, key) is { } fromDatabase)
        {
            return (fromDatabase.Value, CfgValSrc.Database);
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

        return global is not null
            ? (global, CfgValSrc.Global)
            : (ReadBuiltIn(section, key), CfgValSrc.BuiltIn);
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

    // RunVal and CurSrc of one row, derived from the plan's boot source so the
    // ToolTip never contradicts the Settings line. CurSrc is the layer of the
    // row's current value (CurVal): Unknown whenever the boot source is Unknown,
    // the state could not read the value, or the chain no longer resolves that
    // exact value. RunVal equals CurVal unless R3Config's copy of the RPCS3
    // database makes RPCS3 boot the key with another value; when it boots the
    // same value, that value's layer is Database. Display only: a config that
    // cannot be read or decoded leaves the source Unknown and never throws.
    private static (string? Value, CfgValSrc Source) RowSource(
        CfgBootSrc bootSource,
        string targetPath,
        string globalPath,
        IReadOnlyList<RecSyncSetting>? database,
        CfgChg change,
        RecItem stateItem)
    {
        var current = stateItem.Current;
        if (bootSource == CfgBootSrc.Unknown || stateItem.State == RecState.Unknown)
        {
            return (current, CfgValSrc.Unknown);
        }

        return DisplayRead(
            () =>
            {
                var resolved = Resolve(targetPath, globalPath, null, change.Section, change.Key);
                if (!string.Equals(resolved.Value, current, StringComparison.Ordinal))
                {
                    return (current, CfgValSrc.Unknown);
                }

                if (bootSource == CfgBootSrc.Database &&
                    database is not null &&
                    FindSetting(database, change.Section, change.Key) is { } fromDatabase)
                {
                    return string.Equals(fromDatabase.Value, current, StringComparison.Ordinal)
                        ? (current, CfgValSrc.Database)
                        : (fromDatabase.Value, resolved.Source);
                }

                return (current, resolved.Source);
            },
            (current, CfgValSrc.Unknown));
    }

    // Runs a display-only read: an unreadable, undecodable or vanished config
    // gives the fallback. The apply path never goes through here.
    private static T DisplayRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is InvalidDataException or DecoderFallbackException or IOException or
                                       UnauthorizedAccessException or YamlDotNet.Core.YamlException)
        {
            return fallback;
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

    // RPCS3's default for a key from the verified table above, without
    // throwing: false (and null) when the table has no entry.
    public static bool TryReadBuiltIn(
        string section,
        string key,
        [NotNullWhen(true)] out string? value) =>
        BuiltInDefaults.TryGetValue($"{section}/{key}", out value);

    internal static string? ReadBuiltIn(
        string section,
        string key)
    {
        if (!TryReadBuiltIn(section, key, out var value))
        {
            throw new InvalidDataException(
                $"No verified RPCS3 built-in default exists for {section}/{key}.");
        }

        return value;
    }
}

#pragma warning restore IDE0130
