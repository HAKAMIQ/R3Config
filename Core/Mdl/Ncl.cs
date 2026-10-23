namespace R3Config.Mdl;

public enum NclDialect
{
    NetCheat453,
    ArtemisPs3
}

public enum NclDocFormat
{
    ModernHash,
    LegacyBrace
}

public enum NclDiagSev
{
    Info,
    Warning,
    Error
}

public enum NclWriteMode
{
    Replace,
    Or,
    And,
    Xor
}

public enum NclOpKind
{
    DirectWrite,
    TextWrite,
    FloatWrite,
    CondensedWrite,
    PointerResolve,
    CopyBuffer,
    FindReplace,
    ConditionalEqual,
    ConditionalMaskUnset,
    CopyBytes,
    Unsupported
}

public enum NclVerifyStat
{
    OfflineStaticCandidate,
    RuntimeEvidenceRequired,
    ConstantRuntimeRequired,
    OptionSelectionRequired,
    NonExecutableMetadata,
    Unsupported,
    Invalid
}

public sealed record NclDiag(
    NclDiagSev Severity,
    string Code,
    string Message,
    int? Line = null);

public sealed record NclOption(
    string Value,
    string Label);

public sealed record NclOptionSet(
    string Raw,
    IReadOnlyList<NclOption> Values,
    int PlaceholderId = 1);

public abstract record NclOp(
    NclOpKind Kind,
    int Line,
    string Raw);

public sealed record NclDirectWrite(
    int Line,
    string Raw,
    ulong Address,
    IReadOnlyList<byte> Value,
    NclWriteMode Mode,
    uint RawModifier)
    : NclOp(NclOpKind.DirectWrite, Line, Raw);

public sealed record NclTextWrite(
    int Line,
    string Raw,
    ulong Address,
    string Text,
    IReadOnlyList<byte> EncodedBytes,
    bool WritesNullTerminator)
    : NclOp(NclOpKind.TextWrite, Line, Raw);

public sealed record NclFloatWrite(
    int Line,
    string Raw,
    ulong Address,
    float Value,
    IReadOnlyList<byte> BigEndianBytes)
    : NclOp(NclOpKind.FloatWrite, Line, Raw);

public sealed record NclCondensedWrite(
    int Line,
    string Raw,
    ulong Address,
    IReadOnlyList<byte> InitialValue,
    ulong AddressIncrement,
    ulong RepeatCount,
    uint ValueIncrement,
    string SecondRaw)
    : NclOp(NclOpKind.CondensedWrite, Line, Raw);

public sealed record NclPointerResolve(
    int Line,
    string Raw,
    ulong PointerAddress,
    ulong Offset,
    bool RewritesNextValue,
    uint RawModifier)
    : NclOp(NclOpKind.PointerResolve, Line, Raw);

public sealed record NclCopyBuffer(
    int Line,
    string Raw,
    ulong Address,
    ulong Count,
    bool IsPaste,
    uint RawModifier)
    : NclOp(NclOpKind.CopyBuffer, Line, Raw);

public sealed record NclFindReplace(
    int Line,
    string Raw,
    ulong Start,
    ulong Stop,
    ulong MaxMatches,
    IReadOnlyList<byte> Original,
    IReadOnlyList<byte> Replacement,
    string SecondRaw)
    : NclOp(NclOpKind.FindReplace, Line, Raw);

public sealed record NclConditionalEqual(
    int Line,
    string Raw,
    ulong Address,
    ulong ControlledLineCount,
    IReadOnlyList<byte> Expected)
    : NclOp(NclOpKind.ConditionalEqual, Line, Raw);

public sealed record NclConditionalMaskUnset(
    int Line,
    string Raw,
    ulong Address,
    ulong ControlledLineCount,
    IReadOnlyList<byte> Mask)
    : NclOp(NclOpKind.ConditionalMaskUnset, Line, Raw);

public sealed record NclCopyBytes(
    int Line,
    string Raw,
    ulong Source,
    ulong Destination,
    ulong Count)
    : NclOp(NclOpKind.CopyBytes, Line, Raw);

public sealed record NclUnsupportedOp(
    int Line,
    string Raw,
    string Reason)
    : NclOp(NclOpKind.Unsupported, Line, Raw);

public sealed record NclCheat(
    string Name,
    bool ConstantWrite,
    string? Author,
    IReadOnlyList<NclOp> Operations,
    IReadOnlyList<NclOptionSet> Options,
    IReadOnlyList<string> Notes,
    IReadOnlyList<NclDiag> Diags,
    NclDialect Dialect = NclDialect.NetCheat453)
{
    public bool HasErr => Diags.Any(item => item.Severity == NclDiagSev.Error);

    // Placeholder ids the executable code of this cheat actually references,
    // recorded by the parser from the raw segment body while it still has it.
    // Empty unless the cheat is waiting for an explicit option selection, so a
    // malformed or fully unreferenced option source exposes nothing.
    public IReadOnlyList<int> RequiredOptionPlaceholderIds { get; init; } = [];
}

public sealed record NclDoc(
    NclDocFormat Format,
    IReadOnlyList<NclCheat> Cheats,
    IReadOnlyList<NclDiag> Diags,
    string? SourcePath = null,
    NclDialect Dialect = NclDialect.NetCheat453)
{
    public bool HasErr =>
        Diags.Any(item => item.Severity == NclDiagSev.Error) ||
        Cheats.Any(item => item.HasErr);
}

public sealed record NclSourceTarget(
    string Serial,
    string Version,
    string? DiscVersion = null);

// Offsets refer to the original filename stem, never to reconstructed fields.
public sealed record NclIdentityEvidence(
    string Token,
    int Offset,
    int Length,
    string Kind);

public sealed record NclTargetBinding(
    string Serial,
    string? SourceVersion,
    string? AppVersion,
    IReadOnlyList<NclIdentityEvidence> Evidence,
    string? UnresolvedReason = null)
{
    public string? EffectiveVersion => UnresolvedReason is null
        ? AppVersion ?? SourceVersion
        : null;
}

public sealed record NclSourceIdentity(
    string FileName,
    string? Title,
    IReadOnlyList<string> Serials,
    IReadOnlyList<string> Versions)
{
    public IReadOnlyList<NclTargetBinding> TargetBindings { get; init; } = [];

    public string? UnresolvedReason { get; init; }

    // Compatibility projection for conversion consumers. Unresolved bindings
    // stay in TargetBindings for diagnostics but never authorize conversion.
    public IReadOnlyList<NclSourceTarget> Targets =>
        [.. TargetBindings.Where(binding => binding.EffectiveVersion is not null)
            .Select(binding => new NclSourceTarget(
                binding.Serial, binding.EffectiveVersion!, binding.SourceVersion))];

    public bool HasExactTarget => Targets.Count > 0;

    public bool DeclaresSerial(string serial)
    {
        return !string.IsNullOrWhiteSpace(serial) &&
               Serials.Any(candidate =>
                   candidate.Equals(
                       serial,
                       StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesTarget(
        string serial,
        string version)
    {
        return !string.IsNullOrWhiteSpace(serial) &&
               !string.IsNullOrWhiteSpace(version) &&
               Targets.Any(target =>
                   target.Serial.Equals(
                       serial,
                       StringComparison.OrdinalIgnoreCase) &&
                   target.Version.Equals(
                       version.Trim(),
                       StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> VersionsForSerial(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return [];
        }

        return
        [
            .. Targets
                .Where(target =>
                    target.Serial.Equals(
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                .Select(target => target.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}

public sealed record NclVerifyItem(
    string CheatName,
    NclVerifyStat Status,
    IReadOnlyList<NclOpKind> OperationKinds,
    IReadOnlyList<string> Reasons);

public sealed record NclVerifyResult(
    IReadOnlyList<NclVerifyItem> Items,
    int StaticCandidates,
    int RuntimeRequired,
    int ConstantRuntimeRequired,
    int OptionSelectionRequired,
    int NonExecutableMetadata,
    int Unsupported,
    int Invalid);
