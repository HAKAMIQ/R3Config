using System.Globalization;

namespace HistWiki;

public sealed record AfterFinalRow(string Field, int Warnings, int Games, string First, string Last, int EffectiveLeaks);

// Auditable derived tables shared by the CSV writers and the gate report.
public static class Accounting
{
    private static string N(long v) => v.ToString(CultureInfo.InvariantCulture);

    // A page value is never evidence after the final schema retirement of its
    // SourceFieldKey: no effective piece may reach or pass that instant.
    public static int TotalLeaks(Result r) =>
        r.Effective.Effective.Count(e => IsLeak(r, e));

    public static int SettingLeaks(Result r) =>
        r.Effective.Effective.Count(e => e.Page.Kind == "VALUE" && IsLeak(r, e));

    private static bool IsLeak(Result r, EffectiveInterval e)
    {
        var intervals = r.Schema.IntervalsOf(e.SourceKey);
        if (intervals.Count == 0 || intervals[^1].End is null) return false;
        var final = intervals[^1].End!.Value;
        return e.EffEnd is null || e.EffEnd.Value > final || e.EffStart >= final;
    }

    public static List<AfterFinalRow> AfterFinal(Result r)
    {
        var rows = new List<AfterFinalRow>();
        var schema = r.Schema;
        var warnings = r.Effective.Warnings.Where(w => w.Code == "AFTER_FINAL_SCHEMA_RETIREMENT").ToList();
        foreach (var key in warnings.Select(w => w.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var iv = schema.IntervalsOf(key);
            var t = iv.Count > 0 ? iv[^1].End : null;
            if (t is null) continue;
            var after = r.Games.Intervals.Where(i => i.Key == key && (i.End is null || i.End.Value > t.Value)).ToList();
            var first = after.Count == 0 ? "" : after.Select(i => (i.Start >= t.Value ? i.Start : t.Value).Ts).Min(StringComparer.Ordinal);
            var last = after.Count == 0 ? "" : after.Any(i => i.End is null) ? "OPEN_AT_CAPTURE" : after.Select(i => i.End!.Value.Ts).Max(StringComparer.Ordinal);
            var leaks = r.Effective.Effective.Count(e => e.SourceKey == key && IsLeak(r, e));
            rows.Add(new AfterFinalRow(key, warnings.Count(w => w.Key == key), after.Select(i => i.Game).Distinct(StringComparer.Ordinal).Count(), first, last, leaks));
        }

        return rows;
    }

    public static List<(string Section, string Line, string Value)> Transitions(Result r)
    {
        var acct = r.Games.Acct;
        var rec = r.Recon;
        var lines = new List<(string, string, string)>();
        foreach (var (p, label, kind) in new[] { ("V", "SETTINGS", "VALUE"), ("N", "NOTES", "NOTE") })
        {
            int A(string k) => acct.GetValueOrDefault(p + "." + k);
            var clean = r.Games.Events.Count(e => e.Kind == kind && e.Status == "CLEAN");
            var absence = r.Games.Events.Count(e => e.Kind == kind && e.Status == "ABSENCE");
            var sum = A("KEYLOCAL") + A("STRUCTURAL") + A("ABSENCE") + A("CLEAN");
            lines.Add((label, "RAW_VALUE_EVENTS (naive reading of the same parse)", N(A("RAW"))));
            lines.Add((label, "KEY_LOCAL_AMBIGUOUS_EVENTS", N(A("KEYLOCAL"))));
            lines.Add((label, "STRUCTURAL_AMBIGUOUS_EVENTS", N(A("STRUCTURAL"))));
            lines.Add((label, "DETERMINISTIC_ABSENCE_EVENTS", N(A("ABSENCE"))));
            lines.Add((label, "CLEAN_DERIVED_TRANSITIONS", N(A("CLEAN"))));
            lines.Add((label, "identity RAW = KEY_LOCAL + STRUCTURAL + ABSENCE + CLEAN", $"{A("RAW")} = {sum} -> {(A("RAW") == sum ? "holds" : "BROKEN")}"));
            lines.Add((label, "identity accounted events = emitted events (clean / absence)", $"{A("CLEAN")}/{A("ABSENCE")} = {clean}/{absence} -> {(A("CLEAN") == clean && A("ABSENCE") == absence ? "holds" : "BROKEN")}"));
            lines.Add((label, "RECOVERY_EVENTS (values re-established after a barrier; not part of RAW)", N(A("RECOVERY"))));

            if (!rec.Available) continue;
            var rows = rec.Rows.Where(x => x.Kind == kind).ToList();
            var priorOnly = rows.Where(x => x.Side == "PRIOR_TRUSTED_ONLY").ToList();
            var mineOnly = rows.Where(x => x.Side == "MINE_CLEAN_ONLY").ToList();
            var matching = rec.MatchingByKind.GetValueOrDefault(kind);
            var mismatch = rec.TypeMismatchByKind.GetValueOrDefault(kind);
            var trusted = rec.TrustedByKind.GetValueOrDefault(kind);
            lines.Add((label, "earlier analysis: parser-clean transitions", N(trusted)));
            lines.Add((label, "  identical in this run (same game, key, revision, type)", N(matching)));
            lines.Add((label, "  same place, different type", N(mismatch)));
            foreach (var g in priorOnly.GroupBy(x => x.Class, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal))
                lines.Add((label, $"  earlier only: {g.Key}", N(g.Count())));
            foreach (var g in mineOnly.GroupBy(x => x.Class, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal))
                lines.Add((label, $"  this run only: {g.Key}", N(g.Count())));
            lines.Add((label, "identity earlier = identical + type-differs + earlier-only", $"{trusted} = {matching + mismatch + priorOnly.Count} -> {(trusted == matching + mismatch + priorOnly.Count ? "holds" : "BROKEN")}"));
            lines.Add((label, "identity this run = identical + type-differs + this-run-only", $"{clean} = {matching + mismatch + mineOnly.Count} -> {(clean == matching + mismatch + mineOnly.Count ? "holds" : "BROKEN")}"));
            lines.Add((label, $"difference this run - earlier", $"{clean - trusted:+#;-#;0} = this-run-only {mineOnly.Count} - earlier-only {priorOnly.Count}"));
        }

        return lines;
    }
}
