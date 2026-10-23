using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record LibStRes(
    bool IsOk,
    string TgtPath,
    PatchPreparedFile? PreparedFile,
    int PatNum,
    string Message,
    string? Error,
    IReadOnlyList<PatchPreparedFile> Extra);

public sealed class BundledCheatPatchService
{
    public const string BndlName = "art.yml";

    public const string ManName = "r3cht.yml";
    public const string OldName = "r3config_cheats_patch.yml";

    public const string ManMark =
        "# R3Config managed RPCS3 cheat patches - do not edit while R3Config is running";

    private readonly PatchSelectionCompiler _comp = new();
    private readonly ReadSvc _reader = new();

    public static string GetBndl(string baseDirectory)
    {
        return Path.Combine(
            baseDirectory,
            "Data",
            BndlName);
    }



    public static string GetPath(string patchesPath)
    {
        return Path.Combine(
            patchesPath,
            ManName);
    }

    public static string OldPath(string patchesPath)
    {
        return Path.Combine(
            patchesPath,
            OldName);
    }

    public static string GetPath(
        string patchesPath,
        string serial)
    {
        var normalizedSerial = NormSer(serial)
            ?? throw new ArgumentException(
                "A managed RPCS3 cheat file requires a valid 9-character title ID.",
                nameof(serial));

        return Path.Combine(
            patchesPath,
            normalizedSerial + "_patch.yml");
    }

    public static IReadOnlyList<string> GetPaths(
        string patchesPath,
        IEnumerable<PatMatch> matches)
    {
        return matches
            .Select(GetSer)
            .Where(serial => serial is not null)
            .Select(serial => GetPath(patchesPath, serial!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsMan(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var text = R3Bound.ReadUtf8(
                path,
                R3Limits.PatCatBy,
                "managed cheat patch");
            var lineEnd = text.IndexOfAny(['\r', '\n']);
            var firstLine = lineEnd < 0
                ? text
                : text[..lineEnd];

            return string.Equals(
                firstLine,
                ManMark,
                StringComparison.Ordinal);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException)
        {
            return false;
        }
    }

    public LibStRes Prep(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        string targetPath = string.Empty;

        try
        {
            var bundledSelections = selectedMatches
                .Where(match =>
                    match.Patch.Source == PatSrc.Artemis &&
                    PatAct.Can(match))
                .ToArray();

            if (bundledSelections.Length == 0)
            {
                return Success(
                    string.Empty,
                    null,
                    0,
                    UiText.GetText("Patch.CheatLibrary.NoPrepare"));
            }

            if (!TryTgt(
                    patchesPath,
                    bundledSelections,
                    out targetPath,
                    out var pathError))
            {
                return Failure(
                    targetPath,
                    pathError);
            }

            var sourcePath = GetBndl(baseDirectory);

            if (!File.Exists(sourcePath))
            {
                return Failure(
                    targetPath,
                    UiText.GetText("Patch.CheatLibrary.Missing"));
            }

            var targetState =
                PatchFileTransactionService.CaptureExpectedState(
                    targetPath,
                    R3Limits.PatCatBy,
                    "managed title-specific patch");

            List<PatMatch> selections =
            [
                .. bundledSelections
            ];

            var sourceTexts = new List<string>();
            var extra = new List<PatchPreparedFile>();
            var existingText = string.Empty;

            if (File.Exists(targetPath))
            {
                existingText = R3Bound.ReadUtf8(
                    targetPath,
                    R3Limits.PatCatBy,
                    "managed title-specific patch");

                if (!existingText.StartsWith(
                        ManMark,
                        StringComparison.Ordinal))
                {
                    return Failure(
                        targetPath,
                        UiText.GetText(
                            "Patch.CheatLibrary.UnmanagedConflict"));
                }

                var existingRead = _reader.ReadText(
                    existingText,
                    PatSrc.ManagedArtemis,
                    targetPath);

                if (!existingRead.IsOk)
                {
                    return Failure(
                        targetPath,
                        existingRead.Error ??
                        UiText.GetText(
                            "Patch.ImportCanaryInvalidYaml"));
                }

                selections.AddRange(
                    existingRead.Catalog.Entries.Select(
                        Exact));
                sourceTexts.Add(existingText);
            }

            var targetSerial = GetSer(bundledSelections[0])!;
            var libraryPath = GetPath(patchesPath);
            var legacyPath = OldPath(patchesPath);
            var libraryState =
                PatchFileTransactionService.CaptureExpectedState(
                    libraryPath,
                    R3Limits.PatCatBy,
                    "managed cheat library");
            string? readPath = null;

            if (File.Exists(libraryPath) &&
                IsMan(libraryPath))
            {
                readPath = libraryPath;
            }
            else if (File.Exists(legacyPath) &&
                     IsMan(legacyPath))
            {
                if (File.Exists(libraryPath))
                {
                    return Failure(
                        targetPath,
                        UiText.GetText(
                            "Patch.CheatLibrary.UnmanagedConflict"));
                }

                readPath = legacyPath;
            }

            if (readPath is not null)
            {
                var libraryText = R3Bound.ReadUtf8(
                    readPath,
                    R3Limits.PatCatBy,
                    Path.GetFileName(readPath));

                var libraryRead = _reader.ReadText(
                    libraryText,
                    PatSrc.ManagedArtemis,
                    readPath);

                if (!libraryRead.IsOk)
                {
                    return Failure(
                        targetPath,
                        libraryRead.Error ??
                        UiText.GetText(
                            "Patch.ImportCanaryInvalidYaml"));
                }

                selections.AddRange(
                    libraryRead.Catalog.Entries
                        .Where(entry => entry.GameTgt.Any(target =>
                            string.Equals(
                                target.Serial,
                                targetSerial,
                                StringComparison.OrdinalIgnoreCase)))
                        .Select(Exact));
                sourceTexts.Add(libraryText);

                if (readPath.Equals(
                        legacyPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    extra.Add(
                        new PatchPreparedFile(
                            libraryPath,
                            libraryText,
                            PrepKind.PatCat,
                            PatSrc.ManagedArtemis,
                            libraryRead.Catalog.Entries.Count,
                            ExpectedExists: libraryState.Exists,
                            ExpectedSha256: libraryState.Sha256));
                }
            }

            selections =
            [
                .. selections
                    .GroupBy(
                        GetId,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
            ];

            var sourceText = R3Bound.ReadUtf8(
                sourcePath,
                R3Limits.PatCatBy,
                BndlName);
            sourceTexts.Add(sourceText);

            var compiled = _comp.Compile(
                sourceTexts,
                selections,
                bundledSelections);

            if (!compiled.IsOk)
            {
                return Failure(
                    targetPath,
                    compiled.Error ??
                    UiText.GetText(
                        "Patch.ImportCanaryInvalidYaml"));
            }

            var finalText =
                ManMark +
                "\r\n" +
                compiled.PatText;

            if (File.Exists(targetPath) &&
                string.Equals(
                    existingText,
                    finalText,
                    StringComparison.Ordinal))
            {
                return Success(
                    targetPath,
                    null,
                    compiled.SelNum,
                    UiText.Fmt(
                        "Patch.CheatLibrary.Ready",
                        compiled.SelNum),
                    extra);
            }

            var validation = _reader.ReadText(
                finalText,
                PatSrc.ManagedArtemis,
                targetPath);

            if (!validation.IsOk ||
                validation.Catalog.Entries.Count != compiled.SelNum)
            {
                return Failure(
                    targetPath,
                    validation.Error ??
                    "The managed cheat patch failed validation.");
            }

            return Success(
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.ManagedArtemis,
                    compiled.SelNum,
                    ExpectedExists: targetState.Exists,
                    ExpectedSha256: targetState.Sha256),
                compiled.SelNum,
                UiText.Fmt(
                    "Patch.CheatLibrary.Prepared",
                    bundledSelections.Length,
                    compiled.SelNum),
                extra);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException)
        {
            return Failure(
                targetPath,
                ex.Message);
        }
    }

    public LibStRes PrepGenerated(
        string patchesPath,
        string generatedText)
    {
        string targetPath = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(generatedText))
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    "The generated NCL RPCS3 patch text is empty.",
                    []);
            }

            var generatedRead = _reader.ReadText(
                generatedText,
                PatSrc.ManagedArtemis,
                sourcePath: null);

            if (!generatedRead.IsOk ||
                generatedRead.Catalog.Entries.Count != 1)
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    generatedRead.Error ??
                    "Generated NCL patch preparation requires exactly one validated RPCS3 definition.",
                    []);
            }

            var generatedMatch =
                ExactManaged(
                    generatedRead.Catalog.Entries[0]);

            if (string.IsNullOrWhiteSpace(
                    generatedMatch.Patch.DefPrint) ||
                !PatAct.Can(generatedMatch))
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    "The generated NCL RPCS3 definition did not retain an exact compatible identity.",
                    []);
            }

            if (!TryTgt(
                    patchesPath,
                    [generatedMatch],
                    out targetPath,
                    out var targetError))
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    targetError,
                    []);
            }

            var targetState =
                PatchFileTransactionService.CaptureExpectedState(
                    targetPath,
                    R3Limits.PatCatBy,
                    "managed title-specific NCL patch");

            var sourceTexts = new List<string>();
            var selections = new List<PatMatch>();
            var existingText = string.Empty;

            if (File.Exists(targetPath))
            {
                existingText = R3Bound.ReadUtf8(
                    targetPath,
                    R3Limits.PatCatBy,
                    "managed title-specific NCL patch");

                if (!existingText.StartsWith(
                        ManMark,
                        StringComparison.Ordinal))
                {
                    return new LibStRes(
                        false,
                        targetPath,
                        null,
                        0,
                        string.Empty,
                        "The title-specific RPCS3 patch file exists but is not managed by R3Config.",
                        []);
                }

                var existingRead = _reader.ReadText(
                    existingText,
                    PatSrc.ManagedArtemis,
                    targetPath);

                if (!existingRead.IsOk)
                {
                    return new LibStRes(
                        false,
                        targetPath,
                        null,
                        0,
                        string.Empty,
                        existingRead.Error ??
                        "The existing managed title-specific patch failed validation.",
                        []);
                }

                var existingMatches =
                    existingRead.Catalog.Entries
                        .Select(ExactManaged)
                        .ToArray();

                var generatedId = GetId(generatedMatch);
                var resident = existingMatches.FirstOrDefault(
                    match => GetId(match).Equals(
                        generatedId,
                        StringComparison.OrdinalIgnoreCase));

                if (resident is not null &&
                    !string.Equals(
                        resident.Patch.DefPrint,
                        generatedMatch.Patch.DefPrint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new LibStRes(
                        false,
                        targetPath,
                        null,
                        existingRead.Catalog.Entries.Count,
                        string.Empty,
                        $"Managed NCL definition '{generatedMatch.Patch.Title}' already exists with different content.",
                        []);
                }

                sourceTexts.Add(existingText);
                selections.AddRange(existingMatches);
            }

            sourceTexts.Add(generatedText);
            selections.Add(generatedMatch);

            selections =
            [
                .. selections
                    .GroupBy(
                        GetId,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
            ];

            var compiled = _comp.Compile(
                sourceTexts,
                selections,
                [generatedMatch]);

            if (!compiled.IsOk)
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    compiled.Error ??
                    "The generated NCL managed patch failed selection compilation.",
                    []);
            }

            var mergedText = string.IsNullOrEmpty(existingText)
                ? compiled.PatText
                : PatchSelectionCompiler.MergeGeneratedNcl(existingText, generatedText);
            var finalText = mergedText.StartsWith(ManMark, StringComparison.Ordinal)
                ? mergedText
                : ManMark + "\r\n" + mergedText;

            var validation = _reader.ReadText(
                finalText,
                PatSrc.ManagedArtemis,
                targetPath);

            if (!validation.IsOk ||
                validation.Catalog.Entries.Count != compiled.SelNum)
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    validation.Error ??
                    "The generated NCL managed patch failed read-back validation.",
                    []);
            }

            var generatedEntry =
                validation.Catalog.Entries.FirstOrDefault(entry =>
                    entry.RunKey.Equals(
                        generatedMatch.EffectiveRunKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    entry.Title.Equals(
                        generatedMatch.Patch.Title,
                        StringComparison.OrdinalIgnoreCase));

            if (generatedEntry is null ||
                !string.Equals(
                    generatedEntry.DefPrint,
                    generatedMatch.Patch.DefPrint,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new LibStRes(
                    false,
                    targetPath,
                    null,
                    0,
                    string.Empty,
                    "The generated NCL definition changed during managed patch compilation.",
                    []);
            }

            if (File.Exists(targetPath) &&
                string.Equals(
                    existingText,
                    finalText,
                    StringComparison.Ordinal))
            {
                return new LibStRes(
                    true,
                    targetPath,
                    new PatchPreparedFile(
                        targetPath,
                        existingText,
                        PrepKind.PatCat,
                        PatSrc.ManagedArtemis,
                        compiled.SelNum,
                        ExpectedExists: targetState.Exists,
                        ExpectedSha256: targetState.Sha256),
                    compiled.SelNum,
                    "The verified NCL definition is already present in the managed title patch.",
                    null,
                    []);
            }

            return new LibStRes(
                true,
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.ManagedArtemis,
                    compiled.SelNum,
                    ExpectedExists: targetState.Exists,
                    ExpectedSha256: targetState.Sha256),
                compiled.SelNum,
                "The verified NCL definition is prepared for the managed title patch transaction.",
                null,
                []);
        }
        catch (Exception ex) when (
            ex is IOException or
            YamlDotNet.Core.YamlException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException or
            InvalidOperationException)
        {
            return new LibStRes(
                false,
                targetPath,
                null,
                0,
                string.Empty,
                ex.Message,
                []);
        }
    }
    public LibStRes PrepRemove(
        string patchesPath,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        var selections = selectedMatches
            .Where(match => match.Patch.Source is PatSrc.Artemis or PatSrc.ManagedArtemis)
            .ToArray();
        if (selections.Length == 0)
        {
            return Success(string.Empty, null, 0, "No managed Artemis definitions were selected.");
        }
        if (!TryTgt(patchesPath, selections, out var targetPath, out var error))
        {
            return Failure(targetPath, error);
        }
        if (!File.Exists(targetPath))
        {
            return Success(targetPath, null, 0, "The managed Artemis definition is already absent.");
        }
        if (!IsMan(targetPath))
        {
            return Failure(targetPath, UiText.GetText("Patch.CheatLibrary.UnmanagedConflict"));
        }

        try
        {
            var existingText = R3Bound.ReadUtf8(
                targetPath,
                R3Limits.PatCatBy,
                "managed title-specific patch");
            var existing = _reader.ReadText(existingText, PatSrc.ManagedArtemis, targetPath);
            if (!existing.IsOk)
            {
                return Failure(targetPath, existing.Error ?? "Managed Artemis patch YAML is invalid.");
            }

            foreach (var selection in selections.Where(selection =>
                         !string.IsNullOrWhiteSpace(selection.Patch.DefPrint)))
            {
                var current = existing.Catalog.Entries.FirstOrDefault(entry =>
                    GetId(Exact(entry)).Equals(
                        GetId(selection),
                        StringComparison.OrdinalIgnoreCase));

                if (current is not null &&
                    !string.Equals(
                        current.DefPrint,
                        selection.Patch.DefPrint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(
                        targetPath,
                        $"Managed Artemis definition '{selection.Patch.Title}' changed after preview. Reload patches before removing it.");
                }
            }

            var remove = selections.Select(GetId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var remaining = existing.Catalog.Entries
                .Where(entry => !remove.Contains($"{entry.RunKey}|{entry.Title}"))
                .Select(Exact)
                .ToArray();
            if (remaining.Length == existing.Catalog.Entries.Count)
            {
                return Success(targetPath, null, existing.Catalog.Entries.Count,
                    "The selected managed Artemis definition is already absent.");
            }

            var state = PatchFileTransactionService.CaptureExpectedState(
                targetPath,
                R3Limits.PatCatBy,
                "managed title-specific patch");
            if (remaining.Length == 0)
            {
                return Success(
                    targetPath,
                    new PatchPreparedFile(
                        targetPath,
                        string.Empty,
                        PrepKind.PatCat,
                        PatSrc.ManagedArtemis,
                        0,
                        DelTgt: true,
                        ExpectedExists: state.Exists,
                        ExpectedSha256: state.Sha256),
                    0,
                    "The selected managed Artemis definition will be removed.");
            }

            var compiled = _comp.Compile(existingText, remaining, remaining);
            if (!compiled.IsOk)
            {
                return Failure(targetPath, compiled.Error ?? "Managed Artemis removal failed validation.");
            }
            var finalText = ManMark + "\r\n" + compiled.PatText;
            var validation = _reader.ReadText(finalText, PatSrc.ManagedArtemis, targetPath);
            if (!validation.IsOk || validation.Catalog.Entries.Count != remaining.Length)
            {
                return Failure(targetPath, validation.Error ?? "Managed Artemis removal failed read-back validation.");
            }
            return Success(
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.ManagedArtemis,
                    remaining.Length,
                    ExpectedExists: state.Exists,
                    ExpectedSha256: state.Sha256),
                remaining.Length,
                "The selected managed Artemis definition will be removed.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return Failure(targetPath, ex.Message);
        }
    }

    private static PatMatch ExactManaged(
        Rpcs3PatchEntry entry)
    {
        var match = Exact(entry);
        return match with
        {
            Patch = match.Patch with
            {
                Source = PatSrc.ManagedArtemis,
                Risk = PatRisk.Cheat,
                PatType = "Cheat"
            },
            Reason =
                "Verified managed definition generated from an exact NCL conversion."
        };
    }
    private static PatMatch Exact(
        Rpcs3PatchEntry entry)
    {
        if (entry.GameTgt.Count == 0)
        {
            throw new InvalidDataException(
                $"Managed cheat '{entry.Title}' has no game target.");
        }

        var target = entry.GameTgt[0];

        var versions = target.Versions
            .Select(PatVer.Norm)
            .Where(version => version is not null)
            .Select(version => version!)
            .ToArray();

        var exactVersion = versions.FirstOrDefault(
            version => !PatVer.IsAll(version));

        var version =
            exactVersion ??
            (versions.Length > 0 ? versions[0] : null) ??
            throw new InvalidDataException(
                $"Managed cheat '{entry.Title}' has no valid version.");

        return new PatMatch(
            Patch: entry with
            {
                Source = PatSrc.Artemis,
                Risk = PatRisk.Cheat,
                PatType = "Cheat"
            },
            Status: MatStat.Match,
            MatSer: target.Serial,
            MatVer: version,
            HasMeta: true,
            Reason:
                "Previously prepared by R3Config after an exact match.",
            TgtName: target.Title,
            TgtSer: target.Serial,
            TgtVer: version);
    }

    private static string GetId(
        PatMatch match)
    {
        return
            $"{match.Patch.RunKey}|" +
            $"{match.Patch.Title}";
    }

    private static bool TryTgt(
        string patchesPath,
        IReadOnlyList<PatMatch> selections,
        out string targetPath,
        out string error)
    {
        targetPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(patchesPath))
        {
            error = UiText.GetText(
                "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        var patchesDirectory =
            Path.GetFullPath(patchesPath);

        var parent =
            Directory.GetParent(patchesDirectory)?.FullName;

        var serials = selections
            .Select(GetSer)
            .Where(serial => serial is not null)
            .Select(serial => serial!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (serials.Length != 1)
        {
            error =
                "RPCS3 runtime cheat preparation must target exactly one game title ID per operation.";

            return false;
        }

        targetPath = Path.GetFullPath(
            GetPath(
                patchesDirectory,
                serials[0]));

        if (string.IsNullOrWhiteSpace(parent) ||
            !Directory.Exists(parent))
        {
            error = UiText.GetText(
                "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        if (!CanaryPatchImportService.InDir(
                targetPath,
                patchesDirectory))
        {
            error = UiText.GetText(
                "Patch.ImportCanaryUnsafePath");

            return false;
        }

        return true;
    }

    private static string? GetSer(
        PatMatch match)
    {
        return NormSer(
            match.TgtSer ??
            match.MatSer);
    }

    private static string? NormSer(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        var normalized =
            serial.Trim().ToUpperInvariant();

        return normalized.Length == 9 &&
               normalized.All(character =>
                   character is >= 'A' and <= 'Z' or
                   >= '0' and <= '9')
            ? normalized
            : null;
    }

    private static LibStRes Success(
        string targetPath,
        PatchPreparedFile? preparedFile,
        int patchCount,
        string message,
        IReadOnlyList<PatchPreparedFile>? extra = null)
    {
        return new LibStRes(
            true,
            targetPath,
            preparedFile,
            patchCount,
            message,
            null,
            extra ?? []);
    }

    private static LibStRes Failure(
        string targetPath,
        string error)
    {
        return new LibStRes(
            false,
            targetPath,
            null,
            0,
            error,
            error,
            []);
    }
}
