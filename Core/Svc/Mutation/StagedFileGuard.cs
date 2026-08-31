using System;
using System.IO;

namespace R3Config.Svc;

public static class StagedFileGuard
{
    public static void Validate(
        string stagedPath,
        string targetPath,
        string purpose)
    {
        if (string.IsNullOrWhiteSpace(stagedPath))
        {
            throw new ArgumentException(
                "A staged file path is empty.",
                nameof(stagedPath));
        }

        var fullStagePath = RuntimePathPolicy.RequireLocal(
            stagedPath,
            $"{purpose} stage");
        var fullTargetPath = RuntimePathPolicy.RequireLocal(
            targetPath,
            $"{purpose} target");

        if (!File.Exists(fullStagePath))
        {
            throw new FileNotFoundException(
                $"The {purpose} stage is missing.",
                fullStagePath);
        }

        if (Directory.Exists(fullStagePath) ||
            (File.GetAttributes(fullStagePath) &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"The {purpose} stage is not a regular file: {fullStagePath}");
        }

        var stageDirectory = Path.GetDirectoryName(fullStagePath)
            ?? throw new InvalidDataException(
                $"The {purpose} stage has no parent directory.");
        var targetDirectory = Path.GetDirectoryName(fullTargetPath)
            ?? throw new InvalidDataException(
                $"The {purpose} target has no parent directory.");

        if (!SamePath(stageDirectory, targetDirectory))
        {
            throw new InvalidDataException(
                $"The {purpose} stage is outside its target directory: {fullStagePath}");
        }

        var canonicalDirectory = CanPath.GetPath(targetDirectory);
        if (!SamePath(canonicalDirectory, targetDirectory))
        {
            throw new InvalidDataException(
                $"The {purpose} target directory resolves through a reparse point: {targetDirectory}");
        }

        var canonicalStage = CanPath.GetPath(fullStagePath);
        if (!SamePath(canonicalStage, fullStagePath))
        {
            throw new InvalidDataException(
                $"The {purpose} stage resolves to another path: {fullStagePath}");
        }
    }

    private static bool SamePath(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }
}
