using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.NclAudit;

// Developer-only evidence builder. It never writes into the source directory
// or the shipped-data directory; NCL bytes are copied without decoding/editing.
internal static class SourceIntegration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly DateTimeOffset ZipTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static int Run(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine("Usage: R3NclAudit --source-integration <downloadRoot> <oldZip> <outputDir>");
            return 2;
        }
        try
        {
            Execute(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            Console.Error.WriteLine($"SOURCE_INTEGRATION=FAIL {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static void Execute(string root, string oldZip, string output)
    {
        if (IsWithin(output, root) || IsWithin(output, Path.GetDirectoryName(oldZip)!))
        {
            throw new InvalidDataException("Output must be outside downloaded sources and the old corpus directory.");
        }
        var manifestBytes = ReadFile(Path.Combine(root, "NclDownloadManifest.json"), 8 * 1024 * 1024);
        var rows = JsonSerializer.Deserialize<DownloadRow[]>(manifestBytes, JsonOptions)
            ?? throw new InvalidDataException("The download manifest is empty.");
        if (rows.Length is 0 or > NclCorpusReader.MaxNclFiles ||
            rows.Any(row => row.RowOrder <= 0) || rows.Select(row => row.RowOrder).Distinct().Count() != rows.Length)
        {
            throw new InvalidDataException("Invalid or duplicated manifest row identities.");
        }
        var items = Path.Combine(root, "Items");
        var rawAudit = NclCorpusReader.Audit(items, NclDialect.ArtemisPs3);
        var rawReports = rawAudit.Files.ToDictionary(file => file.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var sources = new List<Source>();
        var gaps = new List<DownloadRow>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var row in rows.OrderBy(row => row.RowOrder))
        {
            if (string.IsNullOrWhiteSpace(row.LocalFile))
            {
                gaps.Add(row);
                continue;
            }
            var local = Path.GetFullPath(Path.IsPathFullyQualified(row.LocalFile)
                ? row.LocalFile : Path.Combine(root, row.LocalFile));
            if (!IsWithin(local, items) || !Path.GetFileName(local).Equals("source.ncl", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Row {row.RowOrder}: LocalFile is outside Items or not source.ncl.");
            }
            if (!File.Exists(local))
            {
                gaps.Add(row);
                continue;
            }
            if (!seenPaths.Add(local))
            {
                throw new InvalidDataException($"Row {row.RowOrder}: duplicate LocalFile.");
            }
            var bytes = ReadFile(local, NclCorpusReader.MaxNclBytes);
            totalBytes = checked(totalBytes + bytes.Length);
            if (bytes.Length == 0 || totalBytes > NclCorpusReader.MaxTotalNclBytes ||
                row.Bytes != bytes.Length || !Hash(bytes).Equals(row.SHA256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Row {row.RowOrder}: source length/SHA-256 does not match download evidence.");
            }
            var filename = LogicalFilename(row.ArtemisFileName);
            var identity = NclParser.ParseIdentity(filename);
            var rowIdentity = NclParser.ParseIdentity(row.TitleID + ".ncl");
            var mode = identity.Serials.Count == 0 && rowIdentity.Serials.Count == 0
                ? "TitleOnly" : identity.HasExactTarget ? "ExactSerialVersion" : "Unresolved";
            var relative = Path.GetRelativePath(items, local).Replace('\\', '/');
            if (!rawReports.TryGetValue(relative, out var report))
            {
                throw new InvalidDataException($"Row {row.RowOrder}: source absent from real parser audit.");
            }
            sources.Add(new Source(new SourceMetadata(row.RowOrder, row.Title,
                identity.Serials.ToArray(), row.Version, mode, filename, Hash(bytes),
                $"codes/site/{row.RowOrder.ToString("D4", CultureInfo.InvariantCulture)}/{filename}",
                row.Url, "DownloadedArtemis", relative), bytes, identity, report,
                Path.GetRelativePath(root, local).Replace('\\', '/'), TailDiagnostic(bytes)));
        }
        if (sources.Count != rawAudit.FileCount || rawReports.Keys.Any(relative =>
                !seenPaths.Contains(Path.GetFullPath(Path.Combine(items, relative)))))
        {
            throw new InvalidDataException("Items contains unmanifested NCL sources.");
        }

        var oldAudit = NclCorpusReader.Audit(oldZip, NclDialect.ArtemisPs3);
        var oldSources = ReadOldSources(oldZip, oldAudit, rows.Max(row => row.RowOrder));
        var comparison = Compare(oldSources, sources);
        Directory.CreateDirectory(output);
        WriteJson(Path.Combine(output, "source-acquisition-gaps.json"), gaps);
        WriteJson(Path.Combine(output, "raw-source-audit.json"), rawAudit);
        WriteJson(Path.Combine(output, "old-corpus-audit.json"), oldAudit);
        WriteJson(Path.Combine(output, "source-rows.json"), sources.Select(source => new
        {
            source.Metadata, source.Path, Length = source.Bytes.Length,
            SHA256 = source.Metadata.SourceSha256, source.Identity,
            source.Tail, Parser = source.Report
        }).ToArray());
        WriteJson(Path.Combine(output, "corpus-comparison.json"), comparison);

        var provenance = new ProvenanceEvidence(Hash(manifestBytes), rows.Length, sources.Count,
            gaps.Count, oldAudit.SourceSha256!, ReadHistoricalMetadata(oldZip));
        var newBuild = BuildVerified(output, "ncl-new", sources, provenance);
        var coverage = sources.SelectMany(CoverageKeys).ToHashSet(StringComparer.Ordinal);
        var retained = oldSources.Where(source => CoverageKeys(source).Any(key => !coverage.Contains(key))).ToArray();
        var integrated = sources.Concat(retained).ToArray();
        WriteJson(Path.Combine(output, "retained-old-sources.json"), retained.Select(source => new
        {
            source.Metadata, source.Report.CheatCount,
            MissingDefinitionBindings = CoverageKeys(source).Where(key => !coverage.Contains(key)).ToArray()
        }).ToArray());
        var integratedBuild = BuildVerified(output, "ncl-integrated", integrated, provenance);
        var integratedCoverage = integrated.SelectMany(CoverageKeys).ToHashSet(StringComparer.Ordinal);
        var missingOld = oldSources.SelectMany(CoverageKeys).Where(key => !integratedCoverage.Contains(key)).Distinct(StringComparer.Ordinal).Count();
        if (missingOld != 0)
        {
            throw new InvalidDataException("Integrated corpus lost old definition coverage.");
        }
        var summary = new
        {
            SITE_ROWS = rows.Length, SOURCE_ACQUISITION_GAPS = gaps.Count,
            Comparison = comparison,
            OLD = Counts(oldAudit), NEW = Counts(rawAudit),
            NEW_CORPUS_BUILD = newBuild, INTEGRATED_CORPUS_BUILD = integratedBuild,
            RETAINED_OLD_FILES = retained.Length, OLD_DEFINITION_COVERAGE_MISSING_AFTER_INTEGRATION = missingOld,
            SourceDefinitionsBasis = "Actual R3Config Artemis parser; independently counted delimiter/tail segments are diagnostic evidence, not a parallel parser.",
            TOTAL_SOURCE_FILES = rawAudit.FileCount,
            TOTAL_SOURCE_DEFINITIONS = sources.Sum(source => source.Report.Format == NclDocFormat.ModernHash
                ? source.Tail.HashDelimiterCount + (source.Tail.UnterminatedNonWhitespaceTail ? 1 : 0)
                : source.Report.CheatCount),
            PARSED_DEFINITIONS = rawAudit.CheatCount
        };
        WriteJson(Path.Combine(output, "source-integration-summary.json"), summary);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
    }

    private static Source[] ReadOldSources(string path, NclCorpusReport audit, int lastRow)
    {
        using var archive = ZipFile.OpenRead(path);
        var reports = audit.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var sources = new List<Source>();
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".ncl", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            using var stream = entry.Open();
            var bytes = ReadBounded(stream, NclCorpusReader.MaxNclBytes);
            var relative = entry.FullName.Replace('\\', '/');
            var filename = LogicalFilename(relative.Split('/')[^1]);
            var report = reports[relative];
            var identity = report.Identity;
            lastRow++;
            sources.Add(new Source(new SourceMetadata(lastRow, identity.Title ?? filename,
                identity.Serials.ToArray(), string.Join(";", identity.Versions),
                identity.HasExactTarget ? "ExactSerialVersion" : identity.Serials.Count == 0 ? "TitleOnly" : "Unresolved",
                filename, Hash(bytes), $"codes/retained/{lastRow.ToString("D4", CultureInfo.InvariantCulture)}/{filename}",
                "", "RetainedPreviousCorpus", relative), bytes, identity, report, relative, TailDiagnostic(bytes)));
        }
        return sources.ToArray();
    }

    private static object Compare(Source[] oldSources, List<Source> sources)
    {
        var oldHashes = oldSources.Select(source => source.Metadata.SourceSha256).ToHashSet(StringComparer.Ordinal);
        var newHashes = sources.Select(source => source.Metadata.SourceSha256).ToHashSet(StringComparer.Ordinal);
        var oldIdentities = oldSources.SelectMany(IdentityKeys).ToHashSet(StringComparer.Ordinal);
        var newIdentities = sources.SelectMany(IdentityKeys).ToHashSet(StringComparer.Ordinal);
        var oldTextHashes = oldSources.Select(source => NormalizedTextHash(source.Bytes)).ToHashSet(StringComparer.Ordinal);
        var oldCoverage = oldSources.SelectMany(CoverageKeys).ToHashSet(StringComparer.Ordinal);
        var newCoverage = sources.SelectMany(CoverageKeys).ToHashSet(StringComparer.Ordinal);
        return new
        {
            OLD_ZIP_ENTRIES = oldSources.Length,
            OLD_UNIQUE_NCL = oldHashes.Count,
            NEW_SOURCE_FILES = sources.Count,
            MATCHED_BY_HASH = sources.Count(source => oldHashes.Contains(source.Metadata.SourceSha256)),
            MATCHED_BY_NORMALIZED_TEXT_HASH = sources.Count(source => oldTextHashes.Contains(NormalizedTextHash(source.Bytes))),
            MATCHED_BY_IDENTITY = sources.Count(source => IdentityKeys(source).Any(oldIdentities.Contains)),
            NEW_ONLY = sources.Count(source => !oldHashes.Contains(source.Metadata.SourceSha256) && !IdentityKeys(source).Any(oldIdentities.Contains)),
            OLD_ONLY = oldSources.Count(source => !newHashes.Contains(source.Metadata.SourceSha256) && !IdentityKeys(source).Any(newIdentities.Contains)),
            NEW_ONLY_BY_HASH = newHashes.Except(oldHashes).Count(), OLD_ONLY_BY_HASH = oldHashes.Except(newHashes).Count(),
            DUPLICATE_HASHES = new { OLD = Duplicates(oldSources.Select(source => source.Metadata.SourceSha256)), NEW = Duplicates(sources.Select(source => source.Metadata.SourceSha256)) },
            DUPLICATE_IDENTITIES = new { OLD = Duplicates(oldSources.SelectMany(IdentityKeys)), NEW = Duplicates(sources.SelectMany(IdentityKeys)) },
            OLD_DEFINITION_BINDINGS = oldCoverage.Count, NEW_DEFINITION_BINDINGS = newCoverage.Count,
            MATCHED_DEFINITION_BINDINGS = newCoverage.Intersect(oldCoverage).Count(),
            NEW_ONLY_DEFINITION_BINDINGS = newCoverage.Except(oldCoverage).Count(),
            OLD_ONLY_DEFINITION_BINDINGS = oldCoverage.Except(newCoverage).Count(),
            Definitions = "OLD_UNIQUE_NCL counts unique byte SHA-256. MATCHED counts new files. NEW_ONLY/OLD_ONLY have neither hash nor exact identity matches. Identity means explicit serial plus normalized version, or exact title only when serials are absent. Duplicate groups report count and excess occurrences. Definition bindings use identity plus name plus parser raw-cheat SHA-256 plus semantic status; unresolved identities are kept separately by original filename. Normalized-text SHA changes only CRLF/CR to LF and strips an initial UTF-8 BOM, diagnostic only."
        };
    }

    private static object Counts(NclCorpusReport report) => new
    {
        FILES = report.FileCount, DEFINITIONS = report.CheatCount,
        OFFLINE_STATIC_CANDIDATE = report.StaticCandidates,
        OPTION_SELECTION_REQUIRED = report.OptionSelectionRequired,
        RUNTIME_EVIDENCE_REQUIRED = report.RuntimeEvidenceRequired,
        CONSTANT_RUNTIME_REQUIRED = report.ConstantRuntimeRequired,
        NON_EXECUTABLE_METADATA = report.NonExecutableMetadata,
        UNSUPPORTED = report.Unsupported, INVALID = report.Invalid
    };

    private static object Duplicates(IEnumerable<string> keys)
    {
        var duplicates = keys.GroupBy(key => key, StringComparer.Ordinal).Where(group => group.Count() > 1).ToArray();
        return new { Groups = duplicates.Length, ExcessOccurrences = duplicates.Sum(group => group.Count() - 1) };
    }

    private static IEnumerable<string> IdentityKeys(Source source)
    {
        foreach (var target in source.Identity.Targets)
        {
            yield return $"Serial:{target.Serial}|Version:{target.Version}";
        }
        if (source.Identity.Serials.Count == 0 && source.Metadata.MatchMode == "TitleOnly")
        {
            yield return "Title:" + source.Metadata.Title.ToUpperInvariant();
        }
    }

    private static IEnumerable<string> CoverageKeys(Source source)
    {
        var identities = IdentityKeys(source).ToArray();
        if (identities.Length == 0)
        {
            identities = ["Unresolved:" + source.Metadata.OriginalFilename];
        }
        foreach (var identity in identities)
        {
            foreach (var cheat in source.Report.Cheats)
            {
                yield return JsonSerializer.Serialize(new[] { identity, cheat.Name, cheat.RawSha256, cheat.Status.ToString() });
            }
        }
    }

    private static object BuildVerified(string output, string name, IReadOnlyList<Source> sources, ProvenanceEvidence provenance)
    {
        if (sources.Count > NclCorpusReader.MaxNclFiles || sources.Sum(source => (long)source.Bytes.Length) > NclCorpusReader.MaxTotalNclBytes)
        {
            throw new InvalidDataException("Experimental corpus exceeds existing reader bounds.");
        }
        var path = Path.Combine(output, name + ".zip");
        var second = Path.Combine(output, name + "-repeat.zip");
        Build(path, sources, provenance);
        Build(second, sources, provenance);
        var sha256 = Hash(ReadFile(path, NclCorpusReader.MaxZipBytes));
        if (sha256 != Hash(ReadFile(second, NclCorpusReader.MaxZipBytes)))
        {
            throw new InvalidDataException("Deterministic ZIP repeat hash mismatch.");
        }
        using (var archive = ZipFile.OpenRead(path))
        {
            if (archive.Entries.Count != sources.Count + 2 + provenance.HistoricalMetadata.Count)
            {
                throw new InvalidDataException("ZIP entry count mismatch.");
            }
            foreach (var source in sources)
            {
                var entry = archive.GetEntry(source.Metadata.EntryPath)
                    ?? throw new InvalidDataException("ZIP source entry missing.");
                using var stream = entry.Open();
                if (!ReadBounded(stream, NclCorpusReader.MaxNclBytes).AsSpan().SequenceEqual(source.Bytes))
                {
                    throw new InvalidDataException("Raw NCL bytes changed in ZIP.");
                }
            }
        }
        var report = NclCorpusReader.Audit(path, NclDialect.ArtemisPs3);
        var expectedDefinitions = sources.Sum(source => source.Report.CheatCount);
        if (report.FileCount != sources.Count || report.CheatCount != expectedDefinitions)
        {
            throw new InvalidDataException("Source to corpus parser counts changed.");
        }
        WriteJson(Path.Combine(output, name + "-audit.json"), report);
        return new { BUILD = "PASS", DETERMINISTIC = "PASS", RAW_BYTE_READBACK = "PASS", PARSE = "PASS", Path = path,
            SHA256 = sha256, FILES = sources.Count, ZIP_ENTRIES = sources.Count + 2 + provenance.HistoricalMetadata.Count, DEFINITIONS = report.CheatCount };
    }

    private static void Build(string path, IReadOnlyList<Source> sources, ProvenanceEvidence provenance)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var ordered = sources.OrderBy(source => source.Metadata.RowOrder).ToArray();
        AddEntry(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new
        {
            SchemaVersion = 1, Sources = ordered.Select(source => source.Metadata).ToArray()
        }, JsonOptions));
        AddEntry(archive, "PROVENANCE.json", JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 3, artifactKind = "R3Config NCL corpus", nclFileCount = sources.Count,
            sourceDownloadManifestSha256 = provenance.ManifestSha256,
            sourceRowCount = provenance.RowCount, acquiredSourceCount = provenance.AcquiredCount,
            sourceAcquisitionGapCount = provenance.GapCount,
            previousCorpusSha256 = provenance.PreviousCorpusSha256,
            retainedPreviousSourceCount = sources.Count(source => source.Metadata.SourceKind == "RetainedPreviousCorpus"),
            licensingNotice = "Source acquisition and content hashes establish provenance, not a blanket license for third-party cheat definitions.",
            PreviousProvenance = provenance.HistoricalMetadata.TryGetValue("PREVIOUS-PROVENANCE.json", out var previous)
                ? JsonSerializer.Deserialize<JsonElement>(previous) : (JsonElement?)null
        }, JsonOptions));
        foreach (var metadata in provenance.HistoricalMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AddEntry(archive, metadata.Key, metadata.Value);
        }
        foreach (var source in ordered)
        {
            AddEntry(archive, source.Metadata.EntryPath, source.Bytes);
        }
    }

    private static void AddEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = ZipTime;
        entry.ExternalAttributes = 0;
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string LogicalFilename(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['/', '\\', '\0', ':']) >= 0 || value is "." or "..")
        {
            throw new InvalidDataException("Unsafe logical NCL filename.");
        }
        return value.EndsWith(".ncl", StringComparison.OrdinalIgnoreCase) ? value : value + ".ncl";
    }

    private static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static byte[] ReadFile(string path, long limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadBounded(stream, limit);
    }

    private static byte[] ReadBounded(Stream stream, long limit)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (bytes.Length + count > limit)
            {
                throw new InvalidDataException("Source exceeds audit byte bound.");
            }
            bytes.Write(buffer, 0, count);
        }
        return bytes.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string NormalizedTextHash(byte[] bytes)
    {
        // Latin1 is a reversible byte projection, not a replacement NCL decoder.
        var start = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        var text = Encoding.Latin1.GetString(bytes, start, bytes.Length - start)
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Hash(Encoding.Latin1.GetBytes(text));
    }

    private static TailEvidence TailDiagnostic(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var delimiter = text.LastIndexOf('#');
        var tail = delimiter >= 0 ? text[(delimiter + 1)..] : "";
        return new TailEvidence(delimiter >= 0, text.Count(value => value == '#'),
            !string.IsNullOrWhiteSpace(tail), tail.Length,
            string.IsNullOrWhiteSpace(tail) ? null : Hash(Encoding.Latin1.GetBytes(tail)));
    }

    private static Dictionary<string, byte[]> ReadHistoricalMetadata(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in new[] { "LICENSE-ArtemisPS3.txt", "PROVENANCE.json" })
        {
            var entry = archive.GetEntry(name);
            if (entry is null)
            {
                continue;
            }
            using var stream = entry.Open();
            result.Add(name == "PROVENANCE.json" ? "PREVIOUS-PROVENANCE.json" : name,
                ReadBounded(stream, 2 * 1024 * 1024));
        }
        return result;
    }

    private static void WriteJson<T>(string path, T value) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));

    private sealed record DownloadRow(int RowOrder, string TitleID, string Title, string Version,
        string ArtemisFileName, string Status, int? HttpStatus, long Bytes, string SHA256, string Url, string? LocalFile);
    private sealed record SourceMetadata(int RowOrder, string Title, string[] Serials, string Version,
        string MatchMode, string OriginalFilename, string SourceSha256, string EntryPath, string SourceUrl,
        string SourceKind, string OriginalSourcePath);
    private sealed record Source(SourceMetadata Metadata, byte[] Bytes, NclSourceIdentity Identity,
        NclCorpusFileReport Report, string Path, TailEvidence Tail);
    private sealed record TailEvidence(bool HasHashDelimiter, int HashDelimiterCount,
        bool UnterminatedNonWhitespaceTail, int TailByteCount, string? TailSha256);
    private sealed record ProvenanceEvidence(string ManifestSha256, int RowCount, int AcquiredCount,
        int GapCount, string PreviousCorpusSha256, Dictionary<string, byte[]> HistoricalMetadata);
}
