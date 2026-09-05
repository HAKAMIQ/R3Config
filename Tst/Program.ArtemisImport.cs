using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static void Cat_Dup()
    {
        var reader = new R3Config.Svc.ReadSvc();
        var read = reader.ReadText(DupHash());

        Assert(read.IsOk, "Expected duplicate top-level RPCS3 hash sections to read successfully: " + read.Error);
        Assert(read.Catalog.Entries.Count == 2, "Expected both duplicate hash sections to contribute patches.");
        Assert(read.Catalog.Entries.All(patch => patch.Hash == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
            "Expected duplicate hash sections to preserve their original PPU hash.");
        Assert(read.Catalog.Entries.Any(patch => patch.Title == "First Patch"),
            "Expected first duplicate hash section to remain visible.");
        Assert(read.Catalog.Entries.Any(patch => patch.Title == "Second Patch"),
            "Expected second duplicate hash section to remain visible.");

        Assert(ExeHash.TryNorm(
                   "QoiFRPF8j00N996aQMthtWqEUG0Y",
                   out var normalizedBase57Hash) &&
               normalizedBase57Hash.Equals(
                   "728fc4496a4c347a654ab54d3e3f0e44ed3c9af6",
                   StringComparison.Ordinal),
            "Expected RPCS3's 28-character Base57 SHA-1 key to normalize to the canonical 40-hex executable hash.");
        Assert(ExeHash.TryNorm(
                   "CZX99Q7PX8cN5P8LbjdjNq35CjvH",
                   out var ascensionModuleHash) &&
               ascensionModuleHash.Equals(
                   "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c",
                   StringComparison.Ordinal),
            "Expected a partial RPCS3 Base57 block to restore its zero-padded bytes instead of rounding the SHA-1 down.");
        Assert(!ExeHash.TryNorm(
                   "QoiFRPF8j00N996aQMthtWqEUG0B",
                   out _),
            "Expected invalid RPCS3 Base57 alphabet characters to be rejected.");

        var strictHashText =
            "Version: 1.2\n" +
            $"PPU-{new string('a', 39)}:\n" +
            "  \"Short Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            $"PPU-{new string('b', 41)}:\n" +
            "  \"Long Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "SPU-gggggggggggggggggggggggggggggggggggggggg:\n" +
            "  \"Non Hex Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "PPU-0000000000000000000000000000000000000000:\n" +
            "  \"Zero Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "PPU-QoiFRPF8j00N996aQMthtWqEUG0Y:\n" +
            "  \"Invalid Base57 PPU Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "PRX-QoiFRPF8j00N996aQMthtWqEUG0Y-0:\n" +
            "  \"RPCS3 PRX Base57 Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:\n" +
            "  \"Exact Hash\":\n" +
            "    Games:\n" +
            "      \"Hash Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n";

        var strictHashRead = reader.ReadText(strictHashText);
        Assert(strictHashRead.IsOk,
            "Expected malformed executable hash sections to be skipped without invalidating the full catalog.");
        Assert(strictHashRead.Catalog.Entries.Count == 2 &&
               strictHashRead.Catalog.Entries.Any(entry => entry.Title == "Exact Hash") &&
               strictHashRead.Catalog.Entries.Any(entry =>
                   entry.Title == "RPCS3 PRX Base57 Hash" &&
                   entry.NormHash ==
                       "728fc4496a4c347a654ab54d3e3f0e44ed3c9af6-0"),
            "Expected PPU/SPU/OVL 40-hex keys and RPCS3 PRX Base57-plus-segment keys to enter the catalog.");
        Assert(!string.IsNullOrWhiteSpace(strictHashRead.Error) &&
               strictHashRead.Error.Contains("40-hex", StringComparison.OrdinalIgnoreCase) &&
               strictHashRead.Error.Contains("Base57", StringComparison.OrdinalIgnoreCase),
            "Expected malformed executable hash sections to produce reviewable diagnostics.");

        var base57Patch = strictHashRead.Catalog.Entries.Single(entry =>
            entry.Title == "RPCS3 PRX Base57 Hash");
        var base57Game = new Rpcs3GameDescriptor(
            Serial: "BLUS00001",
            Title: "Hash Game",
            InstPath: @"C:\Games\HashGame",
            Version: "01.00",
            PpuHash: null,
            SpuHash: null,
            HashSrc: "RPCS3.log",
            HashMsg: "Base57 normalization regression.",
            PrxRaw: ["728fc4496a4c347a654ab54d3e3f0e44ed3c9af6-0"]);
        var base57Match = new R3Config.Svc.PatchMatchService().Match(base57Patch, [base57Game]);
        Assert(base57Match.Status == MatStat.Match,
            "Expected RPCS3 PRX Base57 runtime keys to match canonical module evidence.");

        Console.WriteLine("Patch catalog duplicate top-level hash smoke PASS.");
    }

    static void Art_Imp()
    {
        var validSource = new ArtemisSourceFile(
            "Collection Fixture",
            "Version: 1.2\n" +
            "Anchors:\n" +
            "  Fixture: &Fixture\n" +
            "    - [ be32, 0x00000004, 0x60000000 ]\n" +
            "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:\n" +
            "  \"Infinite Health\":\n" +
            "    Games:\n" +
            "      \"Fixture Game (Artemis)\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Author: Fixture\n" +
            "    Notes: Collection importer fixture\n" +
            "    Patch Version: 1.2\n" +
            "    Patch:\n" +
            "      - [ load, *Fixture ]\n");
        var invalidSource = new ArtemisSourceFile(
            "Invalid Runtime Key",
            "PPU-03a0b43e4a5f4bfea64f53612ee7c5d990f88129c:\n" +
            "  \"Rejected Patch\":\n" +
            "    Games:\n" +
            "      \"Fixture Game\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Patch Version: 1.2\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n");

        var importer = new ArtemisCatalogImporter();
        var first = importer.Import([invalidSource, validSource]);
        var replay = importer.Import([validSource, invalidSource]);

        Assert(
            first.IsOk &&
            first.CandidateEntries == 1 &&
            first.AcceptedEntries == 1 &&
            first.RejectedEntries.Count == 1,
            "Expected the deterministic Artemis collection importer to retain one valid RPCS3 patch and reject the invalid runtime key: " + first.Error);
        Assert(
            first.CatalogText == replay.CatalogText &&
            first.RejectedEntries.SequenceEqual(replay.RejectedEntries),
            "Expected Artemis collection output and rejection order to be deterministic.");
        Assert(
            !first.CatalogText.Contains("Anchors:", StringComparison.Ordinal) &&
            !first.CatalogText.Contains("&Fixture", StringComparison.Ordinal),
            "Expected Artemis collection aliases to be expanded in the generated catalog.");

        var read = new ReadSvc().ReadText(
            first.CatalogText,
            PatSrc.Artemis,
            "art.yml");
        Assert(
            read.IsOk &&
            read.Catalog.Entries.Count == 1 &&
            read.Catalog.Entries[0].Source == PatSrc.Artemis &&
            read.Catalog.Entries[0].Risk == PatRisk.Cheat &&
            read.Catalog.Entries[0].GameTgt.Single().Serial == "BLUS00001",
            "Expected generated art.yml data to pass the normal RPCS3 patch reader with Artemis source identity: " + read.Error);

        Console.WriteLine("Artemis RPCS3 patch collection importer smoke tests passed.");
    }

    static void Art_Tool(string tempRoot)
    {
        var root = Path.Combine(
            tempRoot,
            "art-import-tool");
        Directory.CreateDirectory(root);

        const string sourceName = "Fixture.yml";
        const string sourceText =
            "Version: 1.2\n" +
            "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:\n" +
            "  \"Button Enable Code\":\n" +
            "    Games:\n" +
            "      \"Fixture Game (Artemis)\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Author: Fixture\n" +
            "    Notes: Required fixture prerequisite\n" +
            "    Patch Version: 1.2\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000004, 0x60000000 ]\n" +
            "  \"Infinite Health\":\n" +
            "    Games:\n" +
            "      \"Fixture Game (Artemis)\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Author: Fixture\n" +
            "    Notes: Import tool fixture\n" +
            "    Patch Version: 1.2\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00000008, 0x60000000 ]\n";

        var expectedImport = new ArtemisCatalogImporter().Import(
            [new ArtemisSourceFile(sourceName, sourceText)]);
        Assert(
            expectedImport.IsOk &&
            expectedImport.CandidateEntries == 2 &&
            expectedImport.AcceptedEntries == 2,
            "Expected the Artemis import tool fixture to produce two catalog entries.");

        var sourcePath = Path.Combine(
            root,
            "Artemis-Patch-Collection-RPCS3-fixture.zip");
        using (var archive = ZipFile.Open(
                   sourcePath,
                   ZipArchiveMode.Create))
        {
            WriteZipText(
                archive,
                "Artemis-Patch-Collection-RPCS3-fixture/LICENSE",
                "MIT License\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\n");
            WriteZipText(
                archive,
                "Artemis-Patch-Collection-RPCS3-fixture/README.md",
                "Fixture\n");
            WriteZipText(
                archive,
                "Artemis-Patch-Collection-RPCS3-fixture/" + sourceName,
                sourceText);
        }

        var managerPath = Path.Combine(
            root,
            "ARTEMIS-RPCS3-Cheat-Manager-fixture.zip");
        using (var archive = ZipFile.Open(
                   managerPath,
                   ZipArchiveMode.Create))
        {
            WriteZipText(
                archive,
                "README.md",
                "Manager fixture\n");
        }

        var sourceHash = HashFile(sourcePath);
        var managerHash = HashFile(managerPath);
        var catalogHash = HashText(expectedImport.CatalogText);
        var dependencyPath = Path.Combine(
            root,
            ArtDepSvc.FileName);
        var dependencyRequirements = new[] { "Button Enable Code" };
        File.WriteAllText(
            dependencyPath,
            JsonSerializer.Serialize(
                new
                {
                    schemaVersion = "1",
                    sourceIdentity = "Artemis-Patch-Collection-RPCS3",
                    sourceRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    artemisSha256 = catalogHash,
                    rules = new[]
                    {
                        new
                        {
                            runKey = "PPU-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            serial = "BLUS00001",
                            version = "01.00",
                            patchTitle = "Infinite Health",
                            mode = "All",
                            requires = dependencyRequirements,
                            evidence = "Fixture dependency evidence."
                        }
                    }
                }),
            new UTF8Encoding(false));
        var dependencyHash = HashFile(dependencyPath);

        var catalogPath = Path.Combine(root, "art.yml");
        var noticePath = Path.Combine(root, "CHEATS.md");
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = ArtemisImportTool.Run(
            [
                sourcePath,
                catalogPath,
                noticePath,
                sourceHash,
                "user-supplied archive " + Path.GetFileName(sourcePath),
                managerPath,
                managerHash
            ],
            stdout,
            stderr);

        Assert(
            exitCode == 0,
            "Expected the hardened Artemis import tool to succeed: " + stderr);
        Assert(
            File.ReadAllText(catalogPath) == expectedImport.CatalogText,
            "Expected the import tool to commit the deterministic generated catalog.");

        var notice = File.ReadAllText(noticePath);
        Assert(
            notice.Contains(
                $"Source archive SHA-256: {sourceHash}",
                StringComparison.Ordinal) &&
            notice.Contains(
                "Candidate entries: 2",
                StringComparison.Ordinal) &&
            notice.Contains(
                "Accepted entries: 2",
                StringComparison.Ordinal) &&
            notice.Contains(
                "Dependency metadata rules: 1",
                StringComparison.Ordinal) &&
            notice.Contains(
                "Dependency source revision: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                StringComparison.Ordinal) &&
            notice.Contains(
                $"Generated art_dep.json SHA-256: {dependencyHash}",
                StringComparison.Ordinal) &&
            notice.Contains(
                $"Manager reference artifact: {Path.GetFileName(managerPath)}",
                StringComparison.Ordinal) &&
            notice.Contains(
                $"Manager reference archive SHA-256: {managerHash}",
                StringComparison.Ordinal),
            "Expected generated CHEATS.md to bind source, dependency, and manager provenance to verified artifacts.");

        var badCatalogPath = Path.Combine(root, "bad-art.yml");
        var badNoticePath = Path.Combine(root, "bad-CHEATS.md");
        stderr.GetStringBuilder().Clear();
        var managerMismatch = ArtemisImportTool.Run(
            [
                sourcePath,
                badCatalogPath,
                badNoticePath,
                sourceHash,
                "user-supplied archive " + Path.GetFileName(sourcePath),
                managerPath,
                new string('0', 64)
            ],
            TextWriter.Null,
            stderr);
        Assert(
            managerMismatch != 0 &&
            !File.Exists(badCatalogPath) &&
            !File.Exists(badNoticePath) &&
            stderr.ToString().Contains(
                "manager reference archive SHA-256 mismatch",
                StringComparison.OrdinalIgnoreCase),
            "Expected a mismatched manager archive hash to fail closed before output mutation.");

        stderr.GetStringBuilder().Clear();
        var unboundReference = ArtemisImportTool.Run(
            [
                sourcePath,
                badCatalogPath,
                badNoticePath,
                sourceHash,
                "unrelated source reference",
                managerPath,
                managerHash
            ],
            TextWriter.Null,
            stderr);
        Assert(
            unboundReference != 0 &&
            !File.Exists(badCatalogPath) &&
            !File.Exists(badNoticePath),
            "Expected an Artemis source reference that does not identify the actual archive artifact to fail closed.");

        var sourceBefore = File.ReadAllBytes(sourcePath);
        stderr.GetStringBuilder().Clear();
        var collidingPath = ArtemisImportTool.Run(
            [
                sourcePath,
                sourcePath,
                badNoticePath,
                sourceHash,
                "user-supplied archive " + Path.GetFileName(sourcePath),
                managerPath,
                managerHash
            ],
            TextWriter.Null,
            stderr);
        Assert(
            collidingPath != 0 &&
            File.ReadAllBytes(sourcePath).SequenceEqual(sourceBefore),
            "Expected source/output path aliasing to be rejected without modifying the source archive.");

        var duplicateSourcePath = Path.Combine(
            root,
            "Artemis-Patch-Collection-RPCS3-duplicate-fixture.zip");
        using (var archive = ZipFile.Open(
                   duplicateSourcePath,
                   ZipArchiveMode.Create))
        {
            WriteZipText(
                archive,
                "root/LICENSE",
                "MIT License\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\n");
            WriteZipText(
                archive,
                "root/A/" + sourceName,
                sourceText);
            WriteZipText(
                archive,
                "root/B/" + sourceName,
                sourceText);
        }

        stderr.GetStringBuilder().Clear();
        var duplicateExit = ArtemisImportTool.Run(
            [
                duplicateSourcePath,
                badCatalogPath,
                badNoticePath,
                HashFile(duplicateSourcePath),
                "user-supplied archive " + Path.GetFileName(duplicateSourcePath),
                managerPath,
                managerHash
            ],
            TextWriter.Null,
            stderr);
        Assert(
            duplicateExit != 0 &&
            stderr.ToString().Contains(
                "duplicate patch file identity",
                StringComparison.OrdinalIgnoreCase),
            "Expected duplicate ZIP leaf identities to be rejected rather than collapsed into one Artemis source name.");

        var txnRoot = Path.Combine(root, "txn");
        Directory.CreateDirectory(txnRoot);
        var firstPath = Path.Combine(txnRoot, "first.txt");
        var secondPath = Path.Combine(txnRoot, "second.txt");
        File.WriteAllText(firstPath, "first-before", new UTF8Encoding(false));
        File.WriteAllText(secondPath, "second-before", new UTF8Encoding(false));

        try
        {
            ArtifactPairTransaction.Write(
                firstPath,
                "first-after",
                secondPath,
                "second-after",
                step =>
                {
                    if (step == 1)
                    {
                        throw new IOException("Injected commit failure.");
                    }
                });
            throw new InvalidOperationException(
                "Expected the injected Artemis pair commit failure.");
        }
        catch (IOException ex) when (
            ex.Message.Contains(
                "Injected commit failure",
                StringComparison.Ordinal))
        {
        }

        Assert(
            File.ReadAllText(firstPath) == "first-before" &&
            File.ReadAllText(secondPath) == "second-before",
            "Expected the Artemis artifact transaction to restore both original outputs after a mid-commit failure.");

        var recoveryRoot = Path.Combine(root, "recovery");
        Directory.CreateDirectory(recoveryRoot);
        var recoveryFirst = Path.Combine(recoveryRoot, "art.yml");
        var recoverySecond = Path.Combine(recoveryRoot, "CHEATS.md");
        var recoveryFirstBackup = Path.Combine(recoveryRoot, ".art.yml.r3artimport.backup");
        var recoverySecondBackup = Path.Combine(recoveryRoot, ".CHEATS.md.r3artimport.backup");
        var recoveryJournal = Path.Combine(recoveryRoot, ".art.yml.r3artimport.txn.json");
        File.WriteAllText(recoveryFirst, "new-first", new UTF8Encoding(false));
        File.WriteAllText(recoverySecond, "old-second", new UTF8Encoding(false));
        File.WriteAllText(recoveryFirstBackup, "old-first", new UTF8Encoding(false));
        File.WriteAllText(recoverySecondBackup, "old-second", new UTF8Encoding(false));
        File.WriteAllText(
            recoveryJournal,
            JsonSerializer.Serialize(
                new
                {
                    Phase = "prepared",
                    FirstPath = recoveryFirst,
                    SecondPath = recoverySecond,
                    FirstOriginalExisted = true,
                    FirstOriginalHash = HashText("old-first"),
                    SecondOriginalExisted = true,
                    SecondOriginalHash = HashText("old-second"),
                    FirstCommittedHash = HashText("new-first"),
                    SecondCommittedHash = HashText("new-second"),
                    FirstBackupPath = recoveryFirstBackup,
                    SecondBackupPath = recoverySecondBackup
                }),
            new UTF8Encoding(false));

        ArtifactPairTransaction.RecoverInterrupted(
            recoveryJournal,
            recoveryFirst,
            recoverySecond,
            recoveryFirstBackup,
            recoverySecondBackup);
        Assert(
            File.ReadAllText(recoveryFirst) == "old-first" &&
            File.ReadAllText(recoverySecond) == "old-second" &&
            !File.Exists(recoveryJournal) &&
            !File.Exists(recoveryFirstBackup) &&
            !File.Exists(recoverySecondBackup),
            "Expected an interrupted Artemis two-file transaction to recover the byte-equivalent pre-commit state on the next run.");

        var externalRoot = Path.Combine(root, "external-rollback");
        Directory.CreateDirectory(externalRoot);
        var externalFirst = Path.Combine(externalRoot, "first.txt");
        var externalSecond = Path.Combine(externalRoot, "second.txt");
        File.WriteAllText(externalFirst, "first-before", new UTF8Encoding(false));
        File.WriteAllText(externalSecond, "second-before", new UTF8Encoding(false));
        var preservedExternal = false;
        try
        {
            ArtifactPairTransaction.Write(
                externalFirst,
                "first-after",
                externalSecond,
                "second-after",
                step =>
                {
                    if (step == 1)
                    {
                        File.WriteAllText(
                            externalFirst,
                            "external-change",
                            new UTF8Encoding(false));
                        throw new IOException("Injected external-change failure.");
                    }
                });
        }
        catch (IOException ex)
        {
            preservedExternal = ex.Message.Contains(
                "rollback could not safely restore",
                StringComparison.OrdinalIgnoreCase);
        }

        Assert(
            preservedExternal &&
            File.ReadAllText(externalFirst) == "external-change" &&
            File.ReadAllText(externalSecond) == "second-before",
            "Expected rollback to preserve an output that changed externally after commit while restoring untouched committed targets safely.");

        Console.WriteLine(
            "Artemis build-time importer hardening and transaction regressions PASS.");

        static void WriteZipText(
            ZipArchive archive,
            string name,
            string text)
        {
            var entry = archive.CreateEntry(
                name,
                CompressionLevel.NoCompression);
            using var stream = entry.Open();
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false));
            writer.Write(text);
        }

        static string HashFile(string path)
        {
            return Convert
                .ToHexString(
                    SHA256.HashData(
                        File.ReadAllBytes(path)))
                .ToLowerInvariant();
        }

        static string HashText(string text)
        {
            return Convert
                .ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(text)))
                .ToLowerInvariant();
        }
    }

    static void Art_Data()
    {
        var repoRoot = RepoRoot();
        var catalogPath = Path.Combine(repoRoot, "App", "Data", "art.yml");
        var noticePath = Path.Combine(repoRoot, "CHEATS.md");

        Assert(File.Exists(catalogPath),
            "Expected App/Data/art.yml to contain the retained Artemis RPCS3 patch collection.");
        Assert(BundledCheatPatchService.BndlName == "art.yml",
            "Expected the application adapter to load only art.yml for bundled Artemis cheats.");

        var catalogText = File.ReadAllText(catalogPath);
        var read = new ReadSvc().Read(catalogPath, PatSrc.Artemis);
        Assert(
            read.IsOk &&
            string.IsNullOrWhiteSpace(read.Error) &&
            read.Catalog.Entries.Count == 1471,
            "Expected the user-supplied Artemis patch collection snapshot to round-trip exactly 1471 accepted entries: " + read.Error);
        Assert(
            read.Catalog.Entries.All(entry =>
                entry.Source == PatSrc.Artemis &&
                entry.Risk == PatRisk.Cheat),
            "Expected every retained Artemis entry to remain a normal RPCS3 cheat patch definition.");
        Assert(
            !catalogText.Contains("Anchors:", StringComparison.Ordinal) &&
            !catalogText.Contains("PPU-03a0b43e4a5f4bfea64f53612ee7c5d990f88129c:", StringComparison.OrdinalIgnoreCase),
            "Expected art.yml to contain expanded aliases and valid RPCS3 runtime keys only.");

        var catalogHash = Convert
            .ToHexString(SHA256.HashData(File.ReadAllBytes(catalogPath)))
            .ToLowerInvariant();
        var notice = File.ReadAllText(noticePath);
        var requiredNoticeLines = new[]
        {
            "Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams",
            "Source archive SHA-256: 26a72f769fb1f7cb51daa8502fbc39b817a904fa1073dfa2c602a8c5f2d76ac9",
            "Source patch files: 178",
            "Candidate entries: 1496",
            "Accepted entries: 1471",
            $"Generated art.yml SHA-256: {catalogHash}",
            "Dependency metadata rules: 23",
            "Dependency source revision: acfd45b61f8295298523548a9ff3406488736d68",
            "Generated art_dep.json SHA-256: ab0eab975a9217e1e91c406c5600fae1291f56498b89f3c28ea2a8d197844d68",
            "Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip",
            "Manager reference archive SHA-256: 372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f"
        };
        Assert(
            requiredNoticeLines.All(line => notice.Contains(line, StringComparison.Ordinal)),
            "Expected CHEATS.md to record the exact retained user-supplied Artemis collection and manager reference snapshots.");

        Console.WriteLine("Artemis RPCS3 patch collection and provenance tests passed.");
    }

}
