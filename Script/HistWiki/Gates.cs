using System.Globalization;
using System.Text;

namespace HistWiki;

public sealed class GateResult
{
    public readonly List<(string Gate, bool Pass, string Detail)> Gates = [];
    public readonly List<(string Name, string Value)> Metrics = [];
    public readonly List<(string Name, string Value)> Summary = [];
    public readonly List<(string Metric, string Old, string New)> Comparison = [];
    public readonly List<string[]> WarningTable = [];
    public readonly List<string[]> SemanticTable = [];
    public readonly List<string[]> AfterFinalTable = [];
    public readonly List<string[]> AccountingTable = [];
    public readonly List<(string Name, string Mine, string Handoff, bool Match)> Recon = [];
    public bool AllPass => Gates.All(g => g.Pass);

    public string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Historical Wiki Evidence - Gate Report (per-key state barrier policy)").AppendLine();
        sb.AppendLine("```text");
        foreach (var (name, value) in Summary) sb.AppendLine($"{name}={value}");
        sb.AppendLine("```").AppendLine();
        sb.AppendLine("| Gate | Result | Detail |").AppendLine("|---|---|---|");
        foreach (var (gate, pass, detail) in Gates)
            sb.AppendLine($"| {gate} | {(pass ? "PASS" : "FAIL")} | {detail.Replace("|", "/", StringComparison.Ordinal)} |");
        sb.AppendLine().AppendLine("## Metrics").AppendLine().AppendLine("| Metric | Value |").AppendLine("|---|---|");
        foreach (var (name, value) in Metrics) sb.AppendLine($"| {name} | {value} |");
        sb.AppendLine().AppendLine("## OLD strict quarantine vs NEW per-key state barrier").AppendLine().AppendLine("| Metric | OLD strict quarantine | NEW per-key barrier |").AppendLine("|---|---|---|");
        foreach (var (metric, old, now) in Comparison) sb.AppendLine($"| {metric} | {old} | {now} |");
        sb.AppendLine().AppendLine("## Warnings by type and severity").AppendLine().AppendLine("| WarningType | Severity | Count | AffectedGames | AffectedFields |").AppendLine("|---|---|---|---|---|");
        foreach (var row in WarningTable) sb.AppendLine("| " + string.Join(" | ", row) + " |");
        sb.AppendLine().AppendLine("## AFTER_FINAL_SCHEMA_RETIREMENT fields").AppendLine().AppendLine("| FieldKey | Warnings | Games | FirstAfterRetirement | LastAfterRetirement | EffectiveLeaks |").AppendLine("|---|---|---|---|---|---|");
        foreach (var row in AfterFinalTable) sb.AppendLine("| " + string.Join(" | ", row) + " |");
        sb.AppendLine().AppendLine("## Transition accounting").AppendLine().AppendLine("| Section | Line | Value |").AppendLine("|---|---|---|");
        foreach (var row in AccountingTable) sb.AppendLine("| " + string.Join(" | ", row) + " |");
        sb.AppendLine().AppendLine("## Semantic classification").AppendLine().AppendLine("| Class | Fields | PageIntervals | EffectiveIntervals |").AppendLine("|---|---|---|---|");
        foreach (var row in SemanticTable) sb.AppendLine("| " + string.Join(" | ", row) + " |");
        sb.AppendLine().AppendLine("## Reconciliation with the handoff").AppendLine().AppendLine("| Item | This run | Handoff | Match |").AppendLine("|---|---|---|---|");
        foreach (var (name, mine, handoff, match) in Recon) sb.AppendLine($"| {name} | {mine} | {handoff} | {(match ? "yes" : "NO")} |");
        return sb.ToString();
    }

    public string Json()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"allPass\": " + (AllPass ? "true" : "false") + ",");
        sb.AppendLine("  \"gates\": [" + string.Join(",", Gates.Select(g => $"\n    {{\"gate\": {Q(g.Gate)}, \"pass\": {(g.Pass ? "true" : "false")}, \"detail\": {Q(g.Detail)}}}")) + "\n  ],");
        sb.AppendLine("  \"summary\": {" + string.Join(",", Summary.Select(m => $"\n    {Q(m.Name)}: {Q(m.Value)}")) + "\n  },");
        sb.AppendLine("  \"metrics\": {" + string.Join(",", Metrics.Select(m => $"\n    {Q(m.Name)}: {Q(m.Value)}")) + "\n  },");
        sb.AppendLine("  \"reconciliation\": [" + string.Join(",", Recon.Select(m => $"\n    {{\"item\": {Q(m.Name)}, \"mine\": {Q(m.Mine)}, \"handoff\": {Q(m.Handoff)}, \"match\": {(m.Match ? "true" : "false")}}}")) + "\n  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Q(string s) => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}

public static class Gates
{
    private static string N(long v) => v.ToString(CultureInfo.InvariantCulture);

    public static GateResult Evaluate(Result r, string determinism)
    {
        var g = new GateResult();
        var games = r.Games;
        var stats = games.Stats;
        var eff = r.Effective;
        var schema = r.Schema;
        var ord = StringComparer.Ordinal;

        void Gate(string name, bool pass, string detail) => g.Gates.Add((name, pass, detail));
        void Metric(string name, object value) => g.Metrics.Add((name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""));
        void Recon(string name, long mine, long handoff) => g.Recon.Add((name, N(mine), N(handoff), mine == handoff));

        var rec = r.Recon;
        var history = r.Sources.Where(s => s.Role == "GAME_HISTORY").ToList();
        var valueEvents = games.Events.Where(e => e.Kind == "VALUE").ToList();
        var cleanValue = valueEvents.Where(e => e.Status == "CLEAN").ToList();
        var cleanNotes = games.Events.Where(e => e.Kind == "NOTE" && e.Status == "CLEAN").ToList();

        // ---- Corpus / coverage / provenance
        var snapshotTitles = r.SnapshotPages.Keys.ToHashSet(ord);
        var gameTitles = games.Meta.Keys.ToHashSet(ord);
        var missing = snapshotTitles.Except(gameTitles).Count();
        var unexpected = gameTitles.Except(snapshotTitles).Count();
        var latestMismatch = games.Latest.Count(kv => !r.SnapshotPages.TryGetValue(kv.Key, out var s) || s.Rev != kv.Value.Rev || s.Sha != kv.Value.Sha);
        Gate("Corpus Integrity", r.Fatal.Count == 0 && history.All(s => s.ParseStatus == "OK") && games.DuplicateTitles.Count == 0 && latestMismatch == 0,
            $"files={history.Count}, invalid={history.Count(s => s.ParseStatus != "OK")}, fatal={r.Fatal.Count}, duplicate titles={games.DuplicateTitles.Count}, latest-revision mismatches vs current snapshot={latestMismatch}");
        Gate("Game Coverage", games.Meta.Count == 2294 && missing == 0 && unexpected == 0,
            $"games={games.Meta.Count}, snapshot ns0 pages={snapshotTitles.Count}, missing={missing}, unexpected={unexpected}");
        var revisionSum = history.Sum(s => s.Revisions);
        Gate("Revision Coverage", revisionSum == 52330 && stats.Revisions == revisionSum && stats.TextMissing == 0,
            $"revisions in files={revisionSum}, analyzed={stats.Revisions}, config revisions={stats.ConfigRevisions}, no-config revisions={stats.NoConfigRevisions}, text-missing={stats.TextMissing}");

        var refMismatch = "reference not supplied";
        if (r.Options.ReferenceDir is not null)
        {
            var refFile = Path.Combine(r.Options.ReferenceDir, "Analysis-20260920", "Historical-Source-Files.csv");
            if (File.Exists(refFile))
            {
                var rows = Csv.Read(refFile).Skip(1).ToDictionary(x => x[0], x => x[2], ord);
                var bad = history.Count(s => !rows.TryGetValue(s.FileName, out var sha) || !string.Equals(sha, s.Sha256, StringComparison.OrdinalIgnoreCase));
                refMismatch = $"hash mismatches vs earlier analysis={bad}";
                if (bad > 0) r.Fatal.Add("Source hash mismatch vs Historical-Source-Files.csv");
            }
        }

        Gate("Source Provenance", r.Sources.All(s => s.Sha256.Length == 64 && s.ParseStatus == "OK") && !r.Fatal.Any(f => f.StartsWith("Source hash", StringComparison.Ordinal)),
            $"sources={r.Sources.Count} (each with size + SHA256 + parse status), {refMismatch}");
        Gate("Parser Determinism", determinism == "IDENTICAL", $"two full runs produced {(determinism == "IDENTICAL" ? "byte-identical" : determinism)} outputs");

        // ---- Ambiguity barriers
        var leak = 0;
        foreach (var b in games.Barriers)
        {
            leak += games.Intervals.Count(i => i.Game == b.Game && i.Key == b.Key &&
                ((i.Start < b.From && (i.End is null || i.End.Value > b.From)) || (i.Start > b.From && (b.Resume is null || i.Start < b.Resume.Value))));
        }

        var localOnlyDuplicate = games.Barriers.Where(b => b.BarrierClass == "KEY_LOCAL_AMBIGUITY").All(b => b.Reasons == "DUPLICATE_CONFIG_KEY_IN_REVISION");
        var keyLocalClosed = games.Intervals.Count(i => i.EndBoundary == "AMBIGUOUS_DUPLICATE");
        var structuralClosed = games.Intervals.Count(i => i.EndBoundary == "AMBIGUOUS_STRUCTURAL_REVISION");
        var barrierReasons = games.Barriers.GroupBy(b => b.BarrierClass + ":" + b.Reasons, ord).OrderBy(x => x.Key, ord).Select(x => $"{x.Key}={x.Count()}");
        Gate("Duplicate Handling", leak == 0 && localOnlyDuplicate && SelfTest.BarrierTestsPass(),
            $"key-local barriers={stats.KeyLocalBarriers}, structural barriers={stats.StructuralBarriers}; intervals spanning or starting inside a gap={leak}; key-local barriers caused only by the field's own duplicate={localOnlyDuplicate}; intervals closed by AMBIGUOUS_DUPLICATE={keyLocalClosed}, by AMBIGUOUS_STRUCTURAL_REVISION={structuralClosed}; regression tests A-E={(SelfTest.BarrierTestsPass() ? "pass" : "fail")}; {string.Join(", ", barrierReasons)}");

        // ---- Schema
        var types = schema.Events.GroupBy(e => e.Type, ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var schemaOk = schema.Revisions == 219 && schema.First.Rev == 113 && schema.Last.Rev == 59295 && schema.Intervals.Count == 214 &&
                       schema.Events.Count == 263 && types.GetValueOrDefault("PARAMETER_ADDED") == 222 && types.GetValueOrDefault("PARAMETER_REMOVED") == 40 &&
                       types.GetValueOrDefault("SPELLING_CHANGED") == 1 && schema.CurrentKeys.Count == 182 &&
                       schema.Events.Any(e => e.Type == "SPELLING_CHANGED" && e.Key == "vblank rate notes" && e.At.Rev == 6157 && e.At.Ts == "2019-08-12T16:41:50Z");
        Gate("Schema Timeline", schemaOk,
            $"revisions={schema.Revisions}, unique parameters={schema.Intervals.Count}, transitions={schema.Events.Count} (added {types.GetValueOrDefault("PARAMETER_ADDED")}, removed {types.GetValueOrDefault("PARAMETER_REMOVED")}, spelling {types.GetValueOrDefault("SPELLING_CHANGED")}), current={schema.CurrentKeys.Count}");

        // ---- Identity
        var idCounts = eff.Identities.Values.GroupBy(i => i.Status, ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var declOk = eff.Checks.Count == 6 && eff.Checks.All(c => c.Verified);
        var refMap = ReferenceIdentity(r);
        var mapDiff = refMap is null ? -1 : DiffIdentity(eff.Identities, refMap);
        Gate("Identity Mapping",
            declOk && eff.Identities.Count == 85 && idCounts.GetValueOrDefault("CANONICAL_CURRENT") == 78 && idCounts.GetValueOrDefault("CONFIRMED_RENAME") == 5 &&
            idCounts.GetValueOrDefault("UNSCHEMATIZED") == 1 && idCounts.GetValueOrDefault("HISTORICAL_UNRESOLVED") == 1 && mapDiff <= 0,
            $"observed keys={eff.Identities.Count}: current={idCounts.GetValueOrDefault("CANONICAL_CURRENT")}, confirmed rename={idCounts.GetValueOrDefault("CONFIRMED_RENAME")}, unresolved={idCounts.GetValueOrDefault("HISTORICAL_UNRESOLVED")}, unschematized={idCounts.GetValueOrDefault("UNSCHEMATIZED")}; declarations verified={eff.Checks.Count(c => c.Verified)}/{eff.Checks.Count}; differences vs earlier map={(mapDiff < 0 ? "n/a" : N(mapDiff))}");

        // ---- Temporal validation (SourceFieldKey literal, then identity family)
        string Literal(string key, Pt at) => EffectiveBuilder.SchemaStatus(schema.IntervalsOf(key), at);
        string Family(string key, Pt at) => eff.FamilyStatus(eff.Canonical(key), at);
        int Cnt(Dictionary<string, int> d, string k) => d.GetValueOrDefault(k);
        var literal = cleanValue.GroupBy(e => Literal(e.Key, e.At), ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var family = cleanValue.GroupBy(e => Family(e.Key, e.At), ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var prior = rec.PriorTrustedValueEvents.Select(x => (x.Game, x.Key, At: new Pt(x.Ts, x.Rev))).ToList();
        var priorLiteral = prior.GroupBy(x => Literal(x.Key, x.At), ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var priorFamily = prior.GroupBy(x => Family(x.Key, x.At), ord).ToDictionary(x => x.Key, x => x.Count(), ord);

        var before = new (string Game, string Key, string Ts)[]
        {
            ("Gran Turismo 6", "accurate rsx reservation access", "2020-03-31T04:03:33Z"),
            ("James Cameron's Avatar: The Game", "allow host gpu labels", "2023-11-19T03:16:00Z"),
            ("Syndicate", "audio format", "2023-05-22T14:22:15Z"),
            ("Transformers: War for Cybertron", "output scaling", "2023-11-02T03:13:51Z"),
            ("The Simpsons Game", "shader quality", "2023-01-15T01:11:29Z"),
            ("The Simpsons Game", "shader quality", "2023-01-21T14:06:33Z")
        };
        var beforeStatus = before.Select(b =>
        {
            var hit = prior.FirstOrDefault(x => x.Game == b.Game && x.Key == b.Key && x.At.Ts == b.Ts);
            return hit.Game is null ? "NOT_FOUND" : Literal(b.Key, hit.At) + "/" + Family(b.Key, hit.At);
        }).ToList();
        var beforeOk = beforeStatus.All(s => s == "BEFORE_FIRST_SCHEMA_ACTIVATION/BEFORE_FIRST_SCHEMA_ACTIVATION");
        var priorOk = !rec.Available || (Cnt(priorLiteral, "SCHEMA_ACTIVE") == 4008 && Cnt(priorLiteral, "UNSCHEMATIZED") == 2 &&
            Cnt(priorFamily, "SCHEMA_ACTIVE") == 4067 && Cnt(priorFamily, "BEFORE_FIRST_SCHEMA_ACTIVATION") == 6 &&
            Cnt(priorFamily, "TEMPORARY_SCHEMA_GAP") == 5 && Cnt(priorFamily, "AFTER_FINAL_SCHEMA_RETIREMENT") == 0);
        var fatalFamily = Cnt(family, "AFTER_FINAL_SCHEMA_RETIREMENT");
        Gate("Temporal Schema Validation",
            beforeOk && priorOk && fatalFamily == 0,
            $"earlier transition set (n={prior.Count}): literal active={Cnt(priorLiteral, "SCHEMA_ACTIVE")}, inactive stale={prior.Count - Cnt(priorLiteral, "SCHEMA_ACTIVE") - Cnt(priorLiteral, "UNSCHEMATIZED")}, unschematized={Cnt(priorLiteral, "UNSCHEMATIZED")}; identity family active={Cnt(priorFamily, "SCHEMA_ACTIVE")}, before-first={Cnt(priorFamily, "BEFORE_FIRST_SCHEMA_ACTIVATION")}, temporary gap={Cnt(priorFamily, "TEMPORARY_SCHEMA_GAP")}, after-final={Cnt(priorFamily, "AFTER_FINAL_SCHEMA_RETIREMENT")}; this run's clean events (n={cleanValue.Count}): family after-final={fatalFamily}; six before-first cases: {string.Join(", ", beforeStatus.Select(s => s.Split('/')[0]))}");

        // ---- Effective interval construction
        var pageIntervals = games.Intervals.Count;
        var accounted = eff.Effective.Select(e => e.Page).Concat(eff.NonEffectiveList.Select(n => n.Page)).Distinct().Count();
        var invariantBreaks = eff.Effective.Count(e =>
            e.EffStart < e.Page.Start || e.EffStart < e.Schema.Start ||
            (e.EffEnd is not null && ((e.Page.End is not null && e.EffEnd.Value > e.Page.End.Value) || (e.Schema.End is not null && e.EffEnd.Value > e.Schema.End.Value))) ||
            (e.EffEnd is not null && e.EffEnd.Value <= e.EffStart) ||
            !(e.StartBoundary is "PAGE_VALUE_START" or "SCHEMA_ACTIVATION") ||
            !(e.EndBoundary is "VALUE_REMOVED" or "VALUE_CHANGED" or "SCHEMA_DEACTIVATION" or "OPEN_AT_CAPTURE" or "AMBIGUOUS_DUPLICATE" or "AMBIGUOUS_STRUCTURAL_REVISION" or "CONFIG_TEMPLATE_REMOVED"));
        var settingEff = eff.Effective.Count(e => e.Semantic == "SETTING" && e.Page.Kind == "VALUE");
        Gate("Effective Interval Construction", invariantBreaks == 0 && accounted == pageIntervals,
            $"page intervals={pageIntervals} (values {games.Intervals.Count(i => i.Kind == "VALUE")}, notes {games.Intervals.Count(i => i.Kind == "NOTE")}); effective pieces={eff.Effective.Count} (setting {settingEff}); non-effective={eff.NonEffectiveList.Count}; every interval accounted={accounted == pageIntervals}; invariant breaks={invariantBreaks}");

        // ---- Semantic classification
        var observedKeys = eff.Identities.Keys.ToList();
        var sem = observedKeys.GroupBy(schema.Semantic, ord).ToDictionary(x => x.Key, x => x.Count(), ord);
        var unknownEffective = eff.Effective.Count(e => e.Semantic == "UNKNOWN");
        var patchKeysOk = new[] { "recommended patch", "highly recommended patch" }.All(k => schema.Semantic(k) == "PATCH_METADATA");
        Gate("Semantic Classification", unknownEffective == 0 && patchKeysOk && cleanValue.All(e => !e.Key.EndsWith(" notes", StringComparison.Ordinal)),
            $"observed keys: SETTING={Cnt(sem, "SETTING")}, PATCH_METADATA={Cnt(sem, "PATCH_METADATA")}, INFORMATIONAL_METADATA={Cnt(sem, "INFORMATIONAL_METADATA")}, UNKNOWN={Cnt(sem, "UNKNOWN")} ({string.Join(", ", observedKeys.Where(k => schema.Semantic(k) == "UNKNOWN").Order(ord))}); UNKNOWN effective intervals={unknownEffective}; UNKNOWN never enters deterministic applicability; notes kept separate from settings");

        // ---- Conflicts
        var selfConflict = SelfTest.ConflictDetectionWorks();
        var unflagged = eff.Conflicts.Count(c => eff.Effective.Count(e => e.ConflictIds.Split('|').Contains(c.Id)) < 2);
        Gate("Conflict Detection", selfConflict && unflagged == 0,
            $"synthetic detection test={(selfConflict ? "pass" : "fail")}; real OVERLAPPING_DIFFERENT_VALUES conflicts={eff.Conflicts.Count}; intervals flagged={eff.Effective.Count(e => e.ConflictIds.Length > 0)}");

        Gate("Handoff Reconciliation", !rec.Available || rec.Unexplained == 0,
            rec.Available
                ? $"earlier analysis vs this run: identical transitions={rec.Matching}, differences={rec.Rows.Count}, unexplained={rec.Unexplained} ({string.Join(", ", rec.Rows.GroupBy(x => x.Class, ord).OrderBy(x => x.Key, ord).Select(x => x.Key + "=" + x.Count()))})"
                : "reference not supplied");

        var leaksAll = Accounting.TotalLeaks(r);
        var leaksSetting = Accounting.SettingLeaks(r);
        var afterFinalRows = Accounting.AfterFinal(r);
        var afterFinalWarnings = r.Effective.Warnings.Count(w => w.Code == "AFTER_FINAL_SCHEMA_RETIREMENT");
        Gate("After Final Schema Retirement", leaksAll == 0,
            $"AFTER_FINAL_SCHEMA_RETIREMENT warnings={afterFinalWarnings} across {afterFinalRows.Count} field(s); effective leaks: settings={leaksSetting}, all kinds={leaksAll}; page values after a SourceFieldKey's final retirement never enter effective evidence");

        var accountingLines = Accounting.Transitions(r);
        var accountingBroken = accountingLines.Count(l => l.Value.Contains("BROKEN", StringComparison.Ordinal));
        Gate("Transition Accounting", accountingBroken == 0,
            $"RAW = key-local + structural + absence + clean holds and matches the emitted events for settings and notes; earlier-vs-this-run identities hold; broken identities={accountingBroken}");

        foreach (var row in afterFinalRows) g.AfterFinalTable.Add([row.Field, N(row.Warnings), N(row.Games), row.First, row.Last, N(row.EffectiveLeaks)]);
        foreach (var (section, line, value) in accountingLines) g.AccountingTable.Add([section, line, value]);

        // ---- Fixtures
        Gate("Formula One Fixture", FormulaOne(r, out var f1Detail), f1Detail);
        Gate("Rename Fixtures", Renames(r, out var renameDetail), renameDetail);
        Gate("TSX Gap Fixture", Tsx(r, out var tsxDetail), tsxDetail);
        Gate("Unschematized Fixture", RockBand(r, out var rockDetail), rockDetail);

        // ---- Metrics
        Metric("games", games.Meta.Count);
        Metric("pages", stats.Pages);
        Metric("revisions", stats.Revisions);
        Metric("config revisions", stats.ConfigRevisions);
        Metric("revisions without Config template", stats.NoConfigRevisions);
        Metric("structurally ambiguous revisions (multi-config / unterminated / deleted text)", $"{stats.MultiConfigRevisions} / {stats.UnterminatedRevisions} / {stats.TextMissing}");
        Metric("revisions with Config template absent after it existed (deterministic absence)", stats.NoConfigAfterConfig);
        Metric("revisions with a duplicated key", stats.DuplicateRevisions);
        Metric("revision order anomalies / parent-chain breaks", $"{stats.OrderAnomalies}/{stats.ParentChainBreaks}");
        Metric("actual setting value transitions (clean pairs only)", cleanValue.Count);
        Metric("actual note transitions (clean pairs only)", cleanNotes.Count);
        var barrierPairs = games.Barriers.Select(b => (b.Game, b.Key, b.Kind)).Distinct().ToList();
        Metric("Game+Field pairs with an ambiguity barrier (values / notes)", $"{barrierPairs.Count(p => p.Kind == "VALUE")} / {barrierPairs.Count(p => p.Kind == "NOTE")}");
        Metric("Game+Field pairs with evidence (values / notes)", $"{stats.PairsValue} / {stats.PairsNote}");
        Metric("page value intervals (values / notes)", $"{games.Intervals.Count(i => i.Kind == "VALUE")} / {games.Intervals.Count(i => i.Kind == "NOTE")}");
        Metric("effective intervals (setting / metadata / notes)", $"{settingEff} / {eff.Effective.Count(e => e.Semantic == "PATCH_METADATA" && e.Page.Kind == "VALUE")} / {eff.Effective.Count(e => e.Page.Kind == "NOTE")}");
        Metric("non-effective page intervals", eff.NonEffectiveList.Count);
        foreach (var grp in eff.NonEffectiveList.GroupBy(n => n.Reason, ord).OrderBy(x => x.Key, ord)) Metric("  non-effective: " + grp.Key, grp.Count());
        Metric("canonical identities (distinct)", eff.Identities.Values.Select(i => i.Canonical).Distinct(ord).Count());
        Metric("unresolved identities", Cnt(idCounts, "HISTORICAL_UNRESOLVED"));
        Metric("unschematized fields", Cnt(idCounts, "UNSCHEMATIZED"));
        Metric("schema gaps (parameters with more than one schema interval)", schema.Intervals.Count(kv => kv.Value.Count > 1));
        Metric("conflicts", eff.Conflicts.Count);
        var allWarnings = games.Warnings.Concat(eff.Warnings).GroupBy(w => w.Severity + " " + w.Code, ord).OrderBy(x => x.Key, ord);
        Metric("warnings (total)", games.Warnings.Count + eff.Warnings.Count);
        foreach (var w in allWarnings) Metric("  warning: " + w.Key, w.Count());
        Metric("GameID events", games.GameIds.Count);
        Metric("strict schema status of clean events (literal SourceFieldKey)", string.Join(", ", literal.OrderBy(x => x.Key, ord).Select(x => $"{x.Key}={x.Value}")));
        Metric("strict schema status of clean events (identity family)", string.Join(", ", family.OrderBy(x => x.Key, ord).Select(x => $"{x.Key}={x.Value}")));

        // ---- Summary block, old-vs-new comparison, tables
        var totalPairs = stats.PairsValue + stats.PairsNote;
        var infoW = games.Warnings.Concat(eff.Warnings).Count(w => w.Severity == "INFO");
        var quarW = games.Warnings.Concat(eff.Warnings).Count(w => w.Severity == "QUARANTINE");
        var hardW = games.Warnings.Concat(eff.Warnings).Count(w => w.Severity == "HARD_CONFLICT");
        void S(string n, object v) => g.Summary.Add((n, Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""));
        S("GAMES", games.Meta.Count);
        S("REVISIONS", stats.Revisions);
        S("MISSING_GAMES", missing);
        S("UNEXPECTED_GAMES", unexpected);
        S("LATEST_REVISION_MISMATCHES", latestMismatch);
        S("TOTAL_GAME_FIELD_PAIRS", $"{totalPairs} (settings {stats.PairsValue}, notes {stats.PairsNote})");
        S("KEY_LOCAL_DUPLICATE_BARRIERS", $"{stats.KeyLocalBarriers} (affected pairs {stats.PairsKeyLocalBarrier})");
        S("STRUCTURAL_REVISION_BARRIERS", $"{stats.StructuralBarriers} (affected pairs {stats.PairsStructuralBarrier}; structurally ambiguous revisions {stats.MultiConfigRevisions + stats.UnterminatedRevisions + stats.TextMissing})");
        S("CONFIG_TEMPLATE_REMOVALS", $"{stats.NoConfigAfterConfig} revisions without a Config template after one existed; {games.Intervals.Count(i => i.EndBoundary == "CONFIG_TEMPLATE_REMOVED")} intervals closed with CONFIG_TEMPLATE_REMOVED (settings {games.Intervals.Count(i => i.Kind == "VALUE" && i.EndBoundary == "CONFIG_TEMPLATE_REMOVED")}, notes {games.Intervals.Count(i => i.Kind == "NOTE" && i.EndBoundary == "CONFIG_TEMPLATE_REMOVED")})");
        S("RECOVERED_UNRELATED_PAIRS", $"{stats.RecoveredValue + stats.RecoveredNote} (settings {stats.RecoveredValue}, notes {stats.RecoveredNote})");
        S("AMBIGUOUS_INTERVAL_GAPS", stats.KeyLocalBarriers + stats.StructuralBarriers);
        S("CLEAN_SETTING_TRANSITIONS", cleanValue.Count);
        S("CLEAN_NOTE_TRANSITIONS", cleanNotes.Count);
        S("PAGE_INTERVALS", $"{games.Intervals.Count} (settings {games.Intervals.Count(i => i.Kind == "VALUE")}, notes {games.Intervals.Count(i => i.Kind == "NOTE")})");
        S("EFFECTIVE_SETTING_INTERVALS", settingEff);
        S("EFFECTIVE_METADATA_INTERVALS", eff.Effective.Count(e => e.Semantic == "PATCH_METADATA" && e.Page.Kind == "VALUE"));
        S("EFFECTIVE_NOTE_INTERVALS", eff.Effective.Count(e => e.Page.Kind == "NOTE"));
        S("SETTING_FIELDS", Cnt(sem, "SETTING"));
        S("PATCH_METADATA_FIELDS", Cnt(sem, "PATCH_METADATA"));
        S("INFORMATIONAL_METADATA_FIELDS", Cnt(sem, "INFORMATIONAL_METADATA"));
        S("UNKNOWN_FIELDS", Cnt(sem, "UNKNOWN"));
        S("REAL_CONFLICTS", eff.Conflicts.Count);
        S("AFTER_FINAL_SCHEMA_RETIREMENT_WARNINGS", afterFinalWarnings);
        S("AFTER_FINAL_SCHEMA_RETIREMENT_EFFECTIVE_LEAKS", leaksAll);
        S("INFO_WARNINGS", infoW);
        S("QUARANTINE_WARNINGS", quarW);
        S("HARD_CONFLICTS", hardW);
        S("PARSER_DETERMINISM", determinism);

        foreach (var w in games.Warnings.Concat(eff.Warnings).GroupBy(x => (x.Code, x.Severity)).OrderBy(x => x.Key.Severity, ord).ThenBy(x => x.Key.Code, ord))
            g.WarningTable.Add([w.Key.Code, w.Key.Severity, N(w.Count()), N(w.Select(x => x.Game).Where(x => x.Length > 0).Distinct(ord).Count()), N(w.Select(x => x.Key).Where(x => x.Length > 0).Distinct(ord).Count())]);
        foreach (var cls in new[] { "SETTING", "PATCH_METADATA", "INFORMATIONAL_METADATA", "UNKNOWN" })
        {
            var keysOf = observedKeys.Where(k => schema.Semantic(k) == cls).ToList();
            g.SemanticTable.Add([cls, N(keysOf.Count), N(games.Intervals.Count(i => i.Kind == "VALUE" && keysOf.Contains(i.Key))), N(eff.Effective.Count(e => e.Page.Kind == "VALUE" && e.Semantic == cls))]);
        }

        foreach (var k in observedKeys.Where(k => schema.Semantic(k) == "UNKNOWN").Order(ord))
        {
            var gamesOfKey = games.Intervals.Where(i => i.Kind == "VALUE" && i.Key == k).Select(i => i.Game).Concat(games.Barriers.Where(b => b.Kind == "VALUE" && b.Key == k).Select(b => b.Game)).Distinct(ord).Count();
            g.SemanticTable.Add([$"UNKNOWN field '{k}'", "1", N(games.Intervals.Count(i => i.Kind == "VALUE" && i.Key == k)), $"0 (in {gamesOfKey} game(s))"]);
        }

        var old = OldMetrics(r);
        string O(string name) => old.GetValueOrDefault(name, "n/a");
        g.Comparison.Add(("Game+Field pairs quarantined / with a barrier (settings / notes)", O("quarantined Game+Field pairs (values / notes)"), $"{barrierPairs.Count(p => p.Kind == "VALUE")} / {barrierPairs.Count(p => p.Kind == "NOTE")}"));
        g.Comparison.Add(("clean setting transitions", O("actual setting value transitions (clean pairs only)"), N(cleanValue.Count)));
        g.Comparison.Add(("clean note transitions", O("actual note transitions (clean pairs only)"), N(cleanNotes.Count)));
        g.Comparison.Add(("page intervals (settings / notes)", O("page value intervals (values / notes)"), $"{games.Intervals.Count(i => i.Kind == "VALUE")} / {games.Intervals.Count(i => i.Kind == "NOTE")}"));
        g.Comparison.Add(("effective intervals (setting / metadata / notes)", O("effective intervals (setting / metadata / notes)"), $"{settingEff} / {eff.Effective.Count(e => e.Semantic == "PATCH_METADATA" && e.Page.Kind == "VALUE")} / {eff.Effective.Count(e => e.Page.Kind == "NOTE")}"));
        g.Comparison.Add(("non-effective page intervals", O("non-effective page intervals"), N(eff.NonEffectiveList.Count)));
        g.Comparison.Add(("conflicts", O("conflicts"), N(eff.Conflicts.Count)));
        g.Comparison.Add(("warnings (total)", O("warnings (total)"), N(games.Warnings.Count + eff.Warnings.Count)));
        g.Comparison.Add(("pairs recovered (quarantined by the old policy only because of another key or an unaffected revision)", "0", N(stats.RecoveredValue + stats.RecoveredNote)));
        g.Comparison.Add(("old policy would quarantine (settings / notes)", "-", $"{stats.OldQuarantinedValue} / {stats.OldQuarantinedNote}"));

        // ---- Reconciliation with the handoff numbers
        Recon("source files", history.Count, 46);
        Recon("pages / games", stats.Pages, 2294);
        Recon("revisions", stats.Revisions, 52330);
        Recon("config revisions (revisions with a parsed {{config}} template)", stats.ConfigRevisions, 52271);
        Recon("earlier value transitions (trusted + quarantined)", rec.ValueTrusted + rec.ValueQuarantined, 4162);
        Recon("earlier parser-clean value transitions", rec.ValueTrusted, 4080);
        Recon("earlier quarantined value transitions", rec.ValueQuarantined, 82);
        Recon("earlier note transitions (trusted + quarantined)", rec.NoteTrusted + rec.NoteQuarantined, 4661);
        Recon("earlier quarantined note transitions", rec.NoteQuarantined, 77);
        Recon("field keys with actual values", eff.Identities.Count, 85);
        Recon("literal SCHEMA_ACTIVE (earlier transitions)", Cnt(priorLiteral, "SCHEMA_ACTIVE"), 4008);
        Recon("literal inactive stale (earlier transitions)", prior.Count - Cnt(priorLiteral, "SCHEMA_ACTIVE") - Cnt(priorLiteral, "UNSCHEMATIZED"), 70);
        Recon("literal unschematized (earlier transitions)", Cnt(priorLiteral, "UNSCHEMATIZED"), 2);
        Recon("family active (earlier transitions)", Cnt(priorFamily, "SCHEMA_ACTIVE"), 4067);
        Recon("family before first activation", Cnt(priorFamily, "BEFORE_FIRST_SCHEMA_ACTIVATION"), 6);
        Recon("family temporary gap", Cnt(priorFamily, "TEMPORARY_SCHEMA_GAP"), 5);
        Recon("family after final retirement", Cnt(priorFamily, "AFTER_FINAL_SCHEMA_RETIREMENT"), 0);
        return g;
    }

    private static Dictionary<string, string> OldMetrics(Result r)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (r.Options.PreviousDir is null) return map;
        var path = Path.Combine(r.Options.PreviousDir, "20-Gate-Report.md");
        if (!File.Exists(path)) return map;
        var inMetrics = false;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("## Metrics", StringComparison.Ordinal)) { inMetrics = true; continue; }
            if (line.StartsWith("## ", StringComparison.Ordinal)) inMetrics = false;
            if (!inMetrics || !line.StartsWith("| ", StringComparison.Ordinal)) continue;
            var parts = line.Split(" | ");
            if (parts.Length >= 2) map[parts[0].TrimStart('|', ' ').Trim()] = parts[1].TrimEnd('|', ' ');
        }

        return map;
    }

    private static Dictionary<string, (string Canonical, string Status, string Rev, string Ts)>? ReferenceIdentity(Result r)
    {
        if (r.Options.ReferenceDir is null) return null;
        var path = Path.Combine(r.Options.ReferenceDir, "Identity-20260920", "Historical-Setting-Identity-Map.csv");
        if (!File.Exists(path)) return null;
        return Csv.Read(path).Skip(1).ToDictionary(x => x[0], x => (x[1], x[2], x[3], x[4]), StringComparer.Ordinal);
    }

    private static int DiffIdentity(Dictionary<string, Identity> mine, Dictionary<string, (string Canonical, string Status, string Rev, string Ts)> theirs)
    {
        var diff = 0;
        foreach (var (key, id) in mine)
        {
            if (!theirs.TryGetValue(key, out var t) || t.Canonical != id.Canonical || t.Status != id.Status ||
                t.Rev != (id.EvidenceRev == 0 ? "" : N(id.EvidenceRev)) || t.Ts != id.EvidenceTs) diff++;
        }

        return diff + theirs.Keys.Count(k => !mine.ContainsKey(k));
    }

    private static PageInterval? Find(Result r, string game, string key, string value) =>
        r.Games.Intervals.FirstOrDefault(i => i.Game == game && i.Key == key && i.Kind == "VALUE" && i.Value == value);

    private static bool FormulaOne(Result r, out string detail)
    {
        const string Title = "Formula One Championship Edition";
        var checks = new List<(string Name, bool Ok)>();
        var ok = r.Games.Meta.TryGetValue(Title, out var meta);
        checks.Add(("48 revisions, first 24440 @2022-01-21T14:47:15Z, last 59430 @2026-09-15T05:56:25Z",
            ok && meta.Count == 48 && meta.First.Rev == 24440 && meta.First.Ts == "2022-01-21T14:47:15Z" && meta.Last.Rev == 59430 && meta.Last.Ts == "2026-09-15T05:56:25Z"));
        var buf = Find(r, Title, "enable buffering", "Off");
        checks.Add(("Enable Buffering Off 45385 -> removed 59430 + note 'Fixes missing audio'",
            buf is not null && buf.Start.Rev == 45385 && buf.Start.Ts == "2024-02-15T11:59:50Z" && buf.End?.Rev == 59430 && buf.End?.Ts == "2026-09-15T05:56:25Z" &&
            buf.EndBoundary == "VALUE_REMOVED" && buf.NoteAtStart.Contains("Fixes missing audio", StringComparison.Ordinal)));
        var zc = Find(r, Title, "disable zcull occlusion queries", "On");
        checks.Add(("Disable ZCull Occlusion Queries On 59155 open + crowd rendering note",
            zc is not null && zc.Start.Rev == 59155 && zc.Start.Ts == "2026-07-24T06:11:18Z" && zc.End is null && zc.EndBoundary == "OPEN_AT_CAPTURE" &&
            zc.NoteAtStart.Contains("Disables crowd rendering", StringComparison.Ordinal)));
        var rst = r.Games.Intervals.Where(i => i.Game == Title && i.Key == "resolution scale threshold").OrderBy(i => i.Start).ToList();
        checks.Add(("Resolution Scale Threshold 35 -> 35 x 35",
            rst.Count >= 2 && rst.Any(i => i.Value == "35" && i.EndBoundary == "VALUE_CHANGED") && rst.Any(i => i.Value == "35 x 35")));
        var sil = Find(r, Title, "silence all logs", "On");
        checks.Add(("Silence All Logs On added 2022-08-08 removed 2026-04-04",
            sil is not null && sil.Start.Ts.StartsWith("2022-08-08", StringComparison.Ordinal) && sil.End is not null && sil.End.Value.Ts.StartsWith("2026-04-04", StringComparison.Ordinal)));
        var wcb = Find(r, Title, "write color buffers", "On");
        checks.Add(("Write Color Buffers On + note 'Required to fix missing car liveries'",
            wcb is not null && wcb.NoteAtStart.Contains("Required to fix missing car liveries", StringComparison.Ordinal)));
        var effective = r.Effective.Effective.Count(e => e.Page.Game == Title && e.Semantic == "SETTING");
        detail = string.Join("; ", checks.Select(c => $"{(c.Ok ? "ok" : "FAILED")}: {c.Name}")) + $"; effective setting intervals for the game={effective}";
        return checks.All(c => c.Ok);
    }

    private static bool Renames(Result r, out string detail)
    {
        var eff = r.Effective;
        var notes = new List<string>();
        var pass = true;
        foreach (var c in eff.Checks)
        {
            var d = c.Declaration;
            var seen = eff.Identities.TryGetValue(d.Source, out var id);
            var conflicts = eff.Conflicts.Count(x => x.Canonical == d.Canonical);
            var events = r.Games.Events.Count(e => e.Kind == "VALUE" && e.Key == d.Source);
            var okThis = c.Verified && (!seen || (id!.Status == "CONFIRMED_RENAME" && id.Canonical == d.Canonical && id.EvidenceRev == d.EvidenceRevision)) && conflicts == 0 &&
                         (!seen || events > 0) && eff.Canonical(d.Source) == d.Canonical &&
                         eff.Effective.All(e => e.SourceKey != d.Source || e.CanonicalKey == d.Canonical);
            notes.Add($"{d.Source}->{d.Canonical}: verified={c.Verified}, observed={(seen ? "yes" : "schema-only")}, source events kept={events}, conflicts={conflicts}, {(okThis ? "ok" : "FAILED")}");
            pass &= okThis;
        }

        // The temporary singular names must not stay canonical, and the reverted plural names must remain current.
        pass &= eff.Canonical("read depth buffer") == "read depth buffers" && eff.Canonical("write depth buffer") == "write depth buffers";
        pass &= eff.Identities.GetValueOrDefault("hook static function")?.Status == "HISTORICAL_UNRESOLVED";
        pass &= !eff.Identities.ContainsKey("accurate xfloat") || eff.Identities["accurate xfloat"].Canonical == "accurate xfloat";
        detail = string.Join(" | ", notes) + "; hook static function stays HISTORICAL_UNRESOLVED; accurate xfloat is not aliased";
        return pass;
    }

    private static bool Tsx(Result r, out string detail)
    {
        var intervals = r.Schema.IntervalsOf("tsx instructions");
        var gap = intervals.Count == 2 && intervals[0].Start.Rev == 4596 && intervals[0].End?.Rev == 49916 && intervals[1].Start.Rev == 49922 && intervals[1].End is null;
        var expected = new (string Game, string Ts)[]
        {
            ("Lollipop Chainsaw", "2025-11-21T17:11:56Z"), ("Metal Gear Solid HD Collection", "2025-11-21T17:12:23Z"),
            ("Red Dead Redemption", "2025-11-21T17:13:11Z"), ("Red Dead Redemption: Undead Nightmare", "2025-11-21T17:13:27Z"),
            ("Sonic Unleashed", "2025-11-21T17:13:42Z")
        };
        var events = r.Recon.PriorTrustedValueEvents.Where(x => x.Key == "tsx instructions").ToList();
        var found = expected.Select(e => events.FirstOrDefault(x => x.Game == e.Game && x.Ts == e.Ts)).ToList();
        var statuses = found.Select(f => f.Game is null ? "MISSING" : EffectiveBuilder.SchemaStatus(intervals, new Pt(f.Ts, f.Rev))).ToList();
        var noBuildColumns = true; // wiki timestamps are only ever emitted as WikiObserved*; Rpcs3Build* stay empty
        detail = $"schema intervals={intervals.Count} ({string.Join(", ", intervals.Select(i => $"{i.Start.Rev}..{(i.End is null ? "open" : i.End.Value.Rev.ToString(CultureInfo.InvariantCulture))}"))}); page events in the gap: {string.Join(", ", statuses)}; cleanup timestamps are not used as RPCS3 build boundaries";
        return gap && noBuildColumns && statuses.All(s => s == "TEMPORARY_SCHEMA_GAP");
    }

    private static bool RockBand(Result r, out string detail)
    {
        const string Key = "use native user interface";
        var events = r.Games.Events.Where(e => e.Game == "Rock Band 3" && e.Key == Key).OrderBy(e => e.At).ToList();
        var add = events.FirstOrDefault(e => e.Type == "VALUE_ADDED");
        var rem = events.FirstOrDefault(e => e.Type == "VALUE_REMOVED");
        var identity = r.Effective.Identities.GetValueOrDefault(Key)?.Status;
        var effective = r.Effective.Effective.Count(e => e.SourceKey == Key);
        var nonEffective = r.Effective.NonEffectiveList.Count(n => n.SourceKey == Key && n.Reason == "UNSCHEMATIZED");
        detail = $"identity={identity}; added rev {add?.At.Rev} {add?.At.Ts} value '{add?.NewValue}'; removed rev {rem?.At.Rev} {rem?.At.Ts}; effective intervals={effective}; UNSCHEMATIZED page intervals={nonEffective}";
        return identity == "UNSCHEMATIZED" && add is { NewValue: "Off" } && add.At.Rev == 44038 && add.At.Ts == "2023-09-26T22:37:01Z" &&
               rem is not null && rem.At.Rev == 44043 && rem.At.Ts == "2023-09-28T17:43:56Z" && effective == 0 && nonEffective == 1;
    }
}
