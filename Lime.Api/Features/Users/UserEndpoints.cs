using System.Security.Claims;
using Lime.Data;
using Lime.Data.Models;
using Lime.Api.Features.Legal;
using Lime.Api.Features.Points;
using Lime.Api.Features.Storage;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{id:guid}", GetUserAsync);
        app.MapGet("/users/leaderboard", LeaderboardAsync);
        app.MapPatch("/users/me", UpdateMeAsync).RequireAuthorization().RequireConsent();
        // 탈퇴는 미동의 사용자도 가능해야 함 (가입 취소 경로) → RequireConsent X
        app.MapDelete("/users/me", DeleteMeAsync).RequireAuthorization();
        app.MapPost("/users/me/avatar", UploadAvatarAsync)
            .RequireAuthorization()
            .RequireConsent()
            .DisableAntiforgery();
        return app;
    }

    private static readonly Dictionary<string, string> AvatarMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    private static async Task<IResult> UploadAvatarAsync(
        HttpContext ctx, IFormFile? file, AppDbContext db,
        IAvatarStorage storage, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "missing_file" });
        if (file.Length > 2 * 1024 * 1024)
            return Results.BadRequest(new { error = "file_too_large" });
        if (!AvatarMime.TryGetValue(file.ContentType, out var ext))
            return Results.BadRequest(new { error = "invalid_content_type" });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return Results.Unauthorized();

        var previousUrl = user.AvatarUrl;

        await using var stream = file.OpenReadStream();
        var url = await storage.SaveAsync(userId, stream, file.ContentType, ext, ct);

        user.AvatarUrl = url;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // 이전 아바타 정리 (best-effort)
        await storage.TryDeleteAsync(previousUrl, ct);

        return Results.Ok(new { url });
    }

    public record UpdateMeRequest(string? DisplayName, string? AvatarUrl, string? Bio);

    private static async Task<IResult> UpdateMeAsync(
        UpdateMeRequest req, HttpContext ctx, AppDbContext db,
        IPointsService points, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return Results.Unauthorized();

        if (req.DisplayName is string dn)
        {
            var trimmed = dn.Trim();
            if (trimmed.Length < 1 || trimmed.Length > 32)
                return Results.BadRequest(new { error = "invalid_display_name" });

            if (trimmed != user.DisplayName)
            {
                if (user.NicknameChanges >= 1)
                {
                    var paid = await points.TryAdjustAsync(userId, -PointsConfig.NicknameChangeCost,
                        PointReason.NicknameChange, "user", userId, ct);
                    if (!paid)
                        return Results.BadRequest(new
                        {
                            error = "not_enough_points",
                            required = PointsConfig.NicknameChangeCost,
                        });
                }
                user.NicknameChanges += 1;
                user.DisplayName = trimmed;
            }
        }
        if (req.AvatarUrl is string au)
        {
            var trimmed = au.Trim();
            if (trimmed.Length == 0) user.AvatarUrl = null;
            else
            {
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return Results.BadRequest(new { error = "invalid_avatar_url" });
                user.AvatarUrl = trimmed;
            }
        }
        if (req.Bio is string bio)
        {
            var trimmed = bio.Trim();
            if (trimmed.Length > 200) return Results.BadRequest(new { error = "invalid_bio" });
            user.Bio = trimmed.Length == 0 ? null : trimmed;
        }
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = user.Id,
            email = user.Email,
            name = user.DisplayName,
            avatarUrl = user.AvatarUrl,
            bio = user.Bio,
        });
    }

    private static async Task<IResult> DeleteMeAsync(
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return Results.NoContent();

        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }

    private static async Task<IResult> GetUserAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var viewerId = TryGetUserId(ctx, out var v) ? (Guid?)v : null;

        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == id && u.DeletedAt == null)
            .Select(u => new
            {
                id = u.Id,
                displayName = u.DisplayName,
                avatarUrl = u.AvatarUrl,
                bio = u.Bio,
                createdAt = u.CreatedAt,
                reviewCount = db.Reviews.Count(r => r.UserId == u.Id && r.DeletedAt == null),
                likesReceived = db.ReviewReactions.Count(x =>
                    x.Kind == ReactionKind.Like && x.Review!.UserId == u.Id && x.Review.DeletedAt == null),
                followersCount = db.Follows.Count(f => f.FolloweeId == u.Id),
                followingCount = db.Follows.Count(f => f.FollowerId == u.Id),
                isFollowing = viewerId != null && db.Follows.Any(
                    f => f.FollowerId == viewerId && f.FolloweeId == u.Id),
            })
            .FirstOrDefaultAsync(ct);
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> LeaderboardAsync(
        string? sort, int? limit, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 100, 1, 200);

        var q = db.Users.AsNoTracking()
            .Where(u => u.DeletedAt == null)
            .Select(u => new
            {
                id = u.Id,
                displayName = u.DisplayName,
                avatarUrl = u.AvatarUrl,
                reviewCount = db.Reviews.Count(r => r.UserId == u.Id && r.DeletedAt == null),
                likesReceived = db.ReviewReactions.Count(x =>
                    x.Kind == ReactionKind.Like && x.Review!.UserId == u.Id && x.Review.DeletedAt == null),
            })
            .Where(x => x.reviewCount > 0);

        var ordered = sort == "count"
            ? q.OrderByDescending(x => x.reviewCount).ThenByDescending(x => x.likesReceived)
            : q.OrderByDescending(x => x.likesReceived).ThenByDescending(x => x.reviewCount);

        var rows = await ordered.Take(take).ToListAsync(ct);
        return Results.Ok(rows);
    }
}
