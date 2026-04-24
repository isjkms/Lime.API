using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;

namespace Lime.Api.Features.Spotify;

public static class SpotifyEndpoints
{
    private static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AlbumTtl = TimeSpan.FromHours(1);

    public static IEndpointRouteBuilder MapSpotifyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/spotify");
        g.MapGet("/search", SearchAsync);
        g.MapGet("/albums/{id}", GetAlbumAsync);
        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? q,
        string? market,
        SpotifyClient client,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var query = (q ?? "").Trim();
        if (string.IsNullOrEmpty(query))
            return Results.Ok(new JsonObject { ["tracks"] = new JsonArray(), ["albums"] = new JsonArray() });

        var mk = NormalizeMarket(market);
        var key = $"spotify:search:{mk}:{query.ToLowerInvariant()}";

        try
        {
            var result = await cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = SearchTtl;
                var url = $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track,album&market={mk}";
                var node = await client.GetAsync(url, ct);
                return new JsonObject
                {
                    ["tracks"] = node["tracks"]?["items"]?.DeepClone() ?? new JsonArray(),
                    ["albums"] = node["albums"]?["items"]?.DeepClone() ?? new JsonArray(),
                    ["market"] = mk,
                };
            });
            return Results.Json(result);
        }
        catch (SpotifyRateLimitedException ex)
        {
            return RateLimited(ex);
        }
    }

    private static async Task<IResult> GetAlbumAsync(
        string id,
        string? market,
        SpotifyClient client,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var mk = NormalizeMarket(market);
        var key = $"spotify:album:{mk}:{id}";

        try
        {
            var result = await cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = AlbumTtl;
                var node = await client.GetAsync(
                    $"https://api.spotify.com/v1/albums/{Uri.EscapeDataString(id)}?market={mk}", ct);

                var tracks = new JsonArray();
                if (node["tracks"]?["items"] is JsonArray items)
                {
                    foreach (var t in items)
                    {
                        if (t is null) continue;
                        tracks.Add(new JsonObject
                        {
                            ["id"] = t["id"]?.DeepClone(),
                            ["name"] = t["name"]?.DeepClone(),
                            ["artists"] = t["artists"]?.DeepClone(),
                            ["duration_ms"] = t["duration_ms"]?.DeepClone(),
                            ["preview_url"] = t["preview_url"]?.DeepClone(),
                            ["track_number"] = t["track_number"]?.DeepClone(),
                        });
                    }
                }

                return new JsonObject
                {
                    ["id"] = node["id"]?.DeepClone(),
                    ["name"] = node["name"]?.DeepClone(),
                    ["cover"] = (node["images"] as JsonArray)?.FirstOrDefault()?["url"]?.DeepClone(),
                    ["tracks"] = tracks,
                };
            });
            return Results.Json(result);
        }
        catch (SpotifyRateLimitedException ex)
        {
            return RateLimited(ex);
        }
        catch (HttpRequestException)
        {
            return Results.NotFound(new { error = "not_found" });
        }
    }

    private static IResult RateLimited(SpotifyRateLimitedException ex) =>
        Results.Json(new { error = "rate_limited", retryAfter = ex.RetryAfterSeconds }, statusCode: 429);

    private static string NormalizeMarket(string? market)
    {
        if (!string.IsNullOrWhiteSpace(market) && market.Length == 2)
            return market.ToUpperInvariant();
        return "KR";
    }
}
