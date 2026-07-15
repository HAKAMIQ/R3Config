using System;
using System.Buffers.Binary;
using System.Linq;

namespace R3Config.Mdl;

public enum HashKind
{
    Ppu,
    Spu,
    Prx,
    Ovl,
    Unknown
}

public enum HashEvi
{
    None,
    Meta,
    PlainLog,
    ZipLog,
    Cache
}

public static class ExeHash
{
    private const string Alphabet =
        "0123456789ACEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static bool TryNorm(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (TryNorm2(candidate, out normalized))
        {
            return true;
        }

        if (candidate.Length != 28 ||
            !TrySha1(candidate, out var bytes) ||
            bytes.All(valueByte => valueByte == 0))
        {
            return false;
        }

        normalized = Convert.ToHexString(bytes).ToLowerInvariant();
        return true;
    }

    public static bool TryNorm2(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length != 40 ||
            !candidate.All(Uri.IsHexDigit) ||
            candidate.All(character => character == '0'))
        {
            return false;
        }

        normalized = candidate.ToLowerInvariant();
        return true;
    }

    private static bool TrySha1(string value, out byte[] bytes)
    {
        bytes = [];

        try
        {
            var decoded = new byte[20];
            var outputOffset = 0;
            Span<byte> blockBytes = stackalloc byte[8];

            for (var inputOffset = 0; inputOffset < value.Length; inputOffset += 11)
            {
                var blockLength = Math.Min(11, value.Length - inputOffset);
                var outputLength = blockLength == 11
                    ? 8
                    : TailByte(blockLength);

                if (outputLength == 0)
                {
                    return false;
                }

                ulong blockValue = 0;
                ulong multiplier = 0;

                for (var position = 10; position >= 0; position--)
                {
                    multiplier = multiplier == 0
                        ? 1
                        : checked(multiplier * 57);

                    if (position >= blockLength)
                    {
                        continue;
                    }

                    var digit = Alphabet.IndexOf(value[inputOffset + position]);
                    if (digit < 0)
                    {
                        return false;
                    }

                    blockValue = checked(
                        blockValue +
                        checked((ulong)digit * multiplier));
                }

                blockBytes.Clear();
                BinaryPrimitives.WriteUInt64BigEndian(blockBytes, blockValue);
                blockBytes[..outputLength].CopyTo(
                    decoded.AsSpan(outputOffset, outputLength));

                outputOffset += outputLength;
            }

            if (outputOffset != decoded.Length)
            {
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static int TailByte(int encodedLength)
    {
        return encodedLength switch
        {
            2 => 1,
            3 => 2,
            5 => 3,
            6 => 4,
            7 => 5,
            9 => 6,
            10 => 7,
            _ => 0
        };
    }
}

public enum PatSrc
{
    Official,
    Imported,
    Can,
    Cht,
    ManCht,
    TitlePat,
    Unknown
}

public enum PatRisk
{
    SafeFix,
    Fix,
    Perf,
    Fps60,
    Cheat,
    Exp,
    Canary,
    Unknown
}

public enum MatStat
{
    Match,
    Unknown,
    VerUnk,
    VerBad,
    HashBad,
    SerBad,
    NoMeta
}

public sealed record R3PatTgt(
    string Title,
    string Serial,
    IReadOnlyList<string> Versions)
{
    public string Display =>
        Versions.Count == 0
            ? "Any"
            : string.Join(", ", Versions);
}

public sealed record Rpcs3PatchEntry(
    string Title,
    HashKind HashKind,
    string Hash,
    IReadOnlyList<R3PatTgt> GameTgt,
    string PatType,
    PatRisk Risk,
    string? Notes,
    string? Author,
    string? PatVer,
    string? Group,
    PatSrc Source = PatSrc.Unknown,
    string? SrcPath = null,
    IReadOnlyList<string>? DepList = null,
    string? DefPrint = null,
    string? KeySer = null)
{
    public IReadOnlyList<string> Patches =>
        DepList ?? [];

    public string? NormHash =>
        Mdl.RunKey.TryNorm(
            HashKind,
            Hash,
            out var normalized)
            ? normalized
            : null;

    public string RunKey =>
        Mdl.RunKey.Build(
            HashKind,
            Hash,
            KeySer);

    public IReadOnlyList<string> Serials =>
    [
        .. GameTgt
            .Select(target => target.Serial)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
    ];

    public IReadOnlyList<string> GameVer =>
    [
        .. GameTgt
            .SelectMany(target => target.Versions)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
    ];
}

public sealed record PatCat(
    string? Version,
    IReadOnlyList<Rpcs3PatchEntry> Entries,
    IReadOnlyList<PatDiag>? Diags = null)
{
    public IReadOnlyList<PatDiag> Diag =>
        Diags ?? [];
}

public sealed record PatDiag(
    string Code,
    string Message,
    PatSrc KeepSrc,
    PatSrc DropSrc);

public sealed record PatRead(
    bool IsOk,
    PatCat Catalog,
    string? Error)
{
    public static PatRead Success(
        PatCat catalog,
        string? warning = null)
    {
        return new PatRead(true, catalog, warning);
    }

    public static PatRead Failure(string error)
    {
        return new PatRead(
            false,
            new PatCat(null, []),
            error);
    }
}

public sealed record PatMatch(
    Rpcs3PatchEntry Patch,
    MatStat Status,
    string? MatSer,
    string? MatVer,
    bool HasMeta,
    string Reason,
    string? TgtName = null,
    string? TgtSer = null,
    string? TgtVer = null,
    IReadOnlyList<string>? DetHash = null,
    string? HashSrc = null,
    string? HashMsg = null,
    HashEvi HashEvi = HashEvi.None,
    DateTime? HashTime = null,
    PatchCompatibilityDecision? Cmp = null)
{
    public bool IsMatch =>
        Status == MatStat.Match;

    public IReadOnlyList<string> Hashes =>
        DetHash ?? [];
}

public sealed record PatChg(
    string HashKey,
    string PatTitle,
    string GameName,
    string Serial,
    string Version,
    PatRisk Risk);

public sealed record PatchApplyPlan(
    string PatCfg,
    IReadOnlyList<PatChg> Changes)
{
    public bool HasChg => Changes.Count > 0;
}

public enum PrepKind
{
    PatCat,
    PatCfg,
    JsonMeta
}

public sealed record PatchPreparedFile(
    string TgtPath,
    string Content,
    PrepKind Kind,
    PatSrc CatSrc = PatSrc.Unknown,
    int? EntNum = null,
    bool DelTgt = false);

public sealed record PatRes(
    BackupManifest Backup,
    IReadOnlyList<PatChg> ChgList,
    IReadOnlyList<string>? PrepMsgs = null)
{
    public IReadOnlyList<string> Messages =>
        PrepMsgs ?? [];
}

public sealed record HashRes(
    string? PpuHash,
    string? SpuHash,
    HashEvi EviKind,
    string? SrcPath,
    DateTime? SrcTime,
    bool HasHash,
    string Message,
    IReadOnlyList<string>? PpuRaw = null,
    IReadOnlyList<string>? SpuRaw = null,
    IReadOnlyList<string>? PrxRaw = null,
    IReadOnlyList<string>? OvlRaw = null,
    IReadOnlyList<string>? AuthPpu = null)
{
    public IReadOnlyList<string> Hashes =>
        PpuRaw ??
        (PpuHash is null
            ? []
            : [PpuHash]);

    public IReadOnlyList<string> SpuList =>
        SpuRaw ??
        (SpuHash is null
            ? []
            : [SpuHash]);

    public IReadOnlyList<string> PrxList =>
        PrxRaw ?? [];

    public IReadOnlyList<string> OvlList =>
        OvlRaw ?? [];

    public IReadOnlyList<string> PpuList =>
        AuthPpu ?? [];

    public static HashRes Unknown(string message)
    {
        return new HashRes(
            PpuHash: null,
            SpuHash: null,
            EviKind: HashEvi.None,
            SrcPath: null,
            SrcTime: null,
            HasHash: false,
            Message: message);
    }
}
