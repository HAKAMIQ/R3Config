using System.IO;
using System.Text.Json;
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
    ArtDepSvc artemisDependencies,
    IPatchCatalogMessageFormatter messages)
{
    public PatchCatalogResult Load(
        Rpcs3InstallDescriptor install,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var officialPath = Path.Combine(
            install.PatPath,
            "patch.yml");
        var importedPath = Rpcs3AppPaths.ImpPat(install);
        var bundledPath = BundledCanaryPatchService.GetPath(
            AppContext.BaseDirectory);
        var bundledCheatPath = BundledCheatPatchService.GetBndl(
            AppContext.BaseDirectory);
        var dependencyPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            ArtDepSvc.FileName);

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
        ArtDepCat? dependencyCatalog = null;

        sources.Add(ReadFile(
            "patch.yml",
            officialPath,
            hasOfficial,
            PatSrc.Official,
            isImported: false,
            catalogs,
            errors,
            cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            var source = BundledCheatPatchService.IsMan(
                titleSpecificPath)
                ? PatSrc.ManagedArtemis
                : PatSrc.TitlePat;

            sources.Add(ReadFile(
                Path.GetFileName(titleSpecificPath),
                titleSpecificPath,
                exists: true,
                source,
                isImported: true,
                catalogs,
                errors,
                cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (hasBundledCanary)
        {
            try
            {
                var normalized = canaryImport.Norm(
                    R3Bound.ReadUtf8(
                        bundledPath,
                        R3Limits.PatCatBy,
                        BundledCanaryPatchService.BndlName));

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

        sources.Add(ReadArtemis(
            BundledCheatPatchService.BndlName,
            bundledCheatPath,
            hasBundledCheats,
            catalogs,
            errors,
            cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            dependencyCatalog = artemisDependencies.Load(
                dependencyPath,
                bundledCheatPath);
            sources.Add(new PatchCatalogSourceStatus(
                ArtDepSvc.FileName,
                PatSrc.Artemis,
                dependencyPath,
                Exists: true,
                Loaded: true,
                dependencyCatalog.Rules.Count));
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException)
        {
            errors.Add(messages.FmtRead(
                ex.Message,
                isImported: true));
            sources.Add(new PatchCatalogSourceStatus(
                ArtDepSvc.FileName,
                PatSrc.Artemis,
                dependencyPath,
                Exists: File.Exists(dependencyPath),
                Loaded: false,
                EntNum: 0,
                ex.Message));
        }

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
            cancellationToken.ThrowIfCancellationRequested();
            var merged = catalogMerge.Merge(catalogs.ToArray());
            if (dependencyCatalog is not null)
            {
                try
                {
                    merged = artemisDependencies.Apply(
                        dependencyCatalog,
                        merged).Catalog;
                }
                catch (InvalidDataException ex)
                {
                    errors.Add(messages.FmtRead(
                        ex.Message,
                        isImported: true));
                    merged = WithoutArtemis(merged);
                }
            }
            else
            {
                merged = WithoutArtemis(merged);
            }

            return new PatchCatalogResult(
                merged,
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
        return patchMatch.Match(catalog, game)
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
        List<Mdl.PatCat> catalogs,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    private PatchCatalogSourceStatus ReadArtemis(
        string name,
        string path,
        bool exists,
        List<Mdl.PatCat> catalogs,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        return ReadFile(
            name,
            path,
            exists,
            PatSrc.Artemis,
            isImported: true,
            catalogs,
            errors,
            cancellationToken);
    }

    internal static Mdl.PatCat Classify(
        Mdl.PatCat imported,
        Mdl.PatCat bundledCanary)
    {
        var bundledKeys = bundledCanary.Entries
            .Select(PatId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bundledDefinitions = bundledCanary.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DefPrint))
            .Select(CanaryDefinitionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return imported with
        {
            Entries = imported.Entries
                .Select(entry =>
                    bundledKeys.Contains(PatId(entry)) ||
                    (!string.IsNullOrWhiteSpace(entry.DefPrint) &&
                     bundledDefinitions.Contains(CanaryDefinitionId(entry)))
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

    private static string CanaryDefinitionId(
        Rpcs3PatchEntry entry)
    {
        var targets = entry.GameTgt
            .Select(target => string.Join(
                ":",
                target.Title.Trim(),
                target.Serial.Trim().ToUpperInvariant(),
                string.Join(
                    ",",
                    target.Versions
                        .Select(version => PatVer.Norm(version) ?? version.Trim())
                        .OrderBy(version => version, StringComparer.OrdinalIgnoreCase))))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        return string.Join(
            "\0",
            entry.Title.Trim(),
            entry.DefPrint?.Trim() ?? string.Empty,
            string.Join(";", targets));
    }

    private static PatCat WithoutArtemis(PatCat catalog)
    {
        return catalog with
        {
            Entries = catalog.Entries
                .Where(entry => entry.Source is not
                    PatSrc.Artemis and not
                    PatSrc.ManagedArtemis)
                .ToArray()
        };
    }

    private void AddWarn(
        List<string> errors,
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
