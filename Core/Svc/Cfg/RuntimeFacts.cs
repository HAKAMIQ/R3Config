using System;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Builds the runtime facts scope conditions are compared with. A fact is filled only when the
// machine proves it: an unknown, mixed or unprobed value stays null, and null is never a match.
// The renderer is left null on purpose: the effective renderer can be changed by the very plan
// being evaluated, so a renderer-scoped change is Partial (shown, never auto-applied).
public static class RuntimeFacts
{
    public static EvidenceRuntimeFacts Create(
        Rpcs3GameDescriptor game,
        string hostPlatform = "Windows",
        Rpcs3RuntimeBuild? build = null,
        HwInfo? hardware = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        return new EvidenceRuntimeFacts(
            Serial: game.Serial,
            AppVersion: string.IsNullOrWhiteSpace(game.Version) ? null : game.Version,
            PpuHash: string.IsNullOrWhiteSpace(game.PpuHash) ? null : game.PpuHash,
            Build: build,
            Architecture: build is { IsExact: true } ? build.Architecture : null,
            Edition: NoteScope.EditionOf(game.Serial),
            Platform: string.IsNullOrWhiteSpace(hostPlatform) ? null : hostPlatform,
            GpuVendor: GpuVendor(hardware),
            CpuVendor: hardware?.CpuVendor switch
            {
                HwVendor.Amd => "amd",
                HwVendor.Intel => "intel",
                _ => null
            });
    }

    // The GPU vendor is a fact only when every reported adapter has a known vendor and they all
    // agree; with several vendors RPCS3's own adapter is not proven.
    private static string? GpuVendor(HwInfo? hardware)
    {
        var adapters = hardware?.GpuAdapters;
        if (adapters is null || adapters.Count == 0 || adapters.Any(a => a.Vendor is HwVendor.Unknown or HwVendor.Other))
        {
            return null;
        }

        var vendors = adapters.Select(a => a.Vendor).Distinct().ToArray();
        return vendors.Length == 1
            ? vendors[0] switch { HwVendor.Amd => "amd", HwVendor.Nvidia => "nvidia", _ => "intel" }
            : null;
    }
}

#pragma warning restore IDE0130
