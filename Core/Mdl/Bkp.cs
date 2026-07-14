using System.Text.Json.Serialization;

namespace R3Config.Mdl;

public sealed record BkpEntry(
    [property: JsonPropertyName("TargetPath")] string TgtPath,
    [property: JsonPropertyName("BackupPath")] string BkpPath,
    bool Existed,
    long? Length = null,
    string? Sha256 = null);

public sealed record BkpMan(
    [property: JsonPropertyName("CreatedAt")] DateTimeOffset MadeAt,
    IReadOnlyList<BkpEntry> Files);

public sealed record BkpSnap(
    [property: JsonPropertyName("DirectoryPath")] string DirPath,
    [property: JsonPropertyName("CreatedAt")] DateTimeOffset MadeAt,
    IReadOnlyList<BkpEntry> Files)
{
    public string Name =>
        $"{MadeAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} - {Files.Count}";
}
