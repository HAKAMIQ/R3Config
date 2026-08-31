using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

public sealed class PatchRequirementCatalogService
{
    private const int MaxRules = 2048;
    private const int MaxText = 512;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public PatchRequirementCatalog Load(string path)
    {
        var text = R3Bound.ReadUtf8(path, 512 * 1024, "patch requirement catalog");
        var dto = JsonSerializer.Deserialize<CatalogDto>(text, JsonOptions)
            ?? throw new InvalidDataException("Patch requirement catalog is empty.");
        if (string.IsNullOrWhiteSpace(dto.SchemaVersion) ||
            string.IsNullOrWhiteSpace(dto.SourceIdentity) ||
            string.IsNullOrWhiteSpace(dto.SnapshotDate) ||
            dto.Rules is null || dto.Rules.Count > MaxRules)
        {
            throw new InvalidDataException("Patch requirement catalog metadata or bounds are invalid.");
        }

        var rules = new List<PatchRequirementRule>(dto.Rules.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in dto.Rules)
        {
            var title = Limit(item.PatchTitle, nameof(item.PatchTitle));
            var serial = Limit(item.Serial, nameof(item.Serial));
            var version = Limit(item.Version, nameof(item.Version));
            var hash = Limit(item.Hash, nameof(item.Hash));
            var identity = Limit(item.PatchIdentity, nameof(item.PatchIdentity));
            if (string.IsNullOrWhiteSpace(identity) && string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidDataException("Every patch requirement rule needs an identity or patch title.");
            }
            if (item.Requirements is null || item.Requirements.Count == 0)
            {
                throw new InvalidDataException("Patch requirement rules must contain at least one setting.");
            }

            var requirements = item.Requirements.Select(requirement =>
            {
                var section = Limit(requirement.Section, "Section");
                var key = Limit(requirement.Key, "Key");
                var value = Limit(requirement.Value, "Value");
                var evidence = Limit(requirement.Evidence, "Evidence");
                if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(key) ||
                    string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(evidence))
                {
                    throw new InvalidDataException("Patch requirement setting fields are required.");
                }
                return new PatchRequirement(
                    new CfgChg(section, key, value, key, evidence, RecRisk.PatDep), evidence);
            }).ToArray();

            var ruleKey = string.Join("|", identity, title, serial, version, hash, item.Source);
            if (!seen.Add(ruleKey))
            {
                throw new InvalidDataException($"Duplicate patch requirement rule: {ruleKey}");
            }
            rules.Add(new PatchRequirementRule(
                identity ?? string.Empty,
                requirements,
                title,
                serial,
                version,
                hash,
                item.Source,
                Limit(item.Notes, nameof(item.Notes)),
                dto.SourceIdentity,
                Limit(item.EvidenceStatus, nameof(item.EvidenceStatus)) ?? "PROVEN",
                Limit(item.EvidenceSource, nameof(item.EvidenceSource)),
                Limit(item.EvidenceRevision, nameof(item.EvidenceRevision))));
        }

        var canonical = JsonSerializer.Serialize(new
        {
            dto.SchemaVersion,
            dto.SourceIdentity,
            dto.SnapshotDate,
            Rules = rules.Select(rule => new
            {
                rule.PatchIdentity,
                rule.PatchTitle,
                rule.Serial,
                rule.Version,
                rule.Hash,
                rule.Source,
                Requirements = rule.Requirements.Select(req => new
                {
                    req.Setting.Section,
                    req.Setting.Key,
                    req.Setting.Value,
                    req.Evidence
                })
            })
        });
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(dto.ContentSha256) &&
            !dto.ContentSha256.Equals(contentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Patch requirement catalog content hash does not match.");
        }

        return new PatchRequirementCatalog(
            dto.SchemaVersion,
            dto.SourceIdentity,
            dto.SnapshotDate,
            contentHash,
            rules);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public IReadOnlyList<PatchRequirementRule> Resolve(
        PatchRequirementCatalog catalog,
        IReadOnlyList<Rpcs3PatchEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entries);
        var resolved = new List<PatchRequirementRule>();
        foreach (var rule in catalog.Rules)
        {
            if (!rule.EvidenceStatus.Equals("PROVEN", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            foreach (var entry in entries)
            {
                if (!Matches(rule, entry))
                {
                    continue;
                }
                resolved.Add(rule with
                {
                    PatchIdentity = GameSetupPlanningService.PatchIdentity(entry)
                });
            }
        }
        return resolved
            .GroupBy(rule => rule.PatchIdentity, StringComparer.Ordinal)
            .Select(group => group.First() with
            {
                Requirements = group.SelectMany(rule => rule.Requirements).ToArray()
            })
            .ToArray();
    }

    private static bool Matches(PatchRequirementRule rule, Rpcs3PatchEntry entry)
    {
        return (string.IsNullOrWhiteSpace(rule.PatchTitle) ||
                entry.Title.Equals(rule.PatchTitle, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(rule.Serial) ||
                entry.Serials.Contains(rule.Serial, StringComparer.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(rule.Version) ||
                entry.GameVer.Contains(rule.Version, StringComparer.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(rule.Hash) ||
                entry.Hash.Equals(rule.Hash, StringComparison.OrdinalIgnoreCase)) &&
            (rule.Source is null || entry.Source == rule.Source);
    }

    private static string? Limit(string? value, string name)
    {
        if (value is not null && value.Length > MaxText)
        {
            throw new InvalidDataException($"Patch requirement {name} exceeds the input bound.");
        }
        return value?.Trim();
    }

    private sealed class CatalogDto
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string SourceIdentity { get; set; } = string.Empty;
        public string SnapshotDate { get; set; } = string.Empty;
        public string? ContentSha256 { get; set; }
        public List<RuleDto>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        public string? PatchIdentity { get; set; }
        public string? PatchTitle { get; set; }
        public string? Serial { get; set; }
        public string? Version { get; set; }
        public string? Hash { get; set; }
        public PatSrc? Source { get; set; }
        public string? Notes { get; set; }
        public string? EvidenceStatus { get; set; }
        public string? EvidenceSource { get; set; }
        public string? EvidenceRevision { get; set; }
        public List<RequirementDto>? Requirements { get; set; }
    }

    private sealed class RequirementDto
    {
        public string Section { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
    }
}

#pragma warning restore CA1822
