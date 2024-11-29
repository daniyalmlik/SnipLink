using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SnipLink.Api.Services;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly ILinkService _links;
    private readonly IQrCodeService _qr;

    public AnalyticsController(
        IAnalyticsService analytics,
        ILinkService links,
        IQrCodeService qr)
    {
        _analytics = analytics;
        _links = links;
        _qr = qr;
    }

    // ── Link analytics ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns click analytics for a single link over the last N days.
    /// Ownership is enforced — returns 404 for links the caller does not own.
    /// </summary>
    [HttpGet("links/{id:guid}/analytics")]
    [EnableRateLimiting("Analytics")]
    public async Task<IActionResult> GetAnalytics(
        [FromRoute] Guid id,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 365);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var summary = await _analytics.GetSummaryAsync(id, userId, days, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    // ── QR code ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a QR code PNG (base64) for the short URL of a link the caller owns.
    /// </summary>
    [HttpGet("links/{id:guid}/qr")]
    public async Task<IActionResult> GetQrCode(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var baseUrl = GetBaseUrl();

        var link = await _links.GetByIdAsync(id, userId, baseUrl, ct);
        if (link is null) return NotFound();

        var png = _qr.Generate(link.ShortUrl);

        return Ok(new QrCodeResponse
        {
            Slug      = link.Slug,
            ShortUrl  = link.ShortUrl,
            PngBase64 = png
        });
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    /// <summary>Aggregated metrics across all of the caller's links.</summary>
    [HttpGet("dashboard")]
    [EnableRateLimiting("Analytics")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var summary = await _analytics.GetDashboardAsync(userId, GetBaseUrl(), ct);
        return Ok(summary);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private string GetBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
