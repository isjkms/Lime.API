using Lime.Api.Models;

namespace Lime.Api.Features.Auth.Services;

public interface IUserLinker
{
    Task<User> ResolveAsync(OAuthUserInfo info, CancellationToken ct);
}
