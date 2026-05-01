namespace Lime.Admin.Models;

public class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string Action { get; set; } = string.Empty; // 수행한 작업 이름
    public string? TargetType { get; set; } // 작업 대상의 종류
    public string? TargetId { get; set; } // 작업 대상의 식별자
    public string? Metadata { get; set; } // 작업의 상세 정보
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    public AdminUser? AdminUser { get; set; }
}
