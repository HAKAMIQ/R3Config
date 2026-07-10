using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class CatInfoService
{
    public RecommendationCatalogService LoadCatalog(string catalogPath)
    {
        if (!File.Exists(catalogPath))
        {
            throw new CatalogMissingException();
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(catalogPath);
        }
        catch (FileNotFoundException ex)
        {
            throw new CatalogMissingException(ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new CatalogMissingException(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CatalogInvalidException(ex);
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var catalog = RecommendationCatalogService.Load(stream);
            var generatedAt = catalog.GeneratedAt ?? new DateTimeOffset(File.GetLastWriteTimeUtc(catalogPath));
            var sourceHash = ComputeHash(bytes);

            return catalog.WithInfo(catalog.Info with
            {
                GeneratedAt = generatedAt,
                SourceHash = sourceHash,
                RecommendationCount = catalog.Count
            });
        }
        catch (InvalidDataException ex)
        {
            throw new CatalogInvalidException(ex);
        }
    }

    public CatalogInfo ReadCatalogInfo(string catalogPath)
    {
        return LoadCatalog(catalogPath).Info;
    }

    public string ComputeFileHash(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Source file was not found.", filePath);
        }

        return ComputeHash(File.ReadAllBytes(filePath));
    }

    public string FormatInfo(CatalogInfo info)
    {
        var generatedAt = info.GeneratedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "-";

        var builder = new StringBuilder();
        builder.AppendLine($"DataSource: {info.DataSource}");
        builder.AppendLine($"DataVersion: {info.DataVersion}");
        builder.AppendLine($"GeneratedAt: {generatedAt}");
        builder.AppendLine($"SourceHash: {DisplayValue(info.SourceHash)}");
        builder.AppendLine($"RecommendationCount: {info.RecommendationCount}");
        return builder.ToString();
    }

    private static string ComputeHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string DisplayValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}

public sealed class CatalogMissingException : Exception
{
    public const string UserMessage = "Recommendation data is missing";

    public CatalogMissingException()
        : base(UserMessage)
    {
    }

    public CatalogMissingException(Exception innerException)
        : base(UserMessage, innerException)
    {
    }
}

public sealed class CatalogInvalidException : Exception
{
    public const string UserMessage = "Recommendation data is invalid";

    public CatalogInvalidException()
        : base(UserMessage)
    {
    }

    public CatalogInvalidException(Exception innerException)
        : base(UserMessage, innerException)
    {
    }
}
