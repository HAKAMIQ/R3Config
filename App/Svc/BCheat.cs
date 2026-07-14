using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record LibStRes(
    bool IsOk,
    string TgtPath,
    PrepFile? PrepFile,
    int PatNum,
    string Message,
    string? Error,
    IReadOnlyList<PrepFile> Extra);

public sealed class ChtBndl
{
    public const string BndlName = "cheats.yml";
    // RPCS3 does not load either managed library name at game boot. The
    // title-specific file remains the runtime target.
    public const string ManName = "r3cht.yml";
    public const string OldName = "r3config_cheats_patch.yml";

    public const string ManMark =
        "# R3Config managed RPCS3 cheat patches - do not edit while R3Config is running";

    private readonly SelComp _comp = new();
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

            using var reader = new StreamReader(
                path,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

            return string.Equals(
                reader.ReadLine(),
                ManMark,
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
                    match.Patch.Source == PatSrc.Cht &&
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

            List<PatMatch> selections =
            [
                .. bundledSelections
            ];
            var extra = new List<PrepFile>();

            var existingText = string.Empty;
            if (File.Exists(targetPath))
            {
                existingText = File.ReadAllText(
                    targetPath,
                    Encoding.UTF8);

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
                    PatSrc.ManCht,
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
            }

            // Reuse compatible entries from the managed library. If only the
            // legacy library exists, stage an exact validated copy under the
            // new name in the same transaction as the runtime patch.
            var targetSerial = GetSer(bundledSelections[0])!;
            var libraryPath = GetPath(patchesPath);
            var legacyPath = OldPath(patchesPath);
            string? readPath = null;
            if (File.Exists(libraryPath) && IsMan(libraryPath))
            {
                readPath = libraryPath;
            }
            else if (File.Exists(legacyPath) && IsMan(legacyPath))
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
                    PatSrc.ManCht,
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

                if (readPath.Equals(
                        legacyPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    extra.Add(new PrepFile(
                        libraryPath,
                        libraryText,
                        PrepKind.PatCat,
                        PatSrc.ManCht,
                        libraryRead.Catalog.Entries.Count));
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

            var sourceText = File.ReadAllText(
                sourcePath,
                Encoding.UTF8);

            var compiled = _comp.Compile(
                sourceText,
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
                PatSrc.ManCht,
                targetPath);

            if (!validation.IsOk ||
                validation.Catalog.Entries.Count !=
                compiled.SelNum)
            {
                return Failure(
                    targetPath,
                    validation.Error ??
                    "The managed cheat patch failed validation.");
            }

            return Success(
                targetPath,
                new PrepFile(
                    targetPath,
                    finalText,
                    PrepKind.PatCat,
                    PatSrc.ManCht,
                    compiled.SelNum),
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

    private static PatMatch Exact(
        PatEntry entry)
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

        var version = exactVersion ??
                      (versions.Length > 0 ? versions[0] : null) ??
                      throw new InvalidDataException(
                          $"Managed cheat '{entry.Title}' has no valid version.");

        return new PatMatch(
            Patch: entry with
            {
                Source = PatSrc.Cht,
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

        if (!ImpSvc.InDir(
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

        var normalized = serial.Trim().ToUpperInvariant();
        return normalized.Length == 9 &&
               normalized.All(character =>
                   character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            ? normalized
            : null;
    }

    private static LibStRes Success(
        string targetPath,
        PrepFile? preparedFile,
        int patchCount,
        string message,
        IReadOnlyList<PrepFile>? extra = null)
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
