using System.Security.Claims;
using System.Text.Json;
using Lime.Data;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/notifications").RequireAuthorization();
        g.MapGet("/", ListAsync);
        g.MapGet("/unread-count", UnreadCountAsync);
        g.MapPost("/seen", MarkAllSeenAsync);
        g.MapDelete("/", ClearAllAsync);
        g.MapDelete("/{id:guid}", DismissAsync);
        g.MapGet("/stream", StreamAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        int? limit, HttpContext ctx, AppDbContext db, NotificationService svc, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var take = Math.Clamp(limit ?? 30, 1, 100);

        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new
            {
                n.Id,
                n.Kind,
                n.ActorId,
                ActorName = n.Actor != null ? n.Actor.DisplayName : null,
                ActorAvatar = n.Actor != null ? n.Actor.AvatarUrl : null,
                n.RefType,
                n.RefId,
                n.ReadAt,
                n.CreatedAt,
            })
            .ToListAsync(ct);

        var dtos = new List<NotificationDto>(rows.Count);
        foreach (var r in rows)
        {
            dtos.Add(await svc.ToDtoAsync(r.Id, r.Kind, r.ActorId, r.ActorName, r.ActorAvatar,
                r.RefType, r.RefId, r.ReadAt, r.CreatedAt, ct));
        }
        return Results.Ok(dtos);
    }

    private static async Task<IResult> UnreadCountAsync(
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var count = await db.Notifications.CountAsync(
            n => n.UserId == userId && n.ReadAt == null, ct);
        return Results.Ok(new { count });
    }

    private static async Task<IResult> MarkAllSeenAsync(
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        var now = DateTime.UtcNow;
        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearAllAsync(
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        await db.Notifications
            .Where(n => n.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DismissAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        await db.Notifications
            .Where(n => n.Id == id && n.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task StreamAsync(
        HttpContext ctx, NotificationStream stream, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        // 초기 ping (헤더 flush 트리거)
        await ctx.Response.WriteAsync(": ok\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(20));
        var hbTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeat.WaitForNextTickAsync(ct))
                {
                    await ctx.Response.WriteAsync(": hb\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch { }
        }, ct);

        try
        {
            await foreach (var dto in stream.SubscribeAsync(userId, ct))
            {
                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
                await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }
}
