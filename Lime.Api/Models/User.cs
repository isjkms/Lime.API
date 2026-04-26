namespace Lime.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int Points { get; set; }
    public int NicknameChanges { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<UserOAuthAccount> OAuthAccounts { get; set; } = new List<UserOAuthAccount>();
}
