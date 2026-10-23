using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// Domain 5 diagnostic evidence: source role / evidence level semantics, the
// shipped RPCN evidence artifact (current RPCN browser + PS3 private-server
// tracker), fail-closed loading, and the source-separated facts the Runtime
// Context derives from it. Evidence never applies anything.
internal static class DiagnosticEvidenceT
{
    private static readonly Regex SourceRow = new(@"#SourceRow=(\d+)$", RegexOptions.CultureInvariant);

    public static void Run(string root)
    {
        var shipped = LoadShipped();

        AssertShippedBrowserObservations(shipped);
        AssertShippedCommunityIdentity(shipped);
        AssertEvidenceNeverPromotedOrApplied(shipped);
        AssertFailClosedLoad(shipped, root);
        AssertManifestAndRejectionDiagnostics(shipped, root);
        AssertRuntimeFacts(shipped);
        AssertPresentation(shipped);

        Console.WriteLine("Diagnostic evidence (roles, levels, browser observations, community identity fail-closed, no promotion/auto-apply, runtime facts, presentation) PASS.");
    }

    private static string ShippedPath => Path.Combine(AppContext.BaseDirectory, "Data", DiagnosticEvidenceCatalog.FileName);

    private static DiagnosticEvidenceCatalog LoadShipped()
    {
        Assert(File.Exists(ShippedPath), "The build output must carry Data/rpcn-evidence.json.");
        return DiagnosticEvidenceCatalog.Load(ShippedPath);
    }

    private static int RowOf(DiagnosticEvidenceRecord record)
    {
        var match = SourceRow.Match(record.Provenance);
        Assert(match.Success, $"A community record keeps its source row in provenance: {record.Provenance}");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AssertShippedBrowserObservations(DiagnosticEvidenceCatalog catalog)
    {
        var browser = catalog.InRole(EvidenceSourceRole.OfficialCurrentBrowser);
        var listings = browser.Where(record => record.Claim == EvidenceClaimType.CurrentBrowserListing).ToArray();

        Assert(listings.Length == 68 &&
               listings.Select(record => record.Subject.CommId).Distinct(StringComparer.Ordinal).Count() == 68 &&
               listings.Select(record => record.Subject.Title).Distinct(StringComparer.Ordinal).Count() == 67,
            "The current RPCN browser contributes 68 unique CommIds over 67 unique titles.");
        Assert(listings.GroupBy(record => record.Subject.Title, StringComparer.Ordinal).Single(group => group.Count() > 1).Count() == 2,
            "A title with two CommIds (a duplicate title) keeps both CommIds as separate records.");
        Assert(listings.Sum(record => record.Value!.Split(',').Length) == 194,
            "All 194 region badges are kept.");
        Assert(browser.Count == 68 * 4 &&
               browser.All(record => record.Level == EvidenceLevel.Official &&
                                     record.SourceId == "rpcs3-rpcn-current-browser" &&
                                     record.SourceUri is not null &&
                                     record.SnapshotDate is { Length: 10 } &&
                                     record.Provenance.StartsWith("RPCN-Browser-Current/", StringComparison.Ordinal)),
            "Every browser record carries its source, URI, snapshot date and provenance.");

        // Online/peak values are dated observations of the browser, never an
        // RPCN test and never a stand-in for one.
        Assert(catalog.InRole(EvidenceSourceRole.OfficialRpcnTest).Count == 0 &&
               browser.All(record => IsBrowserClaim(record.Claim)),
            "A current-browser observation is never an official RPCN test: the artifact holds no OfficialRpcnTest record.");
        Assert(browser.Where(record => record.Subject.Identity == SubjectIdentityState.Unresolved)
                   .Select(record => record.Subject.CommId).Distinct(StringComparer.Ordinal).Count() == 5,
            "Five browser titles have no deterministic Wiki identity and stay Unresolved rather than being fuzzy-linked.");
    }

    private static bool IsBrowserClaim(EvidenceClaimType claim) =>
        claim is EvidenceClaimType.CurrentBrowserListing
            or EvidenceClaimType.CurrentBrowserOnlineNow
            or EvidenceClaimType.CurrentBrowserPeak24h
            or EvidenceClaimType.CurrentBrowserPeakAllTime;

    private static void AssertShippedCommunityIdentity(DiagnosticEvidenceCatalog catalog)
    {
        var community = catalog.InRole(EvidenceSourceRole.CommunityPrivateServerIndex);
        var rows = community.GroupBy(RowOf).ToDictionary(group => group.Key, group => group.ToArray());

        Assert(rows.Count == 98, "All 98 raw source rows are represented.");
        RowSubject(rows, SubjectIdentityState.Resolved, 60);
        RowSubject(rows, SubjectIdentityState.CommunityScope, 7);
        RowSubject(rows, SubjectIdentityState.Unresolved, 27);
        RowSubject(rows, SubjectIdentityState.ContinuationMetadata, 4);

        var resolved = rows.Values.Where(group => group[0].Subject.Identity == SubjectIdentityState.Resolved).ToArray();
        Assert(resolved.Count(group => group[0].Subject.HasOfficialRpcnTest) == 47 &&
               resolved.Count(group => !group[0].Subject.HasOfficialRpcnTest) == 13,
            "60 resolved identities: 47 linked to an official RPCN test and 13 identity-resolved without one, exactly that.");
        Assert(resolved.SelectMany(group => group).All(record => record.Subject.WikiPage is not null &&
                                                                 record.Level == EvidenceLevel.Anecdotal &&
                                                                 record.Role == EvidenceSourceRole.CommunityPrivateServerIndex),
            "A resolved identity without an official RPCN test stays community/anecdotal; identity does not upgrade evidence.");

        var unresolved = rows.Values.Where(group => group[0].Subject.Identity == SubjectIdentityState.Unresolved).SelectMany(group => group).ToArray();
        Assert(unresolved.All(record => record.Subject.WikiPage is null &&
                                        record.Subject.IdentityMethod == "NO_DETERMINISTIC_WIKI_IDENTITY" &&
                                        record.Applicability == EvidenceApplicability.Unresolved),
            "The 27 unresolved community entries stay fail-closed with no Wiki identity.");

        var scope = rows.Values.Where(group => group[0].Subject.Identity == SubjectIdentityState.CommunityScope).Select(group => group[0]).ToArray();
        Assert(scope.Count(record => record.Subject.ScopeType == "COMMUNITY_ARCHIVE") == 3 &&
               scope.Count(record => record.Subject.ScopeType == "COMMUNITY_COMPOSITE") == 3 &&
               scope.Count(record => record.Subject.ScopeType == "COMMUNITY_UGC") == 1 &&
               scope.All(record => record.Subject.WikiPage is null && record.Applicability == EvidenceApplicability.ScopeOnly),
            "Archive, composite and user-created-content records are scope records; none is forced onto one game.");

        var continuation = rows.Values.Where(group => group[0].Subject.Identity == SubjectIdentityState.ContinuationMetadata).Select(group => group[0]).ToArray();
        Assert(continuation.Select(record => record.Subject.ParentKey).OrderBy(key => key, StringComparer.Ordinal)
                   .SequenceEqual(["row:11", "row:11", "row:2", "row:8"]) &&
               continuation.Select(record => record.Subject.Title).OrderBy(title => title, StringComparer.Ordinal)
                   .SequenceEqual(["Demon's Souls", "Playstation Home", "Playstation Home", "Warhawk"]) &&
               continuation.All(record => record.Claim == EvidenceClaimType.CommunityContinuation &&
                                          record.Applicability == EvidenceApplicability.NotApplicable),
            "Continuation rows point at their parent row and carry no claim of their own.");

        // 98 raw rows - 4 continuation rows = 94 independent titled entries.
        Assert(rows.Count - continuation.Length == 94,
            "Continuation metadata is not counted as an independent game.");

        Assert(catalog.ForWikiPage(null).Count == 0 && catalog.ForWikiPage("").Count == 0 &&
               community.Where(record => record.Subject.Identity != SubjectIdentityState.Resolved)
                   .All(record => catalog.ForWikiPage(record.Subject.Title).All(found => !ReferenceEquals(found, record))),
            "Scope, continuation and unresolved records are never returned for a game page.");
    }

    private static void RowSubject(Dictionary<int, DiagnosticEvidenceRecord[]> rows, SubjectIdentityState state, int expected)
    {
        var actual = rows.Values.Count(group => group.Select(record => record.Subject.Identity).Distinct().Single() == state);
        Assert(actual == expected, $"{state}: expected {expected} source rows, found {actual}.");
    }

    private static void AssertEvidenceNeverPromotedOrApplied(DiagnosticEvidenceCatalog catalog)
    {
        Assert(catalog.Records.All(record => !record.AutoApply && !record.MayMutateSettings),
            "No evidence record can auto-apply or mutate settings.");
        foreach (var name in new[] { nameof(DiagnosticEvidenceRecord.AutoApply), nameof(DiagnosticEvidenceRecord.MayMutateSettings) })
        {
            var property = typeof(DiagnosticEvidenceRecord).GetProperty(name)!;
            Assert(property.CanRead && property.GetSetMethod(nonPublic: true) is null,
                $"{name} has no setter, so it cannot be turned on by data or code.");
        }

        Assert(catalog.Records.Where(record => record.Role == EvidenceSourceRole.CommunityPrivateServerIndex)
                   .All(record => record.Level == EvidenceLevel.Anecdotal),
            "Community private-server claims are Anecdotal; agreement with the Wiki never raises them.");
        Assert(catalog.Records.All(record => !string.IsNullOrWhiteSpace(record.SourceId) &&
                                             !string.IsNullOrWhiteSpace(record.Provenance) &&
                                             record.SnapshotDate is not null),
            "Every record keeps its source id, provenance and snapshot date.");
        Assert(catalog.InRole(EvidenceSourceRole.GithubIssue).Count == 0 && catalog.InRole(EvidenceSourceRole.ForumThread).Count == 0,
            "Forum and issue evidence is not part of the shipped RPCN artifact (Forums capture is still in progress).");
    }

    private static DiagnosticEvidenceRecord Record(
        EvidenceSourceRole role = EvidenceSourceRole.CommunityPrivateServerIndex,
        EvidenceLevel level = EvidenceLevel.Anecdotal,
        EvidenceClaimType claim = EvidenceClaimType.CommunityWorkingOnline,
        SubjectIdentityState identity = SubjectIdentityState.Resolved,
        EvidenceApplicability applicability = EvidenceApplicability.GameScoped,
        string? wikiPage = "Synthetic Game",
        string? parentKey = null) =>
        new(role, level, "synthetic-source", null, "2026-01-01",
            new EvidenceSubject(identity, "TEST", "Synthetic Game", wikiPage, ParentKey: parentKey),
            claim, "Yes", applicability, "synthetic#1");

    private static void AssertRejected(DiagnosticEvidenceRecord record, string because)
    {
        var rejected = false;
        try
        {
            DiagnosticEvidenceCatalog.FromRecords("test", [record]);
        }
        catch (DiagnosticEvidenceRejectedException)
        {
            rejected = true;
        }

        Assert(rejected, because);
    }

    private static void AssertFailClosedLoad(DiagnosticEvidenceCatalog shipped, string root)
    {
        Assert(DiagnosticEvidenceCatalog.FromRecords("test", [Record()]).ForWikiPage("Synthetic Game").Count == 1,
            "A consistent community record loads and is found by its exact Wiki page.");
        AssertRejected(Record(level: EvidenceLevel.Official), "A community record cannot be Official.");
        AssertRejected(Record(role: EvidenceSourceRole.GithubIssue, level: EvidenceLevel.Official, claim: EvidenceClaimType.CommunityWorkingOnline),
            "An issue record cannot be Official.");
        AssertRejected(Record(level: EvidenceLevel.DeveloperConfirmed), "A private-server index cannot be stronger than Anecdotal.");
        AssertRejected(Record(role: EvidenceSourceRole.OfficialCurrentBrowser, level: EvidenceLevel.Anecdotal, claim: EvidenceClaimType.CurrentBrowserListing),
            "An official-source record must be Official.");
        AssertRejected(Record(role: EvidenceSourceRole.OfficialCurrentBrowser, level: EvidenceLevel.Official, claim: EvidenceClaimType.CommunityWorkingOnline),
            "The official browser cannot carry a community claim.");
        AssertRejected(Record(claim: EvidenceClaimType.CurrentBrowserListing),
            "A community source cannot make a current-browser claim.");
        AssertRejected(Record(role: EvidenceSourceRole.OfficialRpcnTest, level: EvidenceLevel.Official, claim: EvidenceClaimType.CurrentBrowserOnlineNow),
            "A browser observation cannot be filed as an official RPCN test.");
        AssertRejected(Record(identity: SubjectIdentityState.Resolved, wikiPage: null), "A resolved identity needs a Wiki page.");
        AssertRejected(Record(identity: SubjectIdentityState.Unresolved, applicability: EvidenceApplicability.Unresolved),
            "An unresolved record cannot name a Wiki page.");
        AssertRejected(Record(identity: SubjectIdentityState.Unresolved, applicability: EvidenceApplicability.GameScoped, wikiPage: null),
            "An unresolved record cannot be game-scoped.");
        AssertRejected(Record(identity: SubjectIdentityState.CommunityScope, applicability: EvidenceApplicability.ScopeOnly),
            "A scope record cannot name a single game.");
        AssertRejected(Record(identity: SubjectIdentityState.ContinuationMetadata, applicability: EvidenceApplicability.NotApplicable, wikiPage: null),
            "Continuation metadata needs its parent row.");

        var text = File.ReadAllText(ShippedPath);
        var directory = Path.Combine(root, "DiagnosticEvidence");
        Directory.CreateDirectory(directory);

        var promoted = Path.Combine(directory, "promoted.json");
        var index = text.IndexOf("\"Level\": \"Anecdotal\"", StringComparison.Ordinal);
        Assert(index > 0, "The shipped artifact carries community records to mutate for the negative test.");
        File.WriteAllText(promoted, text.Remove(index, "\"Level\": \"Anecdotal\"".Length).Insert(index, "\"Level\": \"Official\""));
        var rejected = false;
        try
        {
            DiagnosticEvidenceCatalog.Load(promoted);
        }
        catch (DiagnosticEvidenceRejectedException)
        {
            rejected = true;
        }

        Assert(rejected, "A shipped file that promotes a community record to Official is rejected as a whole.");

        var forced = Path.Combine(directory, "forced-auto-apply.json");
        File.WriteAllText(forced, text.Replace("\"Records\": [", "\"Records\": [", StringComparison.Ordinal)
            .Replace("\"Claim\": \"CurrentBrowserListing\",", "\"Claim\": \"CurrentBrowserListing\", \"AutoApply\": true, \"MayMutateSettings\": true,", StringComparison.Ordinal));
        Assert(DiagnosticEvidenceCatalog.Load(forced).Records.All(record => !record.AutoApply && !record.MayMutateSettings),
            "AutoApply in a data file is ignored: it stays false.");

        var broken = Path.Combine(directory, "broken.json");
        File.WriteAllText(broken, "{ not json");
        var invalid = false;
        try
        {
            DiagnosticEvidenceCatalog.Load(broken);
        }
        catch (DiagnosticEvidenceRejectedException)
        {
            invalid = true;
        }

        Assert(invalid && DiagnosticEvidenceCatalog.Empty.IsEmpty && shipped.Records.Count > 0,
            "A corrupt file is invalid data; the empty catalog stays empty and provides no evidence.");
    }

    private static DiagnosticEvidenceRejectedException Rejected(string path)
    {
        try
        {
            DiagnosticEvidenceCatalog.Load(path);
        }
        catch (DiagnosticEvidenceRejectedException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected the catalog at {path} to be rejected.");
    }

    private static void AssertManifestAndRejectionDiagnostics(DiagnosticEvidenceCatalog catalog, string root)
    {
        var manifest = catalog.Document.Manifest;
        Assert(manifest is not null && manifest.SchemaVersion == 1 &&
               manifest.Generator.Contains("gen-rpcn-evidence", StringComparison.Ordinal) &&
               manifest.GeneratorVersion.Length > 0 &&
               manifest.LatestSnapshotDate == "2026-09-19",
            "The artifact describes itself: schema, generator identity and version, newest source snapshot (no wall-clock time, so generation stays deterministic).");
        Assert(manifest!.Sources.Count == 2 &&
               manifest.Sources.All(source => source.SnapshotDate is { Length: 10 } &&
                                              source.InputHashes.Count >= 2 &&
                                              source.InputHashes.Values.All(hash => hash.Length == 64)),
            "Every source lists its snapshot date and the SHA-256 of each input file.");
        Assert(manifest.RecordsByRole.Values.Sum() == catalog.Records.Count &&
               manifest.RecordsByClaim.Values.Sum() == catalog.Records.Count &&
               manifest.RecordsByIdentity.Values.Sum() == catalog.Records.Count,
            "Record counts by role, claim and identity each add up to the record count.");
        Assert(manifest.SubjectsByRole.Values.Sum() == 68 + 98 && catalog.Records.Count > manifest.SubjectsByRole.Values.Sum() &&
               manifest.RecordSemantics.Contains("not one game", StringComparison.Ordinal),
            "Records are claim rows, not games: 68 browser entries + 98 tracker rows are the subjects behind the records.");

        var directory = Path.Combine(root, "DiagnosticEvidenceRejections");
        Directory.CreateDirectory(directory);
        var text = File.ReadAllText(ShippedPath);

        string Write(string name, string content)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, content);
            return path;
        }

        var promotedPath = Write("promoted.json", text.Replace("\"Level\": \"Anecdotal\"", "\"Level\": \"Official\"", StringComparison.Ordinal));
        var promoted = Rejected(promotedPath);
        Assert(promoted.Rule == EvidenceRejection.CommunityMarkedOfficial &&
               promoted.SourceFile == promotedPath &&
               promoted.RecordIndex is >= 0 &&
               promoted.Message.Contains("[CommunityMarkedOfficial]", StringComparison.Ordinal) &&
               promoted.Message.Contains(promotedPath, StringComparison.Ordinal) &&
               promoted.Message.Contains("Provenance=", StringComparison.Ordinal),
            "A rejection names the rule, the file, the offending record and its provenance.");
        Assert(Rejected(promotedPath).Message == promoted.Message,
            "The rejection message is deterministic.");

        var mismatch = Rejected(Write("mismatch.json", text.Replace("\"CurrentBrowserListing\": 68", "\"CurrentBrowserListing\": 69", StringComparison.Ordinal)));
        Assert(mismatch.Rule == EvidenceRejection.ManifestMismatch && mismatch.RecordIndex is null &&
               mismatch.Reason.Contains("RecordsByClaim", StringComparison.Ordinal),
            "A manifest that disagrees with its records is rejected and names the field.");

        var noManifest = System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
        noManifest.Remove("Manifest");
        Assert(Rejected(Write("no-manifest.json", noManifest.ToJsonString())).Rule == EvidenceRejection.ManifestMissing,
            "A shipped catalog without a manifest is rejected.");
        Assert(Rejected(Write("schema.json", text.Replace("\"Schema\": 1", "\"Schema\": 2", StringComparison.Ordinal))).Rule == EvidenceRejection.UnsupportedSchema &&
               Rejected(Write("bad.json", "{ not json")).Rule == EvidenceRejection.InvalidJson,
            "An unsupported schema and invalid JSON are distinct, named rejections.");

        Assert(Rejected(Write("claim.json", text.Replace("\"Claim\": \"CommunityWorkingOnline\"", "\"Claim\": \"CurrentBrowserListing\"", StringComparison.Ordinal))).Rule
                   == EvidenceRejection.ClaimNotAllowedForRole,
            "A community source cannot carry a current-browser claim.");

        // A rejected file never loads partially: the use case yields the empty
        // catalog plus the rejection reason for the developer.
        var baseDir = Path.Combine(root, "DiagnosticEvidenceUseCase");
        Directory.CreateDirectory(Path.Combine(baseDir, "Data"));
        File.Copy(promotedPath, Path.Combine(baseDir, "Data", DiagnosticEvidenceCatalog.FileName), overwrite: true);
        var rejectedLoad = new WikiEvidenceUseCase().LoadDiagnosticEvidence(baseDir);
        Assert(rejectedLoad.Catalog.IsEmpty && rejectedLoad.Error is not null &&
               rejectedLoad.Error.Contains("[CommunityMarkedOfficial]", StringComparison.Ordinal),
            "A rejected catalog loads as empty (no partial load) and reports the deterministic reason.");
        File.Copy(ShippedPath, Path.Combine(baseDir, "Data", DiagnosticEvidenceCatalog.FileName), overwrite: true);
        var good = new WikiEvidenceUseCase().LoadDiagnosticEvidence(baseDir);
        Assert(!good.Catalog.IsEmpty && good.Error is null &&
               new WikiEvidenceUseCase().LoadDiagnosticEvidence(Path.Combine(root, "DiagnosticEvidenceAbsent")) is { Error: null, Catalog.IsEmpty: true },
            "The shipped catalog loads without error; an absent file is the normal no-evidence state, not an error.");
    }

    private static readonly Rpcs3GameDescriptor Game = new("TEST00002", "Synthetic", string.Empty, null, null, null);

    private static RuntimeContextSnapshot Compat(string? wikiPage, DiagnosticEvidenceCatalog? evidence) =>
        RuntimeContextBuilder.Build(
            null,
            null,
            null,
            Game,
            new GameSettingsModel("TEST00002", wikiPage, [], [], []),
            null,
            evidence: evidence);

    private static void AssertRuntimeFacts(DiagnosticEvidenceCatalog shipped)
    {
        var both = shipped.Records
            .Where(record => record.Applicability == EvidenceApplicability.GameScoped)
            .GroupBy(record => record.Subject.WikiPage!, StringComparer.Ordinal)
            .First(group => group.Any(record => record.Role == EvidenceSourceRole.OfficialCurrentBrowser) &&
                            group.Any(record => record.Role == EvidenceSourceRole.CommunityPrivateServerIndex &&
                                                record.Claim == EvidenceClaimType.CommunityWorkingOnline &&
                                                record.Value is not null));
        var snapshot = Compat(both.Key, shipped);

        var browser = snapshot.Facts.Where(fact => fact.Key == "RpcnCurrentBrowser").ToArray();
        Assert(browser.Length >= 1 &&
               browser.All(fact => fact.Group == RuntimeFactGroup.Compatibility &&
                                   fact.State == EvidenceState.Known &&
                                   fact.Code == "CurrentBrowserObservation" &&
                                   fact.Value!.StartsWith("NPWR", StringComparison.Ordinal)),
            "The official browser listing is shown as a dated observation with its CommId, not as a test result.");
        var community = snapshot.Facts.Where(fact => fact.Key is "RpcnCommunity" or "RpcnCommunityCfw").ToArray();
        Assert(community.Length >= 1 &&
               community.All(fact => fact.State == EvidenceState.Partial && fact.Code == "CommunityIndexOnly"),
            "Community index claims are never Known or Exact and are labelled community-only.");
        Assert(snapshot.Find("Rpcn")!.Code == "NoRpcnEvidence" && snapshot.Find("Rpcn")!.State == EvidenceState.Unknown &&
               snapshot.Find("TestedBuild") is null,
            "Community/browser evidence does not create an RPCN test result or a tested build.");
        Assert(snapshot.Facts.All(fact => fact.Key != "Fix" && fact.Key != "RecommendedSetting" && fact.Key != "RequiredBuild"),
            "No fix, recommended setting or required build is invented from source evidence.");

        var unresolvedTitle = shipped.Records.First(record => record.Subject.Identity == SubjectIdentityState.Unresolved).Subject.Title;
        var scopeTitle = shipped.Records.First(record => record.Subject.Identity == SubjectIdentityState.CommunityScope).Subject.Title;
        foreach (var page in new[] { unresolvedTitle, scopeTitle, null, "Not A Wiki Page" })
        {
            Assert(!Compat(page, shipped).Facts.Any(fact => fact.Key.StartsWith("RpcnC", StringComparison.Ordinal)),
                "An unresolved, scope-only or unknown page gets no source evidence facts.");
        }

        Assert(!Compat(both.Key, null).Facts.Any(fact => fact.Key.StartsWith("RpcnC", StringComparison.Ordinal)) &&
               !Compat(both.Key, DiagnosticEvidenceCatalog.Empty).Facts.Any(fact => fact.Key.StartsWith("RpcnC", StringComparison.Ordinal)),
            "Without an evidence catalog the snapshot has no source evidence facts (absent evidence stays absent).");

        // Identity-resolved without an official RPCN test stays exactly that.
        var noTest = shipped.Records.First(record => record.Role == EvidenceSourceRole.CommunityPrivateServerIndex &&
                                                     record.Subject.Identity == SubjectIdentityState.Resolved &&
                                                     !record.Subject.HasOfficialRpcnTest);
        Assert(Compat(noTest.Subject.WikiPage, shipped).Find("RpcnCommunity") is { State: EvidenceState.Partial } &&
               Compat(noTest.Subject.WikiPage, shipped).Find("Rpcn")!.State == EvidenceState.Unknown,
            "A resolved identity without an official RPCN test shows community evidence only; RPCN evidence stays Unknown.");
    }

    private static void AssertPresentation(DiagnosticEvidenceCatalog shipped)
    {
        var page = shipped.Records.First(record => record.Role == EvidenceSourceRole.CommunityPrivateServerIndex &&
                                                    record.Subject.Identity == SubjectIdentityState.Resolved &&
                                                    record.Claim == EvidenceClaimType.CommunityWorkingOnline &&
                                                    record.Value is not null).Subject.WikiPage;
        var snapshot = Compat(page, shipped);

        var vm = new RuntimeVm();
        try
        {
            foreach (var lang in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(lang);
                vm.Load(snapshot);
                var compatibility = vm.Groups.Single(group => group.Group == RuntimeFactGroup.Compatibility);
                var row = compatibility.SummaryRows.First(item => item.Id == "RpcnCommunity");
                Assert(!row.Label.StartsWith("Runtime.Fact.", StringComparison.Ordinal) &&
                       row.IsTechnical && row.HasState && row.StateText.Length > 0 &&
                       row.HasNote && !row.Note.StartsWith("Runtime.Code.", StringComparison.Ordinal),
                    $"The community evidence row is labelled, stateful and explained ({lang}); its value is raw LTR text.");
            }

            UiText.SetLang(UiText.EnLang);
            vm.SetLang();
            Assert(vm.Groups.Single(group => group.Group == RuntimeFactGroup.Compatibility)
                       .SummaryRows.First(item => item.Id == "RpcnCommunity").Note.Contains("not official", StringComparison.Ordinal),
                "The English note states that the community claim is not official evidence.");
        }
        finally
        {
            UiText.SetLang(UiText.EnLang);
        }
    }
}
