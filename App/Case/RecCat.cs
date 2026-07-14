using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class RecCat(
    Svc.CatSvc catalogInfo)
{
    public CatLoad LoadData(string baseDirectory)
    {
        var path = Path.Combine(
            baseDirectory,
            "Data",
            "cat.json");

        try
        {
            return new CatLoad(
                catalogInfo.LoadCat(path),
                null);
        }
        catch (MissEx ex)
        {
            return new CatLoad(null, ex.Message);
        }
        catch (CatBadEx ex)
        {
            return new CatLoad(null, ex.Message);
        }
        catch
        {
            return new CatLoad(
                null,
                CatBadEx.UserMsg);
        }
    }

    public Svc.RecSvc Rebuild(
        string xmlPath)
    {
        var wiki = WikiSvc.Load(xmlPath);
        var sourceHash = catalogInfo.CalcFile(xmlPath);

        return Svc.RecSvc.FromRec(
            "Developer import",
            DateTime.Today.ToString("yyyy-MM-dd"),
            wiki.Rec,
            DateTimeOffset.UtcNow,
            sourceHash);
    }

    public string FmtInf(Mdl.CatInfo info)
    {
        return catalogInfo.FmtInf(info);
    }

    public static void Export(
        Svc.RecSvc catalog,
        string path)
    {
        catalog.Save(path);
    }
}
