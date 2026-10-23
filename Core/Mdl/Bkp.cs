using System.Text.Json.Serialization;

namespace R3Config.Mdl;

public sealed record BackupFileEntry(
    [property: JsonPropertyName("TargetPath")] string TgtPath,
    [property: JsonPropertyName("BackupPath")] string BkpPath,
    bool Existed,
    long? Length = null,
    string? Sha256 = null);

public sealed record BackupManifest(
    [property: JsonPropertyName("CreatedAt")] DateTimeOffset MadeAt,
    IReadOnlyList<BackupFileEntry> Files);

public sealed record BackupSnapshot(
    [property: JsonPropertyName("DirectoryPath")] string DirPath,
    [property: JsonPropertyName("CreatedAt")] DateTimeOffset MadeAt,
    IReadOnlyList<BackupFileEntry> Files)
{
    [JsonIgnore]
    public bool IsLegacy { get; init; }
}
