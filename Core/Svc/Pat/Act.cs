using System;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

/// <summary>
/// Mirrors RPCS3's two-stage patch safety model. R3Config resolves the exact
/// game target, while RPCS3 resolves the executable hash at boot before it can
/// apply the selected patch block.
/// </summary>
public static class PatAct
{
    private static readonly PatCmp Cmp = new();

    public static bool Can(Mdl.PatMatch match)
    {
        return Cmp.Decide(match).CanOn;
    }

    public static bool Can2(Mdl.PatMatch match)
    {
        return Cmp.Decide(match).CanOff;
    }

    public static CmpDec Decide(
        Mdl.PatMatch match)
    {
        return Cmp.Decide(match);
    }

    public static bool IsRun(Mdl.PatMatch match)
    {
        return Cmp
            .Decide(match)
            .UseGuard;
    }
}

#pragma warning restore IDE0130
