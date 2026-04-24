using System.Security.Claims;
using System.Security.Cryptography;
using Lime.Api.Features.Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Spotify;

public static class SpotifyConnectEndpoints
{
    private const string StateCookie = "lime_spotify_oauth_state";
    private const string RedirectCookie = "lime_spotify_oauth_redirect";
    private const string ReturnCookie = "lime_spotify_oauth_return";

    public static IEndpointRouteBuilder MapSpotifyConnectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/spotify");
        g.MapGet("/connect/start", StartAsync).RequireAuthorization();
        g.MapGet("/connect/callback", CallbackAsync);
        g.MapGet("/user-token", UserTokenAsync).RequireAuthorization();
        g.MapPost("/disconnect", DisconnectAsync).RequireAuthorization();
        return app;
    }

    private static IResult StartAsync(
        string? returnTo,
        HttpContext ctx,
        IOptions<SpotifyOptions> spotifyOpt,
        IOptions<AuthOptions> authOpt)
    {
        var opt = spotifyOpt.Value;
        if (string.IsNullOrWhiteSpace(opt.ClientId))
            return Results.Problem("Spotify not configured");

        var redirectUri = ResolveRedirectUri(ctx, opt);
        var state = GenerateState();
        var safeReturn = (returnTo is not null && returnTo.StartsWith("/")) ? returnTo : "/";

        ctx.Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = authOpt.Value.Cookie.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/spotify",
            MaxAge = TimeSpan.FromMinutes(10),
        });
        ctx.Response.Cookies.Append(RedirectCookie, redirectUri, new CookieOptions
        {
            HttpOnly = true,
            Secure = authOpt.Value.Cookie.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/spotify",
            MaxAge = TimeSpan.FromMinutes(10),
        });
        ctx.Response.Cookies.Append(ReturnCookie, safeReturn, new CookieOptions
        {
            HttpOnly = true,
            Secure = authOpt.Value.Cookie.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/spotify",
            MaxAge = TimeSpan.FromMinutes(10),
        });

        var url = "https://accounts.spotify.com/authorize?" +
                  $"client_id={Uri.EscapeDataString(opt.ClientId)}" +
                  "&response_type=code" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&scope={Uri.EscapeDataString(opt.Scopes)}" +
                  $"&state={Uri.EscapeDataString(state)}";
        return Results.Redirect(url);
    }

    private static async Task<IResult> CallbackAsync(
        string? code,
        string? state,
        HttpContext ctx,
        ISpotifyUserTokenService tokens,
        IOptions<SpotifyOptions> spotifyOpt,
        IOptions<AuthOptions> authOpt,
        CancellationToken ct)
    {
        var webBase = string.IsNullOrWhiteSpace(authOpt.Value.WebBaseUrl) ? "/" : authOpt.Value.WebBaseUrl.TrimEnd('/');
        var cookieState = ctx.Request.Cookies[StateCookie];
        var redirectUri = ctx.Request.Cookies[RedirectCookie] ?? ResolveRedirectUri(ctx, spotifyOpt.Value);
        var returnPath = ctx.Request.Cookies[ReturnCookie] ?? "/";
        if (!returnPath.StartsWith("/")) returnPath = "/";
        ctx.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/spotify" });
        ctx.Response.Cookies.Delete(RedirectCookie, new CookieOptions { Path = "/spotify" });
        ctx.Response.Cookies.Delete(ReturnCookie, new CookieOptions { Path = "/spotify" });

        string AppendQuery(string path, string qs)
        {
            var sep = path.Contains('?') ? "&" : "?";
            return $"{webBase}{path}{sep}{qs}";
        }

        if (string.IsNullOrEmpty(code)) return Results.Redirect(AppendQuery(returnPath, "spotify=error&reason=no_code"));
        if (string.IsNullOrEmpty(state)) return Results.Redirect(AppendQuery(returnPath, "spotify=error&reason=no_state"));
        if (state != cookieState) return Results.Redirect(AppendQuery(returnPath, "spotify=error&reason=state_mismatch"));

        if (!TryGetUserId(ctx, out var userId))
            return Results.Redirect(AppendQuery(returnPath, "spotify=error&reason=auth_required"));

        try
        {
            await tokens.UpsertFromCodeAsync(userId, code, redirectUri, ct);
        }
        catch (Exception ex)
        {
            var msg = Uri.EscapeDataString(ex.Message);
            return Results.Redirect(AppendQuery(returnPath, $"spotify=error&reason=exchange_failed&msg={msg}"));
        }
        return Results.Redirect(AppendQuery(returnPath, "spotify=ok"));
    }

    private static async Task<IResult> UserTokenAsync(
        HttpContext ctx,
        ISpotifyUserTokenService tokens,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var token = await tokens.GetAccessTokenAsync(userId, ct);
        return Results.Ok(new { token });
    }

    private static async Task<IResult> DisconnectAsync(
        HttpContext ctx,
        ISpotifyUserTokenService tokens,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        await tokens.RevokeAsync(userId, ct);
        return Results.NoContent();
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }

    private static string ResolveRedirectUri(HttpContext ctx, SpotifyOptions opt)
    {
        if (!string.IsNullOrWhiteSpace(opt.RedirectUri)) return opt.RedirectUri;
        var req = ctx.Request;
        return $"{req.Scheme}://{req.Host}/spotify/connect/callback";
    }

    private static string GenerateState() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
