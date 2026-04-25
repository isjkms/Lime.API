namespace Lime.Api.Models;

public class Review
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TrackId { get; set; }
    public Guid? AlbumId { get; set; }
    public decimal Rating { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }
    public Track? Track { get; set; }
    public Album? Album { get; set; }
    public List<ReviewReaction> Reactions { get; set; } = new();
}
