namespace Lime.Admin.Models;

public class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AdminUserRole Role { get; set; } = AdminUserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AdminSession> Sessions { get; set; } = new List<AdminSession>();
    public ICollection<AdminAuditLog> AuditLogs { get; set; } = new List<AdminAuditLog>();
}

public enum AdminUserRole
{
    SuperAdmin,
    Admin,
    Viewer,
}
