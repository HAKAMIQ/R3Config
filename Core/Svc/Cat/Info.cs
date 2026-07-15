using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

#pragma warning disable CA1822, IDE0130

namespace R3Config.Svc;

public sealed class CatSvc
{
    public RecSvc LoadCat(string catalogPath)
    {
        if (!File.Exists(catalogPath))
        {
            throw new MissEx();
        }

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(catalogPath);
        }
        catch (FileNotFoundException ex)
        {
            throw new MissEx(ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new MissEx(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CatBadEx(ex);
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var catalog = RecSvc.Load(stream);
            var generatedAt = catalog.GenAt ??
                              new DateTimeOffset(File.GetLastWriteTimeUtc(catalogPath));
            var sourceHash = CalcHash(bytes);

            return catalog.WithInfo(catalog.Info with
            {
                GenAt = generatedAt,
                SrcHash = sourceHash,
                RecNum = catalog.Count
            });
        }
        catch (InvalidDataException ex)
        {
            throw new CatBadEx(ex);
        }
    }

    public Mdl.CatInfo ReadCat(string catalogPath)
    {
        return LoadCat(catalogPath).Info;
    }

    public string CalcFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Source file was not found.",
                filePath);
        }

        return CalcHash(File.ReadAllBytes(filePath));
    }

    public string FmtInf(Mdl.CatInfo info)
    {
        var generatedAt = info.GenAt?.UtcDateTime
            .ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "-";

        var builder = new StringBuilder();
        builder.AppendLine($"DataSource: {info.DataSrc}");
        builder.AppendLine($"DataVersion: {info.DataVer}");
        builder.AppendLine($"GeneratedAt: {generatedAt}");
        builder.AppendLine($"SourceHash: {Display(info.SrcHash)}");
        builder.AppendLine($"RecommendationCount: {info.RecNum}");

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

public sealed class MissEx : Exception
{
    public const string UserMsg = "Recommendation data is missing";

    public MissEx()
        : base(UserMsg)
    {
    }

    public MissEx(Exception innerException)
        : base(UserMsg, innerException)
    {
    }
}

public sealed class CatBadEx : Exception
{
    public const string UserMsg = "Recommendation data is invalid";

    public CatBadEx()
        : base(UserMsg)
    {
    }

    public CatBadEx(Exception innerException)
        : base(UserMsg, innerException)
    {
    }
}

#pragma warning restore CA1822, IDE0130