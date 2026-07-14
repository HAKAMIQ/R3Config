using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed partial class GmScan
{
    private readonly int _DirLimi;
    private readonly Func<string, IEnumerable<string>> _Directo;
    private readonly Func<string, IEnumerable<string>> _Files;

    public GmScan(
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

    public IReadOnlyList<R3Game> Scan(
        R3Inst install)
    {
        return ScanInfo(install).Games;
    }

    public ScanRes ScanInfo(
        R3Inst install)
    {
        var games = new Dictionary<string, R3Game>(
            StringComparer.OrdinalIgnoreCase);
        var diagnostics =
            new DiagList<ScanDiag>(
                R3Limits.Diag);

        ScanYml(install, games, diagnostics);
        ScanHdd(install, games, diagnostics);
        var customConfigSerials = Scan2(
            install,
            diagnostics);

        return new ScanRes(
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
        R3Inst install)
    {
        return Scan2(
            install,
            diagnostics: null);
    }

    private IReadOnlyList<string> Scan2(
        R3Inst install,
        ICollection<ScanDiag>? diagnostics)
    {
        if (!Directory.Exists(install.CustPath))
        {
            return [];
        }

        try
        {
            var entries = _Files(
                    install.CustPath)
                .Take(_DirLimi + 1)
                .ToArray();
            if (entries.Length > _DirLimi)
            {
                diagnostics?.Add(new ScanDiag(
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
                    .Where(name => name is not null &&
                        name.StartsWith(
                            "config_",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(name => name!["config_".Length..]
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
                UnauthorizedAccessException)
        {
            diagnostics?.Add(new ScanDiag(
                ScanCode.NoRead,
                ScanSev.Warning,
                ScanSrc.Dir,
                install.CustPath,
                ex.Message));
            return [];
        }
    }

    private static void ScanYml(
        R3Inst install,
        Dictionary<string, R3Game> games,
        ICollection<ScanDiag> diagnostics)
    {
        var gamesYmlPaths = List(install)
            .ToArray();
        if (gamesYmlPaths.Length == 0)
        {
            diagnostics.Add(new ScanDiag(
                ScanCode.Missing,
                ScanSev.Info,
                ScanSrc.Yaml,
                Path.Combine(install.CfgPath, "games.yml"),
                "No supported games.yml source was found."));
            return;
        }

        foreach (var gamesYmlPath in gamesYmlPaths)
        {
            ScanFile(
                gamesYmlPath,
                games,
                diagnostics);
        }
    }

    private static void ScanFile(
        string gamesYmlPath,
        Dictionary<string, R3Game> games,
        ICollection<ScanDiag> diagnostics)
    {
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
                diagnostics.Add(new ScanDiag(
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
                UnauthorizedAccessException)
        {
            diagnostics.Add(new ScanDiag(
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

            var installPath = valueNode.Value ?? string.Empty;
            var sfoProbe = SfoRead.ReadFor(
                installPath);
            if (sfoProbe.Diag is not null)
            {
                diagnostics.Add(sfoProbe.Diag);
            }
            var sfo = sfoProbe.Info;

            games[serial] = new R3Game(
                Serial: sfo?.TitleId ?? serial,
                Title: sfo?.Title ?? Derive(
                    serial,
                    installPath),
                InstPath: installPath,
                Version: PatVer.Norm(
                    sfo?.AppVer),
                PpuHash: null,
                SpuHash: null);
        }
    }

    private static IEnumerable<string> List(
        R3Inst install)
    {
        var configBasePath =
            Path.GetDirectoryName(install.CfgPath);

        var portablePath = Path.Combine(
            install.RootPath,
            "portable");

        string?[] candidates =
        [
            Path.Combine(
                install.CfgPath,
                "games.yml"),
            configBasePath is null
                ? null
                : Path.Combine(
                    configBasePath,
                    "games.yml"),
            Path.Combine(
                install.RootPath,
                "games.yml"),
            Path.Combine(
                portablePath,
                "config",
                "games.yml"),
            Path.Combine(
                portablePath,
                "games.yml")
        ];

        return candidates
            .OfType<string>()
            .Where(path =>
                !string.IsNullOrWhiteSpace(path))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists);
    }

    private void ScanHdd(
        R3Inst install,
        Dictionary<string, R3Game> games,
        ICollection<ScanDiag> diagnostics)
    {
        var configBasePath =
            Path.GetDirectoryName(install.CfgPath);

        var portablePath = Path.Combine(
            install.RootPath,
            "portable");

        string?[] candidates =
        [
            configBasePath is null
                ? null
                : Path.Combine(
                    configBasePath,
                    "dev_hdd0",
                    "game"),
            Path.Combine(
                install.RootPath,
                "dev_hdd0",
                "game"),
            Path.Combine(
                portablePath,
                "dev_hdd0",
                "game")
        ];

        var hddGamePaths = candidates
            .OfType<string>()
            .Where(path =>
                !string.IsNullOrWhiteSpace(path))
            .Distinct(
                StringComparer.OrdinalIgnoreCase);

        foreach (var hddGamePath in hddGamePaths)
        {
            if (!Directory.Exists(hddGamePath))
            {
                continue;
            }

            string[] directories;

            try
            {
                directories =
                [
                    .. _Directo(hddGamePath)
                        .Take(_DirLimi + 1)
                ];
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException)
            {
                diagnostics.Add(new ScanDiag(
                    ScanCode.Denied,
                    ScanSev.Warning,
                    ScanSrc.GameDir,
                    hddGamePath,
                    ex.Message));
                continue;
            }

            if (directories.Length > _DirLimi)
            {
                diagnostics.Add(new ScanDiag(
                    ScanCode.Reached,
                    ScanSev.Warning,
                    ScanSrc.GameDir,
                    hddGamePath,
                    $"Game-directory enumeration stopped at {_DirLimi} entries."));
            }

            foreach (var directory in directories.Take(
                         _DirLimi))
            {
                var folderName =
                    Path.GetFileName(directory);

                if (string.IsNullOrWhiteSpace(folderName) ||
                    !Serial().IsMatch(folderName))
                {
                    continue;
                }

                var sfoProbe =
                    SfoRead.ReadFor(
                        directory);
                if (sfoProbe.Diag is not null)
                {
                    diagnostics.Add(sfoProbe.Diag);
                }
                var sfo = sfoProbe.Info;

                var serial =
                    sfo?.TitleId ?? folderName;

                if (games.TryGetValue(
                        serial,
                        out var existing))
                {
                    games[serial] = existing with
                    {
                        Title = string.IsNullOrWhiteSpace(
                            sfo?.Title)
                            ? existing.Title
                            : sfo!.Title!,
                        Version = Select(
                            existing.Version,
                            sfo?.AppVer)
                    };

                    continue;
                }

                games[serial] = new R3Game(
                    Serial: serial,
                    Title: sfo?.Title ?? serial,
                    InstPath: directory,
                    Version: PatVer.Norm(
                        sfo?.AppVer),
                    PpuHash: null,
                    SpuHash: null);
            }
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

        return System.Version.TryParse(
                   normalizedUpdate,
                   out var update) &&
               System.Version.TryParse(
                   normalizedBase,
                   out var @base) &&
               update < @base
            ? normalizedBase
            : normalizedUpdate;
    }

    private static string Derive(
        string serial,
        string installPath)
    {
        var fileName =
            Path.GetFileNameWithoutExtension(
                installPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return serial;
        }

        var title = fileName
            .Replace('_', ' ')
            .Replace(
                serial,
                string.Empty,
                StringComparison.OrdinalIgnoreCase);

        title = TitleSep()
            .Replace(
                title,
                " ");

        title = Repeated()
            .Replace(
                title,
                " ")
            .Trim();

        return string.IsNullOrWhiteSpace(title)
            ? serial
            : title;
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
            ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [GeneratedRegex(
        @"^[A-Z]{4}[0-9]{5}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Serial();

    [GeneratedRegex(
        @"[-\[\]()]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TitleSep();

    [GeneratedRegex(
        @"\s{2,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex Repeated();
}

#pragma warning restore CA1822, IDE0130
