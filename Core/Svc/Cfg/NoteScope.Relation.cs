using System;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// The direction words around a version mention, as a structured relation. Only the fixed
// wordings NoteScope already recognises are understood; anything else is an exact match
// request, and a caller that needs more certainty must treat that as unresolved.
public static partial class NoteScope
{
    public static ScopeRelation GameVersionRelation(string text, string mention)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(mention);

        var index = text.IndexOf(mention, StringComparison.Ordinal);
        if (index < 0)
        {
            return ScopeRelation.Unresolved;
        }

        var prefix = index == 0 ? string.Empty : text[Math.Max(0, index - 24)..index];
        var suffix = text[(index + mention.Length)..Math.Min(text.Length, index + mention.Length + 24)];

        if (Newer.IsMatch(prefix))
        {
            return ScopeRelation.Above;
        }

        if (AtLeast.IsMatch(prefix) || TrailingUp.IsMatch(suffix))
        {
            return ScopeRelation.AtLeast;
        }

        if (Older.IsMatch(prefix))
        {
            return ScopeRelation.Below;
        }

        return TrailingDown.IsMatch(suffix) ? ScopeRelation.AtMost : ScopeRelation.Equal;
    }

    public static ScopeRelation Rpcs3Relation(string text, string mention)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(mention);

        var index = text.IndexOf(mention, StringComparison.Ordinal);
        if (index < 0)
        {
            return ScopeRelation.Unresolved;
        }

        var prefix = index == 0 ? string.Empty : text[Math.Max(0, index - 24)..index];
        if (Rpcs3Newer.IsMatch(prefix))
        {
            return ScopeRelation.Above;
        }

        if (Rpcs3AtLeast.IsMatch(prefix))
        {
            return ScopeRelation.AtLeast;
        }

        return Rpcs3Older.IsMatch(prefix) ? ScopeRelation.Below : ScopeRelation.Equal;
    }
}

#pragma warning restore IDE0130
