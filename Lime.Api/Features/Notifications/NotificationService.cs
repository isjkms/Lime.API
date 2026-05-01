using Lime.Data;
using Lime.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Notifications;

public interface INotificationService
{
    Task NotifyNewFollowerAsync(Guid followeeId, Guid followerId, CancellationToken ct);
    Task RemoveNewFollowerAsync(Guid followeeId, Guid followerId, CancellationToken ct);
    Task NotifyReviewLikedAsync(Guid reviewOwnerId, Guid actorId, Guid reviewId, CancellationToken ct);
    Task RemoveReviewLikedAsync(Guid reviewOwnerId, Guid actorId, Guid reviewId, CancellationToken ct);
}

public class NotificationService(AppDbContext db, NotificationStream stream) : INotificationService
{
    public async Task NotifyNewFollowerAsync(Guid followeeId, Guid followerId, CancellationToken ct)
    {
        if (followeeId == followerId) return;

        // 같은 (followee, follower) 알림 중복 차단
        var exists = await db.Notifications.AnyAsync(n =>
            n.UserId == followeeId
            && n.Kind == NotificationKind.NewFollower
            && n.ActorId == followerId, ct);
        if (exists) return;

        var note = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = followeeId,
            Kind = NotificationKind.NewFollower,
            ActorId = followerId,
            RefType = "user",
            RefId = followerId,
        };
        db.Notifications.Add(note);
        await db.SaveChangesAsync(ct);

        await PushAsync(note, ct);
    }

    public async Task RemoveNewFollowerAsync(Guid followeeId, Guid followerId, CancellationToken ct)
    {
        await db.Notifications
            .Where(n => n.UserId == followeeId
                && n.Kind == NotificationKind.NewFollower
                && n.ActorId == followerId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task NotifyReviewLikedAsync(Guid reviewOwnerId, Guid actorId, Guid reviewId, CancellationToken ct)
    {
        if (reviewOwnerId == actorId) return;

        var exists = await db.Notifications.AnyAsync(n =>
            n.UserId == reviewOwnerId
            && n.Kind == NotificationKind.ReviewLiked
            && n.ActorId == actorId
            && n.RefId == reviewId, ct);
        if (exists) return;

        var note = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = reviewOwnerId,
            Kind = NotificationKind.ReviewLiked,
            ActorId = actorId,
            RefType = "review",
            RefId = reviewId,
        };
        db.Notifications.Add(note);
        await db.SaveChangesAsync(ct);

        await PushAsync(note, ct);
    }

    public async Task RemoveReviewLikedAsync(Guid reviewOwnerId, Guid actorId, Guid reviewId, CancellationToken ct)
    {
        await db.Notifications
            .Where(n => n.UserId == reviewOwnerId
                && n.Kind == NotificationKind.ReviewLiked
                && n.ActorId == actorId
                && n.RefId == reviewId)
            .ExecuteDeleteAsync(ct);
    }

    private async Task PushAsync(Notification note, CancellationToken ct)
    {
        var dto = await BuildDtoAsync(note.Id, ct);
        if (dto is not null) stream.Publish(note.UserId, dto);
    }

    private async Task<NotificationDto?> BuildDtoAsync(Guid notificationId, CancellationToken ct)
    {
        var row = await db.Notifications.AsNoTracking()
            .Where(n => n.Id == notificationId)
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
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        return await ToDtoAsync(row.Id, row.Kind, row.ActorId, row.ActorName, row.ActorAvatar,
            row.RefType, row.RefId, row.ReadAt, row.CreatedAt, ct);
    }

    /// <summary>알림 → 표시용 DTO (메시지·링크 계산 포함).</summary>
    public async Task<NotificationDto> ToDtoAsync(
        Guid id, NotificationKind kind, Guid? actorId, string? actorName, string? actorAvatar,
        string? refType, Guid? refId, DateTime? readAt, DateTime createdAt, CancellationToken ct)
    {
        Actor? actor = actorId is Guid aid && actorName is not null
            ? new Actor(aid, actorName, actorAvatar)
            : null;

        string message;
        string? link = null;

        switch (kind)
        {
            case NotificationKind.NewFollower:
                message = $"{actorName ?? "누군가"}님이 팔로우했어요";
                if (actorId is Guid fid) link = $"/u/{fid}";
                break;

            case NotificationKind.ReviewLiked:
                message = $"{actorName ?? "누군가"}님이 내 후기를 좋아합니다";
                if (refId is Guid rid)
                {
                    var reviewLink = await db.Reviews.AsNoTracking()
                        .Where(r => r.Id == rid && r.DeletedAt == null)
                        .Select(r => new
                        {
                            TrackId = r.TrackId,
                            AlbumId = r.AlbumId,
                        })
                        .FirstOrDefaultAsync(ct);
                    if (reviewLink is not null)
                    {
                        if (reviewLink.TrackId is Guid tid)
                            link = $"/tracks/{tid}#review-{rid}";
                        else if (reviewLink.AlbumId is Guid alid)
                            link = $"/albums/{alid}#review-{rid}";
                    }
                }
                break;

            default:
                message = "새 알림";
                break;
        }

        return new NotificationDto(
            id,
            kind.ToString(),
            actor,
            link,
            message,
            readAt is not null,
            createdAt);
    }
}
