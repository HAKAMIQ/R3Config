using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class HwProbe
{
    private const int MaxDisplayDevices = 32;

    private readonly Func<string?> _readNam;
    private readonly Func<int> _readCpu;
    private readonly Func<ulong?> _Bytes;
    private readonly Func<string?> _readNa2;

    public HwProbe(
        Func<string?>? readCpuName = null,
        Func<int>? readLogicalProcessors = null,
        Func<ulong?>? readTotalMemoryBytes = null,
        Func<string?>? readGpuName = null)
    {
        _readNam = readCpuName ?? ReadCpu;
        _readCpu = readLogicalProcessors ?? (() => Environment.ProcessorCount);
        _Bytes = readTotalMemoryBytes ?? Read;
        _readNa2 = readGpuName ?? ReadGpu;
    }

    public HwInfo Probe()
    {
        try
        {
            var cpuName = Norm(_readNam()) ?? "Unknown CPU";
            var logicalProcessors = Math.Max(0, _readCpu());
            var totalMemoryBytes = _Bytes();
            var gpuName = Norm(_readNa2()) ?? "Unknown GPU";
            var tier = Classify(logicalProcessors, totalMemoryBytes, cpuName, gpuName);
            var isFallback = tier == HwTier.Unknown ||
                             cpuName == "Unknown CPU" ||
                             gpuName == "Unknown GPU" ||
                             totalMemoryBytes is null;

            return new HwInfo(
                CpuName: cpuName,
                CpuNum: logicalProcessors,
                MemBytes: totalMemoryBytes,
                GpuName: gpuName,
                Tier: tier,
                IsFall: isFallback,
                ProbeMsg: isFallback
                    ? "Hardware probe completed with partial data."
                    : "Hardware probe completed.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Unknown(ex.Message);
        }
    }

    public static HwInfo Unknown(string message)
    {
        return new HwInfo(
            CpuName: "Unknown CPU",
            CpuNum: 0,
            MemBytes: null,
            GpuName: "Unknown GPU",
            Tier: HwTier.Unknown,
            IsFall: true,
            ProbeMsg: string.IsNullOrWhiteSpace(message) ? "Hardware probe failed safely." : message);
    }

    private static HwTier Classify(
        int logicalProcessors,
        ulong? totalMemoryBytes,
        string cpuName,
        string gpuName)
    {
        if (logicalProcessors <= 0 && totalMemoryBytes is null &&
            cpuName.Equals("Unknown CPU", StringComparison.OrdinalIgnoreCase) &&
            gpuName.Equals("Unknown GPU", StringComparison.OrdinalIgnoreCase))
        {
            return HwTier.Unknown;
        }

        var memoryGiB = totalMemoryBytes is null
            ? 0
            : totalMemoryBytes.Value / 1024d / 1024d / 1024d;

        if (logicalProcessors >= 12 && memoryGiB >= 24)
        {
            return HwTier.High;
        }

        if (logicalProcessors >= 6 && memoryGiB >= 12)
        {
            return HwTier.Mid;
        }

        if (logicalProcessors >= 4 && memoryGiB >= 8 && LooksCpu(cpuName))
        {
            return HwTier.Mid;
        }

        return HwTier.Low;
    }

    private static bool LooksCpu(string cpuName)
    {
        return cpuName.Contains("i5", StringComparison.OrdinalIgnoreCase) ||
               cpuName.Contains("i7", StringComparison.OrdinalIgnoreCase) ||
               cpuName.Contains("i9", StringComparison.OrdinalIgnoreCase) ||
               cpuName.Contains("Ryzen 5", StringComparison.OrdinalIgnoreCase) ||
               cpuName.Contains("Ryzen 7", StringComparison.OrdinalIgnoreCase) ||
               cpuName.Contains("Ryzen 9", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadCpu()
    {
        var processorIdentifier = Norm(Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));

        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416
            var registryValue = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                null) as string;
#pragma warning restore CA1416

            return Norm(registryValue) ?? processorIdentifier;
        }

        return processorIdentifier;
    }

    private static ulong? Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var memoryStatus = new MemoryEx();
        memoryStatus.dwLength = (uint)Marshal.SizeOf<MemoryEx>();
        return GlobalEx(ref memoryStatus)
            ? memoryStatus.ullPhys
            : null;
    }

    private static string? ReadGpu()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string? activeFallback = null;
        string? anyFallback = null;

        for (uint index = 0; index < MaxDisplayDevices; index++)
        {
            var device = new DisplayDevice
            {
                cb = Marshal.SizeOf<DisplayDevice>()
            };

            if (!EnumDisplayDevicesW(
                    lpDevice: null,
                    iDevNum: index,
                    lpDisplayDevice: ref device,
                    dwFlags: 0))
            {
                break;
            }

            var name = Norm(device.DeviceString);
            if (name is null ||
                (device.StateFlags & DisplayDeviceStateFlags.MirroringDriver) != 0)
            {
                continue;
            }

            anyFallback ??= name;

            if ((device.StateFlags & DisplayDeviceStateFlags.Active) == 0)
            {
                continue;
            }

            activeFallback ??= name;

            if ((device.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0)
            {
                return name;
            }
        }

        return activeFallback ?? anyFallback;
    }

    private static string? Norm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        EntryPoint = "GlobalMemoryStatusEx",
        SetLastError = true)]
    private static extern bool GlobalEx(ref MemoryEx buffer);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevicesW(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [Flags]
    private enum DisplayDeviceStateFlags : uint
    {
        Active = 0x00000001,
        MirroringDriver = 0x00000008,
        PrimaryDevice = 0x00000004
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string? DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? DeviceString;

        public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryEx
    {
        public uint dwLength;
        public uint dwLoad;
        public ulong ullPhys;
        public ulong ullPhys2;
        public ulong ullFile;
        public ulong ullFile2;
        public ulong Virtual;
        public ulong Virtual2;
        public ulong Virtual3;
    }
}
