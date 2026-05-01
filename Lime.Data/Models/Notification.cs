namespace Lime.Data.Models;

public enum NotificationKind : short
{
    NewFollower = 1,
    ReviewLiked = 2,
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid? ActorId { get; set; }
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public User? Actor { get; set; }
}
