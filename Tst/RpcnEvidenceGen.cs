using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using R3Config.Mdl;
using R3Config.Svc;

// Development-only generator of the shipped App/Data/rpcn-evidence.json from
// the closed RPCN evidence sources: the current RPCN browser normalization and
// the unified community-final canonical set. It applies no matching of its own
// (identity is taken as the closed sources recorded it; anything they did not
// resolve stays Unresolved), promotes nothing, and the product never reads the
// source folders.
//
//   R3Test.exe --gen-rpcn-evidence <browser-dir> <unified-dir> <tracker-dir> <out.json>
internal static class RpcnEvidenceGen
{
    private const string BrowserSourceId = "rpcs3-rpcn-current-browser";
    private const string BrowserUri = "https://rpcs3.net/rpcn";
    private const string CommunitySourceId = "ps3-private-server-tracker";
    private const string GeneratorName = "R3Test --gen-rpcn-evidence";
    private const string GeneratorVersion = "1";

    public static void Run(string browserDir, string unifiedDir, string trackerDir, string outPath)
    {
        var records = new List<DiagnosticEvidenceRecord>();
        var browserCount = AddBrowser(records, browserDir, unifiedDir);
        var communityCount = AddCommunity(records, unifiedDir, trackerDir);

        DiagnosticEvidenceSource SourceOf(string id, EvidenceSourceRole role, string? uri, params string[] inputs) => new(
            id,
            role,
            uri,
            records.Where(record => record.SourceId == id).Select(record => record.SnapshotDate).Where(date => date is not null).Max(),
            inputs.ToDictionary(
                input => Path.GetFileName(input),
                input => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))),
                StringComparer.Ordinal));

        var communityUri = records.First(record => record.SourceId == CommunitySourceId).SourceUri;
        var sources = new[]
        {
            SourceOf(
                BrowserSourceId,
                EvidenceSourceRole.OfficialCurrentBrowser,
                BrowserUri,
                Path.Combine(browserDir, "rpcn-current-normalized.json"),
                Path.Combine(browserDir, "manifest-rendered-v2.json"),
                Path.Combine(unifiedDir, "browser-wiki-final-linkage.json"),
                Path.Combine(unifiedDir, "rpcn-unified-exact.json")),
            SourceOf(
                CommunitySourceId,
                EvidenceSourceRole.CommunityPrivateServerIndex,
                communityUri,
                Path.Combine(unifiedDir, "community-final-canonical.json"),
                Path.Combine(trackerDir, "capture.json"))
        };

        var manifest = DiagnosticEvidenceCatalog.BuildManifest(records, sources, GeneratorName, GeneratorVersion);
        DiagnosticEvidenceCatalog
            .FromRecords(
                "Compact projection of closed RPCN evidence (current RPCN browser, PS3 private-server tracker). Every record keeps its source role and level; nothing is auto-applied. Regenerate with R3Test --gen-rpcn-evidence; do not edit by hand.",
                records,
                manifest)
            .Save(outPath);

        Console.WriteLine($"records: {records.Count} (claim rows, not games)");
        Console.WriteLine($"browser entries: {browserCount}; community source rows: {communityCount}");
        foreach (var (key, value) in manifest.SubjectsByRole)
        {
            Console.WriteLine($"subjects {key}: {value}");
        }
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? OptStr(JsonElement element, string name)
    {
        var text = Str(element, name);
        return text.Length == 0 ? null : text;
    }

    private static int AddBrowser(List<DiagnosticEvidenceRecord> records, string browserDir, string unifiedDir)
    {
        using var normalized = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(browserDir, "rpcn-current-normalized.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(browserDir, "manifest-rendered-v2.json")));
        using var linkage = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(unifiedDir, "browser-wiki-final-linkage.json")));
        using var exact = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(unifiedDir, "rpcn-unified-exact.json")));

        var captured = manifest.RootElement.GetProperty("pages").EnumerateArray()
            .ToDictionary(page => Str(page, "commId"), page => Str(page, "capturedAt"), StringComparer.Ordinal);

        // Identity exactly as the closed sources recorded it, by CommId.
        var identity = new Dictionary<string, (string? Page, string Method, bool HasTest)>(StringComparer.Ordinal);
        foreach (var row in linkage.RootElement.EnumerateArray())
        {
            var resolved = Str(row, "IdentityStatus") == "RESOLVED";
            foreach (var commId in row.GetProperty("CommIds").EnumerateArray().Select(id => id.GetString()!))
            {
                identity[commId] = (
                    resolved ? OptStr(row, "CanonicalWikiTitle") : null,
                    Str(row, "IdentityMethod"),
                    resolved && row.GetProperty("HasOfficialRpcnTest").GetBoolean());
            }
        }

        foreach (var row in exact.RootElement.EnumerateArray())
        {
            if (!row.GetProperty("HasCurrentBrowser").GetBoolean() || !row.GetProperty("HasWikiTest").GetBoolean())
            {
                continue;
            }

            var wikiRecords = row.GetProperty("WikiRecords").EnumerateArray().ToArray();
            var titles = wikiRecords.Select(item => Str(item, "Title")).Distinct(StringComparer.Ordinal).ToArray();
            if (titles.Length != 1)
            {
                throw new InvalidDataException($"Ambiguous Wiki title for unified row '{Str(row, "TitleKey")}'.");
            }

            foreach (var browser in row.GetProperty("CurrentBrowserRecords").EnumerateArray())
            {
                identity[Str(browser, "commId")] = (titles[0], Str(row, "MatchMethod"), true);
            }
        }

        var count = 0;
        foreach (var record in normalized.RootElement.EnumerateArray().OrderBy(item => Str(item, "commId"), StringComparer.Ordinal))
        {
            var commId = Str(record, "commId");
            var title = Str(record, "gameTitle");
            var known = identity.TryGetValue(commId, out var link);
            var resolved = known && link.Page is not null;
            var subject = new EvidenceSubject(
                resolved ? SubjectIdentityState.Resolved : SubjectIdentityState.Unresolved,
                known ? link.Method : "NO_DETERMINISTIC_WIKI_IDENTITY",
                title,
                resolved ? link.Page : null,
                commId,
                HasOfficialRpcnTest: resolved && link.HasTest);
            var applicability = resolved ? EvidenceApplicability.GameScoped : EvidenceApplicability.Unresolved;
            var date = captured.TryGetValue(commId, out var stamp) && stamp.Length >= 10 ? stamp[..10] : null;
            var provenance = $"RPCN-Browser-Current/rpcn-current-normalized.json#commId={commId}";

            DiagnosticEvidenceRecord Make(EvidenceClaimType claim, string? value) => new(
                EvidenceSourceRole.OfficialCurrentBrowser,
                EvidenceLevel.Official,
                BrowserSourceId,
                $"{BrowserUri}-game?comm_id={commId}",
                date,
                subject,
                claim,
                value,
                applicability,
                provenance);

            var regions = record.GetProperty("regions").EnumerateArray().Select(item => item.GetString()!).OrderBy(x => x, StringComparer.Ordinal);
            records.Add(Make(EvidenceClaimType.CurrentBrowserListing, string.Join(',', regions)));
            records.Add(Make(EvidenceClaimType.CurrentBrowserOnlineNow, OptStr(record.GetProperty("onlineNow"), "rawValue")));
            records.Add(Make(EvidenceClaimType.CurrentBrowserPeak24h, OptStr(record.GetProperty("peak24h"), "rawValue")));
            records.Add(Make(EvidenceClaimType.CurrentBrowserPeakAllTime, OptStr(record.GetProperty("peakAllTime"), "rawValue")));
            count++;
        }

        return count;
    }

    private static int AddCommunity(List<DiagnosticEvidenceRecord> records, string unifiedDir, string trackerDir)
    {
        using var capture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(trackerDir, "capture.json")));
        using var canonical = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(unifiedDir, "community-final-canonical.json")));

        var uri = Str(capture.RootElement, "sourceUrl");
        var stamp = Str(capture.RootElement, "capturedAt");
        var date = stamp.Length >= 10 ? stamp[..10] : null;

        var count = 0;
        foreach (var row in canonical.RootElement.EnumerateArray().OrderBy(item => item.GetProperty("SourceRow").GetInt32()))
        {
            var sourceRow = row.GetProperty("SourceRow").GetInt32();
            var role = Str(row, "RecordRole");
            var status = Str(row, "IdentityStatus");
            var method = Str(row, "IdentityMethod");

            var subject = (role, status) switch
            {
                ("TITLED_ENTRY", "RESOLVED") => new EvidenceSubject(
                    SubjectIdentityState.Resolved,
                    method,
                    OptStr(row, "CommunityTitle"),
                    OptStr(row, "CanonicalWikiTitle") ?? throw new InvalidDataException($"Resolved row {sourceRow} has no Wiki title."),
                    HasOfficialRpcnTest: row.GetProperty("HasOfficialRpcnTest").GetBoolean()),
                ("TITLED_ENTRY", "UNRESOLVED_FAIL_CLOSED") => new EvidenceSubject(
                    SubjectIdentityState.Unresolved,
                    method,
                    OptStr(row, "CommunityTitle")),
                ("COMMUNITY_SCOPE", _) => new EvidenceSubject(
                    SubjectIdentityState.CommunityScope,
                    method,
                    OptStr(row, "CommunityTitle"),
                    ScopeType: OptStr(row, "CommunityScopeType")),
                ("CONTINUATION_METADATA", _) => new EvidenceSubject(
                    SubjectIdentityState.ContinuationMetadata,
                    method,
                    OptStr(row, "ParentCommunityTitle"),
                    ParentKey: $"row:{row.GetProperty("ParentSourceRow").GetInt32()}"),
                _ => throw new InvalidDataException($"Unexpected community row role/status '{role}/{status}' at row {sourceRow}.")
            };

            var applicability = subject.Identity switch
            {
                SubjectIdentityState.Resolved => EvidenceApplicability.GameScoped,
                SubjectIdentityState.CommunityScope => EvidenceApplicability.ScopeOnly,
                SubjectIdentityState.ContinuationMetadata => EvidenceApplicability.NotApplicable,
                _ => EvidenceApplicability.Unresolved
            };

            var source = row.GetProperty("CommunityRecord");
            var provenance = $"RPCN-Unified/community-final-canonical.json#SourceRow={sourceRow}";

            DiagnosticEvidenceRecord Make(EvidenceClaimType claim, string? value) => new(
                EvidenceSourceRole.CommunityPrivateServerIndex,
                EvidenceLevel.Anecdotal,
                CommunitySourceId,
                uri.Length == 0 ? null : uri,
                date,
                subject,
                claim,
                value,
                applicability,
                provenance);

            if (subject.Identity == SubjectIdentityState.ContinuationMetadata)
            {
                records.Add(Make(EvidenceClaimType.CommunityContinuation, null));
            }
            else
            {
                var working = OptStr(source, "WorkingOnline");
                var cfw = OptStr(source, "RequiresCfw");
                if (working is not null || cfw is null)
                {
                    records.Add(Make(EvidenceClaimType.CommunityWorkingOnline, working));
                }

                if (cfw is not null)
                {
                    records.Add(Make(EvidenceClaimType.CommunityRequiresCfw, cfw));
                }
            }

            count++;
        }

        return count;
    }
}
