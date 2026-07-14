using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

/// <summary>
/// Compatibility facade kept for existing callers.  All decisions are owned
/// by <see cref="PatCmp"/>.
/// </summary>
public sealed class MatchSvc(
    PatCmp? compatibility = null)
{
    private readonly PatCmp _Cmp =
        compatibility ?? new PatCmp();

    public IReadOnlyList<Mdl.PatMatch> Match(
        PatCat catalog,
        IEnumerable<R3Game> games)
    {
        return _Cmp.Evaluate(catalog, games);
    }

    public Mdl.PatMatch Match(
        PatEntry patch,
        IEnumerable<R3Game> games)
    {
        return _Cmp.Evaluate(patch, games);
    }
}

#pragma warning restore IDE0130
