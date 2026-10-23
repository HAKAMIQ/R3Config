using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

internal static class NclSemanticT
{
    public static void Run()
    {
        ModernParser_Preserves53SemanticsAndMetadata();
        ModernParser_RetainsFinalBlockWithoutTrailingTerminator();
        Verifier_FailsClosedAcrossRuntimeAndConstantOperations();
        Identity_ExtractsSerialAndVersionWithoutInventingThem();
        Identity_PairsSerialWithEffectiveApplicationVersion();
        Parser_RejectsMalformedCompoundOperations();
        ArtemisDialect_UsesSourceOptionsFilteringAndRuntimeSemantics();
        ArtemisKnownMalformedCorpusSignatures_FailClosed();
        StaticLowerer_ProducesExactBoundedByteEffects();
        AddressBinder_RequiresExplicitUnambiguousDomains();
        PpuAddressDomainEvidence_UsesFileBackedPpuSegments();
        PpuAddressDomainEvidence_RejectsForeignModuleSegments();
        PpuAddressDomainEvidence_RejectsVirtualLoadOverlaySegments();
        NclOptionSubstitution_ResolvesArtemisPlaceholders();
        NclParser_ResolvesSelectedArtemisOptionCheat();
        PpuConversionService_ComposesVerifiedStaticPipeline();
        NclManagedPatchT.Run();
        NclHardeningT.Run();
        RpcS3Emitter_RoundTripsExactBoundBytes();
    }

    private static void ModernParser_Preserves53SemanticsAndMetadata()
    {
        const string text = """
Direct
0
Author A
0 00100000 60000000
#
Bitwise
0
Author B
01 00100004 00000001
#
Text
0
Author C
1 00100008 Hello PS3
#
Float
0
Author D
2 00100010 -3.14159
#
Condensed
0
Author E
4 00100020 0101
41 00000010 27
#
Pointer
0
Author F
6 00100030 00000004
0 00000000 00000063
#
CopyPaste
0
Author G
A1 00100040 04
A2 00100050 00
#
FindReplace
0
Author H
B4 00010000 04000000
B 11223344 AABBCCDD
#
Conditional
0
Author I
D1 00100060 00000001
0 00100064 00000002
#
Mask
0
Author J
E1 00100068 00000004
0 0010006C 00000008
#
Copy
0
Author K
F4 00100070 00100080
#
Options
0
Author L
0 00100090 0000Z
[Z]0001=One;0002=Two[/Z]
#
""";

        var doc = NclParser.ReadText(text, "fixture.ncl");
        Assert(doc.Cheats.Count == 12, "Expected every modern NCL block to be retained.");
        Assert(doc.Cheats[0].Author == "Author A", "Expected author metadata to be retained.");

        var direct = (NclDirectWrite)doc.Cheats[0].Operations.Single();
        Assert(direct.Address == 0x00100000 &&
               direct.Value.SequenceEqual(new byte[] { 0x60, 0x00, 0x00, 0x00 }) &&
               direct.Mode == NclWriteMode.Replace,
            "Expected direct-write semantics to match NetCheat 4.53.");

        var bitwise = (NclDirectWrite)doc.Cheats[1].Operations.Single();
        Assert(bitwise.Mode == NclWriteMode.Or && bitwise.RawModifier == 1,
            "Expected 01 opcode to preserve OR-write semantics.");

        var textWrite = (NclTextWrite)doc.Cheats[2].Operations.Single();
        Assert(textWrite.Text == "Hello PS3" &&
               textWrite.EncodedBytes.Count > 0 &&
               textWrite.EncodedBytes[^1] == 0 &&
               textWrite.WritesNullTerminator,
            "Expected NetCheat text write to preserve spaces and append the NUL terminator.");

        var floatWrite = (NclFloatWrite)doc.Cheats[3].Operations.Single();
        Assert(Math.Abs(floatWrite.Value - (-3.14159f)) < 0.00001f &&
               floatWrite.BigEndianBytes.Count == 4,
            "Expected invariant Float32 parsing and big-endian bytes.");

        var condensed = (NclCondensedWrite)doc.Cheats[4].Operations.Single();
        Assert(condensed.AddressIncrement == 0x10 &&
               condensed.RepeatCount == 0x27 &&
               condensed.ValueIncrement == 1,
            "Expected 4/4[VINC] to be represented as one deterministic semantic operation.");

        var pointer = (NclPointerResolve)doc.Cheats[5].Operations[0];
        Assert(!pointer.RewritesNextValue && pointer.Offset == 4,
            "Expected opcode 6 modifier 0 to redirect the next operation address.");

        Assert(doc.Cheats[6].Operations.OfType<NclCopyBuffer>().Count() == 2,
            "Expected A1/A2 copy-paste operations to remain distinct.");

        var find = (NclFindReplace)doc.Cheats[7].Operations.Single();
        Assert(find.MaxMatches == 4 &&
               find.Original.SequenceEqual(new byte[] { 0x11, 0x22, 0x33, 0x44 }) &&
               find.Replacement.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }),
            "Expected B[COUNT] and its second B line to preserve search/replace semantics.");

        Assert(doc.Cheats[8].Operations[0] is NclConditionalEqual,
            "Expected D conditional semantic node.");
        Assert(doc.Cheats[9].Operations[0] is NclConditionalMaskUnset,
            "Expected E conditional semantic node.");

        var copy = (NclCopyBytes)doc.Cheats[10].Operations.Single();
        Assert(copy.Count == 4 && copy.Source == 0x00100070 && copy.Destination == 0x00100080,
            "Expected F[COUNT] source/destination semantics.");

        Assert(doc.Cheats[11].HasErr,
            "Expected unresolved [Z] placeholder bytes to fail closed instead of being guessed.");
        Assert(doc.Cheats[11].Options.Single().Values.Count == 2,
            "Expected [Z] options to be retained even when the containing write requires later substitution.");
    }

    private static void ModernParser_RetainsFinalBlockWithoutTrailingTerminator()
    {
        const string text = """
AoB Max Level (1 Fight)
0
Author A
0 00100000 60000000
#
AoB Max Level (Whole Game)
0
Author B
0 00100004 00000001
""";

        var doc = NclParser.ReadText(text, "fixture-eof.ncl");
        Assert(doc.Cheats.Count == 2,
            "Expected the final Artemis block to be retained even when the source ends without a trailing '#' terminator.");
        Assert(doc.Cheats[1].Name == "AoB Max Level (Whole Game)",
            "Expected the unterminated final block to keep its own name rather than being dropped or merged.");

        var final = (NclDirectWrite)doc.Cheats[1].Operations.Single();
        Assert(final.Address == 0x00100004 &&
               final.Value.SequenceEqual(new byte[] { 0x00, 0x00, 0x00, 0x01 }),
            "Expected the unterminated final block to parse its write operation, not just survive as metadata.");

        const string trailingWhitespaceText = """
Static
0
Author A
0 00100000 60000000
#


""";
        var whitespaceDoc = NclParser.ReadText(trailingWhitespaceText, "fixture-eof-whitespace.ncl");
        Assert(whitespaceDoc.Cheats.Count == 1,
            "Expected trailing whitespace-only text after the last '#' terminator not to create a phantom block.");
    }

    private static void Verifier_FailsClosedAcrossRuntimeAndConstantOperations()
    {
        const string text = """
Static
0
A
0 00100000 60000000
#
RuntimeOr
0
B
01 00100004 00000001
#
RuntimePointer
0
C
6 00100008 00000004
0 00000000 00000001
#
Constant
1
D
0 0010000C 00000001
#
""";

        var result = NclSemanticVerifier.Verify(NclParser.ReadText(text));
        Assert(result.StaticCandidates == 1, "Expected exactly one offline static candidate.");
        Assert(result.RuntimeRequired == 2, "Expected OR and pointer cheats to require runtime evidence.");
        Assert(result.ConstantRuntimeRequired == 1, "Expected constant-write block to require runtime execution.");
        Assert(result.Invalid == 0 && result.Unsupported == 0,
            "Expected valid modeled opcodes not to be mislabeled invalid or unsupported.");
    }

    private static void Identity_ExtractsSerialAndVersionWithoutInventingThem()
    {
        var exact = NclParser.ParseIdentity("Sonic Example BLUS12345 01.02.ncl");
        Assert(exact.Title == "Sonic Example" &&
               exact.Serials.Count == 1 &&
               exact.Serials[0] == "BLUS12345" &&
               exact.Versions.Count == 1 &&
               exact.Versions[0] == "01.02" &&
               exact.HasExactTarget,
            "Expected exact filename identity extraction.");

        var unknown = NclParser.ParseIdentity("Sonic Example.ncl");
        Assert(!unknown.HasExactTarget && unknown.Serials.Count == 0 && unknown.Versions.Count == 0,
            "Expected missing filename identity to stay unknown.");
    }

    private static void Identity_PairsSerialWithEffectiveApplicationVersion()
    {
        var gta = NclParser.ParseIdentity(
            "Grand Theft Auto IV Complete Edition BLES01128 v01.00 av01.02.ncl");

        Assert(
            gta.MatchesTarget("BLES01128", "01.02") &&
            !gta.MatchesTarget("BLES01128", "01.00") &&
            gta.Targets.Count == 1 &&
            gta.Targets[0].DiscVersion == "01.00",
            "Expected APP_VER to be the effective R3Config game version while preserving the disc VERSION as source evidence.");

        var regional = NclParser.ParseIdentity(
            "Armored Core Verdict Day BLES01898 v01.00 av01.01 BLUS31194 v01.00 av01.02.ncl");

        Assert(
            regional.MatchesTarget("BLES01898", "01.01") &&
            regional.MatchesTarget("BLUS31194", "01.02") &&
            !regional.MatchesTarget("BLES01898", "01.02") &&
            !regional.MatchesTarget("BLUS31194", "01.01"),
            "Expected serial/version pairs to remain regional and never form a Cartesian identity match.");

        var grouped = NclParser.ParseIdentity(
            "Adventure Time BLUS31450 NPUB31523 v01.00 av01.03.ncl");

        Assert(
            grouped.MatchesTarget("BLUS31450", "01.03") &&
            grouped.MatchesTarget("NPUB31523", "01.03") &&
            !grouped.MatchesTarget("BLUS31450", "01.00"),
            "Expected a declared serial group to share the same explicit APP_VER without treating VERSION as an alternative application version.");
    }

    private static void Parser_RejectsMalformedCompoundOperations()
    {
        const string malformed = """
Bad B
0
A
B 00010000 04000000
0 00100000 60000000
#
Bad D
0
B
D2 00100000 00000001
0 00100004 00000001
#
""";

        var doc = NclParser.ReadText(malformed);
        Assert(doc.Cheats[0].HasErr,
            "Expected malformed B pair to fail closed.");
        Assert(doc.Cheats[1].HasErr,
            "Expected conditional count beyond remaining operations to fail closed.");
    }
    private static void StaticLowerer_ProducesExactBoundedByteEffects()
    {
        var cheat = new NclCheat(
            "Static lowering",
            false,
            null,
            [
                new NclDirectWrite(
                    10,
                    "0 00001000 11223344",
                    0x1000,
                    new byte[] { 0x11, 0x22, 0x33, 0x44 },
                    NclWriteMode.Replace,
                    0),
                new NclTextWrite(
                    11,
                    "1 00001010 AB",
                    0x1010,
                    "AB",
                    new byte[] { 0x41, 0x42 },
                    false),
                new NclFloatWrite(
                    12,
                    "2 00001020 1.0",
                    0x1020,
                    1.0f,
                    new byte[] { 0x3F, 0x80, 0x00, 0x00 }),
                new NclCondensedWrite(
                    13,
                    "4 00001030 AA55",
                    0x1030,
                    new byte[] { 0xAA, 0x55 },
                    4,
                    3,
                    0,
                    "4 00000004 00000003")
            ],
            [],
            [],
            [],
            NclDialect.ArtemisPs3);

        var result = NclStaticLowerer.Lower(cheat);

        Assert(result.IsOk &&
               result.Code == NclStaticLowerCode.Success &&
               result.Writes.Count == 6,
            "Expected deterministic static operations to lower to six ordered concrete byte writes.");

        Assert(
            result.Writes
                .Select(write => write.Address)
                .SequenceEqual(
                    new ulong[]
                    {
                        0x1000,
                        0x1010,
                        0x1020,
                        0x1030,
                        0x1034,
                        0x1038
                    }),
            "Expected static lowering to preserve exact write ordering and condensed address expansion.");

        Assert(
            result.Writes[0].Bytes.SequenceEqual(
                new byte[] { 0x11, 0x22, 0x33, 0x44 }) &&
            result.Writes[1].Bytes.SequenceEqual(
                new byte[] { 0x41, 0x42 }) &&
            result.Writes[2].Bytes.SequenceEqual(
                new byte[] { 0x3F, 0x80, 0x00, 0x00 }) &&
            result.Writes[3].Bytes.SequenceEqual(
                new byte[] { 0xAA, 0x55 }) &&
            result.Writes[4].Bytes.SequenceEqual(
                new byte[] { 0xAA, 0x55 }) &&
            result.Writes[5].Bytes.SequenceEqual(
                new byte[] { 0xAA, 0x55 }),
            "Expected direct, text, float, and condensed writes to preserve their exact byte payloads.");

        var readModifyWrite = cheat with
        {
            Operations =
            [
                new NclDirectWrite(
                    20,
                    "01 00002000 01",
                    0x2000,
                    new byte[] { 0x01 },
                    NclWriteMode.Or,
                    1)
            ]
        };

        var readModifyResult =
            NclStaticLowerer.Lower(readModifyWrite);

        Assert(!readModifyResult.IsOk &&
               readModifyResult.Code ==
                   NclStaticLowerCode.NotStaticCandidate,
            "Expected OR/AND/XOR read-modify-write semantics to remain outside static lowering.");

        var incrementedCondensed = cheat with
        {
            Operations =
            [
                new NclCondensedWrite(
                    30,
                    "4 00003000 00000001",
                    0x3000,
                    new byte[] { 0x00, 0x00, 0x00, 0x01 },
                    4,
                    2,
                    1,
                    "41 00000004 00000002")
            ]
        };

        var incrementResult =
            NclStaticLowerer.Lower(incrementedCondensed);

        Assert(!incrementResult.IsOk &&
               incrementResult.Code ==
                   NclStaticLowerCode.UnverifiedValueIncrement,
            "Expected non-zero condensed value increments to remain fail-closed until dialect equivalence is proven.");

        var overflow = cheat with
        {
            Operations =
            [
                new NclDirectWrite(
                    40,
                    "0 FFFFFFFFFFFFFFFF AABB",
                    ulong.MaxValue,
                    new byte[] { 0xAA, 0xBB },
                    NclWriteMode.Replace,
                    0)
            ]
        };

        var overflowResult =
            NclStaticLowerer.Lower(overflow);

        Assert(!overflowResult.IsOk &&
               overflowResult.Code ==
                   NclStaticLowerCode.AddressOverflow,
            "Expected static write range overflow to fail closed.");
    }
    private static void AddressBinder_RequiresExplicitUnambiguousDomains()
    {
        const string ppuKey =
            "PPU-1111111111111111111111111111111111111111";
        const string ovlKey =
            "OVL-2222222222222222222222222222222222222222";

        static NclStaticLowerResult Lowered(
            params NclStaticWrite[] writes) =>
            new(
                true,
                NclStaticLowerCode.Success,
                writes,
                null);

        var writes = Lowered(
            new NclStaticWrite(
                0x1004,
                new byte[] { 0x11, 0x22, 0x33, 0x44 },
                10,
                NclOpKind.DirectWrite),
            new NclStaticWrite(
                0x2002,
                new byte[] { 0x41, 0x42 },
                11,
                NclOpKind.TextWrite));
        var domains = new[]
        {
            new NclAddressDomain(
                ppuKey,
                0x1000,
                0x100,
                0x8000),
            new NclAddressDomain(
                ovlKey,
                0x2000,
                0x20,
                0x40)
        };

        var result = NclAddressBinder.Bind(writes, domains);
        Assert(
            result.IsOk &&
            result.Code == NclAddressBindCode.Success &&
            result.Writes.Count == 2 &&
            result.Writes[0].RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) &&
            result.Writes[0].PatchAddress == 0x8004 &&
            result.Writes[0].SourceAddress == 0x1004 &&
            result.Writes[0].Bytes.SequenceEqual(
                new byte[] { 0x11, 0x22, 0x33, 0x44 }) &&
            result.Writes[1].RunKey.Equals(
                ovlKey,
                StringComparison.OrdinalIgnoreCase) &&
            result.Writes[1].PatchAddress == 0x42 &&
            result.Writes[1].SourceAddress == 0x2002 &&
            result.Writes[1].Bytes.SequenceEqual(
                new byte[] { 0x41, 0x42 }),
            "Expected explicit address domains to bind ordered static byte effects to exact RPCS3 run keys and patch addresses.");

        var loweringRejected = NclAddressBinder.Bind(
            new NclStaticLowerResult(
                false,
                NclStaticLowerCode.NotStaticCandidate,
                [],
                "runtime semantics"),
            domains);
        Assert(
            !loweringRejected.IsOk &&
            loweringRejected.Code ==
                NclAddressBindCode.LoweringRejected,
            "Expected address binding to reject a failed static-lowering result.");

        var noDomains = NclAddressBinder.Bind(writes, []);
        Assert(
            !noDomains.IsOk &&
            noDomains.Code == NclAddressBindCode.NoDomains,
            "Expected address binding to require explicit verified domains.");

        var invalidDomain = NclAddressBinder.Bind(
            writes,
            [new NclAddressDomain("not-a-run-key", 0x1000, 0x100, 0)]);
        Assert(
            !invalidDomain.IsOk &&
            invalidDomain.Code == NclAddressBindCode.InvalidDomain,
            "Expected invalid RPCS3 run keys to fail closed.");

        var overflowingDomain = NclAddressBinder.Bind(
            writes,
            [new NclAddressDomain(ppuKey, ulong.MaxValue, 2, 0)]);
        Assert(
            !overflowingDomain.IsOk &&
            overflowingDomain.Code == NclAddressBindCode.InvalidDomain,
            "Expected overflowing guest address domains to fail closed.");

        var unmapped = NclAddressBinder.Bind(
            Lowered(
                new NclStaticWrite(
                    0x5000,
                    new byte[] { 0xAA },
                    20,
                    NclOpKind.DirectWrite)),
            domains);
        Assert(
            !unmapped.IsOk &&
            unmapped.Code == NclAddressBindCode.UnmappedWrite,
            "Expected writes outside every verified address domain to fail closed.");

        var crossing = NclAddressBinder.Bind(
            Lowered(
                new NclStaticWrite(
                    0x1002,
                    new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
                    21,
                    NclOpKind.DirectWrite)),
            [new NclAddressDomain(ppuKey, 0x1000, 4, 0)]);
        Assert(
            !crossing.IsOk &&
            crossing.Code ==
                NclAddressBindCode.CrossesDomainBoundary,
            "Expected writes crossing a verified address-domain boundary to fail closed instead of being split.");

        var ambiguous = NclAddressBinder.Bind(
            Lowered(
                new NclStaticWrite(
                    0x1010,
                    new byte[] { 0xAA },
                    22,
                    NclOpKind.DirectWrite)),
            [
                new NclAddressDomain(ppuKey, 0x1000, 0x100, 0x1000),
                new NclAddressDomain(ovlKey, 0x1000, 0x100, 0x2000)
            ]);
        Assert(
            !ambiguous.IsOk &&
            ambiguous.Code == NclAddressBindCode.AmbiguousDomain,
            "Expected overlapping verified domains to remain ambiguous rather than selecting one arbitrarily.");

        var patchOverflow = NclAddressBinder.Bind(
            Lowered(
                new NclStaticWrite(
                    0x1000,
                    new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
                    23,
                    NclOpKind.DirectWrite)),
            [
                new NclAddressDomain(
                    ppuKey,
                    0x1000,
                    0x100,
                    uint.MaxValue - 1)
            ]);
        Assert(
            !patchOverflow.IsOk &&
            patchOverflow.Code ==
                NclAddressBindCode.PatchAddressOverflow,
            "Expected RPCS3 UInt32 patch-address overflow to fail closed.");
    }
    private static void PpuAddressDomainEvidence_UsesFileBackedPpuSegments()
    {
        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        const string ppuKey =
            "PPU-a2c90aafce24744d5ac6da8ad009ce301458c8a7";

        const string log =
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x0, p_filesz=0x0, p_memsz=0x0, flags=0x4\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n";

        var evidence =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                log,
                "BLES01128",
                ppuKey,
                "RPCS3.log");

        Assert(
            evidence.IsOk &&
            evidence.Code == NclPpuDomainEvidenceCode.Success &&
            evidence.Domains.Count == 2,
            "Expected exact PPU PT_LOAD evidence from the matching RPCS3 session.");

        Assert(
            evidence.Domains[0].RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) &&
            evidence.Domains[0].GuestStart == 0x00010000 &&
            evidence.Domains[0].Length == 0x00EB3548 &&
            evidence.Domains[0].PatchAddressStart == 0x00010000,
            "Expected the first PPU file-backed PT_LOAD range to map identically.");

        Assert(
            evidence.Domains[1].GuestStart == 0x00ED0000 &&
            evidence.Domains[1].Length == 0x000CF714 &&
            evidence.Domains[1].PatchAddressStart == 0x00ED0000,
            "Expected the second PPU domain to use p_filesz, not p_memsz.");

        var lowering = new NclStaticLowerResult(
            true,
            NclStaticLowerCode.Success,
            [
                new NclStaticWrite(
                    0x0066C908,
                    new byte[] { 0x41, 0x9E, 0xFF, 0x40 },
                    4,
                    NclOpKind.DirectWrite)
            ],
            null);

        var binding = NclAddressBinder.Bind(
            lowering,
            evidence.Domains);

        Assert(
            binding.IsOk &&
            binding.Code == NclAddressBindCode.Success &&
            binding.Writes.Count == 1 &&
            binding.Writes[0].PatchAddress == 0x0066C908 &&
            binding.Writes[0].SourceAddress == 0x0066C908 &&
            binding.Writes[0].Bytes.SequenceEqual(
                new byte[] { 0x41, 0x9E, 0xFF, 0x40 }),
            "Expected the proven BLES01128 PPU direct write to bind to the same RPCS3 patch address.");

        const string staleLog =
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x1000, p_memsz=0x1000, flags=0x5\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n" +
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x20000, p_filesz=0x1000, p_memsz=0x1000, flags=0x5\n" +
            "X ppu_loader: PPU executable hash: PPU-1111111111111111111111111111111111111111\n";

        var stale =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                staleLog,
                "BLES01128",
                ppuKey);

        Assert(
            !stale.IsOk &&
            stale.Code == NclPpuDomainEvidenceCode.HashNotFound,
            "Expected older matching-serial evidence to be rejected when the newest session has a different PPU hash.");

        var scopedMismatch =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                log,
                "BLES01128",
                "BLUS12345-" + ppuKey);

        Assert(
            !scopedMismatch.IsOk &&
            scopedMismatch.Code ==
                NclPpuDomainEvidenceCode.SerialScopeMismatch,
            "Expected a serial-scoped PPU run key to match the selected RPCS3 serial.");

        const string malformedLog =
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=oops, p_filesz=0x1000, p_memsz=0x1000, flags=0x5\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n";

        var malformed =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                malformedLog,
                "BLES01128",
                ppuKey);

        Assert(
            !malformed.IsOk &&
            malformed.Code == NclPpuDomainEvidenceCode.InvalidSegment,
            "Expected malformed relevant PT_LOAD evidence to fail closed.");
    }
    // A PRX, overlay or embedded SPU module logs PT_LOAD segments on the same
    // ppu_loader channel as the main executable (rpcs3 54014a7:
    // PPUModule.cpp:1614 ppu_load_prx, :2193 ppu_load_exec, :2909
    // ppu_load_overlay). Only the main executable emits "PPU executable hash:"
    // (:2368,:2372), so foreign segments must be discarded at their proven
    // module boundary instead of being inherited by the next matching hash.
    private static void PpuAddressDomainEvidence_RejectsForeignModuleSegments()
    {
        const string ppuHash =
            "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";
        const string ppuKey =
            "PPU-3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";
        const string serial = "BCUS98232";

        const string mainSegments =
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x80e468, p_memsz=0x80e468, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x820000, p_filesz=0x3c5f0, p_memsz=0x44e40, flags=0x600006\n";

        const string mainHash =
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + " (<- 179)\n";

        // Foreign PT_LOAD evidence, byte-identical in shape to the main
        // executable's own segment lines.
        const string foreignSegments =
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x0, p_filesz=0x13b68, p_memsz=0x13b68, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x13b70, p_filesz=0xd94, p_memsz=0x161c, flags=0x600006\n";

        void AcceptsOnlyMainDomains(string log, string because)
        {
            var evidence =
                NclPpuAddressDomainEvidenceCollector.CollectText(
                    log,
                    serial,
                    ppuKey,
                    "RPCS3.log");

            Assert(
                evidence.IsOk &&
                evidence.Code == NclPpuDomainEvidenceCode.Success &&
                evidence.Domains.Count == 2 &&
                evidence.Domains[0].GuestStart == 0x00010000 &&
                evidence.Domains[0].Length == 0x0080E468 &&
                evidence.Domains[0].PatchAddressStart == 0x00010000 &&
                evidence.Domains[1].GuestStart == 0x00820000 &&
                evidence.Domains[1].Length == 0x0003C5F0 &&
                evidence.Domains[1].PatchAddressStart == 0x00820000,
                because);
        }

        // 1. Clean main-executable evidence is accepted unchanged.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" + mainSegments + mainHash,
            "Expected clean main PPU PT_LOAD evidence to be accepted.");

        // 2. PRX segments preceding the main hash are discarded at the
        //    "**** Loaded to" boundary (PPUModule.cpp:1672).
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            "X ppu_loader: Loading library: liblv2.sprx\n" +
            foreignSegments +
            "X ppu_loader: **** Loaded to 0x30000000...0x30013b67 (size=0x13b68)\n" +
            mainSegments + mainHash,
            "Expected PRX PT_LOAD segments to be excluded from main PPU evidence.");

        // 3. The discriminator is the loader boundary, not "Loading library:":
        //    ppu_load_prx is also reached from sys_prx.cpp with no such line.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            foreignSegments +
            "X ppu_loader: **** Loaded to 0x30000000...0x30013b67 (size=0x13b68)\n" +
            mainSegments + mainHash,
            "Expected PRX segments loaded without a library banner to be excluded.");

        // 4. Overlay segments are discarded at the overlay boundary
        //    (sys_overlay.cpp:88 begin, :72 completion).
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            "X sys_overlay: lv2_overlay::load(): vpath='/app_home/OVL.SELF', offset=0x0\n" +
            foreignSegments +
            "X sys_overlay: Loaded overlay: \"/app_home/OVL.SELF\" (id=0x25000001)\n" +
            mainSegments + mainHash,
            "Expected overlay PT_LOAD segments to be excluded from main PPU evidence.");

        // 5. Embedded SPU images end a segment run (PPUModule.cpp:1483,1487).
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            foreignSegments +
            "X ppu_loader: SPU executable hash: SPU-dd4f975640172a0c30a4d5bc502094b2eef6f963\n" +
            mainSegments + mainHash,
            "Expected embedded SPU boundaries to discard pending PT_LOAD evidence.");

        // 6. Real BCUS98232 ordering: an earlier non-matching PPU executable
        //    hash, then PRX modules, then the matching main executable.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xec30, p_memsz=0xec30, flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-1f2967e954ef4713e2d37bc8ce41ed93d17413a7\n" +
            foreignSegments +
            "X ppu_loader: **** Loaded to 0x30000000...0x30013b67 (size=0x13b68)\n" +
            mainSegments + mainHash,
            "Expected the real BCUS98232 session shape to yield exactly the two main domains.");

        // 7. Foreign evidence alone leaves nothing trustworthy: fail closed.
        var contaminatedOnly =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                "X SYS: Serial: " + serial + "\n" +
                foreignSegments +
                "X ppu_loader: **** Loaded to 0x30000000...0x30013b67 (size=0x13b68)\n" +
                mainHash,
                serial,
                ppuKey,
                "RPCS3.log");

        Assert(
            !contaminatedOnly.IsOk &&
            contaminatedOnly.Code ==
                NclPpuDomainEvidenceCode.NoLoadSegments,
            "Expected a matching PPU hash with only foreign segments to fail closed.");
    }

    // PPUThread.cpp:4516 calls ppu_load_overlay(obj, true, path, offset)
    // directly, so a precompiled overlay never reaches sys_overlay.cpp:88 or
    // :72, and ppu_load_overlay logs no completion marker of its own. The only
    // boundary it cannot bypass is its own section list: every loader entry
    // point emits all "** Segment:" lines before any "** Section:" line
    // (PPUModule.cpp:1614/1697, :2193/2292, :2909/2985).
    private static void PpuAddressDomainEvidence_RejectsVirtualLoadOverlaySegments()
    {
        const string ppuHash =
            "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";
        const string ppuKey =
            "PPU-3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";
        const string serial = "BCUS98232";

        const string mainSegments =
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x80e468, p_memsz=0x80e468, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x820000, p_filesz=0x3c5f0, p_memsz=0x44e40, flags=0x600006\n";

        const string mainSections =
            "X ppu_loader: ** Section: sh_type=0x1, addr=0x10000, size=0x80e468, flags=0x6\n" +
            "X ppu_loader: ** Section: sh_type=0x8, addr=0x85c5f0, size=0x8850, flags=0x3\n";

        const string mainHash =
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + " (<- 179)\n";

        // A virtual-load overlay: PT_LOAD lines followed only by its own
        // section list. No "**** Loaded to", no sys_overlay marker, no
        // "SPU executable hash:", no OVL hash line anywhere.
        const string overlaySegments =
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x30000000, p_filesz=0x6b204, p_memsz=0x6b204, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x3006c000, p_filesz=0x1a10, p_memsz=0x2ec0, flags=0x600006\n";

        const string overlaySections =
            "X ppu_loader: ** Section: sh_type=0x1, addr=0x30000000, size=0x6b204, flags=0x6\n";

        void AcceptsOnlyMainDomains(string log, string because)
        {
            var evidence =
                NclPpuAddressDomainEvidenceCollector.CollectText(
                    log,
                    serial,
                    ppuKey,
                    "RPCS3.log");

            Assert(
                evidence.IsOk &&
                evidence.Code == NclPpuDomainEvidenceCode.Success &&
                evidence.Domains.Count == 2 &&
                evidence.Domains[0].GuestStart == 0x00010000 &&
                evidence.Domains[0].Length == 0x0080E468 &&
                evidence.Domains[0].PatchAddressStart == 0x00010000 &&
                evidence.Domains[1].GuestStart == 0x00820000 &&
                evidence.Domains[1].Length == 0x0003C5F0 &&
                evidence.Domains[1].PatchAddressStart == 0x00820000,
                because);
        }

        // 1. The main executable's own "** Section:" run sits between its
        //    segments and its hash (PPUModule.cpp:2193 -> :2292 -> :2366).
        //    It must never discard that executable's own evidence.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            mainSegments + mainSections + mainHash,
            "Expected the main executable's own section list to preserve its PT_LOAD evidence.");

        // 2. A direct virtual-load overlay carries no sys_overlay marker and
        //    no "**** Loaded to" line. Its section list is the boundary.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            overlaySegments + overlaySections +
            mainSegments + mainSections + mainHash,
            "Expected virtual-load overlay PT_LOAD segments to be excluded from main PPU evidence.");

        // 3. Precompilation ordering: overlay in an "SPRX Worker", then the
        //    "PPU Exec Worker" retry that emits the matching hash
        //    (PPUThread.cpp:4388, :4516, :4605, :4664).
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            "X PPU: Trying to load: /dev_hdd0/game/" + serial + "/USRDIR/OVL.SELF\n" +
            overlaySegments + overlaySections +
            "X PPU: Trying to load as executable: /dev_hdd0/game/" + serial + "/USRDIR/EBOOT.BIN\n" +
            mainSegments + mainSections + mainHash,
            "Expected the precompilation exec-worker retry to inherit no overlay segments.");

        // 4. An overlay with no section headers still cannot leak, because the
        //    exec-worker marker on the PPU channel opens the next module.
        AcceptsOnlyMainDomains(
            "X SYS: Serial: " + serial + "\n" +
            overlaySegments +
            "X PPU: Trying to load as executable: /dev_hdd0/game/" + serial + "/USRDIR/EBOOT.BIN\n" +
            mainSegments + mainSections + mainHash,
            "Expected a section-less virtual overlay to be discarded at the precompilation marker.");

        // 5. Overlay evidence that survives to a matching hash with no main
        //    segment run of its own must fail closed, never be adopted.
        var overlayOnly =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                "X SYS: Serial: " + serial + "\n" +
                "X PPU: Trying to load: /dev_hdd0/game/" + serial + "/USRDIR/OVL.SELF\n" +
                overlaySegments + overlaySections +
                "X PPU: Trying to load as executable: /dev_hdd0/game/" + serial + "/USRDIR/EBOOT.BIN\n" +
                mainHash,
                serial,
                ppuKey,
                "RPCS3.log");

        Assert(
            !overlayOnly.IsOk &&
            overlayOnly.Code ==
                NclPpuDomainEvidenceCode.NoLoadSegments,
            "Expected a matching PPU hash preceded only by overlay segments to fail closed.");

        // 6. Shape observed in a real RPCS3.log (rpcs3 54014a7, serial
        //    ARTPS3001): the main run carries non PT_LOAD program headers and
        //    a zero-length PT_LOAD among its file-backed segments, and is
        //    preceded by a section-terminated foreign module. Only the four
        //    non-empty p_type=0x1 ranges may become domains.
        const string observedMainRun =
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x1f4440, p_memsz=0x1f4440, flags=0x5\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x210000, p_filesz=0x1dee8, p_memsz=0x1dee8, flags=0x6\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000000, p_filesz=0x3ceaa2, p_memsz=0x3ceaa2, flags=0x400004\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x103d0000, p_filesz=0x5ac9c, p_memsz=0xf2788, flags=0x600006\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x0, p_filesz=0x0, p_memsz=0x0, flags=0x6600006\n" +
            "X ppu_loader: ** Segment: p_type=0x7, p_vaddr=0x22dee8, p_filesz=0x0, p_memsz=0x780, flags=0x4\n" +
            "X ppu_loader: ** Segment: p_type=0x60000001, p_vaddr=0x0, p_filesz=0x0, p_memsz=0x0, flags=0x0\n" +
            "X ppu_loader: ** Segment: p_type=0x60000002, p_vaddr=0x204418, p_filesz=0x28, p_memsz=0x28, flags=0x0\n" +
            "X ppu_loader: ** Section: sh_type=0x1000000, addr=0x10230, size=0x1ec414, flags=0x6\n" +
            "X ppu_loader: PPU executable hash: PPU-dcf006836f868f89d1d7cd0ea8f5649a706df3ef\n";

        var observed =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                "X SYS: Serial: ARTPS3001\n" +
                overlaySegments + overlaySections + observedMainRun,
                "ARTPS3001",
                "PPU-dcf006836f868f89d1d7cd0ea8f5649a706df3ef",
                "RPCS3.log");

        Assert(
            observed.IsOk &&
            observed.Code == NclPpuDomainEvidenceCode.Success &&
            observed.Domains.Count == 4 &&
            observed.Domains[0].GuestStart == 0x00010000 &&
            observed.Domains[0].Length == 0x001F4440 &&
            observed.Domains[1].GuestStart == 0x00210000 &&
            observed.Domains[1].Length == 0x0001DEE8 &&
            observed.Domains[2].GuestStart == 0x10000000 &&
            observed.Domains[2].Length == 0x003CEAA2 &&
            observed.Domains[3].GuestStart == 0x103D0000 &&
            observed.Domains[3].Length == 0x0005AC9C,
            "Expected the observed ARTPS3001 main run to yield exactly its four file-backed PT_LOAD domains.");
    }

    // ArtemisPS3 docs/INSTALLATION.md defines an option tag id as one or more
    // capital Z characters and states that the matching id placed anywhere in
    // the code is replaced by the selected option value. The shipped corpus
    // (App/Data/ncl.zip) uses both standalone and embedded placeholder runs.
    private static void NclOptionSubstitution_ResolvesArtemisPlaceholders()
    {
        static NclOptionSet Set(
            string raw,
            int placeholderId,
            params string[] values)
        {
            return new NclOptionSet(
                raw,
                [.. values.Select(item => new NclOption(item, item + " label"))],
                placeholderId);
        }

        // 1. Official documentation form: a standalone "ZZ" run.
        const string officialBody =
            "Body Colour\n0\nartemis\n0 201FEE08 ZZ\n" +
            "[ZZ]01=Yellow;02=Red;03=Blue;04=Green[/ZZ]";

        var official = NclOptionSubstitution.Resolve(
            officialBody,
            [Set("[ZZ]01=Yellow;02=Red;03=Blue;04=Green[/ZZ]", 2, "01", "02", "03", "04")],
            new Dictionary<int, string> { [2] = "02" });

        Assert(
            official.IsOk &&
            official.Code == NclOptionResolveCode.Success &&
            official.SubstitutionCount == 1 &&
            official.ResolvedText.Contains(
                "0 201FEE08 02",
                StringComparison.Ordinal) &&
            !official.ResolvedText.Contains(
                "0 201FEE08 ZZ",
                StringComparison.Ordinal),
            "Expected a standalone ZZ placeholder to be replaced by the selected value.");

        // 2. Embedded run: only the tail of the value token is the placeholder.
        var embedded = NclOptionSubstitution.Resolve(
            "Speed\n0\nbungholio\n0 007207FC 3C00Z\n[Z]4000=2x;4040=3x[/Z]",
            [Set("[Z]4000=2x;4040=3x[/Z]", 1, "4000", "4040")],
            new Dictionary<int, string> { [1] = "4040" });

        Assert(
            embedded.IsOk &&
            embedded.ResolvedText.Contains(
                "0 007207FC 3C004040",
                StringComparison.Ordinal),
            "Expected an embedded Z placeholder to preserve the surrounding code text.");

        // 3. Corpus shape (Alpha Protocol BLES00704): independent Z and ZZ runs
        //    in one cheat must resolve separately and must not collide.
        const string twoIds =
            "+9999 Gadgets\n0\njgduff1\n" +
            "0 013792C0 Z\n0 013792C8 ZZ\n" +
            "[Z]380B270D=On;380BFFFF=Off[/Z]\n" +
            "[ZZ]91690000=On;90090000=Off[/ZZ]";

        NclOptionSet[] twoSets =
        [
            Set("[Z]380B270D=On;380BFFFF=Off[/Z]", 1, "380B270D", "380BFFFF"),
            Set("[ZZ]91690000=On;90090000=Off[/ZZ]", 2, "91690000", "90090000")
        ];

        var pair = NclOptionSubstitution.Resolve(
            twoIds,
            twoSets,
            new Dictionary<int, string> { [1] = "380BFFFF", [2] = "91690000" });

        Assert(
            pair.IsOk &&
            pair.SubstitutionCount == 2 &&
            pair.RequiredPlaceholderIds.SequenceEqual([1, 2]) &&
            pair.ResolvedText.Contains(
                "0 013792C0 380BFFFF",
                StringComparison.Ordinal) &&
            pair.ResolvedText.Contains(
                "0 013792C8 91690000",
                StringComparison.Ordinal),
            "Expected independent Z and ZZ placeholder runs to resolve without collision.");

        // 4. A partially answered cheat names exactly the unanswered ids.
        var partial = NclOptionSubstitution.Resolve(
            twoIds,
            twoSets,
            new Dictionary<int, string> { [1] = "380BFFFF" });

        Assert(
            !partial.IsOk &&
            partial.Code == NclOptionResolveCode.SelectionRequired &&
            partial.MissingPlaceholderIds.SequenceEqual([2]),
            "Expected a missing option selection to fail closed and name the unresolved id.");

        // 5. No selection at all is still fail-closed, never auto-chosen.
        var none = NclOptionSubstitution.Resolve(twoIds, twoSets, null);

        Assert(
            !none.IsOk &&
            none.Code == NclOptionResolveCode.SelectionRequired &&
            none.MissingPlaceholderIds.SequenceEqual([1, 2]) &&
            none.ResolvedText.Length == 0,
            "Expected an unanswered option cheat to require explicit selection.");

        // 6. A value the set does not declare is rejected.
        var undeclaredValue = NclOptionSubstitution.Resolve(
            twoIds,
            twoSets,
            new Dictionary<int, string> { [1] = "DEADBEEF", [2] = "91690000" });

        Assert(
            !undeclaredValue.IsOk &&
            undeclaredValue.Code == NclOptionResolveCode.InvalidSelection,
            "Expected a selected value outside the declared option set to be rejected.");

        // 7. Corpus shape (Metal Gear Rising seg 164): the code references an id
        //    that no option set declares.
        var unknown = NclOptionSubstitution.Resolve(
            "X\n0\na\n0 00100000 ZZZ\n[Z]0001=One[/Z]",
            [Set("[Z]0001=One[/Z]", 1, "0001")],
            new Dictionary<int, string> { [1] = "0001", [3] = "0002" });

        Assert(
            !unknown.IsOk &&
            unknown.Code == NclOptionResolveCode.UnknownPlaceholder,
            "Expected an undeclared placeholder id to fail closed.");

        // 8. Corpus shape (Tales Of Berseria seg 12): the same id declared twice
        //    makes ownership ambiguous.
        var ambiguous = NclOptionSubstitution.Resolve(
            "X\n0\na\n0 00100000 Z\n[Z]0001=One[/Z]\n[Z]0002=Two[/Z]",
            [
                Set("[Z]0001=One[/Z]", 1, "0001"),
                Set("[Z]0002=Two[/Z]", 1, "0002")
            ],
            new Dictionary<int, string> { [1] = "0001" });

        Assert(
            !ambiguous.IsOk &&
            ambiguous.Code == NclOptionResolveCode.AmbiguousOptionSet,
            "Expected duplicate option-set ids to fail closed as ambiguous.");

        // 9. One id used by several code lines substitutes every occurrence.
        var repeated = NclOptionSubstitution.Resolve(
            "X\n0\na\n0 00100000 3860Z\n0 00100004 3860Z\n[Z]000A=10x[/Z]",
            [Set("[Z]000A=10x[/Z]", 1, "000A")],
            new Dictionary<int, string> { [1] = "000A" });

        Assert(
            repeated.IsOk &&
            repeated.SubstitutionCount == 2 &&
            repeated.ResolvedText.Contains(
                "0 00100000 3860000A",
                StringComparison.Ordinal) &&
            repeated.ResolvedText.Contains(
                "0 00100004 3860000A",
                StringComparison.Ordinal),
            "Expected every occurrence of one placeholder id to be substituted.");

        // 10. Option tags and block comments are metadata: a Z inside either is
        //     never a code placeholder, so nothing is required and nothing moves.
        const string maskedBody =
            "X\n0\na\n0 00100000 60000000\n/* ZZZ note */\n[Z]0001=One[/Z]";

        var masked = NclOptionSubstitution.Resolve(
            maskedBody,
            [Set("[Z]0001=One[/Z]", 1, "0001")],
            null);

        Assert(
            masked.IsOk &&
            masked.SubstitutionCount == 0 &&
            masked.RequiredPlaceholderIds.Count == 0 &&
            string.Equals(
                masked.ResolvedText,
                maskedBody,
                StringComparison.Ordinal),
            "Expected declared-but-unreferenced option sets to need no selection.");

        // 11. A cheat with no option tags at all is returned untouched.
        const string plainBody =
            "X\n0\na\n0 0066C908 419EFF40";

        var plain = NclOptionSubstitution.Resolve(
            plainBody,
            [],
            null);

        Assert(
            plain.IsOk &&
            plain.SubstitutionCount == 0 &&
            string.Equals(
                plain.ResolvedText,
                plainBody,
                StringComparison.Ordinal),
            "Expected a cheat without option tags to be unchanged by substitution.");

        // 12. Option sets that do not correspond to the supplied body fail closed
        //     rather than substituting against a different source.
        var desynced = NclOptionSubstitution.Resolve(
            plainBody,
            [Set("[Z]0001=One[/Z]", 1, "0001")],
            new Dictionary<int, string> { [1] = "0001" });

        Assert(
            !desynced.IsOk &&
            desynced.Code == NclOptionResolveCode.InvalidInput,
            "Expected option sets that do not match the cheat body to be rejected.");

        // 13. Two different selections must reach the existing parser and static
        //     lowerer as different exact concrete bytes.
        //
        //     Substitution leaves the [Z]..[/Z] tag in the text: it is metadata
        //     that ArtemisCodeLineValidity drops later, not something the
        //     resolver strips. Re-parsing already-resolved text would therefore
        //     make ParseArtemisOptions read that tag a second time and classify
        //     the cheat as unresolved again. The production seam is a single
        //     parse driven by an explicit selection, which is what
        //     NclPpuConversionService uses, so the test drives that same seam.
        static IReadOnlyList<byte> LowerFirstWrite(
            string cheatBody,
            IReadOnlyDictionary<int, string> selections)
        {
            var document = NclParser.ReadText(
                cheatBody + "\n#\n",
                "options.ncl",
                NclDialect.ArtemisPs3,
                new NclOptionSelection(0, selections));

            var lowering = NclStaticLowerer.Lower(document.Cheats[0]);

            Assert(
                lowering.IsOk &&
                lowering.Code == NclStaticLowerCode.Success &&
                lowering.Writes.Count == 1,
                "Expected a resolved option cheat to lower to exactly one static write.");

            return lowering.Writes[0].Bytes;
        }

        const string abBody =
            "Lot Of Items\n0\njgduff1\n0 00114AB8 Z\n" +
            "[Z]919F0480=On;90BF0480=Off[/Z]";

        NclOptionSet[] abSets =
            [Set("[Z]919F0480=On;90BF0480=Off[/Z]", 1, "919F0480", "90BF0480")];

        var choiceA = NclOptionSubstitution.Resolve(
            abBody,
            abSets,
            new Dictionary<int, string> { [1] = "919F0480" });

        var choiceB = NclOptionSubstitution.Resolve(
            abBody,
            abSets,
            new Dictionary<int, string> { [1] = "90BF0480" });

        Assert(
            choiceA.IsOk && choiceB.IsOk,
            "Expected both declared option values to resolve.");

        var bytesA = LowerFirstWrite(
            abBody,
            new Dictionary<int, string> { [1] = "919F0480" });

        var bytesB = LowerFirstWrite(
            abBody,
            new Dictionary<int, string> { [1] = "90BF0480" });

        Assert(
            bytesA.SequenceEqual(new byte[] { 0x91, 0x9F, 0x04, 0x80 }) &&
            bytesB.SequenceEqual(new byte[] { 0x90, 0xBF, 0x04, 0x80 }) &&
            !bytesA.SequenceEqual(bytesB),
            "Expected each selected option value to produce its own exact bytes.");

        // 14. Substitution that yields malformed code is rejected by the normal
        //     parser, not repaired and not accepted.
        var malformed = NclOptionSubstitution.Resolve(
            "X\n0\na\n0 00114AB8 Z\n[Z]ABC=Odd[/Z]",
            [Set("[Z]ABC=Odd[/Z]", 1, "ABC")],
            new Dictionary<int, string> { [1] = "ABC" });

        Assert(
            malformed.IsOk,
            "Expected substitution itself to succeed before parsing decides validity.");

        var malformedDoc = NclParser.ReadText(
            "X\n0\na\n0 00114AB8 Z\n[Z]ABC=Odd[/Z]\n#\n",
            "options.ncl",
            NclDialect.ArtemisPs3,
            new NclOptionSelection(
                0,
                new Dictionary<int, string> { [1] = "ABC" }));

        var malformedLowering =
            NclStaticLowerer.Lower(malformedDoc.Cheats[0]);

        // The rejection must come from the parser judging the substituted hex,
        // not from the cheat still looking unresolved.
        Assert(
            malformedDoc.Cheats[0].HasErr &&
            !malformedLowering.IsOk &&
            malformedLowering.Code == NclStaticLowerCode.NotStaticCandidate,
            "Expected odd-length substituted hex to be rejected by the existing pipeline.");
    }

    // The parser hook: capital Z is not an Artemis hex character, so an
    // unresolved placeholder line never survives ArtemisCodeLineValidity.
    // Substitution therefore runs on the raw cheat body, and only the cheat the
    // caller selected is resolved.
    private static void NclParser_ResolvesSelectedArtemisOptionCheat()
    {
        const string document =
            "Lot Of Items\n0\njgduff1\n0 00114AB8 Z\n" +
            "[Z]919F0480=On;90BF0480=Off[/Z]\n#\n" +
            "Plain Cheat\n0\njgduff1\n0 0066C908 419EFF40\n#\n";

        // Without a selection the option cheat keeps its unresolved state.
        var unresolved = NclParser.ReadText(
            document,
            "options.ncl",
            NclDialect.ArtemisPs3);

        var unresolvedVerify =
            NclSemanticVerifier.Verify(unresolved);

        Assert(
            unresolved.Cheats.Count == 2 &&
            unresolved.Cheats[0].Options.Count == 1 &&
            unresolved.Cheats[0].Operations.Count == 0 &&
            unresolvedVerify.Items[0].Status ==
                NclVerifyStat.OptionSelectionRequired,
            "Expected an unselected Artemis option cheat to stay OptionSelectionRequired.");

        // The non-option cheat is unaffected either way.
        Assert(
            unresolved.Cheats[1].Operations.Count == 1 &&
            unresolvedVerify.Items[1].Status ==
                NclVerifyStat.OfflineStaticCandidate,
            "Expected a cheat without option tags to keep the existing path.");

        // With an explicit selection the same source resolves to concrete code.
        var resolved = NclParser.ReadText(
            document,
            "options.ncl",
            NclDialect.ArtemisPs3,
            new NclOptionSelection(
                0,
                new Dictionary<int, string> { [1] = "90BF0480" }));

        var resolvedVerify = NclSemanticVerifier.Verify(resolved);

        Assert(
            resolved.Cheats[0].Options.Count == 0 &&
            resolved.Cheats[0].Operations.Count == 1 &&
            resolvedVerify.Items[0].Status ==
                NclVerifyStat.OfflineStaticCandidate,
            "Expected the selected option cheat to resolve into normal executable operations.");

        var lowering = NclStaticLowerer.Lower(resolved.Cheats[0]);

        Assert(
            lowering.IsOk &&
            lowering.Writes.Count == 1 &&
            lowering.Writes[0].Address == 0x00114AB8 &&
            lowering.Writes[0].Bytes.SequenceEqual(
                new byte[] { 0x90, 0xBF, 0x04, 0x80 }),
            "Expected the selected option value to lower to its own exact bytes.");

        // The unselected cheat in the same document is untouched.
        Assert(
            resolved.Cheats[1].Operations.Count == 1 &&
            resolvedVerify.Items[1].Status ==
                NclVerifyStat.OfflineStaticCandidate,
            "Expected a selection to resolve only the cheat it targets.");

        // A selection aimed at another cheat index leaves this one unresolved.
        var otherIndex = NclParser.ReadText(
            document,
            "options.ncl",
            NclDialect.ArtemisPs3,
            new NclOptionSelection(
                1,
                new Dictionary<int, string> { [1] = "90BF0480" }));

        Assert(
            otherIndex.Cheats[0].Options.Count == 1 &&
            otherIndex.Cheats[0].Operations.Count == 0,
            "Expected an option selection for a different cheat index to be ignored here.");

        // A supplied selection that cannot be honoured fails closed as invalid
        // rather than silently returning the unresolved cheat.
        var rejected = NclParser.ReadText(
            document,
            "options.ncl",
            NclDialect.ArtemisPs3,
            new NclOptionSelection(
                0,
                new Dictionary<int, string> { [1] = "DEADBEEF" }));

        Assert(
            rejected.Cheats[0].HasErr &&
            NclSemanticVerifier.Verify(rejected).Items[0].Status ==
                NclVerifyStat.Invalid,
            "Expected an undeclared selected value to fail closed at the parser.");

        // A resolved B opcode is still runtime-required: substitution grants no
        // new opcode support.
        // Corpus shape (Aliens Colonial Marines): opcode B needs its range line
        // and a second B line carrying the patterns, with the placeholder inside
        // the replacement pattern.
        var findReplace = NclParser.ReadText(
            "AoB\n0\nbungholio\n" +
            "B 00010000 04000000\n" +
            "B 804900047C0903A64E800421D03C0058 3C00Z90030094C0231B4CD03C0058\n" +
            "[Z]4000=2x;4040=3x[/Z]\n#\n",
            "options.ncl",
            NclDialect.ArtemisPs3,
            new NclOptionSelection(
                0,
                new Dictionary<int, string> { [1] = "4000" }));

        Assert(
            NclSemanticVerifier.Verify(findReplace).Items[0].Status ==
                NclVerifyStat.RuntimeEvidenceRequired,
            "Expected a resolved find/replace cheat to remain runtime-required.");
    }

    private static void PpuConversionService_ComposesVerifiedStaticPipeline()
    {
        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        const string ppuKey =
            "PPU-a2c90aafce24744d5ac6da8ad009ce301458c8a7";

        const string ncl =
            "1P Invincible Mode\n" +
            "0\n" +
            "xtatu\n" +
            "0 0066C908 419EFF40\n" +
            "#\n" +
            "AoB 1P Invincible Mode\n" +
            "0\n" +
            "xtatu\n" +
            "B 00010000 04000000\n" +
            "B 409EFF404089034C812100A43B60000039E00000827E8078 419EFF404089034C812100A43B60000039E00000827E8078\n" +
            "#\n";

        const string log =
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n";

        var target = new NclPpuConversionTarget(
            ppuKey,
            "1P Invincible Mode",
            "Grand Theft Auto IV Complete Edition",
            "BLES01128",
            "01.00",
            "xtatu",
            "NCL end-to-end regression");

        var result =
            NclPpuConversionService.ConvertText(
                new NclPpuConversionTextRequest(
                    ncl,
                    log,
                    NclDialect.ArtemisPs3,
                    0,
                    target,
                    "Grand Theft Auto IV Complete Edition BLES01128 01.00.ncl",
                    "RPCS3.log"));

        Assert(
            result.IsOk &&
            result.Code == NclPpuConversionCode.Success &&
            result.RunKey is not null &&
            result.RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) &&
            result.CheatIndex == 0 &&
            result.CheatName == "1P Invincible Mode" &&
            result.LoweringCode == NclStaticLowerCode.Success &&
            result.EvidenceCode == NclPpuDomainEvidenceCode.Success &&
            result.BindingCode == NclAddressBindCode.Success &&
            result.EmissionCode == NclRpcS3EmitCode.Success &&
            result.InstructionCount == 4 &&
            result.VerifiedByteCount == 4 &&
            result.PatchText.Contains(
                "0x0066C908",
                StringComparison.OrdinalIgnoreCase),
            "Expected the proven BLES01128 direct NCL write to traverse the complete verified PPU conversion pipeline.");

        var runtime =
            NclPpuConversionService.ConvertText(
                new NclPpuConversionTextRequest(
                    ncl,
                    log,
                    NclDialect.ArtemisPs3,
                    1,
                    target));

        Assert(
            !runtime.IsOk &&
            runtime.Code == NclPpuConversionCode.LoweringRejected &&
            runtime.LoweringCode ==
                NclStaticLowerCode.NotStaticCandidate,
            "Expected the Artemis B find/replace cheat to remain outside the offline-static conversion subset.");

        var missingHashLog =
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-1111111111111111111111111111111111111111\n";

        var missingHash =
            NclPpuConversionService.ConvertText(
                new NclPpuConversionTextRequest(
                    ncl,
                    missingHashLog,
                    NclDialect.ArtemisPs3,
                    0,
                    target));

        Assert(
            !missingHash.IsOk &&
            missingHash.Code ==
                NclPpuConversionCode.EvidenceRejected &&
            missingHash.EvidenceCode ==
                NclPpuDomainEvidenceCode.HashNotFound,
            "Expected end-to-end conversion to reject a target PPU hash absent from the newest matching session.");

        var badSelection =
            NclPpuConversionService.ConvertText(
                new NclPpuConversionTextRequest(
                    ncl,
                    log,
                    NclDialect.ArtemisPs3,
                    2,
                    target));

        Assert(
            !badSelection.IsOk &&
            badSelection.Code ==
                NclPpuConversionCode.CheatSelectionOutOfRange,
            "Expected explicit cheat selection to fail closed when the parsed index is out of range.");
    }
    private static void RpcS3Emitter_RoundTripsExactBoundBytes()
    {
        const string ppuKey =
            "PPU-1111111111111111111111111111111111111111";

        var binding = new NclAddressBindResult(
            true,
            NclAddressBindCode.Success,
            [
                new NclBoundWrite(
                    ppuKey,
                    0x8004,
                    new byte[] { 0x11, 0x22, 0x33, 0x44 },
                    0x1004,
                    10,
                    NclOpKind.DirectWrite),
                new NclBoundWrite(
                    ppuKey,
                    0x9000,
                    new byte[] { 0x41, 0x42 },
                    0x2000,
                    11,
                    NclOpKind.TextWrite)
            ],
            null);

        var result = NclRpcS3Emitter.Emit(
            new NclRpcS3EmitRequest(
                binding,
                "NCL exact bytes",
                "Fixture Game",
                "BLUS12345",
                "01.00",
                "Fixture Author",
                "Emitter regression"));

        Assert(
            result.IsOk &&
            result.Code == NclRpcS3EmitCode.Success &&
            result.RunKey is not null &&
            result.RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) &&
            result.InstructionCount == 6 &&
            result.VerifiedByteCount == 6 &&
            result.PatchText.StartsWith(
                "Version: 1.2\r\n",
                StringComparison.Ordinal),
            "Expected a successful RPCS3 Patch 1.2 byte-exact emission.");

        var read = new ReadSvc().ReadText(
            result.PatchText,
            PatSrc.ManagedArtemis);

        Assert(
            read.IsOk &&
            read.Catalog.Entries.Count == 1 &&
            read.Catalog.Entries[0].RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) &&
            read.Catalog.Entries[0].GameTgt.Count == 1 &&
            read.Catalog.Entries[0].GameTgt[0].Serial == "BLUS12345" &&
            read.Catalog.Entries[0].GameTgt[0].Versions.Count == 1 &&
            read.Catalog.Entries[0].GameTgt[0].Versions[0] == "01.00",
            "Expected ReadSvc to accept the emitted run key and exact game target.");

        var multiRunKey = binding with
        {
            Writes =
            [
                .. binding.Writes,
                new NclBoundWrite(
                    "OVL-2222222222222222222222222222222222222222",
                    0x20,
                    new byte[] { 0xAA },
                    0x3000,
                    12,
                    NclOpKind.DirectWrite)
            ]
        };

        var multiResult = NclRpcS3Emitter.Emit(
            new NclRpcS3EmitRequest(
                multiRunKey,
                "NCL multi module",
                "Fixture Game",
                "BLUS12345",
                "01.00"));

        Assert(
            !multiResult.IsOk &&
            multiResult.Code ==
                NclRpcS3EmitCode.MultipleRunKeys,
            "Expected one NCL cheat spanning multiple RPCS3 run keys to fail closed until atomic cross-module activation exists.");

        var invalidMetadata = NclRpcS3Emitter.Emit(
            new NclRpcS3EmitRequest(
                binding,
                "NCL invalid metadata",
                "Fixture Game",
                "BAD",
                "01.00"));

        Assert(
            !invalidMetadata.IsOk &&
            invalidMetadata.Code ==
                NclRpcS3EmitCode.InvalidMetadata,
            "Expected invalid RPCS3 game-target metadata to fail closed.");

        var scopedMismatch = binding with
        {
            Writes =
            [
                new NclBoundWrite(
                    "BLES54321-PPU-1111111111111111111111111111111111111111",
                    0x8004,
                    new byte[] { 0x11 },
                    0x1004,
                    13,
                    NclOpKind.DirectWrite)
            ]
        };

        var scopedMismatchResult = NclRpcS3Emitter.Emit(
            new NclRpcS3EmitRequest(
                scopedMismatch,
                "NCL scoped mismatch",
                "Fixture Game",
                "BLUS12345",
                "01.00"));

        Assert(
            !scopedMismatchResult.IsOk &&
            scopedMismatchResult.Code ==
                NclRpcS3EmitCode.InvalidMetadata,
            "Expected a serial-scoped RPCS3 run key to match the emitted Games serial.");

        var rejectedBinding = NclRpcS3Emitter.Emit(
            new NclRpcS3EmitRequest(
                new NclAddressBindResult(
                    false,
                    NclAddressBindCode.UnmappedWrite,
                    [],
                    "unmapped"),
                "NCL rejected binding",
                "Fixture Game",
                "BLUS12345",
                "01.00"));

        Assert(
            !rejectedBinding.IsOk &&
            rejectedBinding.Code ==
                NclRpcS3EmitCode.BindingRejected,
            "Expected RPCS3 emission to require a successful address-binding result.");
    }
    private static void ArtemisKnownMalformedCorpusSignatures_FailClosed()
    {
        var cases = new[]
        {
            (
                Name: "Aliens Colonial Marines BLES01770 01.05 / AoB Speed Modifier",
                Source:
                    "AoB Speed Modifier\n" +
                    "0\n" +
                    "dron_3\n" +
                    "B 00010000 04000000\n" +
                    "B 804900047C0903A64E800421D03C0058 3C00Z90030094C0231B4CD03C0058\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Chou Jigen Game Neptune BLJM60247 01.02 / Infinite GP",
                Source:
                    "Infinite GP\n" +
                    "0\n" +
                    "xtatu\n" +
                    "0 00284284 38C900000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL011" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Chou Jigen Game Neptune BLJM60247 01.04 / Infinite GP",
                Source:
                    "Infinite GP\n" +
                    "0\n" +
                    "xtatu\n" +
                    "0 00278604 38C900000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL011" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Fairy Fencer F BLUS31423 01.00 / Fang Max HP",
                Source:
                    "Fang Max HP\n" +
                    "0\n" +
                    "JorgeFFC\n" +
                    "0 32179196 1869F\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL011" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "I Am Alive NPUB30383 01.00 / AoB Enable Pause + Start Menu",
                Source:
                    "AoB Enable Pause + Start Menu (Cheats)\n" +
                    "B 00010000 04000000\n" +
                    "B D804000038C6FFFF386300014BFFFE8C 6000000038C6FFFF386300014BFFFE8C\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL018" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Ninja Gaiden Sigma 2 / AoB Max Money On Pickup",
                Source:
                    "AoB Max Money On Pickup\n" +
                    "0\n" +
                    "Skiller\n" +
                    "B 00010000 04000000\n" +
                    "B 800400007F805000409D0008  916400008004000060000000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Ninokuni BLJS10150 03.00 / AoB 100% Auto Guard Rate",
                Source:
                    "AoB 100% Auto Guard Rate\n" +
                    "0\n" +
                    "Medo\n" +
                    "B 00010000 04000000\n" +
                    "B =63BD20002F9B0000419E000863BD1000 =63BD20002F9B00006000000063BD1000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Raiden IV Overkill NPUB31371 01.00 / AoB Infinite Bombs",
                Source:
                    "AoB Infinite Bombs + Invincible\n" +
                    "0\n" +
                    "optantic\n" +
                    "B 00010000 04000000\n" +
                    "B 807F00A43063FFFF907F00A4  38600006906505609065076C\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Shin Sangoku Musou 6 BLJM61000 01.00 / Infinite Musou",
                Source:
                    "Infinite Musou\n" +
                    "0\n" +
                    "xtatu\n" +
                    "0 0008A7DC 409C00008\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL011" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Siren Blood Curse NPUA80136 / AoB Enemies Can Not See You",
                Source:
                    "AoB Enemies Can Not See You\n" +
                    "0\n" +
                    "bungholio\n" +
                    "B 00010000 04000000\n" +
                    "B F1122334455667788 99AA\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019", "NCL018" },
                ExpectedErrorCount: 2
            ),
            (
                Name: "Siren Blood Curse NPUA80136 / AoB Some Attacks Do No Damage",
                Source:
                    "AoB Some Attacks Do No Damage\n" +
                    "0\n" +
                    "bungholio\n" +
                    "B 00010000 04000000\n" +
                    "B 81670010810A0150812B0000FC00681880090144392100802F800000D0060008 =81670010810A0150812B0000FC00681880090144392100802F80000060000000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Street Fighter X Tekken NPEB01792 / AoB Auto Max Power",
                Source:
                    "AoB Auto Max Power 1P + 2P\n" +
                    "0\n" +
                    "xtatu\n" +
                    "B 00010000 04000000\n" +
                    "B 408100A4480000402C030006408100102C030007408100144800008C3860000 418100A4480000402C030006408100102C030007408100144800008C3860000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019", "NCL018" },
                ExpectedErrorCount: 2
            ),
            (
                Name: "Tales Of Graces F BLUS30903 / terminal B range",
                Source:
                    "AoB Max All Stats\n" +
                    "0\n" +
                    "Medo\n" +
                    "B 00010000 04000000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "Tears To Tiara BLJM60166 / All Characters No Limit to EP",
                Source:
                    "All Characters No Limit to EP in Battle\n" +
                    "0\n" +
                    "CodeFreak/PS3UserCheat\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "0 00000000 42C8000042C800000\n" +
                    "#\n",
                ExpectedCodes: new[]
                {
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011",
                    "NCL011"
                },
                ExpectedErrorCount: 10
            ),
            (
                Name: "The Evil Within NPEB01797 BLES01916 / Infinite Sprint",
                Source:
                    "AoB Infinite Sprint Stamina\n" +
                    "0\n" +
                    "FBMLB 00010000 04000000\n" +
                    "B C0380C683B200001EC21D82AD0380C68 C0380C683B200001EC21D82A60000000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL018" },
                ExpectedErrorCount: 1
            ),
            (
                Name: "inFamous 2 / AoB Very Fast Karma Gain + Lose",
                Source:
                    "AoB Very Fast Karma Gain + Lose\n" +
                    "0\n" +
                    "bungholio\n" +
                    "B 00010000 04000000\n" +
                    "B 7F83E378 4BFFD15D 7F83E378 60000000\n" +
                    "#\n",
                ExpectedCodes: new[] { "NCL019" },
                ExpectedErrorCount: 1
            )
        };

        var invalidCheats = 0;
        var errorDiagnostics = 0;

        foreach (var item in cases)
        {
            var document = NclParser.ReadText(
                item.Source,
                item.Name + ".ncl",
                NclDialect.ArtemisPs3);

            Assert(document.Cheats.Count == 1,
                $"Expected one retained cheat for known malformed Artemis signature: {item.Name}.");

            var result = NclSemanticVerifier.Verify(document);
            var cheat = document.Cheats.Single();
            var errors = cheat.Diags
                .Where(diag => diag.Severity == NclDiagSev.Error)
                .ToArray();

            Assert(result.Invalid == 1 && cheat.HasErr,
                $"Expected known malformed Artemis signature to remain fail-closed: {item.Name}.");

            Assert(errors.Length == item.ExpectedErrorCount,
                $"Unexpected diagnostic count for known malformed Artemis signature: {item.Name}.");

            Assert(errors
                    .Select(diag => diag.Code)
                    .SequenceEqual(item.ExpectedCodes),
                $"Unexpected diagnostic sequence for known malformed Artemis signature: {item.Name}.");

            invalidCheats += result.Invalid;
            errorDiagnostics += errors.Length;
        }

        Assert(cases.Length == 16 &&
               invalidCheats == 16 &&
               errorDiagnostics == 27,
            "Expected the known Artemis malformed-source regression set to remain exactly 16 invalid cheats with 27 error diagnostics.");
    }
    private static void ArtemisDialect_UsesSourceOptionsFilteringAndRuntimeSemantics()
    {
        const string text = """
Metadata Only
0
DANNY G
This line is descriptive text, not executable code.
#
Static
0
DANNY G
0 00100000 60000000
#
Options
0
0 00100010 0000ZZ
[ZZ]0001=One;0002=Two[/ZZ]
#
Pointer
0
61 00100020 00000004
0 00000000 00000001
#
Unequal B
0
B4 00010000 04000000
B 11223344 AABB
#
""";

        var document = NclParser.ReadText(
            text,
            "artemis-fixture.ncl",
            NclDialect.ArtemisPs3);
        var result = NclSemanticVerifier.Verify(document);

        Assert(document.Dialect == NclDialect.ArtemisPs3 &&
               document.Cheats.Count == 5,
            "Expected Artemis dialect and every source-delimited entry to be retained.");
        Assert(document.Cheats[0].Author is null &&
               document.Cheats[0].Operations.Count == 0 &&
               document.Cheats[0].Notes.Count >= 1,
            "Expected Artemis author-like and descriptive lines to remain non-executable metadata rather than structural author fields.");
        Assert(result.NonExecutableMetadata == 1 &&
               result.StaticCandidates == 1 &&
               result.OptionSelectionRequired == 1 &&
               result.RuntimeRequired == 2 &&
               result.Invalid == 0 &&
               result.Unsupported == 0,
            "Expected Artemis-specific metadata, option, static, and runtime classifications without false invalid entries.");

        var option = document.Cheats[2].Options.Single();
        Assert(option.PlaceholderId == 2 && option.Values.Count == 2,
            "Expected [ZZ] option placeholders to preserve their Artemis placeholder id and values.");

        var pointer = (NclPointerResolve)document.Cheats[3].Operations[0];
        Assert(!pointer.RewritesNextValue && pointer.RawModifier == 1,
            "Expected Artemis opcode 6 to redirect the next address even when the command contains a modifier.");

        var find = (NclFindReplace)document.Cheats[4].Operations.Single();
        Assert(find.Original.Count == 4 && find.Replacement.Count == 2,
            "Expected Artemis/NetCheat find-replace to permit unequal original and replacement byte lengths.");
    }

}
