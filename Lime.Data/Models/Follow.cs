namespace Lime.Data.Models;

public class Follow
{
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? Follower { get; set; }
    public User? Followee { get; set; }
}
