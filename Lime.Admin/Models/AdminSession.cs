namespace Lime.Admin.Models;

public class AdminSession
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public AdminUser? AdminUser { get; set; }
}
