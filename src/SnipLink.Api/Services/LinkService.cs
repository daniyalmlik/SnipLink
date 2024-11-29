using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;
using SnipLink.Shared.Common;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Services;

public sealed class LinkService : ILinkService
{
    private const int MaxSlugRetries = 5;

    private readonly AppDbContext _db;
    private readonly ISlugGenerator _slugGen;
    private readonly IAbuseDetectionService _abuse;

    public LinkService(AppDbContext db, ISlugGenerator slugGen, IAbuseDetectionService abuse)
    {
        _db = db;
        _slugGen = slugGen;
        _abuse = abuse;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<LinkResponse>> CreateAsync(
        CreateLinkRequest request,
        string userId,
        string baseUrl,
        CancellationToken ct = default)
    {
        // 1. URL must be absolute http/https
        if (!Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ServiceResult<LinkResponse>.Invalid(
                "URL must be an absolute http or https address.");
        }

        // 2. Abuse check on URL
        if (!_abuse.IsUrlSafe(request.OriginalUrl))
            return new ServiceResult<LinkResponse>.Invalid("URL scheme or file type is not allowed.");

        // 3. Resolve slug (custom or random)
        string slug;

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();

            if (!_slugGen.IsValid(slug))
                return new ServiceResult<LinkResponse>.Invalid(
                    "Slug must be 3–50 chars, lowercase alphanumeric and hyphens, " +
                    "and cannot start or end with a hyphen.");

            if (await _abuse.IsSlugBlockedAsync(slug, ct))
                return new ServiceResult<LinkResponse>.Invalid("This slug is reserved and cannot be used.");

            if (await _db.ShortLinks.AnyAsync(s => s.Slug == slug, ct))
                return new ServiceResult<LinkResponse>.Conflict($"Slug '{slug}' is already taken.");
        }
        else
        {
            // Retry loop handles the astronomically unlikely collision case
            slug = null!;
            for (int attempt = 0; attempt < MaxSlugRetries; attempt++)
            {
                var candidate = _slugGen.Generate();
                if (!await _db.ShortLinks.AnyAsync(s => s.Slug == candidate, ct))
                {
                    slug = candidate;
                    break;
                }
            }

            if (slug is null)
                return new ServiceResult<LinkResponse>.Conflict(
                    "Could not generate a unique slug after several attempts. Please try again.");
        }

        // 4. Persist
        var link = new ShortLink
        {
            Slug        = slug,
            OriginalUrl = request.OriginalUrl,
            Title       = request.Title,
            ExpiresAt   = request.ExpiresAt,
            OwnerId     = userId
        };

        _db.ShortLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        return new ServiceResult<LinkResponse>.Success(MapToResponse(link, baseUrl));
    }

    // ── Redirect lookup ───────────────────────────────────────────────────────

    public Task<ShortLink?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _db.ShortLinks
           .AsNoTracking()
           .FirstOrDefaultAsync(s => s.Slug == slug, ct);

    // ── Single-link (owner-scoped) ────────────────────────────────────────────

    public async Task<LinkResponse?> GetByIdAsync(
        Guid id, string userId, string baseUrl, CancellationToken ct = default)
    {
        var link = await _db.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerId == userId, ct);

        return link is null ? null : MapToResponse(link, baseUrl);
    }

    // ── Paginated list ────────────────────────────────────────────────────────

    public async Task<LinkListResponse> ListAsync(
        string userId,
        int page,
        int pageSize,
        string? search,
        string baseUrl,
        CancellationToken ct = default)
    {
        var query = _db.ShortLinks
            .AsNoTracking()
            .Where(s => s.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.Slug.Contains(term) ||
                s.OriginalUrl.Contains(term) ||
                (s.Title != null && s.Title.Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new LinkListResponse
        {
            Items      = items.ConvertAll(l => MapToResponse(l, baseUrl)),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var deleted = await _db.ShortLinks
            .Where(s => s.Id == id && s.OwnerId == userId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }

    // ── Toggle active ─────────────────────────────────────────────────────────

    public async Task<LinkResponse?> ToggleActiveAsync(
        Guid id, string userId, string baseUrl, CancellationToken ct = default)
    {
        var link = await _db.ShortLinks
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerId == userId, ct);

        if (link is null) return null;

        link.IsActive = !link.IsActive;
        await _db.SaveChangesAsync(ct);

        return MapToResponse(link, baseUrl);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static LinkResponse MapToResponse(ShortLink link, string baseUrl) => new()
    {
        Id          = link.Id,
        Slug        = link.Slug,
        OriginalUrl = link.OriginalUrl,
        Title       = link.Title,
        ShortUrl    = $"{baseUrl.TrimEnd('/')}/{link.Slug}",
        IsActive    = link.IsActive,
        CreatedAt   = link.CreatedAt,
        ExpiresAt   = link.ExpiresAt,
        ClickCount  = link.ClickCount
    };
}
