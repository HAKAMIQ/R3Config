namespace R3Config.Mdl;

public enum HCanaryPatchType
{
    Ppu,
    Spu,
    Canary
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
