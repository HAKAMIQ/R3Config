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
    int CpuNum,
    ulong? MemBytes,
    string GpuName,
    HwTier Tier,
    bool IsFall,
    string ProbeMsg)
{
    public double? TotalB => MemBytes is null
        ? null
        : MemBytes.Value / 1024d / 1024d / 1024d;

    public string Display => TotalB is null
        ? "RAM: Unknown"
        : $"RAM: {TotalB.Value:0.#} GB";
}
