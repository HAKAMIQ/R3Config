using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record PatchLibraryWriteResult(
    bool Succeeded,
    string TargetPath,
    string? BackupPath,
    int PatchCount,
    string Message,
    string? Error);

public sealed record PatchLibraryStageResult(
    bool Succeeded,
    string TargetPath,
    PatchPreparedFile? PreparedFile,
    int PatchCount,
    string Message,
    string? Error);

public sealed class BundledCheatPatchService
{
    public const string BundledFileName = "cheats.yml";
    // Kept only so older R3Config-owned libraries can be migrated. RPCS3 does
    // not load this arbitrary filename while booting a game.
    public const string ManagedFileName = "r3config_cheats_patch.yml";

    public const string ManagedMarker =
        "# R3Config managed RPCS3 cheat patches - do not edit while R3Config is running";

    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly PatchSelectionCompiler _compiler = new();
    private readonly PatchCatalogReader _reader = new();

    public static string GetBundledPath(string baseDirectory)
    {
        return Path.Combine(
            baseDirectory,
            "Data",
            BundledFileName);
    }

    public static string GetManagedPath(string patchesPath)
    {
        return Path.Combine(
            patchesPath,
            ManagedFileName);
    }

    public static string GetManagedPath(
        string patchesPath,
        string serial)
    {
        var normalizedSerial = NormalizeSerial(serial)
            ?? throw new ArgumentException(
                "A managed RPCS3 cheat file requires a valid 9-character title ID.",
                nameof(serial));

        return Path.Combine(
            patchesPath,
            normalizedSerial + "_patch.yml");
    }

    public static IReadOnlyList<string> GetManagedPaths(
        string patchesPath,
        IEnumerable<Rpcs3PatchMatchResult> matches)
    {
        return matches
            .Select(MatchedSerial)
            .Where(serial => serial is not null)
            .Select(serial => GetManagedPath(patchesPath, serial!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsManagedFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var reader = new StreamReader(
                path,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

            return string.Equals(
                reader.ReadLine(),
                ManagedMarker,
                StringComparison.Ordinal);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            return false;
        }
    }

    public PatchLibraryStageResult PrepareManagedPatchFile(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        string targetPath = string.Empty;

        try
        {
            var bundledSelections = selectedMatches
                .Where(match =>
                    match.Patch.Source == PatchSourceOrigin.BundledCheats &&
                    PatchActivationPolicy.CanEnable(match))
                .ToArray();

            if (bundledSelections.Length == 0)
            {
                return StageSuccess(
                    string.Empty,
                    null,
                    0,
                    UiText.T("Patch.CheatLibrary.NoPrepare"));
            }

            if (!TryResolveTarget(
                    patchesPath,
                    bundledSelections,
                    out targetPath,
                    out var pathError))
            {
                return StageFailure(
                    targetPath,
                    pathError);
            }

            var sourcePath = GetBundledPath(baseDirectory);
            if (!File.Exists(sourcePath))
            {
                return StageFailure(
                    targetPath,
                    UiText.T("Patch.CheatLibrary.Missing"));
            }

            List<Rpcs3PatchMatchResult> selections =
            [
                .. bundledSelections
            ];

            var existingText = string.Empty;
            if (File.Exists(targetPath))
            {
                existingText = File.ReadAllText(
                    targetPath,
                    Encoding.UTF8);

                if (!existingText.StartsWith(
                        ManagedMarker,
                        StringComparison.Ordinal))
                {
                    return StageFailure(
                        targetPath,
                        UiText.T(
                            "Patch.CheatLibrary.UnmanagedConflict"));
                }

                var existingRead = _reader.ReadText(
                    existingText,
                    PatchSourceOrigin.ManagedCheats,
                    targetPath);

                if (!existingRead.Succeeded)
                {
                    return StageFailure(
                        targetPath,
                        existingRead.Error ??
                        UiText.T(
                            "Patch.ImportCanaryInvalidYaml"));
                }

                selections.AddRange(
                    existingRead.Catalog.Entries.Select(
                        ToExactMatch));
            }

            // R3Config versions before the title-specific runtime fix wrote an
            // inert r3config_cheats_patch.yml. RPCS3 only loads patch.yml,
            // imported_patch.yml and <TITLE_ID>_patch.yml at game boot. Reuse
            // compatible entries from the owned legacy file when the user next
            // enables a cheat, but never treat an unmarked file as ours.
            var targetSerial = MatchedSerial(bundledSelections[0])!;
            var legacyPath = GetManagedPath(patchesPath);
            if (!legacyPath.Equals(
                    targetPath,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(legacyPath) &&
                IsManagedFile(legacyPath))
            {
                var legacyRead = _reader.Read(
                    legacyPath,
                    PatchSourceOrigin.ManagedCheats);

                if (legacyRead.Succeeded)
                {
                    selections.AddRange(
                        legacyRead.Catalog.Entries
                            .Where(entry => entry.GameTargets.Any(target =>
                                string.Equals(
                                    target.Serial,
                                    targetSerial,
                                    StringComparison.OrdinalIgnoreCase)))
                            .Select(ToExactMatch));
                }
            }

            selections =
            [
                .. selections
                    .GroupBy(
                        PatchIdentity,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
            ];

            var sourceText = File.ReadAllText(
                sourcePath,
                Encoding.UTF8);

            var compiled = _compiler.Compile(
                sourceText,
                selections,
                bundledSelections);

            if (!compiled.Succeeded)
            {
                return StageFailure(
                    targetPath,
                    compiled.Error ??
                    UiText.T(
                        "Patch.ImportCanaryInvalidYaml"));
            }

            var finalText =
                ManagedMarker +
                "\r\n" +
                compiled.PatchText;

            if (File.Exists(targetPath) &&
                string.Equals(
                    existingText,
                    finalText,
                    StringComparison.Ordinal))
            {
                return StageSuccess(
                    targetPath,
                    null,
                    compiled.SelectedCount,
                    UiText.F(
                        "Patch.CheatLibrary.Ready",
                        compiled.SelectedCount));
            }

            var validation = _reader.ReadText(
                finalText,
                PatchSourceOrigin.ManagedCheats,
                targetPath);

            if (!validation.Succeeded ||
                validation.Catalog.Entries.Count !=
                compiled.SelectedCount)
            {
                return StageFailure(
                    targetPath,
                    validation.Error ??
                    "The managed cheat patch failed validation.");
            }

            return StageSuccess(
                targetPath,
                new PatchPreparedFile(
                    targetPath,
                    finalText,
                    PatchPreparedFileKind.PatchCatalog,
                    PatchSourceOrigin.ManagedCheats,
                    compiled.SelectedCount),
                compiled.SelectedCount,
                UiText.F(
                    "Patch.CheatLibrary.Prepared",
                    bundledSelections.Length,
                    compiled.SelectedCount));
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException)
        {
            return StageFailure(
                targetPath,
                ex.Message);
        }
    }

    public PatchLibraryWriteResult PrepareManagedPatch(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        string targetPath = string.Empty;
        string? backupPath = null;
        string? tempPath = null;

        try
        {
            var staged = PrepareManagedPatchFile(
                patchesPath,
                baseDirectory,
                selectedMatches);

            targetPath = staged.TargetPath;

            if (!staged.Succeeded)
            {
                return Failure(
                    targetPath,
                    null,
                    staged.Error ?? staged.Message);
            }

            if (staged.PreparedFile is null)
            {
                return Success(
                    targetPath,
                    null,
                    staged.PatchCount,
                    staged.Message);
            }

            var patchesDirectory =
                Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException(
                    "The managed cheat target has no parent directory.");

            Directory.CreateDirectory(
                patchesDirectory);

            if (File.Exists(targetPath))
            {
                backupPath =
                    CreateBackupPath(
                        patchesDirectory,
                        targetPath);

                File.Copy(
                    targetPath,
                    backupPath,
                    overwrite: false);
            }

            tempPath =
                targetPath +
                ".tmp-" +
                Guid.NewGuid().ToString("N");

            File.WriteAllText(
                tempPath,
                staged.PreparedFile.Content,
                Utf8NoBom);

            var validation = _reader.Read(
                tempPath,
                PatchSourceOrigin.ManagedCheats);

            if (!validation.Succeeded ||
                validation.Catalog.Entries.Count !=
                staged.PatchCount)
            {
                throw new InvalidDataException(
                    validation.Error ??
                    "The managed cheat patch failed read-back validation.");
            }

            File.Move(
                tempPath,
                targetPath,
                overwrite: true);

            tempPath = null;

            return Success(
                targetPath,
                backupPath,
                staged.PatchCount,
                staged.Message);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException or
            InvalidOperationException)
        {
            if (tempPath is not null)
            {
                TryDelete(tempPath);
            }

            if (backupPath is not null &&
                File.Exists(backupPath) &&
                !string.IsNullOrWhiteSpace(targetPath))
            {
                TryRestoreBackup(
                    backupPath,
                    targetPath);
            }

            return Failure(
                targetPath,
                backupPath,
                ex.Message);
        }
    }

    private static Rpcs3PatchMatchResult ToExactMatch(
        Rpcs3PatchEntry entry)
    {
        if (entry.GameTargets.Count == 0)
        {
            throw new InvalidDataException(
                $"Managed cheat '{entry.Title}' has no game target.");
        }

        var target = entry.GameTargets[0];

        var versions = target.Versions
            .Select(Rpcs3PatchVersion.Normalize)
            .Where(version => version is not null)
            .Select(version => version!)
            .ToArray();

        var exactVersion = versions.FirstOrDefault(
            version => !Rpcs3PatchVersion.IsAll(version));

        var version = exactVersion ??
                      (versions.Length > 0 ? versions[0] : null) ??
                      throw new InvalidDataException(
                          $"Managed cheat '{entry.Title}' has no valid version.");

        return new Rpcs3PatchMatchResult(
            Patch: entry with
            {
                Source = PatchSourceOrigin.BundledCheats,
                Risk = PatchRisk.Cheat,
                PatchType = "Cheat"
            },
            Status: PatchMatchStatus.ExactMatch,
            MatchedSerial: target.Serial,
            MatchedVersion: version,
            HasHashMetadata: true,
            MatchReason:
                "Previously prepared by R3Config after an exact match.",
            MatchedTargetTitle: target.Title,
            MatchedTargetSerial: target.Serial,
            MatchedTargetVersion: version);
    }

    private static string PatchIdentity(
        Rpcs3PatchMatchResult match)
    {
        return
            $"{match.Patch.RuntimeHashKey}|" +
            $"{match.Patch.Title}";
    }

    private static bool TryResolveTarget(
        string patchesPath,
        IReadOnlyList<Rpcs3PatchMatchResult> selections,
        out string targetPath,
        out string error)
    {
        targetPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(patchesPath))
        {
            error = UiText.T(
                "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        var patchesDirectory =
            Path.GetFullPath(patchesPath);

        var parent =
            Directory.GetParent(patchesDirectory)?.FullName;

        var serials = selections
            .Select(MatchedSerial)
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
            GetManagedPath(
                patchesDirectory,
                serials[0]));

        if (string.IsNullOrWhiteSpace(parent) ||
            !Directory.Exists(parent))
        {
            error = UiText.T(
                "Patch.ImportCanaryNoRpcs3Path");

            return false;
        }

        if (!CanaryPatchImportService.IsPathInsideDirectory(
                targetPath,
                patchesDirectory))
        {
            error = UiText.T(
                "Patch.ImportCanaryUnsafePath");

            return false;
        }

        return true;
    }

    private static string CreateBackupPath(
        string patchesDirectory,
        string targetPath)
    {
        var timestamp = DateTime.Now.ToString(
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture);

        var candidate = Path.Combine(
            patchesDirectory,
            Path.GetFileName(targetPath) +
            ".bak-" +
            timestamp);

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        return Path.Combine(
            patchesDirectory,
            Path.GetFileName(targetPath) +
            ".bak-" +
            DateTime.Now.Ticks.ToString(
                CultureInfo.InvariantCulture));
    }

    private static string? MatchedSerial(
        Rpcs3PatchMatchResult match)
    {
        return NormalizeSerial(
            match.MatchedTargetSerial ??
            match.MatchedSerial);
    }

    private static string? NormalizeSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        var normalized = serial.Trim().ToUpperInvariant();
        return normalized.Length == 9 &&
               normalized.All(character =>
                   character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            ? normalized
            : null;
    }

    private static PatchLibraryStageResult StageSuccess(
        string targetPath,
        PatchPreparedFile? preparedFile,
        int patchCount,
        string message)
    {
        return new PatchLibraryStageResult(
            true,
            targetPath,
            preparedFile,
            patchCount,
            message,
            null);
    }

    private static PatchLibraryStageResult StageFailure(
        string targetPath,
        string error)
    {
        return new PatchLibraryStageResult(
            false,
            targetPath,
            null,
            0,
            error,
            error);
    }

    private static PatchLibraryWriteResult Success(
        string targetPath,
        string? backupPath,
        int patchCount,
        string message)
    {
        return new PatchLibraryWriteResult(
            true,
            targetPath,
            backupPath,
            patchCount,
            message,
            null);
    }

    private static PatchLibraryWriteResult Failure(
        string targetPath,
        string? backupPath,
        string error)
    {
        return new PatchLibraryWriteResult(
            false,
            targetPath,
            backupPath,
            0,
            error,
            error);
    }

    private static void TryRestoreBackup(
        string backupPath,
        string targetPath)
    {
        try
        {
            File.Copy(
                backupPath,
                targetPath,
                overwrite: true);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
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
        }
    }
}
