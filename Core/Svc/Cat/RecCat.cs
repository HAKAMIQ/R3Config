using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class RecSvc
{
    private static readonly Encoding Utf8Bom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new RiskJson(), new JsonStringEnumConverter() }
    };

    private readonly IReadOnlyDictionary<string, WikiRec> _Rec;

    private RecSvc(
        Mdl.CatInfo info,
        IReadOnlyDictionary<string, WikiRec> recommendations)
    {
        Info = info with
        {
            RecNum = recommendations.Count
        };

        _Rec = recommendations;
    }

    public Mdl.CatInfo Info { get; }

    public string DataSrc => Info.DataSrc;

    public string DataVer => Info.DataVer;

    public DateTimeOffset? GenAt => Info.GenAt;

    public string SrcHash => Info.SrcHash;

    public int Count => _Rec.Count;

    public IReadOnlyCollection<WikiRec> Rec =>
    [
        .. _Rec.Values
    ];

    public WikiRec? Find(string serial)
    {
        return _Rec.TryGetValue(serial, out var recommendation)
            ? recommendation
            : null;
    }

    public RecSvc WithInfo(Mdl.CatInfo info)
    {
        return new RecSvc(info, _Rec);
    }

    public static RecSvc Load(string catalogPath)
    {
        using var stream = File.OpenRead(catalogPath);
        return Load(stream);
    }

    public static RecSvc Load(Stream catalogStream)
    {
        ArgumentNullException.ThrowIfNull(catalogStream);

        var bytes = R3Bound.ReadBytes(
            catalogStream,
            R3Limits.PatCatBy,
            "recommendation catalog");

        if (bytes.Length == 0)
        {
            throw new InvalidDataException(
                "Recommendation catalog is empty or invalid.");
        }

        RecDto catalog;

        try
        {
            using var document = JsonDocument.Parse(bytes);
            VldKeys(document.RootElement, "$");

            catalog = JsonSerializer.Deserialize<RecDto>(
                bytes,
                Options)
                ?? throw new InvalidDataException(
                    "Recommendation catalog is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Recommendation catalog JSON is invalid.",
                ex);
        }

        var dataVersion = Require(
            catalog.DataVer,
            "DataVersion");

        var recommendations = BuildMap(catalog.Rec);

        var info = new Mdl.CatInfo(
            string.IsNullOrWhiteSpace(catalog.DataSrc)
                ? "Built-in"
                : catalog.DataSrc.Trim(),
            dataVersion,
            catalog.GenAt,
            string.IsNullOrWhiteSpace(catalog.SrcHash)
                ? string.Empty
                : catalog.SrcHash.Trim(),
            recommendations.Count);

        return new RecSvc(
            info,
            recommendations);
    }

    public static RecSvc FromRec(
        string dataSource,
        string dataVersion,
        IEnumerable<WikiRec> recommendations,
        DateTimeOffset? generatedAt = null,
        string? sourceHash = null)
    {
        ArgumentNullException.ThrowIfNull(recommendations);

        var map = BuildMap(recommendations);

        var info = new Mdl.CatInfo(
            string.IsNullOrWhiteSpace(dataSource)
                ? "Built-in"
                : dataSource.Trim(),
            Require(dataVersion, "DataVersion"),
            generatedAt ?? DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(sourceHash)
                ? string.Empty
                : sourceHash.Trim(),
            map.Count);

        return new RecSvc(info, map);
    }

    public static void Save(
        string catalogPath,
        string dataVersion,
        IEnumerable<WikiRec> recommendations)
    {
        FromRec(
            "Built-in",
            dataVersion,
            recommendations)
            .Save(catalogPath);
    }

    public static void Save(
        string catalogPath,
        Mdl.CatInfo info,
        IEnumerable<WikiRec> recommendations)
    {
        FromRec(
            info.DataSrc,
            info.DataVer,
            recommendations,
            info.GenAt,
            info.SrcHash)
            .Save(catalogPath);
    }

    public void Save(string catalogPath)
    {
        var catalog = new RecDto
        {
            DataSrc = Info.DataSrc,
            DataVer = Info.DataVer,
            GenAt = Info.GenAt,
            SrcHash = Info.SrcHash,
            RecNum = Count,
            Rec =
            [
                .. _Rec.Values
                    .Select(NormRec)
                    .OrderBy(
                        item => item.Serial,
                        StringComparer.OrdinalIgnoreCase)
            ]
        };

        var directory = Path.GetDirectoryName(catalogPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer
            .Serialize(catalog, Options)
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal);

        // Write beside the target, then move over it: a move replaces the
        // directory entry, so a hardlinked target name never writes through
        // to the linked file, and a failed write leaves the target intact.
        var fullPath = Path.GetFullPath(catalogPath);
        var stagePath = Path.Combine(
            Path.GetDirectoryName(fullPath) ?? string.Empty,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                stagePath,
                json,
                Utf8Bom);
            File.Move(
                stagePath,
                fullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(stagePath))
            {
                File.Delete(stagePath);
            }
        }
    }

    private static Dictionary<string, WikiRec> BuildMap(
        IEnumerable<WikiRec>? source)
    {
        if (source is null)
        {
            throw new InvalidDataException(
                "Recommendation catalog is missing Recommendations.");
        }

        var map =
            new Dictionary<string, WikiRec>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in source)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "Recommendation catalog contains a null recommendation.");
            }

            var normalized = NormRec(item);

            if (!map.TryAdd(normalized.Serial, normalized))
            {
                throw new InvalidDataException(
                    $"Recommendation catalog contains duplicate serial '{normalized.Serial}'.");
            }
        }

        return map;
    }

    private static WikiRec NormRec(
        WikiRec recommendation)
    {
        var serial = Require(
            recommendation.Serial,
            "Serial")
            .ToUpperInvariant();

        if (!Serial().IsMatch(serial))
        {
            throw new InvalidDataException(
                $"Recommendation serial '{serial}' is invalid.");
        }

        var title = Require(
            recommendation.WikiName,
            $"WikiTitle for {serial}");

        var sourceChanges = recommendation.Changes
            ?? throw new InvalidDataException(
                $"Recommendation '{serial}' is missing Changes.");

        List<CfgChg> changes = [];
        var paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var change in sourceChanges)
        {
            if (change is null)
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' contains a null change.");
            }

            var section = Require(
                change.Section,
                $"Section for {serial}");

            var key = Require(
                change.Key,
                $"Key for {serial}");

            var value = Require(
                change.Value,
                $"Value for {serial} / {section} / {key}");

            var wikiKey = Require(
                change.WikiKey,
                $"WikiKey for {serial} / {section} / {key}");

            var path = $"{section}\0{key}";

            if (!paths.Add(path))
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' contains duplicate setting '{section} / {key}'.");
            }

            var notes = string.IsNullOrWhiteSpace(change.Notes)
                ? null
                : change.Notes.Trim();

            changes.Add(change with
            {
                Section = section,
                Key = key,
                Value = value,
                WikiKey = wikiKey,
                Notes = notes,
                Risk = RecPol.Classify(
                    wikiKey,
                    value,
                    notes)
            });
        }

        // Ambiguous Wiki guidance is text, not a setting: it keeps no value, it
        // never shares a Section/Key with a change, and a duplicate target is
        // dropped rather than merged. Nothing downstream can apply it.
        List<CfgHint> hints = [];

        foreach (var hint in recommendation.Guidance)
        {
            if (hint is null)
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' contains a null guidance entry.");
            }

            var section = Require(
                hint.Section,
                $"Guidance Section for {serial}");

            var key = Require(
                hint.Key,
                $"Guidance Key for {serial}");

            var wikiKey = Require(
                hint.WikiKey,
                $"Guidance WikiKey for {serial} / {section} / {key}");

            // A catalog is not always one R3Config generated: the published data
            // bundle comes in through this same loader. So the range is re-checked
            // against the one rule the generator used, instead of being trusted
            // because it parsed. A hint naming a setting R3Config does not map, or
            // bounds outside RPCS3's own limit for that key, would put a range on
            // screen that no RPCS3 version stands behind.
            if (!WikiSvc.IsHintRange(wikiKey, section, key, hint.Min, hint.Max))
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' guidance '{section} / {key}' is not a range RPCS3 allows for '{wikiKey}' ({hint.Min}-{hint.Max}).");
            }

            if (!paths.Add($"{section}\0{key}"))
            {
                throw new InvalidDataException(
                    $"Recommendation '{serial}' guidance '{section} / {key}' collides with another setting.");
            }

            hints.Add(hint with
            {
                Section = section,
                Key = key,
                WikiKey = wikiKey,
                RawValue = Require(
                    hint.RawValue,
                    $"Guidance Value for {serial} / {section} / {key}"),
                Notes = string.IsNullOrWhiteSpace(hint.Notes)
                    ? null
                    : hint.Notes.Trim()
            });
        }

        return recommendation with
        {
            Serial = serial,
            WikiName = title,
            Changes =
            [
                .. changes
            ],
            Hints = hints.Count == 0
                ? null
                :
                [
                    .. hints
                ]
        };
    }

    private static string Require(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Recommendation catalog field '{fieldName}' is missing or empty.");
        }

        return value.Trim();
    }

    private static void VldKeys(
        JsonElement element,
        string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var keys = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var property in element.EnumerateObject())
                    {
                        if (!keys.Add(property.Name))
                        {
                            throw new InvalidDataException(
                                $"Recommendation catalog contains duplicate JSON key '{property.Name}' at '{path}'.");
                        }

                        VldKeys(
                            property.Value,
                            $"{path}.{property.Name}");
                    }

                    break;
                }

            case JsonValueKind.Array:
                {
                    var index = 0;

                    foreach (var item in element.EnumerateArray())
                    {
                        VldKeys(
                            item,
                            $"{path}[{index}]");

                        index++;
                    }

                    break;
                }
        }
    }

    [GeneratedRegex(
        @"^[A-Z0-9]{9}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Serial();

    private sealed class RecDto
    {
        [JsonPropertyName("DataSource")]
        public string? DataSrc { get; set; } = "Built-in";

        [JsonPropertyName("DataVersion")]
        public string? DataVer { get; set; }

        [JsonPropertyName("GeneratedAt")]
        public DateTimeOffset? GenAt { get; set; }

        [JsonPropertyName("SourceHash")]
        public string? SrcHash { get; set; }

        [JsonPropertyName("RecommendationCount")]
        public int RecNum { get; set; }

        [JsonPropertyName("Recommendations")]
        public List<WikiRec>? Rec { get; set; }
    }

    private sealed class RiskJson : JsonConverter<RecRisk>
    {
        public override RecRisk Read(
            ref Utf8JsonReader reader,
            Type type,
            JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return value switch
            {
                "Recommended" or "Rec" => RecRisk.Rec,
                "Conditional" or "Cond" => RecRisk.Cond,
                "PerformanceCost" or "PerfCost" => RecRisk.PerfCost,
                "PatchDependent" or "PatDep" => RecRisk.PatDep,
                "HardwareDependent" or "Dep" => RecRisk.Dep,
                "ManualTiming" or "Timing" => RecRisk.Timing,
                "ExpertOnly" or "Only" => RecRisk.Only,
                _ when Enum.TryParse<RecRisk>(
                    value,
                    ignoreCase: true,
                    out var risk) => risk,
                _ => throw new JsonException(
                    $"Unknown recommendation risk '{value}'.")
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            RecRisk value,
            JsonSerializerOptions options)
        {
            var text = value switch
            {
                RecRisk.Rec => "Recommended",
                RecRisk.Cond => "Conditional",
                RecRisk.PerfCost => "PerformanceCost",
                RecRisk.PatDep => "PatchDependent",
                RecRisk.Dep => "HardwareDependent",
                RecRisk.Timing => "ManualTiming",
                RecRisk.Only => "ExpertOnly",
                _ => value.ToString()
            };
            writer.WriteStringValue(text);
        }
    }
}
