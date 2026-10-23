using System;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// A value with its raw text preserved, a normalized form (null when it cannot
// be normalized without guessing) and the method that produced it.
public sealed record NormalizedValue(string? Raw, string? Value, string Method);

public enum HardwareSource
{
    // Read from the machine R3Config is running on.
    CurrentMachine,

    // Stated by an imported RPCS3 log, which may come from another machine.
    ImportedLog
}

public sealed record LogHardwareView(
    HardwareSource Source,
    NormalizedValue CpuVendor,
    NormalizedValue GpuVendor,
    NormalizedValue GpuDriver);

// Hardware as an imported log states it, kept apart from the current machine.
// Raw text is never overwritten; a vendor comes from a name token only, and a
// driver is normalized only for the one form whose meaning is known.
public static partial class LogHardware
{
    [GeneratedRegex(@"^(?<major>\d{1,4})\.(?<minor>\d{1,3})\.0\.0$", RegexOptions.CultureInvariant)]
    private static partial Regex NvidiaQuadRegex();

    public static LogHardwareView Read(LogSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var cpuLine = session.CpuLine;
        var cpuVendor = cpuLine is null
            ? new NormalizedValue(null, null, "NOT_STATED")
            : cpuLine.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? new NormalizedValue(cpuLine, "Intel", "NAME_TOKEN")
            : cpuLine.Contains("AMD", StringComparison.OrdinalIgnoreCase) ? new NormalizedValue(cpuLine, "AMD", "NAME_TOKEN")
            : new NormalizedValue(cpuLine, null, "NO_VENDOR_TOKEN");

        var gpuVendor = session.GpuName is null
            ? new NormalizedValue(null, null, "NOT_STATED")
            : HwProbe.VendorFromName(session.GpuName) switch
            {
                HwVendor.Nvidia => new NormalizedValue(session.GpuName, "NVIDIA", "NAME_TOKEN"),
                HwVendor.Amd => new NormalizedValue(session.GpuName, "AMD", "NAME_TOKEN"),
                HwVendor.Intel => new NormalizedValue(session.GpuName, "Intel", "NAME_TOKEN"),
                _ => new NormalizedValue(session.GpuName, null, "NO_VENDOR_TOKEN")
            };

        var driverRaw = session.RunContext.GpuDriver;
        var driver = driverRaw is null
            ? new NormalizedValue(null, null, "NOT_STATED")
            : gpuVendor.Value == "NVIDIA" && NvidiaQuadRegex().Match(driverRaw) is { Success: true } quad
                ? new NormalizedValue(driverRaw, $"{quad.Groups["major"].Value}.{quad.Groups["minor"].Value}", "NVIDIA_VULKAN_QUAD_TRIM")
                : new NormalizedValue(driverRaw, null, "NO_KNOWN_NORMALIZATION");

        return new LogHardwareView(HardwareSource.ImportedLog, cpuVendor, gpuVendor, driver);
    }
}

#pragma warning restore IDE0130
