using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Svc;

// Development-only generator of the shipped App/Data/rpcs3-builds.json from the
// validated RPCS3 build-history master database and the refreshed complete
// GitHub release listing. It is deterministic (sorted, one row per line) and
// never runs in the product; the shipped file is the only runtime input.
//
//   R3Test.exe --gen-build-catalog <master.json> <github-windows-builds.csv> <out.json>
internal static class Rpcs3BuildCatalogGen
{
    private const string Windows = Rpcs3BuildCatalog.WindowsPlatform;
    private const string Historical = "HISTORICALLY_UNAVAILABLE_CURRENT_GITHUB_PRESENT";

    private static readonly JsonSerializerOptions RowOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Run(string masterPath, string githubCsvPath, string outPath)
    {
        var github = ReadCsv(githubCsvPath);
        using var master = JsonDocument.Parse(File.ReadAllBytes(masterPath));
        var records = master.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var byKey = records.ToDictionary(record => Str(record, "BuildKey"), StringComparer.Ordinal);

        var entries = new List<Rpcs3BuildEntry>();
        var historicalPresent = 0;

        foreach (var row in github)
        {
            var key = $"{row["Version"]}-{row["Build"]}";
            var hasRecord = byKey.TryGetValue(key, out var record);
            var historical = hasRecord && Str(record, "WindowsAvailability") == Historical;
            if (historical)
            {
                historicalPresent++;
            }

            var sha = hasRecord ? Str(record, "SHA256") : string.Empty;
            if (sha.Length == 0 && row["Digest"].StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                sha = row["Digest"][7..].ToUpperInvariant();
            }

            entries.Add(new Rpcs3BuildEntry(
                row["Version"],
                int.Parse(row["Build"], CultureInfo.InvariantCulture),
                Windows,
                row["DownloadURL"],
                CommitFull: row["Tag"].StartsWith("build-", StringComparison.Ordinal) ? row["Tag"][6..] : null,
                SizeBytes: long.Parse(row["SizeBytes"], CultureInfo.InvariantCulture),
                Sha256: sha.Length == 0 ? null : sha,
                SourceClass: Rpcs3BuildCatalog.GitHubDirectClass,
                MatchMethod: "EXACT_VERSION_BUILD",
                HistoricalUnavailable: historical));
        }

        var githubRows = entries.ToDictionary(entry => $"{entry.Version}-{entry.Build}", StringComparer.Ordinal);
        var legacy = 0;
        var archive = 0;
        var directRows = 0;
        var excluded = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            var key = Str(record, "BuildKey");
            var sourceClass = Str(record, "SourceClass");
            var method = Str(record, "MatchMethod");
            var verification = Str(record, "VerificationLevel");
            var (version, build) = Split(key);

            if (sourceClass == Rpcs3BuildCatalog.GitHubDirectClass)
            {
                directRows++;
                var url = Str(record, "DownloadURL");
                if (method == "OFFICIAL_URL_MATCH")
                {
                    entries.Add(new Rpcs3BuildEntry(
                        version,
                        build,
                        Windows,
                        url,
                        CommitFull: Str(record, "CommitFull"),
                        SizeBytes: Size(record),
                        Sha256: Opt(record, "SHA256"),
                        SourceClass: sourceClass,
                        MatchMethod: method));
                    legacy++;
                }
                else if (!githubRows.TryGetValue(key, out var listed) || listed.DownloadUrl != url)
                {
                    throw new InvalidDataException($"Master direct row {key} does not match the GitHub listing.");
                }
            }
            else if (sourceClass == Rpcs3BuildCatalog.ArchivePointerClass)
            {
                if (verification is Rpcs3BuildCatalog.ArchiveMetadataVerified or Rpcs3BuildCatalog.ArchiveIdentityVerified)
                {
                    if (githubRows.ContainsKey(key))
                    {
                        throw new InvalidDataException($"Archive row {key} collides with an official GitHub row.");
                    }

                    entries.Add(new Rpcs3BuildEntry(
                        version,
                        build,
                        Windows,
                        Str(record, "DownloadURL"),
                        CommitFull: Opt(record, "CommitFull"),
                        SizeBytes: Size(record),
                        Sha256: Opt(record, "SHA256"),
                        SourceClass: sourceClass,
                        MatchMethod: method,
                        Verification: verification,
                        Sha1: Opt(record, "SHA1")));
                    archive++;
                }
                else
                {
                    excluded[verification] = excluded.GetValueOrDefault(verification) + 1;
                }
            }
        }

        var sorted = entries
            .OrderBy(entry => entry.Build)
            .ThenBy(entry => entry.Version, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourceClass, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Select(entry => (entry.Version, entry.Build, entry.SourceClass)).Distinct().Count() != sorted.Length)
        {
            throw new InvalidDataException("The generated catalog would hold duplicate Version+Build rows.");
        }

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["officialGitHubRows"] = sorted.Count(entry => entry.SourceClass == Rpcs3BuildCatalog.GitHubDirectClass),
            ["officialGitHubNumberedFromListing"] = github.Count,
            ["legacyOfficialUrlMatchRows"] = legacy,
            ["historicalUnavailableCurrentGitHubPresentRows"] = historicalPresent,
            ["archiveMetadataVerifiedRows"] = sorted.Count(entry => entry.Verification == Rpcs3BuildCatalog.ArchiveMetadataVerified),
            ["archiveIdentityVerifiedRows"] = sorted.Count(entry => entry.Verification == Rpcs3BuildCatalog.ArchiveIdentityVerified),
            ["archiveEvidenceOnlyRows"] = archive
        };
        foreach (var pair in excluded)
        {
            counts["excluded_" + pair.Key] = pair.Value;
        }

        var text = new StringBuilder();
        text.Append("{\n  \"schema\": ").Append(Rpcs3BuildCatalog.SchemaVersion).Append(",\n");
        text.Append("  \"source\": ").Append(JsonSerializer.Serialize(
            "Generated from the validated RPCS3 build-history master database (rpcs3.net builds history captured 2026-09-19, 7536 rows) and the complete RPCS3/rpcs3-binaries-win release listing refreshed 2026-09-19. " +
            "OFFICIAL_GITHUB_DIRECT rows are exact official Windows binaries and the only downloadable rows. OFFICIAL_SITE_ARCHIVE_POINTER rows are historical Internet Archive evidence (metadata/identity verified, never hash verified) and are reference data only: no Archive download is offered. " +
            "Ambiguous or unresolved history rows are not shipped and resolve to nothing. Regenerate with R3Test --gen-build-catalog; do not edit by hand.",
            RowOptions)).Append(",\n");
        text.Append("  \"counts\": ").Append(JsonSerializer.Serialize(counts, RowOptions)).Append(",\n");
        text.Append("  \"builds\": [\n");
        for (var index = 0; index < sorted.Length; index++)
        {
            text.Append("    ").Append(JsonSerializer.Serialize(sorted[index], RowOptions))
                .Append(index + 1 < sorted.Length ? ",\n" : "\n");
        }

        text.Append("  ]\n}\n");
        File.WriteAllText(outPath, text.ToString(), new UTF8Encoding(false));

        Console.WriteLine($"BUILD_CATALOG_ROWS={sorted.Length} GITHUB={counts["officialGitHubRows"]} LEGACY_URL_MATCH={legacy} " +
                          $"HISTORICAL_UNAVAILABLE_PRESENT={historicalPresent} ARCHIVE_EVIDENCE={archive} DIRECT_MASTER_ROWS={directRows}");
        foreach (var pair in excluded)
        {
            Console.WriteLine($"BUILD_CATALOG_EXCLUDED {pair.Key}={pair.Value}");
        }
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? Opt(JsonElement element, string name)
    {
        var value = Str(element, name);
        return value.Length == 0 ? null : value;
    }

    private static long? Size(JsonElement element) =>
        element.TryGetProperty("SizeBytes", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

    private static (string Version, int Build) Split(string key)
    {
        var dash = key.LastIndexOf('-');
        return (key[..dash], int.Parse(key[(dash + 1)..], CultureInfo.InvariantCulture));
    }

    private static List<Dictionary<string, string>> ReadCsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = SplitCsv(reader.ReadLine() ?? throw new InvalidDataException("Empty CSV."));
        var rows = new List<Dictionary<string, string>>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = SplitCsv(line);
            if (fields.Count != header.Count)
            {
                throw new InvalidDataException("CSV row has an unexpected field count.");
            }

            rows.Add(header.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal));
        }

        return rows;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var c = line[index];
            if (quoted)
            {
                if (c == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
