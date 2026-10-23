using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

internal static class NclCorpusT
{
    public static void Run()
    {
        DirectoryAudit_AggregatesVerifiedStatuses();
        ArtemisAudit_SeparatesOptionsMetadataAndInvalid();
        ZipAudit_UsesWindows1252FallbackWithoutReplacement();
        ZipAudit_RejectsUnsafeLogicalPaths();
        ZipLookup_RequiresExactApplicationVersionAndPairsSerials();
        ZipLookup_DiscoversRuntimeOnlyCheatsWithoutMakingThemConvertible();
        Identity_ThreeDGameHeroesSharesVersionAcrossThreeSerials();
        Identity_AkibaPairsInterleavedSerialsAndVersions();
        Identity_AkibaKeepsSourceAndApplicationVersionsSeparate();
        Identity_DarkSoulsPairsVersionWithEachSerialGroup();
        Identity_DeadIslandRetainsSerialAfterApplicationVersion();
        Identity_AmbiguousBindingDoesNotEraseIndependentTarget();
        Identity_MissingAndMalformedSerialsRemainUnresolved();
        Identity_MissingVersionDoesNotEraseIndependentTarget();
        Identity_SingleDigitVersionTokensStayProvenEvidence();
        ZipLookup_CollapsesProvenDuplicatesAndPreservesVariants();
        ZipCandidateRead_RejectsChangedSourceOrSemanticIdentity();
    }

    private static void DirectoryAudit_AggregatesVerifiedStatuses()
    {
        var root = NewTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "Static Game BLUS12345 01.00.ncl"),
                """
Static
0
Author
0 00100000 60000000
#
""");

            File.WriteAllText(
                Path.Combine(root, "Mixed Game BLES54321 01.01.ncl"),
                """
Runtime
0
Author
01 00100004 00000001
#
Constant
1
Author
0 00100008 00000001
#
""");

            var report = NclCorpusReader.Audit(root);

            Assert(report.FileCount == 2 &&
                   report.CheatCount == 3 &&
                   report.StaticCandidates == 1 &&
                   report.RuntimeEvidenceRequired == 1 &&
                   report.ConstantRuntimeRequired == 1 &&
                   report.Unsupported == 0 &&
                   report.Invalid == 0,
                "Expected directory corpus audit to aggregate semantic statuses exactly.");

            Assert(report.StaticLowered == 1 &&
                   report.StaticLowerRejected == 0 &&
                   report.StaticLowerUnverifiedValueIncrement == 0 &&
                   report.StaticLowerExpansionLimit == 0 &&
                   report.StaticLowerAddressOverflow == 0 &&
                   report.StaticLowerOtherRejected == 0 &&
                   report.LoweredWriteCount == 1 &&
                   report.LoweredByteCount == 4 &&
                   report.LoweredWritesOutsideUInt32 == 0 &&
                   report.LoweredCheatsOutsideUInt32 == 0,
                "Expected the verifier-static fixture to lower to one exact four-byte write without a UInt32 address-range violation.");
            Assert(report.ExactIdentityFiles == 2 &&
                   report.MissingIdentityFiles == 0 &&
                   report.FilesWithParserErrors == 0,
                "Expected exact filename identity and clean parser state for fixtures.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ArtemisAudit_SeparatesOptionsMetadataAndInvalid()
    {
        var root = NewTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "Dialect Game BLUS33333 01.00.ncl"),
                """
Metadata
0
Author-like line
Human readable note
#
Option
0
0 00100000 0000ZZ
[ZZ]0001=One;0002=Two[/ZZ]
#
Unequal B
0
B2 00010000 04000000
B 11223344 AABB
#
Invalid Odd Hex
0
0 00100004 123
#
""");

            var report = NclCorpusReader.Audit(
                root,
                NclDialect.ArtemisPs3);

            Assert(report.Dialect == NclDialect.ArtemisPs3 &&
                   report.CheatCount == 4 &&
                   report.NonExecutableMetadata == 1 &&
                   report.OptionSelectionRequired == 1 &&
                   report.RuntimeEvidenceRequired == 1 &&
                   report.Invalid == 1 &&
                   report.Unsupported == 0,
                "Expected Artemis corpus audit to separate metadata, options, runtime find-replace, and genuinely malformed executable bytes.");

            Assert(report.CheatsWithOptions == 1 &&
                   report.OptionSetCount == 1 &&
                   report.FilesWithParserErrors == 1,
                "Expected Artemis option and parser-error evidence to remain explicit in the corpus report.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ZipAudit_UsesWindows1252FallbackWithoutReplacement()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "corpus.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(
                    "USERLIST/Encoding Game BLUS11111 01.00.ncl");
                using var stream = entry.Open();

                var prefix = Encoding.ASCII.GetBytes(
                    "Static\n0\nAuthor");
                stream.Write(prefix);
                stream.WriteByte(0x92);
                var suffix = Encoding.ASCII.GetBytes(
                    "\n0 00100000 60000000\n#\n");
                stream.Write(suffix);
            }

            var report = NclCorpusReader.Audit(zipPath);

            Assert(report.SourceSha256 is { Length: 64 } &&
                   report.FileCount == 1 &&
                   report.CheatCount == 1 &&
                   report.StaticCandidates == 1,
                "Expected bounded ZIP audit and source SHA-256 evidence.");

            Assert(report.FilesWithWindows1252Fallback == 1 &&
                   report.Files[0].UsedWindows1252Fallback,
                "Expected invalid UTF-8 source bytes to use the explicit Windows-1252 source fallback without replacement characters.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ZipAudit_RejectsUnsafeLogicalPaths()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "unsafe.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(
                    "../Escape BLUS22222 01.00.ncl");
                using var writer = new StreamWriter(
                    entry.Open(),
                    Encoding.UTF8,
                    bufferSize: 1024,
                    leaveOpen: false);
                writer.Write(
                    "Static\n0\nAuthor\n0 00100000 60000000\n#\n");
            }

            var rejected = false;
            try
            {
                _ = NclCorpusReader.Audit(zipPath);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Assert(rejected,
                "Expected unsafe ZIP logical paths to fail closed.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ZipLookup_RequiresExactApplicationVersionAndPairsSerials()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "lookup.zip");
            CreateZip(
                zipPath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["codes/Game BLES01898 v01.00 av01.01 BLUS31194 v01.00 av01.02.ncl"] =
                        StaticCheat("Regional", "00100000", "60000000"),
                    ["codes/Game BLES01898 v01.00 av01.03.ncl"] =
                        StaticCheat("Other Version", "00100004", "60000000")
                });

            var sha = FileSha256(zipPath);
            var eu = NclCorpusReader.LookupZip(
                zipPath,
                "BLES01898",
                "01.01",
                sha);
            var us = NclCorpusReader.LookupZip(
                zipPath,
                "BLUS31194",
                "01.02",
                sha);
            var wrong = NclCorpusReader.LookupZip(
                zipPath,
                "BLES01898",
                "01.02",
                sha);

            Assert(
                eu.IsOk &&
                eu.Cheats.Count == 1 &&
                eu.Cheats[0].Name == "Regional" &&
                eu.OtherVersions.SequenceEqual(["01.03"]),
                "Expected corpus discovery to use the exact BLES application-version pair and report only other versions for the same serial.");

            Assert(
                us.IsOk &&
                us.Cheats.Count == 1 &&
                us.Cheats[0].Name == "Regional",
                "Expected the same multi-region source file to expose the explicitly paired BLUS application version.");

            Assert(
                !wrong.IsOk &&
                wrong.Code == NclCorpusLookupCode.NoExactTarget &&
                !wrong.Cheats.Any(),
                "Expected a serial match with the wrong application version to remain fail-closed rather than leak another region's version.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ZipLookup_DiscoversRuntimeOnlyCheatsWithoutMakingThemConvertible()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "discovery-before-convertibility.zip");
            CreateZip(
                zipPath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["codes/Discovery Game BLUS12345 01.00.ncl"] =
                        "Static\n0\nAuthor\n0 00100000 60000000\n#\n" +
                        "Option\n0\nAuthor\n0 00100004 0000ZZ\n[ZZ]0001=One;0002=Two[/ZZ]\n#\n" +
                        "Runtime Pointer\n0\nAuthor\n01 00100008 00000001\n#\n" +
                        "Constant Writer\n1\nAuthor\n0 0010000C 00000001\n#\n" +
                        "Metadata\n0\nAuthor-like line\nHuman readable note\n#\n" +
                        "Invalid Odd Hex\n0\nAuthor\n0 00100010 123\n#\n"
                });

            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(
                zipPath,
                "BLUS12345",
                "01.00",
                sha);

            Assert(
                result.IsOk &&
                result.Code == NclCorpusLookupCode.Success &&
                result.ExactSourceFileCount == 1 &&
                result.RejectedSourceFileCount == 0 &&
                result.Cheats.Count == 6,
                "Exact identity discovery must expose every parsed NCL cheat before conversion eligibility is considered.");

            var staticCheat = result.Cheats.Single(item => item.Name == "Static");
            var option = result.Cheats.Single(item => item.Name == "Option");
            var runtime = result.Cheats.Single(item => item.Name == "Runtime Pointer");
            var constant = result.Cheats.Single(item => item.Name == "Constant Writer");
            var metadata = result.Cheats.Single(item => item.Name == "Metadata");
            var invalid = result.Cheats.Single(item => item.Name == "Invalid Odd Hex");

            Assert(
                staticCheat.Status == NclVerifyStat.OfflineStaticCandidate &&
                staticCheat.CanConvert &&
                option.Status == NclVerifyStat.OptionSelectionRequired &&
                option.CanConvert &&
                runtime.Status == NclVerifyStat.RuntimeEvidenceRequired &&
                !runtime.CanConvert &&
                constant.Status == NclVerifyStat.ConstantRuntimeRequired &&
                !constant.CanConvert &&
                metadata.Status == NclVerifyStat.NonExecutableMetadata &&
                !metadata.CanConvert &&
                invalid.Status == NclVerifyStat.Invalid &&
                !invalid.CanConvert,
                "Discovery visibility and verified offline convertibility must remain separate for every verifier class.");

            var reread = NclCorpusReader.ReadZipCandidate(
                zipPath,
                sha,
                runtime,
                "BLUS12345",
                "01.00");
            Assert(
                reread.IsOk &&
                reread.Code == NclCorpusCandidateReadCode.Success &&
                reread.Cheat?.Name == "Runtime Pointer",
                "A discovery-only candidate must retain the same source/read-back integrity checks as a convertible candidate.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void Identity_ThreeDGameHeroesSharesVersionAcrossThreeSerials()
    {
        AssertIdentityFixture(
            "3D Dot Game Heroes BLES00875 BLJM60180 BLUS30490 01.00.ncl",
            [("BLES00875", "01.00", null),
             ("BLJM60180", "01.00", null),
             ("BLUS30490", "01.00", null)],
            [("BLES00875", "01.01"), ("BLJM60180", "01.01"), ("BLUS30490", "01.01")]);
    }

    private static void Identity_AkibaPairsInterleavedSerialsAndVersions()
    {
        AssertIdentityFixture(
            "Akiba's Trip 2 Undead And Undressed BLUS31425 01.00 BLJM61105 01.03.ncl",
            [("BLUS31425", "01.00", null), ("BLJM61105", "01.03", null)],
            [("BLUS31425", "01.03"), ("BLJM61105", "01.00")]);
    }

    private static void Identity_AkibaKeepsSourceAndApplicationVersionsSeparate()
    {
        AssertIdentityFixture(
            "Akiba's Trip 2 Undead And Undressed BLUS31425 v01.01 av01.00 BLJM61105 01.03.ncl",
            [("BLUS31425", "01.01", "01.00"), ("BLJM61105", "01.03", null)],
            [("BLUS31425", "01.01"), ("BLUS31425", "01.03"), ("BLJM61105", "01.00")]);
    }

    private static void Identity_DarkSoulsPairsVersionWithEachSerialGroup()
    {
        AssertIdentityFixture(
            "Dark Souls 2 BLUS41045 01.10 BLES02135 BLUS31528 01.00.ncl",
            [("BLUS41045", "01.10", null),
             ("BLES02135", "01.00", null),
             ("BLUS31528", "01.00", null)],
            [("BLUS41045", "01.00"), ("BLES02135", "01.10"), ("BLUS31528", "01.10")]);
    }

    private static void Identity_DeadIslandRetainsSerialAfterApplicationVersion()
    {
        AssertIdentityFixture(
            "Dead Island BLES00749 BLUS30790 v01.00 av01.04 NPEB00721 av01.00.ncl",
            [("BLES00749", "01.00", "01.04"),
             ("BLUS30790", "01.00", "01.04"),
             ("NPEB00721", null, "01.00")],
            [("BLES00749", "01.00"), ("BLUS30790", "01.00"), ("NPEB00721", "01.04")]);
    }

    private static void Identity_AmbiguousBindingDoesNotEraseIndependentTarget()
    {
        const string fileName = "Ambiguous Game BLES12345 v01.00 v01.01 av01.04 BLUS12345 01.02.ncl";
        var identity = NclParser.ParseIdentity(fileName);
        Assert(identity.TargetBindings.Count == 2 &&
               identity.TargetBindings[0].Serial == "BLES12345" &&
               identity.TargetBindings[0].UnresolvedReason is not null &&
               identity.TargetBindings[0].EffectiveVersion is null &&
               identity.TargetBindings[0].Evidence.Any(item => item.Token == "v01.00") &&
               identity.TargetBindings[0].Evidence.Any(item => item.Token == "v01.01") &&
               identity.TargetBindings[0].Evidence.Any(item => item.Token == "av01.04"),
            "Expected conflicting source versions to retain the serial and all conflicting token evidence while withholding its effective version.");
        Assert(identity.TargetBindings[1].Serial == "BLUS12345" &&
               identity.TargetBindings[1].SourceVersion == "01.02" &&
               identity.TargetBindings[1].AppVersion is null &&
               identity.TargetBindings[1].UnresolvedReason is null &&
               identity.MatchesTarget("BLUS12345", "01.02") &&
               identity.Targets.Count == 1,
            "Expected an ambiguous regional binding to leave the independent, proven binding available.");
        AssertIdentityLookup(
            fileName,
            [("BLUS12345", "01.02")],
            [("BLES12345", "01.00"), ("BLES12345", "01.01"), ("BLES12345", "01.04")]);
    }

    private static void Identity_MissingAndMalformedSerialsRemainUnresolved()
    {
        foreach (var fileName in new[]
                 {
                     "3D Dot Game Heroes 01.00.ncl",
                     "Malformed Game BLUS BLES 01.00.ncl",
                     "Malformed Game BLUS1234 BLES123456 BLUS1234567 01.00.ncl"
                 })
        {
            var identity = NclParser.ParseIdentity(fileName);
            Assert(identity.Serials.Count == 0 &&
                   identity.TargetBindings.Count == 0 &&
                   identity.Targets.Count == 0 &&
                   !identity.HasExactTarget &&
                   identity.UnresolvedReason == "UNRESOLVED_NO_SERIAL",
                $"Expected absent or malformed serials to stay explicitly unresolved without title-based guesses: {fileName}");
            AssertIdentityLookup(fileName, [], [("BLUS12345", "01.00"), ("BLES00875", "01.00")]);
        }

        AssertIdentityFixture(
            "Partial Game BLUS BLES1234 BLES123456 BLUS12345 01.00.ncl",
            [("BLUS12345", "01.00", null)],
            [("BLES12345", "01.00")]);
    }

    private static void Identity_MissingVersionDoesNotEraseIndependentTarget()
    {
        const string fileName = "Partial Game BLES12345 01.00 BLUS12345.ncl";
        var identity = NclParser.ParseIdentity(fileName);
        Assert(identity.TargetBindings.Count == 2 &&
               identity.TargetBindings[0].Serial == "BLES12345" &&
               identity.TargetBindings[0].EffectiveVersion == "01.00" &&
               identity.TargetBindings[1].Serial == "BLUS12345" &&
               identity.TargetBindings[1].SourceVersion is null &&
               identity.TargetBindings[1].AppVersion is null &&
               identity.TargetBindings[1].EffectiveVersion is null &&
               identity.TargetBindings[1].UnresolvedReason is not null &&
               identity.Targets.Count == 1,
            "Expected a trailing serial without version evidence to remain unresolved without inheriting another serial's version or erasing it.");
        AssertIdentityLookup(fileName, [("BLES12345", "01.00")], [("BLUS12345", "01.00")]);

        var versionless = NclParser.ParseIdentity("Versionless Game BLUS12345.ncl");
        Assert(versionless.TargetBindings.Count == 1 &&
               versionless.TargetBindings[0].Serial == "BLUS12345" &&
               versionless.TargetBindings[0].UnresolvedReason is not null &&
               !versionless.HasExactTarget,
            "Expected a serial-only filename to retain unresolved serial evidence without inventing a default version.");
        AssertIdentityLookup("Versionless Game BLUS12345.ncl", [], [("BLUS12345", "01.00")]);
    }

    // PatVer, this project's version grammar, accepts one or two digits on each
    // side. The identity grammar demanded two, so real evidence such as
    // "BLUS31162 1.00" was demoted to unknown text and the whole file lost its
    // binding. These four names are taken verbatim from the shipped corpus.
    private static void Identity_SingleDigitVersionTokensStayProvenEvidence()
    {
        AssertIdentityFixture(
            "Battlefield 4 BLUS31162 1.00.ncl",
            [("BLUS31162", "01.00", null)],
            [("BLUS31162", "01.01")]);

        AssertIdentityFixture(
            "Drakengard 3 BLUS31197 v1.01.ncl",
            [("BLUS31197", "01.01", null)],
            [("BLUS31197", "01.00")]);

        AssertIdentityFixture(
            "Killzone 3 EU BCES01007 1.14.ncl",
            [("BCES01007", "01.14", null)],
            [("BCES01007", "01.00")]);

        // A trailing tag after complete identity is a suffix, not a rewrite.
        AssertIdentityFixture(
            "Sonic The Hedgehog 2 - NPUB30443 v1.00 JG.ncl",
            [("NPUB30443", "01.00", null)],
            [("NPUB30443", "01.01")]);
    }

    private static void AssertIdentityFixture(
        string fileName,
        (string Serial, string? SourceVersion, string? AppVersion)[] expected,
        (string Serial, string Version)[] excluded)
    {
        var identity = NclParser.ParseIdentity(fileName);
        Assert(identity.UnresolvedReason is null &&
               identity.TargetBindings.Count == expected.Length &&
               identity.Serials.SequenceEqual(expected.Select(item => item.Serial), StringComparer.Ordinal),
            $"Expected every valid serial to retain its ordered target binding: {fileName}");

        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var index = 0; index < expected.Length; index++)
        {
            var actual = identity.TargetBindings[index];
            var target = expected[index];
            Assert(actual.Serial == target.Serial &&
                   actual.SourceVersion == target.SourceVersion &&
                   actual.AppVersion == target.AppVersion &&
                   actual.EffectiveVersion == (target.AppVersion ?? target.SourceVersion) &&
                   actual.UnresolvedReason is null,
                $"Expected exact source/application version separation for {target.Serial}: {fileName}");
            Assert(actual.Evidence.Any(item => item.Token == target.Serial) &&
                   actual.Evidence.Count >= 2 &&
                   actual.Evidence.All(item =>
                       item.Offset >= 0 && item.Length > 0 &&
                       item.Offset <= stem.Length - item.Length &&
                       stem.Substring(item.Offset, item.Length) == item.Token &&
                       !string.IsNullOrWhiteSpace(item.Kind)),
                $"Expected each binding to retain original serial/version token spans and parse provenance: {fileName}");
            if (target.AppVersion is not null)
            {
                Assert(actual.Evidence.Any(item => item.Token == "av" + target.AppVersion),
                    $"Expected explicit application-version token evidence for {target.Serial}: {fileName}");
            }

            if (target.SourceVersion is not null && target.AppVersion is not null)
            {
                Assert(actual.Evidence.Any(item => item.Token == "v" + target.SourceVersion),
                    $"Expected source-version evidence to survive separately from the application version for {target.Serial}: {fileName}");
            }
        }

        AssertIdentityLookup(
            fileName,
            expected.Select(item => (item.Serial, (item.AppVersion ?? item.SourceVersion)!)).ToArray(),
            excluded);
    }

    private static void AssertIdentityLookup(
        string fileName,
        (string Serial, string Version)[] proven,
        (string Serial, string Version)[] excluded)
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "identity.zip");
            var entryPath = "codes/" + fileName;
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [entryPath] = StaticCheat("Shared Definition", "00100000", "60000000")
            });
            var sha = FileSha256(zipPath);
            var report = NclCorpusReader.Audit(zipPath);
            Assert(report.FileCount == 1 && report.CheatCount == 1,
                "Expected one physical source and one semantic definition regardless of its number of target bindings.");

            NclCorpusCheatCandidate? first = null;
            foreach (var target in proven)
            {
                var lookup = NclCorpusReader.LookupZip(zipPath, target.Serial, target.Version, sha);
                Assert(lookup.IsOk && lookup.Cheats.Count == 1 &&
                       lookup.ExactSourceFileCount == 1,
                    $"Expected exactly one reachable definition for proven target {target.Serial}/{target.Version}: {fileName}");
                var candidate = lookup.Cheats[0];
                Assert(candidate.Name == "Shared Definition" &&
                       candidate.Sources.Count == 1 && candidate.PrimarySource.RelativePath == entryPath &&
                       (first is null ||
                        (first.SemanticSha256 == candidate.SemanticSha256 &&
                         first.Sources.SequenceEqual(candidate.Sources))),
                    "Expected every target to reference the same original cheat and semantic identity without copied source definitions.");
                first ??= candidate;

                var read = NclCorpusReader.ReadZipCandidate(zipPath, sha, candidate, target.Serial, target.Version);
                Assert(read.IsOk && read.Code == NclCorpusCandidateReadCode.Success &&
                       read.Cheat?.Name == "Shared Definition",
                    $"Expected discovered target to survive candidate read-back: {target.Serial}/{target.Version}");
            }

            foreach (var target in excluded)
            {
                var lookup = NclCorpusReader.LookupZip(zipPath, target.Serial, target.Version, sha);
                Assert(!lookup.IsOk && lookup.Code == NclCorpusLookupCode.NoExactTarget &&
                       lookup.Cheats.Count == 0,
                    $"Expected unproven target to remain unreachable: {target.Serial}/{target.Version}: {fileName}");
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    // The corpus contract: a listed cheat is one proven set of effective
    // writes. Definitions collapse only when PatchWriteSemantics proves they
    // write the same bytes - never because a shape-sensitive hash matched, and
    // never by dropping one of two definitions that share a name.
    private static void ZipLookup_CollapsesProvenDuplicatesAndPreservesVariants()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "duplicates.zip");
            CreateZip(
                zipPath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["codes/A Game BLUS12345 01.00.ncl"] =
                        StaticCheat("Same", "00100000", "60000000") +
                        StaticCheat("Conflict", "00100004", "60000000") +
                        StaticCheat2(
                            "Shape",
                            "00100008", "11111111",
                            "0010000C", "22222222"),
                    ["codes/B Game BLUS12345 01.00.ncl"] =
                        StaticCheat("Same", "00100000", "60000000") +
                        StaticCheat("Conflict", "00100004", "60000001") +
                        StaticCheat2(
                            "Shape",
                            "0010000C", "22222222",
                            "00100008", "11111111")
                });

            var result = NclCorpusReader.LookupZip(
                zipPath,
                "BLUS12345",
                "01.00",
                FileSha256(zipPath));

            Assert(result.IsOk && result.Cheats.Count == 4,
                "Expected four listed definitions: one Same, one Shape, and both Conflict variants.");

            var same = result.Cheats
                .Where(item => item.Name == "Same")
                .ToArray();
            Assert(same.Length == 1 &&
                   same[0].Sources.Count == 2 &&
                   !same[0].IsDisambiguated &&
                   same[0].SourceName == "Same",
                "Expected byte-identical duplicate cheats to collapse deterministically, keep the source name, and retain both provenance sources.");

            // The two Shape definitions emit the same writes in opposite
            // order. Their lowered-sequence hashes differ, so collapsing them
            // is only possible through the effective-write projection.
            var shape = result.Cheats
                .Where(item => item.Name == "Shape")
                .ToArray();
            Assert(shape.Length == 1 &&
                   shape[0].Sources.Count == 2 &&
                   !shape[0].IsDisambiguated &&
                   shape[0].Sources
                       .Select(source => source.RelativePath)
                       .Distinct(StringComparer.Ordinal)
                       .Count() == 2,
                "Expected reordered writes with identical effect to collapse on proven effective writes rather than on the shape-sensitive semantic hash.");

            var divergent = result.Cheats
                .Where(item => item.SourceName == "Conflict")
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();
            Assert(divergent.Length == 2 &&
                   divergent.All(item =>
                       item.IsDisambiguated &&
                       item.Sources.Count == 1 &&
                       item.Name.StartsWith("Conflict #", StringComparison.Ordinal) &&
                       item.Name.Length == "Conflict #".Length + 8) &&
                   !divergent[0].Name.Equals(
                       divergent[1].Name,
                       StringComparison.Ordinal),
                "Expected same-name divergent semantics to survive as independent variants under deterministic distinct names.");

            Assert(result.Conflicts.Count == 0,
                "Expected no definition to be suppressed for sharing a name with a different definition.");

            var repeated = NclCorpusReader.LookupZip(
                zipPath,
                "BLUS12345",
                "01.00",
                FileSha256(zipPath));
            Assert(repeated.Cheats
                       .Select(item => item.Name)
                       .SequenceEqual(
                           result.Cheats.Select(item => item.Name),
                           StringComparer.Ordinal),
                "Expected variant names to be a function of variant identity, so repeated discovery is byte-stable.");

            // The read-back path validates against the source spelling, so a
            // disambiguated name must still resolve to its original cheat.
            var read = NclCorpusReader.ReadZipCandidate(
                zipPath,
                FileSha256(zipPath),
                divergent[0],
                "BLUS12345",
                "01.00");
            Assert(read.IsOk &&
                   read.Code == NclCorpusCandidateReadCode.Success &&
                   read.Cheat is not null &&
                   read.Cheat.Name == "Conflict",
                "Expected a disambiguated variant to re-read against its original source name.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ZipCandidateRead_RejectsChangedSourceOrSemanticIdentity()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "stable.zip");
            CreateZip(
                zipPath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["codes/Game BLUS12345 01.00.ncl"] =
                        StaticCheat("Stable", "00100000", "60000000")
                });

            var firstSha = FileSha256(zipPath);
            var lookup = NclCorpusReader.LookupZip(
                zipPath,
                "BLUS12345",
                "01.00",
                firstSha);

            Assert(
                lookup.IsOk && lookup.Cheats.Count == 1,
                "Expected a stable bundled candidate fixture before mutation.");

            var read = NclCorpusReader.ReadZipCandidate(
                zipPath,
                firstSha,
                lookup.Cheats[0],
                "BLUS12345",
                "01.00");

            Assert(
                read.IsOk &&
                read.Code == NclCorpusCandidateReadCode.Success &&
                read.Cheat?.Name == "Stable",
                "Expected an unchanged discovered corpus candidate to revalidate successfully.");

            CreateZip(
                zipPath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["codes/Game BLUS12345 01.00.ncl"] =
                        StaticCheat("Stable", "00100000", "60000001")
                });

            var changed = NclCorpusReader.ReadZipCandidate(
                zipPath,
                firstSha,
                lookup.Cheats[0],
                "BLUS12345",
                "01.00");

            Assert(
                !changed.IsOk &&
                changed.Code == NclCorpusCandidateReadCode.SourceRejected,
                "Expected corpus drift between discovery and preview to abort before the candidate can be converted.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string StaticCheat(
        string name,
        string address,
        string value)
    {
        return string.Concat(
            name, "\n0\nAuthor\n0 ", address, " ", value, "\n#\n");
    }

    // Two direct writes in one cheat, so a fixture can express the same effect
    // in two different operation orders.
    private static string StaticCheat2(
        string name,
        string firstAddress,
        string firstValue,
        string secondAddress,
        string secondValue)
    {
        return string.Concat(
            name, "\n0\nAuthor\n0 ", firstAddress, " ", firstValue,
            "\n0 ", secondAddress, " ", secondValue, "\n#\n");
    }

    private static void CreateZip(
        string path,
        IReadOnlyDictionary<string, string> entries)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var pair in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(
                pair.Key,
                CompressionLevel.NoCompression);
            using var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false),
                bufferSize: 1024,
                leaveOpen: false);
            writer.Write(pair.Value);
        }
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

    private static string NewTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "R3Config-NclCorpusT-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
