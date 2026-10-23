namespace R3Config.Mdl;

public enum NclPpuConversionCode
{
    Success,
    InvalidRequest,
    ParseRejected,
    CheatSelectionOutOfRange,
    VerificationMismatch,
    LoweringRejected,
    EvidenceRejected,
    BindingRejected,
    EmissionRejected
}

public sealed record NclPpuConversionTarget(
    string RunKey,
    string PatchTitle,
    string GameTitle,
    string Serial,
    string Version,
    string? Author = null,
    string? Notes = null);

public sealed record NclPpuConversionFileRequest(
    string NclPath,
    string RpcS3LogPath,
    NclDialect Dialect,
    int CheatIndex,
    NclPpuConversionTarget Target,
    string? ExpectedSourceSha256 = null,
    NclOptionSelection? OptionSelection = null);

public sealed record NclPpuConversionTextFileRequest(
    string NclText,
    string RpcS3LogPath,
    NclDialect Dialect,
    int CheatIndex,
    NclPpuConversionTarget Target,
    string? SourcePath = null,
    NclOptionSelection? OptionSelection = null);

public sealed record NclPpuConversionTextRequest(
    string NclText,
    string RpcS3LogText,
    NclDialect Dialect,
    int CheatIndex,
    NclPpuConversionTarget Target,
    string? SourcePath = null,
    string? LogSourcePath = null,
    NclOptionSelection? OptionSelection = null);

public sealed record NclPpuConversionResult(
    bool IsOk,
    NclPpuConversionCode Code,
    string PatchText,
    string? RunKey,
    int CheatIndex,
    string? CheatName,
    NclStaticLowerCode? LoweringCode,
    NclPpuDomainEvidenceCode? EvidenceCode,
    NclAddressBindCode? BindingCode,
    NclRpcS3EmitCode? EmissionCode,
    int InstructionCount,
    int VerifiedByteCount,
    string? Error);

public enum NclRunKeyEligibilityCode
{
    Success,
    InvalidRequest,
    ParseRejected,
    CheatSelectionOutOfRange,
    VerificationMismatch,
    LoweringRejected,
    CandidateLimit
}

// One candidate PPU executable measured against the concrete writes the
// selected cheat/option instance actually produces. The verdict comes from the
// same address-domain evidence and address binder the conversion itself uses,
// never from the title, the serial alone, or the order of the list.
public sealed record NclRunKeyCandidate(
    string RunKey,
    bool IsEligible,
    NclPpuDomainEvidenceCode EvidenceCode,
    NclAddressBindCode? BindingCode,
    string? Error);

public sealed record NclRunKeyEligibilityResult(
    bool IsOk,
    NclRunKeyEligibilityCode Code,
    IReadOnlyList<NclRunKeyCandidate> Candidates,
    NclStaticLowerCode? LoweringCode,
    string? Error)
{
    public IReadOnlyList<string> EligibleRunKeys =>
        [.. Candidates
            .Where(item => item.IsEligible)
            .Select(item => item.RunKey)];
}
