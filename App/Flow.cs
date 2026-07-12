using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

public sealed class AppFlow
{
    private readonly GameScanner _scan = new();
    private readonly PlanService _plan = new();
    private readonly BackupService _bkp = new();
    private readonly ConfigApplyService _cfgApply = new();
    private readonly PatchCatalogReader _patchCat = new();
    private readonly PatchCatalogMergeService _patchCatMerge = new();
    private readonly PatchMatchService _patchMatch = new();
    private readonly PatchConfigReader _patchCfg = new();
    private readonly PatchApplyPlanner _patchPlan = new();
    private readonly PatchApplyService _patchApply = new();
    private readonly CatInfoService _catInfo = new();
    private readonly HwProbe _hw = new();
    private readonly PatchHashProbeService _patchHash = new();
    private readonly BundledCanaryPatchService _bundledCanary = new();
    private readonly HCanaryPatchAuthoringService _hCanaryAuthoring = new();

    public CatLoadResult LoadBuiltInCat(string baseDir)
    {
        var path = Path.Combine(baseDir, "Data", "cat.json");

        try
        {
            return new CatLoadResult(_catInfo.LoadCatalog(path), null);
        }
        catch (CatalogMissingException ex)
        {
            return new CatLoadResult(null, ex.Message);
        }
        catch (CatalogInvalidException ex)
        {
            return new CatLoadResult(null, ex.Message);
        }
        catch
        {
            return new CatLoadResult(null, CatalogInvalidException.UserMessage);
        }
    }

    public async Task<AnalyzeResult> AnalyzeAsync(string rpcs3ExePath, RecommendationCatalogService cat)
    {
        var install = Rpcs3InstallService.FromExePath(rpcs3ExePath);
        var hw = await Task.Run(() => _hw.Probe());
        var scanned = await Task.Run(() => _scan.Scan(install));
        var games = await Task.Run(() => _patchHash.Enrich(install, scanned));

        var rows = games
            .Select(game =>
            {
                var rec = cat.Find(game.Serial);
                var plan = _plan.CreateConfigPlan(install, game, rec, hw);
                return new GamePlanRow(game, rec, plan);
            })
            .ToArray();

        var patchCat = LoadPatchCat(install);
        var backups = ListBackups(install);

        return new AnalyzeResult(install, hw, rows, patchCat, backups);
    }

    public ConfigPlan BuildCfgPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiGameRecommendation? rec,
        HwInfo? hw)
    {
        return _plan.CreateConfigPlan(install, game, rec, hw);
    }

    public ConfigApplyResult ApplyCfg(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        return _cfgApply.Apply(plan, changes, BkpRoot(install));
    }

    public ConfigApplyResult RemoveCfg(
        ConfigPlan plan,
        IReadOnlyList<ConfigChange> changes,
        Rpcs3InstallDescriptor install)
    {
        return _cfgApply.Remove(plan, changes, BkpRoot(install));
    }

    public PatchCatResult LoadPatchCat(Rpcs3InstallDescriptor install)
    {
        var officialPath = Path.Combine(install.PatchesPath, "patch.yml");
        var importedPath = Path.Combine(install.PatchesPath, "imported_patch.yml");
        var bundledPath = _bundledCanary.GetBundledPath(AppContext.BaseDirectory);

        var hasOfficial = File.Exists(officialPath);
        var hasImported = File.Exists(importedPath);
        var hasBundledCanary = File.Exists(bundledPath);
        var titleSpecificPaths = Directory.Exists(install.PatchesPath)
            ? Directory.EnumerateFiles(install.PatchesPath, "*_patch.yml", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals("imported_patch.yml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var catalogs = new List<Rpcs3PatchCatalog>();
        var errors = new List<string>();
        Rpcs3PatchCatalog? importedCatalog = null;
        Rpcs3PatchCatalog? bundledCanaryCatalog = null;

        if (hasOfficial)
        {
            var read = _patchCat.Read(officialPath, PatchSourceOrigin.Official);
            if (read.Succeeded)
            {
                catalogs.Add(read.Catalog);
                AddPatchReadWarning(errors, read.Error, isImported: false);
            }
            else
            {
                errors.Add(FormatPatchReadError(read.Error, isImported: false));
            }
        }

        if (hasImported)
        {
            var read = _patchCat.Read(importedPath, PatchSourceOrigin.Imported);
            if (read.Succeeded)
            {
                importedCatalog = _hCanaryAuthoring.ApplyLocalMetadata(read.Catalog, importedPath);
                catalogs.Add(importedCatalog);
                AddPatchReadWarning(errors, read.Error, isImported: true);
            }
            else
            {
                errors.Add(FormatPatchReadError(read.Error, isImported: true));
            }
        }

        foreach (var titleSpecificPath in titleSpecificPaths)
        {
            var read = _patchCat.Read(titleSpecificPath, PatchSourceOrigin.TitleSpecific);
            if (read.Succeeded)
            {
                catalogs.Add(read.Catalog);
                AddPatchReadWarning(errors, read.Error, isImported: true);
            }
            else
            {
                errors.Add(FormatPatchReadError(read.Error, isImported: true));
            }
        }

        if (hasBundledCanary)
        {
            try
            {
                var bundledText = File.ReadAllText(bundledPath);
                var normalize = new CanaryPatchImportService().Normalize(bundledText);

                if (normalize.Succeeded)
                {
                    var read = _patchCat.ReadText(
                        normalize.NormalizedText,
                        PatchSourceOrigin.BundledCanary,
                        bundledPath);
                    if (read.Succeeded)
                    {
                        bundledCanaryCatalog = read.Catalog;
                        catalogs.Add(bundledCanaryCatalog);
                        AddPatchReadWarning(errors, read.Error, isImported: true);
                    }
                    else
                    {
                        errors.Add(FormatPatchReadError(read.Error, isImported: true));
                    }
                }
                else
                {
                    errors.Add(FormatPatchReadError(normalize.Error, isImported: true));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(FormatPatchReadError(ex.Message, isImported: true));
            }
        }

        if (importedCatalog is not null && bundledCanaryCatalog is not null)
        {
            var index = catalogs.IndexOf(importedCatalog);
            if (index >= 0)
            {
                importedCatalog = ClassifyImportedCanaryEntries(importedCatalog, bundledCanaryCatalog);
                catalogs[index] = importedCatalog;
            }
        }

        if (catalogs.Count > 0)
        {
            var merged = _patchCatMerge.Merge(catalogs.ToArray());
            return new PatchCatResult(
                merged,
                CombinePatchReadErrors(errors));
        }

        return new PatchCatResult(
            null,
            CombinePatchReadErrors(errors) ??
            (!hasOfficial && !hasImported && titleSpecificPaths.Length == 0 && !hasBundledCanary
                ? UiText.T("Patch.FileMissing")
                : UiText.T("Patch.FileInvalid")));
    }

    public CanaryPatchWriteResult PrepareBundledCanaryPatch(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> selectedMatches)
    {
        return _bundledCanary.PrepareImportedPatch(install.PatchesPath, AppContext.BaseDirectory, selectedMatches);
    }

    public CanaryPatchRemoveResult RemovePreparedCanaryPatch(Rpcs3InstallDescriptor install)
    {
        return _bundledCanary.RemovePreparedCanary(install.PatchesPath);
    }

    public IReadOnlyList<Rpcs3PatchMatchResult> BuildPatchPreview(
        Rpcs3PatchCatalog patchCat,
        Rpcs3GameDescriptor game)
    {
        return patchCat.Entries
            .Select(entry => _patchMatch.Match(entry, [game]))
            .Where(match => match.Status is not PatchMatchStatus.SerialMismatch and not PatchMatchStatus.NoMetadata)
            .ToArray();
    }

    public PatchApplyPlan BuildPatchPlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches,
        bool expertMode)
    {
        return _patchPlan.CreatePlan(install, matches, expertMode);
    }

    public PatchApplyPlan BuildPatchDisablePlan(
        Rpcs3InstallDescriptor install,
        IReadOnlyList<Rpcs3PatchMatchResult> matches)
    {
        return _patchPlan.CreateDisablePlan(install, matches);
    }

    public PatchApplyChange? CreatePatchChange(Rpcs3PatchMatchResult match)
    {
        return _patchPlan.CreateChangeForMatch(match);
    }

    public PatchApplyResult ApplyPatch(PatchApplyPlan plan, Rpcs3InstallDescriptor install)
    {
        return _patchApply.Apply(plan, BkpRoot(install));
    }

    public PatchApplyResult DisablePatch(PatchApplyPlan plan, Rpcs3InstallDescriptor install)
    {
        return _patchApply.Disable(plan, BkpRoot(install));
    }

    public bool CanReadPatchCfg(Rpcs3InstallDescriptor install)
    {
        try
        {
            _patchCfg.Load(install.PatchConfigPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public RecommendationCatalogService RebuildCat(string xmlPath)
    {
        var wiki = WikiRecommendationService.Load(xmlPath);
        var sourceHash = _catInfo.ComputeFileHash(xmlPath);

        return RecommendationCatalogService.FromRecommendations(
            "Developer import",
            DateTime.Today.ToString("yyyy-MM-dd"),
            wiki.Recommendations,
            DateTimeOffset.UtcNow,
            sourceHash);
    }

    public string CatInfoText(CatalogInfo info)
    {
        return _catInfo.FormatInfo(info);
    }

    public void ExportCat(RecommendationCatalogService cat, string path)
    {
        cat.Save(path);
    }

    public IReadOnlyList<BackupSnapshot> ListBackups(Rpcs3InstallDescriptor install)
    {
        return _bkp.ListBackups(BkpRoot(install));
    }

    public void Restore(BackupSnapshot bkp)
    {
        _bkp.Restore(bkp);
    }

    public static string BkpRoot(Rpcs3InstallDescriptor install)
    {
        return Path.Combine(install.ConfigPath, "R3ConfigBackups");
    }
    public IReadOnlyList<HCanaryStoredPatch> LoadHCanaryPatches(Rpcs3InstallDescriptor install)
    {
        return _hCanaryAuthoring.Load(Path.Combine(install.PatchesPath, "imported_patch.yml"));
    }

    public HCanarySaveResult SaveHCanaryPatch(
        Rpcs3InstallDescriptor install,
        HCanaryPatchDraft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        return _hCanaryAuthoring.Save(
            Path.Combine(install.PatchesPath, "imported_patch.yml"),
            BkpRoot(install),
            draft,
            originalIdentity,
            saveAsNew);
    }

    public HCanarySaveResult DeleteHCanaryPatch(
        Rpcs3InstallDescriptor install,
        string identity)
    {
        return _hCanaryAuthoring.Delete(
            Path.Combine(install.PatchesPath, "imported_patch.yml"),
            BkpRoot(install),
            identity);
    }

    private static Rpcs3PatchCatalog ClassifyImportedCanaryEntries(
        Rpcs3PatchCatalog imported,
        Rpcs3PatchCatalog bundledCanary)
    {
        var bundledKeys = bundledCanary.Entries
            .Select(PatchIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = imported.Entries
            .Select(entry => bundledKeys.Contains(PatchIdentity(entry))
                ? entry with { Risk = PatchRisk.Canary, PatchType = "Canary" }
                : entry)
            .ToArray();

        return imported with { Entries = entries };
    }

    private static string PatchIdentity(Rpcs3PatchEntry entry)
    {
        return $"{entry.HashKind}|{entry.Hash}|{entry.Title}";
    }

    private static void AddPatchReadWarning(
        ICollection<string> errors,
        string? warning,
        bool isImported)
    {
        if (!string.IsNullOrWhiteSpace(warning))
        {
            errors.Add(FormatPatchReadError(warning, isImported));
        }
    }

    private static string FormatPatchReadError(string? rawError, bool isImported)
    {
        if (isImported && IsDuplicateKeyError(rawError))
        {
            return UiText.T("Patch.ImportedDuplicateKey");
        }

        if (!isImported && IsDuplicateKeyError(rawError))
        {
            return UiText.T("Patch.Localized.DuplicateKey");
        }

        var fallback = isImported
            ? UiText.T("Patch.ImportedFileInvalid")
            : UiText.T("Patch.FileInvalid");

        var detail = string.IsNullOrWhiteSpace(rawError)
            ? fallback
            : rawError.Trim();

        return UiText.F(
            isImported ? "Patch.ImportedReadWarning" : "Patch.OfficialReadWarning",
            detail);
    }

    private static string? CombinePatchReadErrors(IReadOnlyList<string> errors)
    {
        var cleanErrors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray();

        return cleanErrors.Length == 0
            ? null
            : string.Join(Environment.NewLine, cleanErrors);
    }

    private static bool IsDuplicateKeyError(string? error)
    {
        return ContainsAny(
            error ?? string.Empty,
            "duplicate key",
            "duplicated key",
            "already contains an entry");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle =>
            value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CatLoadResult(
    RecommendationCatalogService? Cat,
    string? Error);

public sealed record PatchCatResult(
    Rpcs3PatchCatalog? Cat,
    string? Error);

public sealed record GamePlanRow(
    Rpcs3GameDescriptor Game,
    WikiGameRecommendation? Rec,
    ConfigPlan Plan);

public sealed record AnalyzeResult(
    Rpcs3InstallDescriptor Install,
    HwInfo Hw,
    IReadOnlyList<GamePlanRow> Games,
    PatchCatResult PatchCat,
    IReadOnlyList<BackupSnapshot> Backups);
