using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexUsage.Core.Reset;

public sealed class CodexResetsClient : IDisposable
{
    public const string StatusEndpoint = "https://codex-resets.com/api/v1/status";

    private readonly HttpClient _httpClient;
    private EntityTagHeaderValue? _etag;
    private PublicResetStatus? _lastStatus;
    private DateTimeOffset? _lastSuccessfulAt;

    public CodexResetsClient(PublicResetStatus? cachedStatus = null, DateTimeOffset? cachedAt = null)
    {
        _lastStatus = cachedStatus;
        _lastSuccessfulAt = cachedAt;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CodexTitlebarHud/0.1");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<PublicResetFetch> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, StatusEndpoint);
            if (_etag is not null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && _lastStatus is not null)
            {
                _lastSuccessfulAt = DateTimeOffset.UtcNow;
                return new PublicResetFetch(true, _lastStatus, _lastSuccessfulAt);
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var status = await JsonSerializer.DeserializeAsync<PublicResetStatus>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("Reset status response was empty.");

            _etag = response.Headers.ETag;
            _lastStatus = status;
            _lastSuccessfulAt = DateTimeOffset.UtcNow;
            return new PublicResetFetch(true, status, _lastSuccessfulAt);
        }
        catch
        {
            return new PublicResetFetch(false, _lastStatus, _lastSuccessfulAt);
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
