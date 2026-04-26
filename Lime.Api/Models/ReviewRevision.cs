namespace Lime.Api.Models;

/// <summary>
/// 5분 grace 이후 수정될 때마다 이전 본문/별점을 보관.
/// UI에는 "(수정됨)" 라벨만 노출, 실제 본문 변경 이력은 운영자/신고 도구에서만 접근.
/// </summary>
public class ReviewRevision
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public decimal Rating { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Review? Review { get; set; }
}
