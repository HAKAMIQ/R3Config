using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public static class Rpcs3ConfigYaml
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
        catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    public static string? ReadValue(string path, string section, string key)
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
        catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    public static void ApplyChanges(string path, IReadOnlyList<ConfigChange> changes)
    {
        var yaml = LoadYaml(path);
        var root = GetRootMapping(yaml);

        foreach (var sectionGroup in changes.GroupBy(change => change.Section))
        {
            var section = GetOrCreateMapping(root, sectionGroup.Key);

            foreach (var change in sectionGroup)
            {
                SetYamlPath(section, change.Key, NormalizeScalar(change.Value));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SaveAtomically(path, yaml);
    }

    public static void RemoveChanges(string path, IReadOnlyList<ConfigChange> changes)
    {
        if (!File.Exists(path) || changes.Count == 0)
        {
            return;
        }

        var yaml = LoadYaml(path);
        var root = GetRootMapping(yaml);

        foreach (var sectionGroup in changes.GroupBy(change => change.Section))
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
            File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SaveAtomically(path, yaml);
    }

    private static YamlStream LoadYaml(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new YamlStream(new YamlDocument(new YamlMappingNode()));
        }

        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            yaml.Documents.Add(new YamlDocument(new YamlMappingNode()));
        }

        return yaml;
    }

    private static YamlMappingNode GetRootMapping(YamlStream yaml)
    {
        if (yaml.Documents[0].RootNode is YamlMappingNode root)
        {
            return root;
        }

        var replacement = new YamlMappingNode();
        yaml.Documents[0] = new YamlDocument(replacement);
        return replacement;
    }

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode mapping, string key)
    {
        var existing = FindValue(mapping, key);
        if (existing is YamlMappingNode existingMapping)
        {
            return existingMapping;
        }

        var newMapping = new YamlMappingNode();
        SetValue(mapping, key, newMapping);
        return newMapping;
    }

    private static void SetYamlPath(YamlMappingNode section, string key, string value)
    {
        var path = key.Split('.', 2);
        if (path.Length == 1)
        {
            SetValue(section, key, new YamlScalarNode(value));
            return;
        }

        var nested = GetOrCreateMapping(section, path[0]);
        SetValue(nested, path[1], new YamlScalarNode(value));
    }

    private static YamlNode? FindValueByPath(YamlMappingNode section, string key)
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

    private static void RemoveYamlPath(YamlMappingNode section, string key)
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

    private static YamlNode? FindValue(YamlMappingNode mapping, string key)
    {
        return mapping.Children
            .Where(child => child.Key is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            .Select(child => child.Value)
            .FirstOrDefault();
    }

    private static void RemoveValue(YamlMappingNode mapping, string key)
    {
        var existingKey = mapping.Children.Keys
            .OfType<YamlScalarNode>()
            .FirstOrDefault(scalar => string.Equals(scalar.Value, key, StringComparison.Ordinal));

        if (existingKey is not null)
        {
            mapping.Children.Remove(existingKey);
        }
    }

    private static void SetValue(YamlMappingNode mapping, string key, YamlNode value)
    {
        var existingKey = mapping.Children.Keys
            .OfType<YamlScalarNode>()
            .FirstOrDefault(scalar => string.Equals(scalar.Value, key, StringComparison.Ordinal));

        if (existingKey is not null)
        {
            mapping.Children[existingKey] = value;
            return;
        }

        mapping.Add(new YamlScalarNode(key), value);
    }

    private static string NormalizeScalar(string value)
    {
        if (value.EndsWith('%') && int.TryParse(value[..^1], out var percent))
        {
            return percent.ToString();
        }

        return value;
    }

    private static void SaveAtomically(string path, YamlStream yaml)
    {
        var directory = Path.GetDirectoryName(path)!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var writer = new StreamWriter(tempPath, append: false, Utf8NoBom))
            {
                writer.NewLine = "\r\n";
                yaml.Save(writer, assignAnchors: false);
            }

            using (var reader = new StreamReader(tempPath))
            {
                var validationYaml = new YamlStream();
                validationYaml.Load(reader);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
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
