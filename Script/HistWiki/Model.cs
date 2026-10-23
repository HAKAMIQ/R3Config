namespace HistWiki;

// Ordering key for wiki revisions: ISO-8601 UTC timestamps sort chronologically
// as strings; the revision id breaks same-second ties. Null means "open".
public readonly record struct Pt(string Ts, long Rev) : IComparable<Pt>
{
    public int CompareTo(Pt other)
    {
        var c = string.CompareOrdinal(Ts, other.Ts);
        return c != 0 ? c : Rev.CompareTo(other.Rev);
    }

    public static bool operator <(Pt a, Pt b) => a.CompareTo(b) < 0;
    public static bool operator >(Pt a, Pt b) => a.CompareTo(b) > 0;
    public static bool operator <=(Pt a, Pt b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Pt a, Pt b) => a.CompareTo(b) >= 0;
}

public enum St { Missing, Empty, Value, Unknown }

public sealed record KeyState(St S, string Value, string UnknownReason, IReadOnlyList<string>? DupValues, string Raw = "", bool Structural = false)
{
    public static readonly KeyState Missing = new(St.Missing, "", "", null);
    public static readonly KeyState Empty = new(St.Empty, "", "", null);
    public static KeyState Val(string v, string raw) => new(St.Value, v, "", null, raw);
    public static KeyState Unk(string reason, IReadOnlyList<string>? dup = null, bool structural = false) => new(St.Unknown, "", reason, dup, "", structural);
}

public sealed record RevMeta(Pt At, string Contributor, string Comment);

public sealed class KeyEvent
{
    public string Game = "", Key = "", Kind = "", Type = "", Status = "CLEAN", Detail = "";
    public string PrevRaw = "", NewRaw = "";
    public string PrevState = "", PrevValue = "", NewState = "", NewValue = "", Contributor = "", Comment = "", SourceFile = "";
    public Pt At;
    public long PrevRevision;
}

public sealed class PageInterval
{
    public string Game = "", Key = "", Kind = "", Value = "", SourceFile = "";
    public string StartContributor = "", StartComment = "", EndContributor = "", EndComment = "";
    public string StartFrom = "";
    public bool StartAfterGap;
    public Pt Start;
    public Pt? End;
    public string EndBoundary = "";
    public string NoteAtStart = "", NoteState = "NONE";
}

public sealed class Warning
{
    public string Severity = "", Code = "", Game = "", Key = "", Detail = "";
    public string Ts = "";
    public long Rev;
}

// One maximal run of revisions in which the state of one Game+Field is unknown.
// KEY_LOCAL_AMBIGUITY: the field itself is duplicated in the revision.
// REVISION_STRUCTURAL_AMBIGUITY: the Config boundaries of the revision are not interpretable.
public sealed class Barrier
{
    public string Game = "", Key = "", Kind = "", BarrierClass = "", Reasons = "", GapRevisions = "", ObservedValues = "";
    public Pt From;
    public Pt? Resume;
    public string ClosedValue = "", ResumedValue = "";
}

public sealed record SchemaEvent(string Key, Pt At, string Type, string PrevName, string NewName, string Contributor, string Comment);

public sealed class SchemaInterval
{
    public string Key = "";
    public Pt Start;
    public Pt? End;
    public string Spellings = "";
    public string StartContributor = "", StartComment = "", EndContributor = "", EndComment = "";
}

public sealed class EffectiveInterval
{
    public PageInterval Page = null!;
    public string SourceKey = "", CanonicalKey = "", IdentityStatus = "", Semantic = "";
    public SchemaInterval Schema = null!;
    public Pt EffStart;
    public Pt? EffEnd;
    public string StartBoundary = "", EndBoundary = "", Continuity = "NONE";
    public string ConflictIds = "";
}

public sealed class NonEffective
{
    public PageInterval Page = null!;
    public string SourceKey = "", CanonicalKey = "", IdentityStatus = "", Semantic = "", Reason = "";
}

public sealed class Conflict
{
    public string Id = "", Game = "", Canonical = "", ExistingValue = "", NewValue = "";
    public string ExistingSource = "", NewSource = "";
    public Pt ExistingStart, NewStart;
    public Pt? ExistingEnd, NewEnd;
}

public sealed class Identity
{
    public string Source = "", Canonical = "", Status = "", EvidenceTs = "";
    public long EvidenceRev;
    public int Games, Transitions;
}
