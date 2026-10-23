using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// Recommendation database sync regression suite. Every network call goes to
// FakeHttp; fixtures are a real subset of the live config API response and
// MediaWiki-shaped Wiki responses (Tst/Fixtures/RecSync/NOTICE.md). Each case
// uses its own temporary store folder; nothing touches LocalAppData or RPCS3.
internal static class RecSyncT
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ProcessedRevisions = ["61001", "61002", "61003"];
    private static readonly Lazy<RecSvc> Shipped = new(() => RecSvc.Load(Path.Combine(Root(), "App", "Data", "cat.json")));

    public static void Run(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "RecSync");
        Directory.CreateDirectory(root);

        ParserContract();
        HttpBoundary();
        GitHubForkData(Path.Combine(root, "github"));
        var first = FirstSync(Path.Combine(root, "first"));
        NoChange(Path.Combine(root, "nochange"));
        ConfirmedEdits(Path.Combine(root, "edits"));
        SourceConflict();
        Failures(Path.Combine(root, "failures"));
        StaleResponse(Path.Combine(root, "stale"));
        LocalDatabaseFallback(Path.Combine(root, "local-db"));
        PartialConfig();
        Rpcs3DataRoot(Path.Combine(root, "data-root"));
        LocalDatabaseOfflineBackoffAndWriter(Path.Combine(root, "local-db-io"));
        WikiRecentChanges(Path.Combine(root, "wiki"));
        WikiFullReconciliation(Path.Combine(root, "wiki-full"));
        PreferencesAndNotifications(Path.Combine(root, "prefs"));
        Cancellation(Path.Combine(root, "cancel"));
        Concurrency(Path.Combine(root, "concurrent"));
        ReopenAndRecovery(Path.Combine(root, "recovery"));
        BaselineUpgrade(Path.Combine(root, "upgrade"));
        Rpcs3FilesUntouched(Path.Combine(root, "rpcs3"));
        PlanNeverAutoSelectsSync(first);

        Console.WriteLine("Recommendation sync regression PASS: GitHub bundle update/unchanged/hash rejection/network/404/Last Known Good; API/Wiki parser, edits/conflicts, 403/429/500/network/invalid/partial, stale responses, read-only RPCS3 config_database.dat fallback, partial configs, data-root resolution, Wiki pagination/reconciliation, preferences, cancellation, concurrency, recovery, baseline upgrade, RPCS3 files byte-identical.");
    }

    // Deterministic rows for the UI gate: long titles, both dispositions.
    public static RecSyncStoreData UiSample()
    {
        var at = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        RecSyncChange Row(RecSyncChangeKind kind, string serial, string title, string section, string key, string? old, string? value, RecSyncDisposition disposition, RecSyncReason reason, RecSyncSource source) =>
            RecSyncMerge.Change(source, kind, serial, title, section, key, old, value, disposition, reason, "1792479016", source == RecSyncSource.Wiki ? RecSyncMerge.WikiPageUrl + Uri.EscapeDataString(title) : RecSyncMerge.ConfigApiUrl, at);

        var empty = RecSyncStoreData.Empty("SAMPLE", "2026-09-20");
        return empty with
        {
            Sources =
            [
                new RecSyncSourceState(RecSyncSource.ConfigApi, RecSyncSourceStatus.Updated, at, at, null, 0, null, null, "A35F37C4", "1792479016", at, "OK."),
                new RecSyncSourceState(RecSyncSource.Wiki, RecSyncSourceStatus.Blocked, at, null, at.AddHours(24), 1, null, null, null, null, null, "The source requires a browser challenge; automated access is not permitted."),
                new RecSyncSourceState(RecSyncSource.GitHub, RecSyncSourceStatus.NotPublished, at, null, at.AddHours(1), 1, null, null, null, null, null, "A technical 404 detail must stay hidden in the normal interface.")
            ],
            History =
            [
                Row(RecSyncChangeKind.Added, "BLUS31465", "Assassin's Creed Rogue", "Video", "Write Color Buffers", null, "true", RecSyncDisposition.Applied, RecSyncReason.None, RecSyncSource.ConfigApi),
                Row(RecSyncChangeKind.Updated, "BLES00229", "Grand Theft Auto IV", "Core", "SPU Block Size", "Safe", "Mega", RecSyncDisposition.Applied, RecSyncReason.None, RecSyncSource.Wiki),
                Row(RecSyncChangeKind.Added, "BLES00978", "Dragon Ball: Raging Blast 2 - An Unusually Long Title For Layout", "VFS", "Emulate BD-ROM Read Speed", null, "true", RecSyncDisposition.Quarantined, RecSyncReason.UnsupportedSetting, RecSyncSource.ConfigApi),
                Row(RecSyncChangeKind.Removed, "NPUA70112", "Heavy Rain", "Video", "Frame limit", "Off", null, RecSyncDisposition.Applied, RecSyncReason.None, RecSyncSource.ConfigApi)
            ]
        };
    }

    private static void ParserContract()
    {
        var snapshot = ConfigApiParser.Parse(Fixture("config-api-v1.json"));
        Assert(snapshot.Games.Count == 120 && snapshot.Issues.Count == 0 && snapshot.Timestamp == 1792479016,
            "The live-format config API fixture must parse completely.");
        var nested = snapshot.Games.Single(game => game.Serial == "BCAS20066").Settings;
        Assert(nested.Any(setting => setting.Section == "Video" && setting.Key == "Vulkan.Asynchronous Texture Streaming" && setting.Value == "false") &&
               nested.Any(setting => setting.Section == "Core" && setting.Key == "SPU Block Size" && setting.Value == "Mega"),
            "Nested RPCS3 YAML maps must flatten to the catalog's dotted key.");
        Assert(snapshot.Games.Single(game => game.Serial == "NPUA70218").Settings
                   .Any(setting => setting.Key == "Libraries Control" && setting.Value == "libvdec.sprx:lle"),
            "LLE library sequences must become the catalog's library value.");

        foreach (var (text, why) in new[]
                 {
                     ("Video:\n  MSAA: {a: b}", "flow mapping"),
                     ("Core:\n  - libvdec.sprx:lle", "orphan sequence"),
                     ("Unknown:\n  Key: value", "unknown section"),
                     ("Video:\n\tMSAA: Disabled", "tab indentation"),
                     ("Video:\n   MSAA: Disabled", "odd indentation"),
                     ("Video:\n  MSAA: Disabled\n  MSAA: Auto", "duplicate key"),
                     ("Video: Disabled", "top-level scalar"),
                     ("Video:\n  MSAA: &anchor Disabled", "anchor")
                 })
        {
            Assert(!ConfigApiParser.TryParseConfig(text, out _, out _), $"Config YAML with {why} must be rejected.");
        }

        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(Encoding.UTF8.GetBytes("{\"return_code\":1,\"timestamp\":1,\"games\":{}}")), "A non-zero return_code must be rejected.");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(Encoding.UTF8.GetBytes("<html>challenge</html>")), "Non-JSON must be rejected.");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(Encoding.UTF8.GetBytes("{\"return_code\":0,\"return_code\":0,\"timestamp\":1,\"games\":{}}")), "Duplicate JSON keys must be rejected.");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(ApiBody(games => Keep(games, 50))), "A truncated response with too few games must be rejected.");
        var full = Fixture("config-api-v1.json");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(full[..(full.Length / 2)]), "A cut-off JSON body must be rejected.");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(ApiBody(games =>
        {
            foreach (var serial in games.Select(item => item.Key).Take(10).ToArray())
            {
                games[serial] = new JsonObject { ["config"] = "Video:\n  MSAA: {bad}" };
            }
        })), "A response with more than 5% invalid games must be rejected.");

        // A lone UTF-16 surrogate escape is valid JSON syntax but not text; it
        // must be a rejected response, not an unexpected exception that aborts the run.
        var fixture = Encoding.UTF8.GetString(Fixture("config-api-v1.json"));
        var surrogate = fixture.Replace("\"games\": {", "\"games\": {\"BLUS00000\":{\"config\":\"Video:\\n  MSAA: \\uD800\"},", StringComparison.Ordinal);
        Assert(surrogate != fixture, "The fixture must contain the games object.");
        MustFail<InvalidDataException>(() => ConfigApiParser.Parse(Encoding.UTF8.GetBytes(surrogate)), "A lone surrogate escape must be rejected as invalid data.");

        var oneBad = ConfigApiParser.Parse(ApiBody(games => games["BCAS20066"] = new JsonObject { ["config"] = "Video:\n  MSAA: {bad}" }));
        Assert(oneBad.Games.Count == 119 && oneBad.Issues.Single().Serial == "BCAS20066",
            "One unreadable game must be reported, not fail the response.");

        Assert(WikiSvc.IsSyncValue("Video", "Write Color Buffers", "true") &&
               !WikiSvc.IsSyncValue("Video", "Write Color Buffers", "maybe") &&
               WikiSvc.IsSyncValue("Video", "Frame limit", "Infinite") &&
               !WikiSvc.IsSyncValue("Video", "Frame limit", "Turbo") &&
               WikiSvc.IsSyncValue("Core", "Max SPURS Threads", "4") &&
               !WikiSvc.IsSyncValue("Core", "Max SPURS Threads", "four") &&
               WikiSvc.IsSyncValue("Core", "Libraries Control", "libaudio.sprx:lle, libvdec.sprx:lle") &&
               !WikiSvc.IsSyncValue("Core", "Libraries Control", "libvdec.sprx:hle") &&
               !WikiSvc.IsSyncValue("VFS", "Emulate HDD Read Speed", "true"),
            "Sync values must come from verified domains only.");
    }

    private static void HttpBoundary()
    {
        foreach (var url in new[] { "http://api.rpcs3.net/config/?api=v1", "https://example.com/", "https://api.rpcs3.net:8443/", "https://user@api.rpcs3.net/", "https://rpcs3.net.evil.example/" })
        {
            Assert(!RecSyncHttp.IsAllowed(new Uri(url)), $"Only official HTTPS hosts are allowed: {url}");
        }

        var fake = new FakeHttp((_, _) => Task.FromResult(Json(Fixture("config-api-v1.json"))));
        using var http = new RecSyncHttp(fake, "test", NoDelay);
        var refused = http.GetAsync(new Uri("https://example.com/x"), 1024, "application/json", null, null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(refused.Status == RecSyncHttpStatus.Rejected && fake.Requests.Count == 0, "A disallowed URL must be refused before any request.");

        var redirect = new FakeHttp((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://example.com/");
            return Task.FromResult(response);
        });
        using var redirectHttp = new RecSyncHttp(redirect, "test", NoDelay);
        Assert(redirectHttp.GetAsync(new Uri(RecSyncMerge.ConfigApiUrl), 1024, "application/json", null, null, CancellationToken.None).GetAwaiter().GetResult().Status == RecSyncHttpStatus.Rejected,
            "Redirects must not be followed.");

        var large = new FakeHttp((_, _) => Task.FromResult(Json(new byte[4096])));
        using var largeHttp = new RecSyncHttp(large, "test", NoDelay);
        Assert(largeHttp.GetAsync(new Uri(RecSyncMerge.ConfigApiUrl), 1024, "application/json", null, null, CancellationToken.None).GetAwaiter().GetResult().Status == RecSyncHttpStatus.TooLarge,
            "Responses over the size limit must be refused.");

        var seen = fake.Requests;
        var ok = new FakeHttp((request, _) => Task.FromResult(Json(Fixture("config-api-v1.json"))));
        using var okHttp = new RecSyncHttp(ok, "1.2.3", NoDelay);
        _ = okHttp.GetAsync(new Uri(RecSyncMerge.ConfigApiUrl), ConfigApiParser.MaxResponseBytes, "application/json", "\"v1\"", null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(ok.Requests.Single().UserAgent == "R3Config/1.2.3 (recommendation-sync)" && ok.Requests.Single().IfNoneMatch == "\"v1\"" && ok.Requests.Single().Method == "GET",
            "Requests carry only the product User-Agent and cache validators.");
        _ = seen;
    }

    private static void GitHubForkData(string folder)
    {
        Directory.CreateDirectory(folder);
        var baseline = Shipped.Value;
        var unavailable = Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            GitHubFixture);
        var (first, firstFake) = Make(Path.Combine(folder, "store"), unavailable);
        using (first)
        {
            var report = Sync(first, baseline, manual: true);
            var accepted = first.Read(baseline).Data;
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Updated &&
                   accepted?.GitHubData?.Version == "20261020120000" &&
                   accepted.GitHubData.Recommendations.Single().Serial == "BLUS31465" &&
                   accepted.GitHubData.Notes.Single().NoteArabic == "ملاحظة اختبارية" &&
                   RecSyncMerge.BuildEffective(baseline, accepted).Find("BLUS31465")?.Changes.Single().Origin == RecOrigin.Sync &&
                   firstFake.Requests.Count(request => request.Uri.Host == "raw.githubusercontent.com") == 3,
                "A valid GitHub manifest and both hash-verified files must update the local effective data.");
        }

        var (unchanged, unchangedFake) = Make(Path.Combine(folder, "store"), unavailable);
        using (unchanged)
        {
            var report = Sync(unchanged, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Unchanged &&
                   unchangedFake.Requests.Count(request => request.Uri.Host == "raw.githubusercontent.com") == 1 &&
                   unchanged.Read(baseline).Data?.GitHubData?.Version == "20261020120000",
                "An unchanged manifest must retain the Last Known Good bundle without re-downloading its files.");
        }

        var (corrupt, _) = Make(Path.Combine(folder, "store"), Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            request => request.Uri.AbsolutePath.EndsWith("manifest.json", StringComparison.Ordinal)
                ? Json(ChangedGitHubManifest())
                : request.Uri.AbsolutePath.EndsWith("recommendations.json", StringComparison.Ordinal)
                    ? Json(Fixture("github-recommendations.json"))
                    : Json(CorruptFixture("github-notes-ar.json"))));
        using (corrupt)
        {
            var report = Sync(corrupt, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Invalid &&
                   corrupt.Read(baseline).Data?.GitHubData?.Version == "20261020120000",
                "A corrupt linked file must be rejected while preserving the prior Last Known Good bundle.");
        }

        var (badSchema, _) = Make(Path.Combine(folder, "store"), Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            _ => Json(InvalidGitHubSchema())));
        using (badSchema)
        {
            var report = Sync(badSchema, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Invalid &&
                   badSchema.Read(baseline).Data?.GitHubData?.Version == "20261020120000",
                "An unsupported GitHub manifest schema must be rejected without replacing Last Known Good.");
        }

        var (badVersion, _) = Make(Path.Combine(folder, "store"), Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            request => request.Uri.AbsolutePath.EndsWith("manifest.json", StringComparison.Ordinal)
                ? Json(GitHubVersionMismatchManifest())
                : request.Uri.AbsolutePath.EndsWith("recommendations.json", StringComparison.Ordinal)
                    ? Json(GitHubVersionMismatchRecommendations())
                    : Json(Fixture("github-notes-ar.json"))));
        using (badVersion)
        {
            var report = Sync(badVersion, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Invalid &&
                   badVersion.Read(baseline).Data?.GitHubData?.Version == "20261020120000",
                "A payload version that differs from the manifest must be rejected while preserving Last Known Good.");
        }

        var (offline, _) = Make(Path.Combine(folder, "store"), Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            _ => throw new HttpRequestException("offline", null, null)));
        using (offline)
        {
            var report = Sync(offline, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.Unavailable &&
                   offline.Read(baseline).Data?.GitHubData?.Version == "20261020120000",
                "A network interruption must keep the Last Known Good GitHub bundle unchanged.");
        }

        File.WriteAllText(Path.Combine(folder, "store", RecSyncStore.FileName), "{ damaged store");
        var (recoveredStore, _) = Make(Path.Combine(folder, "store"), unavailable);
        using (recoveredStore)
        {
            var (data, health) = recoveredStore.Read(baseline);
            Assert(health == RecSyncStoreHealth.RecoveredFromBackup && data?.GitHubData?.Version == "20261020120000",
                "A damaged main store must restore the previously accepted GitHub bundle from Last Known Good.");
        }

        var (notPublished, _) = Make(Path.Combine(folder, "not-published"), Respond(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Challenge(),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using (notPublished)
        {
            var report = Sync(notPublished, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.GitHub) == RecSyncSourceStatus.NotPublished &&
                   SourceStatus(report, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                   SourceStatus(report, RecSyncSource.Wiki) == RecSyncSourceStatus.Blocked &&
                   notPublished.Read(baseline).Data?.GitHubData is null,
                "A missing GitHub manifest must report the unpublished database state without stopping other sources.");
        }
    }

    private static HttpResponseMessage GitHubFixture(FakeRequest request) => request.Uri.AbsolutePath switch
    {
        "/HAKAMIQ/rpcs3/master/r3config-data/manifest.json" => Json(Fixture("github-manifest.json")),
        "/HAKAMIQ/rpcs3/master/r3config-data/recommendations.json" => Json(Fixture("github-recommendations.json")),
        "/HAKAMIQ/rpcs3/master/r3config-data/notes-ar.json" => Json(Fixture("github-notes-ar.json")),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound)
    };

    private static byte[] CorruptFixture(string name)
    {
        var bytes = Fixture(name);
        bytes[0] = bytes[0] == (byte)'{' ? (byte)'[' : (byte)'{';
        return bytes;
    }

    private static byte[] ChangedGitHubManifest()
    {
        var manifest = JsonNode.Parse(Fixture("github-manifest.json"))!.AsObject();
        manifest["version"] = "20261020120100";
        manifest["generatedAt"] = "2026-10-20T12:01:00+00:00";
        return Encoding.UTF8.GetBytes(manifest.ToJsonString());
    }

    private static byte[] InvalidGitHubSchema()
    {
        var manifest = JsonNode.Parse(Fixture("github-manifest.json"))!.AsObject();
        manifest["schema"] = 2;
        return Encoding.UTF8.GetBytes(manifest.ToJsonString());
    }

    private static byte[] GitHubVersionMismatchRecommendations()
    {
        var recommendations = JsonNode.Parse(Fixture("github-recommendations.json"))!.AsObject();
        recommendations["dataVersion"] = "20261020120001";
        return Encoding.UTF8.GetBytes(recommendations.ToJsonString());
    }

    private static byte[] GitHubVersionMismatchManifest()
    {
        var recommendations = GitHubVersionMismatchRecommendations();
        var manifest = JsonNode.Parse(Fixture("github-manifest.json"))!.AsObject();
        var file = manifest["files"]!.AsArray().Single(item => item!["path"]!.GetValue<string>() == "recommendations.json")!;
        file["sizeBytes"] = recommendations.Length;
        file["sha256"] = Convert.ToHexString(SHA256.HashData(recommendations));
        return Encoding.UTF8.GetBytes(manifest.ToJsonString());
    }

    private static RecSyncStoreData FirstSync(string folder)
    {
        var baseline = Shipped.Value;
        var (service, fake) = Make(folder, Respond(api: _ => Json(Fixture("config-api-v1.json"), etag: "\"v1\"")));
        using (service)
        {
            var report = Sync(service, baseline, manual: false);
            Assert(report.Status == RecSyncRunStatus.Partial &&
                   SourceStatus(report, RecSyncSource.Wiki) == RecSyncSourceStatus.Blocked &&
                   SourceStatus(report, RecSyncSource.ConfigApi) is RecSyncSourceStatus.Updated or RecSyncSourceStatus.Unchanged,
                "The first sync must update from the config API while the blocked Wiki is reported as Partial.");

            var (data, _) = service.Read(baseline);
            Assert(data is not null, "The first sync must persist the store.");
            var effective = RecSyncMerge.BuildEffective(baseline, data);
            var applied = report.NewChanges.Where(change => change.Disposition == RecSyncDisposition.Applied).ToArray();
            Assert(applied.Length > 0 && applied.All(change =>
                    change.Kind == RecSyncChangeKind.Added &&
                    change.Source == RecSyncSource.ConfigApi &&
                    baseline.Find(change.Serial) is { Binding: WikiGameIdRole.Primary } rec &&
                    rec.Changes.All(item => !(item.Section == change.Section && item.Key == change.Key)) &&
                    WikiSvc.IsSyncValue(change.Section, change.Key, change.NewValue!)),
                "A first sync may only add supported, valid values to Primary serials that lacked them.");

            foreach (var rec in baseline.Rec)
            {
                var after = effective.Find(rec.Serial)!;
                Assert(rec.Changes.All(change => after.Changes.Any(item => item.Section == change.Section && item.Key == change.Key && item.Value == change.Value)),
                    $"A first sync must never remove or change a baseline recommendation ({rec.Serial}).");
            }

            Assert(effective.Count == baseline.Count, "The config API alone must not invent games without a proven identity.");

            var snapshot = ConfigApiParser.Parse(Fixture("config-api-v1.json"));
            foreach (var game in snapshot.Games)
            {
                foreach (var setting in game.Settings)
                {
                    var value = effective.Find(game.Serial)?.Changes.FirstOrDefault(change => change.Section == setting.Section && change.Key == setting.Key)?.Value;
                    Assert(value == setting.Value || data!.Quarantine.Any(item => item.Serial == game.Serial && item.Section == setting.Section && item.Key == setting.Key),
                        $"Every source value must be applied or quarantined ({game.Serial} {setting.Section}/{setting.Key}).");
                }
            }

            Assert(data!.Quarantine.Any(item => item.Serial == "BLES00978" && item.Section == "VFS" && item.Reason == RecSyncReason.UnsupportedSetting),
                "Settings R3Config does not support must be quarantined.");
            Assert(effective.Rec.SelectMany(rec => rec.Changes).Count(change => change.Origin == RecOrigin.Sync) == applied.Length,
                "Every applied value must be marked as a local sync change.");
            Assert(fake.Requests.All(request => request.Uri.Host is "api.rpcs3.net" or "wiki.rpcs3.net" or "raw.githubusercontent.com") &&
                   fake.Requests.Where(request => request.Uri.Host == "raw.githubusercontent.com").All(request =>
                       request.Uri.AbsolutePath == "/HAKAMIQ/rpcs3/master/r3config-data/manifest.json"),
                "Network access must stay on the two official sources and the fixed GitHub master manifest path.");
            return data;
        }
    }

    private static void NoChange(string folder)
    {
        var baseline = Shipped.Value;
        var calls = 0;
        var (service, fake) = Make(folder, Respond(api: request =>
        {
            calls++;
            return calls == 2 && request.IfNoneMatch == "\"v1\""
                ? new HttpResponseMessage(HttpStatusCode.NotModified)
                : Json(Fixture("config-api-v1.json"), etag: calls == 1 ? "\"v1\"" : "\"v2\"");
        }));
        using (service)
        {
            Sync(service, baseline, manual: true);
            var before = File.ReadAllBytes(Path.Combine(folder, RecSyncStore.FileName));
            var history = service.Read(baseline).Data!.History.Count;

            var notModified = Sync(service, baseline, manual: true);
            Assert(SourceStatus(notModified, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unchanged && notModified.NewChanges.Count == 0,
                "A 304 response must be reported as unchanged.");
            var sameBody = Sync(service, baseline, manual: true);
            Assert(SourceStatus(sameBody, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unchanged && sameBody.NewChanges.Count == 0 &&
                   service.Read(baseline).Data!.History.Count == history,
                "An identical body must not add history.");
            Assert(fake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net") == 3, "Each check makes one config API request.");
            _ = before;
        }
    }

    private static void ConfirmedEdits(string folder)
    {
        var baseline = Shipped.Value;
        var snapshot = ConfigApiParser.Parse(Fixture("config-api-v1.json"));
        var primary = snapshot.Games.Where(game => baseline.Find(game.Serial) is { Binding: WikiGameIdRole.Primary }).ToArray();

        // Update: a value the baseline and the API agree on changes in the API.
        var (updSerial, updSetting) = primary
            .SelectMany(game => game.Settings.Select(setting => (game.Serial, setting)))
            .First(item => item.setting.Section == "Video" && item.setting.Key == "Write Color Buffers" && item.setting.Value == "true" &&
                           baseline.Find(item.Serial)!.Changes.Any(change => change.Key == "Write Color Buffers" && change.Value == "true"));
        // Remove: a baseline value the API listed disappears (the game keeps other settings).
        var (remSerial, remSetting) = primary
            .Where(game => game.Serial != updSerial && game.Settings.Count >= 2)
            .SelectMany(game => game.Settings.Select(setting => (game.Serial, setting)))
            .First(item => item.setting.Key != "Libraries Control" &&
                           baseline.Find(item.Serial)!.Changes.Any(change => change.Section == item.setting.Section && change.Key == item.setting.Key && change.Value == item.setting.Value));
        // Add: a Primary game gains a supported value nobody on its page had.
        var pageKeys = baseline.Rec.GroupBy(rec => rec.WikiName).ToDictionary(group => group.Key, group => group.SelectMany(rec => rec.Changes).Select(change => change.Key).ToHashSet());
        var addSerial = primary
            .Where(game => game.Settings.All(setting => setting.Key is not ("MSAA" or "Strict Rendering Mode")))
            .Select(game => game.Serial)
            .First(serial => serial != updSerial && serial != remSerial &&
                             !pageKeys[baseline.Find(serial)!.WikiName].Contains("Strict Rendering Mode"));

        var (service, _) = Make(folder, Respond(api: _ => Json(Fixture("config-api-v1.json"))));
        using (service)
        {
            Sync(service, baseline, manual: true);
            var edited = ApiBody(games =>
            {
                games[updSerial] = new JsonObject { ["config"] = Edit(Config(games, updSerial), "  Write Color Buffers: true", "  Write Color Buffers: false") };
                games[remSerial] = new JsonObject { ["config"] = RemoveLine(Config(games, remSerial), remSetting) };
                games[addSerial] = new JsonObject { ["config"] = AddVideo(Config(games, addSerial), "Strict Rendering Mode: true\n  MSAA: Turbo") };
            }, timestamp: 1792565416);

            var (service2, _) = Make(folder, Respond(api: _ => Json(edited)));
            using (service2)
            {
                var report = Sync(service2, baseline, manual: true);
                var (data, _) = service2.Read(baseline);
                var effective = RecSyncMerge.BuildEffective(baseline, data);

                Assert(report.NewChanges.Any(change => change.Serial == updSerial && change.Kind == RecSyncChangeKind.Updated && change.OldValue == "true" && change.NewValue == "false" && change.Disposition == RecSyncDisposition.Applied && change.RevisionId == "1792565416") &&
                       effective.Find(updSerial)!.Changes.Single(change => change.Key == "Write Color Buffers") is { Value: "false", Origin: RecOrigin.Sync },
                    "A confirmed value change between two complete responses must update the recommendation.");
                Assert(report.NewChanges.Any(change => change.Serial == remSerial && change.Kind == RecSyncChangeKind.Removed && change.Disposition == RecSyncDisposition.Applied) &&
                       effective.Find(remSerial)!.Changes.All(change => !(change.Section == remSetting.Section && change.Key == remSetting.Key)),
                    "A confirmed removal between two complete responses must remove the recommendation.");
                Assert(effective.Find(addSerial)!.Changes.Any(change => change.Key == "Strict Rendering Mode" && change.Value == "true" && change.Origin == RecOrigin.Sync),
                    "A confirmed addition must add the recommendation.");
                Assert(data!.Quarantine.Any(item => item.Serial == addSerial && item.Key == "MSAA" && item.Reason == RecSyncReason.InvalidValue) &&
                       effective.Find(addSerial)!.Changes.All(change => change.Value != "Turbo"),
                    "An unknown value must stay quarantined.");
                Assert(baseline.Find(remSerial)!.Changes.Any(change => change.Section == remSetting.Section && change.Key == remSetting.Key),
                    "The read-only baseline itself is never edited.");
            }
        }
    }

    private static void SourceConflict()
    {
        var baseline = Shipped.Value;
        var snapshot = ConfigApiParser.Parse(Fixture("config-api-v1.json"));
        var (serial, setting) = snapshot.Games
            .SelectMany(game => game.Settings.Select(item => (game.Serial, item)))
            .First(item => item.item.Key == "Write Color Buffers" && baseline.Find(item.Serial) is { Binding: WikiGameIdRole.Primary } rec &&
                           rec.Changes.Any(change => change.Key == "Write Color Buffers" && change.Value == "true"));
        var rec = baseline.Find(serial)!;
        var wikiRec = rec with
        {
            Changes = [.. rec.Changes.Select(change => change.Key == "Write Color Buffers" ? change with { Value = "false" } : change)]
        };
        var store = RecSyncStoreData.Empty(baseline.Info.SrcHash, baseline.Info.DataVer) with
        {
            WikiPages = [new RecSyncWikiPage(rec.WikiName, 1, "70000", Now, [wikiRec])]
        };

        var merged = RecSyncMerge.ApplyConfigApi(baseline, store, snapshot, Now);
        Assert(merged.Quarantine.Any(item => item.Serial == serial && item.Key == "Write Color Buffers" && item.Reason == RecSyncReason.SourceConflict),
            "A Wiki revision and the config API that disagree must be quarantined as a source conflict.");

        var overridden = store with
        {
            Overrides = [new RecSyncOverride(serial, "Video", "Write Color Buffers", "true", "1", Now)]
        };
        var effective = RecSyncMerge.BuildEffective(baseline, overridden);
        Assert(effective.Find(serial)!.Changes.Single(change => change.Key == "Write Color Buffers").Value == "true" &&
               effective.Find(serial)!.Changes.Single(change => change.Key == "Write Color Buffers").Origin == RecOrigin.Baseline,
            "When both sources changed a setting differently, the baseline value stays in effect.");
        _ = setting;
    }

    private static void Failures(string folder)
    {
        var baseline = Shipped.Value;
        var cases = new (string Name, Func<FakeRequest, HttpResponseMessage> Api, RecSyncSourceStatus Expected, int Attempts)[]
        {
            ("network", _ => throw new HttpRequestException("offline", null, null), RecSyncSourceStatus.Unavailable, 3),
            ("server", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), RecSyncSourceStatus.Unavailable, 3),
            ("forbidden", _ => Challenge(), RecSyncSourceStatus.Blocked, 1),
            ("html", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>", Encoding.UTF8, "text/html") }, RecSyncSourceStatus.Blocked, 1),
            ("invalid-json", _ => Json(Encoding.UTF8.GetBytes("{\"return_code\":0,")), RecSyncSourceStatus.Invalid, 1),
            ("partial", _ => Json(ApiBody(games => Keep(games, 40))), RecSyncSourceStatus.Invalid, 1),
            ("mass-removal", _ => Json(ApiBody(games => Keep(games, 100))), RecSyncSourceStatus.Invalid, 1)
        };

        foreach (var (name, api, expected, attempts) in cases)
        {
            var caseFolder = Path.Combine(folder, name);
            var good = true;
            var (service, fake) = Make(caseFolder, Respond(api: request => good ? Json(Fixture("config-api-v1.json")) : api(request)));
            using (service)
            {
                Sync(service, baseline, manual: true);
                var before = service.Read(baseline).Data!;
                var effectiveBefore = RecSyncMerge.SettingCount(RecSyncMerge.BuildEffective(baseline, before));
                good = false;
                var start = fake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net");

                var report = Sync(service, baseline, manual: true);
                var after = service.Read(baseline).Data!;
                Assert(SourceStatus(report, RecSyncSource.ConfigApi) == expected &&
                       fake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net") - start == attempts,
                    $"Failure '{name}' must be reported as {expected} after {attempts} attempt(s).");
                Assert(JsonSerializer.Serialize(after.ApiSnapshot) == JsonSerializer.Serialize(before.ApiSnapshot) &&
                       JsonSerializer.Serialize(after.Overrides) == JsonSerializer.Serialize(before.Overrides) &&
                       RecSyncMerge.SettingCount(RecSyncMerge.BuildEffective(baseline, after)) == effectiveBefore,
                    $"Failure '{name}' must keep the last good data.");
                Assert(after.State(RecSyncSource.ConfigApi).NextAttemptUtc > Now, $"Failure '{name}' must schedule a later attempt.");
            }
        }

        var limited = Path.Combine(folder, "rate-limited");
        var (rateService, rateFake) = Make(limited, Respond(api: _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
            return response;
        }));
        using (rateService)
        {
            var report = Sync(rateService, baseline, manual: true);
            var state = rateService.Read(baseline).Data!.State(RecSyncSource.ConfigApi);
            Assert(SourceStatus(report, RecSyncSource.ConfigApi) == RecSyncSourceStatus.RateLimited && state.NextAttemptUtc == Now.AddHours(1),
                "429 must honour Retry-After.");
            var again = Sync(rateService, baseline, manual: true);
            Assert(SourceStatus(again, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Skipped &&
                   rateFake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net") == 1,
                "Even a manual check must wait out a source's Retry-After.");
            Assert(!RecSyncService.IsDue(rateService.Read(baseline).Data, Now.AddMinutes(30)),
                "An automatic run is not due while every source asked to wait.");
        }
    }

    // The config API timestamp is its export time. A body older than the
    // accepted snapshot (a stale cache or mirror) must not roll it back.
    private static void StaleResponse(string folder)
    {
        var baseline = Shipped.Value;
        var body = Fixture("config-api-v1.json");
        var (service, _) = Make(folder, Respond(api: _ => Json(body)));
        using (service)
        {
            Sync(service, baseline, manual: true);
            var before = service.Read(baseline).Data!;
            body = ApiBody(games => games.Remove(games.First().Key), timestamp: 1792392616);

            var report = Sync(service, baseline, manual: true);
            var after = service.Read(baseline).Data!;
            Assert(SourceStatus(report, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Invalid &&
                   report.NewChanges.Count == 0 &&
                   after.ApiSnapshot!.Count == 120 &&
                   after.State(RecSyncSource.ConfigApi).RevisionId == "1792479016" &&
                   JsonSerializer.Serialize(after.Overrides) == JsonSerializer.Serialize(before.Overrides),
                "A response older than the accepted snapshot must be refused and keep the last good data.");
        }
    }

    // RPCS3's GuiConfigs/config_database.dat is its verbatim copy of the config
    // API response. It is read only when the official request fails, applied
    // only when strictly newer than the accepted snapshot, and never written.
    private static void LocalDatabaseFallback(string folder)
    {
        var baseline = Shipped.Value;
        var rpcs3 = Path.Combine(folder, "rpcs3");
        var config = Path.Combine(rpcs3, "config");
        var install = new Rpcs3InstallDescriptor(rpcs3, Path.Combine(rpcs3, "rpcs3.exe"), config, Path.Combine(config, "custom_configs"), Path.Combine(rpcs3, "patches"), Path.Combine(config, "patch_config.yml"));
        var database = ConfigApiParser.LocalDatabasePath(install)!;
        Assert(database == Path.Combine(rpcs3, "GuiConfigs", "config_database.dat") &&
               ConfigApiParser.LocalDatabasePath(install with { DataRoot = Path.Combine(rpcs3, "portable") }) == Path.Combine(rpcs3, "portable", "GuiConfigs", "config_database.dat") &&
               ConfigApiParser.LocalDatabasePath(null) is null,
            "The local database is RPCS3's GuiConfigs/config_database.dat under its data root.");

        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "config.yml"), "Video:\n  Resolution Scale: 150\n  Write Color Buffers: false\n");
        var userConfig = Hashes(config);

        // With no accepted snapshot, a copy exported before the shipped
        // baseline is not proven newer and is ignored.
        var stale = ApiBody(_ => { }, timestamp: baseline.Info.GenAt!.Value.AddDays(-1).ToUnixTimeSeconds());
        File.WriteAllBytes(database, stale);
        var (cold, _) = Make(Path.Combine(folder, "store-old"), Respond(api: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using (cold)
        {
            var report = cold.SyncAsync(baseline, true, database, CancellationToken.None).GetAwaiter().GetResult();
            Assert(report.NewChanges.Count == 0 && cold.Read(baseline).Data!.ApiSnapshot is null && File.ReadAllBytes(database).SequenceEqual(stale),
                "A local copy older than the shipped baseline must not seed the first snapshot.");
        }

        var written = Fixture("config-api-v1.json");
        File.WriteAllBytes(database, written);

        Func<FakeRequest, HttpResponseMessage> api = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var (service, _) = Make(Path.Combine(folder, "store"), Respond(api: request => api(request)));
        using (service)
        {
            RecSyncReport Run()
            {
                var report = service.SyncAsync(baseline, true, database, CancellationToken.None).GetAwaiter().GetResult();
                Assert(File.ReadAllBytes(database).SequenceEqual(written), "Syncing must never write RPCS3's config_database.dat.");
                return report;
            }

            RecSyncStoreData Data() => service.Read(baseline).Data!;

            // No accepted snapshot and the API is down: the local copy is used,
            // and the API failure is still reported and retried.
            var first = Run();
            var state = Data().State(RecSyncSource.ConfigApi);
            Assert(SourceStatus(first, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                   first.Applied > 0 &&
                   Data().ApiSnapshot!.Count == 120 &&
                   state.RevisionId == "1792479016" &&
                   state.Sha256 == Convert.ToHexString(SHA256.HashData(written)) &&
                   state.NextAttemptUtc > Now &&
                   state.LastSuccessUtc is null,
                "A failed request may fall back to RPCS3's local copy while still reporting the failure.");
            var effective = RecSyncMerge.BuildEffective(baseline, Data());
            Assert(baseline.Rec.All(rec => rec.Changes.All(change => effective.Find(rec.Serial)!.Changes.Any(item => item.Section == change.Section && item.Key == change.Key && item.Value == change.Value))),
                "The local copy must not change or remove an approved baseline recommendation.");

            // The API then serves the same bytes: same fingerprint, nothing is reprocessed.
            var history = Data().History.Count;
            api = _ => Json(Fixture("config-api-v1.json"));
            var same = Run();
            Assert(SourceStatus(same, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unchanged &&
                   same.NewChanges.Count == 0 &&
                   Data().History.Count == history,
                "An API body identical to the local copy must be recognised by its fingerprint.");

            // The API moves ahead; an older, same-time or damaged local copy is then ignored.
            var newer = ApiBody(games => games.Remove(games.First().Key), timestamp: 1792565416);
            api = _ => Json(newer);
            Run();
            Assert(Data().ApiSnapshot!.Count == 119, "The newer API response must be accepted.");
            api = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            foreach (var (local, why) in new[]
                     {
                         (Fixture("config-api-v1.json"), "older"),
                         (ApiBody(games => Keep(games, 110), timestamp: 1792565416), "same-time"),
                         (Encoding.UTF8.GetBytes("{\"return_code\":0,"), "damaged")
                     })
            {
                written = local;
                File.WriteAllBytes(database, written);
                var before = JsonSerializer.Serialize(Data().ApiSnapshot);
                var report = Run();
                Assert(SourceStatus(report, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                       report.NewChanges.Count == 0 &&
                       JsonSerializer.Serialize(Data().ApiSnapshot) == before &&
                       Data().State(RecSyncSource.ConfigApi).RevisionId == "1792565416",
                    $"A {why} local copy must not replace the accepted snapshot.");
            }

            // A strictly newer local copy is used while the request fails, even
            // while another process holds the file open without write sharing.
            var newest = ApiBody(games =>
            {
                games.Remove(games.First().Key);
                games.Remove(games.First().Key);
            }, timestamp: 1792651816);
            written = newest;
            File.WriteAllBytes(database, written);
            RecSyncReport fallback;
            using (new FileStream(database, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fallback = Run();
            }

            Assert(SourceStatus(fallback, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                   Data().ApiSnapshot!.Count == 118 &&
                   Data().State(RecSyncSource.ConfigApi).RevisionId == "1792651816" &&
                   fallback.NewChanges.All(change => change.RevisionId is null || change.RevisionId == "1792651816") &&
                   fallback.Sources.Single(item => item.Source == RecSyncSource.ConfigApi).Detail!.Contains(ConfigApiParser.LocalDatabaseFileName, StringComparison.Ordinal),
                "A strictly newer local copy must be applied, read-only, when the request fails.");

            // When the request succeeds, the local copy is not a second source.
            written = ApiBody(games => Keep(games, 117), timestamp: 1792738216);
            File.WriteAllBytes(database, written);
            api = _ => Json(newest);
            var online = Run();
            Assert(SourceStatus(online, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unchanged &&
                   Data().ApiSnapshot!.Count == 118 &&
                   Data().State(RecSyncSource.ConfigApi).RevisionId == "1792651816",
                "A successful official response must not be overridden by the local copy.");
        }

        Assert(Hashes(config).SequenceEqual(userConfig) &&
               Directory.EnumerateFiles(rpcs3, "*", SearchOption.AllDirectories).Count() == 2,
            "The fallback must not change user settings (Resolution Scale included) or create RPCS3 files.");
    }

    // RPCS3's cfg_root::validate rejects a game's whole config when one key
    // is unknown to the installed build (2026-10-20: 21 API games carry
    // "SPU MMIO Delay Enabled" or "Emulate BD-ROM Read Speed", which build
    // c673da45 lacks). R3Config cannot write such keys, so it must not apply
    // the rest of that config either: the game is quarantined as a partial
    // config and its earlier API overrides fall back to the baseline.
    private static void PartialConfig()
    {
        var baseline = Shipped.Value;
        var full = ConfigApiParser.Parse(Fixture("config-api-v1.json"));
        var pageKeys = baseline.Rec.GroupBy(rec => rec.WikiName).ToDictionary(group => group.Key, group => group.SelectMany(rec => rec.Changes).Select(change => change.Key).ToHashSet());
        var serial = full.Games
            .Where(game => baseline.Find(game.Serial) is { Binding: WikiGameIdRole.Primary } &&
                           game.Settings.All(setting => WikiSvc.TryGetSyncTarget(setting.Section, setting.Key, out _) && setting.Key != "Strict Rendering Mode"))
            .Select(game => game.Serial)
            .First(item => !pageKeys[baseline.Find(item)!.WikiName].Contains("Strict Rendering Mode"));
        Assert(!WikiSvc.TryGetSyncTarget("Core", "SPU MMIO Delay Enabled", out _),
            "The partial-config fixture needs a key R3Config cannot write.");

        ConfigApiSnapshot Snapshot(bool partial, long timestamp) => ConfigApiParser.Parse(ApiBody(games =>
        {
            var config = AddVideo(Config(games, serial), "Strict Rendering Mode: true");
            games[serial] = new JsonObject
            {
                ["config"] = partial ? config + "\nCore:\n  SPU MMIO Delay Enabled: true" : config
            };
        }, timestamp));
        string? Effective(RecSyncStoreData store) =>
            RecSyncMerge.BuildEffective(baseline, store).Find(serial)!.Changes
                .FirstOrDefault(change => change.Key == "Strict Rendering Mode")?.Value;

        var empty = RecSyncStoreData.Empty(baseline.Info.SrcHash, baseline.Info.DataVer);
        var first = RecSyncMerge.ApplyConfigApi(baseline, empty, Snapshot(partial: true, 1792479016), Now);
        Assert(Effective(first.Store) is null &&
               first.Store.Overrides.All(item => item.Serial != serial) &&
               first.Quarantine.Any(item => item.Serial == serial && item.Key == "Strict Rendering Mode" && item.Reason == RecSyncReason.PartialConfig) &&
               first.Quarantine.Any(item => item.Serial == serial && item.Key == "SPU MMIO Delay Enabled" && item.Reason == RecSyncReason.UnsupportedSetting),
            "A game config with a key R3Config cannot write must be quarantined as partial, not applied in part.");

        var whole = RecSyncMerge.ApplyConfigApi(baseline, first.Store, Snapshot(partial: false, 1792565416), Now);
        Assert(Effective(whole.Store) == "true" &&
               whole.Applied.Any(change => change.Serial == serial && change.Key == "Strict Rendering Mode" && change.Kind == RecSyncChangeKind.Added),
            "Once the same game config is fully supported, its unchanged values must be applied.");

        var again = RecSyncMerge.ApplyConfigApi(baseline, whole.Store, Snapshot(partial: true, 1792651816), Now);
        Assert(Effective(again.Store) is null &&
               again.Store.Overrides.All(item => item.Serial != serial) &&
               again.Applied.Any(change => change.Serial == serial && change.Key == "Strict Rendering Mode" && change.Kind == RecSyncChangeKind.Removed) &&
               again.Quarantine.Any(item => item.Serial == serial && item.Key == "Strict Rendering Mode" && item.Reason == RecSyncReason.PartialConfig) &&
               again.Store.Overrides.Where(item => item.Serial != serial).SequenceEqual(whole.Store.Overrides.Where(item => item.Serial != serial)),
            "A game config that becomes partial must fall back to the baseline without touching other games.");

        // A store saved before this rule can hold an override next to a
        // partial snapshot; the same API body is not merged again ("Same
        // content"), so the effective catalog itself must not apply it.
        var legacy = whole.Store with { ApiSnapshot = again.Store.ApiSnapshot };
        Assert(legacy.Overrides.Any(item => item.Serial == serial) && Effective(legacy) is null,
            "An override stored for a now partial game config must not reach the effective catalog.");
    }

    // RPCS3 (Utilities/File.cpp fs::get_config_dir at c673da45) prefers
    // <exe>/portable/, else takes RPCS3_CONFIG_DIR, turns '\' into '/' and
    // cuts it after the last '/': "C:\cfg\" is C:\cfg, "C:\cfg" is C:\.
    // GuiConfigs and config/custom_configs hang off that directory.
    private static void Rpcs3DataRoot(string folder)
    {
        var rpcs3 = Path.Combine(folder, "rpcs3");
        Directory.CreateDirectory(rpcs3);
        var exe = Path.Combine(rpcs3, "rpcs3.exe");
        File.WriteAllText(exe, string.Empty);
        var configured = Path.Combine(folder, "cfg");
        var previous = Environment.GetEnvironmentVariable("RPCS3_CONFIG_DIR");
        try
        {
            foreach (var (value, expected, why) in new[]
                     {
                         (configured + Path.DirectorySeparatorChar, configured, "a trailing backslash"),
                         (configured.Replace('\\', '/') + "/", configured, "a trailing slash"),
                         (configured, folder, "no trailing separator (RPCS3 keeps the parent)")
                     })
            {
                Environment.SetEnvironmentVariable("RPCS3_CONFIG_DIR", value);
                var install = Rpcs3InstallService.FromExe(exe);
                Assert(install.DataRoot == expected &&
                       install.CustPath == Path.Combine(expected, "config", "custom_configs") &&
                       ConfigApiParser.LocalDatabasePath(install) == Path.Combine(expected, "GuiConfigs", ConfigApiParser.LocalDatabaseFileName),
                    $"RPCS3_CONFIG_DIR with {why} must resolve to RPCS3's own directory ({install.DataRoot}).");
            }

            var portable = Path.Combine(rpcs3, "portable");
            Directory.CreateDirectory(portable);
            Environment.SetEnvironmentVariable("RPCS3_CONFIG_DIR", configured + Path.DirectorySeparatorChar);
            var portableInstall = Rpcs3InstallService.FromExe(exe);
            Assert(portableInstall.DataRoot == portable &&
                   ConfigApiParser.LocalDatabasePath(portableInstall) == Path.Combine(portable, "GuiConfigs", ConfigApiParser.LocalDatabaseFileName),
                "An existing portable folder must win over RPCS3_CONFIG_DIR.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("RPCS3_CONFIG_DIR", previous);
        }
    }

    // RPCS3 rewrites config_database.dat in place (QFile::WriteOnly, which
    // truncates and shares read/write access). R3Config must survive a
    // missing network, honour its back-off, and never take a half-written
    // or locked copy, while never writing the file itself.
    private static void LocalDatabaseOfflineBackoffAndWriter(string folder)
    {
        var baseline = Shipped.Value;
        var database = Path.Combine(folder, "rpcs3", "GuiConfigs", ConfigApiParser.LocalDatabaseFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var written = Fixture("config-api-v1.json");
        File.WriteAllBytes(database, written);

        var (service, fake) = Make(Path.Combine(folder, "store"), Respond(api: _ => throw new HttpRequestException("offline", null, null)));
        using (service)
        {
            int Requests() => fake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net");
            RecSyncReport Run(bool manual, bool locked = false)
            {
                var report = service.SyncAsync(baseline, manual, database, CancellationToken.None).GetAwaiter().GetResult();
                if (!locked)
                {
                    using var check = new FileStream(database, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var copy = new MemoryStream();
                    check.CopyTo(copy);
                    Assert(copy.ToArray().SequenceEqual(written), "Syncing must never write RPCS3's config_database.dat.");
                }

                return report;
            }

            RecSyncStoreData Data() => service.Read(baseline).Data!;

            var offline = Run(manual: false);
            Assert(SourceStatus(offline, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                   Requests() == 3 &&
                   Data().ApiSnapshot!.Count == 120 &&
                   Data().State(RecSyncSource.ConfigApi).NextAttemptUtc > Now,
                "Without a network the local copy must be used while the failure is reported and retried later.");

            // During back-off an automatic run makes no request and does not
            // read the local copy; a manual check retries and may use it.
            written = ApiBody(games => games.Remove(games.First().Key), timestamp: 1792565416);
            File.WriteAllBytes(database, written);
            var waiting = Run(manual: false);
            Assert(SourceStatus(waiting, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Skipped &&
                   Requests() == 3 &&
                   Data().ApiSnapshot!.Count == 120,
                "An automatic run during back-off must not contact the API or apply the local copy.");

            // RPCS3 is rewriting the file: truncated, half written, write handle open.
            var next = ApiBody(games =>
            {
                games.Remove(games.First().Key);
                games.Remove(games.First().Key);
            }, timestamp: 1792651816);
            using (var writer = new FileStream(database, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                writer.Write(next, 0, next.Length / 2);
                writer.Flush();
                written = next[..(next.Length / 2)];
                var torn = Run(manual: true);
                Assert(SourceStatus(torn, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                       Requests() == 6 &&
                       Data().ApiSnapshot!.Count == 120 &&
                       Data().State(RecSyncSource.ConfigApi).RevisionId == "1792479016",
                    "A copy RPCS3 is still writing must be ignored.");
                writer.Write(next, next.Length / 2, next.Length - (next.Length / 2));
            }

            written = next;
            using (new FileStream(database, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var locked = Run(manual: true, locked: true);
                Assert(SourceStatus(locked, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                       Data().ApiSnapshot!.Count == 120,
                    "A copy locked by another process must be skipped without an error.");
            }

            var complete = Run(manual: true);
            Assert(SourceStatus(complete, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable &&
                   Data().ApiSnapshot!.Count == 118 &&
                   Data().State(RecSyncSource.ConfigApi).RevisionId == "1792651816",
                "The finished, strictly newer copy must then be applied.");
        }
    }

    private static void WikiRecentChanges(string folder)
    {
        var baseline = WikiBaseline(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero));
        var (service, fake) = Make(folder, Respond(api: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), wiki: WikiFixtures));
        using (service)
        {
            var report = Sync(service, baseline, manual: true);
            Assert(report.Status == RecSyncRunStatus.Partial && SourceStatus(report, RecSyncSource.Wiki) == RecSyncSourceStatus.Updated &&
                   SourceStatus(report, RecSyncSource.ConfigApi) == RecSyncSourceStatus.Unavailable,
                "The Wiki source must update independently of a failing config API.");

            var rc = fake.Requests.Where(request => request.Query.GetValueOrDefault("list") == "recentchanges").ToArray();
            Assert(rc.Length == 2 && !rc[0].Query.ContainsKey("rccontinue") && rc[1].Query["rccontinue"] == "20261018120000|91002" &&
                   rc[0].Query["rcstart"] == "2026-09-19T18:00:00Z",
                "recentchanges must start at the watermark minus the overlap window and follow continuation.");
            var revisions = fake.Requests.Where(request => request.Query.GetValueOrDefault("prop") == "revisions").ToArray();
            Assert(revisions.Length == 1 && revisions[0].Query["titles"].Split('|').Length == 3,
                "Each changed page must be fetched once even when listed twice.");

            var (data, _) = service.Read(baseline);
            var effective = RecSyncMerge.BuildEffective(baseline, data);
            var gta = report.NewChanges.Where(change => change.Title == "Grand Theft Auto IV").ToArray();
            Assert(gta.Any(change => change.Kind == RecSyncChangeKind.Removed && change.Key == "Sleep Timers Accuracy" && change.RevisionId == "61001") &&
                   gta.Any(change => change.Kind == RecSyncChangeKind.Added && change.Key == "SPU Block Size" && change.NewValue == "Mega") &&
                   gta.All(change => change.SourceUrl == RecSyncMerge.WikiPageUrl + "Grand%20Theft%20Auto%20IV"),
                "A semantic Wiki edit must produce per-setting changes with the revision and source link.");
            Assert(report.NewChanges.All(change => change.Title != "Demon's Souls") &&
                   data!.WikiPages.All(page => page.Title != "Demon's Souls"),
                "A prose-only edit is not a settings change.");
            Assert(effective.Find("BLUS99901") is { WikiName: "Fixture New Game" } added &&
                   added.Changes.Any(change => change.Key == "Write Color Buffers" && change.Origin == RecOrigin.Sync),
                "A new Wiki game page adds the game with its proven identity.");
            var state = data!.State(RecSyncSource.Wiki);
            Assert(state.Watermark == new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero) &&
                   ProcessedRevisions.All(data.SeenWikiRevisions.Contains),
                "The watermark and processed revisions are saved after the batch completes.");

            var again = Sync(service, baseline, manual: true);
            Assert(again.NewChanges.Count == 0 &&
                   fake.Requests.Count(request => request.Query.GetValueOrDefault("prop") == "revisions") == 1,
                "Revisions already processed inside the overlap window are skipped.");
        }
    }

    private static void WikiFullReconciliation(string folder)
    {
        var baseline = WikiBaseline(Now.AddDays(-100));
        var (service, fake) = Make(folder, Respond(api: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), wiki: WikiFixtures));
        using (service)
        {
            var report = Sync(service, baseline, manual: true);
            Assert(SourceStatus(report, RecSyncSource.Wiki) == RecSyncSourceStatus.Updated &&
                   fake.Requests.Any(request => request.Query.GetValueOrDefault("list") == "embeddedin") &&
                   fake.Requests.All(request => request.Query.GetValueOrDefault("list") != "recentchanges"),
                "A watermark older than the recentchanges retention must reconcile every config page.");
            Assert(service.Read(baseline).Data!.State(RecSyncSource.Wiki).Watermark == Now,
                "Full reconciliation restarts recent changes from the reconciliation time.");
        }
    }

    private static void PreferencesAndNotifications(string folder)
    {
        var store = new RecSyncStore(folder);
        Assert(store.LoadPreferences().Preferences == RecSyncPreferences.Default &&
               RecSyncPreferences.Default is { AutoUpdate: true, Notifications: RecSyncNotifications.Silent },
            "Defaults: automatic updates on, notifications silent.");
        store.SavePreferences(new RecSyncPreferences(false, RecSyncNotifications.ImportantForMyGames));
        Assert(new RecSyncStore(folder).LoadPreferences().Preferences == new RecSyncPreferences(false, RecSyncNotifications.ImportantForMyGames),
            "Preferences must persist across sessions.");
        File.WriteAllText(Path.Combine(folder, RecSyncStore.PreferencesFileName), "{ not json");
        var corrupt = store.LoadPreferences();
        Assert(corrupt.Recovered && !corrupt.Preferences.AutoUpdate,
            "Unreadable preferences must fail closed (no automatic requests).");

        Assert(!RecommendationSyncUseCase.IsAutoSyncAllowed(new RecSyncPreferences(false, RecSyncNotifications.Silent)) &&
               RecommendationSyncUseCase.IsAutoSyncAllowed(RecSyncPreferences.Default) == (Environment.GetEnvironmentVariable(RecommendationSyncUseCase.AutoSyncKillSwitch) != "1"),
            "Disabled automatic updates must stop automatic runs.");

        var change = RecSyncMerge.Change(RecSyncSource.ConfigApi, RecSyncChangeKind.Updated, "BLUS31465", "Assassin's Creed Rogue", "Video", "Write Color Buffers", "true", "false", RecSyncDisposition.Applied, RecSyncReason.None, "1", RecSyncMerge.ConfigApiUrl, Now);
        var other = change with { Id = "OTHER", Serial = "BLES00229" };
        var quarantined = change with { Id = "Q", Disposition = RecSyncDisposition.Quarantined };
        Assert(RecommendationSyncUseCase.SelectNotifications(RecSyncPreferences.Default, null, [change], ["BLUS31465"]).Count == 0,
            "Silent mode never notifies.");
        var important = new RecSyncPreferences(true, RecSyncNotifications.ImportantForMyGames);
        Assert(RecommendationSyncUseCase.SelectNotifications(important, null, [change, other, quarantined], ["BLUS31465"]).Single() == change,
            "Notifications cover applied changes for installed games only.");

        var baseline = Shipped.Value;
        var (service, fake) = Make(Path.Combine(folder, "store"), Respond(api: _ => Json(Fixture("config-api-v1.json"))));
        using (service)
        {
            var useCase = new RecommendationSyncUseCase(service);
            var local = useCase.ReadLocal(baseline);
            Assert(fake.Requests.Count == 0 && ReferenceEquals(local.Effective, baseline),
                "Reading the local layer must not touch the network.");
            Sync(service, baseline, manual: true);
            Assert(useCase.MarkNotifiedAsync(baseline, [change.Id], CancellationToken.None).GetAwaiter().GetResult() &&
                   RecommendationSyncUseCase.SelectNotifications(important, service.Read(baseline).Data, [change], ["BLUS31465"]).Count == 0,
                "A notice is never repeated for the same revision.");
        }
    }

    private static void Cancellation(string folder)
    {
        var baseline = Shipped.Value;
        var download = Path.Combine(folder, "download");
        var (service, _) = Make(download, Respond(api: _ => Json(Fixture("config-api-v1.json"))));
        using (service)
        {
            Sync(service, baseline, manual: true);
        }

        var before = File.ReadAllBytes(Path.Combine(download, RecSyncStore.FileName));
        using var cts = new CancellationTokenSource();
        var (slow, _) = Make(download, async (request, token) =>
        {
            cts.CancelAfter(50);
            await Task.Delay(Timeout.Infinite, token);
            return Json([]);
        });
        using (slow)
        {
            var report = slow.SyncAsync(baseline, true, cts.Token).GetAwaiter().GetResult();
            Assert(report.Status == RecSyncRunStatus.Cancelled && File.ReadAllBytes(Path.Combine(download, RecSyncStore.FileName)).SequenceEqual(before),
                "Cancelling during download must leave the store unchanged.");
        }

        using var late = new CancellationTokenSource();
        var edited = ApiBody(games => games.Remove(games.First().Key), timestamp: 1792565416);
        var (beforeSave, _) = Make(download, (request, _) =>
        {
            if (request.RequestUri!.Host == "wiki.rpcs3.net")
            {
                late.Cancel();
                return Task.FromResult(Challenge());
            }

            return Task.FromResult(Json(edited));
        });
        using (beforeSave)
        {
            var report = beforeSave.SyncAsync(baseline, true, late.Token).GetAwaiter().GetResult();
            Assert(report.Status == RecSyncRunStatus.Cancelled && File.ReadAllBytes(Path.Combine(download, RecSyncStore.FileName)).SequenceEqual(before),
                "Cancelling after processing but before saving must keep the last good store and cursors.");
        }

        var store = new RecSyncStore(download);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        MustFail<OperationCanceledException>(() => store.Save(store.Load().Data!, cancelled.Token), "A cancelled save must not commit.");
        Assert(File.ReadAllBytes(Path.Combine(download, RecSyncStore.FileName)).SequenceEqual(before) &&
               !Directory.EnumerateFiles(download, "*.tmp").Any(),
            "A cancelled save leaves the store and no temporary file.");
    }

    private static void Concurrency(string folder)
    {
        var baseline = Shipped.Value;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, fake) = Make(folder, async (request, token) =>
        {
            if (request.RequestUri!.Host == "api.rpcs3.net")
            {
                await gate.Task.WaitAsync(token);
                return Json(Fixture("config-api-v1.json"));
            }

            return Challenge();
        });
        using (service)
        {
            var first = Task.Run(() => service.SyncAsync(baseline, false, CancellationToken.None));
            SpinWait.SpinUntil(() => service.IsRunning, TimeSpan.FromSeconds(10));
            var second = service.SyncAsync(baseline, true, CancellationToken.None).GetAwaiter().GetResult();
            gate.SetResult();
            var done = first.GetAwaiter().GetResult();
            Assert(second.Status == RecSyncRunStatus.AlreadyRunning && done.Status == RecSyncRunStatus.Partial &&
                   fake.Requests.Count(request => request.Uri.Host == "api.rpcs3.net") == 1,
                "Only one sync may run at a time.");
        }
    }

    private static void ReopenAndRecovery(string folder)
    {
        var baseline = Shipped.Value;
        var body = Fixture("config-api-v1.json");
        var (service, _) = Make(folder, Respond(api: _ => Json(body)));
        using (service)
        {
            Sync(service, baseline, manual: true);
            body = ApiBody(games => games.Remove(games.First().Key), timestamp: 1792565416);
            Sync(service, baseline, manual: true);
        }

        var main = Path.Combine(folder, RecSyncStore.FileName);
        var backup = Path.Combine(folder, RecSyncStore.BackupFileName);
        Assert(File.Exists(backup), "A second save must keep the previous store as last known good.");

        var (reopened, reopenedFake) = Make(folder, Respond(api: _ => Json(body)));
        using (reopened)
        {
            var (data, health) = reopened.Read(baseline);
            Assert(health == RecSyncStoreHealth.Loaded && data!.ApiSnapshot!.Count == 119 && reopenedFake.Requests.Count == 0,
                "Reopening restores the store without network access.");
        }

        var good = File.ReadAllBytes(main);
        File.WriteAllText(Path.Combine(folder, ".store.json.0123456789abcdef0123456789abcdef.tmp"), "partial");
        File.WriteAllText(main, "{\"Schema\":1,\"Overrides\":[{\"Serial\":\"bad serial\"");
        var recovered = new RecSyncStore(folder).Load();
        Assert(recovered.Health == RecSyncStoreHealth.RecoveredFromBackup && recovered.Data is not null &&
               Directory.EnumerateFiles(folder, "store.json.corrupt-*").Any() &&
               !Directory.EnumerateFiles(folder, "*.tmp").Any(),
            "A damaged store must fall back to the last known good copy and clean temporary files.");

        File.WriteAllText(main, "garbage");
        File.WriteAllText(backup, "garbage");
        var reset = new RecSyncStore(folder).Load();
        Assert(reset.Health == RecSyncStoreHealth.Reset && reset.Data is null &&
               ReferenceEquals(RecSyncMerge.BuildEffective(baseline, reset.Data), baseline),
            "With no readable copy the built-in baseline is used alone.");

        File.WriteAllBytes(main, good);
        var text = File.ReadAllText(main).Replace("\"Schema\":1", "\"Schema\":99", StringComparison.Ordinal);
        File.WriteAllText(main, text);
        var (newer, newerFake) = Make(folder, Respond(api: _ => Json(body)));
        using (newer)
        {
            Assert(newer.Read(baseline).Health == RecSyncStoreHealth.NewerSchema &&
                   newer.SyncAsync(baseline, true, CancellationToken.None).GetAwaiter().GetResult().Status == RecSyncRunStatus.StoreReadOnly &&
                   newerFake.Requests.Count == 0 &&
                   File.ReadAllText(main) == text,
                "A store from a newer R3Config is never overwritten and blocks syncing.");
            MustFail<InvalidOperationException>(() => newer.Store.Save(RecSyncStoreData.Empty("x", "y")), "Saving over a newer store must be refused.");
        }
    }

    private static void BaselineUpgrade(string folder)
    {
        var baseline = Shipped.Value;
        var (service, _) = Make(folder, Respond(api: _ => Json(Fixture("config-api-v1.json"), etag: "\"v1\"")));
        RecSyncOverride folded;
        using (service)
        {
            Sync(service, baseline, manual: true);
            folded = service.Read(baseline).Data!.Overrides.First(item => item.Value is not null);
        }

        var recs = baseline.Rec.Select(rec => rec.Serial != folded.Serial ? rec : rec with
        {
            Changes = [.. rec.Changes.Append(new CfgChg(folded.Section, folded.Key, folded.Value!, WikiSvc.TryGetSyncTarget(folded.Section, folded.Key, out var key) ? key : "x", null, RecRisk.Unknown))]
        });
        var upgraded = RecSvc.FromRec(baseline.Info.DataSrc, "2026-11-01", recs, Now, "NEWHASH");

        var (next, _) = Make(folder, Respond(api: request => request.IfNoneMatch == "\"v1\"" ? new HttpResponseMessage(HttpStatusCode.NotModified) : Json(Fixture("config-api-v1.json"))));
        using (next)
        {
            var (data, _) = next.Read(upgraded);
            Assert(data!.BaselineHash == "NEWHASH" && data.Overrides.All(item => !(item.Serial == folded.Serial && item.Key == folded.Key)) &&
                   RecSyncMerge.BuildEffective(upgraded, data).Find(folded.Serial)!.Changes.Single(change => change.Key == folded.Key) is { Origin: RecOrigin.Baseline },
                "A newer baseline that already carries an update absorbs it without duplication.");
            Sync(next, upgraded, manual: true);
            Assert(new RecSyncStore(folder).Load().Data!.BaselineHash == "NEWHASH", "The upgraded store is saved on the next run.");
        }
    }

    private static void Rpcs3FilesUntouched(string folder)
    {
        var rpcs3 = Path.Combine(folder, "rpcs3");
        var files = new Dictionary<string, string>
        {
            ["rpcs3.exe"] = "MZ fixture",
            ["config.yml"] = "Video:\n  Resolution Scale: 150\n  Write Color Buffers: false\n",
            [Path.Combine("config", "custom_configs", "config_BLUS31465.yml")] = "Video:\n  Resolution Scale: 200\n",
            [Path.Combine("patches", "patch.yml")] = "Version: 1.2\n",
            [Path.Combine("config", "patch_config.yml")] = "{}\n",
            [Path.Combine("dev_hdd0", "game", "BLUS31465", "PARAM.SFO")] = "SFO"
        };
        foreach (var (relative, content) in files)
        {
            var path = Path.Combine(rpcs3, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        var before = Hashes(rpcs3);
        var baseline = Shipped.Value;
        var (service, fake) = Make(Path.Combine(folder, "store"), Respond(api: _ => Json(Fixture("config-api-v1.json")), wiki: WikiFixtures));
        using (service)
        {
            Sync(service, baseline, manual: true);
            Sync(service, baseline, manual: true);
        }

        Assert(Hashes(rpcs3).SequenceEqual(before), "Syncing must leave every RPCS3 file byte-identical (SHA-256).");
        Assert(fake.Requests.All(request => request.Method == "GET" && !request.Uri.ToString().Contains("BLUS31465", StringComparison.Ordinal)),
            "Requests must not carry the user's game list.");
    }

    private static void PlanNeverAutoSelectsSync(RecSyncStoreData first)
    {
        var baseline = Shipped.Value;
        var effective = RecSyncMerge.BuildEffective(baseline, first);
        var rec = effective.Rec.First(item => item.Changes.Any(change => change.Origin == RecOrigin.Sync));
        var game = new Rpcs3GameDescriptor(rec.Serial, rec.WikiName, string.Empty, "01.00", null, null);
        // Use a deterministic risk so the baseline twin below is auto-selectable.
        var sync = rec.Changes.First(change => change.Origin == RecOrigin.Sync) with { Risk = RecRisk.Rec, Notes = null };
        var sum = new StateSvc().Evaluate(game, [rec with { Changes = [sync] }], (_, _) => "__current__");
        var item = sum.Items.Single();
        Assert(!item.IsSel && item.State != RecState.Pending,
            "A synced recommendation must never be pre-selected in the configuration plan.");
        var asBaseline = new StateSvc().Evaluate(game, [rec with { Changes = [sync with { Origin = RecOrigin.Baseline }] }], (_, _) => "__current__");
        Assert(asBaseline.Items.Single().IsSel,
            "The same value from the shipped baseline keeps the existing auto-selection behaviour.");
    }

    // Live check against the real official sources, into a temporary store.
    public static void Live(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var baseline = Shipped.Value;
        var folder = Path.Combine(outDir, "store");
        using var http = new RecSyncHttp(RecSyncHttp.CreateHandler(), "live-check");
        using var service = new RecSyncService(new RecSyncStore(folder), http, new WikiRevisionSource(http));
        var report = service.SyncAsync(baseline, true, CancellationToken.None).GetAwaiter().GetResult();
        var (data, _) = service.Read(baseline);
        var effective = RecSyncMerge.BuildEffective(baseline, data);

        List<string> lines =
        [
            $"Run: {report.Status}",
            .. report.Sources.Select(source => $"Source {source.Source}: {source.Status} - {source.Detail}"),
            $"Games: {baseline.Count} -> {effective.Count}",
            $"Settings: {RecSyncMerge.SettingCount(baseline)} -> {RecSyncMerge.SettingCount(effective)}",
            $"Applied: {report.Applied}, Quarantined: {report.Quarantined}",
            .. (data?.Quarantine ?? []).GroupBy(item => item.Reason).OrderBy(group => group.Key).Select(group => $"Quarantine {group.Key}: {group.Count()}"),
            .. report.NewChanges.Where(change => change.Disposition == RecSyncDisposition.Applied).Take(40)
                .Select(change => $"APPLIED {change.Kind} {change.Serial} [{change.Title}] {change.Section}/{change.Key}: {change.OldValue ?? "-"} -> {change.NewValue ?? "-"}"),
            .. (data?.Quarantine ?? []).Take(40)
                .Select(change => $"QUARANTINE {change.Reason} {change.Kind} {change.Serial} [{change.Title}] {change.Section}/{change.Key}: {change.OldValue ?? "-"} -> {change.NewValue ?? "-"}")
        ];
        File.WriteAllLines(Path.Combine(outDir, "live-report.txt"), lines);
        foreach (var line in lines.Take(30))
        {
            Console.WriteLine(line);
        }
    }

    private static RecSvc WikiBaseline(DateTimeOffset generated)
    {
        using var document = JsonDocument.Parse(Fixture("wiki-baseline-revisions.json"));
        var pages = document.RootElement.GetProperty("query").GetProperty("pages").EnumerateArray()
            .Select(page =>
            {
                var revision = page.GetProperty("revisions")[0];
                return new WikiLivePage(
                    page.GetProperty("pageid").GetInt64(),
                    page.GetProperty("title").GetString()!,
                    0,
                    revision.GetProperty("revid").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(revision.GetProperty("timestamp").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    revision.GetProperty("slots").GetProperty("main").GetProperty("content").GetString()!);
            })
            .ToArray();
        var recs = WikiSvc.ParseLivePages(pages).SelectMany(page => page.Recommendations).ToArray();
        Assert(recs.Any(rec => rec.WikiName == "Grand Theft Auto IV" && rec.Changes.Any(change => change.Key == "Sleep Timers Accuracy")),
            "The Wiki baseline fixture must parse with the shipped parser.");
        return RecSvc.FromRec("RPCS3 Wiki snapshot", generated.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), recs, generated, "WIKIFIXTURE");
    }

    private static HttpResponseMessage WikiFixtures(FakeRequest request)
    {
        var query = request.Query;
        if (query.GetValueOrDefault("list") == "recentchanges")
        {
            return Json(Fixture(query.ContainsKey("rccontinue") ? "wiki-recentchanges-2.json" : "wiki-recentchanges-1.json"));
        }

        if (query.GetValueOrDefault("list") == "embeddedin")
        {
            return Json(Fixture("wiki-embeddedin.json"));
        }

        if (query.GetValueOrDefault("prop") == "revisions")
        {
            var titles = query["titles"].Split('|').ToHashSet(StringComparer.Ordinal);
            var all = JsonNode.Parse(Fixture("wiki-revisions.json"))!;
            var pages = all["query"]!["pages"]!.AsArray();
            foreach (var page in pages.ToArray())
            {
                if (!titles.Contains(page!["title"]!.GetValue<string>()))
                {
                    pages.Remove(page);
                }
            }

            return Json(Encoding.UTF8.GetBytes(all.ToJsonString()));
        }

        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond(
        Func<FakeRequest, HttpResponseMessage> api,
        Func<FakeRequest, HttpResponseMessage>? wiki = null,
        Func<FakeRequest, HttpResponseMessage>? github = null)
    {
        return (request, _) =>
        {
            var seen = FakeRequest.From(request);
            var response = request.RequestUri!.Host switch
            {
                "api.rpcs3.net" => api(seen),
                "raw.githubusercontent.com" => (github ?? (_ => new HttpResponseMessage(HttpStatusCode.NotFound)))(seen),
                _ => (wiki ?? (_ => Challenge()))(seen)
            };
            return Task.FromResult(response);
        };
    }

    private static (RecSyncService Service, FakeHttp Fake) Make(
        string folder,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var fake = new FakeHttp(respond);
        var http = new RecSyncHttp(fake, "test", NoDelay);
        return (new RecSyncService(new RecSyncStore(folder), http, new WikiRevisionSource(http, NoDelay), () => Now), fake);
    }

    private static RecSyncReport Sync(RecSyncService service, RecSvc baseline, bool manual) =>
        service.SyncAsync(baseline, manual, CancellationToken.None).GetAwaiter().GetResult();

    private static RecSyncSourceStatus SourceStatus(RecSyncReport report, RecSyncSource source) =>
        report.Sources.Single(item => item.Source == source).Status;

    private static Task NoDelay(TimeSpan delay, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static HttpResponseMessage Json(byte[] body, string? etag = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (etag is not null)
        {
            response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return response;
    }

    private static HttpResponseMessage Challenge()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("<!DOCTYPE html><title>Just a moment...</title>", Encoding.UTF8, "text/html")
        };
        response.Headers.TryAddWithoutValidation("cf-mitigated", "challenge");
        return response;
    }

    private static byte[] ApiBody(Action<JsonObject> edit, long? timestamp = null)
    {
        var root = JsonNode.Parse(Fixture("config-api-v1.json"))!.AsObject();
        edit(root["games"]!.AsObject());
        if (timestamp is not null)
        {
            root["timestamp"] = timestamp.Value;
        }

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static void Keep(JsonObject games, int count)
    {
        foreach (var serial in games.Select(item => item.Key).Skip(count).ToArray())
        {
            games.Remove(serial);
        }
    }

    private static string Config(JsonObject games, string serial) => games[serial]!["config"]!.GetValue<string>();

    private static string Edit(string config, string from, string to)
    {
        Assert(config.Contains(from, StringComparison.Ordinal), $"Fixture config must contain '{from}'.");
        return config.Replace(from, to, StringComparison.Ordinal);
    }

    private static string RemoveLine(string config, RecSyncSetting setting)
    {
        var key = setting.Key.Split('.').Last();
        var lines = config.Split('\n').ToList();
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(key + ": ", StringComparison.Ordinal));
        Assert(index >= 0, $"Fixture config must contain {setting.Key}.");
        lines.RemoveAt(index);
        return string.Join('\n', lines);
    }

    private static string AddVideo(string config, string lines) =>
        config.Contains("Video:\n", StringComparison.Ordinal)
            ? config.Replace("Video:\n", "Video:\n  " + lines + "\n", StringComparison.Ordinal)
            : config + "\nVideo:\n  " + lines;

    private static string[] Hashes(string root) =>
        [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))];

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(Root(), "Tst", "Fixtures", "RecSync", name));

    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    internal sealed record FakeRequest(Uri Uri, string Method, string? IfNoneMatch, string UserAgent, Dictionary<string, string> Query)
    {
        public static FakeRequest From(HttpRequestMessage request)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in request.RequestUri!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                query[Uri.UnescapeDataString(pair[0])] = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            }

            return new FakeRequest(
                request.RequestUri,
                request.Method.Method,
                request.Headers.IfNoneMatch.FirstOrDefault()?.ToString(),
                request.Headers.UserAgent.ToString(),
                query);
        }
    }

    internal sealed class FakeHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<FakeRequest> _requests = [];

        public IReadOnlyList<FakeRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _requests.Add(FakeRequest.From(request));
            }

            return respond(request, cancellationToken);
        }
    }
}
