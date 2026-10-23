using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

#pragma warning disable IDE0130
namespace R3Config.Svc;
#pragma warning restore IDE0130

public sealed partial class GameScanner
{
    private const string CustomConfigPrefix = "config_";

    private readonly int _DirLimi;
    private readonly Func<string, IEnumerable<string>> _Directo;
    private readonly Func<string, IEnumerable<string>> _Files;

    public GameScanner(
        int directoryEntryLimit = R3Limits.DirEnt,
        Func<string, IEnumerable<string>>? enumerateDirectories = null,
        Func<string, IEnumerable<string>>? enumerateCustomConfigFiles = null)
    {
        if (directoryEntryLimit <= 0 ||
            directoryEntryLimit > R3Limits.DirEnt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryEntryLimit));
        }

        _DirLimi = directoryEntryLimit;
        _Directo = enumerateDirectories ??
            Directory.EnumerateDirectories;
        _Files = enumerateCustomConfigFiles ??
            (path => Directory.EnumerateFiles(
                path,
                "config_*.yml",
                SearchOption.TopDirectoryOnly));
    }

    public IReadOnlyList<Rpcs3GameDescriptor> Scan(
        Rpcs3InstallDescriptor install,
        CancellationToken cancellationToken = default)
    {
        return ScanInfo(
            install,
            cancellationToken).Games;
    }

    public GameScanResult ScanInfo(
        Rpcs3InstallDescriptor install,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        cancellationToken.ThrowIfCancellationRequested();

        var games = new Dictionary<string, Rpcs3GameDescriptor>(
            StringComparer.OrdinalIgnoreCase);
        var diagnostics =
            new DiagList<GameScanDiagnostic>(
                R3Limits.Diag);

        ScanYml(
            install,
            games,
            diagnostics,
            cancellationToken);

        ScanHdd(
            install,
            games,
            diagnostics,
            cancellationToken);

        var customConfigSerials = Scan2(
            install,
            diagnostics,
            cancellationToken);

        return new GameScanResult(
            [
                .. games.Values.OrderBy(
                    game => game.Serial,
                    StringComparer.OrdinalIgnoreCase)
            ],
            [
                .. diagnostics
                    .OrderBy(
                        diagnostic => diagnostic.SrcPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(diagnostic => diagnostic.Code)
                    .ThenBy(
                        diagnostic => diagnostic.TechMsg,
                        StringComparer.Ordinal)
            ],
            customConfigSerials);
    }

    public IReadOnlyList<string> Scan2(
        Rpcs3InstallDescriptor install,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);

        return Scan2(
            install,
            diagnostics: null,
            cancellationToken);
    }

    private IReadOnlyList<string> Scan2(
        Rpcs3InstallDescriptor install,
        DiagList<GameScanDiagnostic>? diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(install.CustPath))
        {
            return [];
        }

        try
        {
            var entries = _Files(install.CustPath)
                .Take(_DirLimi + 1)
                .ToArray();
            cancellationToken.ThrowIfCancellationRequested();

            if (entries.Length > _DirLimi)
            {
                diagnostics?.Add(new GameScanDiagnostic(
                    ScanCode.Reached,
                    ScanSev.Warning,
                    ScanSrc.Dir,
                    install.CustPath,
                    $"Custom-config enumeration stopped at {_DirLimi} entries."));
            }

            return
            [
                .. entries
                    .Take(_DirLimi)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        name.StartsWith(
                            CustomConfigPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(name => name![CustomConfigPrefix.Length..]
                        .ToUpperInvariant())
                    .Where(serial => Serial().IsMatch(serial))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        serial => serial,
                        StringComparer.OrdinalIgnoreCase)
            ];
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                SecurityException)
        {
            diagnostics?.Add(new GameScanDiagnostic(
                ScanCode.NoRead,
                ScanSev.Warning,
                ScanSrc.Dir,
                install.CustPath,
                ex.Message));

            return [];
        }
    }

    private static void ScanYml(
        Rpcs3InstallDescriptor install,
        Dictionary<string, Rpcs3GameDescriptor> games,
        DiagList<GameScanDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gamesYmlPaths = List(install)
            .ToArray();

        if (gamesYmlPaths.Length == 0)
        {
            diagnostics.Add(new GameScanDiagnostic(
                ScanCode.Missing,
                ScanSev.Info,
                ScanSrc.Yaml,
                Path.Combine(
                    install.CfgPath,
                    "games.yml"),
                "No supported games.yml source was found."));

            return;
        }

        foreach (var gamesYmlPath in gamesYmlPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanFile(
                gamesYmlPath,
                games,
                diagnostics,
                cancellationToken);
        }
    }

    private static void ScanFile(
        string gamesYmlPath,
        Dictionary<string, Rpcs3GameDescriptor> games,
        DiagList<GameScanDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        YamlMappingNode root;

        try
        {
            var text = R3Bound.ReadUtf8(
                gamesYmlPath,
                R3Limits.GameByte,
                "games.yml");

            var yaml = R3Bound.LoadYaml(
                text,
                "games.yml");

            if (yaml.Documents.Count == 0 ||
                yaml.Documents.Count > R3Limits.YamlDoc ||
                yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.Yaml,
                    gamesYmlPath,
                    "games.yml must contain one YAML mapping document."));

                return;
            }

            root = mapping;
        }
        catch (Exception ex) when (
            ex is YamlException or
                InvalidDataException or
                IOException or
                UnauthorizedAccessException or
                SecurityException)
        {
            diagnostics.Add(new GameScanDiagnostic(
                IsFile(
                    gamesYmlPath,
                    R3Limits.GameByte)
                    ? ScanCode.Large
                    : ScanCode.GameBad,
                ScanSev.Warning,
                ScanSrc.Yaml,
                gamesYmlPath,
                ex.Message));

            return;
        }

        foreach (var child in root.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.Key is not YamlScalarNode keyNode ||
                child.Value is not YamlScalarNode valueNode)
            {
                continue;
            }

            var serial = keyNode.Value;

            if (string.IsNullOrWhiteSpace(serial) ||
                !Serial().IsMatch(serial))
            {
                continue;
            }

            var installPath =
                valueNode.Value?.Trim() ?? string.Empty;

            if (!Directory.Exists(installPath))
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.Missing,
                    ScanSev.Info,
                    ScanSrc.GameDir,
                    installPath,
                    $"games.yml entry {serial} points to a missing game directory."));

                continue;
            }

            var sfoProbe =
                SfoRead.ReadFor(installPath);

            if (sfoProbe.Diag is not null)
            {
                diagnostics.Add(sfoProbe.Diag);
            }

            var sfo = sfoProbe.Info;
            var title = SfoTitle(
                serial,
                sfo);
            var category = SfoCategory(sfo);

            if (title is null)
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.ParamSfo,
                    installPath,
                    $"Installed game {serial} has no trusted PARAM.SFO title or exact TITLE_ID."));

                continue;
            }

            if (!IsGameCategory(category))
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.ParamSfo,
                    installPath,
                    $"games.yml entry {serial} has unsupported PARAM.SFO CATEGORY '{category ?? "<missing>"}'."));

                continue;
            }

            games.TryAdd(
                serial,
                new Rpcs3GameDescriptor(
                    Serial: serial,
                    Title: title,
                    InstPath: installPath,
                    Version: PatVer.Norm(
                        sfo?.EffectiveVersion),
                    PpuHash: null,
                    SpuHash: null,
                    Category: category));
        }
    }

    private static IEnumerable<string> List(
        Rpcs3InstallDescriptor install)
    {
        var dataRoot = DataRoot(install);

        string[] candidates =
        [
            Path.Combine(
                install.CfgPath,
                "games.yml"),
            Path.Combine(
                dataRoot,
                "games.yml")
        ];

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists);
    }

    private void ScanHdd(
        Rpcs3InstallDescriptor install,
        Dictionary<string, Rpcs3GameDescriptor> games,
        DiagList<GameScanDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hdd0Path = Rpcs3InstallService.Hdd0(
            install,
            out var hdd0Issue);

        if (string.IsNullOrWhiteSpace(hdd0Path))
        {
            if (!string.IsNullOrWhiteSpace(hdd0Issue))
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.Yaml,
                    Path.Combine(
                        install.CfgPath,
                        "vfs.yml"),
                    hdd0Issue));
            }

            return;
        }

        var hddGamePath = Path.Combine(
            hdd0Path,
            "game");

        if (!Directory.Exists(hddGamePath))
        {
            return;
        }

        string[] directories;

        try
        {
            directories =
            [
                .. _Directo(hddGamePath)
                    .Take(_DirLimi + 1)
            ];
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                SecurityException)
        {
            diagnostics.Add(new GameScanDiagnostic(
                ScanCode.Denied,
                ScanSev.Warning,
                ScanSrc.GameDir,
                hddGamePath,
                ex.Message));

            return;
        }

        if (directories.Length > _DirLimi)
        {
            diagnostics.Add(new GameScanDiagnostic(
                ScanCode.Reached,
                ScanSev.Warning,
                ScanSrc.GameDir,
                hddGamePath,
                $"Game-directory enumeration stopped at {_DirLimi} entries."));
        }

        foreach (var directory in directories.Take(
                     _DirLimi))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderName =
                Path.GetFileName(directory);

            if (string.IsNullOrWhiteSpace(folderName) ||
                !Serial().IsMatch(folderName))
            {
                continue;
            }

            var sfoProbe =
                SfoRead.ReadFor(directory);

            if (sfoProbe.Diag is not null)
            {
                diagnostics.Add(sfoProbe.Diag);
            }

            var sfo = sfoProbe.Info;

            if (!SfoIdentityMatches(
                    folderName,
                    sfo))
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.ParamSfo,
                    directory,
                    $"RPCS3 data folder {folderName} has no exact matching PARAM.SFO TITLE_ID."));

                continue;
            }

            var category = SfoCategory(sfo);

            if (games.TryGetValue(
                    folderName,
                    out var existing))
            {
                if (string.Equals(
                        existing.Category,
                        "DG",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        category,
                        "GD",
                        StringComparison.Ordinal))
                {
                    games[folderName] = existing with
                    {
                        Version = Select(
                            existing.Version,
                            sfo?.EffectiveVersion)
                    };

                    continue;
                }

                if (string.Equals(
                        existing.Category,
                        category,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.ParamSfo,
                    directory,
                    $"RPCS3 data folder {folderName} CATEGORY '{category ?? "<missing>"}' conflicts with installed-game CATEGORY '{existing.Category ?? "<missing>"}' and was ignored."));

                continue;
            }

            if (!IsGameCategory(category))
            {
                // RPCS3 enumerates game-data entries, but GD and other data
                // categories do not establish R3Config installed-game membership.
                if (!string.Equals(
                        category,
                        "GD",
                        StringComparison.Ordinal))
                {
                    diagnostics.Add(new GameScanDiagnostic(
                        ScanCode.GameBad,
                        ScanSev.Warning,
                        ScanSrc.ParamSfo,
                        directory,
                        $"RPCS3 data folder {folderName} has unsupported PARAM.SFO CATEGORY '{category ?? "<missing>"}'."));
                }

                continue;
            }

            var title = SfoTitle(
                folderName,
                sfo);

            if (title is null)
            {
                diagnostics.Add(new GameScanDiagnostic(
                    ScanCode.GameBad,
                    ScanSev.Warning,
                    ScanSrc.ParamSfo,
                    directory,
                    $"RPCS3 data folder {folderName} has no trusted PARAM.SFO title."));

                continue;
            }

            games[folderName] = new Rpcs3GameDescriptor(
                Serial: folderName,
                Title: title,
                InstPath: directory,
                Version: PatVer.Norm(
                    sfo?.EffectiveVersion),
                PpuHash: null,
                SpuHash: null,
                Category: category);
        }
    }

    private static string? Select(
        string? baseVersion,
        string? updateVersion)
    {
        var normalizedBase =
            PatVer.Norm(baseVersion);

        var normalizedUpdate =
            PatVer.Norm(updateVersion);

        if (normalizedUpdate is null)
        {
            return normalizedBase;
        }

        if (normalizedBase is null)
        {
            return normalizedUpdate;
        }

        var updateIsVersion = System.Version.TryParse(
            normalizedUpdate,
            out var update);

        var baseIsVersion = System.Version.TryParse(
            normalizedBase,
            out var @base);

        if (!updateIsVersion)
        {
            return normalizedBase;
        }

        if (!baseIsVersion)
        {
            return normalizedUpdate;
        }

        return update < @base
            ? normalizedBase
            : normalizedUpdate;
    }

    private static string DataRoot(
        Rpcs3InstallDescriptor install)
    {
        if (!string.IsNullOrWhiteSpace(
                install.DataRoot))
        {
            return install.DataRoot;
        }

        var configBasePath =
            Path.GetDirectoryName(
                install.CfgPath);

        return string.IsNullOrWhiteSpace(
            configBasePath)
            ? install.RootPath
            : configBasePath;
    }

    private static bool SfoIdentityMatches(
        string serial,
        SfoInfo? sfo)
    {
        var titleId = sfo?.TitleId?.Trim();

        return !string.IsNullOrWhiteSpace(titleId) &&
               titleId.Equals(
                   serial,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? SfoCategory(SfoInfo? sfo)
    {
        var category = sfo?.Category?.Trim();

        return string.IsNullOrWhiteSpace(category)
            ? null
            : category.ToUpperInvariant();
    }

    private static bool IsGameCategory(string? category)
    {
        return category is "DG" or "HG";
    }

    private static string? SfoTitle(
        string serial,
        SfoInfo? sfo)
    {
        if (sfo is null ||
            string.IsNullOrWhiteSpace(
                sfo.Title))
        {
            return null;
        }

        if (!SfoIdentityMatches(
                serial,
                sfo))
        {
            return null;
        }

        var cleaned = string.Concat(
            sfo.Title.Select(character =>
                char.IsControl(character)
                    ? ' '
                    : character));

        cleaned = Repeated()
            .Replace(
                cleaned,
                " ")
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned) ||
            cleaned.Equals(
                serial,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return cleaned;
    }

    private static bool IsFile(
        string path,
        long maxBytes)
    {
        try
        {
            return File.Exists(path) &&
                   new FileInfo(path).Length > maxBytes;
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                SecurityException)
        {
            return false;
        }
    }

    [GeneratedRegex(
        @"^[A-Z]{4}[0-9]{5}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Serial();

    [GeneratedRegex(
        @"\s{2,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex Repeated();
}
