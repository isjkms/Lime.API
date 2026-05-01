using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Lime.Data;
using Lime.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Spotify;

public interface ISpotifyUserTokenService
{
    Task UpsertFromCodeAsync(Guid userId, string code, string redirectUri, CancellationToken ct);
    Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken ct);
    Task RevokeAsync(Guid userId, CancellationToken ct);
}

public class SpotifyUserTokenService(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    IOptions<SpotifyOptions> opt) : ISpotifyUserTokenService
{
    private readonly SpotifyOptions _opt = opt.Value;

    public async Task UpsertFromCodeAsync(Guid userId, string code, string redirectUri, CancellationToken ct)
    {
        var data = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        }, ct);

        var spotifyUserId = await FetchSpotifyUserIdAsync(data.access_token, ct);
        var now = DateTime.UtcNow;

        var existing = await db.UserSpotifyLinks.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is null)
        {
            db.UserSpotifyLinks.Add(new UserSpotifyLink
            {
                UserId = userId,
                SpotifyUserId = spotifyUserId,
                RefreshToken = data.refresh_token ?? throw new InvalidOperationException("missing refresh_token"),
                AccessToken = data.access_token,
                AccessExpiresAt = now.AddSeconds(data.expires_in),
                Scope = data.scope,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.SpotifyUserId = spotifyUserId ?? existing.SpotifyUserId;
            if (!string.IsNullOrEmpty(data.refresh_token)) existing.RefreshToken = data.refresh_token;
            existing.AccessToken = data.access_token;
            existing.AccessExpiresAt = now.AddSeconds(data.expires_in);
            existing.Scope = data.scope ?? existing.Scope;
            existing.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken ct)
    {
        var link = await db.UserSpotifyLinks.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (link is null) return null;

        if (!string.IsNullOrEmpty(link.AccessToken)
            && link.AccessExpiresAt is { } exp
            && exp > DateTime.UtcNow.AddSeconds(30))
        {
            return link.AccessToken;
        }

        var data = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = link.RefreshToken,
        }, ct);

        var now = DateTime.UtcNow;
        link.AccessToken = data.access_token;
        link.AccessExpiresAt = now.AddSeconds(data.expires_in);
        if (!string.IsNullOrEmpty(data.refresh_token)) link.RefreshToken = data.refresh_token;
        if (!string.IsNullOrEmpty(data.scope)) link.Scope = data.scope;
        link.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return link.AccessToken;
    }

    public async Task RevokeAsync(Guid userId, CancellationToken ct)
    {
        await db.UserSpotifyLinks.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
    }

    private async Task<TokenResponse> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        var http = httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(form),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.ClientId}:{_opt.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Spotify token endpoint {(int)res.StatusCode}: {body}");
        }
        return await res.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
               ?? throw new InvalidOperationException("empty spotify token response");
    }

    private async Task<string?> FetchSpotifyUserIdAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var me = await res.Content.ReadFromJsonAsync<MeResponse>(cancellationToken: ct);
            return me?.id;
        }
        catch
        {
            return null;
        }
    }

    private record TokenResponse(string access_token, int expires_in, string? refresh_token, string? scope, string? token_type);
    private record MeResponse(string? id);
}
