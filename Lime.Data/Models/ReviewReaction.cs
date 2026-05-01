namespace Lime.Data.Models;

public enum ReactionKind : short
{
    Like = 1,
    Dislike = -1,
}

public class ReviewReaction
{
    public Guid ReviewId { get; set; }
    public Guid UserId { get; set; }
    public ReactionKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }

    public Review? Review { get; set; }
    public User? User { get; set; }
}
