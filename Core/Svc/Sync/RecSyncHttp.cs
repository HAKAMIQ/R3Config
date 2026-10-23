using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace R3Config.Svc;

public enum RecSyncHttpStatus
{
    Ok,
    NotModified,
    Blocked,
    RateLimited,
    ServerError,
    NetworkError,
    Timeout,
    TooLarge,
    Rejected
}

public sealed record RecSyncHttpResult(
    RecSyncHttpStatus Status,
    int HttpStatus,
    byte[] Body,
    string? ETag,
    string? LastModified,
    TimeSpan? RetryAfter,
    string Detail);

// Bounded HTTPS GET for fixed, allowlisted recommendation sources only. It
// sends no user or game data: only the fixed source URL, a product User-Agent and optional
// cache validators. Redirects, other hosts, HTML challenge pages and
// oversized bodies are refused; transient failures get a limited retry.
public sealed class RecSyncHttp : IDisposable
{
    public const int MaxAttempts = 3;
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxInlineRetryAfter = TimeSpan.FromSeconds(10);

    private static readonly string[] AllowedHosts =
    [
        "api.rpcs3.net",
        "wiki.rpcs3.net",
        "raw.githubusercontent.com"
    ];

    private readonly HttpClient _client;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly string _userAgent;

    public RecSyncHttp(
        HttpMessageHandler handler,
        string productVersion,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _delay = delay ?? Task.Delay;
        _userAgent = $"R3Config/{(string.IsNullOrWhiteSpace(productVersion) ? "0" : productVersion.Trim())} (recommendation-sync)";
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public static HttpMessageHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            MaxResponseHeadersLength = 64,
            UseCookies = false,
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };
    }

    public static bool IsAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return uri.IsAbsoluteUri &&
               uri.Scheme == Uri.UriSchemeHttps &&
               uri.IsDefaultPort &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               AllowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<RecSyncHttpResult> GetAsync(
        Uri uri,
        long maxBytes,
        string expectedMediaType,
        string? etag,
        string? lastModified,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!IsAllowed(uri))
        {
            return Fail(RecSyncHttpStatus.Rejected, 0, "The URL is not an allowed official HTTPS source.");
        }

        RecSyncHttpResult result = Fail(RecSyncHttpStatus.NetworkError, 0, "No attempt was made.");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await SendOnceAsync(uri, maxBytes, expectedMediaType, etag, lastModified, cancellationToken)
                .ConfigureAwait(false);

            var retry = result.Status switch
            {
                RecSyncHttpStatus.ServerError or
                RecSyncHttpStatus.NetworkError or
                RecSyncHttpStatus.Timeout => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                RecSyncHttpStatus.RateLimited when result.RetryAfter is { } wait && wait <= MaxInlineRetryAfter => wait,
                _ => (TimeSpan?)null
            };

            if (retry is null || attempt == MaxAttempts)
            {
                return result;
            }

            await _delay(retry.Value, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<RecSyncHttpResult> SendOnceAsync(
        Uri uri,
        long maxBytes,
        string expectedMediaType,
        string? etag,
        string? lastModified,
        CancellationToken cancellationToken)
    {
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptCts.CancelAfter(AttemptTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(_userAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(expectedMediaType));
        if (!string.IsNullOrWhiteSpace(etag) &&
            EntityTagHeaderValue.TryParse(etag, out var tag))
        {
            request.Headers.IfNoneMatch.Add(tag);
        }

        if (!string.IsNullOrWhiteSpace(lastModified) &&
            DateTimeOffset.TryParse(lastModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
        {
            request.Headers.IfModifiedSince = since;
        }

        try
        {
            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                .ConfigureAwait(false);
            var code = (int)response.StatusCode;
            var retryAfter = ReadRetryAfter(response);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new RecSyncHttpResult(RecSyncHttpStatus.NotModified, code, [], etag, lastModified, null, "Not modified.");
            }

            if (code is >= 300 and < 400)
            {
                return Fail(RecSyncHttpStatus.Rejected, code, "Redirects are not followed.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new RecSyncHttpResult(RecSyncHttpStatus.RateLimited, code, [], null, null, retryAfter ?? TimeSpan.FromHours(1), "Rate limited (429).");
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                var challenge = response.Headers.TryGetValues("cf-mitigated", out var values) &&
                                values.Any(value => value.Contains("challenge", StringComparison.OrdinalIgnoreCase));
                return Fail(
                    RecSyncHttpStatus.Blocked,
                    code,
                    challenge
                        ? "The source requires a browser challenge; automated access is not permitted."
                        : $"Access denied ({code}).");
            }

            if (code >= 500)
            {
                return new RecSyncHttpResult(RecSyncHttpStatus.ServerError, code, [], null, null, retryAfter, $"Server error ({code}).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail(RecSyncHttpStatus.Rejected, code, $"Unexpected status ({code}).");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
                        ? RecSyncHttpStatus.Blocked
                        : RecSyncHttpStatus.Rejected,
                    code,
                    $"Unexpected content type '{mediaType ?? "-"}'.");
            }

            if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
            {
                return Fail(RecSyncHttpStatus.TooLarge, code, "The response exceeds the size limit.");
            }

            var body = await ReadBoundedAsync(response.Content, maxBytes, attemptCts.Token).ConfigureAwait(false);
            if (body is null)
            {
                return Fail(RecSyncHttpStatus.TooLarge, code, "The response exceeds the size limit.");
            }

            return new RecSyncHttpResult(
                RecSyncHttpStatus.Ok,
                code,
                body,
                response.Headers.ETag?.ToString(),
                response.Content.Headers.LastModified?.ToString("R", CultureInfo.InvariantCulture),
                null,
                "OK");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(RecSyncHttpStatus.Timeout, 0, "The request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return Fail(RecSyncHttpStatus.NetworkError, 0, $"Network error: {ex.HttpRequestError}.");
        }
        catch (IOException)
        {
            return Fail(RecSyncHttpStatus.NetworkError, 0, "The connection was interrupted.");
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpContent content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private static RecSyncHttpResult Fail(RecSyncHttpStatus status, int code, string detail) =>
        new(status, code, [], null, null, null, detail);
}
