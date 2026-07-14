using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.UseCases;

public sealed class RecommendationCatalogUseCase(
    CatInfoService catalogInfo)
{
    public CatLoadResult LoadBuiltIn(string baseDirectory)
    {
        var path = Path.Combine(
            baseDirectory,
            "Data",
            "cat.json");

        try
        {
            return new CatLoadResult(
                catalogInfo.LoadCatalog(path),
                null);
        }
        catch (CatalogMissingException ex)
        {
            return new CatLoadResult(null, ex.Message);
        }
        catch (CatalogInvalidException ex)
        {
            return new CatLoadResult(null, ex.Message);
        }
        catch
        {
            return new CatLoadResult(
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

    public string FormatInfo(CatalogInfo info)
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
