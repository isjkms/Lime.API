using System.Security.Claims;
using Lime.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Points;

public static class PointsEndpoints
{
    public static IEndpointRouteBuilder MapPointsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users/me/points", GetMyPointsAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetMyPointsAsync(
        int? limit, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Results.Unauthorized();

        var take = Math.Clamp(limit ?? 30, 1, 100);

        var balance = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Points)
            .FirstOrDefaultAsync(ct);

        var transactions = await db.PointTransactions.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new
            {
                id = x.Id,
                delta = x.Delta,
                reason = x.Reason.ToString(),
                refType = x.RefType,
                refId = x.RefId,
                createdAt = x.CreatedAt,
            })
            .ToListAsync(ct);

        return Results.Ok(new { balance, transactions });
    }
}
