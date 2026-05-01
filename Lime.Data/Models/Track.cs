namespace Lime.Data.Models;

public class Track
{
    public Guid Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public Guid? AlbumId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public int? TrackNumber { get; set; }
    public string? PreviewUrl { get; set; }
    public List<ArtistRef> Artists { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Album? Album { get; set; }
}
