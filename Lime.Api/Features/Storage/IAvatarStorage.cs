namespace Lime.Api.Features.Storage;

public interface IAvatarStorage
{
    /// <summary>
    /// 업로드 후 공개 접근용 URL을 반환한다.
    /// </summary>
    Task<string> SaveAsync(Guid userId, Stream content, string contentType, string ext, CancellationToken ct);

    /// <summary>
    /// 이전 아바타 URL을 받아 가능하면 삭제한다. 외부 URL(우리 스토리지 아님)은 무시.
    /// </summary>
    Task TryDeleteAsync(string? previousUrl, CancellationToken ct);
}
