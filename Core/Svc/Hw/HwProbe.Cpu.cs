using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using R3Config.Mdl;

namespace R3Config.Svc;

// Native CPU readings for the Runtime Context. They mirror what RPCS3 itself
// reads (CPUID, GetNativeSystemInfo, GetLogicalProcessorInformationEx,
// IsProcessorFeaturePresent) so the analyzer can apply RPCS3's own rules.
// A reading that fails becomes null in HostCpuRawFacts, never a default.
public sealed partial class HwProbe
{
    private const int RelationProcessorCore = 0;

    private const int MaxCoreInformationBytes = 1 << 20;

    private static HostCpuRawFacts? ReadHostCpu()
    {
        var (_, _, osArchitecture, processArchitecture) = ReadOs();
        if (!OperatingSystem.IsWindows())
        {
            return new HostCpuRawFacts(osArchitecture, processArchitecture);
        }

        return new HostCpuRawFacts(
            osArchitecture,
            processArchitecture,
            Cpuid: X86Base.IsSupported ? ReadCpuid : null,
            EnabledXState: X86Base.IsSupported ? GuardValue(() => (ulong?)GetEnabledXStateFeatures()) : null,
            ProcessorFeaturePresent: feature => IsProcessorFeaturePresent((uint)feature),
            NativeThreadCount: GuardValue(ReadNativeThreadCount),
            ProcessorGroups: GuardValue(() => (int?)GetActiveProcessorGroupCount()),
            Cores: GuardRef(ReadCores));
    }

    private static CpuidRegisters ReadCpuid(uint leaf, uint subLeaf)
    {
        var (eax, ebx, ecx, edx) = X86Base.CpuId(unchecked((int)leaf), unchecked((int)subLeaf));
        return new CpuidRegisters(unchecked((uint)eax), unchecked((uint)ebx), unchecked((uint)ecx), unchecked((uint)edx));
    }

    private static T? GuardValue<T>(Func<T?> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            return null;
        }
    }

    private static T? GuardRef<T>(Func<T?> read) where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            return null;
        }
    }

    private static bool IsProbeFailure(Exception ex) =>
        ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException or
            InvalidOperationException or NotSupportedException or PlatformNotSupportedException;

    // GetNativeSystemInfo is what utils::get_thread_count() calls: the logical
    // processors of the current processor group only.
    private static uint? ReadNativeThreadCount()
    {
        var info = new SystemInfo();
        GetNativeSystemInfo(ref info);
        return info.NumberOfProcessors;
    }

    private static IReadOnlyList<CpuCoreRecord>? ReadCores()
    {
        uint length = 0;
        if (GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length) ||
            Marshal.GetLastWin32Error() != ErrorInsufficientBuffer ||
            length == 0 ||
            length > MaxCoreInformationBytes)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
            {
                return null;
            }

            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return HostCpuAnalyzer.ParseProcessorCoreInformation(bytes);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int ErrorInsufficientBuffer = 122;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void GetNativeSystemInfo(ref SystemInfo info);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ushort GetActiveProcessorGroupCount();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ulong GetEnabledXStateFeatures();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessorFeaturePresent(uint processorFeature);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInfo
    {
        public ushort ProcessorArchitecture;
        public ushort Reserved;
        public uint PageSize;
        public IntPtr MinimumApplicationAddress;
        public IntPtr MaximumApplicationAddress;
        public UIntPtr ActiveProcessorMask;
        public uint NumberOfProcessors;
        public uint ProcessorType;
        public uint AllocationGranularity;
        public ushort ProcessorLevel;
        public ushort ProcessorRevision;
    }
}
