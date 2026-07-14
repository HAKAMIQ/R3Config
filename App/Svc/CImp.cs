using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record CanaryPatchNormalizeResult(
    bool Succeeded,
    string NormalizedText,
    bool DuplicatePpuMerged,
    string Message,
    string? Error);

public sealed record CanaryPatchWriteResult(
    bool Succeeded,
    string TargetPath,
    string? BackupPath,
    string Message,
    string? Error);

public sealed record CanaryPatchRemoveResult(
    bool Succeeded,
    string TargetPath,
    string? BackupPath,
    string Message,
    string? Error);

public sealed record CanaryPatchStageResult(
    bool Succeeded,
    string TargetPath,
    PatchPreparedFile? PreparedFile,
    string Message,
    string? Error);

public sealed class CanaryPatchImportService
{
    public const string ImportedPatchFileName = "imported_patch.yml";
    public const string RequiredVersionHeader = "Version: 1.2";

    private const long MaxImportedPatchBytes = 128L * 1024L * 1024L;

    private const string ManagedBlockBegin = "# R3Config managed Canary patches - begin";
    private const string ManagedBlockEnd = "# R3Config managed Canary patches - end";
    private const string BundledSourceMarker = "R3Config bundled RPCS3 Canary patches";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly Regex TopLevelKeyRegex = new(@"^(?<key>[^\s:#][^:#]*):(?:\s.*)?$", RegexOptions.Compiled);

    private readonly PatchCatalogReader _catalogReader;

    public CanaryPatchImportService(
        PatchCatalogReader? catalogReader = null)
    {
        _catalogReader = catalogReader ?? new PatchCatalogReader();
    }

    public CanaryPatchNormalizeResult Normalize(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return Failure(UiText.T("Patch.ImportCanaryInvalidYaml"));
        }

        var normalizedInput = sourceText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        var lines = normalizedInput.Split('\n');
        var prefixLines = new List<string>();
        var ppuBlocks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ppuPatchNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var ppuOrder = new List<string>();
        var duplicatePpuMerged = false;

        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            var topKey = TryReadTopLevelKey(line);

            if (string.Equals(topKey, "Version", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (topKey is not null && IsPatchRuntimeKey(topKey))
            {
                var blockLines = new List<string>();
                index++;

                while (index < lines.Length && !IsTopLevelKey(lines[index]))
                {
                    blockLines.Add(lines[index]);
                    index++;
                }

                if (!ppuBlocks.TryGetValue(topKey, out var existingBody))
                {
                    existingBody = [];
                    ppuBlocks[topKey] = existingBody;
                    ppuPatchNames[topKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    ppuOrder.Add(topKey);
                }
                else
                {
                    duplicatePpuMerged = true;
                }

                var filteredBlockLines = FilterDuplicatePatchBlocks(
                    blockLines,
                    ppuPatchNames[topKey],
                    out var duplicatePatchSkipped);

                if (duplicatePatchSkipped)
                {
                    duplicatePpuMerged = true;
                }

                existingBody.AddRange(filteredBlockLines);
                continue;
            }

            prefixLines.Add(line);
            index++;
        }

        if (ppuBlocks.Count == 0)
        {
            return Failure(UiText.T("Patch.ImportCanaryInvalidYaml"));
        }

        var output = new List<string> { RequiredVersionHeader };
        var cleanedPrefix = TrimEmptyEdges(prefixLines);

        if (cleanedPrefix.Count > 0)
        {
            output.Add(string.Empty);
            output.AddRange(cleanedPrefix);
        }

        foreach (var ppu in ppuOrder)
        {
            output.Add(string.Empty);
            output.Add(ppu + ":");
            output.AddRange(TrimEmptyEdges(ppuBlocks[ppu]));
        }

        var normalized = string.Join("\r\n", output).TrimEnd() + "\r\n";
        if (HasDuplicateTopLevelPatchHash(normalized))
        {
            return Failure(UiText.T("Patch.ImportCanaryDuplicateKeyUnmerged"));
        }

        return new CanaryPatchNormalizeResult(
            true,
            normalized,
            duplicatePpuMerged,
            duplicatePpuMerged
                ? UiText.T("Patch.ImportCanaryDuplicatePpuMerged")
                : UiText.T("Patch.ImportCanaryFormatValid"),
            null);
    }

    public CanaryPatchStageResult PrepareManagedImportedPatchFile(
        string patchesPath,
        string sourceText)
    {
        string targetPath = string.Empty;

        try
        {
            var validation = ValidateTargetPath(
                patchesPath,
                out _,
                out targetPath,
                out var validationError);
            if (!validation)
            {
                return new CanaryPatchStageResult(
                    false,
                    targetPath,
                    null,
                    validationError,
                    validationError);
            }

            var normalize = Normalize(sourceText);
            if (!normalize.Succeeded)
            {
                var error = normalize.Error ?? normalize.Message;
                return new CanaryPatchStageResult(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            var incomingCatalog = _catalogReader.ReadText(
                normalize.NormalizedText,
                PatchSourceOrigin.Imported,
                targetPath);
            if (!incomingCatalog.Succeeded)
            {
                var error = incomingCatalog.Error ??
                            UiText.T("Patch.ImportCanaryInvalidYaml");
                return new CanaryPatchStageResult(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            var existingText = File.Exists(targetPath)
                ? ReadImportedPatchText(targetPath)
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(existingText))
            {
                var existingCatalog = _catalogReader.ReadText(
                    existingText,
                    PatchSourceOrigin.Imported,
                    targetPath);
                if (!existingCatalog.Succeeded)
                {
                    var error = existingCatalog.Error ??
                                UiText.T("Patch.ImportCanaryInvalidYaml");
                    return new CanaryPatchStageResult(
                        false,
                        targetPath,
                        null,
                        error,
                        error);
                }

                var existingNormalized = Normalize(existingText);
                if (existingNormalized.Succeeded &&
                    string.Equals(
                        existingNormalized.NormalizedText,
                        normalize.NormalizedText,
                        StringComparison.Ordinal))
                {
                    return new CanaryPatchStageResult(
                        true,
                        targetPath,
                        null,
                        UiText.T("Patch.PrepareCanaryAlreadyReady"),
                        null);
                }
            }

            var preservedText = !string.IsNullOrWhiteSpace(existingText) &&
                                (!LooksR3ConfigGenerated(existingText) ||
                                 ContainsManagedBlock(existingText))
                ? RemoveManagedBlock(existingText)
                : string.Empty;

            var finalText = ComposeManagedImportedPatch(
                preservedText,
                normalize.NormalizedText);

            if (File.Exists(targetPath) &&
                string.Equals(existingText, finalText, StringComparison.Ordinal))
            {
                return new CanaryPatchStageResult(
                    true,
                    targetPath,
                    null,
                    UiText.T("Patch.PrepareCanaryAlreadyReady"),
                    null);
            }

            var finalCatalog = _catalogReader.ReadText(
                finalText,
                PatchSourceOrigin.Imported,
                targetPath);
            if (!finalText.StartsWith(
                    RequiredVersionHeader,
                    StringComparison.Ordinal) ||
                !finalCatalog.Succeeded)
            {
                var error = finalCatalog.Error ??
                            UiText.T("Patch.ImportCanaryInvalidYaml");
                return new CanaryPatchStageResult(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            return new CanaryPatchStageResult(
                true,
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                     finalText,
                     PatchPreparedFileKind.PatchCatalog,
                     PatchSourceOrigin.Imported,
                     finalCatalog.Catalog.Entries.Count),
                 UiText.T("Patch.PrepareCanarySelectedSuccess"),
                 null);
        }
        catch (Exception ex)
        {
            var error = SafeErrorMessage(ex);
            return new CanaryPatchStageResult(
                false,
                targetPath,
                null,
                error,
                error);
        }
    }

    public CanaryPatchWriteResult WriteManagedImportedPatch(
        string patchesPath,
        string sourceText)
    {
        string targetPath = string.Empty;
        string? backupPath = null;
        string? tempPath = null;

        try
        {
            var staged = PrepareManagedImportedPatchFile(
                patchesPath,
                sourceText);
            targetPath = staged.TargetPath;

            if (!staged.Succeeded)
            {
                return WriteFailure(
                    targetPath,
                    null,
                    staged.Error ?? staged.Message);
            }

            if (staged.PreparedFile is null)
            {
                return new CanaryPatchWriteResult(
                    true,
                    targetPath,
                    null,
                    staged.Message,
                    null);
            }

            var patchesDir = Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException(
                    "The managed Canary target has no parent directory.");
            Directory.CreateDirectory(patchesDir);

            if (File.Exists(targetPath))
            {
                backupPath = CreateBackupPath(patchesDir);
                File.Copy(
                    targetPath,
                    backupPath,
                    overwrite: false);
            }

            tempPath = targetPath + ".tmp";
            File.WriteAllText(
                tempPath,
                staged.PreparedFile.Content,
                Utf8NoBom);

            var tempReadBack = File.ReadAllText(
                tempPath,
                Encoding.UTF8);
            if (!tempReadBack.StartsWith(
                    RequiredVersionHeader,
                    StringComparison.Ordinal) ||
                !Normalize(tempReadBack).Succeeded)
            {
                TryDeleteFile(tempPath);
                return WriteFailure(
                    targetPath,
                    backupPath,
                    UiText.T("Patch.ImportCanaryInvalidYaml"));
            }

            File.Move(
                tempPath,
                targetPath,
                overwrite: true);
            tempPath = null;

            var readBack = File.ReadAllText(
                targetPath,
                Encoding.UTF8);
            if (!string.Equals(
                    readBack,
                    staged.PreparedFile.Content,
                    StringComparison.Ordinal))
            {
                return WriteFailure(
                    targetPath,
                    backupPath,
                    UiText.T("Patch.ImportCanaryInvalidYaml"));
            }

            return new CanaryPatchWriteResult(
                true,
                targetPath,
                backupPath,
                staged.Message,
                null);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                TryDeleteFile(tempPath);
            }

            return WriteFailure(
                targetPath,
                backupPath,
                SafeErrorMessage(ex));
        }
    }

    public CanaryPatchRemoveResult RemoveManagedImportedPatch(string patchesPath)
    {
        string targetPath = string.Empty;
        string? backupPath = null;
        string? tempPath = null;

        try
        {
            var validation = ValidateTargetPath(patchesPath, out var patchesDir, out targetPath, out var validationError);
            if (!validation)
            {
                return RemoveFailure(targetPath, null, validationError);
            }

            if (!File.Exists(targetPath))
            {
                return new CanaryPatchRemoveResult(
                    true,
                    targetPath,
                    null,
                    UiText.T("Patch.RemoveCanaryNothing"),
                    null);
            }

            var existingText = File.ReadAllText(targetPath, Encoding.UTF8);
            string finalText;

            if (LooksR3ConfigGenerated(existingText))
            {
                finalText = string.Empty;
            }
            else if (ContainsManagedBlock(existingText))
            {
                finalText = ComposeManagedImportedPatch(RemoveManagedBlock(existingText), string.Empty);
            }
            else
            {
                return new CanaryPatchRemoveResult(
                    true,
                    targetPath,
                    null,
                    UiText.T("Patch.RemoveCanaryNothing"),
                    null);
            }

            backupPath = CreateBackupPath(patchesDir);
            File.Copy(targetPath, backupPath, overwrite: false);

            if (string.IsNullOrWhiteSpace(BodyWithoutVersion(finalText)))
            {
                File.Delete(targetPath);
                return new CanaryPatchRemoveResult(
                    true,
                    targetPath,
                    backupPath,
                    UiText.T("Patch.RemoveCanarySuccess"),
                    null);
            }

            tempPath = targetPath + ".tmp";
            File.WriteAllText(tempPath, finalText, Utf8NoBom);
            if (!Normalize(File.ReadAllText(tempPath, Encoding.UTF8)).Succeeded)
            {
                TryDeleteFile(tempPath);
                return RemoveFailure(targetPath, backupPath, UiText.T("Patch.ImportCanaryInvalidYaml"));
            }

            File.Move(tempPath, targetPath, overwrite: true);
            tempPath = null;

            return new CanaryPatchRemoveResult(
                true,
                targetPath,
                backupPath,
                UiText.T("Patch.RemoveCanarySuccess"),
                null);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                TryDeleteFile(tempPath);
            }

            return RemoveFailure(targetPath, backupPath, SafeErrorMessage(ex));
        }
    }

    public bool HasManagedImportedPatch(string patchesPath)
    {
        try
        {
            var validation = ValidateTargetPath(patchesPath, out _, out var targetPath, out _);
            if (!validation || !File.Exists(targetPath))
            {
                return false;
            }

            var text = File.ReadAllText(targetPath, Encoding.UTF8);
            return ContainsManagedBlock(text) || LooksR3ConfigGenerated(text);
        }
        catch
        {
            return false;
        }
    }

    private static CanaryPatchNormalizeResult Failure(string error)
    {
        return new CanaryPatchNormalizeResult(false, string.Empty, false, error, error);
    }

    private static CanaryPatchWriteResult WriteFailure(string targetPath, string? backupPath, string error)
    {
        return new CanaryPatchWriteResult(false, targetPath, backupPath, error, error);
    }

    private static bool IsTopLevelKey(string line)
    {
        return TryReadTopLevelKey(line) is not null;
    }

    private static string? TryReadTopLevelKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
        {
            return null;
        }

        var match = TopLevelKeyRegex.Match(line);
        return match.Success ? match.Groups["key"].Value.Trim() : null;
    }

    public string ReadImportedPatchText(string targetPath)
    {
        using var stream = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        if (stream.Length > MaxImportedPatchBytes)
        {
            throw new InvalidDataException(
                $"The imported patch file exceeds the {MaxImportedPatchBytes}-byte read limit.");
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);

        return reader.ReadToEnd();
    }

    private static bool IsPatchRuntimeKey(string value)
    {
        return Rpcs3PatchRuntimeKey.TryParse(value, out _);
    }

    private static List<string> FilterDuplicatePatchBlocks(
        IReadOnlyList<string> blockLines,
        HashSet<string> knownPatchNames,
        out bool duplicatePatchSkipped)
    {
        var output = new List<string>();
        duplicatePatchSkipped = false;

        for (var index = 0; index < blockLines.Count;)
        {
            var line = blockLines[index];
            if (!TryReadPatchEntryName(line, out var patchName))
            {
                output.Add(line);
                index++;
                continue;
            }

            var patchBlock = new List<string> { line };
            index++;

            while (index < blockLines.Count && !TryReadPatchEntryName(blockLines[index], out _))
            {
                patchBlock.Add(blockLines[index]);
                index++;
            }

            if (!knownPatchNames.Add(patchName))
            {
                duplicatePatchSkipped = true;
                continue;
            }

            output.AddRange(patchBlock);
        }

        return output;
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

        var candidate = trimmed[..colonIndex].Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        patchName = candidate;
        return true;
    }

    private static int FindPatchNameColon(string value)
    {
        if (value.Length == 0)
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

    private static List<string> TrimEmptyEdges(IEnumerable<string> lines)
    {
        var list = lines.ToList();

        while (list.Count > 0 && string.IsNullOrWhiteSpace(list[0]))
        {
            list.RemoveAt(0);
        }

        while (list.Count > 0 && string.IsNullOrWhiteSpace(list[^1]))
        {
            list.RemoveAt(list.Count - 1);
        }

        return list;
    }

    private static bool HasDuplicateTopLevelPatchHash(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var key = TryReadTopLevelKey(line);
            if (key is not null && IsPatchRuntimeKey(key) && !seen.Add(key))
            {
                return true;
            }
        }

        return false;
    }

    private static string CreateBackupPath(string patchesDir)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(patchesDir, ImportedPatchFileName + ".bak-" + timestamp);

        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; index <= 999; index++)
        {
            var candidate = Path.Combine(
                patchesDir,
                ImportedPatchFileName + ".bak-" + timestamp + "-" + index.ToString("000", CultureInfo.InvariantCulture));

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(
            patchesDir,
            ImportedPatchFileName + ".bak-" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture));
    }


    private static bool ValidateTargetPath(string patchesPath, out string patchesDir, out string targetPath, out string error)
    {
        patchesDir = string.Empty;
        targetPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(patchesPath))
        {
            error = UiText.T("Patch.ImportCanaryNoRpcs3Path");
            return false;
        }

        patchesDir = Path.GetFullPath(patchesPath);
        var parentPath = Directory.GetParent(patchesDir)?.FullName;
        targetPath = Path.GetFullPath(Path.Combine(patchesDir, ImportedPatchFileName));

        if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
        {
            error = UiText.T("Patch.ImportCanaryNoRpcs3Path");
            return false;
        }

        if (!IsPathInsideDirectory(targetPath, patchesDir) || !IsPathInsideDirectory(patchesDir, parentPath))
        {
            error = UiText.T("Patch.ImportCanaryUnsafePath");
            return false;
        }

        return true;
    }

    private static CanaryPatchRemoveResult RemoveFailure(string targetPath, string? backupPath, string error)
    {
        return new CanaryPatchRemoveResult(false, targetPath, backupPath, error, error);
    }

    private string ComposeManagedImportedPatch(string preservedText, string managedText)
    {
        var preservedPrefix = ExtractNormalizedPrefixLines(
            RemoveManagedBlock(preservedText));
        var preservedSections = ExtractNormalizedHashSections(RemoveManagedBlock(preservedText));
        var managedSections = ExtractNormalizedHashSections(managedText);

        var order = new List<string>();
        var manualByHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var managedByHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in preservedSections)
        {
            if (!manualByHash.ContainsKey(section.HashKey))
            {
                order.Add(section.HashKey);
                manualByHash[section.HashKey] = [];
            }

            manualByHash[section.HashKey].AddRange(section.BodyLines);
        }

        foreach (var section in managedSections)
        {
            if (!managedByHash.ContainsKey(section.HashKey))
            {
                managedByHash[section.HashKey] = [];
            }

            managedByHash[section.HashKey].AddRange(section.BodyLines);

            if (!order.Contains(section.HashKey, StringComparer.OrdinalIgnoreCase))
            {
                order.Add(section.HashKey);
            }
        }

        var output = new List<string> { RequiredVersionHeader };
        if (preservedPrefix.Count > 0)
        {
            output.Add(string.Empty);
            output.AddRange(preservedPrefix);
        }

        foreach (var hashKey in order)
        {
            var manualLines = manualByHash.TryGetValue(hashKey, out var manual)
                ? TrimEmptyEdges(manual)
                : new List<string>();
            var managedLines = managedByHash.TryGetValue(hashKey, out var managed)
                ? RemoveEquivalentPatchBlocksOrThrow(managed, manualLines)
                : new List<string>();

            manualLines = TrimEmptyEdges(manualLines);
            managedLines = TrimEmptyEdges(managedLines);

            if (manualLines.Count == 0 && managedLines.Count == 0)
            {
                continue;
            }

            output.Add(string.Empty);
            output.Add(hashKey + ":");
            output.AddRange(manualLines);

            if (managedLines.Count > 0)
            {
                output.Add("  " + ManagedBlockBegin);
                output.AddRange(managedLines);
                output.Add("  " + ManagedBlockEnd);
            }
        }

        return string.Join("\r\n", output).TrimEnd() + "\r\n";
    }

    private static string RemoveManagedBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = SplitLines(text);
        var output = new List<string>();
        var inside = false;

        foreach (var line in lines)
        {
            if (string.Equals(line.Trim(), ManagedBlockBegin, StringComparison.Ordinal))
            {
                inside = true;
                continue;
            }

            if (string.Equals(line.Trim(), ManagedBlockEnd, StringComparison.Ordinal))
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                output.Add(line);
            }
        }

        return string.Join("\r\n", output).TrimEnd() + "\r\n";
    }

    private static bool ContainsManagedBlock(string text)
    {
        return text.Contains(ManagedBlockBegin, StringComparison.Ordinal) &&
               text.Contains(ManagedBlockEnd, StringComparison.Ordinal);
    }

    private static bool LooksR3ConfigGenerated(string text)
    {
        return text.Contains(BundledSourceMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static string BodyWithoutVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var output = new List<string>();
        foreach (var line in SplitLines(text))
        {
            var key = TryReadTopLevelKey(line);
            if (string.Equals(key, "Version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            output.Add(line);
        }

        return string.Join("\r\n", TrimEmptyEdges(output));
    }

    private List<PatchHashTextSection> ExtractNormalizedHashSections(string text)
    {
        if (string.IsNullOrWhiteSpace(BodyWithoutVersion(text)))
        {
            return [];
        }

        var normalized = Normalize(text);
        if (!normalized.Succeeded)
        {
            return [];
        }

        var lines = SplitLines(normalized.NormalizedText);
        var sections = new List<PatchHashTextSection>();

        for (var index = 0; index < lines.Count;)
        {
            var key = TryReadTopLevelKey(lines[index]);
            if (key is null || !IsPatchRuntimeKey(key))
            {
                index++;
                continue;
            }

            var hashKey = key;
            var body = new List<string>();
            index++;

            while (index < lines.Count)
            {
                var nextKey = TryReadTopLevelKey(lines[index]);
                if (nextKey is not null && IsPatchRuntimeKey(nextKey))
                {
                    break;
                }

                if (!string.Equals(TryReadTopLevelKey(lines[index]), "Version", StringComparison.OrdinalIgnoreCase))
                {
                    body.Add(lines[index]);
                }

                index++;
            }

            sections.Add(new PatchHashTextSection(hashKey, TrimEmptyEdges(body)));
        }

        return sections;
    }

    private List<string> ExtractNormalizedPrefixLines(string text)
    {
        if (string.IsNullOrWhiteSpace(BodyWithoutVersion(text)))
        {
            return [];
        }

        var normalized = Normalize(text);
        if (!normalized.Succeeded)
        {
            return [];
        }

        var output = new List<string>();
        foreach (var line in SplitLines(normalized.NormalizedText))
        {
            var key = TryReadTopLevelKey(line);
            if (string.Equals(
                    key,
                    "Version",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key is not null && IsPatchRuntimeKey(key))
            {
                break;
            }

            output.Add(line);
        }

        return TrimEmptyEdges(output);
    }

    private static List<string> RemoveEquivalentPatchBlocksOrThrow(
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> existingLines)
    {
        var existingBlocks = ReadPatchBlocks(existingLines);
        var output = new List<string>();

        for (var index = 0; index < candidateLines.Count;)
        {
            var line = candidateLines[index];
            if (!TryReadPatchEntryName(line, out var patchName))
            {
                output.Add(line);
                index++;
                continue;
            }

            var patchBlock = new List<string> { line };
            index++;
            while (index < candidateLines.Count &&
                   !TryReadPatchEntryName(candidateLines[index], out _))
            {
                patchBlock.Add(candidateLines[index]);
                index++;
            }

            if (!existingBlocks.TryGetValue(
                    patchName,
                    out var existingBlock))
            {
                output.AddRange(patchBlock);
                continue;
            }

            if (!string.Equals(
                    NormalizePatchBlock(existingBlock),
                    NormalizePatchBlock(patchBlock),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    UiText.T("Patch.ImportCanaryDuplicatePatchName"));
            }
        }

        return output;
    }

    private static Dictionary<string, IReadOnlyList<string>> ReadPatchBlocks(
        IReadOnlyList<string> lines)
    {
        var output = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Count;)
        {
            if (!TryReadPatchEntryName(lines[index], out var patchName))
            {
                index++;
                continue;
            }

            var patchBlock = new List<string> { lines[index] };
            index++;
            while (index < lines.Count &&
                   !TryReadPatchEntryName(lines[index], out _))
            {
                patchBlock.Add(lines[index]);
                index++;
            }

            output.TryAdd(patchName, patchBlock);
        }

        return output;
    }

    private static string NormalizePatchBlock(
        IReadOnlyList<string> lines)
    {
        return string.Join(
            "\n",
            TrimEmptyEdges(lines)
                .Select(line => line.TrimEnd()));
    }

    private static List<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private sealed record PatchHashTextSection(string HashKey, IReadOnlyList<string> BodyLines);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary import files.
        }
    }

    private static string SafeErrorMessage(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? UiText.T("Patch.ImportCanaryInvalidYaml")
            : exception.Message;
    }

    public static bool IsPathInsideDirectory(string path, string directory)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   normalizedDirectory.TrimEnd(Path.DirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }
}
