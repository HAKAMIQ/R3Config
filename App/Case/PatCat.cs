using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class PatCat(
    PatchCatalogReader catalogReader,
    PatchCatalogMergeService catalogMerge,
    PatchMatchService patchMatch,
    PatchConfigReader patchConfig,
    HCanaryPatchAuthoringService hCanaryAuthoring,
    CanaryPatchImportService canaryImport,
    IPatMsg messages)
{
    public PatCatRs Load(
        Rpcs3InstallDescriptor install)
    {
        var officialPath = Path.Combine(
            install.PatchesPath,
            "patch.yml");
        var importedPath = R3Paths.ImpPat(install);
        var bundledPath = BundledCanaryPatchService.GetBundledPath(
            AppContext.BaseDirectory);
        var bundledCheatPath = BundledCheatPatchService.GetBundledPath(
            AppContext.BaseDirectory);

        var hasOfficial = File.Exists(officialPath);
        var hasImported = File.Exists(importedPath);
        var hasBundledCanary = File.Exists(bundledPath);
        var hasBundledCheats = File.Exists(bundledCheatPath);
        var titleSpecificPaths = Directory.Exists(install.PatchesPath)
            ? Directory
                .EnumerateFiles(
                    install.PatchesPath,
                    "*_patch.yml",
                    SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals(
                    CanaryPatchImportService.ImportedPatchFileName,
                    StringComparison.OrdinalIgnoreCase))
                // Legacy R3Config builds wrote this arbitrary wildcard name.
                // RPCS3's boot path never loads it, so do not present it as a
                // resident definition. The cheat service reads it only while
                // migrating entries into <TITLE_ID>_patch.yml.
                .Where(path => !Path.GetFileName(path).Equals(
                    BundledCheatPatchService.ManagedFileName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var catalogs = new List<Rpcs3PatchCatalog>();
        var errors = new List<string>();
        var sources = new List<PatSrcSt>();
        Rpcs3PatchCatalog? importedCatalog = null;
        Rpcs3PatchCatalog? bundledCanaryCatalog = null;

        sources.Add(ReadFile(
            "patch.yml",
            officialPath,
            hasOfficial,
            PatchSourceOrigin.Official,
            isImported: false,
            catalogs,
            errors));

        if (hasImported)
        {
            var read = catalogReader.Read(
                importedPath,
                PatchSourceOrigin.Imported);
            if (read.Succeeded)
            {
                importedCatalog = hCanaryAuthoring.ApplyLocalMetadata(
                    read.Catalog,
                    importedPath);
                catalogs.Add(importedCatalog);
                AddWarn(errors, read.Error, isImported: true);
                sources.Add(new PatSrcSt(
                    CanaryPatchImportService.ImportedPatchFileName,
                    PatchSourceOrigin.Imported,
                    importedPath,
                    Exists: true,
                    Loaded: true,
                    importedCatalog.Entries.Count,
                    read.Error));
            }
            else
            {
                errors.Add(messages.FmtRead(
                    read.Error,
                    isImported: true));
                sources.Add(new PatSrcSt(
                    CanaryPatchImportService.ImportedPatchFileName,
                    PatchSourceOrigin.Imported,
                    importedPath,
                    Exists: true,
                    Loaded: false,
                    EntryCount: 0,
                    read.Error));
            }
        }
        else
        {
            sources.Add(new PatSrcSt(
                CanaryPatchImportService.ImportedPatchFileName,
                PatchSourceOrigin.Imported,
                importedPath,
                Exists: false,
                Loaded: false,
                EntryCount: 0));
        }

        foreach (var titleSpecificPath in titleSpecificPaths)
        {
            var source = BundledCheatPatchService.IsManagedFile(
                titleSpecificPath)
                ? PatchSourceOrigin.ManagedCheats
                : PatchSourceOrigin.TitleSpecific;

            sources.Add(ReadFile(
                Path.GetFileName(titleSpecificPath),
                titleSpecificPath,
                exists: true,
                source,
                isImported: true,
                catalogs,
                errors));
        }

        if (hasBundledCanary)
        {
            try
            {
                var normalized = canaryImport.Normalize(
                    File.ReadAllText(bundledPath));

                if (normalized.Succeeded)
                {
                    var read = catalogReader.ReadText(
                        normalized.NormalizedText,
                        PatchSourceOrigin.BundledCanary,
                        bundledPath);
                    if (read.Succeeded)
                    {
                        bundledCanaryCatalog = read.Catalog;
                        catalogs.Add(bundledCanaryCatalog);
                        AddWarn(
                            errors,
                            read.Error,
                            isImported: true);
                        sources.Add(new PatSrcSt(
                            "can.yml",
                            PatchSourceOrigin.BundledCanary,
                            bundledPath,
                            Exists: true,
                            Loaded: true,
                            bundledCanaryCatalog.Entries.Count,
                            read.Error));
                    }
                    else
                    {
                        errors.Add(messages.FmtRead(
                            read.Error,
                            isImported: true));
                        sources.Add(new PatSrcSt(
                            "can.yml",
                            PatchSourceOrigin.BundledCanary,
                            bundledPath,
                            Exists: true,
                            Loaded: false,
                            EntryCount: 0,
                            read.Error));
                    }
                }
                else
                {
                    errors.Add(messages.FmtRead(
                        normalized.Error,
                        isImported: true));
                    sources.Add(new PatSrcSt(
                        "can.yml",
                        PatchSourceOrigin.BundledCanary,
                        bundledPath,
                        Exists: true,
                        Loaded: false,
                        EntryCount: 0,
                        normalized.Error));
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(messages.FmtRead(
                    ex.Message,
                    isImported: true));
                sources.Add(new PatSrcSt(
                    "can.yml",
                    PatchSourceOrigin.BundledCanary,
                    bundledPath,
                    Exists: true,
                    Loaded: false,
                    EntryCount: 0,
                    ex.Message));
            }
        }
        else
        {
            sources.Add(new PatSrcSt(
                "can.yml",
                PatchSourceOrigin.BundledCanary,
                bundledPath,
                Exists: false,
                Loaded: false,
                EntryCount: 0));
        }

        sources.Add(ReadFile(
            "cheats.yml",
            bundledCheatPath,
            hasBundledCheats,
            PatchSourceOrigin.BundledCheats,
            isImported: true,
            catalogs,
            errors));

        if (importedCatalog is not null &&
            bundledCanaryCatalog is not null)
        {
            var index = catalogs.IndexOf(importedCatalog);
            if (index >= 0)
            {
                catalogs[index] = Classify(
                    importedCatalog,
                    bundledCanaryCatalog);
            }
        }

        if (catalogs.Count > 0)
        {
            return new PatCatRs(
                catalogMerge.Merge(catalogs.ToArray()),
                JoinErr(errors),
                sources);
        }

        return new PatCatRs(
            null,
            JoinErr(errors) ??
            (!hasOfficial &&
             !hasImported &&
             titleSpecificPaths.Length == 0 &&
             !hasBundledCanary &&
             !hasBundledCheats
                ? messages.MissCat
                : messages.BadCat),
            sources);
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> Build(
        Rpcs3PatchCatalog catalog,
        Rpcs3GameDescriptor game)
    {
        return catalog.Entries
            .Select(entry => patchMatch.Match(entry, [game]))
            .Where(match => match.Status is not
                PatchMatchStatus.SerialMismatch and not
                PatchMatchStatus.NoMetadata)
            .ToArray();
    }

    public bool CanRead(
        Rpcs3InstallDescriptor install)
    {
        try
        {
            ReadCfg(install);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public PatchConfigSnapshot ReadCfg(
        Rpcs3InstallDescriptor install)
    {
        return patchConfig.ReadSnapshot(
            install.PatchConfigPath);
    }

    private PatSrcSt ReadFile(
        string name,
        string path,
        bool exists,
        PatchSourceOrigin source,
        bool isImported,
        ICollection<Rpcs3PatchCatalog> catalogs,
        ICollection<string> errors)
    {
        if (!exists)
        {
            return new PatSrcSt(
                name,
                source,
                path,
                Exists: false,
                Loaded: false,
                EntryCount: 0);
        }

        try
        {
            var read = catalogReader.Read(path, source);
            if (read.Succeeded)
            {
                catalogs.Add(read.Catalog);
                AddWarn(errors, read.Error, isImported);
                return new PatSrcSt(
                    name,
                    source,
                    path,
                    Exists: true,
                    Loaded: true,
                    read.Catalog.Entries.Count,
                    read.Error);
            }

            errors.Add(messages.FmtRead(
                read.Error,
                isImported));
            return new PatSrcSt(
                name,
                source,
                path,
                Exists: true,
                Loaded: false,
                EntryCount: 0,
                read.Error);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(messages.FmtRead(
                ex.Message,
                isImported));
            return new PatSrcSt(
                name,
                source,
                path,
                Exists: true,
                Loaded: false,
                EntryCount: 0,
                ex.Message);
        }
    }

    private static Rpcs3PatchCatalog Classify(
        Rpcs3PatchCatalog imported,
        Rpcs3PatchCatalog bundledCanary)
    {
        var bundledKeys = bundledCanary.Entries
            .Select(PatId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return imported with
        {
            Entries = imported.Entries
                .Select(entry => bundledKeys.Contains(PatId(entry))
                    ? entry with
                    {
                        Risk = PatchRisk.Canary,
                        PatchType = "Canary"
                    }
                    : entry)
                .ToArray()
        };
    }

    private static string PatId(
        Rpcs3PatchEntry entry)
    {
        return $"{entry.RuntimeHashKey}|{entry.Title}";
    }

    private void AddWarn(
        ICollection<string> errors,
        string? warning,
        bool isImported)
    {
        if (!string.IsNullOrWhiteSpace(warning))
        {
            errors.Add(messages.FmtRead(
                warning,
                isImported));
        }
    }

    private static string? JoinErr(
        IReadOnlyList<string> errors)
    {
        var cleanErrors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray();

        return cleanErrors.Length == 0
            ? null
            : string.Join(Environment.NewLine, cleanErrors);
    }

}
