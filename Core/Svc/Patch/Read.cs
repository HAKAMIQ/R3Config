using System;
using System.IO;
using R3Config.Mdl;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class CfgRead
{
    public YamlStream Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                return new YamlStream(new YamlDocument(new YamlMappingNode()));
            }

            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCfgBy,
                "patch_config.yml");
            var yaml = R3Bound.LoadYaml(
                text,
                "patch_config.yml");

            if (yaml.Documents.Count == 0)
            {
                yaml.Documents.Add(new YamlDocument(new YamlMappingNode()));
            }

            if (yaml.Documents.Count > R3Limits.YamlDoc)
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

    public bool IsOn(string path, PatChg change)
    {
        return ReadSnap(path).IsOn(change);
    }

    public CfgSnap ReadSnap(string path)
    {
        var yaml = Load(path);
        return new CfgSnap(
            (YamlMappingNode)yaml.Documents[0].RootNode);
    }

    internal static YamlNode? FindVal(YamlMappingNode mapping, string key)
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

    internal static YamlMappingNode? Find(
        YamlMappingNode root,
        params string[] path)
    {
        YamlMappingNode current = root;

        foreach (var key in path)
        {
            if (FindVal(current, key) is not YamlMappingNode next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    internal static YamlNode? FindNode(
        YamlMappingNode root,
        params string[] path)
    {
        YamlNode current = root;

        foreach (var key in path)
        {
            if (current is not YamlMappingNode mapping ||
                FindVal(mapping, key) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}

public sealed class CfgSnap
{
    private readonly YamlMappingNode _root;

    internal CfgSnap(YamlMappingNode root)
    {
        _root = root;
    }

    public bool IsOn(PatChg change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var versionNode = CfgRead.FindNode(
            _root,
            change.HashKey,
            change.PatTitle,
            change.GameName,
            change.Serial,
            change.Version);

        if (versionNode is YamlScalarNode legacyScalar &&
            bool.TryParse(legacyScalar.Value, out var legacyEnabled))
        {
            return legacyEnabled;
        }

        var enabledNode = versionNode is YamlMappingNode versionMapping
            ? CfgRead.FindVal(versionMapping, "Enabled")
            : null;

        return enabledNode is YamlScalarNode scalar &&
               bool.TryParse(scalar.Value, out var enabled) &&
               enabled;
    }
}

#pragma warning restore CA1822, IDE0130
