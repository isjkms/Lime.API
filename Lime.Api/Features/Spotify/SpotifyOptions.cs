namespace Lime.Api.Features.Spotify;

public class SpotifyOptions
{
    public const string SectionName = "Spotify";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string Scopes { get; set; } =
        "streaming user-read-email user-read-private user-modify-playback-state user-read-playback-state";
}
