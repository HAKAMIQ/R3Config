using System.Globalization;
using System.IO;
using System.Linq;
using R3Config.Svc;
using YamlDotNet.RepresentationModel;

namespace R3Config.Tests;

// Structure is not meaning. These pin the reduction of RPCS3 patch operations
// to the bytes they write, using the two relationships observed in the real
// BCUS98103 corpus plus the boundaries around them. Anything this contract
// cannot express stays Unknown rather than being forced into a neighbouring
// answer.
internal static class PatchWriteSemanticsT
{
    // Hoisted per CA1861: a constant array driving a loop must not be rebuilt.
    private static readonly string[] UnprovableOperations =
    [
        "- [ utf8, 0x00001000, hello ]",
        "- [ load, [ [ be32, 0x00001000, 0x60000000 ] ], 0x10 ]",
        "- [ jump, 0x00001000, 0x00002000 ]",
        "- [ alloc, 0x00001000, 0x100 ]",
        "- [ calloc, 0x00001000, 0x100 ]"
    ];

    private static readonly string[] MalformedOperations =
    [
        "- [ byte, 0x00001000, 0x160 ]",
        "- [ be16, 0x00001000, 0x10000 ]",
        "- [ be32, 0x00001000 ]",
        "- [ be32, 0x00001000, 0x60000000, extra ]",
        "- [ be32, notanaddress, 0x60000000 ]",
        "- [ be32, 0x00001000, notavalue ]",
        "- [ be32, 0xFFFFFFFFFFFFFFFF, 0x60000000 ]"
    ];

    public static void Run()
    {
        ExactEquivalenceAcrossOperationShape();
        TrueOverlappingDifferentIsDetectedWithoutTitles();
        PartialIdenticalOverlapIsUnknown();
        RepeatedWritesInsideOneDefinition();
        DisjointWritesDoNotConflict();
        UnsupportedOperationsFailClosed();
        MalformedDefinitionsFailClosed();
        WidthAndEndianness();
        FloatingStoresAndLoadWrappers();
        IntegerRadix();
        CanonicalRunEncoding();
        BoundedAnalysis();

        Console.WriteLine(
            "Patch effective-write normalization and collision classification PASS.");
    }

    // Official BCUS98103 "Infinite Health" is one be32; the NCL lowering of the
    // same NetCheat code is four byte stores. Same address, same bytes.
    private static void ExactEquivalenceAcrossOperationShape()
    {
        var official = Project("- [ be32, 0x002AC77C, 0x60000000 ]");
        var lowered = Project(
            "- [ byte, 0x002AC77C, 0x60 ]",
            "- [ byte, 0x002AC77D, 0x00 ]",
            "- [ byte, 0x002AC77E, 0x00 ]",
            "- [ byte, 0x002AC77F, 0x00 ]");

        Assert(official == lowered,
            "A be32 store and its byte-wise lowering must canonicalize identically.");
        Assert(PatchWriteSemantics.Classify(official, lowered) ==
               PatchWriteRelation.SemanticallyEquivalent,
            "Only a full identical write map is semantic equivalence.");
        Assert(PatchWriteSemantics.Classify(lowered, official) ==
               PatchWriteRelation.SemanticallyEquivalent,
            "Classification must not depend on argument order.");
    }

    // Managed "Ammo Never Decreases" and Official "Infinite Ammo" carry
    // different titles and write different values to the same instruction.
    private static void TrueOverlappingDifferentIsDetectedWithoutTitles()
    {
        var managed = Project(
            "- [ byte, 0x005D14D8, 0x60 ]",
            "- [ byte, 0x005D14D9, 0x00 ]",
            "- [ byte, 0x005D14DA, 0x00 ]",
            "- [ byte, 0x005D14DB, 0x00 ]");
        var official = Project("- [ be32, 0x005D14D8, 0x39290000 ]");

        Assert(PatchWriteSemantics.Classify(managed, official) ==
               PatchWriteRelation.OverlappingDifferent,
            "A differing byte inside the intersection is overlapping-different.");
        Assert(PatchWriteSemantics.Classify(official, managed) ==
               PatchWriteRelation.OverlappingDifferent,
            "Overlapping-different must not depend on argument order.");

        // A single differing byte is enough, even when the rest agrees.
        var nearly = Project(
            "- [ be16, 0x005D14D8, 0x6000 ]",
            "- [ be16, 0x005D14DA, 0x0001 ]");
        Assert(PatchWriteSemantics.Classify(managed, nearly) ==
               PatchWriteRelation.OverlappingDifferent,
            "One differing byte in an otherwise identical range must still conflict.");
    }

    // A covers 0x1000-0x1003, B covers 0x1002-0x1003, intersecting bytes equal.
    // Not equivalent, not disjoint, and nothing conflicts: unproven.
    private static void PartialIdenticalOverlapIsUnknown()
    {
        var wide = Project("- [ be32, 0x00001000, 0x11223344 ]");
        var narrow = Project("- [ be16, 0x00001002, 0x3344 ]");

        Assert(PatchWriteSemantics.Classify(wide, narrow) ==
               PatchWriteRelation.Unknown,
            "A subset whose shared bytes all agree must stay unproven.");
        Assert(PatchWriteSemantics.Classify(narrow, wide) ==
               PatchWriteRelation.Unknown,
            "Partial identical overlap must be unproven in both directions.");

        // The same shape one byte wider on the other side.
        var official = Project("- [ be32, 0x005D14D8, 0x39290000 ]");
        var superset = Project(
            "- [ be32, 0x005D14D8, 0x39290000 ]",
            "- [ byte, 0x005D14DC, 0x01 ]");
        Assert(PatchWriteSemantics.Classify(official, superset) ==
               PatchWriteRelation.Unknown,
            "A superset of agreeing writes is neither equivalent nor a conflict.");
    }

    private static void RepeatedWritesInsideOneDefinition()
    {
        var repeated = Project(
            "- [ byte, 0x00001000, 0x11 ]",
            "- [ byte, 0x00001000, 0x11 ]");
        Assert(repeated == Project("- [ byte, 0x00001000, 0x11 ]"),
            "Repeating a store with the same value merges and stays provable.");
        Assert(PatchWriteSemantics.Classify(
                   repeated,
                   Project("- [ byte, 0x00001000, 0x11 ]")) ==
               PatchWriteRelation.SemanticallyEquivalent,
            "A redundant repeat must not change what the definition means.");

        // Overlapping stores that agree on the shared byte are still provable:
        // 0x1001 is written twice, both times as 0x22.
        Assert(Project(
                   "- [ be16, 0x00001000, 0x1122 ]",
                   "- [ be16, 0x00001001, 0x2233 ]") ==
               Project(
                   "- [ byte, 0x00001000, 0x11 ]",
                   "- [ byte, 0x00001001, 0x22 ]",
                   "- [ byte, 0x00001002, 0x33 ]"),
            "Agreeing overlapped stores must reduce to one contiguous run.");

        var conflicting = Project(
            "- [ byte, 0x00001000, 0x11 ]",
            "- [ byte, 0x00001000, 0x22 ]");
        Assert(PatchWriteSemantics.Classify(
                   conflicting,
                   Project("- [ byte, 0x00001000, 0x22 ]")) ==
               PatchWriteRelation.Unknown,
            "A definition that stores two values at one address must fail closed.");
        Assert(PatchWriteSemantics.Classify(
                   conflicting,
                   Project("- [ byte, 0x00001000, 0x11 ]")) ==
               PatchWriteRelation.Unknown,
            "Order of a self-overwriting definition must never be assumed.");
    }

    private static void DisjointWritesDoNotConflict()
    {
        var health = Project("- [ be32, 0x002AC77C, 0x60000000 ]");
        var ammo = Project("- [ be32, 0x005D14D8, 0x39290000 ]");

        Assert(PatchWriteSemantics.Classify(health, ammo) ==
               PatchWriteRelation.Disjoint,
            "Definitions touching separate addresses must not conflict.");

        Assert(PatchWriteSemantics.Classify(
                   Project("- [ be32, 0x00001000, 0x11223344 ]"),
                   Project("- [ be32, 0x00001004, 0x55667788 ]")) ==
               PatchWriteRelation.Disjoint,
            "Ranges that merely touch must remain disjoint.");
    }

    private static void UnsupportedOperationsFailClosed()
    {
        var supported = Project("- [ be32, 0x00001000, 0x60000000 ]");

        foreach (var unsupported in UnprovableOperations)
        {
            var projected = Project(unsupported);
            Assert(PatchWriteSemantics.Classify(projected, supported) ==
                   PatchWriteRelation.Unknown,
                $"An operation with unproven effect must fail closed: {unsupported}");
            Assert(PatchWriteSemantics.Classify(supported, projected) ==
                   PatchWriteRelation.Unknown,
                $"Unknown must survive argument order: {unsupported}");
        }

        // One unsupported operation poisons the whole definition; supported
        // siblings are never analysed in isolation.
        Assert(PatchWriteSemantics.Classify(
                   Project(
                       "- [ be32, 0x00001000, 0x60000000 ]",
                       "- [ utf8, 0x00002000, hello ]"),
                   supported) ==
               PatchWriteRelation.Unknown,
            "A single unsupported operation must make the whole definition unknown.");

        Assert(PatchWriteSemantics.Project(null) is null,
            "A definition with no Patch node projects to nothing.");
        Assert(PatchWriteSemantics.Classify(null, supported) ==
               PatchWriteRelation.Unknown,
            "A missing projection must fail closed.");
    }

    private static void MalformedDefinitionsFailClosed()
    {
        var supported = Project("- [ be32, 0x00001000, 0x60000000 ]");

        foreach (var malformed in MalformedOperations)
        {
            Assert(PatchWriteSemantics.Classify(Project(malformed), supported) ==
                   PatchWriteRelation.Unknown,
                $"A malformed operation must fail closed: {malformed}");
        }
    }

    private static void WidthAndEndianness()
    {
        Assert(Project("- [ be16, 0x00001000, 0x1234 ]") ==
               Project(
                   "- [ byte, 0x00001000, 0x12 ]",
                   "- [ byte, 0x00001001, 0x34 ]"),
            "be16 must expand most significant byte first.");
        Assert(Project("- [ le16, 0x00001000, 0x1234 ]") ==
               Project(
                   "- [ byte, 0x00001000, 0x34 ]",
                   "- [ byte, 0x00001001, 0x12 ]"),
            "le16 must expand least significant byte first.");
        Assert(Project("- [ be64, 0x00001000, 0x1122334455667788 ]") ==
               Project(
                   "- [ be32, 0x00001000, 0x11223344 ]",
                   "- [ be32, 0x00001004, 0x55667788 ]"),
            "be64 must equal the two be32 halves that cover it.");
        Assert(PatchWriteSemantics.Classify(
                   Project("- [ be16, 0x00001000, 0x1234 ]"),
                   Project("- [ le16, 0x00001000, 0x1234 ]")) ==
               PatchWriteRelation.OverlappingDifferent,
            "Endianness must change the effective bytes.");
    }

    private static void FloatingStoresAndLoadWrappers()
    {
        var direct = Project(
            "- [ bef32, 0x003a52b4, 1000 ]",
            "- [ bef32, 0x002d024c, 6667 ]");
        var wrapped = Project(
            "- [ load, [ [ bef32, 0x003a52b4, 1000 ], [ bef32, 0x002d024c, 6667 ] ] ]");

        Assert(PatchWriteSemantics.IsProven(direct) && direct == wrapped,
            "A modifier-free load wrapper must preserve the exact effective floating-point writes.");
        Assert(PatchWriteSemantics.Classify(direct, wrapped) ==
               PatchWriteRelation.SemanticallyEquivalent,
            "Direct and load-wrapped God of War floating-point stores must be proven equivalent.");
        Assert(Project("- [ bef32, 0x00001000, 1.0 ]") ==
               Project("- [ be32, 0x00001000, 0x3f800000 ]"),
            "bef32 must reduce a finite value to its big-endian IEEE-754 bytes.");
        Assert(Project("- [ bef64, 0x00001000, 1.0 ]") ==
               Project("- [ be64, 0x00001000, 0x3ff0000000000000 ]"),
            "bef64 must reduce a finite value to its big-endian IEEE-754 bytes.");
        Assert(Project("- [ lef32, 0x00001000, 1.0 ]") ==
               Project("- [ le32, 0x00001000, 0x3f800000 ]"),
            "lef32 must reduce a finite value to its little-endian IEEE-754 bytes.");
        Assert(Project("- [ lef64, 0x00001000, 1.0 ]") ==
               Project("- [ le64, 0x00001000, 0x3ff0000000000000 ]"),
            "lef64 must reduce a finite value to its little-endian IEEE-754 bytes.");
        Assert(Project("- [ bef32, 0x00001000, NaN ]") == "U" &&
               Project("- [ bef64, 0x00001000, Infinity ]") == "U",
            "Non-finite floating-point stores must remain unproven.");
        Assert(Project("- [ lef32, 0xFFFFFFFFFFFFFFFF, 1.0 ]") == "U" &&
               Project("- [ lef64, 0xFFFFFFFFFFFFFFFF, 1.0 ]") == "U",
            "Floating-point stores whose exact width overflows the address space must remain unproven.");
    }

    internal static void IntegerRadix()
    {
        var decimalWrite = Project("- [ byte, 0x1000, 10 ]");
        var hexWrite = Project("- [ byte, 0x1000, 0x10 ]");
        Assert(decimalWrite == "C:1000:0a" && hexWrite == "C:1000:10",
            "Unprefixed decimal 10 must write 0A; hexadecimal 0x10 must write 10.");
        Assert(PatchWriteSemantics.Classify(decimalWrite, hexWrite) == PatchWriteRelation.OverlappingDifferent,
            "Different integer radices must not collapse to a proven equivalent write.");
        Assert(Project("- [ byte, 4096, 10 ]") == Project("- [ byte, 0x1000, 0x0a ]"),
            "Proven decimal and hexadecimal forms of the same address/value must agree.");
        foreach (var literal in new[] { "010", "FF", "-1", "+10", "0b10", "1_0", "18446744073709551616" })
        {
            Assert(Project($"- [ be64, 0x1000, '{literal}' ]") == "U",
                "Unsupported or overflowing integer literal must remain unproven: " + literal);
        }
    }

    private static void CanonicalRunEncoding()
    {
        Assert(Project("- [ be32, 0x002AC77C, 0x60000000 ]") ==
               "C:2ac77c:60000000",
            "A contiguous span must canonicalize as one run, not four byte pairs.");
        Assert(Project(
                   "- [ byte, 0x00001000, 0x11 ]",
                   "- [ byte, 0x00002000, 0x22 ]") ==
               "C:1000:11;2000:22",
            "Separate spans must canonicalize as separate ascending runs.");
        Assert(Project(
                   "- [ byte, 0x00002000, 0x22 ]",
                   "- [ byte, 0x00001000, 0x11 ]") ==
               "C:1000:11;2000:22",
            "Canonical order must not depend on the order operations were written.");
    }

    private static void BoundedAnalysis()
    {
        var operations = new string[PatchWriteSemantics.MaxWrittenBytes / 4 + 1];

        for (var index = 0; index < operations.Length; index++)
        {
            var address = 0x1000 + index * 4;
            operations[index] =
                "- [ be32, 0x" +
                address.ToString("X8", CultureInfo.InvariantCulture) +
                ", 0x60000000 ]";
        }

        Assert(PatchWriteSemantics.Classify(
                   Project(operations),
                   Project("- [ be32, 0x00001000, 0x60000000 ]")) ==
               PatchWriteRelation.Unknown,
            "A definition beyond the analysis bound must fail closed rather than allocate.");
    }

    private static string? Project(params string[] operations)
    {
        var document = "Patch:\n" + string.Join("\n", operations) + "\n";
        var yaml = new YamlStream();
        yaml.Load(new StringReader(document));
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var patchNode = root.Children
            .Where(pair => pair.Key is YamlScalarNode { Value: "Patch" })
            .Select(pair => pair.Value)
            .Single();
        return PatchWriteSemantics.Project(patchNode);
    }
}
