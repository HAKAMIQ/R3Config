using System;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

/// <summary>
/// Mirrors RPCS3's two-stage patch safety model. R3Config resolves the exact
/// game target, while RPCS3 resolves the executable hash at boot before it can
/// apply the selected patch block.
/// </summary>
public static class PatchActivationPolicy
{
    private static readonly PatchCompatibilityService Compatibility = new();

    public static bool CanEnable(Rpcs3PatchMatchResult match)
    {
        return Compatibility.Decide(match).CanEnable;
    }

    public static bool CanDisable(Rpcs3PatchMatchResult match)
    {
        return Compatibility.Decide(match).CanDisable;
    }

    public static PatchCompatibilityDecision Decide(
        Rpcs3PatchMatchResult match)
    {
        return Compatibility.Decide(match);
    }

    public static bool IsRuntimeGuardedMatch(Rpcs3PatchMatchResult match)
    {
        return Compatibility
            .Decide(match)
            .UsesRpcs3RuntimeHashGuard;
    }
}

#pragma warning restore IDE0130
