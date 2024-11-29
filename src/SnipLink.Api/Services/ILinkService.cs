using SnipLink.Api.Domain;
using SnipLink.Shared.Common;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Services;

public interface ILinkService
{
    /// <summary>
    /// Creates a short link for the given user.
    /// Returns Success, Conflict (slug taken), or Invalid (bad input / abuse).
    /// </summary>
    Task<ServiceResult<LinkResponse>> CreateAsync(
        CreateLinkRequest request,
        string userId,
        string baseUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches the raw entity by slug for redirect. Uses AsNoTracking.
    /// Returns null when not found.
    /// </summary>
    Task<ShortLink?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Ownership-scoped single-link lookup.</summary>
    Task<LinkResponse?> GetByIdAsync(Guid id, string userId, string baseUrl, CancellationToken ct = default);

    /// <summary>Paginated, searchable list scoped to the owner.</summary>
    Task<LinkListResponse> ListAsync(
        string userId,
        int page,
        int pageSize,
        string? search,
        string baseUrl,
        CancellationToken ct = default);

    /// <summary>Ownership-scoped delete. Returns false when not found.</summary>
    Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default);

    /// <summary>Flips IsActive. Returns updated response or null when not found.</summary>
    Task<LinkResponse?> ToggleActiveAsync(Guid id, string userId, string baseUrl, CancellationToken ct = default);
}
