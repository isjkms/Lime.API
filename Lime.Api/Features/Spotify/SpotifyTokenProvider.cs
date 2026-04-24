using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Spotify;

public interface ISpotifyTokenProvider
{
    Task<string> GetAppTokenAsync(CancellationToken ct = default);
}

public class SpotifyTokenProvider(IHttpClientFactory httpFactory, IOptions<SpotifyOptions> opt) : ISpotifyTokenProvider
{
    private readonly SpotifyOptions _opt = opt.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public async Task<string> GetAppTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && _expiresAtUtc > DateTime.UtcNow.AddSeconds(30))
            return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && _expiresAtUtc > DateTime.UtcNow.AddSeconds(30))
                return _token;

            if (string.IsNullOrWhiteSpace(_opt.ClientId) || string.IsNullOrWhiteSpace(_opt.ClientSecret))
                throw new InvalidOperationException("Spotify credentials not configured");

            var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                }),
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.ClientId}:{_opt.ClientSecret}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            var http = httpFactory.CreateClient();
            using var res = await http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var data = await res.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("empty spotify token response");

            _token = data.access_token;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(data.expires_in);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private record TokenResponse(string access_token, int expires_in);
}
