using System;

namespace R3Config.Mdl;

// What RPCS3 does with a host CPU feature, taken from the consumers of the
// utils::has_* probe in the frozen source. BannerOnly means the probe is only
// printed in the log header; no execution path reads it.
[Flags]
public enum Rpcs3FeatureUse
{
    None = 0,
    ExecutionPath = 1,
    LlvmTargetSelection = 2,
    MemoryRoutine = 4,
    WaitPrimitive = 8,
    Timing = 16,
    UiGate = 32,
    BannerOnly = 64,
    Requirement = 128
}

public sealed record Rpcs3FeatureSemantics(
    CpuFeature Feature,
    CpuArchitecture Architecture,
    string Probe,
    Rpcs3FeatureUse Use,
    string Consumers);

// Whether the installed RPCS3 binary was built with LLVM_AVAILABLE, a
// compile-time switch. R3Config has no source for it yet, so it is Unknown.
public enum LlvmBuildCapability
{
    Unknown,
    Present,
    Absent
}

// A decoder value as the frozen source defines it for an architecture.
// BuildCapabilityUnknown means it needs an LLVM build that is not proven.
public enum DecoderAvailability
{
    Available,
    BuildCapabilityUnknown,
    NotAvailable,
    ReplacedAtStart
}

public sealed record Rpcs3DecoderSupport(string Decoder, DecoderAvailability Availability, string Source);
