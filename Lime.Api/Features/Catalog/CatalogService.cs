using System.Text.Json.Nodes;
using Lime.Data;
using Lime.Data.Models;
using Lime.Api.Features.Spotify;

using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Catalog;

public interface ICatalogService
{
    Task<Album> EnsureAlbumAsync(string spotifyId, CancellationToken ct);
    Task<Track> EnsureTrackAsync(string spotifyId, CancellationToken ct);
}

public class CatalogService(AppDbContext db, SpotifyClient spotify) : ICatalogService
{
    public async Task<Album> EnsureAlbumAsync(string spotifyId, CancellationToken ct)
    {
        var album = await db.Albums.FirstOrDefaultAsync(a => a.SpotifyId == spotifyId, ct);
        var node = await spotify.GetAsync($"https://api.spotify.com/v1/albums/{Uri.EscapeDataString(spotifyId)}?market=KR", ct);

        if (album is null)
        {
            album = new Album
            {
                Id = Guid.NewGuid(),
                SpotifyId = spotifyId,
                Name = node["name"]?.GetValue<string>() ?? "",
                CoverUrl = (node["images"] as JsonArray)?.FirstOrDefault()?["url"]?.GetValue<string>(),
                ReleaseDate = node["release_date"]?.GetValue<string>(),
                Artists = ParseArtists(node["artists"]),
            };
            db.Albums.Add(album);
        }

        if (node["tracks"]?["items"] is JsonArray items)
        {
            var spotifyIds = items.OfType<JsonNode>()
                .Select(t => t["id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var existingTracks = await db.Tracks
                .Where(t => spotifyIds.Contains(t.SpotifyId))
                .ToListAsync(ct);
            var existingBySpotify = existingTracks.ToDictionary(t => t.SpotifyId);

            foreach (var t in items)
            {
                if (t is null) continue;
                var tSpotifyId = t["id"]?.GetValue<string>();
                if (string.IsNullOrEmpty(tSpotifyId)) continue;

                var trackNumber = t["track_number"]?.GetValue<int?>();
                var name = t["name"]?.GetValue<string>() ?? "";
                var durationMs = t["duration_ms"]?.GetValue<int?>();
                var previewUrl = t["preview_url"]?.GetValue<string>();
                var artists = ParseArtists(t["artists"]);

                if (existingBySpotify.TryGetValue(tSpotifyId, out var et))
                {
                    et.AlbumId = album.Id;
                    et.Name = name;
                    et.DurationMs = durationMs;
                    et.TrackNumber = trackNumber;
                    et.PreviewUrl = previewUrl;
                    et.Artists = artists;
                    et.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.Tracks.Add(new Track
                    {
                        Id = Guid.NewGuid(),
                        SpotifyId = tSpotifyId,
                        AlbumId = album.Id,
                        Name = name,
                        DurationMs = durationMs,
                        TrackNumber = trackNumber,
                        PreviewUrl = previewUrl,
                        Artists = artists,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return album;
    }

    public async Task<Track> EnsureTrackAsync(string spotifyId, CancellationToken ct)
    {
        var existing = await db.Tracks.FirstOrDefaultAsync(t => t.SpotifyId == spotifyId, ct);
        if (existing is not null) return existing;

        var node = await spotify.GetAsync($"https://api.spotify.com/v1/tracks/{Uri.EscapeDataString(spotifyId)}?market=KR", ct);
        var albumNode = node["album"];
        var albumSpotifyId = albumNode?["id"]?.GetValue<string>();

        Guid? albumId = null;
        if (!string.IsNullOrEmpty(albumSpotifyId))
        {
            var album = await db.Albums.FirstOrDefaultAsync(a => a.SpotifyId == albumSpotifyId, ct);
            if (album is null)
            {
                album = new Album
                {
                    Id = Guid.NewGuid(),
                    SpotifyId = albumSpotifyId,
                    Name = albumNode?["name"]?.GetValue<string>() ?? "",
                    CoverUrl = (albumNode?["images"] as JsonArray)?.FirstOrDefault()?["url"]?.GetValue<string>(),
                    ReleaseDate = albumNode?["release_date"]?.GetValue<string>(),
                    Artists = ParseArtists(albumNode?["artists"]),
                };
                db.Albums.Add(album);
            }
            albumId = album.Id;
        }

        var track = new Track
        {
            Id = Guid.NewGuid(),
            SpotifyId = spotifyId,
            AlbumId = albumId,
            Name = node["name"]?.GetValue<string>() ?? "",
            DurationMs = node["duration_ms"]?.GetValue<int?>(),
            TrackNumber = node["track_number"]?.GetValue<int?>(),
            PreviewUrl = node["preview_url"]?.GetValue<string>(),
            Artists = ParseArtists(node["artists"]),
        };
        db.Tracks.Add(track);
        await db.SaveChangesAsync(ct);
        return track;
    }

    private static List<ArtistRef> ParseArtists(JsonNode? node)
    {
        var list = new List<ArtistRef>();
        if (node is not JsonArray arr) return list;
        foreach (var a in arr)
        {
            var id = a?["id"]?.GetValue<string>();
            var name = a?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                list.Add(new ArtistRef(id, name));
        }
        return list;
    }
}
