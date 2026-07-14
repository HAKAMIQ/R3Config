using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class Rpcs3ConfigYaml
{
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static bool CanRead(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            var yaml = LoadYaml(path);
            _ = GetRootMapping(yaml);
            return true;
        }
        catch (Exception ex) when (
            ex is YamlException or
                IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            return false;
        }
    }

    public static string? ReadValue(
        string path,
        string section,
        string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var yaml = LoadYaml(path);
            var root = GetRootMapping(yaml);

            if (FindValue(root, section) is not YamlMappingNode sectionNode)
            {
                return null;
            }

            var value = FindValueByPath(sectionNode, key);

            return value switch
            {
                YamlScalarNode scalar => scalar.Value,
                null => null,
                _ => value.ToString()
            };
        }
        catch (Exception ex) when (
            ex is YamlException or
                IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            return null;
        }
    }

    public static void ApplyChanges(
        string path,
        IReadOnlyList<ConfigChange> changes,
        Action? validateBeforeCommit = null)
    {
        var yaml = LoadYaml(path);
        var root = GetRootMapping(yaml);

        foreach (var sectionGroup in changes.GroupBy(
                     change => change.Section))
        {
            var section = GetOrCreateMapping(
                root,
                sectionGroup.Key);

            foreach (var change in sectionGroup)
            {
                SetYamlPath(
                    section,
                    change.Key,
                    NormalizeScalar(change.Value));
            }
        }

        SaveAtomically(
            path,
            yaml,
            validateBeforeCommit);
    }

    public static void RemoveChanges(
        string path,
        IReadOnlyList<ConfigChange> changes,
        Action? validateBeforeCommit = null)
    {
        if (!File.Exists(path) || changes.Count == 0)
        {
            return;
        }

        var yaml = LoadYaml(path);
        var root = GetRootMapping(yaml);

        foreach (var sectionGroup in changes.GroupBy(
                     change => change.Section))
        {
            if (FindValue(root, sectionGroup.Key) is not YamlMappingNode section)
            {
                continue;
            }

            foreach (var change in sectionGroup)
            {
                RemoveYamlPath(section, change.Key);
            }

            if (section.Children.Count == 0)
            {
                RemoveValue(root, sectionGroup.Key);
            }
        }

        if (root.Children.Count == 0)
        {
            validateBeforeCommit?.Invoke();
            File.Delete(path);
            return;
        }

        SaveAtomically(
            path,
            yaml,
            validateBeforeCommit);
    }

    private static YamlStream LoadYaml(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new YamlStream(
                new YamlDocument(
                    new YamlMappingNode()));
        }

        var text = Rpcs3BoundedInput.ReadUtf8TextFile(
            path,
            Rpcs3InputLimits.CustomConfigYamlBytes,
            "RPCS3 custom config YAML");
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));

        if (yaml.Documents.Count == 0)
        {
            yaml.Documents.Add(
                new YamlDocument(
                    new YamlMappingNode()));
        }

        if (yaml.Documents.Count > Rpcs3InputLimits.YamlDocuments)
        {
            throw new InvalidDataException(
                "RPCS3 custom config contains too many YAML documents.");
        }

        return yaml;
    }

    private static YamlMappingNode GetRootMapping(YamlStream yaml)
    {
        if (yaml.Documents[0].RootNode is YamlMappingNode root)
        {
            return root;
        }

        throw new InvalidDataException(
            "RPCS3 config root must be a YAML mapping.");
    }

    private static YamlMappingNode GetOrCreateMapping(
        YamlMappingNode mapping,
        string key)
    {
        var existing = FindValue(mapping, key);

        if (existing is YamlMappingNode existingMapping)
        {
            return existingMapping;
        }

        if (existing is not null)
        {
            throw new InvalidDataException(
                $"RPCS3 config key '{key}' must be a YAML mapping.");
        }

        var newMapping = new YamlMappingNode();
        SetValue(mapping, key, newMapping);
        return newMapping;
    }

    private static void SetYamlPath(
        YamlMappingNode section,
        string key,
        string value)
    {
        var path = key.Split('.', 2);

        if (path.Length == 1)
        {
            SetValue(
                section,
                key,
                new YamlScalarNode(value));

            return;
        }

        var nested = GetOrCreateMapping(
            section,
            path[0]);

        SetValue(
            nested,
            path[1],
            new YamlScalarNode(value));
    }

    private static YamlNode? FindValueByPath(
        YamlMappingNode section,
        string key)
    {
        var path = key.Split('.', 2);

        if (path.Length == 1)
        {
            return FindValue(section, key);
        }

        return FindValue(section, path[0]) is YamlMappingNode nested
            ? FindValue(nested, path[1])
            : null;
    }

    private static void RemoveYamlPath(
        YamlMappingNode section,
        string key)
    {
        var path = key.Split('.', 2);

        if (path.Length == 1)
        {
            RemoveValue(section, key);
            return;
        }

        if (FindValue(section, path[0]) is not YamlMappingNode nested)
        {
            return;
        }

        RemoveValue(nested, path[1]);

        if (nested.Children.Count == 0)
        {
            RemoveValue(section, path[0]);
        }
    }

    private static YamlNode? FindValue(
        YamlMappingNode mapping,
        string key)
    {
        return mapping.Children
            .Where(child =>
                child.Key is YamlScalarNode scalar &&
                string.Equals(
                    scalar.Value,
                    key,
                    StringComparison.Ordinal))
            .Select(child => child.Value)
            .FirstOrDefault();
    }

    private static void RemoveValue(
        YamlMappingNode mapping,
        string key)
    {
        var existingKey = mapping.Children.Keys
            .OfType<YamlScalarNode>()
            .FirstOrDefault(scalar =>
                string.Equals(
                    scalar.Value,
                    key,
                    StringComparison.Ordinal));

        if (existingKey is not null)
        {
            mapping.Children.Remove(existingKey);
        }
    }

    private static void SetValue(
        YamlMappingNode mapping,
        string key,
        YamlNode value)
    {
        var existingKey = mapping.Children.Keys
            .OfType<YamlScalarNode>()
            .FirstOrDefault(scalar =>
                string.Equals(
                    scalar.Value,
                    key,
                    StringComparison.Ordinal));

        if (existingKey is not null)
        {
            mapping.Children[existingKey] = value;
            return;
        }

        mapping.Add(
            new YamlScalarNode(key),
            value);
    }

    private static string NormalizeScalar(string value)
    {
        if (value.EndsWith('%') &&
            int.TryParse(
                value[..^1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var percent))
        {
            return percent.ToString(
                CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static void SaveAtomically(
        string path,
        YamlStream yaml,
        Action? validateBeforeCommit)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "RPCS3 config path has no parent directory.");

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var writer = new StreamWriter(
                       tempPath,
                       append: false,
                       Utf8NoBom))
            {
                writer.NewLine = "\r\n";
                yaml.Save(writer, assignAnchors: false);
            }

            var validationText = Rpcs3BoundedInput.ReadUtf8TextFile(
                tempPath,
                Rpcs3InputLimits.CustomConfigYamlBytes,
                "Prepared RPCS3 custom config YAML");
            using (var reader = new StringReader(validationText))
            {
                var validationYaml = new YamlStream();
                validationYaml.Load(reader);
                if (validationYaml.Documents.Count == 0 ||
                    validationYaml.Documents.Count > Rpcs3InputLimits.YamlDocuments)
                {
                    throw new InvalidDataException(
                        "Prepared RPCS3 custom config contains an invalid YAML document count.");
                }
                _ = GetRootMapping(validationYaml);
            }

            validateBeforeCommit?.Invoke();

            if (File.Exists(fullPath))
            {
                File.Replace(
                    tempPath,
                    fullPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

#pragma warning restore IDE0130
