using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Lime.Api.Features.Legal;

/// <summary>
/// 인증된 요청 중 필수 동의가 모두 끝난 사용자만 통과시키는 엔드포인트 필터.
/// Web의 쿠키 게이트는 UX 최적화일 뿐이고, 실제 권한 검증은 API에서 한다.
/// </summary>
public class RequireConsentFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        var sub = http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(sub, out var userId))
        {
            var consents = http.RequestServices.GetRequiredService<IConsentService>();
            if (!await consents.HasAllRequiredAsync(userId, http.RequestAborted))
            {
                return Results.Json(
                    new { error = "consent_required" },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }
        return await next(ctx);
    }
}

public static class RequireConsentExtensions
{
    public static TBuilder RequireConsent<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(b => b.Metadata.Add(new RequireConsentMarker()));
        // Filter는 IEndpointConventionBuilder.AddEndpointFilter로 추가
        if (builder is RouteHandlerBuilder rhb)
            rhb.AddEndpointFilter<RequireConsentFilter>();
        return builder;
    }

    private sealed class RequireConsentMarker { }
}
