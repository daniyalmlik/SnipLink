using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;

namespace SnipLink.Api.Services;

public sealed class AbuseDetectionService : IAbuseDetectionService
{
    private static readonly HashSet<string> DangerousSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "data", "javascript", "vbscript" };

    private static readonly HashSet<string> DangerousExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".scr", ".bat", ".cmd", ".com",
            ".pif", ".vbs", ".js", ".jar", ".msi", ".ps1"
        };

    private readonly AppDbContext _db;

    public AbuseDetectionService(AppDbContext db) => _db = db;

    public async Task<bool> IsSlugBlockedAsync(string slug, CancellationToken ct = default)
    {
        // Each BlockedSlug.Pattern is a SQL LIKE pattern (e.g. "admin%", "login").
        return await _db.BlockedSlugs
            .AnyAsync(b => EF.Functions.Like(slug, b.Pattern), ct);
    }

    public bool IsUrlSafe(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (DangerousSchemes.Contains(uri.Scheme))
            return false;

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrEmpty(extension) && DangerousExtensions.Contains(extension))
            return false;

        return true;
    }
}
