namespace Lime.Admin.Features.Auth.Services;

using System.Security.Claims;
using Lime.Admin.Data;
using Lime.Admin.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class AuthService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);

    private readonly AdminDbContext _db;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;

    public AuthService(
        AdminDbContext db,
        IPasswordHasher<AdminUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> LoginAsync(HttpContext httpContext, string email, string password)
    {
        var now = DateTime.UtcNow;
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var adminUser = await _db.AdminUsers
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail);

        if (adminUser is null || !adminUser.IsActive)
        {
            return false;
        }

        if (adminUser.LockedUntil is not null && adminUser.LockedUntil > now)
        {
            return false;
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(
            adminUser,
            adminUser.PasswordHash,
            password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            adminUser.FailedLoginAttempts += 1;

            if (adminUser.FailedLoginAttempts >= 5)
            {
                adminUser.LockedUntil = now.AddMinutes(15);
            }

            adminUser.UpdatedAt = now;

            await _db.SaveChangesAsync();
            return false;
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, password);
        }

        adminUser.FailedLoginAttempts = 0;
        adminUser.LockedUntil = null;
        adminUser.LastLoginAt = now;
        adminUser.UpdatedAt = now;

        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUser.Id,
            Action = "Login",
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new Claim(ClaimTypes.Name, adminUser.DisplayName),
            new Claim(ClaimTypes.Email, adminUser.Email),
            new Claim(ClaimTypes.Role, adminUser.Role.ToString()),
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                AllowRefresh = true,
                ExpiresUtc = new DateTimeOffset(now.Add(SessionLifetime)),
                IssuedUtc = new DateTimeOffset(now),
            });

        return true;
    }

    public async Task LogoutAsync(HttpContext httpContext)
    {
        var adminUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(adminUserId, out var parsedAdminUserId))
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                Id = Guid.NewGuid(),
                AdminUserId = parsedAdminUserId,
                Action = "Logout",
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
                CreatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
