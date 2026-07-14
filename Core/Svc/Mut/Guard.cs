using System;
using System.IO;
using R3Config.Mdl;

namespace R3Config.Svc;

public enum MutBlock
{
    Inst,
    Tgt,
    Running
}

public sealed class MutBlkEx(
    MutBlock reason,
    string message,
    Exception? innerException = null) :
    InvalidOperationException(message, innerException)
{
    public MutBlock Reason { get; } = reason;
}

public interface IMutGd
{
    void CheckCan(
        R3Inst install,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<string>? workPaths = null);
}

public static class CanPath
{
    public static string GetPath(string path)
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

    public static string GetTgt(
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

        var canonicalParent = GetPath(
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

    public static string GetTgt2(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);
        if (Directory.Exists(fullTargetPath))
        {
            throw new InvalidDataException(
                $"A mutation target is a directory: {fullTargetPath}");
        }

        if (File.Exists(fullTargetPath))
        {
            return GetPath(fullTargetPath);
        }

        return Path.Combine(
            GetTgt(fullTargetPath),
            Path.GetFileName(fullTargetPath));
    }

    public static string GetPath2(
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
            return GetPath(fullPath);
        }

        return GetTgt(
            Path.Combine(fullPath, ".r3guard"));
    }
}

public sealed class MutGuard(
    Func<bool>? isRpcs3Running = null) : IMutGd
{
    private readonly Func<bool> _Running =
        isRpcs3Running ?? InstSvc.IsR3;

    public void CheckCan(
        R3Inst install,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<string>? workPaths = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(targetPaths);

        var roots = VldInst(install);

        if (targetPaths.Count == 0)
        {
            throw Unsafe(
                "A mutation must declare at least one target file.");
        }

        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw Unsafe("A mutation target path is empty.");
            }

            var target = GetPath(
                roots,
                targetPath,
                isDirectory: false);

            if (!seen.Add(target))
            {
                throw Unsafe(
                    $"A mutation target is duplicated: {target}");
            }

        }

        foreach (var workPath in workPaths ?? [])
        {
            if (string.IsNullOrWhiteSpace(workPath))
            {
                throw Unsafe("A mutation work path is empty.");
            }

            _ = GetPath(
                roots,
                workPath,
                isDirectory: true);
        }

        if (_Running())
        {
            throw new MutBlkEx(
                MutBlock.Running,
                "Close RPCS3 before modifying its configuration or patch files.");
        }
    }

    private static CanRoots VldInst(
        R3Inst install)
    {
        try
        {
            var root = Path.GetFullPath(install.RootPath);
            var exe = Path.GetFullPath(install.ExePath);
            var dataRoot = Path.GetFullPath(
                install.DataRoot ?? install.RootPath);
            var config = Path.GetFullPath(install.CfgPath);
            var customConfigs = Path.GetFullPath(
                install.CustPath);
            var patches = Path.GetFullPath(install.PatPath);
            var patchConfig = Path.GetFullPath(
                install.PatCfg);

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
                !Paths(Path.GetDirectoryName(exe) ?? string.Empty, root) ||
                !Paths(config, expectedConfig) ||
                !Paths(patches, expectedPatches) ||
                !Paths(customConfigs, expectedCustomConfigs) ||
                !Paths(patchConfig, expectedPatchConfig))
            {
                throw Invalid(
                    "The RPCS3 installation descriptor does not match its executable and data-root layout.");
            }

            if (!Directory.Exists(root) ||
                !File.Exists(exe) ||
                !Directory.Exists(dataRoot))
            {
                throw Invalid(
                    "The RPCS3 installation, executable, and data root must exist.");
            }

            var canonicalRoot =
                CanPath.GetPath(root);
            var canonicalExe =
                CanPath.GetPath(exe);
            var canonicalDataRoot =
                CanPath.GetPath(dataRoot);
            if (!Paths(
                    Path.GetDirectoryName(canonicalExe) ?? string.Empty,
                    canonicalRoot))
            {
                throw Invalid(
                    "The RPCS3 executable resolves outside its declared installation root.");
            }

            return new CanRoots(
                canonicalDataRoot,
                config,
                patches);
        }
        catch (MutBlkEx)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw new MutBlkEx(
                MutBlock.Inst,
                "The RPCS3 installation descriptor could not be resolved safely.",
                ex);
        }
    }

    private static string GetPath(
        CanRoots roots,
        string path,
        bool isDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var declaredRoot = IsChild(
                roots.CfgPath,
                fullPath)
            ? roots.CfgPath
            : IsChild(
                roots.PatPath,
                fullPath)
                ? roots.PatPath
                : null;

        if (declaredRoot is null)
        {
            throw Unsafe(
                $"Refusing a mutation path outside the RPCS3 config and patches directories: {fullPath}");
        }

        try
        {
            var canonicalRoot =
                CanPath.GetPath2(
                    declaredRoot);
            var expectedRoot = Path.Combine(
                roots.DataRoot,
                Path.GetFileName(declaredRoot));
            if (!Paths(canonicalRoot, expectedRoot))
            {
                throw Unsafe(
                    $"A mutation root resolves outside the RPCS3 data root: {declaredRoot}");
            }

            var canonicalPath = isDirectory
                ? CanPath.GetPath2(
                    fullPath)
                : CanPath.GetTgt2(
                    fullPath);

            if (!IsChild(canonicalRoot, canonicalPath))
            {
                throw Unsafe(
                    $"A mutation path resolves outside its RPCS3 data directory: {fullPath}");
            }

            return canonicalPath;
        }
        catch (MutBlkEx)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw Unsafe(
                $"A mutation path could not be resolved safely: {fullPath}",
                ex);
        }
    }

    private static bool IsChild(
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

    private static bool Paths(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static MutBlkEx Invalid(
        string message)
    {
        return new MutBlkEx(
            MutBlock.Inst,
            message);
    }

    private static MutBlkEx Unsafe(
        string message,
        Exception? innerException = null)
    {
        return new MutBlkEx(
            MutBlock.Tgt,
            message,
            innerException);
    }

    private sealed record CanRoots(
        string DataRoot,
        string CfgPath,
        string PatPath);
}
