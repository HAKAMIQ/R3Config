using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

// Development-only generator of the shipped App/Data/diagnostics-kb.json from
// the qualified GitHub regression evidence and the working/broken RPCS3 logs
// attached to those issues. Deterministic; never runs in the product.
//
//   R3Test.exe --gen-diagnostics-kb <indexes-dir> <attachments-dir> <out.json>
//
// An entry is only what the evidence proves: build boundaries, scope and fix
// come from the qualified regression rows; a log signature is added only when
// the same normalized problem event appears in every readable broken log of
// the issue and in no readable working log (same game and platform).
internal static class DiagnosticKbGen
{
    private static readonly JsonSerializerOptions WriteOptions = new(DiagnosticKnowledgeBase.JsonOptions)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] GenericPatterns = ["Verification failed"];

    private static readonly LogCategory[] SignatureCategories =
    [
        LogCategory.Crash, LogCategory.FatalError, LogCategory.Assertion, LogCategory.RsxError,
        LogCategory.VulkanError, LogCategory.OpenglError, LogCategory.RenderingError, LogCategory.ShaderCompilationEvent,
        LogCategory.Timeout, LogCategory.RpcnError, LogCategory.NetworkError, LogCategory.PpuError, LogCategory.SpuError,
        LogCategory.MemoryError, LogCategory.OutOfMemory, LogCategory.AudioError, LogCategory.GameDataError, LogCategory.PrxError
    ];

    public static void Run(string indexes, string attachments, string outPath)
    {
        var qualified = Csv(Path.Combine(indexes, "final-regression-evidence-qualified-complete.csv"));
        var corroborated = Csv(Path.Combine(indexes, "final-corroborated-regressions.csv"));
        var issues = Csv(Path.Combine(indexes, "issues.csv")).ToDictionary(row => row["Number"], StringComparer.Ordinal);

        var entries = new List<DiagnosticEntry>();
        var withSignatures = 0;
        foreach (var row in qualified.OrderBy(row => Int(row["IssueNumber"])).ThenBy(row => Int(row["FirstObservedBrokenBuild"])))
        {
            var number = Int(row["IssueNumber"])!.Value;
            var issue = issues[row["IssueNumber"]];
            var good = Int(row["LastObservedGoodBuild"]);
            var broken = Int(row["FirstObservedBrokenBuild"]);
            var fixedBuild = row["FixCIBuildConfirmed"] == "True" ? Int(row["FixPublishedBuild"]) : null;
            var causing = Int(corroborated.FirstOrDefault(other =>
                                  other["IssueNumber"] == row["IssueNumber"] && other["FirstBrokenBuild"] == row["FirstObservedBrokenBuild"])?["CausingPR"] ?? string.Empty)
                          ?? Int(row["CausingPR"]);
            var titleIds = string.IsNullOrWhiteSpace(row["TitleId"]) ? new List<string>() : [row["TitleId"]];
            var platform = row["Platform"] is "Windows" or "macOS" or "Linux" ? row["Platform"] : null;
            var architecture = string.IsNullOrWhiteSpace(row["Architecture"]) ? null : row["Architecture"];

            var (signatures, corroboratedLogs, workingLogs) = Signatures(
                Path.Combine(attachments, $"issue-{number:D6}"), titleIds, platform, good, broken, fixedBuild);

            var evidence = fixedBuild is not null
                ? DiagnosticEvidenceClass.DeveloperConfirmedFix
                : corroboratedLogs > 0 && workingLogs > 0 || corroborated.Any(other => other["IssueNumber"] == row["IssueNumber"])
                    ? DiagnosticEvidenceClass.CorroboratedRegression
                    : DiagnosticEvidenceClass.ReportedOnly;
            if (signatures.Count > 0)
            {
                withSignatures++;
            }

            var categories = signatures
                .SelectMany(signature => signature.Categories)
                .Distinct()
                .OrderBy(category => category)
                .ToArray();
            entries.Add(new DiagnosticEntry(
                $"gh-{number}-{broken}",
                DiagnosticEntryKind.Regression,
                evidence,
                new DiagnosticSource("GITHUB_ISSUE", issue["HtmlUrl"], issue["Title"], number, issue["CreatedAt"].Length >= 10 ? issue["CreatedAt"][..10] : null),
                row["Scope"],
                new DiagnosticScope(titleIds, platform, architecture),
                good,
                broken,
                fixedBuild,
                causing,
                Int(row["FixPR"]),
                categories,
                [.. signatures.Select(signature => signature.Ref)],
                [],
                string.IsNullOrWhiteSpace(row["Notes"]) ? null : row["Notes"]));
        }

        var document = new DiagnosticKnowledgeDocument(
            DiagnosticKnowledgeBase.SchemaVersion,
            "Generated from the qualified RPCS3 GitHub regression evidence (snapshot 2026-09-17). Build boundaries, scope and fixes come from the qualified regression rows; " +
            "a log signature is included only when the same normalized problem event appears in every readable broken log of the issue and in no readable working log. " +
            "Entries are documented cases, not causes: a match needs the exact signature, the documented scope and the build boundary. Regenerate with R3Test --gen-diagnostics-kb; do not edit by hand.",
            entries);
        File.WriteAllText(outPath, JsonSerializer.Serialize(document, WriteOptions).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
        Console.WriteLine($"DIAGNOSTIC_KB_ENTRIES={entries.Count} WITH_SIGNATURES={withSignatures} SIGNATURES={entries.Sum(entry => entry.Signatures.Count)} " +
                          $"BY_CLASS={string.Join(",", entries.GroupBy(entry => entry.EvidenceClass).Select(group => $"{group.Key}={group.Count()}"))}");
    }

    private sealed record Derived(DiagnosticSignatureRef Ref, IReadOnlyList<LogCategory> Categories);

    private static (List<Derived> Signatures, int Broken, int Working) Signatures(
        string folder, List<string> titleIds, string? platform, int? good, int? broken, int? fixedBuild)
    {
        var workingSets = new List<HashSet<string>>();
        var brokenSets = new List<Dictionary<string, (LogSignature Signature, IReadOnlyList<LogCategory> Categories)>>();

        if (Directory.Exists(folder) && good is not null && broken is not null)
        {
            foreach (var path in Directory.EnumerateFiles(folder).OrderBy(file => file, StringComparer.Ordinal))
            {
                var gz = path.EndsWith(".log.gz", StringComparison.OrdinalIgnoreCase);
                if (!gz && !path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = new FileInfo(path);
                LogSession session;
                try
                {
                    session = Rpcs3LogService.ReadOne(
                        new LogSourceInfo(info.FullName, gz ? LogSourceKind.RotatedGzip : LogSourceKind.Current, info.Length, info.LastWriteTimeUtc));
                }
                catch (InvalidDataException)
                {
                    continue;
                }

                var build = session.Build?.BuildNumber;
                if (build is null || session.Truncated ||
                    (titleIds.Count > 0 && !titleIds.Contains(session.GameSerial ?? string.Empty, StringComparer.OrdinalIgnoreCase)) ||
                    (platform is not null && !(session.OsLine ?? string.Empty).Contains(platform, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var problems = session.Problems
                    .Where(item => !item.IsMinor && item.Level is 'E' or 'F' && item.Signature is not null &&
                                   item.Categories.Any(SignatureCategories.Contains))
                    .GroupBy(item => item.Signature!.Id)
                    .ToDictionary(group => group.Key, group => (group.First().Signature!, group.First().Categories));

                if (build <= good)
                {
                    workingSets.Add([.. problems.Keys]);
                }
                else if (build >= broken && (fixedBuild is null || build < fixedBuild))
                {
                    brokenSets.Add(problems);
                }
            }
        }

        var result = new List<Derived>();
        if (workingSets.Count > 0 && brokenSets.Count > 0)
        {
            var inAllBroken = brokenSets[0].Keys.Where(key => brokenSets.All(set => set.ContainsKey(key)));
            foreach (var key in inAllBroken.Where(key => !workingSets.Any(set => set.Contains(key))).OrderBy(key => key, StringComparer.Ordinal))
            {
                var (signature, categories) = brokenSets[0][key];

                // A generic assertion text is shared by unrelated bugs, so it
                // identifies a case only when the case is also title-scoped.
                if (titleIds.Count == 0 && GenericPatterns.Any(pattern => signature.Pattern.Contains(pattern, StringComparison.Ordinal)))
                {
                    continue;
                }

                result.Add(new Derived(
                    new DiagnosticSignatureRef(signature.Id, signature.Subsystem, signature.Level, signature.Pattern, signature.ErrorCode),
                    categories.Where(SignatureCategories.Contains).ToArray()));
            }
        }

        return (result, brokenSets.Count, workingSets.Count);
    }

    private static int? Int(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    // RFC-4180-style CSV (quoted fields may contain commas, quotes and newlines).
    private static List<Dictionary<string, string>> Csv(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (quoted)
            {
                if (c == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(fields);
                    fields = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(fields);
        }

        var header = records[0].Select(name => name.TrimStart('﻿')).ToList();
        return
        [
            .. records.Skip(1)
                .Where(record => record.Count == header.Count)
                .Select(record => header.Zip(record).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal))
        ];
    }
}
