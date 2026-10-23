using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

// CPU Runtime Context: architecture mapping, RPCS3-rule feature detection,
// topology, scheduler layout, fail-closed behavior, RPCS3 applicability and the
// Runtime Context integration. Every fixture is synthetic; only
// RunRealHost() reads the machine and it asserts invariants, never values.
internal static class HostCpuT
{
    private const uint MaxLeaf = 0x20;

    private sealed class Cpuid
    {
        private readonly Dictionary<(uint, uint), CpuidRegisters> _leaves = [];

        public Cpuid Set(uint leaf, uint sub, uint eax = 0, uint ebx = 0, uint ecx = 0, uint edx = 0)
        {
            _leaves[(leaf, sub)] = new CpuidRegisters(eax, ebx, ecx, edx);
            return this;
        }

        public Cpuid Brand(string brand)
        {
            var bytes = new byte[48];
            System.Text.Encoding.ASCII.GetBytes(brand).CopyTo(bytes, 0);
            for (uint index = 0; index < 3; index++)
            {
                var offset = (int)index * 16;
                Set(
                    0x80000002 + index,
                    0,
                    BitConverter.ToUInt32(bytes, offset),
                    BitConverter.ToUInt32(bytes, offset + 4),
                    BitConverter.ToUInt32(bytes, offset + 8),
                    BitConverter.ToUInt32(bytes, offset + 12));
            }

            return Set(0x80000000, 0, eax: 0x80000008);
        }

        public Func<uint, uint, CpuidRegisters> Reader => (leaf, sub) =>
            _leaves.TryGetValue((leaf, sub), out var registers) ? registers : default;
    }

    private const uint Ssse3 = 0x200;
    private const uint Sse41 = 0x80000;
    private const uint Fma3 = 0x1000;
    private const uint AvxBit = 0x10000000;
    private const uint OsXsave = 0x0C000000;

    private static Cpuid IntelLike(string brand, bool avx512 = false) => new Cpuid()
        .Set(0, 0, eax: MaxLeaf)
        .Set(1, 0, ecx: Ssse3 | Sse41 | Fma3 | AvxBit | OsXsave)
        .Set(7, 0, ebx: 0x20 | 0x200 | 0x1000000 | (avx512 ? 0xd0030000u : 0), ecx: 0x20, edx: 0x10)
        .Set(0x80000001, 0)
        .Set(0x80000007, 0, edx: 0x100)
        .Brand(brand);

    private static Cpuid AmdLike(string brand) => new Cpuid()
        .Set(0, 0, eax: MaxLeaf)
        .Set(1, 0, ecx: Ssse3 | Sse41 | Fma3 | AvxBit | OsXsave)
        .Set(7, 0, ebx: 0x20 | 0x200 | 0x1000000, ecx: 0, edx: 0)
        .Set(0x80000001, 0, ecx: 0x20000000 | 0x800 | 0x10000)
        .Set(0x80000007, 0, edx: 0x100)
        .Brand(brand);

    private static IReadOnlyList<CpuCoreRecord> Cores(int count, int logicalPerCore, byte flags, byte efficiencyClass = 0) =>
        [.. Enumerable.Range(0, count).Select(_ => new CpuCoreRecord(flags, efficiencyClass, logicalPerCore))];

    private static HostCpuRawFacts X64(
        Cpuid cpuid,
        IReadOnlyList<CpuCoreRecord>? cores,
        uint? threads,
        ulong? xstate = 0x6,
        string os = "X64",
        string process = "X64",
        int groups = 1) =>
        new(os, process, cpuid.Reader, xstate, null, threads, groups, cores);

    public static void Run()
    {
        AssertArchitectureMapping();
        AssertIntelLikeFixture();
        AssertAmdLikeFixture();
        AssertSchedulerRules();
        AssertFailClosed();
        AssertArm64();
        AssertCoreInformationParser();
        AssertRpcs3Applicability();
        AssertProbeInjection();
        AssertRuntimeContextIntegration();
        AssertLlvmBuildCapabilityFailsClosed();
        AssertFeatureProvenanceAndCoverage();
        AssertHybridAndSchedulerAreSeparate();
        AssertNoHeuristicLeakage();

        Console.WriteLine("Host CPU runtime context (architecture, RPCS3 feature rules, topology, scheduler layout, fail-closed, applicability, integration) PASS.");
    }

    public static void RunRealHost()
    {
        var info = new HwProbe().Probe();
        var cpu = info.Cpu;
        Assert(cpu is not null, "The real probe fills the CPU context on Windows.");
        var expected = HostCpuAnalyzer.MapArchitecture(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString());
        Assert(cpu!.Architecture == expected, "The CPU architecture is the OS architecture.");
        Assert(cpu.NativeThreadCount is > 0, "GetNativeSystemInfo reports the thread count.");
        Assert(cpu.PhysicalCores is > 0 && cpu.LogicalProcessorsTotal >= cpu.PhysicalCores,
            "Physical cores are at most the logical processors.");
        Assert(cpu.CoreClasses.Sum(coreClass => coreClass.PhysicalCores) == cpu.PhysicalCores,
            "Every physical core belongs to one efficiency class.");
        Assert(cpu.Smt != SmtState.Unknown && cpu.Hybrid != HybridTopology.Unknown, "SMT and hybrid state come from the same topology reading.");
        if (cpu.Architecture == CpuArchitecture.X64 && !HostCpuAnalyzer.IsEmulated(info.OsArchitecture, info.ProcessArchitecture))
        {
            Assert(cpu.PresenceOf(CpuFeature.Sse41) != CpuFeaturePresence.Unknown, "A native x64 process reads CPUID.");
            Assert(cpu.Brand is not null && cpu.Brand == cpu.Brand.Trim() && !cpu.Brand.Contains("  ", StringComparison.Ordinal),
                "The brand string is normalized the way RPCS3 normalizes it.");
            Assert(cpu.SchedulerLayout != Rpcs3SchedulerLayout.Unknown, "The scheduler layout is derived on a native x64 host.");
        }

        Console.WriteLine("Host CPU real-host invariants PASS.");
    }

    private static void AssertArchitectureMapping()
    {
        Assert(HostCpuAnalyzer.MapArchitecture("X64") == CpuArchitecture.X64 &&
               HostCpuAnalyzer.MapArchitecture("x64") == CpuArchitecture.X64 &&
               HostCpuAnalyzer.MapArchitecture("Arm64") == CpuArchitecture.Arm64 &&
               HostCpuAnalyzer.MapArchitecture("arm64") == CpuArchitecture.Arm64,
            "x64 and ARM64 map, case-insensitively (the RPCS3 log prints lower case).");
        Assert(HostCpuAnalyzer.MapArchitecture("X86") == CpuArchitecture.Unsupported &&
               HostCpuAnalyzer.MapArchitecture("Arm") == CpuArchitecture.Unsupported &&
               HostCpuAnalyzer.MapArchitecture("unknown") == CpuArchitecture.Unsupported,
            "Architectures without an RPCS3 backend are Unsupported, never mapped to the nearest one.");
        Assert(HostCpuAnalyzer.MapArchitecture(null) == CpuArchitecture.Unknown &&
               HostCpuAnalyzer.MapArchitecture("  ") == CpuArchitecture.Unknown,
            "A missing architecture is Unknown.");
        Assert(HostCpuAnalyzer.IsEmulated("Arm64", "X64") && HostCpuAnalyzer.IsEmulated("Arm64", "X86") &&
               !HostCpuAnalyzer.IsEmulated("Arm64", "Arm64") && !HostCpuAnalyzer.IsEmulated("X64", "X86") &&
               !HostCpuAnalyzer.IsEmulated("X64", "X64"),
            "Only a non-ARM64 process on ARM64 Windows is emulated; WOW64 x86 on x64 reads the real CPU.");
    }

    private static void AssertIntelLikeFixture()
    {
        var hybridCores = new List<CpuCoreRecord>();
        hybridCores.AddRange(Cores(8, 2, 1, 1));
        hybridCores.AddRange(Cores(8, 1, 0, 0));
        var cpu = HostCpuAnalyzer.Analyze(X64(IntelLike("  12th Gen   Intel(R) Core(TM) i9  "), hybridCores, 24));

        Assert(cpu.Architecture == CpuArchitecture.X64 && cpu.Brand == "12th Gen Intel(R) Core(TM) i9",
            "The CPUID brand is trimmed and runs of spaces collapse, as sysinfo.cpp get_cpu_brand does.");
        Assert(cpu.PresenceOf(CpuFeature.Ssse3) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Sse41) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Avx) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Avx2) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Fma3) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Clwb) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Erms) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Fsrm) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Waitpkg) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.InvariantTsc) == CpuFeaturePresence.Present,
            "Present features come from the CPUID bits RPCS3 tests.");
        Assert(cpu.PresenceOf(CpuFeature.Avx512) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Avx512Icl) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Fma4) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Xop) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Waitx) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Rtm) == CpuFeaturePresence.Absent,
            "Features whose bits are clear are Absent.");
        Assert(cpu.Features.All(fact => fact.Source.Length > 0), "Every feature fact carries its provenance.");

        Assert(cpu.PhysicalCores == 16 && cpu.LogicalProcessorsTotal == 24 && cpu.NativeThreadCount == 24 &&
               cpu.Smt == SmtState.Active && cpu.Hybrid == HybridTopology.Heterogeneous &&
               cpu.CoreClasses.Count == 2 && cpu.CoreClasses[0] == new CpuCoreClass(0, 8, 8) && cpu.CoreClasses[1] == new CpuCoreClass(1, 8, 16),
            "Physical cores, SMT and the efficiency classes come from the core records.");
        Assert(cpu.SchedulerLayout == Rpcs3SchedulerLayout.IntelHt &&
               cpu.SchedulerEffect == SchedulerAffinityEffect.LayoutSpecific,
            "An Intel brand with an SMT core is intel_ht, and 24 threads reach the alt-mode mask rule.");

        var avx512 = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Xeon(R) Test", avx512: true), Cores(4, 2, 1), 8, xstate: 0xe6));
        Assert(avx512.PresenceOf(CpuFeature.Avx512) == CpuFeaturePresence.Present &&
               avx512.PresenceOf(CpuFeature.Avx512Icl) == CpuFeaturePresence.Absent,
            "AVX-512 needs the CPUID bits and the OS XState; ICL needs its own extra bits.");
        var osDisabled = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Xeon(R) Test", avx512: true), Cores(4, 2, 1), 8, xstate: 0x6));
        Assert(osDisabled.PresenceOf(CpuFeature.Avx512) == CpuFeaturePresence.Absent &&
               osDisabled.PresenceOf(CpuFeature.Avx) == CpuFeaturePresence.Present,
            "A CPU with AVX-512 that the OS does not enable is Absent, like XGETBV in RPCS3.");
        var noAvxOs = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test"), Cores(2, 1, 0), 2, xstate: 0x2));
        Assert(noAvxOs.PresenceOf(CpuFeature.Avx) == CpuFeaturePresence.Absent &&
               noAvxOs.PresenceOf(CpuFeature.Avx2) == CpuFeaturePresence.Absent &&
               noAvxOs.PresenceOf(CpuFeature.Xop) == CpuFeaturePresence.Absent,
            "AVX needs the OS to enable YMM state.");
    }

    private static void AssertAmdLikeFixture()
    {
        var cpu = HostCpuAnalyzer.Analyze(X64(AmdLike("AMD Ryzen 7 5800X 8-Core Processor"), Cores(8, 2, 1), 16));
        Assert(cpu.SchedulerLayout == Rpcs3SchedulerLayout.AmdCcx &&
               cpu.SchedulerEffect == SchedulerAffinityEffect.LayoutSpecific &&
               cpu.Hybrid == HybridTopology.Uniform && cpu.Smt == SmtState.Active,
            "A Ryzen brand is amd_ccx; 16 threads reach the family/model mask rules.");
        Assert(cpu.PresenceOf(CpuFeature.Fma4) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Xop) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Waitx) == CpuFeaturePresence.Present,
            "AMD extended-leaf features come from CPUID 0x80000001.");

        var small = HostCpuAnalyzer.Analyze(X64(AmdLike("AMD Ryzen 5 3500 6-Core Processor"), Cores(6, 1, 0), 6));
        Assert(small.SchedulerLayout == Rpcs3SchedulerLayout.AmdCcx && small.SchedulerEffect == SchedulerAffinityEffect.NoChange &&
               small.Smt == SmtState.Inactive,
            "amd_ccx with 8 threads or fewer returns the whole process mask.");

        var epyc = HostCpuAnalyzer.Analyze(X64(AmdLike("AMD EPYC 7763 64-Core Processor"), Cores(64, 2, 1), 64));
        Assert(epyc.SchedulerLayout == Rpcs3SchedulerLayout.Generic && epyc.SchedulerEffect == SchedulerAffinityEffect.NoChange,
            "Only the Ryzen and Intel brand checks select a layout; every other brand is generic.");
    }

    private static void AssertSchedulerRules()
    {
        var below = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Core(TM) i5"), Cores(4, 2, 1), 8));
        var at = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Core(TM) i7"), Cores(6, 2, 1), 12));
        Assert(below.SchedulerLayout == Rpcs3SchedulerLayout.IntelHt && below.SchedulerEffect == SchedulerAffinityEffect.NoChange &&
               at.SchedulerEffect == SchedulerAffinityEffect.LayoutSpecific,
            "intel_ht narrows the mask only from 12 threads.");

        var intelNoSmt = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Core(TM) i5"), Cores(6, 1, 0), 6));
        Assert(intelNoSmt.SchedulerLayout == Rpcs3SchedulerLayout.Generic && intelNoSmt.SchedulerEffect == SchedulerAffinityEffect.NoChange,
            "An Intel CPU without an SMT core stays generic.");

        var noTopology = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Core(TM) i5"), null, 6));
        Assert(noTopology.SchedulerLayout == Rpcs3SchedulerLayout.Unknown && noTopology.SchedulerEffect == SchedulerAffinityEffect.Unknown &&
               noTopology.Smt == SmtState.Unknown && noTopology.PhysicalCores is null,
            "If the topology query failed, the Intel layout is not guessed.");

        var noThreads = HostCpuAnalyzer.Analyze(X64(AmdLike("AMD Ryzen 9"), Cores(8, 2, 1), null));
        Assert(noThreads.SchedulerLayout == Rpcs3SchedulerLayout.AmdCcx && noThreads.SchedulerEffect == SchedulerAffinityEffect.Unknown &&
               noThreads.NativeThreadCount is null,
            "Without a thread count the affinity effect is Unknown.");

        var grouped = HostCpuAnalyzer.Analyze(X64(AmdLike("AMD Ryzen Threadripper"), Cores(64, 2, 1), 64, groups: 2));
        Assert(grouped.ThreadCountIsPerGroup && grouped.ProcessorGroups == 2,
            "With several processor groups the RPCS3 thread count is flagged as per-group.");
    }

    private static void AssertFailClosed()
    {
        Assert(ReferenceEquals(HostCpuAnalyzer.Analyze(null), HostCpuInfo.Unknown) &&
               HostCpuInfo.Unknown.Architecture == CpuArchitecture.Unknown &&
               HostCpuInfo.Unknown.Features.Count == 0 &&
               HostCpuInfo.Unknown.SchedulerLayout == Rpcs3SchedulerLayout.Unknown &&
               HostCpuInfo.Unknown.Smt == SmtState.Unknown && HostCpuInfo.Unknown.Hybrid == HybridTopology.Unknown,
            "No readings at all is Unknown everywhere.");

        var noCpuid = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("X64", "X64", NativeThreadCount: 8));
        Assert(noCpuid.Features.Count > 0 &&
               noCpuid.Features.All(fact => fact.Presence == CpuFeaturePresence.Unknown && fact.Source == "CpuidUnavailable") &&
               noCpuid.Brand is null && noCpuid.SchedulerLayout == Rpcs3SchedulerLayout.Unknown,
            "Without CPUID every feature is Unknown, not Absent, and no layout is derived.");

        var noXState = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test"), Cores(2, 2, 1), 4, xstate: null));
        Assert(noXState.PresenceOf(CpuFeature.Ssse3) == CpuFeaturePresence.Present &&
               noXState.PresenceOf(CpuFeature.Avx) == CpuFeaturePresence.Unknown &&
               noXState.PresenceOf(CpuFeature.Avx2) == CpuFeaturePresence.Unknown &&
               noXState.PresenceOf(CpuFeature.Avx512) == CpuFeaturePresence.Absent,
            "AVX needs the OS state: without it AVX is Unknown; AVX-512 is still Absent because its CPUID bits are clear.");

        var unsupported = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("X86", "X86", NativeThreadCount: 2));
        Assert(unsupported.Architecture == CpuArchitecture.Unsupported && unsupported.Features.Count == 0 &&
               unsupported.SchedulerLayout == Rpcs3SchedulerLayout.Unknown,
            "An architecture without an RPCS3 backend gets no feature list and no layout.");

        var emulated = HostCpuAnalyzer.Analyze(new HostCpuRawFacts(
            "Arm64", "X64", IntelLike("Intel(R) Emulated").Reader, 0x6, null, 8, 1, Cores(4, 2, 1)));
        Assert(emulated.Architecture == CpuArchitecture.Arm64 && emulated.Brand is null &&
               emulated.Features.All(fact => fact.Presence == CpuFeaturePresence.Unknown && fact.Source == "EmulatedProcess") &&
               emulated.SchedulerLayout == Rpcs3SchedulerLayout.Unknown,
            "An emulated x64 process on ARM64 reports the emulated CPU: nothing is claimed about the host.");

        var badCpuid = HostCpuAnalyzer.Analyze(X64(new Cpuid().Set(0, 0, eax: 0), Cores(2, 1, 0), 2));
        Assert(badCpuid.PresenceOf(CpuFeature.Ssse3) == CpuFeaturePresence.Absent &&
               badCpuid.PresenceOf(CpuFeature.Erms) == CpuFeaturePresence.Absent &&
               badCpuid.Brand is null && badCpuid.SchedulerLayout == Rpcs3SchedulerLayout.Generic,
            "A CPU whose highest leaf is 0 has no tested feature, and no brand leaves is RPCS3's 'Unknown CPU' (generic).");
    }

    private static void AssertArm64()
    {
        var present = new HashSet<int> { ArmProcessorFeature.Neon, ArmProcessorFeature.DotProd, ArmProcessorFeature.I8mm };
        var cpu = HostCpuAnalyzer.Analyze(new HostCpuRawFacts(
            "Arm64", "Arm64", null, null, present.Contains, 12, 1, Cores(12, 1, 0)));
        Assert(cpu.Architecture == CpuArchitecture.Arm64 && cpu.Brand is null,
            "ARM64 is recognized; its brand comes from RPCS3 ARM code and is not reproduced.");
        Assert(cpu.PresenceOf(CpuFeature.Neon) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.DotProd) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.I8mm) == CpuFeaturePresence.Present &&
               cpu.PresenceOf(CpuFeature.Sha3) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Sve) == CpuFeaturePresence.Absent &&
               cpu.PresenceOf(CpuFeature.Sve2) == CpuFeaturePresence.Absent,
            "ARM64 features come from the PF_* ids RPCS3 passes to IsProcessorFeaturePresent.");
        Assert(cpu.Features.All(fact => Rpcs3CpuSemantics.For(fact.Feature)?.Architecture == CpuArchitecture.Arm64) &&
               cpu.PresenceOf(CpuFeature.Avx2) == CpuFeaturePresence.Unknown,
            "An ARM64 host lists only ARM features; x64 features are not reported as missing.");
        Assert(cpu.SchedulerLayout == Rpcs3SchedulerLayout.Unknown && cpu.SchedulerEffect == SchedulerAffinityEffect.Unknown,
            "The ARM64 scheduler layout depends on the ARM brand string and is not derived.");

        var emulated = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("Arm64", "X64", ProcessorFeaturePresent: present.Contains));
        Assert(emulated.Features.All(fact => fact.Presence == CpuFeaturePresence.Unknown),
            "An emulated process cannot read the ARM feature set.");
        Assert(ArmProcessorFeature.Neon == 18 && ArmProcessorFeature.DotProd == 43 && ArmProcessorFeature.Sve == 46 &&
               ArmProcessorFeature.Sve2 == 47 && ArmProcessorFeature.Sha3 == 64 && ArmProcessorFeature.I8mm == 66,
            "The PF_* ids match winnt.h (SDK 10.0.26100.0).");
    }

    private static byte[] CoreRecord(byte flags, byte efficiencyClass, ulong[] masks, int pointerSize = 8)
    {
        var groupSize = pointerSize == 8 ? 16 : 12;
        var size = 32 + (masks.Length * groupSize);
        var record = new byte[size];
        BitConverter.GetBytes(0).CopyTo(record, 0);
        BitConverter.GetBytes((uint)size).CopyTo(record, 4);
        record[8] = flags;
        record[9] = efficiencyClass;
        BitConverter.GetBytes((ushort)masks.Length).CopyTo(record, 30);
        for (var index = 0; index < masks.Length; index++)
        {
            var at = 32 + (index * groupSize);
            var mask = pointerSize == 8 ? BitConverter.GetBytes(masks[index]) : BitConverter.GetBytes((uint)masks[index]);
            mask.CopyTo(record, at);
            BitConverter.GetBytes((ushort)index).CopyTo(record, at + pointerSize);
        }

        return record;
    }

    private static void AssertCoreInformationParser()
    {
        var buffer = CoreRecord(1, 1, [0b11]).Concat(CoreRecord(0, 0, [0b100])).ToArray();
        var cores = HostCpuAnalyzer.ParseProcessorCoreInformation(buffer, 8);
        Assert(cores is { Count: 2 } &&
               cores[0] == new CpuCoreRecord(1, 1, 2) && cores[1] == new CpuCoreRecord(0, 0, 1),
            "The parser reads flags, efficiency class and the logical processors of each core.");

        var multiGroup = HostCpuAnalyzer.ParseProcessorCoreInformation(CoreRecord(1, 0, [0b1, 0b11]), 8);
        Assert(multiGroup is { Count: 1 } && multiGroup[0].LogicalProcessors == 3,
            "Logical processors add up across group masks.");

        var thirtyTwo = HostCpuAnalyzer.ParseProcessorCoreInformation(CoreRecord(1, 0, [0b111], 4), 4);
        Assert(thirtyTwo is { Count: 1 } && thirtyTwo[0].LogicalProcessors == 3, "The 32-bit layout is parsed with its own group size.");

        Assert(HostCpuAnalyzer.ParseProcessorCoreInformation(buffer.AsSpan(0, buffer.Length - 1), 8) is null,
            "A truncated buffer is rejected, not read as fewer cores.");
        var oversized = CoreRecord(0, 0, [1]);
        BitConverter.GetBytes(4096u).CopyTo(oversized, 4);
        Assert(HostCpuAnalyzer.ParseProcessorCoreInformation(oversized, 8) is null, "A record larger than the buffer is rejected.");
        var noGroups = CoreRecord(0, 0, [1]);
        BitConverter.GetBytes((ushort)0).CopyTo(noGroups, 30);
        Assert(HostCpuAnalyzer.ParseProcessorCoreInformation(noGroups, 8) is null, "A core with no group mask is rejected.");
        Assert(HostCpuAnalyzer.ParseProcessorCoreInformation([], 8) is { Count: 0 } &&
               HostCpuAnalyzer.ParseProcessorCoreInformation(buffer, 3) is null,
            "An empty buffer has no cores and an invalid pointer size is rejected.");
    }

    private static void AssertRpcs3Applicability()
    {
        var x64 = Rpcs3CpuSemantics.SpuDecoders(CpuArchitecture.X64);
        var arm = Rpcs3CpuSemantics.SpuDecoders(CpuArchitecture.Arm64);
        Assert(x64.Count == 4 &&
               x64.Single(decoder => decoder.Decoder == "Interpreter (static)").Availability == DecoderAvailability.Available &&
               x64.Single(decoder => decoder.Decoder == "Recompiler (ASMJIT)").Availability == DecoderAvailability.Available,
            "x64 source defines the static interpreter and ASMJIT without an LLVM dependency (SPUCommonRecompiler.cpp creates ASMJIT under ARCH_X64).");
        Assert(arm.Count == 4 &&
               arm.Single(decoder => decoder.Decoder == "Recompiler (ASMJIT)").Availability == DecoderAvailability.ReplacedAtStart &&
               arm.Single(decoder => decoder.Decoder == "Interpreter (static)").Availability == DecoderAvailability.Available,
            "ARM64 replaces the ASMJIT SPU decoder at start (System.cpp fixup_settings).");
        Assert(Rpcs3CpuSemantics.SpuDecoders(CpuArchitecture.Unknown).Count == 0 &&
               Rpcs3CpuSemantics.SpuDecoders(CpuArchitecture.Unsupported).Count == 0,
            "No decoder claim is made for an unknown or unsupported architecture.");

        var withFma = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test"), Cores(2, 2, 1), 4));
        var withoutFma = HostCpuAnalyzer.Analyze(X64(new Cpuid().Set(0, 0, eax: MaxLeaf).Set(1, 0, ecx: Ssse3).Brand("Test"), Cores(2, 1, 0), 2));
        var unknownFma = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("X64", "X64"));
        Assert(Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.X64, withFma) == true &&
               Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.X64, withoutFma) == false &&
               Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.X64, unknownFma) is null &&
               Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.X64, null) is null,
            "The DFMA checkbox lock follows has_fma3 || has_fma4, and Unknown stays Unknown.");
        Assert(Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.Arm64, null) == true,
            "On ARM64 has_fma3 is the constant true, so the checkbox is locked whatever the host reading.");
        var armHost = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("Arm64", "Arm64", ProcessorFeaturePresent: _ => true));
        Assert(Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.X64, armHost) is null &&
               Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.Unknown, withFma) is null &&
               Rpcs3CpuSemantics.AccurateDfmaUiLocked(CpuArchitecture.Unsupported, withFma) is null,
            "An x64 RPCS3 on an ARM64 host, or an unknown RPCS3 architecture, is not proven either way.");

        Assert(Enum.GetValues<CpuFeature>().All(feature => Rpcs3CpuSemantics.Features.Count(entry => entry.Feature == feature) == 1),
            "Every probed feature has exactly one RPCS3 semantics entry.");
        Assert(Rpcs3CpuSemantics.Features.All(entry => entry.Probe.Contains("rpcs3/util/sysinfo.cpp", StringComparison.Ordinal) &&
                                                       entry.Consumers.Length > 0 && entry.Use != Rpcs3FeatureUse.None),
            "Every feature names its RPCS3 probe and consumers.");
        Assert(Rpcs3CpuSemantics.For(CpuFeature.Rtm)!.Use == Rpcs3FeatureUse.BannerOnly &&
               Rpcs3CpuSemantics.For(CpuFeature.Fma3)!.Use.HasFlag(Rpcs3FeatureUse.UiGate),
            "TSX (RTM) has no execution consumer in the frozen source; FMA gates a UI option.");
        Assert(Rpcs3CpuSemantics.FrozenHead.Length == 40 && Rpcs3CpuSemantics.FrozenHead.All(Uri.IsHexDigit),
            "The catalog names the frozen RPCS3 commit it was audited against.");
        var analyzed = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test"), Cores(2, 2, 1), 4)).Features.Select(fact => fact.Feature);
        Assert(analyzed.All(feature => Rpcs3CpuSemantics.For(feature)!.Architecture == CpuArchitecture.X64),
            "An x64 analysis reports only x64 features.");
    }

    private static HostCpuRawFacts IntelRaw() =>
        X64(IntelLike("Intel(R) Core(TM) i9"), Cores(8, 2, 1), 16);

    private static HwInfo Probe(HostCpuRawFacts? raw, string osArchitecture = "X64", string processArchitecture = "X64") =>
        new HwProbe(
            readCpuName: () => "Test CPU 9000",
            readLogicalProcessors: () => 16,
            readTotalMemoryBytes: () => 32UL * 1024 * 1024 * 1024,
            readGpuName: () => "Test GPU",
            readOs: () => ("Windows 10.0.26200", "26200", osArchitecture, processArchitecture),
            readHostCpu: raw is null ? null : () => raw).Probe();

    private static void AssertProbeInjection()
    {
        Assert(Probe(null).Cpu is null,
            "A synthetic machine without an injected host CPU has no CPU context and never reads the test host.");
        var probed = Probe(IntelRaw());
        Assert(probed.Cpu is { Architecture: CpuArchitecture.X64, PhysicalCores: 8 } &&
               probed.Cpu.PresenceOf(CpuFeature.Avx2) == CpuFeaturePresence.Present,
            "An injected host CPU reading is analyzed into HwInfo.Cpu.");
        Assert(HwProbe.Unknown("x").Cpu is null, "A failed probe carries no CPU evidence.");
    }

    private static Rpcs3BuildIdentity Build(string? architecture) => new(
        "0.0.41", 19000, "89abcdef0123456789abcdef0123456789abcdef", "Alpha", "master", architecture,
        Rpcs3BuildIdentityState.Exact, []);

    private static void AssertRuntimeContextIntegration()
    {
        var hardware = Probe(IntelRaw());
        var snapshot = RuntimeContextBuilder.Build(hardware, null, Build("x64"), null, null, null);
        RuntimeFact Get(string key) => snapshot.Find(key) ?? throw new InvalidOperationException(key);

        Assert(Get("CpuArchitecture").Value == "x64" && Get("CpuArchitecture").State == EvidenceState.Known &&
               Get("CpuPhysicalCores").Value == "8" && Get("CpuNativeThreads").Value == "16" &&
               Get("CpuSmt").Code == "SmtActive" && Get("CpuHybrid").Code == "HybridUniform",
            "The hardware group carries architecture, cores, SMT, hybrid state and the RPCS3 thread count.");
        Assert(Get("CpuFeatures").State == EvidenceState.Known && Get("CpuFeatures").Value!.Contains("AVX2", StringComparison.Ordinal) &&
               !Get("CpuFeatures").Value!.Contains("AVX-512", StringComparison.Ordinal),
            "Features list only what is present; a fully read set carries no state badge.");
        Assert(Get("Rpcs3SchedulerLayout").Value == "intel_ht" && Get("Rpcs3SchedulerLayout").Code == "SchedulerLayoutSpecific",
            "The scheduler layout is shown as RPCS3's own layout name with its effect.");
        Assert(Get("BuildArchitectureMatch").State == EvidenceState.Known && Get("BuildArchitectureMatch").Code == "ArchitectureMatches" &&
               Get("Rpcs3SpuDecoders").Value!.Contains("Recompiler (ASMJIT)", StringComparison.Ordinal) &&
               Get("Rpcs3AccurateDfmaUi").Code == "DfmaUiLocked",
            "RPCS3-side facts follow the logged RPCS3 architecture.");

        var hybrid = new List<CpuCoreRecord>();
        hybrid.AddRange(Cores(4, 2, 1, 1));
        hybrid.AddRange(Cores(8, 1, 0, 0));
        var hybridSnapshot = RuntimeContextBuilder.Build(
            Probe(X64(IntelLike("Intel(R) Core(TM) Test"), hybrid, 16, groups: 2)), null, null, null, null, null);
        Assert(hybridSnapshot.Find("CpuHybrid")!.Code == "HybridNoRpcs3Handling" &&
               hybridSnapshot.Find("CpuHybrid")!.Value == "8 cores (class 0) + 4 cores (class 1)" &&
               hybridSnapshot.Find("CpuNativeThreads")!.State == EvidenceState.Partial &&
               hybridSnapshot.Find("CpuNativeThreads")!.Code == "ThreadCountIsPerGroup",
            "Hybrid topology lists the classes without naming P or E cores; several groups make the thread count Partial.");

        var arm = RuntimeContextBuilder.Build(
            Probe(new HostCpuRawFacts("Arm64", "Arm64", ProcessorFeaturePresent: id => id == ArmProcessorFeature.Neon, NativeThreadCount: 8), "Arm64", "Arm64"),
            null, Build("arm64"), null, null, null);
        Assert(arm.Find("Rpcs3SpuDecoders")!.Code == "AsmjitReplacedOnArm64" &&
               !arm.Find("Rpcs3SpuDecoders")!.Value!.Contains("ASMJIT", StringComparison.Ordinal) &&
               arm.Find("Rpcs3AccurateDfmaUi")!.Code == "DfmaUiLocked" &&
               arm.Find("CpuFeatures")!.Value == "NEON" &&
               arm.Find("BuildArchitectureMatch")!.State == EvidenceState.Known,
            "An ARM64 RPCS3 build drops ASMJIT from the SPU decoder list and locks the DFMA option.");

        var translated = RuntimeContextBuilder.Build(
            Probe(new HostCpuRawFacts("Arm64", "Arm64", ProcessorFeaturePresent: _ => true), "Arm64", "Arm64"),
            null, Build("x64"), null, null, null);
        Assert(translated.Find("BuildArchitectureMatch")!.State == EvidenceState.Partial &&
               translated.Find("BuildArchitectureMatch")!.Code == "ArchitectureDiffers" &&
               translated.Find("Rpcs3AccurateDfmaUi")!.State == EvidenceState.Unknown,
            "An x64 RPCS3 on an ARM64 host is flagged, and what RPCS3 sees of the CPU is not claimed.");

        var emulatedSnapshot = RuntimeContextBuilder.Build(
            Probe(new HostCpuRawFacts("Arm64", "X64", IntelLike("Intel(R) Emulated").Reader, 0x6, null, 8, 1, Cores(4, 2, 1)), "Arm64", "X64"),
            null, null, null, null, null);
        Assert(emulatedSnapshot.Find("CpuFeatures")!.State == EvidenceState.Unknown &&
               emulatedSnapshot.Find("CpuFeatures")!.Code == "CpuEmulatedProcess" &&
               emulatedSnapshot.Find("CpuArchitecture")!.Code == "CpuEmulatedProcess",
            "An emulated R3Config process never presents emulated CPUID as host features.");

        var noBuild = RuntimeContextBuilder.Build(hardware, null, null, null, null, null);
        var noArch = RuntimeContextBuilder.Build(hardware, null, Build(null), null, null, null);
        Assert(noBuild.Find("Rpcs3SpuDecoders")!.State == EvidenceState.Unavailable &&
               noArch.Find("Rpcs3SpuDecoders")!.State == EvidenceState.Unknown &&
               noArch.Find("Rpcs3SpuDecoders")!.Code == "BuildArchitectureNotLogged" &&
               noArch.Find("BuildArchitectureMatch")!.State == EvidenceState.Unknown,
            "Without a logged RPCS3 architecture nothing about RPCS3 decoders is claimed, and the CPU is never used as a substitute.");

        var unprobed = RuntimeContextBuilder.Build(Probe(null), null, null, null, null, null);
        var unscanned = RuntimeContextBuilder.Build(null, null, null, null, null, null);
        Assert(unprobed.Find("CpuFeatures")!.State == EvidenceState.Unknown && unprobed.Find("CpuFeatures")!.Code == "CpuNotProbed" &&
               unscanned.Find("CpuFeatures")!.State == EvidenceState.Unavailable,
            "An unprobed CPU is Unknown and an unscanned machine is Unavailable.");

        AssertLocalizationAndView(
            [snapshot, hybridSnapshot, arm, translated, emulatedSnapshot, noBuild, noArch, unprobed, unscanned]);
    }

    private static void AssertLocalizationAndView(IReadOnlyList<RuntimeContextSnapshot> snapshots)
    {
        var root = FindRepoRoot();
        var english = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, "App", "Lang", "en-US.json")))!;
        var arabic = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, "App", "Lang", "ar-SA.json")))!;
        foreach (var fact in snapshots.SelectMany(snapshot => snapshot.Facts))
        {
            foreach (var map in new[] { english, arabic })
            {
                Assert(map.ContainsKey($"Runtime.Fact.{fact.Key}") &&
                       (fact.Code is null || map.ContainsKey($"Runtime.Code.{fact.Code}")) &&
                       map.ContainsKey($"Runtime.State.{fact.State}"),
                    $"Fact {fact.Key}/{fact.Code} needs English and Arabic resources.");
            }
        }

        var newKeys = english.Keys.Where(key => key.StartsWith("Runtime.Fact.Cpu", StringComparison.Ordinal) ||
                                                key.StartsWith("Runtime.Fact.Rpcs3", StringComparison.Ordinal) ||
                                                key == "Runtime.Fact.BuildArchitectureMatch").ToArray();
        Assert(newKeys.Length >= 10 && newKeys.All(key => arabic[key].Length > 0 && !arabic[key].Any(IsArabicDiacritic)),
            "The new CPU resources exist in Arabic without diacritics.");

        var vm = new R3Config.Vm.RuntimeVm();
        try
        {
            UiText.SetLang(UiText.EnLang);
            vm.Load(snapshots[0]);
            var hardware = vm.Groups.Single(group => group.Group == RuntimeFactGroup.Hardware);
            var build = vm.Groups.Single(group => group.Group == RuntimeFactGroup.Build);
            Assert(hardware.DetailRows.Any(row => row.Id == "CpuFeatures") && hardware.SummaryRows.All(row => row.Id != "CpuFeatures") &&
                   build.DetailRows.Any(row => row.Id == "Rpcs3SpuDecoders") && build.SummaryRows.All(row => row.Id != "Rpcs3SpuDecoders"),
                "The CPU facts sit behind the Details expander so the summary stays uncluttered.");
            var features = hardware.DetailRows.Single(row => row.Id == "CpuFeatures");
            Assert(features.IsTechnical && features.Label == "Tracked RPCS3 CPU features (not a complete list)" && !features.HasState,
                "The feature list is a technical value whose label does not claim completeness.");
            UiText.SetLang(UiText.ArabLang);
            vm.SetLang();
            var arabicHardware = vm.Groups.Single(group => group.Group == RuntimeFactGroup.Hardware);
            var arabicRows = arabicHardware.DetailRows.Concat(arabicHardware.SummaryRows).ToArray();
            Assert(arabicRows.Single(row => row.Id == "CpuSmt").Value.StartsWith("مفعل", StringComparison.Ordinal) &&
                   arabicRows.Single(row => row.Id == "CpuFeatures").Value == features.Value &&
                   arabicRows.All(row => !row.Label.StartsWith("Runtime.", StringComparison.Ordinal) &&
                                         !row.Value.StartsWith("Runtime.", StringComparison.Ordinal)),
                "Arabic localizes the descriptions while technical values keep Western digits and Latin text.");
        }
        finally
        {
            UiText.SetLang(UiText.EnLang);
        }
    }

    private static void AssertLlvmBuildCapabilityFailsClosed()
    {
        foreach (var architecture in new[] { CpuArchitecture.X64, CpuArchitecture.Arm64 })
        {
            var unknown = Rpcs3CpuSemantics.SpuDecoders(architecture);
            var llvm = unknown.Where(decoder => decoder.Decoder is "Recompiler (LLVM)" or "Interpreter (dynamic)").ToArray();
            Assert(llvm.Length == 2 && llvm.All(decoder => decoder.Availability == DecoderAvailability.BuildCapabilityUnknown),
                $"{architecture}: LLVM-dependent SPU decoders stay BuildCapabilityUnknown without evidence of the installed build.");
            Assert(Rpcs3CpuSemantics.SpuDecoders(architecture, LlvmBuildCapability.Present)
                       .Where(decoder => decoder.Decoder is "Recompiler (LLVM)" or "Interpreter (dynamic)")
                       .All(decoder => decoder.Availability == DecoderAvailability.Available) &&
                   Rpcs3CpuSemantics.SpuDecoders(architecture, LlvmBuildCapability.Absent)
                       .Where(decoder => decoder.Decoder is "Recompiler (LLVM)" or "Interpreter (dynamic)")
                       .All(decoder => decoder.Availability == DecoderAvailability.NotAvailable),
                $"{architecture}: only proven build evidence changes the LLVM-dependent decoders.");
        }

        foreach (var (arch, hostRaw, hostOs) in new (string, HostCpuRawFacts, string)[]
                 {
                     ("x64", IntelRaw(), "X64"),
                     ("arm64", new HostCpuRawFacts("Arm64", "Arm64", ProcessorFeaturePresent: _ => true, NativeThreadCount: 8), "Arm64")
                 })
        {
            var snapshot = RuntimeContextBuilder.Build(Probe(hostRaw, hostOs, hostOs), null, Build(arch), null, null, null);
            var listed = snapshot.Find("Rpcs3SpuDecoders")!;
            var llvmFact = snapshot.Find("Rpcs3SpuDecodersLlvm")!;
            Assert(!listed.Value!.Contains("LLVM", StringComparison.Ordinal) && !listed.Value.Contains("dynamic", StringComparison.Ordinal) &&
                   llvmFact.State == EvidenceState.Unknown && llvmFact.Code == "LlvmBuildNotProven" &&
                   llvmFact.Value == "Interpreter (dynamic), Recompiler (LLVM)",
                $"{arch}: the Runtime Context never lists an LLVM decoder as available; it lists them as unproven.");
        }
    }

    private static void AssertFeatureProvenanceAndCoverage()
    {
        var host = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test"), Cores(2, 2, 1), 4));
        Assert(host.Features.Single(fact => fact.Feature == CpuFeature.Ssse3).Basis == CpuFeatureBasis.SourceRuleCpuid &&
               host.Features.Single(fact => fact.Feature == CpuFeature.Avx2).Basis == CpuFeatureBasis.SourceRuleCpuidWithHostXState &&
               host.Features.Single(fact => fact.Feature == CpuFeature.Avx2).Source.Contains("OsXState", StringComparison.Ordinal) &&
               !host.Features.Any(fact => fact.Source.Contains("XCR0", StringComparison.Ordinal) || fact.Source.Contains("XGETBV", StringComparison.Ordinal)),
            "AVX-class features name the Windows XState reading, never XGETBV/XCR0 as if RPCS3's instruction were executed.");
        var arm = HostCpuAnalyzer.Analyze(new HostCpuRawFacts("Arm64", "Arm64", ProcessorFeaturePresent: _ => false));
        Assert(arm.Features.All(fact => fact.Basis == CpuFeatureBasis.SourceRuleHostApi), "ARM64 features come from the processor-feature API.");

        var noXState = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test", avx512: true), Cores(2, 2, 1), 4, xstate: null));
        Assert(new[] { CpuFeature.Avx, CpuFeature.Avx2, CpuFeature.Avx512 }
                   .All(feature => noXState.PresenceOf(feature) == CpuFeaturePresence.Unknown),
            "With no XState answer every AVX-class feature fails closed to Unknown, even when CPUID reports the bits.");

        var vbmiEcx = IntelLike("Intel(R) Test", avx512: true)
            .Set(7, 0, ebx: 0x20 | 0x200 | 0x1000000 | 0xd0030000u, ecx: 0x22, edx: 0x10)
            .Set(0x80000001, 0, ecx: 0x40);
        var fast = HostCpuAnalyzer.Analyze(X64(vbmiEcx, Cores(2, 2, 1), 4, xstate: 0xe6));
        var noSse4a = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Test", avx512: true).Set(7, 0, ebx: 0xd0030000u, ecx: 0x22), Cores(2, 2, 1), 4, xstate: 0xe6));
        var noOs = HostCpuAnalyzer.Analyze(X64(vbmiEcx, Cores(2, 2, 1), 4, xstate: null));
        Assert(fast.PresenceOf(CpuFeature.FastVperm2b) == CpuFeaturePresence.Present &&
               noSse4a.PresenceOf(CpuFeature.FastVperm2b) == CpuFeaturePresence.Absent &&
               noOs.PresenceOf(CpuFeature.FastVperm2b) == CpuFeaturePresence.Unknown,
            "has_fast_vperm2b is AVX-512 with VBMI and SSE4A; without the OS state it is Unknown.");
        Assert(Rpcs3CpuSemantics.For(CpuFeature.FastVperm2b)!.Use == Rpcs3FeatureUse.ExecutionPath &&
               Rpcs3CpuSemantics.For(CpuFeature.FastVperm2b)!.Consumers.Contains("CPUTranslator.h", StringComparison.Ordinal),
            "The derived VPERM2B capability has a proven execution consumer (CPUTranslator.h vperm2b).");

        var rtm = Rpcs3CpuSemantics.For(CpuFeature.Rtm)!;
        Assert(rtm.Use == Rpcs3FeatureUse.BannerOnly && !rtm.Use.HasFlag(Rpcs3FeatureUse.ExecutionPath) &&
               Rpcs3CpuSemantics.DisplayName(CpuFeature.Rtm).Contains("banner only", StringComparison.Ordinal),
            "RTM is detected but classified BannerOnly: no execution consumer is proven, and its label says so.");
        Assert(Rpcs3CpuSemantics.Features.Where(entry => entry.Use.HasFlag(Rpcs3FeatureUse.ExecutionPath)).All(entry => entry.Feature != CpuFeature.Rtm),
            "No TSX execution capability is claimed.");

        Assert(Rpcs3CpuSemantics.UntrackedProbes.Count > 0 &&
               Rpcs3CpuSemantics.UntrackedProbes.All(entry => Rpcs3CpuSemantics.Features.All(tracked => !tracked.Probe.StartsWith(entry.Probe, StringComparison.Ordinal))),
            "Probes that are not modeled are listed as untracked, so the tracked list never claims to be complete.");
        var english = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "App", "Lang", "en-US.json")))!;
        Assert(english["Runtime.Fact.CpuFeatures"].Contains("Tracked", StringComparison.Ordinal) &&
               english["Runtime.Fact.CpuFeatures"].Contains("not a complete list", StringComparison.Ordinal),
            "The feature row label says the list is tracked, not complete.");
    }

    private static void AssertHybridAndSchedulerAreSeparate()
    {
        var cores = new List<CpuCoreRecord>();
        cores.AddRange(Cores(6, 2, 1, 1));
        cores.AddRange(Cores(4, 1, 0, 0));
        var raw = X64(IntelLike("Intel(R) Core(TM) i5"), cores, 16);
        var cpu = HostCpuAnalyzer.Analyze(raw);
        Assert(cpu.Hybrid == HybridTopology.Heterogeneous && cpu.SchedulerLayout == Rpcs3SchedulerLayout.IntelHt &&
               cpu.PhysicalCores == 10 && cpu.LogicalProcessorsTotal == 16 && cpu.Smt == SmtState.Active,
            "A hybrid Intel host is Heterogeneous and intel_ht at once: the layout is RPCS3's brand+SMT classification, not a topology claim.");

        var snapshot = RuntimeContextBuilder.Build(Probe(raw), null, null, null, null, null);
        var hybrid = snapshot.Find("CpuHybrid")!;
        var layout = snapshot.Find("Rpcs3SchedulerLayout")!;
        Assert(hybrid.Value == "4 cores (class 0) + 6 cores (class 1)" && layout.Value == "intel_ht" &&
               !System.Text.RegularExpressions.Regex.IsMatch(hybrid.Value, "P[- ]?core|E[- ]?core|performance|efficient", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            "Efficiency classes are shown as numbers; they are never relabeled P-core or E-core.");

        var english = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "App", "Lang", "en-US.json")))!;
        var arabic = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "App", "Lang", "ar-SA.json")))!;
        Assert(english["Runtime.Fact.Rpcs3SchedulerLayout"].Contains("classification", StringComparison.Ordinal) &&
               english["Runtime.Code.SchedulerLayoutSpecific"].Contains("not a description of the core layout", StringComparison.Ordinal) &&
               english["Runtime.Code.SchedulerNoAffinityChange"].Contains("not a description of the core layout", StringComparison.Ordinal),
            "The scheduler row says it is RPCS3's classification, not the topology.");
        var forbidden = new[] { "أفضل", "موصى", "يوصى", "توصية", "Recommended", "recommended", "best", "optimal", "Optimal" };
        var cpuKeys = english.Keys.Where(key => key.StartsWith("Runtime.Fact.Cpu", StringComparison.Ordinal) ||
                                                key.StartsWith("Runtime.Fact.Rpcs3", StringComparison.Ordinal) ||
                                                key.StartsWith("Runtime.Fact.BuildArchitectureMatch", StringComparison.Ordinal) ||
                                                key.Contains("Scheduler", StringComparison.Ordinal) || key.Contains("Hybrid", StringComparison.Ordinal) ||
                                                key.Contains("Smt", StringComparison.Ordinal) || key.Contains("Dfma", StringComparison.Ordinal) ||
                                                key.Contains("Llvm", StringComparison.Ordinal) || key.Contains("Cpu", StringComparison.Ordinal)).ToArray();
        Assert(cpuKeys.Length > 20 && cpuKeys.All(key => !forbidden.Any(word => english[key].Contains(word, StringComparison.Ordinal) ||
                                                                              arabic[key].Contains(word, StringComparison.Ordinal))),
            "CPU texts carry no recommendation wording.");
        Assert(cpuKeys.All(key => !english[key].Contains("current", StringComparison.OrdinalIgnoreCase) &&
                                  !english[key].Contains("active decoder", StringComparison.OrdinalIgnoreCase)) ,
            "CPU texts never claim to describe the user's current setting.");
    }

    private static void AssertNoHeuristicLeakage()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            Path.Combine("Core", "Mdl", "HostCpu.cs"), Path.Combine("Core", "Mdl", "Rpcs3Cpu.cs"),
            Path.Combine("Core", "Svc", "Hw", "HostCpuAnalyzer.cs"), Path.Combine("Core", "Svc", "Hw", "HwProbe.Cpu.cs"),
            Path.Combine("Core", "Svc", "Runtime", "Rpcs3CpuSemantics.cs"), Path.Combine("Core", "Svc", "Runtime", "RuntimeContextBuilder.cs")
        };
        foreach (var file in files)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert(!text.Contains("Tier", StringComparison.Ordinal) && !text.Contains("Recommend", StringComparison.OrdinalIgnoreCase),
                $"{file} must not read HwTier or recommend anything.");
        }

        var missing = HostCpuAnalyzer.Analyze(X64(IntelLike("Intel(R) Core(TM) i5"), null, 8));
        var snapshot = RuntimeContextBuilder.Build(Probe(X64(IntelLike("Intel(R) Core(TM) i5"), null, 8)), null, null, null, null, null);
        Assert(missing.PhysicalCores is null && missing.Smt == SmtState.Unknown && missing.Hybrid == HybridTopology.Unknown &&
               missing.CoreClasses.Count == 0 &&
               snapshot.Find("CpuPhysicalCores")!.State == EvidenceState.Unknown && snapshot.Find("CpuPhysicalCores")!.Value is null &&
               snapshot.Find("CpuSmt")!.State == EvidenceState.Unknown && snapshot.Find("CpuHybrid")!.State == EvidenceState.Unknown,
            "A missing topology is Unknown everywhere: no fabricated false, zero or 'uniform'.");

        var wide = new List<CpuCoreRecord>();
        wide.AddRange(Cores(12, 2, 1));
        var counted = RuntimeContextBuilder.Build(Probe(X64(IntelLike("Intel(R) Test"), wide, 16)), null, null, null, null, null);
        Assert(counted.Find("CpuThreads")!.Value == "24" && counted.Find("CpuNativeThreads")!.Value == "16",
            "The logical-processor row follows the OS topology; the RPCS3 thread count is a separate, labeled fact.");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "R3Cfg.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static bool IsArabicDiacritic(char value) => value is >= 'ً' and <= 'ٟ' or 'ٰ';
}
