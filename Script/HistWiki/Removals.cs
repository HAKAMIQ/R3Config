using System.Text.RegularExpressions;

namespace HistWiki;

// Deterministic classification of historical setting removals from the Wiki history alone.
// It only reads frozen HistWiki outputs and never changes them. A reason is assigned only
// when the Wiki history itself proves it; everything else stays Unknown. A Wiki date is never
// a build boundary, and ClaimedFixed (an editor's words) is never VerifiedFixedUpstream.
public sealed record RemovalInput(
    string Game, string Key, string Canonical, string Type, string Status, long Revision, string Timestamp,
    string Contributor, string Comment, string LiteralSchemaStatus, string FamilyStatus, string Detail);

public sealed record RemovalRow(
    string Game, string SourceFieldKey, string CanonicalFieldKey, long Revision, string Timestamp, string Contributor,
    string EditComment, string RemovalReason, string Basis, string Verification, int BulkGroupGames);

public static class Removals
{
    private static readonly Regex FixWords = new(@"\b(?:fixed|has been fixed|have been fixed|resolved|solved|patched)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NoLongerWords = new(@"\b(?:no longer (?:required|needed|relevant|necessary)|not (?:required|needed|necessary) anymore|outdated|obsolete)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ReplaceWords = new(@"\b(?:replaced by|replaced with|instead of|switch(?:ed)? to)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public const int BulkMinimumGames = 10;
    public const string BotName = "RPCS3 Bot";

    public static List<RemovalRow> Classify(IReadOnlyList<RemovalInput> events, IReadOnlyList<RemovalInput> schemaStatus)
    {
        var statusByEvent = schemaStatus
            .GroupBy(s => (s.Game, s.Key, s.Revision))
            .ToDictionary(g => g.Key, g => g.First());
        var removals = events.Where(e => e.Type == "VALUE_REMOVED" && e.Status is "CLEAN" or "ABSENCE").OrderBy(e => e.Game, StringComparer.Ordinal).ThenBy(e => e.Key, StringComparer.Ordinal).ThenBy(e => e.Revision).ToList();
        var additions = events.Where(e => e.Type is "VALUE_ADDED" or "VALUE_CHANGED")
            .GroupBy(e => (e.Game, e.Revision)).ToDictionary(g => g.Key, g => g.ToList());
        var bulkGroups = removals
            .GroupBy(e => (e.Canonical, Date: e.Timestamp[..10], e.Contributor))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Game).Distinct(StringComparer.Ordinal).Count());

        var rows = new List<RemovalRow>();
        foreach (var e in removals)
        {
            statusByEvent.TryGetValue((e.Game, e.Key, e.Revision), out var status);
            var bulkGames = bulkGroups[(e.Canonical, e.Timestamp[..10], e.Contributor)];
            string reason;
            string basis;
            string verification = "NONE";

            if (e.Detail == "CONFIG_TEMPLATE_REMOVED")
            {
                rows.Add(new RemovalRow(e.Game, e.Key, e.Canonical, e.Revision, e.Timestamp, e.Contributor, e.Comment, "Unknown", "CONFIG_TEMPLATE_ABSENT", "NONE", bulkGames));
                continue;
            }

            var renamedInSameRevision = additions.TryGetValue((e.Game, e.Revision), out var added) &&
                                        added.Any(a => a.Canonical == e.Canonical && a.Key != e.Key);
            if (renamedInSameRevision)
            {
                (reason, basis) = ("FieldRenamed", "SAME_REVISION_ADDS_SAME_CANONICAL_KEY");
            }
            else if (status is not null && status.LiteralSchemaStatus != "SCHEMA_ACTIVE" && status.LiteralSchemaStatus != "UNSCHEMATIZED" && status.FamilyStatus == "SCHEMA_ACTIVE")
            {
                (reason, basis) = ("FieldRenamed", "OLD_NAME_RETIRED_CANONICAL_FAMILY_STILL_ACTIVE");
            }
            else if (status is not null && status.LiteralSchemaStatus is "TEMPORARY_SCHEMA_GAP" or "AFTER_FINAL_SCHEMA_RETIREMENT" && status.FamilyStatus is "TEMPORARY_SCHEMA_GAP" or "AFTER_FINAL_SCHEMA_RETIREMENT")
            {
                (reason, basis) = ("SettingRemoved", "FIELD_ABSENT_FROM_TEMPLATE_CONFIG_SCHEMA_AT_REMOVAL");
            }
            else if (e.Contributor == BotName || bulkGames >= BulkMinimumGames)
            {
                (reason, basis) = ("BulkCleanup", e.Contributor == BotName ? "EDITED_BY_RPCS3_BOT" : $"SAME_DAY_SAME_EDITOR_SAME_KEY_IN_{bulkGames}_GAMES");
            }
            else if (FixWords.IsMatch(e.Comment))
            {
                (reason, basis, verification) = ("ClaimedFixed", "EDIT_COMMENT_CLAIMS_FIXED", "CLAIMED_ONLY");
            }
            else if (ReplaceWords.IsMatch(e.Comment))
            {
                (reason, basis) = ("Replaced", "EDIT_COMMENT_CLAIMS_REPLACED");
            }
            else if (NoLongerWords.IsMatch(e.Comment))
            {
                (reason, basis) = ("Unknown", "CLAIMED_NO_LONGER_REQUIRED_CAUSE_NOT_STATED");
            }
            else if (e.Detail == "CONFIG_TEMPLATE_REMOVED")
            {
                (reason, basis) = ("Unknown", "CONFIG_TEMPLATE_ABSENT");
            }
            else
            {
                (reason, basis) = ("Unknown", "NO_EVIDENCE_OF_CAUSE");
            }

            rows.Add(new RemovalRow(e.Game, e.Key, e.Canonical, e.Revision, e.Timestamp, e.Contributor, e.Comment, reason, basis, verification, bulkGames));
        }

        return rows;
    }

    public static int Run(string eventsCsv, string schemaStatusCsv, string outDir)
    {
        var events = ReadEvents(eventsCsv);
        var status = ReadSchemaStatus(schemaStatusCsv);
        var rows = Classify(events, status);
        Directory.CreateDirectory(outDir);
        using (var w = new Csv.Writer(Path.Combine(outDir, "Historical-Removal-Classification.csv"),
                   "Game", "SourceFieldKey", "CanonicalFieldKey", "Revision", "WikiObservedUntil", "Contributor", "EditComment", "RemovalReason", "Basis",
                   "Verification", "BulkGroupGames", "Rpcs3BuildMin", "Rpcs3BuildMax", "BuildEvidenceStatus"))
        {
            foreach (var r in rows.OrderBy(r => r.Game, StringComparer.Ordinal).ThenBy(r => r.SourceFieldKey, StringComparer.Ordinal).ThenBy(r => r.Revision))
                w.Row(r.Game, r.SourceFieldKey, r.CanonicalFieldKey, r.Revision, r.Timestamp, r.Contributor, r.EditComment, r.RemovalReason, r.Basis, r.Verification, r.BulkGroupGames, "", "", "NOT_CORRELATED");
        }

        using (var w = new Csv.Writer(Path.Combine(outDir, "Historical-Removal-Summary.csv"), "RemovalReason", "Basis", "Removals"))
        {
            foreach (var g in rows.GroupBy(r => (r.RemovalReason, r.Basis)).OrderBy(g => g.Key.RemovalReason, StringComparer.Ordinal).ThenBy(g => g.Key.Basis, StringComparer.Ordinal))
                w.Row(g.Key.RemovalReason, g.Key.Basis, g.Count());
        }

        Console.WriteLine($"removals classified: {rows.Count}");
        foreach (var g in rows.GroupBy(r => r.RemovalReason).OrderBy(g => g.Key, StringComparer.Ordinal)) Console.WriteLine($"  {g.Key}: {g.Count()}");
        return 0;
    }

    private static List<RemovalInput> ReadEvents(string path)
    {
        var rows = Csv.Read(path);
        var h = rows[0].ToList();
        int I(string n) => h.IndexOf(n);
        return rows.Skip(1).Select(r => new RemovalInput(
            r[I("Game")], r[I("SourceFieldKey")], r[I("CanonicalFieldKey")], r[I("EventType")], r[I("Status")], long.Parse(r[I("Revision")]), r[I("Timestamp")],
            r[I("Contributor")], r[I("EditComment")], "", "", r[I("Detail")])).ToList();
    }

    private static List<RemovalInput> ReadSchemaStatus(string path)
    {
        var rows = Csv.Read(path);
        var h = rows[0].ToList();
        int I(string n) => h.IndexOf(n);
        return rows.Skip(1).Select(r => new RemovalInput(
            r[I("Game")], r[I("SourceFieldKey")], r[I("CanonicalFieldKey")], r[I("EventType")], "CLEAN", long.Parse(r[I("Revision")]), r[I("Timestamp")],
            "", "", r[I("LiteralSchemaStatus")], r[I("IdentityFamilyStatus")], "")).ToList();
    }
}
