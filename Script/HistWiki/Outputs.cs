namespace HistWiki;

public static class Outputs
{
    private static string Ts(Pt? p) => p is null ? "" : p.Value.Ts;
    private static string Rv(Pt? p) => p is null ? "" : p.Value.Rev.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static void WriteAll(Result r, string dir)
    {
        Directory.CreateDirectory(dir);
        var games = r.Games;
        var eff = r.Effective;
        var schema = r.Schema;
        string P(string name) => Path.Combine(dir, name);
        var ord = StringComparer.Ordinal;

        using (var w = new Csv.Writer(P("01-Source-Provenance-Manifest.csv"),
                   "Role", "FullPath", "FileName", "Size", "SHA256", "ParseStatus", "PageCount", "RevisionCount", "FirstRevisionTimestamp", "LastRevisionTimestamp", "Error"))
        {
            foreach (var s in r.Sources.OrderBy(s => s.Role, ord).ThenBy(s => s.FileName, ord))
                w.Row(s.Role, s.FullPath, s.FileName, s.Size, s.Sha256, s.ParseStatus, s.Pages, s.Revisions, s.FirstTs, s.LastTs, s.Error);
        }

        var valueEvents = games.Events.Where(e => e.Kind == "VALUE").OrderBy(e => e.Game, ord).ThenBy(e => e.Key, ord).ThenBy(e => e.At).ToList();
        using (var w = new Csv.Writer(P("02-Historical-Config-Value-Events.csv"),
                   "Game", "SourceFieldKey", "CanonicalFieldKey", "SemanticClass", "EventType", "Status", "PreviousState", "PreviousRawValue", "PreviousCleanValue",
                   "NewState", "NewRawValue", "NewCleanValue", "Revision", "Timestamp", "Contributor", "EditComment", "SourceFile", "Detail"))
        {
            foreach (var e in valueEvents)
                w.Row(e.Game, e.Key, eff.Canonical(e.Key), schema.Semantic(e.Key), e.Type, e.Status, e.PrevState, e.PrevRaw, e.PrevValue,
                    e.NewState, e.NewRaw, e.NewValue, e.At.Rev, e.At.Ts, e.Contributor, e.Comment, e.SourceFile, e.Detail);
        }

        var noteEvents = games.Events.Where(e => e.Kind == "NOTE").OrderBy(e => e.Game, ord).ThenBy(e => e.Key, ord).ThenBy(e => e.At).ToList();
        using (var w = new Csv.Writer(P("03-Historical-Config-Notes-Events.csv"),
                   "Game", "NoteKey", "BaseSourceFieldKey", "BaseCanonicalFieldKey", "EventType", "Status", "PreviousState", "PreviousRawValue", "PreviousCleanValue",
                   "NewState", "NewRawValue", "NewCleanValue", "Revision", "Timestamp", "Contributor", "EditComment", "SourceFile", "Detail"))
        {
            foreach (var e in noteEvents)
                w.Row(e.Game, e.Key, e.Key[..^6], eff.Canonical(e.Key[..^6]), e.Type, e.Status, e.PrevState, e.PrevRaw, e.PrevValue,
                    e.NewState, e.NewRaw, e.NewValue, e.At.Rev, e.At.Ts, e.Contributor, e.Comment, e.SourceFile, e.Detail);
        }

        using (var w = new Csv.Writer(P("04-Template-Config-Schema-Events.csv"),
                   "Parameter", "ChangeType", "PreviousSpelling", "NewSpelling", "Revision", "Timestamp", "Contributor", "EditComment"))
        {
            foreach (var e in schema.Events.OrderBy(e => e.At).ThenBy(e => e.Key, ord).ThenBy(e => e.Type, ord))
                w.Row(e.Key, e.Type, e.PrevName, e.NewName, e.At.Rev, e.At.Ts, e.Contributor, e.Comment);
        }

        using (var w = new Csv.Writer(P("05-Template-Config-Schema-Intervals.csv"),
                   "Parameter", "IntervalIndex", "Semantic", "Section", "SourceSpellings", "StartRevision", "StartTime", "EndRevision", "EndTime", "EndExclusive", "Current",
                   "StartContributor", "StartComment", "EndContributor", "EndComment"))
        {
            foreach (var (key, list) in schema.Intervals.OrderBy(kv => kv.Key, ord))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    w.Row(key, i + 1, schema.Semantic(key), schema.Section.GetValueOrDefault(key, ""), s.Spellings, s.Start.Rev, s.Start.Ts,
                        Rv(s.End), Ts(s.End), true, s.End is null, s.StartContributor, s.StartComment, s.EndContributor, s.EndComment);
                }
            }
        }

        using (var w = new Csv.Writer(P("06-Setting-Identity-Map.csv"),
                   "SourceFieldKey", "CanonicalFieldKey", "IdentityStatus", "EvidenceRevision", "EvidenceTimestamp", "Games", "CleanTransitions", "SemanticClass"))
        {
            foreach (var i in eff.Identities.Values.OrderBy(i => i.Source, ord))
                w.Row(i.Source, i.Canonical, i.Status, i.EvidenceRev == 0 ? "" : i.EvidenceRev, i.EvidenceTs, i.Games, i.Transitions, schema.Semantic(i.Source));
        }

        using (var w = new Csv.Writer(P("07-Schema-Confirmed-Renames.csv"),
                   "SourceFieldKey", "CanonicalFieldKey", "EvidenceRevision", "EvidenceTimestamp", "Kind", "Verified", "Detail", "DeclaredEvidence"))
        {
            foreach (var c in eff.Checks.OrderBy(c => c.Declaration.Source, ord))
                w.Row(c.Declaration.Source, c.Declaration.Canonical, c.Declaration.EvidenceRevision, c.EvidenceTs, c.Declaration.Kind, c.Verified, c.Detail, c.Declaration.Evidence);
        }

        void PageIntervals(string file, string kind)
        {
            using var w = new Csv.Writer(P(file),
                "Game", "SourceFieldKey", "CanonicalFieldKey", "SemanticClass", "Value", "PageStart", "PageStartRevision", "PageEnd", "PageEndRevision", "StartFrom", "EndBoundary",
                "NoteState", "NoteAtStart", "AmbiguityContext", "StartContributor", "StartComment", "EndContributor", "EndComment", "SourceFile");
            foreach (var i in games.Intervals.Where(i => i.Kind == kind).OrderBy(i => i.Game, ord).ThenBy(i => i.Key, ord).ThenBy(i => i.Start))
                w.Row(i.Game, i.Key, eff.Canonical(i.Key), schema.Semantic(i.Key), i.Value, i.Start.Ts, i.Start.Rev, Ts(i.End), Rv(i.End), i.StartFrom, i.EndBoundary,
                    i.NoteState, i.NoteAtStart, Ambiguity(i), i.StartContributor, i.StartComment, i.EndContributor, i.EndComment, i.SourceFile);
        }

        PageIntervals("08-Page-Value-Intervals.csv", "VALUE");
        PageIntervals("09-Page-Note-Intervals.csv", "NOTE");

        using (var w = new Csv.Writer(P("10-Ambiguity-Barriers.csv"),
                   "Game", "FieldKey", "Kind", "BarrierClass", "Reasons", "BarrierStartRevision", "BarrierStartTimestamp", "ResumeRevision", "ResumeTimestamp",
                   "GapRevisions", "ClosedValue", "ResumedValue", "ObservedValuesInGap"))
        {
            foreach (var q in games.Barriers.OrderBy(q => q.Game, ord).ThenBy(q => q.Key, ord).ThenBy(q => q.From))
                w.Row(q.Game, q.Key, q.Kind, q.BarrierClass, q.Reasons, q.From.Rev, q.From.Ts, Rv(q.Resume), Ts(q.Resume), q.GapRevisions, q.ClosedValue, q.ResumedValue, q.ObservedValues);
        }

        void Effective(string file, Func<EffectiveInterval, bool> pick)
        {
            using var w = new Csv.Writer(P(file),
                "Game", "SourceFieldKey", "CanonicalFieldKey", "IdentityStatus", "SemanticClass", "Value",
                "PageStart", "PageStartRevision", "PageEnd", "PageEndRevision",
                "SchemaStart", "SchemaStartRevision", "SchemaEnd", "SchemaEndRevision",
                "EffectiveStart", "EffectiveStartRevision", "EffectiveEnd", "EffectiveEndRevision", "StartBoundary", "EndBoundary", "SchemaContinuity", "AmbiguityContext",
                "NoteState", "NoteAtStart", "ConflictIds",
                "WikiObservedFrom", "WikiObservedUntil", "Rpcs3BuildMin", "Rpcs3BuildMax", "Rpcs3CommitIntroduced", "Rpcs3CommitFixed", "BuildEvidenceStatus", "RemovalReason",
                "Contributor", "Comment", "SourceFile");
            foreach (var e in eff.Effective.Where(pick).OrderBy(e => e.Page.Game, ord).ThenBy(e => e.SourceKey, ord).ThenBy(e => e.EffStart))
            {
                var removed = e.Page.End is not null && (e.EndBoundary is "VALUE_REMOVED" or "VALUE_CHANGED");
                w.Row(e.Page.Game, e.SourceKey, e.CanonicalKey, e.IdentityStatus, e.Semantic, e.Page.Value,
                    e.Page.Start.Ts, e.Page.Start.Rev, Ts(e.Page.End), Rv(e.Page.End),
                    e.Schema.Start.Ts, e.Schema.Start.Rev, Ts(e.Schema.End), Rv(e.Schema.End),
                    e.EffStart.Ts, e.EffStart.Rev, Ts(e.EffEnd), Rv(e.EffEnd), e.StartBoundary, e.EndBoundary, e.Continuity, Ambiguity(e.Page),
                    e.Page.NoteState, e.Page.NoteAtStart, e.ConflictIds,
                    e.Page.Start.Ts, Ts(e.Page.End), "", "", "", "", "NOT_CORRELATED", removed ? "Unknown" : "",
                    e.Page.StartContributor, e.Page.StartComment, e.Page.SourceFile);
            }
        }

        Effective("11-Effective-Historical-Setting-Intervals.csv", e => e.Semantic == "SETTING" && e.Page.Kind == "VALUE");
        Effective("12-Effective-Historical-Metadata-Intervals.csv", e => e.Semantic == "PATCH_METADATA" && e.Page.Kind == "VALUE");

        using (var w = new Csv.Writer(P("13-Non-Effective-Page-Intervals.csv"),
                   "Game", "SourceFieldKey", "CanonicalFieldKey", "IdentityStatus", "SemanticClass", "Kind", "Value", "PageStart", "PageStartRevision", "PageEnd", "PageEndRevision", "Reason"))
        {
            foreach (var n in eff.NonEffectiveList.OrderBy(n => n.Page.Game, ord).ThenBy(n => n.SourceKey, ord).ThenBy(n => n.Page.Start))
                w.Row(n.Page.Game, n.SourceKey, n.CanonicalKey, n.IdentityStatus, n.Semantic, n.Page.Kind, n.Page.Value, n.Page.Start.Ts, n.Page.Start.Rev, Ts(n.Page.End), Rv(n.Page.End), n.Reason);
        }

        using (var w = new Csv.Writer(P("14-Effective-Historical-Notes-Intervals.csv"),
                   "Game", "NoteKey", "BaseSourceFieldKey", "BaseCanonicalFieldKey", "IdentityStatus", "Note", "EffectiveStart", "EffectiveStartRevision", "EffectiveEnd", "EffectiveEndRevision",
                   "StartBoundary", "EndBoundary", "SchemaContinuity", "SourceFile"))
        {
            foreach (var e in eff.Effective.Where(e => e.Page.Kind == "NOTE").OrderBy(e => e.Page.Game, ord).ThenBy(e => e.SourceKey, ord).ThenBy(e => e.EffStart))
                w.Row(e.Page.Game, e.SourceKey, e.SourceKey[..^6], e.CanonicalKey[..^6], e.IdentityStatus, e.Page.Value,
                    e.EffStart.Ts, e.EffStart.Rev, Ts(e.EffEnd), Rv(e.EffEnd), e.StartBoundary, e.EndBoundary, e.Continuity, e.Page.SourceFile);
        }

        using (var w = new Csv.Writer(P("15-Conflicts.csv"),
                   "ConflictId", "Game", "CanonicalFieldKey", "Conflict", "ExistingValue", "NewValue", "ExistingStart", "ExistingEnd", "NewStart", "NewEnd", "ExistingSources", "NewSource"))
        {
            foreach (var c in eff.Conflicts)
                w.Row(c.Id, c.Game, c.Canonical, "OVERLAPPING_DIFFERENT_VALUES", c.ExistingValue, c.NewValue, c.ExistingStart.Ts, Ts(c.ExistingEnd), c.NewStart.Ts, Ts(c.NewEnd), c.ExistingSource, c.NewSource);
        }

        var warnings = games.Warnings.Concat(eff.Warnings)
            .OrderBy(x => x.Code, ord).ThenBy(x => x.Game, ord).ThenBy(x => x.Key, ord).ThenBy(x => x.Ts, ord).ThenBy(x => x.Rev).ThenBy(x => x.Detail, ord).ToList();
        using (var w = new Csv.Writer(P("16-Warnings.csv"), "Severity", "Code", "Game", "Key", "Revision", "Timestamp", "Detail"))
        {
            foreach (var x in warnings) w.Row(x.Severity, x.Code, x.Game, x.Key, x.Rev, x.Ts, x.Detail);
        }

        using (var w = new Csv.Writer(P("17-Schema-Event-Status.csv"),
                   "Game", "SourceFieldKey", "CanonicalFieldKey", "EventType", "Revision", "Timestamp", "LiteralSchemaStatus", "IdentityFamilyStatus"))
        {
            foreach (var e in valueEvents.Where(e => e.Status == "CLEAN"))
                w.Row(e.Game, e.Key, eff.Canonical(e.Key), e.Type, e.At.Rev, e.At.Ts,
                    EffectiveBuilder.SchemaStatus(schema.IntervalsOf(e.Key), e.At), FamilyStatus(r, e));
        }

        using (var w = new Csv.Writer(P("18-Historical-GameId-Events.csv"),
                   "Game", "Change", "PreviousGameIdTemplate", "CurrentGameIdTemplate", "Ids", "Oids", "Revision", "Timestamp", "Contributor", "EditComment", "SourceFile"))
        {
            foreach (var g in games.GameIds.OrderBy(g => g.Game, ord).ThenBy(g => g.At))
                w.Row(g.Game, g.Change, g.Previous, g.Current, g.Ids, g.Oids, g.At.Rev, g.At.Ts, g.Contributor, g.Comment, g.SourceFile);
        }

        using (var w = new Csv.Writer(P("26-After-Final-Retirement-Fields.csv"), "FieldKey", "Warnings", "Games", "FirstAfterRetirement", "LastAfterRetirement", "EffectiveLeaks"))
        {
            foreach (var row in Accounting.AfterFinal(r)) w.Row(row.Field, row.Warnings, row.Games, row.First, row.Last, row.EffectiveLeaks);
        }

        using (var w = new Csv.Writer(P("27-Transition-Accounting.csv"), "Section", "Line", "Value"))
        {
            foreach (var (section, line, value) in Accounting.Transitions(r)) w.Row(section, line, value);
        }

        using (var w = new Csv.Writer(P("24-Warning-Summary.csv"), "WarningType", "Severity", "Count", "AffectedGames", "AffectedFields"))
        {
            foreach (var g in warnings.GroupBy(x => (x.Code, x.Severity)).OrderBy(x => x.Key.Severity, ord).ThenBy(x => x.Key.Code, ord))
                w.Row(g.Key.Code, g.Key.Severity, g.Count(), g.Select(x => x.Game).Where(x => x.Length > 0).Distinct(ord).Count(), g.Select(x => x.Key).Where(x => x.Length > 0).Distinct(ord).Count());
        }

        using (var w = new Csv.Writer(P("25-Semantic-Classification.csv"), "SemanticClass", "FieldKey", "Games", "PageIntervals", "EffectiveIntervals", "IdentityStatus"))
        {
            var keys = eff.Identities.Keys.ToList();
            foreach (var key in keys.OrderBy(k => schema.Semantic(k), ord).ThenBy(k => k, ord))
            {
                var pageIv = games.Intervals.Where(i => i.Kind == "VALUE" && i.Key == key).ToList();
                var effIv = eff.Effective.Where(e => e.Page.Kind == "VALUE" && e.SourceKey == key).ToList();
                var gamesOf = pageIv.Select(i => i.Game).Concat(games.Barriers.Where(b => b.Kind == "VALUE" && b.Key == key).Select(b => b.Game)).Distinct(ord).Count();
                w.Row(schema.Semantic(key), key, gamesOf, pageIv.Count, effIv.Count, eff.Identities[key].Status);
            }
        }

        using (var w = new Csv.Writer(P("22-Reconciliation-Diff.csv"), "Kind", "Side", "Class", "Game", "FieldKey", "Revision", "Type", "Detail"))
        {
            foreach (var x in r.Recon.Rows) w.Row(x.Kind, x.Side, x.Class, x.Game, x.Key, x.Revision, x.Type, x.Detail);
        }

        using (var w = new Csv.Writer(P("19-Template-Config-Schema-Inventory.csv"),
                   "Parameter", "Semantic", "Section", "Current", "IntervalCount", "FirstRevision", "FirstTime", "LatestSpelling"))
        {
            foreach (var (key, list) in schema.Intervals.OrderBy(kv => kv.Key, ord))
                w.Row(key, schema.Semantic(key), schema.Section.GetValueOrDefault(key, ""), schema.CurrentKeys.Contains(key), list.Count, list[0].Start.Rev, list[0].Start.Ts, schema.LatestSpelling.GetValueOrDefault(key, ""));
        }
    }

    public static string Ambiguity(PageInterval i)
    {
        var parts = new List<string>();
        if (i.StartAfterGap) parts.Add("AFTER_AMBIGUOUS_GAP");
        if (i.EndBoundary.StartsWith("AMBIGUOUS", StringComparison.Ordinal)) parts.Add("BEFORE_AMBIGUOUS_GAP");
        return parts.Count == 0 ? "NONE" : string.Join("|", parts);
    }

    public static string FamilyStatus(Result r, KeyEvent e)
    {
        var baseKey = r.Effective.Canonical(e.Key);
        return r.Effective.FamilyStatus(baseKey, e.At);
    }
}
