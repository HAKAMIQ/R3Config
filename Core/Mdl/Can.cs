namespace R3Config.Mdl;

public enum CanSev
{
    Info,
    Warning,
    Error
}

public enum CanCmp
{
    Write,
    Change,
    Ins,
    MultiIns,
    Pat,
    CodeCave,
    AncBased,
    Module,
    Dynamic,
    NoSupp
}

public sealed record CanDiag(
    CanSev Severity,
    string Code,
    string Message,
    string? ModKey = null,
    string? PatTitle = null);

public sealed record CanInst(
    string Kind,
    string Address,
    string Value,
    IReadOnlyList<string> ExtraArg,
    string? AncName = null);

public sealed record CanVal(
    string Name,
    string Type,
    string DefVal,
    IReadOnlyDictionary<string, string> AllowVal);

public sealed record CanDef(
    string Title,
    string NormName,
    HashKind ModKind,
    string ModHash,
    string? NormHash,
    IReadOnlyList<R3PatTgt> GameTgt,
    string PatVer,
    string Author,
    string? Notes,
    string? Group,
    IReadOnlyList<string> AncDeps,
    IReadOnlyList<CanVal> CfgVals,
    IReadOnlyList<CanInst> InsList,
    string InsPrint,
    CanCmp Cpx,
    bool IsAuto,
    string ClassWhy,
    PatSrc Source,
    string? SrcPath)
{
    public string Key => $"{ModKind.ToString().ToUpperInvariant()}-{ModHash}";

    public IReadOnlyList<string> Serials => GameTgt
        .Select(target => target.Serial)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> Versions => GameTgt
        .SelectMany(target => target.Versions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed record CanCat(
    string? Version,
    IReadOnlyList<CanDef> DefList,
    IReadOnlyList<CanDiag> Diags,
    int BadDefs,
    int AncNum)
{
    public bool HasErr => Diags.Any(item => item.Severity == CanSev.Error);
}

public sealed record CanCatRs(
    bool IsOk,
    CanCat Catalog);

public sealed record CanFam(
    string FamilyId,
    string Title,
    string NormName,
    IReadOnlyList<CanDef> RefList,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Vers,
    IReadOnlyList<string> Hashes,
    IReadOnlyList<string> Prints,
    CanCmp Cpx);


public enum PortStat
{
    ExactRef,
    Missing,
    NoSupp,
    ClashRef,
    Review,
    NoPort
}

public sealed record PortAnlz(
    string FamilyId,
    string Title,
    PortStat Status,
    string TgtSer,
    string? TgtVer,
    HashEvi EviKind,
    string? EviSrc,
    IReadOnlyList<string> TgtHash,
    IReadOnlyList<CanDef> CandRefs,
    IReadOnlyList<CanDef> ExactRef,
    bool HasProof,
    string Reason);

public sealed record CanRpt(
    int PatNum,
    IReadOnlyDictionary<HashKind, int> ModNums,
    int BadDefs,
    int FamNum,
    int SimFam,
    int SimpleNo,
    int CpxNum,
    IReadOnlyList<CanFam> Families,
    IReadOnlyList<CanDiag> Diags);

public enum HCanType
{
    Ppu,
    Spu,
    Can
}

public enum DraftSt
{
    Draft,
    Saved,
    Testing,
    Tested,
    Failed
}

public sealed record HCanHash(
    HashKind Kind,
    string Value,
    string SrcPath,
    int Line)
{
    public string Key => $"{Kind.ToString().ToUpperInvariant()}-{Value}";
}

public enum HashReadCode
{
    Found,
    NoLog,
    NoMatch,
    Error,
    Rpcs3Running,
    SelectedGameRunning,
    LogNotChanged,
    EmptyLog,
    WrongGame,
    WrongVersion,
    SelectedGameNotFound,
    NoExecutableHashes,
    CanaryHashMismatch
}

public sealed record CanaryHashExpectation(
    IReadOnlyList<string> PpuHashes,
    IReadOnlyList<string> SpuHashes)
{
    public static CanaryHashExpectation Empty { get; } =
        new([], []);

    public bool HasTargets =>
        PpuHashes.Count > 0 ||
        SpuHashes.Count > 0;
}

public sealed record HashRead(
    bool IsOk,
    string SrcPath,
    IReadOnlyList<HCanHash> Hashes,
    string? Error = null,
    HashReadCode Code = HashReadCode.Found,
    string? Serial = null,
    string? Version = null,
    DateTime? DetectedUtc = null,
    bool IsSaved = false);

public sealed record HCanLogMark(
    bool Exists,
    long Length,
    DateTime? LastWriteTimeUtc,
    string Fingerprint);

public sealed record HCanDiscoverySession(
    string Rpcs3ExecutablePath,
    string Serial,
    string? GameVersion,
    string GameInstallPath,
    int ProcessId,
    DateTime StartedUtc,
    string LogPath,
    HCanLogMark Baseline);

public sealed record HCanHashBinding(
    string Rpcs3ExecutablePath,
    string Serial,
    string? GameVersion,
    string GameInstallPath,
    IReadOnlyList<string> DetectedPpuHashes,
    IReadOnlyList<string> DetectedSpuHashes,
    DateTime DetectedUtc,
    DateTime DiscoverySessionStartUtc,
    DateTime LogLastWriteTimeUtc,
    string LogFingerprint,
    string LogPath);

public sealed class HCanInst
{
    public string Op { get; set; } = "be32";
    public string Address { get; set; } = "0x00000000";
    public string Value { get; set; } = "0x60000000";

    public HCanInst Clone() => new()
    {
        Op = Op,
        Address = Address,
        Value = Value
    };
}

public sealed class HCanTgt
{
    public string GameName { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string GameVer { get; set; } = string.Empty;

    public HCanTgt Clone() => new()
    {
        GameName = GameName,
        GameId = GameId,
        GameVer = GameVer
    };
}

public sealed class HCanAnc
{
    public string Name { get; set; } = string.Empty;
    public List<HCanInst> Ins { get; set; } = [];

    public HCanAnc Clone() => new()
    {
        Name = Name,
        Ins = Ins.Select(item => item.Clone()).ToList()
    };
}

public sealed class HCanDrft
{
    public HCanType PatType { get; set; } = HCanType.Ppu;
    public bool IsExp { get; set; }
    public string ExeHash { get; set; } = string.Empty;
    public string PatName { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string PatVer { get; set; } = "1.0";
    public DraftSt Status { get; set; } = DraftSt.Draft;
    public List<HCanTgt> Games { get; set; } = [];
    public List<HCanInst> Ins { get; set; } = [];
    public List<HCanAnc> Anchors { get; set; } = [];

    public HCanDrft Clone() => new()
    {
        PatType = PatType,
        IsExp = IsExp,
        ExeHash = ExeHash,
        PatName = PatName,
        Author = Author,
        Notes = Notes,
        Group = Group,
        PatVer = PatVer,
        Status = Status,
        Games = Games.Select(item => item.Clone()).ToList(),
        Ins = Ins.Select(item => item.Clone()).ToList(),
        Anchors = Anchors.Select(item => item.Clone()).ToList()
    };
}

public sealed record VldIssue(
    string Code,
    string Field,
    string Message);

public sealed record VldRes(
    IReadOnlyList<VldIssue> Issues,
    string Yaml)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed record HCanStor(
    string Identity,
    string Name,
    HCanDrft Draft);

public sealed record SaveRes(
    string Path,
    BackupManifest Backup,
    int PatNum,
    bool Replaced);
