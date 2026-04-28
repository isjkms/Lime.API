using System.Security.Claims;
using Lime.Api.Data;
using Lime.Api.Features.Auth;
using Lime.Api.Features.Auth.Models;
using Lime.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lime.Api.Features.Legal;

public static class LegalEndpoints
{
    public static IEndpointRouteBuilder MapLegalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/legal/current-versions", () => Results.Ok(new
        {
            terms = LegalDocuments.TermsCurrentVersion,
            privacyCollection = LegalDocuments.PrivacyCollectionCurrentVersion,
        }));

        var g = app.MapGroup("/users/me/consents").RequireAuthorization();
        g.MapGet("/", ListMyConsentsAsync);
        g.MapPost("/", RecordConsentsAsync);
        return app;
    }

    public record ConsentItem(string DocKind, string DocVersion);
    public record RecordConsentsRequest(List<ConsentItem> Items);

    private static async Task<IResult> ListMyConsentsAsync(
        HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();

        var rows = await db.UserConsents.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AgreedAt)
            .Select(c => new
            {
                docKind = c.DocKind.ToString(),
                docVersion = c.DocVersion,
                agreedAt = c.AgreedAt,
            })
            .ToListAsync(ct);

        var requiredOk = await new ConsentService(db).HasAllRequiredAsync(userId, ct);
        return Results.Ok(new
        {
            requiredOk,
            currentVersions = new
            {
                terms = LegalDocuments.TermsCurrentVersion,
                privacyCollection = LegalDocuments.PrivacyCollectionCurrentVersion,
            },
            history = rows,
        });
    }

    private static async Task<IResult> RecordConsentsAsync(
        RecordConsentsRequest req, HttpContext ctx,
        IConsentService consents, IOptions<AuthOptions> opt,
        CancellationToken ct)
    {
        if (!TryGetUserId(ctx, out var userId)) return Results.Unauthorized();
        if (req.Items is null || req.Items.Count == 0)
            return Results.BadRequest(new { error = "missing_items" });

        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var ua = ctx.Request.Headers.UserAgent.ToString();

        foreach (var it in req.Items)
        {
            if (!Enum.TryParse<ConsentDoc>(it.DocKind, ignoreCase: true, out var doc))
                return Results.BadRequest(new { error = "invalid_doc_kind", value = it.DocKind });

            if (!LegalDocuments.CurrentVersions.TryGetValue(doc, out var current) || current != it.DocVersion)
                return Results.BadRequest(new { error = "version_mismatch", expected = current, got = it.DocVersion });

            await consents.RecordAsync(userId, doc, it.DocVersion, ip, ua, ct);
        }

        // 모든 필수 동의가 끝났으면 신호 cookie 발행 → Web 미들웨어가 통과시킴.
        if (await consents.HasAllRequiredAsync(userId, ct))
            AuthEndpoints.WriteConsentOkCookie(ctx, opt.Value.Cookie);

        return Results.NoContent();
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId);
    }
}
