using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Lime.Api.Data;
using Lime.Api.Features.Auth.Models;
using Lime.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lime.Api.Features.Auth.Services;

public class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly AuthOptions _opt;

    public SessionService(AppDbContext db, IOptions<AuthOptions> opt)
    {
        _db = db;
        _opt = opt.Value;
    }

    public async Task<IssuedTokens> IssueAsync(User user, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var accessExp = now.AddMinutes(_opt.Jwt.AccessTokenMinutes);
        var refreshExp = now.AddDays(_opt.Jwt.RefreshTokenDays);

        var access = CreateAccessToken(user, now, accessExp);
        var refresh = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refresh),
            ExpiresAt = refreshExp,
        });
        await _db.SaveChangesAsync(ct);

        return new IssuedTokens(access, accessExp, refresh, refreshExp);
    }

    public async Task<IssuedTokens?> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var existing = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= DateTime.UtcNow || existing.User is null)
            return null;

        var issued = await IssueAsync(existing.User, ct);
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedById = await _db.RefreshTokens
            .Where(r => r.TokenHash == Hash(issued.RefreshToken))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
        await _db.SaveChangesAsync(ct);
        return issued;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (existing is null || existing.RevokedAt is not null) return;
        existing.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private string CreateAccessToken(User user, DateTime now, DateTime exp)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.DisplayName)) claims.Add(new("name", user.DisplayName));

        var token = new JwtSecurityToken(
            issuer: _opt.Jwt.Issuer,
            audience: _opt.Jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: exp,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
