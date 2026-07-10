using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class GameScanner
{
    private static readonly Regex SerialPattern = new(
        @"^[A-Z]{4}\d{5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<Rpcs3GameDescriptor> Scan(Rpcs3InstallDescriptor install)
    {
        var games = new Dictionary<string, Rpcs3GameDescriptor>(StringComparer.OrdinalIgnoreCase);

        ScanGamesYml(install, games);
        ScanHddGameFolders(install, games);

        return games.Values
            .OrderBy(game => game.Serial, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ScanGamesYml(Rpcs3InstallDescriptor install, IDictionary<string, Rpcs3GameDescriptor> games)
    {
        foreach (var gamesYmlPath in EnumerateGamesYmlPaths(install))
        {
            ScanGamesYmlFile(gamesYmlPath, games);
        }
    }

    private static void ScanGamesYmlFile(string gamesYmlPath, IDictionary<string, Rpcs3GameDescriptor> games)
    {
        YamlMappingNode? root;
        try
        {
            using var reader = new StreamReader(gamesYmlPath);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                return;
            }

            root = mapping;
        }
        catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in root.Children)
        {
            if (child.Key is not YamlScalarNode keyNode ||
                child.Value is not YamlScalarNode valueNode ||
                string.IsNullOrWhiteSpace(keyNode.Value) ||
                !SerialPattern.IsMatch(keyNode.Value))
            {
                continue;
            }

            var serial = keyNode.Value;
            var installPath = valueNode.Value ?? string.Empty;
            var sfo = ParamSfoReader.TryReadForGamePath(installPath);
            games[serial] = new Rpcs3GameDescriptor(
                Serial: sfo?.TitleId ?? serial,
                Title: sfo?.Title ?? DeriveTitle(serial, installPath),
                InstallPath: installPath,
                Version: Rpcs3PatchVersion.Normalize(sfo?.AppVersion),
                PpuHash: null,
                SpuHash: null);
        }
    }

    private static IEnumerable<string> EnumerateGamesYmlPaths(Rpcs3InstallDescriptor install)
    {
        var configBasePath = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var candidates = new[]
        {
            Path.Combine(install.ConfigPath, "games.yml"),
            configBasePath is null ? null : Path.Combine(configBasePath, "games.yml"),
            Path.Combine(install.RootPath, "games.yml"),
            Path.Combine(portablePath, "config", "games.yml"),
            Path.Combine(portablePath, "games.yml")
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists);
    }

    private static void ScanHddGameFolders(Rpcs3InstallDescriptor install, IDictionary<string, Rpcs3GameDescriptor> games)
    {
        var configBasePath = Path.GetDirectoryName(install.ConfigPath);
        var portablePath = Path.Combine(install.RootPath, "portable");
        var hddGamePaths = new[]
        {
            configBasePath is null ? null : Path.Combine(configBasePath, "dev_hdd0", "game"),
            Path.Combine(install.RootPath, "dev_hdd0", "game"),
            Path.Combine(portablePath, "dev_hdd0", "game")
        }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>();

        foreach (var hddGamePath in hddGamePaths)
        {
            if (!Directory.Exists(hddGamePath))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(hddGamePath))
            {
                var folderName = Path.GetFileName(directory);
                if (!SerialPattern.IsMatch(folderName))
                {
                    continue;
                }

                var sfo = ParamSfoReader.TryReadForGamePath(directory);
                var serial = sfo?.TitleId ?? folderName;
                if (games.TryGetValue(serial, out var existing))
                {
                    games[serial] = existing with
                    {
                        Title = string.IsNullOrWhiteSpace(sfo?.Title) ? existing.Title : sfo!.Title!,
                        Version = SelectInstalledVersion(existing.Version, sfo?.AppVersion)
                    };
                    continue;
                }

                games[serial] = new Rpcs3GameDescriptor(
                    Serial: serial,
                    Title: sfo?.Title ?? serial,
                    InstallPath: directory,
                    Version: Rpcs3PatchVersion.Normalize(sfo?.AppVersion),
                    PpuHash: null,
                    SpuHash: null);
            }
        }
    }

    private static string? SelectInstalledVersion(string? baseVersion, string? updateVersion)
    {
        var normalizedBase = Rpcs3PatchVersion.Normalize(baseVersion);
        var normalizedUpdate = Rpcs3PatchVersion.Normalize(updateVersion);
        if (normalizedUpdate is null)
        {
            return normalizedBase;
        }

        if (normalizedBase is null)
        {
            return normalizedUpdate;
        }

        return System.Version.TryParse(normalizedUpdate, out var update) &&
               System.Version.TryParse(normalizedBase, out var @base) &&
               update < @base
            ? normalizedBase
            : normalizedUpdate;
    }

    private static string DeriveTitle(string serial, string installPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(installPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return serial;
        }

        var title = fileName.Replace('_', ' ');
        title = Regex.Replace(title, Regex.Escape(serial), string.Empty, RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"[-\[\]()]+", " ");
        title = Regex.Replace(title, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(title) ? serial : title;
    }
}
