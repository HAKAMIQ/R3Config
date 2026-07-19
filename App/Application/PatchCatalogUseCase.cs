using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class PatchCatalogUseCase(
    Svc.ReadSvc catalogReader,
    PatchCatalogMergeService catalogMerge,
    Svc.PatchMatchService patchMatch,
    CfgRead patchConfig,
    HCanaryPatchAuthoringService hCanaryAuthoring,
    Svc.CanaryPatchImportService canaryImport,
    IPatchCatalogMessageFormatter messages)
{
    public PatchCatalogResult Load(
        Rpcs3InstallDescriptor install)
    {
        var officialPath = Path.Combine(
            install.PatPath,
            "patch.yml");
        var importedPath = Rpcs3AppPaths.ImpPat(install);
        var bundledPath = BundledCanaryPatchService.GetPath(
            AppContext.BaseDirectory);
        var bundledCheatPath = BundledCheatPatchService.GetBndl(
            AppContext.BaseDirectory);

        var hasOfficial = File.Exists(officialPath);
        var hasImported = File.Exists(importedPath);
        var hasBundledCanary = File.Exists(bundledPath);
        var hasBundledCheats = File.Exists(bundledCheatPath);
        var titleSpecificPaths = Directory.Exists(install.PatPath)
            ? Directory
                .EnumerateFiles(
                    install.PatPath,
                    "*_patch.yml",
                    SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals(
                    Svc.CanaryPatchImportService.ImpName,
                    StringComparison.OrdinalIgnoreCase))
                // Legacy R3Config builds wrote this arbitrary wildcard name.
                // RPCS3's boot path never loads it, so do not present it as a
                // resident definition. The cheat service reads it only while
                // migrating entries into <TITLE_ID>_patch.yml.
                .Where(path => !Path.GetFileName(path).Equals(
                    BundledCheatPatchService.ManName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var catalogs = new List<Mdl.PatCat>();
        var errors = new List<string>();
        var sources = new List<PatchCatalogSourceStatus>();
        Mdl.PatCat? importedCatalog = null;
        Mdl.PatCat? bundledCanaryCatalog = null;

        sources.Add(ReadFile(
            "patch.yml",
            officialPath,
            hasOfficial,
            PatSrc.Official,
            isImported: false,
            catalogs,
            errors));

        if (hasImported)
        {
            var read = catalogReader.Read(
                importedPath,
                PatSrc.Imported);
            if (read.IsOk)
            {
                importedCatalog = hCanaryAuthoring.Apply(
                    read.Catalog,
                    importedPath);
                catalogs.Add(importedCatalog);
                AddWarn(errors, read.Error, isImported: true);
                sources.Add(new PatchCatalogSourceStatus(
                    Svc.CanaryPatchImportService.ImpName,
                    PatSrc.Imported,
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
                sources.Add(new PatchCatalogSourceStatus(
                    Svc.CanaryPatchImportService.ImpName,
                    PatSrc.Imported,
                    importedPath,
                    Exists: true,
                    Loaded: false,
                    EntNum: 0,
                    read.Error));
            }
        }
        else
        {
            sources.Add(new PatchCatalogSourceStatus(
                Svc.CanaryPatchImportService.ImpName,
                PatSrc.Imported,
                importedPath,
                Exists: false,
                Loaded: false,
                EntNum: 0));
        }

        foreach (var titleSpecificPath in titleSpecificPaths)
        {
            var source = BundledCheatPatchService.IsMan(
                titleSpecificPath)
                ? PatSrc.ManCht
                : PatSrc.TitlePat;

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
                var normalized = canaryImport.Norm(
                    File.ReadAllText(bundledPath));

                if (normalized.IsOk)
                {
                    var read = catalogReader.ReadText(
                        normalized.NormText,
                        PatSrc.Can,
                        bundledPath);
                    if (read.IsOk)
                    {
                        bundledCanaryCatalog = read.Catalog;
                        catalogs.Add(bundledCanaryCatalog);
                        AddWarn(
                            errors,
                            read.Error,
                            isImported: true);
                        sources.Add(new PatchCatalogSourceStatus(
                            "can.yml",
                            PatSrc.Can,
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
                        sources.Add(new PatchCatalogSourceStatus(
                            "can.yml",
                            PatSrc.Can,
                            bundledPath,
                            Exists: true,
                            Loaded: false,
                            EntNum: 0,
                            read.Error));
                    }
                }
                else
                {
                    errors.Add(messages.FmtRead(
                        normalized.Error,
                        isImported: true));
                    sources.Add(new PatchCatalogSourceStatus(
                        "can.yml",
                        PatSrc.Can,
                        bundledPath,
                        Exists: true,
                        Loaded: false,
                        EntNum: 0,
                        normalized.Error));
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(messages.FmtRead(
                    ex.Message,
                    isImported: true));
                sources.Add(new PatchCatalogSourceStatus(
                    "can.yml",
                    PatSrc.Can,
                    bundledPath,
                    Exists: true,
                    Loaded: false,
                    EntNum: 0,
                    ex.Message));
            }
        }
        else
        {
            sources.Add(new PatchCatalogSourceStatus(
                "can.yml",
                PatSrc.Can,
                bundledPath,
                Exists: false,
                Loaded: false,
                EntNum: 0));
        }

        sources.Add(ReadFile(
            "cheats.yml",
            bundledCheatPath,
            hasBundledCheats,
            PatSrc.Cht,
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
            return new PatchCatalogResult(
                catalogMerge.Merge(catalogs.ToArray()),
                JoinErr(errors),
                sources);
        }

        return new PatchCatalogResult(
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

    public IReadOnlyList<Mdl.PatMatch> Build(
        Mdl.PatCat catalog,
        Rpcs3GameDescriptor game)
    {
        return catalog.Entries
            .Select(entry => patchMatch.Match(entry, [game]))
            .Where(match => match.Status is not
                MatStat.SerBad and not
                MatStat.NoMeta)
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

    public CfgSnap ReadCfg(
        Rpcs3InstallDescriptor install)
    {
        return patchConfig.ReadSnap(
            install.PatCfg);
    }

    private PatchCatalogSourceStatus ReadFile(
        string name,
        string path,
        bool exists,
        PatSrc source,
        bool isImported,
        ICollection<Mdl.PatCat> catalogs,
        ICollection<string> errors)
    {
        if (!exists)
        {
            return new PatchCatalogSourceStatus(
                name,
                source,
                path,
                Exists: false,
                Loaded: false,
                EntNum: 0);
        }

        try
        {
            var read = catalogReader.Read(path, source);
            if (read.IsOk)
            {
                catalogs.Add(read.Catalog);
                AddWarn(errors, read.Error, isImported);
                return new PatchCatalogSourceStatus(
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
            return new PatchCatalogSourceStatus(
                name,
                source,
                path,
                Exists: true,
                Loaded: false,
                EntNum: 0,
                read.Error);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(messages.FmtRead(
                ex.Message,
                isImported));
            return new PatchCatalogSourceStatus(
                name,
                source,
                path,
                Exists: true,
                Loaded: false,
                EntNum: 0,
                ex.Message);
        }
    }

    private static Mdl.PatCat Classify(
        Mdl.PatCat imported,
        Mdl.PatCat bundledCanary)
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
                        Risk = PatRisk.Canary,
                        Class = PatClass.Canary
                    }
                    : entry)
                .ToArray()
        };
    }

    private static string PatId(
        Rpcs3PatchEntry entry)
    {
        return $"{entry.RunKey}|{entry.Title}";
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
