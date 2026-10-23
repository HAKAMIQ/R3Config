using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Single owner of "which RPCS3 Settings tab does this Wiki setting belong to"
// and "how does the Wiki frame it". The Wiki settings template defines no
// parameter for the System or GUI tabs, so they are never produced by the
// explicit map; they only ever appear if a future Wiki key is added here.
public static class SettingsSectionMap
{
    private static readonly Dictionary<string, Rpcs3SettingsSection> ByWikiKey =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu decoder"] = Rpcs3SettingsSection.Cpu,
            ["spu decoder"] = Rpcs3SettingsSection.Cpu,
            ["preferred spu threads"] = Rpcs3SettingsSection.Cpu,
            ["thread scheduler"] = Rpcs3SettingsSection.Cpu,
            ["enable spu loop detection"] = Rpcs3SettingsSection.Cpu,
            ["spu xfloat accuracy"] = Rpcs3SettingsSection.Cpu,
            ["xfloat accuracy"] = Rpcs3SettingsSection.Cpu,
            ["spu block size"] = Rpcs3SettingsSection.Cpu,
            ["tsx instructions"] = Rpcs3SettingsSection.Cpu,

            ["renderer"] = Rpcs3SettingsSection.Gpu,
            ["aspect ratio"] = Rpcs3SettingsSection.Gpu,
            ["framelimit"] = Rpcs3SettingsSection.Gpu,
            ["anisotropic filter"] = Rpcs3SettingsSection.Gpu,
            ["anti-aliasing"] = Rpcs3SettingsSection.Gpu,
            ["zcull accuracy"] = Rpcs3SettingsSection.Gpu,
            ["shader quality"] = Rpcs3SettingsSection.Gpu,
            ["3d rendering mode"] = Rpcs3SettingsSection.Gpu,
            ["default resolution"] = Rpcs3SettingsSection.Gpu,
            ["resolution scale"] = Rpcs3SettingsSection.Gpu,
            ["resolution scale threshold"] = Rpcs3SettingsSection.Gpu,
            ["shader mode"] = Rpcs3SettingsSection.Gpu,
            ["write color buffers"] = Rpcs3SettingsSection.Gpu,
            ["strict rendering mode"] = Rpcs3SettingsSection.Gpu,
            ["vsync"] = Rpcs3SettingsSection.Gpu,
            ["stretch to display area"] = Rpcs3SettingsSection.Gpu,
            ["multithreaded rsx"] = Rpcs3SettingsSection.Gpu,
            ["asynchronous texture streaming"] = Rpcs3SettingsSection.Gpu,
            ["handle rsx memory tiling"] = Rpcs3SettingsSection.Gpu,
            ["disable vertex cache"] = Rpcs3SettingsSection.Gpu,
            ["allow host gpu labels"] = Rpcs3SettingsSection.Gpu,
            ["force hardware msaa resolve"] = Rpcs3SettingsSection.Gpu,
            ["emulate special depth comparison"] = Rpcs3SettingsSection.Gpu,
            ["disable asynchronous memory manager"] = Rpcs3SettingsSection.Gpu,
            ["lod bias offset"] = Rpcs3SettingsSection.Gpu,
            ["output scaling"] = Rpcs3SettingsSection.Gpu,
            ["force gpu texture scaling"] = Rpcs3SettingsSection.Gpu,

            ["audio out windows"] = Rpcs3SettingsSection.Audio,
            ["audio out linux"] = Rpcs3SettingsSection.Audio,
            ["audio format"] = Rpcs3SettingsSection.Audio,
            ["dump to file"] = Rpcs3SettingsSection.Audio,
            ["convert to 16-bit"] = Rpcs3SettingsSection.Audio,
            ["volume"] = Rpcs3SettingsSection.Audio,
            ["enable buffering"] = Rpcs3SettingsSection.Audio,
            ["audio buffer duration"] = Rpcs3SettingsSection.Audio,
            ["enable time stretching"] = Rpcs3SettingsSection.Audio,
            ["time stretching threshold"] = Rpcs3SettingsSection.Audio,
            ["microphone type"] = Rpcs3SettingsSection.Audio,

            ["keyboard handler"] = Rpcs3SettingsSection.Io,
            ["mouse handler"] = Rpcs3SettingsSection.Io,
            ["camera input"] = Rpcs3SettingsSection.Io,
            ["camera settings"] = Rpcs3SettingsSection.Io,
            ["move handler"] = Rpcs3SettingsSection.Io,

            ["network status"] = Rpcs3SettingsSection.Network,
            ["psn status"] = Rpcs3SettingsSection.Network,

            ["debug console mode"] = Rpcs3SettingsSection.Advanced,
            ["accurate llvm dfma"] = Rpcs3SettingsSection.Advanced,
            ["accurate dfma"] = Rpcs3SettingsSection.Advanced,
            ["accurate getllar"] = Rpcs3SettingsSection.Advanced,
            ["accurate rsx reservation access"] = Rpcs3SettingsSection.Advanced,
            ["accurate spu dma"] = Rpcs3SettingsSection.Advanced,
            ["ppu llvm accurate vector nans"] = Rpcs3SettingsSection.Advanced,
            ["ppu llvm java mode handling"] = Rpcs3SettingsSection.Advanced,
            ["silence all logs"] = Rpcs3SettingsSection.Advanced,
            ["sleep timers accuracy"] = Rpcs3SettingsSection.Advanced,
            ["maximum spurs threads"] = Rpcs3SettingsSection.Advanced,
            ["firmware libraries"] = Rpcs3SettingsSection.Advanced,
            ["read depth buffers"] = Rpcs3SettingsSection.Advanced,
            ["write depth buffers"] = Rpcs3SettingsSection.Advanced,
            ["read color buffers"] = Rpcs3SettingsSection.Advanced,
            ["disable on-disk shader cache"] = Rpcs3SettingsSection.Advanced,
            ["driver wake-up delay"] = Rpcs3SettingsSection.Advanced,
            ["rsx fifo accuracy"] = Rpcs3SettingsSection.Advanced,
            ["vblank rate"] = Rpcs3SettingsSection.Advanced,
            ["vblank ntsc fixup"] = Rpcs3SettingsSection.Advanced,
            ["clocks scale"] = Rpcs3SettingsSection.Advanced,

            ["disable zcull occlusion queries"] = Rpcs3SettingsSection.Debug,
            ["force cpu blit emulation"] = Rpcs3SettingsSection.Debug,
            ["hook static functions"] = Rpcs3SettingsSection.Debug,
            ["accurate ppu 128 reservations"] = Rpcs3SettingsSection.Debug,
            ["ppu thread count"] = Rpcs3SettingsSection.Debug,
            ["disable spu getllar spin optimization"] = Rpcs3SettingsSection.Debug
        };

    private static readonly Regex ExplicitOptional = new(
        @"^\W*optional\b|\boptional\W*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyCollection<string> WikiKeys => ByWikiKey.Keys;

    public static bool IsExplicit(string wikiKey) =>
        ByWikiKey.ContainsKey(wikiKey);

    // The explicit Wiki-key map wins. A key the map does not know still lands
    // on the RPCS3 tab that owns its YAML section instead of being dropped;
    // the caller learns about this through usedFallback so it can be gated.
    public static Rpcs3SettingsSection Classify(
        CfgChg change,
        out bool usedFallback)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (ByWikiKey.TryGetValue(change.WikiKey, out var section))
        {
            usedFallback = false;
            return section;
        }

        usedFallback = true;
        return change.Section.ToLowerInvariant() switch
        {
            "core" => Rpcs3SettingsSection.Cpu,
            "video" => Rpcs3SettingsSection.Gpu,
            "audio" => Rpcs3SettingsSection.Audio,
            "input/output" => Rpcs3SettingsSection.Io,
            "net" => Rpcs3SettingsSection.Network,
            "system" => Rpcs3SettingsSection.System,
            "miscellaneous" => Rpcs3SettingsSection.Emulator,
            _ => Rpcs3SettingsSection.Advanced
        };
    }

    // Explicit Wiki wording ("Optional: ...") is authoritative over the coarser
    // risk bucket, but a mandatory requirement is never downgraded by it.
    public static SettingRole ClassifyRole(CfgChg change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Class == WikiRecommendationClass.MandatoryGameConfig ||
            change.Risk == RecRisk.Required)
        {
            return SettingRole.Required;
        }

        if (change.Risk == RecRisk.Optional ||
            (!string.IsNullOrWhiteSpace(change.Notes) &&
             ExplicitOptional.IsMatch(change.Notes)))
        {
            return SettingRole.Optional;
        }

        return change.Risk switch
        {
            RecRisk.Rec => SettingRole.Recommended,
            RecRisk.Cond or RecRisk.PatDep or RecRisk.Dep => SettingRole.Conditional,
            _ => SettingRole.Other
        };
    }
}

#pragma warning restore IDE0130
