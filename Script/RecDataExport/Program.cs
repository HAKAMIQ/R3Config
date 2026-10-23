using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Svc;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: RecDataExport <cat.json> <output-directory>");
    return 2;
}

var source = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var catalog = RecSvc.Load(source);
var version = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new JsonStringEnumConverter());

var recommendationBytes = JsonSerializer.SerializeToUtf8Bytes(new
{
    schema = 1,
    dataVersion = version,
    generatedAt = DateTimeOffset.UtcNow,
    recommendations = catalog.Rec.OrderBy(item => item.Serial, StringComparer.Ordinal)
}, options);
var notesBytes = JsonSerializer.SerializeToUtf8Bytes(new
{
    schema = 1,
    dataVersion = version,
    dataSource = "R3Config reviewed Arabic Wiki notes",
    notes = NoteSvc.ExportReviewedWikiArabic()
        .OrderBy(item => item.Key, StringComparer.Ordinal)
        .Select(item => new { sourceEnglish = item.Key, noteArabic = item.Value })
}, options);

Directory.CreateDirectory(output);
File.WriteAllBytes(Path.Combine(output, "recommendations.json"), recommendationBytes);
File.WriteAllBytes(Path.Combine(output, "notes-ar.json"), notesBytes);

var manifest = JsonSerializer.SerializeToUtf8Bytes(new
{
    schema = 1,
    version,
    repository = "HAKAMIQ/rpcs3",
    generatedAt = DateTimeOffset.UtcNow,
    files = new[]
    {
        new { path = "recommendations.json", sizeBytes = recommendationBytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(recommendationBytes)) },
        new { path = "notes-ar.json", sizeBytes = notesBytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(notesBytes)) }
    }
}, options);
File.WriteAllBytes(Path.Combine(output, "manifest.json"), manifest);

Console.WriteLine($"Exported local data to {output}");
Console.WriteLine("No network or Git operation was performed.");
return 0;
