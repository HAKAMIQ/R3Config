namespace R3Config.Mdl;

public enum NclRpcS3EmitCode
{
    Success,
    BindingRejected,
    InvalidMetadata,
    InvalidRunKey,
    MultipleRunKeys,
    InvalidWrite,
    InstructionLimit,
    ValidationFailed,
    RoundTripMismatch
}

public sealed record NclRpcS3EmitRequest(
    NclAddressBindResult Binding,
    string PatchTitle,
    string GameTitle,
    string Serial,
    string Version,
    string? Author = null,
    string? Notes = null);

public sealed record NclRpcS3EmitResult(
    bool IsOk,
    NclRpcS3EmitCode Code,
    string PatchText,
    string? RunKey,
    int InstructionCount,
    int VerifiedByteCount,
    string? Error);
