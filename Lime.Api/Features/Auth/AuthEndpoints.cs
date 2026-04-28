using System.Security.Claims;
using System.Security.Cryptography;
using Lime.Api.Data;
using Lime.Api.Features.Auth.Models;
using Lime.Api.Features.Auth.Services;
using Lime.Api.Features.Legal;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Auth;

public static class AuthEndpoints
{
    private const string StateCookie = "lime_oauth_state";
    private const string ReturnCookie = "lime_oauth_return";
    /// <summary>Web 미들웨어가 동의 게이트 판단에 쓰는 신호 cookie. "1" = 동의 완료.</summary>
    public const string ConsentOkCookie = "lime_consent_ok";

    public static void WriteConsentOkCookie(HttpContext ctx, SessionCookieOptions opt)
    {
        ctx.Response.Cookies.Append(ConsentOkCookie, "1", new CookieOptions
        {
            HttpOnly = false, // Web 미들웨어가 읽어야 하므로 JS 노출은 무관
            Secure = opt.Secure,
            SameSite = Enum.TryParse<SameSiteMode>(opt.SameSite, true, out var mode) ? mode : SameSiteMode.Lax,
            Domain = opt.Domain,
            Path = "/",
            Expires = DateTime.UtcNow.AddDays(365),
        });
    }

    public static void ClearConsentOkCookie(HttpContext ctx, SessionCookieOptions opt)
    {
        ctx.Response.Cookies.Delete(ConsentOkCookie, new CookieOptions { Path = "/", Domain = opt.Domain });
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth");

        g.MapGet("/{provider}/start", StartAsync);
        g.MapGet("/{provider}/callback", CallbackAsync);
        g.MapPost("/refresh", RefreshAsync);
        g.MapPost("/signout", SignOutAsync);
        g.MapGet("/me", MeAsync).RequireAuthorization();

        return app;
    }

    private static IResult StartAsync(
        string provider,
        string? returnTo,
        HttpContext ctx,
        OAuthProviderRegistry registry,
        IOptions<AuthOptions> opt)
    {
        if (!registry.TryGet(provider, out var p))
            return Results.NotFound(new { error = "unknown_provider" });

        var state = GenerateState();
        ctx.Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = opt.Value.Cookie.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            MaxAge = TimeSpan.FromMinutes(10),
        });
        ctx.Response.Cookies.Append(ReturnCookie, returnTo ?? "/", new CookieOptions
        {
            HttpOnly = true,
            Secure = opt.Value.Cookie.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            MaxAge = TimeSpan.FromMinutes(10),
        });

        var redirectUri = ResolveRedirectUri(ctx, provider, opt.Value);
        return Results.Redirect(p.BuildAuthorizeUrl(state, redirectUri));
    }

    private static async Task<IResult> CallbackAsync(
        string provider,
        string? code,
        string? state,
        HttpContext ctx,
        OAuthProviderRegistry registry,
        IUserLinker linker,
        ISessionService sessions,
        IConsentService consents,
        IOptions<AuthOptions> opt,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Results.BadRequest(new { error = "missing_code_or_state" });
        if (!registry.TryGet(provider, out var p))
            return Results.NotFound(new { error = "unknown_provider" });

        var cookieState = ctx.Request.Cookies[StateCookie];
        if (string.IsNullOrEmpty(cookieState) || cookieState != state)
            return Results.BadRequest(new { error = "state_mismatch" });

        var returnTo = ctx.Request.Cookies[ReturnCookie] ?? "/";
        ctx.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/auth" });
        ctx.Response.Cookies.Delete(ReturnCookie, new CookieOptions { Path = "/auth" });

        var redirectUri = ResolveRedirectUri(ctx, provider, opt.Value);
        var info = await p.ExchangeAndFetchAsync(code, redirectUri, ct);
        var user = await linker.ResolveAsync(info, ct);
        var tokens = await sessions.IssueAsync(user, ct);

        WriteSessionCookies(ctx, tokens, opt.Value.Cookie);

        // 동의 신호 cookie — 동의 완료면 발행, 아니면 명시적으로 회수.
        var consentOk = await consents.HasAllRequiredAsync(user.Id, ct);
        if (consentOk) WriteConsentOkCookie(ctx, opt.Value.Cookie);
        else ClearConsentOkCookie(ctx, opt.Value.Cookie);

        var webBase = string.IsNullOrWhiteSpace(opt.Value.WebBaseUrl) ? "/" : opt.Value.WebBaseUrl.TrimEnd('/');
        var target = returnTo.StartsWith("/") ? webBase + returnTo : returnTo;
        return Results.Redirect(target);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext ctx,
        ISessionService sessions,
        IOptions<AuthOptions> opt,
        CancellationToken ct)
    {
        var refresh = ctx.Request.Cookies[opt.Value.Cookie.RefreshName];
        if (string.IsNullOrEmpty(refresh)) return Results.Unauthorized();

        var tokens = await sessions.RotateAsync(refresh, ct);
        if (tokens is null) return Results.Unauthorized();

        WriteSessionCookies(ctx, tokens, opt.Value.Cookie);
        return Results.Ok(new { expiresAt = tokens.AccessExpiresAt });
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext ctx,
        ISessionService sessions,
        IOptions<AuthOptions> opt,
        CancellationToken ct)
    {
        var refresh = ctx.Request.Cookies[opt.Value.Cookie.RefreshName];
        if (!string.IsNullOrEmpty(refresh))
            await sessions.RevokeAsync(refresh, ct);

        ClearSessionCookies(ctx, opt.Value.Cookie);
        ClearConsentOkCookie(ctx, opt.Value.Cookie);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        HttpContext ctx, AppDbContext db, IConsentService consents, CancellationToken ct)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Results.Unauthorized();

        var consentOk = await consents.HasAllRequiredAsync(userId, ct);

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new
            {
                id = u.Id,
                email = u.Email,
                name = u.DisplayName,
                avatarUrl = u.AvatarUrl,
                bio = u.Bio,
                points = u.Points,
                nicknameChanges = u.NicknameChanges,
                createdAt = u.CreatedAt,
                providers = u.OAuthAccounts
                    .OrderBy(a => a.LinkedAt)
                    .Select(a => a.Provider)
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (user is null) return Results.Unauthorized();
        return Results.Ok(new
        {
            user.id, user.email, user.name, user.avatarUrl, user.bio,
            user.points, user.nicknameChanges, user.createdAt, user.providers,
            consentRequired = !consentOk,
        });
    }

    private static void WriteSessionCookies(HttpContext ctx, IssuedTokens tokens, SessionCookieOptions opt)
    {
        ctx.Response.Cookies.Append(opt.AccessName, tokens.AccessToken, BuildCookieOptions(opt, tokens.AccessExpiresAt, "/"));
        ctx.Response.Cookies.Append(opt.RefreshName, tokens.RefreshToken, BuildCookieOptions(opt, tokens.RefreshExpiresAt, "/auth"));
    }

    private static void ClearSessionCookies(HttpContext ctx, SessionCookieOptions opt)
    {
        ctx.Response.Cookies.Delete(opt.AccessName, new CookieOptions { Path = "/", Domain = opt.Domain });
        ctx.Response.Cookies.Delete(opt.RefreshName, new CookieOptions { Path = "/auth", Domain = opt.Domain });
    }

    private static CookieOptions BuildCookieOptions(SessionCookieOptions opt, DateTime expires, string path) => new()
    {
        HttpOnly = true,
        Secure = opt.Secure,
        SameSite = Enum.TryParse<SameSiteMode>(opt.SameSite, true, out var mode) ? mode : SameSiteMode.Lax,
        Domain = opt.Domain,
        Path = path,
        Expires = expires,
    };

    private static string ResolveRedirectUri(HttpContext ctx, string provider, AuthOptions opt)
    {
        var configured = provider.ToLowerInvariant() switch
        {
            "google" => opt.OAuth.Google.RedirectUri,
            "kakao" => opt.OAuth.Kakao.RedirectUri,
            "naver" => opt.OAuth.Naver.RedirectUri,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(configured)) return configured!;

        var req = ctx.Request;
        return $"{req.Scheme}://{req.Host}/auth/{provider}/callback";
    }

    private static string GenerateState() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
