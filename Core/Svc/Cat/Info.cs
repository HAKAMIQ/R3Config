using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class CatSvc
{
    public RecSvc LoadCat(string catalogPath)
    {
        if (!File.Exists(catalogPath))
        {
            throw new MissingCatalogException();
        }

        byte[] bytes;

        try
        {
            bytes = R3Bound.ReadBytes(
                catalogPath,
                R3Limits.PatCatBy,
                "recommendation catalog");
        }
        catch (FileNotFoundException ex)
        {
            throw new MissingCatalogException(ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new MissingCatalogException(ex);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidCatalogException(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidCatalogException(ex);
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var catalog = RecSvc.Load(stream);
            var generatedAt = catalog.GenAt ??
                              new DateTimeOffset(File.GetLastWriteTimeUtc(catalogPath));
            var sourceHash = string.IsNullOrWhiteSpace(catalog.SrcHash)
                ? CalcHash(bytes)
                : catalog.SrcHash;

            return catalog.WithInfo(catalog.Info with
            {
                GenAt = generatedAt,
                SrcHash = sourceHash,
                RecNum = catalog.Count
            });
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidCatalogException(ex);
        }
    }

    public string FmtInf(Mdl.CatInfo info)
    {
        var generatedAt = info.GenAt?.UtcDateTime
            .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "-";

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"DataSource: {info.DataSrc}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"DataVersion: {info.DataVer}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"GeneratedAt: {generatedAt}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"SourceHash: {Display(info.SrcHash)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"RecommendationCount: {info.RecNum}");

        return builder.ToString();
    }

    private static string CalcHash(byte[] bytes)
    {
        return Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value;
    }
}

public sealed class MissingCatalogException : Exception
{
    public const string UserMsg = "Recommendation data is missing";

    public MissingCatalogException()
        : base(UserMsg)
    {
    }

    public MissingCatalogException(Exception innerException)
        : base(UserMsg, innerException)
    {
    }
}

public sealed class InvalidCatalogException : Exception
{
    public const string UserMsg = "Recommendation data is invalid";

    public InvalidCatalogException()
        : base(UserMsg)
    {
    }

    public InvalidCatalogException(Exception innerException)
        : base(UserMsg, innerException)
    {
    }
}

#pragma warning restore CA1822, IDE0130
