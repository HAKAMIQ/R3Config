using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class HwProbe
{
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

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_VideoController | Select-Object -First 1 -ExpandProperty Name\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        if (!process.WaitForExit(3000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return null;
        }

        return process.ExitCode == 0
            ? Norm(process.StandardOutput.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            : null;
    }

    private static string? Norm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GlobalMemoryStatusEx",
        SetLastError = true)]
    private static extern bool GlobalEx(ref MemoryEx buffer);

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
