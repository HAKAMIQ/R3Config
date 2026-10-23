using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using R3Config.Mdl;
using R3Config.Svc;

// Gate over the machine-readable discord-bot audit corpus (docs/DiscordBotAudit).
// "Reviewed" is not accepted as a status: every upstream regex, note rule, game
// family and subsystem must carry exactly one known disposition, the counts in
// summary.json must be recomputable from the files, every IMPLEMENTED claim must
// point at code that exists, and the corpus must not store the bot's advice text.
internal static class DiscordBotAuditT
{
    private static readonly string[] RegexDispositions = ["ALREADY_COVERED", "IMPLEMENTED", "QUEUED_FIXTURE", "REFERENCE_ONLY", "SUPERSEDED", "NOT_APPLICABLE"];
    private static readonly string[] NoteDispositions = ["ALREADY_COVERED", "IMPLEMENTED", "REFERENCE_ONLY_CURRENTLY_UNVERIFIED", "SUPERSEDED_BY_CURRENT_EVIDENCE", "NOT_APPLICABLE"];
    private static readonly string[] SubsystemDispositions = ["ALREADY_COVERED", "IMPLEMENTED", "REFERENCE_ONLY", "NOT_APPLICABLE"];
    private static readonly string[] FamilyClassifications = ["OFFICIAL_CURRENT", "CORROBORATED_NON_OFFICIAL", "REFERENCE_ONLY", "SUPERSEDED", "CONFLICT", "OBSOLETE"];

    private static string Directory_()
    {
        var current = new DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs", "DiscordBotAudit");
            if (System.IO.Directory.Exists(candidate) && File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("docs/DiscordBotAudit was not found from the working directory.");
    }

    private static JsonNode Read(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Directory_(), name)))!;

    public static void Run()
    {
        AssertRegexCoverage();
        AssertNoteRules();
        AssertFamilies();
        AssertSubsystems();
        AssertSummaryMatches();

        Console.WriteLine("discord-bot audit corpus (188 regex, note rules, game families, subsystems: one known disposition each, implemented claims resolve to code, counts recompute, no advice text stored) PASS.");
    }

    private static void AssertRegexCoverage()
    {
        var doc = Read("regex-coverage.json");
        var patterns = doc["patterns"]!.AsArray();
        Assert(doc["total"]!.GetValue<int>() == 188 && patterns.Count == 188 &&
               patterns.Select(item => item!["name"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == 188,
            "All 188 upstream regex patterns are present exactly once.");
        Assert(patterns.All(item => RegexDispositions.Contains(item!["disposition"]!.GetValue<string>(), StringComparer.Ordinal)),
            "Every regex has exactly one known disposition (no missing entry, no prose status).");

        var kinds = LogObservationRules.Rules.Select(rule => rule.Kind.ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var item in patterns.Where(item => item!["disposition"]!.GetValue<string>() == "IMPLEMENTED"))
        {
            var by = item!["implementedBy"]?.GetValue<string>();
            Assert(!string.IsNullOrEmpty(by), $"Implemented pattern {item["name"]} names its implementation.");
            if (by!.StartsWith("LogObservationKind.", StringComparison.Ordinal))
            {
                Assert(Enum.TryParse<LogObservationKind>(by["LogObservationKind.".Length..], out var kind) && kinds.Contains(kind.ToString()),
                    $"Implemented pattern {item["name"]} points at an observation rule that exists ({by}).");
            }
        }

        Assert(patterns.Where(item => item!["disposition"]!.GetValue<string>() == "IMPLEMENTED")
                   .Count(item => item!["implementedBy"]!.GetValue<string>().StartsWith("LogObservationKind.", StringComparison.Ordinal)) == LogObservationRules.Rules.Length,
            "Every observation rule is claimed by exactly one upstream pattern and vice versa.");
        Assert(patterns.Where(item => item!["disposition"]!.GetValue<string>() is "SUPERSEDED" or "NOT_APPLICABLE" or "REFERENCE_ONLY" or "QUEUED_FIXTURE")
                   .All(item => item!["rationale"]!.GetValue<string>().Length > 10),
            "Every non-implemented pattern records why.");
    }

    private static void AssertNoteRules()
    {
        foreach (var name in new[] { "general-notes-rules.json", "weird-settings-rules.json" })
        {
            var doc = Read(name);
            var rules = doc["rules"]!.AsArray();
            Assert(doc["total"]!.GetValue<int>() == rules.Count &&
                   rules.Select(rule => rule!["id"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == rules.Count &&
                   rules.All(rule => NoteDispositions.Contains(rule!["disposition"]!.GetValue<string>(), StringComparer.Ordinal) &&
                                     rule["rationale"]!.GetValue<string>().Length > 10),
                $"{name}: every upstream diagnostic condition ends in one known disposition with a reason.");
            Assert(rules.All(rule => rule!.AsObject().All(pair => pair.Key is not ("message" or "text" or "sentence" or "advice"))),
                $"{name}: the corpus stores keys, constants, settings names and line references, never the upstream advice sentences.");
        }

        Assert(Read("general-notes-rules.json")["total"]!.GetValue<int>() == 112 && Read("weird-settings-rules.json")["total"]!.GetValue<int>() == 216,
            "112 GeneralNotes and 216 WeirdSettings emissions are inventoried.");
    }

    private static void AssertFamilies()
    {
        var doc = Read("weird-settings-families.json");
        var families = doc["families"]!.AsArray();
        Assert(doc["total"]!.GetValue<int>() == families.Count && families.Count == 33,
            "All 33 upstream game-family serial sets are preserved.");
        foreach (var family in families)
        {
            var serials = family!["serials"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
            Assert(serials.All(serial => TextSafety.ExactProductCode(serial) is not null) && serials.Distinct(StringComparer.Ordinal).Count() == serials.Length &&
                   family["serialCount"]!.GetValue<int>() == serials.Length,
                $"Family {family["name"]}: exact, unique product codes only (no fuzzy expansion, no related-edition inheritance).");
            Assert(FamilyClassifications.Contains(family["classification"]!.GetValue<string>(), StringComparer.Ordinal) &&
                   !family["officialCurrent"]!.GetValue<bool>() && !family["corroboratedNonOfficial"]!.GetValue<bool>(),
                $"Family {family["name"]}: not an active finding without predicate-level evidence.");
        }
    }

    private static void AssertSubsystems()
    {
        var subsystems = Read("subsystems.json")["subsystems"]!.AsArray();
        Assert(subsystems.All(item => SubsystemDispositions.Contains(item!["disposition"]!.GetValue<string>(), StringComparer.Ordinal) &&
                                      item["note"]!.GetValue<string>().Length > 10),
            "Every subsystem has one known disposition and a note.");
        var text = string.Join('\n', subsystems.Select(item => item!["subsystem"]!.GetValue<string>()));
        foreach (var required in new[]
                 {
                     "Log parsing", "GeneralNotes", "WeirdSettings", "Hardware", "PerfSensor", "IRD", "PSN", "Compatibility API", "GitHub", "Win32",
                     "Syscall", "OCR", "Unicode", "Archive", "Caching", "migrations", "cancellation", "shutdown", "Explain", "Localization"
                 })
        {
            Assert(text.Contains(required, StringComparison.OrdinalIgnoreCase), $"The subsystem ledger covers '{required}'.");
        }
    }

    private static void AssertSummaryMatches()
    {
        var summary = Read("summary.json");
        int Count(string file, string arrayName, string key, string value) =>
            Read(file)[arrayName]!.AsArray().Count(item => item![key]!.GetValue<string>() == value);

        foreach (var (node, disposition) in summary["regex"]!["byDisposition"]!.AsObject().Select(pair => (pair.Key, pair.Value!.GetValue<int>())).Select(pair => (pair.Key, pair.Item2)))
        {
            Assert(Count("regex-coverage.json", "patterns", "disposition", node) == disposition, $"summary regex.{node} recomputes.");
        }

        foreach (var pair in summary["generalNotes"]!["byDisposition"]!.AsObject())
        {
            Assert(Count("general-notes-rules.json", "rules", "disposition", pair.Key) == pair.Value!.GetValue<int>(), $"summary generalNotes.{pair.Key} recomputes.");
        }

        foreach (var pair in summary["weirdSettingsRules"]!["byDisposition"]!.AsObject())
        {
            Assert(Count("weird-settings-rules.json", "rules", "disposition", pair.Key) == pair.Value!.GetValue<int>(), $"summary weirdSettingsRules.{pair.Key} recomputes.");
        }

        Assert(summary["weirdSettingsFamilies"]!["activeCorroborated"]!.GetValue<int>() == 0,
            "No game-family rule is an active finding.");
    }
}
