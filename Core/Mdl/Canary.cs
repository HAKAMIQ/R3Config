namespace R3Config.Mdl;

public enum CanaryDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum CanaryComplexity
{
    SimpleWrite,
    ConstantChange,
    SingleInstruction,
    MultiInstruction,
    BranchPatch,
    CodeCave,
    AnchorBased,
    MultiModule,
    Dynamic,
    Unsupported
}

public sealed record CanaryCatalogDiagnostic(
    CanaryDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? ModuleKey = null,
    string? PatchTitle = null);

public sealed record CanaryInstruction(
    string Kind,
    string Address,
    string Value,
    IReadOnlyList<string> ExtraArguments,
    string? AnchorName = null);

public sealed record CanaryConfigValue(
    string Name,
    string Type,
    string DefaultValue,
    IReadOnlyDictionary<string, string> AllowedValues);

public sealed record CanaryPatchDefinition(
    string Title,
    string NormalizedTitle,
    PatchHashKind ModuleKind,
    string ModuleHash,
    string? NormalizedModuleHash,
    IReadOnlyList<Rpcs3PatchGameTarget> GameTargets,
    string PatchVersion,
    string Author,
    string? Notes,
    string? Group,
    IReadOnlyList<string> AnchorDependencies,
    IReadOnlyList<CanaryConfigValue> ConfigurableValues,
    IReadOnlyList<CanaryInstruction> Instructions,
    string InstructionFingerprint,
    CanaryComplexity Complexity,
    bool IsAutomaticCandidate,
    string ClassificationReason,
    PatchSourceOrigin Source,
    string? SourcePath)
{
    public string ModuleKey => $"{ModuleKind.ToString().ToUpperInvariant()}-{ModuleHash}";

    public IReadOnlyList<string> Serials => GameTargets
        .Select(target => target.Serial)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> Versions => GameTargets
        .SelectMany(target => target.Versions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed record CanaryCatalog(
    string? Version,
    IReadOnlyList<CanaryPatchDefinition> Definitions,
    IReadOnlyList<CanaryCatalogDiagnostic> Diagnostics,
    int RejectedDefinitions,
    int AnchorCount)
{
    public bool HasErrors => Diagnostics.Any(item => item.Severity == CanaryDiagnosticSeverity.Error);
}

public sealed record CanaryCatalogResult(
    bool Succeeded,
    CanaryCatalog Catalog);

public sealed record CanaryPatchFamily(
    string FamilyId,
    string Title,
    string NormalizedTitle,
    IReadOnlyList<CanaryPatchDefinition> ReferenceEntries,
    IReadOnlyList<string> SupportedRegions,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> KnownHashes,
    IReadOnlyList<string> InstructionFingerprints,
    CanaryComplexity Complexity);


public enum CanaryPortStatus
{
    ExactReference,
    HashEvidenceMissing,
    VersionUnsupported,
    ConflictingReferences,
    ComplexManualReview,
    NotPortable
}

public sealed record CanaryPortAnalysis(
    string FamilyId,
    string Title,
    CanaryPortStatus Status,
    string TargetSerial,
    string? TargetVersion,
    PatchHashEvidenceKind EvidenceKind,
    string? EvidenceSource,
    IReadOnlyList<string> TargetHashes,
    IReadOnlyList<CanaryPatchDefinition> CandidateReferences,
    IReadOnlyList<CanaryPatchDefinition> ExactReferences,
    bool HasDeterministicEvidence,
    string Reason);

public sealed record CanaryCatalogReport(
    int PatchCount,
    IReadOnlyDictionary<PatchHashKind, int> ModuleCounts,
    int RejectedDefinitions,
    int FamilyCount,
    int SimilarFamilyCount,
    int SimplePatchCount,
    int ComplexPatchCount,
    IReadOnlyList<CanaryPatchFamily> Families,
    IReadOnlyList<CanaryCatalogDiagnostic> Diagnostics);

public enum HCanaryPatchType
{
    Ppu,
    Spu,
    LegacyCanary
}

public enum HCanaryDraftStatus
{
    Draft,
    Saved,
    Testing,
    Tested,
    Failed
}

public sealed record HCanaryHash(
    PatchHashKind Kind,
    string Value,
    string SourcePath,
    int FirstLine)
{
    public string Key => $"{Kind.ToString().ToUpperInvariant()}-{Value}";
}

public sealed record HCanaryHashReadResult(
    bool Succeeded,
    string SourcePath,
    IReadOnlyList<HCanaryHash> Hashes,
    string? Error = null);

public sealed class HCanaryInstruction
{
    public string Operation { get; set; } = "be32";
    public string Address { get; set; } = "0x00000000";
    public string Value { get; set; } = "0x60000000";

    public HCanaryInstruction Clone() => new()
    {
        Operation = Operation,
        Address = Address,
        Value = Value
    };
}

public sealed class HCanaryGameTarget
{
    public string GameName { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;

    public HCanaryGameTarget Clone() => new()
    {
        GameName = GameName,
        GameId = GameId,
        GameVersion = GameVersion
    };
}

public sealed class HCanaryAnchor
{
    public string Name { get; set; } = string.Empty;
    public List<HCanaryInstruction> Instructions { get; set; } = [];

    public HCanaryAnchor Clone() => new()
    {
        Name = Name,
        Instructions = Instructions.Select(item => item.Clone()).ToList()
    };
}

public sealed class HCanaryPatchDraft
{
    public HCanaryPatchType PatchType { get; set; } = HCanaryPatchType.Ppu;
    public bool IsExperimental { get; set; }
    public string ExecutableHash { get; set; } = string.Empty;
    public string PatchName { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string PatchVersion { get; set; } = "1.0";
    public HCanaryDraftStatus Status { get; set; } = HCanaryDraftStatus.Draft;
    public List<HCanaryGameTarget> Games { get; set; } = [];
    public List<HCanaryInstruction> Instructions { get; set; } = [];
    public List<HCanaryAnchor> Anchors { get; set; } = [];

    public HCanaryPatchDraft Clone() => new()
    {
        PatchType = PatchType,
        IsExperimental = IsExperimental,
        ExecutableHash = ExecutableHash,
        PatchName = PatchName,
        Author = Author,
        Notes = Notes,
        Group = Group,
        PatchVersion = PatchVersion,
        Status = Status,
        Games = Games.Select(item => item.Clone()).ToList(),
        Instructions = Instructions.Select(item => item.Clone()).ToList(),
        Anchors = Anchors.Select(item => item.Clone()).ToList()
    };
}

public sealed record HCanaryValidationIssue(
    string Code,
    string Field,
    string Message);

public sealed record HCanaryValidationResult(
    IReadOnlyList<HCanaryValidationIssue> Issues,
    string Yaml)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed record HCanaryStoredPatch(
    string Identity,
    string DisplayName,
    HCanaryPatchDraft Draft);

public sealed record HCanarySaveResult(
    string Path,
    BackupManifest Backup,
    int PatchCount,
    bool ReplacedExisting);
