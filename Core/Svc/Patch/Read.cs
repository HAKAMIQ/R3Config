using System;
using System.IO;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class PatchConfigReader
{
    public YamlStream Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                return new YamlStream(new YamlDocument(new YamlMappingNode()));
            }

            var text = Rpcs3BoundedInput.ReadUtf8TextFile(
                path,
                Rpcs3InputLimits.PatchConfigYamlBytes,
                "patch_config.yml");
            var yaml = Rpcs3BoundedInput.LoadYaml(
                text,
                "patch_config.yml");

            if (yaml.Documents.Count == 0)
            {
                yaml.Documents.Add(new YamlDocument(new YamlMappingNode()));
            }

            if (yaml.Documents.Count > Rpcs3InputLimits.YamlDocuments)
            {
                throw new InvalidDataException(
                    "patch_config.yml contains too many YAML documents.");
            }

            if (yaml.Documents[0].RootNode is not YamlMappingNode)
            {
                throw new InvalidDataException("patch_config.yml root must be a mapping.");
            }

            return yaml;
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException("patch_config.yml is invalid YAML.", ex);
        }
    }

    public bool IsEnabled(string path, PatchApplyChange change)
    {
        return ReadSnapshot(path).IsEnabled(change);
    }

    public PatchConfigSnapshot ReadSnapshot(string path)
    {
        var yaml = Load(path);
        return new PatchConfigSnapshot(
            (YamlMappingNode)yaml.Documents[0].RootNode);
    }

    internal static YamlNode? FindValue(YamlMappingNode mapping, string key)
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                return child.Value;
            }
        }

        return null;
    }

    internal static YamlMappingNode? FindNestedMapping(
        YamlMappingNode root,
        params string[] path)
    {
        YamlMappingNode current = root;

        foreach (var key in path)
        {
            if (FindValue(current, key) is not YamlMappingNode next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    internal static YamlNode? FindNestedNode(
        YamlMappingNode root,
        params string[] path)
    {
        YamlNode current = root;

        foreach (var key in path)
        {
            if (current is not YamlMappingNode mapping ||
                FindValue(mapping, key) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}

public sealed class PatchConfigSnapshot
{
    private readonly YamlMappingNode _root;

    internal PatchConfigSnapshot(YamlMappingNode root)
    {
        _root = root;
    }

    public bool IsEnabled(PatchApplyChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var versionNode = PatchConfigReader.FindNestedNode(
            _root,
            change.HashKey,
            change.PatchTitle,
            change.GameTitle,
            change.Serial,
            change.Version);

        if (versionNode is YamlScalarNode legacyScalar &&
            bool.TryParse(legacyScalar.Value, out var legacyEnabled))
        {
            return legacyEnabled;
        }

        var enabledNode = versionNode is YamlMappingNode versionMapping
            ? PatchConfigReader.FindValue(versionMapping, "Enabled")
            : null;

        return enabledNode is YamlScalarNode scalar &&
               bool.TryParse(scalar.Value, out var enabled) &&
               enabled;
    }
}

#pragma warning restore CA1822, IDE0130
