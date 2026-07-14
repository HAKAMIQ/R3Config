using System;
using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

public enum Rpcs3MutationBlockReason
{
    InvalidInstall,
    UnsafeTarget,
    Rpcs3Running
}

public sealed class Rpcs3MutationBlockedException(
    Rpcs3MutationBlockReason reason,
    string message,
    Exception? innerException = null) :
    InvalidOperationException(message, innerException)
{
    public Rpcs3MutationBlockReason Reason { get; } = reason;
}

public interface IRpcs3MutationGuard
{
    void EnsureCanMutate(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<string>? workPaths = null);
}

public static class Rpcs3CanonicalPath
{
    public static string ResolveCanonicalExistingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "A canonical path is empty.",
                nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidDataException(
                $"A path has no filesystem root: {fullPath}");
        var current = root;
        var relative = fullPath[root.Length..];

        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var isDirectory = Directory.Exists(current);
            var isFile = File.Exists(current);
            if (!isDirectory && !isFile)
            {
                throw new FileNotFoundException(
                    "A canonical path component does not exist.",
                    current);
            }

            if ((File.GetAttributes(current) &
                 FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }

            FileSystemInfo info = isDirectory
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            var resolved = info.ResolveLinkTarget(
                returnFinalTarget: true)
                ?? throw new InvalidDataException(
                    $"A reparse point could not be resolved safely: {current}");
            current = Path.GetFullPath(resolved.FullName);

            if (!Directory.Exists(current) &&
                !File.Exists(current))
            {
                throw new FileNotFoundException(
                    "A reparse point resolves to a missing path.",
                    current);
            }
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(current));
    }

    public static string ResolveCanonicalParentForTarget(
        string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException(
                "A mutation target path is empty.",
                nameof(targetPath));
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        var parentPath = Path.GetDirectoryName(fullTargetPath)
            ?? throw new InvalidDataException(
                $"A mutation target has no parent directory: {fullTargetPath}");
        var missingSegments = new Stack<string>();
        var existingAncestor = parentPath;

        while (!Directory.Exists(existingAncestor))
        {
            if (File.Exists(existingAncestor))
            {
                throw new InvalidDataException(
                    $"A mutation target parent is a file: {existingAncestor}");
            }

            var segment = Path.GetFileName(existingAncestor);
            if (string.IsNullOrEmpty(segment))
            {
                throw new DirectoryNotFoundException(
                    $"A mutation target has no existing parent: {fullTargetPath}");
            }

            missingSegments.Push(segment);
            existingAncestor = Path.GetDirectoryName(existingAncestor)
                ?? throw new DirectoryNotFoundException(
                    $"A mutation target has no existing parent: {fullTargetPath}");
        }

        var canonicalParent = ResolveCanonicalExistingPath(
            existingAncestor);
        while (missingSegments.Count > 0)
        {
            canonicalParent = Path.Combine(
                canonicalParent,
                missingSegments.Pop());
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(canonicalParent));
    }

    public static string ResolveCanonicalTarget(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);
        if (Directory.Exists(fullTargetPath))
        {
            throw new InvalidDataException(
                $"A mutation target is a directory: {fullTargetPath}");
        }

        if (File.Exists(fullTargetPath))
        {
            return ResolveCanonicalExistingPath(fullTargetPath);
        }

        return Path.Combine(
            ResolveCanonicalParentForTarget(fullTargetPath),
            Path.GetFileName(fullTargetPath));
    }

    public static string ResolveCanonicalDirectoryPath(
        string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException(
                "A canonical directory path is empty.",
                nameof(directoryPath));
        }

        var fullPath = Path.GetFullPath(directoryPath);
        if (File.Exists(fullPath))
        {
            throw new InvalidDataException(
                $"A mutation directory path is a file: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            return ResolveCanonicalExistingPath(fullPath);
        }

        return ResolveCanonicalParentForTarget(
            Path.Combine(fullPath, ".r3guard"));
    }
}

public sealed class Rpcs3MutationGuard(
    Func<bool>? isRpcs3Running = null) : IRpcs3MutationGuard
{
    private readonly Func<bool> _isRpcs3Running =
        isRpcs3Running ?? Rpcs3InstallService.IsRpcs3Running;

    public void EnsureCanMutate(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<string>? workPaths = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(targetPaths);

        var roots = ValidateInstall(install);

        if (targetPaths.Count == 0)
        {
            throw UnsafeTarget(
                "A mutation must declare at least one target file.");
        }

        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw UnsafeTarget("A mutation target path is empty.");
            }

            var target = ResolveAllowedPath(
                roots,
                targetPath,
                isDirectory: false);

            if (!seen.Add(target))
            {
                throw UnsafeTarget(
                    $"A mutation target is duplicated: {target}");
            }

        }

        foreach (var workPath in workPaths ?? [])
        {
            if (string.IsNullOrWhiteSpace(workPath))
            {
                throw UnsafeTarget("A mutation work path is empty.");
            }

            _ = ResolveAllowedPath(
                roots,
                workPath,
                isDirectory: true);
        }

        if (_isRpcs3Running())
        {
            throw new Rpcs3MutationBlockedException(
                Rpcs3MutationBlockReason.Rpcs3Running,
                "Close RPCS3 before modifying its configuration or patch files.");
        }
    }

    private static CanonicalInstallRoots ValidateInstall(
        Rpcs3InstallDescriptor install)
    {
        try
        {
            var root = Path.GetFullPath(install.RootPath);
            var exe = Path.GetFullPath(install.ExePath);
            var dataRoot = Path.GetFullPath(
                install.DataRootPath ?? install.RootPath);
            var config = Path.GetFullPath(install.ConfigPath);
            var customConfigs = Path.GetFullPath(
                install.CustomConfigsPath);
            var patches = Path.GetFullPath(install.PatchesPath);
            var patchConfig = Path.GetFullPath(
                install.PatchConfigPath);

            var expectedConfig = Path.Combine(dataRoot, "config");
            var expectedPatches = Path.Combine(dataRoot, "patches");
            var expectedCustomConfigs = Path.Combine(
                expectedConfig,
                "custom_configs");
            var expectedPatchConfig = Path.Combine(
                expectedConfig,
                "patch_config.yml");

            if (!Path.GetFileName(exe).Equals(
                    "rpcs3.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                !PathsEqual(Path.GetDirectoryName(exe) ?? string.Empty, root) ||
                !PathsEqual(config, expectedConfig) ||
                !PathsEqual(patches, expectedPatches) ||
                !PathsEqual(customConfigs, expectedCustomConfigs) ||
                !PathsEqual(patchConfig, expectedPatchConfig))
            {
                throw InvalidInstall(
                    "The RPCS3 installation descriptor does not match its executable and data-root layout.");
            }

            if (!Directory.Exists(root) ||
                !File.Exists(exe) ||
                !Directory.Exists(dataRoot))
            {
                throw InvalidInstall(
                    "The RPCS3 installation, executable, and data root must exist.");
            }

            var canonicalRoot =
                Rpcs3CanonicalPath.ResolveCanonicalExistingPath(root);
            var canonicalExe =
                Rpcs3CanonicalPath.ResolveCanonicalExistingPath(exe);
            var canonicalDataRoot =
                Rpcs3CanonicalPath.ResolveCanonicalExistingPath(dataRoot);
            if (!PathsEqual(
                    Path.GetDirectoryName(canonicalExe) ?? string.Empty,
                    canonicalRoot))
            {
                throw InvalidInstall(
                    "The RPCS3 executable resolves outside its declared installation root.");
            }

            return new CanonicalInstallRoots(
                canonicalDataRoot,
                config,
                patches);
        }
        catch (Rpcs3MutationBlockedException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw new Rpcs3MutationBlockedException(
                Rpcs3MutationBlockReason.InvalidInstall,
                "The RPCS3 installation descriptor could not be resolved safely.",
                ex);
        }
    }

    private static string ResolveAllowedPath(
        CanonicalInstallRoots roots,
        string path,
        bool isDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var declaredRoot = IsChildPath(
                roots.ConfigPath,
                fullPath)
            ? roots.ConfigPath
            : IsChildPath(
                roots.PatchesPath,
                fullPath)
                ? roots.PatchesPath
                : null;

        if (declaredRoot is null)
        {
            throw UnsafeTarget(
                $"Refusing a mutation path outside the RPCS3 config and patches directories: {fullPath}");
        }

        try
        {
            var canonicalRoot =
                Rpcs3CanonicalPath.ResolveCanonicalDirectoryPath(
                    declaredRoot);
            var expectedRoot = Path.Combine(
                roots.DataRootPath,
                Path.GetFileName(declaredRoot));
            if (!PathsEqual(canonicalRoot, expectedRoot))
            {
                throw UnsafeTarget(
                    $"A mutation root resolves outside the RPCS3 data root: {declaredRoot}");
            }

            var canonicalPath = isDirectory
                ? Rpcs3CanonicalPath.ResolveCanonicalDirectoryPath(
                    fullPath)
                : Rpcs3CanonicalPath.ResolveCanonicalTarget(
                    fullPath);

            if (!IsChildPath(canonicalRoot, canonicalPath))
            {
                throw UnsafeTarget(
                    $"A mutation path resolves outside its RPCS3 data directory: {fullPath}");
            }

            return canonicalPath;
        }
        catch (Rpcs3MutationBlockedException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw UnsafeTarget(
                $"A mutation path could not be resolved safely: {fullPath}",
                ex);
        }
    }

    private static bool IsChildPath(
        string parentPath,
        string childPath)
    {
        var relative = Path.GetRelativePath(parentPath, childPath);

        return !Path.IsPathRooted(relative) &&
               !relative.Equals(".", StringComparison.Ordinal) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.AltDirectorySeparatorChar}",
                   StringComparison.Ordinal);
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Rpcs3MutationBlockedException InvalidInstall(
        string message)
    {
        return new Rpcs3MutationBlockedException(
            Rpcs3MutationBlockReason.InvalidInstall,
            message);
    }

    private static Rpcs3MutationBlockedException UnsafeTarget(
        string message,
        Exception? innerException = null)
    {
        return new Rpcs3MutationBlockedException(
            Rpcs3MutationBlockReason.UnsafeTarget,
            message,
            innerException);
    }

    private sealed record CanonicalInstallRoots(
        string DataRootPath,
        string ConfigPath,
        string PatchesPath);
}
