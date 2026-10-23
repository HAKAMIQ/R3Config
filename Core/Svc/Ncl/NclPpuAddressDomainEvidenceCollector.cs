using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public static partial class NclPpuAddressDomainEvidenceCollector
{
    private const long MaxLogBytes = 64L * 1024 * 1024;
    private const int ReadBufferChars = 8192;

    public static NclPpuDomainEvidenceResult CollectFile(
        string logPath,
        string serial,
        string runKey)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return Failure(
                NclPpuDomainEvidenceCode.InvalidInput,
                "RPCS3 log path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(logPath);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return Failure(
                NclPpuDomainEvidenceCode.InvalidInput,
                ex.Message);
        }

        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length > MaxLogBytes)
            {
                return Failure(
                    NclPpuDomainEvidenceCode.LogTooLarge,
                    $"RPCS3 log exceeds the {MaxLogBytes / (1024 * 1024)} MiB evidence limit.",
                    fullPath);
            }

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: ReadBufferChars,
                leaveOpen: false);

            var text = new StringBuilder(
                (int)Math.Min(stream.Length, 1024 * 1024));
            var buffer = new char[ReadBufferChars];

            while (true)
            {
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                if ((long)text.Length + read > MaxLogBytes)
                {
                    return Failure(
                        NclPpuDomainEvidenceCode.LogTooLarge,
                        $"Expanded RPCS3 log text exceeds the {MaxLogBytes / (1024 * 1024)} MiB evidence limit.",
                        fullPath);
                }

                text.Append(buffer, 0, read);
            }

            return CollectText(
                text.ToString(),
                serial,
                runKey,
                fullPath);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException)
        {
            return Failure(
                NclPpuDomainEvidenceCode.ReadFailed,
                ex.Message,
                fullPath);
        }
    }

    public static NclPpuDomainEvidenceResult CollectText(
        string logText,
        string serial,
        string runKey,
        string? sourcePath = null)
    {
        if (string.IsNullOrEmpty(logText) ||
            string.IsNullOrWhiteSpace(serial) ||
            string.IsNullOrWhiteSpace(runKey))
        {
            return Failure(
                NclPpuDomainEvidenceCode.InvalidInput,
                "Log text, serial, and exact RPCS3 run key are required.",
                sourcePath);
        }

        var normalizedSerial = serial.Trim().ToUpperInvariant();
        if (!SerialValueRegex().IsMatch(normalizedSerial))
        {
            return Failure(
                NclPpuDomainEvidenceCode.InvalidInput,
                "RPCS3 serial must be exactly nine alphanumeric characters.",
                sourcePath);
        }

        if (!RunKey.TryParse(runKey, out var parsedKey))
        {
            return Failure(
                NclPpuDomainEvidenceCode.InvalidRunKey,
                "RPCS3 run key is invalid.",
                sourcePath);
        }

        var canonicalRunKey = parsedKey.RunKey;
        var keyMatch = RuntimePpuKeyRegex().Match(canonicalRunKey);
        if (!keyMatch.Success)
        {
            return Failure(
                NclPpuDomainEvidenceCode.NotPpuRunKey,
                "Runtime PPU evidence requires an exact 40-hex PPU run key.",
                sourcePath);
        }

        var scopedSerial = keyMatch.Groups["serial"].Value;
        if (scopedSerial.Length > 0 &&
            !scopedSerial.Equals(
                normalizedSerial,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                NclPpuDomainEvidenceCode.SerialScopeMismatch,
                "The serial-scoped RPCS3 run key does not match the requested game serial.",
                sourcePath);
        }

        var targetHash = keyMatch.Groups["hash"].Value;
        var lines = logText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var sessionStart = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            var serialMatch = SerialLineRegex().Match(lines[index]);
            if (serialMatch.Success &&
                serialMatch.Groups["serial"].Value.Equals(
                    normalizedSerial,
                    StringComparison.OrdinalIgnoreCase))
            {
                sessionStart = index;
            }
        }

        if (sessionStart < 0)
        {
            return Failure(
                NclPpuDomainEvidenceCode.SessionNotFound,
                "The RPCS3 log contains no matching SYS: Serial session.",
                sourcePath);
        }

        var sessionEnd = lines.Length;
        for (var index = sessionStart + 1; index < lines.Length; index++)
        {
            if (SerialLineRegex().IsMatch(lines[index]))
            {
                sessionEnd = index;
                break;
            }
        }

        var pending = new List<SegmentEvidence>();
        List<SegmentEvidence>? proven = null;
        var moduleSectionListSeen = false;

        for (var index = sessionStart + 1; index < sessionEnd; index++)
        {
            var line = lines[index];

            if (LooksLikePpuLoaderSegment(line))
            {
                // Every RPCS3 loader entry point emits its complete
                // "** Segment:" run strictly before its "** Section:" run:
                //   ppu_load_prx      PPUModule.cpp:1614 -> :1697
                //   ppu_load_exec     PPUModule.cpp:2193 -> :2292
                //   ppu_load_overlay  PPUModule.cpp:2909 -> :2985
                //   ppu_load_rel_exec PPUModule.cpp:3259 (sections only)
                // A segment line that follows a section list therefore always
                // opens a different module load. This is the only boundary a
                // direct virtual-load overlay (PPUThread.cpp:4516) cannot
                // bypass: ppu_load_overlay logs no completion marker of its
                // own and never reaches sys_overlay.cpp:72 or :88.
                if (moduleSectionListSeen)
                {
                    pending.Clear();
                    moduleSectionListSeen = false;
                }

                if (!LooksLikePpuLoadSegment(line))
                {
                    continue;
                }

                var segmentMatch = PpuLoadSegmentRegex().Match(line);
                if (!segmentMatch.Success ||
                    !TryHex(
                        segmentMatch.Groups["type"].Value,
                        out var type) ||
                    !TryHex(
                        segmentMatch.Groups["vaddr"].Value,
                        out var virtualAddress) ||
                    !TryHex(
                        segmentMatch.Groups["filesz"].Value,
                        out var fileSize))
                {
                    return Failure(
                        NclPpuDomainEvidenceCode.InvalidSegment,
                        $"Malformed PPU PT_LOAD evidence at log line {index + 1}.",
                        sourcePath);
                }

                if (type == 1 && fileSize > 0)
                {
                    pending.Add(
                        new SegmentEvidence(
                            virtualAddress,
                            fileSize,
                            index + 1));
                }

                continue;
            }

            if (LooksLikePpuLoaderSection(line))
            {
                moduleSectionListSeen = true;
                continue;
            }

            // PRX, overlay and embedded SPU modules log their own PT_LOAD
            // segments on the same ppu_loader channel as the main executable,
            // but they are relocated independently and are never part of the
            // main executable address domain. Any foreign module boundary
            // therefore discards pending evidence so a later matching PPU
            // executable hash cannot inherit another module's segments.
            if (LooksLikeForeignModuleBoundary(line))
            {
                pending.Clear();
                moduleSectionListSeen = false;
                continue;
            }

            var hashMatch = PpuHashRegex().Match(line);
            if (!hashMatch.Success)
            {
                continue;
            }

            var observedHash = hashMatch.Groups["hash"].Value;
            if (!observedHash.Equals(
                    targetHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                pending.Clear();
                moduleSectionListSeen = false;
                continue;
            }

            if (proven is null)
            {
                if (pending.Count == 0)
                {
                    return Failure(
                        NclPpuDomainEvidenceCode.NoLoadSegments,
                        "The target PPU hash was found without preceding file-backed PT_LOAD evidence.",
                        sourcePath);
                }

                proven = [.. pending];
            }
            else if (pending.Count > 0 &&
                     !SameSegments(proven, pending))
            {
                // A second load of the same executable hash inside one
                // session must reproduce the same file-backed layout. Picking
                // either run would let log order decide the address domain.
                return Failure(
                    NclPpuDomainEvidenceCode.AmbiguousSegment,
                    "The requested PPU hash appears more than once in the newest matching RPCS3 session with different PT_LOAD evidence.",
                    sourcePath);
            }

            pending.Clear();
            moduleSectionListSeen = false;
        }

        if (proven is not null)
        {
            return BuildDomains(
                canonicalRunKey,
                proven,
                sourcePath);
        }

        return Failure(
            NclPpuDomainEvidenceCode.HashNotFound,
            "The newest matching RPCS3 serial session does not contain the requested PPU executable hash.",
            sourcePath);
    }

    private static NclPpuDomainEvidenceResult BuildDomains(
        string runKey,
        IReadOnlyList<SegmentEvidence> segments,
        string? sourcePath)
    {
        var domains = new List<NclAddressDomain>(segments.Count);

        foreach (var segment in segments)
        {
            ulong endExclusive;
            try
            {
                endExclusive = checked(
                    segment.VirtualAddress +
                    segment.FileSize);
            }
            catch (OverflowException)
            {
                return Failure(
                    NclPpuDomainEvidenceCode.InvalidSegment,
                    $"PPU segment at log line {segment.Line} overflows the guest address space.",
                    sourcePath);
            }

            if (segment.VirtualAddress > uint.MaxValue ||
                endExclusive == 0 ||
                endExclusive - 1 > uint.MaxValue)
            {
                return Failure(
                    NclPpuDomainEvidenceCode.InvalidSegment,
                    $"PPU segment at log line {segment.Line} is outside the RPCS3 UInt32 patch-address domain.",
                    sourcePath);
            }

            domains.Add(
                new NclAddressDomain(
                    runKey,
                    segment.VirtualAddress,
                    segment.FileSize,
                    (uint)segment.VirtualAddress));
        }

        if (domains.Count == 0)
        {
            return Failure(
                NclPpuDomainEvidenceCode.NoLoadSegments,
                "No non-empty file-backed PPU PT_LOAD segment was available.",
                sourcePath);
        }

        var ordered = domains
            .OrderBy(item => item.GuestStart)
            .ToArray();

        for (var index = 1; index < ordered.Length; index++)
        {
            var previousEnd = checked(
                ordered[index - 1].GuestStart +
                ordered[index - 1].Length);

            if (previousEnd > ordered[index].GuestStart)
            {
                return Failure(
                    NclPpuDomainEvidenceCode.AmbiguousSegment,
                    "Overlapping PPU PT_LOAD ranges cannot form unambiguous NCL address domains.",
                    sourcePath);
            }
        }

        return new NclPpuDomainEvidenceResult(
            true,
            NclPpuDomainEvidenceCode.Success,
            domains,
            sourcePath,
            null);
    }

    // Proven RPCS3 log boundaries (rpcs3 54014a7de4b2ccec98c9c0cb7dbebec0606c5cd6):
    //   "**** Loaded to 0x"     PPUModule.cpp:1672      ends a ppu_load_prx segment run
    //   "SPU executable hash:"  PPUModule.cpp:1483,1487 embedded SPU image boundary
    //   "lv2_overlay::load():"  sys_overlay.cpp:88      overlay load begins
    //   "Loaded overlay:"       sys_overlay.cpp:72      overlay load completed
    // The three "Trying to load" markers open a virtual-load attempt on the
    // precompilation path (PPUThread.cpp:4388 and :4605, System.cpp:1966).
    // They are emitted on the "PPU" channel (PPUThread.h:12) rather than on
    // "ppu_loader", and RPCS3 supports per-channel levels (util/logs.hpp:178,
    // :184), so they are an additional boundary, never a sufficient one.
    private static readonly string[] ForeignModuleBoundaries =
    [
        "**** Loaded to 0x",
        "SPU executable hash:",
        "lv2_overlay::load():",
        "Loaded overlay:",
        "Trying to load: ",
        "Trying to load as executable: ",
        "Trying to load binary: "
    ];

    private static bool LooksLikeForeignModuleBoundary(string line)
    {
        foreach (var marker in ForeignModuleBoundaries)
        {
            if (line.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Any "** Segment:" line on the ppu_loader channel, regardless of p_type.
    // A module's segment run can begin with a non PT_LOAD program header, so
    // the module boundary must be detected before the PT_LOAD filter.
    private static bool LooksLikePpuLoaderSegment(string line)
    {
        return line.Contains(
                   "ppu_loader:",
                   StringComparison.OrdinalIgnoreCase) &&
               line.Contains(
                   "** Segment:",
                   StringComparison.OrdinalIgnoreCase);
    }

    // "** Section:" closes a module's program-header run in every loader
    // entry point (PPUModule.cpp:1697, :2292, :2985, :3259).
    private static bool LooksLikePpuLoaderSection(string line)
    {
        return line.Contains(
                   "ppu_loader:",
                   StringComparison.OrdinalIgnoreCase) &&
               line.Contains(
                   "** Section:",
                   StringComparison.OrdinalIgnoreCase);
    }

    // A file-backed PT_LOAD candidate. The exact p_type is confirmed
    // numerically after the regex parse; this is only a cheap prefilter.
    private static bool LooksLikePpuLoadSegment(string line)
    {
        return LooksLikePpuLoaderSegment(line) &&
               line.Contains(
                   "p_type=0x1",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryHex(
        string value,
        out ulong result)
    {
        return ulong.TryParse(
            value,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool SameSegments(
        IReadOnlyList<SegmentEvidence> left,
        IReadOnlyList<SegmentEvidence> right)
    {
        return left.Count == right.Count &&
               left.Zip(right).All(pair =>
                   pair.First.VirtualAddress == pair.Second.VirtualAddress &&
                   pair.First.FileSize == pair.Second.FileSize);
    }

    private static NclPpuDomainEvidenceResult Failure(
        NclPpuDomainEvidenceCode code,
        string error,
        string? sourcePath = null)
    {
        return new NclPpuDomainEvidenceResult(
            false,
            code,
            [],
            sourcePath,
            error);
    }

    private sealed record SegmentEvidence(
        ulong VirtualAddress,
        ulong FileSize,
        int Line);

    [GeneratedRegex(
        @"^[A-Z0-9]{9}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SerialValueRegex();

    [GeneratedRegex(
        @"\bSYS:\s*Serial:\s*(?<serial>[A-Z0-9]{9})\b",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex SerialLineRegex();

    [GeneratedRegex(
        @"^(?:(?<serial>[A-Z0-9]{9})-)?PPU-(?<hash>[0-9a-f]{40})$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex RuntimePpuKeyRegex();

    [GeneratedRegex(
        @"\bPPU executable hash:\s*PPU-(?<hash>[0-9a-f]{40})(?![0-9a-f])",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex PpuHashRegex();

    [GeneratedRegex(
        @"\bppu_loader:\s*\*\*\s*Segment:\s*p_type=0x(?<type>[0-9a-f]+),\s*p_vaddr=0x(?<vaddr>[0-9a-f]+),\s*p_filesz=0x(?<filesz>[0-9a-f]+),\s*p_memsz=0x(?<memsz>[0-9a-f]+)",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex PpuLoadSegmentRegex();
}