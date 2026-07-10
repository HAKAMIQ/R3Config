using System;
using System.IO;
using System.Text;
using R3Config.Mdl;
using YamlDotNet.RepresentationModel;

namespace R3Config.Svc;

public sealed class PatchConfigWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly PatchConfigReader _reader;

    public PatchConfigWriter(PatchConfigReader? reader = null)
    {
        _reader = reader ?? new PatchConfigReader();
    }

    public void Write(string path, IReadOnlyList<PatchApplyChange> changes, bool enabled = true)
    {
        var yaml = _reader.Load(path);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        foreach (var change in changes)
        {
            if (enabled)
            {
                var versionNode = GetOrCreateNestedMapping(
                    root,
                    change.HashKey,
                    change.PatchTitle,
                    change.GameTitle,
                    change.Serial,
                    change.Version);
                SetValue(versionNode, "Enabled", new YamlScalarNode("true"));
            }
            else
            {
                RemoveEnabledAndPrune(
                    root,
                    change.HashKey,
                    change.PatchTitle,
                    change.GameTitle,
                    change.Serial,
                    change.Version);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SaveAtomically(path, yaml);
    }

    private static YamlMappingNode GetOrCreateNestedMapping(YamlMappingNode root, params string[] path)
    {
        var current = root;
        foreach (var key in path)
        {
            if (PatchConfigReader.FindValue(current, key) is YamlMappingNode existing)
            {
                current = existing;
                continue;
            }

            var created = new YamlMappingNode();
            SetValue(current, key, created);
            current = created;
        }

        return current;
    }

    private static void SetValue(YamlMappingNode mapping, string key, YamlNode value)
    {
        foreach (var existingKey in mapping.Children.Keys)
        {
            if (existingKey is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                mapping.Children[existingKey] = value;
                return;
            }
        }

        mapping.Add(new YamlScalarNode(key), value);
    }

    private static void RemoveEnabledAndPrune(YamlMappingNode root, params string[] path)
    {
        var mappings = new List<YamlMappingNode> { root };
        var keys = new List<YamlNode>();
        YamlNode current = root;

        foreach (var segment in path)
        {
            if (current is not YamlMappingNode mapping || !TryFindChild(mapping, segment, out var keyNode, out var valueNode))
            {
                return;
            }

            keys.Add(keyNode);
            current = valueNode;
            if (valueNode is YamlMappingNode childMapping)
            {
                mappings.Add(childMapping);
            }
        }

        var versionKey = keys[^1];
        if (current is YamlMappingNode versionMapping)
        {
            var versionParent = mappings[^2];
            RemoveValue(versionMapping, "Enabled");
            if (versionMapping.Children.Count == 0)
            {
                versionParent.Children.Remove(versionKey);
            }
        }
        else
        {
            // RPCS3 still accepts the legacy "version: true" shape. Disabling it
            // removes the leaf just like RPCS3's current Patch Manager does.
            mappings[^1].Children.Remove(versionKey);
        }

        var pruneFrom = current is YamlMappingNode ? mappings.Count - 2 : mappings.Count - 1;
        for (var index = pruneFrom; index > 0; index--)
        {
            var mapping = mappings[index];
            if (mapping.Children.Count > 0)
            {
                break;
            }

            mappings[index - 1].Children.Remove(keys[index - 1]);
        }
    }

    private static bool TryFindChild(
        YamlMappingNode mapping,
        string key,
        out YamlNode keyNode,
        out YamlNode valueNode)
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                keyNode = child.Key;
                valueNode = child.Value;
                return true;
            }
        }

        keyNode = null!;
        valueNode = null!;
        return false;
    }

    private static void RemoveValue(YamlMappingNode mapping, string key)
    {
        var keyNode = mapping.Children.Keys.FirstOrDefault(candidate =>
            candidate is YamlScalarNode scalar &&
            string.Equals(scalar.Value, key, StringComparison.Ordinal));

        if (keyNode is not null)
        {
            mapping.Children.Remove(keyNode);
        }
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
