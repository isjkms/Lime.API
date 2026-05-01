namespace Lime.Data.Models;

public class UserOAuthAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }

    public User? User { get; set; }
}
