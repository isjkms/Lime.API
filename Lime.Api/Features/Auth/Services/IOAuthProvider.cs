namespace Lime.Api.Features.Auth.Services;

public record OAuthUserInfo(
    string Provider,
    string ProviderUserId,
    string? Email,
    bool EmailVerified,
    string? Name,
    string? AvatarUrl);

public interface IOAuthProvider
{
    string Name { get; }
    string BuildAuthorizeUrl(string state, string redirectUri);
    Task<OAuthUserInfo> ExchangeAndFetchAsync(string code, string redirectUri, CancellationToken ct);
}
