using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public static class PatAct
{
    private static readonly PatchCompatibilityService Cmp = new();

    public static bool Can(Mdl.PatMatch match)
    {
        return Cmp.Decide(match).CanOn;
    }

    public static bool Can2(Mdl.PatMatch match)
    {
        return Cmp.Decide(match).CanOff;
    }

    public static PatchCompatibilityDecision Decide(
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
