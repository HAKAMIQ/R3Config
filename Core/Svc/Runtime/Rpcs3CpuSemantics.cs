using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// RPCS3 CPU behavior that is proven directly from the frozen source and is
// carries no advice. Every entry names the RPCS3 symbol and
// the consuming files so a rule can be re-audited against FrozenHead.
public static class Rpcs3CpuSemantics
{
    public const string FrozenHead = "9e86f165d1711b9429d48b0487e7bc5ba0cc9c6c";

    private const string Sysinfo = "rpcs3/util/sysinfo.cpp";

    public static IReadOnlyList<Rpcs3FeatureSemantics> Features { get; } =
    [
        X64(CpuFeature.Ssse3, "utils::has_ssse3", Rpcs3FeatureUse.ExecutionPath,
            "Emu/Cell/PPUInterpreter.cpp; Emu/Cell/SPUASMJITRecompiler.cpp"),
        X64(CpuFeature.Sse41, "utils::has_sse41", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.MemoryRoutine,
            "Emu/Cell/SPUASMJITRecompiler.cpp; Emu/RSX/Common/BufferUtils.cpp; util/simd.hpp; Utilities/JITASM.cpp"),
        X64(CpuFeature.Avx, "utils::has_avx", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/Cell/PPUThread.cpp; Emu/Cell/SPUThread.cpp; Emu/Cell/SPUASMJITRecompiler.cpp; Utilities/JITLLVM.cpp (jit_compiler::cpu)"),
        X64(CpuFeature.Avx2, "utils::has_avx2", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection | Rpcs3FeatureUse.MemoryRoutine,
            "Emu/Cell/SPUASMJITRecompiler.cpp; Emu/RSX/Common/BufferUtils.cpp; util/simd.hpp; Utilities/JITASM.cpp; Utilities/JITLLVM.cpp"),
        X64(CpuFeature.Avx512, "utils::has_avx512", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/Cell/SPUASMJITRecompiler.cpp; Utilities/JITASM.cpp; Utilities/JITLLVM.cpp (jit_compiler::cpu)"),
        X64(CpuFeature.Avx512Icl, "utils::has_avx512_icl", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection | Rpcs3FeatureUse.MemoryRoutine,
            "Emu/Cell/SPUASMJITRecompiler.cpp; Emu/CPU/CPUTranslator.h; Emu/RSX/Common/BufferUtils.cpp; Emu/RSX/Program/ProgramStateCache.cpp; Utilities/JITASM.cpp; Utilities/JITLLVM.cpp"),
        X64(CpuFeature.FastVperm2b, "utils::has_fast_vperm2b", Rpcs3FeatureUse.ExecutionPath,
            "Emu/CPU/CPUTranslator.h cpu_translator::vperm2b (LLVM translator lowering)"),
        X64(CpuFeature.Fma3, "utils::has_fma3", Rpcs3FeatureUse.UiGate | Rpcs3FeatureUse.BannerOnly,
            "rpcs3qt/settings_dialog.cpp (Use Accurate DFMA checkbox); sysinfo.cpp get_system_info"),
        X64(CpuFeature.Fma4, "utils::has_fma4", Rpcs3FeatureUse.UiGate | Rpcs3FeatureUse.BannerOnly,
            "rpcs3qt/settings_dialog.cpp (Use Accurate DFMA checkbox); sysinfo.cpp get_system_info"),
        X64(CpuFeature.Xop, "utils::has_xop", Rpcs3FeatureUse.ExecutionPath,
            "Emu/Cell/SPUASMJITRecompiler.cpp"),
        X64(CpuFeature.Clwb, "utils::has_clwb", Rpcs3FeatureUse.LlvmTargetSelection,
            "Utilities/JITLLVM.cpp (jit_compiler::cpu: znver1 upgrade)"),
        X64(CpuFeature.Erms, "utils::has_erms", Rpcs3FeatureUse.MemoryRoutine,
            "utils::get_rep_movsb_threshold -> Emu/Cell/SPUThread.cpp s_rep_movsb_threshold"),
        X64(CpuFeature.Fsrm, "utils::has_fsrm", Rpcs3FeatureUse.MemoryRoutine,
            "utils::get_rep_movsb_threshold -> Emu/Cell/SPUThread.cpp s_rep_movsb_threshold"),
        X64(CpuFeature.Waitx, "utils::has_waitx", Rpcs3FeatureUse.WaitPrimitive,
            "util/asm.hpp; utils::has_um_wait / has_appropriate_um_wait -> Emu/Cell/lv2/lv2.cpp, Emu/Cell/SPUThread.cpp"),
        X64(CpuFeature.Waitpkg, "utils::has_waitpkg", Rpcs3FeatureUse.WaitPrimitive,
            "util/asm.hpp; Emu/Cell/lv2/lv2.cpp; Emu/Cell/SPUThread.cpp"),
        X64(CpuFeature.InvariantTsc, "utils::has_invariant_tsc", Rpcs3FeatureUse.Timing,
            "sysinfo.cpp s_tsc_freq calibration -> utils::get_tsc_freq consumers (lv2/sys_time.cpp, SPULLVMRecompiler.cpp, util/asm.hpp)"),
        X64(CpuFeature.Rtm, "utils::has_rtm", Rpcs3FeatureUse.BannerOnly,
            "sysinfo.cpp get_system_info (TSX label); no caller of _xbegin/_xend in the frozen source"),
        Arm(CpuFeature.Neon, "utils::has_neon", Rpcs3FeatureUse.Requirement,
            "sysinfo.cpp get_system_info throws 'Neon support not present' when absent"),
        Arm(CpuFeature.Sha3, "utils::has_sha3", Rpcs3FeatureUse.LlvmTargetSelection,
            "Utilities/JITLLVM.cpp (+sha3/-sha3 target attribute)"),
        Arm(CpuFeature.DotProd, "utils::has_dotprod", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/CPU/CPUTranslator.cpp (m_use_dotprod); Utilities/JITLLVM.cpp"),
        Arm(CpuFeature.I8mm, "utils::has_i8mm", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/CPU/CPUTranslator.cpp (m_use_i8mm); Utilities/JITLLVM.cpp"),
        Arm(CpuFeature.Sve, "utils::has_sve", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/CPU/CPUTranslator.cpp (m_use_sve_128 only when utils::sve_length() == 128); Utilities/JITLLVM.cpp"),
        Arm(CpuFeature.Sve2, "utils::has_sve2", Rpcs3FeatureUse.ExecutionPath | Rpcs3FeatureUse.LlvmTargetSelection,
            "Emu/CPU/CPUTranslator.cpp (m_use_sve2_128 only when utils::sve_length() == 128); Utilities/JITLLVM.cpp")
    ];

    // Probes that exist in sysinfo.cpp and are deliberately not modeled: the
    // tracked feature list is not a complete mirror of RPCS3's probes.
    public static IReadOnlyList<(string Probe, string Reason)> UntrackedProbes { get; } =
    [
        ("utils::has_mpx", "only qualifies the TSX label in the log banner"),
        ("utils::has_tsx_force_abort", "only qualifies the TSX label in the log banner"),
        ("utils::has_rtm_always_abort", "only selects the 'TSX disabled via microcode' banner text"),
        ("utils::has_avx512_vnni", "no consumer in the frozen source outside the probe itself"),
        ("utils::has_avx10 / avx10_isa_version", "only the log banner reads them"),
        ("utils::has_um_wait / has_appropriate_um_wait", "derived from WAITX/WAITPKG, thread count and a measured TSC frequency")
    ];

    private static readonly Dictionary<CpuFeature, string> DisplayNames = new()
    {
        [CpuFeature.Ssse3] = "SSSE3",
        [CpuFeature.Sse41] = "SSE4.1",
        [CpuFeature.Avx] = "AVX",
        [CpuFeature.Avx2] = "AVX2",
        [CpuFeature.Avx512] = "AVX-512",
        [CpuFeature.Avx512Icl] = "AVX-512 ICL",
        [CpuFeature.FastVperm2b] = "Fast VPERM2B",
        [CpuFeature.Fma3] = "FMA3",
        [CpuFeature.Fma4] = "FMA4",
        [CpuFeature.Xop] = "XOP",
        [CpuFeature.Clwb] = "CLWB",
        [CpuFeature.Erms] = "ERMS",
        [CpuFeature.Fsrm] = "FSRM",
        [CpuFeature.Waitx] = "WAITX",
        [CpuFeature.Waitpkg] = "WAITPKG",
        [CpuFeature.InvariantTsc] = "Invariant TSC",
        [CpuFeature.Rtm] = "RTM (banner only)",
        [CpuFeature.Neon] = "NEON",
        [CpuFeature.Sha3] = "SHA3",
        [CpuFeature.DotProd] = "DotProd",
        [CpuFeature.I8mm] = "I8MM",
        [CpuFeature.Sve] = "SVE",
        [CpuFeature.Sve2] = "SVE2"
    };

    public static string DisplayName(CpuFeature feature) =>
        DisplayNames.TryGetValue(feature, out var name) ? name : feature.ToString();

    public static Rpcs3FeatureSemantics? For(CpuFeature feature) =>
        Features.FirstOrDefault(entry => entry.Feature == feature);

    // SPU decoder values the frozen source defines for an RPCS3 architecture.
    // ARM64 has no ASMJIT SPU backend: System.cpp fixup_settings() replaces it
    // at start; SPUCommonRecompiler.cpp only creates the ASMJIT recompiler under
    // ARCH_X64. The LLVM recompiler and the dynamic interpreter need an RPCS3
    // build with LLVM_AVAILABLE (settings_dialog.cpp disables both without it),
    // a compile-time switch of the installed binary. It defaults to Unknown and
    // is never inferred from the host, the architecture or this source tree.
    public static IReadOnlyList<Rpcs3DecoderSupport> SpuDecoders(
        CpuArchitecture rpcs3Architecture,
        LlvmBuildCapability llvm = LlvmBuildCapability.Unknown)
    {
        if (rpcs3Architecture is not (CpuArchitecture.X64 or CpuArchitecture.Arm64))
        {
            return [];
        }

        const string source = "Emu/system_config_types.h spu_decoder_type";
        const string llvmSource = "rpcs3qt/settings_dialog.cpp (disabled without LLVM_AVAILABLE)";
        var llvmAvailability = llvm switch
        {
            LlvmBuildCapability.Present => DecoderAvailability.Available,
            LlvmBuildCapability.Absent => DecoderAvailability.NotAvailable,
            _ => DecoderAvailability.BuildCapabilityUnknown
        };
        var asmjit = rpcs3Architecture == CpuArchitecture.X64
            ? new Rpcs3DecoderSupport("Recompiler (ASMJIT)", DecoderAvailability.Available, source)
            : new Rpcs3DecoderSupport("Recompiler (ASMJIT)", DecoderAvailability.ReplacedAtStart, "Emu/System.cpp fixup_settings (ARCH_ARM64)");
        return
        [
            new("Interpreter (static)", DecoderAvailability.Available, source),
            new("Interpreter (dynamic)", llvmAvailability, llvmSource),
            asmjit,
            new("Recompiler (LLVM)", llvmAvailability, llvmSource)
        ];
    }

    // rpcs3qt/settings_dialog.cpp disables the "Use Accurate DFMA" checkbox
    // when utils::has_fma3() || utils::has_fma4(). On ARM64 has_fma3() is the
    // constant true (sysinfo.cpp). This is a UI lock only; the emulator still
    // reads the value. Null when RPCS3's own CPU view is not proven, which
    // includes an x64 RPCS3 on a host that is not native x64.
    public static bool? AccurateDfmaUiLocked(CpuArchitecture rpcs3Architecture, HostCpuInfo? host)
    {
        if (rpcs3Architecture == CpuArchitecture.Arm64)
        {
            return true;
        }

        if (rpcs3Architecture != CpuArchitecture.X64 || host is null || host.Architecture != CpuArchitecture.X64)
        {
            return null;
        }

        var fma3 = host.PresenceOf(CpuFeature.Fma3);
        var fma4 = host.PresenceOf(CpuFeature.Fma4);
        if (fma3 == CpuFeaturePresence.Present || fma4 == CpuFeaturePresence.Present)
        {
            return true;
        }

        return fma3 == CpuFeaturePresence.Absent && fma4 == CpuFeaturePresence.Absent ? false : null;
    }

    private static Rpcs3FeatureSemantics X64(CpuFeature feature, string probe, Rpcs3FeatureUse use, string consumers) =>
        new(feature, CpuArchitecture.X64, $"{probe} ({Sysinfo})", use, consumers);

    private static Rpcs3FeatureSemantics Arm(CpuFeature feature, string probe, Rpcs3FeatureUse use, string consumers) =>
        new(feature, CpuArchitecture.Arm64, $"{probe} ({Sysinfo})", use, consumers);
}
