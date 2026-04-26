using Lime.Api.Data;
using Lime.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/catalog");
        g.MapPost("/tracks/ensure", EnsureTrackAsync);
        g.MapPost("/albums/ensure", EnsureAlbumAsync);
        g.MapGet("/tracks/{id:guid}", GetTrackAsync);
        g.MapGet("/albums/{id:guid}", GetAlbumAsync);
        g.MapGet("/tracks", ListTracksAsync);
        g.MapGet("/albums", ListAlbumsAsync);
        g.MapGet("/recent-reviewed", RecentReviewedAsync);
        g.MapGet("/top-rated", TopRatedAsync);
        return app;
    }

    public record EnsureRequest(string SpotifyId);

    private static async Task<IResult> EnsureTrackAsync(
        EnsureRequest req, ICatalogService catalog, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SpotifyId)) return Results.BadRequest(new { error = "missing_spotify_id" });
        var track = await catalog.EnsureTrackAsync(req.SpotifyId, ct);
        return Results.Ok(await LoadTrackDtoAsync(db, track.Id, ct));
    }

    private static async Task<IResult> EnsureAlbumAsync(
        EnsureRequest req, ICatalogService catalog, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SpotifyId)) return Results.BadRequest(new { error = "missing_spotify_id" });
        var album = await catalog.EnsureAlbumAsync(req.SpotifyId, ct);
        return Results.Ok(await LoadAlbumDtoAsync(db, album.Id, ct));
    }

    private static async Task<IResult> GetTrackAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var dto = await LoadTrackDtoAsync(db, id, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> GetAlbumAsync(
        Guid id, AppDbContext db, ICatalogService catalog, CancellationToken ct)
    {
        var album = await db.Albums.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.SpotifyId, TrackCount = a.Tracks.Count() })
            .FirstOrDefaultAsync(ct);
        if (album is null) return Results.NotFound();

        if (album.TrackCount <= 1)
        {
            try { await catalog.EnsureAlbumAsync(album.SpotifyId, ct); } catch { }
        }

        var dto = await LoadAlbumDtoAsync(db, id, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> ListTracksAsync(
        string? q, int? limit, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 60, 1, 100);
        var query = db.Tracks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern));
        }

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .Select(t => new
            {
                id = t.Id,
                spotifyId = t.SpotifyId,
                name = t.Name,
                previewUrl = t.PreviewUrl,
                albumId = t.AlbumId,
                coverUrl = t.Album != null ? t.Album.CoverUrl : null,
                artists = t.Artists,
                stats = db.Reviews
                    .Where(r => r.TrackId == t.Id && r.DeletedAt == null)
                    .GroupBy(_ => 1)
                    .Select(g => new { avg = (double?)g.Average(x => (double)x.Rating), count = g.Count() })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Results.Ok(rows.Select(r => new
        {
            r.id, r.spotifyId, r.name, r.previewUrl, r.albumId, r.coverUrl, r.artists,
            avg = r.stats?.avg ?? 0,
            n = r.stats?.count ?? 0,
        }));
    }

    private static async Task<IResult> ListAlbumsAsync(
        string? q, int? limit, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 60, 1, 100);
        var query = db.Albums.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Name, pattern));
        }

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new
            {
                id = a.Id,
                spotifyId = a.SpotifyId,
                name = a.Name,
                coverUrl = a.CoverUrl,
                releaseDate = a.ReleaseDate,
                artists = a.Artists,
                stats = db.Reviews
                    .Where(r => r.AlbumId == a.Id && r.DeletedAt == null)
                    .GroupBy(_ => 1)
                    .Select(g => new { avg = (double?)g.Average(x => (double)x.Rating), count = g.Count() })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Results.Ok(rows.Select(r => new
        {
            r.id, r.spotifyId, r.name, r.coverUrl, r.releaseDate, r.artists,
            avg = r.stats?.avg ?? 0,
            n = r.stats?.count ?? 0,
        }));
    }

    private static async Task<IResult> RecentReviewedAsync(
        string? target, int? limit, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 8, 1, 50);
        if (target == "track")
        {
            var rows = await db.Reviews.AsNoTracking()
                .Where(r => r.DeletedAt == null && r.TrackId != null)
                .GroupBy(r => r.TrackId!.Value)
                .Select(g => new { trackId = g.Key, lastAt = g.Max(x => x.CreatedAt) })
                .OrderByDescending(g => g.lastAt)
                .Take(take)
                .Join(db.Tracks, g => g.trackId, t => t.Id, (g, t) => new
                {
                    id = t.Id,
                    spotifyId = t.SpotifyId,
                    title = t.Name,
                    artists = t.Artists,
                    coverUrl = t.Album != null ? t.Album.CoverUrl : null,
                    previewUrl = t.PreviewUrl,
                    albumId = t.AlbumId,
                    lastReviewAt = g.lastAt,
                    stats = db.Reviews
                        .Where(r => r.TrackId == t.Id && r.DeletedAt == null)
                        .GroupBy(_ => 1)
                        .Select(s => new { avg = (double?)s.Average(x => (double)x.Rating), count = s.Count() })
                        .FirstOrDefault(),
                })
                .ToListAsync(ct);
            return Results.Ok(rows.Select(r => new
            {
                r.id, r.spotifyId, r.title, r.artists, r.coverUrl, r.previewUrl, r.albumId,
                lastReviewAt = r.lastReviewAt,
                avg = r.stats?.avg ?? 0, n = r.stats?.count ?? 0,
            }));
        }
        if (target == "album")
        {
            var rows = await db.Reviews.AsNoTracking()
                .Where(r => r.DeletedAt == null && r.AlbumId != null)
                .GroupBy(r => r.AlbumId!.Value)
                .Select(g => new { albumId = g.Key, lastAt = g.Max(x => x.CreatedAt) })
                .OrderByDescending(g => g.lastAt)
                .Take(take)
                .Join(db.Albums, g => g.albumId, a => a.Id, (g, a) => new
                {
                    id = a.Id,
                    spotifyId = a.SpotifyId,
                    title = a.Name,
                    artists = a.Artists,
                    coverUrl = a.CoverUrl,
                    lastReviewAt = g.lastAt,
                    stats = db.Reviews
                        .Where(r => r.AlbumId == a.Id && r.DeletedAt == null)
                        .GroupBy(_ => 1)
                        .Select(s => new { avg = (double?)s.Average(x => (double)x.Rating), count = s.Count() })
                        .FirstOrDefault(),
                })
                .ToListAsync(ct);
            return Results.Ok(rows.Select(r => new
            {
                r.id, r.spotifyId, r.title, r.artists, r.coverUrl,
                lastReviewAt = r.lastReviewAt,
                avg = r.stats?.avg ?? 0, n = r.stats?.count ?? 0,
            }));
        }
        return Results.BadRequest(new { error = "invalid_target" });
    }

    private static async Task<IResult> TopRatedAsync(
        string? target, string? period, int? limit, AppDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 5, 1, 50);
        var since = period switch
        {
            "day" => DateTime.UtcNow.AddDays(-1),
            "month" => DateTime.UtcNow.AddMonths(-1),
            "year" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.UtcNow.AddYears(-1),
        };

        if (target == "track")
        {
            var rows = await db.Reviews.AsNoTracking()
                .Where(r => r.DeletedAt == null && r.TrackId != null && r.CreatedAt >= since)
                .GroupBy(r => r.TrackId!.Value)
                .Select(g => new { trackId = g.Key, avg = g.Average(x => (double)x.Rating), n = g.Count() })
                .OrderByDescending(g => g.avg).ThenByDescending(g => g.n)
                .Take(take)
                .Join(db.Tracks, g => g.trackId, t => t.Id, (g, t) => new
                {
                    id = t.Id,
                    spotifyId = t.SpotifyId,
                    title = t.Name,
                    artists = t.Artists,
                    coverUrl = t.Album != null ? t.Album.CoverUrl : null,
                    previewUrl = t.PreviewUrl,
                    albumId = t.AlbumId,
                    avg = g.avg,
                    n = g.n,
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        }
        if (target == "album")
        {
            var rows = await db.Reviews.AsNoTracking()
                .Where(r => r.DeletedAt == null && r.AlbumId != null && r.CreatedAt >= since)
                .GroupBy(r => r.AlbumId!.Value)
                .Select(g => new { albumId = g.Key, avg = g.Average(x => (double)x.Rating), n = g.Count() })
                .OrderByDescending(g => g.avg).ThenByDescending(g => g.n)
                .Take(take)
                .Join(db.Albums, g => g.albumId, a => a.Id, (g, a) => new
                {
                    id = a.Id,
                    spotifyId = a.SpotifyId,
                    title = a.Name,
                    artists = a.Artists,
                    coverUrl = a.CoverUrl,
                    avg = g.avg,
                    n = g.n,
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        }
        return Results.BadRequest(new { error = "invalid_target" });
    }

    private static async Task<object?> LoadTrackDtoAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var row = await db.Tracks.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                id = t.Id,
                spotifyId = t.SpotifyId,
                name = t.Name,
                durationMs = t.DurationMs,
                trackNumber = t.TrackNumber,
                previewUrl = t.PreviewUrl,
                artists = t.Artists,
                albumId = t.AlbumId,
                albumName = t.Album != null ? t.Album.Name : null,
                coverUrl = t.Album != null ? t.Album.CoverUrl : null,
                releaseDate = t.Album != null ? t.Album.ReleaseDate : null,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var stats = await db.Reviews
            .Where(r => r.TrackId == id && r.DeletedAt == null)
            .GroupBy(_ => 1)
            .Select(g => new { avg = (double?)g.Average(x => (double)x.Rating), count = g.Count() })
            .FirstOrDefaultAsync(ct);

        return new
        {
            row.id, row.spotifyId, row.name, row.durationMs, row.trackNumber, row.previewUrl,
            row.artists, row.albumId, row.albumName, row.coverUrl, row.releaseDate,
            stats = new { avgRating = stats?.avg ?? 0, reviewCount = stats?.count ?? 0 },
        };
    }

    private static async Task<object?> LoadAlbumDtoAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var row = await db.Albums.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                id = a.Id,
                spotifyId = a.SpotifyId,
                name = a.Name,
                coverUrl = a.CoverUrl,
                releaseDate = a.ReleaseDate,
                artists = a.Artists,
                tracks = a.Tracks.OrderBy(t => t.TrackNumber).Select(t => new
                {
                    id = t.Id,
                    spotifyId = t.SpotifyId,
                    name = t.Name,
                    durationMs = t.DurationMs,
                    trackNumber = t.TrackNumber,
                    previewUrl = t.PreviewUrl,
                    artists = t.Artists,
                }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var stats = await db.Reviews
            .Where(r => r.AlbumId == id && r.DeletedAt == null)
            .GroupBy(_ => 1)
            .Select(g => new { avg = (double?)g.Average(x => (double)x.Rating), count = g.Count() })
            .FirstOrDefaultAsync(ct);

        return new
        {
            row.id, row.spotifyId, row.name, row.coverUrl, row.releaseDate, row.artists, row.tracks,
            stats = new { avgRating = stats?.avg ?? 0, reviewCount = stats?.count ?? 0 },
        };
    }
}
