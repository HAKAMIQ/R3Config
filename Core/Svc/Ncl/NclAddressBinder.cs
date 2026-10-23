using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclAddressBinder
{
    public static NclAddressBindResult Bind(
        NclStaticLowerResult lowering,
        IReadOnlyList<NclAddressDomain> domains)
    {
        ArgumentNullException.ThrowIfNull(lowering);
        ArgumentNullException.ThrowIfNull(domains);

        if (!lowering.IsOk ||
            lowering.Code != NclStaticLowerCode.Success)
        {
            return Failure(
                NclAddressBindCode.LoweringRejected,
                lowering.Error ??
                "Static lowering did not produce a verified write set.");
        }

        if (lowering.Writes.Count == 0)
        {
            return Failure(
                NclAddressBindCode.InvalidWrite,
                "A successful lowering result must contain at least one concrete write.");
        }

        if (domains.Count == 0)
        {
            return Failure(
                NclAddressBindCode.NoDomains,
                "No verified RPCS3 address domains were supplied.");
        }

        var normalizedDomains = new List<NormalizedDomain>(domains.Count);
        foreach (var domain in domains)
        {
            if (string.IsNullOrWhiteSpace(domain.RunKey) ||
                !RunKey.TryParse(domain.RunKey, out var parsedKey))
            {
                return Failure(
                    NclAddressBindCode.InvalidDomain,
                    $"Address domain run key '{domain.RunKey}' is not a valid RPCS3 run key.");
            }

            if (domain.Length == 0)
            {
                return Failure(
                    NclAddressBindCode.InvalidDomain,
                    $"Address domain '{parsedKey.RunKey}' has a zero byte length.");
            }

            var finalOffset = domain.Length - 1;
            if (domain.GuestStart > ulong.MaxValue - finalOffset)
            {
                return Failure(
                    NclAddressBindCode.InvalidDomain,
                    $"Address domain '{parsedKey.RunKey}' overflows the UInt64 guest-address space.");
            }

            normalizedDomains.Add(
                new NormalizedDomain(
                    parsedKey.RunKey,
                    domain.GuestStart,
                    domain.GuestStart + finalOffset,
                    domain.PatchAddressStart));
        }

        var bound = new List<NclBoundWrite>(lowering.Writes.Count);
        foreach (var write in lowering.Writes)
        {
            if (write.Bytes.Count == 0)
            {
                return Failure(
                    NclAddressBindCode.InvalidWrite,
                    $"Static write from source line {write.SourceLine} has an empty byte payload.");
            }

            var finalOffset = (ulong)(write.Bytes.Count - 1);
            if (write.Address > ulong.MaxValue - finalOffset)
            {
                return Failure(
                    NclAddressBindCode.InvalidWrite,
                    $"Static write from source line {write.SourceLine} overflows the UInt64 guest-address space.");
            }

            var guestEnd = write.Address + finalOffset;
            var matches = new List<NormalizedDomain>();
            var intersectingDomainCount = 0;

            foreach (var domain in normalizedDomains)
            {
                if (write.Address <= domain.GuestEnd &&
                    guestEnd >= domain.GuestStart)
                {
                    intersectingDomainCount++;
                }

                if (write.Address >= domain.GuestStart &&
                    guestEnd <= domain.GuestEnd)
                {
                    matches.Add(domain);
                }
            }

            if (matches.Count == 0)
            {
                return Failure(
                    intersectingDomainCount > 0
                        ? NclAddressBindCode.CrossesDomainBoundary
                        : NclAddressBindCode.UnmappedWrite,
                    intersectingDomainCount > 0
                        ? $"Static write 0x{write.Address:X}-0x{guestEnd:X} from source line {write.SourceLine} crosses a verified address-domain boundary."
                        : $"Static write 0x{write.Address:X}-0x{guestEnd:X} from source line {write.SourceLine} is outside every verified address domain.");
            }

            if (matches.Count != 1 ||
                intersectingDomainCount != 1)
            {
                return Failure(
                    NclAddressBindCode.AmbiguousDomain,
                    $"Static write 0x{write.Address:X}-0x{guestEnd:X} from source line {write.SourceLine} matches more than one verified address domain.");
            }

            var match = matches[0];
            var guestDelta = write.Address - match.GuestStart;
            if (guestDelta > uint.MaxValue)
            {
                return Failure(
                    NclAddressBindCode.PatchAddressOverflow,
                    $"Static write from source line {write.SourceLine} cannot be represented by RPCS3's UInt32 patch-address field.");
            }

            var patchAddress =
                (ulong)match.PatchAddressStart + guestDelta;
            if (patchAddress > uint.MaxValue ||
                patchAddress > uint.MaxValue - finalOffset)
            {
                return Failure(
                    NclAddressBindCode.PatchAddressOverflow,
                    $"Static write from source line {write.SourceLine} exceeds RPCS3's UInt32 patch-address range.");
            }

            bound.Add(
                new NclBoundWrite(
                    match.RunKey,
                    (uint)patchAddress,
                    write.Bytes.ToArray(),
                    write.Address,
                    write.SourceLine,
                    write.SourceKind));
        }

        return new NclAddressBindResult(
            true,
            NclAddressBindCode.Success,
            bound.ToArray(),
            null);
    }

    private static NclAddressBindResult Failure(
        NclAddressBindCode code,
        string error)
    {
        return new NclAddressBindResult(
            false,
            code,
            [],
            error);
    }

    private sealed record NormalizedDomain(
        string RunKey,
        ulong GuestStart,
        ulong GuestEnd,
        uint PatchAddressStart);
}
