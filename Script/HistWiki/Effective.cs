namespace HistWiki;

public sealed record Declaration(string Source, string Canonical, long EvidenceRevision, string Kind, string Evidence);

public sealed class DeclarationCheck
{
    public Declaration Declaration = null!;
    public bool Verified;
    public string Detail = "";
    public string EvidenceTs = "";
}

// Identity map (evidence-based only) and the schema-intersected effective
// intervals. A canonical identity unifies history; it never extends the schema
// validity of an old SourceFieldKey.
public sealed class EffectiveBuilder
{
    private readonly SchemaModel _schema;
    public readonly List<DeclarationCheck> Checks = [];
    public readonly Dictionary<string, Identity> Identities = new(StringComparer.Ordinal);
    public readonly List<EffectiveInterval> Effective = [];
    public readonly List<NonEffective> NonEffectiveList = [];
    public readonly List<Conflict> Conflicts = [];
    public readonly List<Warning> Warnings = [];

    public EffectiveBuilder(SchemaModel schema) => _schema = schema;

    public static List<Declaration> LoadDeclarations(string path)
    {
        var rows = Csv.Read(path);
        return rows.Skip(1)
            .Where(r => r.Length >= 5 && r[0].Length > 0)
            .Select(r => new Declaration(r[0], r[1], long.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture), r[3], r[4]))
            .OrderBy(d => d.Source, StringComparer.Ordinal)
            .ToList();
    }

    // Each declared rename must be provable from Template:Config's own history.
    public void VerifyDeclarations(IEnumerable<Declaration> declarations)
    {
        foreach (var d in declarations)
        {
            var check = new DeclarationCheck { Declaration = d };
            Checks.Add(check);
            var atRev = _schema.Events.Where(e => e.At.Rev == d.EvidenceRevision).ToList();
            var removed = atRev.Where(e => e.Type == "PARAMETER_REMOVED" && !e.Key.EndsWith(" notes", StringComparison.Ordinal)).Select(e => e.Key).ToList();
            var added = atRev.Where(e => e.Type == "PARAMETER_ADDED" && !e.Key.EndsWith(" notes", StringComparison.Ordinal)).Select(e => e.Key).ToList();
            check.EvidenceTs = atRev.FirstOrDefault()?.At.Ts ?? "";
            if (!removed.Contains(d.Source) || !added.Contains(d.Canonical))
            {
                check.Detail = $"Revision {d.EvidenceRevision} does not remove '{d.Source}' and add '{d.Canonical}'.";
                continue;
            }

            var noteRemoved = atRev.Any(e => e.Type == "PARAMETER_REMOVED" && e.Key == d.Source + " notes");
            var noteAdded = atRev.Any(e => e.Type == "PARAMETER_ADDED" && e.Key == d.Canonical + " notes");
            if (noteRemoved != noteAdded)
            {
                check.Detail = "The notes companion parameter did not move together with the setting parameter.";
                continue;
            }

            if (d.Kind == "SAME_REVISION_SWAP")
            {
                if (removed.Count != 1 || added.Count != 1)
                {
                    check.Detail = $"Revision {d.EvidenceRevision} swaps {removed.Count} removed / {added.Count} added parameters; the pair is not unique.";
                    continue;
                }
            }
            else if (d.Kind == "SAME_REVISION_SWAP_REVERT")
            {
                // The canonical (older) name existed first and was replaced by the
                // temporary name; the swap at EvidenceRevision switches back.
                var temp = _schema.IntervalsOf(d.Source).Where(i => i.End is not null && i.End.Value.Rev == d.EvidenceRevision).ToList();
                var earlier = temp.Count != 1
                    ? []
                    : _schema.IntervalsOf(d.Canonical)
                        .Where(i => i.End is not null && i.Start < temp[0].Start && i.End.Value >= temp[0].Start && i.End.Value <= temp[0].End!.Value)
                        .ToList();
                if (earlier.Count == 0)
                {
                    check.Detail = $"'{d.Canonical}' has no earlier interval that was replaced by '{d.Source}' before the revert.";
                    continue;
                }
            }
            else
            {
                check.Detail = $"Unknown declaration kind '{d.Kind}'.";
                continue;
            }

            check.Verified = true;
            check.Detail = $"Verified at revision {d.EvidenceRevision} ({check.EvidenceTs}).";
        }
    }

    public string Canonical(string key)
    {
        var baseKey = key.EndsWith(" notes", StringComparison.Ordinal) ? key[..^6] : key;
        var suffix = key.EndsWith(" notes", StringComparison.Ordinal) ? " notes" : "";
        var declared = Checks.FirstOrDefault(c => c.Verified && c.Declaration.Source == baseKey);
        return (declared?.Declaration.Canonical ?? baseKey) + suffix;
    }

    public void BuildIdentities(IEnumerable<KeyEvent> valueEvents)
    {
        var observed = valueEvents.Where(e => e.Kind == "VALUE")
            .Select(e => (e.Key, e.Game, Clean: e.Status == "CLEAN" ? 1 : 0))
            .GroupBy(x => x.Key, StringComparer.Ordinal);
        foreach (var group in observed)
        {
            var key = group.Key;
            var identity = new Identity
            {
                Source = key,
                Games = group.Select(x => x.Game).Distinct(StringComparer.Ordinal).Count(),
                Transitions = group.Sum(x => x.Clean),
                Canonical = key
            };
            identity.Canonical = key;
            var check = Checks.FirstOrDefault(c => c.Verified && c.Declaration.Source == key);
            if (check is not null)
            {
                identity.Status = "CONFIRMED_RENAME";
                identity.Canonical = check.Declaration.Canonical;
                identity.EvidenceRev = check.Declaration.EvidenceRevision;
                identity.EvidenceTs = check.EvidenceTs;
            }
            else if (_schema.CurrentKeys.Contains(key))
            {
                identity.Status = "CANONICAL_CURRENT";
            }
            else if (_schema.IsScheme(key))
            {
                identity.Status = "HISTORICAL_UNRESOLVED";
            }
            else
            {
                identity.Status = "UNSCHEMATIZED";
            }

            Identities[key] = identity;
        }
    }

    public string IdentityStatus(string key)
    {
        var baseKey = key.EndsWith(" notes", StringComparison.Ordinal) ? key[..^6] : key;
        if (Identities.TryGetValue(baseKey, out var identity)) return identity.Status;
        var check = Checks.FirstOrDefault(c => c.Verified && c.Declaration.Source == baseKey);
        if (check is not null) return "CONFIRMED_RENAME";
        if (_schema.CurrentKeys.Contains(key)) return "CANONICAL_CURRENT";
        return _schema.IsScheme(key) ? "HISTORICAL_UNRESOLVED" : "UNSCHEMATIZED";
    }

    // Schema-validity status of one instant for one SourceFieldKey.
    public static string SchemaStatus(IReadOnlyList<SchemaInterval> intervals, Pt at)
    {
        if (intervals.Count == 0) return "UNSCHEMATIZED";
        if (intervals.Any(i => i.Start <= at && (i.End is null || at < i.End.Value))) return "SCHEMA_ACTIVE";
        if (at < intervals[0].Start) return "BEFORE_FIRST_SCHEMA_ACTIVATION";
        var last = intervals[^1];
        if (last.End is not null && at >= last.End.Value) return "AFTER_FINAL_SCHEMA_RETIREMENT";
        return "TEMPORARY_SCHEMA_GAP";
    }

    // Same classification for a canonical family (union of its members' intervals).
    public string FamilyStatus(string canonicalBase, Pt at)
    {
        var members = Checks.Where(c => c.Verified && c.Declaration.Canonical == canonicalBase).Select(c => c.Declaration.Source)
            .Append(canonicalBase).Distinct(StringComparer.Ordinal).ToList();
        var all = members.SelectMany(m => _schema.IntervalsOf(m)).OrderBy(i => i.Start).ToList();
        var merged = new List<SchemaInterval>();
        foreach (var i in all)
        {
            var open = merged.Count > 0 ? merged[^1] : null;
            if (open is not null && (open.End is null || i.Start <= open.End.Value))
            {
                if (open.End is not null && (i.End is null || i.End.Value > open.End.Value)) open.End = i.End;
                continue;
            }

            merged.Add(new SchemaInterval { Key = canonicalBase, Start = i.Start, End = i.End });
        }

        return SchemaStatus(merged, at);
    }

    public void BuildEffective(IEnumerable<PageInterval> intervals)
    {
        foreach (var page in intervals.OrderBy(i => i.Game, StringComparer.Ordinal).ThenBy(i => i.Key, StringComparer.Ordinal).ThenBy(i => i.Start))
        {
            var key = page.Key;
            var semantic = _schema.Semantic(key);
            var identityStatus = IdentityStatus(key);
            var canonical = Canonical(key);
            var schemaIntervals = _schema.IntervalsOf(key);
            NonEffective Reject(string reason) => new()
            {
                Page = page, SourceKey = key, CanonicalKey = canonical, IdentityStatus = identityStatus, Semantic = semantic, Reason = reason
            };

            if (schemaIntervals.Count == 0)
            {
                NonEffectiveList.Add(Reject("UNSCHEMATIZED"));
                Warn("INFO", "UNSCHEMATIZED", page, "Source key never appeared in Template:Config.");
                continue;
            }

            if (semantic == "UNKNOWN")
            {
                NonEffectiveList.Add(Reject("SEMANTIC_UNKNOWN"));
                Warn("INFO", "SEMANTIC_UNKNOWN", page, "Template:Config does not place this parameter in a configuration or patch section.");
                continue;
            }

            var pieces = new List<EffectiveInterval>();
            for (var s = 0; s < schemaIntervals.Count; s++)
            {
                var schema = schemaIntervals[s];
                var start = page.Start >= schema.Start ? page.Start : schema.Start;
                Pt? end;
                if (page.End is null) end = schema.End;
                else if (schema.End is null) end = page.End;
                else end = page.End.Value <= schema.End.Value ? page.End : schema.End;
                if (end is not null && start >= end.Value) continue;

                var piece = new EffectiveInterval
                {
                    Page = page, SourceKey = key, CanonicalKey = canonical, IdentityStatus = identityStatus, Semantic = semantic,
                    Schema = schema, EffStart = start, EffEnd = end,
                    StartBoundary = page.Start >= schema.Start ? "PAGE_VALUE_START" : "SCHEMA_ACTIVATION"
                };
                if (end is null) piece.EndBoundary = "OPEN_AT_CAPTURE";
                else if (page.End is not null && end.Value == page.End.Value) piece.EndBoundary = page.EndBoundary;
                else piece.EndBoundary = "SCHEMA_DEACTIVATION";

                var flags = new List<string>();
                if (piece.StartBoundary == "SCHEMA_ACTIVATION")
                {
                    flags.Add(s == 0 ? "STARTED_BEFORE_FIRST_SCHEMA_ACTIVATION" : "RESUMED_AFTER_TEMPORARY_SCHEMA_GAP");
                }

                if (piece.EndBoundary == "SCHEMA_DEACTIVATION")
                {
                    flags.Add(s < schemaIntervals.Count - 1 ? "CUT_BY_TEMPORARY_SCHEMA_GAP" : "CUT_BY_FINAL_SCHEMA_RETIREMENT");
                }

                piece.Continuity = flags.Count == 0 ? "NONE" : string.Join("|", flags);
                foreach (var flag in flags)
                {
                    var code = flag switch
                    {
                        "STARTED_BEFORE_FIRST_SCHEMA_ACTIVATION" => "BEFORE_FIRST_SCHEMA_ACTIVATION",
                        "RESUMED_AFTER_TEMPORARY_SCHEMA_GAP" or "CUT_BY_TEMPORARY_SCHEMA_GAP" => "TEMPORARY_SCHEMA_GAP",
                        _ => "AFTER_FINAL_SCHEMA_RETIREMENT"
                    };
                    Warn("INFO", code, page, flag);
                }

                pieces.Add(piece);
            }

            if (pieces.Count == 0)
            {
                var status = SchemaStatus(schemaIntervals, page.Start);
                var end = page.End ?? page.Start;
                var reason = status == "SCHEMA_ACTIVE" ? "ZERO_LENGTH_AFTER_INTERSECTION" : status;
                if (status == "BEFORE_FIRST_SCHEMA_ACTIVATION" && end >= schemaIntervals[0].Start) reason = "ZERO_LENGTH_AFTER_INTERSECTION";
                NonEffectiveList.Add(Reject(reason));
                if (reason is "BEFORE_FIRST_SCHEMA_ACTIVATION" or "TEMPORARY_SCHEMA_GAP" or "AFTER_FINAL_SCHEMA_RETIREMENT")
                {
                    Warn("INFO", reason, page, "Page value interval has no overlap with any schema-active interval of its SourceFieldKey.");
                }

                continue;
            }

            Effective.AddRange(pieces);
        }
    }

    public void DetectConflicts()
    {
        var groups = Effective
            .Where(e => e.Semantic is "SETTING" or "PATCH_METADATA")
            .GroupBy(e => (e.Page.Game, e.CanonicalKey))
            .OrderBy(g => g.Key.Game, StringComparer.Ordinal)
            .ThenBy(g => g.Key.CanonicalKey, StringComparer.Ordinal);
        var n = 0;
        foreach (var group in groups)
        {
            var list = group.OrderBy(e => e.EffStart).ToList();
            for (var a = 0; a < list.Count; a++)
            {
                for (var b = a + 1; b < list.Count; b++)
                {
                    var x = list[a];
                    var y = list[b];
                    var overlap = y.EffStart < (x.EffEnd ?? new Pt("9999", long.MaxValue)) && x.EffStart < (y.EffEnd ?? new Pt("9999", long.MaxValue));
                    if (!overlap || string.Equals(x.Page.Value, y.Page.Value, StringComparison.Ordinal)) continue;
                    var id = "C" + (++n).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
                    Conflicts.Add(new Conflict
                    {
                        Id = id, Game = group.Key.Game, Canonical = group.Key.CanonicalKey,
                        ExistingValue = x.Page.Value, NewValue = y.Page.Value,
                        ExistingSource = $"{x.SourceKey}@{x.Page.Start.Rev}", NewSource = $"{y.SourceKey}@{y.Page.Start.Rev}",
                        ExistingStart = x.EffStart, ExistingEnd = x.EffEnd, NewStart = y.EffStart, NewEnd = y.EffEnd
                    });
                    x.ConflictIds = x.ConflictIds.Length == 0 ? id : x.ConflictIds + "|" + id;
                    y.ConflictIds = y.ConflictIds.Length == 0 ? id : y.ConflictIds + "|" + id;
                    Warnings.Add(new Warning
                    {
                        Severity = "HARD_CONFLICT", Code = "OVERLAPPING_DIFFERENT_VALUES", Game = group.Key.Game,
                        Key = group.Key.CanonicalKey, Ts = y.EffStart.Ts, Rev = y.EffStart.Rev,
                        Detail = $"{id}: '{x.Page.Value}' vs '{y.Page.Value}'"
                    });
                }
            }
        }
    }

    private void Warn(string severity, string code, PageInterval page, string detail) =>
        Warnings.Add(new Warning { Severity = severity, Code = code, Game = page.Game, Key = page.Key, Ts = page.Start.Ts, Rev = page.Start.Rev, Detail = detail });
}
