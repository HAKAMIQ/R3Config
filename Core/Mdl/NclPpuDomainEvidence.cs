namespace R3Config.Mdl;

public enum NclPpuDomainEvidenceCode
{
    Success,
    InvalidInput,
    InvalidRunKey,
    NotPpuRunKey,
    SerialScopeMismatch,
    LogTooLarge,
    ReadFailed,
    SessionNotFound,
    HashNotFound,
    InvalidSegment,
    NoLoadSegments,
    AmbiguousSegment
}

public sealed record NclPpuDomainEvidenceResult(
    bool IsOk,
    NclPpuDomainEvidenceCode Code,
    IReadOnlyList<NclAddressDomain> Domains,
    string? SourcePath,
    string? Error);