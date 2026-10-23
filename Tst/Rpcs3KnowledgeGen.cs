using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;
using R3Config.Svc;

// Development-only generator of the shipped App/Data/rpcs3-knowledge.json. It distils the closed
// evidence pipelines into a compact, deterministic runtime file and ships none of their raw data:
//   - the historical Wiki removal classification (only informative reasons, only for pages that still
//     exist under the exact same title),
//   - the frozen RPCS3 source lifecycle rows,
//   - the community CPU workbook extraction (thread-scaling cells and tier lists).
// It matches nothing by similarity: a workbook or history game that does not equal a current Wiki page
// title exactly stays unattached (or is dropped), and every value not proven stays null.
//
//   R3Test.exe --gen-knowledge <removals.csv> <lifecycle.csv> <cpu-dir> <cat.json> <wiki-notes.json> <out.json>
internal static partial class Rpcs3KnowledgeGen
{
    private const string DataVersion = "2026-09-20.1";
    private const string WikiSnapshotDate = "2026-09-20";
    private const string FrozenHead = "9e86f165d1711b9429d48b0487e7bc5ba0cc9c6c";

    [GeneratedRegex(@"^(?<c>\d+)C/(?<t>\d+)T$", RegexOptions.CultureInvariant)]
    private static partial Regex CoreThread();

    public static void Run(string removals, string lifecycle, string cpuDir, string catJson, string notesJson, string outPath)
    {
        var text = Generate(removals, lifecycle, cpuDir, catJson, notesJson, out var summary);
        File.WriteAllText(outPath, text, new UTF8Encoding(false));
        Console.WriteLine(summary);
    }

    public static string Generate(string removals, string lifecycle, string cpuDir, string catJson, string notesJson, out string summary)
    {
        var pages = CurrentPages(catJson, notesJson);

        var removalRows = Csv(removals);
        var shipped = new List<HistoricalRemovalFact>();
        var droppedUnresolved = 0;
        foreach (var row in removalRows)
        {
            if (!Enum.TryParse<RemovalReason>(row["RemovalReason"], out var reason) ||
                reason is not (RemovalReason.ClaimedFixed or RemovalReason.SettingRemoved or RemovalReason.Replaced))
            {
                continue;
            }

            if (!pages.Contains(row["Game"]))
            {
                droppedUnresolved++;
                continue;
            }

            shipped.Add(new HistoricalRemovalFact(
                row["Game"],
                row["CanonicalFieldKey"],
                reason,
                row["Basis"],
                row["WikiObservedUntil"],
                Comment(row["EditComment"])));
        }

        var settings = Csv(lifecycle).Select(row => new SettingLifecycleFact(
            row["WikiKey"],
            row["ConfigDisplayName"],
            bool.Parse(row["DeclaredAtFrozenHead"]),
            row["LastChangeBuild"].Length == 0 ? null : int.Parse(row["LastChangeBuild"], CultureInfo.InvariantCulture),
            row["LastChangeCommit"].Length == 0 ? null : row["LastChangeCommit"],
            row["Evidence"])).ToArray();

        var scalingRows = Csv(Path.Combine(cpuDir, "05-Thread-Scaling.csv"));
        var scaling = scalingRows.Select(row =>
        {
            var config = row["CoreThreadConfig"];
            var match = CoreThread().Match(config);
            return new ThreadScalingFact(
                row["Game"],
                pages.Contains(row["Game"]) ? row["Game"] : null,
                Fps(row["FpsTarget"]),
                config,
                match.Success ? int.Parse(match.Groups["c"].Value, CultureInfo.InvariantCulture) : null,
                match.Success ? int.Parse(match.Groups["t"].Value, CultureInfo.InvariantCulture) : null,
                row["Grade"],
                row["SourceLink"].Length == 0 ? null : row["SourceLink"],
                Comment(row["CellNote"]));
        }).ToArray();

        var tiers = Csv(Path.Combine(cpuDir, "03-Desktop-Cpu-Tiers.csv"))
            .Concat(Csv(Path.Combine(cpuDir, "04-Mobile-Cpu-Tiers.csv")))
            .Select(row => new CpuTierFact(row["List"], row["Block"], row["Tier"], Regex.Replace(row["CPU"], @"\s+", " ").Trim()))
            .ToArray();

        var ord = StringComparer.Ordinal;
        var document = new Rpcs3KnowledgeDocument(
            Rpcs3KnowledgeCatalog.SchemaVersion,
            DataVersion,
            [
                Source("wiki-history-removals", "HistoricalWiki", removals, WikiSnapshotDate,
                    "Editor-claimed removals only; ClaimedFixed is not verified and never a build boundary. Bulk cleanups and unclassified removals are not shipped."),
                Source("rpcs3-source-lifecycle", "Rpcs3Source", lifecycle, null,
                    $"Frozen RPCS3 source {FrozenHead}. Build numbers are git commit counts. Lifecycle only."),
                Source("cpu-workbook-thread-scaling", "HardwareData", Path.Combine(cpuDir, "05-Thread-Scaling.csv"), "2026-06-06",
                    "Community workbook. Informational hardware evidence; never applied."),
                Source("cpu-workbook-tiers", "HardwareData", Path.Combine(cpuDir, "03-Desktop-Cpu-Tiers.csv"), "2025-07-08",
                    "Desktop and mobile tier lists (mobile file hashed in the note). Informational hardware evidence; never applied.")
            ],
            settings.OrderBy(f => f.WikiKey, ord).ToArray(),
            shipped.OrderBy(f => f.Page, ord).ThenBy(f => f.WikiKey, ord).ThenBy(f => f.ObservedUntil, ord).ToArray(),
            scaling.OrderBy(f => f.Game, ord).ThenBy(f => f.FpsTarget, ord).ThenBy(f => f.CoreThreadConfig, ord).ToArray(),
            tiers.OrderBy(f => f.List, ord).ThenBy(f => f.Block, ord).ThenBy(f => f.Tier, ord).ThenBy(f => f.Cpu, ord).ToArray());

        // The mobile tier file is a second input of the last source.
        var mobileSha = Hash(Path.Combine(cpuDir, "04-Mobile-Cpu-Tiers.csv"));
        var sources = document.Sources.ToArray();
        sources[3] = sources[3] with { Note = sources[3].Note + $" Mobile list SHA-256 {mobileSha}." };
        document = document with { Sources = sources };

        var textOut = Rpcs3KnowledgeCatalog.Serialize(document);
        summary = $"Knowledge: {document.Settings.Count} lifecycle, {document.HistoricalRemovals.Count} removals " +
                  $"({droppedUnresolved} dropped: page title not current), {document.ThreadScaling.Count} thread-scaling " +
                  $"({document.ThreadScaling.Count(f => f.WikiPage is not null)} attached by exact title), {document.CpuTiers.Count} tiers, {textOut.Length} chars.";
        return textOut;
    }

    // "30.0" is 30; a range such as "30-60" is kept as written, never turned into a number.
    private static string? Fps(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0) return null;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("0.##", CultureInfo.InvariantCulture)
            : text;
    }

    private static KnowledgeSource Source(string id, string kind, string file, string? date, string note) =>
        new(id, kind, Path.GetFileName(file), Hash(file), date, note);

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    // A comment is history for a person to read: one line, no control characters, bounded. Bot edit
    // summaries carry the whole replaced page and are dropped.
    private static string? Comment(string raw)
    {
        var line = Regex.Replace(raw, @"\s+", " ").Trim();
        line = new string(line.Where(c => !char.IsControl(c)).ToArray());
        if (line.Length == 0 || line.StartsWith("Text replacement", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return line.Length <= 300 ? line : line[..297] + "...";
    }

    // Every Wiki page title the shipped data knows: the pages of the settings catalog and of the notes file.
    private static HashSet<string> CurrentPages(string catJson, string notesJson)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        using (var cat = JsonDocument.Parse(File.ReadAllBytes(catJson)))
        {
            foreach (var rec in cat.RootElement.GetProperty("Recommendations").EnumerateArray())
            {
                if (rec.TryGetProperty("WikiTitle", out var title) && title.GetString() is { Length: > 0 } text)
                {
                    titles.Add(text);
                }
            }
        }

        using var notes = JsonDocument.Parse(File.ReadAllBytes(notesJson));
        foreach (var page in notes.RootElement.GetProperty("Pages").EnumerateArray())
        {
            if (page.TryGetProperty("Page", out var title) && title.GetString() is { Length: > 0 } text)
            {
                titles.Add(text);
            }
        }

        return titles;
    }

    private static List<Dictionary<string, string>> Csv(string path)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        var quoted = false;
        var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = [];
                    break;
                default: field.Append(c); break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        var header = rows[0];
        var result = new List<Dictionary<string, string>>();
        foreach (var data in rows.Skip(1).Where(r => r.Count > 1 || r.Count == 1 && r[0].Length > 0))
        {
            if (data.Count != header.Count)
            {
                throw new InvalidDataException($"{Path.GetFileName(path)}: a row has {data.Count} fields, expected {header.Count}.");
            }

            result.Add(header.Zip(data).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal));
        }

        return result;
    }
}
