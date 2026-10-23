using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace R3Config.Svc;

public sealed record WikiSyncFetch(
    RecSyncHttpStatus Status,
    string Detail,
    IReadOnlyList<WikiLivePage> Pages,
    DateTimeOffset? Watermark,
    IReadOnlyList<string> ProcessedRevisions,
    TimeSpan? RetryAfter,
    bool FullReconciliation);

// Reads changed Wiki pages through the public MediaWiki API: recentchanges
// (with continuation) since the stored watermark minus an overlap window,
// then the current revision of each changed page. When the watermark is
// missing or older than the recentchanges retention, it reconciles every page
// that uses Template:Config. Nothing is sent except fixed query parameters
// and page titles that came from the Wiki itself.
public sealed class WikiRevisionSource
{
    public const string ApiUrl = "https://wiki.rpcs3.net/api.php";
    public const long MaxResponseBytes = 16L * 1024 * 1024;
    public const int TitlesPerRequest = 50;
    public const int MaxListRequests = 40;
    public static readonly TimeSpan Overlap = TimeSpan.FromHours(6);
    public static readonly TimeSpan RecentChangesRetention = TimeSpan.FromDays(80);
    public static readonly TimeSpan Courtesy = TimeSpan.FromSeconds(1);

    private readonly RecSyncHttp _http;
    private readonly Func<TimeSpan, CancellationToken, Task> _pause;

    public WikiRevisionSource(
        RecSyncHttp http,
        Func<TimeSpan, CancellationToken, Task>? pause = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _pause = pause ?? Task.Delay;
    }

    public async Task<WikiSyncFetch> FetchAsync(
        DateTimeOffset? watermark,
        IReadOnlyCollection<string> seenRevisions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seenRevisions);

        var full = watermark is null || now - watermark.Value > RecentChangesRetention;
        var titles = new List<string>();
        var processed = new List<string>();
        DateTimeOffset? newest = watermark;

        if (!full)
        {
            var since = watermark!.Value - Overlap;
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["action"] = "query",
                ["format"] = "json",
                ["formatversion"] = "2",
                ["list"] = "recentchanges",
                ["rcnamespace"] = "0",
                ["rctype"] = "edit|new",
                ["rcprop"] = "title|ids|timestamp",
                ["rcdir"] = "newer",
                ["rcstart"] = since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["rclimit"] = "500",
                ["maxlag"] = "5"
            };

            var seen = new HashSet<string>(seenRevisions, StringComparer.Ordinal);
            var titleSet = new HashSet<string>(StringComparer.Ordinal);
            var listed = await ListAsync(parameters, "recentchanges", cancellationToken).ConfigureAwait(false);
            if (listed.Failure is not null)
            {
                return listed.Failure;
            }

            if (listed.Truncated)
            {
                full = true;
            }
            else
            {
                foreach (var item in listed.Items)
                {
                    var revid = item.TryGetProperty("revid", out var id) && id.TryGetInt64(out var number)
                        ? number.ToString(CultureInfo.InvariantCulture)
                        : null;
                    var title = item.TryGetProperty("title", out var text) ? text.GetString() : null;
                    var timestamp = item.TryGetProperty("timestamp", out var stamp) &&
                                    DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                        ? parsed
                        : (DateTimeOffset?)null;

                    if (revid is null || string.IsNullOrWhiteSpace(title) || timestamp is null)
                    {
                        return Fail(RecSyncHttpStatus.Rejected, "A recentchanges entry is incomplete.");
                    }

                    if (newest is null || timestamp > newest)
                    {
                        newest = timestamp;
                    }

                    if (!seen.Add(revid))
                    {
                        // Already processed inside the overlap window.
                        continue;
                    }

                    processed.Add(revid);
                    if (titleSet.Add(title))
                    {
                        titles.Add(title);
                    }
                }
            }
        }

        if (full)
        {
            titles.Clear();
            processed.Clear();
            newest = now;

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["action"] = "query",
                ["format"] = "json",
                ["formatversion"] = "2",
                ["list"] = "embeddedin",
                ["eititle"] = "Template:Config",
                ["einamespace"] = "0",
                ["eilimit"] = "500",
                ["maxlag"] = "5"
            };

            var listed = await ListAsync(parameters, "embeddedin", cancellationToken).ConfigureAwait(false);
            if (listed.Failure is not null)
            {
                return listed.Failure;
            }

            if (listed.Truncated)
            {
                return Fail(RecSyncHttpStatus.Rejected, "The Wiki page list exceeds the reconciliation limit.");
            }

            titles.AddRange(listed.Items
                .Select(item => item.TryGetProperty("title", out var title) ? title.GetString() : null)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title!)
                .Distinct(StringComparer.Ordinal));
        }

        if (titles.Count > WikiSvc.MaxLivePages)
        {
            return Fail(RecSyncHttpStatus.Rejected, "Too many changed Wiki pages.");
        }

        var pages = new List<WikiLivePage>();
        var queue = new Queue<string[]>(titles.Chunk(TitlesPerRequest));
        var retried = new HashSet<string>(StringComparer.Ordinal);
        while (queue.TryDequeue(out var batch))
        {
            await _pause(Courtesy, cancellationToken).ConfigureAwait(false);

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["action"] = "query",
                ["format"] = "json",
                ["formatversion"] = "2",
                ["prop"] = "revisions",
                ["rvprop"] = "ids|timestamp|content",
                ["rvslots"] = "main",
                ["titles"] = string.Join('|', batch),
                ["maxlag"] = "5"
            };

            var response = await GetJsonAsync(parameters, cancellationToken).ConfigureAwait(false);
            if (response.Failure is not null)
            {
                return response.Failure;
            }

            using var document = response.Document!;
            if (!TryGetQuery(document.RootElement, "pages", out var pageArray))
            {
                return Fail(RecSyncHttpStatus.Rejected, "The revisions response has no pages.");
            }

            foreach (var page in pageArray.EnumerateArray())
            {
                if (page.TryGetProperty("missing", out _) || page.TryGetProperty("invalid", out _))
                {
                    continue;
                }

                if (!page.TryGetProperty("revisions", out _) &&
                    page.TryGetProperty("title", out var pending) &&
                    pending.GetString() is { Length: > 0 } pendingTitle &&
                    retried.Add(pendingTitle))
                {
                    // The API left this page's content for a continuation;
                    // fetch it again on its own instead of guessing.
                    queue.Enqueue([pendingTitle]);
                    continue;
                }

                var parsed = ReadPage(page);
                if (parsed is null)
                {
                    return Fail(RecSyncHttpStatus.Rejected, "A Wiki revision is incomplete or not wikitext.");
                }

                pages.Add(parsed);
            }
        }

        return new WikiSyncFetch(
            RecSyncHttpStatus.Ok,
            full ? "Full reconciliation." : "Recent changes.",
            pages,
            newest,
            processed,
            null,
            full);
    }

    private static WikiLivePage? ReadPage(JsonElement page)
    {
        if (!page.TryGetProperty("title", out var titleElement) ||
            titleElement.GetString() is not { Length: > 0 } title ||
            !page.TryGetProperty("ns", out var nsElement) ||
            !nsElement.TryGetInt32(out var ns) ||
            !page.TryGetProperty("revisions", out var revisions) ||
            revisions.ValueKind != JsonValueKind.Array ||
            revisions.GetArrayLength() != 1)
        {
            return null;
        }

        var revision = revisions[0];
        if (!revision.TryGetProperty("revid", out var revid) ||
            !revid.TryGetInt64(out var revisionId) ||
            !revision.TryGetProperty("timestamp", out var stamp) ||
            !DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) ||
            !revision.TryGetProperty("slots", out var slots) ||
            !slots.TryGetProperty("main", out var main) ||
            !main.TryGetProperty("contentmodel", out var model) ||
            model.GetString() != "wikitext" ||
            !main.TryGetProperty("content", out var content) ||
            content.GetString() is not { } text ||
            text.Length > WikiSvc.MaxLivePageChars)
        {
            return null;
        }

        long? pageId = page.TryGetProperty("pageid", out var id) && id.TryGetInt64(out var number) ? number : null;
        return new WikiLivePage(pageId, title, ns, revisionId.ToString(CultureInfo.InvariantCulture), timestamp, text);
    }

    private async Task<ListResult> ListAsync(
        Dictionary<string, string> parameters,
        string listName,
        CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var request = new Dictionary<string, string>(parameters, StringComparer.Ordinal);

        for (var count = 0; count < MaxListRequests; count++)
        {
            if (count > 0)
            {
                await _pause(Courtesy, cancellationToken).ConfigureAwait(false);
            }

            var response = await GetJsonAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Failure is not null)
            {
                return new ListResult(items, false, response.Failure);
            }

            using var document = response.Document!;
            if (!TryGetQuery(document.RootElement, listName, out var list))
            {
                return new ListResult(items, false, Fail(RecSyncHttpStatus.Rejected, $"The {listName} response is malformed."));
            }

            items.AddRange(list.EnumerateArray().Select(item => item.Clone()));

            if (!document.RootElement.TryGetProperty("continue", out var next) ||
                next.ValueKind != JsonValueKind.Object)
            {
                return new ListResult(items, false, null);
            }

            request = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            foreach (var property in next.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String ||
                    property.Value.GetString() is not { Length: <= 256 } value)
                {
                    return new ListResult(items, false, Fail(RecSyncHttpStatus.Rejected, "The continuation token is invalid."));
                }

                request[property.Name] = value;
            }
        }

        return new ListResult(items, true, null);
    }

    private async Task<JsonResult> GetJsonAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var query = new StringBuilder();
        foreach (var (name, value) in parameters)
        {
            query.Append(query.Length == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        var result = await _http
            .GetAsync(new Uri(ApiUrl + query), MaxResponseBytes, "application/json", null, null, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status != RecSyncHttpStatus.Ok)
        {
            return new JsonResult(null, new WikiSyncFetch(result.Status, result.Detail, [], null, [], result.RetryAfter, false));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(result.Body, new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException)
        {
            return new JsonResult(null, Fail(RecSyncHttpStatus.Rejected, "The Wiki API returned invalid JSON."));
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            return new JsonResult(null, Fail(RecSyncHttpStatus.Rejected, "The Wiki API returned an unexpected document."));
        }

        if (document.RootElement.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
            document.Dispose();
            return new JsonResult(
                null,
                code is "maxlag" or "ratelimited"
                    ? new WikiSyncFetch(RecSyncHttpStatus.RateLimited, $"Wiki API asked to slow down ({code}).", [], null, [], TimeSpan.FromMinutes(30), false)
                    : Fail(RecSyncHttpStatus.Rejected, $"Wiki API error ({code ?? "unknown"})."));
        }

        return new JsonResult(document, null);
    }

    private static bool TryGetQuery(JsonElement root, string name, out JsonElement list)
    {
        list = default;
        return root.TryGetProperty("query", out var query) &&
               query.TryGetProperty(name, out list) &&
               list.ValueKind == JsonValueKind.Array;
    }

    private static WikiSyncFetch Fail(RecSyncHttpStatus status, string detail) =>
        new(status, detail, [], null, [], null, false);

    private sealed record ListResult(
        IReadOnlyList<JsonElement> Items,
        bool Truncated,
        WikiSyncFetch? Failure);

    private sealed record JsonResult(
        JsonDocument? Document,
        WikiSyncFetch? Failure);
}
