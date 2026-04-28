namespace Lime.Api.Models;

public enum ConsentDoc : short
{
    Terms = 1,             // 이용약관
    PrivacyCollection = 2, // 개인정보 수집·이용 동의
}

public class UserConsent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ConsentDoc DocKind { get; set; }
    /// <summary>해당 문서의 시행일/버전 식별자. 예: "2026-04-27".</summary>
    public string DocVersion { get; set; } = string.Empty;
    public DateTime AgreedAt { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }

    public User? User { get; set; }
}
