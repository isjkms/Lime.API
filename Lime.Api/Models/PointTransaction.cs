namespace Lime.Api.Models;

public enum PointReason : short
{
    WelcomeBonus = 1,
    ReviewCreated = 2,
    ReviewEdited = 3,
    ReviewDeleted = 4,
    LikeReceived = 5,
    LikeRevoked = 6,
    NicknameChange = 7,
}

public class PointTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Delta { get; set; }
    public PointReason Reason { get; set; }
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
