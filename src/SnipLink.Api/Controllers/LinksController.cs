using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SnipLink.Api.Services;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Controllers;

[ApiController]
public sealed class LinksController : ControllerBase
{
    private readonly ILinkService _links;
    private readonly IAnalyticsService _analytics;

    public LinksController(ILinkService links, IAnalyticsService analytics)
    {
        _links = links;
        _analytics = analytics;
    }

    // ── Redirect ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Public redirect endpoint. Returns 301 on success, 404 if the slug
    /// doesn't exist, and 410 if the link has expired or been deactivated.
    /// Click tracking is fire-and-forget — it does not block the response.
    /// </summary>
    [HttpGet("/{slug}")]
    [EnableRateLimiting("Redirect")]
    public async Task<IActionResult> Redirect(
        [FromRoute] string slug,
        CancellationToken ct)
    {
        var link = await _links.GetBySlugAsync(slug, ct);

        if (link is null)
            return NotFound();

        if (!link.IsActive || (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow))
            return StatusCode(StatusCodes.Status410Gone);

        // Fire-and-forget: synchronous enqueue, never throws, does not delay the redirect.
        _analytics.TrackClick(link.Id, HttpContext);

        return RedirectPermanent(link.OriginalUrl);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpPost("api/links")]
    [Authorize]
    [EnableRateLimiting("CreateLink")]
    public async Task<IActionResult> Create(
        [FromBody] CreateLinkRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var baseUrl = GetBaseUrl();

        var result = await _links.CreateAsync(request, userId, baseUrl, ct);

        return result.Match<IActionResult>(
            onSuccess: response =>
                CreatedAtAction(nameof(GetById), new { id = response.Id }, response),
            onConflict: msg =>
                Conflict(new { error = msg }),
            onInvalid: msg =>
                BadRequest(new { error = msg })
        );
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [HttpGet("api/links")]
    [Authorize]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _links.ListAsync(userId, page, pageSize, search, GetBaseUrl(), ct);
        return Ok(result);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    [HttpGet("api/links/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var response = await _links.GetByIdAsync(id, userId, GetBaseUrl(), ct);
        return response is null ? NotFound() : Ok(response);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpDelete("api/links/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var deleted = await _links.DeleteAsync(id, userId, ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Toggle active ─────────────────────────────────────────────────────────

    [HttpPatch("api/links/{id:guid}/toggle")]
    [Authorize]
    public async Task<IActionResult> Toggle(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var response = await _links.ToggleActiveAsync(id, userId, GetBaseUrl(), ct);
        return response is null ? NotFound() : Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetBaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
