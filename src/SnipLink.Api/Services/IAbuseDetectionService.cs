namespace SnipLink.Api.Services;

public interface IAbuseDetectionService
{
    /// <summary>
    /// Checks the slug against BlockedSlug patterns (LIKE matching).
    /// Returns true when the slug should be rejected.
    /// </summary>
    Task<bool> IsSlugBlockedAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Returns false for data:, javascript:, vbscript: schemes and dangerous
    /// executable file extensions.
    /// </summary>
    bool IsUrlSafe(string url);
}
