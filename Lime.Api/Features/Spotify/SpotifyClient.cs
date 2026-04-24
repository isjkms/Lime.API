using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Lime.Api.Features.Spotify;

public class SpotifyClient(HttpClient http, ISpotifyTokenProvider tokens)
{
    public async Task<JsonNode> GetAsync(string path, CancellationToken ct)
    {
        var token = await tokens.GetAppTokenAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retry = res.Headers.RetryAfter?.Delta?.TotalSeconds ?? 1;
            throw new SpotifyRateLimitedException((int)retry);
        }
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Spotify {(int)res.StatusCode} {res.StatusCode} on {path}: {body}");
        }

        return await res.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct)
               ?? throw new InvalidOperationException("empty spotify response");
    }
}

public class SpotifyRateLimitedException(int retryAfterSeconds)
    : Exception("spotify_rate_limited")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
