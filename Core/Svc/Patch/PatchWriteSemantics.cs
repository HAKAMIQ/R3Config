using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using YamlDotNet.RepresentationModel;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// How two RPCS3 patch definitions relate once their operations are reduced to
// the bytes they actually write. Structure is not meaning: a single be32 and
// four byte stores can land the same value at the same address.
public enum PatchWriteRelation
{
    // The relationship cannot be established: an operation whose effect is not
    // provable, a definition that is not fully determined, or an overlap whose
    // safety this four-value contract cannot express.
    Unknown,
    Disjoint,
    SemanticallyEquivalent,
    OverlappingDifferent
}

// Projects an RPCS3 "Patch:" node onto the concrete bytes it writes, and
// compares two projections. The projection is carried beside DefPrint on the
// catalog entry and is produced by the same reader, so every source that flows
// through the catalog gets one without a second parse of the document.
//
// Integer and finite IEEE-754 stores are normalized. A modifier-free load is
// only a structural wrapper and is expanded recursively. A load with an address
// modifier, string, allocation or control-flow operation has no fixed byte
// image here, so any definition containing one projects as incomplete and every
// comparison involving it answers Unknown. Widening this table to accept more
// comparisons would trade a proof for a guess.
public static class PatchWriteSemantics
{
    // A hostile or generated definition must not be able to make this analysis
    // allocate without bound; beyond the cap the projection is incomplete.
    public const int MaxWrittenBytes = 4096;

    private const string CompletePrefix = "C:";

    private const string IncompleteMarker = "U";

    private static readonly Dictionary<string, (int Width, bool LittleEndian)> IntegerStores =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["byte"] = (1, false),
            ["be16"] = (2, false),
            ["be32"] = (4, false),
            ["be64"] = (8, false),
            ["le16"] = (2, true),
            ["le32"] = (4, true),
            ["le64"] = (8, true)
        };

    private static readonly Dictionary<string, (int Width, bool LittleEndian)> FloatingStores =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bef32"] = (4, false),
            ["bef64"] = (8, false),
            ["lef32"] = (4, true),
            ["lef64"] = (8, true)
        };

    // Canonical form: "C:" then contiguous runs "address:hexbytes" joined by
    // ';', addresses ascending. One run per contiguous span rather than one
    // entry per byte, which is what a be32 store actually is.
    public static string? Project(YamlNode? patchNode)
    {
        if (patchNode is null)
        {
            return null;
        }

        if (patchNode is not YamlSequenceNode operations)
        {
            return IncompleteMarker;
        }

        var written = new SortedDictionary<ulong, byte>();

        foreach (var operation in operations.Children)
        {
            if (!TryApply(operation, written))
            {
                return IncompleteMarker;
            }
        }

        return Canonical(written);
    }

    // The same projection taken from concrete writes instead of a YAML node.
    // The NCL pipeline lowers a cheat to exactly this shape, so a definition
    // that never became YAML is measured by the identical rules rather than by
    // a second comparator written for it. It carries its own name rather than
    // overloading Project: a null literal would otherwise be ambiguous between
    // the two, which is a compile break waiting at every future call site.
    public static string? ProjectWrites(
        IEnumerable<(ulong Address, IReadOnlyList<byte> Bytes)>? writes)
    {
        if (writes is null)
        {
            return null;
        }

        var written = new SortedDictionary<ulong, byte>();

        foreach (var (address, bytes) in writes)
        {
            if (bytes is null ||
                bytes.Count == 0 ||
                address > ulong.MaxValue - (ulong)bytes.Count ||
                !TryAdd(written, address, bytes))
            {
                return IncompleteMarker;
            }
        }

        return Canonical(written);
    }

    // True when a projection pins every byte the definition writes. An absent
    // or incomplete projection proves nothing, so it must never be used to
    // declare two definitions equivalent - only to keep them apart.
    public static bool IsProven(string? print)
    {
        return print is not null &&
               print.StartsWith(CompletePrefix, StringComparison.Ordinal);
    }

    public static PatchWriteRelation Classify(string? left, string? right)
    {
        var leftWrites = Parse(left);
        var rightWrites = Parse(right);

        if (leftWrites is null || rightWrites is null)
        {
            return PatchWriteRelation.Unknown;
        }

        var shared = leftWrites.Keys
            .Where(rightWrites.ContainsKey)
            .ToArray();

        if (shared.Length == 0)
        {
            return PatchWriteRelation.Disjoint;
        }

        if (shared.Any(address => leftWrites[address] != rightWrites[address]))
        {
            return PatchWriteRelation.OverlappingDifferent;
        }

        // Every shared byte agrees. Only an exact match is equivalence; a strict
        // subset is neither equivalent nor a conflict, and this contract has no
        // value for it, so it stays unproven rather than being forced into one.
        return shared.Length == leftWrites.Count &&
               shared.Length == rightWrites.Count
            ? PatchWriteRelation.SemanticallyEquivalent
            : PatchWriteRelation.Unknown;
    }

    private static string Canonical(SortedDictionary<ulong, byte> written)
    {
        var canonical = new StringBuilder(CompletePrefix);
        var first = true;
        ulong? runStart = null;
        ulong previous = 0;
        var run = new StringBuilder();

        foreach (var pair in written)
        {
            if (runStart is not null &&
                pair.Key == previous + 1)
            {
                run.Append(pair.Value.ToString("x2", CultureInfo.InvariantCulture));
                previous = pair.Key;
                continue;
            }

            if (runStart is not null)
            {
                AppendRun(canonical, ref first, runStart.Value, run);
            }

            runStart = pair.Key;
            previous = pair.Key;
            run.Clear();
            run.Append(pair.Value.ToString("x2", CultureInfo.InvariantCulture));
        }

        if (runStart is not null)
        {
            AppendRun(canonical, ref first, runStart.Value, run);
        }

        return canonical.ToString();
    }

    private static void AppendRun(
        StringBuilder canonical,
        ref bool first,
        ulong address,
        StringBuilder bytes)
    {
        if (!first)
        {
            canonical.Append(';');
        }

        canonical
            .Append(address.ToString("x", CultureInfo.InvariantCulture))
            .Append(':')
            .Append(bytes);
        first = false;
    }

    private static bool TryApply(
        YamlNode operation,
        SortedDictionary<ulong, byte> written)
    {
        if (operation is not YamlSequenceNode fields ||
            fields.Children.Count == 0 ||
            fields.Children[0] is not YamlScalarNode opcodeNode ||
            opcodeNode.Value is not { } opcode)
        {
            return false;
        }

        opcode = opcode.Trim();
        if (opcode.Equals("load", StringComparison.OrdinalIgnoreCase))
        {
            if (fields.Children.Count != 2 ||
                fields.Children[1] is not YamlSequenceNode loaded)
            {
                return false;
            }

            foreach (var loadedOperation in loaded.Children)
            {
                if (!TryApply(loadedOperation, written))
                {
                    return false;
                }
            }

            return true;
        }

        if (fields.Children.Count != 3 ||
            fields.Children[1] is not YamlScalarNode addressNode ||
            fields.Children[2] is not YamlScalarNode valueNode ||
            !TryInteger(addressNode.Value, out var address))
        {
            return false;
        }

        if (IntegerStores.TryGetValue(opcode, out var integerStore))
        {
            return TryInteger(valueNode.Value, out var value) &&
                   TryApplyInteger(written, address, value, integerStore);
        }

        return FloatingStores.TryGetValue(opcode, out var floatingStore) &&
               TryApplyFloating(
                   written,
                   address,
                   valueNode.Value,
                   floatingStore);
    }

    private static bool TryApplyInteger(
        SortedDictionary<ulong, byte> written,
        ulong address,
        ulong value,
        (int Width, bool LittleEndian) store)
    {

        if (store.Width < 8 &&
            value >= 1UL << (store.Width * 8))
        {
            // The literal does not fit the width it was declared with; the
            // definition is malformed rather than merely unsupported.
            return false;
        }

        if (address > ulong.MaxValue - (ulong)store.Width)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[8];

        for (var index = 0; index < store.Width; index++)
        {
            var shift = store.LittleEndian
                ? index * 8
                : (store.Width - 1 - index) * 8;
            bytes[index] = (byte)(value >> shift & 0xFF);
        }

        return TryAdd(written, address, bytes[..store.Width].ToArray());
    }

    private static bool TryApplyFloating(
        SortedDictionary<ulong, byte> written,
        ulong address,
        string? text,
        (int Width, bool LittleEndian) store)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            address > ulong.MaxValue - (ulong)store.Width)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[8];
        if (store.Width == 4)
        {
            if (!float.TryParse(
                    text.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                !float.IsFinite(value))
            {
                return false;
            }

            var bits = BitConverter.SingleToUInt32Bits(value);
            if (store.LittleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, bits);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(bytes, bits);
            }
        }
        else
        {
            if (!double.TryParse(
                    text.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                !double.IsFinite(value))
            {
                return false;
            }

            var bits = BitConverter.DoubleToUInt64Bits(value);
            if (store.LittleEndian)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, bits);
            }
            else
            {
                BinaryPrimitives.WriteUInt64BigEndian(bytes, bits);
            }
        }

        return TryAdd(written, address, bytes[..store.Width].ToArray());
    }

    // The single accumulator both entry points share. Keeping it in one place
    // is what makes a YAML definition and a lowered NCL cheat comparable at
    // all: they are reduced by the same rules or they are not comparable.
    private static bool TryAdd(
        SortedDictionary<ulong, byte> written,
        ulong address,
        IReadOnlyList<byte> bytes)
    {
        for (var index = 0; index < bytes.Count; index++)
        {
            var target = address + (ulong)index;

            if (written.TryGetValue(target, out var existing))
            {
                // Repeating a store with the same value is redundant and merges
                // safely. Repeating it with a different value makes the result
                // depend on application order, which nothing in this project
                // establishes for RPCS3, so the definition stays unproven.
                if (existing != bytes[index])
                {
                    return false;
                }

                continue;
            }

            if (written.Count >= MaxWrittenBytes)
            {
                return false;
            }

            written[target] = bytes[index];
        }

        return true;
    }

    private static SortedDictionary<ulong, byte>? Parse(string? print)
    {
        if (string.IsNullOrWhiteSpace(print) ||
            !print.StartsWith(CompletePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var written = new SortedDictionary<ulong, byte>();
        var body = print[CompletePrefix.Length..];

        if (body.Length == 0)
        {
            return written;
        }

        foreach (var item in body.Split(';'))
        {
            var separator = item.IndexOf(':', StringComparison.Ordinal);

            if (separator <= 0 ||
                !ulong.TryParse(
                    item[..separator],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var address))
            {
                return null;
            }

            var hex = item[(separator + 1)..];

            if (hex.Length == 0 ||
                hex.Length % 2 != 0 ||
                hex.Length / 2 > MaxWrittenBytes)
            {
                return null;
            }

            for (var index = 0; index < hex.Length; index += 2)
            {
                if (!byte.TryParse(
                        hex.AsSpan(index, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var value) ||
                    address > ulong.MaxValue - (ulong)(index / 2))
                {
                    return null;
                }

                written[address + (ulong)(index / 2)] = value;
            }
        }

        return written;
    }

    private static bool TryInteger(string? text, out ulong value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();

        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[2..];
            return candidate.Length is > 0 and <= 16 &&
                   candidate.All(Uri.IsHexDigit) &&
                   ulong.TryParse(candidate, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        // Proven subset: decimal digits without a leading zero, or zero itself.
        // Signs, octal-like leading zeros, separators and other literal forms
        // remain unproven rather than acquiring guessed RPCS3 semantics.
        return candidate.Length > 0 &&
               candidate.Length <= 20 &&
               (candidate.Length == 1 || candidate[0] != '0') &&
               candidate.All(char.IsAsciiDigit) &&
               ulong.TryParse(
                   candidate,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out value);
    }
}

#pragma warning restore IDE0130
