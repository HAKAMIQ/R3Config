namespace R3Config.Mdl;

public enum NclAddressBindCode
{
    Success,
    LoweringRejected,
    NoDomains,
    InvalidDomain,
    InvalidWrite,
    UnmappedWrite,
    CrossesDomainBoundary,
    AmbiguousDomain,
    PatchAddressOverflow
}

public sealed record NclAddressDomain(
    string RunKey,
    ulong GuestStart,
    ulong Length,
    uint PatchAddressStart);

public sealed record NclBoundWrite(
    string RunKey,
    uint PatchAddress,
    IReadOnlyList<byte> Bytes,
    ulong SourceAddress,
    int SourceLine,
    NclOpKind SourceKind);

public sealed record NclAddressBindResult(
    bool IsOk,
    NclAddressBindCode Code,
    IReadOnlyList<NclBoundWrite> Writes,
    string? Error);
