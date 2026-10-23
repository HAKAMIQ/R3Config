namespace HistWiki;

public sealed class ReconRow
{
    public string Kind = "", Side = "", Class = "", Game = "", Key = "", Type = "", Detail = "";
    public long Revision;
}

public sealed class ReconSummary
{
    public bool Available;
    public readonly List<ReconRow> Rows = [];
    public int PriorTrusted, PriorQuarantined, MineClean, Matching, TypeMismatch;
    public readonly Dictionary<string, int> MatchingByKind = new(), TypeMismatchByKind = new(), TrustedByKind = new(), MineCleanByKind = new();
    public int ValueTrusted, ValueQuarantined, NoteTrusted, NoteQuarantined;
    public readonly List<(string Game, string Key, long Rev, string Ts)> PriorTrustedValueEvents = [];
    public int Unexplained => Rows.Count(r => r.Class == "UNEXPLAINED");
}

// Explains every difference between this analyzer and the earlier CSV-based
// analysis. A difference is only acceptable when one of the known, verifiable
// causes applies; anything else stays UNEXPLAINED and fails the gate.
public static class Reconcile
{
    public static ReconSummary Run(Result r)
    {
        var summary = new ReconSummary();
        if (r.Options.ReferenceDir is null) return summary;
        var dir = Path.Combine(r.Options.ReferenceDir, "Analysis-20260920");
        if (!Directory.Exists(dir)) return summary;
        summary.Available = true;

        foreach (var (kind, label) in new[] { ("VALUE", "Value"), ("NOTE", "Note") })
        {
            var trustedRows = Csv.Read(Path.Combine(dir, $"Historical-Config-{label}-Transitions-Trusted.csv"));
            var th = trustedRows[0].ToList();
            int TC(string n) => th.IndexOf(n);
            var trusted = trustedRows.Skip(1)
                .ToDictionary(x => (x[TC("Game")], x[TC("FieldKey")], long.Parse(x[TC("Revision")])),
                    x => (Type: x[TC("ChangeType")], Ts: x[TC("Timestamp")], PrevRev: long.TryParse(x[TC("PreviousRevision")], out var p) ? p : 0));
            var quarRows = Csv.Read(Path.Combine(dir, $"Historical-Config-{label}-Transitions-Quarantine.csv"));
            var qh = quarRows[0].ToList();
            int QC(string n) => qh.IndexOf(n);
            var quarantined = quarRows.Skip(1).Select(x => (x[QC("Game")], x[QC("FieldKey")], long.Parse(x[QC("Revision")]))).ToHashSet();
            var mine = r.Games.Events.Where(e => e.Kind == kind && e.Status == "CLEAN").ToDictionary(e => (e.Game, e.Key, e.At.Rev), e => e.Type);
            var mineAbsence = r.Games.Events.Where(e => e.Kind == kind && e.Status == "ABSENCE").Select(e => (e.Game, e.Key, e.At.Rev)).ToHashSet();
            summary.TrustedByKind[kind] = trusted.Count;
            summary.MineCleanByKind[kind] = mine.Count;
            var gaps = r.Games.Barriers.Where(q => q.Kind == kind).GroupBy(q => (q.Game, q.Key))
                .ToDictionary(x => x.Key, x => x.Select(b => (Revs: b.GapRevisions.Split(';').Select(long.Parse).ToHashSet(), Resume: b.Resume?.Rev ?? -1L, b.BarrierClass)).ToList());
            if (kind == "VALUE")
            {
                summary.ValueTrusted = trusted.Count;
                summary.ValueQuarantined = quarantined.Count;
                summary.PriorTrustedValueEvents.AddRange(trusted.Select(t => (t.Key.Item1, t.Key.Item2, t.Key.Item3, t.Value.Ts)));
            }
            else
            {
                summary.NoteTrusted = trusted.Count;
                summary.NoteQuarantined = quarantined.Count;
            }

            summary.PriorTrusted += trusted.Count;
            summary.PriorQuarantined += quarantined.Count;
            summary.MineClean += mine.Count;

            string Explain(string game, string key, long rev, long prevRev, out string detail)
            {
                if (mineAbsence.Contains((game, key, rev)))
                {
                    detail = "Recorded as a deterministic absence event (Config template removed or restored).";
                    return "MINE_DETERMINISTIC_ABSENCE_EVENT";
                }

                if (gaps.TryGetValue((game, key), out var list) &&
                    list.FirstOrDefault(g => g.Revs.Contains(rev) || g.Resume == rev || (prevRev != 0 && g.Revs.Contains(prevRev))) is { Revs: not null } hit)
                {
                    detail = hit.BarrierClass;
                    return "MINE_AMBIGUITY_BARRIER";
                }

                if (r.Games.InlineKeys.Contains((game, rev, key)) || (prevRev != 0 && r.Games.InlineKeys.Contains((game, prevRev, key))))
                {
                    detail = "Parameter written on the same line as a previous parameter (MediaWiki splits it; a line-based reader does not).";
                    return "LEGACY_LINE_PARSER_INLINE_PARAM";
                }

                if (r.Games.CommentSpanKeys.Contains((game, rev, key)) || (prevRev != 0 && r.Games.CommentSpanKeys.Contains((game, prevRev, key))))
                {
                    detail = "Value holds a comment closed on a later line (typo such as '->>'): MediaWiki hides text up to the next '-->'; a line-based reader shows the text.";
                    return "LEGACY_LINE_PARSER_MULTILINE_COMMENT";
                }

                detail = "";
                return "UNEXPLAINED";
            }

            foreach (var (k, v) in trusted.OrderBy(t => t.Key.Item1, StringComparer.Ordinal).ThenBy(t => t.Key.Item2, StringComparer.Ordinal).ThenBy(t => t.Key.Item3))
            {
                if (mine.TryGetValue(k, out var type))
                {
                    if (type == v.Type) { summary.Matching++; summary.MatchingByKind[kind] = summary.MatchingByKind.GetValueOrDefault(kind) + 1; continue; }
                    summary.TypeMismatch++;
                    summary.TypeMismatchByKind[kind] = summary.TypeMismatchByKind.GetValueOrDefault(kind) + 1;
                    var mismatchClass = Explain(k.Item1, k.Item2, k.Item3, v.PrevRev, out var mismatchDetail);
                    summary.Rows.Add(new ReconRow { Kind = kind, Side = "TYPE_MISMATCH", Class = mismatchClass, Game = k.Item1, Key = k.Item2, Revision = k.Item3, Type = $"{v.Type} vs {type}", Detail = mismatchDetail });
                    continue;
                }

                var cls = Explain(k.Item1, k.Item2, k.Item3, v.PrevRev, out var detail);
                summary.Rows.Add(new ReconRow { Kind = kind, Side = "PRIOR_TRUSTED_ONLY", Class = cls, Game = k.Item1, Key = k.Item2, Revision = k.Item3, Type = v.Type, Detail = detail });
            }

            foreach (var (k, type) in mine.OrderBy(t => t.Key.Item1, StringComparer.Ordinal).ThenBy(t => t.Key.Item2, StringComparer.Ordinal).ThenBy(t => t.Key.Item3))
            {
                if (trusted.ContainsKey(k)) continue;
                var cls = quarantined.Contains(k) ? "PRIOR_QUARANTINED_TRANSITION" : "UNEXPLAINED";
                summary.Rows.Add(new ReconRow { Kind = kind, Side = "MINE_CLEAN_ONLY", Class = cls, Game = k.Item1, Key = k.Item2, Revision = k.Item3, Type = type });
            }

            foreach (var k in quarantined.OrderBy(t => t.Item1, StringComparer.Ordinal).ThenBy(t => t.Item2, StringComparer.Ordinal).ThenBy(t => t.Item3))
            {
                if (mine.ContainsKey(k))
                {
                    summary.Rows.Add(new ReconRow { Kind = kind, Side = "PRIOR_QUARANTINED_ONLY", Class = "PRIOR_QUARANTINED_TRANSITION", Game = k.Item1, Key = k.Item2, Revision = k.Item3, Type = mine[k] });
                    continue;
                }

                var qcls = Explain(k.Item1, k.Item2, k.Item3, 0, out var qdetail);
                summary.Rows.Add(new ReconRow { Kind = kind, Side = "PRIOR_QUARANTINED_ONLY", Class = qcls, Game = k.Item1, Key = k.Item2, Revision = k.Item3, Type = "", Detail = qdetail });
            }
        }

        return summary;
    }
}
