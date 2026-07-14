namespace R3Config.Mdl;

public sealed record BackupFileEntry(
    string TargetPath,
    string BackupPath,
    bool Existed,
    long? Length = null,
    string? Sha256 = null);

public sealed record BackupManifest(
    DateTimeOffset CreatedAt,
    IReadOnlyList<BackupFileEntry> Files);

public sealed record BackupSnapshot(
    string DirectoryPath,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BackupFileEntry> Files)
{
    public string DisplayName =>
        $"{CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} - {Files.Count}";
}
