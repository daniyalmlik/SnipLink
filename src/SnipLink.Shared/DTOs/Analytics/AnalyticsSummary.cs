namespace SnipLink.Shared.DTOs.Analytics;

public sealed class AnalyticsSummary
{
    public long TotalClicks { get; init; }
    public IReadOnlyList<TimeSeriesPoint> ClicksOverTime { get; init; } = [];
    public IReadOnlyList<ReferrerBreakdown> TopReferrers { get; init; } = [];
    public IReadOnlyList<DeviceBreakdown> DeviceBreakdown { get; init; } = [];
    public IReadOnlyList<CountryBreakdown> TopCountries { get; init; } = [];
}
