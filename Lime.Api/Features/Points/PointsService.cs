using Lime.Api.Data;
using Lime.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Points;

public static class PointsConfig
{
    public const int WelcomeBonus = 10;
    public const int ReviewCreatedReward = 5;
    public const int LikeReceivedReward = 1;

    /// <summary>작성 후 이 시간 이내 수정·삭제는 무료, "(수정됨)" 라벨도 안 붙음.</summary>
    public static readonly TimeSpan EditDeleteGrace = TimeSpan.FromMinutes(5);

    public const int ReviewEditCost = 30;
    public const int ReviewDeleteCost = 50;

    /// <summary>닉네임 첫 변경은 무료, 이후 부과.</summary>
    public const int NicknameChangeCost = 300;
}

public interface IPointsService
{
    Task<bool> TryAdjustAsync(Guid userId, int delta, PointReason reason,
        string? refType, Guid? refId, CancellationToken ct);
}

/// <summary>
/// 포인트 적립·소비를 단일 진입점으로 다룬다. 음수 잔액은 차단.
/// 호출 시점에 즉시 SaveChanges (호출자는 별도 SaveChanges 불필요).
/// </summary>
public class PointsService(AppDbContext db) : IPointsService
{
    public async Task<bool> TryAdjustAsync(
        Guid userId, int delta, PointReason reason,
        string? refType, Guid? refId, CancellationToken ct)
    {
        if (delta == 0) return true;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return false;

        if (delta < 0 && user.Points + delta < 0) return false;

        user.Points += delta;
        user.UpdatedAt = DateTime.UtcNow;

        db.PointTransactions.Add(new PointTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Delta = delta,
            Reason = reason,
            RefType = refType,
            RefId = refId,
        });

        await db.SaveChangesAsync(ct);
        return true;
    }
}
