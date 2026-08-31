namespace R3Config.Mdl;

public enum SetupIdentityEvidence
{
    InstalledMetadata,
    RuntimeLog,
    RuntimeCache,
    Unavailable
}

public enum SetupPatchSourceKind
{
    OfficialMain,
    OfficialSpu,
    Canary,
    Artemis,
    Custom
}

public enum SetupCompatibilityStatus
{
    ExactMatch,
    VersionUnresolved,
    VersionMismatch,
    HashRequired,
    HashUnknown,
    HashMismatch,
    SerialMismatch,
    Unsupported
}

public enum SetupDuplicateStatus
{
    None,
    ExactDuplicate,
    OlderVersion,
    NewerVersion,
    ConflictingPatch,
    SameNameDifferentHash,
    SameHashDifferentVersion
}

public enum SetupFileKind
{
    CustomConfig,
    PatchEnablement,
    ImportedPatch,
    ArtemisTitlePatch
}

public sealed record GameIdentity(
    string Title,
    string Serial,
    string? Version,
    string InstallPath,
    string CustomConfigPath,
    IReadOnlyList<string> PpuHashes,
    IReadOnlyList<string> SpuHashes,
    IReadOnlyList<string> PrxHashes,
    IReadOnlyList<string> OvlHashes,
    SetupIdentityEvidence VersionEvidence,
    SetupIdentityEvidence HashEvidence);

public sealed record SetupPatchProvenance(
    SetupPatchSourceKind Kind,
    string Identity,
    string? SourcePath,
    DateTimeOffset? SourceTimestampUtc);

public sealed record PatchRequirement(
    CfgChg Setting,
    string Evidence);

public sealed record PatchRequirementRule(
    string PatchIdentity,
    IReadOnlyList<PatchRequirement> Requirements,
    string? PatchTitle = null,
    string? Serial = null,
    string? Version = null,
    string? Hash = null,
    PatSrc? Source = null,
    string? Notes = null,
    string? SourceIdentity = null,
    string EvidenceStatus = "PROVEN",
    string? EvidenceSource = null,
    string? EvidenceRevision = null);

public sealed record PatchRequirementCatalog(
    string SchemaVersion,
    string SourceIdentity,
    string SnapshotDate,
    string ContentSha256,
    IReadOnlyList<PatchRequirementRule> Rules);

public sealed record SetupPatchCandidate(
    string Identity,
    PatMatch Match,
    SetupPatchProvenance Provenance,
    SetupCompatibilityStatus Compatibility,
    IReadOnlyList<PatchRequirement> RequiredSettings,
    SetupDuplicateStatus DuplicateStatus,
    bool DefaultSelected = false,
    bool IsEnabled = false)
{
    public bool CanSelect =>
        Match.Cmp?.CanOn == true &&
        (Compatibility is SetupCompatibilityStatus.ExactMatch or
            SetupCompatibilityStatus.HashUnknown) &&
        (DuplicateStatus is not SetupDuplicateStatus.OlderVersion and
            not SetupDuplicateStatus.ConflictingPatch and
            not SetupDuplicateStatus.SameNameDifferentHash and
            not SetupDuplicateStatus.SameHashDifferentVersion);
}

public sealed record GameSetupPlan(
    GameIdentity Game,
    CatInfo RecommendationProvenance,
    CfgPlan BaseConfiguration,
    string PatchConfigPath,
    string ImportedPatchPath,
    string OfficialPatchPath,
    IReadOnlyList<SetupPatchCandidate> OfficialPatches,
    IReadOnlyList<SetupPatchCandidate> CanaryPatches,
    IReadOnlyList<SetupPatchCandidate> ArtemisPatches,
    IReadOnlyList<SetupPatchCandidate> CustomPatches,
    IReadOnlyList<string> Warnings,
    bool? ExpectedCustomConfigExists = null,
    string? ExpectedCustomConfigSha256 = null)
{
    public IReadOnlyList<SetupPatchCandidate> Patches =>
    [
        .. OfficialPatches,
        .. CanaryPatches,
        .. ArtemisPatches,
        .. CustomPatches
    ];
}

public sealed record GameSetupSelection(
    IReadOnlyList<CfgChg> BaseSettings,
    IReadOnlyList<string> PatchIdentities);

public sealed record SetupFileChange(
    SetupFileKind Kind,
    string Path,
    string Reason);

public sealed record GameSetupPreview(
    GameSetupPlan Plan,
    IReadOnlyList<CfgChg> SelectedBaseSettings,
    IReadOnlyList<SetupPatchCandidate> SelectedPatches,
    IReadOnlyList<PatchRequirement> PatchRequirements,
    IReadOnlyList<CfgChg> EffectiveSettings,
    IReadOnlyList<SetupFileChange> Files,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Conflicts,
    bool? ExpectedPreparedCustomConfigExists = null,
    string? ExpectedPreparedCustomConfigSha256 = null)
{
    public bool CanApply =>
        Conflicts.Count == 0 &&
        (EffectiveSettings.Count > 0 || SelectedPatches.Count > 0);
}

public sealed record GameSetupResult(
    bool IsGameReady,
    bool AlreadyPrepared,
    int AppliedGameSettings,
    int EnabledOfficialPatches,
    int InstalledCanaryPatches,
    int EnabledCanaryPatches,
    int InstalledArtemisPatches,
    int EnabledArtemisPatches,
    IReadOnlyList<string> Warnings,
    BackupManifest? Backup);
