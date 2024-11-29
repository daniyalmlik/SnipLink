using SnipLink.Shared.DTOs;

namespace SnipLink.Shared.DTOs.Analytics;

public sealed class DashboardSummary
{
    public int TotalLinks { get; init; }
    public long TotalClicks { get; init; }
    public long ClicksToday { get; init; }

    /// <summary>Top 5 links ordered by lifetime click count.</summary>
    public IReadOnlyList<LinkResponse> TopLinks { get; init; } = [];
}
