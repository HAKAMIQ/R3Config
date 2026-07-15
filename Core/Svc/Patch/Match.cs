using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

/// <summary>
/// Compatibility facade kept for existing callers.  All decisions are owned
/// by <see cref="PatchCompatibilityService"/>.
/// </summary>
public sealed class PatchMatchService(
    PatchCompatibilityService? compatibility = null)
{
    private readonly PatchCompatibilityService _Cmp =
        compatibility ?? new PatchCompatibilityService();

    public IReadOnlyList<Mdl.PatMatch> Match(
        PatCat catalog,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _Cmp.Evaluate(catalog, games);
    }

    public Mdl.PatMatch Match(
        Rpcs3PatchEntry patch,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _Cmp.Evaluate(patch, games);
    }
}

#pragma warning restore IDE0130
