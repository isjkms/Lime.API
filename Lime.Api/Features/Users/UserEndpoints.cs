using Lime.Api.Data;
using Lime.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{id:guid}", GetUserAsync);
        app.MapGet("/users/leaderboard", LeaderboardAsync);
        return app;
    }

    private static async Task<IResult> GetUserAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
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
