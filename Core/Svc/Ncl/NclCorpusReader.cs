using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclCorpusReader
{
    public const int MaxNclFiles = 4096;
    public const long MaxNclBytes = 2L * 1024 * 1024;
    public const long MaxTotalNclBytes = 32L * 1024 * 1024;
    public const long MaxZipBytes = 128L * 1024 * 1024;

    // Hoisted per CA1861: a constant array used inside a loop.
    private static readonly byte[] RawCheatSeparator = [0x00];

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly object IdentityIndexLock = new();
    private static string? _indexedSourceSha256;
    private static NclCorpusTargetIndex? _identityIndex;

    // One bounded, immutable filename index. The archive is rehashed before
    // this is consulted; no stream, source bytes or semantic data are copied.
    private static NclCorpusTargetIndex GetIdentityIndex(
        string sourceSha256, ZipArchiveEntry[] entries)
    {
        lock (IdentityIndexLock)
        {
            if (_indexedSourceSha256 != sourceSha256 || _identityIndex is null)
            {
                _identityIndex = new NclCorpusTargetIndex(entries.Select(entry =>
                    entry.FullName.Replace('\\', '/')));
                _indexedSourceSha256 = sourceSha256;
            }
            return _identityIndex;
        }
    }

    public static NclCorpusReport Audit(
        string sourcePath,
        NclDialect dialect = NclDialect.ArtemisPs3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        if (Directory.Exists(fullPath))
        {
            return AuditDirectory(fullPath, dialect);
        }

        if (File.Exists(fullPath))
        {
            return AuditZip(fullPath, dialect);
        }

        throw new FileNotFoundException(
            "NCL corpus source was not found.",
            fullPath);
    }

    public static NclCorpusLookupResult LookupZip(
        string sourcePath,
        string serial,
        string version,
        string expectedSourceSha256,
        NclDialect dialect = NclDialect.ArtemisPs3,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !IsSerial(serial) ||
            string.IsNullOrWhiteSpace(PatVer.Norm(version)) ||
            !IsSha256(expectedSourceSha256))
        {
            return LookupFailure(
                NclCorpusLookupCode.InvalidTarget,
                "NCL corpus lookup requires a valid source path, serial, application version, and pinned SHA-256.");
        }

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            return LookupFailure(
                NclCorpusLookupCode.SourceMissing,
                "The bundled NCL corpus is missing.");
        }

        try
        {
            using var stream = OpenZipRead(fullPath);
            var sourceSha256 = HashOpenStream(stream);
            if (!sourceSha256.Equals(
                    expectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LookupFailure(
                    NclCorpusLookupCode.SourceRejected,
                    "The bundled NCL corpus does not match its pinned source fingerprint.",
                    sourceSha256);
            }

            stream.Position = 0;
            using var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false);

            var entries = GetValidatedNclEntries(archive);
            var manifest = NclCorpusManifest.Read(archive, entries);
            var normalizedSerial = serial.Trim().ToUpperInvariant();
            var normalizedVersion = PatVer.Norm(version)!;
            var identityIndex = GetIdentityIndex(sourceSha256, entries);
            var otherVersions = new HashSet<string>(
                identityIndex.Versions(normalizedSerial)
                    .Where(value => !PatVer.Equals(value, normalizedVersion)),
                StringComparer.OrdinalIgnoreCase);
            var matchingPaths = new HashSet<string>(
                identityIndex.Find(normalizedSerial, normalizedVersion), StringComparer.Ordinal);
            if (manifest is not null)
            {
                matchingPaths.UnionWith(manifest.FindTitle(title, normalizedVersion));
            }
            var (drafts, exactSourceFiles, rejectedSourceFiles) = CollectDrafts(
                entries, matchingPaths, manifest, dialect, versionProven: true);

            var (cheats, conflicts) = BuildCandidates(
                drafts, sourceSha256, normalizedSerial, normalizedVersion, manifest, dialect,
                versionProven: true);

            // Same-serial, different-version discovery is deliberately a
            // separate tier: it reuses the exact identity index per other
            // version so the exact-match scan above stays byte-for-byte
            // unchanged, and every candidate it produces carries
            // VersionProven=false so CanConvert/Apply stay blocked without
            // touching the TitleOnly gate.
            var otherVersionCheats = new List<NclCorpusCheatCandidate>();
            foreach (var otherVersion in otherVersions.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                var otherPaths = new HashSet<string>(
                    identityIndex.Find(normalizedSerial, otherVersion), StringComparer.Ordinal);
                if (otherPaths.Count == 0)
                {
                    continue;
                }

                var (otherDrafts, _, _) = CollectDrafts(
                    entries, otherPaths, manifest, dialect, versionProven: false);
                var (otherCandidates, _) = BuildCandidates(
                    otherDrafts, sourceSha256, normalizedSerial, otherVersion, manifest, dialect,
                    versionProven: false);
                otherVersionCheats.AddRange(otherCandidates);
            }

            if (exactSourceFiles == 0)
            {
                return new NclCorpusLookupResult(
                    false,
                    NclCorpusLookupCode.NoExactTarget,
                    sourceSha256,
                    [],
                    otherVersions
                        .OrderBy(
                            item => item,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    [],
                    0,
                    0,
                    "No bundled NCL source declares the selected serial and exact application version.",
                    otherVersionCheats.ToArray());
            }

            if (cheats.Count == 0)
            {
                return new NclCorpusLookupResult(
                    false,
                    NclCorpusLookupCode.NoStaticCandidates,
                    sourceSha256,
                    [],
                    otherVersions
                        .OrderBy(
                            item => item,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    conflicts.ToArray(),
                    exactSourceFiles,
                    rejectedSourceFiles,
                    "Exact NCL sources exist, but no discoverable cheat definition was parsed.",
                    otherVersionCheats.ToArray());
            }

            return new NclCorpusLookupResult(
                true,
                NclCorpusLookupCode.Success,
                sourceSha256,
                cheats.ToArray(),
                otherVersions
                    .OrderBy(
                        item => item,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                conflicts.ToArray(),
                exactSourceFiles,
                rejectedSourceFiles,
                null,
                otherVersionCheats.ToArray());
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            OverflowException or
            DecoderFallbackException)
        {
            return LookupFailure(
                NclCorpusLookupCode.SourceRejected,
                ex.Message);
        }
    }

    // Reads every entry in matchingPaths into candidate drafts. Shared
    // verbatim by the exact Serial+Version scan and the same-serial,
    // different-version scan; only versionProven differs between the two,
    // and it only ever narrows CanConvert - it never changes what is parsed
    // or which statuses are recognized.
    private static (List<NclCorpusCandidateDraft> Drafts, int MatchedFiles, int RejectedFiles) CollectDrafts(
        ZipArchiveEntry[] entries,
        HashSet<string> matchingPaths,
        NclCorpusManifest? manifest,
        NclDialect dialect,
        bool versionProven)
    {
        var drafts = new List<NclCorpusCandidateDraft>();
        var matchedFiles = 0;
        var rejectedFiles = 0;

        foreach (var entry in entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (!matchingPaths.Contains(relative))
            {
                continue;
            }

            matchedFiles++;
            using var entryStream = entry.Open();
            var bytes = ReadBounded(
                entryStream,
                MaxNclBytes,
                relative);
            var entrySha256 = Convert.ToHexString(
                SHA256.HashData(bytes));
            manifest?.VerifyHash(relative, entrySha256);
            var identityProven = manifest?[relative].MatchMode != "TitleOnly";
            var text = DecodeText(
                bytes,
                out _);
            var document = NclParser.ReadText(
                text,
                relative,
                dialect);
            var verification =
                NclSemanticVerifier.Verify(document);

            if (verification.Items.Count != document.Cheats.Count)
            {
                rejectedFiles++;
                continue;
            }

            var addedFromFile = false;
            for (var index = 0;
                 index < document.Cheats.Count;
                 index++)
            {
                var status = verification.Items[index].Status;
                var cheat = document.Cheats[index];

                // Discovery is identity-based, not convertibility-based.
                // Keep every parsed cheat from an exact Serial+Version
                // source visible, then record separately whether the
                // verified offline conversion path can actually use it.
                // Runtime-only, metadata, unsupported and invalid entries
                // therefore remain discoverable without becoming writable.
                var semanticSha256 = RawCheatSha256(cheat);
                string? writePrint = null;
                IReadOnlyList<int> requiredOptionIds = [];
                IReadOnlyList<NclOptionSet> declaredOptionSets = [];
                var canConvert = false;

                if (status == NclVerifyStat.OptionSelectionRequired)
                {
                    if (TryDescribeRequiredOptions(
                            cheat,
                            out requiredOptionIds,
                            out declaredOptionSets))
                    {
                        semanticSha256 = OptionSemanticSha256(
                            cheat,
                            requiredOptionIds,
                            declaredOptionSets);
                        canConvert = true;
                    }
                }
                else if (status == NclVerifyStat.OfflineStaticCandidate)
                {
                    var lowering = NclStaticLowerer.Lower(cheat);
                    if (lowering.IsOk &&
                        lowering.Code == NclStaticLowerCode.Success)
                    {
                        semanticSha256 = StaticSemanticSha256(lowering);
                        writePrint = PatchWriteSemantics.ProjectWrites(
                            lowering.Writes.Select(write =>
                                (write.Address, write.Bytes)));
                        canConvert = true;
                    }
                }

                var notes = cheat.Notes.Count == 0
                    ? null
                    : string.Join(
                        Environment.NewLine,
                        cheat.Notes);

                drafts.Add(
                    new NclCorpusCandidateDraft(
                        cheat.Name,
                        cheat.Author,
                        notes,
                        semanticSha256,
                        status,
                        new NclCorpusSourceRef(
                            relative,
                            entrySha256,
                            index),
                        requiredOptionIds,
                        declaredOptionSets,
                        writePrint,
                        canConvert && identityProven && versionProven));
                addedFromFile = true;
            }

            if (!addedFromFile)
            {
                rejectedFiles++;
            }
        }

        return (drafts, matchedFiles, rejectedFiles);
    }

    // Groups drafts into listed candidates exactly as the exact-match scan
    // always has: variant identity (proven write bytes, or raw text when
    // unproven) decides what one listed cheat is, never the name alone.
    private static (List<NclCorpusCheatCandidate> Cheats, List<NclCorpusConflict> Conflicts) BuildCandidates(
        List<NclCorpusCandidateDraft> drafts,
        string sourceSha256,
        string serial,
        string version,
        NclCorpusManifest? manifest,
        NclDialect dialect,
        bool versionProven)
    {
        var conflicts = new List<NclCorpusConflict>();
        var cheats = new List<NclCorpusCheatCandidate>();

        foreach (var nameGroup in drafts
                     .GroupBy(
                         item => item.Name,
                         StringComparer.OrdinalIgnoreCase)
                     .OrderBy(
                         group => group.Key,
                         StringComparer.OrdinalIgnoreCase))
        {
            var variants = nameGroup
                .GroupBy(item => manifest is null ? VariantKey(item)
                    : item.Source.RelativePath + ":" + item.Source.CheatIndex,
                    StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToArray();

            foreach (var variant in variants)
            {
                var ordered = variant
                    .OrderBy(
                        item => item.Source.RelativePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Source.CheatIndex)
                    .ToArray();
                var primary = ordered[0];
                var disambiguated = variants.Length > 1;

                cheats.Add(
                    new NclCorpusCheatCandidate(
                        sourceSha256,
                        serial,
                        version,
                        disambiguated
                            ? primary.Name + " #" + VariantTag(variant.Key)
                            : primary.Name,
                        primary.Author,
                        primary.Notes,
                        dialect,
                        primary.SemanticSha256,
                        ordered
                            .Select(item => item.Source)
                            .ToArray(),
                        primary.RequiredOptionIds,
                        primary.DeclaredOptionSets,
                        disambiguated ? primary.Name : null,
                        primary.Status,
                        primary.CanConvert,
                        IdentityProven: manifest?[primary.Source.RelativePath].MatchMode != "TitleOnly",
                        VersionProven: versionProven));
            }
        }

        return (cheats, conflicts);
    }

    public static NclCorpusCandidateReadResult ReadZipCandidate(
        string sourcePath,
        string expectedSourceSha256,
        NclCorpusCheatCandidate candidate,
        string serial,
        string version)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !IsSha256(expectedSourceSha256) ||
            !candidate.IdentityProven ||
            !candidate.VersionProven ||
            candidate.Sources.Count == 0 ||
            !candidate.CorpusSha256.Equals(
                expectedSourceSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !candidate.Serial.Equals(
                serial,
                StringComparison.OrdinalIgnoreCase) ||
            !PatVer.Equals(candidate.Version, version))
        {
            return CandidateReadFailure(
                NclCorpusCandidateReadCode.InvalidCandidate,
                "The selected bundled NCL candidate does not match the requested corpus or game identity.");
        }

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            return CandidateReadFailure(
                NclCorpusCandidateReadCode.SourceMissing,
                "The bundled NCL corpus is missing.");
        }

        try
        {
            using var stream = OpenZipRead(fullPath);
            var sourceSha256 = HashOpenStream(stream);
            if (!sourceSha256.Equals(
                    expectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.SourceRejected,
                    "The bundled NCL corpus changed after discovery.");
            }

            stream.Position = 0;
            using var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false);
            var primary = candidate.PrimarySource;
            var entries = archive.Entries
                .Where(entry =>
                    entry.Name.Length > 0 &&
                    entry.FullName.Replace('\\', '/').Equals(
                        primary.RelativePath,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (entries.Length != 1)
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.EntryMissing,
                    "The selected bundled NCL source entry is missing or duplicated.");
            }

            var entry = entries[0];
            var relative = entry.FullName.Replace('\\', '/');
            ValidateLogicalPath(relative);
            if (entry.Length < 0 ||
                entry.Length > MaxNclBytes)
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.EntryChanged,
                    "The selected bundled NCL source entry exceeds its bounded size contract.");
            }

            using var entryStream = entry.Open();
            var bytes = ReadBounded(
                entryStream,
                MaxNclBytes,
                relative);
            var entrySha256 = Convert.ToHexString(
                SHA256.HashData(bytes));
            if (!entrySha256.Equals(
                    primary.EntrySha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.EntryChanged,
                    "The selected bundled NCL source entry changed after discovery.");
            }

            var identity = NclParser.ParseIdentity(
                Path.GetFileName(relative));
            if (!identity.MatchesTarget(serial, version))
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.TargetMismatch,
                    "The selected bundled NCL source no longer declares the exact game target.");
            }

            var text = DecodeText(bytes, out _);
            var document = NclParser.ReadText(
                text,
                relative,
                candidate.Dialect);

            if (primary.CheatIndex < 0 ||
                primary.CheatIndex >= document.Cheats.Count)
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.SemanticMismatch,
                    "The selected bundled NCL cheat index changed after discovery.");
            }

            // Every discovered candidate is revalidated against the exact
            // verifier status seen during discovery. Convertibility is a
            // separate property and is never inferred from visibility.
            var verification =
                NclSemanticVerifier.Verify(document);
            if (verification.Items.Count != document.Cheats.Count ||
                verification.Items[primary.CheatIndex].Status !=
                    candidate.Status)
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.SemanticMismatch,
                    "The selected bundled NCL cheat no longer passes semantic verification.");
            }

            var cheat = document.Cheats[primary.CheatIndex];
            if (!cheat.Name.Equals(
                    candidate.SourceName,
                    StringComparison.Ordinal))
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.SemanticMismatch,
                    "The selected bundled NCL cheat changed after discovery.");
            }

            if (candidate.CanConvert &&
                candidate.Status == NclVerifyStat.OptionSelectionRequired)
            {
                if (!TryDescribeRequiredOptions(
                        cheat,
                        out var currentOptionIds,
                        out var currentOptionSets) ||
                    !OptionSemanticSha256(
                        cheat,
                        currentOptionIds,
                        currentOptionSets).Equals(
                        candidate.SemanticSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return CandidateReadFailure(
                        NclCorpusCandidateReadCode.SemanticMismatch,
                        "The selected bundled NCL cheat changed after discovery.");
                }
            }
            else if (candidate.CanConvert &&
                     candidate.Status == NclVerifyStat.OfflineStaticCandidate)
            {
                var lowering = NclStaticLowerer.Lower(cheat);
                if (!lowering.IsOk ||
                    lowering.Code != NclStaticLowerCode.Success ||
                    !StaticSemanticSha256(lowering).Equals(
                        candidate.SemanticSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return CandidateReadFailure(
                        NclCorpusCandidateReadCode.SemanticMismatch,
                        "The selected bundled NCL cheat changed after discovery.");
                }
            }
            else if (!RawCheatSha256(cheat).Equals(
                         candidate.SemanticSha256,
                         StringComparison.OrdinalIgnoreCase))
            {
                return CandidateReadFailure(
                    NclCorpusCandidateReadCode.SemanticMismatch,
                    "The selected bundled NCL cheat changed after discovery.");
            }

            return new NclCorpusCandidateReadResult(
                true,
                NclCorpusCandidateReadCode.Success,
                text,
                document,
                cheat,
                null);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            OverflowException or
            DecoderFallbackException)
        {
            return CandidateReadFailure(
                NclCorpusCandidateReadCode.SourceRejected,
                ex.Message);
        }
    }

    private static NclCorpusReport AuditDirectory(
        string root,
        NclDialect dialect)
    {
        var files = Directory
            .EnumerateFiles(root, "*.ncl", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ValidateFileCount(files.Length);

        var reports = new List<NclCorpusFileReport>(files.Length);
        long totalBytes = 0;

        foreach (var path in files)
        {
            var relative = Path.GetRelativePath(root, path)
                .Replace('\\', '/');

            ValidateLogicalPath(relative);

            var bytes = ReadFileBounded(path);
            totalBytes = CheckedTotal(totalBytes, bytes.LongLength);
            reports.Add(AuditBytes(relative, bytes, dialect));
        }

        return BuildReport(
            NclCorpusSourceKind.Directory,
            root,
            sourceSha256: null,
            totalBytes,
            reports,
            dialect);
    }

    private static NclCorpusReport AuditZip(
        string zipPath,
        NclDialect dialect)
    {
        var zipInfo = new FileInfo(zipPath);
        if (zipInfo.Length > MaxZipBytes)
        {
            throw new InvalidDataException(
                $"NCL ZIP exceeds the {MaxZipBytes} byte archive limit.");
        }

        string sourceSha256;
        using (var hashStream = new FileStream(
                   zipPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            sourceSha256 = Convert.ToHexString(
                SHA256.HashData(hashStream));
        }

        using var stream = new FileStream(
            zipPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: false);

        var entries = archive.Entries
            .Where(entry =>
                entry.Name.Length > 0 &&
                entry.FullName.EndsWith(
                    ".ncl",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ValidateFileCount(entries.Length);

        var manifest = NclCorpusManifest.Read(archive, entries);
        var seenPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var reports = new List<NclCorpusFileReport>(entries.Length);
        long totalBytes = 0;

        foreach (var entry in entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            ValidateLogicalPath(relative);

            if (!seenPaths.Add(relative))
            {
                throw new InvalidDataException(
                    $"Duplicate NCL ZIP entry path: {relative}");
            }

            if (entry.Length < 0 || entry.Length > MaxNclBytes)
            {
                throw new InvalidDataException(
                    $"NCL ZIP entry '{relative}' exceeds the {MaxNclBytes} byte file limit.");
            }

            totalBytes = CheckedTotal(totalBytes, entry.Length);

            using var entryStream = entry.Open();
            var bytes = ReadBounded(
                entryStream,
                MaxNclBytes,
                relative);
            manifest?.VerifyHash(relative, Convert.ToHexString(SHA256.HashData(bytes)));
            reports.Add(AuditBytes(relative, bytes, dialect));
        }

        return BuildReport(
            NclCorpusSourceKind.Zip,
            zipPath,
            sourceSha256,
            totalBytes,
            reports,
            dialect);
    }

    private static NclCorpusFileReport AuditBytes(
        string relativePath,
        byte[] bytes,
        NclDialect dialect)
    {
        var text = DecodeText(
            bytes,
            out var usedWindows1252Fallback);
        var document = NclParser.ReadText(
            text,
            relativePath,
            dialect);
        var verification = NclSemanticVerifier.Verify(document);
        var identity = NclParser.ParseIdentity(
            Path.GetFileName(relativePath));

        var errorCodes = document.Diags
            .Where(item => item.Severity == NclDiagSev.Error)
            .Select(item => item.Code)
            .Concat(document.Cheats
                .SelectMany(cheat => cheat.Diags)
                .Where(item => item.Severity == NclDiagSev.Error)
                .Select(item => item.Code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var cheatsWithOptions = 0;
        var optionSetCount = 0;

        foreach (var cheat in document.Cheats)
        {
            if (cheat.Options.Count == 0)
            {
                continue;
            }

            cheatsWithOptions++;
            optionSetCount += cheat.Options.Count;
        }

        if (verification.Items.Count != document.Cheats.Count)
        {
            throw new InvalidDataException(
                $"NCL verifier item count mismatch for '{relativePath}': cheats={document.Cheats.Count}, items={verification.Items.Count}.");
        }

        var staticLowered = 0;
        var staticLowerRejected = 0;
        var staticLowerUnverifiedValueIncrement = 0;
        var staticLowerExpansionLimit = 0;
        var staticLowerAddressOverflow = 0;
        var staticLowerOtherRejected = 0;
        long loweredWriteCount = 0;
        long loweredByteCount = 0;
        long loweredWritesOutsideUInt32 = 0;
        var loweredCheatsOutsideUInt32 = 0;

        var cheatReports = new List<NclCorpusCheatReport>(
            document.Cheats.Count);

        for (var index = 0; index < document.Cheats.Count; index++)
        {
            var cheat = document.Cheats[index];
            var status = verification.Items[index].Status;

            if (status != NclVerifyStat.OfflineStaticCandidate)
            {
                cheatReports.Add(
                    new NclCorpusCheatReport(
                        index,
                        cheat.Name,
                        cheat.Author,
                        status,
                        cheat.Options.Count,
                        NclStaticLowerCode.NotStaticCandidate,
                        null,
                        null,
                        0,
                        RawCheatSha256(cheat)));
                continue;
            }

            var lower = NclStaticLowerer.Lower(cheat);

            if (!lower.IsOk)
            {
                staticLowerRejected++;

                switch (lower.Code)
                {
                    case NclStaticLowerCode.UnverifiedValueIncrement:
                        staticLowerUnverifiedValueIncrement++;
                        break;
                    case NclStaticLowerCode.ExpansionLimit:
                        staticLowerExpansionLimit++;
                        break;
                    case NclStaticLowerCode.AddressOverflow:
                        staticLowerAddressOverflow++;
                        break;
                    default:
                        staticLowerOtherRejected++;
                        break;
                }

                cheatReports.Add(
                    new NclCorpusCheatReport(
                        index,
                        cheat.Name,
                        cheat.Author,
                        status,
                        cheat.Options.Count,
                        lower.Code,
                        null,
                        null,
                        0,
                        RawCheatSha256(cheat)));
                continue;
            }

            staticLowered++;
            loweredWriteCount = checked(
                loweredWriteCount + lower.Writes.Count);

            var cheatOutsideUInt32 = false;
            long cheatBytes = 0;

            foreach (var write in lower.Writes)
            {
                loweredByteCount = checked(
                    loweredByteCount + write.Bytes.Count);
                cheatBytes = checked(
                    cheatBytes + write.Bytes.Count);

                var finalOffset = write.Bytes.Count == 0
                    ? 0UL
                    : (ulong)(write.Bytes.Count - 1);

                var outsideUInt32 =
                    write.Address > uint.MaxValue ||
                    finalOffset >
                    (ulong)uint.MaxValue - write.Address;

                if (!outsideUInt32)
                {
                    continue;
                }

                loweredWritesOutsideUInt32++;
                cheatOutsideUInt32 = true;
            }

            if (cheatOutsideUInt32)
            {
                loweredCheatsOutsideUInt32++;
            }

            // Both fingerprints, each from the engine that already owns it.
            // This reader computes no semantics of its own here.
            cheatReports.Add(
                new NclCorpusCheatReport(
                    index,
                    cheat.Name,
                    cheat.Author,
                    status,
                    cheat.Options.Count,
                    lower.Code,
                    StaticSemanticSha256(lower),
                    PatchWriteSemantics.ProjectWrites(
                        lower.Writes.Select(write =>
                            (write.Address, write.Bytes))),
                    cheatBytes,
                    RawCheatSha256(cheat)));
        }

        if (staticLowered + staticLowerRejected !=
            verification.StaticCandidates)
        {
            throw new InvalidDataException(
                $"NCL static lowering classification mismatch for '{relativePath}': verifier={verification.StaticCandidates}, lowered={staticLowered}, rejected={staticLowerRejected}.");
        }

        if (staticLowerUnverifiedValueIncrement +
            staticLowerExpansionLimit +
            staticLowerAddressOverflow +
            staticLowerOtherRejected !=
            staticLowerRejected)
        {
            throw new InvalidDataException(
                $"NCL static lowering rejection breakdown mismatch for '{relativePath}'.");
        }
        return new NclCorpusFileReport(
            relativePath,
            bytes.LongLength,
            usedWindows1252Fallback,
            document.Format,
            document.Dialect,
            identity,
            document.Cheats.Count,
            verification.StaticCandidates,
            verification.RuntimeRequired,
            verification.ConstantRuntimeRequired,
            verification.OptionSelectionRequired,
            verification.NonExecutableMetadata,
            verification.Unsupported,
            verification.Invalid,
            cheatsWithOptions,
            optionSetCount,
            errorCodes,
            StaticLowered: staticLowered,
            StaticLowerRejected: staticLowerRejected,
            StaticLowerUnverifiedValueIncrement:
                staticLowerUnverifiedValueIncrement,
            StaticLowerExpansionLimit:
                staticLowerExpansionLimit,
            StaticLowerAddressOverflow:
                staticLowerAddressOverflow,
            StaticLowerOtherRejected:
                staticLowerOtherRejected,
            LoweredWriteCount:
                loweredWriteCount,
            LoweredByteCount:
                loweredByteCount,
            LoweredWritesOutsideUInt32:
                loweredWritesOutsideUInt32,
            LoweredCheatsOutsideUInt32:
                loweredCheatsOutsideUInt32,
            CheatReports: cheatReports);
    }

    private static NclCorpusReport BuildReport(
        NclCorpusSourceKind sourceKind,
        string sourcePath,
        string? sourceSha256,
        long totalBytes,
        List<NclCorpusFileReport> files,
        NclDialect dialect)
    {
        var modernFiles = 0;
        var legacyFiles = 0;
        var cheatCount = 0;
        var staticCandidates = 0;
        var runtimeRequired = 0;
        var constantRuntimeRequired = 0;
        var optionSelectionRequired = 0;
        var nonExecutableMetadata = 0;
        var unsupported = 0;
        var invalid = 0;
        var exactIdentityFiles = 0;
        var filesWithParserErrors = 0;
        var filesWithWindows1252Fallback = 0;
        var cheatsWithOptions = 0;
        var optionSetCount = 0;
        var staticLowered = 0;
        var staticLowerRejected = 0;
        var staticLowerUnverifiedValueIncrement = 0;
        var staticLowerExpansionLimit = 0;
        var staticLowerAddressOverflow = 0;
        var staticLowerOtherRejected = 0;
        long loweredWriteCount = 0;
        long loweredByteCount = 0;
        long loweredWritesOutsideUInt32 = 0;
        var loweredCheatsOutsideUInt32 = 0;

        foreach (var file in files)
        {
            if (file.Format == NclDocFormat.ModernHash)
            {
                modernFiles++;
            }
            else
            {
                legacyFiles++;
            }

            cheatCount += file.CheatCount;
            staticCandidates += file.StaticCandidates;
            runtimeRequired += file.RuntimeEvidenceRequired;
            constantRuntimeRequired += file.ConstantRuntimeRequired;
            optionSelectionRequired += file.OptionSelectionRequired;
            nonExecutableMetadata += file.NonExecutableMetadata;
            unsupported += file.Unsupported;
            invalid += file.Invalid;
            cheatsWithOptions += file.CheatsWithOptions;
            optionSetCount += file.OptionSetCount;
            staticLowered += file.StaticLowered;
            staticLowerRejected += file.StaticLowerRejected;
            staticLowerUnverifiedValueIncrement +=
                file.StaticLowerUnverifiedValueIncrement;
            staticLowerExpansionLimit +=
                file.StaticLowerExpansionLimit;
            staticLowerAddressOverflow +=
                file.StaticLowerAddressOverflow;
            staticLowerOtherRejected +=
                file.StaticLowerOtherRejected;
            loweredWriteCount += file.LoweredWriteCount;
            loweredByteCount += file.LoweredByteCount;
            loweredWritesOutsideUInt32 +=
                file.LoweredWritesOutsideUInt32;
            loweredCheatsOutsideUInt32 +=
                file.LoweredCheatsOutsideUInt32;

            if (file.HasExactIdentity)
            {
                exactIdentityFiles++;
            }

            if (file.HasParserErrors)
            {
                filesWithParserErrors++;
            }

            if (file.UsedWindows1252Fallback)
            {
                filesWithWindows1252Fallback++;
            }
        }

        var classified = checked(
            staticCandidates +
            runtimeRequired +
            constantRuntimeRequired +
            optionSelectionRequired +
            nonExecutableMetadata +
            unsupported +
            invalid);

        if (classified != cheatCount)
        {
            throw new InvalidDataException(
                $"NCL corpus classification mismatch: cheats={cheatCount}, classified={classified}.");
        }

        if (staticLowered + staticLowerRejected !=
            staticCandidates)
        {
            throw new InvalidDataException(
                $"NCL corpus static lowering mismatch: verifier={staticCandidates}, lowered={staticLowered}, rejected={staticLowerRejected}.");
        }

        if (staticLowerUnverifiedValueIncrement +
            staticLowerExpansionLimit +
            staticLowerAddressOverflow +
            staticLowerOtherRejected !=
            staticLowerRejected)
        {
            throw new InvalidDataException(
                "NCL corpus static lowering rejection breakdown mismatch.");
        }
        return new NclCorpusReport(
            sourceKind,
            sourcePath,
            sourceSha256,
            dialect,
            files.Count,
            modernFiles,
            legacyFiles,
            cheatCount,
            staticCandidates,
            runtimeRequired,
            constantRuntimeRequired,
            optionSelectionRequired,
            nonExecutableMetadata,
            unsupported,
            invalid,
            exactIdentityFiles,
            files.Count - exactIdentityFiles,
            filesWithParserErrors,
            filesWithWindows1252Fallback,
            cheatsWithOptions,
            optionSetCount,
            totalBytes,
            files.ToArray(),
            StaticLowered: staticLowered,
            StaticLowerRejected: staticLowerRejected,
            StaticLowerUnverifiedValueIncrement:
                staticLowerUnverifiedValueIncrement,
            StaticLowerExpansionLimit:
                staticLowerExpansionLimit,
            StaticLowerAddressOverflow:
                staticLowerAddressOverflow,
            StaticLowerOtherRejected:
                staticLowerOtherRejected,
            LoweredWriteCount:
                loweredWriteCount,
            LoweredByteCount:
                loweredByteCount,
            LoweredWritesOutsideUInt32:
                loweredWritesOutsideUInt32,
            LoweredCheatsOutsideUInt32:
                loweredCheatsOutsideUInt32);
    }

    private static byte[] ReadFileBounded(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return ReadBounded(stream, MaxNclBytes, path);
    }

    private static byte[] ReadBounded(
        Stream stream,
        long maxBytes,
        string label)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];

        while (true)
        {
            var read = stream.Read(chunk);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException(
                    $"NCL input '{label}' exceeds the {maxBytes} byte limit.");
            }

            buffer.Write(chunk.AsSpan(0, read));
        }

        return buffer.ToArray();
    }

    private static string DecodeText(
        byte[] bytes,
        out bool usedWindows1252Fallback)
    {
        try
        {
            usedWindows1252Fallback = false;
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            usedWindows1252Fallback = true;
            return DecodeWindows1252(bytes);
        }
    }

    private static string DecodeWindows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            chars[index] = Windows1252Char(bytes[index]);
        }

        return new string(chars);
    }

    private static char Windows1252Char(byte value) =>
        value switch
        {
            0x80 => '\u20AC',
            0x82 => '\u201A',
            0x83 => '\u0192',
            0x84 => '\u201E',
            0x85 => '\u2026',
            0x86 => '\u2020',
            0x87 => '\u2021',
            0x88 => '\u02C6',
            0x89 => '\u2030',
            0x8A => '\u0160',
            0x8B => '\u2039',
            0x8C => '\u0152',
            0x8E => '\u017D',
            0x91 => '\u2018',
            0x92 => '\u2019',
            0x93 => '\u201C',
            0x94 => '\u201D',
            0x95 => '\u2022',
            0x96 => '\u2013',
            0x97 => '\u2014',
            0x98 => '\u02DC',
            0x99 => '\u2122',
            0x9A => '\u0161',
            0x9B => '\u203A',
            0x9C => '\u0153',
            0x9E => '\u017E',
            0x9F => '\u0178',
            _ => (char)value
        };

    private sealed record NclCorpusCandidateDraft(
        string Name,
        string? Author,
        string? Notes,
        string SemanticSha256,
        NclVerifyStat Status,
        NclCorpusSourceRef Source,
        IReadOnlyList<int>? RequiredOptionPlaceholderIds = null,
        IReadOnlyList<NclOptionSet>? DeclaredOptionSetsOrNull = null,
        // The bytes this draft provably writes, from PatchWriteSemantics.
        // Null whenever the definition was not lowered to concrete writes.
        string? WritePrint = null,
        bool CanConvert = false)
    {
        public IReadOnlyList<int> RequiredOptionIds =>
            RequiredOptionPlaceholderIds ?? [];

        public IReadOnlyList<NclOptionSet> DeclaredOptionSets =>
            DeclaredOptionSetsOrNull ?? [];
    }

    // The placeholder ids a bundled option cheat references, with the option
    // set that owns each one, in referenced order. Ambiguous, undeclared and
    // empty option sets are refused so a malformed source is never listed.
    private static bool TryDescribeRequiredOptions(
        NclCheat cheat,
        out IReadOnlyList<int> requiredOptionIds,
        out IReadOnlyList<NclOptionSet> declaredOptionSets)
    {
        requiredOptionIds = [];
        declaredOptionSets = [];

        var required = cheat.RequiredOptionPlaceholderIds;
        if (required.Count == 0)
        {
            return false;
        }

        var declared = new Dictionary<int, NclOptionSet>();
        foreach (var set in cheat.Options)
        {
            if (!declared.TryAdd(set.PlaceholderId, set))
            {
                return false;
            }
        }

        var ordered = new List<NclOptionSet>(required.Count);
        foreach (var placeholderId in required)
        {
            if (!declared.TryGetValue(placeholderId, out var set) ||
                set.Values.Count == 0)
            {
                return false;
            }

            ordered.Add(set);
        }

        requiredOptionIds = [.. required];
        declaredOptionSets = ordered;
        return true;
    }

    // Drift fingerprint for a cheat that cannot be lowered yet. It covers the
    // exact unresolved surface discovery showed the user; the whole entry is
    // still guarded by NclCorpusSourceRef.EntrySha256, and the resolved
    // instance is still lowered and verified by the normal pipeline.
    private static string OptionSemanticSha256(
        NclCheat cheat,
        IReadOnlyList<int> requiredOptionIds,
        IReadOnlyList<NclOptionSet> declaredOptionSets)
    {
        var builder = new StringBuilder();
        builder.Append(cheat.Name).Append('\n');

        for (var index = 0; index < declaredOptionSets.Count; index++)
        {
            builder
                .Append(requiredOptionIds[index])
                .Append(':');

            foreach (var value in declaredOptionSets[index].Values)
            {
                builder
                    .Append(value.Value)
                    .Append('=')
                    .Append(value.Label)
                    .Append(';');
            }

            builder.Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static FileStream OpenZipRead(string path)
    {
        var info = new FileInfo(path);
        if (info.Length < 0 ||
            info.Length > MaxZipBytes)
        {
            throw new InvalidDataException(
                $"NCL ZIP exceeds the {MaxZipBytes} byte archive limit.");
        }

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        if (stream.Length < 0 ||
            stream.Length > MaxZipBytes)
        {
            stream.Dispose();
            throw new InvalidDataException(
                $"NCL ZIP exceeds the {MaxZipBytes} byte archive limit.");
        }

        return stream;
    }

    private static string HashOpenStream(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidDataException(
                "NCL corpus source stream must be seekable.");
        }

        stream.Position = 0;
        var result = Convert.ToHexString(
            SHA256.HashData(stream));
        stream.Position = 0;
        return result;
    }

    private static ZipArchiveEntry[] GetValidatedNclEntries(
        ZipArchive archive)
    {
        var entries = archive.Entries
            .Where(entry =>
                entry.Name.Length > 0 &&
                entry.FullName.EndsWith(
                    ".ncl",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                entry => entry.FullName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ValidateFileCount(entries.Length);

        var seenPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (var entry in entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            ValidateLogicalPath(relative);
            if (!seenPaths.Add(relative))
            {
                throw new InvalidDataException(
                    $"Duplicate NCL ZIP entry path: {relative}");
            }

            if (entry.Length < 0 ||
                entry.Length > MaxNclBytes)
            {
                throw new InvalidDataException(
                    $"NCL ZIP entry '{relative}' exceeds the {MaxNclBytes} byte file limit.");
            }

            totalBytes = CheckedTotal(
                totalBytes,
                entry.Length);
        }

        return entries;
    }

    private static string StaticSemanticSha256(
        NclStaticLowerResult lowering)
    {
        using var hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[8];

        foreach (var write in lowering.Writes)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
                number,
                write.Address);
            hash.AppendData(number);

            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
                number,
                checked((ulong)write.Bytes.Count));
            hash.AppendData(number);
            hash.AppendData(write.Bytes.ToArray());
        }

        return Convert.ToHexString(
            hash.GetHashAndReset());
    }

    // The cheat's own text: its name and the raw source line of every
    // operation, in file order. Two cheats share this only when the source
    // spells them the same way, so it distinguishes definitions that cannot
    // be lowered without ever asserting anything about their effect.
    private static string RawCheatSha256(NclCheat cheat)
    {
        using var hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(cheat.Name));

        foreach (var operation in cheat.Operations)
        {
            hash.AppendData(RawCheatSeparator);
            hash.AppendData(Encoding.UTF8.GetBytes(operation.Raw));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    // What a listed cheat is, independently of what it is called. A proven
    // effective-write projection is the identity when one exists; an unproven
    // definition keeps its own shape-sensitive hash so it can never be merged
    // with anything, only distinguished from it.
    private static string VariantKey(NclCorpusCandidateDraft draft)
    {
        return PatchWriteSemantics.IsProven(draft.WritePrint)
            ? "W:" + draft.WritePrint
            : "S:" + draft.Status + ":" + draft.SemanticSha256;
    }

    // A short, stable tag for one variant. It is a function of the variant's
    // identity alone, so the same definition always earns the same suffix
    // regardless of how many sources carry it or what order they are read in.
    private static string VariantTag(string variantKey)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(variantKey)))[..8];
    }

    private static bool IsSerial(string serial)
    {
        var value = serial?.Trim();
        return value is not null &&
               value.Length == 9 &&
               value[..4].All(ch =>
                   ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z') &&
               value[4..].All(ch => ch is >= '0' and <= '9');
    }

    private static bool IsSha256(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length == 64 &&
               value.All(Uri.IsHexDigit);
    }

    private static NclCorpusLookupResult LookupFailure(
        NclCorpusLookupCode code,
        string error,
        string? sourceSha256 = null)
    {
        return new NclCorpusLookupResult(
            false,
            code,
            sourceSha256,
            [],
            [],
            [],
            0,
            0,
            error);
    }

    private static NclCorpusCandidateReadResult CandidateReadFailure(
        NclCorpusCandidateReadCode code,
        string error)
    {
        return new NclCorpusCandidateReadResult(
            false,
            code,
            string.Empty,
            null,
            null,
            error);
    }

    private static long CheckedTotal(long current, long next)
    {
        var total = checked(current + next);
        if (total > MaxTotalNclBytes)
        {
            throw new InvalidDataException(
                $"NCL corpus exceeds the {MaxTotalNclBytes} byte expanded-data limit.");
        }

        return total;
    }

    private static void ValidateFileCount(int count)
    {
        if (count <= 0)
        {
            throw new InvalidDataException(
                "NCL corpus contains no .ncl files.");
        }

        if (count > MaxNclFiles)
        {
            throw new InvalidDataException(
                $"NCL corpus contains {count} files; limit is {MaxNclFiles}.");
        }
    }

    private static void ValidateLogicalPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"Unsafe NCL logical path: {relativePath}");
        }

        var segments = relativePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidDataException(
                $"Unsafe NCL logical path: {relativePath}");
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new InvalidDataException(
                    $"Unsafe NCL logical path: {relativePath}");
            }
        }
    }
}
