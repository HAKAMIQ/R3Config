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

public sealed partial class GameScanner
{
    private readonly int _directoryEntryLimit;

    public GameScanner(
        int directoryEntryLimit = Rpcs3InputLimits.DirectoryEntries)
    {
        if (directoryEntryLimit <= 0 ||
            directoryEntryLimit > Rpcs3InputLimits.DirectoryEntries)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryEntryLimit));
        }

        _directoryEntryLimit = directoryEntryLimit;
    }

    public IReadOnlyList<Rpcs3GameDescriptor> Scan(
        Rpcs3InstallDescriptor install)
    {
        var games = new Dictionary<string, Rpcs3GameDescriptor>(
            StringComparer.OrdinalIgnoreCase);

        ScanGamesYml(install, games);
        ScanHddGameFolders(install, games);

        return
        [
            .. games.Values.OrderBy(
                game => game.Serial,
                StringComparer.OrdinalIgnoreCase)
        ];
    }

    public IReadOnlyList<string> ScanCustomConfigSerials(
        Rpcs3InstallDescriptor install)
    {
        if (!Directory.Exists(install.CustomConfigsPath))
        {
            return [];
        }

        try
        {
            return
            [
                .. Directory
                    .EnumerateFiles(
                        install.CustomConfigsPath,
                        "config_*.yml",
                        SearchOption.TopDirectoryOnly)
                    .Take(_directoryEntryLimit)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => name is not null &&
                        name.StartsWith(
                            "config_",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(name => name!["config_".Length..]
                        .ToUpperInvariant())
                    .Where(serial => SerialPattern().IsMatch(serial))
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
            return [];
        }
    }

    private static void ScanGamesYml(
        Rpcs3InstallDescriptor install,
        Dictionary<string, Rpcs3GameDescriptor> games)
    {
        foreach (var gamesYmlPath in EnumerateGamesYmlPaths(install))
        {
            ScanGamesYmlFile(
                gamesYmlPath,
                games);
        }
    }

    private static void ScanGamesYmlFile(
        string gamesYmlPath,
        Dictionary<string, Rpcs3GameDescriptor> games)
    {
        YamlMappingNode root;

        try
        {
            var text = Rpcs3BoundedInput.ReadUtf8TextFile(
                gamesYmlPath,
                Rpcs3InputLimits.GamesYamlBytes,
                "games.yml");
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));

            if (yaml.Documents.Count == 0 ||
                yaml.Documents.Count > Rpcs3InputLimits.YamlDocuments ||
                yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
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
                !SerialPattern().IsMatch(serial))
            {
                continue;
            }

            var installPath = valueNode.Value ?? string.Empty;
            var sfo = ParamSfoReader.TryReadForGamePath(installPath);

            games[serial] = new Rpcs3GameDescriptor(
                Serial: sfo?.TitleId ?? serial,
                Title: sfo?.Title ?? DeriveTitle(
                    serial,
                    installPath),
                InstallPath: installPath,
                Version: Rpcs3PatchVersion.Normalize(
                    sfo?.AppVersion),
                PpuHash: null,
                SpuHash: null);
        }
    }

    private static IEnumerable<string> EnumerateGamesYmlPaths(
        Rpcs3InstallDescriptor install)
    {
        var configBasePath =
            Path.GetDirectoryName(install.ConfigPath);

        var portablePath = Path.Combine(
            install.RootPath,
            "portable");

        string?[] candidates =
        [
            Path.Combine(
                install.ConfigPath,
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

    private void ScanHddGameFolders(
        Rpcs3InstallDescriptor install,
        Dictionary<string, Rpcs3GameDescriptor> games)
    {
        var configBasePath =
            Path.GetDirectoryName(install.ConfigPath);

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
                    .. Directory.EnumerateDirectories(
                        hddGamePath)
                        .Take(_directoryEntryLimit)
                ];
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var directory in directories)
            {
                var folderName =
                    Path.GetFileName(directory);

                if (string.IsNullOrWhiteSpace(folderName) ||
                    !SerialPattern().IsMatch(folderName))
                {
                    continue;
                }

                var sfo =
                    ParamSfoReader.TryReadForGamePath(
                        directory);

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
                        Version = SelectInstalledVersion(
                            existing.Version,
                            sfo?.AppVersion)
                    };

                    continue;
                }

                games[serial] = new Rpcs3GameDescriptor(
                    Serial: serial,
                    Title: sfo?.Title ?? serial,
                    InstallPath: directory,
                    Version: Rpcs3PatchVersion.Normalize(
                        sfo?.AppVersion),
                    PpuHash: null,
                    SpuHash: null);
            }
        }
    }

    private static string? SelectInstalledVersion(
        string? baseVersion,
        string? updateVersion)
    {
        var normalizedBase =
            Rpcs3PatchVersion.Normalize(baseVersion);

        var normalizedUpdate =
            Rpcs3PatchVersion.Normalize(updateVersion);

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

    private static string DeriveTitle(
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

        title = TitleSeparatorPattern()
            .Replace(
                title,
                " ");

        title = RepeatedWhitespacePattern()
            .Replace(
                title,
                " ")
            .Trim();

        return string.IsNullOrWhiteSpace(title)
            ? serial
            : title;
    }

    [GeneratedRegex(
        @"^[A-Z]{4}[0-9]{5}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SerialPattern();

    [GeneratedRegex(
        @"[-\[\]()]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TitleSeparatorPattern();

    [GeneratedRegex(
        @"\s{2,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWhitespacePattern();
}

#pragma warning restore CA1822, IDE0130
