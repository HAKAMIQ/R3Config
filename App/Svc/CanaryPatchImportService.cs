using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record CanNorm(
    bool IsOk,
    string NormText,
    bool PpuMerge,
    string Message,
    string? Error);

public sealed record CanWrite(
    bool IsOk,
    string TgtPath,
    string? BkpPath,
    string Message,
    string? Error);

public sealed record CanDel(
    bool IsOk,
    string TgtPath,
    string? BkpPath,
    string Message,
    string? Error);

public sealed record CanStage(
    bool IsOk,
    string TgtPath,
    PatchPreparedFile? PreparedFile,
    string Message,
    string? Error);

public sealed class CanaryPatchImportService
{
    public const string ImpName = "imported_patch.yml";
    public const string VerHead = "Version: 1.2";

    private const long MaxBytes =
        128L * 1024L * 1024L;

    private const string BlkBegin =
        "# R3Config managed Canary patches v1 - begin";

    private const string BlkEnd =
        "# R3Config managed Canary patches v1 - end";

    private const string AncBegin =
        "# R3Config managed Canary anchors v1 - begin";

    private const string AncEnd =
        "# R3Config managed Canary anchors v1 - end";

    private static readonly Regex TopKeyRx = new(
        @"^(?<key>[^\s:#][^:#]*):(?:\s.*)?$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

    private static readonly Regex MarkRx = new(
        @"^  # R3Config managed Canary patches(?<version> v[0-9]+)? - (?<edge>begin|end)$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

    private readonly ReadSvc _reader;

    public CanaryPatchImportService(
        ReadSvc? catalogReader = null)
    {
        _reader = catalogReader ??
                  new ReadSvc();
    }

    public CanNorm Norm(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return Failure(
                UiText.GetText(
                    "Patch.ImportCanaryInvalidYaml"));
        }

        var cleanInput =
            sourceText![0] == '\uFEFF'
                ? sourceText[1..]
                : sourceText;

        var normalizedInput = cleanInput
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);

        var lines =
            normalizedInput.Split('\n');

        var prefixLines =
            new List<string>();

        var ppuBlocks =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        var ppuPatchNames =
            new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);

        var ppuOrder =
            new List<string>();

        var duplicatePpuMerged = false;

        for (var index = 0;
             index < lines.Length;)
        {
            var line = lines[index];
            var topKey = TryTop(line);

            if (string.Equals(
                    topKey,
                    "Version",
                    StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (topKey is not null &&
                IsRunKey(topKey))
            {
                var blockLines =
                    new List<string>();

                index++;

                while (index < lines.Length &&
                       !IsTopKey(lines[index]))
                {
                    blockLines.Add(
                        lines[index]);

                    index++;
                }

                if (!ppuBlocks.TryGetValue(
                        topKey,
                        out var existingBody))
                {
                    existingBody = [];

                    ppuBlocks[topKey] =
                        existingBody;

                    ppuPatchNames[topKey] =
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                    ppuOrder.Add(topKey);
                }
                else
                {
                    duplicatePpuMerged = true;
                }

                var filteredBlockLines =
                    FiltDup(
                        blockLines,
                        ppuPatchNames[topKey],
                        out var duplicatePatchSkipped);

                if (duplicatePatchSkipped)
                {
                    duplicatePpuMerged = true;
                }

                existingBody.AddRange(
                    filteredBlockLines);

                continue;
            }

            prefixLines.Add(line);
            index++;
        }

        if (ppuBlocks.Count == 0)
        {
            return Failure(
                UiText.GetText(
                    "Patch.ImportCanaryInvalidYaml"));
        }

        var output =
            new List<string>
            {
                VerHead
            };

        var cleanedPrefix =
            Trim(prefixLines);

        if (cleanedPrefix.Count > 0)
        {
            output.Add(string.Empty);
            output.AddRange(cleanedPrefix);
        }

        foreach (var ppu in ppuOrder)
        {
            output.Add(string.Empty);
            output.Add(ppu + ":");
            output.AddRange(
                Trim(ppuBlocks[ppu]));
        }

        var normalized =
            string.Join(
                "\r\n",
                output)
            .TrimEnd() +
            "\r\n";

        if (HasDup(normalized))
        {
            return Failure(
                UiText.GetText(
                    "Patch.ImportCanaryDuplicateKeyUnmerged"));
        }

        return new CanNorm(
            true,
            normalized,
            duplicatePpuMerged,
            duplicatePpuMerged
                ? UiText.GetText(
                    "Patch.ImportCanaryDuplicatePpuMerged")
                : UiText.GetText(
                    "Patch.ImportCanaryFormatValid"),
            null);
    }

    public CanStage PrepareManagedImportedPatchFile(
        string patchesPath,
        string sourceText)
    {
        var targetPath = string.Empty;

        try
        {
            var validation = VldPath(
                patchesPath,
                out _,
                out targetPath,
                out var validationError);

            if (!validation)
            {
                return new CanStage(
                    false,
                    targetPath,
                    null,
                    validationError,
                    validationError);
            }

            var normalize =
                Norm(sourceText);

            if (!normalize.IsOk)
            {
                var error =
                    normalize.Error ??
                    normalize.Message;

                return new CanStage(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            var incomingCatalog =
                _reader.ReadText(
                    normalize.NormText,
                    PatSrc.Imported,
                    targetPath);

            if (!incomingCatalog.IsOk)
            {
                var error =
                    incomingCatalog.Error ??
                    UiText.GetText(
                        "Patch.ImportCanaryInvalidYaml");

                return new CanStage(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            var targetExisted =
                File.Exists(targetPath);

            var existing =
                targetExisted
                    ? ReadTarget(targetPath)
                    : null;

            var existingText =
                existing?.Text ??
                string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    existingText))
            {
                var existingCatalog =
                    _reader.ReadText(
                        existingText,
                        PatSrc.Imported,
                        targetPath);

                if (!existingCatalog.IsOk)
                {
                    var error =
                        existingCatalog.Error ??
                        UiText.GetText(
                            "Patch.ImportCanaryInvalidYaml");

                    return new CanStage(
                        false,
                        targetPath,
                        null,
                        error,
                        error);
                }

                var existingNormalized =
                    Norm(existingText);

                if (existing is { HasBom: false } &&
                    HasCanonicalHeader(existing.Text) &&
                    existingNormalized.IsOk &&
                    string.Equals(
                        existingNormalized.NormText,
                        normalize.NormText,
                        StringComparison.Ordinal))
                {
                    return new CanStage(
                        true,
                        targetPath,
                        null,
                        UiText.GetText(
                            "Patch.PrepareCanaryAlreadyReady"),
                        null);
                }
            }

            var preservedText =
                string.IsNullOrWhiteSpace(existingText)
                    ? string.Empty
                    : DelBlock(existingText);

            var finalText =
                Compose(
                    preservedText,
                    normalize.NormText);

            if (targetExisted &&
                existing is { HasBom: false } &&
                string.Equals(
                    existingText,
                    finalText,
                    StringComparison.Ordinal))
            {
                return new CanStage(
                    true,
                    targetPath,
                    null,
                    UiText.GetText(
                        "Patch.PrepareCanaryAlreadyReady"),
                    null);
            }

            var finalCatalog =
                _reader.ReadText(
                    finalText,
                    PatSrc.Imported,
                    targetPath);

            if (!finalText.StartsWith(
                    VerHead,
                    StringComparison.Ordinal) ||
                !finalCatalog.IsOk)
            {
                var error =
                    finalCatalog.Error ??
                    UiText.GetText(
                        "Patch.ImportCanaryInvalidYaml");

                return new CanStage(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            return new CanStage(
                true,
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    finalCatalog.Catalog.Entries.Count,
                    ExpectedExists: targetExisted,
                    ExpectedSha256: existing?.Sha256),
                UiText.GetText(
                    "Patch.PrepareCanarySelectedSuccess"),
                null);
        }
        catch (Exception ex)
        {
            var error =
                SafeMsg(ex);

            return new CanStage(
                false,
                targetPath,
                null,
                error,
                error);
        }
    }

    public CanStage PrepDel(
        string patchesPath)
    {
        var targetPath = string.Empty;

        try
        {
            var validation = VldPath(
                patchesPath,
                out _,
                out targetPath,
                out var validationError);

            if (!validation)
            {
                return new CanStage(
                    false,
                    targetPath,
                    null,
                    validationError,
                    validationError);
            }

            if (!File.Exists(targetPath))
            {
                return new CanStage(
                    true,
                    targetPath,
                    null,
                    UiText.GetText(
                        "Patch.RemoveCanaryNothing"),
                    null);
            }

            var existing =
                ReadTarget(targetPath);

            var existingText =
                existing.Text;

            var existingCatalog =
                _reader.ReadText(
                    existingText,
                    PatSrc.Imported,
                    targetPath);

            if (!existingCatalog.IsOk)
            {
                var error =
                    existingCatalog.Error ??
                    UiText.GetText(
                        "Patch.ImportCanaryInvalidYaml");

                return new CanStage(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            var scan =
                ScanBlk(existingText);

            if (!scan.HasBlock)
            {
                return new CanStage(
                    true,
                    targetPath,
                    null,
                    UiText.GetText(
                        "Patch.RemoveCanaryNothing"),
                    null);
            }

            var finalText =
                Compose(
                    scan.Text,
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    Body(finalText)))
            {
                return new CanStage(
                    true,
                    targetPath,
                    new PatchPreparedFile(
                        targetPath,
                        string.Empty,
                        PrepKind.PatCat,
                        PatSrc.Imported,
                        DelTgt: true,
                        ExpectedExists: true,
                        ExpectedSha256: existing.Sha256),
                    UiText.GetText(
                        "Patch.RemoveCanarySuccess"),
                    null);
            }

            var finalCatalog =
                _reader.ReadText(
                    finalText,
                    PatSrc.Imported,
                    targetPath);

            if (!finalCatalog.IsOk)
            {
                var error =
                    finalCatalog.Error ??
                    UiText.GetText(
                        "Patch.ImportCanaryInvalidYaml");

                return new CanStage(
                    false,
                    targetPath,
                    null,
                    error,
                    error);
            }

            return new CanStage(
                true,
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    finalCatalog.Catalog.Entries.Count,
                    ExpectedExists: true,
                    ExpectedSha256: existing.Sha256),
                UiText.GetText(
                    "Patch.RemoveCanarySuccess"),
                null);
        }
        catch (Exception ex)
        {
            var error =
                SafeMsg(ex);

            return new CanStage(
                false,
                targetPath,
                null,
                error,
                error);
        }
    }

    public bool HasPat(string patchesPath)
    {
        try
        {
            var validation = VldPath(
                patchesPath,
                out _,
                out var targetPath,
                out _);

            if (!validation ||
                !File.Exists(targetPath))
            {
                return false;
            }

            var text =
                R3Bound.ReadUtf8(
                    targetPath,
                    R3Limits.PatCatBy,
                    ImpName);

            return ScanBlk(text).HasBlock;
        }
        catch
        {
            return false;
        }
    }

    public string ReadText(string targetPath)
    {
        return ReadTarget(
            targetPath).Text;
    }

    public static bool InDir(
        string path,
        string directory)
    {
        var normalizedPath =
            Path.GetFullPath(path);

        var normalizedDirectory =
            Path.GetFullPath(directory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(
                   normalizedDirectory,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedPath.TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar),
                   normalizedDirectory.TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static CanNorm Failure(
        string error)
    {
        return new CanNorm(
            false,
            string.Empty,
            false,
            error,
            error);
    }

    private static bool IsTopKey(
        string line)
    {
        return TryTop(line) is not null;
    }

    private static string? TryTop(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            char.IsWhiteSpace(line[0]) ||
            line.TrimStart().StartsWith(
                "#",
                StringComparison.Ordinal))
        {
            return null;
        }

        var match =
            TopKeyRx.Match(line);

        return match.Success
            ? match.Groups["key"].Value.Trim()
            : null;
    }

    private static TargetRead ReadTarget(
        string targetPath)
    {
        using var stream =
            new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        if (stream.Length > MaxBytes)
        {
            throw new InvalidDataException(
                $"The imported patch file exceeds the {MaxBytes}-byte read limit.");
        }

        var bytes =
            new byte[checked((int)stream.Length)];

        stream.ReadExactly(bytes);

        var digest =
            Convert.ToHexString(
                SHA256.HashData(bytes));

        var hasBom =
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;

        var offset =
            hasBom
                ? 3
                : 0;

        var utf8 =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

        var text =
            utf8.GetString(
                bytes,
                offset,
                bytes.Length - offset);

        return new TargetRead(
            text,
            digest,
            hasBom);
    }

    private static bool HasCanonicalHeader(
        string text)
    {
        return text.Equals(
                   VerHead,
                   StringComparison.Ordinal) ||
               text.StartsWith(
                   VerHead + "\r\n",
                   StringComparison.Ordinal) ||
               text.StartsWith(
                   VerHead + "\n",
                   StringComparison.Ordinal);
    }

    private static bool IsRunKey(
        string value)
    {
        return RunKey.TryParse(
            value,
            out _);
    }

    private static List<string> FiltDup(
        IReadOnlyList<string> blockLines,
        HashSet<string> knownPatchNames,
        out bool duplicatePatchSkipped)
    {
        var output =
            new List<string>();

        duplicatePatchSkipped = false;

        for (var index = 0;
             index < blockLines.Count;)
        {
            var line =
                blockLines[index];

            if (!TryName(
                    line,
                    out var patchName))
            {
                output.Add(line);
                index++;
                continue;
            }

            var patchBlock =
                new List<string>
                {
                    line
                };

            index++;

            while (index < blockLines.Count &&
                   !TryName(
                       blockLines[index],
                       out _))
            {
                patchBlock.Add(
                    blockLines[index]);

                index++;
            }

            if (!knownPatchNames.Add(
                    patchName))
            {
                duplicatePatchSkipped = true;
                continue;
            }

            output.AddRange(
                patchBlock);
        }

        return output;
    }

    private static bool TryName(
        string line,
        out string patchName)
    {
        patchName = string.Empty;

        if (line.Length < 3 ||
            !line.StartsWith(
                "  ",
                StringComparison.Ordinal) ||
            char.IsWhiteSpace(line[2]))
        {
            return false;
        }

        var trimmed =
            line.Trim();

        var colonIndex =
            FindName(trimmed);

        if (colonIndex <= 0)
        {
            return false;
        }

        var candidate =
            trimmed[..colonIndex]
                .Trim()
                .Trim(
                    '"',
                    '\'');

        if (string.IsNullOrWhiteSpace(
                candidate))
        {
            return false;
        }

        patchName = candidate;
        return true;
    }

    private static int FindName(
        string value)
    {
        if (value.Length == 0)
        {
            return -1;
        }

        if (value[0] is '"' or '\'')
        {
            var quote =
                value[0];

            for (var index = 1;
                 index < value.Length;
                 index++)
            {
                if (value[index] == quote &&
                    index + 1 < value.Length &&
                    value[index + 1] == ':')
                {
                    return index + 1;
                }
            }

            return -1;
        }

        return value.IndexOf(
            ':',
            StringComparison.Ordinal);
    }

    private static List<string> Trim(
        IEnumerable<string> lines)
    {
        var list =
            lines.ToList();

        while (list.Count > 0 &&
               string.IsNullOrWhiteSpace(
                   list[0]))
        {
            list.RemoveAt(0);
        }

        while (list.Count > 0 &&
               string.IsNullOrWhiteSpace(
                   list[^1]))
        {
            list.RemoveAt(
                list.Count - 1);
        }

        return list;
    }

    private static bool HasDup(
        string text)
    {
        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var line in Lines(text))
        {
            var key =
                TryTop(line);

            if (key is not null &&
                IsRunKey(key) &&
                !seen.Add(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool VldPath(
        string patchesPath,
        out string patchesDir,
        out string targetPath,
        out string error)
    {
        patchesDir = string.Empty;
        targetPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(
                patchesPath))
        {
            error =
                UiText.GetText(
                    "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        patchesDir =
            Path.GetFullPath(
                patchesPath);

        var parentPath =
            Directory.GetParent(
                patchesDir)?.FullName;

        targetPath =
            Path.GetFullPath(
                Path.Combine(
                    patchesDir,
                    ImpName));

        if (string.IsNullOrWhiteSpace(
                parentPath) ||
            !Directory.Exists(parentPath))
        {
            error =
                UiText.GetText(
                    "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        if (!InDir(
                targetPath,
                patchesDir) ||
            !InDir(
                patchesDir,
                parentPath))
        {
            error =
                UiText.GetText(
                    "Patch.ImportCanaryUnsafePath");

            return false;
        }

        return true;
    }

    private string Compose(
        string preservedText,
        string managedText)
    {
        var cleanPreservedText =
            DelAnchors(
                DelBlock(
                    preservedText));

        var preservedPrefix =
            ExtLines(
                cleanPreservedText);

        var managedPrefix =
            ExtLines(
                managedText);

        var preservedSections =
            ExtHash(
                cleanPreservedText);

        var managedSections =
            ExtHash(
                managedText);

        var order =
            new List<string>();

        var manualByHash =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        var managedByHash =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var section in preservedSections)
        {
            if (!manualByHash.ContainsKey(
                    section.HashKey))
            {
                order.Add(
                    section.HashKey);

                manualByHash[section.HashKey] = [];
            }

            manualByHash[section.HashKey]
                .AddRange(
                    section.Lines);
        }

        foreach (var section in managedSections)
        {
            if (!managedByHash.ContainsKey(
                    section.HashKey))
            {
                managedByHash[section.HashKey] = [];
            }

            managedByHash[section.HashKey]
                .AddRange(
                    section.Lines);

            if (!order.Contains(
                    section.HashKey,
                    StringComparer.OrdinalIgnoreCase))
            {
                order.Add(
                    section.HashKey);
            }
        }

        var output =
            new List<string>
            {
                VerHead
            };

        var mergedPrefix =
            MergeAnchors(
                preservedPrefix,
                managedPrefix);

        if (mergedPrefix.Count > 0)
        {
            output.Add(string.Empty);
            output.AddRange(
                mergedPrefix);
        }

        foreach (var hashKey in order)
        {
            var manualLines =
                manualByHash.TryGetValue(
                    hashKey,
                    out var manual)
                    ? Trim(manual)
                    : [];

            var managedLines =
                managedByHash.TryGetValue(
                    hashKey,
                    out var managed)
                    ? DelEquiv(
                        managed,
                        manualLines)
                    : [];

            manualLines =
                Trim(manualLines);

            managedLines =
                Trim(managedLines);

            if (manualLines.Count == 0 &&
                managedLines.Count == 0)
            {
                continue;
            }

            output.Add(string.Empty);
            output.Add(hashKey + ":");
            output.AddRange(manualLines);

            if (managedLines.Count > 0)
            {
                output.Add(
                    "  " + BlkBegin);

                output.AddRange(
                    managedLines);

                output.Add(
                    "  " + BlkEnd);
            }
        }

        return string.Join(
                   "\r\n",
                   output)
               .TrimEnd() +
               "\r\n";
    }

    private static List<string> MergeAnchors(
        IReadOnlyList<string> preservedPrefix,
        IReadOnlyList<string> managedPrefix)
    {
        var output =
            preservedPrefix.ToList();

        var managed =
            AnchorSection(
                managedPrefix);

        if (managed is null)
        {
            RemoveEmptyAnchorsSection(
                output);

            return output;
        }

        var existing =
            AnchorSection(
                output);

        if (existing is null)
        {
            if (output.Count > 0 &&
                !string.IsNullOrWhiteSpace(
                    output[^1]))
            {
                output.Add(string.Empty);
            }

            output.Add("Anchors:");
            output.Add(
                "  " + AncBegin);

            output.AddRange(
                managed.Body.Skip(1));

            output.Add(
                "  " + AncEnd);

            return output;
        }

        var known =
            AnchorEntries(existing.Body)
                .ToDictionary(
                    entry => entry.Name,
                    entry => entry.Lines,
                    StringComparer.Ordinal);

        var additions =
            new List<string>();

        foreach (var incoming in
                 AnchorEntries(managed.Body))
        {
            if (!known.TryGetValue(
                    incoming.Name,
                    out var current))
            {
                known.Add(
                    incoming.Name,
                    incoming.Lines);

                additions.AddRange(
                    incoming.Lines);

                continue;
            }

            if (!string.Equals(
                    NormBlk(current),
                    NormBlk(incoming.Lines),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"YAML anchor '{incoming.Name}' already exists with different content.");
            }
        }

        if (additions.Count > 0)
        {
            additions.Insert(
                0,
                "  " + AncBegin);

            additions.Add(
                "  " + AncEnd);

            output.InsertRange(
                existing.End,
                additions);
        }

        RemoveEmptyAnchorsSection(
            output);

        return output;
    }

    private static string DelAnchors(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var lines =
            Lines(text);

        var output =
            new List<string>(
                lines.Count);

        var insideAnchors = false;
        var insideManaged = false;

        foreach (var line in lines)
        {
            var topKey =
                TryTop(line);

            if (topKey is not null)
            {
                insideAnchors =
                    string.Equals(
                        topKey,
                        "Anchors",
                        StringComparison.OrdinalIgnoreCase);
            }

            if (line.Equals(
                    "  " + AncBegin,
                    StringComparison.Ordinal))
            {
                if (!insideAnchors ||
                    insideManaged)
                {
                    throw new InvalidDataException(
                        "A managed Canary anchor marker is misplaced or duplicated.");
                }

                insideManaged = true;
                continue;
            }

            if (line.Equals(
                    "  " + AncEnd,
                    StringComparison.Ordinal))
            {
                if (!insideAnchors ||
                    !insideManaged)
                {
                    throw new InvalidDataException(
                        "A managed Canary anchor marker is incomplete or misplaced.");
                }

                insideManaged = false;
                continue;
            }

            if (!insideManaged)
            {
                output.Add(line);
            }
        }

        if (insideManaged)
        {
            throw new InvalidDataException(
                "A managed Canary anchor marker is missing its end marker.");
        }

        RemoveEmptyAnchorsSection(
            output);

        var trimmed =
            Trim(output);

        return trimmed.Count == 0
            ? string.Empty
            : string.Join(
                  "\r\n",
                  trimmed)
              .TrimEnd() +
              "\r\n";
    }

    private static void RemoveEmptyAnchorsSection(
        List<string> lines)
    {
        for (var index = 0;
             index < lines.Count;
             index++)
        {
            var key =
                TryTop(lines[index]);

            if (!string.Equals(
                    key,
                    "Anchors",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var end =
                index + 1;

            while (end < lines.Count &&
                   TryTop(lines[end]) is null)
            {
                end++;
            }

            var hasAnchorEntry = false;

            for (var bodyIndex = index + 1;
                 bodyIndex < end;
                 bodyIndex++)
            {
                if (TryName(
                        lines[bodyIndex],
                        out _))
                {
                    hasAnchorEntry = true;
                    break;
                }
            }

            if (hasAnchorEntry)
            {
                return;
            }

            lines.RemoveRange(
                index,
                end - index);

            while (index > 0 &&
                   index < lines.Count &&
                   string.IsNullOrWhiteSpace(
                       lines[index - 1]) &&
                   string.IsNullOrWhiteSpace(
                       lines[index]))
            {
                lines.RemoveAt(index);
            }

            return;
        }
    }

    private static AnchorSect? AnchorSection(
        IReadOnlyList<string> lines)
    {
        for (var index = 0;
             index < lines.Count;
             index++)
        {
            var key =
                TryTop(lines[index]);

            if (!string.Equals(
                    key,
                    "Anchors",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var end =
                index + 1;

            while (end < lines.Count &&
                   TryTop(lines[end]) is null)
            {
                end++;
            }

            return new AnchorSect(
                index,
                end,
                lines
                    .Skip(index)
                    .Take(end - index)
                    .ToArray());
        }

        return null;
    }

    private static IReadOnlyList<AnchorEnt> AnchorEntries(
        IReadOnlyList<string> anchorSection)
    {
        var output =
            new List<AnchorEnt>();

        for (var index = 1;
             index < anchorSection.Count;)
        {
            if (!TryName(
                    anchorSection[index],
                    out var name))
            {
                index++;
                continue;
            }

            var block =
                new List<string>
                {
                    anchorSection[index]
                };

            index++;

            while (index < anchorSection.Count &&
                   !TryName(
                       anchorSection[index],
                       out _))
            {
                block.Add(
                    anchorSection[index]);

                index++;
            }

            output.Add(
                new AnchorEnt(
                    name,
                    block));
        }

        return output;
    }

    private static string DelBlock(
        string text)
    {
        return ScanBlk(text).Text;
    }

    private static BlkScan ScanBlk(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new BlkScan(
                false,
                string.Empty);
        }

        var lines =
            Lines(text);

        var output =
            new List<string>();

        var inside = false;
        var found = false;
        var activeVersion = string.Empty;
        var activeHash = string.Empty;

        string? currentHash = null;

        foreach (var line in lines)
        {
            var topKey =
                TryTop(line);

            if (topKey is not null)
            {
                currentHash =
                    IsRunKey(topKey)
                        ? topKey
                        : null;
            }

            if (!TryMark(
                    line,
                    out var version,
                    out var isBegin))
            {
                if (!inside)
                {
                    output.Add(line);
                }

                continue;
            }

            if (currentHash is null)
            {
                throw new InvalidDataException(
                    "A managed Canary marker is outside a patch hash block.");
            }

            if (version is not "legacy" and not "v1")
            {
                throw new InvalidDataException(
                    $"Unsupported managed Canary marker version: {version}");
            }

            if (isBegin)
            {
                if (inside)
                {
                    throw new InvalidDataException(
                        "A managed Canary marker block is nested or duplicated.");
                }

                inside = true;
                found = true;
                activeVersion = version;
                activeHash = currentHash;

                continue;
            }

            if (!inside ||
                !string.Equals(
                    activeVersion,
                    version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    activeHash,
                    currentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "A managed Canary marker block is incomplete or mismatched.");
            }

            inside = false;
            activeVersion = string.Empty;
            activeHash = string.Empty;
        }

        if (inside)
        {
            throw new InvalidDataException(
                "A managed Canary marker block is missing its end marker.");
        }

        return new BlkScan(
            found,
            string.Join(
                    "\r\n",
                    output)
                .TrimEnd() +
            "\r\n");
    }

    private static bool TryMark(
        string line,
        out string version,
        out bool isBegin)
    {
        version = string.Empty;
        isBegin = false;

        var match =
            MarkRx.Match(line);

        if (!match.Success)
        {
            return false;
        }

        var markerVersion =
            match.Groups["version"]
                .Value
                .Trim();

        version =
            string.IsNullOrEmpty(
                markerVersion)
                ? "legacy"
                : markerVersion;

        isBegin =
            string.Equals(
                match.Groups["edge"].Value,
                "begin",
                StringComparison.Ordinal);

        return true;
    }

    private static string Body(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var output =
            new List<string>();

        foreach (var line in Lines(text))
        {
            var key =
                TryTop(line);

            if (string.Equals(
                    key,
                    "Version",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            output.Add(line);
        }

        return string.Join(
            "\r\n",
            Trim(output));
    }

    private List<HashSect> ExtHash(
        string text)
    {
        if (string.IsNullOrWhiteSpace(
                Body(text)))
        {
            return [];
        }

        var normalized =
            Norm(text);

        if (!normalized.IsOk)
        {
            return [];
        }

        var lines =
            Lines(
                normalized.NormText);

        var sections =
            new List<HashSect>();

        for (var index = 0;
             index < lines.Count;)
        {
            var key =
                TryTop(lines[index]);

            if (key is null ||
                !IsRunKey(key))
            {
                index++;
                continue;
            }

            var hashKey = key;
            var body =
                new List<string>();

            index++;

            while (index < lines.Count)
            {
                var nextKey =
                    TryTop(lines[index]);

                if (nextKey is not null &&
                    IsRunKey(nextKey))
                {
                    break;
                }

                if (!string.Equals(
                        TryTop(lines[index]),
                        "Version",
                        StringComparison.OrdinalIgnoreCase))
                {
                    body.Add(
                        lines[index]);
                }

                index++;
            }

            sections.Add(
                new HashSect(
                    hashKey,
                    Trim(body)));
        }

        return sections;
    }

    private List<string> ExtLines(
        string text)
    {
        if (string.IsNullOrWhiteSpace(
                Body(text)))
        {
            return [];
        }

        var normalized =
            Norm(text);

        if (!normalized.IsOk)
        {
            return [];
        }

        var output =
            new List<string>();

        foreach (var line in
                 Lines(normalized.NormText))
        {
            var key =
                TryTop(line);

            if (string.Equals(
                    key,
                    "Version",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key is not null &&
                IsRunKey(key))
            {
                break;
            }

            output.Add(line);
        }

        return Trim(output);
    }

    private static List<string> DelEquiv(
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> existingLines)
    {
        var existingBlocks =
            ReadBlk(existingLines);

        var output =
            new List<string>();

        for (var index = 0;
             index < candidateLines.Count;)
        {
            var line =
                candidateLines[index];

            if (!TryName(
                    line,
                    out var patchName))
            {
                output.Add(line);
                index++;
                continue;
            }

            var patchBlock =
                new List<string>
                {
                    line
                };

            index++;

            while (index < candidateLines.Count &&
                   !TryName(
                       candidateLines[index],
                       out _))
            {
                patchBlock.Add(
                    candidateLines[index]);

                index++;
            }

            if (!existingBlocks.TryGetValue(
                    patchName,
                    out var existingBlock))
            {
                output.AddRange(
                    patchBlock);

                continue;
            }

            if (!string.Equals(
                    NormBlk(existingBlock),
                    NormBlk(patchBlock),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    UiText.GetText(
                        "Patch.ImportCanaryDuplicatePatchName"));
            }
        }

        return output;
    }

    private static Dictionary<string, IReadOnlyList<string>> ReadBlk(
        IReadOnlyList<string> lines)
    {
        var output =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase);

        for (var index = 0;
             index < lines.Count;)
        {
            if (!TryName(
                    lines[index],
                    out var patchName))
            {
                index++;
                continue;
            }

            var patchBlock =
                new List<string>
                {
                    lines[index]
                };

            index++;

            while (index < lines.Count &&
                   !TryName(
                       lines[index],
                       out _))
            {
                patchBlock.Add(
                    lines[index]);

                index++;
            }

            output.TryAdd(
                patchName,
                patchBlock);
        }

        return output;
    }

    private static string NormBlk(
        IReadOnlyList<string> lines)
    {
        return string.Join(
            "\n",
            Trim(lines)
                .Select(
                    line =>
                        line.TrimEnd()));
    }

    private static List<string> Lines(
        string text)
    {
        return text
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private static string SafeMsg(
        Exception exception)
    {
        return string.IsNullOrWhiteSpace(
                exception.Message)
            ? UiText.GetText(
                "Patch.ImportCanaryInvalidYaml")
            : exception.Message;
    }

    private sealed record HashSect(
        string HashKey,
        IReadOnlyList<string> Lines);

    private sealed record TargetRead(
        string Text,
        string Sha256,
        bool HasBom);

    private sealed record AnchorSect(
        int Start,
        int End,
        IReadOnlyList<string> Lines)
    {
        public IReadOnlyList<string> Body =>
            Lines;
    }

    private sealed record AnchorEnt(
        string Name,
        IReadOnlyList<string> Lines);

    private sealed record BlkScan(
        bool HasBlock,
        string Text);
}