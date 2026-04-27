using Amazon.S3;
using Amazon.S3.Model;

namespace Lime.Api.Features.Storage;

/// <summary>
/// S3 호환 (Cloudflare R2, AWS S3 등) 오브젝트 스토리지.
/// 키 패턴: avatars/{userId:N}-{timestamp}{ext}
/// 공개 접근은 S3:PublicUrl 도메인을 통해 노출 (버킷이 public이거나 커스텀 도메인 매핑 가정).
/// </summary>
public class S3AvatarStorage(IAmazonS3 s3, S3Options options) : IAvatarStorage
{
    private const string KeyPrefix = "avatars/";

    public async Task<string> SaveAsync(
        Guid userId, Stream content, string contentType, string ext, CancellationToken ct)
    {
        var key = $"{KeyPrefix}{userId:N}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";

        // S3 PutObject는 stream을 처음부터 읽으므로, 이미 일부 읽힌 스트림은 메모리로 복사.
        Stream uploadStream = content;
        if (!content.CanSeek)
        {
            var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            ms.Position = 0;
            uploadStream = ms;
        }
        else
        {
            uploadStream.Position = 0;
        }

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = key,
            InputStream = uploadStream,
            ContentType = contentType,
            DisablePayloadSigning = true, // R2 호환: 서명된 페이로드 비활성
        }, ct);

        var publicBase = (string.IsNullOrWhiteSpace(options.PublicUrl)
            ? options.EndPointUrl
            : options.PublicUrl).TrimEnd('/');
        return $"{publicBase}/{key}";
    }

    public async Task TryDeleteAsync(string? previousUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(previousUrl)) return;
        try
        {
            var key = ExtractKey(previousUrl);
            if (key is null) return;

            await s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = options.BucketName,
                Key = key,
            }, ct);
        }
        catch { /* best-effort */ }
    }

    private string? ExtractKey(string url)
    {
        var marker = "/" + KeyPrefix;
        var idx = url.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var key = url[(idx + 1)..]; // skip leading slash
        return key.Contains("..") ? null : key;
    }
}
