using Lime.Api.Models;

namespace Lime.Api.Features.Legal;

/// <summary>
/// 약관·정책 현재 활성 버전 관리. 본문이 바뀌면 버전 ↑ → 기존 동의 무효 → 재동의 요구.
/// 본문 자체는 Web에서 표시 (lib/legal). 여기는 식별자만.
/// </summary>
public static class LegalDocuments
{
    public const string TermsCurrentVersion = "2026-04-27";
    public const string PrivacyCollectionCurrentVersion = "2026-04-27";

    public static readonly IReadOnlyDictionary<ConsentDoc, string> CurrentVersions =
        new Dictionary<ConsentDoc, string>
        {
            [ConsentDoc.Terms] = TermsCurrentVersion,
            [ConsentDoc.PrivacyCollection] = PrivacyCollectionCurrentVersion,
        };

    /// <summary>가입 시 필수 동의 항목.</summary>
    public static readonly ConsentDoc[] Required =
    {
        ConsentDoc.Terms,
        ConsentDoc.PrivacyCollection,
    };
}
