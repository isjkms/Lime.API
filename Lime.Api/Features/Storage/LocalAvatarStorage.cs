namespace Lime.Api.Features.Storage;

/// <summary>
/// 로컬 디스크 (wwwroot/uploads/avatars). 단일 인스턴스 dev용.
/// 운영은 S3AvatarStorage 사용.
/// </summary>
public class LocalAvatarStorage(IWebHostEnvironment env, IHttpContextAccessor http) : IAvatarStorage
{
    public async Task<string> SaveAsync(
        Guid userId, Stream content, string contentType, string ext, CancellationToken ct)
    {
        var rootPath = string.IsNullOrEmpty(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var dir = Path.Combine(rootPath, "uploads", "avatars");
        Directory.CreateDirectory(dir);

        var fileName = $"{userId:N}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await content.CopyToAsync(stream, ct);
        }

        var req = http.HttpContext?.Request;
        var origin = req is null ? "" : $"{req.Scheme}://{req.Host}";
        return $"{origin}/uploads/avatars/{fileName}";
    }

    public Task TryDeleteAsync(string? previousUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(previousUrl)) return Task.CompletedTask;
        try
        {
            var marker = "/uploads/avatars/";
            var idx = previousUrl.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return Task.CompletedTask;
            var fileName = previousUrl[(idx + marker.Length)..];
            if (string.IsNullOrEmpty(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
                return Task.CompletedTask;

            var rootPath = string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;
            var fullPath = Path.Combine(rootPath, "uploads", "avatars", fileName);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch { /* best-effort */ }
        return Task.CompletedTask;
    }
}
