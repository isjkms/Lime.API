namespace Lime.Api.Features.Auth.Models;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public JwtOptions Jwt { get; set; } = new();
    public SessionCookieOptions Cookie { get; set; } = new();
    public OAuthProvidersOptions OAuth { get; set; } = new();
    public string WebBaseUrl { get; set; } = string.Empty;
}

public class JwtOptions
{
    public string Issuer { get; set; } = "lime";
    public string Audience { get; set; } = "lime";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

public class SessionCookieOptions
{
    public string AccessName { get; set; } = "lime_access";
    public string RefreshName { get; set; } = "lime_refresh";
    public string? Domain { get; set; }
    public bool Secure { get; set; } = true;
    public string SameSite { get; set; } = "Lax";
}

public class OAuthProvidersOptions
{
    public OAuthProviderOptions Google { get; set; } = new();
    public OAuthProviderOptions Kakao { get; set; } = new();
    public OAuthProviderOptions Naver { get; set; } = new();
}

public class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
