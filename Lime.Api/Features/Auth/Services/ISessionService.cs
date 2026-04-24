using Lime.Api.Models;

namespace Lime.Api.Features.Auth.Services;

public record IssuedTokens(string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);

public interface ISessionService
{
    Task<IssuedTokens> IssueAsync(User user, CancellationToken ct);
    Task<IssuedTokens?> RotateAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}
