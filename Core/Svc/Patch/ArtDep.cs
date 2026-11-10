using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed class ArtDepSvc
{
    public const string FileName = "art_dep.json";
    public const string SchemaVersion = "1";
    public const string SourceIdentity = "Artemis-Patch-Collection-RPCS3";
    public const string SourceRevision = "fd267a65759a2f1dcae31e362d318de7a6e16594";
    public const string ArtemisSha256 =
        "68c1f43daef16b3f05514227d8e63346c56dee9f9e63fda273c8cd5acb630bf2";
    public const int ReviewedRuleCount = 23;

    private const int MaxRules = 256;
    private const int MaxText = 512;
    private const int MaxFileBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public ArtDepCat Load(
        string dependencyPath,
        string artemisPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artemisPath);

        var actualArtemisHash = HashFile(artemisPath);
        if (!actualArtemisHash.Equals(
                ArtemisSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The Artemis dependency overlay does not match the shipped art.yml corpus.");
        }

        var text = R3Bound.ReadUtf8(
            dependencyPath,
            MaxFileBytes,
            "Artemis dependency overlay");
        var dto = JsonSerializer.Deserialize<CatalogDto>(
            text,
            JsonOptions)
            ?? throw new InvalidDataException(
                "The Artemis dependency overlay is empty.");

        if (!string.Equals(
                dto.SchemaVersion,
                SchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                dto.SourceIdentity,
                SourceIdentity,
                StringComparison.Ordinal) ||
            !string.Equals(
                dto.SourceRevision,
                SourceRevision,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                dto.ArtemisSha256,
                ArtemisSha256,
                StringComparison.OrdinalIgnoreCase) ||
            dto.Rules is null ||
            dto.Rules.Count != ReviewedRuleCount ||
            dto.Rules.Count > MaxRules)
        {
            throw new InvalidDataException(
                "The Artemis dependency overlay metadata, provenance, or reviewed rule count is invalid.");
        }

        var rules = new List<ArtDepRule>(dto.Rules.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dto.Rules)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "The Artemis dependency overlay contains a null rule.");
            }

            var runKey = Required(item.RunKey, nameof(item.RunKey));
            var serial = Required(item.Serial, nameof(item.Serial));
            var version = Required(item.Version, nameof(item.Version));
            var patchTitle = Required(item.PatchTitle, nameof(item.PatchTitle));
            var evidence = Required(item.Evidence, nameof(item.Evidence));

            if (!Mdl.RunKey.TryParse(runKey, out var parsedKey) ||
                !parsedKey.RunKey.Equals(runKey, StringComparison.OrdinalIgnoreCase) ||
                !IsSerial(serial) ||
                PatVer.Norm(version) is not { } normalizedVersion ||
                PatVer.IsAll(normalizedVersion) ||
                item.Mode is not PatDepMode.All and not PatDepMode.Any ||
                item.Requires is null ||
                item.Requires.Count == 0 ||
                item.Requires.Count > 16)
            {
                throw new InvalidDataException(
                    $"Artemis dependency rule '{patchTitle}' has an invalid exact identity or requirement set.");
            }

            var requirements = item.Requires
                .Select(title => Required(title, "Requires"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (requirements.Length != item.Requires.Count ||
                requirements.Any(title => title.Equals(
                    patchTitle,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    $"Artemis dependency rule '{patchTitle}' contains a duplicate or self dependency.");
            }

            var mode = item.Mode!.Value;
            var identity = string.Join(
                "\0",
                parsedKey.RunKey,
                serial,
                normalizedVersion,
                patchTitle,
                mode);
            if (!seen.Add(identity))
            {
                throw new InvalidDataException(
                    $"Duplicate Artemis dependency rule: {patchTitle}.");
            }

            rules.Add(new ArtDepRule(
                parsedKey.RunKey,
                serial.ToUpperInvariant(),
                normalizedVersion,
                patchTitle,
                mode,
                requirements,
                evidence));
        }

        return new ArtDepCat(
            dto.SchemaVersion,
            dto.SourceIdentity,
            dto.SourceRevision,
            dto.ArtemisSha256.ToLowerInvariant(),
            rules);
    }

    public ArtDepProjection Apply(
        ArtDepCat dependencies,
        PatCat catalog)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(catalog);

        var groupsByEntry = new Dictionary<int, List<PatDepGroup>>();
        foreach (var rule in dependencies.Rules)
        {
            var candidates = catalog.Entries
                .Select((entry, index) => (entry, index))
                .Where(candidate => Matches(rule, candidate.entry))
                .ToArray();

            if (candidates.Length != 1)
            {
                throw new InvalidDataException(
                    $"Artemis dependency rule '{rule.PatchTitle}' projected to {candidates.Length} exact source definitions.");
            }

            if (!groupsByEntry.TryGetValue(
                    candidates[0].index,
                    out var groups))
            {
                groups = [];
                groupsByEntry.Add(candidates[0].index, groups);
            }

            groups.Add(new PatDepGroup(
                rule.Mode,
                rule.Requires));
        }

        var entries = catalog.Entries
            .Select((entry, index) => groupsByEntry.TryGetValue(index, out var groups)
                ? entry with
                {
                    DepGroups =
                    [
                        .. entry.DepGroups ?? [],
                        .. groups
                    ]
                }
                : entry)
            .ToArray();

        PatDepVal.ValidateCatalog(entries);

        return new ArtDepProjection(
            catalog with { Entries = entries },
            dependencies.Rules.Count,
            groupsByEntry.Count);
    }

    private static bool Matches(
        ArtDepRule rule,
        Rpcs3PatchEntry entry)
    {
        return entry.Source is PatSrc.Artemis or PatSrc.ManagedArtemis &&
               entry.RunKey.Equals(
                   rule.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               entry.Title.Equals(
                   rule.PatchTitle,
                   StringComparison.OrdinalIgnoreCase) &&
               entry.GameTgt.Any(target =>
                   target.Serial.Equals(
                       rule.Serial,
                       StringComparison.OrdinalIgnoreCase) &&
                   target.Versions.Any(version =>
                       PatVer.Equals(version, rule.Version)));
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(
                SHA256.HashData(stream))
            .ToLowerInvariant();
    }

    private static bool IsSerial(string value)
    {
        return value.Length == 9 &&
               value.All(character =>
                   character is >= 'A' and <= 'Z' or
                   >= '0' and <= '9');
    }

    private static string Required(
        string? value,
        string name)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > MaxText)
        {
            throw new InvalidDataException(
                $"Artemis dependency {name} is missing or exceeds the input bound.");
        }

        return normalized;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class CatalogDto
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string SourceIdentity { get; set; } = string.Empty;
        public string SourceRevision { get; set; } = string.Empty;
        public string ArtemisSha256 { get; set; } = string.Empty;
        public List<RuleDto?>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        public string? RunKey { get; set; }
        public string? Serial { get; set; }
        public string? Version { get; set; }
        public string? PatchTitle { get; set; }
        public PatDepMode? Mode { get; set; }
        public List<string?>? Requires { get; set; }
        public string? Evidence { get; set; }
    }
}

#pragma warning restore CA1822
