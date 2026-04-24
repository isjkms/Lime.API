using System.Net.Http.Headers;
using System.Text.Json;
using Lime.Api.Features.Auth.Models;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Auth.Services;

public class KakaoOAuthProvider : IOAuthProvider
{
    public string Name => "kakao";

    private readonly HttpClient _http;
    private readonly OAuthProviderOptions _opt;

    public KakaoOAuthProvider(HttpClient http, IOptions<AuthOptions> auth)
    {
        _http = http;
        _opt = auth.Value.OAuth.Kakao;
    }

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"] = _opt.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "account_email profile_nickname profile_image",
            ["state"] = state,
        };
        return GoogleOAuthProvider.QueryHelpers("https://kauth.kakao.com/oauth/authorize", q);
    }

    public async Task<OAuthUserInfo> ExchangeAndFetchAsync(string code, string redirectUri, CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _opt.ClientId,
            ["redirect_uri"] = redirectUri,
            ["code"] = code,
        };
        if (!string.IsNullOrEmpty(_opt.ClientSecret))
            body["client_secret"] = _opt.ClientSecret;

        var tokenRes = await _http.PostAsync("https://kauth.kakao.com/oauth/token", new FormUrlEncodedContent(body), ct);
        tokenRes.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync(ct));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "https://kapi.kakao.com/v2/user/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;

        var id = root.GetProperty("id").GetRawText();
        string? email = null;
        bool emailVerified = false;
        string? nickname = null;
        string? avatar = null;

        if (root.TryGetProperty("kakao_account", out var acct))
        {
            if (acct.TryGetProperty("email", out var em)) email = em.GetString();
            if (acct.TryGetProperty("is_email_valid", out var ev1) && ev1.GetBoolean() &&
                acct.TryGetProperty("is_email_verified", out var ev2) && ev2.GetBoolean())
                emailVerified = true;

            if (acct.TryGetProperty("profile", out var profile))
            {
                if (profile.TryGetProperty("nickname", out var nk)) nickname = nk.GetString();
                if (profile.TryGetProperty("profile_image_url", out var img)) avatar = img.GetString();
            }
        }

        return new OAuthUserInfo(
            Provider: Name,
            ProviderUserId: id,
            Email: email,
            EmailVerified: emailVerified,
            Name: nickname,
            AvatarUrl: avatar);
    }
}
