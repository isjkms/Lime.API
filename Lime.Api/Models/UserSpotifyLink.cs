namespace Lime.Api.Models;

public class UserSpotifyLink
{
    public Guid UserId { get; set; }
    public string? SpotifyUserId { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public DateTime? AccessExpiresAt { get; set; }
    public string? Scope { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
