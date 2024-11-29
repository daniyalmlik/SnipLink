using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;
using SnipLink.Shared.DTOs;
using SnipLink.Shared.DTOs.Analytics;
using SnipLink.Shared.Enums;

namespace SnipLink.Api.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IClickTrackingQueue _queue;
    private readonly AppDbContext _db;
    private readonly string _salt;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IClickTrackingQueue queue,
        AppDbContext db,
        IConfiguration config,
        ILogger<AnalyticsService> logger)
    {
        _queue = queue;
        _db = db;
        _logger = logger;
        // Salt prevents rainbow-table attacks on hashed IPs.
        // In production this should be a secret stored in Key Vault / secrets.json.
        _salt = config["Analytics:IpHashSalt"] ?? "default-dev-salt-change-in-prod";
    }

    // ── Track ─────────────────────────────────────────────────────────────────

    public void TrackClick(Guid linkId, HttpContext context)
    {
        // Synchronous extraction — HttpContext is only valid during the request.
        // Enqueue is non-blocking; the ClickTrackingWorker handles DB persistence.
        try
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var ua = context.Request.Headers.UserAgent.FirstOrDefault();
            var referrer = context.Request.Headers.Referer.FirstOrDefault();

            _queue.Enqueue(new ClickTrackingData
            {
                ShortLinkId = linkId,
                IpHash      = HashIp(ip),
                UserAgent   = ua,
                Referrer    = referrer,
                DeviceType  = DetectDevice(ua)
            });
        }
        catch (Exception ex)
        {
            // Never let tracking errors surface to the caller.
            _logger.LogError(ex, "Failed to enqueue click for link {LinkId}", linkId);
        }
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    public async Task<AnalyticsSummary?> GetSummaryAsync(
        Guid linkId, string userId, int days, CancellationToken ct = default)
    {
        // Ownership guard
        var owned = await _db.ShortLinks
            .AsNoTracking()
            .AnyAsync(s => s.Id == linkId && s.OwnerId == userId, ct);
        if (!owned) return null;

        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        // ── 1. Total clicks ────────────────────────────────────────────────────
        var totalClicks = await _db.ClickEvents
            .Where(c => c.ShortLinkId == linkId && c.ClickedAt >= since)
            .LongCountAsync(ct);

        // ── 2. Clicks over time (SQL-level group-by) ──────────────────────────
        // The composite index IX_ClickEvents_ShortLinkId_ClickedAt makes this fast.
        var rawTimeSeries = await _db.ClickEvents
            .AsNoTracking()
            .Where(c => c.ShortLinkId == linkId && c.ClickedAt >= since)
            .GroupBy(c => c.ClickedAt.Date)
            .Select(g => new { Date = g.Key, Count = (long)g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        // ── 3. Top referrers — group in SQL, extract hostname in memory ────────
        // Extracting the host component of a URL is not SQL-translatable, so we
        // group by the raw referrer column first, then consolidate by host.
        var rawReferrers = await _db.ClickEvents
            .AsNoTracking()
            .Where(c => c.ShortLinkId == linkId && c.ClickedAt >= since)
            .GroupBy(c => c.Referrer)
            .Select(g => new { Referrer = g.Key, Count = (long)g.Count() })
            .ToListAsync(ct);

        var topReferrers = rawReferrers
            .GroupBy(r => ExtractHost(r.Referrer))
            .Select(g => new ReferrerBreakdown
            {
                Host   = g.Key,
                Clicks = g.Sum(x => x.Count)
            })
            .OrderByDescending(r => r.Clicks)
            .Take(10)
            .ToList();

        // ── 4. Device breakdown ────────────────────────────────────────────────
        var rawDevices = await _db.ClickEvents
            .AsNoTracking()
            .Where(c => c.ShortLinkId == linkId && c.ClickedAt >= since)
            .GroupBy(c => c.DeviceType)
            .Select(g => new { Device = g.Key, Count = (long)g.Count() })
            .ToListAsync(ct);

        var deviceBreakdown = rawDevices
            .Select(d => new DeviceBreakdown
            {
                DeviceType = d.Device.ToString(),
                Clicks     = d.Count,
                Percentage = totalClicks > 0
                    ? Math.Round((double)d.Count / totalClicks * 100, 1)
                    : 0
            })
            .OrderByDescending(d => d.Clicks)
            .ToList();

        // ── 5. Top countries ───────────────────────────────────────────────────
        // Country is populated by GeoIP enrichment (not yet wired — future phase).
        // The query structure is correct; most entries will show "Unknown" until
        // a GeoIP provider is integrated.
        var rawCountries = await _db.ClickEvents
            .AsNoTracking()
            .Where(c => c.ShortLinkId == linkId && c.ClickedAt >= since)
            .GroupBy(c => c.Country)
            .Select(g => new { Country = g.Key, Count = (long)g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        return new AnalyticsSummary
        {
            TotalClicks    = totalClicks,
            ClicksOverTime = rawTimeSeries
                .Select(x => new TimeSeriesPoint
                {
                    Date   = DateOnly.FromDateTime(x.Date),
                    Clicks = x.Count
                })
                .ToList(),
            TopReferrers   = topReferrers,
            DeviceBreakdown = deviceBreakdown,
            TopCountries   = rawCountries
                .Select(x => new CountryBreakdown
                {
                    Country = x.Country ?? "Unknown",
                    Clicks  = x.Count
                })
                .ToList()
        };
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<DashboardSummary> GetDashboardAsync(
        string userId, string baseUrl, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var totalLinks = await _db.ShortLinks
            .CountAsync(s => s.OwnerId == userId, ct);

        var totalClicks = await _db.ShortLinks
            .Where(s => s.OwnerId == userId)
            .SumAsync(s => (long?)s.ClickCount ?? 0L, ct);

        // Traverses the ShortLink navigation property — EF Core translates this
        // to a JOIN, kept fast by the IX_ShortLinks_OwnerId index.
        var clicksToday = await _db.ClickEvents
            .Where(c => c.ShortLink.OwnerId == userId && c.ClickedAt >= today)
            .LongCountAsync(ct);

        var topLinks = await _db.ShortLinks
            .AsNoTracking()
            .Where(s => s.OwnerId == userId)
            .OrderByDescending(s => s.ClickCount)
            .Take(5)
            .ToListAsync(ct);

        return new DashboardSummary
        {
            TotalLinks   = totalLinks,
            TotalClicks  = totalClicks,
            ClicksToday  = clicksToday,
            TopLinks     = topLinks.ConvertAll(l => MapToResponse(l, baseUrl))
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string HashIp(string? ip)
    {
        if (ip is null) return string.Empty;
        var input = Encoding.UTF8.GetBytes($"{_salt}:{ip}");
        var hash = SHA256.HashData(input);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static DeviceType DetectDevice(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return DeviceType.Unknown;
        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("tablet", StringComparison.Ordinal) ||
            ua.Contains("ipad", StringComparison.Ordinal))
            return DeviceType.Tablet;
        if (ua.Contains("mobile", StringComparison.Ordinal) ||
            ua.Contains("android", StringComparison.Ordinal) ||
            ua.Contains("iphone", StringComparison.Ordinal))
            return DeviceType.Mobile;
        return DeviceType.Desktop;
    }

    private static string ExtractHost(string? referrer)
    {
        if (string.IsNullOrWhiteSpace(referrer)) return "direct";
        if (Uri.TryCreate(referrer, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();
        return "other";
    }

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
