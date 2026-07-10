namespace R3Config.Mdl;

public enum HwTier
{
    Low,
    Mid,
    High,
    Unknown
}

public sealed record HwInfo(
    string CpuName,
    int LogicalProcessorCount,
    ulong? TotalMemoryBytes,
    string GpuName,
    HwTier Tier,
    bool IsFallback,
    string ProbeMessage)
{
    public double? TotalMemoryGiB => TotalMemoryBytes is null
        ? null
        : TotalMemoryBytes.Value / 1024d / 1024d / 1024d;

    public string MemoryDisplay => TotalMemoryGiB is null
        ? "RAM: Unknown"
        : $"RAM: {TotalMemoryGiB.Value:0.#} GB";
}
