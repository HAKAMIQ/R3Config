using System.Collections.Generic;
using System.Linq;

namespace R3Config.Mdl;

// Native architecture of the host CPU. Only x64 and ARM64 have RPCS3 backends
// (utils::get_architecture); anything else is Unsupported, never mapped to
// the nearest one.
public enum CpuArchitecture
{
    Unknown,
    X64,
    Arm64,
    Unsupported
}

// Only features RPCS3's own probes (rpcs3/util/sysinfo.cpp) evaluate. The
// per-feature rule and consumers are in Rpcs3CpuSemantics.
public enum CpuFeature
{
    Ssse3,
    Sse41,
    Avx,
    Avx2,
    Avx512,
    Avx512Icl,
    FastVperm2b,
    Fma3,
    Fma4,
    Xop,
    Clwb,
    Erms,
    Fsrm,
    Waitx,
    Waitpkg,
    InvariantTsc,
    Rtm,
    Neon,
    Sha3,
    DotProd,
    I8mm,
    Sve,
    Sve2
}

// Unknown is never folded into Absent: a feature that could not be read is not
// a feature that is missing.
public enum CpuFeaturePresence
{
    Unknown,
    Present,
    Absent
}

// How a feature reading is obtained. The rule is always the one RPCS3 applies
// (rpcs3/util/sysinfo.cpp); this states what R3Config measures it with.
public enum CpuFeatureBasis
{
    // CPUID bits read on the host and judged by the RPCS3 rule.
    SourceRuleCpuid,

    // As above, plus OS-enabled vector state from the Windows XState API.
    // RPCS3 itself uses XGETBV(0); the Windows reading serves the same purpose
    // but is independent host evidence, and full equivalence is not proven.
    SourceRuleCpuidWithHostXState,

    // The Windows processor-feature API, which RPCS3 also calls on ARM64.
    SourceRuleHostApi
}

public sealed record CpuFeatureFact(CpuFeature Feature, CpuFeaturePresence Presence, string Source)
{
    public CpuFeatureBasis Basis => Feature switch
    {
        CpuFeature.Avx or CpuFeature.Avx2 or CpuFeature.Avx512 or CpuFeature.Avx512Icl or
            CpuFeature.FastVperm2b or CpuFeature.Xop => CpuFeatureBasis.SourceRuleCpuidWithHostXState,
        CpuFeature.Neon or CpuFeature.Sha3 or CpuFeature.DotProd or CpuFeature.I8mm or
            CpuFeature.Sve or CpuFeature.Sve2 => CpuFeatureBasis.SourceRuleHostApi,
        _ => CpuFeatureBasis.SourceRuleCpuid
    };
}

// Active: at least one physical core reports SMT (LTP_PC_SMT). Inactive covers
// both "not capable" and "disabled"; Windows does not distinguish them.
public enum SmtState
{
    Unknown,
    Active,
    Inactive
}

public enum HybridTopology
{
    Unknown,
    Uniform,
    Heterogeneous
}

// One EfficiencyClass value reported by the OS. Which class is "performance"
// is not asserted here and RPCS3 has no hybrid-specific handling.
public sealed record CpuCoreClass(int EfficiencyClass, int PhysicalCores, int LogicalProcessors);

// thread_ctrl::native_core_arrangement as RPCS3 detect_cpu_layout() derives it.
public enum Rpcs3SchedulerLayout
{
    Unknown,
    Generic,
    AmdCcx,
    IntelHt
}

// What a non-OS Thread Scheduler Mode can do to the affinity mask of this
// host: NoChange is proven from get_affinity_mask control flow; LayoutSpecific
// means a mask rule exists that depends on mode, family/model or thread count.
public enum SchedulerAffinityEffect
{
    Unknown,
    NoChange,
    LayoutSpecific
}

public sealed record HostCpuInfo(
    CpuArchitecture Architecture,
    string? Brand,
    IReadOnlyList<CpuFeatureFact> Features,
    int? PhysicalCores,
    int? LogicalProcessorsTotal,
    uint? NativeThreadCount,
    int? ProcessorGroups,
    SmtState Smt,
    HybridTopology Hybrid,
    IReadOnlyList<CpuCoreClass> CoreClasses,
    Rpcs3SchedulerLayout SchedulerLayout,
    SchedulerAffinityEffect SchedulerEffect)
{
    // utils::get_thread_count() is the logical processor count of the current
    // processor group only; with several groups it is not the machine total.
    public bool ThreadCountIsPerGroup => ProcessorGroups is > 1;

    public CpuFeaturePresence PresenceOf(CpuFeature feature) =>
        Features.FirstOrDefault(fact => fact.Feature == feature)?.Presence ?? CpuFeaturePresence.Unknown;

    public static HostCpuInfo Unknown { get; } = new(
        CpuArchitecture.Unknown,
        null,
        [],
        null,
        null,
        null,
        null,
        SmtState.Unknown,
        HybridTopology.Unknown,
        [],
        Rpcs3SchedulerLayout.Unknown,
        SchedulerAffinityEffect.Unknown);
}
