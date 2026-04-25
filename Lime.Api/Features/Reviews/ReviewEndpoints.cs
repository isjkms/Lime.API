using System.Security.Claims;
using Lime.Api.Data;
using Lime.Api.Features.Catalog;
using Lime.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Reviews;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/reviews");
        g.MapGet("/", ListAsync);
        g.MapGet("/feed", FeedAsync);
        g.MapGet("/famous-feed", FamousFeedAsync);
        g.MapPost("/", CreateAsync).RequireAuthorization();
        g.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization();
        g.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization();
        g.MapPost("/{id:guid}/reactions", ReactAsync).RequireAuthorization();
        g.MapDelete("/{id:guid}/reactions", UnreactAsync).RequireAuthorization();

        app.MapGet("/users/{userId:guid}/reviews", ListByUserAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        string? target, string? spotifyId, string? sort, int page, int pageSize,
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(spotifyId))
            return Results.BadRequest(new { error = "missing_target" });

        var q = db.Reviews.AsNoTracking().Where(r => r.DeletedAt == null);
        q = target switch
        {
            "track" => q.Where(r => r.Track!.SpotifyId == spotifyId),
            "album" => q.Where(r => r.Album!.SpotifyId == spotifyId),
            _ => q.Where(r => false),
        };

        return await PageAsync(q, sort, page, pageSize, GetUserId(ctx), db, ct);
    }

    private static async Task<IResult> ListByUserAsync(
        Guid userId, string? sort, int page, int pageSize,
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var q = db.Reviews.AsNoTracking().Where(r => r.DeletedAt == null && r.UserId == userId);
        return await PageAsync(q, sort, page, pageSize, GetUserId(ctx), db, ct);
    }

    private static async Task<IResult> FeedAsync(
        string? sort, int page, int pageSize,
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var q = db.Reviews.AsNoTracking().Where(r => r.DeletedAt == null);
        return await PageAsync(q, sort ?? "recent", page, pageSize, GetUserId(ctx), db, ct);
    }

    private static async Task<IResult> FamousFeedAsync(
        int? limit, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 8, 1, 50);
        var famousIds = db.Users
            .Where(u => u.DeletedAt == null
                && db.Reviews.Count(r => r.UserId == u.Id && r.DeletedAt == null) >= 1000
                && db.ReviewReactions.Count(x =>
                    x.Kind == ReactionKind.Like && x.Review!.UserId == u.Id && x.Review.DeletedAt == null) >= 1000)
            .Select(u => u.Id);

        var q = db.Reviews.AsNoTracking()
            .Where(r => r.DeletedAt == null && famousIds.Contains(r.UserId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(take);

        return await PageProjectAsync(q, GetUserId(ctx), db, ct);
    }

    private static async Task<IResult> PageProjectAsync(
        IQueryable<Review> q, Guid? viewerId, AppDbContext db, CancellationToken ct)
    {
        var rows = await q.Select(r => new
        {
            id = r.Id,
            rating = r.Rating,
            body = r.Body,
            createdAt = r.CreatedAt,
            user = new
            {
                id = r.User!.Id,
                name = r.User.DisplayName,
                avatarUrl = r.User.AvatarUrl,
            },
            target = r.TrackId != null ? "track" : "album",
            track = r.Track == null ? null : new
            {
                id = r.Track.Id,
                spotifyId = r.Track.SpotifyId,
                name = r.Track.Name,
                coverUrl = r.Track.Album!.CoverUrl,
                artists = r.Track.Artists,
            },
            album = r.Album == null ? null : new
            {
                id = r.Album.Id,
                spotifyId = r.Album.SpotifyId,
                name = r.Album.Name,
                coverUrl = r.Album.CoverUrl,
                artists = r.Album.Artists,
            },
        }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateAsync(
        CreateReviewRequest req, HttpContext ctx, AppDbContext db, ICatalogService catalog,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        if (req.Rating < 0.5m || req.Rating > 10m) return Results.BadRequest(new { error = "invalid_rating" });
        var body = (req.Body ?? "").Trim();
        if (body.Length == 0 || body.Length > 140) return Results.BadRequest(new { error = "invalid_body" });

        Guid? trackId = null, albumId = null;
        if (req.Target == "track")
            trackId = (await catalog.EnsureTrackAsync(req.SpotifyId, ct)).Id;
        else if (req.Target == "album")
            albumId = (await catalog.EnsureAlbumAsync(req.SpotifyId, ct)).Id;
        else
            return Results.BadRequest(new { error = "invalid_target" });

        var existing = await db.Reviews.FirstOrDefaultAsync(
            r => r.UserId == userId && r.TrackId == trackId && r.AlbumId == albumId && r.DeletedAt == null, ct);
        if (existing is not null)
        {
            existing.Rating = req.Rating;
            existing.Body = body;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { id = existing.Id });
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrackId = trackId,
            AlbumId = albumId,
            Rating = req.Rating,
            Body = body,
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/reviews/{review.Id}", new { id = review.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateReviewRequest req, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, ct);
        if (review is null) return Results.NotFound();
        if (review.UserId != userId) return Results.Forbid();

        if (req.Rating is decimal rating)
        {
            if (rating < 0.5m || rating > 10m) return Results.BadRequest(new { error = "invalid_rating" });
            review.Rating = rating;
        }
        if (req.Body is string body)
        {
            var trimmed = body.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 140) return Results.BadRequest(new { error = "invalid_body" });
            review.Body = trimmed;
        }
        review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, ct);
        if (review is null) return Results.NotFound();
        if (review.UserId != userId) return Results.Forbid();

        review.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReactAsync(
        Guid id, ReactionRequest req, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var kind = req.Kind switch
        {
            "like" => ReactionKind.Like,
            "dislike" => ReactionKind.Dislike,
            _ => (ReactionKind?)null,
        };
        if (kind is null) return Results.BadRequest(new { error = "invalid_kind" });

        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, ct);
        if (review is null) return Results.NotFound();
        if (review.UserId == userId) return Results.BadRequest(new { error = "self_reaction" });

        var existing = await db.ReviewReactions.FirstOrDefaultAsync(
            x => x.ReviewId == id && x.UserId == userId, ct);
        if (existing is null)
        {
            db.ReviewReactions.Add(new ReviewReaction
            {
                ReviewId = id,
                UserId = userId,
                Kind = kind.Value,
            });
        }
        else
        {
            existing.Kind = kind.Value;
        }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UnreactAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        await db.ReviewReactions
            .Where(x => x.ReviewId == id && x.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PageAsync(
        IQueryable<Review> q, string? sort, int page, int pageSize, Guid? viewerId,
        AppDbContext db, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var total = await q.CountAsync(ct);

        IQueryable<Review> ordered = sort == "top"
            ? q.OrderByDescending(r => r.Reactions.Count(x => x.Kind == ReactionKind.Like))
               .ThenByDescending(r => r.CreatedAt)
            : q.OrderByDescending(r => r.CreatedAt);

        var rows = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                id = r.Id,
                rating = r.Rating,
                body = r.Body,
                createdAt = r.CreatedAt,
                updatedAt = r.UpdatedAt,
                user = new
                {
                    id = r.User!.Id,
                    name = r.User.DisplayName,
                    avatarUrl = r.User.AvatarUrl,
                    reviewCount = db.Reviews.Count(x => x.UserId == r.UserId && x.DeletedAt == null),
                    likesReceived = db.ReviewReactions.Count(x =>
                        x.Kind == ReactionKind.Like && x.Review!.UserId == r.UserId && x.Review.DeletedAt == null),
                },
                target = r.TrackId != null ? "track" : "album",
                track = r.Track == null ? null : new
                {
                    id = r.Track.Id,
                    spotifyId = r.Track.SpotifyId,
                    name = r.Track.Name,
                    albumId = r.Track.AlbumId,
                    coverUrl = r.Track.Album!.CoverUrl,
                    artists = r.Track.Artists,
                },
                album = r.Album == null ? null : new
                {
                    id = r.Album.Id,
                    spotifyId = r.Album.SpotifyId,
                    name = r.Album.Name,
                    coverUrl = r.Album.CoverUrl,
                    artists = r.Album.Artists,
                },
                likes = r.Reactions.Count(x => x.Kind == ReactionKind.Like),
                dislikes = r.Reactions.Count(x => x.Kind == ReactionKind.Dislike),
                myReaction = viewerId == null ? null
                    : r.Reactions.Where(x => x.UserId == viewerId)
                        .Select(x => x.Kind == ReactionKind.Like ? "like" : "dislike")
                        .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items = rows });
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }

    private static Guid? GetUserId(HttpContext ctx)
        => TryGetUserId(ctx, out var id) ? id : null;
}
