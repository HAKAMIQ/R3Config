using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed class RecommendationCatalogUseCase(
    Svc.CatSvc catalogInfo)
{
    public CatalogLoadResult LoadData(string baseDirectory)
    {
        var path = Path.Combine(
            baseDirectory,
            "Data",
            "cat.json");

        try
        {
            return new CatalogLoadResult(
                catalogInfo.LoadCat(path),
                null);
        }
        catch (MissEx ex)
        {
            return new CatalogLoadResult(null, ex.Message);
        }
        catch (CatBadEx ex)
        {
            return new CatalogLoadResult(null, ex.Message);
        }
        catch
        {
            return new CatalogLoadResult(
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
