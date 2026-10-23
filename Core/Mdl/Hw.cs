namespace R3Config.Mdl;

public enum HwTier
{
    Low,
    Mid,
    High,
    Unknown
}

// Hardware vendor as far as the evidence proves it. Other is a real vendor
// R3Config has no explicit condition for; Unknown means the evidence is absent.
public enum HwVendor
{
    Unknown,
    Amd,
    Nvidia,
    Intel,
    Other
}

// One display adapter of this machine. Vendor comes from the PCI vendor ID when
// the OS reports it (VendorFromPci); a name-only vendor is kept but marked so.
public sealed record GpuAdapterInfo(
    string Name,
    HwVendor Vendor,
    string? PciVendorId,
    string? PciDeviceId,
    bool IsPrimary,
    bool IsActive)
{
    public bool VendorFromPci => PciVendorId is not null && Vendor != HwVendor.Unknown;
}

public sealed record HwInfo(
    string CpuName,
    int CpuNum,
    ulong? MemBytes,
    string GpuName,
    HwTier Tier,
    bool IsFall,
    string ProbeMsg,
    HwVendor CpuVendor = HwVendor.Unknown,
    string? CpuVendorId = null,
    System.Collections.Generic.IReadOnlyList<GpuAdapterInfo>? Adapters = null,
    string? OsDescription = null,
    string? OsBuild = null,
    string? OsArchitecture = null,
    string? ProcessArchitecture = null,
    HostCpuInfo? Cpu = null)
{
    public double? TotalB => MemBytes is null
        ? null
        : MemBytes.Value / 1024d / 1024d / 1024d;

    public string Display => TotalB is null
        ? "RAM: Unknown"
        : $"RAM: {TotalB.Value:0.#} GB";

    // Every adapter the probe reported; never guessed from GpuName alone.
    public System.Collections.Generic.IReadOnlyList<GpuAdapterInfo> GpuAdapters => Adapters ?? [];
}
