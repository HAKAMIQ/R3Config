namespace R3Config.Mdl;

public enum NclStaticLowerCode
{
    Success,
    NotStaticCandidate,
    UnsupportedOperation,
    UnverifiedValueIncrement,
    ExpansionLimit,
    AddressOverflow,
    NoConcreteWrites
}

public sealed record NclStaticWrite(
    ulong Address,
    IReadOnlyList<byte> Bytes,
    int SourceLine,
    NclOpKind SourceKind);

public sealed record NclStaticLowerResult(
    bool IsOk,
    NclStaticLowerCode Code,
    IReadOnlyList<NclStaticWrite> Writes,
    string? Error);