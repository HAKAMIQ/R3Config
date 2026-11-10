using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.ArtImport;

public static class ArtemisImportTool
{
    private const long MaxSourceArchiveBytes = 64L * 1024 * 1024;
    private const long MaxManagerArchiveBytes = 128L * 1024 * 1024;
    private const int MaxSourceReferenceLength = 512;
    private const int MaxDependencyBytes = 256 * 1024;

    public static int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Count != 7)
        {
            stderr.WriteLine(
                "Usage: R3ArtImport <source.zip> <art.yml> <CHEATS.md> <expected-source-sha256> <source-reference> <manager-reference.zip> <expected-manager-sha256>");
            return 2;
        }

        string archivePath;
        string catalogPath;
        string noticePath;
        string managerReferencePath;

        try
        {
            archivePath = Path.GetFullPath(args[0]);
            catalogPath = Path.GetFullPath(args[1]);
            noticePath = Path.GetFullPath(args[2]);
            managerReferencePath = Path.GetFullPath(args[5]);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            stderr.WriteLine(
                "One or more Artemis import paths are invalid: " +
                ex.Message);
            return 3;
        }

        var expectedSourceHash = args[3]
            .Trim()
            .ToLowerInvariant();
        var sourceReference = args[4].Trim();
        var expectedManagerHash = args[6]
            .Trim()
            .ToLowerInvariant();
        var sourceArtifact = Path.GetFileName(archivePath);
        var managerArtifact = Path.GetFileName(managerReferencePath);

        if (!File.Exists(archivePath))
        {
            stderr.WriteLine(
                $"Artemis source archive not found: {archivePath}");
            return 4;
        }

        if (!IsSha256(expectedSourceHash))
        {
            stderr.WriteLine(
                "The expected source SHA-256 must be exactly 64 ASCII hexadecimal characters.");
            return 5;
        }

        if (!ValidSourceReference(
                sourceReference,
                sourceArtifact))
        {
            stderr.WriteLine(
                "The source reference must be a bounded single-line label that includes the actual source artifact file name.");
            return 6;
        }

        if (!File.Exists(managerReferencePath))
        {
            stderr.WriteLine(
                $"Artemis manager reference archive not found: {managerReferencePath}");
            return 7;
        }

        if (!IsSha256(expectedManagerHash))
        {
            stderr.WriteLine(
                "The expected manager reference SHA-256 must be exactly 64 ASCII hexadecimal characters.");
            return 8;
        }

        var catalogDirectory = Path.GetDirectoryName(catalogPath);
        if (string.IsNullOrWhiteSpace(catalogDirectory))
        {
            stderr.WriteLine(
                "The Artemis catalog path must have a parent directory.");
            return 9;
        }

        var dependencyPath = Path.GetFullPath(
            Path.Combine(
                catalogDirectory,
                ArtDepSvc.FileName));

        var transactionPaths = ArtifactPairTransaction.ReservedPaths(
            catalogPath,
            noticePath);
        try
        {
            EnsureDistinctPaths(
                ("source archive", CanonicalExistingPath(archivePath)),
                ("manager reference archive", CanonicalExistingPath(managerReferencePath)),
                ("generated catalog", CanonicalTargetPath(catalogPath)),
                ("generated notice", CanonicalTargetPath(noticePath)),
                ("dependency metadata", CanonicalTargetPath(dependencyPath)),
                ("transaction journal", CanonicalTargetPath(transactionPaths[0])),
                ("catalog transaction backup", CanonicalTargetPath(transactionPaths[1])),
                ("notice transaction backup", CanonicalTargetPath(transactionPaths[2])),
                ("transaction lock", CanonicalTargetPath(transactionPaths[3])));
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                NotSupportedException)
        {
            stderr.WriteLine(
                "Could not establish distinct canonical Artemis import paths: " +
                ex.Message);
            return 10;
        }

        byte[] sourceArchive;
        string sourceHash;
        string managerReferenceHash;

        try
        {
            sourceArchive = ReadBoundedFile(
                archivePath,
                MaxSourceArchiveBytes,
                "Artemis source archive");
            sourceHash = Sha256Bytes(sourceArchive);

            var managerArchive = ReadBoundedFile(
                managerReferencePath,
                MaxManagerArchiveBytes,
                "Artemis manager reference archive");
            managerReferenceHash = Sha256Bytes(managerArchive);
            ValidateManagerArchive(managerArchive);
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            stderr.WriteLine(
                "Could not snapshot the Artemis provenance inputs: " +
                ex.Message);
            return 11;
        }

        if (!sourceHash.Equals(
                expectedSourceHash,
                StringComparison.Ordinal))
        {
            stderr.WriteLine(
                $"Artemis source archive SHA-256 mismatch. Expected {expectedSourceHash}, found {sourceHash}.");
            return 12;
        }

        if (!managerReferenceHash.Equals(
                expectedManagerHash,
                StringComparison.Ordinal))
        {
            stderr.WriteLine(
                $"Artemis manager reference archive SHA-256 mismatch. Expected {expectedManagerHash}, found {managerReferenceHash}.");
            return 13;
        }

        IReadOnlyList<ArtemisSourceFile> sourceFiles;
        int archiveFileCount;

        try
        {
            (sourceFiles, archiveFileCount) = ReadArchive(
                sourceArchive);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
                IOException or
                DecoderFallbackException)
        {
            stderr.WriteLine(
                "Could not read the Artemis source archive: " +
                ex.Message);
            return 14;
        }

        var result = new ArtemisCatalogImporter().Import(
            sourceFiles);
        if (!result.IsOk)
        {
            stderr.WriteLine(
                result.Error ??
                "The Artemis source archive did not produce a valid catalog.");
            foreach (var rejected in result.RejectedEntries)
            {
                stderr.WriteLine(
                    $"{rejected.Code}: {rejected.SourceName} | {rejected.RuntimeKey} | {rejected.Title} | {rejected.Reason}");
            }

            return 15;
        }

        var catalogHash = Sha256Bytes(
            Encoding.UTF8.GetBytes(
                result.CatalogText));

        DependencyNoticeInfo dependency;
        try
        {
            dependency = ReadDependencyNotice(
                dependencyPath,
                catalogHash,
                result.CatalogText);
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                JsonException or
                DecoderFallbackException)
        {
            stderr.WriteLine(
                "Could not validate the Artemis dependency provenance: " +
                ex.Message);
            return 16;
        }

        var notice = BuildNotice(
            sourceHash,
            sourceArtifact,
            sourceReference,
            managerArtifact,
            managerReferenceHash,
            catalogHash,
            archiveFileCount,
            dependency,
            result);

        try
        {
            ArtifactPairTransaction.Write(
                catalogPath,
                result.CatalogText,
                noticePath,
                notice);
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            stderr.WriteLine(
                "Could not commit the generated Artemis catalog and notice: " +
                DescribeException(ex));
            return 17;
        }

        stdout.WriteLine(
            $"Generated {catalogPath}");
        stdout.WriteLine(
            $"Source files: {archiveFileCount}");
        stdout.WriteLine(
            $"Candidate entries: {result.CandidateEntries}");
        stdout.WriteLine(
            $"Accepted entries: {result.AcceptedEntries}");
        stdout.WriteLine(
            $"Rejected entries: {result.RejectedEntries.Count}");
        stdout.WriteLine(
            $"Generated catalog SHA-256: {catalogHash}");
        stdout.WriteLine(
            $"Verified manager reference SHA-256: {managerReferenceHash}");
        return 0;
    }

    private static (IReadOnlyList<ArtemisSourceFile> SourceFiles, int FileCount)
        ReadArchive(byte[] archiveBytes)
    {
        const int maxArchiveEntries = 514;
        const int maxSourceFiles = 512;
        const long maxEntryBytes = 8L * 1024 * 1024;
        const long maxExpandedBytes = 128L * 1024 * 1024;

        using var input = new MemoryStream(
            archiveBytes,
            writable: false);
        using var archive = new ZipArchive(
            input,
            ZipArchiveMode.Read,
            leaveOpen: false,
            entryNameEncoding: Encoding.UTF8);

        var sourceFiles = new List<ArtemisSourceFile>();
        var sourceNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        string? licenseText = null;
        var fileCount = 0;
        var archiveEntryCount = 0;
        long expandedBytes = 0;
        var utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        foreach (var entry in archive.Entries
                     .OrderBy(
                         item => item.FullName,
                         StringComparer.Ordinal))
        {
            archiveEntryCount++;
            if (archiveEntryCount > maxArchiveEntries)
            {
                throw new InvalidDataException(
                    $"The source archive exceeds the {maxArchiveEntries}-entry limit.");
            }

            ValidateEntryPath(entry.FullName);

            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (entry.Length > maxEntryBytes)
            {
                throw new InvalidDataException(
                    $"ZIP entry exceeds the {maxEntryBytes}-byte input limit: {entry.FullName}");
            }

            var content = ReadEntryBytes(
                entry,
                maxEntryBytes,
                maxExpandedBytes,
                ref expandedBytes);
            if (content.Length == 0)
            {
                continue;
            }

            var text = DecodeUtf8(
                content,
                utf8);
            var separator = entry.FullName.LastIndexOf('/');
            var name = separator >= 0
                ? entry.FullName[(separator + 1)..]
                : entry.FullName;

            if (name.Equals(
                    "LICENSE",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (licenseText is not null)
                {
                    throw new InvalidDataException(
                        "The source archive contains more than one LICENSE file identity.");
                }

                licenseText = text;
                continue;
            }

            if (name.Equals(
                    "README.md",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!sourceNames.Add(name))
            {
                throw new InvalidDataException(
                    $"The source archive contains duplicate patch file identity '{name}'.");
            }

            fileCount++;
            if (fileCount > maxSourceFiles)
            {
                throw new InvalidDataException(
                    $"The source archive exceeds the {maxSourceFiles}-patch-file limit.");
            }

            sourceFiles.Add(
                new ArtemisSourceFile(
                    name,
                    text));
        }

        if (licenseText is null ||
            !licenseText.Contains(
                "MIT License",
                StringComparison.Ordinal) ||
            !licenseText.Contains(
                "Permission is hereby granted",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The source archive does not contain the expected MIT license.");
        }

        return (sourceFiles, fileCount);
    }

    private static DependencyNoticeInfo ReadDependencyNotice(
        string dependencyPath,
        string catalogHash,
        string catalogText)
    {
        if (!File.Exists(dependencyPath))
        {
            throw new InvalidDataException(
                $"Required Artemis dependency metadata was not found: {dependencyPath}");
        }

        var bytes = ReadBoundedFile(
            dependencyPath,
            MaxDependencyBytes,
            "Artemis dependency metadata");
        var utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        var text = DecodeUtf8(
            bytes,
            utf8);

        using var document = JsonDocument.Parse(
            text,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "The Artemis dependency metadata root must be a JSON object.");
        }

        var schemaVersion = RequiredJsonString(
            root,
            "schemaVersion");
        var sourceIdentity = RequiredJsonString(
            root,
            "sourceIdentity");
        var sourceRevision = RequiredJsonString(
            root,
            "sourceRevision");
        var artemisSha256 = RequiredJsonString(
            root,
            "artemisSha256")
            .ToLowerInvariant();

        if (!schemaVersion.Equals(
                ArtDepSvc.SchemaVersion,
                StringComparison.Ordinal) ||
            !sourceIdentity.Equals(
                ArtDepSvc.SourceIdentity,
                StringComparison.Ordinal) ||
            sourceRevision.Length != 40 ||
            !sourceRevision.All(IsAsciiHex) ||
            !IsSha256(artemisSha256) ||
            !artemisSha256.Equals(
                catalogHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Artemis dependency metadata provenance does not match the generated catalog.");
        }

        if (!root.TryGetProperty(
                "rules",
                out var rules) ||
            rules.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "The Artemis dependency metadata rules array is missing.");
        }

        var ruleCount = rules.GetArrayLength();
        if (ruleCount <= 0 ||
            ruleCount > 256)
        {
            throw new InvalidDataException(
                "The Artemis dependency metadata rule count is outside the accepted bound.");
        }

        var parsedRules = new List<ArtDepRule>(ruleCount);
        var seenRules = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in rules.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "The Artemis dependency metadata contains a non-object rule.");
            }

            var runKey = RequiredJsonString(item, "runKey");
            var serial = RequiredJsonString(item, "serial");
            var version = RequiredJsonString(item, "version");
            var patchTitle = RequiredJsonString(item, "patchTitle");
            var modeText = RequiredJsonString(item, "mode");
            var evidence = RequiredJsonString(item, "evidence");

            if (!RunKey.TryParse(
                    runKey,
                    out var parsedRunKey) ||
                !parsedRunKey.RunKey.Equals(
                    runKey,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsSerial(serial) ||
                PatVer.Norm(version) is not { } normalizedVersion ||
                PatVer.IsAll(normalizedVersion))
            {
                throw new InvalidDataException(
                    $"Artemis dependency rule '{patchTitle}' has an invalid exact identity.");
            }

            var mode = modeText switch
            {
                "All" => PatDepMode.All,
                "Any" => PatDepMode.Any,
                _ => throw new InvalidDataException(
                    $"Artemis dependency rule '{patchTitle}' has an invalid mode.")
            };

            if (!item.TryGetProperty(
                    "requires",
                    out var requiresElement) ||
                requiresElement.ValueKind != JsonValueKind.Array ||
                requiresElement.GetArrayLength() == 0 ||
                requiresElement.GetArrayLength() > 16)
            {
                throw new InvalidDataException(
                    $"Artemis dependency rule '{patchTitle}' has an invalid requirement set.");
            }

            var requires = new List<string>();
            var seenRequires = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var requirementElement in requiresElement.EnumerateArray())
            {
                if (requirementElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException(
                        $"Artemis dependency rule '{patchTitle}' contains a non-string requirement.");
                }

                var requirement = requirementElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(requirement) ||
                    requirement.Length > 512 ||
                    requirement.Any(char.IsControl) ||
                    requirement.Equals(
                        patchTitle,
                        StringComparison.OrdinalIgnoreCase) ||
                    !seenRequires.Add(requirement))
                {
                    throw new InvalidDataException(
                        $"Artemis dependency rule '{patchTitle}' contains a duplicate, self, or invalid requirement.");
                }

                requires.Add(requirement);
            }

            var normalizedSerial = serial.ToUpperInvariant();
            var identity = string.Join(
                "\0",
                parsedRunKey.RunKey,
                normalizedSerial,
                normalizedVersion,
                patchTitle,
                mode);
            if (!seenRules.Add(identity))
            {
                throw new InvalidDataException(
                    $"Duplicate Artemis dependency rule: {patchTitle}.");
            }

            parsedRules.Add(
                new ArtDepRule(
                    parsedRunKey.RunKey,
                    normalizedSerial,
                    normalizedVersion,
                    patchTitle,
                    mode,
                    requires,
                    evidence));
        }

        var catalogRead = new ReadSvc().ReadText(
            catalogText,
            PatSrc.Artemis,
            "art.yml");
        if (!catalogRead.IsOk)
        {
            throw new InvalidDataException(
                "The generated Artemis catalog could not be re-read for dependency validation: " +
                catalogRead.Error);
        }

        var dependencyCatalog = new ArtDepCat(
            schemaVersion,
            sourceIdentity,
            sourceRevision.ToLowerInvariant(),
            artemisSha256,
            parsedRules);
        _ = new ArtDepSvc().Apply(
            dependencyCatalog,
            catalogRead.Catalog);

        return new DependencyNoticeInfo(
            sourceRevision.ToLowerInvariant(),
            ruleCount,
            Sha256Bytes(bytes));
    }

    private static string BuildNotice(
        string sourceHash,
        string sourceArtifact,
        string sourceReference,
        string managerArtifact,
        string managerReferenceHash,
        string catalogHash,
        int archiveFileCount,
        DependencyNoticeInfo dependency,
        ArtemisImportResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Artemis RPCS3 Patch Collection Notice");
        builder.AppendLine();
        builder.AppendLine("R3Config ships one executable Artemis cheat catalog: `App/Data/art.yml`. It is generated from the user-supplied ChiDreams RPCS3 patch collection and is consumed through the normal RPCS3 patch pipeline. The companion `App/Data/art_dep.json` contains only reviewed dependency relationships between exact entries in that catalog; it contains no executable patch operations. R3Config does not ship or execute a second Artemis database, raw PS3/NCL programs, a live-memory cheat engine, or a separate Artemis runtime transport.");
        builder.AppendLine();
        builder.AppendLine("Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams");
        builder.AppendLine("Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Source artifact: {sourceArtifact}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Source reference: {sourceReference}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Source archive SHA-256: {sourceHash}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Source patch files: {archiveFileCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Candidate entries: {result.CandidateEntries}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Accepted entries: {result.AcceptedEntries}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Rejected records: {result.RejectedEntries.Count}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Generated art.yml SHA-256: {catalogHash}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Dependency metadata rules: {dependency.RuleCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Dependency source revision: {dependency.SourceRevision}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Generated art_dep.json SHA-256: {dependency.Sha256}");
        builder.AppendLine("License: MIT");
        builder.AppendLine();
        builder.AppendLine("## Manager reference");
        builder.AppendLine();
        builder.AppendLine("The second user-supplied archive is retained as a provenance/format reference for the same RPCS3 patch collection. It is not a second bundled cheat corpus and none of its Python/Tkinter application code is embedded in R3Config.");
        builder.AppendLine();
        builder.AppendLine("Manager reference: ARTEMIS RPCS3 Cheat Manager 2.00");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Manager reference artifact: {managerArtifact}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Manager reference archive SHA-256: {managerReferenceHash}");
        builder.AppendLine("Manager role: external editor/downloader/validator reference for RPCS3 imported_patch.yml v1.2 and the ChiDreams collection; not a bundled cheat database");
        builder.AppendLine();
        builder.AppendLine("## Import result");
        builder.AppendLine();
        builder.AppendLine("The collection importer reads patch identity from each YAML definition, expands YAML aliases, validates RPCS3 patch keys and operations, resolves duplicates deterministically, and emits a single offline `Version: 1.2` catalog.");
        builder.AppendLine();
        builder.AppendLine("Rejected reason counts:");
        builder.AppendLine();

        foreach (var reason in RejectedReasonCounts(result.RejectedEntries))
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"- {reason.Code}: {reason.Count}");
        }

        builder.AppendLine();
        builder.AppendLine("The rejected records are not silently repaired or guessed. Invalid source YAML, invalid RPCS3 executable keys, missing executable patch sequences, and conflicting same-version definitions stay outside the generated catalog.");
        builder.AppendLine();
        builder.AppendLine("## Application path");
        builder.AppendLine();
        builder.AppendLine("`art.yml` entries remain ordinary RPCS3 cheat patch definitions. R3Config matches them against the selected game's title ID, application version, and executable fingerprint where available. The dependency overlay is accepted only when its source revision and `art.yml` hash match this notice, and every reviewed rule resolves to one exact entry. Enable planning enforces ALL/ANY prerequisites; disable planning blocks removal while an enabled dependent remains. The user must explicitly select a patch. Apply/remove operations use the existing managed title-specific patch file, patch configuration, mutation guards, backup, validation, and rollback mechanisms.");
        builder.AppendLine();
        builder.AppendLine("Only `Data/art.yml` is shipped as executable Artemis cheat data. `Data/art_dep.json` is bounded, non-executable planning metadata pinned to that exact catalog. Legacy Artemis sidecars/reference databases, raw NCL libraries, native/reference projection layers, community evidence adapters, and live-memory execution components are not part of this product path.");

        return builder
            .ToString()
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);
    }

    private static RejectedReasonCount[] RejectedReasonCounts(
        IReadOnlyList<ArtemisRejectedEntry> rejected)
    {
        var priority = new Dictionary<string, int>(
            StringComparer.Ordinal)
        {
            ["ARTEMIS_RUNTIME_KEY"] = 0,
            ["ARTEMIS_PATCH_MISSING"] = 1,
            ["ARTEMIS_SOURCE_INVALID"] = 2,
            ["ARTEMIS_DUPLICATE_SUPERSEDED"] = 3,
            ["ARTEMIS_DUPLICATE_CONFLICT"] = 4
        };

        return rejected
            .GroupBy(
                item => item.Code,
                StringComparer.Ordinal)
            .Select(group => new RejectedReasonCount(
                group.Key,
                group.Count()))
            .OrderBy(item => priority.TryGetValue(item.Code, out var rank)
                ? rank
                : int.MaxValue)
            .ThenBy(
                item => item.Code,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] ReadEntryBytes(
        ZipArchiveEntry entry,
        long maxEntryBytes,
        long maxExpandedBytes,
        ref long expandedBytes)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long entryBytes = 0;

        while (true)
        {
            var read = input.Read(
                buffer,
                0,
                buffer.Length);
            if (read == 0)
            {
                break;
            }

            entryBytes = checked(entryBytes + read);
            expandedBytes = checked(expandedBytes + read);
            if (entryBytes > maxEntryBytes)
            {
                throw new InvalidDataException(
                    $"ZIP entry exceeds the {maxEntryBytes}-byte expanded input limit: {entry.FullName}");
            }

            if (expandedBytes > maxExpandedBytes)
            {
                throw new InvalidDataException(
                    $"The source archive exceeds the {maxExpandedBytes}-byte expanded input limit.");
            }

            output.Write(
                buffer,
                0,
                read);
        }

        return output.ToArray();
    }

    private static string DecodeUtf8(
        byte[] content,
        UTF8Encoding utf8)
    {
        var offset = content.Length >= 3 &&
                     content[0] == 0xEF &&
                     content[1] == 0xBB &&
                     content[2] == 0xBF
            ? 3
            : 0;

        return utf8.GetString(
            content,
            offset,
            content.Length - offset);
    }

    private static void ValidateEntryPath(string fullName)
    {
        var hasDrivePrefix = fullName.Length >= 3 &&
                             char.IsAsciiLetter(fullName[0]) &&
                             fullName[1] == ':' &&
                             fullName[2] == '/';
        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Contains('\0') ||
            fullName.Contains('\\') ||
            hasDrivePrefix ||
            Path.IsPathRooted(fullName))
        {
            throw new InvalidDataException(
                $"Unsafe ZIP entry path: {fullName}");
        }

        var segments = fullName.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var trailingDirectory = index == segments.Length - 1 &&
                                    segment.Length == 0;
            if (trailingDirectory)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(segment) ||
                segment is "." or "..")
            {
                throw new InvalidDataException(
                    $"Unsafe ZIP entry path: {fullName}");
            }
        }
    }

    private static byte[] ReadBoundedFile(
        string path,
        long maxBytes,
        string description)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length > maxBytes)
        {
            throw new InvalidDataException(
                $"{description} exceeds the {maxBytes}-byte input limit.");
        }

        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = stream.Read(
                buffer,
                0,
                buffer.Length);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > maxBytes)
            {
                throw new InvalidDataException(
                    $"{description} exceeds the {maxBytes}-byte input limit.");
            }

            output.Write(
                buffer,
                0,
                read);
        }

        return output.ToArray();
    }

    private static void ValidateManagerArchive(byte[] archiveBytes)
    {
        const int maxEntries = 4096;

        using var input = new MemoryStream(
            archiveBytes,
            writable: false);
        using var archive = new ZipArchive(
            input,
            ZipArchiveMode.Read,
            leaveOpen: false);
        if (archive.Entries.Count == 0 ||
            archive.Entries.Count > maxEntries)
        {
            throw new InvalidDataException(
                $"The Artemis manager reference archive must contain between 1 and {maxEntries} entries.");
        }
    }

    private static string DescribeException(Exception exception)
    {
        var messages = new List<string>();
        Add(exception);
        return string.Join(
            " | ",
            messages.Distinct(StringComparer.Ordinal));

        void Add(Exception current)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message);
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    Add(inner);
                }
                return;
            }

            if (current.InnerException is not null)
            {
                Add(current.InnerException);
            }
        }
    }

    private static string CanonicalExistingPath(string path)
    {
        return CanPath.GetPath(path);
    }

    private static string CanonicalTargetPath(string path)
    {
        return CanPath.GetTgt2(path);
    }

    private static void EnsureDistinctPaths(
        params (string Name, string Path)[] paths)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seen = new Dictionary<string, string>(comparer);

        foreach (var item in paths)
        {
            if (seen.TryGetValue(
                    item.Path,
                    out var previous))
            {
                throw new InvalidDataException(
                    $"Artemis import paths must be distinct; '{previous}' and '{item.Name}' resolve to the same path: {item.Path}");
            }

            seen.Add(
                item.Path,
                item.Name);
        }
    }

    private static bool ValidSourceReference(
        string sourceReference,
        string sourceArtifact)
    {
        return !string.IsNullOrWhiteSpace(sourceReference) &&
               sourceReference.Length <= MaxSourceReferenceLength &&
               !sourceReference.Any(char.IsControl) &&
               !string.IsNullOrWhiteSpace(sourceArtifact) &&
               sourceReference.Contains(
                   sourceArtifact,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSerial(string value)
    {
        return value.Length == 9 &&
               value.All(character =>
                   character is >= 'A' and <= 'Z' or
                       >= '0' and <= '9');
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 &&
               value.All(IsAsciiHex);
    }

    private static bool IsAsciiHex(char value)
    {
        return value is >= '0' and <= '9' or
            >= 'a' and <= 'f' or
            >= 'A' and <= 'F';
    }

    private static string RequiredJsonString(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"The Artemis dependency metadata is missing '{propertyName}'.");
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) ||
            text.Length > 512 ||
            text.Any(char.IsControl))
        {
            throw new InvalidDataException(
                $"The Artemis dependency metadata field '{propertyName}' is invalid.");
        }

        return text;
    }

    private static string Sha256Bytes(byte[] content)
    {
        return Convert
            .ToHexString(
                SHA256.HashData(content))
            .ToLowerInvariant();
    }

    private sealed record DependencyNoticeInfo(
        string SourceRevision,
        int RuleCount,
        string Sha256);

    private sealed record RejectedReasonCount(
        string Code,
        int Count);
}
