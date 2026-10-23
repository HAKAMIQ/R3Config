namespace HistWiki;

// Fast synthetic regression tests: no input files, no network.
public static class SelfTest
{
    private static int _failures;

    private static void Check(string name, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
        if (!ok) _failures++;
    }

    public static int Run()
    {
        _failures = 0;
        Parser();
        Intervals();
        Check("conflict detection", ConflictDetectionWorks());
        Check("removal classification", RemovalTests());
        var barrier = BarrierTests();
        Check("barrier tests A-E" + (barrier.Ok ? "" : ": " + barrier.Detail), barrier.Ok);
        Console.WriteLine(_failures == 0 ? "selftest: all passed" : $"selftest: {_failures} failed");
        return _failures == 0 ? 0 : 1;
    }

    private static void Parser()
    {
        var text = "intro {{vginfobox|a=1}}\n==Config==\n<!-- |ignored = pipe in comment -->{{config\n|ppu decoder = Recompiler (LLVM) <!-- x | y -->\n|ppu decoder notes = See [[Link|text]] and {{issue|123}}\n|renderer=Vulkan\n}}";
        var p = WikiText.ParseConfig(text);
        Check("parser: one template, no issues", p.TemplateCount == 1 && p.Issues.Count == 0);
        Check("parser: keys and clean values", p.Params.Count == 3 && p.Params[0].Key == "ppu decoder" && p.Params[0].CleanValue == "Recompiler (LLVM)" && p.Params[2].CleanValue == "Vulkan");
        Check("parser: raw value keeps the comment", p.Params[0].RawValue.Contains("<!--", StringComparison.Ordinal));
        Check("parser: piped link and nested template stay inside one value", p.Params[1].CleanValue == "See [[Link|text]] and {{issue|123}}");
        var dup = WikiText.ParseConfig("{{config|renderer=Vulkan|Renderer=OpenGL}}");
        Check("parser: duplicate keys are both kept (no first/last pick)", dup.Params.Count == 2 && dup.Params.All(x => x.Key == "renderer"));
        Check("parser: two config templates are an issue", WikiText.ParseConfig("{{config|a=1}} {{Config|b=2}}").Issues.Contains("MULTIPLE_CONFIG_TEMPLATES"));
        Check("parser: unterminated template is an issue", WikiText.ParseConfig("{{config|a=1").Issues.Contains("UNTERMINATED_CONFIG_TEMPLATE"));
        var swallowed = WikiText.ParseConfig("{{vginfobox|a=1\n==Configuration==\n{{config|renderer=Vulkan}}");
        Check("parser: an unmatched earlier '{{' does not swallow the Config template", swallowed.TemplateCount == 1 && swallowed.Params.Count == 1 && swallowed.Params[0].CleanValue == "Vulkan");
        var inline = WikiText.ParseConfig("{{config\n|shader mode notes = |write color buffers = On\n|next = x\n}}");
        Check("parser: inline pipe splits like MediaWiki and is flagged", inline.Params.Count == 3 && inline.Params[1].Key == "write color buffers" && inline.Params[1].CleanValue == "On" && inline.Params[1].Inline && !inline.Params[2].Inline);
        Check("parser: config/doc is not a config template", WikiText.ParseConfig("{{config/doc|a=1}}").TemplateCount == 0);
        var schema = WikiText.ParseSchema("<includeonly>{{#if:\n<!-- CPU configuration -->\n{{{ppu decoder|}}}\n<!-- Recommended patches -->\n{{{recommended patch|}}}\n|body {{{ppu decoder notes|}}}}}</includeonly>");
        Check("schema: sections and notes", schema.Any(s => s.Key == "ppu decoder" && s.Section == "CPU configuration") &&
            schema.Any(s => s.Key == "recommended patch" && s.Section == "Recommended patches") && schema.Any(s => s.Key == "ppu decoder notes" && s.Section is null));
    }

    private static SchemaModel Schema(params (string Key, long From, long? To)[] intervals)
    {
        var model = new SchemaModel();
        foreach (var (key, from, to) in intervals)
        {
            if (!model.Intervals.TryGetValue(key, out var list)) model.Intervals[key] = list = [];
            list.Add(new SchemaInterval { Key = key, Start = At(from), End = to is null ? null : At(to.Value) });
            model.Section[key] = "GPU configuration";
        }

        return model;
    }

    private static Pt At(long n) => new($"2020-01-{n:D2}T00:00:00Z", n);

    private static PageInterval Page(string game, string key, string value, long from, long? to) => new()
    {
        Game = game, Key = key, Kind = "VALUE", Value = value, Start = At(from), End = to is null ? null : At(to.Value),
        EndBoundary = to is null ? "OPEN_AT_CAPTURE" : "VALUE_REMOVED", StartFrom = "PAGE_VALUE_START"
    };

    private static void Intervals()
    {
        var schema = Schema(("gap key", 2, 10), ("gap key", 12, null), ("late key", 5, 20));
        var b = new EffectiveBuilder(schema);
        b.BuildEffective(
        [
            Page("g", "gap key", "On", 1, 15),       // starts before activation, cut by the gap, resumes
            Page("g", "gap key", "Off", 20, null),    // fully active
            Page("g", "late key", "On", 1, 3),        // ends before first activation: no evidence
            Page("g", "late key", "On", 25, null),    // after final retirement: no evidence
            Page("g", "never key", "On", 1, null)     // unschematized
        ]);
        var pieces = b.Effective.Where(e => e.SourceKey == "gap key" && e.Page.Value == "On").OrderBy(e => e.EffStart).ToList();
        Check("effective: value split by a temporary schema gap", pieces.Count == 2 && pieces[0].StartBoundary == "SCHEMA_ACTIVATION" && pieces[0].EndBoundary == "SCHEMA_DEACTIVATION" &&
            pieces[1].StartBoundary == "SCHEMA_ACTIVATION" && pieces[1].EndBoundary == "VALUE_REMOVED" && pieces[0].Continuity.Contains("STARTED_BEFORE_FIRST", StringComparison.Ordinal) &&
            pieces[1].Continuity.Contains("RESUMED_AFTER_TEMPORARY_SCHEMA_GAP", StringComparison.Ordinal));
        Check("effective: open value stays open at capture", b.Effective.Any(e => e.Page.Value == "Off" && e.EndBoundary == "OPEN_AT_CAPTURE" && e.StartBoundary == "PAGE_VALUE_START"));
        Check("effective: before-first and after-final have no effective interval",
            b.NonEffectiveList.Count(n => n.SourceKey == "late key" && n.Reason == "BEFORE_FIRST_SCHEMA_ACTIVATION") == 1 &&
            b.NonEffectiveList.Count(n => n.SourceKey == "late key" && n.Reason == "AFTER_FINAL_SCHEMA_RETIREMENT") == 1);
        Check("effective: unschematized key is never effective", b.NonEffectiveList.Any(n => n.SourceKey == "never key" && n.Reason == "UNSCHEMATIZED") && b.Effective.All(e => e.SourceKey != "never key"));
        Check("schema status: literal classification",
            EffectiveBuilder.SchemaStatus(schema.IntervalsOf("gap key"), At(1)) == "BEFORE_FIRST_SCHEMA_ACTIVATION" &&
            EffectiveBuilder.SchemaStatus(schema.IntervalsOf("gap key"), At(11)) == "TEMPORARY_SCHEMA_GAP" &&
            EffectiveBuilder.SchemaStatus(schema.IntervalsOf("late key"), At(21)) == "AFTER_FINAL_SCHEMA_RETIREMENT");
    }

    private static PageData SyntheticPage(params string[] configBodies)
    {
        var page = new PageData { Title = "Synthetic", Ns = 0, PageId = 1, SourceFile = "synthetic.xml" };
        for (var i = 0; i < configBodies.Length; i++)
        {
            var id = 100 + i;
            page.Revs.Add(new Rev(id, i == 0 ? null : id - 1, $"2024-01-{i + 1:D2}T00:00:00Z", "tester", "c" + i, configBodies[i], false));
        }

        return page;
    }

    private static string Cfg(params string[] lines) => "{{config\n" + string.Join("\n", lines.Select(l => "|" + l)) + "\n}}";

    private static (bool Ok, string Detail) BarrierTests()
    {
        var failures = new List<string>();

        // Test A: renderer duplicated, write color buffers unique.
        var a = new GameAnalyzer();
        a.Process(SyntheticPage(Cfg("renderer = Vulkan", "renderer = OpenGL", "write color buffers = On")));
        var aWcb = a.Intervals.Where(i => i.Key == "write color buffers").ToList();
        var aRenderer = a.Intervals.Where(i => i.Key == "renderer").ToList();
        if (!(aWcb.Count == 1 && aWcb[0].Value == "On" && aWcb[0].EndBoundary == "OPEN_AT_CAPTURE" && !a.Barriers.Any(b => b.Key == "write color buffers")))
            failures.Add("A: unrelated key must stay valid");
        if (!(aRenderer.Count == 0 && a.Barriers.Count(b => b.Key == "renderer" && b.BarrierClass == "KEY_LOCAL_AMBIGUITY") == 1 &&
              a.Barriers.Single(b => b.Key == "renderer").ObservedValues == "OpenGL || Vulkan"))
            failures.Add("A: duplicated key must be ambiguous and produce no value (no first/last pick)");

        // Test B: On, then duplicate On/Off, then Off.
        var b = new GameAnalyzer();
        b.Process(SyntheticPage(Cfg("renderer = On"), Cfg("renderer = On", "renderer = Off"), Cfg("renderer = Off")));
        var bi = b.Intervals.Where(i => i.Key == "renderer").OrderBy(i => i.Start).ToList();
        if (!(bi.Count == 2 && bi[0].Value == "On" && bi[0].End?.Rev == 101 && bi[0].EndBoundary == "AMBIGUOUS_DUPLICATE" &&
              bi[1].Value == "Off" && bi[1].Start.Rev == 102 && bi[1].StartAfterGap && bi[1].EndBoundary == "OPEN_AT_CAPTURE"))
            failures.Add("B: interval must end at the duplicate revision and a new one start at the first valid revision after it");
        if (b.Events.Any(e => e.Key == "renderer" && e.Status == "CLEAN" && e.At.Rev == 101 || e.Key == "renderer" && e.Status == "CLEAN" && e.At.Rev == 102 && e.Type != "VALUE_ADDED"))
            failures.Add("B: no transition may be inferred across the duplicate revision");

        // Test C: duplicate in the notes key only.
        var c = new GameAnalyzer();
        c.Process(SyntheticPage(Cfg("renderer = Vulkan", "renderer notes = a", "renderer notes = b")));
        if (!(c.Intervals.Count(i => i.Key == "renderer") == 1 && !c.Barriers.Any(x => x.Key == "renderer") &&
              c.Barriers.Any(x => x.Key == "renderer notes" && x.Kind == "NOTE") && c.Intervals.Single(i => i.Key == "renderer").NoteState == "AMBIGUOUS"))
            failures.Add("C: a notes duplicate must not drop the setting and must not lend a note to it");

        // Test D: multiple config templates -> revision-wide structural barrier.
        var d = new GameAnalyzer();
        d.Process(SyntheticPage(Cfg("renderer = Vulkan", "write color buffers = On"), Cfg("renderer = Vulkan") + "\n" + Cfg("write color buffers = On"), Cfg("renderer = Vulkan", "write color buffers = On")));
        var dBarriers = d.Barriers.Where(x => x.BarrierClass == "REVISION_STRUCTURAL_AMBIGUITY").Select(x => x.Key).Order(StringComparer.Ordinal).ToList();
        var dIntervals = d.Intervals.Where(i => i.EndBoundary == "AMBIGUOUS_STRUCTURAL_REVISION").Select(i => i.Key).Order(StringComparer.Ordinal).ToList();
        if (!(dBarriers.SequenceEqual(["renderer", "write color buffers"]) && dIntervals.SequenceEqual(["renderer", "write color buffers"]) &&
              d.Intervals.Count(i => i.StartAfterGap) == 2))
            failures.Add("D: every key must be unknown in a structurally ambiguous revision");

        // Test E: a well-formed revision without a Config template is a deterministic absence.
        var e = new GameAnalyzer();
        e.Process(SyntheticPage(Cfg("renderer = Vulkan"), "plain page text, no template here", Cfg("renderer = OpenGL")));
        var ei = e.Intervals.Where(i => i.Key == "renderer").OrderBy(i => i.Start).ToList();
        if (!(ei.Count == 2 && ei[0].Value == "Vulkan" && ei[0].End?.Rev == 101 && ei[0].EndBoundary == "CONFIG_TEMPLATE_REMOVED" &&
              ei[1].Value == "OpenGL" && ei[1].Start.Rev == 102 && !ei[1].StartAfterGap && ei[1].EndBoundary == "OPEN_AT_CAPTURE"))
            failures.Add("E: Vulkan must end at the template-less revision (CONFIG_TEMPLATE_REMOVED) and OpenGL start fresh at revision 3");
        if (e.Barriers.Count != 0 || e.Stats.StructuralBarriers != 0)
            failures.Add("E: a deterministic absence must not create any ambiguity barrier");
        if (!e.Warnings.Any(w => w.Code == "NO_CONFIG_TEMPLATE_AFTER_CONFIG" && w.Severity == "INFO"))
            failures.Add("E: the absence must be kept as an INFO provenance warning");
        if (e.Events.Count(x => x.Status == "ABSENCE") != 2)
            failures.Add("E: expected one absence removal and one restoration event");

        return (failures.Count == 0, string.Join("; ", failures));
    }

    private static bool RemovalTests()
    {
        RemovalInput Ev(string game, string key, string canon, string type, long rev, string date, string who, string comment, string detail = "", string status = "CLEAN") =>
            new(game, key, canon, type, status, rev, date + "T10:00:00Z", who, comment, "", "", detail);
        RemovalInput St(string game, string key, long rev, string literal, string family) =>
            new(game, key, key, "VALUE_REMOVED", "CLEAN", rev, "2025-01-01T00:00:00Z", "", "", literal, family, "");

        var events = new List<RemovalInput>
        {
            Ev("A", "write depth buffers", "write depth buffers", "VALUE_REMOVED", 10, "2026-02-26", "u1", "rename"),
            Ev("A", "write depth buffer", "write depth buffers", "VALUE_ADDED", 10, "2026-02-26", "u1", "rename"),
            Ev("B", "enable buffering", "enable buffering", "VALUE_REMOVED", 20, "2026-09-15", "u2", "audio issue has been fixed"),
            Ev("C", "renderer", "renderer", "VALUE_REMOVED", 30, "2020-10-02", "RPCS3 Bot", "Text replacement"),
            Ev("D", "x", "x", "VALUE_REMOVED", 40, "2024-01-01", "u3", "no longer required"),
            Ev("E", "tsx instructions", "tsx instructions", "VALUE_REMOVED", 50, "2025-11-21", "u4", "cleanup"),
            Ev("F", "y", "y", "VALUE_REMOVED", 60, "2024-05-05", "u5", "cleanup"),
            Ev("G", "z", "z", "VALUE_REMOVED", 70, "2024-05-06", "u6", "", "CONFIG_TEMPLATE_REMOVED", "ABSENCE"),
            Ev("H", "old name", "new name", "VALUE_REMOVED", 80, "2019-05-01", "u7", "fix-up"),
        };
        for (var g = 0; g < 10; g++) events.Add(Ev("bulk" + g, "vsync", "vsync", "VALUE_REMOVED", 100 + g, "2023-03-03", "u8", "drop default"));
        var schema = new List<RemovalInput> { St("E", "tsx instructions", 50, "TEMPORARY_SCHEMA_GAP", "TEMPORARY_SCHEMA_GAP"), St("H", "old name", 80, "AFTER_FINAL_SCHEMA_RETIREMENT", "SCHEMA_ACTIVE") };

        var rows = Removals.Classify(events, schema).ToDictionary(r => r.Game, r => r);
        return rows["A"].RemovalReason == "FieldRenamed" &&
               rows["B"].RemovalReason == "ClaimedFixed" && rows["B"].Verification == "CLAIMED_ONLY" &&
               rows["C"].RemovalReason == "BulkCleanup" &&
               rows["D"].RemovalReason == "Unknown" && rows["D"].Basis == "CLAIMED_NO_LONGER_REQUIRED_CAUSE_NOT_STATED" &&
               rows["E"].RemovalReason == "SettingRemoved" &&
               rows["F"].RemovalReason == "Unknown" && rows["F"].Basis == "NO_EVIDENCE_OF_CAUSE" &&
               rows["G"].RemovalReason == "Unknown" && rows["G"].Basis == "CONFIG_TEMPLATE_ABSENT" &&
               rows["H"].RemovalReason == "FieldRenamed" &&
               rows["bulk3"].RemovalReason == "BulkCleanup" && rows["bulk3"].BulkGroupGames == 10 &&
               rows.Values.All(r => r.RemovalReason != "VerifiedFixedUpstream" && r.Verification != "VERIFIED");
    }

    public static bool BarrierTestsPass() => BarrierTests().Ok;

    public static bool ConflictDetectionWorks()
    {
        var schema = Schema(("old name", 1, 30), ("new name", 1, null));
        var b = new EffectiveBuilder(schema);
        b.Checks.Add(new DeclarationCheck { Verified = true, Declaration = new Declaration("old name", "new name", 1, "SAME_REVISION_SWAP", "test") });
        b.BuildEffective(
        [
            Page("g", "old name", "On", 5, 20),
            Page("g", "new name", "Off", 10, 25),    // overlaps with a different value
            Page("h", "old name", "On", 5, 20),
            Page("h", "new name", "On", 10, 25),      // overlaps with the same value: no conflict
            Page("i", "old name", "On", 5, 8),
            Page("i", "new name", "Off", 10, 25)      // no overlap
        ]);
        b.DetectConflicts();
        return b.Conflicts.Count == 1 && b.Conflicts[0].Game == "g" && b.Conflicts[0].Canonical == "new name" &&
               b.Effective.Count(e => e.ConflictIds.Length > 0) == 2;
    }
}
