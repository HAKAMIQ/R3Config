using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config;

public sealed record CatLoad(
    RecSvc? Cat,
    string? Error);

public sealed record PatCatRs(
    PatCat? Cat,
    string? Error,
    IReadOnlyList<PatSrcSt>? Sources = null)
{
    public IReadOnlyList<PatSrcSt> SrcStat => Sources ?? [];
}

public sealed record PatSrcSt(
    string Name,
    PatSrc Source,
    string Path,
    bool Exists,
    bool Loaded,
    int EntryCount,
    string? Diagnostic = null);

public sealed record GameRow(
    R3Game Game,
    WikiRec? Rec,
    RecSum RecState,
    bool IsDetectedInRpcs3);

public sealed record AnlzRes(
    R3Inst Install,
    HwInfo Hw,
    IReadOnlyList<GameRow> Games,
    PatCatRs PatchCat,
    IReadOnlyList<BkpSnap> Backups,
    IReadOnlyList<ScanDiag> GameScanDiagnostics);
