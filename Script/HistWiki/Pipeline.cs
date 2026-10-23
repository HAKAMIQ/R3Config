using System.Security.Cryptography;

namespace HistWiki;

public sealed record Options(string ExportDir, string SchemaXml, string SnapshotXml, string OutDir, string Declarations, string? ReferenceDir, string? PreviousDir = null);

public sealed class SourceInfo
{
    public string FullPath = "", FileName = "", Sha256 = "", ParseStatus = "", Role = "", Error = "";
    public long Size;
    public int Pages, Revisions;
    public string FirstTs = "", LastTs = "";
}

public sealed class Result
{
    public Options Options = null!;
    public readonly List<SourceInfo> Sources = [];
    public GameAnalyzer Games = new();
    public SchemaModel Schema = null!;
    public EffectiveBuilder Effective = null!;
    public readonly Dictionary<string, (long Rev, string Sha)> SnapshotPages = new(StringComparer.Ordinal);
    public int SnapshotNs0Pages, SnapshotTotalPages;
    public ReconSummary Recon = new();
    public readonly List<string> Fatal = [];
    public readonly List<(string Name, string Sha)> OutputHashes = [];
}

public static class Pipeline
{
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static Result Run(Options options)
    {
        var result = new Result { Options = options };
        var files = Directory.EnumerateFiles(options.ExportDir, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();

        foreach (var file in files)
        {
            var info = NewSource(file, "GAME_HISTORY");
            result.Sources.Add(info);
            try
            {
                foreach (var page in WikiXml.Pages(file))
                {
                    info.Pages++;
                    info.Revisions += page.Revs.Count;
                    foreach (var r in page.Revs)
                    {
                        if (info.FirstTs.Length == 0 || string.CompareOrdinal(r.Timestamp, info.FirstTs) < 0) info.FirstTs = r.Timestamp;
                        if (string.CompareOrdinal(r.Timestamp, info.LastTs) > 0) info.LastTs = r.Timestamp;
                    }

                    if (page.Ns != 0 || page.Revs.Count == 0)
                    {
                        result.Fatal.Add($"{info.FileName}: page '{page.Title}' has ns={page.Ns} / {page.Revs.Count} revisions; only ns0 pages with revisions are accepted.");
                        continue;
                    }

                    result.Games.Process(page);
                }

                info.ParseStatus = "OK";
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException or IOException)
            {
                info.ParseStatus = "FAILED";
                info.Error = ex.Message;
                result.Fatal.Add($"{info.FileName}: {ex.Message}");
            }
        }

        var schemaInfo = NewSource(options.SchemaXml, "TEMPLATE_CONFIG_HISTORY");
        result.Sources.Add(schemaInfo);
        try
        {
            var pages = WikiXml.Pages(options.SchemaXml).ToList();
            schemaInfo.Pages = pages.Count;
            schemaInfo.Revisions = pages.Sum(p => p.Revs.Count);
            result.Schema = SchemaModel.Build(pages);
            schemaInfo.FirstTs = result.Schema.First.Ts;
            schemaInfo.LastTs = result.Schema.Last.Ts;
            schemaInfo.ParseStatus = "OK";
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException or IOException)
        {
            schemaInfo.ParseStatus = "FAILED";
            schemaInfo.Error = ex.Message;
            result.Fatal.Add($"{schemaInfo.FileName}: {ex.Message}");
            return result;
        }

        var snapInfo = NewSource(options.SnapshotXml, "CURRENT_SNAPSHOT_REFERENCE");
        result.Sources.Add(snapInfo);
        try
        {
            foreach (var page in WikiXml.Pages(options.SnapshotXml))
            {
                snapInfo.Pages++;
                snapInfo.Revisions += page.Revs.Count;
                result.SnapshotTotalPages++;
                if (page.Ns != 0) continue;
                result.SnapshotNs0Pages++;
                var last = page.Revs.OrderBy(r => new Pt(r.Timestamp, r.Id)).Last();
                result.SnapshotPages[page.Title] = (last.Id, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(last.Text))));
            }

            snapInfo.ParseStatus = "OK";
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException or IOException)
        {
            snapInfo.ParseStatus = "FAILED";
            snapInfo.Error = ex.Message;
            result.Fatal.Add($"{snapInfo.FileName}: {ex.Message}");
        }

        var eff = new EffectiveBuilder(result.Schema);
        result.Effective = eff;
        eff.VerifyDeclarations(EffectiveBuilder.LoadDeclarations(options.Declarations));
        eff.BuildIdentities(result.Games.Events);
        eff.BuildEffective(result.Games.Intervals);
        eff.DetectConflicts();
        result.Recon = Reconcile.Run(result);
        return result;
    }

    private static SourceInfo NewSource(string path, string role)
    {
        var full = Path.GetFullPath(path);
        return new SourceInfo
        {
            FullPath = full, FileName = Path.GetFileName(full), Role = role, Size = new FileInfo(full).Length, Sha256 = Sha256File(full)
        };
    }
}
