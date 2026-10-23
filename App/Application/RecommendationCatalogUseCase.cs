using System;
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
        // The export is a user-chosen file write outside the mutation guard.
        // It only produces .json names, and RecSvc.Save replaces the name
        // instead of writing through it, so an RPCS3 YAML file such as
        // patch.yml is never overwritten, even through a hardlink.
        if (!string.Equals(
                Path.GetExtension(path),
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The recommendation catalog can only be exported to a .json file.");
        }

        catalog.Save(path);
    }
}
