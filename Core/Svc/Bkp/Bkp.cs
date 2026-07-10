using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class BackupService
{
    private const string ManifestFileName = "manifest.json";

    public IReadOnlyList<BackupSnapshot> ListBackups(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(backupRootPath)
            .Select(TryLoadSnapshot)
            .Where(snapshot => snapshot is not null)
            .Cast<BackupSnapshot>()
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToArray();
    }

    public BackupManifest CreateBackup(string backupRootPath, IReadOnlyList<string> targetPaths)
    {
        Directory.CreateDirectory(backupRootPath);

        var backupPath = Path.Combine(backupRootPath, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupPath);

        var entries = new List<BackupFileEntry>();
        foreach (var targetPath in targetPaths)
        {
            var existed = File.Exists(targetPath);
            var backupFilePath = Path.Combine(backupPath, MakeSafeFileName(targetPath));

            if (existed)
            {
                File.Copy(targetPath, backupFilePath, overwrite: true);
            }

            entries.Add(new BackupFileEntry(targetPath, backupFilePath, existed));
        }

        var manifest = new BackupManifest(DateTimeOffset.Now, entries);
        var manifestPath = Path.Combine(backupPath, ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return manifest;
    }

    public void Restore(BackupManifest manifest)
    {
        foreach (var entry in manifest.Files)
        {
            if (entry.Existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
                File.Copy(entry.BackupPath, entry.TargetPath, overwrite: true);
            }
            else if (File.Exists(entry.TargetPath))
            {
                File.Delete(entry.TargetPath);
            }
        }
    }

    public void Restore(BackupSnapshot snapshot)
    {
        Restore(new BackupManifest(snapshot.CreatedAt, snapshot.Files));
    }

    private static BackupSnapshot? TryLoadSnapshot(string backupDirectory)
    {
        var manifestPath = Path.Combine(backupDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath));
            return manifest is null
                ? null
                : new BackupSnapshot(backupDirectory, manifest.CreatedAt, manifest.Files);
        }
        catch
        {
            return null;
        }
    }

    private static string MakeSafeFileName(string path)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            path = path.Replace(invalidChar, '_');
        }

        return path.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
    }
}
