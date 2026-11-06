using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record Rpcs3PackageTitle(string Type, string Title);

public sealed record Rpcs3PackageChangelog(string Type, string Content);

public sealed record Rpcs3UpdatePackage(
    string Version,
    long Size,
    string Sha1,
    string? SystemVersion,
    string? DrmType,
    IReadOnlyList<Rpcs3PackageChangelog> Changelogs,
    IReadOnlyList<Rpcs3PackageTitle> Titles);

public sealed record Rpcs3Patchset(
    string TagId,
    string Popup,
    string Signoff,
    int? PopupDelay,
    string? MinimumSystemVersion,
    IReadOnlyList<Rpcs3UpdatePackage> Packages);

public sealed record Rpcs3CompatibilityEntry(
    string Serial,
    string Status,
    DateOnly Date,
    string Update,
    IReadOnlyList<Rpcs3Patchset> Patchsets)
{
    public string? Title => Patchsets
        .SelectMany(patchset => patchset.Packages)
        .SelectMany(package => package.Titles)
        .OrderByDescending(title => title.Type.Equals("TITLE", StringComparison.Ordinal))
        .Select(title => string.Join(
            ' ',
            title.Title.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)))
        .FirstOrDefault(title => title.Length > 0);
}

public sealed class Rpcs3GameDatabase
{
    public const string CompatibilityFileName = "compat-export.json";
    public const string ConfigFileName = "game-configs.json";
    public const string CompatibilitySha256 = "21D87C3CF1CE354747DEDF73E0B0FAEE918EC7F017EA1A5F40D4FBCFF5FC99E8";
    public const string ConfigSha256 = "B08D2FB6A6E8E663E62BB70A0F307C644272BF276A0C855ADB49D86E93B1A391";
    public const int CompatibilityCount = 6798;
    public const int ConfigCount = 2294;

    private const long MaxCompatibilityBytes = 8L * 1024 * 1024;
    private static readonly Regex SerialRule = new("^[A-Z]{4}[0-9]{5}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly IReadOnlyDictionary<string, Rpcs3CompatibilityEntry> _compatibility;
    private readonly IReadOnlyDictionary<string, RecSyncApiGame> _configs;

    private Rpcs3GameDatabase(
        IReadOnlyDictionary<string, Rpcs3CompatibilityEntry> compatibility,
        IReadOnlyDictionary<string, RecSyncApiGame> configs,
        long configTimestamp)
    {
        _compatibility = compatibility;
        _configs = configs;
        ConfigTimestamp = configTimestamp;
    }

    public static Rpcs3GameDatabase Empty { get; } = new(
        new Dictionary<string, Rpcs3CompatibilityEntry>(StringComparer.Ordinal),
        new Dictionary<string, RecSyncApiGame>(StringComparer.Ordinal),
        0);

    public int CompatibilityEntries => _compatibility.Count;

    public int ConfigEntries => _configs.Count;

    public long ConfigTimestamp { get; }

    public IEnumerable<string> CompatibilitySerials => _compatibility.Keys;

    public IEnumerable<string> ConfigSerials => _configs.Keys;

    public Rpcs3CompatibilityEntry? FindCompatibility(string serial) =>
        _compatibility.GetValueOrDefault(serial);

    public RecSyncApiGame? FindConfig(string serial) =>
        _configs.GetValueOrDefault(serial);

    public static Rpcs3GameDatabase Load(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        var compatibilityBytes = R3Bound.ReadBytes(
            Path.Combine(dataDirectory, CompatibilityFileName),
            MaxCompatibilityBytes,
            CompatibilityFileName);
        var configBytes = R3Bound.ReadBytes(
            Path.Combine(dataDirectory, ConfigFileName),
            ConfigApiParser.MaxResponseBytes,
            ConfigFileName);

        RequireHash(compatibilityBytes, CompatibilitySha256, CompatibilityFileName);
        RequireHash(configBytes, ConfigSha256, ConfigFileName);

        var compatibility = ParseCompatibility(compatibilityBytes);
        var configs = ConfigApiParser.Parse(configBytes);
        if (compatibility.Count != CompatibilityCount || configs.Games.Count != ConfigCount)
        {
            throw new InvalidDataException(
                $"RPCS3 game database count mismatch: compatibility={compatibility.Count}, configs={configs.Games.Count}.");
        }

        return new Rpcs3GameDatabase(
            compatibility,
            configs.Games.ToDictionary(game => game.Serial, StringComparer.Ordinal),
            configs.Timestamp);
    }

    internal static Dictionary<string, Rpcs3CompatibilityEntry> ParseCompatibility(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicateKeys(document.RootElement, "$");
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("return_code", out var returnCode) ||
            returnCode.ValueKind != JsonValueKind.Number ||
            returnCode.GetInt32() != 0 ||
            !document.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("RPCS3 compatibility export has an invalid root contract.");
        }

        var output = new Dictionary<string, Rpcs3CompatibilityEntry>(StringComparer.Ordinal);
        foreach (var game in results.EnumerateObject())
        {
            if (!SerialRule.IsMatch(game.Name))
            {
                throw new InvalidDataException($"RPCS3 compatibility export has invalid Game ID '{game.Name}'.");
            }

            var dto = game.Value.Deserialize<CompatibilityDto>(JsonOptions)
                ?? throw new InvalidDataException($"RPCS3 compatibility entry '{game.Name}' is empty.");
            if (string.IsNullOrWhiteSpace(dto.Status) ||
                string.IsNullOrWhiteSpace(dto.Date) ||
                !DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                dto.Update is null ||
                dto.Patchsets is null)
            {
                throw new InvalidDataException($"RPCS3 compatibility entry '{game.Name}' is incomplete.");
            }

            output.Add(game.Name, new Rpcs3CompatibilityEntry(
                game.Name,
                dto.Status,
                date,
                dto.Update,
                [.. dto.Patchsets.Select(Map)]));
        }

        return output;
    }

    private static Rpcs3Patchset Map(PatchsetDto patchset) =>
        new(
            patchset.TagId ?? string.Empty,
            patchset.Popup ?? string.Empty,
            patchset.Signoff ?? string.Empty,
            patchset.PopupDelay,
            patchset.MinimumSystemVersion,
            [.. (patchset.Packages ?? []).Select(package => new Rpcs3UpdatePackage(
                package.Version ?? string.Empty,
                package.Size,
                package.Sha1 ?? string.Empty,
                package.SystemVersion,
                package.DrmType,
                [.. (package.Changelogs ?? []).Select(item => new Rpcs3PackageChangelog(
                    item.Type ?? string.Empty,
                    item.Content ?? string.Empty))],
                [.. (package.Titles ?? []).Select(item => new Rpcs3PackageTitle(
                    item.Type ?? string.Empty,
                    item.Title ?? string.Empty))]))]);

    private static void RequireHash(byte[] bytes, string expected, string fileName)
    {
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{fileName} SHA-256 mismatch: {actual}.");
        }
    }

    private static void RejectDuplicateKeys(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"RPCS3 game database repeats '{property.Name}' at '{path}'.");
                }

                RejectDuplicateKeys(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateKeys(item, $"{path}[{index++}]");
            }
        }
    }

    private sealed class CompatibilityDto
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("date")]
        public string? Date { get; init; }

        [JsonPropertyName("update")]
        public string? Update { get; init; }

        [JsonPropertyName("patchsets")]
        public List<PatchsetDto>? Patchsets { get; init; }
    }

    private sealed class PatchsetDto
    {
        [JsonPropertyName("tag_id")]
        public string? TagId { get; init; }

        [JsonPropertyName("popup")]
        public string? Popup { get; init; }

        [JsonPropertyName("signoff")]
        public string? Signoff { get; init; }

        [JsonPropertyName("popup_delay")]
        public int? PopupDelay { get; init; }

        [JsonPropertyName("min_system_ver")]
        public string? MinimumSystemVersion { get; init; }

        [JsonPropertyName("packages")]
        public List<PackageDto>? Packages { get; init; }
    }

    private sealed class PackageDto
    {
        [JsonPropertyName("version")]
        public string? Version { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("sha1sum")]
        public string? Sha1 { get; init; }

        [JsonPropertyName("ps3_system_ver")]
        public string? SystemVersion { get; init; }

        [JsonPropertyName("drm_type")]
        public string? DrmType { get; init; }

        [JsonPropertyName("changelogs")]
        public List<ChangelogDto>? Changelogs { get; init; }

        [JsonPropertyName("titles")]
        public List<TitleDto>? Titles { get; init; }
    }

    private sealed class ChangelogDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class TitleDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }
    }
}
