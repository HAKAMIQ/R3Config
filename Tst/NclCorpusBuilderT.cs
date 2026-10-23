using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// Builds one unified NCL corpus from several source snapshots and proves the
// result against the definitions that went into it.
//
// It reasons about cheat definitions, not about whole files. Every source is
// read by NclCorpusReader.Audit, so the parser, the verifier, the static
// lowerer and PatchWriteSemantics are the shipped ones; this type contributes
// no semantics of its own. What it decides is only which source entries have
// to be carried so that no definition is lost, and it carries those entries
// byte for byte - it never rewrites NCL text.
//
// Two definitions are the same definition only where PatchWriteSemantics
// proves they write the same bytes. A definition that cannot be lowered keeps
// its own raw identity and is never merged with anything.
internal static class NclCorpusBuilderT
{
    // The one fixed timestamp every emitted entry carries, so the same inputs
    // always produce a byte-identical archive.
    private static readonly DateTimeOffset EntryTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions GateJson =
        new() { WriteIndented = true };

    private static readonly JsonSerializerOptions ProvenanceJson =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    private const string LicenseEntryName = "LICENSE-ArtemisPS3.txt";
    private const string ProvenanceEntryName = "PROVENANCE.json";
    private const string ArtifactKind = "R3Config NCL corpus";

    private const string ArtemisLicenseText = """
MIT License

Copyright (c) 2020 Daniel Gerendasy

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
""";

    private sealed record ProvenanceSource(
        string Id,
        string Kind,
        string Sha256,
        int NclFileCount,
        string? SourcePath = null,
        string? Note = null);

    private sealed record ProvenanceDocument(
        int Schema,
        string ArtifactKind,
        int NclFileCount,
        string Dialect,
        string GeneratedBy,
        string LicenseNotice,
        IReadOnlyList<ProvenanceSource> Sources);

    private static readonly ProvenanceSource[] CanonicalSourceLineage =
    [
        new(
            "ALDOSTOOLS_CODELIST",
            "upstreamArchive",
            "A1ABC379DD6F8BB65399EACD97D147A33C5907BAFE2C1E0B468BA86A786E8CEA",
            2501,
            "_Projects_/codelists"),
        new(
            "BUCANERO_CURRENT",
            "upstreamArchive",
            "8289EFDB5B5441A579C1ECAC8E608852FAB4D225A92728B5EB8D6227ED89EBA2",
            2540,
            "docs/cheatdb.zip"),
        new(
            "DNAWRKSHP_HISTORICAL",
            "historicalUpstreamArchive",
            "8AC628D238D0501D699552EECF40C836D417DCADD66419699DC2C56BBE6D01A6",
            1287,
            "ArtemisPS3-GUI/pkgfiles/USRDIR/USERLIST"),
        new(
            "R3CONFIG_CURRENT",
            "legacyCarryForward",
            "80AA918A4F98A7491A0D684456D7620486296C54B396FEA0C89D117CBF5DD17D",
            2497,
            "App/Data/ncl.zip",
            "Historical shipped corpus lineage; a current rebuilt corpus may be used as a carrier input but is not an independent upstream source."),
        new(
            "AR_SITE_CODELIST_2026",
            "manifestDownloadArchive",
            "734896CB7DD1EEE2B9E6720E6E75D9D852626F213C8676B03914257DF8555DC3",
            2495,
            "NclDownloadManifest.json (site-scraped Artemis codelist)",
            "Row-manifest source: identity (RowOrder/Title/Serials/Version/MatchMode) is recorded per entry in manifest.json instead of the physical filename, because every entry is stored as Items/<RowOrder>/source.ncl.")
    ];

    private sealed record Entry(
        string SourceId,
        string RelativePath,
        string FileKey,
        string ContentTag,
        byte[] Bytes,
        NclCorpusFileReport Report,
        IReadOnlyList<string> Definitions);

    public static void Run(
        string outputDirectory,
        string candidatePath,
        IReadOnlyList<(string Id, string Path)> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0)
        {
            throw new ArgumentException(
                "At least one corpus source is required.",
                nameof(sources));
        }

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(candidatePath))!);

        var entries = Collect(sources);
        var sourceDefinitions = new HashSet<string>(
            entries.SelectMany(entry => entry.Definitions),
            StringComparer.Ordinal);

        Console.WriteLine(
            $"  collected entries={entries.Count} " +
            $"distinctFileNames={entries.Select(item => item.FileKey).Distinct(StringComparer.Ordinal).Count()} " +
            $"sourceDefinitions={sourceDefinitions.Count}");

        var selection = SelectCarriers(entries, out var buildRows);

        Console.WriteLine(
            $"  selected entries={selection.Count} " +
            $"redundantDropped={entries.Count - selection.Count}");

        var provenance = BuildProvenance(sources, selection.Count);
        WriteArchive(candidatePath, selection, provenance);
        VerifyArchiveContract(candidatePath, provenance);

        var determinismPath = candidatePath + ".determinism.tmp";
        try
        {
            WriteArchive(determinismPath, selection, provenance);
            if (!FileSha256(candidatePath).Equals(
                    FileSha256(determinismPath),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The canonical NCL corpus builder is not byte deterministic.");
            }
        }
        finally
        {
            if (File.Exists(determinismPath))
            {
                File.Delete(determinismPath);
            }
        }

        var candidate = NclCorpusReader.Audit(candidatePath);
        var candidateEntries = candidate.Files
            .SelectMany(file => file.Cheats.Select(DefinitionKey))
            .ToArray();
        var candidateDefinitions = new HashSet<string>(
            candidateEntries,
            StringComparer.Ordinal);

        var missing = sourceDefinitions
            .Where(key => !candidateDefinitions.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var gates = Verify(
            outputDirectory,
            candidate,
            candidatePath,
            sourceDefinitions.Count,
            candidateDefinitions.Count,
            missing,
            entries.Count,
            selection.Count);

        Write(outputDirectory, "build.tsv",
            "FileName\tDecision\tSourceId\tRelPath\tContentTag\tDefinitions\tNewDefinitions\tEmittedPath",
            buildRows);
        Write(outputDirectory, "dropped.tsv",
            "DefinitionKey",
            missing);

        File.WriteAllText(
            Path.Combine(outputDirectory, "gates.json"),
            JsonSerializer.Serialize(gates, GateJson),
            new UTF8Encoding(false));

        Console.WriteLine(
            $"NCL corpus candidate: path={candidatePath} " +
            $"sha256={FileSha256(candidatePath)} " +
            $"files={candidate.FileCount} cheats={candidate.CheatCount}");
        Console.WriteLine(
            $"GATES DROPPED_BY_LOOKUP={gates.DroppedByLookup} " +
            $"SILENTLY_DROPPED={gates.SilentlyDropped} " +
            $"ACTIVE_SEMANTIC_DUPLICATES={gates.ActiveSemanticDuplicates} " +
            $"UNSAFE_AUTO_MERGES={gates.UnsafeAutoMerges}");
        Console.WriteLine(
            gates.AllGatesPass
                ? "NCL corpus candidate: ALL GATES PASS"
                : "NCL corpus candidate: GATE FAILURE");

        if (!gates.AllGatesPass)
        {
            throw new InvalidDataException(
                "The unified NCL corpus candidate did not satisfy every gate.");
        }
    }

    // Every .ncl entry of every source, with the definitions the shipped
    // reader found in it. Bytes are kept so the chosen entries can be carried
    // across verbatim.
    private static List<Entry> Collect(
        IReadOnlyList<(string Id, string Path)> sources)
    {
        var entries = new List<Entry>();

        foreach (var (id, path) in sources)
        {
            var report = NclCorpusReader.Audit(path);
            var bytes = ReadAll(path);
            var manifestFileKeys = ReadManifestFileKeys(path);

            foreach (var file in report.Files)
            {
                if (!bytes.TryGetValue(file.RelativePath, out var content))
                {
                    throw new InvalidDataException(
                        $"Source '{id}' reported '{file.RelativePath}' but it could not be read back.");
                }

                // A row-manifest source stores every entry at a physically
                // identical filename (Items/<RowOrder>/source.ncl), so the
                // manifest's own OriginalFilename - the same identity the
                // shipped reader already validated against ParseIdentity -
                // takes the place of the physical filename as the dedup key.
                var fileKey = manifestFileKeys.TryGetValue(
                    file.RelativePath,
                    out var originalFilename)
                    ? originalFilename
                    : Path.GetFileName(file.RelativePath);

                entries.Add(new Entry(
                    id,
                    file.RelativePath,
                    fileKey,
                    Tag(content),
                    content,
                    file,
                    file.Cheats
                        .Select(DefinitionKey)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()));
            }

            Console.WriteLine(
                $"  {id}: files={report.FileCount} cheats={report.CheatCount} " +
                $"bytes={report.TotalNclBytes}");
        }

        return entries;
    }

    // Per file name, the smallest set of source entries whose definitions
    // cover every definition any source carries for that name. One entry is
    // usually enough; a second is emitted only when it is the sole carrier of
    // a definition, which is what keeps the union lossless without rewriting
    // any NCL text.
    private static List<(Entry Entry, string EmittedPath)> SelectCarriers(
        List<Entry> entries,
        out List<string> buildRows)
    {
        var rows = new List<string>();
        var selection = new List<(Entry Entry, string EmittedPath)>();

        foreach (var group in entries
                     .GroupBy(entry => entry.FileKey, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var wanted = new HashSet<string>(
                group.SelectMany(entry => entry.Definitions),
                StringComparer.Ordinal);
            var remaining = group
                .GroupBy(entry => entry.ContentTag, StringComparer.Ordinal)
                .Select(contents => contents
                    .OrderBy(entry => entry.SourceId, StringComparer.Ordinal)
                    .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                    .First())
                .ToList();
            var picked = 0;

            while (wanted.Count > 0 && remaining.Count > 0)
            {
                var best = remaining
                    .Select(entry => (
                        Entry: entry,
                        Gain: entry.Definitions.Count(wanted.Contains)))
                    .OrderByDescending(item => item.Gain)
                    .ThenBy(item => item.Entry.SourceId, StringComparer.Ordinal)
                    .ThenBy(item => item.Entry.ContentTag, StringComparer.Ordinal)
                    .First();

                if (best.Gain == 0)
                {
                    break;
                }

                var emitted = picked == 0
                    ? "codes/" + best.Entry.FileKey
                    : "codes/variants/" + best.Entry.ContentTag + "/" +
                      best.Entry.FileKey;

                selection.Add((best.Entry, emitted));
                rows.Add(string.Join('\t',
                    best.Entry.FileKey,
                    picked == 0 ? "PRIMARY" : "ADDITIONAL_CARRIER",
                    best.Entry.SourceId,
                    best.Entry.RelativePath,
                    best.Entry.ContentTag,
                    best.Entry.Definitions.Count.ToString(CultureInfo.InvariantCulture),
                    best.Gain.ToString(CultureInfo.InvariantCulture),
                    emitted));

                foreach (var key in best.Entry.Definitions)
                {
                    wanted.Remove(key);
                }

                remaining.RemoveAll(item => ReferenceEquals(item, best.Entry));
                picked++;
            }

            foreach (var entry in remaining)
            {
                rows.Add(string.Join('\t',
                    entry.FileKey,
                    "REDUNDANT",
                    entry.SourceId,
                    entry.RelativePath,
                    entry.ContentTag,
                    entry.Definitions.Count.ToString(CultureInfo.InvariantCulture),
                    "0",
                    string.Empty));
            }

            if (wanted.Count > 0)
            {
                throw new InvalidDataException(
                    $"File '{group.Key}' has {wanted.Count} definitions no source entry carries.");
            }
        }

        buildRows = rows;
        return selection;
    }

    private static ProvenanceDocument BuildProvenance(
        IReadOnlyList<(string Id, string Path)> sources,
        int actualNclFileCount)
    {
        var actualIds = sources
            .Select(source => source.Id)
            .ToArray();
        var canonicalIds = CanonicalSourceLineage
            .Select(source => source.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Every invocation must supply only known, proven lineages, but not
        // every lineage needs a live input every rebuild: a source already
        // fully carried forward through R3CONFIG_CURRENT (or any other
        // previously supplied source) does not need to be re-supplied, since
        // its bytes already flow through the carry-forward chain unchanged.
        // The full historical lineage is still emitted below regardless of
        // which ids were live this run.
        var unknownIds = actualIds.Where(id => !canonicalIds.Contains(id)).ToArray();
        if (unknownIds.Length > 0)
        {
            throw new InvalidDataException(
                "Unknown NCL corpus source lineage id(s): " + string.Join(", ", unknownIds) +
                ". Known lineages: " + string.Join(", ", canonicalIds.OrderBy(id => id, StringComparer.Ordinal)));
        }

        if (actualIds.Length == 0)
        {
            throw new InvalidDataException("At least one known source lineage must be supplied.");
        }

        foreach (var source in sources)
        {
            var expected = CanonicalSourceLineage.Single(item =>
                item.Id.Equals(source.Id, StringComparison.Ordinal));
            var report = NclCorpusReader.Audit(source.Path);

            // R3CONFIG_CURRENT is lineage, not independent upstream evidence.
            // Once the canonical corpus has been adopted, the current bundled
            // ZIP may be fed back as a carrier input; its current SHA/count are
            // therefore deliberately not asserted as the historical lineage.
            if (expected.Kind.Equals("legacyCarryForward", StringComparison.Ordinal))
            {
                continue;
            }

            if (!File.Exists(source.Path))
            {
                throw new InvalidDataException(
                    $"Canonical source '{source.Id}' must be an archive file so its provenance SHA-256 can be verified.");
            }

            var actualSha = FileSha256(source.Path);
            if (!actualSha.Equals(expected.Sha256, StringComparison.Ordinal) ||
                report.FileCount != expected.NclFileCount)
            {
                throw new InvalidDataException(
                    $"Canonical source '{source.Id}' provenance mismatch: " +
                    $"sha256={actualSha}, files={report.FileCount}.");
            }
        }

        return new ProvenanceDocument(
            Schema: 2,
            ArtifactKind: ArtifactKind,
            NclFileCount: actualNclFileCount,
            Dialect: NclDialect.ArtemisPs3.ToString(),
            GeneratedBy: "R3Config deterministic NCL corpus builder",
            LicenseNotice: LicenseEntryName,
            Sources: CanonicalSourceLineage
                .OrderBy(source => source.Id, StringComparer.Ordinal)
                .ToArray());
    }

    private static void WriteArchive(
        string candidatePath,
        List<(Entry Entry, string EmittedPath)> selection,
        ProvenanceDocument provenance)
    {
        if (File.Exists(candidatePath))
        {
            File.Delete(candidatePath);
        }

        using var stream = new FileStream(
            candidatePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Create,
            leaveOpen: false);

        foreach (var item in selection
                     .OrderBy(item => item.EmittedPath, StringComparer.Ordinal))
        {
            WriteEntry(archive, item.EmittedPath, item.Entry.Bytes);
        }

        var licenseBytes = Encoding.UTF8.GetBytes(
            ArtemisLicenseText.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        WriteEntry(archive, LicenseEntryName, licenseBytes);

        var provenanceJson = JsonSerializer.Serialize(
            provenance,
            ProvenanceJson) + "\n";
        WriteEntry(
            archive,
            ProvenanceEntryName,
            Encoding.UTF8.GetBytes(provenanceJson));
    }

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTimestamp;
        using var target = entry.Open();
        target.Write(bytes);
    }

    private static void VerifyArchiveContract(
        string candidatePath,
        ProvenanceDocument expectedProvenance)
    {
        using var archive = ZipFile.OpenRead(candidatePath);
        var nclEntries = archive.Entries
            .Where(entry =>
                entry.Name.Length > 0 &&
                entry.FullName.EndsWith(".ncl", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (nclEntries.Length != expectedProvenance.NclFileCount)
        {
            throw new InvalidDataException(
                $"Canonical NCL corpus expected {expectedProvenance.NclFileCount} NCL entries but found {nclEntries.Length}.");
        }

        if (archive.Entries.Count != expectedProvenance.NclFileCount + 2 ||
            archive.GetEntry(LicenseEntryName) is null ||
            archive.GetEntry(ProvenanceEntryName) is null)
        {
            throw new InvalidDataException(
                $"Canonical NCL corpus must contain only {expectedProvenance.NclFileCount} NCL entries plus the root license and provenance records.");
        }

        var provenanceEntry = archive.GetEntry(ProvenanceEntryName)!;
        using var reader = new StreamReader(
            provenanceEntry.Open(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        var actual = JsonSerializer.Deserialize<ProvenanceDocument>(
            reader.ReadToEnd(),
            ProvenanceJson);
        if (actual is null ||
            actual.Schema != expectedProvenance.Schema ||
            !actual.ArtifactKind.Equals(expectedProvenance.ArtifactKind, StringComparison.Ordinal) ||
            actual.NclFileCount != expectedProvenance.NclFileCount ||
            !actual.Dialect.Equals(expectedProvenance.Dialect, StringComparison.Ordinal) ||
            !actual.LicenseNotice.Equals(expectedProvenance.LicenseNotice, StringComparison.Ordinal) ||
            actual.Sources.Count != expectedProvenance.Sources.Count ||
            actual.Sources.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count() != actual.Sources.Count)
        {
            throw new InvalidDataException(
                "Canonical NCL corpus schema-2 provenance did not round-trip exactly.");
        }

        for (var index = 0; index < expectedProvenance.Sources.Count; index++)
        {
            if (actual.Sources[index] != expectedProvenance.Sources[index])
            {
                throw new InvalidDataException(
                    $"Canonical NCL corpus source provenance mismatch at index {index}.");
            }
        }
    }

    // The gates, measured on the written candidate with the same variant rule
    // the runtime lookup uses, so a pass here means the runtime will list every
    // definition rather than suppress it.
    private static GateReport Verify(
        string outputDirectory,
        NclCorpusReport candidate,
        string candidatePath,
        int sourceDefinitions,
        int candidateDefinitions,
        string[] missing,
        int collectedEntries,
        int selectedEntries)
    {
        var instances = candidate.Files
            .Where(file => file.HasExactIdentity)
            .SelectMany(file => file.Identity.Targets
                .SelectMany(target => file.Cheats.Select(cheat => (
                    Serial: target.Serial.ToUpperInvariant(),
                    target.Version,
                    Cheat: cheat))))
            .ToArray();

        var droppedByLookup = 0;
        var activeDuplicates = 0;
        var unsafeMerges = 0;
        var disambiguated = 0;
        var trueDifferent = 0;
        var unknownVariants = 0;
        var collapsed = 0;
        var variantRows = new List<string>();

        foreach (var group in instances
                     .GroupBy(item => (item.Serial, item.Version, item.Cheat.Name),
                         TargetNameComparer.Instance))
        {
            var members = group.ToArray();
            var variants = members
                .GroupBy(item => VariantKey(item.Cheat), StringComparer.Ordinal)
                .OrderBy(variant => variant.Key, StringComparer.Ordinal)
                .ToArray();

            if (variants.Length == 0)
            {
                droppedByLookup++;
                continue;
            }

            collapsed += members.Length - variants.Length;

            foreach (var variant in variants)
            {
                // A class may only hold definitions proven to write the same
                // bytes. Anything else inside one class would be a merge this
                // project has not proven, so it is counted as a failure.
                var distinct = variant
                    .Select(item => item.Cheat.WritePrint)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (distinct.Length > 1)
                {
                    unsafeMerges++;
                }

                if (!PatchWriteSemantics.IsProven(variant.First().Cheat.WritePrint))
                {
                    unknownVariants++;
                }
            }

            // Two classes under one name must be provably different. If any
            // pair comes back equivalent, a duplicate survived as its own
            // listed entry and the corpus is not canonical.
            for (var i = 0; i < variants.Length; i++)
            {
                for (var j = i + 1; j < variants.Length; j++)
                {
                    var relation = PatchWriteSemantics.Classify(
                        variants[i].First().Cheat.WritePrint,
                        variants[j].First().Cheat.WritePrint);

                    if (relation == PatchWriteRelation.SemanticallyEquivalent)
                    {
                        activeDuplicates++;
                    }
                }
            }

            if (variants.Length <= 1)
            {
                continue;
            }

            disambiguated++;
            trueDifferent += variants.Length;
            variantRows.Add(string.Join('\t',
                members[0].Serial,
                members[0].Version,
                members[0].Cheat.Name,
                variants.Length.ToString(CultureInfo.InvariantCulture),
                string.Join(',', variants.Select(variant => Tag(variant.Key)))));
        }

        Write(outputDirectory, "variants.tsv",
            "Serial\tVersion\tSourceName\tVariants\tTags",
            variantRows);

        return new GateReport(
            CandidatePath: Path.GetFullPath(candidatePath),
            CandidateSha256: FileSha256(candidatePath),
            CollectedEntries: collectedEntries,
            EmittedEntries: selectedEntries,
            Files: candidate.FileCount,
            Cheats: candidate.CheatCount,
            SourceDefinitions: sourceDefinitions,
            CandidateDefinitions: candidateDefinitions,
            DroppedByLookup: droppedByLookup,
            SilentlyDropped: missing.Length,
            ActiveSemanticDuplicates: activeDuplicates,
            UnsafeAutoMerges: unsafeMerges,
            CollapsedDuplicateInstances: collapsed,
            DisambiguatedNameGroups: disambiguated,
            PreservedVariants: trueDifferent,
            UnprovenVariants: unknownVariants);
    }

    private sealed record GateReport(
        string CandidatePath,
        string CandidateSha256,
        int CollectedEntries,
        int EmittedEntries,
        int Files,
        int Cheats,
        int SourceDefinitions,
        int CandidateDefinitions,
        int DroppedByLookup,
        int SilentlyDropped,
        int ActiveSemanticDuplicates,
        int UnsafeAutoMerges,
        int CollapsedDuplicateInstances,
        int DisambiguatedNameGroups,
        int PreservedVariants,
        int UnprovenVariants)
    {
        public bool AllGatesPass =>
            DroppedByLookup == 0 &&
            SilentlyDropped == 0 &&
            ActiveSemanticDuplicates == 0 &&
            UnsafeAutoMerges == 0;
    }

    // The identity of one definition: what it is called, and either the bytes
    // it provably writes or - when nothing about its effect is proven - its
    // own raw text. The second branch never claims equivalence; it only keeps
    // an unprovable definition distinguishable so it cannot be dropped.
    private static string DefinitionKey(NclCorpusCheatReport cheat)
    {
        return cheat.Name.ToUpperInvariant() + "|" + VariantKey(cheat);
    }

    private static string VariantKey(NclCorpusCheatReport cheat)
    {
        return PatchWriteSemantics.IsProven(cheat.WritePrint)
            ? "W:" + cheat.WritePrint
            : "R:" + cheat.RawSha256;
    }

    private static string Tag(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];
    }

    private static string Tag(byte[] value)
    {
        return Convert.ToHexString(SHA256.HashData(value))[..8];
    }

    private static string FileSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static Dictionary<string, byte[]> ReadAll(string path)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var full = Path.GetFullPath(path);

        if (Directory.Exists(full))
        {
            foreach (var file in Directory.EnumerateFiles(
                         full, "*.ncl", SearchOption.AllDirectories))
            {
                map[Path.GetRelativePath(full, file).Replace('\\', '/')] =
                    File.ReadAllBytes(file);
            }

            return map;
        }

        using var archive = ZipFile.OpenRead(full);

        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Length == 0 ||
                !entry.FullName.EndsWith(".ncl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            map[entry.FullName.Replace('\\', '/')] = buffer.ToArray();
        }

        return map;
    }

    private sealed record ManifestRow(string EntryPath, string OriginalFilename);
    private sealed record ManifestDocument(int SchemaVersion, ManifestRow[] Sources);

    // Optional: only a row-manifest source (see NclCorpusManifest in Core)
    // carries manifest.json. A plain filename-identified archive returns an
    // empty map and Collect() falls back to the physical filename as before.
    private static Dictionary<string, string> ReadManifestFileKeys(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full) || !File.Exists(full))
        {
            return map;
        }

        using var archive = ZipFile.OpenRead(full);
        var entry = archive.GetEntry("manifest.json");
        if (entry is null)
        {
            return map;
        }

        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<ManifestDocument>(stream)
            ?? throw new InvalidDataException($"Source manifest.json at '{path}' is empty.");
        foreach (var row in manifest.Sources)
        {
            map[row.EntryPath] = row.OriginalFilename;
        }

        return map;
    }

    private static void Write(
        string directory,
        string name,
        string header,
        IEnumerable<string> rows)
    {
        using var writer = new StreamWriter(
            Path.Combine(directory, name),
            false,
            new UTF8Encoding(false));
        writer.WriteLine(header);

        foreach (var row in rows)
        {
            writer.WriteLine(row);
        }
    }

    private sealed class TargetNameComparer
        : IEqualityComparer<(string Serial, string Version, string Name)>
    {
        public static readonly TargetNameComparer Instance = new();

        public bool Equals(
            (string Serial, string Version, string Name) x,
            (string Serial, string Version, string Name) y)
        {
            return string.Equals(x.Serial, y.Serial, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Serial, string Version, string Name) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Serial),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }
    }
}
