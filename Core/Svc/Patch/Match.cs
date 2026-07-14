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
    private readonly PatchCompatibilityService _compatibility =
        compatibility ?? new PatchCompatibilityService();

    public IReadOnlyList<Rpcs3PatchMatchResult> Match(
        Rpcs3PatchCatalog catalog,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _compatibility.Evaluate(catalog, games);
    }

    public Rpcs3PatchMatchResult Match(
        Rpcs3PatchEntry patch,
        IEnumerable<Rpcs3GameDescriptor> games)
    {
        return _compatibility.Evaluate(patch, games);
    }
}

#pragma warning restore IDE0130
