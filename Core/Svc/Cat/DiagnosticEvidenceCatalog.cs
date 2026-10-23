using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public enum EvidenceRejection
{
    InvalidJson,
    UnsupportedSchema,
    IncompleteRecord,
    CommunityMarkedOfficial,
    PrivateServerIndexTooStrong,
    OfficialSourceNotOfficial,
    ClaimNotAllowedForRole,
    IdentityApplicabilityMismatch,
    ManifestMissing,
    ManifestMismatch
}

// Why a catalog was rejected, deterministically: the rule that failed, the file
// it was read from, the zero-based record index (when one record is at fault)
// and the reason. The whole catalog is rejected; nothing is partially loaded.
public sealed class DiagnosticEvidenceRejectedException : Exception
{
    public DiagnosticEvidenceRejectedException(
        EvidenceRejection rule,
        string? sourceFile,
        int? recordIndex,
        string reason,
        Exception? inner = null)
        : base(Format(rule, sourceFile, recordIndex, reason), inner)
    {
        Rule = rule;
        SourceFile = sourceFile;
        RecordIndex = recordIndex;
        Reason = reason;
    }

    public EvidenceRejection Rule { get; }

    public string? SourceFile { get; }

    public int? RecordIndex { get; }

    public string Reason { get; }

    private static string Format(EvidenceRejection rule, string? file, int? index, string reason) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Diagnostic evidence rejected [{rule}] file='{file ?? "<memory>"}' record={(index is null ? "-" : index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))}: {reason}");
}

// Shipped, compact RPCN diagnostic evidence (Data/rpcn-evidence.json). It is
// optional, read-only and generated from closed evidence sources; the runtime
// never crawls anything. Loading is fail-closed: a file that would let a
// community record read as official, a scope or unresolved record attach to a
// game, or a source role carry another role's claim is rejected as a whole.
public sealed class DiagnosticEvidenceCatalog
{
    public const int SchemaVersion = 1;
    public const string FileName = "rpcn-evidence.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILookup<string, DiagnosticEvidenceRecord> _byWikiPage;

    private DiagnosticEvidenceCatalog(DiagnosticEvidenceDocument document)
    {
        Document = document;
        _byWikiPage = document.Records
            .Where(record => record.Applicability == EvidenceApplicability.GameScoped && record.Subject.WikiPage is not null)
            .ToLookup(record => record.Subject.WikiPage!, StringComparer.Ordinal);
    }

    public static DiagnosticEvidenceCatalog Empty { get; } = new(
        new DiagnosticEvidenceDocument(SchemaVersion, string.Empty, null, []));

    public DiagnosticEvidenceDocument Document { get; }

    public IReadOnlyList<DiagnosticEvidenceRecord> Records => Document.Records;

    public bool IsEmpty => Document.Records.Count == 0;

    public const string RecordSemantics =
        "Each record is one dated claim about one source entry (a browser page or a tracker row), not one game. " +
        "Use SubjectsByRole for distinct source entries; a game can have several records.";

    // The manifest is derived from the records and sources only, so it is
    // deterministic and can be re-derived and checked on load.
    public static DiagnosticEvidenceManifest BuildManifest(
        IReadOnlyList<DiagnosticEvidenceRecord> records,
        IReadOnlyList<DiagnosticEvidenceSource> sources,
        string generator,
        string generatorVersion)
    {
        static SortedDictionary<string, int> Count<T>(IEnumerable<T> items, Func<T, string> key)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var name = key(item);
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }

            return counts;
        }

        return new DiagnosticEvidenceManifest(
            SchemaVersion,
            generator,
            generatorVersion,
            RecordSemantics,
            sources.Select(source => source.SnapshotDate).Where(date => date is not null).Max() ?? string.Empty,
            [.. sources.OrderBy(source => source.SourceId, StringComparer.Ordinal)],
            Count(records, record => record.Role.ToString()),
            Count(records, record => record.Claim.ToString()),
            Count(records, record => $"{record.Role}/{record.Subject.Identity}"),
            Count(
                records.Select(record => (record.Role, record.Provenance)).Distinct(),
                pair => pair.Role.ToString()));
    }

    public static DiagnosticEvidenceCatalog FromRecords(
        string source,
        IEnumerable<DiagnosticEvidenceRecord> records,
        DiagnosticEvidenceManifest? manifest = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        var document = new DiagnosticEvidenceDocument(SchemaVersion, source, manifest, [.. records]);
        Validate(document, null, manifestRequired: manifest is not null);
        return new DiagnosticEvidenceCatalog(document);
    }

    public static DiagnosticEvidenceCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = R3Bound.ReadBytes(path, R3Limits.PatCatBy, "diagnostic evidence catalog");

        DiagnosticEvidenceDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<DiagnosticEvidenceDocument>(bytes, Options);
        }
        catch (JsonException ex)
        {
            throw new DiagnosticEvidenceRejectedException(EvidenceRejection.InvalidJson, path, null, ex.Message, ex);
        }

        if (document is null || document.Schema != SchemaVersion || document.Records is null)
        {
            throw new DiagnosticEvidenceRejectedException(
                EvidenceRejection.UnsupportedSchema,
                path,
                null,
                $"the catalog is empty, invalid, or not schema {SchemaVersion}.");
        }

        Validate(document, path, manifestRequired: true);
        return new DiagnosticEvidenceCatalog(document);
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(Document, Options).Replace("\r\n", "\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // Evidence attached to one Wiki game page, by exact page title only. Scope,
    // continuation and unresolved records are never returned here.
    public IReadOnlyList<DiagnosticEvidenceRecord> ForWikiPage(string? wikiPage) =>
        string.IsNullOrEmpty(wikiPage) ? [] : [.. _byWikiPage[wikiPage]];

    public IReadOnlyList<DiagnosticEvidenceRecord> InRole(EvidenceSourceRole role) =>
        [.. Records.Where(record => record.Role == role)];

    private static bool IsCommunityRole(EvidenceSourceRole role) =>
        role is EvidenceSourceRole.CommunityPrivateServerIndex
            or EvidenceSourceRole.GithubIssue
            or EvidenceSourceRole.ForumThread;

    private static bool IsCommunityClaim(EvidenceClaimType claim) =>
        claim is EvidenceClaimType.CommunityWorkingOnline
            or EvidenceClaimType.CommunityRequiresCfw
            or EvidenceClaimType.CommunityContinuation;

    private static bool IsBrowserClaim(EvidenceClaimType claim) =>
        claim is EvidenceClaimType.CurrentBrowserListing
            or EvidenceClaimType.CurrentBrowserOnlineNow
            or EvidenceClaimType.CurrentBrowserPeak24h
            or EvidenceClaimType.CurrentBrowserPeakAllTime;

    private static void Validate(DiagnosticEvidenceDocument document, string? file, bool manifestRequired)
    {
        for (var index = 0; index < document.Records.Count; index++)
        {
            var record = document.Records[index];
            DiagnosticEvidenceRejectedException Reject(EvidenceRejection rule, string reason) =>
                new(rule, file, index, $"{reason} (SourceId={record?.SourceId}, Provenance={record?.Provenance})");

            if (record is null ||
                record.Subject is null ||
                string.IsNullOrWhiteSpace(record.SourceId) ||
                string.IsNullOrWhiteSpace(record.Provenance) ||
                !Enum.IsDefined(record.Role) ||
                !Enum.IsDefined(record.Level) ||
                !Enum.IsDefined(record.Claim) ||
                !Enum.IsDefined(record.Applicability))
            {
                throw Reject(EvidenceRejection.IncompleteRecord, "The record lacks a subject, source id, provenance, or a defined role/level/claim/applicability.");
            }

            // A community, issue or forum source is never official evidence,
            // and a private-server index is never stronger than anecdotal.
            if (IsCommunityRole(record.Role) && record.Level == EvidenceLevel.Official)
            {
                throw Reject(EvidenceRejection.CommunityMarkedOfficial, $"A {record.Role} record cannot be Official.");
            }

            if (record.Role == EvidenceSourceRole.CommunityPrivateServerIndex &&
                record.Level is not (EvidenceLevel.Anecdotal or EvidenceLevel.Unknown))
            {
                throw Reject(EvidenceRejection.PrivateServerIndexTooStrong, $"A private-server index record cannot be {record.Level}; the maximum is Anecdotal.");
            }

            if (!IsCommunityRole(record.Role) && record.Level != EvidenceLevel.Official)
            {
                throw Reject(EvidenceRejection.OfficialSourceNotOfficial, $"An {record.Role} record must be Official, not {record.Level}.");
            }

            // A claim belongs to the one source role that can make it; a
            // browser observation is never an official RPCN test.
            var claimMatchesRole = IsBrowserClaim(record.Claim)
                ? record.Role == EvidenceSourceRole.OfficialCurrentBrowser
                : !IsCommunityClaim(record.Claim) || record.Role == EvidenceSourceRole.CommunityPrivateServerIndex;
            if (!claimMatchesRole)
            {
                throw Reject(EvidenceRejection.ClaimNotAllowedForRole, $"Role {record.Role} cannot make the claim {record.Claim}.");
            }

            var subject = record.Subject;
            var consistent = subject.Identity switch
            {
                SubjectIdentityState.Resolved =>
                    !string.IsNullOrWhiteSpace(subject.WikiPage) && record.Applicability == EvidenceApplicability.GameScoped,
                SubjectIdentityState.CommunityScope =>
                    subject.WikiPage is null && record.Applicability == EvidenceApplicability.ScopeOnly,
                SubjectIdentityState.ContinuationMetadata =>
                    subject.WikiPage is null &&
                    !string.IsNullOrWhiteSpace(subject.ParentKey) &&
                    record.Applicability == EvidenceApplicability.NotApplicable,
                SubjectIdentityState.Unresolved =>
                    subject.WikiPage is null && record.Applicability == EvidenceApplicability.Unresolved,
                _ => false
            };
            if (!consistent)
            {
                throw Reject(
                    EvidenceRejection.IdentityApplicabilityMismatch,
                    $"Identity {subject.Identity} cannot have applicability {record.Applicability} with WikiPage='{subject.WikiPage}' and ParentKey='{subject.ParentKey}'.");
            }
        }

        ValidateManifest(document, file, manifestRequired);
    }

    private static void ValidateManifest(DiagnosticEvidenceDocument document, string? file, bool required)
    {
        var manifest = document.Manifest;
        if (manifest is null)
        {
            if (required)
            {
                throw new DiagnosticEvidenceRejectedException(EvidenceRejection.ManifestMissing, file, null, "The catalog carries no manifest.");
            }

            return;
        }

        var expected = BuildManifest(document.Records, manifest.Sources ?? [], manifest.Generator, manifest.GeneratorVersion);

        static bool Same(IReadOnlyDictionary<string, int>? left, IReadOnlyDictionary<string, int> right) =>
            left is not null &&
            left.Count == right.Count &&
            right.All(pair => left.TryGetValue(pair.Key, out var value) && value == pair.Value);

        string? mismatch =
            manifest.SchemaVersion != SchemaVersion ? "SchemaVersion"
            : !Same(manifest.RecordsByRole, expected.RecordsByRole) ? "RecordsByRole"
            : !Same(manifest.RecordsByClaim, expected.RecordsByClaim) ? "RecordsByClaim"
            : !Same(manifest.RecordsByIdentity, expected.RecordsByIdentity) ? "RecordsByIdentity"
            : !Same(manifest.SubjectsByRole, expected.SubjectsByRole) ? "SubjectsByRole"
            : manifest.LatestSnapshotDate != expected.LatestSnapshotDate ? "LatestSnapshotDate"
            : null;
        if (mismatch is not null)
        {
            throw new DiagnosticEvidenceRejectedException(
                EvidenceRejection.ManifestMismatch,
                file,
                null,
                $"The manifest field {mismatch} does not match the records it describes.");
        }
    }
}

#pragma warning restore IDE0130
