namespace R3Config.Mdl;

public sealed record Rpcs3InstallDescriptor(
    string RootPath,
    string ExePath,
    string ConfigPath,
    string CustomConfigsPath,
    string PatchesPath,
    string PatchConfigPath);

public sealed record Rpcs3GameDescriptor(
    string Serial,
    string Title,
    string InstallPath,
    string? Version,
    string? PpuHash,
    string? SpuHash,
    IReadOnlyList<string>? PpuHashes = null,
    IReadOnlyList<string>? SpuHashes = null,
    string? HashSource = null,
    string? HashMessage = null)
{
    public IReadOnlyList<string> AllPpuHashes => MergeHashes(PpuHash, PpuHashes);

    public IReadOnlyList<string> AllSpuHashes => MergeHashes(SpuHash, SpuHashes);

    private static IReadOnlyList<string> MergeHashes(string? primary, IReadOnlyList<string>? hashes)
    {
        return (hashes ?? [])
            .Append(primary)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Select(hash => hash!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record ParamSfoInfo(
    string? Title,
    string? TitleId,
    string? AppVersion);
