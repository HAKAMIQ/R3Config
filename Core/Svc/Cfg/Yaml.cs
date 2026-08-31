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

public static class R3Yaml
{
    private static readonly Encoding Utf8Bom =
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
            _ = GetRoot(yaml);
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

    public static void VldText(string text)
    {
        R3Bound.Check(
            text,
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");
        var yaml = R3Bound.LoadYaml(
            text,
            "RPCS3 custom config YAML");
        if (yaml.Documents.Count == 0 ||
            yaml.Documents.Count > R3Limits.YamlDoc)
        {
            throw new InvalidDataException(
                "RPCS3 custom config contains an invalid YAML document count.");
        }

        _ = GetRoot(yaml);
    }

    public static string? ReadVal(
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
            var root = GetRoot(yaml);

            if (FindVal2(root, section) is not YamlMappingNode sectionNode)
            {
                return null;
            }

            var value = FindVal(sectionNode, key);

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

    public static void Apply(
        string path,
        IReadOnlyList<CfgChg> changes,
        Action? validateBeforeCommit = null)
    {
        Commit(
            path,
            Build(path, changes),
            validateBeforeCommit);
    }

    public static string Build(
        string path,
        IReadOnlyList<CfgChg> changes)
    {
        var yaml = LoadYaml(path);
        var root = GetRoot(yaml);

        foreach (var sectionGroup in changes.GroupBy(
                     change => change.Section))
        {
            var section = GetOr(
                root,
                sectionGroup.Key);

            foreach (var change in sectionGroup)
            {
                SetYaml(
                    section,
                    change.Key,
                    Norm(change.Value));
            }
        }

        return Write(yaml);
    }

    public static void Del(
        string path,
        IReadOnlyList<CfgChg> changes,
        Action? validateBeforeCommit = null)
    {
        if (!File.Exists(path) || changes.Count == 0)
        {
            return;
        }

        var prepared = BuildDel(path, changes);
        Commit(path, prepared, validateBeforeCommit);
    }

    public static string? BuildDel(
        string path,
        IReadOnlyList<CfgChg> changes)
    {
        if (!File.Exists(path) || changes.Count == 0)
        {
            return null;
        }

        var yaml = LoadYaml(path);
        var root = GetRoot(yaml);

        foreach (var sectionGroup in changes.GroupBy(
                     change => change.Section))
        {
            if (FindVal2(root, sectionGroup.Key) is not YamlMappingNode section)
            {
                continue;
            }

            foreach (var change in sectionGroup)
            {
                DelYaml(section, change.Key);
            }

            if (section.Children.Count == 0)
            {
                DelVal(root, sectionGroup.Key);
            }
        }

        if (root.Children.Count == 0)
        {
            return null;
        }

        return Write(yaml);
    }

    public static void Commit(
        string path,
        string? preparedText,
        Action? validateBeforeCommit = null)
    {
        if (preparedText is null)
        {
            validateBeforeCommit?.Invoke();
            File.Delete(path);
            return;
        }

        Save(path, preparedText, validateBeforeCommit);
    }

    private static YamlStream LoadYaml(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new YamlStream(
                new YamlDocument(
                    new YamlMappingNode()));
        }

        var text = R3Bound.ReadUtf8(
            path,
            R3Limits.CfgByte,
            "RPCS3 custom config YAML");
        R3Bound.VldYaml(
            text,
            "RPCS3 custom config YAML");
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));

        if (yaml.Documents.Count == 0)
        {
            yaml.Documents.Add(
                new YamlDocument(
                    new YamlMappingNode()));
        }

        if (yaml.Documents.Count > R3Limits.YamlDoc)
        {
            throw new InvalidDataException(
                "RPCS3 custom config contains too many YAML documents.");
        }

        return yaml;
    }

    private static YamlMappingNode GetRoot(YamlStream yaml)
    {
        if (yaml.Documents[0].RootNode is YamlMappingNode root)
        {
            return root;
        }

        throw new InvalidDataException(
            "RPCS3 config root must be a YAML mapping.");
    }

    private static YamlMappingNode GetOr(
        YamlMappingNode mapping,
        string key)
    {
        var existing = FindVal2(mapping, key);

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

    private static void SetYaml(
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

        var nested = GetOr(
            section,
            path[0]);

        SetValue(
            nested,
            path[1],
            new YamlScalarNode(value));
    }

    private static YamlNode? FindVal(
        YamlMappingNode section,
        string key)
    {
        var path = key.Split('.', 2);

        if (path.Length == 1)
        {
            return FindVal2(section, key);
        }

        return FindVal2(section, path[0]) is YamlMappingNode nested
            ? FindVal2(nested, path[1])
            : null;
    }

    private static void DelYaml(
        YamlMappingNode section,
        string key)
    {
        var path = key.Split('.', 2);

        if (path.Length == 1)
        {
            DelVal(section, key);
            return;
        }

        if (FindVal2(section, path[0]) is not YamlMappingNode nested)
        {
            return;
        }

        DelVal(nested, path[1]);

        if (nested.Children.Count == 0)
        {
            DelVal(section, path[0]);
        }
    }

    private static YamlNode? FindVal2(
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

    private static void DelVal(
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

    private static string Norm(string value)
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

    private static string Write(YamlStream yaml)
    {
        using var writer = new StringWriter(
            CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        };
        yaml.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static void Save(
        string path,
        string preparedText,
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
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       Utf8Bom,
                       bufferSize: 16 * 1024,
                       leaveOpen: true))
            {
                writer.NewLine = "\r\n";
                writer.Write(preparedText);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            StagedFileGuard.Validate(
                tempPath,
                fullPath,
                "custom config");

            var validationText = R3Bound.ReadUtf8(
                tempPath,
                R3Limits.CfgByte,
                "Prepared RPCS3 custom config YAML");
            using (var reader = new StringReader(validationText))
            {
                var validationYaml = new YamlStream();
                R3Bound.VldYaml(
                    validationText,
                    "Prepared RPCS3 custom config YAML");
                validationYaml.Load(reader);
                if (validationYaml.Documents.Count == 0 ||
                    validationYaml.Documents.Count > R3Limits.YamlDoc)
                {
                    throw new InvalidDataException(
                        "Prepared RPCS3 custom config contains an invalid YAML document count.");
                }
                _ = GetRoot(validationYaml);
            }

            validateBeforeCommit?.Invoke();
            StagedFileGuard.Validate(
                tempPath,
                fullPath,
                "custom config");

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
