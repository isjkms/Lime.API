using System.Security.Cryptography;
using System.Text;
using Lime.Data;
using Lime.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Features.Legal;

public interface IConsentService
{
    Task<bool> HasAllRequiredAsync(Guid userId, CancellationToken ct);
    Task RecordAsync(Guid userId, ConsentDoc doc, string version,
        string? ip, string? userAgent, CancellationToken ct);
}

public class ConsentService(AppDbContext db) : IConsentService
{
    public async Task<bool> HasAllRequiredAsync(Guid userId, CancellationToken ct)
    {
        foreach (var doc in LegalDocuments.Required)
        {
            var version = LegalDocuments.CurrentVersions[doc];
            var ok = await db.UserConsents.AnyAsync(
                c => c.UserId == userId && c.DocKind == doc && c.DocVersion == version, ct);
            if (!ok) return false;
        }
        return true;
    }

    public async Task RecordAsync(
        Guid userId, ConsentDoc doc, string version,
        string? ip, string? userAgent, CancellationToken ct)
    {
        var exists = await db.UserConsents.AnyAsync(
            c => c.UserId == userId && c.DocKind == doc && c.DocVersion == version, ct);
        if (exists) return;

        db.UserConsents.Add(new UserConsent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocKind = doc,
            DocVersion = version,
            AgreedAt = DateTime.UtcNow,
            IpHash = ip is null ? null : HashIp(ip),
            UserAgent = userAgent is null ? null : Trunc(userAgent, 255),
        });
        await db.SaveChangesAsync(ct);
    }

    private static string HashIp(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
