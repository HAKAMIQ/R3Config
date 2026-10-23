namespace HistWiki;

// Template:Config parameter timeline. A parameter is identified by its
// lower-cased trimmed name; a case-only spelling change is recorded as an event
// but never breaks the parameter's continuous schema interval.
public sealed class SchemaModel
{
    public int Revisions;
    public Pt First, Last;
    public string PageTitle = "";
    public readonly List<SchemaEvent> Events = [];
    public readonly Dictionary<string, List<SchemaInterval>> Intervals = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Section = new(StringComparer.Ordinal);
    public readonly HashSet<string> CurrentKeys = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> LatestSpelling = new(StringComparer.Ordinal);
    public readonly List<(string Key, Pt At, string Contributor, string Comment)> Timeline = [];

    public static SchemaModel Build(IEnumerable<PageData> pages)
    {
        var model = new SchemaModel();
        var page = pages.SingleOrDefault(p => p.Title == "Template:Config")
            ?? throw new InvalidDataException("Template:Config page was not found in the schema history XML.");
        model.PageTitle = page.Title;
        var revs = page.Revs.OrderBy(r => new Pt(r.Timestamp, r.Id)).ToList();
        model.Revisions = revs.Count;
        model.First = new Pt(revs[0].Timestamp, revs[0].Id);
        model.Last = new Pt(revs[^1].Timestamp, revs[^1].Id);

        var active = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rev in revs)
        {
            if (rev.TextMissing)
            {
                throw new InvalidDataException($"Template:Config revision {rev.Id} has no text; the schema timeline cannot be trusted.");
            }

            var at = new Pt(rev.Timestamp, rev.Id);
            var refs = WikiText.ParseSchema(rev.Text);
            var now = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in refs.GroupBy(r => r.Key, StringComparer.Ordinal))
            {
                now[group.Key] = string.Join("|", group.Select(r => r.RawName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
                foreach (var r in group.Where(r => r.Section is not null))
                {
                    model.Section[group.Key] = r.Section!;
                }
            }

            foreach (var key in now.Keys.Order(StringComparer.Ordinal))
            {
                model.Timeline.Add((key, at, rev.Contributor, rev.Comment));
                if (!active.TryGetValue(key, out var previousName))
                {
                    model.Events.Add(new SchemaEvent(key, at, "PARAMETER_ADDED", "", now[key], rev.Contributor, rev.Comment));
                    if (!model.Intervals.TryGetValue(key, out var list)) model.Intervals[key] = list = [];
                    list.Add(new SchemaInterval { Key = key, Start = at, Spellings = now[key], StartContributor = rev.Contributor, StartComment = rev.Comment });
                }
                else if (!string.Equals(previousName, now[key], StringComparison.Ordinal))
                {
                    model.Events.Add(new SchemaEvent(key, at, "SPELLING_CHANGED", previousName, now[key], rev.Contributor, rev.Comment));
                    var open = model.Intervals[key][^1];
                    open.Spellings = string.Join("|", open.Spellings.Split('|').Concat(now[key].Split('|')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
                }
            }

            foreach (var key in active.Keys.Except(now.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                model.Events.Add(new SchemaEvent(key, at, "PARAMETER_REMOVED", active[key], "", rev.Contributor, rev.Comment));
                var open = model.Intervals[key][^1];
                open.End = at;
                open.EndContributor = rev.Contributor;
                open.EndComment = rev.Comment;
            }

            active = now;
        }

        foreach (var key in active.Keys)
        {
            model.CurrentKeys.Add(key);
            model.LatestSpelling[key] = active[key];
        }

        return model;
    }

    public IReadOnlyList<SchemaInterval> IntervalsOf(string key) =>
        Intervals.TryGetValue(key, out var list) ? list : [];

    public bool IsScheme(string key) => Intervals.ContainsKey(key);

    // SETTING/PATCH_METADATA is proven only by the section the parameter sits
    // under in Template:Config itself. Anything else is UNKNOWN (fail closed).
    public string Semantic(string key)
    {
        if (key.EndsWith(" notes", StringComparison.Ordinal)) return "NOTES";
        if (!Section.TryGetValue(key, out var section)) return "UNKNOWN";
        if (section.EndsWith(" configuration", StringComparison.OrdinalIgnoreCase)) return "SETTING";
        if (string.Equals(section, "Recommended patches", StringComparison.OrdinalIgnoreCase)) return "PATCH_METADATA";
        return "UNKNOWN";
    }
}
