using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclStaticLowerer
{
    private const int MaxExpandedWrites = 65_536;
    private const long MaxExpandedBytes = 16L * 1024L * 1024L;

    public static NclStaticLowerResult Lower(NclCheat cheat)
    {
        ArgumentNullException.ThrowIfNull(cheat);

        if (cheat.HasErr)
        {
            return Failure(
                NclStaticLowerCode.NotStaticCandidate,
                "The NCL cheat contains parser errors.");
        }

        if (cheat.Options.Count > 0)
        {
            return Failure(
                NclStaticLowerCode.NotStaticCandidate,
                "NCL option placeholders must be resolved before static lowering.");
        }

        if (cheat.ConstantWrite)
        {
            return Failure(
                NclStaticLowerCode.NotStaticCandidate,
                "Constant-write NCL semantics require runtime execution.");
        }

        if (cheat.Operations.Count == 0)
        {
            return Failure(
                NclStaticLowerCode.NoConcreteWrites,
                "The NCL cheat contains no executable operations.");
        }

        var writes = new List<NclStaticWrite>();
        long expandedBytes = 0;

        foreach (var operation in cheat.Operations)
        {
            switch (operation)
            {
                case NclDirectWrite direct
                    when direct.Mode == NclWriteMode.Replace:
                {
                    if (TryAdd(
                            writes,
                            ref expandedBytes,
                            direct.Address,
                            direct.Value,
                            direct.Line,
                            direct.Kind) is { } failure)
                    {
                        return failure;
                    }

                    break;
                }

                case NclDirectWrite:
                    return Failure(
                        NclStaticLowerCode.NotStaticCandidate,
                        "Read-modify-write direct operations cannot be lowered as unconditional static bytes.");

                case NclTextWrite text:
                {
                    if (TryAdd(
                            writes,
                            ref expandedBytes,
                            text.Address,
                            text.EncodedBytes,
                            text.Line,
                            text.Kind) is { } failure)
                    {
                        return failure;
                    }

                    break;
                }

                case NclFloatWrite floating:
                {
                    if (TryAdd(
                            writes,
                            ref expandedBytes,
                            floating.Address,
                            floating.BigEndianBytes,
                            floating.Line,
                            floating.Kind) is { } failure)
                    {
                        return failure;
                    }

                    break;
                }

                case NclCondensedWrite condensed:
                {
                    if (condensed.ValueIncrement != 0)
                    {
                        return Failure(
                            NclStaticLowerCode.UnverifiedValueIncrement,
                            "Condensed writes with a non-zero value increment remain fail-closed until dialect-specific increment equivalence is proven.");
                    }

                    var remainingWriteCapacity =
                        MaxExpandedWrites - writes.Count;

                    if (condensed.RepeatCount >
                        (ulong)remainingWriteCapacity)
                    {
                        return Failure(
                            NclStaticLowerCode.ExpansionLimit,
                            $"Condensed write expansion exceeds the {MaxExpandedWrites:N0}-write safety limit.");
                    }

                    for (ulong index = 0;
                         index < condensed.RepeatCount;
                         index++)
                    {
                        ulong address;

                        try
                        {
                            var offset = checked(
                                index *
                                condensed.AddressIncrement);

                            address = checked(
                                condensed.Address +
                                offset);
                        }
                        catch (OverflowException)
                        {
                            return Failure(
                                NclStaticLowerCode.AddressOverflow,
                                "Condensed write address arithmetic overflowed UInt64.");
                        }

                        if (TryAdd(
                                writes,
                                ref expandedBytes,
                                address,
                                condensed.InitialValue,
                                condensed.Line,
                                condensed.Kind) is { } failure)
                        {
                            return failure;
                        }
                    }

                    break;
                }

                case NclPointerResolve _:
                case NclCopyBuffer _:
                case NclFindReplace _:
                case NclConditionalEqual _:
                case NclConditionalMaskUnset _:
                case NclCopyBytes _:
                    return Failure(
                        NclStaticLowerCode.NotStaticCandidate,
                        $"Operation '{operation.Kind}' requires runtime-dependent semantics.");

                case NclUnsupportedOp unsupported:
                    return Failure(
                        NclStaticLowerCode.UnsupportedOperation,
                        unsupported.Reason);

                default:
                    return Failure(
                        NclStaticLowerCode.UnsupportedOperation,
                        $"Operation '{operation.Kind}' has no verified static lowering rule.");
            }
        }

        if (writes.Count == 0)
        {
            return Failure(
                NclStaticLowerCode.NoConcreteWrites,
                "Static lowering produced no concrete memory writes.");
        }

        return new NclStaticLowerResult(
            true,
            NclStaticLowerCode.Success,
            writes.ToArray(),
            null);
    }

    private static NclStaticLowerResult? TryAdd(
        List<NclStaticWrite> writes,
        ref long expandedBytes,
        ulong address,
        IReadOnlyList<byte> bytes,
        int sourceLine,
        NclOpKind sourceKind)
    {
        if (bytes.Count == 0)
        {
            return null;
        }

        if (writes.Count >= MaxExpandedWrites)
        {
            return Failure(
                NclStaticLowerCode.ExpansionLimit,
                $"Static lowering exceeds the {MaxExpandedWrites:N0}-write safety limit.");
        }

        if (expandedBytes >
            MaxExpandedBytes - bytes.Count)
        {
            return Failure(
                NclStaticLowerCode.ExpansionLimit,
                $"Static lowering exceeds the {MaxExpandedBytes / (1024 * 1024)} MiB expanded-byte safety limit.");
        }

        var finalOffset = (ulong)(bytes.Count - 1);

        if (address > ulong.MaxValue - finalOffset)
        {
            return Failure(
                NclStaticLowerCode.AddressOverflow,
                "Static write range overflows UInt64.");
        }

        writes.Add(
            new NclStaticWrite(
                address,
                bytes.ToArray(),
                sourceLine,
                sourceKind));

        expandedBytes += bytes.Count;

        return null;
    }

    private static NclStaticLowerResult Failure(
        NclStaticLowerCode code,
        string error)
    {
        return new NclStaticLowerResult(
            false,
            code,
            [],
            error);
    }
}