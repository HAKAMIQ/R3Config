namespace HistWiki;

public sealed class GameIdEvent
{
    public string Game = "", Change = "", Previous = "", Current = "", Ids = "", Oids = "", Contributor = "", Comment = "", SourceFile = "";
    public Pt At;
}

public sealed class GameStats
{
    public int Pages, Revisions, ConfigRevisions, NoConfigRevisions, MultiConfigRevisions, UnterminatedRevisions;
    public int OrderAnomalies, ParentChainBreaks, TextMissing;
    public int NoConfigAfterConfig, DuplicateRevisions, AbsenceRemovedEvents;
    public int PairsValue, PairsNote;
    public int PairsKeyLocalBarrier, PairsStructuralBarrier, PairsAnyBarrier;
    public int KeyLocalBarriers, StructuralBarriers;
    public int OldQuarantinedValue, OldQuarantinedNote, RecoveredValue, RecoveredNote;
    public int CleanValueEvents, CleanNoteEvents;
    public int ConfigTextWithoutTemplate, ConfigLinesWithoutTemplate;
}

// Per-game processing: revision snapshots -> per-key state timelines -> events
// -> state intervals. A duplicated key is a per-key state barrier: that key is
// unknown in that revision only (no first/last pick, no merge, no carried
// value), the open interval closes there, and the timeline resumes from the
// first later revision in which the key is structurally valid. Only structural
// failures of the Config template itself make every key unknown in a revision.
public sealed class GameAnalyzer
{
    public readonly GameStats Stats = new();
    public readonly List<KeyEvent> Events = [];
    public readonly List<PageInterval> Intervals = [];
    public readonly List<Warning> Warnings = [];
    public readonly List<Barrier> Barriers = [];
    public readonly List<GameIdEvent> GameIds = [];
    public readonly Dictionary<string, (long Rev, string Sha)> Latest = new(StringComparer.Ordinal);
    public readonly Dictionary<string, (int Count, Pt First, Pt Last, int Ns)> Meta = new(StringComparer.Ordinal);
    public readonly List<string> DuplicateTitles = [];
    // Transition accounting: "V"/"N" (settings/notes) x RAW, KEYLOCAL, STRUCTURAL, ABSENCE, CLEAN, RECOVERY.
    public readonly Dictionary<string, int> Acct = new(StringComparer.Ordinal);
    private void Count(string key) => Acct[key] = Acct.GetValueOrDefault(key) + 1;
    // (Game, revision, key) parameters written on the same line as a previous parameter.
    public readonly HashSet<(string Game, long Rev, string Key)> InlineKeys = [];
    // (Game, revision, key) whose value holds a comment that runs across lines.
    public readonly HashSet<(string Game, long Rev, string Key)> CommentSpanKeys = [];

    private static readonly System.Text.RegularExpressions.Regex ConfigWord = new(@"\{\{\s*config\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex ConfigLine = new(@"^\s*\|\s*[a-z0-9 .-]+ notes\s*=",System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly HashSet<string> FatalIssues =
        new(StringComparer.Ordinal) { "UNTERMINATED_COMMENT", "UNTERMINATED_CONFIG_TEMPLATE", "MULTIPLE_CONFIG_TEMPLATES" };

    public void Process(PageData page)
    {
        Stats.Pages++;
        var game = page.Title;
        if (Meta.ContainsKey(game))
        {
            DuplicateTitles.Add(game);
            throw new InvalidDataException($"Duplicate game title '{game}' across history files; refusing to merge timelines.");
        }

        var revs = page.Revs.OrderBy(r => new Pt(r.Timestamp, r.Id)).ToList();
        Stats.Revisions += revs.Count;

        if (!revs.Select(r => r.Id).SequenceEqual(page.Revs.OrderBy(r => r.Id).Select(r => r.Id)))
        {
            Stats.OrderAnomalies++;
            Warn("INFO", "REVISION_ORDER_ANOMALY", game, "", revs[0], "Timestamp order differs from revision-id order.");
        }

        for (var i = 1; i < revs.Count; i++)
        {
            if (revs[i].ParentId != revs[i - 1].Id)
            {
                Stats.ParentChainBreaks++;
                Warn("INFO", "PARENT_CHAIN_BREAK", game, "", revs[i], $"Parent {revs[i].ParentId?.ToString() ?? "none"} is not the previous revision {revs[i - 1].Id}.");
            }
        }

        var parses = new ConfigParse?[revs.Count];
        var fatal = new string?[revs.Count];
        var absent = new bool[revs.Count];
        var absentAfterConfig = new bool[revs.Count];
        var firstConfig = -1;
        for (var i = 0; i < revs.Count; i++)
        {
            var rev = revs[i];
            if (rev.TextMissing)
            {
                Stats.TextMissing++;
                fatal[i] = "TEXT_DELETED";
                continue;
            }

            var parse = WikiText.ParseConfig(rev.Text);
            parses[i] = parse;
            foreach (var spanning in parse.Params.Where(p => p.CommentSpansLines)) CommentSpanKeys.Add((game, rev.Id, spanning.Key));
            for (var pi = 0; pi < parse.Params.Count; pi++)
            {
                if (!parse.Params[pi].Inline) continue;
                InlineKeys.Add((game, rev.Id, parse.Params[pi].Key));
                if (pi > 0) InlineKeys.Add((game, rev.Id, parse.Params[pi - 1].Key));
            }

            if (parse.TemplateCount == 0 && ConfigLine.IsMatch(rev.Text))
            {
                Stats.ConfigLinesWithoutTemplate++;
                Warn("INFO", "CONFIG_LIKE_LINES_WITHOUT_TEMPLATE", game, "", rev, "Lines that look like Config parameters exist outside any {{config}} template.");
            }

            if (parse.TemplateCount == 0 && ConfigWord.IsMatch(rev.Text))
            {
                Stats.ConfigTextWithoutTemplate++;
                Warn("INFO", "CONFIG_TEXT_WITHOUT_TEMPLATE", game, "", rev, "The text mentions a config template start that is inside a comment/nowiki or otherwise not a template call.");
            }
            if (parse.TemplateCount >= 1)
            {
                Stats.ConfigRevisions++;
                if (firstConfig < 0) firstConfig = i;
            }
            else
            {
                Stats.NoConfigRevisions++;
            }

            foreach (var issue in parse.Issues.Distinct(StringComparer.Ordinal))
            {
                if (FatalIssues.Contains(issue))
                {
                    fatal[i] = issue;
                    if (issue == "MULTIPLE_CONFIG_TEMPLATES") Stats.MultiConfigRevisions++;
                    else Stats.UnterminatedRevisions++;
                    Warn("QUARANTINE", issue, game, "", rev, "Revision state of every Config key is unknown.");
                }
                else
                {
                    Warn("INFO", issue, game, "", rev, "Config parameter ignored by the parser.");
                }
            }

            // A well-formed revision with no Config template is a deterministic absence:
            // every field is absent in it. It is provenance, not ambiguity.
            if (parse.TemplateCount == 0 && fatal[i] is null)
            {
                absent[i] = true;
                if (firstConfig >= 0)
                {
                    absentAfterConfig[i] = true;
                    Stats.NoConfigAfterConfig++;
                    Warn("INFO", "NO_CONFIG_TEMPLATE_AFTER_CONFIG", game, "", rev, "CONFIG_TEMPLATE_ABSENT: page had a Config template earlier; every field is absent from this revision.");
                }
            }

        }

        var dupRev = new bool[revs.Count];
        for (var i = 0; i < revs.Count; i++)
        {
            if (parses[i] is null || fatal[i] is not null) continue;
            var duplicated = parses[i]!.Params.Where(p => !p.Positional).GroupBy(p => p.Key, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).Order(StringComparer.Ordinal).ToList();
            dupRev[i] = duplicated.Count > 0;
            if (!dupRev[i]) continue;
            Stats.DuplicateRevisions++;
            foreach (var dupKey in duplicated)
                Warn("QUARANTINE", "DUPLICATE_CONFIG_KEY", game, dupKey, revs[i], "KEY_LOCAL_AMBIGUITY: this key is unknown in this revision; other keys are unaffected.");
        }

        var keys = parses
            .Where(p => p is not null)
            .SelectMany(p => p!.Params.Where(x => !x.Positional).Select(x => x.Key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var meta = revs.Select(r => new RevMeta(new Pt(r.Timestamp, r.Id), r.Contributor, r.Comment)).ToList();
        var timelines = new Dictionary<string, KeyState[]>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var states = new KeyState[revs.Count];
            for (var i = 0; i < revs.Count; i++)
            {
                if (fatal[i] is not null) { states[i] = KeyState.Unk(fatal[i]!, null, structural: true); continue; }
                var occurrences = parses[i]!.Params.Where(p => !p.Positional && p.Key == key).ToList();
                states[i] = occurrences.Count switch
                {
                    0 => KeyState.Missing,
                    1 => occurrences[0].CleanValue.Length == 0 ? KeyState.Empty : KeyState.Val(occurrences[0].CleanValue, occurrences[0].RawValue),
                    _ => KeyState.Unk("DUPLICATE_CONFIG_KEY_IN_REVISION", occurrences.Select(o => o.CleanValue).ToList())
                };
            }

            timelines[key] = states;
        }

        foreach (var key in keys)
        {
            var kind = key.EndsWith(" notes", StringComparison.Ordinal) ? "NOTE" : "VALUE";
            var states = timelines[key];
            if (!states.Any(s => s.S == St.Value || Observed(s))) continue;

            if (kind == "VALUE") Stats.PairsValue++; else Stats.PairsNote++;

            // The earlier strict policy (any duplicate anywhere in a revision quarantines
            // every field through it) is evaluated only to report what this policy recovers.
            var oldQuarantined = Enumerable.Range(0, states.Length).Any(i => fatal[i] is not null || dupRev[i] || absentAfterConfig[i]);
            var anyBarrier = states.Any(s => s.S == St.Unknown);
            if (oldQuarantined)
            {
                if (kind == "VALUE") Stats.OldQuarantinedValue++; else Stats.OldQuarantinedNote++;
                if (!anyBarrier)
                {
                    if (kind == "VALUE") Stats.RecoveredValue++; else Stats.RecoveredNote++;
                }
            }

            Account(kind, states, absentAfterConfig);
            var events = WalkTimeline(game, key, kind, states, meta, page.SourceFile, absentAfterConfig);
            Events.AddRange(events);
            BuildIntervals(game, key, kind, events, timelines, revs, page.SourceFile);
        }

        var lastRev = revs[^1];
        Latest[game] = (lastRev.Id, Sha(lastRev.Text));
        Meta[game] = (revs.Count, meta[0].At, meta[^1].At, page.Ns);

        GameIdTimeline(game, revs, page.SourceFile);
    }

    private static bool Observed(KeyState s) =>
        s.S == St.Unknown && s.DupValues is not null && s.DupValues.Any(v => v.Length > 0);

    public const string BoundaryKeyLocal = "AMBIGUOUS_DUPLICATE";
    public const string BoundaryStructural = "AMBIGUOUS_STRUCTURAL_REVISION";

    // Independent transition accounting. RAW is the count of value transitions a naive
    // reader of the same MediaWiki-correct parse would see (duplicate key: last
    // occurrence; structurally ambiguous revision: everything absent). Every RAW
    // transition is assigned to exactly one bucket by the kind of revision on each side.
    private void Account(string kind, KeyState[] states, bool[] absent)
    {
        var p = kind == "VALUE" ? "V" : "N";
        var prevNaive = KeyState.Missing;
        for (var i = 0; i < states.Length; i++)
        {
            var s = states[i];
            KeyState naive = s.S != St.Unknown
                ? s
                : s.Structural
                    ? KeyState.Missing
                    : s.DupValues is { Count: > 0 } dv && dv[^1].Length > 0 ? KeyState.Val(dv[^1], dv[^1]) : KeyState.Empty;
            if (Classify(prevNaive, naive) is not null)
            {
                Count(p + ".RAW");
                var prevState = i > 0 ? states[i - 1] : KeyState.Missing;
                var keyLocal = (s.S == St.Unknown && !s.Structural) || (prevState.S == St.Unknown && !prevState.Structural);
                var structural = (s.S == St.Unknown && s.Structural) || (prevState.S == St.Unknown && prevState.Structural);
                var absence = absent[i] || (i > 0 && absent[i - 1]);
                Count(p + (keyLocal ? ".KEYLOCAL" : structural ? ".STRUCTURAL" : absence ? ".ABSENCE" : ".CLEAN"));
            }

            prevNaive = naive;
        }
    }

    // Known -> known transitions are ordinary events. A run of unknown revisions is
    // a barrier: one AMBIGUOUS_BARRIER event where it starts (closing any open
    // value) and, when the key holds a value at the first valid revision after it,
    // one VALUE_REESTABLISHED event. Nothing is inferred across the barrier.
    private List<KeyEvent> WalkTimeline(string game, string key, string kind, KeyState[] states, List<RevMeta> meta, string file, bool[] absent)
    {
        var events = new List<KeyEvent>();
        var prev = KeyState.Missing;
        var prevKnown = true;
        var prevRev = 0L;
        var pairKeyLocal = false;
        var pairStructural = false;
        var i = 0;
        while (i < states.Length)
        {
            if (states[i].S != St.Unknown)
            {
                if (prevKnown)
                {
                    var type = Classify(prev, states[i]);
                    if (type is not null)
                    {
                        var absenceLinked = absent[i] || (i > 0 && absent[i - 1]);
                        events.Add(new KeyEvent
                        {
                            Game = game, Key = key, Kind = kind, Type = type, Status = absenceLinked ? "ABSENCE" : "CLEAN",
                            Detail = absent[i] ? "CONFIG_TEMPLATE_REMOVED" : absenceLinked ? "CONFIG_TEMPLATE_RESTORED" : "",
                            PrevState = prev.S.ToString().ToUpperInvariant(), PrevValue = prev.Value, PrevRaw = prev.Raw,
                            NewState = states[i].S.ToString().ToUpperInvariant(), NewValue = states[i].Value, NewRaw = states[i].Raw,
                            At = meta[i].At, PrevRevision = prevRev, Contributor = meta[i].Contributor, Comment = meta[i].Comment, SourceFile = file
                        });
                        if (!absenceLinked) { if (kind == "VALUE") Stats.CleanValueEvents++; else Stats.CleanNoteEvents++; }
                        else if (absent[i] && kind == "VALUE") Stats.AbsenceRemovedEvents++;
                    }
                }

                prev = states[i];
                prevKnown = true;
                prevRev = meta[i].At.Rev;
                i++;
                continue;
            }

            var j = i;
            while (j < states.Length && states[j].S == St.Unknown) j++;
            var structural = Enumerable.Range(i, j - i).Any(k => states[k].Structural);
            var boundary = structural ? BoundaryStructural : BoundaryKeyLocal;
            var observed = Enumerable.Range(i, j - i).Where(k => states[k].DupValues is not null).SelectMany(k => states[k].DupValues!).Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var resume = j < states.Length ? states[j] : null;
            if ((prevKnown && prev.S == St.Value) || observed.Count > 0 || resume?.S == St.Value)
            {
                Barriers.Add(new Barrier
                {
                    Game = game, Key = key, Kind = kind,
                    BarrierClass = structural ? "REVISION_STRUCTURAL_AMBIGUITY" : "KEY_LOCAL_AMBIGUITY",
                    Reasons = string.Join("|", Enumerable.Range(i, j - i).Select(k => states[k].UnknownReason).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                    GapRevisions = string.Join(";", Enumerable.Range(i, j - i).Select(k => meta[k].At.Rev)),
                    ObservedValues = string.Join(" || ", observed),
                    From = meta[i].At,
                    Resume = j < states.Length ? meta[j].At : null,
                    ClosedValue = prevKnown && prev.S == St.Value ? prev.Value : "",
                    ResumedValue = resume?.S == St.Value ? resume.Value : ""
                });
                if (structural) { Stats.StructuralBarriers++; pairStructural = true; } else { Stats.KeyLocalBarriers++; pairKeyLocal = true; }
                events.Add(new KeyEvent
                {
                    Game = game, Key = key, Kind = kind, Type = "AMBIGUOUS_BARRIER", Status = "AMBIGUOUS", Detail = boundary,
                    PrevState = prev.S.ToString().ToUpperInvariant(), PrevValue = prev.Value, PrevRaw = prev.Raw,
                    NewState = "UNKNOWN", NewValue = string.Join(" || ", observed),
                    At = meta[i].At, PrevRevision = prevRev, Contributor = meta[i].Contributor, Comment = meta[i].Comment, SourceFile = file
                });
                if (resume?.S == St.Value)
                {
                    Count((kind == "VALUE" ? "V" : "N") + ".RECOVERY");
                    events.Add(new KeyEvent
                    {
                        Game = game, Key = key, Kind = kind, Type = "VALUE_REESTABLISHED", Status = "REESTABLISHED", Detail = "AFTER_AMBIGUOUS_GAP",
                        PrevState = "UNKNOWN", NewState = "VALUE", NewValue = resume.Value, NewRaw = resume.Raw,
                        At = meta[j].At, PrevRevision = meta[j - 1].At.Rev, Contributor = meta[j].Contributor, Comment = meta[j].Comment, SourceFile = file
                    });
                }
            }

            prevKnown = false;
            prev = resume ?? KeyState.Missing;
            i = j;
        }

        if (pairKeyLocal) Stats.PairsKeyLocalBarrier++;
        if (pairStructural) Stats.PairsStructuralBarrier++;
        if (pairKeyLocal || pairStructural) Stats.PairsAnyBarrier++;
        return events;
    }

    private static string Sha(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    private void Warn(string severity, string code, string game, string key, Rev rev, string detail) =>
        Warnings.Add(new Warning { Severity = severity, Code = code, Game = game, Key = key, Ts = rev.Timestamp, Rev = rev.Id, Detail = detail });

    private void Warn(string severity, string code, string game, string key, RevMeta rev, string detail) =>
        Warnings.Add(new Warning { Severity = severity, Code = code, Game = game, Key = key, Ts = rev.At.Ts, Rev = rev.At.Rev, Detail = detail });

    // Event type of a transition between two known states, or null when the
    // transition is not an actual value transition (empty/missing churn).
    private static string? Classify(KeyState prev, KeyState cur)
    {
        var prevValue = prev.S == St.Value;
        var curValue = cur.S == St.Value;
        if (!prevValue && curValue) return "VALUE_ADDED";
        if (prevValue && !curValue) return "VALUE_REMOVED";
        if (prevValue && curValue && !string.Equals(prev.Value, cur.Value, StringComparison.Ordinal)) return "VALUE_CHANGED";
        return null;
    }

    // Replays the event stream into state intervals; any inconsistency between
    // the event stream and the open state is reported, never repaired.
    private void BuildIntervals(string game, string key, string kind, List<KeyEvent> events,
        Dictionary<string, KeyState[]> timelines, List<Rev> revs, string file)
    {
        PageInterval? open = null;
        foreach (var e in events)
        {
            var at = new RevMeta(e.At, e.Contributor, e.Comment);
            switch (e.Type)
            {
                case "VALUE_ADDED":
                    if (open is not null)
                    {
                        Warn("INFO", "VALUE_ADDED_WHILE_OPEN", game, key, at, "A value was added while another value was open.");
                        Close(open, e, "VALUE_CHANGED");
                    }

                    open = Open(game, key, kind, e, timelines, revs, file);
                    break;
                case "VALUE_REESTABLISHED":
                    if (open is not null)
                    {
                        Warn("INFO", "VALUE_ADDED_WHILE_OPEN", game, key, at, "A value was re-established while another value was open.");
                        Close(open, e, "VALUE_CHANGED");
                    }

                    open = Open(game, key, kind, e, timelines, revs, file);
                    open.StartAfterGap = true;
                    break;
                case "VALUE_CHANGED":
                    if (open is null) Warn("INFO", "VALUE_CHANGED_WITHOUT_OPEN_STATE", game, key, at, "A value change had no open value.");
                    else Close(open, e, "VALUE_CHANGED");
                    open = Open(game, key, kind, e, timelines, revs, file);
                    open.StartFrom = "VALUE_CHANGED";
                    break;
                case "VALUE_REMOVED":
                    if (open is null)
                    {
                        Warn("INFO", "VALUE_REMOVED_WITHOUT_OPEN_STATE", game, key, at, "A value removal had no open value.");
                    }
                    else
                    {
                        Close(open, e, e.Detail.Length > 0 ? e.Detail : "VALUE_REMOVED");
                        open = null;
                    }

                    break;
                case "AMBIGUOUS_BARRIER":
                    if (open is not null)
                    {
                        Close(open, e, e.Detail);
                        open = null;
                    }

                    break;
            }
        }

        if (open is not null)
        {
            open.EndBoundary = "OPEN_AT_CAPTURE";
        }
    }

    private void Close(PageInterval interval, KeyEvent e, string boundary)
    {
        interval.End = e.At;
        interval.EndBoundary = boundary;
        interval.EndContributor = e.Contributor;
        interval.EndComment = e.Comment;
    }

    private PageInterval Open(string game, string key, string kind, KeyEvent e,
        Dictionary<string, KeyState[]> timelines, List<Rev> revs, string file)
    {
        var interval = new PageInterval
        {
            Game = game, Key = key, Kind = kind, Value = e.NewValue, SourceFile = file,
            Start = e.At, StartContributor = e.Contributor, StartComment = e.Comment, StartFrom = "PAGE_VALUE_START"
        };
        if (kind == "VALUE" && timelines.TryGetValue(key + " notes", out var noteStates))
        {
            var index = revs.FindIndex(r => r.Id == e.At.Rev);
            var note = noteStates[index];
            interval.NoteState = note.S == St.Unknown ? "AMBIGUOUS" : note.S == St.Value ? "VALUE" : "EMPTY";
            interval.NoteAtStart = note.S == St.Value ? note.Value : "";
        }

        Intervals.Add(interval);
        return interval;
    }

    private void GameIdTimeline(string game, List<Rev> revs, string file)
    {
        string previous = "";
        foreach (var rev in revs)
        {
            if (rev.TextMissing) continue;
            var map = WikiText.ParseGameId(rev.Text);
            var current = map is null
                ? ""
                : string.Join(";", map.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value));
            if (current == previous) continue;
            GameIds.Add(new GameIdEvent
            {
                Game = game,
                Change = previous.Length == 0 ? "ADDED" : current.Length == 0 ? "REMOVED" : "CHANGED",
                Previous = previous,
                Current = current,
                Ids = map is not null && map.TryGetValue("id", out var ids) ? ids : "",
                Oids = map is not null && map.TryGetValue("oid", out var oids) ? oids : "",
                At = new Pt(rev.Timestamp, rev.Id),
                Contributor = rev.Contributor,
                Comment = rev.Comment,
                SourceFile = file
            });
            previous = current;
        }
    }
}
