using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class BundledCanaryPatchService
{
    public const string BundledFileName = "can.yml";

    private static readonly Regex HashKeyRegex = new(@"^(?<kind>PPU|SPU)-(?<hash>[0-9A-Fa-f]{40})$", RegexOptions.Compiled);

    private readonly CanaryPatchImportService _importService = new();

    public string GetBundledPath(string baseDirectory)
    {
        return Path.Combine(baseDirectory, "Data", BundledFileName);
    }

    public bool Exists(string baseDirectory)
    {
        return File.Exists(GetBundledPath(baseDirectory));
    }

    public string ReadText(string baseDirectory)
    {
        var path = GetBundledPath(baseDirectory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(UiText.T("Patch.BundledCanaryMissing"), path);
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    public CanaryPatchWriteResult PrepareImportedPatch(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        var selectedCanary = selectedMatches
            .Where(match => match.Patch.Risk == PatchRisk.Canary)
            .ToArray();

        if (selectedCanary.Length == 0)
        {
            return new CanaryPatchWriteResult(
                true,
                string.Empty,
                null,
                UiText.T("Patch.PrepareCanaryNoSelection"),
                null);
        }

        var bundledText = ReadText(baseDirectory);
        var selectedText = BuildSelectedCanaryText(bundledText, selectedCanary);
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return new CanaryPatchWriteResult(
                true,
                string.Empty,
                null,
                UiText.T("Patch.PrepareCanaryAlreadyInImported"),
                null);
        }

        return _importService.WriteManagedImportedPatch(patchesPath, selectedText);
    }

    public CanaryPatchRemoveResult RemovePreparedCanary(string patchesPath)
    {
        return _importService.RemoveManagedImportedPatch(patchesPath);
    }

    private static string BuildSelectedCanaryText(
        string bundledText,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        var selected = selectedMatches
            .Select(match => new SelectedPatchKey(match.Patch.HashKind, match.Patch.Hash, match.Patch.Title))
            .ToHashSet();

        var blocks = ExtractSelectedBlocks(bundledText, selected);
        if (blocks.Count == 0)
        {
            return string.Empty;
        }

        var output = new List<string>
        {
            CanaryPatchImportService.RequiredVersionHeader
        };

        foreach (var block in blocks)
        {
            output.Add(string.Empty);
            output.Add(block.HashKey + ":");
            foreach (var patchBlock in block.PatchBlocks)
            {
                output.AddRange(patchBlock);
            }
        }

        return string.Join("\r\n", output).TrimEnd() + "\r\n";
    }

    private static List<SelectedHashBlock> ExtractSelectedBlocks(
        string text,
        HashSet<SelectedPatchKey> selected)
    {
        var lines = SplitLines(text);
        var output = new List<SelectedHashBlock>();

        for (var index = 0; index < lines.Count;)
        {
            var hashKey = ReadTopLevelHashKey(lines[index]);
            if (hashKey is null)
            {
                index++;
                continue;
            }

            var hashKind = hashKey.StartsWith("SPU-", StringComparison.OrdinalIgnoreCase)
                ? PatchHashKind.Spu
                : PatchHashKind.Ppu;
            var hash = hashKey[4..];
            var patchBlocks = new List<List<string>>();

            index++;
            while (index < lines.Count && ReadTopLevelHashKey(lines[index]) is null)
            {
                if (!TryReadPatchEntryName(lines[index], out var title))
                {
                    index++;
                    continue;
                }

                var patchBlock = new List<string> { lines[index] };
                index++;

                while (index < lines.Count && ReadTopLevelHashKey(lines[index]) is null && !TryReadPatchEntryName(lines[index], out _))
                {
                    patchBlock.Add(lines[index]);
                    index++;
                }

                if (selected.Contains(new SelectedPatchKey(hashKind, hash, title)))
                {
                    patchBlocks.Add(patchBlock);
                }
            }

            if (patchBlocks.Count > 0)
            {
                output.Add(new SelectedHashBlock(hashKey, patchBlocks));
            }
        }

        return output;
    }

    private static string? ReadTopLevelHashKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
        {
            return null;
        }

        var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex <= 0)
        {
            return null;
        }

        var key = line[..colonIndex].Trim();
        return HashKeyRegex.IsMatch(key) ? key : null;
    }

    private static bool TryReadPatchEntryName(string line, out string patchName)
    {
        patchName = string.Empty;

        if (line.Length < 3 || !line.StartsWith("  ", StringComparison.Ordinal) || char.IsWhiteSpace(line[2]))
        {
            return false;
        }

        var trimmed = line.Trim();
        var colonIndex = FindPatchNameColon(trimmed);
        if (colonIndex <= 0)
        {
            return false;
        }

        patchName = trimmed[..colonIndex].Trim().Trim('"', '\'');
        return !string.IsNullOrWhiteSpace(patchName);
    }

    private static int FindPatchNameColon(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return -1;
        }

        if (value[0] is '"' or '\'')
        {
            var quote = value[0];
            for (var index = 1; index < value.Length; index++)
            {
                if (value[index] == quote && index + 1 < value.Length && value[index + 1] == ':')
                {
                    return index + 1;
                }
            }

            return -1;
        }

        return value.IndexOf(':', StringComparison.Ordinal);
    }

    private static List<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private readonly struct SelectedPatchKey : IEquatable<SelectedPatchKey>
    {
        public SelectedPatchKey(PatchHashKind hashKind, string hash, string title)
        {
            HashKind = hashKind;
            Hash = hash;
            Title = title;
        }

        private PatchHashKind HashKind { get; }

        private string Hash { get; }

        private string Title { get; }

        public bool Equals(SelectedPatchKey other)
        {
            return HashKind == other.HashKind &&
                   string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is SelectedPatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashKind,
                StringComparer.OrdinalIgnoreCase.GetHashCode(Hash),
                StringComparer.OrdinalIgnoreCase.GetHashCode(Title));
        }
    }

    private sealed record SelectedHashBlock(string HashKey, IReadOnlyList<IReadOnlyList<string>> PatchBlocks);
}
