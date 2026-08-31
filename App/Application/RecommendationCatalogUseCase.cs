using System.IO;
using System.Globalization;
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
        catch (MissingCatalogException ex)
        {
            return new CatalogLoadResult(null, ex.Message);
        }
        catch (InvalidCatalogException ex)
        {
            return new CatalogLoadResult(null, ex.Message);
        }
        catch
        {
            return new CatalogLoadResult(
                null,
                InvalidCatalogException.UserMsg);
        }
    }

    public static Svc.RecSvc Rebuild(
        string xmlPath)
    {
        var analysis = WikiSvc.Analyze(xmlPath);
        var generatedAt = DateTimeOffset.ParseExact(
            analysis.SnapshotDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        return Svc.RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            analysis.SnapshotDate,
            analysis.CatalogRecommendations,
            generatedAt,
            analysis.SourceSha256);
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
