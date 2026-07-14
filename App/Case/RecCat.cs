using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Case;

public sealed class RecCat(
    CatInfoService catalogInfo)
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
                catalogInfo.LoadCatalog(path),
                null);
        }
        catch (CatalogMissingException ex)
        {
            return new CatLoad(null, ex.Message);
        }
        catch (CatalogInvalidException ex)
        {
            return new CatLoad(null, ex.Message);
        }
        catch
        {
            return new CatLoad(
                null,
                CatalogInvalidException.UserMessage);
        }
    }

    public RecommendationCatalogService Rebuild(
        string xmlPath)
    {
        var wiki = WikiRecommendationService.Load(xmlPath);
        var sourceHash = catalogInfo.ComputeFileHash(xmlPath);

        return RecommendationCatalogService.FromRecommendations(
            "Developer import",
            DateTime.Today.ToString("yyyy-MM-dd"),
            wiki.Recommendations,
            DateTimeOffset.UtcNow,
            sourceHash);
    }

    public string FmtInf(CatalogInfo info)
    {
        return catalogInfo.FormatInfo(info);
    }

    public static void Export(
        RecommendationCatalogService catalog,
        string path)
    {
        catalog.Save(path);
    }
}
