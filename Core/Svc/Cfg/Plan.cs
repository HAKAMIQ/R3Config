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
            ["Video/Vblank NTSC Fixup"] = "false",
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
        string hostPlatform = "Windows")
    {
        // hw is never used as a tier: the state of a documented change never depends on
        // Low/Mid/High. Only proven vendor/host/build facts feed the change's own scope.
        var facts = RuntimeFacts.Create(game, hostPlatform, build, hw);
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
                        HasCustomOverride = R3Yaml.HasKey(targetPath, change.Section, change.Key)
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
                item => item.IsAction && item.IsDiff));
    }

    // The value a running RPCS3 would use for this key: the game custom config
    // when it overrides the key, otherwise the global RPCS3 config, otherwise
    // the verified RPCS3 built-in default. Preview and Apply must resolve the
    // current state through this one chain. Reading only the custom config
    // reports every key the custom config inherits rather than overrides as an
    // external change, because an inherited key is physically absent there.
    internal static string? ResolveEffective(
        string targetPath,
        string globalPath,
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
