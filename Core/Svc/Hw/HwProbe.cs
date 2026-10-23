using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class HwProbe
{
    private const int MaxDisplayDevices = 32;

    private readonly Func<string?> _readNam;
    private readonly Func<int> _readCpu;
    private readonly Func<ulong?> _Bytes;
    private readonly Func<string?> _readNa2;
    private readonly Func<IReadOnlyList<GpuAdapterInfo>?> _readAdapters;
    private readonly Func<(HwVendor Vendor, string? Id)> _readCpuVendor;
    private readonly Func<(string? Description, string? Build, string? OsArch, string? ProcessArch)> _readOs;
    private readonly Func<HostCpuRawFacts?> _readHostCpu;

    public HwProbe(
        Func<string?>? readCpuName = null,
        Func<int>? readLogicalProcessors = null,
        Func<ulong?>? readTotalMemoryBytes = null,
        Func<string?>? readGpuName = null,
        Func<IReadOnlyList<GpuAdapterInfo>?>? readGpuAdapters = null,
        Func<(HwVendor Vendor, string? Id)>? readCpuVendor = null,
        Func<(string? Description, string? Build, string? OsArch, string? ProcessArch)>? readOs = null,
        Func<HostCpuRawFacts?>? readHostCpu = null)
    {
        _readNam = readCpuName ?? ReadCpu;
        _readCpu = readLogicalProcessors ?? (() => Environment.ProcessorCount);
        _Bytes = readTotalMemoryBytes ?? Read;
        _readNa2 = readGpuName ?? (() => PrimaryName(ReadAdapters()));

        // An injected single GPU name is the only adapter evidence available
        // (its vendor is name-derived); the real probe enumerates every
        // adapter with its PCI vendor ID.
        _readAdapters = readGpuAdapters ??
            (readGpuName is not null
                ? () => NameOnly(readGpuName())
                : ReadAdapters);
        _readCpuVendor = readCpuVendor ??
            (readCpuName is not null
                ? () => (VendorFromName(readCpuName()), null)
                : ReadCpuVendor);
        _readOs = readOs ?? ReadOs;

        // Any injected reading means a synthetic machine: the real CPU is then
        // not read at all, so fixtures never depend on the test host.
        var anyInjected = readCpuName is not null || readLogicalProcessors is not null ||
                          readTotalMemoryBytes is not null || readGpuName is not null ||
                          readGpuAdapters is not null || readCpuVendor is not null || readOs is not null;
        _readHostCpu = readHostCpu ?? (anyInjected ? () => null : ReadHostCpu);
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
            var adapters = _readAdapters() ?? [];
            var (cpuVendor, cpuVendorId) = _readCpuVendor();
            var os = _readOs();
            var hostCpu = _readHostCpu();
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
                    : "Hardware probe completed.",
                CpuVendor: cpuVendor,
                CpuVendorId: cpuVendorId,
                Adapters: adapters,
                OsDescription: Norm(os.Description),
                OsBuild: Norm(os.Build),
                OsArchitecture: Norm(os.OsArch),
                ProcessArchitecture: Norm(os.ProcessArch),
                Cpu: hostCpu is null ? null : HostCpuAnalyzer.Analyze(hostCpu));
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

    // Vendor from a PCI vendor ID (the OS-reported hardware ID), the only
    // reliable adapter vendor evidence. An unknown ID is Other, never a guess.
    public static HwVendor VendorFromPci(string? pciVendorId)
    {
        return pciVendorId?.Trim().ToUpperInvariant() switch
        {
            null or "" => HwVendor.Unknown,
            "10DE" => HwVendor.Nvidia,
            "1002" or "1022" => HwVendor.Amd,
            "8086" => HwVendor.Intel,
            _ => HwVendor.Other
        };
    }

    // Name-derived vendor: only used when no PCI vendor ID is available, and
    // only for the three vendors R3Config has documented conditions for.
    public static HwVendor VendorFromName(string? name)
    {
        var text = (name ?? string.Empty).ToLowerInvariant();
        if (text.Contains("nvidia", StringComparison.Ordinal) ||
            text.Contains("geforce", StringComparison.Ordinal))
        {
            return HwVendor.Nvidia;
        }

        if (text.Contains("amd", StringComparison.Ordinal) ||
            text.Contains("radeon", StringComparison.Ordinal) ||
            text.Contains("ryzen", StringComparison.Ordinal))
        {
            return HwVendor.Amd;
        }

        return text.Contains("intel", StringComparison.Ordinal)
            ? HwVendor.Intel
            : HwVendor.Unknown;
    }

    private static IReadOnlyList<GpuAdapterInfo> NameOnly(string? name)
    {
        var text = Norm(name);
        return text is null
            ? []
            : [new GpuAdapterInfo(text, VendorFromName(text), null, null, IsPrimary: true, IsActive: true)];
    }

    private static string? PrimaryName(IReadOnlyList<GpuAdapterInfo>? adapters)
    {
        if (adapters is null || adapters.Count == 0)
        {
            return null;
        }

        return (adapters.FirstOrDefault(adapter => adapter.IsPrimary) ??
                adapters.FirstOrDefault(adapter => adapter.IsActive) ??
                adapters[0]).Name;
    }

    private static string? PciPart(string? deviceId, string tag)
    {
        if (deviceId is null)
        {
            return null;
        }

        var start = deviceId.IndexOf(tag + "_", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || start + tag.Length + 5 > deviceId.Length)
        {
            return null;
        }

        var value = deviceId.Substring(start + tag.Length + 1, 4).ToUpperInvariant();
        return value.All(Uri.IsHexDigit) ? value : null;
    }

    private static List<GpuAdapterInfo>? ReadAdapters()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var adapters = new List<GpuAdapterInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            var vendorId = PciPart(device.DeviceId, "VEN");
            var deviceId = PciPart(device.DeviceId, "DEV");

            // One physical adapter is enumerated once per attached output.
            if (!seen.Add(vendorId is null || deviceId is null ? name : $"{vendorId}:{deviceId}:{name}"))
            {
                continue;
            }

            var vendor = vendorId is not null ? VendorFromPci(vendorId) : VendorFromName(name);
            adapters.Add(new GpuAdapterInfo(
                name,
                vendor,
                vendorId,
                deviceId,
                IsPrimary: (device.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0,
                IsActive: (device.StateFlags & DisplayDeviceStateFlags.Active) != 0));
        }

        return adapters;
    }

    private static (HwVendor Vendor, string? Id) ReadCpuVendor()
    {
        string? id = null;
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416
            id = Norm(Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "VendorIdentifier",
                null) as string);
#pragma warning restore CA1416
        }

        id ??= Norm(Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
        return (id switch
        {
            null => HwVendor.Unknown,
            _ when id.Contains("GenuineIntel", StringComparison.OrdinalIgnoreCase) => HwVendor.Intel,
            _ when id.Contains("AuthenticAMD", StringComparison.OrdinalIgnoreCase) => HwVendor.Amd,
            _ => VendorFromName(id)
        }, id);
    }

    private static (string? Description, string? Build, string? OsArch, string? ProcessArch) ReadOs()
    {
        var version = Environment.OSVersion.Version;
        var build = version.Build > 0
            ? version.Build.ToString(CultureInfo.InvariantCulture)
            : null;
        var description = OperatingSystem.IsWindows()
            ? $"Windows {version.Major}.{version.Minor}.{version.Build}"
            : RuntimeInformation.OSDescription;

        return (
            description,
            build,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString());
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
