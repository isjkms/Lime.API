using System.Net.Http.Headers;
using System.Text.Json;
using Lime.Api.Features.Auth.Models;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Auth.Services;

public class NaverOAuthProvider : IOAuthProvider
{
    public string Name => "naver";

    private readonly HttpClient _http;
    private readonly OAuthProviderOptions _opt;

    public NaverOAuthProvider(HttpClient http, IOptions<AuthOptions> auth)
    {
        _http = http;
        _opt = auth.Value.OAuth.Naver;
    }

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var q = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _opt.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
        };
        return GoogleOAuthProvider.QueryHelpers("https://nid.naver.com/oauth2.0/authorize", q);
    }

    public async Task<OAuthUserInfo> ExchangeAndFetchAsync(string code, string redirectUri, CancellationToken ct)
    {
        var tokenUrl = GoogleOAuthProvider.QueryHelpers("https://nid.naver.com/oauth2.0/token", new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        });
        var tokenRes = await _http.GetAsync(tokenUrl, ct);
        tokenRes.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync(ct));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "https://openapi.naver.com/v1/nid/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var resp = json.RootElement.GetProperty("response");

        var id = resp.GetProperty("id").GetString()!;
        string? email = resp.TryGetProperty("email", out var em) ? em.GetString() : null;
        string? nickname = resp.TryGetProperty("nickname", out var nk) ? nk.GetString() : null;
        string? avatar = resp.TryGetProperty("profile_image", out var pi) ? pi.GetString() : null;

        return new OAuthUserInfo(
            Provider: Name,
            ProviderUserId: id,
            Email: email,
            EmailVerified: !string.IsNullOrEmpty(email),
            Name: nickname,
            AvatarUrl: avatar);
    }
}
