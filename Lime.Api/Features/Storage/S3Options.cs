namespace Lime.Api.Features.Storage;

public class S3Options
{
    public const string SectionName = "S3";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>S3 API 엔드포인트. R2: https://{account}.r2.cloudflarestorage.com</summary>
    public string EndPointUrl { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    public string ForcePathStyle { get; set; } = "true";
    /// <summary>오브젝트 공개 URL prefix. R2 공개 도메인 또는 커스텀 도메인. 예: https://pub-xxx.r2.dev</summary>
    public string PublicUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(EndPointUrl)
        && !string.IsNullOrWhiteSpace(BucketName);

    public bool UseForcePathStyle =>
        bool.TryParse(ForcePathStyle, out var b) && b;
}
