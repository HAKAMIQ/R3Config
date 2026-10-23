using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public readonly record struct CpuidRegisters(uint Eax, uint Ebx, uint Ecx, uint Edx);

// One physical core as the OS reports it (RelationProcessorCore).
public sealed record CpuCoreRecord(byte Flags, byte EfficiencyClass, int LogicalProcessors);

// Raw, unjudged host readings. A null member means the reading was not
// possible, which the analyzer turns into Unknown, never into a default.
public sealed record HostCpuRawFacts(
    string? OsArchitecture,
    string? ProcessArchitecture,
    Func<uint, uint, CpuidRegisters>? Cpuid = null,
    ulong? EnabledXState = null,
    Func<int, bool>? ProcessorFeaturePresent = null,
    uint? NativeThreadCount = null,
    int? ProcessorGroups = null,
    IReadOnlyList<CpuCoreRecord>? Cores = null);

// Windows PF_* ids RPCS3 passes to IsProcessorFeaturePresent on ARM64
// (checked against winnt.h of SDK 10.0.26100.0).
public static class ArmProcessorFeature
{
    public const int Neon = 18;
    public const int DotProd = 43;
    public const int Sve = 46;
    public const int Sve2 = 47;
    public const int Sha3 = 64;
    public const int I8mm = 66;
}

// Turns raw host readings into HostCpuInfo by the same rules RPCS3 applies in
// rpcs3/util/sysinfo.cpp and Utilities/Thread.cpp (detect_cpu_layout), so the
// result is what RPCS3 itself would conclude on this host. It reads nothing
// and guesses nothing: missing input yields Unknown.
public static class HostCpuAnalyzer
{
    private const string EmulatedProcess = "EmulatedProcess";
    private const string CpuidUnavailable = "CpuidUnavailable";
    private const string XStateUnavailable = "XStateUnavailable";
    private const string ProbeUnavailable = "ProbeUnavailable";

    // XCR0 bits: SSE + AVX state, and opmask + ZMM_Hi256 + Hi16_ZMM for AVX-512.
    private const ulong XStateAvx = 0x6;
    private const ulong XStateAvx512 = 0xe6;

    private const byte SmtFlag = 0x1;

    public static CpuArchitecture MapArchitecture(string? architecture) =>
        architecture?.Trim().ToUpperInvariant() switch
        {
            null or "" => CpuArchitecture.Unknown,
            "X64" => CpuArchitecture.X64,
            "ARM64" => CpuArchitecture.Arm64,
            _ => CpuArchitecture.Unsupported
        };

    // True when the CPU is native ARM64 but this process is not: CPUID and the
    // PF_* answers then describe an emulated CPU, not the host.
    public static bool IsEmulated(string? osArchitecture, string? processArchitecture) =>
        MapArchitecture(osArchitecture) == CpuArchitecture.Arm64 &&
        MapArchitecture(processArchitecture) != CpuArchitecture.Arm64;

    public static HostCpuInfo Analyze(HostCpuRawFacts? raw)
    {
        if (raw is null)
        {
            return HostCpuInfo.Unknown;
        }

        var architecture = MapArchitecture(raw.OsArchitecture);
        var emulated = IsEmulated(raw.OsArchitecture, raw.ProcessArchitecture);
        string? brand = null;
        IReadOnlyList<CpuFeatureFact> features = [];
        if (architecture == CpuArchitecture.X64)
        {
            (brand, features) = AnalyzeX64(raw);
        }
        else if (architecture == CpuArchitecture.Arm64)
        {
            features = AnalyzeArm64(raw, emulated);
        }

        var cores = raw.Cores;
        var classes = cores is null
            ? []
            : cores.GroupBy(core => (int)core.EfficiencyClass)
                .OrderBy(group => group.Key)
                .Select(group => new CpuCoreClass(group.Key, group.Count(), group.Sum(core => core.LogicalProcessors)))
                .ToArray();

        var smt = cores is null || cores.Count == 0
            ? SmtState.Unknown
            : cores.Any(core => core.Flags == SmtFlag) ? SmtState.Active : SmtState.Inactive;
        var hybrid = classes.Length == 0
            ? HybridTopology.Unknown
            : classes.Length == 1 ? HybridTopology.Uniform : HybridTopology.Heterogeneous;

        var layout = SchedulerLayout(architecture, raw.Cpuid is not null, brand, smt);
        return new HostCpuInfo(
            architecture,
            brand,
            features,
            cores is { Count: > 0 } ? cores.Count : null,
            cores is { Count: > 0 } ? cores.Sum(core => core.LogicalProcessors) : null,
            raw.NativeThreadCount is > 0 ? raw.NativeThreadCount : null,
            raw.ProcessorGroups is > 0 ? raw.ProcessorGroups : null,
            smt,
            hybrid,
            classes,
            layout,
            AffinityEffect(layout, raw.NativeThreadCount));
    }

    // Parses the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX buffer returned for
    // RelationProcessorCore. Any size or layout inconsistency yields null: a
    // truncated buffer is not a smaller CPU.
    public static IReadOnlyList<CpuCoreRecord>? ParseProcessorCoreInformation(ReadOnlySpan<byte> buffer, int pointerSize = 0)
    {
        var maskSize = pointerSize == 0 ? IntPtr.Size : pointerSize;
        if (maskSize is not (4 or 8))
        {
            return null;
        }

        // Header (Relationship, Size) then PROCESSOR_RELATIONSHIP: Flags,
        // EfficiencyClass, Reserved[20], GroupCount, GroupMask[]. GROUP_AFFINITY
        // is Mask + Group + Reserved[3] words, padded to pointer alignment.
        const int headerSize = 8;
        const int groupMaskOffset = 32;
        var groupAffinitySize = maskSize == 8 ? 16 : 12;

        var cores = new List<CpuCoreRecord>();
        var offset = 0;
        while (offset < buffer.Length)
        {
            if (buffer.Length - offset < headerSize + 24)
            {
                return null;
            }

            var relationship = BitConverter.ToInt32(buffer.Slice(offset, 4));
            var size = BitConverter.ToUInt32(buffer.Slice(offset + 4, 4));
            if (size < groupMaskOffset + groupAffinitySize || size > buffer.Length - offset)
            {
                return null;
            }

            if (relationship == 0)
            {
                var flags = buffer[offset + headerSize];
                var efficiencyClass = buffer[offset + headerSize + 1];
                var groupCount = BitConverter.ToUInt16(buffer.Slice(offset + headerSize + 22, 2));
                if (groupCount == 0 || (long)groupMaskOffset + ((long)groupCount * groupAffinitySize) > size)
                {
                    return null;
                }

                var logical = 0;
                for (var index = 0; index < groupCount; index++)
                {
                    var maskOffset = offset + groupMaskOffset + (index * groupAffinitySize);
                    logical += maskSize == 8
                        ? BitOperations.PopCount(BitConverter.ToUInt64(buffer.Slice(maskOffset, 8)))
                        : BitOperations.PopCount(BitConverter.ToUInt32(buffer.Slice(maskOffset, 4)));
                }

                cores.Add(new CpuCoreRecord(flags, efficiencyClass, logical));
            }

            offset += (int)size;
        }

        return cores;
    }

    // Thread.cpp thread_ctrl::detect_cpu_layout(): the brand string decides
    // "Ryzen" (amd_ccx) before "Intel" (intel_ht, only when some core has SMT),
    // otherwise generic. RPCS3 reads the brand from CPUID on x64; the ARM64
    // brand comes from code R3Config cannot reproduce, so it stays Unknown.
    private static Rpcs3SchedulerLayout SchedulerLayout(
        CpuArchitecture architecture,
        bool cpuidAvailable,
        string? brand,
        SmtState smt)
    {
        if (architecture != CpuArchitecture.X64 || !cpuidAvailable)
        {
            return Rpcs3SchedulerLayout.Unknown;
        }

        var name = brand ?? "Unknown CPU";
        if (name.Contains("Ryzen", StringComparison.Ordinal))
        {
            return Rpcs3SchedulerLayout.AmdCcx;
        }

        if (name.Contains("Intel", StringComparison.Ordinal))
        {
            return smt switch
            {
                SmtState.Active => Rpcs3SchedulerLayout.IntelHt,
                SmtState.Inactive => Rpcs3SchedulerLayout.Generic,
                _ => Rpcs3SchedulerLayout.Unknown
            };
        }

        return Rpcs3SchedulerLayout.Generic;
    }

    // Thread.cpp thread_ctrl::get_affinity_mask(): generic returns the whole
    // process mask; amd_ccx returns it for <= 8 threads; intel_ht only narrows
    // it at >= 12 threads. Beyond those bounds the result depends on Thread
    // Scheduler Mode, CPU family/model and the exact thread count.
    private static SchedulerAffinityEffect AffinityEffect(Rpcs3SchedulerLayout layout, uint? threadCount)
    {
        if (layout == Rpcs3SchedulerLayout.Generic)
        {
            return SchedulerAffinityEffect.NoChange;
        }

        if (layout == Rpcs3SchedulerLayout.Unknown || threadCount is not > 0)
        {
            return SchedulerAffinityEffect.Unknown;
        }

        var narrowsFrom = layout == Rpcs3SchedulerLayout.AmdCcx ? 9u : 12u;
        return threadCount < narrowsFrom
            ? SchedulerAffinityEffect.NoChange
            : SchedulerAffinityEffect.LayoutSpecific;
    }

    // CPUID is only read when the OS itself is x64: an x64 process on ARM64
    // Windows takes the ARM64 branch (its CPUID would describe an emulated CPU).
    private static (string? Brand, IReadOnlyList<CpuFeatureFact> Features) AnalyzeX64(HostCpuRawFacts raw)
    {
        var x64Features = new[]
        {
            CpuFeature.Ssse3, CpuFeature.Sse41, CpuFeature.Avx, CpuFeature.Avx2, CpuFeature.Avx512, CpuFeature.Avx512Icl,
            CpuFeature.FastVperm2b, CpuFeature.Fma3, CpuFeature.Fma4, CpuFeature.Xop, CpuFeature.Clwb, CpuFeature.Erms, CpuFeature.Fsrm,
            CpuFeature.Waitx, CpuFeature.Waitpkg, CpuFeature.InvariantTsc, CpuFeature.Rtm
        };

        if (raw.Cpuid is null)
        {
            return (null, [.. x64Features.Select(feature => new CpuFeatureFact(feature, CpuFeaturePresence.Unknown, CpuidUnavailable))]);
        }

        var cpuid = raw.Cpuid;
        var maxLeaf = cpuid(0, 0).Eax;
        var leaf1 = maxLeaf >= 1 ? cpuid(1, 0) : default;
        var leaf7 = maxLeaf >= 7 ? cpuid(7, 0) : default;
        var ext1 = cpuid(0x80000001, 0);
        var ext7 = cpuid(0x80000007, 0);
        var osSupportsAvx = raw.EnabledXState is { } xcr0 ? (xcr0 & XStateAvx) == XStateAvx : (bool?)null;
        var osSupportsAvx512 = raw.EnabledXState is { } xcr0Wide ? (xcr0Wide & XStateAvx512) == XStateAvx512 : (bool?)null;
        var avxOsxsave = (leaf1.Ecx & 0x0C000000) == 0x0C000000;

        CpuFeatureFact Bit(CpuFeature feature, bool present, string source) =>
            new(feature, present ? CpuFeaturePresence.Present : CpuFeaturePresence.Absent, source);

        // sysinfo.cpp has_avx / has_avx2 / has_avx512 also need XGETBV(0). R3Config
        // takes the OS-enabled state from the Windows XState API instead, which
        // is not the RPCS3 instruction. A CPUID-negative answer is already
        // final; otherwise without the API answer the feature stays Unknown.
        CpuFeatureFact WithOsState(CpuFeature feature, bool cpuidPresent, bool? osSupport, string source) =>
            !cpuidPresent
                ? Bit(feature, false, source)
                : osSupport is null
                    ? new(feature, CpuFeaturePresence.Unknown, XStateUnavailable)
                    : Bit(feature, osSupport.Value, source);

        var avx = maxLeaf >= 1 && (leaf1.Ecx & 0x10000000) != 0 && avxOsxsave;
        var avx2 = maxLeaf >= 7 && (leaf7.Ebx & 0x20) != 0 && avxOsxsave;
        var avx512 = maxLeaf >= 7 && (leaf7.Ebx & 0xd0030000) == 0xd0030000 && avxOsxsave;
        var avx512Icl = avx512 &&
                        (leaf7.Ebx & 0x00200000) == 0x00200000 &&
                        (leaf7.Ecx & 0x00005f42) == 0x00005f42;

        // has_fast_vperm2b: AVX-512 with VBMI and SSE4A (Zen4 executes
        // VPERMI2B/VPERMT2B in one uop, current Intel in three).
        var fastVperm2b = avx512 && (leaf7.Ecx & 0x2) == 0x2 && (ext1.Ecx & 0x40) == 0x40;

        var facts = new List<CpuFeatureFact>
        {
            Bit(CpuFeature.Ssse3, maxLeaf >= 1 && (leaf1.Ecx & 0x200) != 0, "cpuid.1:ECX[9]"),
            Bit(CpuFeature.Sse41, maxLeaf >= 1 && (leaf1.Ecx & 0x80000) != 0, "cpuid.1:ECX[19]"),
            WithOsState(CpuFeature.Avx, avx, osSupportsAvx, "cpuid.1:ECX[28,27,26]+OsXState[2:1]"),
            WithOsState(CpuFeature.Avx2, avx2, osSupportsAvx, "cpuid.7:EBX[5]+cpuid.1:ECX[27:26]+OsXState[2:1]"),
            WithOsState(CpuFeature.Avx512, avx512, osSupportsAvx512, "cpuid.7:EBX[31,30,28,17,16]+OsXState[7:5,2:1]"),
            WithOsState(CpuFeature.Avx512Icl, avx512Icl, osSupportsAvx512, "avx512+cpuid.7:EBX[21]+cpuid.7:ECX[14,12:8,6,1]"),
            WithOsState(CpuFeature.FastVperm2b, fastVperm2b, osSupportsAvx512, "avx512+cpuid.7:ECX[1]+cpuid.80000001:ECX[6]"),
            Bit(CpuFeature.Fma3, maxLeaf >= 1 && (leaf1.Ecx & 0x1000) != 0, "cpuid.1:ECX[12]"),
            Bit(CpuFeature.Fma4, maxLeaf >= 7 && (ext1.Ecx & 0x10000) != 0, "cpuid.80000001:ECX[16]"),
            WithOsState(CpuFeature.Xop, avx && (ext1.Ecx & 0x800) != 0, osSupportsAvx, "avx+cpuid.80000001:ECX[11]"),
            Bit(CpuFeature.Clwb, maxLeaf >= 7 && (leaf7.Ebx & 0x1000000) != 0, "cpuid.7:EBX[24]"),
            Bit(CpuFeature.Erms, maxLeaf >= 7 && (leaf7.Ebx & 0x200) != 0, "cpuid.7:EBX[9]"),
            Bit(CpuFeature.Fsrm, maxLeaf >= 7 && (leaf7.Edx & 0x10) != 0, "cpuid.7:EDX[4]"),
            Bit(CpuFeature.Waitx, maxLeaf >= 7 && (ext1.Ecx & 0x20000000) != 0, "cpuid.80000001:ECX[29]"),
            Bit(CpuFeature.Waitpkg, maxLeaf >= 7 && (leaf7.Ecx & 0x20) != 0, "cpuid.7:ECX[5]"),
            Bit(CpuFeature.InvariantTsc, maxLeaf >= 7 && (ext7.Edx & 0x100) != 0, "cpuid.80000007:EDX[8]"),
            Bit(CpuFeature.Rtm, maxLeaf >= 7 && (leaf7.Ebx & 0x800) != 0, "cpuid.7:EBX[11]")
        };

        return (Brand(cpuid), facts);
    }

    // sysinfo.cpp get_cpu_brand(): leaves 0x80000002..4 as raw bytes, cut at
    // the last non-NUL, trimmed, runs of spaces collapsed to one.
    private static string? Brand(Func<uint, uint, CpuidRegisters> cpuid)
    {
        if (cpuid(0x80000000, 0).Eax < 0x80000004)
        {
            return null;
        }

        var bytes = new List<byte>(48);
        for (uint leaf = 0x80000002; leaf <= 0x80000004; leaf++)
        {
            var registers = cpuid(leaf, 0);
            foreach (var value in new[] { registers.Eax, registers.Ebx, registers.Ecx, registers.Edx })
            {
                bytes.AddRange(BitConverter.GetBytes(value));
            }
        }

        var text = Encoding.Latin1.GetString([.. bytes]).TrimEnd('\0').Trim(' ');
        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        return text.Length == 0 ? null : text;
    }

    private static IReadOnlyList<CpuFeatureFact> AnalyzeArm64(HostCpuRawFacts raw, bool emulated)
    {
        var map = new (CpuFeature Feature, int Pf)[]
        {
            (CpuFeature.Neon, ArmProcessorFeature.Neon),
            (CpuFeature.Sha3, ArmProcessorFeature.Sha3),
            (CpuFeature.DotProd, ArmProcessorFeature.DotProd),
            (CpuFeature.I8mm, ArmProcessorFeature.I8mm),
            (CpuFeature.Sve, ArmProcessorFeature.Sve),
            (CpuFeature.Sve2, ArmProcessorFeature.Sve2)
        };

        if (emulated || raw.ProcessorFeaturePresent is null)
        {
            var reason = emulated ? EmulatedProcess : ProbeUnavailable;
            return [.. map.Select(entry => new CpuFeatureFact(entry.Feature, CpuFeaturePresence.Unknown, reason))];
        }

        return [.. map.Select(entry => new CpuFeatureFact(
            entry.Feature,
            raw.ProcessorFeaturePresent(entry.Pf) ? CpuFeaturePresence.Present : CpuFeaturePresence.Absent,
            $"IsProcessorFeaturePresent({entry.Pf})"))];
    }
}
