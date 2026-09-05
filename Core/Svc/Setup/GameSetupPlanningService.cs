using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed class GameSetupPlanningService(
    GameIdentityService? identityService = null,
    PatchCompatibilityService? compatibilityService = null,
    CanaryRuntimeBindingService? canaryRuntimeBindingService = null)
{
    private static readonly string[] PatchLaunchWarnings =
    [
        "PATCH_BOOT_RECOMMENDED: start patched games with Boot Game; save-state startup may not reapply patches."
    ];

    private readonly GameIdentityService _identity =
        identityService ?? new GameIdentityService();
    private readonly PatchMatchService _patchMatch =
        new(compatibilityService, canaryRuntimeBindingService);

    public GameSetupPlan Build(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        CatInfo recommendationProvenance,
        CfgPlan baseConfiguration,
        PatCat patchCatalog,
        IReadOnlyList<PatchRequirementRule>? requirementRules = null)
    {
        ArgumentNullException.ThrowIfNull(recommendationProvenance);
        ArgumentNullException.ThrowIfNull(baseConfiguration);
        ArgumentNullException.ThrowIfNull(patchCatalog);

        var identity = _identity.Resolve(install, game);
        if (!Path.GetFullPath(baseConfiguration.CfgPath).Equals(
                identity.CustomConfigPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The base configuration plan does not target the selected game identity.");
        }

        var rules = (requirementRules ?? [])
            .GroupBy(rule => rule.PatchIdentity, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PatchRequirement>)
                    group.SelectMany(rule => rule.Requirements).ToArray(),
                StringComparer.Ordinal);
        var allEntries = patchCatalog.Entries.ToArray();
        var candidates = _patchMatch
            .Match(patchCatalog, game)
            .Where(match => match.Status is not MatStat.SerBad and not MatStat.NoMeta)
            .Where(match => string.Equals(
                match.MatSer,
                identity.Serial,
                StringComparison.OrdinalIgnoreCase))
            .Select(match => Candidate(match, allEntries, rules))
            .OrderBy(candidate => candidate.Match.Patch.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Identity, StringComparer.Ordinal)
            .ToArray();

        var warnings = new List<string>();
        if (identity.Version is null)
        {
            warnings.Add("VERSION_UNRESOLVED: installed game version is unavailable.");
        }

        if (identity.PpuHashes.Count == 0 && identity.SpuHashes.Count == 0)
        {
            warnings.Add(
                "HASH_UNKNOWN: launch the game once in RPCS3 to generate executable hash evidence when a patch requires it.");
        }

        warnings.AddRange(candidates
            .Where(candidate => candidate.Compatibility is
                SetupCompatibilityStatus.VersionUnresolved or
                SetupCompatibilityStatus.HashRequired or
                SetupCompatibilityStatus.HashMismatch)
            .Where(ShouldProjectCompatibilityWarning)
            .Select(candidate =>
                $"{candidate.Compatibility.ToString().ToUpperInvariant()}: {candidate.Match.Patch.Title}"));
        return new GameSetupPlan(
            identity,
            recommendationProvenance,
            baseConfiguration,
            Path.GetFullPath(install.PatCfg),
            Path.GetFullPath(Path.Combine(install.PatPath, "imported_patch.yml")),
            Path.GetFullPath(Path.Combine(install.PatPath, "patch.yml")),
            candidates.Where(candidate => candidate.Provenance.Kind is
                    SetupPatchSourceKind.OfficialMain or
                    SetupPatchSourceKind.OfficialSpu)
                .ToArray(),
            candidates.Where(candidate =>
                    candidate.Provenance.Kind == SetupPatchSourceKind.Canary)
                .ToArray(),
            candidates.Where(candidate =>
                    candidate.Provenance.Kind == SetupPatchSourceKind.Artemis)
                .ToArray(),
            candidates.Where(candidate =>
                    candidate.Provenance.Kind == SetupPatchSourceKind.Custom)
                .ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    public GameSetupPreview Preview(
        GameSetupPlan plan,
        GameSetupSelection selection)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selection);

        var conflicts = new List<string>();
        var baseSettings = ResolveBaseSettings(plan, selection, conflicts);
        var selectedPatches = ResolvePatches(plan, selection, conflicts);

        foreach (var candidate in selectedPatches.Where(candidate => !candidate.CanSelect))
        {
            conflicts.Add(
                $"Patch '{candidate.Match.Patch.Title}' is not selectable: {candidate.Compatibility}.");
        }

        ValidateGroups(
            selectedPatches,
            plan.Patches,
            conflicts);
        ValidateDependencies(
            selectedPatches,
            plan.Patches,
            conflicts);
        ValidateRuntimeIdentities(
            selectedPatches,
            plan.Patches,
            conflicts);

        var requirements = selectedPatches
            .SelectMany(candidate => candidate.RequiredSettings)
            .ToArray();
        var effectiveSettings = MergeSettings(
            baseSettings,
            requirements,
            conflicts);

        var files = new List<SetupFileChange>();
        if (effectiveSettings.Length > 0)
        {
            files.Add(new SetupFileChange(
                SetupFileKind.CustomConfig,
                plan.Game.CustomConfigPath,
                "Selected base configuration and patch requirements."));
        }

        if (selectedPatches.Count > 0)
        {
            files.Add(new SetupFileChange(
                SetupFileKind.PatchEnablement,
                plan.PatchConfigPath,
                "Selected patch enablement state."));
        }

        if (selectedPatches.Any(candidate =>
                candidate.Match.Patch.Source == PatSrc.Can))
        {
            files.Add(new SetupFileChange(
                SetupFileKind.ImportedPatch,
                plan.ImportedPatchPath,
                "Selected Canary definitions are installed separately from enablement."));
        }

        if (selectedPatches.Any(candidate =>
                candidate.Match.Patch.Source == PatSrc.Artemis))
        {
            files.Add(new SetupFileChange(
                SetupFileKind.ArtemisTitlePatch,
                Path.Combine(
                    Path.GetDirectoryName(plan.ImportedPatchPath)!,
                    plan.Game.Serial + "_patch.yml"),
                "Selected Artemis RPCS3 patch definitions are installed into the managed title-specific patch file."));
        }

        var warnings = plan.Warnings
            .Concat(selectedPatches.Count > 0
                ? PatchLaunchWarnings
                : Array.Empty<string>())
            .Concat(selectedPatches
                .Select(SafetyWarning)
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Select(warning => warning!))
            .Concat(selectedPatches
                .Select(candidate => candidate.Match.Patch.Notes)
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Select(note => note!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new GameSetupPreview(
            plan,
            baseSettings,
            selectedPatches,
            requirements,
            effectiveSettings,
            files,
            warnings,
            conflicts.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string? SafetyWarning(SetupPatchCandidate candidate)
    {
        var safety = PatSafe.Eval(candidate.Match.Patch.Notes);
        return safety.Severity switch
        {
            PatSafetySeverity.High =>
                $"PATCH_SAFETY_HIGH: {candidate.Match.Patch.Title}",
            PatSafetySeverity.Caution =>
                $"PATCH_SAFETY_CAUTION: {candidate.Match.Patch.Title}",
            _ => null
        };
    }

    public static string PatchIdentity(Rpcs3PatchEntry patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var targets = patch.GameTgt
            .Select(target =>
                $"{target.Title.Trim()}|{target.Serial.Trim().ToUpperInvariant()}|" +
                string.Join(",", target.Versions
                    .Select(version => PatVer.Norm(version) ?? string.Empty)
                    .Order(StringComparer.OrdinalIgnoreCase)))
            .Order(StringComparer.OrdinalIgnoreCase);
        var canonical = string.Join("\n",
            patch.Class,
            patch.HashKind,
            patch.RunKey.ToUpperInvariant(),
            patch.Title.Trim(),
            patch.PatVer?.Trim() ?? string.Empty,
            patch.Group?.Trim() ?? string.Empty,
            patch.DefPrint ?? string.Empty,
            string.Join(";", targets));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static void ValidateRuntimeIdentities(
        IReadOnlyList<SetupPatchCandidate> selected,
        IReadOnlyList<SetupPatchCandidate> all,
        List<string> conflicts)
    {
        var selectedIds = selected
            .Select(candidate => candidate.Identity)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in selected)
        {
            var patch = candidate.Match.Patch;
            var channel = Channel(patch);
            var collision = all.FirstOrDefault(other =>
                other.Identity != candidate.Identity &&
                other.Match.EffectiveRunKey.Equals(
                    candidate.Match.EffectiveRunKey,
                    StringComparison.OrdinalIgnoreCase) &&
                other.Match.Patch.Title.Equals(
                    patch.Title,
                    StringComparison.OrdinalIgnoreCase) &&
                Channel(other.Match.Patch) != channel &&
                (selectedIds.Contains(other.Identity) ||
                 IsResident(other.Match.Patch) &&
                 CompareVersion(patch.PatVer, other.Match.Patch.PatVer) <= 0) &&
                !string.Equals(
                    other.Match.Patch.DefPrint,
                    patch.DefPrint,
                    StringComparison.OrdinalIgnoreCase));

            if (collision is not null)
            {
                conflicts.Add(
                    $"Patch '{patch.Title}' has an RPCS3 runtime identity collision between {patch.Source} and {collision.Match.Patch.Source}; the executable patch bodies differ.");
            }
        }
    }

    private static bool IsResident(Rpcs3PatchEntry patch)
    {
        return patch.Source is not PatSrc.Can and not PatSrc.Artemis;
    }

    private static int Channel(Rpcs3PatchEntry patch)
    {
        if (patch.Source == PatSrc.Official)
        {
            return 0;
        }

        if (patch.Class == PatClass.Canary)
        {
            return 1;
        }

        if (patch.Source is PatSrc.Artemis or PatSrc.ManagedArtemis)
        {
            return 2;
        }

        return 3;
    }

    public static SetupDuplicateStatus ClassifyDuplicate(
        Rpcs3PatchEntry incoming,
        IEnumerable<Rpcs3PatchEntry> installed)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(installed);
        var entries = installed.ToArray();
        var sameTitle = entries.Where(entry => entry.Title.Equals(
                incoming.Title,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var sameRunKey = entries.Where(entry => entry.RunKey.Equals(
                incoming.RunKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (sameTitle.Any(entry => PatchIdentity(entry).Equals(
                PatchIdentity(incoming),
                StringComparison.Ordinal)))
        {
            return SetupDuplicateStatus.ExactDuplicate;
        }

        var direct = sameTitle.FirstOrDefault(entry => entry.RunKey.Equals(
            incoming.RunKey,
            StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            var comparison = CompareVersion(incoming.PatVer, direct.PatVer);
            if (comparison > 0)
            {
                return SetupDuplicateStatus.NewerVersion;
            }

            if (comparison < 0)
            {
                return SetupDuplicateStatus.OlderVersion;
            }

            return SetupDuplicateStatus.ConflictingPatch;
        }

        if (sameTitle.Length > 0)
        {
            return SetupDuplicateStatus.SameNameDifferentHash;
        }

        if (sameRunKey.Any(entry => !string.Equals(
                entry.PatVer,
                incoming.PatVer,
                StringComparison.OrdinalIgnoreCase)))
        {
            return SetupDuplicateStatus.SameHashDifferentVersion;
        }

        return SetupDuplicateStatus.None;
    }

    private static SetupPatchCandidate Candidate(
        PatMatch match,
        IReadOnlyList<Rpcs3PatchEntry> catalog,
        Dictionary<string, IReadOnlyList<PatchRequirement>> rules)
    {
        var identity = PatchIdentity(match.Patch);
        var requirementIdentity = RequirementIdentity(
            match,
            catalog,
            rules,
            identity);
        var localEntries = catalog.Where(entry =>
            entry.Source == PatSrc.Imported &&
            !ReferenceEquals(entry, match.Patch));
        rules.TryGetValue(requirementIdentity, out var requirements);
        return new SetupPatchCandidate(
            identity,
            match,
            Provenance(match.Patch),
            Compatibility(match),
            requirements ?? [],
            match.Patch.Source == PatSrc.Can
                ? ClassifyDuplicate(match.Patch, localEntries)
                : SetupDuplicateStatus.None,
            DefaultSelected: false);
    }

    private static string RequirementIdentity(
        PatMatch match,
        IReadOnlyList<Rpcs3PatchEntry> catalog,
        Dictionary<string, IReadOnlyList<PatchRequirement>> rules,
        string runtimeIdentity)
    {
        if (rules.ContainsKey(runtimeIdentity) ||
            match.Patch.Class != PatClass.Canary)
        {
            return runtimeIdentity;
        }

        var sourceIdentities = catalog
            .Where(entry => SameDefinitionExceptTargets(
                entry,
                match.Patch))
            .Select(PatchIdentity)
            .Distinct(StringComparer.Ordinal)
            .Where(rules.ContainsKey)
            .ToArray();

        return sourceIdentities.Length == 1
            ? sourceIdentities[0]
            : runtimeIdentity;
    }

    private static bool SameDefinitionExceptTargets(
        Rpcs3PatchEntry left,
        Rpcs3PatchEntry right)
    {
        return left.Class == right.Class &&
               left.HashKind == right.HashKind &&
               left.RunKey.Equals(
                   right.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               left.Title.Equals(
                   right.Title,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.PatVer,
                   right.PatVer,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.Group,
                   right.Group,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.DefPrint,
                   right.DefPrint,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static SetupPatchProvenance Provenance(Rpcs3PatchEntry patch)
    {
        var kind = patch.Source == PatSrc.Official
            ? patch.HashKind == HashKind.Spu
                ? SetupPatchSourceKind.OfficialSpu
                : SetupPatchSourceKind.OfficialMain
            : patch.Class == PatClass.Canary || patch.Source == PatSrc.Can
                ? SetupPatchSourceKind.Canary
                : patch.Source is PatSrc.Artemis or PatSrc.ManagedArtemis
                    ? SetupPatchSourceKind.Artemis
                : SetupPatchSourceKind.Custom;
        DateTimeOffset? timestamp = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(patch.SrcPath) && File.Exists(patch.SrcPath))
            {
                timestamp = File.GetLastWriteTimeUtc(patch.SrcPath);
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            timestamp = null;
        }

        return new SetupPatchProvenance(
            kind,
            $"{patch.Source}:{patch.SrcPath ?? "resident"}",
            patch.SrcPath,
            timestamp);
    }

    private static SetupCompatibilityStatus Compatibility(PatMatch match)
    {
        return match.Status switch
        {
            MatStat.Match => SetupCompatibilityStatus.ExactMatch,
            MatStat.VerUnk => SetupCompatibilityStatus.VersionUnresolved,
            MatStat.VerBad => SetupCompatibilityStatus.VersionMismatch,
            MatStat.HashBad => SetupCompatibilityStatus.HashMismatch,
            MatStat.SerBad => SetupCompatibilityStatus.SerialMismatch,
            MatStat.Unknown when match.Cmp?.CanOn == true =>
                SetupCompatibilityStatus.HashUnknown,
            MatStat.Unknown => SetupCompatibilityStatus.HashRequired,
            _ => SetupCompatibilityStatus.Unsupported
        };
    }

    private static bool ShouldProjectCompatibilityWarning(SetupPatchCandidate candidate)
    {
        return candidate.Compatibility != SetupCompatibilityStatus.HashRequired ||
               !PatVer.IsAll(candidate.Match.TgtSer);
    }

    private static CfgChg[] ResolveBaseSettings(
        GameSetupPlan plan,
        GameSetupSelection selection,
        List<string> conflicts)
    {
        var available = plan.BaseConfiguration.Items
            .Where(item => item.IsAction)
            .Select(item => item.Change)
            .ToArray();
        var output = new List<CfgChg>();
        foreach (var selected in selection.BaseSettings)
        {
            var match = available.FirstOrDefault(candidate => SameSetting(
                candidate,
                selected));
            if (match is null)
            {
                conflicts.Add(
                    $"Selected base setting '{selected.Path}' is not actionable in this plan.");
                continue;
            }

            output.Add(match);
        }

        return output.DistinctBy(
            setting => $"{setting.Section}\0{setting.Key}",
            StringComparer.Ordinal).ToArray();
    }

    private static List<SetupPatchCandidate> ResolvePatches(
        GameSetupPlan plan,
        GameSetupSelection selection,
        List<string> conflicts)
    {
        var byIdentity = plan.Patches.ToDictionary(
            candidate => candidate.Identity,
            StringComparer.Ordinal);
        var output = new List<SetupPatchCandidate>();
        foreach (var identity in selection.PatchIdentities.Distinct(StringComparer.Ordinal))
        {
            if (!byIdentity.TryGetValue(identity, out var candidate))
            {
                conflicts.Add($"Selected patch identity '{identity}' is not in this setup plan.");
                continue;
            }

            output.Add(candidate);
        }

        return output;
    }

    private static void ValidateGroups(
        IReadOnlyList<SetupPatchCandidate> selected,
        IReadOnlyList<SetupPatchCandidate> available,
        List<string> conflicts)
    {
        var active = selected
            .Concat(available.Where(candidate =>
                candidate.IsEnabled &&
                selected.All(item => !item.Identity.Equals(
                    candidate.Identity,
                    StringComparison.Ordinal))))
            .ToArray();
        foreach (var group in active
                     .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Match.Patch.Group))
                     .GroupBy(candidate => candidate.Match.Patch.Group!.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Select(candidate => candidate.Match.Patch.Title)
                         .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            conflicts.Add(
                $"Patch group '{group.Key}' is mutually exclusive: " +
                string.Join(", ", group.Select(candidate => candidate.Match.Patch.Title)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<SetupPatchCandidate> selected,
        IReadOnlyList<SetupPatchCandidate> available,
        List<string> conflicts)
    {
        var active = selected
            .Concat(available.Where(candidate => candidate.IsEnabled))
            .DistinctBy(candidate => candidate.Identity, StringComparer.Ordinal)
            .ToArray();

        try
        {
            PatDepVal.ValidateEnable(
                selected.Select(candidate => candidate.Match).ToArray(),
                active.Select(candidate => candidate.Match).ToArray());
        }
        catch (PatchDependencyException ex)
        {
            conflicts.Add(ex.Message);
        }
    }

    private static CfgChg[] MergeSettings(
        IReadOnlyList<CfgChg> baseSettings,
        IReadOnlyList<PatchRequirement> requirements,
        List<string> conflicts)
    {
        var output = new Dictionary<string, CfgChg>(StringComparer.Ordinal);
        foreach (var setting in baseSettings.Concat(requirements.Select(item => item.Setting)))
        {
            var key = $"{setting.Section}\0{setting.Key}";
            if (output.TryGetValue(key, out var existing) &&
                !existing.Value.Equals(setting.Value, StringComparison.Ordinal))
            {
                conflicts.Add(
                    $"Configuration conflict for '{setting.Path}': '{existing.Value}' versus '{setting.Value}'.");
                continue;
            }

            output[key] = setting;
        }

        return output.Values
            .OrderBy(setting => setting.Section, StringComparer.Ordinal)
            .ThenBy(setting => setting.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SameSetting(CfgChg left, CfgChg right)
    {
        return left.Section.Equals(right.Section, StringComparison.Ordinal) &&
               left.Key.Equals(right.Key, StringComparison.Ordinal) &&
               left.Value.Equals(right.Value, StringComparison.Ordinal);
    }

    private static int CompareVersion(string? left, string? right)
    {
        if (Version.TryParse(left, out var leftVersion) &&
            Version.TryParse(right, out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? 0
            : 0;
    }
}

#pragma warning restore CA1822
