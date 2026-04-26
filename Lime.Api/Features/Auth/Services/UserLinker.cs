using Lime.Api.Data;
using Lime.Api.Features.Points;
using Lime.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Auth.Services;

public class UserLinker : IUserLinker
{
    private readonly AppDbContext _db;

    public UserLinker(AppDbContext db) { _db = db; }

    public async Task<User> ResolveAsync(OAuthUserInfo info, CancellationToken ct)
    {
        var existingLink = await _db.UserOAuthAccounts
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Provider == info.Provider && o.ProviderUserId == info.ProviderUserId, ct);

        if (existingLink?.User is not null)
        {
            existingLink.User.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existingLink.User;
        }

        User? user = null;
        if (!string.IsNullOrWhiteSpace(info.Email) && info.EmailVerified)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == info.Email && u.DeletedAt == null, ct);
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = info.Email,
                DisplayName = !string.IsNullOrWhiteSpace(info.Name) ? info.Name! : "user",
                AvatarUrl = info.AvatarUrl,
                Points = PointsConfig.WelcomeBonus,
                LastLoginAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
            _db.PointTransactions.Add(new PointTransaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Delta = PointsConfig.WelcomeBonus,
                Reason = PointReason.WelcomeBonus,
            });
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
        }

        _db.UserOAuthAccounts.Add(new UserOAuthAccount
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = info.Provider,
            ProviderUserId = info.ProviderUserId,
            Email = info.Email,
        });

        await _db.SaveChangesAsync(ct);
        return user;
    }
}
