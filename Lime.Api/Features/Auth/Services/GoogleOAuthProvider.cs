using System.Net.Http.Headers;
using System.Text.Json;
using Lime.Api.Features.Auth.Models;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Auth.Services;

public class GoogleOAuthProvider : IOAuthProvider
{
    public string Name => "google";

    private readonly HttpClient _http;
    private readonly OAuthProviderOptions _opt;

    public GoogleOAuthProvider(HttpClient http, IOptions<AuthOptions> auth)
    {
        _http = http;
        _opt = auth.Value.OAuth.Google;
    }

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"] = _opt.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "select_account",
        };
        return QueryHelpers("https://accounts.google.com/o/oauth2/v2/auth", q);
    }

    public async Task<OAuthUserInfo> ExchangeAndFetchAsync(string code, string redirectUri, CancellationToken ct)
    {
        var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });
        var tokenRes = await _http.PostAsync("https://oauth2.googleapis.com/token", tokenReq, ct);
        tokenRes.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync(ct));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;

        return new OAuthUserInfo(
            Provider: Name,
            ProviderUserId: root.GetProperty("sub").GetString()!,
            Email: root.TryGetProperty("email", out var em) ? em.GetString() : null,
            EmailVerified: root.TryGetProperty("email_verified", out var ev) && ev.GetBoolean(),
            Name: root.TryGetProperty("name", out var n) ? n.GetString() : null,
            AvatarUrl: root.TryGetProperty("picture", out var p) ? p.GetString() : null);
    }

    internal static string QueryHelpers(string url, IDictionary<string, string?> q)
    {
        var pairs = q.Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return url + "?" + string.Join("&", pairs);
    }
}
