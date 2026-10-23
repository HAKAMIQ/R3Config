using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Focused coverage for the same-serial, different-version Discovery tier:
// exact Serial+Version stays ProvenExact and unchanged, a same-serial
// mismatch surfaces separately with VersionProven=false and CanConvert
// always false, a different serial never leaks in, and the two tiers never
// cross-pollute.
internal static class NclSameSerialVersionT
{
    public static void Run()
    {
        ExactMatch_UnchangedBehavior();
        SameSerialDifferentVersion_VisibleButApplyBlocked();
        DifferentSerial_ExcludedFromSameSerialDiscovery();
        ExactAndMismatch_CoexistWithoutPollution();
        MultiSerial_VersionPolicyIsPerSerial();
        Discovery_IsDeterministicWithoutRuntimeDependency();
        NoAutomaticCompatibilityUpgrade();
        Presentation_CheatNameStaysCleanAndMismatchMetadataIsSeparate();
        Console.WriteLine("NclSameSerialVersionT: PASS (exact regression, version-mismatch discovery, serial exclusion, no cross-pollution, multi-serial, determinism, no auto-upgrade, clean presentation).");
    }

    private static void ExactMatch_UnchangedBehavior()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "exact.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Infinite Health", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "01.01", sha);

            Assert(result.IsOk && result.Cheats.Count == 1 &&
                result.Cheats[0].VersionProven && result.Cheats[0].IdentityProven &&
                result.Cheats[0].Version == "01.01",
                "Expected exact Serial+Version lookup to remain ProvenExact and unchanged.");
            Assert(result.OtherVersionCheats.Count == 0,
                "Expected no same-serial-other-version candidates when the exact version already matches.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void SameSerialDifferentVersion_VisibleButApplyBlocked()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "mismatch.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Infinite Health", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);

            Assert(!result.IsOk && result.Code == NclCorpusLookupCode.NoExactTarget &&
                result.Cheats.Count == 0,
                "Expected exact lookup to stay fail-closed for a version the source never declares.");
            Assert(result.OtherVersionCheats.Count == 1 &&
                result.OtherVersionCheats[0].Name == "Infinite Health" &&
                result.OtherVersionCheats[0].Version == "01.01" &&
                !result.OtherVersionCheats[0].VersionProven &&
                !result.OtherVersionCheats[0].CanConvert,
                "Expected the same-serial, different-version cheat to surface for Discovery with Apply blocked.");

            var read = NclCorpusReader.ReadZipCandidate(
                zipPath, sha, result.OtherVersionCheats[0], "BLUS30109", "02.00");
            Assert(!read.IsOk,
                "Expected Apply/read-back to fail closed for a version-unproven candidate.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void DifferentSerial_ExcludedFromSameSerialDiscovery()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "otherserial.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game A BLUS11111 01.00.ncl"] = StaticCheat("Cheat A", "00100000", "60000000"),
                ["codes/Game B BLUS22222 01.00.ncl"] = StaticCheat("Cheat B", "00100004", "60000000"),
            });
            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(zipPath, "BLUS11111", "02.00", sha);

            // BLUS11111's own other-version cheat ("Cheat A") is expected here;
            // what must never happen is a completely different serial's cheat
            // ("Cheat B", bound only to BLUS22222) leaking into this list.
            Assert(result.OtherVersionCheats.Count == 1 &&
                result.OtherVersionCheats[0].Name == "Cheat A",
                "Expected same-serial discovery to surface only this serial's own other-version cheat.");
            Assert(!result.OtherVersionCheats.Any(c => c.Name == "Cheat B"),
                "Expected a completely different serial to never appear in same-serial discovery.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void ExactAndMismatch_CoexistWithoutPollution()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "coexist.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Old Cheat", "00100000", "60000000"),
                ["codes/Game BLUS30109 02.00.ncl"] = StaticCheat("New Cheat", "00100004", "60000000"),
            });
            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);

            Assert(result.IsOk && result.Cheats.Count == 1 &&
                result.Cheats[0].Name == "New Cheat" && result.Cheats[0].VersionProven,
                "Expected the exact-version tier to remain ProvenExact when a mismatched version also exists.");
            Assert(result.OtherVersionCheats.Count == 1 &&
                result.OtherVersionCheats[0].Name == "Old Cheat" &&
                !result.OtherVersionCheats[0].VersionProven,
                "Expected the mismatched-version tier to stay separate.");
            Assert(!result.Cheats.Any(c => c.Name == "Old Cheat") &&
                !result.OtherVersionCheats.Any(c => c.Name == "New Cheat"),
                "Expected zero cross-contamination between the two tiers.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void MultiSerial_VersionPolicyIsPerSerial()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "multiserial.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLES01898 v01.00 av01.01 BLUS31194 v01.00 av01.02.ncl"] =
                    StaticCheat("Regional", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);

            var euExact = NclCorpusReader.LookupZip(zipPath, "BLES01898", "01.01", sha);
            var usExact = NclCorpusReader.LookupZip(zipPath, "BLUS31194", "01.02", sha);
            var euMismatch = NclCorpusReader.LookupZip(zipPath, "BLES01898", "02.00", sha);
            var usMismatch = NclCorpusReader.LookupZip(zipPath, "BLUS31194", "02.00", sha);

            Assert(euExact.IsOk && euExact.Cheats.Count == 1 &&
                usExact.IsOk && usExact.Cheats.Count == 1,
                "Expected both explicitly declared serials to reach ProvenExact at their own paired version.");
            Assert(euMismatch.OtherVersionCheats.Count == 1 &&
                usMismatch.OtherVersionCheats.Count == 1,
                "Expected each declared serial to independently surface same-serial-other-version discovery.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void Discovery_IsDeterministicWithoutRuntimeDependency()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "deterministic.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Infinite Health", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);

            var first = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);
            var second = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);

            Assert(first.OtherVersionCheats.Count == 1 &&
                first.OtherVersionCheats.Count == second.OtherVersionCheats.Count,
                "Expected repeated Discovery calls to return an identical same-serial-other-version count with no external/runtime dependency.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void NoAutomaticCompatibilityUpgrade()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "noupgrade.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Infinite Health", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);
            var result = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);

            Assert(result.OtherVersionCheats.Count == 1 &&
                !result.OtherVersionCheats[0].CanConvert &&
                !result.OtherVersionCheats[0].VersionProven,
                "Expected no binary-evidence upgrade path to exist yet: every same-serial-other-version candidate stays CanConvert=false and VersionProven=false.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    // Presentation-only: the cheat name/Display must stay exactly the source
    // name, never concatenated with version or compatibility text; that
    // metadata must instead be reachable as its own independently bindable
    // property. Core matching/discovery for both tiers is asserted first and
    // must remain byte-for-byte the same as the other tests in this file.
    private static void Presentation_CheatNameStaysCleanAndMismatchMetadataIsSeparate()
    {
        var root = NewTempDirectory();
        try
        {
            var zipPath = Path.Combine(root, "presentation.zip");
            CreateZip(zipPath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codes/Game BLUS30109 01.01.ncl"] = StaticCheat("Infinite Health", "00100000", "60000000"),
            });
            var sha = FileSha256(zipPath);

            var exact = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "01.01", sha);
            Assert(exact.IsOk && exact.Cheats.Count == 1 && exact.Cheats[0].Name == "Infinite Health",
                "Expected exact-match Core identity to remain unchanged.");

            var mismatch = NclCorpusReader.LookupZip(zipPath, "BLUS30109", "02.00", sha);
            Assert(!mismatch.IsOk &&
                mismatch.OtherVersionCheats.Count == 1 &&
                mismatch.OtherVersionCheats[0].Name == "Infinite Health" &&
                mismatch.OtherVersionCheats[0].Version == "01.01" &&
                !mismatch.OtherVersionCheats[0].VersionProven,
                "Expected same-serial-other-version Core matching/discovery to remain exactly unchanged.");

            var candidate = mismatch.OtherVersionCheats[0];
            var choice = new NclManagedPatchCheatChoice(
                candidate.PrimarySource.CheatIndex,
                candidate.Name,
                candidate.Author,
                candidate.Notes,
                candidate);

            Assert(choice.Name == "Infinite Health" && choice.Display == "Infinite Health",
                "Expected the cheat name and Display to stay exactly the clean source name.");
            Assert(!choice.Name.Contains('(') && !choice.Name.Contains(')') &&
                !choice.Name.Contains('—') && !choice.Name.Contains(candidate.Version, StringComparison.Ordinal),
                "Expected no parentheses, dashes, or version text concatenated into the cheat name.");
            Assert(choice.IsVersionMismatch && !choice.VersionProven && choice.SourceVersion == "01.01",
                "Expected version-mismatch metadata (IsVersionMismatch/VersionProven/SourceVersion) to be independently bindable, separate from Name.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string StaticCheat(string name, string address, string value)
    {
        return string.Concat(name, "\n0\nAuthor\n0 ", address, " ", value, "\n#\n");
    }

    private static void CreateZip(string path, IReadOnlyDictionary<string, string> entries)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var pair in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(pair.Key, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false), bufferSize: 1024, leaveOpen: false);
            writer.Write(pair.Value);
        }
    }

    private static string FileSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "R3Config-NclSameSerialVersionT-" + Guid.NewGuid().ToString("N"));
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
