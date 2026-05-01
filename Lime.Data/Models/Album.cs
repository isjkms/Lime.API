namespace Lime.Data.Models;

public class Album
{
    public Guid Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string? ReleaseDate { get; set; }
    public List<ArtistRef> Artists { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Track> Tracks { get; set; } = new();
}
