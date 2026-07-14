using R3Config.Comp;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Case;

namespace R3Config;

/// <summary>
/// Compatibility facade for the WPF layer. Business workflows live in
/// Application use cases and are composed in <see cref="AppComp"/>.
/// </summary>
public sealed class AppFlow
{
    private readonly AppCases _cases;

    public AppFlow()
        : this(AppComp.Build())
    {
    }

    public AppFlow(AppCases useCases)
    {
        _cases = useCases ??
            throw new ArgumentNullException(nameof(useCases));
    }

    public CatLoad LoadCat(string baseDir)
    {
        return _cases.RecCat.LoadData(baseDir);
    }

    public Task<AnlzRes> DoAsync(
        string rpcs3ExePath,
        Svc.RecSvc cat)
    {
        return _cases.Analysis.RunAsync(
            rpcs3ExePath,
            cat);
    }

    public CfgPlan CfgPlan(
        R3Inst install,
        R3Game game,
        WikiRec? rec,
        HwInfo? hw)
    {
        return _cases.Config.Build(
            install,
            game,
            rec,
            hw);
    }

    public CfgRes ApplyCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        R3Inst install)
    {
        return _cases.Config.Apply(
            plan,
            changes,
            install);
    }

    public CfgRes DelCfg(
        CfgPlan plan,
        IReadOnlyList<CfgChg> changes,
        R3Inst install)
    {
        return _cases.Config.Remove(
            plan,
            changes,
            install);
    }

    public PatCatRs LoadPat(
        R3Inst install)
    {
        return _cases.PatCat.Load(install);
    }

    public CanDel DelCan(
        R3Inst install)
    {
        return _cases.CanImp.Remove(install);
    }

    public CanNorm NormCan(
        string? sourceText)
    {
        return _cases.CanImp.Norm(sourceText);
    }

    public bool HasImp(
        R3Inst install)
    {
        return _cases.CanImp.HasPat(
            install);
    }

    public CanWrite ImpCan(
        R3Inst install,
        string sourceText)
    {
        return _cases.CanImp.Import(
            install,
            sourceText);
    }

    public IReadOnlyList<PatMatch> PatPrev(
        Mdl.PatCat patchCat,
        R3Game game)
    {
        return _cases.PatCat.Build(
            patchCat,
            game);
    }

    public PatPlan PatPlan(
        R3Inst install,
        IReadOnlyList<PatMatch> matches,
        bool expertMode)
    {
        return _cases.PatMut.BuildOn(
            install,
            matches,
            expertMode);
    }

    public PatPlan OffPlan(
        R3Inst install,
        IReadOnlyList<PatMatch> matches)
    {
        return _cases.PatMut.BuildOff(
            install,
            matches);
    }

    public PatChg? PatChg(
        PatMatch match)
    {
        return _cases.PatMut.BuildChg(match);
    }

    public PatRes ApplyTxn(
        PatPlan plan,
        R3Inst install,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        return _cases.PatMut.ApplyTxn(
            plan,
            install,
            selectedMatches);
    }

    public PatRes ApplyPat(
        PatPlan plan,
        R3Inst install)
    {
        return _cases.PatMut.Apply(
            plan,
            install);
    }

    public PatRes OffPat(PatPlan plan,
        R3Inst install)
    {
        return _cases.PatMut.Disable(
            plan,
            install);
    }

    public bool CanRead(
        R3Inst install)
    {
        return _cases.PatCat.CanRead(install);
    }

    public CfgSnap ReadCfg(
        R3Inst install)
    {
        return _cases.PatCat.ReadCfg(install);
    }

    public Svc.RecSvc BuildCat(string xmlPath)
    {
        return _cases.RecCat.Rebuild(xmlPath);
    }

    public string CatText(CatInfo info)
    {
        return _cases.RecCat.FmtInf(info);
    }

    public void ExpCat(
        Svc.RecSvc cat,
        string path)
    {
        Case.RecCat.Export(cat, path);
    }

    public IReadOnlyList<BkpSnap> ListBkp(
        R3Inst install)
    {
        return _cases.Backup.List(install);
    }

    public void Restore(
        R3Inst install,
        BkpSnap backup)
    {
        _cases.Backup.Restore(install, backup);
    }

    public static string BkpRoot(
        R3Inst install)
    {
        return R3Paths.BkpRoot(install);
    }

    public IReadOnlyList<HCanStor> LoadHCan(
        R3Inst install)
    {
        return _cases.HCanary.Load(install);
    }

    public SaveRes SaveHCan(
        R3Inst install,
        HCanDrft draft,
        string? originalIdentity = null,
        bool saveAsNew = false)
    {
        return _cases.HCanary.Save(
            install,
            draft,
            originalIdentity,
            saveAsNew);
    }

    public SaveRes DelHCan(
        R3Inst install,
        string identity)
    {
        return _cases.HCanary.Delete(
            install,
            identity);
    }
}
