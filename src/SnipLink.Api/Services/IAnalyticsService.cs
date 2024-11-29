using SnipLink.Shared.DTOs.Analytics;

namespace SnipLink.Api.Services;

public interface IAnalyticsService
{
    /// <summary>
    /// Extracts click metadata from the current HTTP context, enqueues for persistence,
    /// and swallows all exceptions so redirect latency is never affected.
    /// </summary>
    void TrackClick(Guid linkId, HttpContext context);

    /// <summary>
    /// Returns analytics for a link owned by the given user over the last
    /// <paramref name="days"/> days. Returns null when the link is not found
    /// or not owned by the user.
    /// </summary>
    Task<AnalyticsSummary?> GetSummaryAsync(
        Guid linkId, string userId, int days, CancellationToken ct = default);

    /// <summary>Aggregated dashboard metrics for all links belonging to the user.</summary>
    Task<DashboardSummary> GetDashboardAsync(
        string userId, string baseUrl, CancellationToken ct = default);
}
