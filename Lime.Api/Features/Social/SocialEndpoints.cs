using System.Security.Claims;
using Lime.Api.Data;
using Lime.Api.Features.Legal;
using Lime.Api.Features.Notifications;
using Lime.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Social;

public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users/{id:guid}/follow", FollowAsync).RequireAuthorization().RequireConsent();
        app.MapDelete("/users/{id:guid}/follow", UnfollowAsync).RequireAuthorization().RequireConsent();
        app.MapGet("/users/{id:guid}/followers", FollowersAsync);
        app.MapGet("/users/{id:guid}/following", FollowingAsync);
        return app;
    }

    private static async Task<IResult> FollowAsync(
        Guid id, HttpContext ctx, AppDbContext db, INotificationService notifications,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var meId)) return Results.Unauthorized();
        if (meId == id) return Results.BadRequest(new { error = "self_follow" });

        var targetExists = await db.Users.AnyAsync(u => u.Id == id && u.DeletedAt == null, ct);
        if (!targetExists) return Results.NotFound();

        var existing = await db.Follows.AnyAsync(
            f => f.FollowerId == meId && f.FolloweeId == id, ct);
        if (!existing)
        {
            db.Follows.Add(new Follow { FollowerId = meId, FolloweeId = id });
            await db.SaveChangesAsync(ct);
            await notifications.NotifyNewFollowerAsync(id, meId, ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UnfollowAsync(
        Guid id, HttpContext ctx, AppDbContext db, INotificationService notifications,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var meId)) return Results.Unauthorized();
        await db.Follows
            .Where(f => f.FollowerId == meId && f.FolloweeId == id)
            .ExecuteDeleteAsync(ct);
        await notifications.RemoveNewFollowerAsync(id, meId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> FollowersAsync(
        Guid id, int? page, int? pageSize, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var (p, ps) = ClampPaging(page, pageSize);
        var viewerId = GetUserId(ctx);

        var q = db.Follows.AsNoTracking()
            .Where(f => f.FolloweeId == id)
            .OrderByDescending(f => f.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((p - 1) * ps).Take(ps)
            .Select(f => new
            {
                id = f.Follower!.Id,
                displayName = f.Follower.DisplayName,
                avatarUrl = f.Follower.AvatarUrl,
                followedAt = f.CreatedAt,
                isFollowing = viewerId != null && db.Follows.Any(
                    x => x.FollowerId == viewerId && x.FolloweeId == f.Follower.Id),
            })
            .ToListAsync(ct);

        return Results.Ok(new { total, page = p, pageSize = ps, items });
    }

    private static async Task<IResult> FollowingAsync(
        Guid id, int? page, int? pageSize, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var (p, ps) = ClampPaging(page, pageSize);
        var viewerId = GetUserId(ctx);

        var q = db.Follows.AsNoTracking()
            .Where(f => f.FollowerId == id)
            .OrderByDescending(f => f.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((p - 1) * ps).Take(ps)
            .Select(f => new
            {
                id = f.Followee!.Id,
                displayName = f.Followee.DisplayName,
                avatarUrl = f.Followee.AvatarUrl,
                followedAt = f.CreatedAt,
                isFollowing = viewerId != null && db.Follows.Any(
                    x => x.FollowerId == viewerId && x.FolloweeId == f.Followee.Id),
            })
            .ToListAsync(ct);

        return Results.Ok(new { total, page = p, pageSize = ps, items });
    }

    private static (int page, int pageSize) ClampPaging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var ps = pageSize ?? 30;
        if (p < 1) p = 1;
        if (ps < 1 || ps > 100) ps = 30;
        return (p, ps);
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }

    private static Guid? GetUserId(HttpContext ctx)
        => TryGetUserId(ctx, out var id) ? id : null;
}
